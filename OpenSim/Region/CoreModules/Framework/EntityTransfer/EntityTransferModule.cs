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
using OpenSim.Region.PhysicsModules.SharedBase;
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
                        if (DateTime.UtcNow < m_banUntil)
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
                this.Add(pRegionHandle, pAgentID, 45, 15);
            }

            public void Add(ulong pRegionHandle, UUID pAgentID, double newTime, double extendTime)
            {
                if (!m_bannedRegions.TryGetValue(pAgentID, out m_idCache))
                {
                    m_idCache = new ExpiringCache<ulong, DateTime>();
                    m_bannedRegions.Add(pAgentID, m_idCache, TimeSpan.FromSeconds(newTime));
                }
                m_idCache.Add(pRegionHandle, DateTime.UtcNow + TimeSpan.FromSeconds(extendTime), TimeSpan.FromSeconds(extendTime));
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

            // Check Default Location (Also See ScenePresence.CompleteMovement)
            if (position.X == 128f && position.Y == 128f && position.Z == 22.5f)
                position = sp.Scene.RegionInfo.DefaultLandingPoint;

            // TODO: Get proper AVG Height
            float localHalfAVHeight = 0.8f;
            if (sp.Appearance != null)
                localHalfAVHeight = sp.Appearance.AvatarHeight / 2;

            float posZLimit = 22;

            // TODO: Check other Scene HeightField
            posZLimit = (float)sp.Scene.Heightmap[(int)position.X, (int)position.Y];

            posZLimit += localHalfAVHeight + 0.1f;

            if ((position.Z < posZLimit) && !(Single.IsInfinity(posZLimit) || Single.IsNaN(posZLimit)))
            {
                position.Z = posZLimit;
            }

            if (sp.Flying)
                teleportFlags |= (uint)TeleportFlags.IsFlying;

            m_entityTransferStateMachine.UpdateInTransit(sp.UUID, AgentTransferState.Transferring);

            sp.ControllingClient.SendTeleportStart(teleportFlags);
            lookAt.Z = 0f;

            if(Math.Abs(lookAt.X) < 0.01f && Math.Abs(lookAt.Y) < 0.01f)
            {
                lookAt.X = 1.0f;
                lookAt.Y = 0;
            }

            sp.ControllingClient.SendLocalTeleport(position, lookAt, teleportFlags);
            sp.TeleportFlags = (Constants.TeleportFlags)teleportFlags;
            sp.RotateToLookAt(lookAt);
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
                block.X = (ushort)(regX);
                block.Y = (ushort)(regY);
                block.Access = (byte)SimAccess.Down; // == not there

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

            GridRegion reg;

            // handle legacy HG. linked regions are mapped into y = 0 and have no size information
            // so we can only search by base handle
            if( y == 0)
            {
                reg = gridService.GetRegionByPosition(scope, (int)x, (int)y);
                return reg;
            }

            // Compute the world location we're teleporting to
            double worldX = (double)x + position.X;
            double worldY = (double)y + position.Y;

            // Find the region that contains the position
            reg = GetRegionContainingWorldLocation(gridService, scope, worldX, worldY);

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
            EntityTransferContext ctx = new EntityTransferContext();

            if (!Scene.SimulationService.QueryAccess(
                finalDestination, sp.ControllingClient.AgentId, homeURI, true, position, sp.Scene.GetFormatsOffered(), ctx, out reason))
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
                "[ENTITY TRANSFER MODULE]: {0} transfer protocol version to {1} is {2} / {3}",
                sp.Scene.Name, finalDestination.RegionName, ctx.OutboundVersion, ctx.InboundVersion);

            // Fixing a bug where teleporting while sitting results in the avatar ending up removed from
            // both regions
            if (sp.ParentID != (uint)0)
                sp.StandUp();
            else if (sp.Flying)
                teleportFlags |= (uint)TeleportFlags.IsFlying;

            sp.IsInTransit = true;

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

