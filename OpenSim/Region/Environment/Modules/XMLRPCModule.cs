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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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
using System.Threading;
using libsecondlife;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework.Console;

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

namespace OpenSim.Region.Environment.Modules
{
    public class XMLRPCModule : IRegionModule, IXMLRPC
    {
        private Scene m_scene;
        private Queue<RPCRequestInfo> rpcQueue = new Queue<RPCRequestInfo>();
        private object XMLRPCListLock = new object();
        private string m_name = "XMLRPCModule";
        private int RemoteReplyScriptWait = 300;
        private int RemoteReplyScriptTimeout = 900;
        private int m_remoteDataPort = 0;
        private List<Scene> m_scenes = new List<Scene>();
        private LogBase m_log;

        // <channel id, RPCChannelInfo>
        private Dictionary<LLUUID, RPCChannelInfo> m_openChannels;

        // <channel id, RPCRequestInfo>
        private Dictionary<LLUUID, RPCRequestInfo> m_pendingResponse;

        public XMLRPCModule()
        {
            m_log = MainLog.Instance;
        }

        public void Initialise(Scene scene, IConfigSource config)
        {
            try
            {

                m_remoteDataPort = config.Configs["Network"].GetInt("remoteDataPort", m_remoteDataPort);

            }
            catch (Exception e)
            {
            }

            if (!m_scenes.Contains(scene))
            {
                m_scenes.Add(scene);

                scene.RegisterModuleInterface<IXMLRPC>(this);
            }
        }

