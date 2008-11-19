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
 *     * Neither the name of the OpenSim Project nor the
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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Interfaces;
using LLSD = OpenMetaverse.StructuredData.LLSD;

namespace OpenSim.Region.Environment.Scenes
{
    public delegate void KiPrimitiveDelegate(uint localID);

    public delegate void RemoveKnownRegionsFromAvatarList(UUID avatarID, List<ulong> regionlst);

    public class SceneCommunicationService //one instance per region
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected CommunicationsManager m_commsProvider;
        protected RegionInfo m_regionInfo;

        protected RegionCommsListener regionCommsHost;

        public event AgentCrossing OnAvatarCrossingIntoRegion;
        public event ExpectUserDelegate OnExpectUser;
        public event ExpectPrimDelegate OnExpectPrim;
        public event CloseAgentConnection OnCloseAgentConnection;
        public event PrimCrossing OnPrimCrossingIntoRegion;
        public event RegionUp OnRegionUp;
        public event ChildAgentUpdate OnChildAgentUpdate;
        public event RemoveKnownRegionsFromAvatarList OnRemoveKnownRegionFromAvatar;
        public event LogOffUser OnLogOffUser;
        public event GetLandData OnGetLandData;

        private AgentCrossing handlerAvatarCrossingIntoRegion = null; // OnAvatarCrossingIntoRegion;
        private ExpectUserDelegate handlerExpectUser = null; // OnExpectUser;
        private ExpectPrimDelegate handlerExpectPrim = null; // OnExpectPrim;
        private CloseAgentConnection handlerCloseAgentConnection = null; // OnCloseAgentConnection;
        private PrimCrossing handlerPrimCrossingIntoRegion = null; // OnPrimCrossingIntoRegion;
        private RegionUp handlerRegionUp = null; // OnRegionUp;
        private ChildAgentUpdate handlerChildAgentUpdate = null; // OnChildAgentUpdate;
        private RemoveKnownRegionsFromAvatarList handlerRemoveKnownRegionFromAvatar = null; // OnRemoveKnownRegionFromAvatar;
        private LogOffUser handlerLogOffUser = null;
        private GetLandData handlerGetLandData = null; // OnGetLandData

        public KiPrimitiveDelegate KiPrimitive;

        public SceneCommunicationService(CommunicationsManager commsMan)
        {
            m_commsProvider = commsMan;
        }

        /// <summary>
        /// Register a region with the grid
        /// </summary>
        /// <param name="regionInfos"></param>
        /// <exception cref="System.Exception">Thrown if region registration fails.</exception>
        public void RegisterRegion(RegionInfo regionInfos)
        {
            m_regionInfo = regionInfos;
            m_commsProvider.GridService.gdebugRegionName = regionInfos.RegionName;
            m_commsProvider.InterRegion.rdebugRegionName = regionInfos.RegionName;            
            regionCommsHost = m_commsProvider.GridService.RegisterRegion(m_regionInfo);

            if (regionCommsHost != null)
            {
                //m_log.Info("[INTER]: " + debugRegionName + ": SceneCommunicationService: registered with gridservice and got" + regionCommsHost.ToString());

                regionCommsHost.debugRegionName = regionInfos.RegionName;
                regionCommsHost.OnExpectPrim += IncomingPrimCrossing;
                regionCommsHost.OnExpectUser += NewUserConnection;
                regionCommsHost.OnAvatarCrossingIntoRegion += AgentCrossing;
                regionCommsHost.OnCloseAgentConnection += CloseConnection;
                regionCommsHost.OnRegionUp += newRegionUp;
                regionCommsHost.OnChildAgentUpdate += ChildAgentUpdate;
                regionCommsHost.OnLogOffUser += GridLogOffUser;
                regionCommsHost.OnGetLandData += FetchLandData;
            }
            else
            {
                //m_log.Info("[INTER]: " + debugRegionName + ": SceneCommunicationService: registered with gridservice and got null");
            }
        }

        public RegionInfo RequestClosestRegion(string name)
        {
            return m_commsProvider.GridService.RequestClosestRegion(name);
        }

