
using System;
using System.Collections;
using Mono.Unix;
using GConf.PropertyEditors;
using Gnome.Keyring;

namespace Tomboy
{
	public class Preferences
	{
		public const string ENABLE_SPELLCHECKING = "/apps/tomboy/enable_spellchecking";
		public const string ENABLE_WIKIWORDS = "/apps/tomboy/enable_wikiwords";
		public const string ENABLE_CUSTOM_FONT = "/apps/tomboy/enable_custom_font";
		public const string ENABLE_KEYBINDINGS = "/apps/tomboy/enable_keybindings";

		public const string CUSTOM_FONT_FACE = "/apps/tomboy/custom_font_face";
		public const string MENU_NOTE_COUNT = "/apps/tomboy/menu_note_count";
		public const string MENU_PINNED_NOTES = "/apps/tomboy/menu_pinned_notes";

		public const string KEYBINDING_SHOW_NOTE_MENU = "/apps/tomboy/global_keybindings/show_note_menu";
		public const string KEYBINDING_OPEN_START_HERE = "/apps/tomboy/global_keybindings/open_start_here";
		public const string KEYBINDING_CREATE_NEW_NOTE = "/apps/tomboy/global_keybindings/create_new_note";
		public const string KEYBINDING_OPEN_SEARCH = "/apps/tomboy/global_keybindings/open_search";
		public const string KEYBINDING_OPEN_RECENT_CHANGES = "/apps/tomboy/global_keybindings/open_recent_changes";
		
		public const string SHARING_GUID = "/apps/tomboy/sharing/guid";
		public const string SHARING_ENABLE_LOCAL_BROWSING = "/apps/tomboy/sharing/enable_local_browsing";
		public const string SHARING_ENABLE_LOCAL_PUBLISHING = "/apps/tomboy/sharing/enable_local_publishing";
		public const string SHARING_SELECTED_NOTES = "/apps/tomboy/sharing/selected_notes";
		public const string SHARING_SHARED_NAME = "/apps/tomboy/sharing/shared_name";
		public const string SHARING_PASSWORD_DOMAIN = "NoteSharingPassword";
		public const string SYNCHRONIZING_PASSWORD_DOMAIN = "NoteSynchronizingPassword";

		public const string EXPORTHTML_LAST_DIRECTORY = "/apps/tomboy/export_html/last_directory";
		public const string EXPORTHTML_EXPORT_LINKED = "/apps/tomboy/export_html/export_linked";

		public const string STICKYNOTEIMPORTER_FIRST_RUN = "/apps/tomboy/sticky_note_importer/sticky_importer_first_run";

		static GConf.Client client;
		static GConf.NotifyEventHandler changed_handler;

		public static GConf.Client Client 
		{
			get {
				if (client == null) {
					client = new GConf.Client ();

					changed_handler = new GConf.NotifyEventHandler (OnSettingChanged);
					client.AddNotify ("/apps/tomboy", changed_handler);
				}
				return client;
			}
		}

		// NOTE: Keep synced with tomboy.schemas.in
		public static object GetDefault (string key)
		{
			switch (key) {
			case ENABLE_SPELLCHECKING:
			case ENABLE_CUSTOM_FONT:
			case ENABLE_KEYBINDINGS:
				return true;

			case ENABLE_WIKIWORDS:
				return false;

			case CUSTOM_FONT_FACE:
				return "Serif 11";

			case MENU_NOTE_COUNT:
				return 10;

			case MENU_PINNED_NOTES:
				return "";

			case KEYBINDING_SHOW_NOTE_MENU:
				return "<Alt>F12";
				
			case KEYBINDING_OPEN_START_HERE:
				return "<Alt>F11";

			case KEYBINDING_CREATE_NEW_NOTE:
			case KEYBINDING_OPEN_SEARCH:
			case KEYBINDING_OPEN_RECENT_CHANGES:
				return "disabled";
			
			case SHARING_GUID:
				return Guid.NewGuid ().ToString ();
				break;
				
			case SHARING_ENABLE_LOCAL_BROWSING:
			case SHARING_ENABLE_LOCAL_PUBLISHING:
				return false;
			
			case SHARING_SELECTED_NOTES:
				return "";
			
			case SHARING_SHARED_NAME:
				return string.Format (Catalog.GetString ("{0}-{1}'s Notes"), Environment.UserName, Environment.MachineName);

			case EXPORTHTML_EXPORT_LINKED:
				return true;

			case EXPORTHTML_LAST_DIRECTORY:
				return "";

			case STICKYNOTEIMPORTER_FIRST_RUN:
				return true;
			}

