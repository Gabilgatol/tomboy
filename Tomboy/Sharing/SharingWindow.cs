using System;
using Gtk;
using Mono.Unix;

namespace Tomboy.Sharing
{
	public class SharingWindow : Gtk.Window
	{
		Gtk.AccelGroup accel_group;
		Gtk.Button close_button;
		Gtk.ScrolledWindow nodes_window;
		Gtk.VBox content_vbox;

		Gtk.TreeView tree;
		Gtk.TreeModelFilter store_filter;
		Gtk.TreeModelSort store_sort;
		
		Gtk.Button download_note_button;
		
		NoteManager manager;
		SharingManager sharing_manager;
		
		public SharingWindow (NoteManager manager, SharingManager sharing_manager)
			: base (Catalog.GetString ("Shared Notes"))
		{
			this.manager = manager;
			this.sharing_manager = sharing_manager;
			this.IconName = "tomboy";
			this.DefaultWidth = 200;
			this.DefaultHeight = 400;

			// For Escape (Close)
			accel_group = new Gtk.AccelGroup ();
			AddAccelGroup (accel_group);

			Gtk.Image image = new Gtk.Image (Gtk.Stock.Refresh, 
							 Gtk.IconSize.Dialog);

			Gtk.Label label = new Gtk.Label (Catalog.GetString (
				"<span size=\"larger\"><b>Note Sharing</b></span>\n" +
				"Browse for shared notes on your local network\n\n" +
				"Double-click a note to download and open it."));
			label.UseMarkup = true;
			label.Wrap = true;

			Gtk.HBox hbox = new Gtk.HBox (false, 2);
			hbox.BorderWidth = 8;
			hbox.PackStart (image, false, false, 4);
			hbox.PackStart (label, false, false, 0);
			hbox.ShowAll ();

			MakeNodesTree ();
			tree.Show ();

			// Set up the TreeModel
			store_filter = new Gtk.TreeModelFilter (sharing_manager.ShareNodes, null);
			store_filter.VisibleFunc = FilterNodes;
			store_sort = new Gtk.TreeModelSort (store_filter);
			store_sort.SetSortFunc (0, new Gtk.TreeIterCompareFunc (CompareNodes));
			store_sort.SetSortColumnId (0, Gtk.SortType.Ascending);
			
//			store = new Gtk.ListStore (typeof (ShareNode));
//			store.SetSortFunc (0, new Gtk.TreeIterCompareFunc (CompareNodes));
//			store.SetSortColumnId (0, Gtk.SortType.Ascending);

			tree.Model = store_sort;

			nodes_window = new Gtk.ScrolledWindow ();
			nodes_window.ShadowType = Gtk.ShadowType.In;
			nodes_window.HscrollbarPolicy = Gtk.PolicyType.Automatic;
			nodes_window.VscrollbarPolicy = Gtk.PolicyType.Automatic;

/*
			// Reign in the window size if there are notes with long
			// names, or a lot of notes...

			Gtk.Requisition tree_req = tree.SizeRequest ();
			if (tree_req.Height > 420)
				nodes_window.HeightRequest = 420;
			else
				nodes_window.VscrollbarPolicy = Gtk.PolicyType.Never;

			if (tree_req.Width > 480)
				nodes_window.WidthRequest = 480;
			else
				nodes_window.HscrollbarPolicy = Gtk.PolicyType.Never;
*/

			nodes_window.Add (tree);
			nodes_window.Show ();
			
			download_note_button = new Gtk.Button (Catalog.GetString ("_Download"));
			download_note_button.Image = new Gtk.Image (Gtk.Stock.GoDown, Gtk.IconSize.Button);
			download_note_button.Clicked += DownloadNote;
			download_note_button.Sensitive = false;
			download_note_button.Show ();

			Gtk.HButtonBox tree_button_box = new Gtk.HButtonBox ();
			tree_button_box.Layout = Gtk.ButtonBoxStyle.Start;
			tree_button_box.Spacing = 8;
			tree_button_box.PackStart (download_note_button);
			tree_button_box.Show ();

			close_button = new Gtk.Button (Gtk.Stock.Close);
			close_button.Clicked += CloseClicked;
			close_button.AddAccelerator ("activate",
						     accel_group,
						     (uint) Gdk.Key.Escape, 
						     0,
						     Gtk.AccelFlags.Visible);
			close_button.Show ();
			
			Gtk.HButtonBox button_box = new Gtk.HButtonBox ();
			button_box.Layout = Gtk.ButtonBoxStyle.End;
			button_box.Spacing = 8;
			button_box.PackStart (close_button);
			button_box.Show ();

			content_vbox = new Gtk.VBox (false, 8);
			content_vbox.BorderWidth = 6;
			content_vbox.PackStart (hbox, false, false, 0);
			content_vbox.PackStart (nodes_window);
			content_vbox.PackStart (tree_button_box, false, true, 0);
			content_vbox.PackStart (button_box, false, true, 0);
			content_vbox.Show ();

			tree.Selection.Changed += OnSelectionChanged;

			this.Add (content_vbox);
		}

