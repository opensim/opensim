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