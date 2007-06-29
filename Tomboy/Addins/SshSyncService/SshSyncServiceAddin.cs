using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Gtk;

using Mono.Unix;

using Tomboy;

namespace Tomboy.Sync
{
	public class SshSyncServiceAddin : SyncServiceAddin
	{
		// TODO: Extract most of the code here and build GenericSyncServiceAddin
		// that supports a field, a username, and password.  This could be useful
		// in quickly building SshSyncServiceAddin, FtpSyncServiceAddin, etc.
		
		Entry serverEntry;
		Entry folderEntry;
		Entry usernameEntry;
		Entry passwordEntry;
		
		string mountPath;
		
		InterruptableTimeout unmountTimeout;
		
		/// <summary>
		/// Called as soon as Tomboy needs to do anything with the service
		/// </summary>
		public override void Initialize ()
		{
//			if (IsConfigured) {
				// Make sure the mount is loaded
				SetUpMountPath ();
//			}
			unmountTimeout = new InterruptableTimeout ();
			unmountTimeout.Timeout += UnmountTimeout;
		}

		/// <summary>
		/// Creates a SyncServer instance that the SyncManager can use to
		/// synchronize with this service.  This method is called during
		/// every synchronization process.  If the same SyncServer object
		/// is returned here, it should be reset as if it were new.
		/// </summary>
		public override SyncServer CreateSyncServer ()
		{
			unmountTimeout.Cancel (); // Prevent unmount during sync
			SyncServer server = null;
			
			string url, folder, username, password;
			if (GetConfigSettings (out url, out folder, out username, out password)) {
				if (IsMounted () == false) {
					if (MountSshfs (url, folder, username, password) == false) {
						throw new Exception ("Could not mount " + mountPath);
					}
				}

				server = new FileSystemSyncServer (mountPath);
			} else {
				throw new InvalidOperationException ("SshSyncServiceAddin.CreateSyncServer () called without being configured");
			}
			
			return server;
		}
		
		public override void PostSyncCleanup ()
		{
			// Try unmounting in five minutes
			// TODO: Ensure that this happens before Tomboy shuts down
			unmountTimeout.Reset (1000 * 60 * 5);
		}
		
		private void UnmountTimeout (object sender, System.EventArgs e)
		{
			Process p = new Process ();
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.RedirectStandardOutput = false;
			p.StartInfo.FileName = "/usr/bin/fusermount";
			p.StartInfo.Arguments =
				string.Format (
					"-u {0}",
					mountPath);
			p.StartInfo.CreateNoWindow = true;
			p.Start ();
			p.WaitForExit ();
			
			if (p.ExitCode == 1) {
				Logger.Debug ("Error unmounting " + Id);
				unmountTimeout.Reset (1000 * 60 * 5); // Try again in five minutes
			}
			else {
				Logger.Debug ("Successfully unmounted " + Id);
				unmountTimeout.Cancel ();
			}
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
			string server = Preferences.Get ("/apps/tomboy/sync_sshfs_server") as String;
			string folder = Preferences.Get ("/apps/tomboy/sync_sshfs_folder") as String;
			string username = Preferences.Get ("/apps/tomboy/sync_sshfs_username") as String;
			string password = Preferences.Get ("/apps/tomboy/sync_sshfs_password") as String;
			if (server == null)
				server = string.Empty;
			if (folder == null)
				folder = string.Empty;
			if (username == null)
				username = string.Empty;
			if (password == null)
				password = string.Empty;
			
			bool activeSyncService = server != string.Empty || folder != string.Empty ||
				username != string.Empty || password != string.Empty;
			
			Label l = new Label (Catalog.GetString ("Server:"));
			l.Xalign = 1;
			l.Show ();
			table.Attach (l, 0, 1, 0, 1);
			
			serverEntry = new Entry ();
			serverEntry.Text = server;
			serverEntry.Show ();
			table.Attach (serverEntry, 1, 2, 0, 1);
			
			l = new Label (Catalog.GetString ("Folder (optional):"));
			l.Xalign = 1;
			l.Show ();
			table.Attach (l, 0, 1, 1, 2);
			
			folderEntry = new Entry ();
			folderEntry.Text = folder;
			folderEntry.Show ();
			table.Attach (folderEntry, 1, 2, 1, 2);
			
			l = new Label (Catalog.GetString ("Username:"));
			l.Xalign = 1;
			l.Show ();
			table.Attach (l, 0, 1, 2, 3);
			
			usernameEntry = new Entry ();
			usernameEntry.Text = username;
			usernameEntry.Show ();
			table.Attach (usernameEntry, 1, 2, 2, 3);
			
			l = new Label (Catalog.GetString ("Password:"));
			l.Xalign = 1;
			l.Show ();
			table.Attach (l, 0, 1, 3, 4);
			l.Sensitive = false;
			
			passwordEntry = new Entry ();
			passwordEntry.Text = password;
			passwordEntry.Visibility = false;
			passwordEntry.Show ();
			table.Attach (passwordEntry, 1, 2, 3, 4);
			passwordEntry.Sensitive = false;
			
			table.Sensitive = !activeSyncService;
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
			string server = serverEntry.Text.Trim ();
			string folder = folderEntry.Text.Trim ();
			string username = usernameEntry.Text.Trim ();
			string password = passwordEntry.Text.Trim ();
			
			if (server == string.Empty
						|| username == string.Empty) {
				// TODO: Figure out a way to send the error back to the client
				Logger.Debug ("One of url, username was empty");
				return false;
			}
			
			SetUpMountPath ();
			
			// TODO: Check to see if the mount is already mounted
			bool mounted = MountSshfs (server, folder, username, password);
			
			if (mounted) {
				PostSyncCleanup ();
				Preferences.Set ("/apps/tomboy/sync_sshfs_server", server);
				Preferences.Set ("/apps/tomboy/sync_sshfs_folder", folder);
				Preferences.Set ("/apps/tomboy/sync_sshfs_username", username);
				// TODO: MUST FIX THIS.  DO NOT STORE CLEAR TEXT PASSWORD IN GCONF!
				Preferences.Set ("/apps/tomboy/sync_sshfs_password", password);
			}
			
			return mounted;
		}

