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
using log4net;
using Mono.Addins;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Servers;

namespace OpenSim.ApplicationPlugins.RegionProxy
{
    /* This module has an interface to OpenSim clients that is constant, and is responsible for relaying
     * messages to and from clients to the region objects. Since the region objects can be duplicated and
     * moved dynamically, the proxy provides methods for changing and adding regions. If more than one region
     * is associated with a client port, then the message will be broadcasted to all those regions.
     *
     * The client interface port may be blocked. While being blocked, all messages from the clients will be
     * stored in the proxy. Once the interface port is unblocked again, all stored messages will be resent
     * to the regions. This functionality is used when moving or cloning an region to make sure that no messages
     * are sent to the region while it is being reconfigured.
     *
     * The proxy opens a XmlRpc interface with these public methods:
     * - AddPort
     * - AddRegion
     * - ChangeRegion
     * - BlockClientMessages
     * - UnblockClientMessages
     */

    public class RegionProxyPlugin : IApplicationPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private BaseHttpServer command_server;
        private ProxyServer proxy;

        #region IApplicationPlugin Members
        // TODO: required by IPlugin, but likely not at all right
        string m_name = "RegionProxy";
        string m_version = "0.1";

        public string Version { get { return m_version; } }
        public string Name { get { return m_name; } }

        public void Initialise() 
        { 
            m_log.Info("[PROXY]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException (Name);
        }

        public void Initialise(OpenSimBase openSim)
        {
            m_log.Info("[PROXY] Starting proxy");
            string proxyURL = openSim.ConfigSource.Source.Configs["Network"].GetString("proxy_url", "");
            if (proxyURL.Length == 0) return;

            uint port = (uint) Int32.Parse(proxyURL.Split(new char[] {':'})[2]);
            command_server = new BaseHttpServer(port);
            command_server.Start();
            command_server.AddXmlRPCHandler("AddPort", AddPort);
            command_server.AddXmlRPCHandler("AddRegion", AddRegion);
            command_server.AddXmlRPCHandler("DeleteRegion", DeleteRegion);
            command_server.AddXmlRPCHandler("ChangeRegion", ChangeRegion);
            command_server.AddXmlRPCHandler("BlockClientMessages", BlockClientMessages);
            command_server.AddXmlRPCHandler("UnblockClientMessages", UnblockClientMessages);
            command_server.AddXmlRPCHandler("Stop", Stop);

            proxy = new ProxyServer(m_log);
        }

        public void Dispose()
        {
        }

        #endregion

        private XmlRpcResponse Stop(XmlRpcRequest request)
        {
            try
            {
                proxy.Stop();
            }
            catch (Exception e)
            {
                m_log.Error("[PROXY]" + e.Message);
                m_log.Error("[PROXY]" + e.StackTrace);
            }
            return new XmlRpcResponse();
        }

        private XmlRpcResponse AddPort(XmlRpcRequest request)
        {
            try
            {
                int clientPort = (int) request.Params[0];
                int regionPort = (int) request.Params[1];
                string regionUrl = (string) request.Params[2];
                proxy.AddPort(clientPort, regionPort, regionUrl);
            }
            catch (Exception e)
            {
                m_log.Error("[PROXY]" + e.Message);
                m_log.Error("[PROXY]" + e.StackTrace);
            }
            return new XmlRpcResponse();
        }

        private XmlRpcResponse AddRegion(XmlRpcRequest request)
        {
            try
            {
                int currentRegionPort = (int) request.Params[0];
                string currentRegionUrl = (string) request.Params[1];
                int newRegionPort = (int) request.Params[2];
                string newRegionUrl = (string) request.Params[3];
                proxy.AddRegion(currentRegionPort, currentRegionUrl, newRegionPort, newRegionUrl);
            }
            catch (Exception e)
            {
                m_log.Error("[PROXY]" + e.Message);
                m_log.Error("[PROXY]" + e.StackTrace);
            }
            return new XmlRpcResponse();
        }

        private XmlRpcResponse ChangeRegion(XmlRpcRequest request)
        {
            try
            {
                int currentRegionPort = (int) request.Params[0];
                string currentRegionUrl = (string) request.Params[1];
                int newRegionPort = (int) request.Params[2];
                string newRegionUrl = (string) request.Params[3];
                proxy.ChangeRegion(currentRegionPort, currentRegionUrl, newRegionPort, newRegionUrl);
            }
            catch (Exception e)
            {
                m_log.Error("[PROXY]" + e.Message);
                m_log.Error("[PROXY]" + e.StackTrace);
            }
            return new XmlRpcResponse();
        }

        private XmlRpcResponse DeleteRegion(XmlRpcRequest request)
        {
            try
            {
                int currentRegionPort = (int) request.Params[0];
                string currentRegionUrl = (string) request.Params[1];
                proxy.DeleteRegion(currentRegionPort, currentRegionUrl);
            }
            catch (Exception e)
            {
                m_log.Error("[PROXY]" + e.Message);
                m_log.Error("[PROXY]" + e.StackTrace);
            }
            return new XmlRpcResponse();
        }

        private XmlRpcResponse BlockClientMessages(XmlRpcRequest request)
        {
            try
            {
                string regionUrl = (string) request.Params[0];
                int regionPort = (int) request.Params[1];
                proxy.BlockClientMessages(regionUrl, regionPort);
            }
            catch (Exception e)
            {
                m_log.Error("[PROXY]" + e.Message);
                m_log.Error("[PROXY]" + e.StackTrace);
            }
            return new XmlRpcResponse();
        }

        private XmlRpcResponse UnblockClientMessages(XmlRpcRequest request)
        {
            try
            {
                string regionUrl = (string) request.Params[0];
                int regionPort = (int) request.Params[1];
                proxy.UnblockClientMessages(regionUrl, regionPort);
            }
            catch (Exception e)
            {
                m_log.Error("[PROXY]" + e.Message);
                m_log.Error("[PROXY]" + e.StackTrace);
            }
            return new XmlRpcResponse();
        }
    }


