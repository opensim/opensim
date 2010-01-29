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

        protected static IPresenceService m_PresenceService;
        protected static IGridService m_GridService;
        protected static GatekeeperServiceConnector m_GatekeeperConnector;

        public UserAgentService(IConfigSource config)
        {
            if (!m_Initialized)
            {
                m_log.DebugFormat("[HOME USERS SECURITY]: Starting...");
                
                IConfig serverConfig = config.Configs["UserAgentService"];
                if (serverConfig == null)
                    throw new Exception(String.Format("No section UserAgentService in config file"));

                string gridService = serverConfig.GetString("GridService", String.Empty);
                string presenceService = serverConfig.GetString("PresenceService", String.Empty);

                if (gridService == string.Empty || presenceService == string.Empty)
                    throw new Exception(String.Format("Incomplete specifications, UserAgent Service cannot function."));

                Object[] args = new Object[] { config };
                m_GridService = ServerUtils.LoadPlugin<IGridService>(gridService, args);
                m_PresenceService = ServerUtils.LoadPlugin<IPresenceService>(presenceService, args);
                m_GatekeeperConnector = new GatekeeperServiceConnector();

                m_Initialized = true;
            }
        }

        public GridRegion GetHomeRegion(UUID userID, out Vector3 position, out Vector3 lookAt)
        {
            position = new Vector3(128, 128, 0); lookAt = Vector3.UnitY;

            m_log.DebugFormat("[USER AGENT SERVICE]: Request to get home region of user {0}", userID);

            GridRegion home = null;
            PresenceInfo[] presences = m_PresenceService.GetAgents(new string[] { userID.ToString() });
            if (presences != null && presences.Length > 0)
            {
                UUID homeID = presences[0].HomeRegionID;
                if (homeID != UUID.Zero)
                {
                    home = m_GridService.GetRegionByUUID(UUID.Zero, homeID);
                    position = presences[0].HomePosition;
                    lookAt = presences[0].HomeLookAt;
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

        public bool LoginAgentToGrid(AgentCircuitData agentCircuit, GridRegion gatekeeper, GridRegion finalDestination, out string reason)
        {
            m_log.DebugFormat("[USER AGENT SERVICE]: Request to login user {0} {1} to grid {2}", 
                agentCircuit.firstname, agentCircuit.lastname, gatekeeper.ExternalHostName +":"+ gatekeeper.HttpPort);

            // Take the IP address + port of the gatekeeper (reg) plus the info of finalDestination
            GridRegion region = new GridRegion(gatekeeper);
            region.RegionName = finalDestination.RegionName;
            region.RegionID = finalDestination.RegionID;
            region.RegionLocX = finalDestination.RegionLocX;
            region.RegionLocY = finalDestination.RegionLocY;

            // Generate a new service session
            agentCircuit.ServiceSessionID = "http://" + region.ExternalHostName + ":" + region.HttpPort + ";" + UUID.Random();
            TravelingAgentInfo old = UpdateTravelInfo(agentCircuit, region);

            bool success = m_GatekeeperConnector.CreateAgent(region, agentCircuit, (uint)Constants.TeleportFlags.ViaLogin, out reason);

            if (!success)
            {
                m_log.DebugFormat("[USER AGENT SERVICE]: Unable to login user {0} {1} to grid {2}, reason: {3}", 
                    agentCircuit.firstname, agentCircuit.lastname, region.ExternalHostName + ":" + region.HttpPort, reason);

                // restore the old travel info
                lock (m_TravelingAgents)
                    m_TravelingAgents[agentCircuit.SessionID] = old;

                return false;
            }

            return true;
        }

        TravelingAgentInfo UpdateTravelInfo(AgentCircuitData agentCircuit, GridRegion region)
        {
            TravelingAgentInfo travel = new TravelingAgentInfo();
            TravelingAgentInfo old = null;
            lock (m_TravelingAgents)
            {
                if (m_TravelingAgents.ContainsKey(agentCircuit.SessionID))
                {
                    old = m_TravelingAgents[agentCircuit.SessionID];
                }

                m_TravelingAgents[agentCircuit.SessionID] = travel;
            }
            travel.UserID = agentCircuit.AgentID;
            travel.GridExternalName = region.ExternalHostName + ":" + region.HttpPort;
            travel.ServiceToken = agentCircuit.ServiceSessionID;
            if (old != null)
                travel.ClientToken = old.ClientToken;

            return old;
        }

        public void LogoutAgent(UUID userID, UUID sessionID)
        {
            m_log.DebugFormat("[USER AGENT SERVICE]: User {0} logged out", userID);

            lock (m_TravelingAgents)
            {
                List<UUID> travels = new List<UUID>();
                foreach (KeyValuePair<UUID, TravelingAgentInfo> kvp in m_TravelingAgents)
                    if (kvp.Value.UserID == userID)
                        travels.Add(kvp.Key);
                foreach (UUID session in travels)
                    m_TravelingAgents.Remove(session);
            }
        }

        // We need to prevent foreign users with the same UUID as a local user
        public bool AgentIsComingHome(UUID sessionID, string thisGridExternalName)
        {
            if (!m_TravelingAgents.ContainsKey(sessionID))
                return false;

            TravelingAgentInfo travel = m_TravelingAgents[sessionID];
            return travel.GridExternalName == thisGridExternalName;
        }

        public bool VerifyClient(UUID sessionID, string token)
        {
            if (m_TravelingAgents.ContainsKey(sessionID))
            {
                // Aquiles heel. Must trust the first grid upon login
                if (m_TravelingAgents[sessionID].ClientToken == string.Empty)
                {
                    m_TravelingAgents[sessionID].ClientToken = token;
                    return true;
                }
                return m_TravelingAgents[sessionID].ClientToken == token;
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
        public string ClientToken = string.Empty;
    }

}
