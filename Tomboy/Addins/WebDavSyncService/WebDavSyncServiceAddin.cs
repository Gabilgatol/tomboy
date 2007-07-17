using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Gtk;

using Mono.Unix;

using Tomboy;

namespace Tomboy.Sync
{
	public class WebDavSyncServiceAddin : FuseSyncServiceAddin
	{
		Entry urlEntry;
		Entry usernameEntry;
		Entry passwordEntry;
		
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
			
			bool activeSyncService = url != string.Empty || username != string.Empty ||
				password != string.Empty;
			
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
			
			table.Sensitive = !activeSyncService;
			table.Show ();
			return table;
		}
		
		protected override bool VerifyConfiguration ()
		{
			string url, username, password;
			
			if (!GetPrefWidgetSettings (out url, out username, out password)) {
				// TODO: Figure out a way to send the error back to the client
				Logger.Debug ("One of url, username, or password was empty");
				return false;
			}
			
			return true;
		}
		
		protected override void SaveConfigurationValues ()
		{
			string url, username, password;
			GetPrefWidgetSettings (out url, out username, out password);
			
			Preferences.Set ("/apps/tomboy/sync_wdfs_url", url);
			Preferences.Set ("/apps/tomboy/sync_wdfs_username", username);
			// TODO: MUST FIX THIS.  DO NOT STORE CLEAR TEXT PASSWORD IN GCONF!
			Preferences.Set ("/apps/tomboy/sync_wdfs_password", password);
		}

		/// <summary>
		/// Reset the configuration so that IsConfigured will return false.
		/// </summary>
		protected override void ResetConfigurationValues ()
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
				string url, username, password;				
				return GetConfigSettings (out url, out username, out password);
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
		
		protected override string GetFuseMountExeArgs (string mountPath, bool fromStoredValues)
		{
			string url, username, password;
			if (fromStoredValues)
				GetConfigSettings (out url, out username, out password);
			else
				GetPrefWidgetSettings (out url, out username, out password);
			return string.Format ("{0} -a {1} -u {2} -p {3} -o fsname=tomboywdfs",
			                      mountPath,
			                      url,
			                      username,
			                      password);
		}

		protected override string FuseMountExeName {
			get { return "wdfs"; }
		}
		
		#region Private Methods
		/// <summary>
		/// Get config settings
		/// </summary>
		private bool GetConfigSettings (out string url, out string username, out string password)
		{
			url = Preferences.Get ("/apps/tomboy/sync_wdfs_url") as String;
			username = Preferences.Get ("/apps/tomboy/sync_wdfs_username") as String;
			password = Preferences.Get ("/apps/tomboy/sync_wdfs_password") as String;
				
			return !string.IsNullOrEmpty (url)
					&& !string.IsNullOrEmpty (username)
					&& !string.IsNullOrEmpty (password);
		}

		
		/// <summary>
		/// Get config settings
		/// </summary>
		private bool GetPrefWidgetSettings (out string url, out string username, out string password)
		{
			url = urlEntry.Text.Trim ();
			username = usernameEntry.Text.Trim ();
			password = passwordEntry.Text.Trim ();
				
			return !string.IsNullOrEmpty (url)
					&& !string.IsNullOrEmpty (username)
					&& !string.IsNullOrEmpty (password);
		}
		#endregion // Private Methods
	}
}
