using System;
using Gtk;
using Mono.Unix;

namespace Tomboy.Sharing
{
	public class TomboyShareNode : ShareNode
	{
		private static Gdk.Pixbuf icon;
		
		private TomboyService service;
		private bool connected;
		
		static TomboyShareNode ()
		{
			icon = GuiUtils.GetIcon ("stock_notebook", 16);
		}
		
		public override Gdk.Pixbuf Pixbuf
		{
			get { return icon; }
		}
		
		public override string Guid
		{
			get { return service.Guid; }
		}

		public override string Name
		{
			get { return service.Name; }
		}
		
		public override string Status
		{
			get { return String.Empty; }
		}
		
		public TomboyService Service
		{
			get { return service; }
		}
		
		public bool Connected
		{
			get { return connected; }
		}
		
		public TomboyShareNode (TomboyService service)
		{
			this.service = service;
			this.connected = false;
		}
		
		public bool ConnectForSharing ()
		{
			return ConnectForSharing (null);
		}
		
		public bool ConnectForSharing (string password)
		{
			Logger.Debug ("FIXME: Implement TomboyShareNode.ConnectForSharing ()");
			connected = true;
			
			return connected;
		}
		
		public NoteShareNode [] GetSharedNotes ()
		{
			Logger.Debug ("FIXME: Implement TomboyShareNode.NoteShareNode");
			return new NoteShareNode [0];
		}
		
		/// <summary>
		/// For now (to drastically simplify things), this should download the raw
		/// Tomboy *.note file into the ~/.tomboy/ directory (without overwriting),
		/// somehow get it added to the NoteManager, and returned to the caller as
		/// a standard local note.
		///
		/// If an existing note file already exists, a new note should be created
		/// entirely and renamed automatically to not cause a name conflict.
		/// </summary>
		public Note DownloadNote (NoteShareNode note_node)
		{
			Logger.Debug ("FIXME: Implement TomboyShareNode.NodeData");
			return null;
		}
	}
}