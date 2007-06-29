using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;

using System.Threading;

using Mono.Unix;

namespace Tomboy
{
	public enum SyncState {
		/// <summary>
		/// The synchronization thread is not running
		/// </summary>
		Idle,
		
		/// <summary>
		/// Indicates that no sync service has been configured
		/// </summary>
		NoConfiguredSyncService,
		
		/// <summary>
		/// Indicates that SyncServiceAddin.CreateSyncServer () failed
		/// </summary>
		SyncServerCreationFailed,
		
		/// <summary>
		/// Connecting to the server
		/// </summary>
		Connecting,
		
		/// <summary>
		/// Acquiring the right to be the exclusive sync client
		/// </summary>
		AcquiringLock,

		/// <summary>
		/// Another client is currently synchronizing
		/// </summary>
		Locked,
		
		/// <summary>
		/// Preparing to download new/updated notes from the server.  This also
		/// includes checking for note title name conflicts.
		/// </summary>
		PrepareDownload,

		/// <summary>
		/// Downloading notes from the server
		/// </summary>
		Downloading,
		
		/// <summary>
		/// Checking for files to send to the server
		/// </summary>
		PrepareUpload,

		/// <summary>
		/// Uploading new/changed notes from the client
		/// </summary>
		Uploading,
		
		/// <summary>
		/// Deleting notes from the server
		/// </summary>
		DeleteServerNotes,
		
		/// <summary>
		/// Committing Changes to the server
		/// </summary>
		CommittingChanges,
		
		/// <summary>
		/// SyncSuccess
		/// </summary>
		Succeeded,
		
		/// <summary>
		/// The synchronization failed
		/// </summary>
		Failed,
		
		/// <summary>
		/// The synchronization was cancelled by the user
		/// </summary>
		UserCancelled
	};
	
	public enum NoteSyncType {
		UploadNew,
		UploadModified,
		DownloadNew,
		DownloadModified,
		DeleteFromServer,
		DeleteFromClient
	};
	
	/// <summary>
	/// Handle state SyncManager state changes
	/// </summary>
	public delegate void SyncStateChangedHandler (SyncState state);
	
	/// <summary>
	/// Handle when notes are uploaded, downloaded, or deleted
	/// </summary>
	public delegate void NoteSyncHandler (string noteTitle, NoteSyncType type);
	
	/// <summary>
	/// Handle a note conflict
	/// </summary>
	public delegate void NoteConflictHandler (NoteManager manager, Note localConflictNote);
	
	public class SyncManager
	{
		//private static SyncServer server;
		private static SyncClient client;
		private static SyncState state = SyncState.Idle;
		private static Thread syncThread = null;
		// TODO: Expose the next enum more publicly
		private static SyncTitleConflictResolution conflictResolution;
		
		/// <summary>
		/// Emitted when the state of the synchronization changes
		/// </summary>
		public static event SyncStateChangedHandler StateChanged;
		
		/// <summary>
		/// Emmitted when a file is uploaded, downloaded, or deleted.
		/// </summary>
		public static event NoteSyncHandler NoteSynchronized;
		
		/// <summary>
		/// 
		/// </summary>
		public static event NoteConflictHandler NoteConflictDetected;
		
		static SyncManager ()
		{
			client = new TomboySyncClient ();
			//server = new FileSystemSyncServer ();
		}
		