        public void PostInitialise()
        {
            if ( IsEnabled() )
            {
                m_openChannels = new Dictionary<LLUUID, RPCChannelInfo>();
                m_pendingResponse = new Dictionary<LLUUID, RPCRequestInfo>();

                // Start http server
                // Attach xmlrpc handlers
                m_log.Verbose("REMOTE_DATA", "Starting XMLRPC Server on port " + m_remoteDataPort + " for llRemoteData commands.");
                BaseHttpServer httpServer = new BaseHttpServer((uint)m_remoteDataPort);
                httpServer.AddXmlRPCHandler("llRemoteData", XmlRpcRemoteData);
                httpServer.Start();
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return m_name; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public bool IsEnabled()
        {
            return (m_remoteDataPort > 0);
        }

        /**********************************************
         * OpenXMLRPCChannel
         * 
         * Generate a LLUUID channel key and add it and
         * the prim id to dictionary <channelUUID, primUUID>
         * 
         * First check if there is a channel assigned for
         * this itemID.  If there is, then someone called
         * llOpenRemoteDataChannel twice.  Just return the
         * original channel.  Other option is to delete the
         * current channel and assign a new one.
         * 
         * ********************************************/

        public LLUUID OpenXMLRPCChannel(uint localID, LLUUID itemID)
        {
            LLUUID channel = null;

            //Is a dupe?
            foreach (RPCChannelInfo ci in m_openChannels.Values)
            {
                if (ci.GetItemID().Equals(itemID))
                {
                    // return the original channel ID for this item
                    channel = ci.GetChannelID();
                    break;
                }
            }

            if ((channel.Equals(null)) || (channel.Equals(LLUUID.Zero)))
            {
                channel = LLUUID.Random();
                RPCChannelInfo rpcChanInfo = new RPCChannelInfo(localID, itemID, channel);
                lock (XMLRPCListLock)
                {
                    m_openChannels.Add(channel, rpcChanInfo);
                }
            }

            return channel;
        }

        /**********************************************
         * Remote Data Reply
         * 
         * Response to RPC message
         * 
         *********************************************/

        public void RemoteDataReply(string channel, string message_id, string sdata, int idata)
        {
            RPCRequestInfo rpcInfo;
            LLUUID message_key = new LLUUID(message_id);

            if (m_pendingResponse.TryGetValue(message_key, out rpcInfo))
            {
                rpcInfo.SetRetval(sdata);
                rpcInfo.SetProcessed(true);

                lock (XMLRPCListLock)
                {
                    m_pendingResponse.Remove(message_key);
                }
            }
        }

        /**********************************************
         * CloseXMLRPCChannel
         * 
         * Remove channel from dictionary
         * 
         *********************************************/

        public void CloseXMLRPCChannel(LLUUID channelKey)
        {
            if (m_openChannels.ContainsKey(channelKey))
                m_openChannels.Remove(channelKey);
        }


        public XmlRpcResponse XmlRpcRemoteData(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();

            Hashtable requestData = (Hashtable) request.Params[0];
            bool GoodXML = (requestData.Contains("Channel") && requestData.Contains("IntValue") &&
                            requestData.Contains("StringValue"));

            if (GoodXML)
            {
                LLUUID channel = new LLUUID((string) requestData["Channel"]);
                RPCChannelInfo rpcChanInfo;
                if (m_openChannels.TryGetValue(channel, out rpcChanInfo))
                {
                    string intVal = (string) requestData["IntValue"];
                    string strVal = (string) requestData["StringValue"];

                    RPCRequestInfo rpcInfo;

                    lock (XMLRPCListLock)
                    {
                        rpcInfo =
                            new RPCRequestInfo(rpcChanInfo.GetLocalID(), rpcChanInfo.GetItemID(), channel, strVal,
                                               intVal);
                        rpcQueue.Enqueue(rpcInfo);
                    }

                    int timeoutCtr = 0;

                    while (!rpcInfo.IsProcessed() && (timeoutCtr < RemoteReplyScriptTimeout))
                    {
                        Thread.Sleep(RemoteReplyScriptWait);
                        timeoutCtr += RemoteReplyScriptWait;
                    }
                    if (rpcInfo.IsProcessed())
                    {
                        response.Value = rpcInfo.GetRetval();
                        rpcInfo = null;
                    }
                    else
                    {
                        response.SetFault(-1, "Script timeout");
                        lock (XMLRPCListLock)
                        {
                            m_pendingResponse.Remove(rpcInfo.GetMessageID());
                        }
                    }
                }
                else
                {
                    response.SetFault(-1, "Invalid channel");
                }
            }

            return response;
        }

        public bool hasRequests()
        {
            return (rpcQueue.Count > 0);
        }

        public RPCRequestInfo GetNextRequest()
        {
            lock (XMLRPCListLock)
            {
                RPCRequestInfo rpcInfo = rpcQueue.Dequeue();
                m_pendingResponse.Add(rpcInfo.GetMessageID(), rpcInfo);
                return rpcInfo;
            }
        }
    }

    /**************************************************************
     * 
     * Class RPCRequestInfo
     * 
     * Holds details about incoming requests until they are picked
     * from the queue by LSLLongCmdHandler
     * ***********************************************************/

    public class RPCRequestInfo
    {
        private string m_StrVal;
        private string m_IntVal;
        private bool m_processed;
        private string m_resp;
        private uint m_localID;
        private LLUUID m_ItemID;
        private LLUUID m_MessageID;
        private LLUUID m_ChannelKey;

        public RPCRequestInfo(uint localID, LLUUID itemID, LLUUID channelKey, string strVal, string intVal)
        {
            m_localID = localID;
            m_StrVal = strVal;
            m_IntVal = intVal;
            m_ItemID = itemID;
            m_ChannelKey = channelKey;
            m_MessageID = LLUUID.Random();
            m_processed = false;
            m_resp = "";
        }

        public bool IsProcessed()
        {
            return m_processed;
        }

        public LLUUID GetChannelKey()
        {
            return m_ChannelKey;
        }

        public void SetProcessed(bool processed)
        {
            m_processed = processed;
        }

        public void SetRetval(string resp)
        {
            m_resp = resp;
        }

        public string GetRetval()
        {
            return m_resp;
        }

        public uint GetLocalID()
        {
            return m_localID;
        }

        public LLUUID GetItemID()
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

        public LLUUID GetMessageID()
        {
            return m_MessageID;
        }
    }

    public class RPCChannelInfo
    {
        private LLUUID m_itemID;
        private uint m_localID;
        private LLUUID m_ChannelKey;

        public RPCChannelInfo(uint localID, LLUUID itemID, LLUUID channelID)
        {
            m_ChannelKey = channelID;
            m_localID = localID;
            m_itemID = itemID;
        }

        public LLUUID GetItemID()
        {
            return m_itemID;
        }

        public LLUUID GetChannelID()
        {
            return m_ChannelKey;
        }

        public uint GetLocalID()
        {
            return m_localID;
        }
    }
}