			return null;
		}

		public static object Get (string key)
		{
			try {
				return Client.Get (key);
			} catch (GConf.NoSuchKeyException) {
				object default_val = GetDefault (key);

				if (default_val != null)
					Client.Set (key, default_val);

				return default_val;
			}
		}

		public static void Set (string key, object value)
		{
			Client.Set (key, value);
		}
		
		public static void SetPassword (string password_domain, string password)
		{
			if (password_domain == null || password == null)
				return;
			
			string keyring = Ring.GetDefaultKeyring ();
			Hashtable attributes = new Hashtable ();
			attributes ["name"] = password_domain;
			
			Ring.CreateItem (
				keyring,
				ItemType.GenericSecret,
				password_domain,
				attributes,
				password,
				true);
			Logger.Log ("Stored password for \"{0}\"", password_domain);
		}
		
		public static string GetPassword (string password_domain)
		{
			string password = null;
			Hashtable attributes = new Hashtable ();
			attributes ["name"] = password_domain;
			try {
				ItemData [] results = Ring.Find (ItemType.GenericSecret, attributes);
				if (results != null && results.Length > 0) {
					attributes = results [0].Attributes;
					if (attributes != null) {
						password = results [0].Secret;
					}
				}
			} catch (Exception e) {
				Logger.Log ("Error retrieving stored password for \"{0}\": {1}",
					password_domain, e.Message);
			}
			
			return password;
		}
		
		public static void ClearPassword (string password_domain)
		{
			string keyring = Ring.GetDefaultKeyring ();
			Hashtable attributes = new Hashtable ();
			attributes ["name"] = password_domain;
			try {
				foreach (ItemData result in Ring.Find (ItemType.GenericSecret, attributes)) {
					Ring.DeleteItem (keyring, result.ItemID);
					Logger.Log ("Cleared stored password for \"{0}\"", password_domain);
				}
			} catch (Exception e) {
				Logger.Log ("Error clearing stored password for \"{0}\": {1}",
					password_domain, e.Message);
			}
		}
		
		public static void AddSharedNote (Note note)
		{
			string pref = Preferences.Get (Preferences.SHARING_SELECTED_NOTES) as string;
			if (pref == null || pref.CompareTo ("ALL") == 0)
				return;
			
			string [] selected_notes = pref.Split (new char [] {','});
			
			if (selected_notes == null || selected_notes.Length == 0)
				selected_notes = new string [0];
			
			int idx = Array.IndexOf (selected_notes, note.Uri);
			if (idx >= 0)
				return; // The note is already selected
			
			string updated_pref;
			if (pref == String.Empty)
				updated_pref = note.Uri;
			else
				updated_pref = pref + "," + note.Uri;
			Preferences.Set (Preferences.SHARING_SELECTED_NOTES, updated_pref);
		}
		
		public static void RemoveSharedNote (Note note)
		{
			string pref = Preferences.Get (Preferences.SHARING_SELECTED_NOTES) as string;
			if (pref == null || pref.CompareTo ("ALL") == 0)
				return;
			
			string [] selected_notes = pref.Split (new char [] {','});
			
			if (selected_notes == null || selected_notes.Length == 0)
				return;
			
			int idx = Array.IndexOf (selected_notes, note.Uri);
			if (idx < 0)
				return;
			
			// Update the list of selected notes manually
			int j = 0;
			string [] updated_notes = new string [selected_notes.Length - 1];
			for (int i = 0; i < selected_notes.Length; i++) {
				if (i == idx)
					continue;
				
				updated_notes [j] = selected_notes [i];
				j++;
			}
			
			string updated_pref = String.Join (",", updated_notes);
			Preferences.Set (Preferences.SHARING_SELECTED_NOTES, updated_pref);
		}

		public static event GConf.NotifyEventHandler SettingChanged;

		static void OnSettingChanged (object sender, GConf.NotifyEventArgs args)
		{
			if (SettingChanged != null) {
				SettingChanged (sender, args);
			}
		}
	}

	public class PreferencesDialog : Gtk.Dialog
	{
		NoteManager manager;

		Gtk.Button font_button;
		Gtk.Label font_face;
		Gtk.Label font_size;

		// Sharing
		Gtk.CheckButton local_browsing_check;
		Gtk.CheckButton local_publishing_check;
		Gtk.RadioButton all_radio;
		Gtk.RadioButton selected_radio;
		Gtk.TreeView selected_tree;
		Gtk.ListStore selected_store;
		Gtk.CellRendererToggle toggle_renderer;
		Gtk.Label local_shared_name_label;
		Gtk.Entry local_shared_name;
		Gtk.CheckButton require_password_check;
		Gtk.Entry local_publishing_password;
//		Gtk.CheckButton enable_sync_check;
//		Gtk.Label sync_password_label;
//		Gtk.Entry sync_password;
		