//            agentCircuit.Appearance = sp.Appearance;
//            agentCircuit.Appearance = new AvatarAppearance(sp.Appearance, true, false);
            agentCircuit.Appearance = new AvatarAppearance();
            agentCircuit.Appearance.AvatarHeight = sp.Appearance.AvatarHeight;

            if (currentAgentCircuit != null)
            {
                agentCircuit.ServiceURLs = currentAgentCircuit.ServiceURLs;
                agentCircuit.IPAddress = currentAgentCircuit.IPAddress;
                agentCircuit.Viewer = currentAgentCircuit.Viewer;
                agentCircuit.Channel = currentAgentCircuit.Channel;
                agentCircuit.Mac = currentAgentCircuit.Mac;
                agentCircuit.Id0 = currentAgentCircuit.Id0;
            }

            IClientIPEndpoint ipepClient;

            uint newRegionX, newRegionY, oldRegionX, oldRegionY;
            Util.RegionHandleToRegionLoc(destinationHandle, out newRegionX, out newRegionY);
            Util.RegionHandleToRegionLoc(sourceRegion.RegionHandle, out oldRegionX, out oldRegionY);
            int oldSizeX = (int)sourceRegion.RegionSizeX;
            int oldSizeY = (int)sourceRegion.RegionSizeY;
            int newSizeX = finalDestination.RegionSizeX;
            int newSizeY = finalDestination.RegionSizeY;

            bool OutSideViewRange = NeedsNewAgent(sp.RegionViewDistance, oldRegionX, newRegionX, oldRegionY, newRegionY,
                oldSizeX, oldSizeY, newSizeX, newSizeY);

            if (OutSideViewRange)
            {
                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Determined that region {0} at {1},{2} size {3},{4} needs new child agent for agent {5} from {6}",
                    finalDestination.RegionName, newRegionX, newRegionY,newSizeX, newSizeY, sp.Name, Scene.Name);

                //sp.ControllingClient.SendTeleportProgress(teleportFlags, "Creating agent...");
                #region IP Translation for NAT
                // Uses ipepClient above
                if (sp.ClientView.TryGet(out ipepClient))
                {
                    endPoint.Address = NetworkUtil.GetIPFor(ipepClient.EndPoint, endPoint.Address);
                }
                #endregion
                agentCircuit.CapsPath = CapsUtil.GetRandomCapsObjectPath();
            }
            else
            {
                agentCircuit.CapsPath = sp.Scene.CapsModule.GetChildSeed(sp.UUID, reg.RegionHandle);
                if (agentCircuit.CapsPath == null)
                    agentCircuit.CapsPath = CapsUtil.GetRandomCapsObjectPath();
            }

            // We're going to fallback to V1 if the destination gives us anything smaller than 0.2
            if (ctx.OutboundVersion >= 0.2f)
                TransferAgent_V2(sp, agentCircuit, reg, finalDestination, endPoint, teleportFlags, OutSideViewRange , ctx, out reason);
            else
                TransferAgent_V1(sp, agentCircuit, reg, finalDestination, endPoint, teleportFlags, OutSideViewRange, ctx, out reason);
        }

        private void TransferAgent_V1(ScenePresence sp, AgentCircuitData agentCircuit, GridRegion reg, GridRegion finalDestination,
            IPEndPoint endPoint, uint teleportFlags, bool OutSideViewRange, EntityTransferContext ctx, out string reason)
        {
            ulong destinationHandle = finalDestination.RegionHandle;
            AgentCircuitData currentAgentCircuit = sp.Scene.AuthenticateHandler.GetAgentCircuitData(sp.ControllingClient.CircuitCode);

            m_log.DebugFormat(
                "[ENTITY TRANSFER MODULE]: Using TP V1 for {0} going from {1} to {2}",
                sp.Name, Scene.Name, finalDestination.RegionName);

            string capsPath = finalDestination.ServerURI + CapsUtil.GetCapsSeedPath(agentCircuit.CapsPath);

            // Let's create an agent there if one doesn't exist yet.
            // NOTE: logout will always be false for a non-HG teleport.
            bool logout = false;
            if (!CreateAgent(sp, reg, finalDestination, agentCircuit, teleportFlags, ctx, out reason, out logout))
            {
                m_interRegionTeleportFailures.Value++;

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Teleport of {0} from {1} to {2} was refused because {3}",
                    sp.Name, sp.Scene.RegionInfo.RegionName, finalDestination.RegionName, reason);

                sp.ControllingClient.SendTeleportFailed(reason);
                sp.IsInTransit = false;
                return;
            }

            if (m_entityTransferStateMachine.GetAgentTransferState(sp.UUID) == AgentTransferState.Cancelling)
            {
                m_interRegionTeleportCancels.Value++;

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Cancelled teleport of {0} to {1} from {2} after CreateAgent on client request",
                    sp.Name, finalDestination.RegionName, sp.Scene.Name);
                sp.IsInTransit = false;
                return;
            }
            else if (m_entityTransferStateMachine.GetAgentTransferState(sp.UUID) == AgentTransferState.Aborting)
            {
                m_interRegionTeleportAborts.Value++;

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Aborted teleport of {0} to {1} from {2} after CreateAgent due to previous client close.",
                    sp.Name, finalDestination.RegionName, sp.Scene.Name);
                sp.IsInTransit = false;
                return;
            }

            // Past this point we have to attempt clean up if the teleport fails, so update transfer state.
            m_entityTransferStateMachine.UpdateInTransit(sp.UUID, AgentTransferState.Transferring);

            // OK, it got this agent. Let's close some child agents

            if (OutSideViewRange)
            {
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

            // Let's send a full update of the agent. This is a synchronous call.
            AgentData agent = new AgentData();
            sp.CopyTo(agent,false);

            if ((teleportFlags & (uint)TeleportFlags.IsFlying) != 0)
                agent.ControlFlags |= (uint)AgentManager.ControlFlags.AGENT_CONTROL_FLY;

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
                sp.IsInTransit = false;
                return;
            }

            // A common teleport failure occurs when we can send CreateAgent to the
            // destination region but the viewer cannot establish the connection (e.g. due to network issues between
            // the viewer and the destination).  In this case, UpdateAgent timesout after 10 seconds, although then
            // there's a further 10 second wait whilst we attempt to tell the destination to delete the agent in Fail().
            if (!UpdateAgent(reg, finalDestination, agent, sp, ctx))
            {
                if (m_entityTransferStateMachine.GetAgentTransferState(sp.UUID) == AgentTransferState.Aborting)
                {
                    m_interRegionTeleportAborts.Value++;

                    m_log.DebugFormat(
                        "[ENTITY TRANSFER MODULE]: Aborted teleport of {0} to {1} from {2} after UpdateAgent due to previous client close.",
                        sp.Name, finalDestination.RegionName, sp.Scene.Name);
                    sp.IsInTransit = false;
                    return;
                }

                m_log.WarnFormat(
                    "[ENTITY TRANSFER MODULE]: UpdateAgent failed on teleport of {0} to {1}.  Keeping avatar in {2}",
                    sp.Name, finalDestination.RegionName, sp.Scene.Name);

                Fail(sp, finalDestination, logout, currentAgentCircuit.SessionID.ToString(), "Connection between viewer and destination region could not be established.");
                sp.IsInTransit = false;
                return;
            }

            if (m_entityTransferStateMachine.GetAgentTransferState(sp.UUID) == AgentTransferState.Cancelling)
            {
                m_interRegionTeleportCancels.Value++;

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Cancelled teleport of {0} to {1} from {2} after UpdateAgent on client request",
                    sp.Name, finalDestination.RegionName, sp.Scene.Name);

                CleanupFailedInterRegionTeleport(sp, currentAgentCircuit.SessionID.ToString(), finalDestination);
                sp.IsInTransit = false;
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
                    sp.IsInTransit = false;
                    return;
                }

                m_log.WarnFormat(
                    "[ENTITY TRANSFER MODULE]: Teleport of {0} to {1} from {2} failed due to no callback from destination region.  Returning avatar to source region.",
                    sp.Name, finalDestination.RegionName, sp.Scene.RegionInfo.RegionName);

                Fail(sp, finalDestination, logout, currentAgentCircuit.SessionID.ToString(), "Destination region did not signal teleport completion.");
                sp.IsInTransit = false;
                return;
            }


/*
            // TODO: This may be 0.6. Check if still needed
            // For backwards compatibility
            if (version == 0f)
            {
                // CrossAttachmentsIntoNewRegion is a synchronous call. We shouldn't need to wait after it
                m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Old simulator, sending attachments one by one...");
                CrossAttachmentsIntoNewRegion(finalDestination, sp, true);
            }
*/

            m_entityTransferStateMachine.UpdateInTransit(sp.UUID, AgentTransferState.CleaningUp);

            sp.CloseChildAgents(logout, destinationHandle, finalDestination.RegionSizeX, finalDestination.RegionSizeY);

            // call HG hook
            AgentHasMovedAway(sp, logout);

            sp.HasMovedAway(!(OutSideViewRange || logout));

