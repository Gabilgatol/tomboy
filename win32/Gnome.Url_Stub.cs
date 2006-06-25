using System.Diagnostics;

namespace Gnome
{
	public class Url
	{
		public static void Show(string url)
		{
			Process.Start(url);
		}
	}
}