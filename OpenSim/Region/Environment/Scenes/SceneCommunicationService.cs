using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Communications;


namespace OpenSim.Region.Environment.Scenes
{
    public delegate void KillObjectDelegate(uint localID);

    public class SceneCommunicationService //one instance per region
    {
        protected CommunicationsManager m_commsProvider;
        protected RegionInfo m_regionInfo;

        protected RegionCommsListener regionCommsHost;

        public event AgentCrossing OnAvatarCrossingIntoRegion;
        public event ExpectUserDelegate OnExpectUser;
        public event CloseAgentConnection OnCloseAgentConnection;
        public event PrimCrossing OnPrimCrossingIntoRegion;
        public event RegionUp OnRegionUp;

        public KillObjectDelegate KillObject;
        public string _debugRegionName = "";


        public string debugRegionName
        {
            get { return _debugRegionName; }
            set
            {
                _debugRegionName = value;
               
                

            }
        }

        public SceneCommunicationService(CommunicationsManager commsMan)
        {
            m_commsProvider = commsMan;
            m_commsProvider.GridService.gdebugRegionName = _debugRegionName;
            m_commsProvider.InterRegion.rdebugRegionName = _debugRegionName;
        }

        public void RegisterRegion(RegionInfo regionInfos)
        {
            m_regionInfo = regionInfos;
            regionCommsHost = m_commsProvider.GridService.RegisterRegion(m_regionInfo);

            if (regionCommsHost != null)
            {
                //MainLog.Instance.Verbose("INTER", debugRegionName + ": SceneCommunicationService: registered with gridservice and got" + regionCommsHost.ToString());

                regionCommsHost.debugRegionName = _debugRegionName;

                regionCommsHost.OnExpectUser += NewUserConnection;
                regionCommsHost.OnAvatarCrossingIntoRegion += AgentCrossing;
                regionCommsHost.OnPrimCrossingIntoRegion += PrimCrossing;
                regionCommsHost.OnCloseAgentConnection += CloseConnection;
                regionCommsHost.OnRegionUp += newRegionUp;


            }
            else
            {
                //MainLog.Instance.Verbose("INTER", debugRegionName + ": SceneCommunicationService: registered with gridservice and got null");

            }
        }

