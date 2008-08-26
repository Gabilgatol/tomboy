namespace Tomboy
{
	class PlatformFactory
	{
		public static IPropertyEditor CreatePropertyEditorEntry (string key, Gtk.Entry sourceEntry)
		{
			return new GConfPropertyEditorEntry (key, sourceEntry);
		}

		public static IPropertyEditorBool CreatePropertyEditorToggleButton (
		        string key, Gtk.CheckButton sourceButton)
		{
			return new GConfPropertyEditorToggleButton (key, sourceButton);
		}

		public static IPreferencesClient CreatePreferencesClient ()
		{
			return new GConfPreferencesClient ();
		}

		public static INativeApplication CreateNativeApplication ()
		{
			return new GnomeApplication ();
			// or GtkApplication
		}

		public static IKeybinder CreateKeybinder ()
		{
			return new XKeybinder ();
			// or NullKeybinder
			// (consider having a separate file+class
			// for NeutralPlatformFactory vs GnomePlatformFactory)
		}
	}
}
