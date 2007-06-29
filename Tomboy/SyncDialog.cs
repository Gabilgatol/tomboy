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
			
			SyncManager.StateChanged += OnSyncStateChanged;
			SyncManager.NoteSynchronized += OnNoteSynchronized;
			SyncManager.NoteConflictDetected += OnNoteConflictDetected;
			
			VBox.ShowAll ();
		}
		
		public override void Destroy ()
		{
			SyncManager.StateChanged -= OnSyncStateChanged;
			SyncManager.NoteSynchronized -= OnNoteSynchronized;
			SyncManager.NoteConflictDetected -= OnNoteConflictDetected;
			base.Destroy ();
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
			SyncManager.PerformSynchronization ();
		}
		
		void OnSyncStateChanged (SyncState state)
		{
			// This event handler will be called by the synchronization thread
			// so we have to use the delegate here to manipulate the GUI.
			Gtk.Application.Invoke (delegate {
				// FIXME: Change these strings to be user-friendly
				switch (state) {
				case SyncState.AcquiringLock:
					ProgressText = Catalog.GetString ("Acquiring sync lock");
					progressBar.Pulse ();
					break;
				case SyncState.CommittingChanges:
					ProgressText = Catalog.GetString ("Committing changes");
					progressBar.Pulse ();
					break;
				case SyncState.Connecting:
					model.Clear ();
					syncButton.Sensitive = false;
					ProgressText = Catalog.GetString ("Connecting to the server");
					progressBar.Fraction = 0;
					break;
				case SyncState.DeleteServerNotes:
					ProgressText = Catalog.GetString ("Deleting notes off of the server");
					progressBar.Pulse ();
					break;
				case SyncState.Downloading:
					ProgressText = Catalog.GetString ("Downloading new/updated notes");
					progressBar.Pulse ();
					break;
				case SyncState.Idle:
					progressBar.Fraction = 0;
					syncButton.Sensitive = true;
					break;
				case SyncState.Locked:
					ProgressText = Catalog.GetString ("Another client is synchronizing, please try again.");
					progressBar.Fraction = 0;
					syncButton.Sensitive = true;
					break;
				case SyncState.PrepareDownload:
					ProgressText = Catalog.GetString ("Preparing to download updates from server");
					progressBar.Pulse ();
					break;
				case SyncState.PrepareUpload:
					ProgressText = Catalog.GetString ("Preparing to upload updates from server");
					progressBar.Pulse ();
					break;
				case SyncState.Uploading:
					ProgressText = Catalog.GetString ("Uploading notes to server");
					progressBar.Pulse ();
					break;
				case SyncState.Failed:
					ProgressText = Catalog.GetString ("Failed");
					break;
				case SyncState.Succeeded:
					ProgressText = Catalog.GetString ("Succeeded");
					break;
				case SyncState.UserCancelled:
					progressBar.Fraction = 0;
					ProgressText = Catalog.GetString ("Cancelled by user");
					break;
				}
			});
		}
		
		void OnNoteSynchronized (string noteTitle, NoteSyncType type)
		{
			// This event handler will be called by the synchronization thread
			// so we have to use the delegate here to manipulate the GUI.
			Gtk.Application.Invoke (delegate {
				// FIXME: Change these strings to be more user-friendly
				string statusText = string.Empty;
				switch (type) {
				case NoteSyncType.DeleteFromClient:
					statusText = Catalog.GetString ("Deleting from local copy");
					break;
				case NoteSyncType.DeleteFromServer:
					statusText = Catalog.GetString ("Deleting from server");
					break;
				case NoteSyncType.DownloadModified:
					statusText = Catalog.GetString ("Downloading updates from server");
					break;
				case NoteSyncType.DownloadNew:
					statusText = Catalog.GetString ("Downloading new note from server");
					break;
				case NoteSyncType.UploadModified:
					statusText = Catalog.GetString ("Uploading changes to server");
					break;
				case NoteSyncType.UploadNew:
					statusText = Catalog.GetString ("Uploading new note to server");
					break;
				}
				AddUpdateItem (noteTitle, statusText);
			});
		}
		
		void OnNoteConflictDetected (NoteManager manager, Note localConflictNote)
		{
			SyncTitleConflictResolution resolution = SyncTitleConflictResolution.DeleteExisting;
			// This event handler will be called by the synchronization thread
			// so we have to use the delegate here to manipulate the GUI.
			Gtk.Application.Invoke (delegate {
				SyncTitleConflictDialog conflictDlg =
					new SyncTitleConflictDialog (localConflictNote);
				Gtk.ResponseType reponse = (Gtk.ResponseType) conflictDlg.Run ();
				
				if (reponse == Gtk.ResponseType.Cancel)
					resolution = SyncTitleConflictResolution.Cancel;
				else {
					resolution = conflictDlg.Resolution;
					switch (resolution) {
					case SyncTitleConflictResolution.DeleteExisting:
						manager.Delete (localConflictNote);
						break;
					case SyncTitleConflictResolution.RenameExistingAndUpdate:
						RenameNote (localConflictNote, conflictDlg.RenamedTitle, true);
						break;
					case SyncTitleConflictResolution.RenameExistingNoUpdate:
						RenameNote (localConflictNote, conflictDlg.RenamedTitle, false);
						break;
					}
				}
				
				conflictDlg.Hide ();
				conflictDlg.Destroy ();
			
				// Let the SyncManager continue
				SyncManager.ResolveConflict (localConflictNote, resolution);
			});
		}

		#endregion // Private Event Handlers
		
