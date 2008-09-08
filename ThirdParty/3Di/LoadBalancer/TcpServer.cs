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
    public class StateObject
    {
        public const int BufferSize = 2048;
        public byte[] buffer = new byte[BufferSize];
        public InternalPacketHeader header = null;
        public MemoryStream ms_ptr = new MemoryStream();
        public Socket workSocket = null;
    }

    public class AsynchronousSocketListener
    {
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        public static string data = null;

        #region KIRYU

        #region Delegates

        public delegate void PacketRecieveHandler(InternalPacketHeader header, byte[] buff);

        #endregion

        public static PacketRecieveHandler PacketHandler = null;

        #endregion

        public AsynchronousSocketListener()
        {
        }

        public static void StartListening(int port)
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

            Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);
                while (true)
                {
                    allDone.Reset();
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                    allDone.WaitOne();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            /*
              Console.WriteLine("\nPress ENTER to continue...");
              Console.Read();
            */
        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            allDone.Set();
            Socket listener = (Socket) ar.AsyncState;
            Socket handler = listener.EndAccept(ar);
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
        }

        public static void ReadCallback(IAsyncResult ar)
        {
            StateObject state = (StateObject) ar.AsyncState;
            Socket handler = state.workSocket;

            try
            {
                int bytesRead = handler.EndReceive(ar);

                //MainLog.Instance.Verbose("TCPSERVER", "Received packet [{0}]", bytesRead);

                if (bytesRead > 0)
                {
                    state.ms_ptr.Write(state.buffer, 0, bytesRead);
                }
                else
                {
                    //MainLog.Instance.Verbose("TCPSERVER", "Connection terminated");
                    return;
                }

                long rest_size = state.ms_ptr.Length;
                long current_pos = 0;
                while (rest_size > TcpClient.internalPacketHeaderSize)
                {
                    if ((state.header == null) && (rest_size >= TcpClient.internalPacketHeaderSize))
                    {
                        //MainLog.Instance.Verbose("TCPSERVER", "Processing header");

                        // reading header
                        state.header = new InternalPacketHeader();

                        byte[] headerbytes = new byte[TcpClient.internalPacketHeaderSize];
                        state.ms_ptr.Position = current_pos;
                        state.ms_ptr.Read(headerbytes, 0, TcpClient.internalPacketHeaderSize);
                        state.ms_ptr.Seek(0, SeekOrigin.End);
                        state.header.FromBytes(headerbytes);
                    }

                    if ((state.header != null) && (rest_size >= state.header.numbytes + TcpClient.internalPacketHeaderSize))
                    {
                        //MainLog.Instance.Verbose("TCPSERVER", "Processing body");

                        // reading body
                        byte[] packet = new byte[state.header.numbytes];
                        state.ms_ptr.Position = current_pos + TcpClient.internalPacketHeaderSize;
                        state.ms_ptr.Read(packet, 0, state.header.numbytes);

/*
                        for (int i=0; i<state.header.numbytes; i++)
                        {
                            System.Console.Write(packet[i] + " ");
                        }
                        System.Console.WriteLine();
*/

                        state.ms_ptr.Seek(0, SeekOrigin.End);
                        // call loadbarancer function
                        if (PacketHandler == null)
                        {
                            //MainLog.Instance.Verbose("TCPSERVER", "PacketHandler not found");
                        }
                        else
                        {
                            //MainLog.Instance.Verbose("TCPSERVER", "calling PacketHandler");
                            PacketHandler(state.header, packet);
                        }

                        int read_size = state.header.numbytes + TcpClient.internalPacketHeaderSize;
                        state.header = null;

                        rest_size -= read_size;
                        current_pos += read_size;

                        if (rest_size < TcpClient.internalPacketHeaderSize)
                        {
                            byte[] rest_bytes = new byte[rest_size];
                            state.ms_ptr.Position = read_size;
                            state.ms_ptr.Read(rest_bytes, 0, (int) rest_size);
                            state.ms_ptr.Close();
                            state.ms_ptr = new MemoryStream();
                            state.ms_ptr.Write(rest_bytes, 0, (int) rest_size);
                            break;
                        }
                    }
                } // while (true)
            }
            catch (Exception)
            {
                //MainLog.Instance.Verbose("TCPSERVER", e.ToString());
                //MainLog.Instance.Verbose("TCPSERVER", e.StackTrace);
            }

            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
        }
    }

    public class TcpServer
    {
        private int mPort = 11000;

        public TcpServer()
        {
        }

        public TcpServer(int port)
        {
            mPort = port;
        }

        public void start()
        {
            AsynchronousSocketListener.StartListening(mPort);
        }
    }
}