        public void Close()
        {
            if (regionCommsHost != null)
            {
                regionCommsHost.OnLogOffUser -= GridLogOffUser;
                regionCommsHost.OnChildAgentUpdate -= ChildAgentUpdate;
                regionCommsHost.OnRegionUp -= newRegionUp;
                regionCommsHost.OnExpectUser -= NewUserConnection;
                regionCommsHost.OnExpectPrim -= IncomingPrimCrossing;
                regionCommsHost.OnAvatarCrossingIntoRegion -= AgentCrossing;
                regionCommsHost.OnCloseAgentConnection -= CloseConnection;
                regionCommsHost.OnGetLandData -= FetchLandData;
                
                try
                {
                    m_commsProvider.GridService.DeregisterRegion(m_regionInfo);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[GRID]: Deregistration of region {0} from the grid failed - {1}.  Continuing", 
                        m_regionInfo.RegionName, e);
                }
                
                regionCommsHost = null;
            }
        }

        #region CommsManager Event handlers

        /// <summary>
        ///
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agent"></param>
        ///
        protected void NewUserConnection(AgentCircuitData agent)
        {
            handlerExpectUser = OnExpectUser;
            if (handlerExpectUser != null)
            {
                //m_log.Info("[INTER]: " + debugRegionName + ": SceneCommunicationService: OnExpectUser Fired for User:" + agent.firstname + " " + agent.lastname);
                handlerExpectUser(agent);
            }
        }

        protected void GridLogOffUser(UUID AgentID, UUID RegionSecret, string message)
        {
            handlerLogOffUser = OnLogOffUser;
            if (handlerLogOffUser != null)
            {
                handlerLogOffUser(AgentID, RegionSecret, message);
            }
        }

        protected bool newRegionUp(RegionInfo region)
        {
            handlerRegionUp = OnRegionUp;
            if (handlerRegionUp != null)
            {
                //m_log.Info("[INTER]: " + debugRegionName + ": SceneCommunicationService: newRegionUp Fired for User:" + region.RegionName);
                handlerRegionUp(region);
            }
            return true;
        }

        protected bool ChildAgentUpdate(ChildAgentDataUpdate cAgentData)
        {
            handlerChildAgentUpdate = OnChildAgentUpdate;
            if (handlerChildAgentUpdate != null)
                handlerChildAgentUpdate(cAgentData);


            return true;
        }

        protected void AgentCrossing(UUID agentID, Vector3 position, bool isFlying)
        {
            handlerAvatarCrossingIntoRegion = OnAvatarCrossingIntoRegion;
            if (handlerAvatarCrossingIntoRegion != null)
            {
                handlerAvatarCrossingIntoRegion(agentID, position, isFlying);
            }
        }

        protected bool IncomingPrimCrossing(UUID primID, String objXMLData, int XMLMethod)
        {
            handlerExpectPrim = OnExpectPrim;
            if (handlerExpectPrim != null)
            {
                return handlerExpectPrim(primID, objXMLData, XMLMethod);
            }
            else
            {
                return false;
            }

        }

        protected void PrimCrossing(UUID primID, Vector3 position, bool isPhysical)
        {
            handlerPrimCrossingIntoRegion = OnPrimCrossingIntoRegion;
            if (handlerPrimCrossingIntoRegion != null)
            {
                handlerPrimCrossingIntoRegion(primID, position, isPhysical);
            }
        }

        protected bool CloseConnection(UUID agentID)
        {
            m_log.Debug("[INTERREGION]: Incoming Agent Close Request for agent: " + agentID);
            
            handlerCloseAgentConnection = OnCloseAgentConnection;
            if (handlerCloseAgentConnection != null)
            {
                return handlerCloseAgentConnection(agentID);
            }
            
            return false;
        }

        protected LandData FetchLandData(uint x, uint y)
        {
            handlerGetLandData = OnGetLandData;
            if (handlerGetLandData != null)
            {
                return handlerGetLandData(x, y);
            }
            return null;
        }

        #endregion

        #region Inform Client of Neighbours

        private delegate void InformClientOfNeighbourDelegate(
            ScenePresence avatar, AgentCircuitData a, ulong regionHandle, IPEndPoint endPoint);

        private void InformClientOfNeighbourCompleted(IAsyncResult iar)
        {
            InformClientOfNeighbourDelegate icon = (InformClientOfNeighbourDelegate) iar.AsyncState;
            icon.EndInvoke(iar);
        }

        /// <summary>
        /// Async component for informing client of which neighbours exist
        /// </summary>
        /// <remarks>
        /// This needs to run asynchronesously, as a network timeout may block the thread for a long while
        /// </remarks>
        /// <param name="remoteClient"></param>
        /// <param name="a"></param>
        /// <param name="regionHandle"></param>
        /// <param name="endPoint"></param>
        private void InformClientOfNeighbourAsync(ScenePresence avatar, AgentCircuitData a, ulong regionHandle,
                                                  IPEndPoint endPoint)
        {
            m_log.Info("[INTERGRID]: Starting to inform client about neighbours");
            bool regionAccepted = m_commsProvider.InterRegion.InformRegionOfChildAgent(regionHandle, a);

            if (regionAccepted)
            {
                IEventQueue eq = avatar.Scene.RequestModuleInterface<IEventQueue>();
                if (eq != null)
                {
                    LLSD Item = EventQueueHelper.EnableSimulator(regionHandle, endPoint);
                    eq.Enqueue(Item, avatar.UUID);
                }
                else
                {
                    avatar.ControllingClient.InformClientOfNeighbour(regionHandle, endPoint);
                    // TODO: make Event Queue disablable!
                }
                
                avatar.AddNeighbourRegion(regionHandle);
                m_log.Info("[INTERGRID]: Completed inform client about neighbours");
            }
        }

        public void RequestNeighbors(RegionInfo region)
        {
            // List<SimpleRegionInfo> neighbours =
            m_commsProvider.GridService.RequestNeighbours(m_regionInfo.RegionLocX, m_regionInfo.RegionLocY);
            //IPEndPoint blah = new IPEndPoint();

            //blah.Address = region.RemotingAddress;
            //blah.Port = region.RemotingPort;
        }

        /// <summary>
        /// This informs all neighboring regions about agent "avatar".
        /// Calls an asynchronous method to do so..  so it doesn't lag the sim.
        /// </summary>
        public void EnableNeighbourChildAgents(ScenePresence avatar, List<RegionInfo> lstneighbours)
        {
            List<SimpleRegionInfo> neighbours = new List<SimpleRegionInfo>();

            //m_commsProvider.GridService.RequestNeighbours(m_regionInfo.RegionLocX, m_regionInfo.RegionLocY);
            for (int i = 0; i < lstneighbours.Count; i++)
            {
                // We don't want to keep sending to regions that consistently fail on comms.
                if (!(lstneighbours[i].commFailTF))
                {
                    neighbours.Add(new SimpleRegionInfo(lstneighbours[i]));
                }
            }
            // we're going to be using the above code once neighbour cache is correct.  Currently it doesn't appear to be
            // So we're temporarily going back to the old method of grabbing it from the Grid Server Every time :/
            neighbours =
                m_commsProvider.GridService.RequestNeighbours(m_regionInfo.RegionLocX, m_regionInfo.RegionLocY);

            if (neighbours != null)
            {
                for (int i = 0; i < neighbours.Count; i++)
                {
                    AgentCircuitData agent = avatar.ControllingClient.RequestClientInfo();
                    agent.BaseFolder = UUID.Zero;
                    agent.InventoryFolder = UUID.Zero;
                    agent.startpos = new Vector3(128, 128, 70);
                    agent.child = true;

                    InformClientOfNeighbourDelegate d = InformClientOfNeighbourAsync;

                    try
                    {
                        d.BeginInvoke(avatar, agent, neighbours[i].RegionHandle, neighbours[i].ExternalEndPoint,
                                      InformClientOfNeighbourCompleted,
                                      d);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat(
                            "[REGIONINFO]: Could not resolve external hostname {0} for region {1} ({2}, {3}).  {4}",
                            neighbours[i].ExternalHostName,
                            neighbours[i].RegionHandle,
                            neighbours[i].RegionLocX,
                            neighbours[i].RegionLocY,
                            e);

                        // FIXME: Okay, even though we've failed, we're still going to throw the exception on,
                        // since I don't know what will happen if we just let the client continue

                        // XXX: Well, decided to swallow the exception instead for now.  Let us see how that goes.
                        // throw e;

                    }
                }
            }
        }

        /// <summary>
        /// This informs a single neighboring region about agent "avatar".
        /// Calls an asynchronous method to do so..  so it doesn't lag the sim.
        /// </summary>
        public void InformNeighborChildAgent(ScenePresence avatar, RegionInfo region, List<RegionInfo> neighbours)
        {
            AgentCircuitData agent = avatar.ControllingClient.RequestClientInfo();
            agent.BaseFolder = UUID.Zero;
            agent.InventoryFolder = UUID.Zero;
            agent.startpos = new Vector3(128, 128, 70);
            agent.child = true;

            InformClientOfNeighbourDelegate d = InformClientOfNeighbourAsync;
            d.BeginInvoke(avatar, agent, region.RegionHandle, region.ExternalEndPoint,
                          InformClientOfNeighbourCompleted,
                          d);
        }

        #endregion

        public delegate void InformNeighbourThatRegionUpDelegate(RegionInfo region, ulong regionhandle);

        private void InformNeighborsThatRegionisUpCompleted(IAsyncResult iar)
        {
            InformNeighbourThatRegionUpDelegate icon = (InformNeighbourThatRegionUpDelegate) iar.AsyncState;
            icon.EndInvoke(iar);
        }

        /// <summary>
        /// Asynchronous call to information neighbouring regions that this region is up
        /// </summary>
        /// <param name="region"></param>
        /// <param name="regionhandle"></param>
        private void InformNeighboursThatRegionIsUpAsync(RegionInfo region, ulong regionhandle)
        {
            m_log.Info("[INTERGRID]: Starting to inform neighbors that I'm here");
            //RegionUpData regiondata = new RegionUpData(region.RegionLocX, region.RegionLocY, region.ExternalHostName, region.InternalEndPoint.Port);

            bool regionAccepted =
                m_commsProvider.InterRegion.RegionUp(new SerializableRegionInfo(region), regionhandle);

            if (regionAccepted)
            {
                m_log.Info("[INTERGRID]: Completed informing neighbors that I'm here");
                handlerRegionUp = OnRegionUp;

                // yes, we're notifying ourselves.
                if (handlerRegionUp != null)
                    handlerRegionUp(region);
            }
            else
            {
                m_log.Warn("[INTERGRID]: Failed to inform neighbors that I'm here.");
            }
        }

        /// <summary>
        /// Called by scene when region is initialized (not always when it's listening for agents)
        /// This is an inter-region message that informs the surrounding neighbors that the sim is up.
        /// </summary>
        public void InformNeighborsThatRegionisUp(RegionInfo region)
        {
            //m_log.Info("[INTER]: " + debugRegionName + ": SceneCommunicationService: Sending InterRegion Notification that region is up " + region.RegionName);


            List<SimpleRegionInfo> neighbours = new List<SimpleRegionInfo>();
            // This stays uncached because we don't already know about our neighbors at this point.
            neighbours = m_commsProvider.GridService.RequestNeighbours(m_regionInfo.RegionLocX, m_regionInfo.RegionLocY);
            if (neighbours != null)
            {
                for (int i = 0; i < neighbours.Count; i++)
                {
                    InformNeighbourThatRegionUpDelegate d = InformNeighboursThatRegionIsUpAsync;

                    d.BeginInvoke(region, neighbours[i].RegionHandle,
                                  InformNeighborsThatRegionisUpCompleted,
                                  d);
                }
            }

            //bool val = m_commsProvider.InterRegion.RegionUp(new SerializableRegionInfo(region));
        }

        public delegate void SendChildAgentDataUpdateDelegate(ChildAgentDataUpdate cAgentData, ScenePresence presence);

        /// <summary>
        /// This informs all neighboring regions about the settings of it's child agent.
        /// Calls an asynchronous method to do so..  so it doesn't lag the sim.
        ///
        /// This contains information, such as, Draw Distance, Camera location, Current Position, Current throttle settings, etc.
        ///
        /// </summary>
        private void SendChildAgentDataUpdateAsync(ChildAgentDataUpdate cAgentData, ScenePresence presence)
        {
            //m_log.Info("[INTERGRID]: Informing neighbors about my agent.");
            try
            {
                foreach (ulong regionHandle in presence.KnownChildRegions)
                {
                    bool regionAccepted = m_commsProvider.InterRegion.ChildAgentUpdate(regionHandle, cAgentData);

                    if (regionAccepted)
                    {
                        //m_log.Info("[INTERGRID]: Completed sending a neighbor an update about my agent");
                    }
                    else
                    {
                        //m_log.Info("[INTERGRID]: Failed sending a neighbor an update about my agent");
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // We're ignoring a collection was modified error because this data gets old and outdated fast.
            }

        }

        private void SendChildAgentDataUpdateCompleted(IAsyncResult iar)
        {
            SendChildAgentDataUpdateDelegate icon = (SendChildAgentDataUpdateDelegate) iar.AsyncState;
            icon.EndInvoke(iar);
        }

        public void SendChildAgentDataUpdate(ChildAgentDataUpdate cAgentData, ScenePresence presence)
        {
            // This assumes that we know what our neighbors are.
            SendChildAgentDataUpdateDelegate d = SendChildAgentDataUpdateAsync;
            d.BeginInvoke(cAgentData,presence,
                          SendChildAgentDataUpdateCompleted,
                          d);
        }

        public delegate void SendCloseChildAgentDelegate(UUID agentID, List<ulong> regionlst);

        /// <summary>
        /// This Closes child agents on neighboring regions
        /// Calls an asynchronous method to do so..  so it doesn't lag the sim.
        /// </summary>
        private void SendCloseChildAgentAsync(UUID agentID, List<ulong> regionlst)
        {

            foreach (ulong regionHandle in regionlst)
            {
                bool regionAccepted = m_commsProvider.InterRegion.TellRegionToCloseChildConnection(regionHandle, agentID);

                if (regionAccepted)
                {
                    m_log.Info("[INTERGRID]: Completed sending agent Close agent Request to neighbor");

                }
                else
                {
                    m_log.Info("[INTERGRID]: Failed sending agent Close agent Request to neighbor");

                }

            }
            // We remove the list of known regions from the agent's known region list through an event
            // to scene, because, if an agent logged of, it's likely that there will be no scene presence
            // by the time we get to this part of the method.
            handlerRemoveKnownRegionFromAvatar = OnRemoveKnownRegionFromAvatar;
            if (handlerRemoveKnownRegionFromAvatar != null)
            {
                handlerRemoveKnownRegionFromAvatar(agentID, regionlst);
            }
        }

        private void SendCloseChildAgentCompleted(IAsyncResult iar)
        {
            SendCloseChildAgentDelegate icon = (SendCloseChildAgentDelegate)iar.AsyncState;
            icon.EndInvoke(iar);
        }

        public void SendCloseChildAgentConnections(UUID agentID, List<ulong> regionslst)
        {
            // This assumes that we know what our neighbors are.
            SendCloseChildAgentDelegate d = SendCloseChildAgentAsync;
            d.BeginInvoke(agentID, regionslst,
                          SendCloseChildAgentCompleted,
                          d);
        }

        /// <summary>
        /// Helper function to request neighbors from grid-comms
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public virtual RegionInfo RequestNeighbouringRegionInfo(ulong regionHandle)
        {
            //m_log.Info("[INTER]: " + debugRegionName + ": SceneCommunicationService: Sending Grid Services Request about neighbor " + regionHandle.ToString());
            return m_commsProvider.GridService.RequestNeighbourInfo(regionHandle);
        }

        /// <summary>
        /// Helper function to request neighbors from grid-comms
        /// </summary>
        /// <param name="regionID"></param>
        /// <returns></returns>
        public virtual RegionInfo RequestNeighbouringRegionInfo(UUID regionID)
        {
            //m_log.Info("[INTER]: " + debugRegionName + ": SceneCommunicationService: Sending Grid Services Request about neighbor " + regionID);
            return m_commsProvider.GridService.RequestNeighbourInfo(regionID);
        }

        /// <summary>
        /// Requests map blocks in area of minX, maxX, minY, MaxY in world cordinates
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        public virtual void RequestMapBlocks(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY)
        {
            List<MapBlockData> mapBlocks;
            mapBlocks = m_commsProvider.GridService.RequestNeighbourMapBlocks(minX - 4, minY - 4, minX + 4, minY + 4);
            remoteClient.SendMapBlock(mapBlocks, 0);
        }

        /// <summary>
        /// Try to teleport an agent to a new region.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="RegionHandle"></param>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <param name="flags"></param>
        public virtual void RequestTeleportToLocation(ScenePresence avatar, ulong regionHandle, Vector3 position,
                                                      Vector3 lookAt, uint teleportFlags)
        {
            if (!avatar.Scene.ExternalChecks.ExternalChecksCanTeleport(avatar.UUID))
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
                        "[SCENE COMMUNICATION SERVICE]: RequestTeleportToLocation() was given an illegal position of {0} for avatar {1}, {2}.  Substituting {3}",
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
                        List<ulong> childRegions = new List<ulong>(avatar.GetKnownRegionList());
                        // Compared to ScenePresence.CrossToNewRegion(), there's no obvious code to handle a teleport
                        // failure at this point (unlike a border crossing failure).  So perhaps this can never fail
                        // once we reach here...
                        avatar.Scene.RemoveCapsHandler(avatar.UUID);
                        agent.child = false;
                        m_commsProvider.InterRegion.InformRegionOfChildAgent(reg.RegionHandle, agent);
                        
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

                        
                        if (eq != null)
                        {
                            LLSD Item = EventQueueHelper.TeleportFinishEvent(reg.RegionHandle, 13, reg.ExternalEndPoint,
                                                                             4, teleportFlags, capsPath, avatar.UUID);
                            eq.Enqueue(Item, avatar.UUID);
                        }
                        else
                        {
                            avatar.ControllingClient.SendRegionTeleport(reg.RegionHandle, 13, reg.ExternalEndPoint, 4,
                                                                        teleportFlags, capsPath);
                        }

                        avatar.MakeChildAgent();
                        Thread.Sleep(7000);
                        avatar.CrossAttachmentsIntoNewRegion(reg.RegionHandle, true);
                        if (KiPrimitive != null)
                        {
                            KiPrimitive(avatar.LocalId);
                        }

                        avatar.Close();

                        uint newRegionX = (uint)(reg.RegionHandle >> 40);
                        uint newRegionY = (((uint)(reg.RegionHandle)) >> 8);
                        uint oldRegionX = (uint)(m_regionInfo.RegionHandle >> 40);
                        uint oldRegionY = (((uint)(m_regionInfo.RegionHandle)) >> 8);
                        if (Util.fast_distance2d((int)(newRegionX - oldRegionX), (int)(newRegionY - oldRegionY)) > 3)
                        {
                            SendCloseChildAgentConnections(avatar.UUID,avatar.GetKnownRegionList());
                            SendCloseChildAgentConnections(avatar.UUID, childRegions);
                            CloseConnection(avatar.UUID);
                        }
                        // if (teleport success) // seems to be always success here
                        // the user may change their profile information in other region,
                        // so the userinfo in UserProfileCache is not reliable any more, delete it
                        if (avatar.Scene.NeedSceneCacheClear(avatar.UUID))
                            m_commsProvider.UserProfileCacheService.RemoveUser(avatar.UUID);
                        m_log.InfoFormat("User {0} is going to another region, profile cache removed", avatar.UUID);
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
                    Helpers.LongToUInts(regionHandle, out regX, out regY);
                    
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

        /// <summary>
        /// Inform a neighbouring region that an avatar is about to cross into it.
        /// </summary>
        /// <param name="regionhandle"></param>
        /// <param name="agentID"></param>
        /// <param name="position"></param>
        public bool CrossToNeighbouringRegion(ulong regionhandle, UUID agentID, Vector3 position, bool isFlying)
        {
            return m_commsProvider.InterRegion.ExpectAvatarCrossing(regionhandle, agentID, position, isFlying);
        }

        public bool PrimCrossToNeighboringRegion(ulong regionhandle, UUID primID, string objData, int XMLMethod)
        {
            return m_commsProvider.InterRegion.InformRegionOfPrimCrossing(regionhandle, primID, objData, XMLMethod);
        }

        public Dictionary<string, string> GetGridSettings()
        {
            return m_commsProvider.GridService.GetGridSettings();
        }

        public void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, Vector3 position, Vector3 lookat)
        {
            m_commsProvider.LogOffUser(userid, regionid, regionhandle, position, lookat);
        }

        // deprecated as of 2008-08-27
        public void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, float posx, float posy, float posz)
        {
             m_commsProvider.LogOffUser(userid, regionid, regionhandle, posx, posy, posz);
        }

        public void ClearUserAgent(UUID avatarID)
        {
            m_commsProvider.UserService.ClearUserAgent(avatarID);
        }

        public void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms)
        {
            m_commsProvider.AddNewUserFriend(friendlistowner, friend, perms);
        }

        public void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms)
        {
            m_commsProvider.UpdateUserFriendPerms(friendlistowner, friend, perms);
        }

        public void RemoveUserFriend(UUID friendlistowner, UUID friend)
        {
            m_commsProvider.RemoveUserFriend(friendlistowner, friend);
        }

        public List<FriendListItem> GetUserFriendList(UUID friendlistowner)
        {
            return m_commsProvider.GetUserFriendList(friendlistowner);
        }

        public  List<MapBlockData> RequestNeighbourMapBlocks(int minX, int minY, int maxX, int maxY)
        {
            return m_commsProvider.GridService.RequestNeighbourMapBlocks(minX, minY, maxX, maxY);
        }

        public List<AvatarPickerAvatar> GenerateAgentPickerRequestResponse(UUID queryID, string query)
        {
            return m_commsProvider.GenerateAgentPickerRequestResponse(queryID, query);
        }
        
        public List<RegionInfo> RequestNamedRegions(string name, int maxNumber)
        {
            return m_commsProvider.GridService.RequestNamedRegions(name, maxNumber);
        }
    }
}
