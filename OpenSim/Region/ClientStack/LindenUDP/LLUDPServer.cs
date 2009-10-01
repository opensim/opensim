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
using log4net;
using Nini.Config;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    /// <summary>
    /// This class handles the initial UDP circuit setup with a client and passes on subsequent packets to the LLPacketServer
    /// </summary>
    public class LLUDPServer : ILLClientStackNetworkHandler, IClientNetworkServer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <value>
        /// The client circuits established with this UDP server.  If a client exists here we can also assume that
        /// it is populated in clientCircuits_reverse and proxyCircuits (if relevant)
        /// </value>
        protected Dictionary<EndPoint, uint> clientCircuits = new Dictionary<EndPoint, uint>();
        public Hashtable clientCircuits_reverse = Hashtable.Synchronized(new Hashtable());
        protected Dictionary<uint, EndPoint> proxyCircuits = new Dictionary<uint, EndPoint>();
        
        private Socket m_socket;
        protected IPEndPoint ServerIncoming;
        protected byte[] RecvBuffer = new byte[4096];
        protected byte[] ZeroBuffer = new byte[8192];

        /// <value>
        /// This is an endpoint that is reused where we don't need to protect the information from potentially
        /// being stomped on by other threads.
        /// </value>
        protected EndPoint reusedEpSender = new IPEndPoint(IPAddress.Any, 0);
        
        protected int proxyPortOffset;
        
        protected AsyncCallback ReceivedData;
        protected LLPacketServer m_packetServer;
        protected Location m_location;

        protected uint listenPort;
        protected bool Allow_Alternate_Port;
        protected IPAddress listenIP = IPAddress.Parse("0.0.0.0");
        protected IScene m_localScene;
        protected int m_clientSocketReceiveBuffer = 0;

        /// <value>
        /// Manages authentication for agent circuits
        /// </value>
        protected AgentCircuitManager m_circuitManager;

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
            //return x.RegionHandle == m_location.RegionHandle;
            return x == m_location;
        }

        public void AddScene(IScene x)
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

        public LLUDPServer(
            IPAddress _listenIP, ref uint port, int proxyPortOffset, bool allow_alternate_port, IConfigSource configSource, 
            AgentCircuitManager authenticateClass)
        {
            Initialise(_listenIP, ref port, proxyPortOffset, allow_alternate_port, configSource, authenticateClass);
        }

        /// <summary>
        /// Initialize the server
        /// </summary>
        /// <param name="_listenIP"></param>
        /// <param name="port"></param>
        /// <param name="proxyPortOffsetParm"></param>
        /// <param name="allow_alternate_port"></param>
        /// <param name="configSource"></param>
        /// <param name="assetCache"></param>
        /// <param name="circuitManager"></param>
        public void Initialise(
            IPAddress _listenIP, ref uint port, int proxyPortOffsetParm, bool allow_alternate_port, IConfigSource configSource,
            AgentCircuitManager circuitManager)
        {
            ClientStackUserSettings userSettings = new ClientStackUserSettings();
            
            IConfig config = configSource.Configs["ClientStack.LindenUDP"];

            if (config != null)
            {
                if (config.Contains("client_throttle_max_bps"))
                {
                    int maxBPS = config.GetInt("client_throttle_max_bps", 1500000);
                    userSettings.TotalThrottleSettings = new ThrottleSettings(0, maxBPS,
                    maxBPS > 28000 ? maxBPS : 28000);
                }

                if (config.Contains("client_throttle_multiplier"))
                    userSettings.ClientThrottleMultipler = config.GetFloat("client_throttle_multiplier");
                if (config.Contains("client_socket_rcvbuf_size"))
                    m_clientSocketReceiveBuffer = config.GetInt("client_socket_rcvbuf_size");
            }
            
            m_log.DebugFormat("[CLIENT]: client_throttle_multiplier = {0}", userSettings.ClientThrottleMultipler);
            m_log.DebugFormat("[CLIENT]: client_socket_rcvbuf_size  = {0}", (m_clientSocketReceiveBuffer != 0 ? 
                                                                             m_clientSocketReceiveBuffer.ToString() : "OS default"));
                
            proxyPortOffset = proxyPortOffsetParm;
            listenPort = (uint) (port + proxyPortOffsetParm);
            listenIP = _listenIP;
            Allow_Alternate_Port = allow_alternate_port;
            m_circuitManager = circuitManager;
            CreatePacketServer(userSettings);

            // Return new port
            // This because in Grid mode it is not really important what port the region listens to as long as it is correctly registered.
            // So the option allow_alternate_ports="true" was added to default.xml
            port = (uint)(listenPort - proxyPortOffsetParm);
        }

        protected virtual void CreatePacketServer(ClientStackUserSettings userSettings)
        {
            new LLPacketServer(this, userSettings);
        }

        /// <summary>
        /// This method is called every time that we receive new UDP data. 
        /// </summary>
        /// <param name="result"></param>
        protected virtual void OnReceivedData(IAsyncResult result)
        {
            Packet packet = null;
            int numBytes = 1;
            EndPoint epSender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint epProxy = null;

            try
            {
                if (EndReceive(out numBytes, result, ref epSender))
                {
                    // Make sure we are getting zeroes when running off the
                    // end of grab / degrab packets from old clients
                    Array.Clear(RecvBuffer, numBytes, RecvBuffer.Length - numBytes);
                    
                    int packetEnd = numBytes - 1;
                    if (proxyPortOffset != 0) packetEnd -= 6;
                    
                    try
                    {
                        packet = PacketPool.Instance.GetPacket(RecvBuffer, ref packetEnd, ZeroBuffer);
                    }
                    catch (MalformedDataException e)
                    {
                        m_log.DebugFormat("[CLIENT]: Dropped Malformed Packet due to MalformedDataException: {0}", e.StackTrace);
                    }
                    catch (IndexOutOfRangeException e)
                    {
                        m_log.DebugFormat("[CLIENT]: Dropped Malformed Packet due to IndexOutOfRangeException: {0}", e.StackTrace);
                    }
                    catch (Exception e)
                    {
                        m_log.Debug("[CLIENT]: " + e);
                    }
                }
            
            
                if (proxyPortOffset != 0)
                {
                    // If we've received a use circuit packet, then we need to decode an endpoint proxy, if one exists,
                    // before allowing the RecvBuffer to be overwritten by the next packet. 
                    if (packet != null && packet.Type == PacketType.UseCircuitCode)
                    {
                        epProxy = epSender;
                    }
                    
                    // Now decode the message from the proxy server
                    epSender = ProxyCodec.DecodeProxyMessage(RecvBuffer, ref numBytes);
                }
            }
            catch (Exception ex)
            {
                m_log.ErrorFormat("[CLIENT]: Exception thrown during EndReceive(): {0}", ex);
            }

            BeginRobustReceive(); 

            if (packet != null)
            {
                if (packet.Type == PacketType.UseCircuitCode)
                    AddNewClient((UseCircuitCodePacket)packet, epSender, epProxy);
                else
                    ProcessInPacket(packet, epSender);
            }
        }
        
        /// <summary>
        /// Process a successfully received packet.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="epSender"></param>
        protected virtual void ProcessInPacket(Packet packet, EndPoint epSender)
        {
            try
            {
                // do we already have a circuit for this endpoint
                uint circuit;
                bool ret;
                
                lock (clientCircuits)
                {
                    ret = clientCircuits.TryGetValue(epSender, out circuit);
                }

                if (ret)
                {
                    //if so then send packet to the packetserver
                    //m_log.DebugFormat(
                    //    "[UDPSERVER]: For circuit {0} {1} got packet {2}", circuit, epSender, packet.Type);

                    m_packetServer.InPacket(circuit, packet);
                }
            }
            catch (Exception e)
            {
                m_log.Error("[CLIENT]: Exception in processing packet - ignoring: ", e);
            }
        }
        
        /// <summary>
        /// Begin an asynchronous receive of the next bit of raw data
        /// </summary>
        protected virtual void BeginReceive()
        {
            m_socket.BeginReceiveFrom(
                RecvBuffer, 0, RecvBuffer.Length, SocketFlags.None, ref reusedEpSender, ReceivedData, null);
        }

        /// <summary>
        /// Begin a robust asynchronous receive of the next bit of raw data.  Robust means that SocketExceptions are
        /// automatically dealt with until the next set of valid UDP data is received.
        /// </summary>
        private void BeginRobustReceive()
        {
            bool done = false;

            while (!done)
            {
                try
                {
                    BeginReceive();
                    done = true;
                }
                catch (SocketException e)
                {
                    // ENDLESS LOOP ON PURPOSE!
                    // Reset connection and get next UDP packet off the buffer
                    // If the UDP packet is part of the same stream, this will happen several hundreds of times before
                    // the next set of UDP data is for a valid client.

                    try
                    {
                        CloseCircuit(e);
                    }
                    catch (Exception e2)
                    {
                        m_log.ErrorFormat(
                            "[CLIENT]: Exception thrown when trying to close the circuit for {0} - {1}", reusedEpSender,
                            e2);
                    }
                }
                catch (ObjectDisposedException)
                {
                    m_log.Info(
                        "[UDPSERVER]: UDP Object disposed.   No need to worry about this if you're restarting the simulator.");

                    done = true;
                }
                catch (Exception ex)
                {
                    m_log.ErrorFormat("[CLIENT]: Exception thrown during BeginReceive(): {0}", ex);
                }
            }
        }

        /// <summary>
        /// Close a client circuit.  This is done in response to an exception on receive, and should not be called
        /// normally.
        /// </summary>
        /// <param name="e">The exception that caused the close.  Can be null if there was no exception</param>
        private void CloseCircuit(Exception e)
        {
            uint circuit;
            lock (clientCircuits)
            {
                if (clientCircuits.TryGetValue(reusedEpSender, out circuit))
                {
                    m_packetServer.CloseCircuit(circuit);
                    
                    if (e != null)
                        m_log.ErrorFormat(
                            "[CLIENT]: Closed circuit {0} {1} due to exception {2}", circuit, reusedEpSender, e);
                }
            }
        }
        
        /// <summary>
        /// Finish the process of asynchronously receiving the next bit of raw data
        /// </summary>
        /// <param name="numBytes">The number of bytes received.  Will return 0 if no bytes were recieved
        /// <param name="result"></param>
        /// <param name="epSender">The sender of the data</param>
        /// <returns></returns>
        protected virtual bool EndReceive(out int numBytes, IAsyncResult result, ref EndPoint epSender)
        {
            bool hasReceivedOkay = false;
            numBytes = 0;
            
            try
            {
                numBytes = m_socket.EndReceiveFrom(result, ref epSender);
                hasReceivedOkay = true;
            }
            catch (SocketException e)
            {
                // TODO : Actually only handle those states that we have control over, re-throw everything else,
                // TODO: implement cases as we encounter them.
                //m_log.Error("[CLIENT]: Connection Error! - " + e.ToString());
                switch (e.SocketErrorCode)
                {
                    case SocketError.AlreadyInProgress:
                        return hasReceivedOkay;

                    case SocketError.NetworkReset:
                    case SocketError.ConnectionReset:
                    case SocketError.OperationAborted:
                        break;

                    default:
                        throw;
                }
            }
            catch (ObjectDisposedException e)
            {
                m_log.DebugFormat("[CLIENT]: ObjectDisposedException: Object {0} disposed.", e.ObjectName);
                // Uhh, what object, and why? this needs better handling.
            }
            
            return hasReceivedOkay;
        }

        /// <summary>
        /// Add a new client circuit.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="epSender"></param>
        /// <param name="epProxy"></param>
        protected virtual void AddNewClient(UseCircuitCodePacket useCircuit, EndPoint epSender, EndPoint epProxy)
        {
            //Slave regions don't accept new clients
            if (m_localScene.RegionStatus != RegionStatus.SlaveScene)
            {
                AuthenticateResponse sessionInfo;
                bool isNewCircuit = false;
                
                if (!m_packetServer.IsClientAuthorized(useCircuit, m_circuitManager, out sessionInfo))
                {
                    m_log.WarnFormat(
                        "[CONNECTION FAILURE]: Connection request for client {0} connecting with unnotified circuit code {1} from {2}",
                        useCircuit.CircuitCode.ID, useCircuit.CircuitCode.Code, epSender);
                    
                    return;
                }
                
                lock (clientCircuits)
                {
                    if (!clientCircuits.ContainsKey(epSender))
                    {
                        clientCircuits.Add(epSender, useCircuit.CircuitCode.Code);
                        isNewCircuit = true;
                    }
                }

                if (isNewCircuit)
                {
                    // This doesn't need locking as it's synchronized data
                    clientCircuits_reverse[useCircuit.CircuitCode.Code] = epSender;

                    lock (proxyCircuits)
                    {
                        proxyCircuits[useCircuit.CircuitCode.Code] = epProxy;
                    }
                    
                    m_packetServer.AddNewClient(epSender, useCircuit, sessionInfo, epProxy);
                                    
                    //m_log.DebugFormat(
                    //    "[CONNECTION SUCCESS]: Incoming client {0} (circuit code {1}) received and authenticated for {2}", 
                    //    useCircuit.CircuitCode.ID, useCircuit.CircuitCode.Code, m_localScene.RegionInfo.RegionName);
                }
            }
            
            // Ack the UseCircuitCode packet
            PacketAckPacket ack_it = (PacketAckPacket)PacketPool.Instance.GetPacket(PacketType.PacketAck);
            // TODO: don't create new blocks if recycling an old packet
            ack_it.Packets = new PacketAckPacket.PacketsBlock[1];
            ack_it.Packets[0] = new PacketAckPacket.PacketsBlock();
            ack_it.Packets[0].ID = useCircuit.Header.Sequence;
            // ((useCircuit.Header.Sequence < uint.MaxValue) ? useCircuit.Header.Sequence : 0) is just a failsafe to ensure that we don't overflow.
            ack_it.Header.Sequence = ((useCircuit.Header.Sequence < uint.MaxValue) ? useCircuit.Header.Sequence : 0) + 1;
            ack_it.Header.Reliable = false;

            byte[] ackmsg = ack_it.ToBytes();

            // Need some extra space in case we need to add proxy
            // information to the message later
            byte[] msg = new byte[4096];
            Buffer.BlockCopy(ackmsg, 0, msg, 0, ackmsg.Length);

            SendPacketTo(msg, ackmsg.Length, SocketFlags.None, useCircuit.CircuitCode.Code);

            PacketPool.Instance.ReturnPacket(useCircuit);
        }

        public void ServerListener()
        {
            uint newPort = listenPort;
            m_log.Info("[UDPSERVER]: Opening UDP socket on " + listenIP + " " + newPort + ".");

            ServerIncoming = new IPEndPoint(listenIP, (int)newPort);
            m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            if (0 != m_clientSocketReceiveBuffer)
                m_socket.ReceiveBufferSize = m_clientSocketReceiveBuffer;
            m_socket.Bind(ServerIncoming);
            // Add flags to the UDP socket to prevent "Socket forcibly closed by host"
            // uint IOC_IN = 0x80000000;
            // uint IOC_VENDOR = 0x18000000;
            // uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            // TODO: this apparently works in .NET but not in Mono, need to sort out the right flags here.
            // m_socket.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);

            listenPort = newPort;

            m_log.Info("[UDPSERVER]: UDP socket bound, getting ready to listen");

            ReceivedData = OnReceivedData;
            BeginReceive();

            m_log.Info("[UDPSERVER]: Listening on port " + newPort);
        }

        public virtual void RegisterPacketServer(LLPacketServer server)
        {
            m_packetServer = server;
        }

        public virtual void SendPacketTo(byte[] buffer, int size, SocketFlags flags, uint circuitcode)
            //EndPoint packetSender)
        {
            // find the endpoint for this circuit
            EndPoint sendto;
            try 
            {
                sendto = (EndPoint)clientCircuits_reverse[circuitcode];
            } 
            catch 
            {
                // Exceptions here mean there is no circuit
                m_log.Warn("[CLIENT]: Circuit not found, not sending packet");
                return;
            }

            if (sendto != null)
            {
                //we found the endpoint so send the packet to it
                if (proxyPortOffset != 0)
                {
                    //MainLog.Instance.Verbose("UDPSERVER", "SendPacketTo proxy " + proxyCircuits[circuitcode].ToString() + ": client " + sendto.ToString());
                    ProxyCodec.EncodeProxyMessage(buffer, ref size, sendto);
                    m_socket.SendTo(buffer, size, flags, proxyCircuits[circuitcode]);
                }
                else
                {
                    //MainLog.Instance.Verbose("UDPSERVER", "SendPacketTo : client " + sendto.ToString());
                    try
                    {
                        m_socket.SendTo(buffer, size, flags, sendto);
                    }
                    catch (SocketException SockE)
                    {
                        m_log.ErrorFormat("[UDPSERVER]: Caught Socket Error in the send buffer!. {0}",SockE.ToString());
                    }
                }
            }
        }

        public virtual void RemoveClientCircuit(uint circuitcode)
        {
            EndPoint sendto;
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
                            "[CLIENT]: endpoint for circuit code {0} in RemoveClientCircuit() was unexpectedly null!", circuitcode);
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
            
            AuthenticateResponse sessionInfo;
            
            if (!m_packetServer.IsClientAuthorized(useCircuit, m_circuitManager, out sessionInfo))
            {
                m_log.WarnFormat(
                    "[CLIENT]: Restore request denied to avatar {0} connecting with unauthorized circuit code {1}",
                    useCircuit.CircuitCode.ID, useCircuit.CircuitCode.Code);
                
                return;
            }

            lock (clientCircuits)
            {
                if (!clientCircuits.ContainsKey(userEP))
                    clientCircuits.Add(userEP, useCircuit.CircuitCode.Code);
                else
                    m_log.Error("[CLIENT]: clientCircuits already contains entry for user " + useCircuit.CircuitCode.Code + ". NOT adding.");
            }

            // This data structure is synchronized, so we don't need the lock
            if (!clientCircuits_reverse.ContainsKey(useCircuit.CircuitCode.Code))
                clientCircuits_reverse.Add(useCircuit.CircuitCode.Code, userEP);
            else
                m_log.Error("[CLIENT]: clientCurcuits_reverse already contains entry for user " + useCircuit.CircuitCode.Code + ". NOT adding.");

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

            m_packetServer.AddNewClient(userEP, useCircuit, sessionInfo, proxyEP);
        }
    }
}
