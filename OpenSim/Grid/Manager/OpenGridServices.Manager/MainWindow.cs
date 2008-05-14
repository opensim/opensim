/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using Gtk;

namespace OpenGridServices.Manager
{
    public partial class MainWindow: Gtk.Window
    {
        public MainWindow() : base (Gtk.WindowType.Toplevel)
        {
            Build();
        }

        public void SetStatus(string statustext)
        {
            this.statusbar1.Pop(0);
            this.statusbar1.Push(0, statustext);
        }

        public void DrawGrid(RegionBlock[][] regions)
        {
            for (int x=0; x<=regions.GetUpperBound(0); x++)
            {
                for (int y=0; y<=regions.GetUpperBound(1); y++)
                {
                    Gdk.Image themap = new Gdk.Image(Gdk.ImageType.Fastest,Gdk.Visual.System,256,256);
                    this.drawingarea1.GdkWindow.DrawImage(new Gdk.GC(this.drawingarea1.GdkWindow),themap,0,0,x*256,y*256,256,256);
                }
            }
        }

        public void SetGridServerConnected(bool connected)
        {
            if (connected)
            {
                this.ConnectToGridserver.Visible=false;
                this.DisconnectFromGridServer.Visible=true;
            }
            else
            {
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
