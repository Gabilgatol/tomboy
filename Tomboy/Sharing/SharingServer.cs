using System;
using System.IO;
using System.Net;
using Mono.Zeroconf;
using Mono.WebServer;

namespace Tomboy.Sharing
{
	/// <summary>
	/// This class is responsible for:
	///     1) Running an embedded HTTP Server so other Tomboy Clients can
	///        communicate with this Tomboy Client.
	///     2) Advertising this Tomboy Client via mDNS using the information
	///        inside the TomboyService object passed in the constructor.
	/// </summary>
	public class SharingServer
	{
		private static SharingServer instance = null;
		private static string locker = "locker";

		private TomboyService service;
		private bool running;
		
		private RegisterService zc_service;
		private object zc_lock = new object ();
		
		private ApplicationServer web_app_server;
		private int port;
		private string path;
		
		public event EventHandler NameCollision;
		
		public static SharingServer GetInstance ()
		{
			if (instance == null) {
				lock (locker) {
					if (instance == null) {
						instance = CreateInstance ();
					}
				}
			}
			
			return instance;
		}

		public TomboyService Service
		{
			get { return service; }
		}
		
		public bool Running
		{
			get { return running; }
		}
		
		/// <summary>
		/// Start the 
		/// </summary>
		public void Start ()
		{
			if (running)
				return;

			Logger.Log ("Starting up the SharingServer (shared notes being published)");

			StartWebServer ();
//			if (!StartWebServer ()) {
//				Logger.Log ("FIXME: StartWebServer failed.  Figure out what to do");
//			}
			
			RegisterService ();
			running = true;
		}
		
		public void Stop ()
		{
			if (!running)
				return;

			Logger.Log ("Shutting down the SharingServer");

			StopWebServer ();
			
			UnregisterService ();
			running = false;
		}
		
		private SharingServer (TomboyService service)
		{
			this.service = service;
			bool running = false;

			port = 8088;	// FIXME: Get this stored in GConf
			path = Environment.CurrentDirectory + "/../lib/tomboy/web";

//			path = "." + Path.DirectorySeparatorChar.ToString ();
			
			service.Port = (short)port;
		}
		
		private static SharingServer CreateInstance ()
		{
			TomboyService local_service = TomboyService.LocalInstance;
			SharingServer server = new SharingServer (local_service);
			
			local_service.PortChanged += server.OnPortChanged;
			local_service.GuidChanged += server.OnGuidChanged;
			local_service.NameChanged += server.OnNameChanged;
			local_service.PasswordProtectedChanged += server.OnPasswordProtectedChanged;
			local_service.SharingStatusChanged += server.OnSharingStatusChanged;
			local_service.RevisionChanged += server.OnRevisionChanged;
			
			Tomboy.ExitingEvent += server.OnExitingEvent;
			
			return server;
		}
		
		private void RegisterService ()
		{
			lock (zc_lock) {
				if (zc_service != null)
					UnregisterService ();
				
				zc_service = new RegisterService (service.Guid, null, 
												TomboyService.SERVICE_TYPE);
				zc_service.Port = service.Port; // FIXME: This should be the port that the web server is running on
				zc_service.TxtRecord = new TxtRecord ();
				zc_service.TxtRecord.Add (TomboyService.TXT_GUID, service.Guid);
				zc_service.TxtRecord.Add (TomboyService.TXT_NAME, service.Name);
				zc_service.TxtRecord.Add (TomboyService.TXT_PASSWORD_PROTECTED,
										  service.PasswordProtected.ToString ());
				zc_service.TxtRecord.Add (TomboyService.TXT_SHARING_ENABLED,
										  service.SharingEnabled.ToString ());
				zc_service.TxtRecord.Add (TomboyService.TXT_REVISION,
										  service.Revision.ToString ());
				zc_service.Response += OnRegisterServiceResponse;
				zc_service.AutoRename = false;
				zc_service.RegisterAsync ();
			}
		}
		
		private void UnregisterService ()
		{
			lock (zc_lock) {
				if (zc_service == null)
					return;
				
				try {
					zc_service.Dispose ();
				} catch {
				} finally {
					zc_service = null;
				}
			}
		}
		
