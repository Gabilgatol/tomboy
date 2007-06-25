using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;

namespace Tomboy
{
	public class FileSystemSyncServer : SyncServer
	{
		private class RevisionDetails
		{
			public int Revision;
			public IList<string> UpdatedNotes;
			public IList<string> DeletedNotes;
			
			private FileSystemSyncServer server;
			
			public RevisionDetails (int revision, FileSystemSyncServer server)
			{
				Revision = revision;
				this.server = server;
				UpdatedNotes = new List<string> ();
				DeletedNotes = new List<string> ();
				
				int revisionParentDir = revision / 100;
				string manifestFileParentDir = Path.Combine (server.serverPath,
				                                             revisionParentDir.ToString ());
				string manifestFilePath = Path.Combine (manifestFileParentDir,
				                                        revision.ToString () + ".rev");
				
				StreamReader reader = new StreamReader (manifestFilePath);
				
				// TODO: Read details from file
				string section = "";
				while (!reader.EndOfStream) {
					string line = reader.ReadLine ();
					if (line == "U")
						section = "U";
					else if (line == "D")
						section = "D";
					else if (line.Length > 0 && section == "U")
						UpdatedNotes.Add (line);
					else if (line.Length > 0 && section == "D")
						DeletedNotes.Add (line);
				}
				
				reader.Close ();
			}
		}
		
		private List<string> updatedNotes;
		private List<string> deletedNotes;
		
		private string serverPath;
		private string notePath;
		private string lockPath;
		
		private static DateTime initialSyncAttempt = DateTime.MinValue;
		
		public FileSystemSyncServer ()
		{			
			// TODO: Handle changes to serverPath...
			//       Should there be a new instance of this class
			//       for each sync?			
			serverPath = (string) Preferences.Get (Preferences.SYNC_URL);
			
			if (!Directory.Exists (serverPath))
				throw new DirectoryNotFoundException (serverPath);
			
			notePath = Tomboy.DefaultNoteManager.NoteDirectoryPath;
			lockPath = Path.Combine (serverPath, "lock");
		}
		
		public virtual void UploadNotes (IList<Note> notes)
		{
			foreach (Note note in notes) {
				try {
					string serverNotePath = Path.Combine (serverPath, Path.GetFileName (note.FilePath));
					File.Copy (note.FilePath, serverNotePath, true);
					Mono.Unix.Native.Syscall.chmod (serverNotePath, Mono.Unix.Native.FilePermissions.ACCESSPERMS);
					updatedNotes.Add (Path.GetFileNameWithoutExtension (note.FilePath));
				} catch (Exception e) {
					Logger.Log ("Sync: Error uploading note: " + e.Message);
				}
			}
		}

		public virtual void DeleteNotes (IList<string> deletedNoteUUIDs)
		{
			foreach (string uuid in deletedNoteUUIDs) {
				try {
					File.Delete (Path.Combine (serverPath, uuid + ".note"));
					deletedNotes.Add (uuid);
				} catch (Exception e) {
					Logger.Log ("Sync: Error deleting note: " + e.Message);
				}
			}				
		}

		public IList<string> GetAllNoteUUIDs ()
		{
			List<string> noteUUIDs = new List<string> ();
			
			foreach (string fileName in Directory.GetFiles (serverPath, "*.note"))
				noteUUIDs.Add (Path.GetFileNameWithoutExtension (fileName));
			
			return noteUUIDs;
		}

		public virtual IDictionary<string, NoteUpdate> GetNoteUpdatesSince (int revision)
		{
			// TODO: Empty temp dir each time
			string tempPath = Path.Combine (notePath, "sync_temp");
			if (!Directory.Exists (tempPath))
				Directory.CreateDirectory (tempPath);
			
			Dictionary<string, NoteUpdate> noteUpdates = new Dictionary<string, NoteUpdate> ();
			// TODO: Read manifest file
			for (int i = LatestRevision; i> revision; i--) {
				RevisionDetails revDetails = new RevisionDetails (i, this);
				// TODO: Pull info into NoteUpdate objects
				foreach (string uuid in revDetails.DeletedNotes) {
					if (!noteUpdates.ContainsKey (uuid)) {
						NoteUpdate update = new NoteUpdate (string.Empty,string.Empty,uuid,i);
						noteUpdates[uuid] = update;
					}
				}
				
				foreach (string uuid in revDetails.UpdatedNotes) {
					if (!noteUpdates.ContainsKey (uuid)) {
						// TODO: Put file in temp dir
						File.Copy (Path.Combine (serverPath, uuid + ".note"),
						           Path.Combine (tempPath, uuid + ".note"),
						           true);
						// TODO: Get title, contents, etc
						string noteTitle = "";
						StreamReader reader = new StreamReader (Path.Combine (tempPath, uuid + ".note"));
						string noteXml = reader.ReadToEnd ();
						reader.Close ();
						NoteUpdate update = new NoteUpdate (noteXml, noteTitle, uuid, i);
						noteUpdates[uuid] = update;
					}
				}
			}
			
			return noteUpdates;
		}

