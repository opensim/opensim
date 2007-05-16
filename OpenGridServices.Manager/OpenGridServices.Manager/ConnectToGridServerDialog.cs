using Gtk;
using System;
	
	public partial class ConnectToGridServerDialog : Gtk.Dialog
	{
		
		public ConnectToGridServerDialog()
		{
			this.Build();
		}

		protected virtual void ConnectBtn(object sender, System.EventArgs e)
		{
			this.Hide();
		}

		protected virtual void CancelBtn(object sender, System.EventArgs e)
		{
			this.Hide();
		}

	}

