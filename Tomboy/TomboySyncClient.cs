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
		}
				
		private void OnChanged(object source, FileSystemEventArgs e)
		{
			Parse (localManifestFilePath);
		}
		
		private void Parse (string manifestPath)
		{
			// blah blah blah, read XML
			lastSyncDate = DateTime.Today.AddDays (-1);
			lastSyncRev = 0;
			
			if (!File.Exists (manifestPath)) {
				lastSyncDate = new DateTime (0);
				lastSyncRev = 0;
				Write (manifestPath);
			}
			
			StreamReader reader = new StreamReader (manifestPath,
			                                        System.Text.Encoding.UTF8);
			XmlTextReader xml = new XmlTextReader (reader);
			xml.Namespaces = false;

			while (xml.Read ()) {
				switch (xml.NodeType) {
				case XmlNodeType.Element:
					switch (xml.Name) {
					case "last-sync-rev":
						lastSyncRev = int.Parse (xml.ReadString ());
						break;
					case "last-sync-date":
						lastSyncDate = XmlConvert.ToDateTime (xml.ReadString (), NoteArchiver.DATE_TIME_FORMAT);
						break;
				}
				break;
				}
			}
			
			xml.Close ();
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
			
			xml.WriteEndElement ();
			
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

		
	}
}