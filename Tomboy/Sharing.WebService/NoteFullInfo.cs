using System;
using System.Xml.Serialization;

namespace Tomboy.Sharing.Web
{
	[Serializable]
	public class NoteFullInfo : NoteInfo
	{
		public string XmlContent;

		public NoteFullInfo ()
		{
		}
		
		public NoteFullInfo (NoteData note_data) : base (note_data)
		{
			this.XmlContent = note_data.Text;
		}
	}
}