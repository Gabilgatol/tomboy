using System;
using Mono.Unix;

using Gtk;

namespace Tomboy
{
	public class SyncDialog : Gtk.Dialog
	{
		private Gtk.Image image;
		private Gtk.Label headerLabel;
		private Gtk.Label messageLabel;
		private Gtk.ProgressBar progressBar;
		private Gtk.Label progressLabel;
		
		private Gtk.Expander expander;
		private Gtk.Button closeButton;
		private uint progressBarTimeoutId;
		
		private Gtk.ListStore model;
		
		// TODO: Possible to make Tomboy not crash if quit while dialog is up?
		public SyncDialog ()
			: base (string.Empty, 
					null,
					Gtk.DialogFlags.DestroyWithParent)
		{
			progressBarTimeoutId = 0;
			
			SetSizeRequest (400, -1);
			
			VBox outerVBox = new VBox (false, 12);
			outerVBox.BorderWidth = 12;
			outerVBox.Spacing = 8;
			
			HBox hbox = new HBox (false, 8);
			
			image = new Image (GuiUtils.GetIcon ("tomboy", 48));
			image.Show ();
			hbox.PackStart (image, false, false, 0);
			
			VBox vbox = new VBox (false, 8);
			
			headerLabel = new Label ();
			headerLabel.UseMarkup = true;
			headerLabel.Xalign = 0;
			headerLabel.UseUnderline = false;
			headerLabel.Show ();
			vbox.PackStart (headerLabel, false, false, 0);
			
			messageLabel = new Label ();
			messageLabel.Xalign = 0;
			messageLabel.UseUnderline = false;
			messageLabel.LineWrap = true;
			messageLabel.Wrap = true;
			messageLabel.Show ();
			vbox.PackStart (messageLabel, false, false, 0);
			
			vbox.Show ();
			hbox.PackStart (vbox, true, true, 0);
			
			hbox.Show ();
			outerVBox.PackStart (hbox, false, false, 0);
			
			progressBar = new Gtk.ProgressBar ();
			//progressBar.Text = "Contacting Server...";
			progressBar.Orientation = Gtk.ProgressBarOrientation.LeftToRight;
			progressBar.BarStyle = ProgressBarStyle.Continuous;
			progressBar.ActivityBlocks = 30;
			progressBar.Show ();
			outerVBox.PackStart (progressBar, false, false, 0);
			
			progressLabel = new Label ();
			progressLabel.UseMarkup = true;
			progressLabel.Xalign = 0;
			progressLabel.UseUnderline = false;
			progressLabel.LineWrap = true;
			progressLabel.Wrap = true;
			progressLabel.Show ();
			outerVBox.PackStart (progressLabel, false, false, 0);
			
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
			expander = new Gtk.Expander (Catalog.GetString ("Details"));
			expander.Add (expandVBox);
			expander.Show ();
			outerVBox.PackStart (expander, true, true, 5);
			
			closeButton = (Gtk.Button) AddButton (Gtk.Stock.Close, Gtk.ResponseType.Close);
			closeButton.Sensitive = false;
			
			HasSeparator = false;
			
			expander.Activated += OnExpanderActivated;
			
			outerVBox.Show ();
			VBox.PackStart (outerVBox, true, true, 0);

			VBox.ShowAll ();
		}
		
		public override void Destroy ()
		{
			SyncManager.StateChanged -= OnSyncStateChanged;
			SyncManager.NoteSynchronized -= OnNoteSynchronized;
			SyncManager.NoteConflictDetected -= OnNoteConflictDetected;
			base.Destroy ();
		}
		
		protected override void OnRealized ()
		{
			base.OnRealized ();
			
			SyncManager.StateChanged += OnSyncStateChanged;
			SyncManager.NoteSynchronized += OnNoteSynchronized;
			SyncManager.NoteConflictDetected += OnNoteConflictDetected;

			SyncState state = SyncManager.State;
			if (state == SyncState.Idle) {
				// Kick off a timer to keep the progress bar going
				progressBarTimeoutId = GLib.Timeout.Add (500, OnPulseProgressBar);
			
				// Kick off a new synchronization
				SyncManager.PerformSynchronization ();
			} else {
				// Adjust the GUI accordingly
				OnSyncStateChanged (state);
			}
		}
		
		private void OnExpanderActivated (object sender, EventArgs e)
		{
			if (expander.Expanded)
				this.Resizable = true;
			else
				this.Resizable = false;
		}
		
		public string HeaderText
		{
			set {
				headerLabel.Markup = string.Format (
					"<span size=\"large\" weight=\"bold\">{0}</span>",
					value);
			}
		}
		
		public string MessageText
		{
			set { messageLabel.Text = value; }
		}
		
