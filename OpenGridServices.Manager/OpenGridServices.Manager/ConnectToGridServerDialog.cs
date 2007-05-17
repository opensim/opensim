using Gtk;
using System;

namespace OpenGridServices.Manager {
	public partial class ConnectToGridServerDialog : Gtk.Dialog
	{
		
		public ConnectToGridServerDialog()
		{
			this.Build();
		}

		protected virtual void OnResponse(object o, Gtk.ResponseArgs args)
		{
			switch(args.ResponseId) {
				case Gtk.ResponseType.Ok:
					MainClass.PendingOperations.Enqueue("connect_to_gridserver " + this.entry1.Text);
				break;
				
				case Gtk.ResponseType.Cancel:
				break;
			}
			this.Hide();
			
		}

	}

}