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
using System.Collections.Generic;
using System.Reflection;

using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Server.Base;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

using OpenMetaverse;
using log4net;

namespace OpenSim.Region.CoreModules.World.Estate
{
    public class EstateConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected EstateModule m_EstateModule;
        private string token;
        uint port = 0;

        public EstateConnector(EstateModule module, string _token, uint _port)
        {
            m_EstateModule = module;
            token = _token;
            port = _port;
        }

        public void SendTeleportHomeOneUser(uint EstateID, UUID PreyID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "teleport_home_one_user";
            sendData["TOKEN"] = token;

            sendData["EstateID"] = EstateID.ToString();
            sendData["PreyID"] = PreyID.ToString();

            SendToEstate(EstateID, sendData);
        }

        public void SendTeleportHomeAllUsers(uint EstateID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "teleport_home_all_users";
            sendData["TOKEN"] = token;

            sendData["EstateID"] = EstateID.ToString();

            SendToEstate(EstateID, sendData);
        }

        public bool SendUpdateCovenant(uint EstateID, UUID CovenantID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "update_covenant";
            sendData["TOKEN"] = token;

            sendData["CovenantID"] = CovenantID.ToString();
            sendData["EstateID"] = EstateID.ToString();

            // Handle local regions locally
            //
            foreach (Scene s in m_EstateModule.Scenes)
            {
                if (s.RegionInfo.EstateSettings.EstateID == EstateID)
                    s.RegionInfo.RegionSettings.Covenant = CovenantID;
//                    s.ReloadEstateData();
            }

            SendToEstate(EstateID, sendData);

            return true;
        }

        public bool SendUpdateEstate(uint EstateID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "update_estate";
            sendData["TOKEN"] = token;

            sendData["EstateID"] = EstateID.ToString();

            // Handle local regions locally
            //
            foreach (Scene s in m_EstateModule.Scenes)
            {
                if (s.RegionInfo.EstateSettings.EstateID == EstateID)
                    s.ReloadEstateData();
            }

            SendToEstate(EstateID, sendData);

            return true;
        }

        public void SendEstateMessage(uint EstateID, UUID FromID, string FromName, string Message)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["METHOD"] = "estate_message";
            sendData["TOKEN"] = token;

            sendData["EstateID"] = EstateID.ToString();
            sendData["FromID"] = FromID.ToString();
            sendData["FromName"] = FromName;
            sendData["Message"] = Message;

            SendToEstate(EstateID, sendData);
        }

        private void SendToEstate(uint EstateID, Dictionary<string, object> sendData)
        {
            List<UUID> regions = m_EstateModule.Scenes[0].GetEstateRegions((int)EstateID);

            // Don't send to the same instance twice
            List<string> done = new List<string>();

            // Handle local regions locally
            lock (m_EstateModule.Scenes)
            {
                foreach (Scene s in m_EstateModule.Scenes)
                {
                    RegionInfo sreg = s.RegionInfo;
                    if (regions.Contains(sreg.RegionID))
                    {
                        string url = sreg.ExternalHostName + ":" + sreg.HttpPort;
                        regions.Remove(sreg.RegionID);
                        if(!done.Contains(url)) // we may have older regs with same url lost in dbs
                            done.Add(url);
                    }
                }
            }

            if(regions.Count == 0)
                return;

            Scene baseScene = m_EstateModule.Scenes[0];
            UUID ScopeID = baseScene.RegionInfo.ScopeID;
            IGridService gridService = baseScene.GridService;
            if(gridService == null)
                return;

            // Send to remote regions
            foreach (UUID regionID in regions)
            {
                GridRegion region = gridService.GetRegionByUUID(ScopeID, regionID);
                if (region != null)
                {
                    string url = region.ExternalHostName + ":" + region.HttpPort;
                    if(done.Contains(url))
                        continue;
                    Call(region, sendData);
                    done.Add(url);
                }
            }
        }

        private bool Call(GridRegion region, Dictionary<string, object> sendData)
        {
            string reqString = ServerUtils.BuildQueryString(sendData);
            // m_log.DebugFormat("[XESTATE CONNECTOR]: queryString = {0}", reqString);
            try
            {
                string url = "";
                if(port != 0)
                    url = "http://" + region.ExternalHostName + ":" + port;
                else
                    url = region.ServerURI;

                string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        url + "/estate",
                        reqString);
                if (reply != string.Empty)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if (replyData.ContainsKey("RESULT"))
                    {
                        if (replyData["RESULT"].ToString().ToLower() == "true")
                            return true;
                        else
                            return false;
                    }
                    else
                        m_log.DebugFormat("[XESTATE CONNECTOR]: reply data does not contain result field");

                }
                else
                    m_log.DebugFormat("[XESTATE CONNECTOR]: received empty reply");
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[XESTATE CONNECTOR]: Exception when contacting remote sim: {0}", e.Message);
            }

            return false;
        }
    }
}
