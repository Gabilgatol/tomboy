namespace Tomboy
{
	class PlatformFactory
	{
		public static IPropertyEditor CreatePropertyEditorEntry (string key, Gtk.Entry sourceEntry)
		{
			return new PropertyEditorEntry (key, sourceEntry);
		}

		public static IPropertyEditorBool CreatePropertyEditorToggleButton (
		        string key, Gtk.CheckButton sourceButton)
		{
			return new PropertyEditorToggleButton (key, sourceButton);
		}

		public static IPreferencesClient CreatePreferencesClient ()
		{
			return new XmlPreferencesClient ();
			//return new NullPreferencesClient ();
		}

		public static INativeApplication CreateNativeApplication ()
		{
			return new WindowsApplication ();
			// or GtkApplication
		}

		public static IKeybinder CreateKeybinder ()
		{
			return new NullKeybinder ();
			// or NullKeybinder
			// (consider having a separate file+class
			// for NeutralPlatformFactory vs GnomePlatformFactory)
		}
	}
}
