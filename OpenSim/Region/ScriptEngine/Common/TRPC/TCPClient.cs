using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace OpenSim.Region.ScriptEngine.Common.TRPC
{
    public class TCPClient : TCPCommon.ClientInterface
    {

        public TCPClient()
        {
        }
        private readonly Dictionary<int, TCPSocket> Clients = new Dictionary<int, TCPSocket>();
        private int ClientCount = 0;


        public event TCPCommon.ClientConnectedDelegate ClientConnected;
        public event TCPCommon.DataReceivedDelegate DataReceived;
        public event TCPCommon.DataSentDelegate DataSent;
        public event TCPCommon.CloseDelegate Close;
        public event TCPCommon.ConnectErrorDelegate ConnectError;


        /// <summary>
        /// Creates client connection
        /// </summary>
        public void Connect(string RemoteHost, int RemotePort)
        {
            Socket newsock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ipe = new IPEndPoint(IPAddress.Parse(RemoteHost), RemotePort);
            //newsock.BeginConnect(ipe, new AsyncCallback(asyncConnected), newsock);
            newsock.Connect(ipe);
        }
        public int ConnectAndReturnID(string RemoteHost, int RemotePort)
        {
            Socket newsock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ipe = new IPEndPoint(IPAddress.Parse(RemoteHost), RemotePort);
            //newsock.BeginConnect(ipe, new AsyncCallback(asyncConnected), newsock);
            newsock.Connect(ipe);
            return ProcessConnection(newsock);
        }


        public void Disconnect(int ID)
        {
            Clients[ID].Disconnect();
        }

        void asyncConnected(IAsyncResult iar)
        {
            Socket client = (Socket)iar.AsyncState;
            client.EndConnect(iar);
            ProcessConnection(client);
        }

        private int ProcessConnection(Socket client)
        {
            try
            {
                


                int id = ClientCount++;
                TCPSocket S = new TCPSocket(id, client);

                // Add to dictionary
                Clients.Add(id, S);

                // Add event handlers
                S.Close += new TCPSocket.CloseDelegate(S_Close);
                S.DataReceived += new TCPSocket.DataReceivedDelegate(S_DataReceived);
                S.DataSent += new TCPSocket.DataSentDelegate(S_DataSent);

                // Start it
                S.Start();

                Debug.WriteLine("Connection established: " + client.RemoteEndPoint.ToString());

                // Fire Connected-event
                if (ClientConnected != null)
                    ClientConnected(id, client.RemoteEndPoint);

                return id;
            }
            catch (SocketException sex)
            {
                if (ConnectError != null)
                    ConnectError(sex.Message);
            }
            return -1;
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