using System;
using Gtk;
using Mono.Unix;

namespace Tomboy.Sharing
{
	public class NoteShareNode : ShareNode
	{
		private static Gdk.Pixbuf note_icon;
		
		private string guid;
		private string name;
		private DateTime last_modified;
		
		private string note_id;
		private uint revision;
		private string client_guid;
		
		static NoteShareNode ()
		{
			note_icon = GuiUtils.GetIcon ("stock_notes", 16);
		}
		
		public override Gdk.Pixbuf Pixbuf
		{
			get { return note_icon; }
		}
		
		public override string Guid
		{
			get { return guid; }
		}

		public override string Name
		{
			get { return name; }
		}
		
		public override string Status
		{
			get { return GuiUtils.PrettyPrintDate (last_modified); }
		}
		
		public uint Revision
		{
			get { return revision; }
		}
		
		/// <summary>
		/// The Guid of the Tomboy Client this note came from.  This is here as
		/// a convenience so we don't have to write code to go look up the
		/// TomboyShareNode all the time.
		/// </summary>
		public string ClientGuid
		{
			get { return client_guid; }
		}
		
		public NoteShareNode (string note_uri, string name,
							  DateTime last_modified, uint revision,
							  string client_guid)
		{
			this.guid = note_uri;
			this.name = name;
			this.last_modified = last_modified;
			this.revision = revision;
			this.client_guid = client_guid;
		}
	}
}