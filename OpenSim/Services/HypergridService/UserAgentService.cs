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
using System.Net;
using System.Reflection;

using OpenSim.Framework;
using OpenSim.Services.Connectors.Hypergrid;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Server.Base;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// This service is for HG1.5 only, to make up for the fact that clients don't
    /// keep any private information in themselves, and that their 'home service'
    /// needs to do it for them.
    /// Once we have better clients, this shouldn't be needed.
    /// </summary>
    public class UserAgentService : IUserAgentService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        // This will need to go into a DB table
        static Dictionary<UUID, TravelingAgentInfo> m_TravelingAgents = new Dictionary<UUID, TravelingAgentInfo>();

        static bool m_Initialized = false;

        protected static IGridUserService m_GridUserService;
        protected static IGridService m_GridService;
        protected static GatekeeperServiceConnector m_GatekeeperConnector;
        protected static IGatekeeperService m_GatekeeperService;

        protected static string m_GridName;

        protected static bool m_BypassClientVerification;

        public UserAgentService(IConfigSource config)
        {
            if (!m_Initialized)
            {
                m_Initialized = true;

                m_log.DebugFormat("[HOME USERS SECURITY]: Starting...");
                
                IConfig serverConfig = config.Configs["UserAgentService"];
                if (serverConfig == null)
                    throw new Exception(String.Format("No section UserAgentService in config file"));

                string gridService = serverConfig.GetString("GridService", String.Empty);
                string gridUserService = serverConfig.GetString("GridUserService", String.Empty);
                string gatekeeperService = serverConfig.GetString("GatekeeperService", String.Empty);

                m_BypassClientVerification = serverConfig.GetBoolean("BypassClientVerification", false);

                if (gridService == string.Empty || gridUserService == string.Empty || gatekeeperService == string.Empty)
                    throw new Exception(String.Format("Incomplete specifications, UserAgent Service cannot function."));

                Object[] args = new Object[] { config };
                m_GridService = ServerUtils.LoadPlugin<IGridService>(gridService, args);
                m_GridUserService = ServerUtils.LoadPlugin<IGridUserService>(gridUserService, args);
                m_GatekeeperConnector = new GatekeeperServiceConnector();
                m_GatekeeperService = ServerUtils.LoadPlugin<IGatekeeperService>(gatekeeperService, args);

                m_GridName = serverConfig.GetString("ExternalName", string.Empty);
                if (m_GridName == string.Empty)
                {
                    serverConfig = config.Configs["GatekeeperService"];
                    m_GridName = serverConfig.GetString("ExternalName", string.Empty);
                }
                if (!m_GridName.EndsWith("/"))
                    m_GridName = m_GridName + "/";
            }
        }

        public GridRegion GetHomeRegion(UUID userID, out Vector3 position, out Vector3 lookAt)
        {
            position = new Vector3(128, 128, 0); lookAt = Vector3.UnitY;

            m_log.DebugFormat("[USER AGENT SERVICE]: Request to get home region of user {0}", userID);

            GridRegion home = null;
            GridUserInfo uinfo = m_GridUserService.GetGridUserInfo(userID.ToString());
            if (uinfo != null)
            {
                if (uinfo.HomeRegionID != UUID.Zero)
                {
                    home = m_GridService.GetRegionByUUID(UUID.Zero, uinfo.HomeRegionID);
                    position = uinfo.HomePosition;
                    lookAt = uinfo.HomeLookAt;
                }
                if (home == null)
                {
                    List<GridRegion> defs = m_GridService.GetDefaultRegions(UUID.Zero);
                    if (defs != null && defs.Count > 0)
                        home = defs[0];
                }
            }

            return home;
        }

        public bool LoginAgentToGrid(AgentCircuitData agentCircuit, GridRegion gatekeeper, GridRegion finalDestination, IPEndPoint clientIP, out string reason)
        {
            m_log.DebugFormat("[USER AGENT SERVICE]: Request to login user {0} {1} (@{2}) to grid {3}", 
                agentCircuit.firstname, agentCircuit.lastname, ((clientIP == null) ? "stored IP" : clientIP.Address.ToString()), gatekeeper.ServerURI);
            // Take the IP address + port of the gatekeeper (reg) plus the info of finalDestination
            GridRegion region = new GridRegion(gatekeeper);
            region.ServerURI = gatekeeper.ServerURI;
            region.ExternalHostName = finalDestination.ExternalHostName;
            region.InternalEndPoint = finalDestination.InternalEndPoint;
            region.RegionName = finalDestination.RegionName;
            region.RegionID = finalDestination.RegionID;
            region.RegionLocX = finalDestination.RegionLocX;
            region.RegionLocY = finalDestination.RegionLocY;

            // Generate a new service session
            agentCircuit.ServiceSessionID = region.ServerURI + ";" + UUID.Random();
            TravelingAgentInfo old = UpdateTravelInfo(agentCircuit, region);
            
            bool success = false;
            string myExternalIP = string.Empty;
            string gridName = gatekeeper.ServerURI;

            m_log.DebugFormat("[USER AGENT SERVICE]: m_grid - {0}, gn - {1}", m_GridName, gridName);
            
            if (m_GridName == gridName)
                success = m_GatekeeperService.LoginAgent(agentCircuit, finalDestination, out reason);
            else
                success = m_GatekeeperConnector.CreateAgent(region, agentCircuit, (uint)Constants.TeleportFlags.ViaLogin, out myExternalIP, out reason);

            if (!success)
            {
                m_log.DebugFormat("[USER AGENT SERVICE]: Unable to login user {0} {1} to grid {2}, reason: {3}", 
                    agentCircuit.firstname, agentCircuit.lastname, region.ServerURI, reason);

                // restore the old travel info
                lock (m_TravelingAgents)
                    m_TravelingAgents[agentCircuit.SessionID] = old;

                return false;
            }

            m_log.DebugFormat("[USER AGENT SERVICE]: Gatekeeper sees me as {0}", myExternalIP);
            // else set the IP addresses associated with this client
            if (clientIP != null)
                m_TravelingAgents[agentCircuit.SessionID].ClientIPAddress = clientIP.Address.ToString();
            m_TravelingAgents[agentCircuit.SessionID].MyIpAddress = myExternalIP;
            return true;
        }

        public bool LoginAgentToGrid(AgentCircuitData agentCircuit, GridRegion gatekeeper, GridRegion finalDestination, out string reason)
        {
            reason = string.Empty;
            return LoginAgentToGrid(agentCircuit, gatekeeper, finalDestination, null, out reason);
        }

        private void SetClientIP(UUID sessionID, string ip)
        {
            if (m_TravelingAgents.ContainsKey(sessionID))
            {
                m_log.DebugFormat("[USER AGENT SERVICE]: Setting IP {0} for session {1}", ip, sessionID);
                m_TravelingAgents[sessionID].ClientIPAddress = ip;
            }
        }

        TravelingAgentInfo UpdateTravelInfo(AgentCircuitData agentCircuit, GridRegion region)
        {
            TravelingAgentInfo travel = new TravelingAgentInfo();
            TravelingAgentInfo old = null;
            lock (m_TravelingAgents)
            {
                if (m_TravelingAgents.ContainsKey(agentCircuit.SessionID))
                {
                    // Very important! Override whatever this agent comes with.
                    // UserAgentService always sets the IP for every new agent
                    // with the original IP address.
                    agentCircuit.IPAddress = m_TravelingAgents[agentCircuit.SessionID].ClientIPAddress;

                    old = m_TravelingAgents[agentCircuit.SessionID];
                }

                m_TravelingAgents[agentCircuit.SessionID] = travel;
            }
            travel.UserID = agentCircuit.AgentID;
            travel.GridExternalName = region.ServerURI;
            travel.ServiceToken = agentCircuit.ServiceSessionID;
            if (old != null)
                travel.ClientIPAddress = old.ClientIPAddress;

            return old;
        }

        public void LogoutAgent(UUID userID, UUID sessionID)
        {
            m_log.DebugFormat("[USER AGENT SERVICE]: User {0} logged out", userID);

            lock (m_TravelingAgents)
            {
                List<UUID> travels = new List<UUID>();
                foreach (KeyValuePair<UUID, TravelingAgentInfo> kvp in m_TravelingAgents)
                    if (kvp.Value == null) // do some clean up
                        travels.Add(kvp.Key);
                    else if (kvp.Value.UserID == userID)
                        travels.Add(kvp.Key);
                foreach (UUID session in travels)
                    m_TravelingAgents.Remove(session);
            }

            GridUserInfo guinfo = m_GridUserService.GetGridUserInfo(userID.ToString());
            if (guinfo != null)
                m_GridUserService.LoggedOut(userID.ToString(), sessionID, guinfo.LastRegionID, guinfo.LastPosition, guinfo.LastLookAt);
        }

        // We need to prevent foreign users with the same UUID as a local user
        public bool AgentIsComingHome(UUID sessionID, string thisGridExternalName)
        {
            if (!m_TravelingAgents.ContainsKey(sessionID))
                return false;

            TravelingAgentInfo travel = m_TravelingAgents[sessionID];

            return travel.GridExternalName == thisGridExternalName;
        }

        public bool VerifyClient(UUID sessionID, string reportedIP)
        {
            if (m_BypassClientVerification)
                return true;

            m_log.DebugFormat("[USER AGENT SERVICE]: Verifying Client session {0} with reported IP {1}.", 
                sessionID, reportedIP);

            if (m_TravelingAgents.ContainsKey(sessionID))
            {
                m_log.DebugFormat("[USER AGENT SERVICE]: Comparing with login IP {0} and MyIP {1}", 
                    m_TravelingAgents[sessionID].ClientIPAddress, m_TravelingAgents[sessionID].MyIpAddress);

                return m_TravelingAgents[sessionID].ClientIPAddress == reportedIP ||
                    m_TravelingAgents[sessionID].MyIpAddress == reportedIP; // NATed
            }

            return false;
        }

        public bool VerifyAgent(UUID sessionID, string token)
        {
            if (m_TravelingAgents.ContainsKey(sessionID))
            {
                m_log.DebugFormat("[USER AGENT SERVICE]: Verifying agent token {0} against {1}", token, m_TravelingAgents[sessionID].ServiceToken);
                return m_TravelingAgents[sessionID].ServiceToken == token;
            }

            m_log.DebugFormat("[USER AGENT SERVICE]: Token verification for session {0}: no such session", sessionID);

            return false;
        }

    }

    class TravelingAgentInfo
    {
        public UUID UserID;
        public string GridExternalName = string.Empty;
        public string ServiceToken = string.Empty;
        public string ClientIPAddress = string.Empty; // as seen from this user agent service
        public string MyIpAddress = string.Empty; // the user agent service's external IP, as seen from the next gatekeeper
    }

}
