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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace OpenSim.ApplicationPlugins.LoadBalancer
{
    public class AsynchronousClient
    {
        private static ManualResetEvent connectDone = new ManualResetEvent(false);
        private static ManualResetEvent sendDone = new ManualResetEvent(false);

        public static Socket StartClient(string hostname, int port)
        {
            try
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(hostname);
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), client);
                connectDone.WaitOne();
                /*
                  Send(client,"This is a test<EOF>");
                  sendDone.WaitOne();
                  Receive(client);
                  receiveDone.WaitOne();
                  client.Shutdown(SocketShutdown.Both);
                  client.Close();
                */
                return client;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                throw new Exception("socket error !!");
            }
        }

        private static void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket) ar.AsyncState;
                client.EndConnect(ar);
                Console.WriteLine("Socket connected to {0}", client.RemoteEndPoint.ToString());
                connectDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public static void Send(Socket client, byte[] byteData)
        {
            client.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), client);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket) ar.AsyncState;
                int bytesSent = client.EndSend(ar);
                if (bytesSent > 0)
                {
                    //Console.WriteLine("Sent {0} bytes to server.", bytesSent);
                }
                sendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }

    public class InternalPacketHeader
    {
        public Guid agent_id;
        private byte[] buffer = new byte[32];
        public int numbytes;
        public int region_port;
        public int throttlePacketType;
        public int type;

        public void FromBytes(byte[] bytes)
        {
            MemoryStream memstr = new MemoryStream(bytes);
            memstr.Seek(0, SeekOrigin.Begin);
            BinaryReader binread = new BinaryReader(memstr);

            type = binread.ReadInt32();
            throttlePacketType = binread.ReadInt32();
            numbytes = binread.ReadInt32();
            agent_id = new Guid(binread.ReadBytes(16));
            region_port = binread.ReadInt32();

            binread.Close();
        }

        public byte[] ToBytes()
        {
            int i = 0;
            buffer[i++] = (byte) (type % 256);
            buffer[i++] = (byte) ((type >> 8) % 256);
            buffer[i++] = (byte) ((type >> 16) % 256);
            buffer[i++] = (byte) ((type >> 24) % 256);

            buffer[i++] = (byte) (throttlePacketType % 256);
            buffer[i++] = (byte) ((throttlePacketType >> 8) % 256);
            buffer[i++] = (byte) ((throttlePacketType >> 16) % 256);
            buffer[i++] = (byte) ((throttlePacketType >> 24) % 256);

            buffer[i++] = (byte) (numbytes % 256);
            buffer[i++] = (byte) ((numbytes >> 8) % 256);
            buffer[i++] = (byte) ((numbytes >> 16) % 256);
            buffer[i++] = (byte) ((numbytes >> 24) % 256);

            // no endian care
            Buffer.BlockCopy(agent_id.ToByteArray(), 0, buffer, i, 16);
            i += 16;

            buffer[i++] = (byte) (region_port % 256);
            buffer[i++] = (byte) ((region_port >> 8) % 256);
            buffer[i++] = (byte) ((region_port >> 16) % 256);
            buffer[i++] = (byte) ((region_port >> 24) % 256);

            return buffer;
        }
    }

    public class TcpClient
    {
        public static int internalPacketHeaderSize = 4 * 4 + 16 * 1;
        private Socket mConnection;

        private string mHostname;
        private int mPort;

        public TcpClient(string hostname, int port)
        {
            mHostname = hostname;
            mPort = port;
            mConnection = null;
        }

        public void connect()
        {
            mConnection = AsynchronousClient.StartClient(mHostname, mPort);
        }

/*
        public void receive()
        {
            if (mConnection == null)
            {
                throw new Exception("client not initialized");
            }
            try
            {
                AsynchronousClient.Receive(this.mConnection);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                mConnection = null;
            }
        }
*/

        public void send(InternalPacketHeader header, byte[] packet)
        {
            lock (this)
            {
                if (mConnection == null)
                {
//                    throw new Exception("client not initialized");
                    connect();
                }

                AsynchronousClient.Send(mConnection, header.ToBytes());

/*
for (int i = 0; i < 10; i++)
{
    Console.Write(packet[i] + " ");
}
Console.WriteLine("");
*/
                AsynchronousClient.Send(mConnection, packet);
            }
        }
    }
}