		public virtual bool BeginSyncTransaction ()
		{
			// Lock expiration: If a lock file exists on the server, a client
			// will never be able to synchronize on its first attempt.  The
			// client should record the time elapsed
			if (File.Exists (lockPath)) {
				TimeSpan ts = LockExpirationDuration;
				if (initialSyncAttempt == DateTime.MinValue) {
					initialSyncAttempt = DateTime.Now;
					return false;
				} else if (DateTime.Now - ts < initialSyncAttempt) {
					// Not enough time has expired since the initial sync attempt 
					return false;
				} else {
					Logger.Debug ("Sync: Deleting expired lockfile");
					File.Delete (lockPath);
				}
			}
			
			// Reset the initialSyncAttempt
			initialSyncAttempt = DateTime.MinValue;
			
			// Create a new lock file so other clients know another client is
			// actively synchronizing right now.
			CreateLockFile (new TimeSpan (0, 1, 0));
			
			updatedNotes = new List<string> ();
			deletedNotes = new List<string> ();
			
			return true;
		}

		public virtual bool CommitSyncTransaction ()
		{
			if (updatedNotes.Count > 0 || deletedNotes.Count > 0)
			{
				// TODO: error-checking, etc
				int newRevision = LatestRevision + 1;
				int newRevisionParentDir = newRevision / 100;
				string manifestFileParentDir = Path.Combine (serverPath,
				                                             newRevisionParentDir.ToString ());
				string manifestFilePath = Path.Combine (manifestFileParentDir,
				                                        newRevision.ToString () + ".rev");
				if (!Directory.Exists (manifestFileParentDir))
				{
					Directory.CreateDirectory (manifestFileParentDir);
					Mono.Unix.Native.Syscall.chmod (manifestFileParentDir,
					                                Mono.Unix.Native.FilePermissions.ACCESSPERMS);
				}
				
				FileInfo info = new FileInfo (manifestFilePath);
				StreamWriter writer = info.CreateText ();
				if (updatedNotes.Count > 0) {
					writer.WriteLine ("U");
					foreach (string uuid in updatedNotes)
						writer.WriteLine (uuid);
				}
				if (deletedNotes.Count > 0) {
					writer.WriteLine ("D");
					foreach (string uuid in deletedNotes)
						writer.WriteLine (uuid);
				}

				writer.Close ();
				Mono.Unix.Native.Syscall.chmod (manifestFilePath,
				                                Mono.Unix.Native.FilePermissions.ACCESSPERMS);
			}
			
			File.Delete (lockPath);//TODO: Errors?  When return false?
			return true;
		}
		
		public virtual int LatestRevision
		{
			get
			{
				int latestRevDir = -1;
				foreach (string dir in Directory.GetDirectories (serverPath)) {
					try {
						int currentRevDir = int.Parse (Path.GetFileName (dir));// TODO: Do this better!
						if (currentRevDir > latestRevDir)
							latestRevDir = currentRevDir;
					} catch {}
				}
				
				int latestRev = -1;
				if (latestRevDir > -1) {
					foreach (string revFile in Directory.GetFiles (Path.Combine (serverPath,
					                                                                   latestRevDir.ToString ()))) {
						try {
							int currentRevFile = int.Parse (Path.GetFileNameWithoutExtension (revFile));
							if (currentRevFile > latestRev)
								latestRev = currentRevFile;
						} catch {}
					}
				}
				
				return latestRev;
			}
		}
		
		/// <summary>
		/// The amount of time that must expire before a client can claim
		/// ownership of a synchronization session.  If a lock file exists
		/// on the server, the client must wait this amount of time before
		/// checking for the lock file again.  If the same lock file exists
		/// the client can delete the lock file and begin synchronizing.
		/// </summary>
		public virtual TimeSpan LockExpirationDuration
		{
			get {
				// Set a default of 5 minutes
				TimeSpan ts = new TimeSpan (0, 5, 0);
				
				XmlDocument doc = new XmlDocument ();
				FileStream fs = new FileStream (lockPath, FileMode.Open);
				doc.Load (fs);
				
				XmlNode node = doc.SelectSingleNode ("//lock-expiration-duration/text ()");
				if (node != null) {
					string span_txt = node.InnerText;
					ts = TimeSpan.Parse (span_txt);
				}
				
				fs.Close ();
				
				return ts;
			}
		}
		
		#region Pivate Methods
		public void CreateLockFile (TimeSpan ts)
		{
			XmlTextWriter xml = new XmlTextWriter (lockPath, System.Text.Encoding.UTF8);
			
			xml.Formatting = Formatting.Indented;

			xml.WriteStartDocument ();
			xml.WriteStartElement (null, "lock", null);
			
			xml.WriteStartElement (null, "lock-expiration-duration", null);
			xml.WriteString (ts.ToString ());
			xml.WriteEndElement ();
			
			xml.WriteEndElement ();
			xml.WriteEndDocument ();
			
			xml.Close ();
		}
		#endregion
	}
}