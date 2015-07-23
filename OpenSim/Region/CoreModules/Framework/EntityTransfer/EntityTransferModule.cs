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
using System.Threading;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Client;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;
using OpenSim.Services.Interfaces;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using log4net;
using Nini.Config;
using Mono.Addins;

namespace OpenSim.Region.CoreModules.Framework.EntityTransfer
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "EntityTransferModule")]
    public class EntityTransferModule : INonSharedRegionModule, IEntityTransferModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[ENTITY TRANSFER MODULE]";

        public const int DefaultMaxTransferDistance = 4095;
        public const bool WaitForAgentArrivedAtDestinationDefault = true;

        public string OutgoingTransferVersionName { get; set; }

        /// <summary>
        /// Determine the maximum entity transfer version we will use for teleports.
        /// </summary>
        public float MaxOutgoingTransferVersion { get; set; }

        /// <summary>
        /// The maximum distance, in standard region units (256m) that an agent is allowed to transfer.
        /// </summary>
        public int MaxTransferDistance { get; set; }

        /// <summary>
        /// If true then on a teleport, the source region waits for a callback from the destination region.  If
        /// a callback fails to arrive within a set time then the user is pulled back into the source region.
        /// </summary>
        public bool WaitForAgentArrivedAtDestination { get; set; }

        /// <summary>
        /// If true then we ask the viewer to disable teleport cancellation and ignore teleport requests.
        /// </summary>
        /// <remarks>
        /// This is useful in situations where teleport is very likely to always succeed and we want to avoid a 
        /// situation where avatars can be come 'stuck' due to a failed teleport cancellation.  Unfortunately, the
        /// nature of the teleport protocol makes it extremely difficult (maybe impossible) to make teleport 
        /// cancellation consistently suceed.
        /// </remarks>
        public bool DisableInterRegionTeleportCancellation { get; set; }

        /// <summary>
        /// Number of times inter-region teleport was attempted.
        /// </summary>
        private Stat m_interRegionTeleportAttempts;

        /// <summary>
        /// Number of times inter-region teleport was aborted (due to simultaneous client logout).
        /// </summary>
        private Stat m_interRegionTeleportAborts;

        /// <summary>
        /// Number of times inter-region teleport was successfully cancelled by the client.
        /// </summary>
        private Stat m_interRegionTeleportCancels;

        /// <summary>
        /// Number of times inter-region teleport failed due to server/client/network problems (e.g. viewer failed to
        /// connect with destination region).
        /// </summary>
        /// <remarks>
        /// This is not necessarily a problem for this simulator - in open-grid/hg conditions, viewer connectivity to
        /// destination simulator is unknown.
        /// </remarks>
        private Stat m_interRegionTeleportFailures;

        protected string m_ThisHomeURI;
        protected string m_GatekeeperURI;

        protected bool m_Enabled = false;

        public Scene Scene { get; private set; }

        /// <summary>
        /// Handles recording and manipulation of state for entities that are in transfer within or between regions
        /// (cross or teleport).
        /// </summary>
        private EntityTransferStateMachine m_entityTransferStateMachine;

        // For performance, we keed a cached of banned regions so we don't keep going
        //    to the grid service.
        private class BannedRegionCache
        {
            private ExpiringCache<UUID, ExpiringCache<ulong, DateTime>> m_bannedRegions =
                    new ExpiringCache<UUID, ExpiringCache<ulong, DateTime>>();
            ExpiringCache<ulong, DateTime> m_idCache;
            DateTime m_banUntil;
            public BannedRegionCache()
            {
            }
            // Return 'true' if there is a valid ban entry for this agent in this region
            public bool IfBanned(ulong pRegionHandle, UUID pAgentID)
            {
                bool ret = false;
                if (m_bannedRegions.TryGetValue(pAgentID, out m_idCache))
                {
                    if (m_idCache.TryGetValue(pRegionHandle, out m_banUntil))
                    {
                        if (DateTime.Now < m_banUntil)
                        {
                            ret = true;
                        }
                    }
                }
                return ret;
            }
            // Add this agent in this region as a banned person
            public void Add(ulong pRegionHandle, UUID pAgentID)
            {
                if (!m_bannedRegions.TryGetValue(pAgentID, out m_idCache))
                {
                    m_idCache = new ExpiringCache<ulong, DateTime>();
                    m_bannedRegions.Add(pAgentID, m_idCache, TimeSpan.FromSeconds(45));
                }
                m_idCache.Add(pRegionHandle, DateTime.Now + TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
            }
            // Remove the agent from the region's banned list
            public void Remove(ulong pRegionHandle, UUID pAgentID)
            {
                if (m_bannedRegions.TryGetValue(pAgentID, out m_idCache))
                {
                    m_idCache.Remove(pRegionHandle);
                }
            }
        }
        private BannedRegionCache m_bannedRegionCache = new BannedRegionCache();

        private IEventQueue m_eqModule;
        private IRegionCombinerModule m_regionCombinerModule;

        #region ISharedRegionModule

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public virtual string Name
        {
            get { return "BasicEntityTransferModule"; }
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("EntityTransferModule", "");
                if (name == Name)
                {
                    InitialiseCommon(source);
                    m_log.DebugFormat("[ENTITY TRANSFER MODULE]: {0} enabled.", Name);
                }
            }
        }

        /// <summary>
        /// Initialize config common for this module and any descendents.
        /// </summary>
        /// <param name="source"></param>
        protected virtual void InitialiseCommon(IConfigSource source)
        {
            string transferVersionName = "SIMULATION";
            float maxTransferVersion = 0.3f;

            IConfig hypergridConfig = source.Configs["Hypergrid"];
            if (hypergridConfig != null)
            {
                m_ThisHomeURI = hypergridConfig.GetString("HomeURI", string.Empty);
                if (m_ThisHomeURI != string.Empty && !m_ThisHomeURI.EndsWith("/"))
                    m_ThisHomeURI += '/';

                m_GatekeeperURI = hypergridConfig.GetString("GatekeeperURI", string.Empty);
                if (m_GatekeeperURI != string.Empty && !m_GatekeeperURI.EndsWith("/"))
                    m_GatekeeperURI += '/';
            }

            IConfig transferConfig = source.Configs["EntityTransfer"];
            if (transferConfig != null)
            {
                string rawVersion 
                    = transferConfig.GetString(
                        "MaxOutgoingTransferVersion", 
                        string.Format("{0}/{1}", transferVersionName, maxTransferVersion));

                string[] rawVersionComponents = rawVersion.Split(new char[] { '/' });

                bool versionValid = false;

                if (rawVersionComponents.Length >= 2)
                    versionValid = float.TryParse(rawVersionComponents[1], out maxTransferVersion);

                if (!versionValid)
                {
                    m_log.ErrorFormat(
                        "[ENTITY TRANSFER MODULE]: MaxOutgoingTransferVersion {0} is invalid, using {1}", 
                        rawVersion, string.Format("{0}/{1}", transferVersionName, maxTransferVersion));
                }
                else
                {
                    transferVersionName = rawVersionComponents[0];

                    m_log.InfoFormat(
                        "[ENTITY TRANSFER MODULE]: MaxOutgoingTransferVersion set to {0}", 
                        string.Format("{0}/{1}", transferVersionName, maxTransferVersion));
                }

                DisableInterRegionTeleportCancellation 
                    = transferConfig.GetBoolean("DisableInterRegionTeleportCancellation", false);

                WaitForAgentArrivedAtDestination
                    = transferConfig.GetBoolean("wait_for_callback", WaitForAgentArrivedAtDestinationDefault);
                
                MaxTransferDistance = transferConfig.GetInt("max_distance", DefaultMaxTransferDistance);
            }
            else
            {
                MaxTransferDistance = DefaultMaxTransferDistance;
            }

            OutgoingTransferVersionName = transferVersionName;
            MaxOutgoingTransferVersion = maxTransferVersion;

            m_entityTransferStateMachine = new EntityTransferStateMachine(this);

            m_Enabled = true;
        }

        public virtual void PostInitialise()
        {
        }

        public virtual void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            Scene = scene;

            m_interRegionTeleportAttempts = 
                new Stat(
                    "InterRegionTeleportAttempts",
                    "Number of inter-region teleports attempted.",
                    "This does not count attempts which failed due to pre-conditions (e.g. target simulator refused access).\n"
                        + "You can get successfully teleports by subtracting aborts, cancels and teleport failures from this figure.",
                    "",
                    "entitytransfer",
                    Scene.Name,
                    StatType.Push,
                    null,
                    StatVerbosity.Debug);

            m_interRegionTeleportAborts = 
                new Stat(
                    "InterRegionTeleportAborts",
                    "Number of inter-region teleports aborted due to client actions.",
                    "The chief action is simultaneous logout whilst teleporting.",
                    "",
                    "entitytransfer",
                    Scene.Name,
                    StatType.Push,
                    null,
                    StatVerbosity.Debug);

            m_interRegionTeleportCancels = 
                new Stat(
                    "InterRegionTeleportCancels",
                    "Number of inter-region teleports cancelled by the client.",
                    null,
                    "",
                    "entitytransfer",
                    Scene.Name,
                    StatType.Push,
                    null,
                    StatVerbosity.Debug);

            m_interRegionTeleportFailures = 
                new Stat(
                    "InterRegionTeleportFailures",
                    "Number of inter-region teleports that failed due to server/client/network issues.",
                    "This number may not be very helpful in open-grid/hg situations as the network connectivity/quality of destinations is uncontrollable.",
                    "",
                    "entitytransfer",
                    Scene.Name,
                    StatType.Push,
                    null,
                    StatVerbosity.Debug);

            StatsManager.RegisterStat(m_interRegionTeleportAttempts);
            StatsManager.RegisterStat(m_interRegionTeleportAborts);
            StatsManager.RegisterStat(m_interRegionTeleportCancels);
            StatsManager.RegisterStat(m_interRegionTeleportFailures);

            scene.RegisterModuleInterface<IEntityTransferModule>(this);
            scene.EventManager.OnNewClient += OnNewClient;
        }

        protected virtual void OnNewClient(IClientAPI client)
        {
            client.OnTeleportHomeRequest += TriggerTeleportHome;
            client.OnTeleportLandmarkRequest += RequestTeleportLandmark;

            if (!DisableInterRegionTeleportCancellation)
                client.OnTeleportCancel += OnClientCancelTeleport;

            client.OnConnectionClosed += OnConnectionClosed;
        }

        public virtual void Close() {}

        public virtual void RemoveRegion(Scene scene) 
        {
            if (m_Enabled)
            {
                StatsManager.DeregisterStat(m_interRegionTeleportAttempts);
                StatsManager.DeregisterStat(m_interRegionTeleportAborts);
                StatsManager.DeregisterStat(m_interRegionTeleportCancels);
                StatsManager.DeregisterStat(m_interRegionTeleportFailures);
            }
        }

        public virtual void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_eqModule = Scene.RequestModuleInterface<IEventQueue>();
            m_regionCombinerModule = Scene.RequestModuleInterface<IRegionCombinerModule>();
        }

        #endregion

        #region Agent Teleports

        private void OnConnectionClosed(IClientAPI client)
        {
            if (client.IsLoggingOut && m_entityTransferStateMachine.UpdateInTransit(client.AgentId, AgentTransferState.Aborting))
            {
                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Aborted teleport request from {0} in {1} due to simultaneous logout", 
                    client.Name, Scene.Name);
            }
        }

        private void OnClientCancelTeleport(IClientAPI client)
        {
            m_entityTransferStateMachine.UpdateInTransit(client.AgentId, AgentTransferState.Cancelling);

            m_log.DebugFormat(
                "[ENTITY TRANSFER MODULE]: Received teleport cancel request from {0} in {1}", client.Name, Scene.Name);
        }

        // Attempt to teleport the ScenePresence to the specified position in the specified region (spec'ed by its handle).
        public void Teleport(ScenePresence sp, ulong regionHandle, Vector3 position, Vector3 lookAt, uint teleportFlags)
        {
            if (sp.Scene.Permissions.IsGridGod(sp.UUID))
            {
                // This user will be a God in the destination scene, too
                teleportFlags |= (uint)TeleportFlags.Godlike;
            }

            if (!sp.Scene.Permissions.CanTeleport(sp.UUID))
                return;

            string destinationRegionName = "(not found)";

            // Record that this agent is in transit so that we can prevent simultaneous requests and do later detection
            // of whether the destination region completes the teleport.
            if (!m_entityTransferStateMachine.SetInTransit(sp.UUID))
            {
                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Ignoring teleport request of {0} {1} to {2}@{3} - agent is already in transit.",
                    sp.Name, sp.UUID, position, regionHandle);

                sp.ControllingClient.SendTeleportFailed("Previous teleport process incomplete.  Please retry shortly.");

                return;
            }

            try
            {
                // Reset animations; the viewer does that in teleports.
                sp.Animator.ResetAnimations();

                if (regionHandle == sp.Scene.RegionInfo.RegionHandle)
                {
                    destinationRegionName = sp.Scene.RegionInfo.RegionName;

                    TeleportAgentWithinRegion(sp, position, lookAt, teleportFlags);
                }
                else // Another region possibly in another simulator
                {
                    GridRegion finalDestination = null;
                    try
                    {
                        TeleportAgentToDifferentRegion(
                            sp, regionHandle, position, lookAt, teleportFlags, out finalDestination);
                    }
                    finally
                    {
                        if (finalDestination != null)
                            destinationRegionName = finalDestination.RegionName;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[ENTITY TRANSFER MODULE]: Exception on teleport of {0} from {1}@{2} to {3}@{4}: {5}{6}",
                    sp.Name, sp.AbsolutePosition, sp.Scene.RegionInfo.RegionName, position, destinationRegionName,
                    e.Message, e.StackTrace);

                sp.ControllingClient.SendTeleportFailed("Internal error");
            }
            finally
            {
                m_entityTransferStateMachine.ResetFromTransit(sp.UUID);
            }
        }

        /// <summary>
        /// Teleports the agent within its current region.
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <param name="teleportFlags"></param>
        private void TeleportAgentWithinRegion(ScenePresence sp, Vector3 position, Vector3 lookAt, uint teleportFlags)
        {
            m_log.DebugFormat(
                "[ENTITY TRANSFER MODULE]: Teleport for {0} to {1} within {2}",
                sp.Name, position, sp.Scene.RegionInfo.RegionName);

            // Teleport within the same region
            if (!sp.Scene.PositionIsInCurrentRegion(position) || position.Z < 0)
            {
                Vector3 emergencyPos = new Vector3(128, 128, 128);

                m_log.WarnFormat(
                    "[ENTITY TRANSFER MODULE]: RequestTeleportToLocation() was given an illegal position of {0} for avatar {1}, {2} in {3}.  Substituting {4}",
                    position, sp.Name, sp.UUID, Scene.Name, emergencyPos);

                position = emergencyPos;
            }

            // TODO: Get proper AVG Height
            float localAVHeight = 1.56f;
            float posZLimit = 22;

            // TODO: Check other Scene HeightField
            posZLimit = (float)sp.Scene.Heightmap[(int)position.X, (int)position.Y];

            float newPosZ = posZLimit + localAVHeight;
            if (posZLimit >= (position.Z - (localAVHeight / 2)) && !(Single.IsInfinity(newPosZ) || Single.IsNaN(newPosZ)))
            {
                position.Z = newPosZ;
            }

            if (sp.Flying)
                teleportFlags |= (uint)TeleportFlags.IsFlying;

            m_entityTransferStateMachine.UpdateInTransit(sp.UUID, AgentTransferState.Transferring);

            sp.ControllingClient.SendTeleportStart(teleportFlags);

            sp.ControllingClient.SendLocalTeleport(position, lookAt, teleportFlags);
            sp.TeleportFlags = (Constants.TeleportFlags)teleportFlags;
            sp.Velocity = Vector3.Zero;
            sp.Teleport(position);

            m_entityTransferStateMachine.UpdateInTransit(sp.UUID, AgentTransferState.ReceivedAtDestination);

            foreach (SceneObjectGroup grp in sp.GetAttachments())
            {
                sp.Scene.EventManager.TriggerOnScriptChangedEvent(grp.LocalId, (uint)Changed.TELEPORT);
            }

            m_entityTransferStateMachine.UpdateInTransit(sp.UUID, AgentTransferState.CleaningUp);
        }

        /// <summary>
        /// Teleports the agent to a different region.
        /// </summary>
        /// <param name='sp'></param>
        /// <param name='regionHandle'>/param>
        /// <param name='position'></param>
        /// <param name='lookAt'></param>
        /// <param name='teleportFlags'></param>
        /// <param name='finalDestination'></param>
        private void TeleportAgentToDifferentRegion(
            ScenePresence sp, ulong regionHandle, Vector3 position,
            Vector3 lookAt, uint teleportFlags, out GridRegion finalDestination)
        {
            // Get destination region taking into account that the address could be an offset
            //     region inside a varregion.
            GridRegion reg = GetTeleportDestinationRegion(sp.Scene.GridService, sp.Scene.RegionInfo.ScopeID, regionHandle, ref position);

            if (reg != null)
            {
                string homeURI = Scene.GetAgentHomeURI(sp.ControllingClient.AgentId);

                string message;
                finalDestination = GetFinalDestination(reg, sp.ControllingClient.AgentId, homeURI, out message);

                if (finalDestination == null)
                {
                    m_log.WarnFormat( "{0} Final destination is having problems. Unable to teleport {1} {2}: {3}",
                                            LogHeader, sp.Name, sp.UUID, message);

                    sp.ControllingClient.SendTeleportFailed(message);
                    return;
                }

                // Check that these are not the same coordinates
                if (finalDestination.RegionLocX == sp.Scene.RegionInfo.RegionLocX &&
                    finalDestination.RegionLocY == sp.Scene.RegionInfo.RegionLocY)
                {
                    // Can't do. Viewer crashes
                    sp.ControllingClient.SendTeleportFailed("Space warp! You would crash. Move to a different region and try again.");
                    return;
                }

                // Validate assorted conditions
                string reason = string.Empty;
                if (!ValidateGenericConditions(sp, reg, finalDestination, teleportFlags, out reason))
                {
                    sp.ControllingClient.SendTeleportFailed(reason);
                    return;
                }

                if (message != null)
                    sp.ControllingClient.SendAgentAlertMessage(message, true);

                //
                // This is it
                //
                DoTeleportInternal(sp, reg, finalDestination, position, lookAt, teleportFlags);
                //
                //
                //
            }
            else
            {
                finalDestination = null;

                // TP to a place that doesn't exist (anymore)
                // Inform the viewer about that
                sp.ControllingClient.SendTeleportFailed("The region you tried to teleport to doesn't exist anymore");

                // and set the map-tile to '(Offline)'
                uint regX, regY;
                Util.RegionHandleToRegionLoc(regionHandle, out regX, out regY);

                MapBlockData block = new MapBlockData();
                block.X = (ushort)regX;
                block.Y = (ushort)regY;
                block.Access = (byte)SimAccess.Down;

                List<MapBlockData> blocks = new List<MapBlockData>();
                blocks.Add(block);
                sp.ControllingClient.SendMapBlock(blocks, 0);
            }
        }

        // The teleport address could be an address in a subregion of a larger varregion.
        // Find the real base region and adjust the teleport location to account for the
        //    larger region.
        private GridRegion GetTeleportDestinationRegion(IGridService gridService, UUID scope, ulong regionHandle, ref Vector3 position)
        {
            uint x = 0, y = 0;
            Util.RegionHandleToWorldLoc(regionHandle, out x, out y);

            // Compute the world location we're teleporting to
            double worldX = (double)x + position.X;
            double worldY = (double)y + position.Y;

            // Find the region that contains the position
            GridRegion reg = GetRegionContainingWorldLocation(gridService, scope, worldX, worldY);

            if (reg != null)
            {
                // modify the position for the offset into the actual region returned
                position.X += x - reg.RegionLocX;
                position.Y += y - reg.RegionLocY;
            }

            return reg;
        }

        // Nothing to validate here
        protected virtual bool ValidateGenericConditions(ScenePresence sp, GridRegion reg, GridRegion finalDestination, uint teleportFlags, out string reason)
        {
            reason = String.Empty;
            return true;
        }

        /// <summary>
        /// Determines whether this instance is within the max transfer distance.
        /// </summary>
        /// <param name="sourceRegion"></param>
        /// <param name="destRegion"></param>
        /// <returns>
        /// <c>true</c> if this instance is within max transfer distance; otherwise, <c>false</c>.
        /// </returns>
        private bool IsWithinMaxTeleportDistance(RegionInfo sourceRegion, GridRegion destRegion)
        {
            if(MaxTransferDistance == 0)
                return true;

//                        m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Source co-ords are x={0} y={1}", curRegionX, curRegionY);
//
//                        m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Final dest is x={0} y={1} {2}@{3}",
//                            destRegionX, destRegionY, finalDestination.RegionID, finalDestination.ServerURI);

            // Insanely, RegionLoc on RegionInfo is the 256m map co-ord whilst GridRegion.RegionLoc is the raw meters position.
            return Math.Abs(sourceRegion.RegionLocX - destRegion.RegionCoordX) <= MaxTransferDistance
                && Math.Abs(sourceRegion.RegionLocY - destRegion.RegionCoordY) <= MaxTransferDistance;
        }

        /// <summary>
        /// Wraps DoTeleportInternal() and manages the transfer state.
        /// </summary>
        public void DoTeleport(
            ScenePresence sp, GridRegion reg, GridRegion finalDestination,
            Vector3 position, Vector3 lookAt, uint teleportFlags)
        {
            // Record that this agent is in transit so that we can prevent simultaneous requests and do later detection
            // of whether the destination region completes the teleport.
            if (!m_entityTransferStateMachine.SetInTransit(sp.UUID))
            {
                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Ignoring teleport request of {0} {1} to {2} ({3}) {4}/{5} - agent is already in transit.",
                    sp.Name, sp.UUID, reg.ServerURI, finalDestination.ServerURI, finalDestination.RegionName, position);
                sp.ControllingClient.SendTeleportFailed("Agent is already in transit.");
                return;
            }
            
            try
            {
                DoTeleportInternal(sp, reg, finalDestination, position, lookAt, teleportFlags);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[ENTITY TRANSFER MODULE]: Exception on teleport of {0} from {1}@{2} to {3}@{4}: {5}{6}",
                    sp.Name, sp.AbsolutePosition, sp.Scene.RegionInfo.RegionName, position, finalDestination.RegionName,
                    e.Message, e.StackTrace);

                sp.ControllingClient.SendTeleportFailed("Internal error");
            }
            finally
            {
                m_entityTransferStateMachine.ResetFromTransit(sp.UUID);
            }
        }

        /// <summary>
        /// Teleports the agent to another region.
        /// This method doesn't manage the transfer state; the caller must do that.
        /// </summary>
        private void DoTeleportInternal(
            ScenePresence sp, GridRegion reg, GridRegion finalDestination,
            Vector3 position, Vector3 lookAt, uint teleportFlags)
        {
            if (reg == null || finalDestination == null)
            {
                sp.ControllingClient.SendTeleportFailed("Unable to locate destination");
                return;
            }

            string homeURI = Scene.GetAgentHomeURI(sp.ControllingClient.AgentId);

            m_log.DebugFormat(
                "[ENTITY TRANSFER MODULE]: Teleporting {0} {1} from {2} to {3} ({4}) {5}/{6}",
                sp.Name, sp.UUID, sp.Scene.RegionInfo.RegionName,
                reg.ServerURI, finalDestination.ServerURI, finalDestination.RegionName, position);

            RegionInfo sourceRegion = sp.Scene.RegionInfo;

            if (!IsWithinMaxTeleportDistance(sourceRegion, finalDestination))
            {
                sp.ControllingClient.SendTeleportFailed(
                    string.Format(
                      "Can't teleport to {0} ({1},{2}) from {3} ({4},{5}), destination is more than {6} regions way",
                      finalDestination.RegionName, finalDestination.RegionCoordX, finalDestination.RegionCoordY,
                      sourceRegion.RegionName, sourceRegion.RegionLocX, sourceRegion.RegionLocY,
                      MaxTransferDistance));

                return;
            }

            uint newRegionX, newRegionY, oldRegionX, oldRegionY;
            Util.RegionHandleToRegionLoc(reg.RegionHandle, out newRegionX, out newRegionY);
            Util.RegionHandleToRegionLoc(sp.Scene.RegionInfo.RegionHandle, out oldRegionX, out oldRegionY);

            ulong destinationHandle = finalDestination.RegionHandle;

            // Let's do DNS resolution only once in this process, please!
            // This may be a costly operation. The reg.ExternalEndPoint field is not a passive field,
            // it's actually doing a lot of work.
            IPEndPoint endPoint = finalDestination.ExternalEndPoint;
            if (endPoint == null || endPoint.Address == null)
            {
                sp.ControllingClient.SendTeleportFailed("Remote Region appears to be down");

                return;
            }

            if (!sp.ValidateAttachments())
                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Failed validation of all attachments for teleport of {0} from {1} to {2}.  Continuing.",
                    sp.Name, sp.Scene.Name, finalDestination.RegionName);

            string reason;
            string version;
            string myversion = string.Format("{0}/{1}", OutgoingTransferVersionName, MaxOutgoingTransferVersion);
            if (!Scene.SimulationService.QueryAccess(
                finalDestination, sp.ControllingClient.AgentId, homeURI, true, position, myversion, out version, out reason))
            {
                sp.ControllingClient.SendTeleportFailed(reason);

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: {0} was stopped from teleporting from {1} to {2} because: {3}",
                    sp.Name, sp.Scene.Name, finalDestination.RegionName, reason);

                return;
            }

            // Before this point, teleport 'failure' is due to checkable pre-conditions such as whether the target
            // simulator can be found and is explicitly prepared to allow access.  Therefore, we will not count these
            // as server attempts.
            m_interRegionTeleportAttempts.Value++;

            m_log.DebugFormat(
                "[ENTITY TRANSFER MODULE]: {0} max transfer version is {1}/{2}, {3} max version is {4}", 
                sp.Scene.Name, OutgoingTransferVersionName, MaxOutgoingTransferVersion, finalDestination.RegionName, version);

            // Fixing a bug where teleporting while sitting results in the avatar ending up removed from
            // both regions
            if (sp.ParentID != (uint)0)
                sp.StandUp();
            else if (sp.Flying)
                teleportFlags |= (uint)TeleportFlags.IsFlying;

            if (DisableInterRegionTeleportCancellation)
                teleportFlags |= (uint)TeleportFlags.DisableCancel;

            // At least on LL 3.3.4, this is not strictly necessary - a teleport will succeed without sending this to
            // the viewer.  However, it might mean that the viewer does not see the black teleport screen (untested).
            sp.ControllingClient.SendTeleportStart(teleportFlags);

            // the avatar.Close below will clear the child region list. We need this below for (possibly)
            // closing the child agents, so save it here (we need a copy as it is Clear()-ed).
            //List<ulong> childRegions = avatar.KnownRegionHandles;
            // Compared to ScenePresence.CrossToNewRegion(), there's no obvious code to handle a teleport
            // failure at this point (unlike a border crossing failure).  So perhaps this can never fail
            // once we reach here...
            //avatar.Scene.RemoveCapsHandler(avatar.UUID);

            AgentCircuitData currentAgentCircuit = sp.Scene.AuthenticateHandler.GetAgentCircuitData(sp.ControllingClient.CircuitCode);
            AgentCircuitData agentCircuit = sp.ControllingClient.RequestClientInfo();
            agentCircuit.startpos = position;
            agentCircuit.child = true;
            agentCircuit.Appearance = sp.Appearance;
            if (currentAgentCircuit != null)
            {
                agentCircuit.ServiceURLs = currentAgentCircuit.ServiceURLs;
                agentCircuit.IPAddress = currentAgentCircuit.IPAddress;
                agentCircuit.Viewer = currentAgentCircuit.Viewer;
                agentCircuit.Channel = currentAgentCircuit.Channel;
                agentCircuit.Mac = currentAgentCircuit.Mac;
                agentCircuit.Id0 = currentAgentCircuit.Id0;
            }

            // if (NeedsNewAgent(sp.DrawDistance, oldRegionX, newRegionX, oldRegionY, newRegionY))
            float dist = (float)Math.Max(sp.Scene.DefaultDrawDistance,
                (float)Math.Max(sp.Scene.RegionInfo.RegionSizeX, sp.Scene.RegionInfo.RegionSizeY));
            if (NeedsNewAgent(dist, oldRegionX, newRegionX, oldRegionY, newRegionY))
            {
                // brand new agent, let's create a new caps seed
                agentCircuit.CapsPath = CapsUtil.GetRandomCapsObjectPath();
            }

            // We're going to fallback to V1 if the destination gives us anything smaller than 0.2 or we're forcing
            // use of the earlier protocol
            float versionNumber = 0.1f;
            string[] versionComponents = version.Split(new char[] { '/' });
            if (versionComponents.Length >= 2)
                float.TryParse(versionComponents[1], out versionNumber);

            if (versionNumber >= 0.2f && MaxOutgoingTransferVersion >= versionNumber)
                TransferAgent_V2(sp, agentCircuit, reg, finalDestination, endPoint, teleportFlags, oldRegionX, newRegionX, oldRegionY, newRegionY, version, out reason);
            else
                TransferAgent_V1(sp, agentCircuit, reg, finalDestination, endPoint, teleportFlags, oldRegionX, newRegionX, oldRegionY, newRegionY, version, out reason);           
        }

        private void TransferAgent_V1(ScenePresence sp, AgentCircuitData agentCircuit, GridRegion reg, GridRegion finalDestination,
            IPEndPoint endPoint, uint teleportFlags, uint oldRegionX, uint newRegionX, uint oldRegionY, uint newRegionY, string version, out string reason)
        {
            ulong destinationHandle = finalDestination.RegionHandle;
            AgentCircuitData currentAgentCircuit = sp.Scene.AuthenticateHandler.GetAgentCircuitData(sp.ControllingClient.CircuitCode);

            m_log.DebugFormat(
                "[ENTITY TRANSFER MODULE]: Using TP V1 for {0} going from {1} to {2}", 
                sp.Name, Scene.Name, finalDestination.RegionName);

            // Let's create an agent there if one doesn't exist yet. 
            // NOTE: logout will always be false for a non-HG teleport.
            bool logout = false;
            if (!CreateAgent(sp, reg, finalDestination, agentCircuit, teleportFlags, out reason, out logout))
            {
                m_interRegionTeleportFailures.Value++;

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Teleport of {0} from {1} to {2} was refused because {3}",
                    sp.Name, sp.Scene.RegionInfo.RegionName, finalDestination.RegionName, reason);

                sp.ControllingClient.SendTeleportFailed(reason);

                return;
            }

            if (m_entityTransferStateMachine.GetAgentTransferState(sp.UUID) == AgentTransferState.Cancelling)
            {
                m_interRegionTeleportCancels.Value++;

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Cancelled teleport of {0} to {1} from {2} after CreateAgent on client request",
                    sp.Name, finalDestination.RegionName, sp.Scene.Name);

                return;
            }
            else if (m_entityTransferStateMachine.GetAgentTransferState(sp.UUID) == AgentTransferState.Aborting)
            {
                m_interRegionTeleportAborts.Value++;

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Aborted teleport of {0} to {1} from {2} after CreateAgent due to previous client close.",
                    sp.Name, finalDestination.RegionName, sp.Scene.Name);

                return;
            }

            // Past this point we have to attempt clean up if the teleport fails, so update transfer state.
            m_entityTransferStateMachine.UpdateInTransit(sp.UUID, AgentTransferState.Transferring);

            // OK, it got this agent. Let's close some child agents
            sp.CloseChildAgents(newRegionX, newRegionY);

            IClientIPEndpoint ipepClient;
            string capsPath = String.Empty;
            float dist = (float)Math.Max(sp.Scene.DefaultDrawDistance,
                (float)Math.Max(sp.Scene.RegionInfo.RegionSizeX, sp.Scene.RegionInfo.RegionSizeY));
            if (NeedsNewAgent(dist, oldRegionX, newRegionX, oldRegionY, newRegionY))
            {
                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Determined that region {0} at {1},{2} needs new child agent for incoming agent {3} from {4}",
                    finalDestination.RegionName, newRegionX, newRegionY, sp.Name, Scene.Name);

                //sp.ControllingClient.SendTeleportProgress(teleportFlags, "Creating agent...");
                #region IP Translation for NAT
                // Uses ipepClient above
                if (sp.ClientView.TryGet(out ipepClient))
                {
                    endPoint.Address = NetworkUtil.GetIPFor(ipepClient.EndPoint, endPoint.Address);
                }
                #endregion
                capsPath = finalDestination.ServerURI + CapsUtil.GetCapsSeedPath(agentCircuit.CapsPath);

                if (m_eqModule != null)
                {
                    // The EnableSimulator message makes the client establish a connection with the destination
                    // simulator by sending the initial UseCircuitCode UDP packet to the destination containing the
                    // correct circuit code.
                    m_eqModule.EnableSimulator(destinationHandle, endPoint, sp.UUID,
                                        finalDestination.RegionSizeX, finalDestination.RegionSizeY);
                    m_log.DebugFormat("{0} Sent EnableSimulator. regName={1}, size=<{2},{3}>", LogHeader,
                        finalDestination.RegionName, finalDestination.RegionSizeX, finalDestination.RegionSizeY);

                    // XXX: Is this wait necessary?  We will always end up waiting on UpdateAgent for the destination
                    // simulator to confirm that it has established communication with the viewer.
                    Thread.Sleep(200);

                    // At least on LL 3.3.4 for teleports between different regions on the same simulator this appears
                    // unnecessary - teleport will succeed and SEED caps will be requested without it (though possibly
                    // only on TeleportFinish).  This is untested for region teleport between different simulators
                    // though this probably also works.
                    m_eqModule.EstablishAgentCommunication(sp.UUID, endPoint, capsPath, finalDestination.RegionHandle,
                                        finalDestination.RegionSizeX, finalDestination.RegionSizeY);
                }
                else
                {
                    // XXX: This is a little misleading since we're information the client of its avatar destination,
                    // which may or may not be a neighbour region of the source region.  This path is probably little
                    // used anyway (with EQ being the one used).  But it is currently being used for test code.
                    sp.ControllingClient.InformClientOfNeighbour(destinationHandle, endPoint);
                }
            }
            else
            {
                agentCircuit.CapsPath = sp.Scene.CapsModule.GetChildSeed(sp.UUID, reg.RegionHandle);
                capsPath = finalDestination.ServerURI + CapsUtil.GetCapsSeedPath(agentCircuit.CapsPath);
            }

            // Let's send a full update of the agent. This is a synchronous call.
            AgentData agent = new AgentData();
            sp.CopyTo(agent);
            agent.Position = agentCircuit.startpos;
            SetCallbackURL(agent, sp.Scene.RegionInfo);


            // We will check for an abort before UpdateAgent since UpdateAgent will require an active viewer to 
            // establish th econnection to the destination which makes it return true.
            if (m_entityTransferStateMachine.GetAgentTransferState(sp.UUID) == AgentTransferState.Aborting)
            {
                m_interRegionTeleportAborts.Value++;

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Aborted teleport of {0} to {1} from {2} before UpdateAgent",
                    sp.Name, finalDestination.RegionName, sp.Scene.Name);

                return;
            }

            // A common teleport failure occurs when we can send CreateAgent to the 
            // destination region but the viewer cannot establish the connection (e.g. due to network issues between
            // the viewer and the destination).  In this case, UpdateAgent timesout after 10 seconds, although then
            // there's a further 10 second wait whilst we attempt to tell the destination to delete the agent in Fail().
            if (!UpdateAgent(reg, finalDestination, agent, sp))
            {
                if (m_entityTransferStateMachine.GetAgentTransferState(sp.UUID) == AgentTransferState.Aborting)
                {
                    m_interRegionTeleportAborts.Value++;

                    m_log.DebugFormat(
                        "[ENTITY TRANSFER MODULE]: Aborted teleport of {0} to {1} from {2} after UpdateAgent due to previous client close.",
                        sp.Name, finalDestination.RegionName, sp.Scene.Name);

                    return;
                }

                m_log.WarnFormat(
                    "[ENTITY TRANSFER MODULE]: UpdateAgent failed on teleport of {0} to {1}.  Keeping avatar in {2}",
                    sp.Name, finalDestination.RegionName, sp.Scene.Name);

                Fail(sp, finalDestination, logout, currentAgentCircuit.SessionID.ToString(), "Connection between viewer and destination region could not be established.");
                return;
            }

            if (m_entityTransferStateMachine.GetAgentTransferState(sp.UUID) == AgentTransferState.Cancelling)
            {
                m_interRegionTeleportCancels.Value++;

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Cancelled teleport of {0} to {1} from {2} after UpdateAgent on client request",
                    sp.Name, finalDestination.RegionName, sp.Scene.Name);

                CleanupFailedInterRegionTeleport(sp, currentAgentCircuit.SessionID.ToString(), finalDestination);

                return;
            }

            m_log.DebugFormat(
                "[ENTITY TRANSFER MODULE]: Sending new CAPS seed url {0} from {1} to {2}",
                capsPath, sp.Scene.RegionInfo.RegionName, sp.Name);

            // We need to set this here to avoid an unlikely race condition when teleporting to a neighbour simulator,
            // where that neighbour simulator could otherwise request a child agent create on the source which then 
            // closes our existing agent which is still signalled as root.
            sp.IsChildAgent = true;

            // OK, send TPFinish to the client, so that it starts the process of contacting the destination region
            if (m_eqModule != null)
            {
                m_eqModule.TeleportFinishEvent(destinationHandle, 13, endPoint, 0, teleportFlags, capsPath, sp.UUID,
                            finalDestination.RegionSizeX, finalDestination.RegionSizeY);
            }
            else
            {
                sp.ControllingClient.SendRegionTeleport(destinationHandle, 13, endPoint, 4,
                                                            teleportFlags, capsPath);
            }

            // TeleportFinish makes the client send CompleteMovementIntoRegion (at the destination), which
            // trigers a whole shebang of things there, including MakeRoot. So let's wait for confirmation
            // that the client contacted the destination before we close things here.
            if (!m_entityTransferStateMachine.WaitForAgentArrivedAtDestination(sp.UUID))
            {
                if (m_entityTransferStateMachine.GetAgentTransferState(sp.UUID) == AgentTransferState.Aborting)
                {
                    m_interRegionTeleportAborts.Value++;

                    m_log.DebugFormat(
                        "[ENTITY TRANSFER MODULE]: Aborted teleport of {0} to {1} from {2} after WaitForAgentArrivedAtDestination due to previous client close.",
                        sp.Name, finalDestination.RegionName, sp.Scene.Name);

                    return;
                }

                m_log.WarnFormat(
                    "[ENTITY TRANSFER MODULE]: Teleport of {0} to {1} from {2} failed due to no callback from destination region.  Returning avatar to source region.",
                    sp.Name, finalDestination.RegionName, sp.Scene.RegionInfo.RegionName);

                Fail(sp, finalDestination, logout, currentAgentCircuit.SessionID.ToString(), "Destination region did not signal teleport completion.");

                return;
            }

            m_entityTransferStateMachine.UpdateInTransit(sp.UUID, AgentTransferState.CleaningUp);

            // For backwards compatibility
            if (version == "Unknown" || version == string.Empty)
            {
                // CrossAttachmentsIntoNewRegion is a synchronous call. We shouldn't need to wait after it
                m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Old simulator, sending attachments one by one...");
                CrossAttachmentsIntoNewRegion(finalDestination, sp, true);
            }

            // May need to logout or other cleanup
            AgentHasMovedAway(sp, logout);

            // Well, this is it. The agent is over there.
            KillEntity(sp.Scene, sp.LocalId);

            // Now let's make it officially a child agent
            sp.MakeChildAgent();

            // Finally, let's close this previously-known-as-root agent, when the jump is outside the view zone

            if (NeedsClosing(sp.Scene.DefaultDrawDistance, oldRegionX, newRegionX, oldRegionY, newRegionY, reg))
            {
                if (!sp.Scene.IncomingPreCloseClient(sp))
                    return;

                // We need to delay here because Imprudence viewers, unlike v1 or v3, have a short (<200ms, <500ms) delay before
                // they regard the new region as the current region after receiving the AgentMovementComplete
                // response.  If close is sent before then, it will cause the viewer to quit instead.
                //
                // This sleep can be increased if necessary.  However, whilst it's active,
                // an agent cannot teleport back to this region if it has teleported away.
                Thread.Sleep(2000);

                sp.Scene.CloseAgent(sp.UUID, false);
            }
            else
            {
                // now we have a child agent in this region. 
                sp.Reset();
            }
        }

        private void TransferAgent_V2(ScenePresence sp, AgentCircuitData agentCircuit, GridRegion reg, GridRegion finalDestination,
            IPEndPoint endPoint, uint teleportFlags, uint oldRegionX, uint newRegionX, uint oldRegionY, uint newRegionY, string version, out string reason)
        {
            ulong destinationHandle = finalDestination.RegionHandle;
            AgentCircuitData currentAgentCircuit = sp.Scene.AuthenticateHandler.GetAgentCircuitData(sp.ControllingClient.CircuitCode);

            // Let's create an agent there if one doesn't exist yet. 
            // NOTE: logout will always be false for a non-HG teleport.
            bool logout = false;
            if (!CreateAgent(sp, reg, finalDestination, agentCircuit, teleportFlags, out reason, out logout))
            {
                m_interRegionTeleportFailures.Value++;

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Teleport of {0} from {1} to {2} was refused because {3}",
                    sp.Name, sp.Scene.RegionInfo.RegionName, finalDestination.RegionName, reason);

                sp.ControllingClient.SendTeleportFailed(reason);

                return;
            }

            if (m_entityTransferStateMachine.GetAgentTransferState(sp.UUID) == AgentTransferState.Cancelling)
            {
                m_interRegionTeleportCancels.Value++;

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Cancelled teleport of {0} to {1} from {2} after CreateAgent on client request",
                    sp.Name, finalDestination.RegionName, sp.Scene.Name);

                return;
            }
            else if (m_entityTransferStateMachine.GetAgentTransferState(sp.UUID) == AgentTransferState.Aborting)
            {
                m_interRegionTeleportAborts.Value++;

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Aborted teleport of {0} to {1} from {2} after CreateAgent due to previous client close.",
                    sp.Name, finalDestination.RegionName, sp.Scene.Name);

                return;
            }

            // Past this point we have to attempt clean up if the teleport fails, so update transfer state.
            m_entityTransferStateMachine.UpdateInTransit(sp.UUID, AgentTransferState.Transferring);

            IClientIPEndpoint ipepClient;
            string capsPath = String.Empty;
            float dist = (float)Math.Max(sp.Scene.DefaultDrawDistance, 
                (float)Math.Max(sp.Scene.RegionInfo.RegionSizeX, sp.Scene.RegionInfo.RegionSizeY));
            if (NeedsNewAgent(dist, oldRegionX, newRegionX, oldRegionY, newRegionY))
            {
                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Determined that region {0} at {1},{2} needs new child agent for agent {3} from {4}",
                    finalDestination.RegionName, newRegionX, newRegionY, sp.Name, Scene.Name);

                //sp.ControllingClient.SendTeleportProgress(teleportFlags, "Creating agent...");
                #region IP Translation for NAT
                // Uses ipepClient above
                if (sp.ClientView.TryGet(out ipepClient))
                {
                    endPoint.Address = NetworkUtil.GetIPFor(ipepClient.EndPoint, endPoint.Address);
                }
                #endregion
                capsPath = finalDestination.ServerURI + CapsUtil.GetCapsSeedPath(agentCircuit.CapsPath);
            }
            else
            {
                agentCircuit.CapsPath = sp.Scene.CapsModule.GetChildSeed(sp.UUID, reg.RegionHandle);
                capsPath = finalDestination.ServerURI + CapsUtil.GetCapsSeedPath(agentCircuit.CapsPath);
            }

            // We need to set this here to avoid an unlikely race condition when teleporting to a neighbour simulator,
            // where that neighbour simulator could otherwise request a child agent create on the source which then 
            // closes our existing agent which is still signalled as root.
            //sp.IsChildAgent = true;

            // New protocol: send TP Finish directly, without prior ES or EAC. That's what happens in the Linden grid
            if (m_eqModule != null)
                m_eqModule.TeleportFinishEvent(destinationHandle, 13, endPoint, 0, teleportFlags, capsPath, sp.UUID,
                                    finalDestination.RegionSizeX, finalDestination.RegionSizeY);
            else
                sp.ControllingClient.SendRegionTeleport(destinationHandle, 13, endPoint, 4,
                                                            teleportFlags, capsPath);

            m_log.DebugFormat(
                "[ENTITY TRANSFER MODULE]: Sending new CAPS seed url {0} from {1} to {2}",
                capsPath, sp.Scene.RegionInfo.RegionName, sp.Name);

            // Let's send a full update of the agent. 
            AgentData agent = new AgentData();
            sp.CopyTo(agent);
            agent.Position = agentCircuit.startpos;
            agent.SenderWantsToWaitForRoot = true;
            //SetCallbackURL(agent, sp.Scene.RegionInfo);

            // Reset the do not close flag.  This must be done before the destination opens child connections (here
            // triggered by UpdateAgent) to avoid race conditions.  However, we also want to reset it as late as possible
            // to avoid a situation where an unexpectedly early call to Scene.NewUserConnection() wrongly results
            // in no close.
            sp.DoNotCloseAfterTeleport = false;

            // Send the Update. If this returns true, we know the client has contacted the destination
            // via CompleteMovementIntoRegion, so we can let go.
            // If it returns false, something went wrong, and we need to abort.
            if (!UpdateAgent(reg, finalDestination, agent, sp))
            {
                if (m_entityTransferStateMachine.GetAgentTransferState(sp.UUID) == AgentTransferState.Aborting)
                {
                    m_interRegionTeleportAborts.Value++;

                    m_log.DebugFormat(
                        "[ENTITY TRANSFER MODULE]: Aborted teleport of {0} to {1} from {2} after UpdateAgent due to previous client close.",
                        sp.Name, finalDestination.RegionName, sp.Scene.Name);

                    return;
                }

                m_log.WarnFormat(
                    "[ENTITY TRANSFER MODULE]: UpdateAgent failed on teleport of {0} to {1}.  Keeping avatar in {2}",
                    sp.Name, finalDestination.RegionName, sp.Scene.Name);

                Fail(sp, finalDestination, logout, currentAgentCircuit.SessionID.ToString(), "Connection between viewer and destination region could not be established.");
                return;
            }
            
            m_entityTransferStateMachine.UpdateInTransit(sp.UUID, AgentTransferState.CleaningUp);

            // Need to signal neighbours whether child agents may need closing irrespective of whether this
            // one needed closing.  We also need to close child agents as quickly as possible to avoid complicated
            // race conditions with rapid agent releporting (e.g. from A1 to a non-neighbour B, back
            // to a neighbour A2 then off to a non-neighbour C).  Closing child agents any later requires complex
            // distributed checks to avoid problems in rapid reteleporting scenarios and where child agents are
            // abandoned without proper close by viewer but then re-used by an incoming connection.
            sp.CloseChildAgents(newRegionX, newRegionY);

            // May need to logout or other cleanup
            AgentHasMovedAway(sp, logout);

            // Well, this is it. The agent is over there.
            KillEntity(sp.Scene, sp.LocalId);

            // Now let's make it officially a child agent
            sp.MakeChildAgent();

            // Finally, let's close this previously-known-as-root agent, when the jump is outside the view zone
            if (NeedsClosing(sp.Scene.DefaultDrawDistance, oldRegionX, newRegionX, oldRegionY, newRegionY, reg))
            {
                if (!sp.Scene.IncomingPreCloseClient(sp))
                    return;

                // RED ALERT!!!!
                // PLEASE DO NOT DECREASE THIS WAIT TIME UNDER ANY CIRCUMSTANCES.
                // THE VIEWERS SEEM TO NEED SOME TIME AFTER RECEIVING MoveAgentIntoRegion
                // BEFORE THEY SETTLE IN THE NEW REGION.
                // DECREASING THE WAIT TIME HERE WILL EITHER RESULT IN A VIEWER CRASH OR
                // IN THE AVIE BEING PLACED IN INFINITY FOR A COUPLE OF SECONDS.
                Thread.Sleep(15000);
            
                // OK, it got this agent. Let's close everything
                // If we shouldn't close the agent due to some other region renewing the connection 
                // then this will be handled in IncomingCloseAgent under lock conditions
                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Closing agent {0} in {1} after teleport", sp.Name, Scene.Name);

                sp.Scene.CloseAgent(sp.UUID, false);
            }
            else
            {
                // now we have a child agent in this region. 
                sp.Reset();
            }
        }

        /// <summary>
        /// Clean up an inter-region teleport that did not complete, either because of simulator failure or cancellation.
        /// </summary>
        /// <remarks>
        /// All operations here must be idempotent so that we can call this method at any point in the teleport process
        /// up until we send the TeleportFinish event quene event to the viewer.
        /// <remarks>
        /// <param name='sp'> </param>
        /// <param name='finalDestination'></param>
        protected virtual void CleanupFailedInterRegionTeleport(ScenePresence sp, string auth_token, GridRegion finalDestination)
        {
            m_entityTransferStateMachine.UpdateInTransit(sp.UUID, AgentTransferState.CleaningUp);

            if (sp.IsChildAgent) // We had set it to child before attempted TP (V1)
            {
                sp.IsChildAgent = false;
                ReInstantiateScripts(sp);

                EnableChildAgents(sp);
            }
            // Finally, kill the agent we just created at the destination.
            // XXX: Possibly this should be done asynchronously.
            Scene.SimulationService.CloseAgent(finalDestination, sp.UUID, auth_token);
        }

        /// <summary>
        /// Signal that the inter-region teleport failed and perform cleanup.
        /// </summary>
        /// <param name='sp'></param>
        /// <param name='finalDestination'></param>
        /// <param name='logout'></param>
        /// <param name='reason'>Human readable reason for teleport failure.  Will be sent to client.</param>
        protected virtual void Fail(ScenePresence sp, GridRegion finalDestination, bool logout, string auth_code, string reason)
        {
            CleanupFailedInterRegionTeleport(sp, auth_code, finalDestination);

            m_interRegionTeleportFailures.Value++;

            sp.ControllingClient.SendTeleportFailed(
                string.Format(
                    "Problems connecting to destination {0}, reason: {1}", finalDestination.RegionName, reason));

            sp.Scene.EventManager.TriggerTeleportFail(sp.ControllingClient, logout);
        }

        protected virtual bool CreateAgent(ScenePresence sp, GridRegion reg, GridRegion finalDestination, AgentCircuitData agentCircuit, uint teleportFlags, out string reason, out bool logout)
        {
            GridRegion source = new GridRegion(Scene.RegionInfo);
            source.RawServerURI = m_GatekeeperURI;

            logout = false;
            bool success = Scene.SimulationService.CreateAgent(source, finalDestination, agentCircuit, teleportFlags, out reason);

            if (success)
                sp.Scene.EventManager.TriggerTeleportStart(sp.ControllingClient, reg, finalDestination, teleportFlags, logout);

            return success;
        }

        protected virtual bool UpdateAgent(GridRegion reg, GridRegion finalDestination, AgentData agent, ScenePresence sp)
        {
            return Scene.SimulationService.UpdateAgent(finalDestination, agent);
        }

        protected virtual void SetCallbackURL(AgentData agent, RegionInfo region)
        {
            agent.CallbackURI = region.ServerURI + "agent/" + agent.AgentID.ToString() + "/" + region.RegionID.ToString() + "/release/";

            m_log.DebugFormat(
                "[ENTITY TRANSFER MODULE]: Set release callback URL to {0} in {1}",
                agent.CallbackURI, region.RegionName);
        }

        /// <summary>
        /// Clean up operations once an agent has moved away through cross or teleport.
        /// </summary>
        /// <param name='sp'></param>
        /// <param name='logout'></param>
        protected virtual void AgentHasMovedAway(ScenePresence sp, bool logout)
        {
            if (sp.Scene.AttachmentsModule != null)
                sp.Scene.AttachmentsModule.DeleteAttachmentsFromScene(sp, true);
        }

        protected void KillEntity(Scene scene, uint localID)
        {
            scene.SendKillObject(new List<uint> { localID });
        }

        protected virtual GridRegion GetFinalDestination(GridRegion region, UUID agentID, string agentHomeURI, out string message)
        {
            message = null;
            return region;
        }

        // This returns 'true' if the new region already has a child agent for our
        //    incoming agent. The implication is that, if 'false', we have to create  the
        //    child and then teleport into the region.
        protected virtual bool NeedsNewAgent(float drawdist, uint oldRegionX, uint newRegionX, uint oldRegionY, uint newRegionY)
        {
            if (m_regionCombinerModule != null && m_regionCombinerModule.IsRootForMegaregion(Scene.RegionInfo.RegionID))
            {
                Vector2 swCorner, neCorner;
                GetMegaregionViewRange(out swCorner, out neCorner);

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Megaregion view of {0} is from {1} to {2} with new agent check for {3},{4}",
                    Scene.Name, swCorner, neCorner, newRegionX, newRegionY);

                return !(newRegionX >= swCorner.X && newRegionX <= neCorner.X && newRegionY >= swCorner.Y && newRegionY <= neCorner.Y);
            }
            else
            {
                return Util.IsOutsideView(drawdist, oldRegionX, newRegionX, oldRegionY, newRegionY);
            }
        }

        protected virtual bool NeedsClosing(float drawdist, uint oldRegionX, uint newRegionX, uint oldRegionY, uint newRegionY, GridRegion reg)
        {
            return Util.IsOutsideView(drawdist, oldRegionX, newRegionX, oldRegionY, newRegionY);
        }

        #endregion

        #region Landmark Teleport
        /// <summary>
        /// Tries to teleport agent to landmark.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="regionHandle"></param>
        /// <param name="position"></param>
        public virtual void RequestTeleportLandmark(IClientAPI remoteClient, AssetLandmark lm)
        {
            GridRegion info = Scene.GridService.GetRegionByUUID(UUID.Zero, lm.RegionID);

            if (info == null)
            {
                // can't find the region: Tell viewer and abort
                remoteClient.SendTeleportFailed("The teleport destination could not be found.");
                return;
            }
            ((Scene)(remoteClient.Scene)).RequestTeleportLocation(remoteClient, info.RegionHandle, lm.Position, 
                Vector3.Zero, (uint)(Constants.TeleportFlags.SetLastToTarget | Constants.TeleportFlags.ViaLandmark));
        }

        #endregion 

        #region Teleport Home

        public virtual void TriggerTeleportHome(UUID id, IClientAPI client)
        {
            TeleportHome(id, client);
        }

        public virtual bool TeleportHome(UUID id, IClientAPI client)
        {
            m_log.DebugFormat(
                "[ENTITY TRANSFER MODULE]: Request to teleport {0} {1} home", client.Name, client.AgentId);

            //OpenSim.Services.Interfaces.PresenceInfo pinfo = Scene.PresenceService.GetAgent(client.SessionId);
            GridUserInfo uinfo = Scene.GridUserService.GetGridUserInfo(client.AgentId.ToString());

            if (uinfo != null)
            {
                if (uinfo.HomeRegionID == UUID.Zero)
                {
                    // can't find the Home region: Tell viewer and abort
                    m_log.ErrorFormat("{0} No grid user info found for {1} {2}. Cannot send home.",
                                    LogHeader, client.Name, client.AgentId);
                    client.SendTeleportFailed("You don't have a home position set.");
                    return false;
                }
                GridRegion regionInfo = Scene.GridService.GetRegionByUUID(UUID.Zero, uinfo.HomeRegionID);
                if (regionInfo == null)
                {
                    // can't find the Home region: Tell viewer and abort
                    client.SendTeleportFailed("Your home region could not be found.");
                    return false;
                }
                
                m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Home region of {0} is {1} ({2}-{3})",
                    client.Name, regionInfo.RegionName, regionInfo.RegionCoordX, regionInfo.RegionCoordY);

                // a little eekie that this goes back to Scene and with a forced cast, will fix that at some point...
                ((Scene)(client.Scene)).RequestTeleportLocation(
                    client, regionInfo.RegionHandle, uinfo.HomePosition, uinfo.HomeLookAt,
                    (uint)(Constants.TeleportFlags.SetLastToTarget | Constants.TeleportFlags.ViaHome));
                return true;
            }
            else
            {
                // can't find the Home region: Tell viewer and abort
                client.SendTeleportFailed("Your home region could not be found.");
            }
            return false;
        }

        #endregion


        #region Agent Crossings

        // Given a position relative to the current region (which has previously been tested to
        //    see that it is actually outside the current region), find the new region that the
        //    point is actually in.
        // Returns the coordinates and information of the new region or 'null' of it doesn't exist.
        public GridRegion GetDestination(Scene scene, UUID agentID, Vector3 pos,
                                            out string version, out Vector3 newpos, out string failureReason)
        {
            version = String.Empty;
            newpos = pos;
            failureReason = string.Empty;
            string homeURI = scene.GetAgentHomeURI(agentID);

//            m_log.DebugFormat(
//                "[ENTITY TRANSFER MODULE]: Crossing agent {0} at pos {1} in {2}", agent.Name, pos, scene.Name);

            // Compute world location of the object's position
            double presenceWorldX = (double)scene.RegionInfo.WorldLocX + pos.X;
            double presenceWorldY = (double)scene.RegionInfo.WorldLocY + pos.Y;

            // Call the grid service to lookup the region containing the new position.
            GridRegion neighbourRegion = GetRegionContainingWorldLocation(scene.GridService, scene.RegionInfo.ScopeID,
                                                        presenceWorldX, presenceWorldY, 
                                                        Math.Max(scene.RegionInfo.RegionSizeX, scene.RegionInfo.RegionSizeY));

            if (neighbourRegion != null)
            {
                // Compute the entity's position relative to the new region
                newpos = new Vector3((float)(presenceWorldX - (double)neighbourRegion.RegionLocX),
                                      (float)(presenceWorldY - (double)neighbourRegion.RegionLocY),
                                      pos.Z);

                if (m_bannedRegionCache.IfBanned(neighbourRegion.RegionHandle, agentID))
                {
                    failureReason = "Cannot region cross into banned parcel";
                    neighbourRegion = null;
                }
                else
                {
                    // If not banned, make sure this agent is not in the list.
                    m_bannedRegionCache.Remove(neighbourRegion.RegionHandle, agentID);
                }

                // Check to see if we have access to the target region.
                string myversion = string.Format("{0}/{1}", OutgoingTransferVersionName, MaxOutgoingTransferVersion);
                if (neighbourRegion != null
                    && !scene.SimulationService.QueryAccess(neighbourRegion, agentID, homeURI, false, newpos, myversion, out version, out failureReason))
                {
                    // remember banned
                    m_bannedRegionCache.Add(neighbourRegion.RegionHandle, agentID);
                    neighbourRegion = null;
                }
            }
            else
            {
                // The destination region just doesn't exist
                failureReason = "Cannot cross into non-existent region";
            }

            if (neighbourRegion == null)
                m_log.DebugFormat("{0} GetDestination: region not found. Old region name={1} at <{2},{3}> of size <{4},{5}>. Old pos={6}",
                    LogHeader, scene.RegionInfo.RegionName,
                    scene.RegionInfo.RegionLocX, scene.RegionInfo.RegionLocY,
                    scene.RegionInfo.RegionSizeX, scene.RegionInfo.RegionSizeY,
                    pos);
            else
                m_log.DebugFormat("{0} GetDestination: new region={1} at <{2},{3}> of size <{4},{5}>, newpos=<{6},{7}>",
                    LogHeader, neighbourRegion.RegionName,
                    neighbourRegion.RegionLocX, neighbourRegion.RegionLocY, neighbourRegion.RegionSizeX, neighbourRegion.RegionSizeY,
                    newpos.X, newpos.Y);

            return neighbourRegion;
        }

        public bool Cross(ScenePresence agent, bool isFlying)
        {
            Vector3 newpos;
            string version;
            string failureReason;

            GridRegion neighbourRegion = GetDestination(agent.Scene, agent.UUID, agent.AbsolutePosition,
                                                            out version, out newpos, out failureReason);
            if (neighbourRegion == null)
            {
                agent.ControllingClient.SendAlertMessage(failureReason);
                return false;
            }

            agent.IsInTransit = true;

            CrossAgentToNewRegionDelegate d = CrossAgentToNewRegionAsync;
            d.BeginInvoke(agent, newpos, neighbourRegion, isFlying, version, CrossAgentToNewRegionCompleted, d);

            Scene.EventManager.TriggerCrossAgentToNewRegion(agent, isFlying, neighbourRegion);

            return true;
        }


        public delegate void InformClientToInitiateTeleportToLocationDelegate(ScenePresence agent, uint regionX, uint regionY,
                                                            Vector3 position,
                                                            Scene initiatingScene);

        private void InformClientToInitiateTeleportToLocation(ScenePresence agent, uint regionX, uint regionY, Vector3 position, Scene initiatingScene)
        {

            // This assumes that we know what our neighbours are.

            InformClientToInitiateTeleportToLocationDelegate d = InformClientToInitiateTeleportToLocationAsync;
            d.BeginInvoke(agent, regionX, regionY, position, initiatingScene,
                          InformClientToInitiateTeleportToLocationCompleted,
                          d);
        }

        public void InformClientToInitiateTeleportToLocationAsync(ScenePresence agent, uint regionX, uint regionY, Vector3 position,
            Scene initiatingScene)
        {
            Thread.Sleep(10000);

            m_log.DebugFormat(
                "[ENTITY TRANSFER MODULE]: Auto-reteleporting {0} to correct megaregion location {1},{2},{3} from {4}", 
                agent.Name, regionX, regionY, position, initiatingScene.Name);

            agent.Scene.RequestTeleportLocation(
                agent.ControllingClient, 
                Util.RegionLocToHandle(regionX, regionY),
                position, 
                agent.Lookat, 
                (uint)Constants.TeleportFlags.ViaLocation);

            /*
            IMessageTransferModule im = initiatingScene.RequestModuleInterface<IMessageTransferModule>();
            if (im != null)
            {
                UUID gotoLocation = Util.BuildFakeParcelID(
                    Util.RegionLocToHandle(regionX, regionY),
                    (uint)(int)position.X,
                    (uint)(int)position.Y,
                    (uint)(int)position.Z);

                GridInstantMessage m
                    = new GridInstantMessage(
                        initiatingScene,
                        UUID.Zero,
                        "Region",
                        agent.UUID,
                        (byte)InstantMessageDialog.GodLikeRequestTeleport,
                        false,
                        "",
                        gotoLocation,
                        false,
                        new Vector3(127, 0, 0),
                        new Byte[0],
                        false);

                im.SendInstantMessage(m, delegate(bool success)
                {
                    m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Client Initiating Teleport sending IM success = {0}", success);
                });

            }
            */
        }

        private void InformClientToInitiateTeleportToLocationCompleted(IAsyncResult iar)
        {
            InformClientToInitiateTeleportToLocationDelegate icon =
                (InformClientToInitiateTeleportToLocationDelegate)iar.AsyncState;
            icon.EndInvoke(iar);
        }

        public bool CrossAgentToNewRegionPrep(ScenePresence agent, GridRegion neighbourRegion)
        {
            if (neighbourRegion == null)
                return false;
            
            m_entityTransferStateMachine.SetInTransit(agent.UUID);

            agent.RemoveFromPhysicalScene();

            return true;
        }

        /// <summary>
        /// This Closes child agents on neighbouring regions
        /// Calls an asynchronous method to do so..  so it doesn't lag the sim.
        /// </summary>
        public ScenePresence CrossAgentToNewRegionAsync(
                                ScenePresence agent, Vector3 pos, GridRegion neighbourRegion,
                                bool isFlying, string version)
        {
            try
            {
                m_log.DebugFormat("{0}: CrossAgentToNewRegionAsync: new region={1} at <{2},{3}>. newpos={4}",
                            LogHeader, neighbourRegion.RegionName, neighbourRegion.RegionLocX, neighbourRegion.RegionLocY, pos);

                if (!CrossAgentToNewRegionPrep(agent, neighbourRegion))
                {
                    m_log.DebugFormat("{0}: CrossAgentToNewRegionAsync: prep failed. Resetting transfer state", LogHeader);
                    m_entityTransferStateMachine.ResetFromTransit(agent.UUID);
                }

                if (!CrossAgentIntoNewRegionMain(agent, pos, neighbourRegion, isFlying))
                {
                    m_log.DebugFormat("{0}: CrossAgentToNewRegionAsync: cross main failed. Resetting transfer state", LogHeader);
                    m_entityTransferStateMachine.ResetFromTransit(agent.UUID);
                }

                CrossAgentToNewRegionPost(agent, pos, neighbourRegion, isFlying, version);
            }
            catch (Exception e)
            {
                m_log.Error(string.Format("{0}: CrossAgentToNewRegionAsync: failed with exception  ", LogHeader), e);
            }

            return agent;
        }

        public bool CrossAgentIntoNewRegionMain(ScenePresence agent, Vector3 pos, GridRegion neighbourRegion, bool isFlying)
        {
            try
            {
                AgentData cAgent = new AgentData(); 
                agent.CopyTo(cAgent);
                cAgent.Position = pos;

                if (isFlying)
                    cAgent.ControlFlags |= (uint)AgentManager.ControlFlags.AGENT_CONTROL_FLY;

                // We don't need the callback anymnore
                cAgent.CallbackURI = String.Empty;

                // Beyond this point, extra cleanup is needed beyond removing transit state
                m_entityTransferStateMachine.UpdateInTransit(agent.UUID, AgentTransferState.Transferring);

                if (!agent.Scene.SimulationService.UpdateAgent(neighbourRegion, cAgent))
                {
                    // region doesn't take it
                    m_entityTransferStateMachine.UpdateInTransit(agent.UUID, AgentTransferState.CleaningUp);

                    m_log.WarnFormat(
                        "[ENTITY TRANSFER MODULE]: Region {0} would not accept update for agent {1} on cross attempt.  Returning to original region.", 
                        neighbourRegion.RegionName, agent.Name);

                    ReInstantiateScripts(agent);
                    agent.AddToPhysicalScene(isFlying);

                    return false;
                }

            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[ENTITY TRANSFER MODULE]: Problem crossing user {0} to new region {1} from {2}.  Exception {3}{4}",
                    agent.Name, neighbourRegion.RegionName, agent.Scene.RegionInfo.RegionName, e.Message, e.StackTrace);

                // TODO: Might be worth attempting other restoration here such as reinstantiation of scripts, etc.
                return false;
            }

            return true;
        }

        public void CrossAgentToNewRegionPost(ScenePresence agent, Vector3 pos, GridRegion neighbourRegion,
            bool isFlying, string version)
        {
            agent.ControllingClient.RequestClientInfo();

            string agentcaps;
            if (!agent.KnownRegions.TryGetValue(neighbourRegion.RegionHandle, out agentcaps))
            {
                m_log.ErrorFormat("[ENTITY TRANSFER MODULE]: No ENTITY TRANSFER MODULE information for region handle {0}, exiting CrossToNewRegion.",
                                 neighbourRegion.RegionHandle);
                return;
            }

            // No turning back
            agent.IsChildAgent = true;

            string capsPath = neighbourRegion.ServerURI + CapsUtil.GetCapsSeedPath(agentcaps);

            m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Sending new CAPS seed url {0} to client {1}", capsPath, agent.UUID);

            Vector3 vel2 = new Vector3(agent.Velocity.X, agent.Velocity.Y, 0);

            if (m_eqModule != null)
            {
                m_eqModule.CrossRegion(
                    neighbourRegion.RegionHandle, pos + agent.Velocity, vel2 /* agent.Velocity */,
                    neighbourRegion.ExternalEndPoint,
                    capsPath, agent.UUID, agent.ControllingClient.SessionId,
                    neighbourRegion.RegionSizeX, neighbourRegion.RegionSizeY);
            }
            else
            {
                m_log.ErrorFormat("{0} Using old CrossRegion packet. Varregion will not work!!", LogHeader);
                agent.ControllingClient.CrossRegion(neighbourRegion.RegionHandle, pos + agent.Velocity, agent.Velocity, neighbourRegion.ExternalEndPoint,
                                            capsPath);
            }

            // SUCCESS!
            m_entityTransferStateMachine.UpdateInTransit(agent.UUID, AgentTransferState.ReceivedAtDestination);

            // Unlike a teleport, here we do not wait for the destination region to confirm the receipt.
            m_entityTransferStateMachine.UpdateInTransit(agent.UUID, AgentTransferState.CleaningUp);

            agent.MakeChildAgent();

            // FIXME: Possibly this should occur lower down after other commands to close other agents,
            // but not sure yet what the side effects would be.
            m_entityTransferStateMachine.ResetFromTransit(agent.UUID);

            // now we have a child agent in this region. Request all interesting data about other (root) agents
            agent.SendOtherAgentsAvatarDataToClient();
            agent.SendOtherAgentsAppearanceToClient();

            // Backwards compatibility. Best effort
            if (version == "Unknown" || version == string.Empty)
            {
                m_log.DebugFormat("[ENTITY TRANSFER MODULE]: neighbor with old version, passing attachments one by one...");
                Thread.Sleep(3000); // wait a little now that we're not waiting for the callback
                CrossAttachmentsIntoNewRegion(neighbourRegion, agent, true);
            }

            // Next, let's close the child agent connections that are too far away.
            uint neighbourx;
            uint neighboury;
            Util.RegionHandleToRegionLoc(neighbourRegion.RegionHandle, out neighbourx, out neighboury);

            agent.CloseChildAgents(neighbourx, neighboury);

            AgentHasMovedAway(agent, false);

            // the user may change their profile information in other region,
            // so the userinfo in UserProfileCache is not reliable any more, delete it
            // REFACTORING PROBLEM. Well, not a problem, but this method is HORRIBLE!
//            if (agent.Scene.NeedSceneCacheClear(agent.UUID))
//            {
//                m_log.DebugFormat(
//                    "[ENTITY TRANSFER MODULE]: User {0} is going to another region", agent.UUID);
//            }

            //m_log.Debug("AFTER CROSS");
            //Scene.DumpChildrenSeeds(UUID);
            //DumpKnownRegions();

            return;
        }
         
        private void CrossAgentToNewRegionCompleted(IAsyncResult iar)
        {
            CrossAgentToNewRegionDelegate icon = (CrossAgentToNewRegionDelegate)iar.AsyncState;
            ScenePresence agent = icon.EndInvoke(iar);

            //// If the cross was successful, this agent is a child agent
            //if (agent.IsChildAgent)
            //    agent.Reset();
            //else // Not successful
            //    agent.RestoreInCurrentScene();

            // In any case
            agent.IsInTransit = false;

            m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Crossing agent {0} {1} completed.", agent.Firstname, agent.Lastname);
        }

        #endregion

        #region Enable Child Agent

        /// <summary>
        /// This informs a single neighbouring region about agent "avatar".
        /// Calls an asynchronous method to do so..  so it doesn't lag the sim.
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="region"></param>
        public void EnableChildAgent(ScenePresence sp, GridRegion region)
        {
            m_log.DebugFormat("[ENTITY TRANSFER]: Enabling child agent in new neighbour {0}", region.RegionName);

            AgentCircuitData currentAgentCircuit = sp.Scene.AuthenticateHandler.GetAgentCircuitData(sp.ControllingClient.CircuitCode);
            AgentCircuitData agent = sp.ControllingClient.RequestClientInfo();
            agent.BaseFolder = UUID.Zero;
            agent.InventoryFolder = UUID.Zero;
            agent.startpos = new Vector3(128, 128, 70);
            agent.child = true;
            agent.Appearance = sp.Appearance;
            agent.CapsPath = CapsUtil.GetRandomCapsObjectPath();

            agent.ChildrenCapSeeds = new Dictionary<ulong, string>(sp.Scene.CapsModule.GetChildrenSeeds(sp.UUID));
            //m_log.DebugFormat("[XXX] Seeds 1 {0}", agent.ChildrenCapSeeds.Count);

            if (!agent.ChildrenCapSeeds.ContainsKey(sp.Scene.RegionInfo.RegionHandle))
                agent.ChildrenCapSeeds.Add(sp.Scene.RegionInfo.RegionHandle, sp.ControllingClient.RequestClientInfo().CapsPath);
            //m_log.DebugFormat("[XXX] Seeds 2 {0}", agent.ChildrenCapSeeds.Count);

            sp.AddNeighbourRegion(region.RegionHandle, agent.CapsPath);
            //foreach (ulong h in agent.ChildrenCapSeeds.Keys)
            //    m_log.DebugFormat("[XXX] --> {0}", h);
            //m_log.DebugFormat("[XXX] Adding {0}", region.RegionHandle);
            if (agent.ChildrenCapSeeds.ContainsKey(region.RegionHandle))
            {
                m_log.WarnFormat(
                    "[ENTITY TRANSFER]: Overwriting caps seed {0} with {1} for region {2} (handle {3}) for {4} in {5}", 
                    agent.ChildrenCapSeeds[region.RegionHandle], agent.CapsPath, 
                    region.RegionName, region.RegionHandle, sp.Name, Scene.Name);
            }

            agent.ChildrenCapSeeds[region.RegionHandle] = agent.CapsPath;

            if (sp.Scene.CapsModule != null)
            {
                sp.Scene.CapsModule.SetChildrenSeed(sp.UUID, agent.ChildrenCapSeeds);
            }

            if (currentAgentCircuit != null)
            {
                agent.ServiceURLs = currentAgentCircuit.ServiceURLs;
                agent.IPAddress = currentAgentCircuit.IPAddress;
                agent.Viewer = currentAgentCircuit.Viewer;
                agent.Channel = currentAgentCircuit.Channel;
                agent.Mac = currentAgentCircuit.Mac;
                agent.Id0 = currentAgentCircuit.Id0;
            }

            IPEndPoint external = region.ExternalEndPoint;
            if (external != null)
            {
                InformClientOfNeighbourDelegate d = InformClientOfNeighbourAsync;
                d.BeginInvoke(sp, agent, region, external, true,
                          InformClientOfNeighbourCompleted,
                          d);
            }
        }
        #endregion

        #region Enable Child Agents

        private delegate void InformClientOfNeighbourDelegate(
            ScenePresence avatar, AgentCircuitData a, GridRegion reg, IPEndPoint endPoint, bool newAgent);

        /// <summary>
        /// This informs all neighbouring regions about agent "avatar".
        /// </summary>
        /// <param name="sp"></param>
        public void EnableChildAgents(ScenePresence sp)
        {
            List<GridRegion> neighbours = new List<GridRegion>();
            RegionInfo m_regionInfo = sp.Scene.RegionInfo;

            if (m_regionInfo != null)
            {
                neighbours = GetNeighbours(sp, m_regionInfo.RegionLocX, m_regionInfo.RegionLocY);
            }
            else
            {
                m_log.Debug("[ENTITY TRANSFER MODULE]: m_regionInfo was null in EnableChildAgents, is this a NPC?");
            }

            /// We need to find the difference between the new regions where there are no child agents
            /// and the regions where there are already child agents. We only send notification to the former.
            List<ulong> neighbourHandles = NeighbourHandles(neighbours); // on this region
            neighbourHandles.Add(sp.Scene.RegionInfo.RegionHandle);  // add this region too
            List<ulong> previousRegionNeighbourHandles;

            if (sp.Scene.CapsModule != null)
            {
                previousRegionNeighbourHandles =
                    new List<ulong>(sp.Scene.CapsModule.GetChildrenSeeds(sp.UUID).Keys);
            }
            else
            {
                previousRegionNeighbourHandles = new List<ulong>();
            }

            List<ulong> newRegions = NewNeighbours(neighbourHandles, previousRegionNeighbourHandles);
            List<ulong> oldRegions = OldNeighbours(neighbourHandles, previousRegionNeighbourHandles);

//            Dump("Current Neighbors", neighbourHandles);
//            Dump("Previous Neighbours", previousRegionNeighbourHandles);
//            Dump("New Neighbours", newRegions);
//            Dump("Old Neighbours", oldRegions);

            /// Update the scene presence's known regions here on this region
            sp.DropOldNeighbours(oldRegions);

            /// Collect as many seeds as possible
            Dictionary<ulong, string> seeds;
            if (sp.Scene.CapsModule != null)
                seeds = new Dictionary<ulong, string>(sp.Scene.CapsModule.GetChildrenSeeds(sp.UUID));
            else
                seeds = new Dictionary<ulong, string>();

            //m_log.Debug(" !!! No. of seeds: " + seeds.Count);
            if (!seeds.ContainsKey(sp.Scene.RegionInfo.RegionHandle))
                seeds.Add(sp.Scene.RegionInfo.RegionHandle, sp.ControllingClient.RequestClientInfo().CapsPath);

            /// Create the necessary child agents
            List<AgentCircuitData> cagents = new List<AgentCircuitData>();
            foreach (GridRegion neighbour in neighbours)
            {
                if (neighbour.RegionHandle != sp.Scene.RegionInfo.RegionHandle)
                {
                    AgentCircuitData currentAgentCircuit = sp.Scene.AuthenticateHandler.GetAgentCircuitData(sp.ControllingClient.CircuitCode);
                    AgentCircuitData agent = sp.ControllingClient.RequestClientInfo();
                    agent.BaseFolder = UUID.Zero;
                    agent.InventoryFolder = UUID.Zero;
                    agent.startpos = sp.AbsolutePosition + CalculateOffset(sp, neighbour);
                    agent.child = true;
                    agent.Appearance = sp.Appearance;
                    if (currentAgentCircuit != null)
                    {
                        agent.ServiceURLs = currentAgentCircuit.ServiceURLs;
                        agent.IPAddress = currentAgentCircuit.IPAddress;
                        agent.Viewer = currentAgentCircuit.Viewer;
                        agent.Channel = currentAgentCircuit.Channel;
                        agent.Mac = currentAgentCircuit.Mac;
                        agent.Id0 = currentAgentCircuit.Id0;
                    }

                    if (newRegions.Contains(neighbour.RegionHandle))
                    {
                        agent.CapsPath = CapsUtil.GetRandomCapsObjectPath();
                        sp.AddNeighbourRegion(neighbour.RegionHandle, agent.CapsPath);
                        seeds.Add(neighbour.RegionHandle, agent.CapsPath);
                    }
                    else
                    {
                        agent.CapsPath = sp.Scene.CapsModule.GetChildSeed(sp.UUID, neighbour.RegionHandle);
                    }

                    cagents.Add(agent);
                }
            }

            /// Update all child agent with everyone's seeds
            foreach (AgentCircuitData a in cagents)
            {
                a.ChildrenCapSeeds = new Dictionary<ulong, string>(seeds);
            }

            if (sp.Scene.CapsModule != null)
            {
                sp.Scene.CapsModule.SetChildrenSeed(sp.UUID, seeds);
            }
            sp.KnownRegions = seeds;
            //avatar.Scene.DumpChildrenSeeds(avatar.UUID);
            //avatar.DumpKnownRegions();

            bool newAgent = false;
            int count = 0;
            foreach (GridRegion neighbour in neighbours)
            {
                //m_log.WarnFormat("--> Going to send child agent to {0}", neighbour.RegionName);
                // Don't do it if there's already an agent in that region
                if (newRegions.Contains(neighbour.RegionHandle))
                    newAgent = true;
                else
                    newAgent = false;
//                    continue;

                if (neighbour.RegionHandle != sp.Scene.RegionInfo.RegionHandle)
                {
                    try
                    {
                        // Let's put this back at sync, so that it doesn't clog 
                        // the network, especially for regions in the same physical server.
                        // We're really not in a hurry here.
                        InformClientOfNeighbourAsync(sp, cagents[count], neighbour, neighbour.ExternalEndPoint, newAgent);
                        //InformClientOfNeighbourDelegate d = InformClientOfNeighbourAsync;
                        //d.BeginInvoke(sp, cagents[count], neighbour, neighbour.ExternalEndPoint, newAgent,
                        //              InformClientOfNeighbourCompleted,
                        //              d);
                    }

                    catch (ArgumentOutOfRangeException)
                    {
                        m_log.ErrorFormat(
                           "[ENTITY TRANSFER MODULE]: Neighbour Regions response included the current region in the neighbour list.  The following region will not display to the client: {0} for region {1} ({2}, {3}).",
                           neighbour.ExternalHostName,
                           neighbour.RegionHandle,
                           neighbour.RegionLocX,
                           neighbour.RegionLocY);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat(
                            "[ENTITY TRANSFER MODULE]: Could not resolve external hostname {0} for region {1} ({2}, {3}).  {4}",
                            neighbour.ExternalHostName,
                            neighbour.RegionHandle,
                            neighbour.RegionLocX,
                            neighbour.RegionLocY,
                            e);

                        // FIXME: Okay, even though we've failed, we're still going to throw the exception on,
                        // since I don't know what will happen if we just let the client continue

                        // XXX: Well, decided to swallow the exception instead for now.  Let us see how that goes.
                        // throw e;

                    }
                }
                count++;
            }
        }

        // Computes the difference between two region bases.
        // Returns a vector of world coordinates (meters) from base of first region to the second.
        // The first region is the home region of the passed scene presence.
        Vector3 CalculateOffset(ScenePresence sp, GridRegion neighbour)
        {
            /*
            int rRegionX = (int)sp.Scene.RegionInfo.LegacyRegionLocX;
            int rRegionY = (int)sp.Scene.RegionInfo.LegacyRegionLocY;
            int tRegionX = neighbour.RegionLocX / (int)Constants.RegionSize;
            int tRegionY = neighbour.RegionLocY / (int)Constants.RegionSize;
            int shiftx = (rRegionX - tRegionX) * (int)Constants.RegionSize;
            int shifty = (rRegionY - tRegionY) * (int)Constants.RegionSize;
            return new Vector3(shiftx, shifty, 0f);
             */
            return new Vector3( sp.Scene.RegionInfo.WorldLocX - neighbour.RegionLocX,
                                sp.Scene.RegionInfo.WorldLocY - neighbour.RegionLocY,
                                0f);
        }

        public GridRegion GetRegionContainingWorldLocation(IGridService pGridService, UUID pScopeID, double px, double py)
        {
            // Since we don't know how big the regions could be, we have to search a very large area
            //    to find possible regions.
            return GetRegionContainingWorldLocation(pGridService, pScopeID, px, py, Constants.MaximumRegionSize);
        }

        #region NotFoundLocationCache class
        // A collection of not found locations to make future lookups 'not found' lookups quick.
        // A simple expiring cache that keeps not found locations for some number of seconds.
        // A 'not found' location is presumed to be anywhere in the minimum sized region that
        //    contains that point. A conservitive estimate.
        private class NotFoundLocationCache
        {
            private struct NotFoundLocation
            {
                public double minX, maxX, minY, maxY;
                public DateTime expireTime;
            }
            private List<NotFoundLocation> m_notFoundLocations = new List<NotFoundLocation>();
            public NotFoundLocationCache()
            {
            }
            // Add an area to the list of 'not found' places. The area is the snapped region
            //    area around the added point.
            public void Add(double pX, double pY)
            {
                lock (m_notFoundLocations)
                {
                    if (!LockedContains(pX, pY))
                    {
                        NotFoundLocation nfl = new NotFoundLocation();
                        // A not found location is not found for at least a whole region sized area
                        nfl.minX = pX - (pX % (double)Constants.RegionSize);
                        nfl.minY = pY - (pY % (double)Constants.RegionSize);
                        nfl.maxX = nfl.minX + (double)Constants.RegionSize;
                        nfl.maxY = nfl.minY + (double)Constants.RegionSize;
                        nfl.expireTime = DateTime.Now + TimeSpan.FromSeconds(30);
                        m_notFoundLocations.Add(nfl);
                    }
                }
                
            }
            // Test to see of this point is in any of the 'not found' areas.
            // Return 'true' if the point is found inside the 'not found' areas.
            public bool Contains(double pX, double pY)
            {
                bool ret = false;
                lock (m_notFoundLocations)
                    ret = LockedContains(pX, pY);
                return ret;
            }
            private bool LockedContains(double pX, double pY)
            {
                bool ret = false;
                this.DoExpiration();
                foreach (NotFoundLocation nfl in m_notFoundLocations)
                {
                    if (pX >= nfl.minX && pX < nfl.maxX && pY >= nfl.minY && pY < nfl.maxY)
                    {
                        ret = true;
                        break;
                    }
                }
                return ret;
            }
            private void DoExpiration()
            {
                List<NotFoundLocation> m_toRemove = null;
                DateTime now = DateTime.Now;
                foreach (NotFoundLocation nfl in m_notFoundLocations)
                {
                    if (nfl.expireTime < now)
                    {
                        if (m_toRemove == null)
                            m_toRemove = new List<NotFoundLocation>();
                        m_toRemove.Add(nfl);
                    }
                }
                if (m_toRemove != null)
                {
                    foreach (NotFoundLocation nfl in m_toRemove)
                        m_notFoundLocations.Remove(nfl);
                    m_toRemove.Clear();
                }
            }
        }
        #endregion // NotFoundLocationCache class
        private NotFoundLocationCache m_notFoundLocationCache = new NotFoundLocationCache();

        // Given a world position (fractional meter coordinate), get the GridRegion info for
        //   the region containing that point.
        // Someday this should be a method on GridService.
        // 'pSizeHint' is the size of the source region but since the destination point can be anywhere
        //     the size of the target region is unknown thus the search area might have to be very large.
        // Return 'null' if no such region exists.
        public GridRegion GetRegionContainingWorldLocation(IGridService pGridService, UUID pScopeID,
                            double px, double py, uint pSizeHint)
        {
            m_log.DebugFormat("{0} GetRegionContainingWorldLocation: query, loc=<{1},{2}>", LogHeader, px, py);
            GridRegion ret = null;
            const double fudge = 2.0;

            // One problem with this routine is negative results. That is, this can be called lots of times
            //   for regions that don't exist. m_notFoundLocationCache remembers 'not found' results so they
            //   will be quick 'not found's next time.
            // NotFoundLocationCache is an expiring cache so it will eventually forget about 'not found' and
            //   thus re-ask the GridService about the location.
            if (m_notFoundLocationCache.Contains(px, py))
            {
                m_log.DebugFormat("{0} GetRegionContainingWorldLocation: Not found via cache. loc=<{1},{2}>", LogHeader, px, py);
                return null;
            }

            // As an optimization, since most regions will be legacy sized regions (256x256), first try to get
            //   the region at the appropriate legacy region location.
            uint possibleX = (uint)Math.Floor(px);
            possibleX -= possibleX % Constants.RegionSize;
            uint possibleY = (uint)Math.Floor(py);
            possibleY -= possibleY % Constants.RegionSize;
            ret = pGridService.GetRegionByPosition(pScopeID, (int)possibleX, (int)possibleY);
            if (ret != null)
            {
                m_log.DebugFormat("{0} GetRegionContainingWorldLocation: Found region using legacy size. rloc=<{1},{2}>. Rname={3}",
                                    LogHeader, possibleX, possibleY, ret.RegionName);
            }

            if (ret == null)
            {
                // If the simple lookup failed, search the larger area for a region that contains this point
                double range = (double)pSizeHint + fudge;
                while (ret == null && range <= (Constants.MaximumRegionSize + Constants.RegionSize))
                {
                    // Get from the grid service a list of regions that might contain this point.
                    // The region origin will be in the zero direction so only subtract the range.
                    List<GridRegion> possibleRegions = pGridService.GetRegionRange(pScopeID,
                                        (int)(px - range), (int)(px),
                                        (int)(py - range), (int)(py));
                    m_log.DebugFormat("{0} GetRegionContainingWorldLocation: possibleRegions cnt={1}, range={2}",
                                        LogHeader, possibleRegions.Count, range);
                    if (possibleRegions != null && possibleRegions.Count > 0)
                    {
                        // If we found some regions, check to see if the point is within
                        foreach (GridRegion gr in possibleRegions)
                        {
                            m_log.DebugFormat("{0} GetRegionContainingWorldLocation: possibleRegion nm={1}, regionLoc=<{2},{3}>, regionSize=<{4},{5}>",
                                                LogHeader, gr.RegionName, gr.RegionLocX, gr.RegionLocY, gr.RegionSizeX, gr.RegionSizeY);
                            if (px >= (double)gr.RegionLocX && px < (double)(gr.RegionLocX + gr.RegionSizeX)
                                && py >= (double)gr.RegionLocY && py < (double)(gr.RegionLocY + gr.RegionSizeY))
                            {
                                // Found a region that contains the point
                                ret = gr;
                                m_log.DebugFormat("{0} GetRegionContainingWorldLocation: found. RegionName={1}", LogHeader, ret.RegionName);
                                break;
                            }
                        }
                    }
                    // Larger search area for next time around if not found
                    range *= 2;
                }
            }

            if (ret == null)
            {
                // remember this location was not found so we can quickly not find it next time
                m_notFoundLocationCache.Add(px, py);
                m_log.DebugFormat("{0} GetRegionContainingWorldLocation: Not found. Remembering loc=<{1},{2}>", LogHeader, px, py);
            }

            return ret;
        }

        private void InformClientOfNeighbourCompleted(IAsyncResult iar)
        {
            InformClientOfNeighbourDelegate icon = (InformClientOfNeighbourDelegate)iar.AsyncState;
            icon.EndInvoke(iar);
            //m_log.WarnFormat(" --> InformClientOfNeighbourCompleted");
        }

        /// <summary>
        /// Async component for informing client of which neighbours exist
        /// </summary>
        /// <remarks>
        /// This needs to run asynchronously, as a network timeout may block the thread for a long while
        /// </remarks>
        /// <param name="remoteClient"></param>
        /// <param name="a"></param>
        /// <param name="regionHandle"></param>
        /// <param name="endPoint"></param>
        private void InformClientOfNeighbourAsync(ScenePresence sp, AgentCircuitData a, GridRegion reg,
                                                  IPEndPoint endPoint, bool newAgent)
        {
            // Let's wait just a little to give time to originating regions to catch up with closing child agents
            // after a cross here
            Thread.Sleep(500);

            Scene scene = sp.Scene;
            
            m_log.DebugFormat(
                "[ENTITY TRANSFER MODULE]: Informing {0} {1} about neighbour {2} {3} at ({4},{5})",
                sp.Name, sp.UUID, reg.RegionName, endPoint, reg.RegionCoordX, reg.RegionCoordY);

            string capsPath = reg.ServerURI + CapsUtil.GetCapsSeedPath(a.CapsPath);

            string reason = String.Empty;

            bool regionAccepted = scene.SimulationService.CreateAgent(null, reg, a, (uint)TeleportFlags.Default, out reason);

            if (regionAccepted && newAgent)
            {
                if (m_eqModule != null)
                {
                    #region IP Translation for NAT
                    IClientIPEndpoint ipepClient;
                    if (sp.ClientView.TryGet(out ipepClient))
                    {
                        endPoint.Address = NetworkUtil.GetIPFor(ipepClient.EndPoint, endPoint.Address);
                    }
                    #endregion

                    m_log.DebugFormat("{0} {1} is sending {2} EnableSimulator for neighbour region {3}(loc=<{4},{5}>,siz=<{6},{7}>) " +
                        "and EstablishAgentCommunication with seed cap {8}", LogHeader,
                        scene.RegionInfo.RegionName, sp.Name,
                        reg.RegionName, reg.RegionLocX, reg.RegionLocY, reg.RegionSizeX, reg.RegionSizeY , capsPath);

                    m_eqModule.EnableSimulator(reg.RegionHandle, endPoint, sp.UUID, reg.RegionSizeX, reg.RegionSizeY);
                    m_eqModule.EstablishAgentCommunication(sp.UUID, endPoint, capsPath, reg.RegionHandle, reg.RegionSizeX, reg.RegionSizeY);
                }
                else
                {
                    sp.ControllingClient.InformClientOfNeighbour(reg.RegionHandle, endPoint);
                    // TODO: make Event Queue disablable!
                }

                m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Completed inform {0} {1} about neighbour {2}", sp.Name, sp.UUID, endPoint);
            }

            if (!regionAccepted)
                m_log.WarnFormat(
                    "[ENTITY TRANSFER MODULE]: Region {0} did not accept {1} {2}: {3}",
                    reg.RegionName, sp.Name, sp.UUID, reason);
        }

        /// <summary>
        /// Gets the range considered in view of this megaregion (assuming this is a megaregion).
        /// </summary>
        /// <remarks>Expressed in 256m units</remarks>
        /// <param name='swCorner'></param>
        /// <param name='neCorner'></param>
        private void GetMegaregionViewRange(out Vector2 swCorner, out Vector2 neCorner)
        {
            Vector2 extent = Vector2.Zero;

            if (m_regionCombinerModule != null)
            {
                Vector2 megaRegionSize = m_regionCombinerModule.GetSizeOfMegaregion(Scene.RegionInfo.RegionID);
                extent.X = (float)Util.WorldToRegionLoc((uint)megaRegionSize.X);
                extent.Y = (float)Util.WorldToRegionLoc((uint)megaRegionSize.Y);
            }

            swCorner.X = Scene.RegionInfo.RegionLocX - 1;
            swCorner.Y = Scene.RegionInfo.RegionLocY - 1;
            neCorner.X = Scene.RegionInfo.RegionLocX + extent.X;
            neCorner.Y = Scene.RegionInfo.RegionLocY + extent.Y;
        }

        /// <summary>
        /// Return the list of online regions that are considered to be neighbours to the given scene.
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="pRegionLocX"></param>
        /// <param name="pRegionLocY"></param>
        /// <returns></returns>        
        protected List<GridRegion> GetNeighbours(ScenePresence avatar, uint pRegionLocX, uint pRegionLocY)
        {
            Scene pScene = avatar.Scene;
            RegionInfo m_regionInfo = pScene.RegionInfo;
            List<GridRegion> neighbours;

            // Leaving this as a "megaregions" computation vs "non-megaregions" computation; it isn't
            // clear what should be done with a "far view" given that megaregions already extended the
            // view to include everything in the megaregion
            if (m_regionCombinerModule == null || !m_regionCombinerModule.IsRootForMegaregion(Scene.RegionInfo.RegionID))
            {
                // The area to check is as big as the current region.
                // We presume all adjacent regions are the same size as this region.
                uint dd = Math.Max((uint)avatar.Scene.DefaultDrawDistance, 
                                Math.Max(Scene.RegionInfo.RegionSizeX, Scene.RegionInfo.RegionSizeY));

                uint startX = Util.RegionToWorldLoc(pRegionLocX) - dd + Constants.RegionSize/2;
                uint startY = Util.RegionToWorldLoc(pRegionLocY) - dd + Constants.RegionSize/2;

                uint endX = Util.RegionToWorldLoc(pRegionLocX) + dd + Constants.RegionSize/2;
                uint endY = Util.RegionToWorldLoc(pRegionLocY) + dd + Constants.RegionSize/2;

                neighbours 
                    = avatar.Scene.GridService.GetRegionRange(
                        m_regionInfo.ScopeID, (int)startX, (int)endX, (int)startY, (int)endY);
            }
            else
            {
                Vector2 swCorner, neCorner;
                GetMegaregionViewRange(out swCorner, out neCorner);

                neighbours 
                    = pScene.GridService.GetRegionRange(
                        m_regionInfo.ScopeID, 
                        (int)Util.RegionToWorldLoc((uint)swCorner.X), (int)Util.RegionToWorldLoc((uint)neCorner.X),
                        (int)Util.RegionToWorldLoc((uint)swCorner.Y), (int)Util.RegionToWorldLoc((uint)neCorner.Y));
            }

//            neighbours.ForEach(
//                n => 
//                    m_log.DebugFormat(
//                        "[ENTITY TRANSFER MODULE]: Region flags for {0} as seen by {1} are {2}", 
//                        n.RegionName, Scene.Name, n.RegionFlags != null ? n.RegionFlags.ToString() : "not present"));

            // The r.RegionFlags == null check only needs to be made for simulators before 2015-01-14 (pre 0.8.1).
            neighbours.RemoveAll(
                r => 
                    r.RegionID == m_regionInfo.RegionID 
                        || (r.RegionFlags != null && (r.RegionFlags & OpenSim.Framework.RegionFlags.RegionOnline) == 0));

            return neighbours;
        }

        private List<ulong> NewNeighbours(List<ulong> currentNeighbours, List<ulong> previousNeighbours)
        {
            return currentNeighbours.FindAll(delegate(ulong handle) { return !previousNeighbours.Contains(handle); });
        }

        //        private List<ulong> CommonNeighbours(List<ulong> currentNeighbours, List<ulong> previousNeighbours)
        //        {
        //            return currentNeighbours.FindAll(delegate(ulong handle) { return previousNeighbours.Contains(handle); });
        //        }

        private List<ulong> OldNeighbours(List<ulong> currentNeighbours, List<ulong> previousNeighbours)
        {
            return previousNeighbours.FindAll(delegate(ulong handle) { return !currentNeighbours.Contains(handle); });
        }

        private List<ulong> NeighbourHandles(List<GridRegion> neighbours)
        {
            List<ulong> handles = new List<ulong>();
            foreach (GridRegion reg in neighbours)
            {
                handles.Add(reg.RegionHandle);
            }
            return handles;
        }

