using System;
using Gtk;

namespace OpenGridServices.Manager {
	public partial class MainWindow: Gtk.Window
	{	
		public MainWindow (): base (Gtk.WindowType.Toplevel)
		{
			Build ();
		}
		
		public void SetStatus(string statustext)
		{
			this.statusbar1.Pop(0);
			this.statusbar1.Push(0,statustext);
		}

		protected void OnDeleteEvent (object sender, DeleteEventArgs a)
		{
			Application.Quit ();
			MainClass.QuitReq=true;
			a.RetVal = true;
		}
	
		protected virtual void QuitMenu(object sender, System.EventArgs e)
		{
			MainClass.QuitReq=true;
			Application.Quit();
		}
	
		protected virtual void ConnectToGridServerMenu(object sender, System.EventArgs e)
		{
			ConnectToGridServerDialog griddialog = new ConnectToGridServerDialog ();
			griddialog.Show();

		}

		protected virtual void RestartGridserverMenu(object sender, System.EventArgs e)
		{
			MainClass.PendingOperations.Enqueue("restart_gridserver");
		}

	}
}

	