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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using libsecondlife.Packets;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.ClientStack.LindenUDP;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public class LLUDPServer : LLClientStackNetworkHandler, IClientNetworkServer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Dictionary<EndPoint, uint> clientCircuits = new Dictionary<EndPoint, uint>();

        //public Dictionary<uint, EndPoint> clientCircuits_reverse = new Dictionary<uint, EndPoint>();
        public Hashtable clientCircuits_reverse = Hashtable.Synchronized(new Hashtable());

        protected Dictionary<uint, EndPoint> proxyCircuits = new Dictionary<uint, EndPoint>();
        private Socket m_socket;
        protected IPEndPoint ServerIncoming;
        protected byte[] RecvBuffer = new byte[4096];
        protected byte[] ZeroBuffer = new byte[8192];
        protected IPEndPoint ipeSender;
        protected EndPoint epSender;
        protected EndPoint epProxy;
        protected int proxyPortOffset;
        protected AsyncCallback ReceivedData;
        protected LLPacketServer m_packetServer;
        protected Location m_location;

        protected uint listenPort;
        protected bool Allow_Alternate_Port;
        protected IPAddress listenIP = IPAddress.Parse("0.0.0.0");
        protected IScene m_localScene;
        protected AssetCache m_assetCache;
        protected AgentCircuitManager m_authenticateSessionsClass;

        public LLPacketServer PacketServer
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
                m_location = new Location(m_localScene.RegionInfo.RegionHandle);
            }
        }

        public ulong RegionHandle
        {
            get { return m_location.RegionHandle; }
        }

        Socket IClientNetworkServer.Server
        {
            get { return m_socket; }
        }

        public bool HandlesRegion(Location x)
        {
            return x == m_location;
        }

        public void AddScene(Scene x)
        {
            LocalScene = x;
        }

        public void Start()
        {
            ServerListener();
        }

        public void Stop()
        {
            m_socket.Close();
        }

        public LLUDPServer()
        {
        }

        public LLUDPServer(IPAddress _listenIP, ref uint port, int proxyPortOffset, bool allow_alternate_port, AssetCache assetCache, AgentCircuitManager authenticateClass)
        {
            Initialise(_listenIP, ref port, proxyPortOffset, allow_alternate_port, assetCache, authenticateClass);
        }

        public void Initialise(IPAddress _listenIP, ref uint port, int proxyPortOffset, bool allow_alternate_port, AssetCache assetCache, AgentCircuitManager authenticateClass)
        {
            this.proxyPortOffset = proxyPortOffset;
            listenPort = (uint) (port + proxyPortOffset);
            listenIP = _listenIP;
            Allow_Alternate_Port = allow_alternate_port;
            m_assetCache = assetCache;
            m_authenticateSessionsClass = authenticateClass;
            CreatePacketServer();

            // Return new port
            // This because in Grid mode it is not really important what port the region listens to as long as it is correctly registered.
            // So the option allow_alternate_ports="true" was added to default.xml
            port = (uint)(listenPort - proxyPortOffset);
        }

        protected virtual void CreatePacketServer()
        {
            new LLPacketServer(this);
        }

        protected virtual void OnReceivedData(IAsyncResult result)
        {
            ipeSender = new IPEndPoint(listenIP, 0);
            epSender = (EndPoint) ipeSender;
            Packet packet = null;

            int numBytes = 1;

            try
            {
                numBytes = m_socket.EndReceiveFrom(result, ref epSender);
            }
            catch (SocketException e)
            {
                // TODO : Actually only handle those states that we have control over, re-throw everything else,
                // TODO: implement cases as we encounter them.
                //m_log.Error("[UDPSERVER]: Connection Error! - " + e.ToString());
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
                            m_log.Info("[UDPSERVER]: " + a.ToString());
                        }
                        try
                        {
                            m_socket.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender,
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
                            //m_log.Info("[UDPSERVER]" + a.ToString());
                        }
                        try
                        {
                            m_socket.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender,
                                                    ReceivedData, null);

                            // Ter: For some stupid reason ConnectionReset basically kills our async event structure..
                            // so therefore..  we've got to tell the server to BeginReceiveFrom again.
                            // This will happen over and over until we've gone through all packets
                            // sent to and from this particular user.
                            // Stupid I know..
                            // but Flusing the buffer would be even more stupid...  so, we're stuck with this ugly method.
                        }
                        catch (SocketException e2)
                        {
                            m_log.Error("[UDPSERVER]: " + e2.ToString());
                        }

                        // Here's some reference code!   :D
                        // Shutdown and restart the UDP listener!  hehe
                        // Shiny

                        //Server.Shutdown(SocketShutdown.Both);
                        //CloseEndPoint(epSender);
                        //ServerListener();
                        break;
                }

                //return;
            }
            catch (ObjectDisposedException e)
            {
                m_log.Debug("[UDPSERVER]: " + e.ToString());
                try
                {
                    m_socket.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender,
                                            ReceivedData, null);

                    // Ter: For some stupid reason ConnectionReset basically kills our async event structure..
                    // so therefore..  we've got to tell the server to BeginReceiveFrom again.
                    // This will happen over and over until we've gone through all packets
                    // sent to and from this particular user.
                    // Stupid I know..
                    // but Flusing the buffer would be even more stupid...  so, we're stuck with this ugly method.
                }

                catch (SocketException e2)
                {
                    m_log.Error("[UDPSERVER]: " + e2.ToString());
                }
                catch (ObjectDisposedException)
                {
                }
                //return;
            }

            //System.Console.WriteLine("UDPServer : recieved message from {0}", epSender.ToString());
            epProxy = epSender;
            if (proxyPortOffset != 0)
            {
                epSender = PacketPool.DecodeProxyMessage(RecvBuffer, ref numBytes);
            }

            int packetEnd = numBytes - 1;

            try
            {
                packet = PacketPool.Instance.GetPacket(RecvBuffer, ref packetEnd, ZeroBuffer);
            }
            catch (Exception e)
            {
                m_log.Debug("[UDPSERVER]: " + e.ToString());
            }

            try
            {
                m_socket.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender, ReceivedData, null);
            }
            catch (SocketException)
            {
                try
                {
                    CloseEndPoint(epSender);
                }
                catch (Exception a)
                {
                    m_log.Info("[UDPSERVER]: " + a.ToString());
                }
                try
                {
                    m_socket.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender,
                                            ReceivedData, null);

                    // Ter: For some stupid reason ConnectionReset basically kills our async event structure..
                    // so therefore..  we've got to tell the server to BeginReceiveFrom again.
                    // This will happen over and over until we've gone through all packets
                    // sent to and from this particular user.
                    // Stupid I know..
                    // but Flusing the buffer would be even more stupid...  so, we're stuck with this ugly method.
                }
                catch (SocketException e5)
                {
                    m_log.Error("[UDPSERVER]: " + e5.ToString());
                }
            }
            catch (ObjectDisposedException)
            {
            }

            if (packet != null)
            {
                try
                {
                    // do we already have a circuit for this endpoint
                    uint circuit;

                    bool ret = false;
                    lock (clientCircuits)
                    {
                        ret = clientCircuits.TryGetValue(epSender, out circuit);
                    }
                    if (ret)
                    {
                        //if so then send packet to the packetserver
                        //m_log.Warn("[UDPSERVER]: ALREADY HAVE Circuit!");
                        m_packetServer.InPacket(circuit, packet);
                    }
                    else if (packet.Type == PacketType.UseCircuitCode)
                    {
                        // new client
                        m_log.Debug("[UDPSERVER]: Adding New Client");
                        AddNewClient(packet);

                        UseCircuitCodePacket p = (UseCircuitCodePacket)packet;

                        // Ack the first UseCircuitCode packet
                        PacketAckPacket ack_it = (PacketAckPacket)PacketPool.Instance.GetPacket(PacketType.PacketAck);
                        // TODO: don't create new blocks if recycling an old packet
                        ack_it.Packets = new PacketAckPacket.PacketsBlock[1];
                        ack_it.Packets[0] = new PacketAckPacket.PacketsBlock();
                        ack_it.Packets[0].ID = packet.Header.Sequence;
                        ack_it.Header.Reliable = false;
                        SendPacketTo(ack_it.ToBytes(),ack_it.ToBytes().Length,SocketFlags.None,p.CircuitCode.Code);
                    }
                }
                catch (Exception)
                {
                    m_log.Error("[UDPSERVER]: Exception in processing packet.");
                    m_log.Debug("[UDPSERVER]: Adding New Client");
                    try
                    {
                        AddNewClient(packet);
                    }
                    catch (Exception e3)
                    {
                        m_log.Error("[UDPSERVER]: Adding New Client threw exception " + e3.ToString());
                        m_socket.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender,
                                                ReceivedData, null);
                    }
                }
            }

        }

        private void CloseEndPoint(EndPoint sender)
        {
            uint circuit;
            lock (clientCircuits)
            {
                if (clientCircuits.TryGetValue(sender, out circuit))
                {
                    m_packetServer.CloseCircuit(circuit);
                }
            }
        }

        protected virtual void AddNewClient(Packet packet)
        {
            //Slave regions don't accept new clients
            if (m_localScene.Region_Status != RegionStatus.SlaveScene)
            {
                if (!(packet is UseCircuitCodePacket))
                    return;
                
                UseCircuitCodePacket useCircuit = (UseCircuitCodePacket) packet;
                lock (clientCircuits)
                {
                    if (!clientCircuits.ContainsKey(epSender))
                        clientCircuits.Add(epSender, useCircuit.CircuitCode.Code);
                    else
                        m_log.Error("[UDPSERVER]: clientCircuits already contans entry for user " + useCircuit.CircuitCode.Code.ToString() + ". NOT adding.");
                }

                // This doesn't need locking as it's synchronized data
                if (!clientCircuits_reverse.ContainsKey(useCircuit.CircuitCode.Code))
                    clientCircuits_reverse.Add(useCircuit.CircuitCode.Code, epSender);
                else
                    m_log.Error("[UDPSERVER]: clientCurcuits_reverse already contains entry for user " + useCircuit.CircuitCode.Code.ToString() + ". NOT adding.");


                lock (proxyCircuits)
                {
                    if (!proxyCircuits.ContainsKey(useCircuit.CircuitCode.Code))
                        proxyCircuits.Add(useCircuit.CircuitCode.Code, epProxy);
                    else
                        m_log.Error("[UDPSERVER]: proxyCircuits already contains entry for user " + useCircuit.CircuitCode.Code.ToString() + ". NOT adding.");
                }

                PacketServer.AddNewClient(epSender, useCircuit, m_assetCache, m_authenticateSessionsClass, epProxy);
            }
            PacketPool.Instance.ReturnPacket(packet);
        }

        public void ServerListener()
        {
            uint newPort = listenPort;
            m_log.Info("[SERVER]: Opening UDP socket on " + listenIP.ToString() + " " + newPort + ".");

            ServerIncoming = new IPEndPoint(listenIP, (int)newPort);
            m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            m_socket.Bind(ServerIncoming);
            // Add flags to the UDP socket to prevent "Socket forcibly closed by host"
            // uint IOC_IN = 0x80000000;
            // uint IOC_VENDOR = 0x18000000;
            // uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            // TODO: this apparently works in .NET but not in Mono, need to sort out the right flags here.
            // m_socket.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);

            listenPort = newPort;

            m_log.Info("[SERVER]: UDP socket bound, getting ready to listen");

            ipeSender = new IPEndPoint(listenIP, 0);
            epSender = (EndPoint)ipeSender;
            ReceivedData = new AsyncCallback(OnReceivedData);
            m_socket.BeginReceiveFrom(RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref epSender, ReceivedData, null);

            m_log.Info("[SERVER]: Listening on port " + newPort);
        }

        public virtual void RegisterPacketServer(LLPacketServer server)
        {
            m_packetServer = server;
        }

        public virtual void SendPacketTo(byte[] buffer, int size, SocketFlags flags, uint circuitcode)
            //EndPoint packetSender)
        {
            // find the endpoint for this circuit
            EndPoint sendto = null;
            try {
                sendto = (EndPoint)clientCircuits_reverse[circuitcode];
            } catch {
                // Exceptions here mean there is no circuit
                m_log.Warn("Circuit not found, not sending packet");
                return;
            }

            if (sendto != null)
            {
                //we found the endpoint so send the packet to it
                if (proxyPortOffset != 0)
                {
                    //MainLog.Instance.Verbose("UDPSERVER", "SendPacketTo proxy " + proxyCircuits[circuitcode].ToString() + ": client " + sendto.ToString());
                    PacketPool.EncodeProxyMessage(buffer, ref size, sendto);
                    m_socket.SendTo(buffer, size, flags, proxyCircuits[circuitcode]);
                }
                else
                {
                    //MainLog.Instance.Verbose("UDPSERVER", "SendPacketTo : client " + sendto.ToString());
                    m_socket.SendTo(buffer, size, flags, sendto);
                }
            }
        }

        public virtual void RemoveClientCircuit(uint circuitcode)
        {
            EndPoint sendto = null;
            if (clientCircuits_reverse.Contains(circuitcode)) 
            {
                sendto = (EndPoint)clientCircuits_reverse[circuitcode];

                clientCircuits_reverse.Remove(circuitcode);
                
                lock (clientCircuits) 
                {
                    if (sendto != null)
                    {
                        clientCircuits.Remove(sendto);
                    }
                    else
                    {
                        m_log.DebugFormat(
                            "[UDPSERVER]: endpoint for circuit code {0} in RemoveClientCircuit() was unexpectedly null!", circuitcode);
                    }
                }
                lock (proxyCircuits) 
                {
                    proxyCircuits.Remove(circuitcode);
                }
            }
        }

        public void RestoreClient(AgentCircuitData circuit, EndPoint userEP, EndPoint proxyEP)
        {
            //MainLog.Instance.Verbose("UDPSERVER", "RestoreClient");

            UseCircuitCodePacket useCircuit = new UseCircuitCodePacket();
            useCircuit.CircuitCode.Code = circuit.circuitcode;
            useCircuit.CircuitCode.ID = circuit.AgentID;
            useCircuit.CircuitCode.SessionID = circuit.SessionID;

            lock (clientCircuits)
            {
                if (!clientCircuits.ContainsKey(userEP))
                    clientCircuits.Add(userEP, useCircuit.CircuitCode.Code);
                else
                    m_log.Error("[UDPSERVER]: clientCircuits already contans entry for user " + useCircuit.CircuitCode.Code.ToString() + ". NOT adding.");
            }

            // This data structure is synchronized, so we don't need the lock
            if (!clientCircuits_reverse.ContainsKey(useCircuit.CircuitCode.Code))
                clientCircuits_reverse.Add(useCircuit.CircuitCode.Code, userEP);
            else
                m_log.Error("[UDPSERVER]: clientCurcuits_reverse already contains entry for user " + useCircuit.CircuitCode.Code.ToString() + ". NOT adding.");

            lock (proxyCircuits)
            {
                if (!proxyCircuits.ContainsKey(useCircuit.CircuitCode.Code))
                {
                    proxyCircuits.Add(useCircuit.CircuitCode.Code, proxyEP);
                }
                else
                {
                    // re-set proxy endpoint
                    proxyCircuits.Remove(useCircuit.CircuitCode.Code);
                    proxyCircuits.Add(useCircuit.CircuitCode.Code, proxyEP);
                }
            }

            PacketServer.AddNewClient(userEP, useCircuit, m_assetCache, m_authenticateSessionsClass, proxyEP);
        }
    }
}
