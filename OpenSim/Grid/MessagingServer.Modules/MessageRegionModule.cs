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
using System.Timers;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Grid.Framework;
using Timer = System.Timers.Timer;

namespace OpenSim.Grid.MessagingServer.Modules
{
    public class MessageRegionModule : IMessageRegionLookup
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private MessageServerConfig m_cfg;

        private IInterServiceUserService m_userServerModule;

        private IGridServiceCore m_messageCore;

        // a dictionary of all current regions this server knows about
        private Dictionary<ulong, RegionProfileData> m_regionInfoCache = new Dictionary<ulong, RegionProfileData>();

        public MessageRegionModule(MessageServerConfig config, IGridServiceCore messageCore)
        {
            m_cfg = config;
            m_messageCore = messageCore;
        }

        public void Initialise()
        {
            m_messageCore.RegisterInterface<IMessageRegionLookup>(this);
        }

        public void PostInitialise()
        {
            IInterServiceUserService messageUserServer;
            if (m_messageCore.TryGet<IInterServiceUserService>(out messageUserServer))
            {
                m_userServerModule = messageUserServer;
            }
        }

        public void RegisterHandlers()
        {
            //have these in separate method as some servers restart the http server and reregister all the handlers.
           
        }

         /// <summary>
        /// Gets and caches a RegionInfo object from the gridserver based on regionhandle
        /// if the regionhandle is already cached, use the cached values
        /// Gets called by lots of threads!!!!!
        /// </summary>
        /// <param name="regionhandle">handle to the XY of the region we're looking for</param>
        /// <returns>A RegionInfo object to stick in the presence info</returns>
        public RegionProfileData GetRegionInfo(ulong regionhandle)
        {
            RegionProfileData regionInfo = null;

            lock (m_regionInfoCache)
            {
                m_regionInfoCache.TryGetValue(regionhandle, out regionInfo);
            }

            if (regionInfo == null) // not found in cache
            {
                regionInfo = RequestRegionInfo(regionhandle);

                if (regionInfo != null) // lookup was successful
                {
                    lock (m_regionInfoCache)
                    {
                        m_regionInfoCache[regionhandle] = regionInfo;
                    }
                }
            }

            return regionInfo;
        }

        public int ClearRegionCache()
        {
            int cachecount = 0;

            lock (m_regionInfoCache)
            {
                cachecount = m_regionInfoCache.Count;
                m_regionInfoCache.Clear();
            }

            return cachecount;
        }

        /// <summary>
        /// Get RegionProfileData from the GridServer.
        /// We'll cache this information in GetRegionInfo and use it for presence updates
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public RegionProfileData RequestRegionInfo(ulong regionHandle)
        {
            RegionProfileData regionProfile = null;
            try
            {
                Hashtable requestData = new Hashtable();
                requestData["region_handle"] = regionHandle.ToString();
                requestData["authkey"] = m_cfg.GridSendKey;

                ArrayList SendParams = new ArrayList();
                SendParams.Add(requestData);

                XmlRpcRequest GridReq = new XmlRpcRequest("simulator_data_request", SendParams);

                XmlRpcResponse GridResp = GridReq.Send(m_cfg.GridServerURL, 3000);

                Hashtable responseData = (Hashtable)GridResp.Value;

                if (responseData.ContainsKey("error"))
                {
                    m_log.Error("[GRID]: error received from grid server" + responseData["error"]);
                    return null;
                }

                uint regX = Convert.ToUInt32((string)responseData["region_locx"]);
                uint regY = Convert.ToUInt32((string)responseData["region_locy"]);
                string internalIpStr = (string)responseData["sim_ip"];

                regionProfile = new RegionProfileData();
                regionProfile.httpPort = (uint)Convert.ToInt32((string)responseData["http_port"]);
                regionProfile.httpServerURI = "http://" + internalIpStr + ":" + regionProfile.httpPort + "/";
                regionProfile.regionHandle = Utils.UIntsToLong((regX * Constants.RegionSize), (regY * Constants.RegionSize));
                regionProfile.regionLocX = regX;
                regionProfile.regionLocY = regY;

                regionProfile.remotingPort = Convert.ToUInt32((string)responseData["remoting_port"]);
                regionProfile.UUID = new UUID((string)responseData["region_UUID"]);
                regionProfile.regionName = (string)responseData["region_name"];
            }
            catch (WebException)
            {
                m_log.Error("[GRID]: " +
                            "Region lookup failed for: " + regionHandle.ToString() +
                            " - Is the GridServer down?");
            }

            return regionProfile;
        }

        public XmlRpcResponse RegionStartup(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable result = new Hashtable();
            result["success"] = "FALSE";

            if (m_userServerModule.SendToUserServer(requestData, "region_startup"))
                result["success"] = "TRUE";

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = result;
            return response;
        }

        public XmlRpcResponse RegionShutdown(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable result = new Hashtable();
            result["success"] = "FALSE";

            if (m_userServerModule.SendToUserServer(requestData, "region_shutdown"))
                result["success"] = "TRUE";

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = result;
            return response;
        }

    }
}