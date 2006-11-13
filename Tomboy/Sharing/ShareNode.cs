using System;
using Gtk;
using Mono.Unix;

namespace Tomboy.Sharing
{
	public abstract class ShareNode
	{
		public abstract Gdk.Pixbuf Pixbuf
		{
			get;
		}
		
		public abstract string Guid
		{
			get;
		}

		public abstract string Name
		{
			get;
		}
		
		public abstract string Status
		{
			get;
		}
	}
}