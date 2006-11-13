using System;
using Gtk;
using Mono.Unix;

namespace Tomboy.Sharing
{
	/// <summary>
	/// This type of node is placed as a child to all new TomboyShareNodes
	/// so that an expander will appear in a TreeView.  It is removed when
	/// the TreeView is expanded.
	/// </summary>
	public class DummyShareNode : ShareNode
	{
		private string guid;
		private string message;

		public override Gdk.Pixbuf Pixbuf
		{
			get { return null; }
		}
		
		public override string Guid
		{
			get { return guid; }
		}

		public override string Name
		{
			get { return message; }
		}
		
		public override string Status
		{
			get { return String.Empty; }
		}
		
		public DummyShareNode (string message)
		{
			this.guid = System.Guid.NewGuid ().ToString ();
			this.message = message;
		}
		
		public void UpdateMessage (string updated_message)
		{
			message = updated_message;
		}
	}
}