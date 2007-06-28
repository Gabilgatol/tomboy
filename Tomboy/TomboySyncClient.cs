using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;

namespace Tomboy
{
	public class TomboySyncClient : SyncClient
	{
		private const string localManifestFileName = "manifest.xml";
		
		private DateTime lastSyncDate;
		private int lastSyncRev;
		private string localManifestFilePath;
		private Dictionary<string, int> fileRevisions;
		
		public TomboySyncClient ()
		{			
			FileSystemWatcher w = new FileSystemWatcher ();
			w.Path = Tomboy.DefaultNoteManager.NoteDirectoryPath;
			w.Filter = localManifestFileName;
			w.Changed += OnChanged;
			
			localManifestFilePath =
				Path.Combine (Tomboy.DefaultNoteManager.NoteDirectoryPath,
				              localManifestFileName);
			Parse (localManifestFilePath);
			
			Tomboy.DefaultNoteManager.NoteDeleted += NoteDeletedHandler;
		}
		
		private void NoteDeletedHandler (object noteMgr, Note deletedNote)
		{
			fileRevisions.Remove (deletedNote.Id);
		}
				
		private void OnChanged(object source, FileSystemEventArgs e)
		{
			Parse (localManifestFilePath);
		}
		
		private void Parse (string manifestPath)
		{
			// Set defaults before parsing
			lastSyncDate = DateTime.Today.AddDays (-1);
			lastSyncRev = -1;
			fileRevisions = new Dictionary<string,int> ();
			
			if (!File.Exists (manifestPath)) {
				lastSyncDate = DateTime.MinValue;
				Write (manifestPath);
			}			
			
			XmlDocument doc = new XmlDocument ();
			FileStream fs = new FileStream (manifestPath, FileMode.Open);
			doc.Load (fs);
			
			// TODO: Error checking
			foreach (XmlNode noteNode in doc.SelectNodes ("//note-revisions/note")) {
				string guid = noteNode.Attributes ["guid"].InnerXml;
				int revision = -1;
				try {
					revision = int.Parse (noteNode.Attributes ["latest-revision"].InnerXml);
				} catch { }
				
				fileRevisions [guid] = revision;
			}

			XmlNode node = doc.SelectSingleNode ("//last-sync-rev/text ()");
			if (node != null)
				lastSyncRev = int.Parse (node.InnerText);

			node = doc.SelectSingleNode ("//last-sync-date/text ()");
			if (node != null)
				lastSyncDate = XmlConvert.ToDateTime (node.InnerText);
			
			fs.Close ();
		}
		
		private void Write (string manifestPath)
		{
			XmlTextWriter xml = new XmlTextWriter (manifestPath, System.Text.Encoding.UTF8);
			
			xml.Formatting = Formatting.Indented;

			xml.WriteStartDocument ();
			xml.WriteStartElement (null, "manifest", "http://beatniksoftware.com/tomboy");
			
			xml.WriteStartElement (null, "last-sync-date", null);
			xml.WriteString (XmlConvert.ToString (lastSyncDate, NoteArchiver.DATE_TIME_FORMAT));
			xml.WriteEndElement ();
			
			xml.WriteStartElement (null, "last-sync-rev", null);
			xml.WriteString (lastSyncRev.ToString ());
			xml.WriteEndElement ();
			
			xml.WriteStartElement (null, "note-revisions", null);
			
			foreach (string noteGuid in fileRevisions.Keys) {
				xml.WriteStartElement (null, "note", null);
				xml.WriteAttributeString (null, "guid", null, noteGuid);
				xml.WriteAttributeString (null, "latest-revision", null, fileRevisions [noteGuid].ToString ());
				xml.WriteEndElement ();
			}
			
			xml.WriteEndElement (); // </note-revisons>
			
			xml.WriteEndElement (); // </manifest>
			
			xml.Close ();
		}
		
		public virtual DateTime LastSyncDate
		{
			get { return lastSyncDate; }
			set
			{
				lastSyncDate = value;
				Write (localManifestFilePath);
			}
		}

		public virtual int LastSynchronizedRevision
		{
			get { return lastSyncRev; }
			set
			{
				lastSyncRev = value;
				Write (localManifestFilePath);
			}
		}

		public virtual int GetRevision (Note note)
		{
			string noteGuid = note.Id;
			if (fileRevisions.ContainsKey (noteGuid))
				return fileRevisions [noteGuid];
			else
				return -1;
		}
		
		public virtual void SetRevision (Note note, int revision)
		{
			fileRevisions [note.Id] = revision;
			// TODO: Should we write on each of these or no?
			Write (localManifestFilePath);
		}
	}
}