#region Private Methods
		
			
		// TODO: This appears to add <link:internal> around the note title
		//       in the content, and it removes the newline between title
		//       and rest of content.
		private void RenameNote (Note note, string newTitle, bool updateReferencingNotes)
		{
			string oldTitle = note.Title;
			if (updateReferencingNotes)
				note.Title = newTitle;
			else
				note.Data.Title = newTitle;
			string oldContent = note.XmlContent;
			int i = oldContent.IndexOf (oldTitle);
			string newContent = oldContent.Remove (i, newTitle.Length).Insert (i, newTitle);
			note.XmlContent = newContent;
		}
#endregion // Private Methods
		
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
			string suggestedRenameBase = existingNote.Title + Catalog.GetString (" (old)");
			string suggestedRename = suggestedRenameBase;
			for (int i = 1; existingNote.Manager.Find (suggestedRename) != null; i++)
				suggestedRename = suggestedRenameBase + " " + i.ToString();
			
			VBox.PackStart (new Gtk.Label ("The server already has a note called \"" +
			                               existingNote.Title + "\".  What do you want to do with your local note?"));
			
			Gtk.HBox renameHBox = new Gtk.HBox ();
			renameRadio = new Gtk.RadioButton ("Rename local note");
			Gtk.VBox renameOptionsVBox = new Gtk.VBox ();
			
			renameEntry = new Gtk.Entry (suggestedRename);
			renameEntry.Changed += renameEntry_Changed;
			renameUpdateCheck = new Gtk.CheckButton ("Update referencing notes to match new note title");
			renameOptionsVBox.PackStart (renameEntry);
			renameOptionsVBox.PackStart (renameUpdateCheck);
			renameHBox.PackStart (renameRadio);
			renameHBox.PackStart (renameOptionsVBox);
			VBox.PackStart (renameHBox);
			
			deleteExistingRadio = new Gtk.RadioButton (renameRadio, "Delete existing note");
			VBox.PackStart (deleteExistingRadio);
			
			AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel);
			continueButton = (Gtk.Button) AddButton (Gtk.Stock.GoForward, Gtk.ResponseType.Accept);
			
			ShowAll ();
		}
		
		private void renameEntry_Changed (object sender, System.EventArgs e)
		{
			if (existingNote.Manager.Find (RenamedTitle) != null)
				continueButton.Sensitive = false;
			else
				continueButton.Sensitive = true;
		}
		
		public string RenamedTitle
		{
			get { return renameEntry.Text; }
		}
		
		public SyncTitleConflictResolution Resolution
		{
			get
			{
				if (renameRadio.Active) {
					if (renameUpdateCheck.Active)
					        return SyncTitleConflictResolution.RenameExistingAndUpdate;
					else
						return SyncTitleConflictResolution.RenameExistingNoUpdate;
				}
				else
					return SyncTitleConflictResolution.DeleteExisting;
			}
		}
	}	

	public enum SyncTitleConflictResolution
	{
		Cancel,
		RenameExistingNoUpdate,
		RenameExistingAndUpdate,
		DeleteExisting
	}
}