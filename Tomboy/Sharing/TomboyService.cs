using System;
using System.Net;

namespace Tomboy.Sharing
{
	/// <summary>
	/// This class is used to broadcast information about a Tomboy Client on
	/// a mDNS network.
	/// </summary>
	public class TomboyService
	{
		private IPAddress ip_address;
		private short port;
		private string guid;
		private string name;
		private bool password_protected;
		private bool sharing_enabled;
		private uint revision;
		
		private static TomboyService local_instance = null;
		private static string locker = "locker";
		
		public const string SERVICE_TYPE = "_tomboy._tcp";
		public const string TXT_GUID = "Guid";
		public const string TXT_NAME = "Shared Name";
		public const string TXT_PASSWORD_PROTECTED = "Password Protected";
		public const string TXT_SHARING_ENABLED = "Sharing Enabled";
		public const string TXT_REVISION = "Revision";
		
		public IPAddress IPAddress
		{
			get { return ip_address; }
			set {
				ip_address = value;
				if (IPAddressChanged != null)
					IPAddressChanged (this, EventArgs.Empty);
			}
		}
		
		public short Port
		{
			get { return port; }
			set {
				port = value;
				if (PortChanged != null)
					PortChanged (this, EventArgs.Empty);
			}
		}
		
		public string Guid
		{	
			get { return guid; }
			set {
				guid = value;
				if (GuidChanged != null)
					GuidChanged (this, EventArgs.Empty);
			}
		}
		
		public string Name
		{
			get { return name; }
			set {
				name = value;
				if (NameChanged != null)
					NameChanged (this, EventArgs.Empty);
			}
		}
		
		public bool PasswordProtected
		{
			get { return password_protected; }
			set {
				password_protected = value;
				if (PasswordProtectedChanged != null)
					PasswordProtectedChanged (this, EventArgs.Empty);
			}
		}
		
		public bool SharingEnabled
		{
			get { return sharing_enabled; }
			set {
				sharing_enabled = value;
				if (SharingStatusChanged != null)
					SharingStatusChanged (this, EventArgs.Empty);
			}
		}
		
		public uint Revision
		{
			get { return revision; }
			set {
				revision = value;
				if (RevisionChanged != null)
					RevisionChanged (this, EventArgs.Empty);
			}
		}
		
		public static TomboyService LocalInstance
		{
			get {
				if (local_instance == null) {
					lock (locker) {
						if (local_instance == null) {
							local_instance = CreateLocalInstance ();
						}
					}
				}
				
				return local_instance;
			}
		}
		
		public event EventHandler IPAddressChanged;
		public event EventHandler PortChanged;
		public event EventHandler GuidChanged;
		public event EventHandler NameChanged;
		public event EventHandler PasswordProtectedChanged;
		public event EventHandler SharingStatusChanged;
		public event EventHandler RevisionChanged;
		
		public TomboyService ()
		{
		}
		
		private static TomboyService CreateLocalInstance ()
		{
			TomboyService service = new TomboyService ();
			service.ip_address = IPAddress.Parse ("127.0.0.1"); // FIXME: Figure out this client's real IP Address and set it by default here
			service.port = 8034; // FIXME: Read this from config or dynamically choose it?
			service.guid = Preferences.Get (Preferences.SHARING_GUID) as string;
			service.name = Preferences.Get (Preferences.SHARING_SHARED_NAME) as string;
			if (Preferences.GetPassword (Preferences.SHARING_PASSWORD_DOMAIN) != null)
				service.password_protected = true;
			else
				service.password_protected = false;
			
			service.sharing_enabled = (bool) Preferences.Get (Preferences.SHARING_ENABLE_LOCAL_PUBLISHING);
			service.revision = 0; // FIXME: Use this once revisions are supported
			
			// Register Event Listeners to watch for preference changes
			Preferences.SettingChanged += service.OnPreferenceChanged;
			
			return service;
		}
		
		private void OnPreferenceChanged (object sender, GConf.NotifyEventArgs args)
		{
Logger.Debug ("TomboyService.OnPreferenceChanged");
			switch (args.Key) {
			case Preferences.SHARING_GUID:
				this.Guid = args.Value as string;
				break;
			case Preferences.SHARING_SHARED_NAME:
				this.Name = args.Value as string;
				break;
			case Preferences.SHARING_ENABLE_LOCAL_PUBLISHING:
				this.SharingEnabled = (bool) args.Value;
				break;
			}
			
			// FIXME: Modify Preferences.cs to send a changed event when a password is set/changed/cleared
		}
	}
}