		private bool StartWebServer ()
		{
			bool status = false;
			try {
				XSPWebSource web_source = new XSPWebSource (IPAddress.Any, port);
				web_app_server = new ApplicationServer (web_source);
				
				string cmd_line;
				// Check the command-line to see if there was a specific path specified
				string custom_path = GetCustomWebServicePath ();
				if (custom_path != null)
					cmd_line = port.ToString () + ":/tomboy:" + custom_path;
				else
					cmd_line = port.ToString () + ":/tomboy:" + path;
				
				Logger.Debug ("Command Line: {0}", cmd_line);
				web_app_server.AddApplicationsFromCommandLine (cmd_line);
				web_app_server.Start (true);
				Logger.Log ("Mono.WebServer running");
				
				PingLocalServer ();
				status = true;
			} catch (Exception e) {
				Logger.Log ("Exception starting Mono.WebServer: {0}", e.Message);
			}
			
			return status;
		}
		
		private void StopWebServer ()
		{
			if (web_app_server != null) {
				Logger.Log ("Shutting down Mono.WebServer");
				web_app_server.Stop ();
				web_app_server = null;
			}
		}
		
		private string GetCustomWebServicePath ()
		{
			string custom_path = null;

			string [] args = Environment.GetCommandLineArgs ();
			foreach (string arg in args) {
				if (arg.StartsWith ("--webdir=")) {
					try {
						custom_path = arg.Substring (9).Trim ();
						if (custom_path == String.Empty)
							custom_path = null;
					} catch {}
				}
			}
			
			return custom_path;
		}
		
		private void PingLocalServer ()
		{
			HttpWebResponse response = null;

			Uri ping_uri = new Uri (string.Format ("localhost:{0}/tomboy/Tomboy.asmx", port.ToString()), false);
			HttpWebRequest request = WebRequest.Create (ping_uri) as HttpWebRequest;
			request.CookieContainer = new CookieContainer();
			request.Credentials = null;
			
			request.Method = "GET";
			request.ContentLength = 0;

			try {
				request.GetRequestStream ().Close ();
				response = request.GetResponse () as HttpWebResponse;
			} catch( WebException webEx ) {
				// Should catch an exception
				// Changed the test for a mono bug
				//if (webEx.Status == WebExceptionStatus.TrustFailure)
				
				Logger.Debug ("Should be an exception here");
				response = webEx.Response as HttpWebResponse;
				if (response.ContentLength == 0) {
				}
				
				response.Close ();
			} catch(Exception ex) {
				Logger.Log (ex.Message);
				Logger.Log (ex.StackTrace);
			}
		}
		
		private void OnRegisterServiceResponse (object sender, RegisterServiceEventArgs args)
		{
			if (args.NameConflict && NameCollision != null) {
				Logger.Log ("SharingServer encountered a NameCollision: {0}", service.Name);
				NameCollision (this, EventArgs.Empty);
			}
		}
		
		private void OnPortChanged (object sender, EventArgs args)
		{
			// Re-register if we're running so that we start up on a different port
			if (running)
				RegisterService (); // This does an unregister and then registers
		}
		
		private void OnGuidChanged (object sender, EventArgs args)
		{
Logger.Debug ("FIXME: Figure out why SharingServer.OnGuidChanged is never being called");
			if (running)
				RegisterService ();
		}
		
		private void OnNameChanged (object sender, EventArgs args)
		{
			if (running)
				RegisterService ();
		}
		
		private void OnPasswordProtectedChanged (object sender, EventArgs args)
		{
			Logger.Debug ("FIXME: Implement SharingServer.OnPasswordProtectedChanged to update the text record");
		}
		
		private void OnSharingStatusChanged (object sender, EventArgs args)
		{
			Logger.Debug ("FIXME: Implement SharingServer.OnSharingStatusChanged to update the text record");
		}
		
		private void OnRevisionChanged (object sender, EventArgs args)
		{
			Logger.Debug ("FIXME: Implement SharingServer.OnRevisionChanged to update the text record");
		}
		
		private void OnExitingEvent (object sender, EventArgs args)
		{
			if (running)
				Stop ();
		}
	}
}