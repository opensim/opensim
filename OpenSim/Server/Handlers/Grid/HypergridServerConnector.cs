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
using System.Net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using log4net;
using Nwc.XmlRpc;

namespace OpenSim.Server.Handlers.Grid
{
    public class HypergridServiceInConnector : ServiceConnector
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private List<GridRegion> m_RegionsOnSim = new List<GridRegion>();
        private IHyperlinkService m_HyperlinkService;

        public HypergridServiceInConnector(IConfigSource config, IHttpServer server, IHyperlinkService hyperService) :
                base(config, server, String.Empty)
        {
            m_HyperlinkService = hyperService;
            server.AddXmlRPCHandler("link_region", LinkRegionRequest, false);
            server.AddXmlRPCHandler("expect_hg_user", ExpectHGUser, false);
        }

        public void AddRegion(GridRegion rinfo)
        {
            m_RegionsOnSim.Add(rinfo);
        }

        public void RemoveRegion(GridRegion rinfo)
        {
            if (m_RegionsOnSim.Contains(rinfo))
                m_RegionsOnSim.Remove(rinfo);
        }

        /// <summary>
        /// Someone wants to link to us
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse LinkRegionRequest(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            string name = (string)requestData["region_name"];

            m_log.DebugFormat("[HGrid]: Hyperlink request");

            GridRegion regInfo = null;
            foreach (GridRegion r in m_RegionsOnSim)
            {
                if ((r.RegionName != null) && (name != null) && (r.RegionName.ToLower() == name.ToLower()))
                {
                    regInfo = r;
                    break;
                }
            }

            if (regInfo == null)
                regInfo = m_RegionsOnSim[0]; // Send out the first region

            Hashtable hash = new Hashtable();
            hash["uuid"] = regInfo.RegionID.ToString();
            m_log.Debug(">> Here " + regInfo.RegionID);
            hash["handle"] = regInfo.RegionHandle.ToString();
            hash["region_image"] = regInfo.TerrainImage.ToString();
            hash["region_name"] = regInfo.RegionName;
            hash["internal_port"] = regInfo.InternalEndPoint.Port.ToString();
            //m_log.Debug(">> Here: " + regInfo.InternalEndPoint.Port);


            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;
        }

        /// <summary>
        /// Received from other HGrid nodes when a user wants to teleport here.  This call allows
        /// the region to prepare for direct communication from the client.  Sends back an empty
        /// xmlrpc response on completion.
        /// This is somewhat similar to OGS1's ExpectUser, but with the additional task of
        /// registering the user in the local user cache.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse ExpectHGUser(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            ForeignUserProfileData userData = new ForeignUserProfileData();

            userData.FirstName = (string)requestData["firstname"];
            userData.SurName = (string)requestData["lastname"];
            userData.ID = new UUID((string)requestData["agent_id"]);
            UUID sessionID = new UUID((string)requestData["session_id"]);
            userData.HomeLocation = new Vector3((float)Convert.ToDecimal((string)requestData["startpos_x"]),
                                  (float)Convert.ToDecimal((string)requestData["startpos_y"]),
                                  (float)Convert.ToDecimal((string)requestData["startpos_z"]));

            userData.UserServerURI = (string)requestData["userserver_id"];
            userData.UserAssetURI = (string)requestData["assetserver_id"];
            userData.UserInventoryURI = (string)requestData["inventoryserver_id"];

            m_log.DebugFormat("[HGrid]: Prepare for connection from {0} {1} (@{2}) UUID={3}",
                              userData.FirstName, userData.SurName, userData.UserServerURI, userData.ID);

            ulong userRegionHandle = 0;
            int userhomeinternalport = 0;
            if (requestData.ContainsKey("region_uuid"))
            {
                UUID uuid = UUID.Zero;
                UUID.TryParse((string)requestData["region_uuid"], out uuid);
                userData.HomeRegionID = uuid;
                userRegionHandle = Convert.ToUInt64((string)requestData["regionhandle"]);
                userData.UserHomeAddress = (string)requestData["home_address"];
                userData.UserHomePort = (string)requestData["home_port"];
                userhomeinternalport = Convert.ToInt32((string)requestData["internal_port"]);

                m_log.Debug("[HGrid]: home_address: " + userData.UserHomeAddress +
                           "; home_port: " + userData.UserHomePort);
            }
            else
                m_log.WarnFormat("[HGrid]: User has no home region information");

            XmlRpcResponse resp = new XmlRpcResponse();

            // Let's check if someone is trying to get in with a stolen local identity.
            // The need for this test is a consequence of not having truly global names :-/
            bool comingHome = false;
            if (m_HyperlinkService.CheckUserAtEntry(userData.ID, sessionID, out comingHome) == false)
            {
                m_log.WarnFormat("[HGrid]: Access denied to foreign user.");
                Hashtable respdata = new Hashtable();
                respdata["success"] = "FALSE";
                respdata["reason"] = "Foreign user has the same ID as a local user, or logins disabled.";
                resp.Value = respdata;
                return resp;
            }

            // Finally, everything looks ok
            //m_log.Debug("XXX---- EVERYTHING OK ---XXX");

            if (!comingHome)
            {
                // We don't do this if the user is coming to the home grid
                GridRegion home = new GridRegion();
                home.RegionID = userData.HomeRegionID;
                home.ExternalHostName = userData.UserHomeAddress;
                home.HttpPort = Convert.ToUInt32(userData.UserHomePort);
                uint x = 0, y = 0;
                Utils.LongToUInts(userRegionHandle, out x, out y);
                home.RegionLocX = (int)x;
                home.RegionLocY = (int)y;
                home.InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), (int)userhomeinternalport);

                m_HyperlinkService.AcceptUser(userData, home);
            }
            // else the user is coming to a non-home region of the home grid
            // We simply drop this user information altogether
 
            Hashtable respdata2 = new Hashtable();
            respdata2["success"] = "TRUE";
            resp.Value = respdata2;

            return resp;
        }

    }
}
