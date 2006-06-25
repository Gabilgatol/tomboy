namespace Gnome
{
	public class Client
	{
		public void SetRestartCommand(int count, string[] args)
		{
		}

		public RestartStyle RestartStyle;
	}

	public enum RestartStyle {
		IfRunning
	}

	public class Global
	{
		public static Client MasterClient() {
			return new Client();
		}
	}
}