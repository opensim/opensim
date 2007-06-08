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

		public void DrawGrid(RegionBlock[][] regions)
		{
			for(int x=0; x<=regions.GetUpperBound(0); x++) {
			 for(int y=0; y<=regions.GetUpperBound(1); y++) {
				Gdk.Image themap = new Gdk.Image(Gdk.ImageType.Fastest,Gdk.Visual.System,256,256);
				this.drawingarea1.GdkWindow.DrawImage(new Gdk.GC(this.drawingarea1.GdkWindow),themap,0,0,x*256,y*256,256,256);
			 }
			}
		}
		
		public void SetGridServerConnected(bool connected)
		{
			if(connected) {
				this.ConnectToGridserver.Visible=false;
				this.DisconnectFromGridServer.Visible=true;
			} else {
				this.ConnectToGridserver.Visible=true;
				this.DisconnectFromGridServer.Visible=false;
			}
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

		protected virtual void ShutdownGridserverMenu(object sender, System.EventArgs e)
		{
			MainClass.PendingOperations.Enqueue("shutdown_gridserver");
		}

		protected virtual void DisconnectGridServerMenu(object sender, System.EventArgs e)
		{
			MainClass.PendingOperations.Enqueue("disconnect_gridserver");
		}

	}
}

	