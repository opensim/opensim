/**
 * Copyright (c) 2008, Contributors. All rights reserved.
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 *     * Redistributions of source code must retain the above copyright notice, 
 *       this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright notice, 
 *       this list of conditions and the following disclaimer in the documentation 
 *       and/or other materials provided with the distribution.
 *     * Neither the name of the Organizations nor the names of Individual
 *       Contributors may be used to endorse or promote products derived from 
 *       this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES 
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL 
 * THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, 
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED 
 * AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED 
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

using OpenMetaverse;

using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment;
using OpenSim.Region.Interfaces;
using OSD = OpenMetaverse.StructuredData.OSD;

namespace OpenSim.Region.Environment.Scenes.Hypergrid
{
    public class HGSceneCommunicationService : SceneCommunicationService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IHyperlink m_hg;

        public HGSceneCommunicationService(CommunicationsManager commsMan, IHyperlink hg) : base(commsMan)
        {
            m_hg = hg;
        }


        /// <summary>
        /// Try to teleport an agent to a new region.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="RegionHandle"></param>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <param name="flags"></param>
        public override void RequestTeleportToLocation(ScenePresence avatar, ulong regionHandle, Vector3 position,
                                                      Vector3 lookAt, uint teleportFlags)
        {
            if (!avatar.Scene.Permissions.CanTeleport(avatar.UUID))
                return;

            bool destRegionUp = false;

            IEventQueue eq = avatar.Scene.RequestModuleInterface<IEventQueue>();

            if (regionHandle == m_regionInfo.RegionHandle)
            {
                // Teleport within the same region
                if (position.X < 0 || position.X > Constants.RegionSize || position.Y < 0 || position.Y > Constants.RegionSize || position.Z < 0)
                {
                    Vector3 emergencyPos = new Vector3(128, 128, 128);

                    m_log.WarnFormat(
                        "[HGSceneCommService]: RequestTeleportToLocation() was given an illegal position of {0} for avatar {1}, {2}.  Substituting {3}",
                        position, avatar.Name, avatar.UUID, emergencyPos);
                    position = emergencyPos;
                }
                // TODO: Get proper AVG Height
                float localAVHeight = 1.56f;
                float posZLimit = (float)avatar.Scene.GetLandHeight((int)position.X, (int)position.Y);
                float newPosZ = posZLimit + localAVHeight;
                if (posZLimit >= (position.Z - (localAVHeight / 2)) && !(Single.IsInfinity(newPosZ) || Single.IsNaN(newPosZ)))
                {
                    position.Z = newPosZ;
                }

                // Only send this if the event queue is null
                if (eq == null)
                    avatar.ControllingClient.SendTeleportLocationStart();


                avatar.ControllingClient.SendLocalTeleport(position, lookAt, teleportFlags);
                avatar.Teleport(position);
            }
            else
            {
                RegionInfo reg = RequestNeighbouringRegionInfo(regionHandle);
                if (reg != null)
                {
                    ///
                    /// Hypergrid mod start
                    /// 
                    ///
                    bool isHyperLink = m_hg.IsHyperlinkRegion(reg.RegionHandle);
                    bool isHomeUser = true;
                    ulong realHandle = regionHandle;
                    CachedUserInfo uinfo = m_commsProvider.UserProfileCacheService.GetUserDetails(avatar.UUID);
                    if (uinfo != null)
                    {
                        isHomeUser = HGNetworkServersInfo.Singleton.IsLocalUser(uinfo.UserProfile);
                        realHandle = m_hg.FindRegionHandle(regionHandle);
                        Console.WriteLine("XXX ---- home user? " + isHomeUser + " --- hyperlink? " + isHyperLink + " --- real handle: " + realHandle.ToString());
                    }
                    ///
                    /// Hypergrid mod stop
                    /// 
                    ///

                    if (eq == null)
                        avatar.ControllingClient.SendTeleportLocationStart();

                    AgentCircuitData agent = avatar.ControllingClient.RequestClientInfo();
                    agent.BaseFolder = UUID.Zero;
                    agent.InventoryFolder = UUID.Zero;
                    agent.startpos = position;
                    agent.child = true;

                    if (reg.RemotingAddress != "" && reg.RemotingPort != 0)
                    {
                        // region is remote. see if it is up
                        destRegionUp = m_commsProvider.InterRegion.CheckRegion(reg.RemotingAddress, reg.RemotingPort);
                    }
                    else
                    {
                        // assume local regions are always up
                        destRegionUp = true;
                    }

                    if (destRegionUp)
                    {

                        // Fixing a bug where teleporting while sitting results in the avatar ending up removed from
                        // both regions
                        if (avatar.ParentID != (uint)0)
                            avatar.StandUp();
                        if (!avatar.ValidateAttachments())
                        {
                            avatar.ControllingClient.SendTeleportFailed("Inconsistent attachment state");
                            return;
                        }

                        // the avatar.Close below will clear the child region list. We need this below for (possibly)
                        // closing the child agents, so save it here (we need a copy as it is Clear()-ed).
                        //List<ulong> childRegions = new List<ulong>(avatar.GetKnownRegionList());
                        // Compared to ScenePresence.CrossToNewRegion(), there's no obvious code to handle a teleport
                        // failure at this point (unlike a border crossing failure).  So perhaps this can never fail
                        // once we reach here...
                        avatar.Scene.RemoveCapsHandler(avatar.UUID);
                        agent.child = false;
                        m_commsProvider.InterRegion.InformRegionOfChildAgent(reg.RegionHandle, agent);

                        if (eq != null)
                        {
                            OSD Item = EventQueueHelper.EnableSimulator(realHandle, reg.ExternalEndPoint);
                            eq.Enqueue(Item, avatar.UUID);
                        }
                        else
                        {
                            avatar.ControllingClient.InformClientOfNeighbour(realHandle, reg.ExternalEndPoint);
                            // TODO: make Event Queue disablable!
                        }

                        m_commsProvider.InterRegion.ExpectAvatarCrossing(reg.RegionHandle, avatar.ControllingClient.AgentId,
                                                                     position, false);
                        Thread.Sleep(2000);
                        AgentCircuitData circuitdata = avatar.ControllingClient.RequestClientInfo();

                        // TODO Should construct this behind a method
                        string capsPath =
                            "http://" + reg.ExternalHostName + ":" + reg.HttpPort
                            + "/CAPS/" + circuitdata.CapsPath + "0000/";

                        m_log.DebugFormat(
                            "[CAPS]: Sending new CAPS seed url {0} to client {1}", capsPath, avatar.UUID);


                        ///
                        /// Hypergrid mod: realHandle instead of reg.RegionHandle
                        /// 
                        ///
                        if (eq != null)
                        {
                            OSD Item = EventQueueHelper.TeleportFinishEvent(realHandle, 13, reg.ExternalEndPoint,
                                                                             4, teleportFlags, capsPath, avatar.UUID);
                            eq.Enqueue(Item, avatar.UUID);
                        }
                        else
                        {
                            avatar.ControllingClient.SendRegionTeleport(realHandle, 13, reg.ExternalEndPoint, 4,
                                                                        teleportFlags, capsPath);
                        }
                        ///
                        /// Hypergrid mod stop
                        /// 

                        avatar.MakeChildAgent();
                        Thread.Sleep(7000);
                        avatar.CrossAttachmentsIntoNewRegion(reg.RegionHandle, true);
                        if (KiPrimitive != null)
                        {
                            KiPrimitive(avatar.LocalId);
                        }


                        uint newRegionX = (uint)(reg.RegionHandle >> 40);
                        uint newRegionY = (((uint)(reg.RegionHandle)) >> 8);
                        uint oldRegionX = (uint)(m_regionInfo.RegionHandle >> 40);
                        uint oldRegionY = (((uint)(m_regionInfo.RegionHandle)) >> 8);

                        // Let's close some children agents
                        if (isHyperLink) // close them all
                            SendCloseChildAgentConnections(avatar.UUID, avatar.GetKnownRegionList());
                        else // close just a few
                            avatar.CloseChildAgents(newRegionX, newRegionY);
                        
                        avatar.Close();
                        
                        // Finally, let's close this previously-known-as-root agent, when the jump is outside the view zone
                        ///
                        /// Hypergrid mod: extra check for isHyperLink
                        /// 
                        //if ((Util.fast_distance2d((int)(newRegionX - oldRegionX), (int)(newRegionY - oldRegionY)) > 1) || isHyperLink)
                        //if (((int)Math.Abs((int)(newRegionX - oldRegionX)) > 1) || ((int)Math.Abs((int)(newRegionY - oldRegionY)) > 1) || isHyperLink)
                        if (Util.IsOutsideView(oldRegionX, newRegionX, oldRegionY, newRegionY))
                        {
                            CloseConnection(avatar.UUID);
                        }
                        // if (teleport success) // seems to be always success here
                        // the user may change their profile information in other region,
                        // so the userinfo in UserProfileCache is not reliable any more, delete it
                        if (avatar.Scene.NeedSceneCacheClear(avatar.UUID))
                            m_commsProvider.UserProfileCacheService.RemoveUser(avatar.UUID);
                        m_log.InfoFormat("[HGSceneCommService]: User {0} is going to another region, profile cache removed", avatar.UUID);
                    }
                    else
                    {
                        avatar.ControllingClient.SendTeleportFailed("Remote Region appears to be down");
                    }
                }
                else
                {
                    // TP to a place that doesn't exist (anymore)
                    // Inform the viewer about that
                    avatar.ControllingClient.SendTeleportFailed("The region you tried to teleport to doesn't exist anymore");

                    // and set the map-tile to '(Offline)'
                    uint regX, regY;
                    Utils.LongToUInts(regionHandle, out regX, out regY);

                    MapBlockData block = new MapBlockData();
                    block.X = (ushort)(regX / Constants.RegionSize);
                    block.Y = (ushort)(regY / Constants.RegionSize);
                    block.Access = 254; // == not there

                    List<MapBlockData> blocks = new List<MapBlockData>();
                    blocks.Add(block);
                    avatar.ControllingClient.SendMapBlock(blocks, 0);
                }
            }
        }

    }
}
