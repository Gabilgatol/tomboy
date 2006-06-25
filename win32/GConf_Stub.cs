using System.Xml;
using System.Collections;

namespace GConf
{
	public class NoSuchKeyException : System.Exception
	{
		public NoSuchKeyException(string key) : base(string.Format("No such key: {0}", key))
		{}
	}

	public class ClientDriver {
		XmlDocument document = new XmlDocument();
		string fileName;
		private Hashtable events = new Hashtable();

		public static readonly ClientDriver Instance = new ClientDriver();

		private ClientDriver()
		{
			try {
				fileName = "tomboy.xml";
				document.Load(fileName);
			}
			catch {
			}
		}

		public object Get(string key)
		{
			try
			{
			    XmlElement element = document.SelectSingleNode(key) as XmlElement;
			    if (element != null)
			    {
			    	if (element.InnerText.ToLower() == "true") return true;
			    	if (element.InnerText.ToLower() == "false") return false;
			    	return element.InnerText;
			    }
			    throw new System.Exception();
			}
			catch
			{
				throw new NoSuchKeyException(key);
			}
		}

		public void Set(string key, object value)
		{
			try {
				CreatePath(key);
				document.SelectSingleNode(key).InnerText = System.Convert.ToString(value);
				document.Save(fileName);
				foreach (string nkey in events.Keys) {
					NotifyEventHandler handler = events[nkey] as NotifyEventHandler;
					if (handler != null && key.StartsWith(nkey))
					{
						NotifyEventArgs args = new NotifyEventArgs(key, value);
						handler(this, args);
					}
				}
			}
			catch {}
		}

		private void CreatePath(string path)
		{
			if (path.Length == 0) return;
			if (path[0] == '/') path = path.Substring(1);
			if (path.Length == 0) return;

			string[] parts = path.Split('/');
			XmlNode node = document;
			for (int i = 0; i < parts.Length; ++i)
			{
				if (node[parts[i]] == null)
				{
					node.AppendChild(document.CreateElement(parts[i]));
				}
				node = node[parts[i]];
			}
		}

		public void AddNotify(string key, NotifyEventHandler handler)
		{
			lock (events) {
				NotifyEventHandler evt = events[key] as NotifyEventHandler;
				evt += handler;
				events[key] = evt;
			}
		}		
	}

	public class Client
	{
		public Client() {}
		
		public object Get(string key)
		{
			return ClientDriver.Instance.Get(key);
		}

		public void Set(string key, object value)
		{
			ClientDriver.Instance.Set(key, value);
		}

		public void AddNotify(string key, NotifyEventHandler handler)
		{
			ClientDriver.Instance.AddNotify(key, handler);
		}
	}

	public class NotifyEventArgs : System.EventArgs {
		public readonly string Key;
		public readonly object Value;

		public NotifyEventArgs(string key, object value) {
			Key = key;
			Value = value;
		}
	} 

	public delegate void NotifyEventHandler(object sender, NotifyEventArgs args);
}

namespace GConf.PropertyEditors
{
   	public class PropertyEditor {
   		public string Key;

   		public PropertyEditor(string key) {
   			Key = key;
   		}

   		public virtual void Setup()	{
   		}

   		protected object Get() {
			GConf.Client client = new GConf.Client();
			return client.Get(Key);
   		}

   		protected void Set(object value)
   		{
   			GConf.Client client = new GConf.Client();
   			client.Set(Key, value);
   		}
   	} 

   	public class PropertyEditorBool : PropertyEditor {
   		protected ArrayList guards = new ArrayList();
   		
   		public PropertyEditorBool(string key) : base(key) {}

   		public virtual void AddGuard(Gtk.Widget widget)
   		{
   			guards.Add(widget);
   		}

   		protected void Set(bool value)
   		{
   			base.Set(value);
   			UpdateGuards(value);
   		}

   		protected void UpdateGuards()
   		{
   			UpdateGuards(Get());
   		}

   		private void UpdateGuards(bool value) {
   			foreach (Gtk.Widget widget in guards)
   				widget.Sensitive = value;
   		}

   		protected new bool Get() {
   			return (bool)base.Get();
   		}
   	}

   	public class PropertyEditorToggleButton : PropertyEditorBool {
   		Gtk.CheckButton button;

   		public PropertyEditorToggleButton(string key, Gtk.CheckButton sourceButton) :
   			base(key) {
   			button = sourceButton;
   		}	

   		public override void Setup()
   		{
   			button.Active = Get();
   			button.Clicked += new System.EventHandler(OnChanged);
   			UpdateGuards();
   		}

   		private void OnChanged(object sender, System.EventArgs args)
   		{
   			Set(button.Active);
   		}
   	}

   	public class PropertyEditorEntry : PropertyEditor {
   		Gtk.Entry entry;
   		public PropertyEditorEntry(string key, Gtk.Entry sourceEntry) :
   			base(key) {
   			entry = sourceEntry;
   		}

   		public override void Setup() {
   			entry.Text = System.Convert.ToString(base.Get());
   			entry.Changed += OnChanged;
   		}

   		private void OnChanged(object sender, System.EventArgs args) {
   			Set(entry.Text);
   		}
   	}
}
