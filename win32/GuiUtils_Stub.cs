
using System;
using System.Collections;
using System.Text;

using System.IO;
using System.Xml;

namespace Tomboy
{
        public class GuiUtils 
        {
                public static void GetMenuPosition (Gtk.Menu menu, 
                                                    out int  x, 
                                                    out int  y, 
                                                    out bool push_in)
                {
                        Gtk.Requisition menu_req = menu.SizeRequest ();

                        menu.AttachWidget.GdkWindow.GetOrigin (out x, out y);

                        if (y + menu_req.Height >= menu.AttachWidget.Screen.Height)
                                y -= menu_req.Height;
                        else
                                y += menu.AttachWidget.Allocation.Height;

                        push_in = true;
                }
                
                public static void DetachMenu (Gtk.Widget attach, Gtk.Menu menu)
		{
			// Do nothing.  Callers can use this to work around a
			// Gtk#2 binding bug requiring a non-null detach
			// delegate when calling Gtk.Menu.AttachToWidget.
		}

                static void DeactivateMenu (object sender, EventArgs args) 
                {
                        Gtk.Menu menu = (Gtk.Menu) sender;
                        menu.Popdown ();

                        // Unhighlight the parent
                        if (menu.AttachWidget != null)
                                menu.AttachWidget.State = Gtk.StateType.Normal;
                }

                // Place the menu underneath an arbitrary parent widget.  The
                // parent widget must be set using menu.AttachToWidget before
                // calling this
                public static void PopupMenu (Gtk.Menu menu, Gdk.EventButton ev)
                {
                        menu.Deactivated += DeactivateMenu;
                        menu.Popup (null, 
                                    null, 
                                    null, //new Gtk.MenuPositionFunc (GetMenuPosition), 
                                    IntPtr.Zero, 
                                    (ev == null) ? 0 : ev.Button, 
                                    (ev == null) ? Gtk.Global.CurrentEventTime : ev.Time);

                        // Highlight the parent
                        if (menu.AttachWidget != null)
                                menu.AttachWidget.State = Gtk.StateType.Selected;
                }

                public static Gdk.Pixbuf GetIcon (string resource_name) 
                {
                        return new Gdk.Pixbuf (null, resource_name);
                }

                public static Gdk.Pixbuf GetMiniIcon (string resource_name) 
                {
                        Gdk.Pixbuf noicon = new Gdk.Pixbuf (null, resource_name);
                        return noicon.ScaleSimple (24, 24, Gdk.InterpType.Nearest);
                }

                public static Gtk.Button MakeImageButton (Gtk.Image image, string label)
                {
                        Gtk.HBox box = new Gtk.HBox (false, 2);
                        box.PackStart (image, false, false, 0);
                        box.PackEnd (new Gtk.Label (label), false, false, 0);
                        box.ShowAll ();

                        Gtk.Button button = new Gtk.Button ();

                        Gtk.Alignment align = new Gtk.Alignment (0.5f, 0.5f, 0.0f, 0.0f);
                        align.Add (box);
                        align.Show ();

                        button.Add (align);
                        return button;
                }                       

                public static Gtk.Button MakeImageButton (string stock_id, string label)
                {
                        Gtk.Image image = new Gtk.Image (stock_id, Gtk.IconSize.Button);
                        return MakeImageButton (image, label);
                }

                public static System.Type GetNeighbourType(System.Type type, string name)
                {
                        return type.Assembly.GetType(name);
                }
        }
}
