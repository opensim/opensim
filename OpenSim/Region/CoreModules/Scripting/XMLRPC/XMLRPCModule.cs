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
using System.Reflection;
using System.Threading;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Mono.Addins;

/*****************************************************
 *
 * XMLRPCModule
 *
 * Module for accepting incoming communications from
 * external XMLRPC client and calling a remote data
 * procedure for a registered data channel/prim.
 *
 *
 * 1. On module load, open a listener port
 * 2. Attach an XMLRPC handler
 * 3. When a request is received:
 * 3.1 Parse into components: channel key, int, string
 * 3.2 Look up registered channel listeners
 * 3.3 Call the channel (prim) remote data method
 * 3.4 Capture the response (llRemoteDataReply)
 * 3.5 Return response to client caller
 * 3.6 If no response from llRemoteDataReply within
 *     RemoteReplyScriptTimeout, generate script timeout fault
 *
 * Prims in script must:
 * 1. Open a remote data channel
 * 1.1 Generate a channel ID
 * 1.2 Register primid,channelid pair with module
 * 2. Implement the remote data procedure handler
 *
 * llOpenRemoteDataChannel
 * llRemoteDataReply
 * remote_data(integer type, key channel, key messageid, string sender, integer ival, string sval)
 * llCloseRemoteDataChannel
 *
 * **************************************************/

