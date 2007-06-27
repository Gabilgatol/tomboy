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
		Failed
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
	
	public class SyncManager
	{
		//private static SyncServer server;
		private static SyncClient client;
		private static SyncState state = SyncState.Idle;
		
		private static Thread syncThread = null;
		
		/// <summary>
		/// Emitted when the state of the synchronization changes
		/// </summary>
		public static event SyncStateChangedHandler StateChanged;
		
		/// <summary>
		/// Emmitted when a file is uploaded, downloaded, or deleted.
		/// </summary>
		public static event NoteSyncHandler NoteSynchronized;
		
		static SyncManager ()
		{
			client = new TomboySyncClient ();
			//server = new FileSystemSyncServer ();
		}
		
		public static void PerformSynchronization ()
		{
			if (syncThread != null) {
				// A synchronization thread is already running
				Tomboy.SyncDialog.Present ();
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
			SyncServer server;
			SetState (SyncState.Connecting);
			try {
				server = new FileSystemSyncServer ();
			} catch (Exception e) {
				SetState (SyncState.Failed);
				Logger.Log ("Exception while creating SyncServer: {0}\n{1}", e.Message, e.StackTrace);
				SetState (SyncState.Idle);
				return;
				// TODO: Figure out a clever way to get the specific error up to the GUI
			}
			
//			SyncDialog syncDialog = Tomboy.SyncDialog;
//			syncDialog.ProgressText = "Contacting Server...";
//			syncDialog.Response += OnSyncDialogResponse;
//			syncDialog.Show ();
			
			SetState (SyncState.AcquiringLock);
			// TODO: We should really throw exceptions from BeginSyncTransaction ()
			if (!server.BeginSyncTransaction ()) {
				SetState (SyncState.Locked);
				Logger.Log ("PerformSynchronization: Server locked, try again later");
//				syncDialog.ProgressText = "Sync failed: Server locked, try again later";
//				syncDialog.CloseSensitive = true;
				return;
			}
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
						SyncTitleConflictDialog conflictDlg =
							new SyncTitleConflictDialog (existingNote);
						Gtk.ResponseType reponse = (Gtk.ResponseType) conflictDlg.Run ();
						
						if (reponse == Gtk.ResponseType.Cancel)
							// TODO: Message?
							return;
						switch (conflictDlg.Resolution) {
						case SyncTitleConflictDialog.TitleConflictResolution.DeleteExisting:
							NoteMgr.Delete (existingNote);
							break;
						case SyncTitleConflictDialog.TitleConflictResolution.RenameExistingAndUpdate:
							existingNote.Title = conflictDlg.RenamedTitle;  // TODO: What if note is open?
							break;
						case SyncTitleConflictDialog.TitleConflictResolution.RenameExistingNoUpdate:
							existingNote.Data.Title = conflictDlg.RenamedTitle;     // TODO: Does this work?
							break;
						}
						
						conflictDlg.Hide ();
						conflictDlg.Destroy (); // TODO: Necessary?
					}
				}
			}
			
			double updateProgressInterval = 0.4 / (double)noteUpdates.Count;
			
			if (noteUpdates.Count > 0)
				SetState (SyncState.Downloading);

			foreach (NoteUpdate note in noteUpdates.Values) {
//				syncDialog.ProgressFraction += updateProgressInterval;
				Note existingNote = FindNoteByUUID (note.UUID);
				
				if (existingNote == null) {// TODO: not so simple...what if I deleted the note?
					if (note.XmlContent == string.Empty)
						continue; // Deletion of note that doesn't exist

					existingNote = NoteMgr.CreateWithGuid (note.Title, note.UUID);
					existingNote.LoadForeignNoteXml (note.XmlContent);					
					if (NoteSynchronized != null)
						NoteSynchronized (existingNote.Title, NoteSyncType.DownloadNew);
//					syncDialog.AddUpdateItem (existingNote.Title, Catalog.GetString ("Downloading new note"));
				} else if (note.XmlContent == string.Empty &&
				         existingNote.ChangeDate.CompareTo (client.LastSyncDate) <= 0) {
						if (NoteSynchronized != null)
							NoteSynchronized (existingNote.Title, NoteSyncType.DeleteFromClient);
//					syncDialog.AddUpdateItem (existingNote.Title, Catalog.GetString ("Deleting note on client"));
					NoteMgr.Delete (existingNote);
				} else if (existingNote.ChangeDate.CompareTo (client.LastSyncDate) <= 0) {
					existingNote.LoadForeignNoteXml (note.XmlContent);// TODO: live update of note XML
					if (NoteSynchronized != null)
						NoteSynchronized (existingNote.Title, NoteSyncType.DownloadModified);
//					syncDialog.AddUpdateItem (existingNote.Title, Catalog.GetString ("Downloading update"));
				} else {
					// TODO: handle conflicts
				}
			}
			
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
	Logger.Debug ("SYNC: Start Here Revision = {0}", note.Revision);
	Logger.Debug ("SYNC: Start Here Change Date = {0}", note.ChangeDate.ToString ());
}
				if (note.Revision == -1) {
					// This is a new note that has never been synchronized to the server
					newOrModifiedNotes.Add (note);
					if (NoteSynchronized != null)
						NoteSynchronized (note.Title, NoteSyncType.UploadNew);
//					syncDialog.AddUpdateItem (note.Title, Catalog.GetString ("Adding new file to server"));
				} else if (note.Revision <= client.LastSynchronizedRevision &&
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
				note.Revision = newRevision;
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
					// TODO: Cough, ugh.  Can we get the title at this point?
					if (NoteSynchronized != null)
						NoteSynchronized ("FIXME: Get the deleted note's title!", NoteSyncType.DeleteFromServer);
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
		/// determine whether the currently synchronizing client has become
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
	}
}
