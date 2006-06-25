using System;
using System.Collections;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Xml;
using Mono.Unix;

namespace Gnome
{
        public enum PanelAppletBackgroundType {
        
                NoBackground,
                ColorBackground,
                PixmapBackground,
        }    
        
        public abstract class PanelApplet : IDisposable
        {
                private NotifyIcon icon;
                private Gtk.Widget tray;
                private MouseButtons lastbuttons;
                private Gtk.Menu right_click_menu;
                private Gtk.Menu prev_menu;
                
                public PanelApplet(IntPtr raw)
                {
                        icon = new NotifyIcon();
                        icon.Icon = new Icon(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("tintin.ico"));
                        icon.Text = Catalog.GetString ("Tomboy Notes");
                        icon.Click += new EventHandler(OnClick);
                        icon.MouseDown += new MouseEventHandler(OnMouseDown);
                }
                
                public void Add(Gtk.Widget item) {
                        tray = item;
                }
                
                public void ShowAll() {
                        System.Console.WriteLine("Showing icon");
                        icon.Visible = true;
                }
                
                private void CancelPrevMenu()
                {
                        if (prev_menu != null) {
                                Console.WriteLine ("Popping down prev_menu");
                                prev_menu.Popdown();
                                prev_menu = null;
                        }
                }
                
                private void OnClick(object sender, EventArgs args)
                {
                        System.Console.WriteLine(lastbuttons);
                        CancelPrevMenu();
                        
                        if ((lastbuttons & MouseButtons.Right) != 0) {
                                Console.WriteLine ("prev_menu = right_click_menu");
                                prev_menu = right_click_menu;
                        }
                        else {
                                System.Reflection.MethodInfo method = tray.GetType().GetMethod("MakeRecentNotesMenu",
                                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                                prev_menu = (Gtk.Menu)method.Invoke(tray, new object[] { tray });
                        }
                        
                        if (prev_menu != null)
                                Tomboy.GuiUtils.PopupMenu(prev_menu, null);
                }
                
                private void OnMouseDown(object sender, MouseEventArgs args)
                {
                        System.Console.WriteLine(args.Button);
                        lastbuttons = args.Button;
                }
                
                public void SetupMenuFromFile(string dataDir, string fileName, string appName, IList verbs) 
                {
                        Gtk.ImageMenuItem item;
                        right_click_menu = new Gtk.Menu();
                        try
                        {
                                using (TextReader reader = new StreamReader(Path.Combine(dataDir, fileName)))
                                {
                                        XmlTextReader xreader = new XmlTextReader(reader);
                                        xreader.WhitespaceHandling = WhitespaceHandling.None;
                                        xreader.ReadStartElement("Root");
                                        xreader.ReadStartElement("popups");
                                        xreader.ReadStartElement("popup");
                                        while (xreader.IsStartElement("menuitem"))
                                        {
                                                item = new Gtk.ImageMenuItem(Catalog.GetString(xreader["_label"]));
                                                item.Image = new Gtk.Image(xreader["pixname"], Gtk.IconSize.Menu);
                                                foreach (BonoboUIVerb verb in verbs)
                                                {
                                                        if (verb.Name == xreader["verb"])
                                                        item.Activated += new ContextMenuItemCallbackWrapper(verb.Callback).Handler;
                                                        }
                                                        right_click_menu.Append(item);
                                                        xreader.ReadOuterXml();
                                                }
                                }
                        }
                        catch (System.Exception e)
                        {
                                System.Console.WriteLine(e);
                        }
                        
                        right_click_menu.Append(new Gtk.SeparatorMenuItem());
                        
                        item = new Gtk.ImageMenuItem(Catalog.GetString("_Quit"));
                        item.Activated += new EventHandler(Quit);
                        right_click_menu.Append(item);
                        
                        right_click_menu.ShowAll();
                }
                
                private void Quit(object sender, EventArgs args)
                {
                        Type type = Tomboy.GuiUtils.GetNeighbourType(tray.GetType(), "Tomboy.Tomboy");
                        System.Reflection.MethodInfo method = type.GetMethod("Exit",
                        System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public);
                        prev_menu = (Gtk.Menu)method.Invoke(null, new object[] { 0 });
                }
                
                public void Dispose()
                {
                        if (icon != null) { 
                                icon.Visible = false;
                                icon.Dispose();
                        }
                }
                
                public void ModifyStyle(params object[] args) {}
                
                public void ModifyBg(params object[] args) {}
                
                public abstract void Creation();
                public abstract string IID { get; }
                public abstract string FactoryIID { get; }
                
                protected abstract void OnChangeBackground(PanelAppletBackgroundType type, Gdk.Color color, Gdk.Pixmap pixmap);
        }
        
        public class PanelAppletFactory {
                public static void Register(System.Type type)
                {
                        PanelApplet applet;
                        applet = System.Activator.CreateInstance(type, new object[] { IntPtr.Zero }) as PanelApplet;
                        applet.Creation();
                        using (applet)
                        {
                                Gtk.Application.Run();
                        }
                }
        }


        /*public class ChangeBackgroundArgs : System.EventArgs
        {
          public PanelAppletBackgroundType Type;
          public Gdk.Color Color;
        }*/
        
        public class BonoboUIVerb
        {
                public string Name;
                public ContextMenuItemCallback Callback;
                
                public BonoboUIVerb(string name, ContextMenuItemCallback cb)
                {
                        Name = name;
                        Callback = cb;
                }
        }
        
        public delegate void ContextMenuItemCallback();
        
        // TODO: This isn't really from the Gnome namespace
        public class ContextMenuItemCallbackWrapper
        {
                ContextMenuItemCallback cb;
                
                public ContextMenuItemCallbackWrapper(ContextMenuItemCallback cb)
                {
                        this.cb = cb;
                }
                
                public void HandleEvent(object sender, EventArgs args)
                {
                        if (cb != null)
                                cb();
                }
                
                public EventHandler Handler
                {
                        get {
                                return new EventHandler(HandleEvent);
                        }
                }
        }
}
