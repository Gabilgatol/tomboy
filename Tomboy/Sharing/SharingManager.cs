using System;
using System.Collections;
using Gtk;
using Mono.Unix;

namespace Tomboy.Sharing
{
	public enum SharingType
	{
		AllNotes,
		SelectedNotes
	}

	public class SharingManager
	{
		private static SharingManager instance = null;
		private static string locker = "locker";
		
		private TreeStore node_store;

		// key = ShareNode.Guid, value = Gtk.TreeIter of ShareNode in node_store
		private Hashtable node_map;
		private bool sharing_enabled;
		private SharingType sharing_type;
		
		private SharingServer sharing_server;
		private ServiceLocator service_locator;
		
		private Hashtable ws_clients;
		
		public event EventHandler SharingEnabled;
		public event EventHandler SharingDisabled;
		public event EventHandler SharingTypeChanged;
		
		public static SharingManager GetInstance ()
		{
			if (instance == null) {
				lock (locker) {
					if (instance == null) {
						instance = new SharingManager ();
					}
				}
			}
			
			return instance;
		}
		
		public TreeModel ShareNodes
		{
			get {
				return node_store;
			}
		}
		
		public bool IsSharingEnabled
		{
			get { return sharing_enabled; }
		}
		
		public SharingType SharingType
		{
			get { return sharing_type; }
		}
		
		/// <summary>
		/// This should be called when a tomboy client is expanded in a TreeView
		/// </summary>
		public void Connect (TomboyShareNode tomboy_share_node)
		{
Logger.Debug ("SharingManager.Connect (\"{0}\")", tomboy_share_node.Name);
			// FIXME: Eventually do whatever needs to be done for connecting like authentication
			TomboyWebService ws = ws_clients [tomboy_share_node.Guid] as TomboyWebService;
			if (ws == null) {
				ws = new TomboyWebService ();
				
				TomboyService service = tomboy_share_node.Service;
				string ip_addr = service.IPAddress.ToString ();
				string port = service.Port.ToString ();
				
				// Set the correct IP Address and Port by setting the Url
				ws.Url = string.Format ("http://{0}:{1}/tomboy/Tomboy.asmx",
										ip_addr, port);
				
				ws_clients [tomboy_share_node.Guid] = ws;
			}

			try {
				Logger.Debug ("Connecting to: {0} ({1})", tomboy_share_node.Name, ws.Url);
				ws.Ping ();
			} catch (Exception e) {
				Logger.Log ("Could not connect to: {0}", tomboy_share_node.Name);
			}
		}
		
		/// <summary>
		/// After connecting to a Tomboy Client, this should be called to load
		/// all of the shared nodes that are available.
		/// </summary>
		public void LoadSharedNotes (TomboyShareNode tomboy_share_node)
		{
Logger.Debug ("SharingManager.LoadSharedNotes (\"{0}\")", tomboy_share_node.Name);
			// Load in the shared notes and remove the dummy if > 0 returned
			TomboyWebService ws = ws_clients [tomboy_share_node.Guid] as TomboyWebService;
			if (ws == null)
				throw new Exception ("FIXME: This should be a ServiceNotConnected exception.");
			
			try { // FIXME: Fix this to not be an "all-encompassing" try/catch block
				TreeIter iter;
				TreeIter child_iter;
				if (node_map.ContainsKey (tomboy_share_node.Guid)) {
					iter = (TreeIter) node_map [tomboy_share_node.Guid];

					NoteInfo [] shared_notes = ws.GetSharedNotes ();
Logger.Debug ("    shared_notes is {0}, # of notes = {1}", shared_notes == null ? "null" : "valid", shared_notes == null ? "0" : shared_notes.Length.ToString ());
					if (shared_notes != null && shared_notes.Length > 0) {
						// Remove the dummy note or old children
						ClearTreeStoreChildren (iter);
						
						// Add in the real children nodes
						foreach (NoteInfo note_info in shared_notes) {
							NoteShareNode note_share_node =
								new NoteShareNode (
									note_info.Guid,
									note_info.Name,
									DateTime.Parse (note_info.LastModified),
									note_info.Revision,
									tomboy_share_node.Guid);
							child_iter = node_store.AppendNode (iter);
							node_store.SetValue (child_iter, 0, note_share_node);
						}
					} else {
						if (node_store.IterChildren (out child_iter, iter)) {
							ShareNode share_node = node_store.GetValue (child_iter, 0) as ShareNode;
							if (share_node is DummyShareNode) {
								DummyShareNode dummy_node = share_node as DummyShareNode;
								dummy_node.UpdateMessage (Catalog.GetString ("No shared notes found"));
								// Let any TreeView's know something changed
								node_store.EmitRowChanged (node_store.GetPath (child_iter), child_iter);
							}
						}
					}
				}
			} catch (Exception e) {
				Logger.Log ("Error in SharingManager.LoadSharedNotes");
			}
		}
		