		static Type [] column_types =
			new Type [] {
				typeof (bool),		// selected
				typeof (string),	// Note title
				typeof (Note)
			};
		

		public PreferencesDialog (NoteManager manager)
			: base ()
		{
			this.manager = manager;

			IconName = "tomboy";
			HasSeparator = false;
			BorderWidth = 5;
			Resizable = false;
			Title = Catalog.GetString ("Tomboy Preferences");

			VBox.Spacing = 5;
			ActionArea.Layout = Gtk.ButtonBoxStyle.End;


			// Notebook Tabs (Editing, Hotkeys)...

			Gtk.Notebook notebook = new Gtk.Notebook ();
			notebook.TabPos = Gtk.PositionType.Top;
			notebook.BorderWidth = 5;
			notebook.Show ();

			notebook.AppendPage (MakeEditingPane (), 
					     new Gtk.Label (Catalog.GetString ("Editing")));

			notebook.AppendPage (MakeHotkeysPane (), 
					     new Gtk.Label (Catalog.GetString ("Hotkeys")));
			
			notebook.AppendPage (MakeSharingPane (),
					     new Gtk.Label (Catalog.GetString ("Sharing")));

			VBox.PackStart (notebook, true, true, 0);


			// Ok button...
			
			Gtk.Button button = new Gtk.Button (Gtk.Stock.Close);
			button.CanDefault = true;
			button.Show ();

			Gtk.AccelGroup accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);

			button.AddAccelerator ("activate",
					       accel_group,
					       (uint) Gdk.Key.Escape, 
					       0,
					       0);

			AddActionWidget (button, Gtk.ResponseType.Close);
			DefaultResponse = Gtk.ResponseType.Close;
			
			// Update on changes to notes
			manager.NoteDeleted += OnNotesChanged;
			manager.NoteAdded += OnNotesChanged;
			manager.NoteRenamed += OnNoteRenamed;
		}

		// Page 1
		// List of editing options
		public Gtk.Widget MakeEditingPane ()
		{
			Gtk.Label label;
			Gtk.CheckButton check;
			Gtk.Alignment align;
			PropertyEditorBool peditor, font_peditor;
			
			Gtk.VBox options_list = new Gtk.VBox (false, 12);
			options_list.BorderWidth = 12;
			options_list.Show ();


			// Spell checking...

			if (NoteSpellChecker.GtkSpellAvailable) {
				check = MakeCheckButton (
					Catalog.GetString ("_Spell check while typing"));
				options_list.PackStart (check, false, false, 0);
				
				peditor = new PropertyEditorToggleButton (
					Preferences.ENABLE_SPELLCHECKING,
					check);
				SetupPropertyEditor (peditor);

				label = MakeTipLabel (
					Catalog.GetString ("Misspellings will be underlined " +
							   "in red, and correct spelling " +
							   "suggestions shown in the context " +
							   "menu."));
				options_list.PackStart (label, false, false, 0);
			}


			// WikiWords...

			check = MakeCheckButton (Catalog.GetString ("Highlight _WikiWords"));
			options_list.PackStart (check, false, false, 0);

			peditor = new PropertyEditorToggleButton (Preferences.ENABLE_WIKIWORDS, 
								  check);
			SetupPropertyEditor (peditor);

			label = MakeTipLabel (Catalog.GetString ("Enable this option to highlight " +
								 "words <b>ThatLookLikeThis</b>. " +
								 "Clicking the word will create a " +
								 "note with that name."));
			options_list.PackStart (label, false, false, 0);


			// Custom font...

			check = MakeCheckButton (Catalog.GetString ("Use custom _font"));
			options_list.PackStart (check, false, false, 0);

			font_peditor = 
				new PropertyEditorToggleButton (Preferences.ENABLE_CUSTOM_FONT, 
								check);
			SetupPropertyEditor (font_peditor);

			align = new Gtk.Alignment (0.5f, 0.5f, 0.4f, 1.0f);
			align.Show ();
			options_list.PackStart (align, false, false, 0);

			font_button = MakeFontButton ();
			font_button.Sensitive = check.Active;
			align.Add (font_button);
			
			font_peditor.AddGuard (font_button);


			return options_list;
		}