		void MakeNodesTree ()
		{
			tree = new Gtk.TreeView ();
			tree.HeadersVisible = false;
			tree.RulesHint = false;
			tree.RowActivated += OnRowActivated;
			tree.RowExpanded += OnRowExpanded;
//			tree.RowCollapsed += OnRowCollapsed;  // FIXME: Implement this to put the dummy node back

			Gtk.CellRenderer renderer;
			
			Gtk.TreeViewColumn name = new Gtk.TreeViewColumn ();
			name.Sizing = Gtk.TreeViewColumnSizing.Autosize;
			name.Resizable = true;
			
			renderer = new Gtk.CellRendererPixbuf ();
			name.PackStart (renderer, false);
			name.SetCellDataFunc (renderer, new TreeCellDataFunc (PixbufDataFunc));

			renderer = new Gtk.CellRendererText ();
			name.PackStart (renderer, true);
			name.SetCellDataFunc (renderer, new TreeCellDataFunc (NameDataFunc));
			name.SortColumnId = 0; /* computer name/note title */

			tree.AppendColumn (name);

			Gtk.TreeViewColumn status = new Gtk.TreeViewColumn ();
			status.Sizing = Gtk.TreeViewColumnSizing.Autosize;
			status.Resizable = true;

			renderer = new Gtk.CellRendererText ();
			renderer.Data ["xalign"] = 0.0;
			status.PackStart (renderer, false);
			status.SetCellDataFunc (renderer, new TreeCellDataFunc (StatusDataFunc));
			status.SortColumnId = 2; /* change date */

			tree.AppendColumn (status);
		}

		void PixbufDataFunc (TreeViewColumn column, CellRenderer renderer,
							 TreeModel model, TreeIter iter)
		{
			CellRendererPixbuf crp = renderer as CellRendererPixbuf;
			
			ShareNode node = model.GetValue (iter, 0) as ShareNode;
			if (node == null)
				crp.Pixbuf = null;
			else
				crp.Pixbuf = node.Pixbuf;
		}

		void NameDataFunc (TreeViewColumn column, CellRenderer renderer,
						   TreeModel model, TreeIter iter)
		{
			CellRendererText crt = renderer as CellRendererText;
			
			ShareNode node = model.GetValue (iter, 0) as ShareNode;
			if (node == null)
				crt.Text = String.Empty;
			else
				crt.Text = node.Name;
		}

		void StatusDataFunc (TreeViewColumn column, CellRenderer renderer,
							 TreeModel model, TreeIter iter)
		{
			CellRendererText crt = renderer as CellRendererText;

			ShareNode node = model.GetValue (iter, 0) as ShareNode;
			if (node == null)
				crt.Text = String.Empty;
			else
				crt.Text = node.Status;
		}
		
		ShareNode GetSelectedNode ()
		{
			Gtk.TreeModel model;
			Gtk.TreeIter iter;

			if (!tree.Selection.GetSelected (out model, out iter))
				return null;

			return model.GetValue (iter, 0 /* node */) as ShareNode;
		}
		