		public static void Initialize ()
		{
			// NOTE: static constructor should get called if this
			// is the first reference to SyncManager
			
			///
			/// Add a "Synchronize Notes" to Tomboy's Tray Icon Menu
			///
			Gtk.ActionGroup action_group = new Gtk.ActionGroup ("Sync");
			action_group.Add (new Gtk.ActionEntry [] {
				new Gtk.ActionEntry ("ToolsMenuAction", null,
					Catalog.GetString ("_Tools"), null, null, null),
				new Gtk.ActionEntry ("SyncNotesAction", null,
					Catalog.GetString ("Synchronize Notes"), null, null,
					delegate { SyncManager.OpenNoteSyncWindow (); })
			});
			
			Tomboy.ActionManager.UI.AddUiFromString (@"
				<ui>
				    <menubar name='MainWindowMenubar'>
				    	<placeholder name='MainWindowMenuPlaceholder'>
					    	<menu name='ToolsMenu' action='ToolsMenuAction'>
					    		<menuitem name='SyncNotes' action='SyncNotesAction' />
					    	</menu>
					    </placeholder>
				    </menubar>
				</ui>
			");
			
			Tomboy.ActionManager.UI.InsertActionGroup (action_group, 0);
			
			// Initialize all the SyncServiceAddins
			SyncServiceAddin [] addins = Tomboy.DefaultNoteManager.AddinManager.GetSyncServiceAddins ();
			foreach (SyncServiceAddin addin in addins) {
				try {
					addin.Initialize ();
				} catch (Exception e) {
					Logger.Debug ("Error calling {0}.Initialize (): {1}\n{2}",
						addin.Id, e.Message, e.StackTrace);
					
					// TODO: Call something like AddinManager.Disable (addin)
				}
			}
		}
		
		// TODO: Move?
		public static void OpenNoteSyncWindow ()
		{
			if (sync_dlg == null) {
				sync_dlg = new SyncDialog ();
				sync_dlg.Response += OnSyncDialogResponse;
			}
			sync_dlg.Present ();
		}

		static SyncDialog sync_dlg;
		
		static void OnSyncDialogResponse (object sender, Gtk.ResponseArgs args)
		{
			((Gtk.Widget) sender).Destroy ();
			sync_dlg = null;
		}
		
		public static void PerformSynchronization ()
		{
			if (syncThread != null) {
				// A synchronization thread is already running
				sync_dlg.Present ();
				return;
			}
			
			syncThread = new Thread (new ThreadStart (SynchronizationThread));
			syncThread.IsBackground = true;
			syncThread.Start ();
		}
		
		/// <summary>
		/// The function that does all of the work
		/// </summary>
		public static void SynchronizationThread ()
		{
			SyncServiceAddin addin = GetConfiguredSyncService ();
			if (addin == null) {
				SetState (SyncState.NoConfiguredSyncService);
				Logger.Debug ("GetConfiguredSyncService is null");
				SetState (SyncState.Idle);
				syncThread = null;
				return;
			}
			
			Logger.Debug ("SyncThread using SyncServiceAddin: {0}", addin.Name); 
			
			SyncServer server;
			SetState (SyncState.Connecting);
			try {
				server = addin.CreateSyncServer ();
				if (server == null)
					throw new Exception ("addin.CreateSyncServer () returned null");
			} catch (Exception e) {
				SetState (SyncState.SyncServerCreationFailed);
				Logger.Log ("Exception while creating SyncServer: {0}\n{1}", e.Message, e.StackTrace);
				SetState (SyncState.Idle);
				syncThread = null;
				return;
				// TODO: Figure out a clever way to get the specific error up to the GUI
			}
			
			SetState (SyncState.AcquiringLock);
			// TODO: We should really throw exceptions from BeginSyncTransaction ()
			if (!server.BeginSyncTransaction ()) {
				SetState (SyncState.Locked);
				Logger.Log ("PerformSynchronization: Server locked, try again later");
				syncThread = null;
				return;
			}
Logger.Debug ("8");
			int latestServerRevision = server.LatestRevision;
			int newRevision = latestServerRevision + 1;
			
			SetState (SyncState.PrepareDownload);
//			syncDialog.ProgressText = "Getting note updates...";
//			syncDialog.ProgressFraction = 0.1;
			
			// Handle notes modified or added on server
			Logger.Debug ("Sync: GetNoteUpdatesSince rev " + client.LastSynchronizedRevision.ToString ());
			IDictionary<string, NoteUpdate> noteUpdates =
				server.GetNoteUpdatesSince (client.LastSynchronizedRevision);
			Logger.Debug ("Sync: " + noteUpdates.Count + " updates since rev " + client.LastSynchronizedRevision.ToString ());
			// TODO: Before actually doing updates, do this:
			//      1. Loop through NoteUpdates, look for instances where
			//         there is an existing note with the same title but
			//         different UUID.
			//      2. For each note like this, prompt the user to do either:
			//              a. Rename existing note (should this work like
			//                 a normal rename and update links, or no?
			//                 No seems like a good choice if you're getting
			//                 a new note with that name)
			//              b. Delete existing note (server copy replaces)
			//              c. Delete server note? (how would this work?)
			//              d. Cancel sync (remember, no updates have happened yet)
			//      This way, it should be impossible to upload two notes
			//      with the same name.  Still not sure how irritating this
			//      might be for NotD users.  And will the Start Note be
			//      OK with this?  (apparently it's OK to delete 
			
			// TODO: Lots of searching here and in the next foreach...
			//       Want this stuff to happen all at once first, but
			//       maybe there's a way to store this info and pass it on?
			foreach (NoteUpdate noteUpdate in noteUpdates.Values)
			{
				if (FindNoteByUUID (noteUpdate.UUID) == null) {
					Note existingNote = NoteMgr.Find (noteUpdate.Title);
					if (existingNote != null) {
						if (NoteConflictDetected != null) {
							NoteConflictDetected (NoteMgr, existingNote);
							
							// Suspend this thread while the GUI is presented to
							// the user.
							syncThread.Suspend ();
							
							// The user has responded to the conflict.  Read what
							// they've said to do.
							if (conflictResolution == SyncTitleConflictResolution.Cancel) {
								if (server.CancelSyncTransaction ()) {
									SetState (SyncState.UserCancelled);
									SetState (SyncState.Idle);
									syncThread = null;
									return;
								}
							}
						}
					}
				}
			}
			
			double updateProgressInterval = 0.4 / (double)noteUpdates.Count;
			
			if (noteUpdates.Count > 0)
				SetState (SyncState.Downloading);

			// The following loop may need to update GUIs in the main thread
			// TODO: Extract a method here
			AutoResetEvent evt = new AutoResetEvent (false);
			Gtk.Application.Invoke (delegate {

			foreach (NoteUpdate note in noteUpdates.Values) {
//				syncDialog.ProgressFraction += updateProgressInterval;
				Note existingNote = FindNoteByUUID (note.UUID);

				if (existingNote == null) {// TODO: not so simple...what if I deleted the note?
					
					existingNote = NoteMgr.CreateWithGuid (note.Title, note.UUID);
					existingNote.LoadForeignNoteXml (note.XmlContent);
					client.SetRevision (existingNote, note.LatestRevision);
					
					if (NoteSynchronized != null)
						NoteSynchronized (existingNote.Title, NoteSyncType.DownloadNew);
//					syncDialog.AddUpdateItem (existingNote.Title, Catalog.GetString ("Downloading new note"));
				} else if (existingNote.ChangeDate.CompareTo (client.LastSyncDate) <= 0) {
					existingNote.LoadForeignNoteXml (note.XmlContent);
					client.SetRevision (existingNote, note.LatestRevision);
					if (NoteSynchronized != null)
						NoteSynchronized (existingNote.Title, NoteSyncType.DownloadModified);
//					syncDialog.AddUpdateItem (existingNote.Title, Catalog.GetString ("Downloading update"));
				} else {
					// TODO: handle conflicts (may include notes modified on one client, deleted on another)
				}
			}
				// Make list of all local notes
				List<Note> localNotes = new List<Note> ();
				foreach (Note note in NoteMgr.Notes)
					localNotes.Add (note);
				
				// Get all notes currently on server
				IList<string> serverNotes = server.GetAllNoteUUIDs ();
				
				foreach (Note note in localNotes) {
					if (client.GetRevision (note) != -1 &&
					    !serverNotes.Contains (note.Id)) {

						if (NoteSynchronized != null)
							NoteSynchronized (note.Title, NoteSyncType.DeleteFromClient);
//					syncDialog.AddUpdateItem (existingNote.Title, Catalog.GetString ("Deleting note on client"));
						NoteMgr.Delete (note);
					}
				}
			evt.Set ();
			});
			
			evt.WaitOne ();
			
			// TODO: Add following updates to syncDialog treeview
//			syncDialog.ProgressText = "Sending note updates...";
//			syncDialog.ProgressFraction = 0.5;
			
			SetState (SyncState.PrepareUpload);
			// Look through all the notes modified on the client
			// and upload new or modified ones to the server
			List<Note> newOrModifiedNotes = new List<Note> ();
Logger.Debug ("SYNC: client.LastSyncDate = {0}", client.LastSyncDate.ToString ());
			foreach (Note note in NoteMgr.Notes) {
if (note.Title.CompareTo ("Start Here") == 0) {
	Logger.Debug ("SYNC: Start Here Revision = {0}", client.GetRevision (note));
	Logger.Debug ("SYNC: Start Here Change Date = {0}", note.ChangeDate.ToString ());
}
				if (client.GetRevision (note) == -1) {
					// This is a new note that has never been synchronized to the server
					newOrModifiedNotes.Add (note);
					if (NoteSynchronized != null)
						NoteSynchronized (note.Title, NoteSyncType.UploadNew);
//					syncDialog.AddUpdateItem (note.Title, Catalog.GetString ("Adding new file to server"));
				} else if (client.GetRevision (note) <= client.LastSynchronizedRevision &&
				    	note.ChangeDate > client.LastSyncDate) {
					newOrModifiedNotes.Add (note);
					if (NoteSynchronized != null)
						NoteSynchronized (note.Title, NoteSyncType.UploadModified);
//					syncDialog.AddUpdateItem (note.Title, Catalog.GetString ("Uploading changes"));
				}
			}
			// Apply this revision number to all new/modified notes since last sync
			// TODO: Should revision info be stored in note, or a seperate file?
			// TODO: This may actually cause future problems if our network connection
			// dies after changing the revision but never really gets uploaded to the
			// server.
			foreach (Note note in newOrModifiedNotes) {
				client.SetRevision (note, newRevision);
				note.Save ();
			}
			Logger.Debug ("Sync: Uploading " + newOrModifiedNotes.Count.ToString () + " note updates");
			SetState (SyncState.Uploading);
			server.UploadNotes (newOrModifiedNotes);
			
			// Handle notes deleted on client
			List<string> locallyDeletedUUIDs = new List<string> ();			
			foreach (string noteUUID in server.GetAllNoteUUIDs ()) {
				if (FindNoteByUUID (noteUUID) == null) {
					locallyDeletedUUIDs.Add (noteUUID);
					if (NoteSynchronized != null) {
						string deletedTitle = noteUUID;
						if (client.DeletedNoteTitles.ContainsKey (noteUUID))
							deletedTitle = client.DeletedNoteTitles [noteUUID];
						NoteSynchronized (deletedTitle, NoteSyncType.DeleteFromServer);
					}
//					syncDialog.AddUpdateItem (noteUUID, Catalog.GetString ("Deleting from server"));
				}
			}
			if (locallyDeletedUUIDs.Count > 0) {
				SetState (SyncState.DeleteServerNotes);
				server.DeleteNotes (locallyDeletedUUIDs);
			}
			
			SetState (SyncState.CommittingChanges);
			bool commitResult = server.CommitSyncTransaction ();
			if (commitResult) {
//				syncDialog.ProgressFraction = 1.0;
//				syncDialog.ProgressText = "Sync completed successfully!";
				SetState (SyncState.Succeeded);
			} else {
//				syncDialog.ProgressFraction = 0;
//				syncDialog.ProgressText = "Sync failed!";
				SetState (SyncState.Failed);
				// TODO: Figure out a way to let the GUI know what exactly failed
			}
//			syncDialog.CloseSensitive = true;
			
			// This should be equivalent to newRevision
			client.LastSynchronizedRevision = server.LatestRevision;
			
			client.LastSyncDate = DateTime.Now;		
			
			Logger.Debug ("Sync: New revision: {0}", client.LastSynchronizedRevision);
			
			SetState (SyncState.Idle);
			syncThread = null;
		}
		
		/// <summary>
		/// The GUI should call this after having the user resolve a conflict
		/// so the synchronization thread can continue.
		/// </summary>
		public static void ResolveConflict (Note conflictNote,
				SyncTitleConflictResolution resolution)
		{
			if (syncThread != null) {
				conflictResolution = resolution;
				syncThread.Resume ();
			}
		}
		
		private static Note FindNoteByUUID (string uuid)
		{
			return NoteMgr.FindByUri ("note://tomboy/" + uuid);
		}
		
		private static NoteManager NoteMgr
		{
			get { return Tomboy.DefaultNoteManager; }
		}
		
//		private static void OnSyncDialogResponse (object sender, Gtk.ResponseArgs args)
//		{
//			SyncDialog dialog = sender as SyncDialog;
//			dialog.Hide ();
//			dialog.Destroy ();
//		}
		
		#region Public Properties
		/// <summary>
		/// The state of the SyncManager (lame comment, duh!)
		/// </summary>
		public SyncState State
		{
			get { return state; }
		}
		#endregion // Public Properties
		
		#region Private Methods
		private static void SetState (SyncState newState)
		{
			state = newState;
			if (StateChanged != null) {
				// Notify the event handlers
				try {
					StateChanged (state);
				} catch {}
			}
		}
		
		/// <summary>
		/// Read the preferences and load the specified SyncServiceAddin to
		/// perform synchronization.
		/// </summary>
		private static SyncServiceAddin GetConfiguredSyncService ()
		{
			SyncServiceAddin addin = null;
			
			string syncServiceId =
				Preferences.Get (Preferences.SYNC_SELECTED_SERVICE_ADDIN) as String;
			if (syncServiceId != null)
				addin = GetSyncServiceAddin (syncServiceId);
			
			return addin;
		}
		
		/// <summary>
		/// Return the specified SyncServiceAddin
		/// </summary>
		private static SyncServiceAddin GetSyncServiceAddin (string syncServiceId)
		{
			SyncServiceAddin anAddin = null;
			
			SyncServiceAddin [] addins = Tomboy.DefaultNoteManager.AddinManager.GetSyncServiceAddins ();
			foreach (SyncServiceAddin addin in addins) {
				if (addin.Id.CompareTo (syncServiceId) == 0) {
					anAddin = addin;
					break;
				}
			}
			
			return anAddin;
		}
		#endregion // Private Methods
	}
	
