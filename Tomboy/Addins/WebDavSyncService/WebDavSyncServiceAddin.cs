using System;
using System.Diagnostics;
using System.IO;

using Gtk;

using Mono.Unix;

using Tomboy;

namespace Tomboy.Sync
{
	public class WebDavSyncServiceAddin : SyncServiceAddin
	{
		// TODO: Extract most of the code here and build GenericSyncServiceAddin
		// that supports a field, a username, and password.  This could be useful
		// in quickly building SshSyncServiceAddin, FtpSyncServiceAddin, etc.
		
		Entry urlEntry;
		Entry usernameEntry;
		Entry passwordEntry;
		
		string mountPath;
		
		/// <summary>
		/// Called as soon as Tomboy needs to do anything with the service
		/// </summary>
		public override void Initialize ()
		{
//			if (IsConfigured) {
				// Make sure the mount is loaded
				SetUpMountPath ();
//			}
		}

		/// <summary>
		/// Creates a SyncServer instance that the SyncManager can use to
		/// synchronize with this service.  This method is called during
		/// every synchronization process.  If the same SyncServer object
		/// is returned here, it should be reset as if it were new.
		/// </summary>
		public override SyncServer CreateSyncServer ()
		{
			SyncServer server = null;
			
			string url, username, password;
			if (GetConfigSettings (out url, out username, out password)) {
				if (IsMounted () == false) {
					if (MountWebDav (url, username, password) == false) {
						throw new Exception ("Could not mount " + mountPath);
					}
				}

				server = new WebDavSyncServer (mountPath);
			} else {
				throw new InvalidOperationException ("WebDavSyncServiceAddin.CreateSyncServer () called without being configured");
			}
			
			return server;
		}
		
		/// <summary>
		/// Creates a Gtk.Widget that's used to configure the service.  This
		/// will be used in the Synchronization Preferences.  Preferences should
		/// not automatically be saved by a GConf Property Editor.  Preferences
		/// should be saved when SaveConfiguration () is called.
		/// </summary>
		public override Gtk.Widget CreatePreferencesControl ()
		{
			Gtk.Table table = new Gtk.Table (3, 2, false);
			
			// Read settings out of gconf
			string url = Preferences.Get ("/apps/tomboy/sync_wdfs_url") as String;
			string username = Preferences.Get ("/apps/tomboy/sync_wdfs_username") as String;
			string password = Preferences.Get ("/apps/tomboy/sync_wdfs_password") as String;
			if (url == null)
				url = string.Empty;
			if (username == null)
				username = string.Empty;
			if (password == null)
				password = string.Empty;
			
			Label l = new Label (Catalog.GetString ("URL:"));
			l.Xalign = 1;
			l.Show ();
			table.Attach (l, 0, 1, 0, 1);
			
			urlEntry = new Entry ();
			urlEntry.Text = url;
			urlEntry.Show ();
			table.Attach (urlEntry, 1, 2, 0, 1);
			
			l = new Label (Catalog.GetString ("Username:"));
			l.Xalign = 1;
			l.Show ();
			table.Attach (l, 0, 1, 1, 2);
			
			usernameEntry = new Entry ();
			usernameEntry.Text = username;
			usernameEntry.Show ();
			table.Attach (usernameEntry, 1, 2, 1, 2);
			
			l = new Label (Catalog.GetString ("Password:"));
			l.Xalign = 1;
			l.Show ();
			table.Attach (l, 0, 1, 2, 3);
			
			passwordEntry = new Entry ();
			passwordEntry.Text = password;
			passwordEntry.Visibility = false;
			passwordEntry.Show ();
			table.Attach (passwordEntry, 1, 2, 2, 3);
			
			table.Show ();
			return table;
		}
		
		/// <summary>
		/// The Addin should verify and check the connection to the service
		/// when this is called.  If verification and connection is successful,
		/// the addin should save the configuration and return true.
		/// </summary>
		public override bool SaveConfiguration ()
		{
			string url = urlEntry.Text.Trim ();
			string username = usernameEntry.Text.Trim ();
			string password = passwordEntry.Text.Trim ();
			
			if (url == string.Empty
						|| username == string.Empty
						|| password == string.Empty) {
				// TODO: Figure out a way to send the error back to the client
				Logger.Debug ("One of url, username, or password was empty");
				return false;
			}
			
			SetUpMountPath ();
			
			// TODO: Check to see if the mount is already mounted
			bool mounted = MountWebDav (url, username, password);
			
			if (mounted) {
				Preferences.Set ("/apps/tomboy/sync_wdfs_url", url);
				Preferences.Set ("/apps/tomboy/sync_wdfs_username", username);
				// TODO: MUST FIX THIS.  DO NOT STORE CLEAR TEXT PASSWORD IN GCONF!
				Preferences.Set ("/apps/tomboy/sync_wdfs_password", password);
			}
			
			return mounted;
		}

