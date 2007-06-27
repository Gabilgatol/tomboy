using System;
using Mono.Unix;

namespace Tomboy
{
	public class SyncDialog : Gtk.Dialog
	{
		private Gtk.Button syncButton;
		private Gtk.ProgressBar progressBar;
		private Gtk.Expander expander;
		private Gtk.Button closeButton;
		
		private Gtk.ListStore model;
		
		// TODO: Possible to make Tomboy not crash if quit while dialog is up?
		public SyncDialog () : base ("Synchronization Progress", null, Gtk.DialogFlags.DestroyWithParent)
		{
			SetSizeRequest (400, -1);
			
			VBox.PackStart (new Gtk.Label ("Tomboy synchronization is currently in progress"),
			                false, false, 5);
			
			syncButton = new Gtk.Button (new Gtk.Label (Catalog.GetString ("Synchronize Now")));
			syncButton.Clicked += OnSynchronizeButton;
			syncButton.Show ();
			VBox.PackStart (syncButton, false, false, 0);
			
			progressBar = new Gtk.ProgressBar ();
			//progressBar.Text = "Contacting Server...";
			progressBar.Orientation = Gtk.ProgressBarOrientation.LeftToRight;				
			VBox.PackStart (progressBar, false, false, 0);
			
			// Create model for TreeView
			model = new Gtk.ListStore (typeof (string), typeof (string));
			
			// Create TreeView, attach model
			Gtk.TreeView treeView = new Gtk.TreeView (model);
			treeView.Model = model;
			
			// Set up TreeViewColumns
			Gtk.TreeViewColumn column = new Gtk.TreeViewColumn (
					Catalog.GetString ("Note Title"),
					new Gtk.CellRendererText (), "text", 0);
			column.SortColumnId = 0;
			column.Resizable = true;
			treeView.AppendColumn (column);
			
			column = new Gtk.TreeViewColumn (
					Catalog.GetString ("Status"),
					new Gtk.CellRendererText (), "text", 1);
			column.SortColumnId = 1;
			column.Resizable = true;
			treeView.AppendColumn (column);
			
			treeView.Show ();
			
			// Drop TreeView into a ScrolledWindow into a VBox
			Gtk.ScrolledWindow scrolledWindow = new Gtk.ScrolledWindow ();
			scrolledWindow.SetSizeRequest (-1, 200);
			scrolledWindow.Add (treeView);
			scrolledWindow.Show ();
			Gtk.VBox expandVBox = new Gtk.VBox ();
			expandVBox.PackStart (scrolledWindow, true, true, 5);
			
			// Drop all that into into the Expander
			expander = new Gtk.Expander ("Details");
			expander.Add (expandVBox);
			expander.Show ();
			VBox.PackStart (expander, true, true, 5);
			
			closeButton = (Gtk.Button) AddButton (Gtk.Stock.Close, Gtk.ResponseType.Close);
			closeButton.Sensitive = false;
			
			HasSeparator = false;
			
			expander.Activated += OnExpanderActivated;
			
			VBox.ShowAll ();
		}

		private void OnExpanderActivated (object sender, EventArgs e)
		{
			if (expander.Expanded)
				this.Resizable = true;
			else
				this.Resizable = false;
		}
		
		public string ProgressText
		{
			get { return progressBar.Text; }
			set { progressBar.Text = value; }
		}
		
		public double ProgressFraction
		{
			get { return progressBar.Fraction; }
			set { progressBar.Fraction = value;}
		}
		
		public bool CloseSensitive
		{
			get { return closeButton.Sensitive; }
			set { closeButton.Sensitive = value; }
		}
		
		public void AddUpdateItem (string title, string status)
		{
			model.AppendValues (title, status);
		}

		#region Private Event Handlers
		void OnSynchronizeButton (object sender, EventArgs args)
		{
			model.Clear ();
			progressBar.Fraction = 0;
			syncButton.Sensitive = false;
			SyncManager.PerformSynchronization ();
			syncButton.Sensitive = true;
		}
		#endregion // Private Event Handlers
	}


	public class SyncTitleConflictDialog : Gtk.Dialog
	{
		private Note existingNote;
		
		private Gtk.Button continueButton;
		
		private Gtk.Entry renameEntry;
		private Gtk.CheckButton renameUpdateCheck;
		private Gtk.RadioButton renameRadio;
		private Gtk.RadioButton deleteExistingRadio;
					
		public SyncTitleConflictDialog (Note existingNote) :
			base ("Note Title Conflict", null, Gtk.DialogFlags.Modal)
		{
			this.existingNote = existingNote;
			
			VBox.PackStart (new Gtk.Label ("The server already has a note called " +
			                               existingNote.Title + ".  What do you want to do?"));
			
			Gtk.HBox renameHBox = new Gtk.HBox ();
			renameRadio = new Gtk.RadioButton ("Rename existing note");
			Gtk.VBox renameOptionsVBox = new Gtk.VBox ();
			renameEntry = new Gtk.Entry (existingNote.Title);
			renameUpdateCheck = new Gtk.CheckButton ("Update referencing notes to match new note title");
			renameOptionsVBox.PackStart (renameEntry);
			renameOptionsVBox.PackStart (renameUpdateCheck);
			renameHBox.PackStart (renameRadio);
			renameHBox.PackStart (renameOptionsVBox);
			VBox.PackStart (renameHBox);
			
			deleteExistingRadio = new Gtk.RadioButton (renameRadio, "Delete existing note");
			VBox.PackStart (deleteExistingRadio);
			
			continueButton = (Gtk.Button) AddButton (Gtk.Stock.GoForward, Gtk.ResponseType.Accept);
			AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel);
			
			ShowAll ();
		}
		
		public string RenamedTitle
		{
			get { return renameEntry.Text; }
		}
		
		public TitleConflictResolution Resolution
		{
			get
			{
				if (renameRadio.Active) {
					if (renameUpdateCheck.Active)
					        return TitleConflictResolution.RenameExistingAndUpdate;
					else
						return TitleConflictResolution.RenameExistingNoUpdate;
				}
				else
					return TitleConflictResolution.DeleteExisting;
			}
		}
		
		public enum TitleConflictResolution
		{
			RenameExistingNoUpdate,
			RenameExistingAndUpdate,
			DeleteExisting
		}
	}	
}