	public class NoteUpdate
	{
		public string XmlContent;//string.Empty if deleted?
		public string Title;
		public string UUID; //needed?
		public int LatestRevision;
		
		public NoteUpdate (string xmlContent, string title, string uuid, int latestRevision)
		{
			XmlContent = xmlContent;
			Title = title;
			UUID = uuid;
			LatestRevision = latestRevision;
			
			// TODO: Clean this up (and remove title parameter?)
			if (xmlContent != null && xmlContent.Length > 0) {
				XmlTextReader xml = new XmlTextReader (new StringReader (XmlContent));
				xml.Namespaces = false;

				while (xml.Read ()) {
					switch (xml.NodeType) {
					case XmlNodeType.Element:
						switch (xml.Name) {
					case "title":
						Title = xml.ReadString ();
						break;
					}
						break;
					}
				}
			}
		}
	}
	
	public class SyncLockInfo
	{
		/// <summary>
		/// A string to identify which client currently has the
		/// lock open.  Not guaranteed to be unique.
		/// </summary>
		public string ClientId;
		
		/// <summary>
		/// Unique ID for the sync transaction associated with the lock.
		/// </summary>
		public string TransactionId;
		
		/// <summary>
		/// Indicates how many times the client has renewed the lock.
		/// Subsequent clients should watch this (along with the LockOwner) to
		/// determine whether the currently synchronizing client has becomeeither
		/// inactive.  Clients currently synchronizing should update the lock
		/// file before the duration expires to prevent other clients from
		/// overtaking the lock.
		/// </summary>
		public int RenewCount;
		
