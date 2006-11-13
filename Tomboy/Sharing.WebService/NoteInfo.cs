using System;
using System.Xml.Serialization;

namespace Tomboy.Sharing.Web
{
	[Serializable]
	public class NoteInfo
	{
		public string Guid;
		public string Name;
		public string LastModified;
		public uint Revision;
		
		public NoteInfo()
		{
		}
		
		public NoteInfo (NoteData note_data)
		{
			this.Guid = note_data.Uri;
			this.Name = note_data.Title;
			this.LastModified = note_data.ChangeDate.ToString ();
			this.Revision = 0; // FIXME: Implement Revisions!
		}
	}
}