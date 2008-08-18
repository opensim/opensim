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
using System.Threading;
using Gtk;

namespace OpenGridServices.Manager
{
    class MainClass
    {

        public static bool QuitReq=false;
        public static BlockingQueue<string> PendingOperations = new BlockingQueue<string>();

        private static Thread OperationsRunner;

        private static GridServerConnectionManager gridserverConn;

        private static MainWindow win;

        public static void DoMainLoop()
        {
            while (!QuitReq)
            {
                Application.RunIteration();
            }
        }

        public static void RunOperations()
        {
            string operation;
            string cmd;
            char[] sep = new char[1];
            sep[0]=' ';
            while (!QuitReq)
            {
                operation=PendingOperations.Dequeue();
                Console.WriteLine(operation);
                cmd = operation.Split(sep)[0];
                switch (cmd)
                {
                    case "connect_to_gridserver":
                        win.SetStatus("Connecting to grid server...");
                        if (gridserverConn.Connect(operation.Split(sep)[1], operation.Split(sep)[2], operation.Split(sep)[3]))
                        {
                            win.SetStatus("Connected OK with session ID:" + gridserverConn.SessionID);
                            win.SetGridServerConnected(true);
                            Thread.Sleep(3000);
                            win.SetStatus("Downloading region maps...");
                            gridserverConn.DownloadMap();
                        }
                        else
                        {
                            win.SetStatus("Could not connect");
                        }
                        break;

                    case "restart_gridserver":
                        win.SetStatus("Restarting grid server...");
                        if (gridserverConn.RestartServer())
                        {
                            win.SetStatus("Restarted server OK");
                            Thread.Sleep(3000);
                            win.SetStatus("");
                        }
                        else
                        {
                            win.SetStatus("Error restarting grid server!!!");
                        }
                        break;

                    case "shutdown_gridserver":
                        win.SetStatus("Shutting down grid server...");
                        if (gridserverConn.ShutdownServer())
                        {
                            win.SetStatus("Grid server shutdown");
                            win.SetGridServerConnected(false);
                            Thread.Sleep(3000);
                            win.SetStatus("");
                        }
                        else
                        {
                            win.SetStatus("Could not shutdown grid server!!!");
                        }
                        break;

                    case "disconnect_gridserver":
                        gridserverConn.DisconnectServer();
                        win.SetGridServerConnected(false);
                        break;
                }
            }
        }

        public static void Main (string[] args)
        {
            gridserverConn = new GridServerConnectionManager();
            Application.Init ();
            win = new MainWindow ();
            win.Show ();
            OperationsRunner = new Thread(new ThreadStart(RunOperations));
            OperationsRunner.IsBackground=true;
            OperationsRunner.Start();
            DoMainLoop();
        }
    }
}
