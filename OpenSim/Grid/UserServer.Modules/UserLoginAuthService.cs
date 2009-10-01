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
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Services;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Grid.UserServer.Modules
{

    /// <summary>
    /// Hypergrid login service used in grid mode.
    /// </summary>
    public class UserLoginAuthService : HGLoginAuthService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public UserConfig m_config;
        private readonly IRegionProfileRouter m_regionProfileService;

        protected BaseHttpServer m_httpServer;

        public UserLoginAuthService(
            UserManagerBase userManager, IInterServiceInventoryServices inventoryService,
            LibraryRootFolder libraryRootFolder,
            UserConfig config, string welcomeMess, IRegionProfileRouter regionProfileService)
            : base(userManager, welcomeMess, inventoryService, null, true, libraryRootFolder, null)
        {
            m_config = config;
            m_defaultHomeX = m_config.DefaultX;
            m_defaultHomeY = m_config.DefaultY;
            m_interInventoryService = inventoryService;
            m_regionProfileService = regionProfileService;

            NetworkServersInfo serversinfo = new NetworkServersInfo(1000, 1000);
            serversinfo.GridRecvKey = m_config.GridRecvKey;
            serversinfo.GridSendKey = m_config.GridSendKey;
            serversinfo.GridURL = m_config.GridServerURL.ToString();
            serversinfo.InventoryURL = m_config.InventoryUrl.ToString();
            serversinfo.UserURL = m_config.AuthUrl.ToString();
            SetServersInfo(serversinfo);
        }

        public void RegisterHandlers(BaseHttpServer httpServer)
        {
            m_httpServer = httpServer;

            httpServer.AddXmlRPCHandler("hg_login", XmlRpcLoginMethod);
            httpServer.AddXmlRPCHandler("hg_new_auth_key", XmlRpcGenerateKeyMethod);
            httpServer.AddXmlRPCHandler("hg_verify_auth_key", XmlRpcVerifyKeyMethod);
        }


        public override void LogOffUser(UserProfileData theUser, string message)
        {
            RegionProfileData SimInfo;
            try
            {
                SimInfo = m_regionProfileService.RequestSimProfileData(
                    theUser.CurrentAgent.Handle, m_config.GridServerURL,
                    m_config.GridSendKey, m_config.GridRecvKey);

                if (SimInfo == null)
                {
                    m_log.Error("[GRID]: Region user was in isn't currently logged in");
                    return;
                }
            }
            catch (Exception)
            {
                m_log.Error("[GRID]: Unable to look up region to log user off");
                return;
            }

            // Prepare notification
            Hashtable SimParams = new Hashtable();
            SimParams["agent_id"] = theUser.ID.ToString();
            SimParams["region_secret"] = theUser.CurrentAgent.SecureSessionID.ToString();
            SimParams["region_secret2"] = SimInfo.regionSecret;
            //m_log.Info(SimInfo.regionSecret);
            SimParams["regionhandle"] = theUser.CurrentAgent.Handle.ToString();
            SimParams["message"] = message;
            ArrayList SendParams = new ArrayList();
            SendParams.Add(SimParams);

            m_log.InfoFormat(
                "[ASSUMED CRASH]: Telling region {0} @ {1},{2} ({3}) that their agent is dead: {4}",
                SimInfo.regionName, SimInfo.regionLocX, SimInfo.regionLocY, SimInfo.httpServerURI,
                theUser.FirstName + " " + theUser.SurName);

            try
            {
                XmlRpcRequest GridReq = new XmlRpcRequest("logoff_user", SendParams);
                XmlRpcResponse GridResp = GridReq.Send(SimInfo.httpServerURI, 6000);

                if (GridResp.IsFault)
                {
                    m_log.ErrorFormat(
                        "[LOGIN]: XMLRPC request for {0} failed, fault code: {1}, reason: {2}, This is likely an old region revision.",
                        SimInfo.httpServerURI, GridResp.FaultCode, GridResp.FaultString);
                }
            }
            catch (Exception)
            {
                m_log.Error("[LOGIN]: Error telling region to logout user!");
            }

            // Prepare notification
            SimParams = new Hashtable();
            SimParams["agent_id"] = theUser.ID.ToString();
            SimParams["region_secret"] = SimInfo.regionSecret;
            //m_log.Info(SimInfo.regionSecret);
            SimParams["regionhandle"] = theUser.CurrentAgent.Handle.ToString();
            SimParams["message"] = message;
            SendParams = new ArrayList();
            SendParams.Add(SimParams);

            m_log.InfoFormat(
                "[ASSUMED CRASH]: Telling region {0} @ {1},{2} ({3}) that their agent is dead: {4}",
                SimInfo.regionName, SimInfo.regionLocX, SimInfo.regionLocY, SimInfo.httpServerURI,
                theUser.FirstName + " " + theUser.SurName);

            try
            {
                XmlRpcRequest GridReq = new XmlRpcRequest("logoff_user", SendParams);
                XmlRpcResponse GridResp = GridReq.Send(SimInfo.httpServerURI, 6000);

                if (GridResp.IsFault)
                {
                    m_log.ErrorFormat(
                        "[LOGIN]: XMLRPC request for {0} failed, fault code: {1}, reason: {2}, This is likely an old region revision.",
                        SimInfo.httpServerURI, GridResp.FaultCode, GridResp.FaultString);
                }
            }
            catch (Exception)
            {
                m_log.Error("[LOGIN]: Error telling region to logout user!");
            }
            //base.LogOffUser(theUser);
        }

        protected override RegionInfo RequestClosestRegion(string region)
        {
            RegionProfileData profileData = m_regionProfileService.RequestSimProfileData(region,
                                                                                         m_config.GridServerURL, m_config.GridSendKey, m_config.GridRecvKey);

            if (profileData != null)
            {
                return profileData.ToRegionInfo();
            }
            else
            {
                return null;
            }
        }

        protected override RegionInfo GetRegionInfo(ulong homeRegionHandle)
        {
            RegionProfileData profileData = m_regionProfileService.RequestSimProfileData(homeRegionHandle,
                                                                                         m_config.GridServerURL, m_config.GridSendKey,
                                                                                         m_config.GridRecvKey);
            if (profileData != null)
            {
                return profileData.ToRegionInfo();
            }
            else
            {
                return null;
            }
        }

        protected override RegionInfo GetRegionInfo(UUID homeRegionId)
        {
            RegionProfileData profileData = m_regionProfileService.RequestSimProfileData(homeRegionId,
                                                                                         m_config.GridServerURL, m_config.GridSendKey,
                                                                                         m_config.GridRecvKey);
            if (profileData != null)
            {
                return profileData.ToRegionInfo();
            }
            else
            {
                return null;
            }
        }

    }
}
