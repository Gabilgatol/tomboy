namespace Gnome
{
        public class About : Gtk.Dialog
        {
                public About(string name, string version,
                        string copyright, string description,
                        string[] authors, string[] documenters, string transators,
                        Gdk.Pixbuf icon)
                {
                        Title = "About " + name;
                        VBox.PackStart(new Gtk.Label(name));
                        VBox.PackStart(new Gtk.Label("Version: " + version));
                        VBox.PackStart(new Gtk.Label(copyright));
                        VBox.PackStart(new Gtk.HSeparator());
                        VBox.PackStart(new Gtk.Label(description));
                        AddButton(Gtk.Stock.Close, Gtk.ResponseType.Close);
                }

                public new void Show() {
                        Modal = true;
                        ShowAll();
                        Run();
                        Destroy();
                }
        }
}