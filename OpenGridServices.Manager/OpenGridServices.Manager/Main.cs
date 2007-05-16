// project created on 5/14/2007 at 2:04 PM
using System;
using Gtk;

namespace OpenGridServices.Manager
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Application.Init ();
			MainWindow win = new MainWindow ();
			win.Show ();
			Application.Run ();
		}
	}
}