using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;

namespace Tomboy
{
	public class SyncManager
	{
		//private static SyncServer server;
		private static SyncClient client;
		
		static SyncManager ()
		{
			client = new TomboySyncClient ();
			//server = new FileSystemSyncServer ();
		}
		
		public static void PerformSynchronization ()
		{			
			SyncServer server;
			try {
				server = new FileSystemSyncServer ();
			} catch (Exception e) {
				Logger.Log ("Exception while creating SyncServer: " + e.Message);
				return;
				// TODO: This should be a GUI dialog
			}
			
			SyncDialog syncDialog = new SyncDialog ();
			syncDialog.ProgressText = "Contacting Server...";
			syncDialog.Response += OnSyncDialogResponse;
			syncDialog.Show ();
			
			// TODO: How to actually do transactions?  Is it even possible?
			if (!server.BeginSyncTransaction ()) {
				Logger.Log ("PerformSynchronization: Server locked, try again later");
				syncDialog.ProgressText = "Sync failed: Server locked, try again later";
				syncDialog.CloseSensitive = true;
				return;
			}
			int latestServerRevision = server.LatestRevision;
			int newRevision = latestServerRevision + 1;
			
			syncDialog.ProgressText = "Getting note updates...";
			syncDialog.ProgressFraction = 0.1;
			
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
			
			foreach (NoteUpdate note in noteUpdates.Values) {
				syncDialog.ProgressFraction += updateProgressInterval;
				Note existingNote = FindNoteByUUID (note.UUID);
				
				if (existingNote == null) {// TODO: not so simple...what if I deleted the note?
					if (note.XmlContent == string.Empty)
						continue; // Deletion of note that doesn't exist
					
					string newNotePath = Path.Combine (NoteMgr.NoteDirectoryPath,
					                                   note.UUID + ".note");
					FileInfo info = new FileInfo (newNotePath);
					StreamWriter writer = info.CreateText ();
					writer.Write (note.XmlContent);
					writer.Close ();
					Logger.Debug ("Copied new note here: " + newNotePath);
					Logger.Debug ("New note contents are: " + note.XmlContent);
					
					existingNote = Note.Load (newNotePath, NoteMgr);
					NoteMgr.Notes.Add (existingNote);
					
					syncDialog.AddUpdateItem (existingNote.Title, "Added");
				} else if (note.XmlContent == string.Empty &&
				         existingNote.ChangeDate.CompareTo (client.LastSyncDate) <= 0) {
					syncDialog.AddUpdateItem (existingNote.Title, "Deleted");
					NoteMgr.Delete (existingNote);
				} else if (existingNote.ChangeDate.CompareTo (client.LastSyncDate) <= 0) {
					existingNote.XmlContent = note.XmlContent;// TODO: live update of note XML
					syncDialog.AddUpdateItem (existingNote.Title, "Updated");
				} else {
					// TODO: handle conflicts
				}
			}
			
			// TODO: Add following updates to syncDialog treeview
			syncDialog.ProgressText = "Sending note updates...";
			syncDialog.ProgressFraction = 0.5;

			// Upload notes modified or added on client
			List<Note> newOrModifiedNotes = new List<Note> ();			
			foreach (Note note in NoteMgr.Notes) {
				if (note.Revision <= client.LastSynchronizedRevision &&// NOTE: Confused...?
				    note.ChangeDate.CompareTo (client.LastSyncDate) > 0)
					newOrModifiedNotes.Add (note);
			}
			// Apply this revision number to all new/modified notes since last sync
			// TODO: Should revision info be stored in note, or a seperate file?
			foreach (Note note in newOrModifiedNotes) {
				note.Revision = newRevision;
				note.Save ();
			}
			Logger.Debug ("Sync: Uploading " + newOrModifiedNotes.Count.ToString () + " note updates");
			server.UploadNotes (newOrModifiedNotes);
			
			
			// Handle notes deleted on client
			List<string> locallyDeletedUUIDs = new List<string> ();			
			foreach (string noteUUID in server.GetAllNoteUUIDs ()) {
				if (FindNoteByUUID (noteUUID) == null)
					locallyDeletedUUIDs.Add (noteUUID);
			}			
			server.DeleteNotes (locallyDeletedUUIDs);
			
			bool commitResult = server.CommitSyncTransaction ();
			if (commitResult) {
				syncDialog.ProgressFraction = 1.0;
				syncDialog.ProgressText = "Sync completed successfully!";
			}
			else {
				syncDialog.ProgressFraction = 0;
				syncDialog.ProgressText = "Sync failed!";
			}
			syncDialog.CloseSensitive = true;
			
			// This should be equivalent to newRevision
			client.LastSynchronizedRevision = server.LatestRevision;
			
			client.LastSyncDate = DateTime.Now;		
			
			Logger.Debug ("Sync: New revision: {0}", client.LastSynchronizedRevision);
		}
		
		private static Note FindNoteByUUID (string uuid)
		{
			return NoteMgr.FindByUri ("note://tomboy/" + uuid);
		}
		
		private static NoteManager NoteMgr
		{
			get { return Tomboy.DefaultNoteManager; }
		}
		
		private static void OnSyncDialogResponse (object sender, Gtk.ResponseArgs args)
		{
			SyncDialog dialog = sender as SyncDialog;
			dialog.Hide ();
			dialog.Destroy ();
		}
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
	
	public interface SyncServer
	{
		bool BeginSyncTransaction ();
		bool CommitSyncTransaction ();
		IList<string> GetAllNoteUUIDs ();
		IDictionary<string, NoteUpdate> GetNoteUpdatesSince (int revision);
		void DeleteNotes (IList<string> deletedNoteUUIDs);
		void UploadNotes (IList<Note> notes);
		int LatestRevision { get; }
		TimeSpan LockExpirationDuration { get; }
	}
	
	public interface SyncSession
	{
		
	}
	
	public interface SyncClient
	{
		int LastSynchronizedRevision { get; set; }
		DateTime LastSyncDate { get; set; }		
	}
}