    public class ProxyServer
    {
        protected readonly ILog m_log;
        protected ProxyMap proxy_map = new ProxyMap();
        protected AsyncCallback receivedData;
        protected bool running;

        public ProxyServer(ILog log)
        {
            m_log = log;
            running = false;
            receivedData = new AsyncCallback(OnReceivedData);
        }

        public void BlockClientMessages(string regionUrl, int regionPort)
        {
            EndPoint client = proxy_map.GetClient(new IPEndPoint(IPAddress.Parse(regionUrl), regionPort));
            ProxyMap.RegionData rd = proxy_map.GetRegionData(client);
            rd.isBlocked = true;
        }

        public void UnblockClientMessages(string regionUrl, int regionPort)
        {
            EndPoint client = proxy_map.GetClient(new IPEndPoint(IPAddress.Parse(regionUrl), regionPort));
            ProxyMap.RegionData rd = proxy_map.GetRegionData(client);

            rd.isBlocked = false;
            while (rd.storedMessages.Count > 0)
            {
                StoredMessage msg = (StoredMessage) rd.storedMessages.Dequeue();
                //m_log.Verbose("[PROXY]"+"Resending blocked message from {0}", msg.senderEP);
                SendMessage(msg.buffer, msg.length, msg.senderEP, msg.sd);
            }
        }

        public void AddRegion(int oldRegionPort, string oldRegionUrl, int newRegionPort, string newRegionUrl)
        {
            //m_log.Verbose("[PROXY]"+"AddRegion {0} {1}", oldRegionPort, newRegionPort);
            EndPoint client = proxy_map.GetClient(new IPEndPoint(IPAddress.Parse(oldRegionUrl), oldRegionPort));
            ProxyMap.RegionData data = proxy_map.GetRegionData(client);
            data.regions.Add(new IPEndPoint(IPAddress.Parse(newRegionUrl), newRegionPort));
        }

        public void ChangeRegion(int oldRegionPort, string oldRegionUrl, int newRegionPort, string newRegionUrl)
        {
            //m_log.Verbose("[PROXY]"+"ChangeRegion {0} {1}", oldRegionPort, newRegionPort);
            EndPoint client = proxy_map.GetClient(new IPEndPoint(IPAddress.Parse(oldRegionUrl), oldRegionPort));
            ProxyMap.RegionData data = proxy_map.GetRegionData(client);
            data.regions.Clear();
            data.regions.Add(new IPEndPoint(IPAddress.Parse(newRegionUrl), newRegionPort));
        }