		Gtk.Button MakeFontButton ()
		{
			Gtk.HBox font_box = new Gtk.HBox (false, 0);
			font_box.Show ();
			
			font_face = new Gtk.Label (null);
			font_face.UseMarkup = true;
			font_face.Show ();
			font_box.PackStart (font_face, true, true, 0);

			Gtk.VSeparator sep = new Gtk.VSeparator ();
			sep.Show ();
			font_box.PackStart (sep, false, false, 0);

			font_size = new Gtk.Label (null);
			font_size.Xpad = 4;
			font_size.Show ();
			font_box.PackStart (font_size, false, false, 0);
			
			Gtk.Button button = new Gtk.Button ();
			button.Clicked += OnFontButtonClicked;
			button.Add (font_box);
			button.Show ();

			string font_desc = (string) Preferences.Get (Preferences.CUSTOM_FONT_FACE);
			UpdateFontButton (font_desc);

			return button;
		}

		// Page 2
		// List of Hotkey options
		public Gtk.Widget MakeHotkeysPane ()
		{
			Gtk.Label label;
			Gtk.CheckButton check;
			Gtk.Alignment align;
			Gtk.Entry entry;
			PropertyEditorBool keybind_peditor;
			PropertyEditor peditor;
			
			Gtk.VBox hotkeys_list = new Gtk.VBox (false, 12);
			hotkeys_list.BorderWidth = 12;
			hotkeys_list.Show ();


			// Hotkeys...

			check = MakeCheckButton (Catalog.GetString ("Listen for _Hotkeys"));
			hotkeys_list.PackStart (check, false, false, 0);

			keybind_peditor = 
				new PropertyEditorToggleButton (Preferences.ENABLE_KEYBINDINGS, 
								check);
			SetupPropertyEditor (keybind_peditor);

			label = MakeTipLabel (Catalog.GetString ("Hotkeys allow you to quickly access " +
								 "your notes from anywhere with a keypress. " +
								 "Example Hotkeys: " +
								 "<b>&lt;Control&gt;&lt;Shift&gt;F11</b>, " +
								 "<b>&lt;Alt&gt;N</b>"));
			hotkeys_list.PackStart (label, false, false, 0);

			align = new Gtk.Alignment (0.5f, 0.5f, 0.0f, 1.0f);
			align.Show ();
			hotkeys_list.PackStart (align, false, false, 0);

			Gtk.Table table = new Gtk.Table (4, 2, false);
			table.ColumnSpacing = 6;
			table.RowSpacing = 6;
			table.Show ();
			align.Add (table);


			// Show notes menu keybinding...

			label = MakeLabel (Catalog.GetString ("Show notes _menu"));
			table.Attach (label, 0, 1, 0, 1);

			entry = new Gtk.Entry ();
			entry.Show ();
			table.Attach (entry, 1, 2, 0, 1);

			peditor = new PropertyEditorEntry (Preferences.KEYBINDING_SHOW_NOTE_MENU, 
							   entry);
			SetupPropertyEditor (peditor);

			keybind_peditor.AddGuard (entry);


			// Open Start Here keybinding...

			label = MakeLabel (Catalog.GetString ("Open \"_Start Here\""));
			table.Attach (label, 0, 1, 1, 2);

			entry = new Gtk.Entry ();
			entry.Show ();
			table.Attach (entry, 1, 2, 1, 2);

			peditor = new PropertyEditorEntry (Preferences.KEYBINDING_OPEN_START_HERE, 
							   entry);
			SetupPropertyEditor (peditor);

			keybind_peditor.AddGuard (entry);


			// Create new note keybinding...

			label = MakeLabel (Catalog.GetString ("Create _new note"));
			table.Attach (label, 0, 1, 2, 3);

			entry = new Gtk.Entry ();
			entry.Show ();
			table.Attach (entry, 1, 2, 2, 3);

			peditor = new PropertyEditorEntry (Preferences.KEYBINDING_CREATE_NEW_NOTE, 
							   entry);
			SetupPropertyEditor (peditor);

			keybind_peditor.AddGuard (entry);


			// Search dialog keybinding...

			label = MakeLabel (Catalog.GetString ("S_earch notes"));
			table.Attach (label, 0, 1, 3, 4);

			entry = new Gtk.Entry ();
			entry.Show ();
			table.Attach (entry, 1, 2, 3, 4);

			peditor = new PropertyEditorEntry (Preferences.KEYBINDING_OPEN_SEARCH, 
							   entry);
			SetupPropertyEditor (peditor);

			keybind_peditor.AddGuard (entry);


			return hotkeys_list;
		}
		
