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
using OpenSim.Server.Base;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Region.CoreModules.Framework.EntityTransfer
{
    public class HGEntityTransferModule : EntityTransferModule, ISharedRegionModule, IEntityTransferModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Initialized = false;

        private GatekeeperServiceConnector m_GatekeeperConnector;
        private IHomeUsersSecurityService m_Security;

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

                    IConfig config = source.Configs["HGEntityTransferModule"];
                    if (config != null)
                    {
                        string dll = config.GetString("HomeUsersSecurityService", string.Empty);
                        if (dll != string.Empty)
                        {
                            Object[] args = new Object[] { source }; 
                            m_Security = ServerUtils.LoadPlugin<IHomeUsersSecurityService>(dll, args);
                            if (m_Security == null)
                                m_log.Debug("[HG ENTITY TRANSFER MODULE]: Unable to load Home Users Security service");
                            else
                                m_log.Debug("[HG ENTITY TRANSFER MODULE]: Home Users Security service loaded");
                        }
                    }
                    
                    m_Enabled = true;
                    m_log.InfoFormat("[HG ENTITY TRANSFER MODULE]: {0} enabled.", Name);
                }
            }
        }

        public override void AddRegion(Scene scene)
        {
            base.AddRegion(scene);
            if (m_Enabled)
                scene.RegisterModuleInterface<IHomeUsersSecurityService>(m_Security);
        }

        public override void RegionLoaded(Scene scene)
        {
            base.RegionLoaded(scene);
            if (m_Enabled)
                if (!m_Initialized)
                {
                    m_GatekeeperConnector = new GatekeeperServiceConnector(scene.AssetService);
                    m_Initialized = true;
                }

        }
        public override void RemoveRegion(Scene scene)
        {
            base.AddRegion(scene);
            if (m_Enabled)
                scene.UnregisterModuleInterface<IHomeUsersSecurityService>(m_Security);
        }


        #endregion

        #region HG overrides

        protected override GridRegion GetFinalDestination(GridRegion region)
        {
            int flags = m_aScene.GridService.GetRegionFlags(m_aScene.RegionInfo.ScopeID, region.RegionID);
            //m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: region {0} flags: {1}", region.RegionID, flags);
            if ((flags & (int)OpenSim.Data.RegionFlags.Hyperlink) != 0)
            {
                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Destination region {0} is hyperlink", region.RegionID);
                return m_GatekeeperConnector.GetHyperlinkRegion(region, region.RegionID);
            }
            return region;
        }

        protected override bool NeedsClosing(uint oldRegionX, uint newRegionX, uint oldRegionY, uint newRegionY, GridRegion reg)
        {
            return true;
        }

        protected override bool CreateAgent(ScenePresence sp, GridRegion reg, GridRegion finalDestination, AgentCircuitData agentCircuit, uint teleportFlags, out string reason)
        {
            reason = string.Empty;
            int flags = m_aScene.GridService.GetRegionFlags(m_aScene.RegionInfo.ScopeID, reg.RegionID);
            if ((flags & (int)OpenSim.Data.RegionFlags.Hyperlink) != 0)
            {
                // this user is going to another grid
                // Take the IP address + port of the gatekeeper (reg) plus the info of finalDestination
                GridRegion region = new GridRegion(reg);
                region.RegionName = finalDestination.RegionName;
                region.RegionID = finalDestination.RegionID;
                region.RegionLocX = finalDestination.RegionLocX;
                region.RegionLocY = finalDestination.RegionLocY;
                
                // Log their session and remote endpoint in the home users security service
                IHomeUsersSecurityService security = sp.Scene.RequestModuleInterface<IHomeUsersSecurityService>();
                if (security != null)
                    security.SetEndPoint(sp.ControllingClient.SessionId, sp.ControllingClient.RemoteEndPoint);

                //string token = sp.Scene.AuthenticationService.MakeToken(sp.UUID, reg.ExternalHostName + ":" + reg.HttpPort, 30);
                // Log them out of this grid
                sp.Scene.PresenceService.LogoutAgent(agentCircuit.SessionID, sp.AbsolutePosition, sp.Lookat);

                return m_GatekeeperConnector.CreateAgent(region, agentCircuit, teleportFlags, out reason);
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
            if (finalDestination == null)
            {
                client.SendTeleportFailed("Your home region could not be found");
                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Agent's home region not found");
                return;
            }

            ScenePresence sp = ((Scene)(client.Scene)).GetScenePresence(client.AgentId);
            if (sp == null)
            {
                client.SendTeleportFailed("Internal error");
                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Agent not found in the scene where it is supposed to be");
                return;
            }

            m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: teleporting user {0} {1} home to {2} via {3}:{4}:{5}", 
                aCircuit.firstname, aCircuit.lastname, finalDestination.RegionName, homeGatekeeper.ExternalHostName, homeGatekeeper.HttpPort, homeGatekeeper.RegionName);

            IEventQueue eq = sp.Scene.RequestModuleInterface<IEventQueue>();
            DoTeleport(sp, homeGatekeeper, finalDestination, position, lookAt, (uint)(Constants.TeleportFlags.SetLastToTarget | Constants.TeleportFlags.ViaHome), eq);
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
            region.InternalEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("0.0.0.0"), (int)0);
            return region;
        }
    }
}