        public void DeleteRegion(int oldRegionPort, string oldRegionUrl)
        {
            m_log.InfoFormat("[PROXY]" + "DeleteRegion {0} {1}", oldRegionPort, oldRegionUrl);
            EndPoint regionEP = new IPEndPoint(IPAddress.Parse(oldRegionUrl), oldRegionPort);
            EndPoint client = proxy_map.GetClient(regionEP);
            ProxyMap.RegionData data = proxy_map.GetRegionData(client);
            data.regions.Remove(regionEP);
        }

        public void AddPort(int clientPort, int regionPort, string regionUrl)
        {
            running = true;

            //m_log.Verbose("[PROXY]"+"AddPort {0} {1}", clientPort, regionPort);
            IPEndPoint clientEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), clientPort);
            proxy_map.Add(clientEP, new IPEndPoint(IPAddress.Parse(regionUrl), regionPort));

            ServerData sd = new ServerData();
            sd.clientEP = new IPEndPoint(clientEP.Address, clientEP.Port);

            OpenPort(sd);
        }

        protected void OpenPort(ServerData sd)
        {
            // sd.clientEP must be set before calling this function

            ClosePort(sd);

            try
            {
                m_log.InfoFormat("[PROXY] Opening special UDP socket on {0}", sd.clientEP);
                sd.serverIP = new IPEndPoint(IPAddress.Parse("0.0.0.0"), ((IPEndPoint) sd.clientEP).Port);
                sd.server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sd.server.Bind(sd.serverIP);

                sd.senderEP = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 0);
                //receivedData = new AsyncCallback(OnReceivedData);
                sd.server.BeginReceiveFrom(sd.recvBuffer, 0, sd.recvBuffer.Length, SocketFlags.None, ref sd.senderEP, receivedData, sd);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROXY] Failed to (re)open socket {0}", sd.clientEP);
                m_log.Error("[PROXY]" + e.Message);
                m_log.Error("[PROXY]" + e.StackTrace);
            }
        }

        protected static void ClosePort(ServerData sd)
        {
            // Close the port if it exists and is open
            if (sd.server == null) return;

            try
            {
                sd.server.Shutdown(SocketShutdown.Both);
                sd.server.Close();
            }
            catch (Exception)
            {
            }
        }

        public void Stop()
        {
            running = false;
            m_log.InfoFormat("[PROXY] Stopping the proxy server");
        }


        protected virtual void OnReceivedData(IAsyncResult result)
        {
            if (!running) return;

            ServerData sd = (ServerData) result.AsyncState;
            sd.senderEP = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 0);

            try
            {
                int numBytes = sd.server.EndReceiveFrom(result, ref sd.senderEP);
                if (numBytes > 0)
                {
                    SendMessage(sd.recvBuffer, numBytes, sd.senderEP, sd);
                }
            }
            catch (Exception e)
            {
//                OpenPort(sd); // reopen the port just in case
                m_log.ErrorFormat("[PROXY] EndReceiveFrom failed in {0}", sd.clientEP);
                m_log.Error("[PROXY]" + e.Message);
                m_log.Error("[PROXY]" + e.StackTrace);
            }

            WaitForNextMessage(sd);
        }

        protected void WaitForNextMessage(ServerData sd)
        {
            bool error = true;
            while (error)
            {
                error = false;
                try
                {
                    sd.server.BeginReceiveFrom(sd.recvBuffer, 0, sd.recvBuffer.Length, SocketFlags.None, ref sd.senderEP, receivedData, sd);
                }
                catch (Exception e)
                {
                    error = true;
                    m_log.ErrorFormat("[PROXY] BeginReceiveFrom failed, retrying... {0}", sd.clientEP);
                    m_log.Error("[PROXY]" + e.Message);
                    m_log.Error("[PROXY]" + e.StackTrace);
                    OpenPort(sd);
                }
            }
        }

        protected void SendMessage(byte[] buffer, int length, EndPoint senderEP, ServerData sd)
        {
            int numBytes = length;

            //m_log.ErrorFormat("[PROXY] Got message from {0} in thread {1}, size {2}", senderEP, sd.clientEP, numBytes);
            EndPoint client = proxy_map.GetClient(senderEP);

            if (client != null)
            {
                try
                {
                    client = ProxyCodec.DecodeProxyMessage(buffer, ref numBytes);
                    try
                    {
                        // This message comes from a region object, forward it to the its client
                        sd.server.SendTo(buffer, numBytes, SocketFlags.None, client);
                        //m_log.InfoFormat("[PROXY] Sending region message from {0} to {1}, size {2}", senderEP, client, numBytes);
                    }
                    catch (Exception e)
                    {
                        OpenPort(sd); // reopen the port just in case
                        m_log.ErrorFormat("[PROXY] Failed sending region message from {0} to {1}", senderEP, client);
                        m_log.Error("[PROXY]" + e.Message);
                        m_log.Error("[PROXY]" + e.StackTrace);
                        return;
                    }
                }
                catch (Exception e)
                {
                    OpenPort(sd); // reopen the port just in case
                    m_log.ErrorFormat("[PROXY] Failed decoding region message from {0}", senderEP);
                    m_log.Error("[PROXY]" + e.Message);
                    m_log.Error("[PROXY]" + e.StackTrace);
                    return;
                }
            }
            else
            {
                // This message comes from a client object, forward it to the the region(s)
                ProxyCodec.EncodeProxyMessage(buffer, ref numBytes, senderEP);
                ProxyMap.RegionData rd = proxy_map.GetRegionData(sd.clientEP);
                foreach (EndPoint region in rd.regions)
                {
                    if (rd.isBlocked)
                    {
                        rd.storedMessages.Enqueue(new StoredMessage(buffer, length, numBytes, senderEP, sd));
                    }
                    else
                    {
                        try
                        {
                            sd.server.SendTo(buffer, numBytes, SocketFlags.None, region);
                            //m_log.InfoFormat("[PROXY] Sending client message from {0} to {1}", senderEP, region);
                        }
                        catch (Exception e)
                        {
                            OpenPort(sd); // reopen the port just in case
                            m_log.ErrorFormat("[PROXY] Failed sending client message from {0} to {1}", senderEP, region);
                            m_log.Error("[PROXY]" + e.Message);
                            m_log.Error("[PROXY]" + e.StackTrace);
                            return;
                        }
                    }
                }
            }
        }

        #region Nested type: ProxyMap

        protected class ProxyMap
        {
            private Dictionary<EndPoint, RegionData> map;

            public ProxyMap()
            {
                map = new Dictionary<EndPoint, RegionData>();
            }

            public void Add(EndPoint client, EndPoint region)
            {
                if (map.ContainsKey(client))
                {
                    map[client].regions.Add(region);
                }
                else
                {
                    RegionData regions = new RegionData();
                    map.Add(client, regions);
                    regions.regions.Add(region);
                }
            }

            public RegionData GetRegionData(EndPoint client)
            {
                return map[client];
            }

            public EndPoint GetClient(EndPoint region)
            {
                foreach (KeyValuePair<EndPoint, RegionData> pair in map)
                {
                    if (pair.Value.regions.Contains(region))
                    {
                        return pair.Key;
                    }
                }
                return null;
            }

            #region Nested type: RegionData

            public class RegionData
            {
                public bool isBlocked = false;
                public List<EndPoint> regions = new List<EndPoint>();
                public Queue storedMessages = new Queue();
            }

            #endregion
        }

        #endregion

        #region Nested type: ServerData

        protected class ServerData
        {
            public EndPoint clientEP;
            public byte[] recvBuffer = new byte[4096];
            public EndPoint senderEP;
            public Socket server;
            public IPEndPoint serverIP;

            public ServerData()
            {
                server = null;
            }
        }

        #endregion

        #region Nested type: StoredMessage

        protected class StoredMessage
        {
            public byte[] buffer;
            public int length;
            public ServerData sd;
            public EndPoint senderEP;

            public StoredMessage(byte[] buffer, int length, int maxLength, EndPoint senderEP, ServerData sd)
            {
                this.buffer = new byte[maxLength];
                this.length = length;
                for (int i = 0; i < length; i++) this.buffer[i] = buffer[i];
                this.senderEP = senderEP;
                this.sd = sd;
            }
        }

        #endregion
    }
}
