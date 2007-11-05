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
    public class SceneCommunicationService //one instance per region
    {
        protected CommunicationsManager m_commsProvider;
        protected RegionInfo m_regionInfo;

        protected RegionCommsListener regionCommsHost;

        public event AgentCrossing OnAvatarCrossingIntoRegion;
        public event ExpectUserDelegate OnExpectUser;


        public SceneCommunicationService(CommunicationsManager commsMan)
        {
            m_commsProvider = commsMan;
        }

        public void RegisterRegion(RegionInfo regionInfos)
        {
            m_regionInfo = regionInfos;
            regionCommsHost = m_commsProvider.GridService.RegisterRegion(m_regionInfo);
            if (regionCommsHost != null)
            {
                regionCommsHost.OnExpectUser += NewUserConnection;
                regionCommsHost.OnAvatarCrossingIntoRegion += AgentCrossing;
            }
        }

        public void Close()
        {
            regionCommsHost.OnExpectUser -= NewUserConnection;
            regionCommsHost.OnAvatarCrossingIntoRegion -= AgentCrossing;
            //regionCommsHost.RemoveRegion(m_regionInfo); //TODO add to method to commsManager
            regionCommsHost = null;
        }

        #region CommsManager Event handlers
        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agent"></param>
        public void NewUserConnection(ulong regionHandle, AgentCircuitData agent)
        {
            if (OnExpectUser != null)
            {
                OnExpectUser(regionHandle, agent);
            }
        }

        public void AgentCrossing(ulong regionHandle, LLUUID agentID, LLVector3 position, bool isFlying)
        {
            if (OnAvatarCrossingIntoRegion != null)
            {
                OnAvatarCrossingIntoRegion(regionHandle, agentID, position, isFlying);
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

        /// <summary>
        /// 
        /// </summary>
        public void InformClientOfNeighbours(ScenePresence avatar)
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
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public virtual RegionInfo RequestNeighbouringRegionInfo(ulong regionHandle)
        {
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
            mapBlocks = m_commsProvider.GridService.RequestNeighbourMapBlocks(minX, minY, maxX, maxY);
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
        public virtual void RequestTeleportLocation(ScenePresence avatar, ulong regionHandle, LLVector3 position,
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
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionhandle"></param>
        /// <param name="agentID"></param>
        /// <param name="position"></param>
        public bool InformNeighbourOfCrossing(ulong regionhandle, LLUUID agentID, LLVector3 position, bool isFlying)
        {
            return m_commsProvider.InterRegion.ExpectAvatarCrossing(regionhandle, agentID, position, isFlying);
        }

        public void CloseChildAgentConnections(ScenePresence presence)
        {

        }
    }
}