		public NoteFullInfo DownloadNote (NoteShareNode note_share_node)
		{
			NoteFullInfo full_info = null;
			TomboyWebService ws = ws_clients [note_share_node.ClientGuid] as TomboyWebService;
			if (ws == null)
				throw new Exception ("FIXME: Ths should be a ServiceNotConnected exception.");
			
			try {	// FIXME: Fix this to not be an "all-encompassing" try/catch block
				full_info = ws.GetNote (note_share_node.Guid);
			} catch (Exception e) {
				Logger.Log ("Error in SharingManager.DownloadNote");
			}
			
			return full_info;
		}
		
		/// <summary>
		/// Mark a note to be shared or not.
		/// </summary>
		public void ShareNote (Note note, bool share)
		{
			if (share)
				Preferences.AddSharedNote (note);
			else
				Preferences.RemoveSharedNote (note);
		}
		
		public bool IsSharedNote (Note note)
		{
			string pref = Preferences.Get (Preferences.SHARING_SELECTED_NOTES) as string;
			if (pref != null && pref.IndexOf (note.Uri) >= 0 || pref.CompareTo ("ALL") == 0)
				return true;
			
			return false;
		}
		
		private SharingManager ()
		{
			node_store = new TreeStore (typeof (ShareNode));
			node_map = Hashtable.Synchronized (new Hashtable ());
			sharing_enabled = false;
			
			string pref = Preferences.Get (Preferences.SHARING_SELECTED_NOTES) as string;
			if (pref.CompareTo ("ALL") == 0)
				sharing_type = SharingType.AllNotes;
			else
				sharing_type = SharingType.SelectedNotes;
			
			ws_clients = new Hashtable ();
			
			Init ();
		}
		
		void Init ()
		{
			Logger.Debug ("SharingManager Initializing");
			
			sharing_server = SharingServer.GetInstance ();
			service_locator = ServiceLocator.GetInstance ();
			
			sharing_server.NameCollision += OnNameCollision;
			
			// Register for ServiceLocator events before starting it up
			service_locator.ServiceFound += OnServiceFound;
			service_locator.ServiceRemoved += OnServiceRemoved;
			service_locator.ServiceUpdated += OnServiceUpdated;
						
			bool enable_local_browsing = (bool) Preferences.Get (Preferences.SHARING_ENABLE_LOCAL_BROWSING);
			if (enable_local_browsing)
				service_locator.Start ();
			
			bool enable_local_publishing = (bool) Preferences.Get (Preferences.SHARING_ENABLE_LOCAL_PUBLISHING);
			if (enable_local_publishing) {
				sharing_server.Start ();
				sharing_enabled = true;
			}
			
			Preferences.SettingChanged += OnPrefSettingChanged;
		}
		
		void OnPrefSettingChanged (object sender, GConf.NotifyEventArgs args)
		{
			switch (args.Key) {
			case Preferences.SHARING_ENABLE_LOCAL_BROWSING:
				bool enable_local_browsing = (bool) args.Value;
				if (enable_local_browsing)
					service_locator.Start ();
				else {
					service_locator.Stop ();

					// Clear out the known services
					node_store.Clear ();
					node_map.Clear ();
					ws_clients.Clear ();
				}
				break;
			case Preferences.SHARING_ENABLE_LOCAL_PUBLISHING:
				bool enable_local_publishing = (bool) args.Value;
				if (enable_local_publishing) {
					sharing_server.Start ();
					sharing_enabled = true;
					if (SharingEnabled != null)
						SharingEnabled (this, EventArgs.Empty);
				} else {
					sharing_server.Stop ();
					sharing_enabled = false;
					if (SharingDisabled != null)
						SharingDisabled (this, EventArgs.Empty);
				}
				break;
			case Preferences.SHARING_SELECTED_NOTES:
				bool changed = false;
				string pref = args.Value as string;
				if (pref.CompareTo ("ALL") == 0) {
					if (sharing_type != SharingType.AllNotes) {
						sharing_type = SharingType.AllNotes;
						changed = true;
					}
				} else {
					if (sharing_type != SharingType.SelectedNotes) {
						sharing_type = SharingType.SelectedNotes;
						changed = true;
					}
				}
				
				if (changed && SharingTypeChanged != null)
					SharingTypeChanged (this, EventArgs.Empty);
				
				break;
			}
		}
		
