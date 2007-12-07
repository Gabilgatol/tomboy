using System;

namespace Tomboy.Platform
{
	public interface INativeApplication
	{
		void Initialize (string locale_dir,
		                 string display_name,
		                 string process_name,
		                 string [] args);

		void RegisterSessionManagerRestart (string executable_path,
		                                    string[] args,
		                                    string[] environment);
		void RegisterSignalHandlers ();
		event EventHandler ExitingEvent;

		void Exit (int exitcode);
		void StartMainLoop ();
		void QuitMainLoop ();
	}
}
