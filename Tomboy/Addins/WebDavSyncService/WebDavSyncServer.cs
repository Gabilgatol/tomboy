using System;
using Tomboy;

namespace Tomboy.Sync
{
	public class WebDavSyncServer : FileSystemSyncServer
	{
		public WebDavSyncServer (string webDavMountPath)
			: base (webDavMountPath)
		{
		}
	}
}
