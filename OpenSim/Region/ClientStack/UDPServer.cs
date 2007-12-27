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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;

namespace OpenSim.Region.ClientStack
{
    public sealed class PacketPool
    {
        // Set up a thread-safe singleton pattern
        static PacketPool()
        {
        }

        private static readonly PacketPool instance = new PacketPool();

        public static PacketPool Instance
        {
            get { return instance; }
        }

        private Hashtable pool = new Hashtable();

        public Packet GetPacket(PacketType type)
        {
            Packet packet = null;

            lock (pool)
            {
                if (pool[type] == null || ((Stack) pool[type]).Count == 0)
                {
                    // Creating a new packet if we cannot reuse an old package
                    packet = Packet.BuildPacket(type);
                }
                else
                {
                    // Recycle old packages
                    packet = (Packet) ((Stack) pool[type]).Pop();
                }
            }

            return packet;
        }

        public Packet GetPacket(byte[] bytes, ref int packetEnd, byte[] zeroBuffer)
        {
            Packet packet = GetPacket(GetType(bytes, packetEnd, zeroBuffer));

            int i = 0;
            packet.FromBytes(bytes, ref i, ref packetEnd, zeroBuffer);
            return packet;
        }

        public PacketType GetType(byte[] bytes, int packetEnd, byte[] zeroBuffer)
        {
            //Function removed from LibSL revision 1540
            // We're using it..    so Built it into UDP server for now..  
            ushort id;
            PacketFrequency freq;
            int i = 0, end = packetEnd;
            Header header = Header.BuildHeader(bytes, ref i, ref end);
            if (header.Zerocoded)
            {
                end = Helpers.ZeroDecode(bytes, end + 1, zeroBuffer) - 1;
                bytes = zeroBuffer;
            }

            if (bytes[6] == 0xFF)
            {
                if (bytes[7] == 0xFF)
                {
                    id = (ushort) ((bytes[8] << 8) + bytes[9]);
                    freq = PacketFrequency.Low;
                }
                else
                {
                    id = (ushort) bytes[7];
                    freq = PacketFrequency.Medium;
                }
            }
            else
            {
                id = (ushort) bytes[6];
                freq = PacketFrequency.High;
            }
            return Packet.GetType(id, freq);
        }

        public void ReturnPacket(Packet packet)
        {
            lock (pool)
            {
                PacketType type = packet.Type;

                if (pool[type] == null)
                {
                    pool[type] = new Stack();
                }

                ((Stack) pool[type]).Push(packet);
            }
        }
    }

    public class UDPServer : ClientStackNetworkHandler
    {
        protected Dictionary<EndPoint, uint> clientCircuits = new Dictionary<EndPoint, uint>();
        protected Dictionary<uint, EndPoint> clientCircuits_reverse = new Dictionary<uint, EndPoint>();
        public Socket Server;
        protected IPEndPoint ServerIncoming;
        protected byte[] RecvBuffer = new byte[4096];
        protected byte[] ZeroBuffer = new byte[8192];
        protected IPEndPoint ipeSender;
        protected EndPoint epSender;
        protected AsyncCallback ReceivedData;
        protected PacketServer m_packetServer;
        protected ulong m_regionHandle;

        protected uint listenPort;
        protected IScene m_localScene;
        protected AssetCache m_assetCache;
        protected LogBase m_log;
        protected AgentCircuitManager m_authenticateSessionsClass;

        public PacketServer PacketServer
        {
            get { return m_packetServer; }
            set { m_packetServer = value; }
        }

        public IScene LocalScene
        {
            set
            {
                m_localScene = value;
                m_packetServer.LocalScene = m_localScene;
                m_regionHandle = m_localScene.RegionInfo.RegionHandle;
            }
        }

        public ulong RegionHandle
        {
            get { return m_regionHandle; }
        }

        public UDPServer()
        {
        }

        public UDPServer(uint port, AssetCache assetCache, LogBase console, AgentCircuitManager authenticateClass)
        {
            listenPort = port;
            m_assetCache = assetCache;
            m_log = console;
            m_authenticateSessionsClass = authenticateClass;
            CreatePacketServer();
        }

        protected virtual void CreatePacketServer()
        {
            PacketServer packetServer = new PacketServer(this);
        }

        protected virtual void OnReceivedData(IAsyncResult result)
        {
            ipeSender = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 0);
            epSender = (EndPoint) ipeSender;
            Packet packet = null;

            int numBytes;