		// Page 3
		// Sharing options
		public Gtk.Widget MakeSharingPane ()
		{
			Gtk.Table table;
			Gtk.HBox hbox;
			Gtk.Label label;
			PropertyEditor local_browsing_peditor, local_publishing_peditor, shared_name_peditor;
			
			table = new Gtk.Table (9, 2, false);
			table.BorderWidth = 12;
			table.Show ();

			// Look for shared notes
			local_browsing_check = MakeCheckButton (Catalog.GetString ("_Look for shared notes"));
			table.Attach (local_browsing_check, 0, 2, 0, 1, Gtk.AttachOptions.Shrink | Gtk.AttachOptions.Fill, 0, 0, 0);
			local_browsing_peditor =
				new PropertyEditorToggleButton (Preferences.SHARING_ENABLE_LOCAL_BROWSING,
						local_browsing_check);
			SetupPropertyEditor (local_browsing_peditor);
			
			// Share my notes
			local_publishing_check = MakeCheckButton (Catalog.GetString ("_Share my notes on my local network"));
			table.Attach (local_publishing_check, 0, 2, 1, 2, Gtk.AttachOptions.Shrink | Gtk.AttachOptions.Fill, 0, 0, 0);
			local_publishing_peditor =
				new PropertyEditorToggleButton (Preferences.SHARING_ENABLE_LOCAL_PUBLISHING,
						local_publishing_check);
			SetupPropertyEditor (local_publishing_peditor);
			
			// SPACER
			label = MakeLabel ("    ");
			table.Attach (label, 0, 1, 2, 7);
			
			// Share all notes
			all_radio = MakeRadioButton (null, Catalog.GetString ("Share _all notes"));
			table.Attach (all_radio, 1, 2, 2, 3, Gtk.AttachOptions.Shrink | Gtk.AttachOptions.Fill, 0, 0, 0);
			
			// Share selected notes
			selected_radio = MakeRadioButton (all_radio, Catalog.GetString ("Share s_elected notes"));
			table.Attach (selected_radio, 1, 2, 3, 4, Gtk.AttachOptions.Shrink | Gtk.AttachOptions.Fill, 0, 0, 0);
			
			// Selected notes TreeView
			Gtk.Widget tree = MakeSelectedTree ();
			table.Attach (tree, 1, 2, 4, 5, Gtk.AttachOptions.Expand | Gtk.AttachOptions.Fill, 0, 0, 0);
			
			// Shared name
			hbox = new Gtk.HBox (false, 0);
			local_shared_name_label = MakeLabel (Catalog.GetString ("S_hared name:"));
			hbox.PackStart (local_shared_name_label, false, false, 0);
			local_shared_name = new Gtk.Entry ();
			label.MnemonicWidget = local_shared_name;
			local_shared_name.Show ();
			hbox.PackStart (local_shared_name, true, true, 0);
			hbox.Show ();
			table.Attach (hbox, 1, 2, 5, 6, Gtk.AttachOptions.Shrink | Gtk.AttachOptions.Fill, 0, 0, 0);
			shared_name_peditor = new PropertyEditorEntry (Preferences.SHARING_SHARED_NAME, 
							   local_shared_name);
			SetupPropertyEditor (shared_name_peditor);

			// Require password
			hbox = new Gtk.HBox (false, 0);
			require_password_check = MakeCheckButton (Catalog.GetString ("_Require password:"));
			hbox.PackStart (require_password_check, false, false, 0);
			local_publishing_password = new Gtk.Entry ();
			local_publishing_password.Visibility = false;
			local_publishing_password.FocusOutEvent += OnLocalPasswordFocusOut;
			local_publishing_password.Show ();
			hbox.PackStart (local_publishing_password, true, true, 0);
			hbox.Show ();
			table.Attach (hbox, 1, 2, 6, 7, Gtk.AttachOptions.Shrink | Gtk.AttachOptions.Fill, 0, 0, 0);

/*			
			// Enable synchronization
			enable_sync_check = MakeCheckButton (Catalog.GetString ("_Enable synchronization with my other computers"));
			table.Attach (enable_sync_check, 0, 2, 7, 8, Gtk.AttachOptions.Shrink | Gtk.AttachOptions.Fill, 0, 0, 0);
			
			// Synchronization password
			label = MakeLabel ("    "); // spacer
			table.Attach (label, 0, 1, 8, 9);

			hbox = new Gtk.HBox (false, 0);
			sync_password_label = MakeLabel (Catalog.GetString ("Synchronization _password:"));
			hbox.PackStart (sync_password_label, false, false, 0);
			sync_password = new Gtk.Entry ();
			label.MnemonicWidget = sync_password;
			sync_password.Visibility = false;
			sync_password.FocusOutEvent += OnSyncPasswordFocusOut;
			sync_password.Show ();
			hbox.PackStart (sync_password, true, true, 0);
			hbox.Show ();
			table.Attach (hbox, 1, 2, 8, 9, Gtk.AttachOptions.Shrink | Gtk.AttachOptions.Fill, 0, 0, 0);
*/			
			table.Realized += OnSharingPageRealizedEvent;
			
			return table;
		}
		
