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

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Connectors.Hypergrid;
using OpenSim.Services.Interfaces;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Region.CoreModules.Framework.EntityTransfer
{
    public class HGEntityTransferModule : EntityTransferModule, ISharedRegionModule, IEntityTransferModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IHypergridService m_HypergridService;
        private IHypergridService HyperGridService
        {
            get
            {
                if (m_HypergridService == null)
                    m_HypergridService = m_aScene.RequestModuleInterface<IHypergridService>();
                return m_HypergridService;
            }
        }

        private GatekeeperServiceConnector m_GatekeeperConnector;

        #region ISharedRegionModule

        public override string Name
        {
            get { return "HGEntityTransferModule"; }
        }

        public override void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("EntityTransferModule", "");
                if (name == Name)
                {
                    m_agentsInTransit = new List<UUID>();
                    m_GatekeeperConnector = new GatekeeperServiceConnector();
                    m_Enabled = true;
                    m_log.InfoFormat("[HG ENTITY TRANSFER MODULE]: {0} enabled.", Name);
                }
            }
        }


        #endregion

        #region HG overrides

        protected override GridRegion GetFinalDestination(GridRegion region)
        {
            return HyperGridService.GetHyperlinkRegion(region, region.RegionID);
        }

        protected override bool NeedsClosing(uint oldRegionX, uint newRegionX, uint oldRegionY, uint newRegionY, GridRegion reg)
        {
            return true;
        }

        protected override bool CreateAgent(GridRegion reg, GridRegion finalDestination, AgentCircuitData agentCircuit, uint teleportFlags, out string reason)
        {
            reason = string.Empty;
            if (reg.RegionLocX != finalDestination.RegionLocX && reg.RegionLocY != finalDestination.RegionLocY)
            {
                // this user is going to another grid
                reg.RegionName = finalDestination.RegionName;
                return m_GatekeeperConnector.CreateAgent(reg, agentCircuit, teleportFlags, out reason);
            }

            return m_aScene.SimulationService.CreateAgent(reg, agentCircuit, teleportFlags, out reason);
        }

        public override void TeleportHome(UUID id, IClientAPI client)
        {
            m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Request to teleport {0} {1} home", client.FirstName, client.LastName);

            // Let's find out if this is a foreign user or a local user
            UserAccount account = m_aScene.UserAccountService.GetUserAccount(m_aScene.RegionInfo.ScopeID, id);
            if (account != null)
            {
                // local grid user
                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: User is local");
                base.TeleportHome(id, client);
                return;
            }

            // Foreign user wants to go home
            // 
            AgentCircuitData aCircuit = ((Scene)(client.Scene)).AuthenticateHandler.GetAgentCircuitData(client.CircuitCode);
            if (aCircuit == null || (aCircuit != null && !aCircuit.ServiceURLs.ContainsKey("GatewayURI")))
            {
                client.SendTeleportFailed("Your information has been lost");
                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Unable to locate agent's gateway information");
                return;
            }

            GridRegion homeGatekeeper = MakeRegion(aCircuit);
            if (homeGatekeeper == null)
            {
                client.SendTeleportFailed("Your information has been lost");
                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Agent's gateway information is malformed");
                return;
            }

            Vector3 position = Vector3.UnitY, lookAt = Vector3.UnitY;
            GridRegion finalDestination = m_GatekeeperConnector.GetHomeRegion(homeGatekeeper, aCircuit.AgentID, out position, out lookAt);
        }
        #endregion

        private GridRegion MakeRegion(AgentCircuitData aCircuit)
        {
            GridRegion region = new GridRegion();

            Uri uri = null;
            if (!Uri.TryCreate(aCircuit.ServiceURLs["GatewayURI"].ToString(), UriKind.Absolute, out uri))
                return null;

            region.ExternalHostName = uri.Host;
            region.HttpPort = (uint)uri.Port;
            region.RegionName = string.Empty;
            return region;
        }
    }
}
