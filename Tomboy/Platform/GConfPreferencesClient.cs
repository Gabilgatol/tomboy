using System.Collections.Generic;

namespace Tomboy.Platform
{
	public class GConfPreferencesClient : IPreferencesClient
	{
		private GConf.Client client;
		private Dictionary<string, NotifyEventHandler> event_map;

		public GConfPreferencesClient ()
		{
			client = new GConf.Client ();
			event_map = new Dictionary<string, NotifyEventHandler> ();
		}

		public void Set (string key, object val)
		{
			client.Set (key, val);
		}

		public object Get (string key)
		{
			try {
				return client.Get (key);
			} catch (GConf.NoSuchKeyException) {
				throw new NoSuchKeyException (key);
			}
		}

		public void AddNotify (string dir, NotifyEventHandler notify)
		{
			if(!event_map.ContainsKey (dir)) {
				event_map.Add (dir, notify);
				client.AddNotify (dir, HandleNotify);
			} else
				event_map[dir] += notify;
		}

		public void RemoveNotify (string dir, NotifyEventHandler notify)
		{
			if(!event_map.ContainsKey (dir)) {
				event_map[dir] -= notify;	// any need to try/catch here?
				if(event_map[dir].GetInvocationList ().Length == 0)
					client.RemoveNotify(dir, HandleNotify);
				// TODO: When list is empty, remove key from dictionary?
			}
		}

		public void SuggestSync ()
		{
			client.SuggestSync ();
		}

		private void HandleNotify(object sender, GConf.NotifyEventArgs args)
		{
			foreach(string key in event_map.Keys)
				if(args.Key.StartsWith (key)) {
					NotifyEventArgs newArgs = new NotifyEventArgs(args.Key, args.Value);
					event_map[key](sender, newArgs);
				}
		}
	}

	public class GConfPropertyEditorToggleButton : GConf.PropertyEditors.PropertyEditorToggleButton, IPropertyEditorBool
	{
		public GConfPropertyEditorToggleButton (string key, Gtk.CheckButton sourceButton) : base (key, sourceButton) {}
	}

   	public class GConfPropertyEditorEntry : GConf.PropertyEditors.PropertyEditorEntry, IPropertyEditor
	{
   		public GConfPropertyEditorEntry (string key, Gtk.Entry sourceEntry) : base (key, sourceEntry) { }
	}
}