		void OnNameCollision (object sender, EventArgs args)
		{
			// This should NEVER be called because System.Guid is supposed to
			// produce a unique ID specific for a system.  If this IS called,
			// regenerate a new System.Guid for this machine.  The local service
			// is watching the Preference changes and should automatically update.
			// The mDNS registration service should automatically restart when
			// the Guid is changed.
			string updated_guid = System.Guid.NewGuid ().ToString ();
			Preferences.Set (Preferences.SHARING_GUID, updated_guid);
		}
		
		void OnServiceFound (object sender, ServiceEventArgs args)
		{
Logger.Debug ("SharingManager.OnServiceFound");
			TomboyService service = args.Service;
			if (node_map.ContainsKey (service.Guid)) {
				// Not sure how this happened, but OnServiceUpdated ()
				// should be used instead.
				OnServiceUpdated (sender, args);
				return;
			}
			
			TomboyShareNode node = CreateNodeFromService (service);
			Gtk.Application.Invoke (delegate {
				AddNode (node);
			});
		}
		
		void OnServiceRemoved (object sender, ServiceEventArgs args)
		{
Logger.Debug ("SharingManager.OnServiceRemoved");
			TomboyService service = args.Service;
			if (node_map.ContainsKey (service.Guid)) {
				Gtk.Application.Invoke (delegate {
					RemoveNode (service.Guid);
				});
			}
		}
		
		void OnServiceUpdated (object sender, ServiceEventArgs args)
		{
Logger.Debug ("SharingManager.OnServiceUpdated");
			TomboyService service = args.Service;
			if (!node_map.ContainsKey (service.Guid)) {
				// Not sure how this happened, but OnServiceFound ()
				// should be used instead;
				OnServiceFound (sender, args);
				return;
			}
			
			// Replace the existing node
			TomboyShareNode updated_node = CreateNodeFromService (service);
			Gtk.Application.Invoke (delegate {
				TreeIter iter = (TreeIter) node_map [service.Guid];
			
				node_store.SetValue (iter, 0, updated_node);
			});
Logger.Debug ("FIXME: Figure out whether we should be calling EmitRowChanged");
		}
		
		TomboyShareNode CreateNodeFromService (TomboyService service)
		{
			TomboyShareNode node = new TomboyShareNode (service);
			
			return node;
		}
		
		void AddNode (ShareNode node)
		{
Logger.Debug ("SharingManager.AddNode");
			TreeIter iter;
			iter = node_store.AppendNode ();
			node_store.SetValue (iter, 0, node);
			node_map [node.Guid] = iter;

			// Add on the dummy node to have an expander appear
			DummyShareNode dummy_node = new DummyShareNode (
				string.Format (Catalog.GetString ("Connecting to {0}..."),
								node.Name));
			TreeIter child_iter = node_store.AppendNode (iter);
			node_store.SetValue (child_iter, 0, dummy_node);
Logger.Debug ("FIXME: Figure out whether we should be calling EmitRowInserted");
		}
		
		void RemoveNode (string node_guid)
		{
Logger.Debug ("SharingManager.RemoveNode");
			TreeIter iter;
			if (!node_map.ContainsKey (node_guid))
				return;
			
			iter = (TreeIter) node_map [node_guid];
			node_store.Remove (ref iter);
			node_map.Remove (node_guid);
			if (ws_clients.ContainsKey (node_guid))
				ws_clients.Remove (node_guid);
Logger.Debug ("FIXME: Figure out whether we should be calling EmitRowDeleted");
		}
		
		void ClearTreeStoreChildren (TreeIter parent_iter)
		{
			TreeIter child_iter;
			if (!node_store.IterIsValid (parent_iter))
				return;
			
			while (node_store.IterChildren (out child_iter, parent_iter)) {
				node_store.Remove (ref child_iter);
			}
		}
	}
}