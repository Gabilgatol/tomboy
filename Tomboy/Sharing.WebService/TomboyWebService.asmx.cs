using System;
using System.Collections;
using System.IO;
using System.Web;
using System.Web.Services;
using System.Web.Services.Protocols;

namespace Tomboy.Sharing.Web
{
	[WebService(
		Namespace="http://beatniksoftware.com/tomboy/web/",
		Name="TomboyWebService",
		Description="Web Service providing access to Tomboy Notes")]
	public class TomboyWebService : WebService
	{
		private string notes_dir;
		public TomboyWebService ()
		{
//			// Since we're running in a different process, prevent the logger from
//			// attempting to load the same log file as the original Tomboy process.
//			Logger.LogDevice = new FileLogger (
//				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Personal), // HOME
//				 ".tomboy/tomboy-web.log"));
			notes_dir =
				Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Personal), // Home
				".tomboy");
		}
		
		[WebMethod(EnableSession=true, Description="Allows a client to ping to make sure the Web Service is up and running")]
		[SoapDocumentMethod]
		public void Ping ()
		{
			// Don't actually do anything, just return.
		}
		
		[WebMethod(EnableSession=true, Description="Get the specified note")]
		[SoapDocumentMethod]
		public NoteFullInfo GetNote (string noteUri)
		{
			NoteFullInfo full_info = null;
			try { // FIXME: Remove this "DEBUG" try/catch statement
				if (noteUri == null)
					return null;
				
				NoteData note_data = LoadNote (noteUri);
				if (note_data != null)
					full_info = new NoteFullInfo (note_data);
			} catch (Exception e) {
				throw new Exception (e.StackTrace);
			}
			
			return full_info;
		}
		
		
		[WebMethod(EnableSession=true, Description="Get all shared notes")]
		[SoapDocumentMethod]
		public NoteInfo [] GetSharedNotes ()
		{
			ArrayList list = new ArrayList ();
			
			try { // FIXME: Remove this "DEBUG" try/catch statement
				string selected_notes = Preferences.Get (Preferences.SHARING_SELECTED_NOTES) as string;
				if (selected_notes != null) {
					if (selected_notes.CompareTo ("ALL") == 0)
						LoadAllNotesIntoArrayList (list);
					else
						LoadSelectedNotesIntoArrayList (selected_notes.Split (','), list);
				}
			} catch (Exception e) {
				throw new Exception (e.StackTrace);
			}
			
			return list.ToArray (typeof (NoteInfo)) as NoteInfo [];
		}
		
		private void LoadAllNotesIntoArrayList (ArrayList list)
		{
			string [] files = Directory.GetFiles (notes_dir, "*.note");
			foreach (string file_path in files) {
				NoteData note_data = LoadNote (file_path, UrlFromPath (file_path));
				if (note_data == null)
					continue;
				
				NoteInfo note_info = new NoteInfo (note_data);
				list.Add (note_info);
			}
		}
		
		// FIXME: WEIRD place to put this, but...  Need to tie into the NoteDeleted handler in the SharingManager or somewhere so that we can remove SelectedNotes from GConf.
		
		private void LoadSelectedNotesIntoArrayList (string [] note_uris, ArrayList list)
		{
			foreach (string note_uri in note_uris) {
				if (note_uri == String.Empty)
					continue;	// Skip empty strings

				NoteData note_data = LoadNote (note_uri);
				if (note_data == null)
					continue;
				
				NoteInfo note_info = new NoteInfo (note_data);
				list.Add (note_info);
			}
		}
		
		private string UrlFromPath (string filepath)
		{
			return "note://tomboy/" +
				Path.GetFileNameWithoutExtension (filepath);
		}
		
		private NoteData LoadNote (string note_uri) {
			string note_file_path = BuildNoteFilePathFromUri (note_uri);
			
			return LoadNote (note_file_path, note_uri);
		}
		
		private NoteData LoadNote (string note_file_path, string note_uri)
		{
			if (note_file_path == null || note_uri == null)
				throw new ArgumentNullException ("LoadNote () called with null arguments.");
			
			NoteData note_data = NoteArchiver.Read (note_file_path, note_uri);
			return note_data;
		}
		
		private string BuildNoteFilePathFromUri (string note_uri)
		{
			if (note_uri == null)
				throw new ArgumentNullException ("BuildNoteFilePathFromUri () called with a null argument.");
			
			string note_guid = ParseNoteGuidFromUri (note_uri);
			
			string note_file_path =
				Path.Combine (notes_dir, note_guid + ".note");
			
			return note_file_path;
		}
		
		private string ParseNoteGuidFromUri (string note_uri)
		{
			if (note_uri == null)
				throw new ArgumentNullException ("ParseNoteGuidFromUri () called with a null argument.");
			
			int last_slash_pos = note_uri.LastIndexOf ("/");
			if (last_slash_pos < 0)
				throw new Exception ("ParseNoteGuidFromUri () called with malformed Note Uri (no forward slash characters found).");
			
			if (note_uri.Length == last_slash_pos + 1)
				throw new Exception ("ParseNoteGuidFromUri () called with malformed Note Uri (trailing slash).");
			
			string note_guid = note_uri.Substring (last_slash_pos + 1);
			
			return note_guid;
		}
	}
}