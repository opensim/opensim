using System;
using System.Collections.Generic;
using System.Reflection;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Server.Base;

using OpenMetaverse;

using Nini.Config;
using log4net;

namespace OpenSim.Services.HypergridService
{
    public class GatekeeperService : IGatekeeperService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        IGridService m_GridService;
        IPresenceService m_PresenceService;
        IAuthenticationService m_AuthenticationService;
        IUserAccountService m_UserAccountService;
        ISimulationService m_SimulationService;

        string m_AuthDll;

        UUID m_ScopeID;
        bool m_AllowTeleportsToAnyRegion;
        GridRegion m_DefaultGatewayRegion;

        public GatekeeperService(IConfigSource config, ISimulationService simService)
        {
            IConfig serverConfig = config.Configs["GatekeeperService"];
            if (serverConfig == null)
                throw new Exception(String.Format("No section GatekeeperService in config file"));

            string accountService = serverConfig.GetString("UserAccountService", String.Empty);
            string gridService = serverConfig.GetString("GridService", String.Empty);
            string presenceService = serverConfig.GetString("PresenceService", String.Empty);
            string simulationService = serverConfig.GetString("SimulationService", String.Empty);

            m_AuthDll = serverConfig.GetString("AuthenticationService", String.Empty);

            if (accountService == string.Empty || gridService == string.Empty ||
                presenceService == string.Empty || m_AuthDll == string.Empty)
                throw new Exception("Incomplete specifications, Gatekeeper Service cannot function.");
            
            string scope = serverConfig.GetString("ScopeID", UUID.Zero.ToString());
            UUID.TryParse(scope, out m_ScopeID);
            //m_WelcomeMessage = serverConfig.GetString("WelcomeMessage", "Welcome to OpenSim!");
            m_AllowTeleportsToAnyRegion = serverConfig.GetBoolean("AllowTeleportsToAnyRegion", true);

            Object[] args = new Object[] { config };
            m_UserAccountService = ServerUtils.LoadPlugin<IUserAccountService>(accountService, args);
            m_GridService = ServerUtils.LoadPlugin<IGridService>(gridService, args);
            m_PresenceService = ServerUtils.LoadPlugin<IPresenceService>(presenceService, args);
            if (simService != null)
                m_SimulationService = simService;
            else if (simulationService != string.Empty)
                    m_SimulationService = ServerUtils.LoadPlugin<ISimulationService>(simulationService, args);

            if (m_UserAccountService == null || m_GridService == null ||
                m_PresenceService == null || m_SimulationService == null)
                throw new Exception("Unable to load a required plugin, Gatekeeper Service cannot function.");

            m_log.Debug("[GATEKEEPER SERVICE]: Starting...");
        }

        public GatekeeperService(IConfigSource config)
            : this(config, null)
        {
        }

        public bool LinkRegion(string regionName, out UUID regionID, out ulong regionHandle, out string imageURL, out string reason)
        {
            regionID = UUID.Zero;
            regionHandle = 0;
            imageURL = string.Empty;
            reason = string.Empty;

            m_log.DebugFormat("[GATEKEEPER SERVICE]: Request to link to {0}", regionName);
            if (!m_AllowTeleportsToAnyRegion)
            {
                try
                {
                    regionID = m_DefaultGatewayRegion.RegionID;
                    regionHandle = m_DefaultGatewayRegion.RegionHandle;
                }
                catch
                {
                    reason = "Grid setup problem";
                    return false;
                }
                if (regionName != string.Empty)
                {
                    reason = "Direct links to regions not allowed";
                    return false;
                }

                return true;
            }

            GridRegion region = m_GridService.GetRegionByName(m_ScopeID, regionName);
            if (region == null)
            {
                reason = "Region not found";
                return false;
            }

            regionID = region.RegionID;
            regionHandle = region.RegionHandle;
            string regionimage = "regionImage" + region.RegionID.ToString();
            regionimage = regionimage.Replace("-", "");

            imageURL = "http://" + region.ExternalHostName + ":" + region.HttpPort + "/index.php?method=" + regionimage;

            return true;
        }

        public GridRegion GetHyperlinkRegion(UUID regionID)
        {
            if (!m_AllowTeleportsToAnyRegion)
                // Don't even check the given regionID
                return m_DefaultGatewayRegion;

            GridRegion region = m_GridService.GetRegionByUUID(m_ScopeID, regionID);
            return region;
        }

        public bool LoginAgent(AgentCircuitData aCircuit, GridRegion destination)
        {
            if (!Authenticate(aCircuit))
                return false;

            // Check to see if we have a local user with that UUID
            UserAccount account = m_UserAccountService.GetUserAccount(m_ScopeID, aCircuit.AgentID);
            if (account != null)
                // No, sorry; go away
                return false;

            // May want to authorize

            // Login the presence
            if (!m_PresenceService.LoginAgent(aCircuit.AgentID.ToString(), aCircuit.SessionID, aCircuit.SecureSessionID))
                return false;

            // Finally launch the agent at the destination
            string reason = string.Empty;
            return m_SimulationService.CreateAgent(destination, aCircuit, 0, out reason);
        }

        public bool LoginAttachments(ISceneObject sog, GridRegion destination)
        {
            // May want to filter attachments
            return m_SimulationService.CreateObject(destination, sog, false);
        }

        protected bool Authenticate(AgentCircuitData aCircuit)
        {
            string authURL = string.Empty; // GetAuthURL(aCircuit);
            if (authURL == string.Empty)
                return false;

            Object[] args = new Object[] { authURL };
            IAuthenticationService authService = ServerUtils.LoadPlugin<IAuthenticationService>(m_AuthDll, args);
            if (authService != null)
                return authService.Verify(aCircuit.AgentID, aCircuit.SecureSessionID.ToString(), 30);

            return false;
        }
    }
}