//        private void Dump(string msg, List<ulong> handles)
//        {
//            m_log.InfoFormat("-------------- HANDLE DUMP ({0}) ---------", msg);
//            foreach (ulong handle in handles)
//            {
//                uint x, y;
//                Utils.LongToUInts(handle, out x, out y);
//                x = x / Constants.RegionSize;
//                y = y / Constants.RegionSize;
//                m_log.InfoFormat("({0}, {1})", x, y);
//            }
//        }

        #endregion

        #region Agent Arrived

        public void AgentArrivedAtDestination(UUID id)
        {
            m_entityTransferStateMachine.SetAgentArrivedAtDestination(id);
        }

        #endregion

        #region Object Transfers

        /// <summary>
        /// Move the given scene object into a new region depending on which region its absolute position has moved
        /// into.
        ///
        /// Using the objects new world location, ask the grid service for a the new region and adjust the prim
        /// position to be relative to the new region.
        /// </summary>
        /// <param name="grp">the scene object that we're crossing</param>
        /// <param name="attemptedPosition">the attempted out of region position of the scene object. This position is
        /// relative to the region the object currently is in.</param>
        /// <param name="silent">if 'true', the deletion of the client from the region is not broadcast to the clients</param>
        public void Cross(SceneObjectGroup grp, Vector3 attemptedPosition, bool silent)
        {
            if (grp == null)
                return;
            if (grp.IsDeleted)
                return;

            Scene scene = grp.Scene;
            if (scene == null)
                return;

            if (grp.RootPart.DIE_AT_EDGE)
            {
                // We remove the object here
                try
                {
                    scene.DeleteSceneObject(grp, false);
                }
                catch (Exception)
                {
                    m_log.Warn("[DATABASE]: exception when trying to remove the prim that crossed the border.");
                }
                return;
            }

            // Remember the old group position in case the region lookup fails so position can be restored.
            Vector3 oldGroupPosition = grp.RootPart.GroupPosition;

            // Compute the absolute position of the object.
            double objectWorldLocX = (double)scene.RegionInfo.WorldLocX + attemptedPosition.X;
            double objectWorldLocY = (double)scene.RegionInfo.WorldLocY + attemptedPosition.Y;

            // Ask the grid service for the region that contains the passed address
            GridRegion destination = GetRegionContainingWorldLocation(scene.GridService, scene.RegionInfo.ScopeID,
                                objectWorldLocX, objectWorldLocY);

            Vector3 pos = Vector3.Zero;
            if (destination != null)
            {
                // Adjust the object's relative position from the old region (attemptedPosition)
                //    to be relative to the new region (pos).
                pos = new Vector3(  (float)(objectWorldLocX - (double)destination.RegionLocX),
                                    (float)(objectWorldLocY - (double)destination.RegionLocY),
                                    attemptedPosition.Z);
            }

            if (destination == null || !CrossPrimGroupIntoNewRegion(destination, pos, grp, silent))
            {
                m_log.InfoFormat("[ENTITY TRANSFER MODULE] cross region transfer failed for object {0}", grp.UUID);

                // We are going to move the object back to the old position so long as the old position
                // is in the region
                oldGroupPosition.X = Util.Clamp<float>(oldGroupPosition.X, 1.0f, (float)(scene.RegionInfo.RegionSizeX - 1));
                oldGroupPosition.Y = Util.Clamp<float>(oldGroupPosition.Y, 1.0f, (float)(scene.RegionInfo.RegionSizeY - 1));
                oldGroupPosition.Z = Util.Clamp<float>(oldGroupPosition.Z, 1.0f, Constants.RegionHeight);

                grp.AbsolutePosition = oldGroupPosition;
                grp.Velocity = Vector3.Zero;
                if (grp.RootPart.PhysActor != null)
                    grp.RootPart.PhysActor.CrossingFailure();

                if (grp.RootPart.KeyframeMotion != null)
                    grp.RootPart.KeyframeMotion.CrossingFailure();

                grp.ScheduleGroupForFullUpdate();
            }
        }

        /// <summary>
        /// Move the given scene object into a new region
        /// </summary>
        /// <param name="newRegionHandle"></param>
        /// <param name="grp">Scene Object Group that we're crossing</param>
        /// <returns>
        /// true if the crossing itself was successful, false on failure
        /// FIMXE: we still return true if the crossing object was not successfully deleted from the originating region
        /// </returns>
        protected bool CrossPrimGroupIntoNewRegion(GridRegion destination, Vector3 newPosition, SceneObjectGroup grp, bool silent)
        {
            //m_log.Debug("  >>> CrossPrimGroupIntoNewRegion <<<");

            bool successYN = false;
            grp.RootPart.ClearUpdateSchedule();
            //int primcrossingXMLmethod = 0;

            if (destination != null)
            {
                //string objectState = grp.GetStateSnapshot();

                //successYN
                //    = m_sceneGridService.PrimCrossToNeighboringRegion(
                //        newRegionHandle, grp.UUID, m_serialiser.SaveGroupToXml2(grp), primcrossingXMLmethod);
                //if (successYN && (objectState != "") && m_allowScriptCrossings)
                //{
                //    successYN = m_sceneGridService.PrimCrossToNeighboringRegion(
                //            newRegionHandle, grp.UUID, objectState, 100);
                //}

                //// And the new channel...
                //if (m_interregionCommsOut != null)
                //    successYN = m_interregionCommsOut.SendCreateObject(newRegionHandle, grp, true);
                if (Scene.SimulationService != null)
                    successYN = Scene.SimulationService.CreateObject(destination, newPosition, grp, true);

                if (successYN)
                {
                    // We remove the object here
                    try
                    {
                        grp.Scene.DeleteSceneObject(grp, silent);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat(
                            "[ENTITY TRANSFER MODULE]: Exception deleting the old object left behind on a border crossing for {0}, {1}",
                            grp, e);
                    }
                }
/*
 * done on caller ( not in attachments crossing for now)
                else
                {

                    if (!grp.IsDeleted)
                    {
                        PhysicsActor pa = grp.RootPart.PhysActor;
                        if (pa != null)
                        {
                            pa.CrossingFailure();
                            if (grp.RootPart.KeyframeMotion != null)
                            {
                                // moved to KeyframeMotion.CrossingFailure
//                                grp.RootPart.Velocity = Vector3.Zero;
                                grp.RootPart.KeyframeMotion.CrossingFailure();
//                                grp.SendGroupRootTerseUpdate();
                            }
                        }
                    }

                    m_log.ErrorFormat("[ENTITY TRANSFER MODULE]: Prim crossing failed for {0}", grp);
                }
 */
            }
            else
            {
                m_log.Error("[ENTITY TRANSFER MODULE]: destination was unexpectedly null in Scene.CrossPrimGroupIntoNewRegion()");
            }

            return successYN;
        }

        /// <summary>
        /// Cross the attachments for an avatar into the destination region.
        /// </summary>
        /// <remarks>
        /// This is only invoked for simulators released prior to April 2011.  Versions of OpenSimulator since then
        /// transfer attachments in one go as part of the ChildAgentDataUpdate data passed in the update agent call.
        /// </remarks>
        /// <param name='destination'></param>
        /// <param name='sp'></param>
        /// <param name='silent'></param>
        protected void CrossAttachmentsIntoNewRegion(GridRegion destination, ScenePresence sp, bool silent)
        {
            List<SceneObjectGroup> attachments = sp.GetAttachments();

//            m_log.DebugFormat(
//                "[ENTITY TRANSFER MODULE]: Crossing {0} attachments into {1} for {2}",
//                m_attachments.Count, destination.RegionName, sp.Name);

            foreach (SceneObjectGroup gobj in attachments)
            {
                // If the prim group is null then something must have happened to it!
                if (gobj != null && !gobj.IsDeleted)
                {
                    SceneObjectGroup clone = (SceneObjectGroup)gobj.CloneForNewScene();
                    clone.RootPart.GroupPosition = gobj.RootPart.AttachedPos;
                    clone.IsAttachment = false;

                    //gobj.RootPart.LastOwnerID = gobj.GetFromAssetID();
                    m_log.DebugFormat(
                        "[ENTITY TRANSFER MODULE]: Sending attachment {0} to region {1}",
                        clone.UUID, destination.RegionName);

                    CrossPrimGroupIntoNewRegion(destination, Vector3.Zero, clone, silent);
                }
            }

            sp.ClearAttachments();
        }

        #endregion

        #region Misc

        public bool IsInTransit(UUID id)
        {
            return m_entityTransferStateMachine.GetAgentTransferState(id) != null;
        }

        protected void ReInstantiateScripts(ScenePresence sp)
        {
            int i = 0;
            if (sp.InTransitScriptStates.Count > 0)
            {
                List<SceneObjectGroup> attachments = sp.GetAttachments();

                foreach (SceneObjectGroup sog in attachments)
                {
                    if (i < sp.InTransitScriptStates.Count)
                    {
                        sog.SetState(sp.InTransitScriptStates[i++], sp.Scene);
                        sog.CreateScriptInstances(0, false, sp.Scene.DefaultScriptEngine, 0);
                        sog.ResumeScripts();
                    }
                    else
                        m_log.ErrorFormat(
                            "[ENTITY TRANSFER MODULE]: InTransitScriptStates.Count={0} smaller than Attachments.Count={1}",
                            sp.InTransitScriptStates.Count, attachments.Count);
                }

                sp.InTransitScriptStates.Clear();
            }
        }
        #endregion

        public virtual bool HandleIncomingSceneObject(SceneObjectGroup so, Vector3 newPosition)
        {
            // If the user is banned, we won't let any of their objects
            // enter. Period.
            //
            if (Scene.RegionInfo.EstateSettings.IsBanned(so.OwnerID))
            {
                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Denied prim crossing of {0} {1} into {2} for banned avatar {3}", 
                    so.Name, so.UUID, Scene.Name, so.OwnerID);

                return false;
            }

            if (newPosition != Vector3.Zero)
                so.RootPart.GroupPosition = newPosition;

            if (!Scene.AddSceneObject(so))
            {
                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Problem adding scene object {0} {1} into {2} ", 
                    so.Name, so.UUID, Scene.Name);

                return false;
            }

            if (!so.IsAttachment)
            {
                // FIXME: It would be better to never add the scene object at all rather than add it and then delete
                // it
                if (!Scene.Permissions.CanObjectEntry(so.UUID, true, so.AbsolutePosition))
                {
                    // Deny non attachments based on parcel settings
                    //
                    m_log.Info("[ENTITY TRANSFER MODULE]: Denied prim crossing because of parcel settings");

                    Scene.DeleteSceneObject(so, false);

                    return false;
                }

                // For attachments, we need to wait until the agent is root
                // before we restart the scripts, or else some functions won't work.
                so.RootPart.ParentGroup.CreateScriptInstances(
                    0, false, Scene.DefaultScriptEngine, GetStateSource(so));

                so.ResumeScripts();

                if (so.RootPart.KeyframeMotion != null)
                    so.RootPart.KeyframeMotion.UpdateSceneObject(so);
            }

            return true;
        }

        private int GetStateSource(SceneObjectGroup sog)
        {
            ScenePresence sp = Scene.GetScenePresence(sog.OwnerID);

            if (sp != null)
                return sp.GetStateSource();

            return 2; // StateSource.PrimCrossing
        }
    }
}