		/// <summary>
		/// Reset the configuration so that IsConfigured will return false.
		/// </summary>
		public override void ResetConfiguration ()
		{
			Preferences.Set ("/apps/tomboy/sync_sshfs_server", string.Empty);
			Preferences.Set ("/apps/tomboy/sync_sshfs_folder", string.Empty);
			Preferences.Set ("/apps/tomboy/sync_sshfs_username", string.Empty);
			Preferences.Set ("/apps/tomboy/sync_sshfs_password", string.Empty);
		}
		
		/// <summary>
		/// Returns whether the addin is configured enough to actually be used.
		/// </summary>
		public override bool IsConfigured
		{
			get {
				string server = Preferences.Get ("/apps/tomboy/sync_sshfs_server") as String;
				string folder = Preferences.Get ("/apps/tomboy/sync_sshfs_folder") as String;
				string username = Preferences.Get ("/apps/tomboy/sync_sshfs_username") as String;
				string password = Preferences.Get ("/apps/tomboy/sync_sshfs_password") as String;
				
				if (server != null && server != string.Empty
						&& username != null && username != string.Empty) {
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
				return Mono.Unix.Catalog.GetString ("SSH (sshfs FUSE)");
			}
		}

		/// <summary>
		/// Specifies a unique identifier for this addin.  This will be used to
		/// set the service in preferences.
		/// </summary>
		public override string Id
		{
			get {
				return "sshfs";
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
				if (System.IO.File.Exists ("/usr/bin/sshfs") == true)
					return true;
				
				return false;
			}
		}
		
		#region Private Methods
		private void SetUpMountPath ()
		{
			string notesPath = Tomboy.DefaultNoteManager.NoteDirectoryPath;
			mountPath = Path.Combine (notesPath, "sync-sshfs");
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
			List<string> outputLines = new List<string> ();
			string line;
			while (!p.StandardOutput.EndOfStream) {
				line = p.StandardOutput.ReadLine ();
				outputLines.Add (line);
			}
			p.WaitForExit ();
			
			if (p.ExitCode == 1) {
				Logger.Debug ("Error calling /bin/mount");
				return false;
			}
			
			foreach (string outputLine in outputLines)
				if (outputLine.StartsWith ("sshfs") &&
				    outputLine.IndexOf (string.Format ("on {0} ", mountPath)) > -1)
					return true;
			
			return false;
		}
		
		/// <summary>
		/// Actually attempt to mount the sshfs URL
		///
		/// Execute: TODO
		/// </summary>
		private bool MountSshfs (string server, string folder, string username, string password)
		{
			if (mountPath == null)
				return false;
			
			if (SyncUtils.IsFuseEnabled () == false) {
				if (SyncUtils.EnableFuse () == false) {
					Logger.Debug ("User canceled or something went wrong enabling fuse in SshSyncServiceAddin.MountSshfs");
					return false;
				}
			}
			
			CreateMountPath ();

			Process p = new Process ();
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.RedirectStandardOutput = false;
			p.StartInfo.FileName = "/usr/bin/sshfs";
			p.StartInfo.Arguments =
				string.Format (
					"{0}@{1}:{2} {3}",
					username,
					server,
				        folder,
					mountPath);
			p.StartInfo.CreateNoWindow = true;
			p.Start ();
			p.WaitForExit ();
	
			
			// TODO: Handle password
			
			if (p.ExitCode == 1) {
				Logger.Debug ("Error calling sshfs");
				return false;
			}
			return true;
		}
		
		/// <summary>
		/// Get config settings
		/// </summary>
		private bool GetConfigSettings (out string server, out string folder, out string username, out string password)
		{
			server = Preferences.Get ("/apps/tomboy/sync_sshfs_server") as String;
			folder = Preferences.Get ("/apps/tomboy/sync_sshfs_folder") as String;
			username = Preferences.Get ("/apps/tomboy/sync_sshfs_username") as String;
			password = Preferences.Get ("/apps/tomboy/sync_sshfs_password") as String;
			
			if (server != null && server != string.Empty
					&& username != null && username != string.Empty) {
				return true;
			}
			
			return false;
		}
		#endregion // Private Methods
	}
}