		Gtk.Widget MakeSelectedTree ()
		{
			selected_tree = new Gtk.TreeView ();
			selected_tree.HeadersVisible = false;
			selected_tree.RulesHint = false;
			selected_tree.EnableSearch = false;
			
			Gtk.CellRenderer renderer;
			
			Gtk.TreeViewColumn title = new Gtk.TreeViewColumn ();
			title.Sizing = Gtk.TreeViewColumnSizing.Autosize;
			title.Resizable = false;
			
			toggle_renderer = new Gtk.CellRendererToggle ();
			toggle_renderer.Toggled += OnNoteSelected;
			title.PackStart (toggle_renderer, false);
			title.AddAttribute (toggle_renderer, "active", 0 /* selected */);
			
			renderer = new Gtk.CellRendererText ();
			title.PackStart (renderer, true);
			title.AddAttribute (renderer, "text", 1 /* title */);
			title.SortColumnId = 1; /* title */
			
			selected_tree.AppendColumn (title);
			selected_tree.Show ();
			
			Gtk.ScrolledWindow sw = new Gtk.ScrolledWindow ();
			sw.HscrollbarPolicy = Gtk.PolicyType.Automatic;
			sw.VscrollbarPolicy = Gtk.PolicyType.Automatic;
			sw.ShadowType = Gtk.ShadowType.In;
			sw.Add (selected_tree);
			sw.Show ();
			
			return sw;
		}
		
		void OnSharingPageRealizedEvent (object sender, EventArgs args)
		{
			Preferences.SettingChanged += OnSharingSettingChanged;

			string password = Preferences.GetPassword (Preferences.SHARING_PASSWORD_DOMAIN);
			if (password == null)
				require_password_check.Active = false;
			else {
				require_password_check.Active = true;
				local_publishing_password.Text = password;
			}
			
/*
			password = Preferences.GetPassword (Preferences.SYNCHRONIZING_PASSWORD_DOMAIN);
			if (password == null) {
				enable_sync_check.Active = false;
			} else {
				enable_sync_check.Active = true;
				sync_password.Text = password;
			}
*/
			UpdateSharingSelectedList ();
			UpdateSharingSensitivity ();

			all_radio.Toggled += OnLocalShareTypeChanged;
			require_password_check.Toggled += OnRequirePasswordToggled;
//			enable_sync_check.Toggled += OnEnableSyncToggled;
		}
		
		void UpdateSharingSelectedList ()
		{
Logger.Debug ("UpdateSharingSelectedList ()");
			string pref = Preferences.Get (Preferences.SHARING_SELECTED_NOTES) as string;

			if (pref.CompareTo ("ALL") == 0)
				all_radio.Active = true;
			else
				selected_radio.Active = true;
			
			selected_store = new Gtk.ListStore (column_types);
			selected_store.SetSortFunc (1, new Gtk.TreeIterCompareFunc (CompareTitles));
			
			foreach (Note note in manager.Notes) {
				// FIXME: Determine if the note is selected
				if (pref.IndexOf (note.Uri) >= 0)
					selected_store.AppendValues (true, note.Title, note);
				else
					selected_store.AppendValues (false, note.Title, note);
			}
			
			selected_store.SetSortColumnId (1, Gtk.SortType.Ascending);
			selected_tree.Model = selected_store;
		}
		
		void UpdateSharingSensitivity ()
		{
			if (local_publishing_check.Active) {
				all_radio.Sensitive = true;
				selected_radio.Sensitive = true;
				if (all_radio.Active) {
					selected_tree.Sensitive = false;
				} else {
					selected_tree.Sensitive = true;
				}
				local_shared_name_label.Sensitive = true;
				local_shared_name.Sensitive = true;
				require_password_check.Sensitive = true;
				
				if (require_password_check.Active)
					local_publishing_password.Sensitive = true;
				else
					local_publishing_password.Sensitive = false;
			} else {
				// Disable local sharing widgets
				all_radio.Sensitive = false;
				selected_radio.Sensitive = false;
				selected_tree.Sensitive = false;
				local_shared_name_label.Sensitive = false;
				local_shared_name.Sensitive = false;
				require_password_check.Sensitive = false;
				local_publishing_password.Sensitive = false;
			}
			
/*
			if (enable_sync_check.Active) {
				sync_password_label.Sensitive = true;
				sync_password.Sensitive = true;
			} else {
				// Disable synchronization password
				sync_password_label.Sensitive = false;
				sync_password.Sensitive = false;
			}
*/
		}
		