//            ulong sourceRegionHandle = sp.RegionHandle;

             // Now let's make it officially a child agent
            sp.MakeChildAgent(destinationHandle);

            // Finally, let's close this previously-known-as-root agent, when the jump is outside the view zone

            if (NeedsClosing(reg, OutSideViewRange))
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
//                if (m_eqModule != null && !sp.DoNotCloseAfterTeleport)
//                    m_eqModule.DisableSimulator(sourceRegionHandle,sp.UUID);
                Thread.Sleep(500);
                sp.Scene.CloseAgent(sp.UUID, false);
            }
            sp.IsInTransit = false;
        }

        private void TransferAgent_V2(ScenePresence sp, AgentCircuitData agentCircuit, GridRegion reg, GridRegion finalDestination,
            IPEndPoint endPoint, uint teleportFlags, bool OutSideViewRange, EntityTransferContext ctx, out string reason)
        {
            ulong destinationHandle = finalDestination.RegionHandle;
            AgentCircuitData currentAgentCircuit = sp.Scene.AuthenticateHandler.GetAgentCircuitData(sp.ControllingClient.CircuitCode);

            string capsPath = finalDestination.ServerURI + CapsUtil.GetCapsSeedPath(agentCircuit.CapsPath);;

            // Let's create an agent there if one doesn't exist yet.
            // NOTE: logout will always be false for a non-HG teleport.
            bool logout = false;
            if (!CreateAgent(sp, reg, finalDestination, agentCircuit, teleportFlags, ctx, out reason, out logout))
            {
                m_interRegionTeleportFailures.Value++;

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Teleport of {0} from {1} to {2} was refused because {3}",
                    sp.Name, sp.Scene.RegionInfo.RegionName, finalDestination.RegionName, reason);

                sp.ControllingClient.SendTeleportFailed(reason);
                sp.IsInTransit = false;
                return;
            }

            if (m_entityTransferStateMachine.GetAgentTransferState(sp.UUID) == AgentTransferState.Cancelling)
            {
                m_interRegionTeleportCancels.Value++;

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Cancelled teleport of {0} to {1} from {2} after CreateAgent on client request",
                    sp.Name, finalDestination.RegionName, sp.Scene.Name);

                sp.IsInTransit = false;
                return;
            }
            else if (m_entityTransferStateMachine.GetAgentTransferState(sp.UUID) == AgentTransferState.Aborting)
            {
                m_interRegionTeleportAborts.Value++;

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Aborted teleport of {0} to {1} from {2} after CreateAgent due to previous client close.",
                    sp.Name, finalDestination.RegionName, sp.Scene.Name);

                sp.IsInTransit = false;
                return;
            }

            // Past this point we have to attempt clean up if the teleport fails, so update transfer state.
            m_entityTransferStateMachine.UpdateInTransit(sp.UUID, AgentTransferState.Transferring);

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
            sp.CopyTo(agent,false);
            agent.Position = agentCircuit.startpos;

            if ((teleportFlags & (uint)TeleportFlags.IsFlying) != 0)
                agent.ControlFlags |= (uint)AgentManager.ControlFlags.AGENT_CONTROL_FLY;

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
            if (!UpdateAgent(reg, finalDestination, agent, sp, ctx))
            {
                if (m_entityTransferStateMachine.GetAgentTransferState(sp.UUID) == AgentTransferState.Aborting)
                {
                    m_interRegionTeleportAborts.Value++;

                    m_log.DebugFormat(
                        "[ENTITY TRANSFER MODULE]: Aborted teleport of {0} to {1} from {2} after UpdateAgent due to previous client close.",
                        sp.Name, finalDestination.RegionName, sp.Scene.Name);
                    sp.IsInTransit = false;
                    return;
                }

                m_log.WarnFormat(
                    "[ENTITY TRANSFER MODULE]: UpdateAgent failed on teleport of {0} to {1}.  Keeping avatar in {2}",
                    sp.Name, finalDestination.RegionName, sp.Scene.Name);

                Fail(sp, finalDestination, logout, currentAgentCircuit.SessionID.ToString(), "Connection between viewer and destination region could not be established.");
                sp.IsInTransit = false;
                return;
            }

            m_entityTransferStateMachine.UpdateInTransit(sp.UUID, AgentTransferState.CleaningUp);

            // Need to signal neighbours whether child agents may need closing irrespective of whether this
            // one needed closing.  We also need to close child agents as quickly as possible to avoid complicated
            // race conditions with rapid agent releporting (e.g. from A1 to a non-neighbour B, back
            // to a neighbour A2 then off to a non-neighbour C).  Closing child agents any later requires complex
            // distributed checks to avoid problems in rapid reteleporting scenarios and where child agents are
            // abandoned without proper close by viewer but then re-used by an incoming connection.
            sp.CloseChildAgents(logout, destinationHandle, finalDestination.RegionSizeX, finalDestination.RegionSizeY);

            sp.HasMovedAway(!(OutSideViewRange || logout));

            //HG hook
            AgentHasMovedAway(sp, logout);

//            ulong sourceRegionHandle = sp.RegionHandle;

            // Now let's make it officially a child agent
            sp.MakeChildAgent(destinationHandle);

            // Finally, let's close this previously-known-as-root agent, when the jump is outside the view zone
            // go by HG hook
            if (NeedsClosing(reg, OutSideViewRange))
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
//                if (m_eqModule != null && !sp.DoNotCloseAfterTeleport)
//                    m_eqModule.DisableSimulator(sourceRegionHandle,sp.UUID);
//                Thread.Sleep(1000);

                // OK, it got this agent. Let's close everything
                // If we shouldn't close the agent due to some other region renewing the connection
                // then this will be handled in IncomingCloseAgent under lock conditions
                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Closing agent {0} in {1} after teleport", sp.Name, Scene.Name);

                sp.Scene.CloseAgent(sp.UUID, false);
            }