		/// <summary>
		/// Reset the configuration so that IsConfigured will return false.
		/// </summary>
		public override void ResetConfiguration ()
		{
			Preferences.Set ("/apps/tomboy/sync_wdfs_url", string.Empty);
			Preferences.Set ("/apps/tomboy/sync_wdfs_username", string.Empty);
			Preferences.Set ("/apps/tomboy/sync_wdfs_password", string.Empty);
			
			// TODO: Unmount the FUSE mount!
		}
		
		/// <summary>
		/// Returns whether the addin is configured enough to actually be used.
		/// </summary>
		public override bool IsConfigured
		{
			get {
				string url = Preferences.Get ("/apps/tomboy/sync_wdfs_url") as String;
				string username = Preferences.Get ("/apps/tomboy/sync_wdfs_username") as String;
				string password = Preferences.Get ("/apps/tomboy/sync_wdfs_password") as String;
				
				if (url != null && url != string.Empty
						&& username != null && username != string.Empty
						&& password != null && password != string.Empty) {
					return true;
				}
				
				return false;
			}
		}
		
		/// <summary>
		/// The name that will be shown in the preferences to distinguish
		/// between this and other SyncServiceAddins.
		/// </summary>
		public override string Name
		{
			get {
				return Mono.Unix.Catalog.GetString ("WebDAV (wdfs FUSE)");
			}
		}

		/// <summary>
		/// Specifies a unique identifier for this addin.  This will be used to
		/// set the service in preferences.
		/// </summary>
		public override string Id
		{
			get {
				return "wdfs";
			}
		}
		
		/// <summary>
		/// Returns true if the addin has all the supporting libraries installed
		/// on the machine or false if the proper environment is not available.
		/// If false, the preferences dialog will still call
		/// CreatePreferencesControl () when the service is selected.  It's up
		/// to the addin to present the user with what they should install/do so
		/// IsSupported will be true.
		/// </summary>
		public override bool IsSupported
		{
			get {
				// TODO: Figure out a better way to do this!
				if (System.IO.File.Exists ("/usr/bin/wdfs") == true)
					return true;
				
				return false;
			}
		}
		
		#region Private Methods
		private void SetUpMountPath ()
		{
			string notesPath = Tomboy.DefaultNoteManager.NoteDirectoryPath;
			mountPath = Path.Combine (notesPath, "sync-wdfs");
		}
		
		private void CreateMountPath ()
		{
			if (Directory.Exists (mountPath) == false) {
				try {
					Directory.CreateDirectory (mountPath);
				} catch (Exception e) {
					throw new Exception (
						string.Format (
							"Couldn't create \"{0}\" directory: {1}",
							mountPath, e.Message));
				}
			}
		}
		
		/// <summary>
		/// Checks to see if the mount is actually mounted and alive
		/// </summary>
		private bool IsMounted ()
		{
			Process p = new Process ();
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.RedirectStandardOutput = true;
			// TODO: Fix the following because this command might not be in /bin/mount
			p.StartInfo.FileName = "/bin/mount";
			p.StartInfo.CreateNoWindow = true;
			p.Start ();
			string output = p.StandardOutput.ReadToEnd ();
			p.WaitForExit ();
			
			if (p.ExitCode == 1) {
				Logger.Debug ("Error calling /bin/mount");
				return false;
			}
			
			if (output.IndexOf (string.Format ("wdfs on {0}", mountPath)) == -1) {
				Logger.Debug ("{0} not mounted", mountPath);
				return false;
			}
			
			return true;
		}
		
		/// <summary>
		/// Actually attempt to mount the WebDav URL
		///
		/// Execute: wdfs <mount-path> -a <url> -u <username> -p <password> -o fsname=tomboywdfs
		/// </summary>
		private bool MountWebDav (string url, string username, string password)
		{
			if (mountPath == null)
				return false;
			
			if (SyncUtils.IsFuseEnabled () == false) {
				if (SyncUtils.EnableFuse () == false) {
					Logger.Debug ("User canceled or something went wrong enabling fuse in WebDavSyncServiceAddin.MountWebDav");
					return false;
				}
			}
			
			CreateMountPath ();

			Process p = new Process ();
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.RedirectStandardOutput = false;
			p.StartInfo.FileName = "/usr/bin/wdfs";
			p.StartInfo.Arguments =
				string.Format (
					"{0} -a {1} -u {2} -p {3} -o fsname=tomboywdfs",
					mountPath,
					url,
					username,
					password);
			p.StartInfo.CreateNoWindow = true;
			p.Start ();
			p.WaitForExit ();
			
			if (p.ExitCode == 1) {
				Logger.Debug ("Error calling wdfs");
				return false;
			}
			return true;
		}
		
		/// <summary>
		/// Get config settings
		/// </summary>
		private bool GetConfigSettings (out string url, out string username, out string password)
		{
			url = Preferences.Get ("/apps/tomboy/sync_wdfs_url") as String;
			username = Preferences.Get ("/apps/tomboy/sync_wdfs_username") as String;
			password = Preferences.Get ("/apps/tomboy/sync_wdfs_password") as String;
			
			if (url != null && url != string.Empty
					&& username != null && username != string.Empty
					&& password != null && password != string.Empty) {
				return true;
			}
			
			return false;
		}
		#endregion // Private Methods
	}
}