		void OnNotesChanged (object sender, Note changed)
		{
			UpdateSharingSelectedList ();
		}
		
		void OnNoteRenamed (Note note, string old_title)
		{
			UpdateSharingSelectedList ();
		}
		
		void OnSharingSettingChanged (object sender, GConf.NotifyEventArgs args)
		{
			switch (args.Key) {
			case Preferences.SHARING_ENABLE_LOCAL_PUBLISHING:
				bool local_sharing_enabled = (bool) args.Value;
Logger.Debug ("OnSharingSettingChanged: SHARING_ENABLE_LOCAL_PUBLISHING = {0}", local_sharing_enabled);
				if (local_sharing_enabled) {
					// FIXME: Tell mDNS to start publishing local notes
				} else {
					// FIXME: Tell mDNS to stop publishing local notes
				}
				
				UpdateSharingSensitivity ();
				break;
//			case Preferences.SHARING_SELECTED_NOTES:
//				UpdateSharingSelectedList ();
//				break;
			case Preferences.SHARING_SHARED_NAME:
Logger.Debug ("OnSharingSettingChanged: SHARING_SHARED_NAME");
				// FIXME: Re-publish shared name (may have to restart mDNS service)
				break;
			}
		}
		
		void OnLocalShareTypeChanged (object sender, EventArgs args)
		{
			if (all_radio.Active) {
				// Share ALL notes
				Preferences.Set (Preferences.SHARING_SELECTED_NOTES, "ALL");
				selected_tree.Sensitive = false;
			} else {
				// Share selected notes only
				Preferences.Set (Preferences.SHARING_SELECTED_NOTES, "");
				selected_tree.Sensitive = true;
			}

			UpdateSharingSelectedList ();
		}
		
		void OnNoteSelected (object sender, Gtk.ToggledArgs args)
		{
			Gtk.TreeIter iter;
			Gtk.TreePath path = new Gtk.TreePath (args.Path);
			
			if (!selected_store.GetIter (out iter, path))
				return;
			
			Note note = selected_store.GetValue (iter, 2 /* note */) as Note;
			if (note == null)
				return;
			
			bool selected = (bool) selected_store.GetValue (iter, 0 /* selected */);
			
			if (selected) {
				Preferences.RemoveSharedNote (note);
			} else {
				Preferences.AddSharedNote (note);
			}
			
			selected_store.SetValue (iter, 0, !selected);
		}
		
		void OnRequirePasswordToggled (object sender, EventArgs args)
		{
Logger.Debug ("OnRequirePasswordToggled");
			if (require_password_check.Active) {
				local_publishing_password.Sensitive = true;
				local_publishing_password.GrabFocus ();
			} else {
				local_publishing_password.Sensitive = false;
				local_publishing_password.Text = String.Empty;

				// Clear the password from GNOME Keyring
				Preferences.ClearPassword (Preferences.SHARING_PASSWORD_DOMAIN);
			}
		}