/*
            else
            {
                // now we have a child agent in this region.
                sp.Reset();
            }
 */
            sp.IsInTransit = false;
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

        protected virtual bool CreateAgent(ScenePresence sp, GridRegion reg, GridRegion finalDestination, AgentCircuitData agentCircuit, uint teleportFlags, EntityTransferContext ctx, out string reason, out bool logout)
        {
            GridRegion source = new GridRegion(Scene.RegionInfo);
            source.RawServerURI = m_GatekeeperURI;

            logout = false;
            bool success = Scene.SimulationService.CreateAgent(source, finalDestination, agentCircuit, teleportFlags, ctx, out reason);

            if (success)
                sp.Scene.EventManager.TriggerTeleportStart(sp.ControllingClient, reg, finalDestination, teleportFlags, logout);

            return success;
        }

        protected virtual bool UpdateAgent(GridRegion reg, GridRegion finalDestination, AgentData agent, ScenePresence sp, EntityTransferContext ctx)
        {
            return Scene.SimulationService.UpdateAgent(finalDestination, agent, ctx);
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
        ///
        /// now just a HG hook
        protected virtual void AgentHasMovedAway(ScenePresence sp, bool logout)
        {
//            if (sp.Scene.AttachmentsModule != null)
//                sp.Scene.AttachmentsModule.DeleteAttachmentsFromScene(sp, logout);
        }

        protected void KillEntity(Scene scene, uint localID)
        {
            scene.SendKillObject(new List<uint> { localID });
        }

        // HG hook
        protected virtual GridRegion GetFinalDestination(GridRegion region, UUID agentID, string agentHomeURI, out string message)
        {
            message = null;
            return region;
        }

        // This returns 'true' if the new region already has a child agent for our
        //    incoming agent. The implication is that, if 'false', we have to create  the
        //    child and then teleport into the region.
        protected virtual bool NeedsNewAgent(float viewdist, uint oldRegionX, uint newRegionX, uint oldRegionY, uint newRegionY,
            int oldsizeX, int oldsizeY, int newsizeX, int newsizeY)
        {
            return Util.IsOutsideView(viewdist, oldRegionX, newRegionX, oldRegionY, newRegionY,
                    oldsizeX, oldsizeY, newsizeX, newsizeY);
        }

        // HG Hook
        protected virtual bool NeedsClosing(GridRegion reg, bool OutViewRange)

        {
            return OutViewRange;
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

        public bool checkAgentAccessToRegion(ScenePresence agent, GridRegion destiny, Vector3 position,
                EntityTransferContext ctx, out string reason)
        {
            reason = String.Empty;

            UUID agentID = agent.UUID;
            ulong destinyHandle = destiny.RegionHandle;

            if (m_bannedRegionCache.IfBanned(destinyHandle, agentID))
            {
                return false;
            }

            Scene ascene = agent.Scene;
            string homeURI = ascene.GetAgentHomeURI(agentID);


            if (!ascene.SimulationService.QueryAccess(destiny, agentID, homeURI, false, position,
                   agent.Scene.GetFormatsOffered(), ctx, out reason))
            {
                m_bannedRegionCache.Add(destinyHandle, agentID, 30.0, 30.0);
                return false;
            }
            return true;
        }


        // Given a position relative to the current region and outside of it
        // find the new region that the point is actually in.
        // returns 'null' if new region not found or if information
        // and new position relative to it
        // now only works for crossings

        public GridRegion GetDestination(Scene scene, UUID agentID, Vector3 pos,
                                            EntityTransferContext ctx, out Vector3 newpos, out string failureReason)
        {
            newpos = pos;
            failureReason = string.Empty;

//            m_log.DebugFormat(
//                "[ENTITY TRANSFER MODULE]: Crossing agent {0} at pos {1} in {2}", agent.Name, pos, scene.Name);

            // Compute world location of the agent's position
            double presenceWorldX = (double)scene.RegionInfo.WorldLocX + pos.X;
            double presenceWorldY = (double)scene.RegionInfo.WorldLocY + pos.Y;

            // Call the grid service to lookup the region containing the new position.
            GridRegion neighbourRegion = GetRegionContainingWorldLocation(
                                scene.GridService, scene.RegionInfo.ScopeID,
                                presenceWorldX, presenceWorldY,
                                Math.Max(scene.RegionInfo.RegionSizeX, scene.RegionInfo.RegionSizeY));

            if (neighbourRegion == null)
            {
                return null;
            }
            if (m_bannedRegionCache.IfBanned(neighbourRegion.RegionHandle, agentID))
            {
                return null;
            }

            m_bannedRegionCache.Remove(neighbourRegion.RegionHandle, agentID);

            // Compute the entity's position relative to the new region
            newpos = new Vector3((float)(presenceWorldX - (double)neighbourRegion.RegionLocX),
                                      (float)(presenceWorldY - (double)neighbourRegion.RegionLocY),
                                      pos.Z);

            string homeURI = scene.GetAgentHomeURI(agentID);

            if (!scene.SimulationService.QueryAccess(
                    neighbourRegion, agentID, homeURI, false, newpos,
                    scene.GetFormatsOffered(), ctx, out failureReason))
            {
                // remember the fail
                m_bannedRegionCache.Add(neighbourRegion.RegionHandle, agentID);
                return null;
            }

            return neighbourRegion;
        }

        public bool Cross(ScenePresence agent, bool isFlying)
        {
            agent.IsInTransit = true;
            CrossAsyncDelegate d = CrossAsync;
            d.BeginInvoke(agent, isFlying, CrossCompleted, d);
            return true;
        }

        private void CrossCompleted(IAsyncResult iar)
        {
            CrossAsyncDelegate icon = (CrossAsyncDelegate)iar.AsyncState;
            ScenePresence agent = icon.EndInvoke(iar);

            m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Crossing agent {0} {1} completed.", agent.Firstname, agent.Lastname);

            if(!agent.IsChildAgent)
            {
                // crossing failed
                agent.CrossToNewRegionFail();
            }
            agent.IsInTransit = false;
        }

        public ScenePresence CrossAsync(ScenePresence agent, bool isFlying)
        {
            Vector3 newpos;
            EntityTransferContext ctx = new EntityTransferContext();
            string failureReason;

            // We need this because of decimal number parsing of the protocols.
            Culture.SetCurrentCulture();

            Vector3 pos = agent.AbsolutePosition + agent.Velocity * 0.2f;

            GridRegion neighbourRegion = GetDestination(agent.Scene, agent.UUID, pos,
                                                            ctx, out newpos, out failureReason);
            if (neighbourRegion == null)
            {
                if (failureReason != String.Empty)
                    agent.ControllingClient.SendAlertMessage(failureReason);
                return agent;
            }

//            agent.IsInTransit = true;

            CrossAgentToNewRegionAsync(agent, newpos, neighbourRegion, isFlying, ctx);
            agent.IsInTransit = false;
            return agent;
        }

        public delegate void InformClientToInitiateTeleportToLocationDelegate(ScenePresence agent, uint regionX,            uint regionY, Vector3 position, Scene initiatingScene);

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
                Util.RegionGridLocToHandle(regionX, regionY),
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



        /// <summary>
        /// This Closes child agents on neighbouring regions
        /// Calls an asynchronous method to do so..  so it doesn't lag the sim.
        /// </summary>
        public ScenePresence CrossAgentToNewRegionAsync(
                                ScenePresence agent, Vector3 pos, GridRegion neighbourRegion,
                                bool isFlying, EntityTransferContext ctx)
        {
            try
            {
                m_log.DebugFormat("{0}: CrossAgentToNewRegionAsync: new region={1} at <{2},{3}>. newpos={4}",
                            LogHeader, neighbourRegion.RegionName, neighbourRegion.RegionLocX, neighbourRegion.RegionLocY, pos);

                if (neighbourRegion == null)
                {
                    m_log.DebugFormat("{0}: CrossAgentToNewRegionAsync: invalid destiny", LogHeader);
                    return agent;
                }

                m_entityTransferStateMachine.SetInTransit(agent.UUID);
                agent.RemoveFromPhysicalScene();

                if (!CrossAgentIntoNewRegionMain(agent, pos, neighbourRegion, isFlying, ctx))
                {
                    m_log.DebugFormat("{0}: CrossAgentToNewRegionAsync: cross main failed. Resetting transfer state", LogHeader);
                    m_entityTransferStateMachine.ResetFromTransit(agent.UUID);
                    return agent;
                }

                CrossAgentToNewRegionPost(agent, pos, neighbourRegion, isFlying, ctx);
            }
            catch (Exception e)
            {
                m_log.Error(string.Format("{0}: CrossAgentToNewRegionAsync: failed with exception  ", LogHeader), e);
            }

            return agent;
        }

        public bool CrossAgentIntoNewRegionMain(ScenePresence agent, Vector3 pos, GridRegion neighbourRegion, bool isFlying, EntityTransferContext ctx)
        {
            int ts = Util.EnvironmentTickCount();
            try
            {
                AgentData cAgent = new AgentData();
                agent.CopyTo(cAgent,true);

//                agent.Appearance.WearableCacheItems = null;

                cAgent.Position = pos;
                cAgent.ChildrenCapSeeds = agent.KnownRegions;

                if (isFlying)
                    cAgent.ControlFlags |= (uint)AgentManager.ControlFlags.AGENT_CONTROL_FLY;

                // We don't need the callback anymnore
                cAgent.CallbackURI = String.Empty;

                // Beyond this point, extra cleanup is needed beyond removing transit state
                m_entityTransferStateMachine.UpdateInTransit(agent.UUID, AgentTransferState.Transferring);

                if (!agent.Scene.SimulationService.UpdateAgent(neighbourRegion, cAgent, ctx))
                {
                    // region doesn't take it
                    m_entityTransferStateMachine.UpdateInTransit(agent.UUID, AgentTransferState.CleaningUp);

                    m_log.WarnFormat(
                        "[ENTITY TRANSFER MODULE]: Region {0} would not accept update for agent {1} on cross attempt.  Returning to original region.",
                        neighbourRegion.RegionName, agent.Name);

                    ReInstantiateScripts(agent);
                    if(agent.ParentID == 0 && agent.ParentUUID == UUID.Zero)
                        agent.AddToPhysicalScene(isFlying);

                    return false;
                }

            m_log.DebugFormat("[CrossAgentIntoNewRegionMain] ok, time {0}ms",Util.EnvironmentTickCountSubtract(ts));

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
            bool isFlying, EntityTransferContext ctx)
        {

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
                    neighbourRegion.RegionHandle, pos, vel2 /* agent.Velocity */,
                    neighbourRegion.ExternalEndPoint,
                    capsPath, agent.UUID, agent.ControllingClient.SessionId,
                    neighbourRegion.RegionSizeX, neighbourRegion.RegionSizeY);
            }
            else
            {
                m_log.ErrorFormat("{0} Using old CrossRegion packet. Varregion will not work!!", LogHeader);
                agent.ControllingClient.CrossRegion(neighbourRegion.RegionHandle, pos, agent.Velocity, neighbourRegion.ExternalEndPoint,
                                            capsPath);
            }

            // SUCCESS!
            m_entityTransferStateMachine.UpdateInTransit(agent.UUID, AgentTransferState.ReceivedAtDestination);

            // Unlike a teleport, here we do not wait for the destination region to confirm the receipt.
            m_entityTransferStateMachine.UpdateInTransit(agent.UUID, AgentTransferState.CleaningUp);

            agent.CloseChildAgents(false, neighbourRegion.RegionHandle, neighbourRegion.RegionSizeX, neighbourRegion.RegionSizeY);

            // this may need the attachments

            agent.HasMovedAway(true);

            agent.MakeChildAgent(neighbourRegion.RegionHandle);

            // FIXME: Possibly this should occur lower down after other commands to close other agents,
            // but not sure yet what the side effects would be.
            m_entityTransferStateMachine.ResetFromTransit(agent.UUID);

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

//            m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Crossing agent {0} {1} completed.", agent.Firstname, agent.Lastname);
        }

        #endregion

        #region Enable Child Agent

        /// <summary>
        /// This informs a single neighbouring region about agent "avatar", and avatar about it
        /// Calls an asynchronous method to do so..  so it doesn't lag the sim.
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="region"></param>
        public void EnableChildAgent(ScenePresence sp, GridRegion region)
        {
            m_log.DebugFormat("[ENTITY TRANSFER]: Enabling child agent in new neighbour {0}", region.RegionName);

            ulong currentRegionHandler = sp.Scene.RegionInfo.RegionHandle;
            ulong regionhandler = region.RegionHandle;

            Dictionary<ulong, string> seeds = new Dictionary<ulong, string>(sp.Scene.CapsModule.GetChildrenSeeds(sp.UUID));

            if (seeds.ContainsKey(regionhandler))
                seeds.Remove(regionhandler);
/*
            List<ulong> oldregions = new List<ulong>(seeds.Keys);

            if (oldregions.Contains(currentRegionHandler))
                oldregions.Remove(currentRegionHandler);
*/
            if (!seeds.ContainsKey(currentRegionHandler))
                seeds.Add(currentRegionHandler, sp.ControllingClient.RequestClientInfo().CapsPath);

            AgentCircuitData currentAgentCircuit = sp.Scene.AuthenticateHandler.GetAgentCircuitData(sp.ControllingClient.CircuitCode);
            AgentCircuitData agent = sp.ControllingClient.RequestClientInfo();
            agent.BaseFolder = UUID.Zero;
            agent.InventoryFolder = UUID.Zero;
            agent.startpos = sp.AbsolutePosition + CalculateOffset(sp, region);
            agent.child = true;
            agent.Appearance = new AvatarAppearance();
            agent.Appearance.AvatarHeight = sp.Appearance.AvatarHeight;

            agent.CapsPath = CapsUtil.GetRandomCapsObjectPath();

            seeds.Add(regionhandler, agent.CapsPath);


//            agent.ChildrenCapSeeds = new Dictionary<ulong, string>(seeds);
            agent.ChildrenCapSeeds = null;

            if (sp.Scene.CapsModule != null)
            {
                sp.Scene.CapsModule.SetChildrenSeed(sp.UUID, seeds);
            }

            sp.KnownRegions = seeds;
            sp.AddNeighbourRegionSizeInfo(region);

            if (currentAgentCircuit != null)
            {
                agent.ServiceURLs = currentAgentCircuit.ServiceURLs;
                agent.IPAddress = currentAgentCircuit.IPAddress;
                agent.Viewer = currentAgentCircuit.Viewer;
                agent.Channel = currentAgentCircuit.Channel;
                agent.Mac = currentAgentCircuit.Mac;
                agent.Id0 = currentAgentCircuit.Id0;
            }
/*
            AgentPosition agentpos = null;

            if (oldregions.Count > 0)
            {
                agentpos = new AgentPosition();
                agentpos.AgentID = new UUID(sp.UUID.Guid);
                agentpos.SessionID = sp.ControllingClient.SessionId;
                agentpos.Size = sp.Appearance.AvatarSize;
                agentpos.Center = sp.CameraPosition;
                agentpos.Far = sp.DrawDistance;
                agentpos.Position = sp.AbsolutePosition;
                agentpos.Velocity = sp.Velocity;
                agentpos.RegionHandle = currentRegionHandler;
                agentpos.Throttles = sp.ControllingClient.GetThrottlesPacked(1);
                agentpos.ChildrenCapSeeds = seeds;
            }
*/
            IPEndPoint external = region.ExternalEndPoint;
            if (external != null)
            {
                InformClientOfNeighbourDelegate d = InformClientOfNeighbourAsync;
                d.BeginInvoke(sp, agent, region, external, true,
                          InformClientOfNeighbourCompleted,
                          d);
            }
/*
            if(oldregions.Count >0)
            {
                uint neighbourx;
                uint neighboury;
                UUID scope = sp.Scene.RegionInfo.ScopeID;
                foreach (ulong handler in oldregions)
                {
                    Utils.LongToUInts(handler, out neighbourx, out neighboury);
                    GridRegion neighbour = sp.Scene.GridService.GetRegionByPosition(scope, (int)neighbourx, (int)neighboury);
                    sp.Scene.SimulationService.UpdateAgent(neighbour, agentpos);
                }
            }
 */
        }

        #endregion

        #region Enable Child Agents

        private delegate void InformClientOfNeighbourDelegate(
            ScenePresence avatar, AgentCircuitData a, GridRegion reg, IPEndPoint endPoint, bool newAgent);

        /// <summary>
        /// This informs all neighbouring regions about agent "avatar".
        /// and as important informs the avatar about then
        /// </summary>
        /// <param name="sp"></param>
        public void EnableChildAgents(ScenePresence sp)
        {
            // assumes that out of view range regions are disconnected by the previus region

            List<GridRegion> neighbours = new List<GridRegion>();
            Scene spScene = sp.Scene;
            RegionInfo m_regionInfo = spScene.RegionInfo;

            if (m_regionInfo != null)
            {
                neighbours = GetNeighbors(sp, m_regionInfo.RegionLocX, m_regionInfo.RegionLocY);
            }
            else
            {
                m_log.Debug("[ENTITY TRANSFER MODULE]: m_regionInfo was null in EnableChildAgents, is this a NPC?");
            }

            ulong currentRegionHandler = m_regionInfo.RegionHandle;

            LinkedList<ulong> previousRegionNeighbourHandles;
            Dictionary<ulong, string> seeds;
            ICapabilitiesModule capsModule = spScene.CapsModule;

            if (capsModule != null)
            {
                seeds = new Dictionary<ulong, string>(capsModule.GetChildrenSeeds(sp.UUID));
                previousRegionNeighbourHandles = new LinkedList<ulong>(seeds.Keys);
            }
            else
            {
                seeds = new Dictionary<ulong, string>();
                previousRegionNeighbourHandles = new LinkedList<ulong>();
            }

            IClientAPI spClient = sp.ControllingClient;

            // This will fail if the user aborts login
            try
            {
                if (!seeds.ContainsKey(currentRegionHandler))
                    seeds.Add(currentRegionHandler, spClient.RequestClientInfo().CapsPath);
            }
            catch
            {
                return;
            }

            AgentCircuitData currentAgentCircuit =
                spScene.AuthenticateHandler.GetAgentCircuitData(sp.ControllingClient.CircuitCode);

            List<AgentCircuitData> cagents = new List<AgentCircuitData>();
            List<ulong> newneighbours = new List<ulong>();

            foreach (GridRegion neighbour in neighbours)
            {
                ulong handler = neighbour.RegionHandle;

                if (previousRegionNeighbourHandles.Contains(handler))
                {
                    // agent already knows this region
                    previousRegionNeighbourHandles.Remove(handler);
                    continue;
                }

                if (handler == currentRegionHandler)
                    continue;

                // a new region to add
                AgentCircuitData agent = spClient.RequestClientInfo();
                agent.BaseFolder = UUID.Zero;
                agent.InventoryFolder = UUID.Zero;
                agent.startpos = sp.AbsolutePosition + CalculateOffset(sp, neighbour);
                agent.child = true;
                agent.Appearance = new AvatarAppearance();
                agent.Appearance.AvatarHeight = sp.Appearance.AvatarHeight;

                if (currentAgentCircuit != null)
                {
                    agent.ServiceURLs = currentAgentCircuit.ServiceURLs;
                    agent.IPAddress = currentAgentCircuit.IPAddress;
                    agent.Viewer = currentAgentCircuit.Viewer;
                    agent.Channel = currentAgentCircuit.Channel;
                    agent.Mac = currentAgentCircuit.Mac;
                    agent.Id0 = currentAgentCircuit.Id0;
                }

                newneighbours.Add(handler);
                agent.CapsPath = CapsUtil.GetRandomCapsObjectPath();
                seeds.Add(handler, agent.CapsPath);

                agent.ChildrenCapSeeds = null;
                cagents.Add(agent);
            }

            if (previousRegionNeighbourHandles.Contains(currentRegionHandler))
                previousRegionNeighbourHandles.Remove(currentRegionHandler);

            // previousRegionNeighbourHandles now contains regions to forget
            foreach (ulong handler in previousRegionNeighbourHandles)
                seeds.Remove(handler);

            /// Update all child agent with everyone's seeds
            //            foreach (AgentCircuitData a in cagents)
            //                a.ChildrenCapSeeds = new Dictionary<ulong, string>(seeds);

            if (capsModule != null)
                capsModule.SetChildrenSeed(sp.UUID, seeds);

            sp.KnownRegions = seeds;
            sp.SetNeighbourRegionSizeInfo(neighbours);

            if(newneighbours.Count > 0 || previousRegionNeighbourHandles.Count > 0)
            {
                AgentPosition agentpos = new AgentPosition();
                agentpos.AgentID = new UUID(sp.UUID.Guid);
                agentpos.SessionID = spClient.SessionId;
                agentpos.Size = sp.Appearance.AvatarSize;
                agentpos.Center = sp.CameraPosition;
                agentpos.Far = sp.DrawDistance;
                agentpos.Position = sp.AbsolutePosition;
                agentpos.Velocity = sp.Velocity;
                agentpos.RegionHandle = currentRegionHandler;
                //agentpos.GodLevel = sp.GodLevel;
                agentpos.GodData = sp.GodController.State();
                agentpos.Throttles = spClient.GetThrottlesPacked(1);
                //            agentpos.ChildrenCapSeeds = seeds;

                Util.FireAndForget(delegate
                {
                    Thread.Sleep(200);  // the original delay that was at InformClientOfNeighbourAsync start
                    int count = 0;

                    foreach (GridRegion neighbour in neighbours)
                    {
                        ulong handler = neighbour.RegionHandle;
                        try
                        {
                            if (newneighbours.Contains(handler))
                            {
                                InformClientOfNeighbourAsync(sp, cagents[count], neighbour,
                                    neighbour.ExternalEndPoint, true);
                                count++;
                            }
                            else if (!previousRegionNeighbourHandles.Contains(handler))
                            {
                                spScene.SimulationService.UpdateAgent(neighbour, agentpos);
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat(
                                "[ENTITY TRANSFER MODULE]: Error creating child agent at {0} ({1} ({2}, {3}).  {4}",
                                neighbour.ExternalHostName,
                                neighbour.RegionHandle,
                                neighbour.RegionLocX,
                                neighbour.RegionLocY,
                                e);
                        }
                    }
                });
            }
        }

        // Computes the difference between two region bases.
        // Returns a vector of world coordinates (meters) from base of first region to the second.
        // The first region is the home region of the passed scene presence.
        Vector3 CalculateOffset(ScenePresence sp, GridRegion neighbour)
        {
              return new Vector3(sp.Scene.RegionInfo.WorldLocX - neighbour.RegionLocX,
                                sp.Scene.RegionInfo.WorldLocY - neighbour.RegionLocY,
                                0f);
        }


        #region NotFoundLocationCache class
        // A collection of not found locations to make future lookups 'not found' lookups quick.
        // A simple expiring cache that keeps not found locations for some number of seconds.
        // A 'not found' location is presumed to be anywhere in the minimum sized region that
        //    contains that point. A conservitive estimate.
        private class NotFoundLocationCache
        {
            private Dictionary<ulong, DateTime> m_notFoundLocations = new Dictionary<ulong, DateTime>();
            public NotFoundLocationCache()
            {
            }
            // just use normal regions handlers and sizes
            public void Add(double pX, double pY)
            {
                ulong psh = (ulong)pX & 0xffffff00ul;
                psh <<= 32;
                psh |= (ulong)pY & 0xffffff00ul;

                lock (m_notFoundLocations)
                    m_notFoundLocations[psh] = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            }
            // Test to see of this point is in any of the 'not found' areas.
            // Return 'true' if the point is found inside the 'not found' areas.
            public bool Contains(double pX, double pY)
            {
                ulong psh = (ulong)pX & 0xffffff00ul;
                psh <<= 32;
                psh |= (ulong)pY & 0xffffff00ul;

                lock (m_notFoundLocations)
                {
                    if(m_notFoundLocations.ContainsKey(psh))
                    {
                        if(m_notFoundLocations[psh] > DateTime.UtcNow)
                            return true;
                        m_notFoundLocations.Remove(psh);
                    }
                    return false;
                }
            }

            private void DoExpiration()
            {
                List<ulong> m_toRemove = new List<ulong>();;
                DateTime now = DateTime.UtcNow;
                lock (m_notFoundLocations)
                {
                    foreach (KeyValuePair<ulong, DateTime> kvp in m_notFoundLocations)
                    {
                        if (kvp.Value < now)
                            m_toRemove.Add(kvp.Key);
                    }

                    if (m_toRemove.Count > 0)
                    {
                        foreach (ulong u in m_toRemove)
                            m_notFoundLocations.Remove(u);
                        m_toRemove.Clear();
                    }
                }
            }
        }

        #endregion // NotFoundLocationCache class
        private NotFoundLocationCache m_notFoundLocationCache = new NotFoundLocationCache();

        protected GridRegion GetRegionContainingWorldLocation(IGridService pGridService, UUID pScopeID, double px, double py)
        {
            // Since we don't know how big the regions could be, we have to search a very large area
            //    to find possible regions.
            return GetRegionContainingWorldLocation(pGridService, pScopeID, px, py, Constants.MaximumRegionSize);
        }

        // Given a world position, get the GridRegion info for
        //   the region containing that point.
        // for compatibility with old grids it does a scan to find large regions
        // 0.9 grids to that

        protected GridRegion GetRegionContainingWorldLocation(IGridService pGridService, UUID pScopeID,
                            double px, double py, uint pSizeHint)
        {
            m_log.DebugFormat("{0} GetRegionContainingWorldLocation: call, XY=<{1},{2}>", LogHeader, px, py);
            GridRegion ret = null;
            const double fudge = 2.0;

            if (m_notFoundLocationCache.Contains(px, py))
            {
//                m_log.DebugFormat("{0} GetRegionContainingWorldLocation: Not found via cache. loc=<{1},{2}>", LogHeader, px, py);
                return null;
            }

            // As an optimization, since most regions will be legacy sized regions (256x256), first try to get
            //   the region at the appropriate legacy region location.
            // this is all that is needed on 0.9 grids
            uint possibleX = (uint)px & 0xffffff00u;
            uint possibleY = (uint)py & 0xffffff00u;
            ret = pGridService.GetRegionByPosition(pScopeID, (int)possibleX, (int)possibleY);
            if (ret != null)
            {
//                m_log.DebugFormat("{0} GetRegionContainingWorldLocation: Found region using legacy size. rloc=<{1},{2}>. Rname={3}",
//                                    LogHeader, possibleX, possibleY, ret.RegionName);
                return ret;
            }

            // for 0.8 regions just make a BIG area request. old code whould do it plus 4 more smaller on region open edges
            // this is what 0.9 grids now do internally
            List<GridRegion> possibleRegions = pGridService.GetRegionRange(pScopeID,
                        (int)(px - Constants.MaximumRegionSize), (int)(px + 1), // +1 bc left mb not part of range
                        (int)(py - Constants.MaximumRegionSize), (int)(py + 1));
//          m_log.DebugFormat("{0} GetRegionContainingWorldLocation: possibleRegions cnt={1}, range={2}",
//                       LogHeader, possibleRegions.Count, range);
            if (possibleRegions != null && possibleRegions.Count > 0)
            {
                // If we found some regions, check to see if the point is within
                foreach (GridRegion gr in possibleRegions)
                {
//                  m_log.DebugFormat("{0} GetRegionContainingWorldLocation: possibleRegion nm={1}, regionLoc=<{2},{3}>, regionSize=<{4},{5}>",
//                               LogHeader, gr.RegionName, gr.RegionLocX, gr.RegionLocY, gr.RegionSizeX, gr.RegionSizeY);
                    if (px >= (double)gr.RegionLocX && px < (double)(gr.RegionLocX + gr.RegionSizeX)
                                && py >= (double)gr.RegionLocY && py < (double)(gr.RegionLocY + gr.RegionSizeY))
                    {
                        // Found a region that contains the point
                        return gr;
//                      m_log.DebugFormat("{0} GetRegionContainingWorldLocation: found. RegionName={1}", LogHeader, ret.RegionName);
                    }
                }
            }

            // remember this location was not found so we can quickly not find it next time
            m_notFoundLocationCache.Add(px, py);
//          m_log.DebugFormat("{0} GetRegionContainingWorldLocation: Not found. Remembering loc=<{1},{2}>", LogHeader, px, py);
            return null;
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
        private void InformClientOfNeighbourAsync(ScenePresence sp, AgentCircuitData agentCircData, GridRegion reg,
                                                  IPEndPoint endPoint, bool newAgent)
        {

            if (newAgent)
            {
                // we may already had lost this sp
                if(sp == null || sp.IsDeleted || sp.ClientView == null) // something bad already happened
                   return;

                Scene scene = sp.Scene;

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Informing {0} {1} about neighbour {2} {3} at ({4},{5})",
                    sp.Name, sp.UUID, reg.RegionName, endPoint, reg.RegionCoordX, reg.RegionCoordY);

                string capsPath = reg.ServerURI + CapsUtil.GetCapsSeedPath(agentCircData.CapsPath);

                string reason = String.Empty;

                EntityTransferContext ctx = new EntityTransferContext();
                bool regionAccepted = scene.SimulationService.CreateAgent(reg, reg, agentCircData, (uint)TeleportFlags.Default, ctx, out reason);

                if (regionAccepted)
                {
                    // give  time for createAgent to finish, since it is async and does grid services access
                    Thread.Sleep(500);

                    if (m_eqModule != null)
                    {
                        #region IP Translation for NAT
                        if(sp == null || sp.IsDeleted || sp.ClientView == null) // something bad already happened
                            return;

                        IClientIPEndpoint ipepClient;
                        if (sp.ClientView.TryGet(out ipepClient))
                        {
                            endPoint.Address = NetworkUtil.GetIPFor(ipepClient.EndPoint, endPoint.Address);
                        }
                        #endregion

                        m_log.DebugFormat("{0} {1} is sending {2} EnableSimulator for neighbour region {3}(loc=<{4},{5}>,siz=<{6},{7}>) " +
                            "and EstablishAgentCommunication with seed cap {8}", LogHeader,
                            scene.RegionInfo.RegionName, sp.Name,
                            reg.RegionName, reg.RegionLocX, reg.RegionLocY, reg.RegionSizeX, reg.RegionSizeY, capsPath);

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

                else
                {
                    sp.RemoveNeighbourRegion(reg.RegionHandle);
                    m_log.WarnFormat(
                        "[ENTITY TRANSFER MODULE]: Region {0} did not accept {1} {2}: {3}",
                        reg.RegionName, sp.Name, sp.UUID, reason);
                }
            }

        }

        /// <summary>
        /// Return the list of online regions that are considered to be neighbours to the given scene.
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="pRegionLocX"></param>
        /// <param name="pRegionLocY"></param>
        /// <returns></returns>
        protected List<GridRegion> GetNeighbors(ScenePresence avatar, uint pRegionLocX, uint pRegionLocY)
        {
            Scene pScene = avatar.Scene;
            RegionInfo m_regionInfo = pScene.RegionInfo;
            List<GridRegion> neighbours;

            uint dd = (uint)avatar.RegionViewDistance;

            // until avatar movement updates client connections, we need to seend at least this current region imediate neighbors
            uint ddX = Math.Max(dd, Constants.RegionSize);
            uint ddY = Math.Max(dd, Constants.RegionSize);

            ddX--;
            ddY--;

            // reference to region edges. Should be avatar position
            uint startX = Util.RegionToWorldLoc(pRegionLocX);
            uint endX = startX + m_regionInfo.RegionSizeX;
            uint startY = Util.RegionToWorldLoc(pRegionLocY);
            uint endY = startY + m_regionInfo.RegionSizeY;

            startX -= ddX;
            startY -= ddY;
            endX += ddX;
            endY += ddY;

            neighbours
                = avatar.Scene.GridService.GetRegionRange(
                    m_regionInfo.ScopeID, (int)startX, (int)endX, (int)startY, (int)endY);

            // The r.RegionFlags == null check only needs to be made for simulators before 2015-01-14 (pre 0.8.1).
            neighbours.RemoveAll( r => r.RegionID == m_regionInfo.RegionID );

            return neighbours;
        }
        #endregion

        #region Agent Arrived

        public void AgentArrivedAtDestination(UUID id)
        {
            m_entityTransferStateMachine.SetAgentArrivedAtDestination(id);
        }

        #endregion

        #region Object Transfers

        public GridRegion GetObjectDestination(SceneObjectGroup grp, Vector3 targetPosition,out Vector3 newpos)
        {
            newpos = targetPosition;

            Scene scene = grp.Scene;
            if (scene == null)
                return null;

            int x = (int)targetPosition.X + (int)scene.RegionInfo.WorldLocX;
            if (targetPosition.X >= 0)
                x++;
            else
                x--;

            int y = (int)targetPosition.Y + (int)scene.RegionInfo.WorldLocY;
            if (targetPosition.Y >= 0)
                y++;
            else
                y--;

            GridRegion neighbourRegion = scene.GridService.GetRegionByPosition(scene.RegionInfo.ScopeID,x,y);
            if (neighbourRegion == null)
            {
                return null;
            }

            float newRegionSizeX = neighbourRegion.RegionSizeX;
            float newRegionSizeY = neighbourRegion.RegionSizeY;
            if (newRegionSizeX == 0)
                newRegionSizeX = Constants.RegionSize;
            if (newRegionSizeY == 0)
                newRegionSizeY = Constants.RegionSize;

            newpos.X = targetPosition.X - (neighbourRegion.RegionLocX - (int)scene.RegionInfo.WorldLocX);
            newpos.Y = targetPosition.Y - (neighbourRegion.RegionLocY - (int)scene.RegionInfo.WorldLocY);

            const float enterDistance = 0.2f;
            newpos.X = Util.Clamp(newpos.X, enterDistance, newRegionSizeX - enterDistance);
            newpos.Y = Util.Clamp(newpos.Y, enterDistance, newRegionSizeY - enterDistance);

            return neighbourRegion;
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
        public bool CrossPrimGroupIntoNewRegion(GridRegion destination, Vector3 newPosition, SceneObjectGroup grp, bool silent, bool removeScripts)
        {
            //m_log.Debug("  >>> CrossPrimGroupIntoNewRegion <<<");

            Culture.SetCurrentCulture();

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
                        grp.Scene.DeleteSceneObject(grp, silent, removeScripts);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat(
                            "[ENTITY TRANSFER MODULE]: Exception deleting the old object left behind on a border crossing for {0}, {1}",
                            grp, e);
                    }
                }
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

                    CrossPrimGroupIntoNewRegion(destination, Vector3.Zero, clone, silent,true);
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
                if (!Scene.Permissions.CanObjectEntry(so, true, so.AbsolutePosition))
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

                // AddSceneObject already does this and doing it again messes
                //if (so.RootPart.KeyframeMotion != null)
                //    so.RootPart.KeyframeMotion.UpdateSceneObject(so);
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