        public void Close()
        {
            if (regionCommsHost != null)
            {
                regionCommsHost.OnRegionUp -= newRegionUp;
                regionCommsHost.OnExpectUser -= NewUserConnection;
                regionCommsHost.OnAvatarCrossingIntoRegion -= AgentCrossing;
                regionCommsHost.OnPrimCrossingIntoRegion -= PrimCrossing;
                regionCommsHost.OnCloseAgentConnection -= CloseConnection;
                m_commsProvider.GridService.DeregisterRegion(m_regionInfo);
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
        protected void NewUserConnection(ulong regionHandle, AgentCircuitData agent)
        {
            if (OnExpectUser != null)
            {
                //MainLog.Instance.Verbose("INTER", debugRegionName + ": SceneCommunicationService: OnExpectUser Fired for User:" + agent.firstname + " " + agent.lastname);
                OnExpectUser(regionHandle, agent);
            }
        }

        protected bool newRegionUp(RegionInfo region)
        {
            if (OnRegionUp != null)
            {
                //MainLog.Instance.Verbose("INTER", debugRegionName + ": SceneCommunicationService: newRegionUp Fired for User:" + region.RegionName);
                OnRegionUp(region);
            }
            return true;
        }

        protected void AgentCrossing(ulong regionHandle, LLUUID agentID, LLVector3 position, bool isFlying)
        {
            if (OnAvatarCrossingIntoRegion != null)
            {
                OnAvatarCrossingIntoRegion(regionHandle, agentID, position, isFlying);
            }
        }
        protected void PrimCrossing(ulong regionHandle, LLUUID primID, LLVector3 position, bool isPhysical)
        {
            if (OnPrimCrossingIntoRegion != null)
            {
                OnPrimCrossingIntoRegion(regionHandle, primID, position, isPhysical);
            }
        }

        protected void CloseConnection(ulong regionHandle, LLUUID agentID)
        {
            if (OnCloseAgentConnection != null)
            {
                OnCloseAgentConnection(regionHandle, agentID);
            }
        }
        #endregion

        #region Inform Client of Neighbours
        private delegate void InformClientOfNeighbourDelegate(
            ScenePresence avatar, AgentCircuitData a, ulong regionHandle, IPEndPoint endPoint);

        private void InformClientOfNeighbourCompleted(IAsyncResult iar)
        {
            InformClientOfNeighbourDelegate icon = (InformClientOfNeighbourDelegate)iar.AsyncState;
            icon.EndInvoke(iar);
        }

        /// <summary>
        /// Async compnent for informing client of which neighbours exists
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
            MainLog.Instance.Notice("INTERGRID", "Starting to inform client about neighbours");
            bool regionAccepted = m_commsProvider.InterRegion.InformRegionOfChildAgent(regionHandle, a);

            if (regionAccepted)
            {
                avatar.ControllingClient.InformClientOfNeighbour(regionHandle, endPoint);
                avatar.AddNeighbourRegion(regionHandle);
                MainLog.Instance.Notice("INTERGRID", "Completed inform client about neighbours");
            }
        }

        public void RequestNeighbors(RegionInfo region)
        {
            List<SimpleRegionInfo> neighbours =
                m_commsProvider.GridService.RequestNeighbours(m_regionInfo.RegionLocX, m_regionInfo.RegionLocY);
            //IPEndPoint blah = new IPEndPoint();
            
            //blah.Address = region.RemotingAddress;
            //blah.Port = region.RemotingPort;
           

        }

        /// <summary>
        /// 
        /// </summary>
        public void EnableNeighbourChildAgents(ScenePresence avatar)
        {
            List<SimpleRegionInfo> neighbours =
                m_commsProvider.GridService.RequestNeighbours(m_regionInfo.RegionLocX, m_regionInfo.RegionLocY);
            if (neighbours != null)
            {
                for (int i = 0; i < neighbours.Count; i++)
                {
                    AgentCircuitData agent = avatar.ControllingClient.RequestClientInfo();
                    agent.BaseFolder = LLUUID.Zero;
                    agent.InventoryFolder = LLUUID.Zero;
                    agent.startpos = new LLVector3(128, 128, 70);
                    agent.child = true;

                    InformClientOfNeighbourDelegate d = InformClientOfNeighbourAsync;
                    d.BeginInvoke(avatar, agent, neighbours[i].RegionHandle, neighbours[i].ExternalEndPoint,
                                  InformClientOfNeighbourCompleted,
                                  d);
                }
            }
        }
        public void InformNeighborChildAgent(ScenePresence avatar, RegionInfo region)
        {
            AgentCircuitData agent = avatar.ControllingClient.RequestClientInfo();
            agent.BaseFolder = LLUUID.Zero;
            agent.InventoryFolder = LLUUID.Zero;
            agent.startpos = new LLVector3(128, 128, 70);
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
            InformNeighbourThatRegionUpDelegate icon = (InformNeighbourThatRegionUpDelegate)iar.AsyncState;
            icon.EndInvoke(iar);
        }


         private void InformNeighboursThatRegionIsUpAsync(RegionInfo region, ulong regionhandle)
        {
            MainLog.Instance.Notice("INTERGRID", "Starting to inform neighbors that I'm here");
            bool regionAccepted = m_commsProvider.InterRegion.RegionUp((new SearializableRegionInfo(region)), regionhandle);

            if (regionAccepted)
            {
                MainLog.Instance.Notice("INTERGRID", "Completed informing neighbors that I'm here");
            }
            else
            {
                MainLog.Instance.Notice("INTERGRID", "Failed to inform neighbors that I'm here");
            }
        }

        public void InformNeighborsThatRegionisUp(RegionInfo region)
        {
            //MainLog.Instance.Verbose("INTER", debugRegionName + ": SceneCommunicationService: Sending InterRegion Notification that region is up " + region.RegionName);
            List<SimpleRegionInfo> neighbours = new List<SimpleRegionInfo>();
                
             lock (neighbours)
             {
                 neighbours = m_commsProvider.GridService.RequestNeighbours(m_regionInfo.RegionLocX, m_regionInfo.RegionLocY);
                 if (neighbours != null)
                 {
                     for (int i = 0; i < neighbours.Count; i++)
                     {

                         InformNeighbourThatRegionUpDelegate d = InformNeighboursThatRegionIsUpAsync;
                         // race condition!  Arg!  I hate race conditions.
                         lock (d)
                         {
                             d.BeginInvoke(region, neighbours[i].RegionHandle,
                                           InformNeighborsThatRegionisUpCompleted,
                                           d);
                         }
                     }
                 }
             }
            //bool val = m_commsProvider.InterRegion.RegionUp(new SearializableRegionInfo(region));
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public virtual RegionInfo RequestNeighbouringRegionInfo(ulong regionHandle)
        {
            //MainLog.Instance.Verbose("INTER", debugRegionName + ": SceneCommunicationService: Sending Grid Services Request about neighbor " + regionHandle.ToString());
            return m_commsProvider.GridService.RequestNeighbourInfo(regionHandle);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        public virtual void RequestMapBlocks(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY)
        {
            List<MapBlockData> mapBlocks;
            mapBlocks = m_commsProvider.GridService.RequestNeighbourMapBlocks(minX-2, minY-2, minX+2, minY+2);
            remoteClient.SendMapBlock(mapBlocks);
            mapBlocks = m_commsProvider.GridService.RequestNeighbourMapBlocks(minX - 2, minY +3, minX +5, minY +5);
            remoteClient.SendMapBlock(mapBlocks);
            mapBlocks = m_commsProvider.GridService.RequestNeighbourMapBlocks(minX +3, minY -5, minX + 5, minY + 2);
            remoteClient.SendMapBlock(mapBlocks);
            mapBlocks = m_commsProvider.GridService.RequestNeighbourMapBlocks(minX -5, minY - 5, minX + 2, minY -3);
            remoteClient.SendMapBlock(mapBlocks);
            mapBlocks = m_commsProvider.GridService.RequestNeighbourMapBlocks(minX - 5, minY - 2, minX -3, minY + 5);
            remoteClient.SendMapBlock(mapBlocks);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="RegionHandle"></param>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <param name="flags"></param>
        public virtual void RequestTeleportToLocation(ScenePresence avatar, ulong regionHandle, LLVector3 position,
                                            LLVector3 lookAt, uint flags)
        {
            if (regionHandle == m_regionInfo.RegionHandle)
            {

                avatar.ControllingClient.SendTeleportLocationStart();
                avatar.ControllingClient.SendLocalTeleport(position, lookAt, flags);
                avatar.Teleport(position);

            }
            else
            {
                RegionInfo reg = RequestNeighbouringRegionInfo(regionHandle);
                if (reg != null)
                {
                    avatar.ControllingClient.SendTeleportLocationStart();
                    AgentCircuitData agent = avatar.ControllingClient.RequestClientInfo();
                    agent.BaseFolder = LLUUID.Zero;
                    agent.InventoryFolder = LLUUID.Zero;
                    agent.startpos = position;
                    agent.child = true;
                    avatar.Close();
                    m_commsProvider.InterRegion.InformRegionOfChildAgent(regionHandle, agent);
                    m_commsProvider.InterRegion.ExpectAvatarCrossing(regionHandle, avatar.ControllingClient.AgentId, position, false);
                    AgentCircuitData circuitdata = avatar.ControllingClient.RequestClientInfo();
                    string capsPath = Util.GetCapsURL(avatar.ControllingClient.AgentId);
                    avatar.ControllingClient.SendRegionTeleport(regionHandle, 13, reg.ExternalEndPoint, 4, (1 << 4), capsPath);
                    avatar.MakeChildAgent();
                    if (KillObject != null)
                    {
                        KillObject(avatar.LocalId);
                    }
                    uint newRegionX = (uint)(regionHandle >> 40);
                    uint newRegionY = (((uint)(regionHandle)) >> 8);
                    uint oldRegionX = (uint)(m_regionInfo.RegionHandle >> 40);
                    uint oldRegionY = (((uint)(m_regionInfo.RegionHandle)) >> 8);
                    if (Util.fast_distance2d((int)(newRegionX - oldRegionX), (int)(newRegionY - oldRegionY)) > 3)
                    {
                        CloseChildAgentConnections(avatar);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionhandle"></param>
        /// <param name="agentID"></param>
        /// <param name="position"></param>
        public bool CrossToNeighbouringRegion(ulong regionhandle, LLUUID agentID, LLVector3 position, bool isFlying)
        {
            return m_commsProvider.InterRegion.ExpectAvatarCrossing(regionhandle, agentID, position, isFlying);
        }



        public bool PrimCrossToNeighboringRegion(ulong regionhandle, LLUUID primID, LLVector3 position, bool isPhysical)
        {
            return m_commsProvider.InterRegion.ExpectPrimCrossing(regionhandle, primID, position, isPhysical);
        }

        public void CloseChildAgentConnections(ScenePresence presence)
        {
            foreach (ulong regionHandle in presence.KnownChildRegions)
            {
                
                m_commsProvider.InterRegion.TellRegionToCloseChildConnection(regionHandle, presence.ControllingClient.AgentId);
                presence.RemoveNeighbourRegion(regionHandle);
            }
        }
    }
}