		/// <summary>
		/// If there's a password, save it in GnomeKeyring.  If there's not,
		/// tell the user that a password was not set and local publishing
		/// will not require a password.
		/// </summary>		
		void OnLocalPasswordFocusOut (object sender, Gtk.FocusOutEventArgs args)
		{
Logger.Debug ("OnLocalPasswordFocusOut");
			string password = local_publishing_password.Text.Trim ();
			if (password == String.Empty) {
				// For now, do not prompt the user, just disable the password
//				Gtk.MessageDialog md = new Gtk.MessageDialog (this,
//					Gtk.DialogFlags.DestroyWithParent | Gtk.DialogFlags.Modal,
//					Gtk.MessageType.Question,
//					Gtk.ButtonsType.YesNo,
//					Catalog.GetString ("Share your notes without a password?"));
//				md.Modal = true;
//				Gtk.ResponseType result = (Gtk.ResponseType) md.Run ();
//				md.Destroy ();
//				if (result == Gtk.ResponseType.Yes) {
					require_password_check.Active = false;
//				} else {
//					local_publishing_password.GrabFocus ();
//				}
			} else {
				Preferences.SetPassword (Preferences.SHARING_PASSWORD_DOMAIN, password);
			}
		}
		
/*
		void OnEnableSyncToggled (object sender, EventArgs args)
		{
			if (enable_sync_check.Active) {
				sync_password_label.Sensitive = true;
				sync_password.Sensitive = true;
				sync_password.GrabFocus ();
			} else {
				sync_password_label.Sensitive = false;
				sync_password.Sensitive = false;
				sync_password.Text = String.Empty;

				// Clear the password from GNOME Keyring
				Preferences.ClearPassword (Preferences.SYNCHRONIZING_PASSWORD_DOMAIN);
			}
		}
		void OnSyncPasswordFocusOut (object sender, Gtk.FocusOutEventArgs args)
		{
			string password = sync_password.Text.Trim ();
			if (password == String.Empty) {
				// For now, do not prompt the user, just disable synchronization
//				Gtk.MessageDialog md = new Gtk.MessageDialog (this,
//					Gtk.DialogFlags.DestroyWithParent | Gtk.DialogFlags.Modal,
//					Gtk.MessageType.Question,
//					Gtk.ButtonsType.YesNo,
//					Catalog.GetString ("You cannot synchronize your notes without a password.  Disable synchronization?"));
//				md.Modal = true;
//				Gtk.ResponseType result = (Gtk.ResponseType) md.Run ();
//				md.Destroy ();
//				if (result == Gtk.ResponseType.Yes) {
					enable_sync_check.Active = false;
//				} else {
//					sync_password.GrabFocus ();
//				}
			} else {
				Preferences.SetPassword (Preferences.SYNCHRONIZING_PASSWORD_DOMAIN, password);
			}
		}
*/		
		
		int CompareTitles (Gtk.TreeModel model, Gtk.TreeIter a, Gtk.TreeIter b)
		{
			string title_a = model.GetValue (a, 1 /* title */) as string;
			string title_b = model.GetValue (b, 1 /* title */) as string;
			
			if (title_a == null || title_b == null)
				return -1;
			
			return title_a.CompareTo (title_b);
		}

		void SetupPropertyEditor (PropertyEditor peditor)
		{
			// Ensure the key exists
			Preferences.Get (peditor.Key);
			peditor.Setup ();
		}

		// Utilities...

		Gtk.Label MakeLabel (string label_text)
		{
			Gtk.Label label = new Gtk.Label (label_text);
			label.UseMarkup = true;
			label.Justify = Gtk.Justification.Left;
			label.SetAlignment (0.0f, 0.5f);
			label.Show ();

			return label;
		}

		Gtk.CheckButton MakeCheckButton (string label_text)
		{
			Gtk.Label label = MakeLabel (label_text);

			Gtk.CheckButton check = new Gtk.CheckButton ();
			check.Add (label);
			check.Show ();

			return check;
		}

		Gtk.Label MakeTipLabel (string label_text)
		{
			Gtk.Label label =  MakeLabel (String.Format ("<small>{0}</small>", 
								     label_text));
			label.LineWrap = true;
			label.Xpad = 20;
			return label;
		}
		
		Gtk.RadioButton MakeRadioButton (Gtk.RadioButton group_button, string label_text)
		{
			Gtk.RadioButton button;
			
			if (group_button != null)
				button = new Gtk.RadioButton (group_button, label_text);
			else
				button = new Gtk.RadioButton (label_text);
			button.Show ();
			
			return button;
		}

		// Font Change handler

		void OnFontButtonClicked (object sender, EventArgs args)
		{
			Gtk.FontSelectionDialog font_dialog = 
				new Gtk.FontSelectionDialog (Catalog.GetString ("Choose Note Font"));

			string font_name = (string) Preferences.Get (Preferences.CUSTOM_FONT_FACE);
			font_dialog.SetFontName (font_name);

			if ((int) Gtk.ResponseType.Ok == font_dialog.Run ()) {
				if (font_dialog.FontName != font_name) {
					Preferences.Set (Preferences.CUSTOM_FONT_FACE, 
							 font_dialog.FontName);

					UpdateFontButton (font_dialog.FontName);
				}
			}

			font_dialog.Destroy ();
		}

		void UpdateFontButton (string font_desc)
		{
			Pango.FontDescription desc = Pango.FontDescription.FromString (font_desc);

			// Set the size label
			font_size.Text = (desc.Size / Pango.Scale.PangoScale).ToString ();

			desc.UnsetFields (Pango.FontMask.Size);

			// Set the font name label
			font_face.Markup = String.Format ("<span font_desc='{0}'>{1}</span>",
							  font_desc,
							  desc.ToString ());
		}
	}
}