		public string ProgressText
		{
			get { return progressLabel.Text; }
			set {
				progressLabel.Markup =
					string.Format ("<span style=\"italic\">{0}</span>",
						value);
			}
		}
		
//		public double ProgressFraction
//		{
//			get { return progressBar.Fraction; }
//			set { progressBar.Fraction = value;}
//		}
		
//		public bool CloseSensitive
//		{
//			get { return closeButton.Sensitive; }
//			set { closeButton.Sensitive = value; }
//		}
		
		public void AddUpdateItem (string title, string status)
		{
			model.AppendValues (title, status);
		}

		#region Private Event Handlers
		bool OnPulseProgressBar ()
		{
			if (SyncManager.State == SyncState.Idle)
				return false;
			
			progressBar.Pulse ();
			
			// Return true to keep things going well
			return true;
		}

		void OnSyncStateChanged (SyncState state)
		{
			// This event handler will be called by the synchronization thread
			// so we have to use the delegate here to manipulate the GUI.
			Gtk.Application.Invoke (delegate {
				// FIXME: Change these strings to be user-friendly
				switch (state) {
				case SyncState.AcquiringLock:
					ProgressText = Catalog.GetString ("Acquiring sync lock...");
					break;
				case SyncState.CommittingChanges:
					ProgressText = Catalog.GetString ("Committing changes...");
					break;
				case SyncState.Connecting:
					Title = Catalog.GetString ("Synchronizing Notes");
					HeaderText = Catalog.GetString ("Synchronizing your notes...");
					MessageText = Catalog.GetString ("This may take a while, kick back and enjoy!");
					model.Clear ();
					ProgressText = Catalog.GetString ("Connecting to the server...");
					progressBar.Fraction = 0;
					progressBar.Show ();
					progressLabel.Show ();
					break;
				case SyncState.DeleteServerNotes:
					ProgressText = Catalog.GetString ("Deleting notes off of the server...");
					progressBar.Pulse ();
					break;
				case SyncState.Downloading:
					ProgressText = Catalog.GetString ("Downloading new/updated notes...");
					progressBar.Pulse ();
					break;
				case SyncState.Idle:
					GLib.Source.Remove (progressBarTimeoutId);
					progressBarTimeoutId = 0;
					progressBar.Fraction = 0;
					progressBar.Hide ();
					progressLabel.Hide ();
					closeButton.Sensitive = true;
					break;
				case SyncState.Locked:
					Title = Catalog.GetString ("Server Locked");
					HeaderText = Catalog.GetString ("Server is locked");
					MessageText = Catalog.GetString ("One of your other computers is currently synchronizing.  Please wait and try again.");
					ProgressText = string.Empty;
					break;
				case SyncState.PrepareDownload:
					ProgressText = Catalog.GetString ("Preparing to download updates from server...");
					break;
				case SyncState.PrepareUpload:
					ProgressText = Catalog.GetString ("Preparing to upload updates from server...");
					break;
				case SyncState.Uploading:
					ProgressText = Catalog.GetString ("Uploading notes to server...");
					break;
				case SyncState.Failed:
					Title = Catalog.GetString ("Synchronization Failed");
					HeaderText = Catalog.GetString ("Failed to synchronize");
					MessageText = Catalog.GetString ("Could not synchronize notes.  Check the details below and try again.");
					ProgressText = string.Empty;
					break;
				case SyncState.Succeeded:
					Title = Catalog.GetString ("Synchronization Complete");
					HeaderText = Catalog.GetString ("Synchronization is complete");
					MessageText = Catalog.GetString ("Your notes are up to date.  See the details below or close the window.");
					ProgressText = string.Empty;
					break;
				case SyncState.UserCancelled:
					Title = Catalog.GetString ("Synchronization Canceled");
					HeaderText = Catalog.GetString ("Synchronization was canceled");
					MessageText = Catalog.GetString ("You canceled the synchronization.  You may close the window now.");
					ProgressText = string.Empty;
					break;
				case SyncState.NoConfiguredSyncService:
					Title = Catalog.GetString ("Synchronization Not Configured");
					HeaderText = Catalog.GetString ("Synchronization is not configured");
					MessageText = Catalog.GetString ("Please configure synchronization in the preferences dialog.");
					ProgressText = string.Empty;
					break;
				case SyncState.SyncServerCreationFailed:
					Title = Catalog.GetString ("Synchronization Service Error");
					HeaderText = Catalog.GetString ("Service error");
					MessageText = Catalog.GetString ("Error connecting to the synchronization service.  Please try again.");
					ProgressText = string.Empty;
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
				SyncManager.ResolveConflict (/*localConflictNote, */resolution);
			});
		}

		#endregion // Private Event Handlers
		
#region Private Methods
		// TODO: This appears to add <link:internal> around the note title
		//       in the content.
		private void RenameNote (Note note, string newTitle, bool updateReferencingNotes)
		{
			string oldTitle = note.Title;
			if (updateReferencingNotes)
				note.Title = newTitle;
			else
				note.Data.Title = newTitle;
			string oldContent = note.XmlContent;
			note.XmlContent = NoteArchiver.Instance.GetRenamedNoteXml (oldContent, oldTitle, newTitle);
			
			// Testing... (the idea being that if the renamed note has a new GUID, conflict handling is easy)
			Logger.Debug ("Entering the realm of testing in RenameNote");
			//bool noteOpen = note.IsOpened;
			string newContent = note.XmlContent;
			Tomboy.DefaultNoteManager.Delete (note);
			Note renamedNote = Tomboy.DefaultNoteManager.Create (newTitle, newContent); // TODO: Doesn't handle tags, etc!
			//if (noteOpen)
			//	renamedNote.Window.Present ();
			
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
		
		private Gtk.Label headerLabel;
		private Gtk.Label messageLabel;

		public SyncTitleConflictDialog (Note existingNote) :
			base (Catalog.GetString ("Note Title Conflict"), null, Gtk.DialogFlags.Modal)
		{
			this.existingNote = existingNote;
			string suggestedRenameBase = existingNote.Title + Catalog.GetString (" (old)");
			string suggestedRename = suggestedRenameBase;
			for (int i = 1; existingNote.Manager.Find (suggestedRename) != null; i++)
				suggestedRename = suggestedRenameBase + " " + i.ToString();
			
			VBox outerVBox = new VBox (false, 12);
			outerVBox.BorderWidth = 12;
			outerVBox.Spacing = 8;
			
			HBox hbox = new HBox (false, 8);
			Image image = new Image (GuiUtils.GetIcon (Gtk.Stock.DialogWarning, 48)); // TODO: Is this the right icon?
			image.Show ();
			hbox.PackStart (image, false, false, 0);
			
			VBox vbox = new VBox (false, 8);
			
			headerLabel = new Label ();
			headerLabel.UseMarkup = true;
			headerLabel.Xalign = 0;
			headerLabel.UseUnderline = false;
			headerLabel.Show ();
			vbox.PackStart (headerLabel, false, false, 0);
			
			messageLabel = new Label ();
			messageLabel.Xalign = 0;
			messageLabel.UseUnderline = false;
			messageLabel.LineWrap = true;
			messageLabel.Wrap = true;
			messageLabel.Show ();
			vbox.PackStart (messageLabel, false, false, 0);
			
			vbox.Show ();
			hbox.PackStart (vbox, true, true, 0);
			
			hbox.Show ();
			//VBox.PackStart (hbox);
			outerVBox.PackStart (hbox);
			VBox.PackStart (outerVBox);
			
			Gtk.HBox renameHBox = new Gtk.HBox ();
			renameRadio = new Gtk.RadioButton ("Rename local note");
			renameRadio.Toggled += radio_Toggled;
			Gtk.VBox renameOptionsVBox = new Gtk.VBox ();
			
			renameEntry = new Gtk.Entry (suggestedRename);
			renameEntry.Changed += renameEntry_Changed;
			renameUpdateCheck = new Gtk.CheckButton (Catalog.GetString ("Update links in referencing notes"));
			renameOptionsVBox.PackStart (renameEntry);
			renameOptionsVBox.PackStart (renameUpdateCheck);
			renameHBox.PackStart (renameRadio);
			renameHBox.PackStart (renameOptionsVBox);
			VBox.PackStart (renameHBox);
			
			deleteExistingRadio = new Gtk.RadioButton (renameRadio, Catalog.GetString ("Delete existing note"));
			deleteExistingRadio.Toggled += radio_Toggled;
			VBox.PackStart (deleteExistingRadio);
			
			AddButton (Gtk.Stock.Cancel, Gtk.ResponseType.Cancel);
			continueButton = (Gtk.Button) AddButton (Gtk.Stock.GoForward, Gtk.ResponseType.Accept);
			
			// Set initial dialog text
			HeaderText = Catalog.GetString ("Note conflict detected");
			MessageText = string.Format (Catalog.GetString ("The server already has a note called \"{0}\"."
			                                                + "  What do you want to do with your local note?"),
			                             existingNote.Title);
			
			ShowAll ();
		}
		
		private void renameEntry_Changed (object sender, System.EventArgs e)
		{
			if (renameRadio.Active &&
			    existingNote.Manager.Find (RenamedTitle) != null)
				continueButton.Sensitive = false;
			else
				continueButton.Sensitive = true;
		}
		
		// Handler for each radio button's Toggled event
		private void radio_Toggled (object sender, System.EventArgs e)
		{
			// Make sure Continue button has the right sensitivity
			renameEntry_Changed (renameEntry, null);

			// Update sensitivity of rename-related widgets
			renameEntry.Sensitive = renameRadio.Active;
			renameUpdateCheck.Sensitive = renameRadio.Active;
		}
		
		public string HeaderText
		{
			set {
				headerLabel.Markup = string.Format (
					"<span size=\"large\" weight=\"bold\">{0}</span>",
					value);
			}
		}
		
		public string MessageText
		{
			set { messageLabel.Text = value; }
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
