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
* 
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using TCPCommon=OpenSim.Region.ScriptEngine.Common.TRPC.TCPCommon;

namespace OpenSim.Region.ScriptEngine.Common.TRPC
{
    public class TCPServer: TCPCommon.ServerInterface 
    {
        public readonly int LocalPort;
        public TCPServer(int localPort)
        {
            LocalPort = localPort;
        }

        private Socket server;

        /// <summary>
        /// Starts listening for new connections
        /// </summary>
        public void StartListen()
        {
            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ipe = new IPEndPoint(IPAddress.Any, LocalPort);
            server.Bind(ipe);
            server.Listen(10);
            server.BeginAccept(new AsyncCallback(AsyncAcceptConnections), server);
        }
        /// <summary>
        /// Stops listening for new connections
        /// </summary>
        public void StopListen()
        {
            server.Close();
            server = null;
        }

        private readonly Dictionary<int, TCPSocket> Clients = new Dictionary<int, TCPSocket>();
        private int ClientCount = 0;


        public event TCPCommon.ClientConnectedDelegate ClientConnected;
        public event TCPCommon.DataReceivedDelegate DataReceived;
        public event TCPCommon.DataSentDelegate DataSent;
        public event TCPCommon.CloseDelegate Close;

        /// <summary>
        /// Async callback for new connections
        /// </summary>
        /// <param name="ar"></param>
        private void AsyncAcceptConnections(IAsyncResult ar)
        {
            int id = ClientCount++;
            Socket oldserver = (Socket)ar.AsyncState;
            Socket client = oldserver.EndAccept(ar);
            TCPSocket S = new TCPSocket(id, client);

            // Add to dictionary
            Clients.Add(id, S);

            // Add event handlers
            S.Close += new TCPSocket.CloseDelegate(S_Close);
            S.DataReceived += new TCPSocket.DataReceivedDelegate(S_DataReceived);
            S.DataSent += new TCPSocket.DataSentDelegate(S_DataSent);

            // Start it
            S.Start();

            Debug.WriteLine("Connection received: " + client.RemoteEndPoint.ToString());

            // Fire Connected-event
            if (ClientConnected != null)
                ClientConnected(id, client.RemoteEndPoint);

        }

        void S_DataSent(int ID, int length)
        {
            if (DataSent != null)
                DataSent(ID, length);
        }

        void S_DataReceived(int ID, byte[] data, int offset, int length)
        {
            if (DataReceived != null)
                DataReceived(ID, data, offset, length);
        }

        void S_Close(int ID)
        {
            if (Close != null)
                Close(ID);
            Clients.Remove(ID);
        }

        public void Send(int clientID, byte[] data, int offset, int len)
        {
            Clients[clientID].Send(clientID, data, offset, len);
        }



    }
}