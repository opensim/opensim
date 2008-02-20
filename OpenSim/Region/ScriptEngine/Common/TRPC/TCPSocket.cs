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
using System.Net.Sockets;

namespace OpenSim.Region.ScriptEngine.Common.TRPC
{
    public class TCPSocket
    {

        public readonly Socket Client;
        public readonly int ID;

        public delegate void DataReceivedDelegate(int ID, byte[] data, int offset, int length);
        public delegate void DataSentDelegate(int ID, int length);
        public delegate void CloseDelegate(int ID);
        public event DataReceivedDelegate DataReceived;
        public event DataSentDelegate DataSent;
        public event CloseDelegate Close;

        private byte[] RecvQueue = new byte[4096];
        private int RecvQueueSize = 4096;

        public TCPSocket(int id, Socket client)
        {
            ID = id;
            Client = client;
        }
        public void Start()
        {
            // Start listening
            BeginReceive();
        }

        private void BeginReceive()
        {
            Client.BeginReceive(RecvQueue, 0, RecvQueueSize, SocketFlags.None, new AsyncCallback(asyncDataReceived), Client);
        }

        /// <summary>
        /// Callback for successful receive (or connection close)
        /// </summary>
        /// <param name="ar"></param>
        private void asyncDataReceived(IAsyncResult ar)
        {
            Socket client = (Socket)ar.AsyncState;
            int recv = client.EndReceive(ar);

            // Is connection closed?
            if (recv == 0)
            {
                client.Close();
                Close(ID);
                return;
            }

            // Call receive event
            DataReceived(ID, RecvQueue, 0, recv);

            // Start new receive
            BeginReceive();

        }


        public void Send(int clientID, byte[] data, int offset, int len)
        {
            Client.BeginSend(data, offset, len, SocketFlags.None, new AsyncCallback(asyncDataSent), Client);
        }

        /// <summary>
        /// Callback for successful send
        /// </summary>
        /// <param name="ar"></param>
        void asyncDataSent(IAsyncResult ar)
        {
            Socket client = (Socket)ar.AsyncState;
            int sent = client.EndSend(ar);
            DataSent(ID, sent);
        }

        public void Disconnect()
        {
            Client.Close();
            Close(ID);
        }
    }
}