namespace OpenSim.Region.CoreModules.Scripting.XMLRPC
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "XMLRPCModule")]
    public class XMLRPCModule : ISharedRegionModule, IXMLRPC
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_name = "XMLRPCModule";

        // <channel id, RPCChannelInfo>
        private Dictionary<UUID, RPCChannelInfo> m_openChannels;
        private Dictionary<UUID, SendRemoteDataRequest> m_pendingSRDResponses;
        private int m_remoteDataPort = 0;
        public int Port
        {
            get { return m_remoteDataPort; }
        }

        private Dictionary<UUID, RPCRequestInfo> m_rpcPending;
        private Dictionary<UUID, RPCRequestInfo> m_rpcPendingResponses;
        private List<Scene> m_scenes = new List<Scene>();
        private int RemoteReplyScriptTimeout = 9000;
        private int RemoteReplyScriptWait = 300;
        private object XMLRPCListLock = new object();

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
            // We need to create these early because the scripts might be calling
            // But since this gets called for every region, we need to make sure they
            // get called only one time (or we lose any open channels)
            m_openChannels = new Dictionary<UUID, RPCChannelInfo>();
            m_rpcPending = new Dictionary<UUID, RPCRequestInfo>();
            m_rpcPendingResponses = new Dictionary<UUID, RPCRequestInfo>();
            m_pendingSRDResponses = new Dictionary<UUID, SendRemoteDataRequest>();
            if (config.Configs["XMLRPC"] != null)
            {
                try
                {
                    m_remoteDataPort = config.Configs["XMLRPC"].GetInt("XmlRpcPort", m_remoteDataPort);
                }
                catch (Exception)
                {
                }
            }
        }

        public void PostInitialise()
        {
            if (IsEnabled())
            {
                // Start http server
                // Attach xmlrpc handlers
                //                m_log.InfoFormat(
                //                    "[XML RPC MODULE]: Starting up XMLRPC Server on port {0} for llRemoteData commands.",
                //                    m_remoteDataPort);

                IHttpServer httpServer = MainServer.GetHttpServer((uint)m_remoteDataPort);
                httpServer.AddXmlRPCHandler("llRemoteData", XmlRpcRemoteData);
            }
        }

        public void AddRegion(Scene scene)
        {
            if (!IsEnabled())
                return;

            if (!m_scenes.Contains(scene))
            {
                m_scenes.Add(scene);

                scene.RegisterModuleInterface<IXMLRPC>(this);
            }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            if (!IsEnabled())
                return;

            if (m_scenes.Contains(scene))
            {
                scene.UnregisterModuleInterface<IXMLRPC>(this);
                m_scenes.Remove(scene);
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return m_name; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        #region IXMLRPC Members

        public bool IsEnabled()
        {
            return (m_remoteDataPort > 0);
        }

        /**********************************************
         * OpenXMLRPCChannel
         *
         * Generate a UUID channel key and add it and
         * the prim id to dictionary <channelUUID, primUUID>
         *
         * A custom channel key can be proposed.
         * Otherwise, passing UUID.Zero will generate
         * and return a random channel
         *
         * First check if there is a channel assigned for
         * this itemID.  If there is, then someone called
         * llOpenRemoteDataChannel twice.  Just return the
         * original channel.  Other option is to delete the
         * current channel and assign a new one.
         *
         * ********************************************/

        public UUID OpenXMLRPCChannel(uint localID, UUID itemID, UUID channelID)
        {
            UUID newChannel = UUID.Zero;

            // This should no longer happen, but the check is reasonable anyway
            if (null == m_openChannels)
            {
                m_log.Warn("[XML RPC MODULE]: Attempt to open channel before initialization is complete");
                return newChannel;
            }

            //Is a dupe?
            foreach (RPCChannelInfo ci in m_openChannels.Values)
            {
                if (ci.GetItemID().Equals(itemID))
                {
                    // return the original channel ID for this item
                    newChannel = ci.GetChannelID();
                    break;
                }
            }

            if (newChannel == UUID.Zero)
            {
                newChannel = (channelID == UUID.Zero) ? UUID.Random() : channelID;
                RPCChannelInfo rpcChanInfo = new RPCChannelInfo(localID, itemID, newChannel);
                lock (XMLRPCListLock)
                {
                    m_openChannels.Add(newChannel, rpcChanInfo);
                }
            }

            return newChannel;
        }

        // Delete channels based on itemID
        // for when a script is deleted
        public void DeleteChannels(UUID itemID)
        {
            if (m_openChannels != null)
            {
                ArrayList tmp = new ArrayList();

                lock (XMLRPCListLock)
                {
                    foreach (RPCChannelInfo li in m_openChannels.Values)
                    {
                        if (li.GetItemID().Equals(itemID))
                        {
                            tmp.Add(itemID);
                        }
                    }

                    IEnumerator tmpEnumerator = tmp.GetEnumerator();
                    while (tmpEnumerator.MoveNext())
                        m_openChannels.Remove((UUID) tmpEnumerator.Current);
                }
            }
        }

        /**********************************************
         * Remote Data Reply
         *
         * Response to RPC message
         *
         *********************************************/

        public void RemoteDataReply(string channel, string message_id, string sdata, int idata)
        {
            UUID message_key = new UUID(message_id);
            UUID channel_key = new UUID(channel);

            RPCRequestInfo rpcInfo = null;

            if (message_key == UUID.Zero)
            {
                foreach (RPCRequestInfo oneRpcInfo in m_rpcPendingResponses.Values)
                    if (oneRpcInfo.GetChannelKey() == channel_key)
                        rpcInfo = oneRpcInfo;
            }
            else
            {
                m_rpcPendingResponses.TryGetValue(message_key, out rpcInfo);
            }

            if (rpcInfo != null)
            {
                rpcInfo.SetStrRetval(sdata);
                rpcInfo.SetIntRetval(idata);
                rpcInfo.SetProcessed(true);
                m_rpcPendingResponses.Remove(message_key);
            }
            else
            {
                m_log.Warn("[XML RPC MODULE]: Channel or message_id not found");
            }
        }

        /**********************************************
         * CloseXMLRPCChannel
         *
         * Remove channel from dictionary
         *
         *********************************************/

        public void CloseXMLRPCChannel(UUID channelKey)
        {
            if (m_openChannels.ContainsKey(channelKey))
                m_openChannels.Remove(channelKey);
        }


        public bool hasRequests()
        {
            lock (XMLRPCListLock)
            {
                if (m_rpcPending != null)
                    return (m_rpcPending.Count > 0);
                else
                    return false;
            }
        }

        public IXmlRpcRequestInfo GetNextCompletedRequest()
        {
            if (m_rpcPending != null)
            {
                lock (XMLRPCListLock)
                {
                    foreach (UUID luid in m_rpcPending.Keys)
                    {
                        RPCRequestInfo tmpReq;

                        if (m_rpcPending.TryGetValue(luid, out tmpReq))
                        {
                            if (!tmpReq.IsProcessed()) return tmpReq;
                        }
                    }
                }
            }
            return null;
        }

        public void RemoveCompletedRequest(UUID id)
        {
            lock (XMLRPCListLock)
            {
                RPCRequestInfo tmp;
                if (m_rpcPending.TryGetValue(id, out tmp))
                {
                    m_rpcPending.Remove(id);
                    m_rpcPendingResponses.Add(id, tmp);
                }
                else
                {
                    m_log.Error("[XML RPC MODULE]: UNABLE TO REMOVE COMPLETED REQUEST");
                }
            }
        }

        public UUID SendRemoteData(uint localID, UUID itemID, string channel, string dest, int idata, string sdata)
        {
            SendRemoteDataRequest req = new SendRemoteDataRequest(
                localID, itemID, channel, dest, idata, sdata
                );
            m_pendingSRDResponses.Add(req.GetReqID(), req);
            req.Process();
            return req.ReqID;
        }

        public IServiceRequest GetNextCompletedSRDRequest()
        {
            if (m_pendingSRDResponses != null)
            {
                lock (XMLRPCListLock)
                {
                    foreach (UUID luid in m_pendingSRDResponses.Keys)
                    {
                        SendRemoteDataRequest tmpReq;

                        if (m_pendingSRDResponses.TryGetValue(luid, out tmpReq))
                        {
                            if (tmpReq.Finished)
                                return tmpReq;
                        }
                    }
                }
            }
            return null;
        }

        public void RemoveCompletedSRDRequest(UUID id)
        {
            lock (XMLRPCListLock)
            {
                SendRemoteDataRequest tmpReq;
                if (m_pendingSRDResponses.TryGetValue(id, out tmpReq))
                {
                    m_pendingSRDResponses.Remove(id);
                }
            }
        }

        public void CancelSRDRequests(UUID itemID)
        {
            if (m_pendingSRDResponses != null)
            {
                lock (XMLRPCListLock)
                {
                    foreach (SendRemoteDataRequest li in m_pendingSRDResponses.Values)
                    {
                        if (li.ItemID.Equals(itemID))
                            m_pendingSRDResponses.Remove(li.GetReqID());
                    }
                }
            }
        }

        #endregion

        public XmlRpcResponse XmlRpcRemoteData(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();

            Hashtable requestData = (Hashtable) request.Params[0];
            bool GoodXML = (requestData.Contains("Channel") && requestData.Contains("IntValue") &&
                            requestData.Contains("StringValue"));

            if (GoodXML)
            {
                UUID channel = new UUID((string) requestData["Channel"]);
                RPCChannelInfo rpcChanInfo;
                if (m_openChannels.TryGetValue(channel, out rpcChanInfo))
                {
                    string intVal = Convert.ToInt32(requestData["IntValue"]).ToString();
                    string strVal = (string) requestData["StringValue"];

                    RPCRequestInfo rpcInfo;

                    lock (XMLRPCListLock)
                    {
                        rpcInfo =
                            new RPCRequestInfo(rpcChanInfo.GetLocalID(), rpcChanInfo.GetItemID(), channel, strVal,
                                               intVal);
                        m_rpcPending.Add(rpcInfo.GetMessageID(), rpcInfo);
                    }

                    int timeoutCtr = 0;

                    while (!rpcInfo.IsProcessed() && (timeoutCtr < RemoteReplyScriptTimeout))
                    {
                        Thread.Sleep(RemoteReplyScriptWait);
                        timeoutCtr += RemoteReplyScriptWait;
                    }
                    if (rpcInfo.IsProcessed())
                    {
                        Hashtable param = new Hashtable();
                        param["StringValue"] = rpcInfo.GetStrRetval();
                        param["IntValue"] = rpcInfo.GetIntRetval();

                        ArrayList parameters = new ArrayList();
                        parameters.Add(param);

                        response.Value = parameters;
                        rpcInfo = null;
                    }
                    else
                    {
                        response.SetFault(-1, "Script timeout");
                        rpcInfo = null;
                    }
                }
                else
                {
                    response.SetFault(-1, "Invalid channel");
                }
            }

            return response;
        }
    }

    public class RPCRequestInfo: IXmlRpcRequestInfo
    {
        private UUID m_ChannelKey;
        private string m_IntVal;
        private UUID m_ItemID;
        private uint m_localID;
        private UUID m_MessageID;
        private bool m_processed;
        private int m_respInt;
        private string m_respStr;
        private string m_StrVal;

        public RPCRequestInfo(uint localID, UUID itemID, UUID channelKey, string strVal, string intVal)
        {
            m_localID = localID;
            m_StrVal = strVal;
            m_IntVal = intVal;
            m_ItemID = itemID;
            m_ChannelKey = channelKey;
            m_MessageID = UUID.Random();
            m_processed = false;
            m_respStr = String.Empty;
            m_respInt = 0;
        }

        public bool IsProcessed()
        {
            return m_processed;
        }

        public UUID GetChannelKey()
        {
            return m_ChannelKey;
        }

        public void SetProcessed(bool processed)
        {
            m_processed = processed;
        }

        public void SetStrRetval(string resp)
        {
            m_respStr = resp;
        }

        public string GetStrRetval()
        {
            return m_respStr;
        }

        public void SetIntRetval(int resp)
        {
            m_respInt = resp;
        }

        public int GetIntRetval()
        {
            return m_respInt;
        }

        public uint GetLocalID()
        {
            return m_localID;
        }

        public UUID GetItemID()
        {
            return m_ItemID;
        }

        public string GetStrVal()
        {
            return m_StrVal;
        }

        public int GetIntValue()
        {
            return int.Parse(m_IntVal);
        }

        public UUID GetMessageID()
        {
            return m_MessageID;
        }
    }

    public class RPCChannelInfo
    {
        private UUID m_ChannelKey;
        private UUID m_itemID;
        private uint m_localID;

        public RPCChannelInfo(uint localID, UUID itemID, UUID channelID)
        {
            m_ChannelKey = channelID;
            m_localID = localID;
            m_itemID = itemID;
        }

        public UUID GetItemID()
        {
            return m_itemID;
        }

        public UUID GetChannelID()
        {
            return m_ChannelKey;
        }

        public uint GetLocalID()
        {
            return m_localID;
        }
    }

    public class SendRemoteDataRequest: IServiceRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public string Channel;
        public string DestURL;
        private bool _finished;
        public bool Finished
        {
            get { return _finished; }
            set { _finished = value; }
        }
        private Thread httpThread;
        public int Idata;
        private UUID _itemID;
        public UUID ItemID
        {
            get { return _itemID; }
            set { _itemID = value; }
        }
        private uint _localID;
        public uint LocalID
        {
            get { return _localID; }
            set { _localID = value; }
        }
        private UUID _reqID;
        public UUID ReqID
        {
            get { return _reqID; }
            set { _reqID = value; }
        }
        public XmlRpcRequest Request;
        public int ResponseIdata;
        public string ResponseSdata;
        public string Sdata;

        public SendRemoteDataRequest(uint localID, UUID itemID, string channel, string dest, int idata, string sdata)
        {
            this.Channel = channel;
            DestURL = dest;
            this.Idata = idata;
            this.Sdata = sdata;
            ItemID = itemID;
            LocalID = localID;

            ReqID = UUID.Random();
        }

        public void Process()
        {
            _finished = false;
            httpThread = WorkManager.StartThread(SendRequest, "HttpRequestThread", ThreadPriority.BelowNormal, true, false);
        }

        /*
         * TODO: More work on the response codes.  Right now
         * returning 200 for success or 499 for exception
         */

        public void SendRequest()
        {
            Hashtable param = new Hashtable();

            // Check if channel is an UUID
            // if not, use as method name
            UUID parseUID;
            string mName = "llRemoteData";
            if (!string.IsNullOrEmpty(Channel))
                if (!UUID.TryParse(Channel, out parseUID))
                    mName = Channel;
                else
                    param["Channel"] = Channel;

            param["StringValue"] = Sdata;
            param["IntValue"] = Convert.ToString(Idata);

            ArrayList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest(mName, parameters);
            try
            {
                XmlRpcResponse resp = req.Send(DestURL, 30000);
                if (resp != null)
                {
                    Hashtable respParms;
                    if (resp.Value.GetType().Equals(typeof(Hashtable)))
                    {
                        respParms = (Hashtable) resp.Value;
                    }
                    else
                    {
                        ArrayList respData = (ArrayList) resp.Value;
                        respParms = (Hashtable) respData[0];
                    }
                    if (respParms != null)
                    {
                        if (respParms.Contains("StringValue"))
                        {
                            Sdata = (string) respParms["StringValue"];
                        }
                        if (respParms.Contains("IntValue"))
                        {
                            Idata = Convert.ToInt32(respParms["IntValue"]);
                        }
                        if (respParms.Contains("faultString"))
                        {
                            Sdata = (string) respParms["faultString"];
                        }
                        if (respParms.Contains("faultCode"))
                        {
                            Idata = Convert.ToInt32(respParms["faultCode"]);
                        }
                    }
                }
            }
            catch (Exception we)
            {
                Sdata = we.Message;
                m_log.Warn("[SendRemoteDataRequest]: Request failed");
                m_log.Warn(we.StackTrace);
            }

            _finished = true;

            Watchdog.RemoveThread();
        }

        public void Stop()
        {
            try
            {
                if (httpThread != null)
                {
                    Watchdog.AbortThread(httpThread.ManagedThreadId);
                    httpThread = null;
                }
            }
            catch (Exception)
            {
            }
        }

        public UUID GetReqID()
        {
            return ReqID;
        }
    }
}