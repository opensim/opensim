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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using log4net;

namespace OpenSim.GridLaunch.GUI.Network
{
    public class TCPD : IGUI, IDisposable
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private List<Client> Clients = new List<Client>();

        private readonly int defaultPort = 7998;
        private TcpListener tcpListener;
        private Thread listenThread;
        private Thread clientThread;


        private List<string> Apps = new List<string>();
        internal string currentApp = "";
        private bool quitTyped = false;

        public TCPD()
        {
            Program.AppCreated += Program_AppCreated;
            Program.AppRemoved += Program_AppRemoved;
            Program.AppConsoleOutput += Program_AppConsoleOutput;
            Program.Command.CommandLine += Command_CommandLine;

        }

        ~TCPD()
        {
            Dispose();
        }
        private bool isDisposed = false;
        ///<summary>
        ///Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        ///</summary>
        ///<filterpriority>2</filterpriority>
        public void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;
            tcpd_Stop();
        }

        public void StartGUI()
        {
            // We are starting
            tcpd_Start();
        }

        public void StopGUI()
        {
            // We are stopping
            tcpd_Stop();
        }


        #region GridLaunch Events
        private void Command_CommandLine(string application, string command, string arguments)
        {
            // If command is a number then someone might be trying to change console: /1, /2, etc.
            int currentAppNum = 0;
            if (int.TryParse(command, out currentAppNum))
                if (currentAppNum <= Apps.Count)
                {
                    currentApp = Apps[currentAppNum - 1];
                    TCPWriteToAll("Changed console to app: " + currentApp + Environment.NewLine);
                }
                else
                    TCPWriteToAll("Unable to change to app number: " + currentAppNum + Environment.NewLine);

            // Has user typed quit?
            if (command.ToLower() == "quit")
                quitTyped = true;

            // Has user typed /list?
            if (command.ToLower() == "list")
            {
                TCPWriteToAll("/0    Log console");
                for (int i = 1; i <= Apps.Count; i++)
                {
                    TCPWriteToAll(string.Format("/{0}    {1}", i, Apps[i - 1]));
                }
            }

        }

        void Program_AppCreated(string App)
        {
            TCPWriteToAll("Started: " + App);
            if (!Apps.Contains(App))
                Apps.Add(App);
        }

        void Program_AppRemoved(string App)
        {
            TCPWriteToAll("Stopped: " + App);
            if (Apps.Contains(App))
                Apps.Remove(App);
        }
        
        private void Program_AppConsoleOutput(string App, string Text)
        {
            TCPWriteToAll(App, Text);
        }

        #endregion

        private void tcpd_Start()
        {
            listenThread = new Thread(new ThreadStart(ListenForClients));
            listenThread.Name = "TCPDThread";
            listenThread.IsBackground = true;
            listenThread.Start();

            while (!quitTyped)
            {
                Thread.Sleep(500);
            }

            //clientThread = new Thread(new ThreadStart(ProcessClients));
            //clientThread.Name = "TCPClientThread";
            //clientThread.IsBackground = true;
            ////clientThread.Start();

        }
        private void tcpd_Stop()
        {
            StopThread(listenThread);
            StopThread(clientThread);
        }
        private void ListenForClients()
        {
            int Port = 0;
            int.TryParse(Program.Settings["TCPPort"], out Port);
            if (Port < 1)
                Port = defaultPort;

            m_log.Info("Starting TCP Server on port " + Port);
            this.tcpListener = new TcpListener(IPAddress.Any, Port);

            this.tcpListener.Start();

            while (true)
            {
                // Blocks until a client has connected to the server
                TcpClient tcpClient = this.tcpListener.AcceptTcpClient();
                Client client = new Client(this, tcpClient);

                lock (Clients)
                {
                    Clients.Add(client);
                }
                System.Threading.Thread.Sleep(500);
            }
        }

        private static void StopThread(Thread t)
        {
            if (t != null)
            {
                m_log.Debug("Stopping thread " + t.Name);
                try
                {
                    if (t.IsAlive)
                        t.Abort();
                    t.Join(2000);
                    t = null;
                }
                catch (Exception ex)
                {
                    m_log.Error("Exception stopping thread: " + ex.ToString());
                }
            }
        }

        private void TCPWriteToAll(string app, string text)
        {
            TCPWriteToAll(text);
        }
        private void TCPWriteToAll(string text)
        {
            foreach (Client c in new ArrayList(Clients))
            {
                try
                {
                    c.Write(text);
                } catch (Exception ex)
                {
                    m_log.Error("Exception writing to TCP: " + ex.ToString());
                }
            }
        }

    }
}
