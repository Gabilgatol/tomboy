using System;
using System.Collections;

using Mono.Zeroconf;

namespace Tomboy.Sharing
{
	public class ServiceLocator
	{
		private static ServiceLocator instance = null;
		private static string locker = "locker";
		
		private ServiceBrowser browser;

		private Hashtable services;
		
		public bool Running
		{
			get { return (browser != null); }
		}
		
		public event ServiceFoundEventHandler ServiceFound;
		public event ServiceRemovedEventHandler ServiceRemoved;
		public event ServiceUpdatedEventHandler ServiceUpdated;
		
		public TomboyService [] Services
		{
			get {
				ArrayList list = new ArrayList (services.Values);
				return list.ToArray (typeof (TomboyService)) as TomboyService [];
			}
		}
		
		public void Start ()
		{
			if (browser != null) {
				Stop ();
			}

			Logger.Log ("Starting up the ServiceLocator (watching for shared Tomboy clients)");
			
			browser = new ServiceBrowser (TomboyService.SERVICE_TYPE);
			browser.ServiceAdded += OnServiceAdded;
			browser.ServiceRemoved += OnServiceRemoved;
			browser.StartAsync ();
		}
		
		public void Stop ()
		{
			if (browser == null)
				return;

			Logger.Log ("Shutting down the ServiceLocator");
			
			browser.Dispose ();
			browser = null;
			services.Clear ();
		}
		
		public static ServiceLocator GetInstance ()
		{
			if (instance == null) {
				lock (locker) {
					if (instance == null) {
						instance = new ServiceLocator ();
					}
				}
			}
			
			return instance;
		}
		
		private ServiceLocator ()
		{
			services = Hashtable.Synchronized (new Hashtable ());
			
			Tomboy.ExitingEvent += OnExitingEvent;
		}
		
		private void OnServiceAdded (object sender, ServiceBrowseEventArgs args)
		{
			args.Service.Resolved += OnServiceResolved;
			args.Service.Resolve ();
		}
		
		private void OnServiceResolved (object sender, EventArgs args)
		{
Logger.Debug ("ServiceLocator.OnServiceResolved");
			BrowseService zc_service = sender as BrowseService;
			
			if (zc_service.Name.CompareTo (TomboyService.LocalInstance.Guid) == 0)
				return;	// Don't do anything with ourself
			
			if (services [zc_service.Name] != null) {
				UpdateService (zc_service);
				return; // we already have it somehow
			}
			
			TomboyService tomboy_service = CreateTomboyService (zc_service);
			if (tomboy_service == null) {
Logger.Debug ("ServiceLocator.OnServiceResolved: CreateTomboyService returned null!");
				return;	// something went wrong
			}
			
			services [zc_service.Name] = tomboy_service;
			if (ServiceFound != null)
				ServiceFound (this, new ServiceEventArgs (tomboy_service));
		}
		
		private void UpdateService (BrowseService zc_service)
		{
Logger.Debug ("ServiceLocator.UpdateService");
			if (services [zc_service.Name] == null)
				return;	// weird!
			
			TomboyService tomboy_service = CreateTomboyService (zc_service);
			if (tomboy_service == null) {
Logger.Debug ("ServiceLocator.UpdateService: CreateTomboyService returned null!");
				return; // something went wrong
			}
			
			services [tomboy_service.Name] = tomboy_service;
			if (ServiceUpdated != null)
				ServiceUpdated (this, new ServiceEventArgs (tomboy_service));
		}
		
		private TomboyService CreateTomboyService (BrowseService zc_service)
		{
Logger.Debug ("ServiceLocator.CreateTomboyService");
			TomboyService service = new TomboyService ();
			
			// IPAddress
			service.IPAddress = zc_service.HostEntry.AddressList [0];
			
			// Port
			service.Port = (short) zc_service.Port;
			
			// Text Records
			foreach (TxtRecordItem item in zc_service.TxtRecord) {
				switch (item.Key) {
				case TomboyService.TXT_GUID:
Logger.Debug ("    Guid = {0}", item.ValueString);
					service.Guid = item.ValueString;
					break;
				case TomboyService.TXT_NAME:
Logger.Debug ("    Name = {0}", item.ValueString);
					service.Name = item.ValueString;
					break;
				case TomboyService.TXT_PASSWORD_PROTECTED:
Logger.Debug ("    PasswordProtected = {0}", item.ValueString);
					service.PasswordProtected = Boolean.Parse (item.ValueString);
					break;
				case TomboyService.TXT_SHARING_ENABLED:
Logger.Debug ("    SharingEnabled = {0}", item.ValueString);
					service.SharingEnabled = Boolean.Parse (item.ValueString);
					break;
				case TomboyService.TXT_REVISION:
Logger.Debug ("    Revision = {0}", item.ValueString);
					service.Revision = UInt32.Parse (item.ValueString);
					break;
				}
			}
			
			return service;
		}
		
		private void OnServiceRemoved (object sender, ServiceBrowseEventArgs args)
		{
			if (args.Service.Name.CompareTo (TomboyService.LocalInstance.Guid) == 0)
				return;	// Don't do anything with ourself

Logger.Debug ("ServiceLocator.OnServiceRemoved");
			if (!services.ContainsKey (args.Service.Name)) {
Logger.Debug ("    Returning since we didn't know about this service in the first place");
				return;
			}
			
			TomboyService tomboy_service = services [args.Service.Name] as TomboyService;
			services.Remove (args.Service.Name);
			
			if (ServiceRemoved != null)
				ServiceRemoved (this, new ServiceEventArgs (tomboy_service));
		}

		private void OnExitingEvent (object sender, EventArgs args)
		{
			Stop ();
		}
	}
	
	public delegate void ServiceFoundEventHandler (object sender, ServiceEventArgs args);
	public delegate void ServiceRemovedEventHandler (object sender, ServiceEventArgs args);
	public delegate void ServiceUpdatedEventHandler (object sender, ServiceEventArgs args);
	
	public class ServiceEventArgs : EventArgs
	{
		private TomboyService service;
		
		public TomboyService Service
		{
			get { return service; }
		}
		
		public ServiceEventArgs (TomboyService service)
		{
			this.service = service;
		}
	}
}