		void DownloadNote (object sender, EventArgs args)
		{
Logger.Debug ("SharingWindow.DownloadNote");
			ShareNode selected_node = GetSelectedNode ();
			if (selected_node == null)
				return;
				
			NoteFullInfo full_info = sharing_manager.DownloadNote (selected_node as NoteShareNode);
			if (full_info == null)
				return;
			
			string local_note_name = full_info.Name;
			int creation_attempts = 0;
			
			createNoteLocally:
			
			try {
				creation_attempts++;
				Note note = manager.Create (local_note_name, full_info.XmlContent);
				if (note != null)
					note.Window.Present ();
			} catch (Exception e) {
Logger.Debug ("Original content: {0}", full_info.XmlContent);
				// Yuck!  We have to parse the exception message to find out
				// if it's what we're looking for.  This should really be changed
				// to throw a named exception.  (FIXME)
				if (e.Message.IndexOf ("A note with this title already exists") >= 0) {
Logger.Debug ("SharingWindow.DownloadNote: 0");
					
					string changed_name = CreateNewLocalName (local_note_name, creation_attempts);
Logger.Debug ("SharingWindow.DownloadNote: 1");
					if (changed_name != null) {
Logger.Debug ("SharingWindow.DownloadNote: 2");
						// Update the title in the XML data
						int title_start_pos = full_info.XmlContent.IndexOf (local_note_name);
Logger.Debug ("SharingWindow.DownloadNote: 3");
						if (title_start_pos > 0) {
Logger.Debug ("SharingWindow.DownloadNote: 4");
							string temp_str = full_info.XmlContent.Remove (title_start_pos, local_note_name.Length);
Logger.Debug ("temp_str = \n{0}", temp_str);
							full_info.XmlContent = temp_str.Insert (title_start_pos, changed_name);
Logger.Debug ("full_info.XmlContent =\n{0}", full_info.XmlContent);
							local_note_name = changed_name;
							goto createNoteLocally;
						}
					}
				} else {
					Logger.Error ("Exception occurred while attempting to save downloaded note: {0}", e.Message);
				}
			}
		}
		
		string CreateNewLocalName (string current_name, int attempts)
		{
Logger.Debug ("CreateNewLocalName ({0}, {1}", current_name, attempts);
			string updated_name = null;

			if (attempts == 1) {
				// This is the first time a note has been renamed, so add a space
				// and the attempt number.
				updated_name = string.Format ("{0} {1}", current_name, attempts);
			} else {
				int last_space_pos = current_name.LastIndexOf (' ');
				if (last_space_pos > 0) {
					updated_name = string.Format ("{0} {1}",
						current_name.Substring (0, last_space_pos),
						attempts);
				}
			}

Logger.Debug ("    -> {0}", updated_name);			
			return updated_name;
		}

		void CloseClicked (object sender, EventArgs args)
		{
			Hide ();
			Destroy ();
		}
		
		bool FilterNodes (Gtk.TreeModel model, Gtk.TreeIter iter)
		{
			// FIXME: Implement FilterNodes to filter based on the search/filter string (which hasn't been added yet to the window)
			return true;
		}

		int CompareNodes (Gtk.TreeModel model, Gtk.TreeIter a, Gtk.TreeIter b)
		{
			ShareNode node_a = model.GetValue (a, 0) as ShareNode;
			ShareNode node_b = model.GetValue (b, 0) as ShareNode;
			
			if (node_a == null || node_b == null)
				return -1;

			string name_a = node_a.Name;
			string name_b = node_b.Name;
			
			if (name_a == null || name_b == null)
				return -1;
			else
				return name_a.CompareTo (name_b);
		}

		void OnRowActivated (object sender, Gtk.RowActivatedArgs args)
		{
			Gtk.TreeIter iter;
			if (!store_sort.GetIter (out iter, args.Path)) 
				return;

			ShareNode node = store_sort.GetValue (iter, 0) as ShareNode;
			
			if (node is NoteShareNode)
				DownloadNote (sender, EventArgs.Empty);
		}
		
		void OnRowExpanded (object sender, Gtk.RowExpandedArgs args)
		{
Logger.Debug ("OnRowExpanded ()");
			TomboyShareNode tomboy_share_node = store_sort.GetValue (args.Iter, 0) as TomboyShareNode;
			if (tomboy_share_node == null)
				return;
			
Logger.Debug ("    {0}", tomboy_share_node.Name);
			// FIXME: Add a try/catch around this so we know when credentials are needed and other problems
			sharing_manager.Connect (tomboy_share_node);
			sharing_manager.LoadSharedNotes (tomboy_share_node);
			
			// Weird voodoo magic to have the tree expand properly.  During
			// sharing_manager.LoadSharedNotes, the children of the tomboy_share_node are
			// all deleted, which makes the node NOT expand.  So, once the
			// call to LoadSharedNotes is done, there will either be a bunch of
			// shared notes, or a dummy node saying nothing was found, but to get
			// it to show up, we have to expand the row again (without having it
			// call OnRowExpanded () automatically).
			tree.RowExpanded -= OnRowExpanded; // Disable the handler temporarily
			
			tree.ExpandRow (args.Path, true);
			
			tree.RowExpanded += OnRowExpanded; // Re-enable the handler
		}
		
		void OnSelectionChanged (object sender, EventArgs args)
		{
			ShareNode node = GetSelectedNode ();
			if (node != null && node is NoteShareNode)
				download_note_button.Sensitive = true;
			else
				download_note_button.Sensitive = false;
		}
	}
}