            try
            {
                numBytes = Server.EndReceiveFrom(result, ref epSender);
            }
            catch (SocketException e)
            {
                // TODO : Actually only handle those states that we have control over, re-throw everything else,
                // TODO: implement cases as we encounter them.
                switch (e.SocketErrorCode)
                {
                    case SocketError.AlreadyInProgress:
                    case SocketError.NetworkReset:
                    case SocketError.ConnectionReset:

                        try
                        {
                            CloseEndPoint(epSender);
                        }
                        catch (Exception a)
                        {
                            MainLog.Instance.Verbose("UDPSERVER", a.ToString());
                        }
                        try
                        {
                            Server.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender,
                                                    ReceivedData, null);

                            // Ter: For some stupid reason ConnectionReset basically kills our async event structure..  
                            // so therefore..  we've got to tell the server to BeginReceiveFrom again.
                            // This will happen over and over until we've gone through all packets 
                            // sent to and from this particular user.
                            // Stupid I know..  
                            // but Flusing the buffer would be even more stupid...  so, we're stuck with this ugly method.
                        }
                        catch (SocketException)
                        {
                        }
                        break;
                    default:
                        try
                        {
                            CloseEndPoint(epSender);
                        }
                        catch (Exception)
                        {
                            //MainLog.Instance.Verbose("UDPSERVER", a.ToString());
                        }
                        try
                        {
                            Server.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender,
                                                    ReceivedData, null);

                            // Ter: For some stupid reason ConnectionReset basically kills our async event structure..  
                            // so therefore..  we've got to tell the server to BeginReceiveFrom again.
                            // This will happen over and over until we've gone through all packets 
                            // sent to and from this particular user.
                            // Stupid I know..  
                            // but Flusing the buffer would be even more stupid...  so, we're stuck with this ugly method.
                        }
                        catch (SocketException)
                        {
                        }

                        // Here's some reference code!   :D  
                        // Shutdown and restart the UDP listener!  hehe
                        // Shiny

                        //Server.Shutdown(SocketShutdown.Both);
                        //CloseEndPoint(epSender);
                        //ServerListener();
                        break;
                }

                return;
            }
            catch (ObjectDisposedException)
            {
                //MainLog.Instance.Debug("UDPSERVER", e.ToString());
                return;
            }

            int packetEnd = numBytes - 1;

            try
            {
                packet = PacketPool.Instance.GetPacket(RecvBuffer, ref packetEnd, ZeroBuffer);
            }
            catch (Exception)
            {
                //MainLog.Instance.Debug("UDPSERVER", e.ToString());
            }

            if (packet != null)
            {
                // do we already have a circuit for this endpoint
                uint circuit;
                if (clientCircuits.TryGetValue(epSender, out circuit))
                {
                    //if so then send packet to the packetserver
                    //MainLog.Instance.Warn("UDPSERVER", "ALREADY HAVE Circuit!");
                    m_packetServer.InPacket(circuit, packet);
                }
                else if (packet.Type == PacketType.UseCircuitCode)
                {
                    // new client
                    MainLog.Instance.Debug("UDPSERVER", "Adding New Client");
                    AddNewClient(packet);
                }
                else
                {
                    // invalid client
                    //CFK: This message seems to have served its usefullness as of 12-15 so I am commenting it out for now
                    //m_log.Warn("client", "Got a packet from an invalid client - " + epSender.ToString());
                }
            }

            Server.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender, ReceivedData, null);
        }

        private void CloseEndPoint(EndPoint sender)
        {
            uint circuit;
            if (clientCircuits.TryGetValue(sender, out circuit))
            {
                m_packetServer.CloseCircuit(circuit);
            }
        }

        protected virtual void AddNewClient(Packet packet)
        {
            UseCircuitCodePacket useCircuit = (UseCircuitCodePacket) packet;
            clientCircuits.Add(epSender, useCircuit.CircuitCode.Code);
            clientCircuits_reverse.Add(useCircuit.CircuitCode.Code, epSender);

            PacketServer.AddNewClient(epSender, useCircuit, m_assetCache, m_authenticateSessionsClass);
        }

        public void ServerListener()
        {
            m_log.Verbose("SERVER", "Opening UDP socket on " + listenPort.ToString());

            ServerIncoming = new IPEndPoint(IPAddress.Parse("0.0.0.0"), (int) listenPort);
            Server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Server.Bind(ServerIncoming);

            m_log.Verbose("SERVER", "UDP socket bound, getting ready to listen");

            ipeSender = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 0);
            epSender = (EndPoint) ipeSender;
            ReceivedData = new AsyncCallback(OnReceivedData);
            Server.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender, ReceivedData, null);

            m_log.Status("SERVER", "Listening...");
        }

        public virtual void RegisterPacketServer(PacketServer server)
        {
            m_packetServer = server;
        }

        public virtual void SendPacketTo(byte[] buffer, int size, SocketFlags flags, uint circuitcode)
            //EndPoint packetSender)
        {
            // find the endpoint for this circuit
            EndPoint sendto = null;
            if (clientCircuits_reverse.TryGetValue(circuitcode, out sendto))
            {
                //we found the endpoint so send the packet to it
                Server.SendTo(buffer, size, flags, sendto);
            }
        }

        public virtual void RemoveClientCircuit(uint circuitcode)
        {
            EndPoint sendto = null;
            if (clientCircuits_reverse.TryGetValue(circuitcode, out sendto))
            {
                clientCircuits.Remove(sendto);


                clientCircuits_reverse.Remove(circuitcode);
            }
        }
    }
}