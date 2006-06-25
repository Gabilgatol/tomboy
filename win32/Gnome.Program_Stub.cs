namespace Gnome
{
	public class Program
	{
		public Program(string name, string version, Modules modules, string[] args)
		{
			Gtk.Application.Init();
		}

		public void Run()
		{
			Gtk.Application.Run();
		}
	}

	public enum Modules
	{
		Console, UI
	}
}

