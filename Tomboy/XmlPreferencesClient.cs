// Permission is hereby granted, free of charge, to any person obtaining 
// a copy of this software and associated documentation files (the 
// "Software"), to deal in the Software without restriction, including 
// without limitation the rights to use, copy, modify, merge, publish, 
// distribute, sublicense, and/or sell copies of the Software, and to 
// permit persons to whom the Software is furnished to do so, subject to 
// the following conditions: 
//  
// The above copyright notice and this permission notice shall be 
// included in all copies or substantial portions of the Software. 
//  
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE 
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION 
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 
// 
// Copyright (c) 2008 Novell, Inc. (http://www.novell.com) 
// 
// Authors: 
//      Sandy Armstrong <sanfordarmstrong@gmail.com>
// 


using System;
using System.Collections.Generic;
using System.Xml;

namespace Tomboy
{
	public class XmlPreferencesClient : IPreferencesClient
	{
		#region Private Members
		
		private string fileName;
		private XmlDocument prefsDoc;
		private Dictionary<string, NotifyEventHandler> events;

		#endregion

		#region Constructor

		public XmlPreferencesClient ()
		{

		}
		
		#endregion
		
		#region IPreferencesClient implementation
		
		public void Set (string key, object value)
		{
			try {
				CreatePath (key);
				prefsDoc.SelectSingleNode (key).InnerText = System.Convert.ToString(value);
				prefsDoc.Save(fileName);
				foreach (string nkey in events.Keys) {
					NotifyEventHandler handler = events [nkey] as NotifyEventHandler;
					if (handler != null && key.StartsWith (nkey))
					{
						NotifyEventArgs args = new NotifyEventArgs (key, value);
						handler (this, args);
					}
				}
			}
			catch {}
		}
		
		public object Get (string key)
		{
			try {
				XmlElement element = prefsDoc.SelectSingleNode(key) as XmlElement;
				if (element != null) {
					if (element.InnerText.ToLower () == "true")
						return true;
					if (element.InnerText.ToLower () == "false")
						return false;
					return element.InnerText;
				}
				
				throw new System.Exception();
			} catch {
				throw new NoSuchKeyException (key);
			}

		}
		
		public void AddNotify (string dir, NotifyEventHandler notify)
		{
			lock (events) {
				events [dir] += notify;
			}
		}
		
		public void RemoveNotify (string dir, NotifyEventHandler notify)
		{
			lock (events) {
				events [dir] -= notify;
			}
		}
		
		public void SuggestSync ()
		{
			// TODO: Sync with file?
		}
		
		#endregion

		#region Private Methods

		private void CreatePath(string path)
		{
			if (path.Length == 0)
				return;
			if (path [0] == '/') 
				path = path.Substring (1);
			if (path.Length == 0)
				return;

			string [] parts = path.Split ('/');
			XmlNode node = prefsDoc;
			for (int i = 0; i < parts.Length; ++i)
			{
				if (node [parts [i]] == null)
				{
					node.AppendChild (prefsDoc.CreateElement (parts [i]));
				}
				node = node [parts [i]];
			}
		}

		#endregion
	}
}
