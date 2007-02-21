using System;

namespace Tomboy.Platform
{
	public interface IKeybinder
	{
		void Bind (string keystring, EventHandler handler);
		void Unbind (string keystring);
		void UnbindAll ();
	}
}