		/// <summary>
		/// A TimeSpan to indicate how long the current synchronization will
		/// take.  If the current synchronization will take longer than this,
		/// the client synchronizing should update the lock file to indicate
		/// this.
		/// </summary>
		public TimeSpan Duration;
		
		/// <summary>
		/// Specifies the current revision that this lock is for.  The client
		/// that lays the lock file down should specify which revision they're
		/// creating.  Clients needing to perform cleanup may want to know which
		/// revision files to clean up by reading the value of this.
		/// </summary>
		public int Revision;
		
		public SyncLockInfo ()
		{
			ClientId = Preferences.Get (Preferences.SYNC_CLIENT_ID) as string;
			TransactionId = System.Guid.NewGuid ().ToString ();
			RenewCount = 0;
			Duration = new TimeSpan (0, 2, 0); // default of 2 minutes
			Revision = 0;
		}
		
		/// <summary>
		/// The point of this property is to let clients quickly know if a sync
		/// lock has changed.
		/// </summary>
		public string HashString
		{
			get {
				return string.Format ("{0}-{1}-{2}-{3}-{4}",
					TransactionId, ClientId, RenewCount,
					Duration.ToString (), Revision);
			}
		}
	}
	
	public interface SyncServer
	{
		bool BeginSyncTransaction ();
		bool CommitSyncTransaction ();
		bool CancelSyncTransaction ();
		IList<string> GetAllNoteUUIDs ();
		IDictionary<string, NoteUpdate> GetNoteUpdatesSince (int revision);
		void DeleteNotes (IList<string> deletedNoteUUIDs);
		void UploadNotes (IList<Note> notes);
		int LatestRevision { get; }
		SyncLockInfo CurrentSyncLock { get; }
	}
	
	public interface SyncClient
	{
		int LastSynchronizedRevision { get; set; }
		DateTime LastSyncDate { get; set; }
		int GetRevision (Note note);
		void SetRevision (Note note, int revision);
		IDictionary<string, string> DeletedNoteTitles { get; }
	}
}
