using System;
using Gtk;

public partial class MainWindow: Gtk.Window
{	
	public MainWindow (): base (Gtk.WindowType.Toplevel)
	{
		Build ();
	}
	
	protected void OnDeleteEvent (object sender, DeleteEventArgs a)
	{
		Application.Quit ();
		a.RetVal = true;
	}

	protected virtual void QuitMenu(object sender, System.EventArgs e)
	{
		Application.Quit();
	}

	protected virtual void ConnectToGridServerMenu(object sender, System.EventArgs e)
	{
			ConnectToGridServerDialog griddialog = new ConnectToGridServerDialog ();
			griddialog.Show();
	}



}