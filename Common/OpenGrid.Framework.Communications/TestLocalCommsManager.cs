using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenGrid.Framework.Communications
{
    public class TestLocalCommsManager : RegionServerCommsManager
    {
        protected Dictionary<uint , RegionInfo> regions = new Dictionary<uint,RegionInfo>();
        protected Dictionary<uint, RegionCommsHostBase> regionHosts = new Dictionary<uint, RegionCommsHostBase>();

        public TestLocalCommsManager()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public override RegionCommsHostBase RegisterRegion(RegionInfo regionInfo)
        {
            if (!this.regions.ContainsKey((uint)regionInfo.RegionHandle))
            {
                this.regions.Add((uint)regionInfo.RegionHandle, regionInfo);
                RegionCommsHostBase regionHost = new RegionCommsHostBase();
                this.regionHosts.Add((uint)regionInfo.RegionHandle, regionHost);

                return regionHost;
            }
      
            //already in our list of regions so for now lets return null
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public override List<RegionInfo> RequestNeighbours(RegionInfo regionInfo)
        {
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override bool InformNeighbourOfChildAgent(uint regionHandle, AgentCircuitData agentData) //should change from agentCircuitData
        {
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="loginData"></param>
        /// <returns></returns>
        public bool AddNewSession(uint regionHandle, Login loginData)
        {
            AgentCircuitData agent = new AgentCircuitData();
            agent.AgentID = loginData.Agent;
            agent.firstname = loginData.First;
            agent.lastname = loginData.Last;
            agent.SessionID = loginData.Session;
            agent.SecureSessionID = loginData.SecureSession;
            agent.circuitcode = loginData.CircuitCode;
            agent.BaseFolder = loginData.BaseFolder;
            agent.InventoryFolder = loginData.InventoryFolder;
            agent.startpos = new LLVector3(128, 128, 70);

            if (this.regionHosts.ContainsKey((uint)regionHandle))
            {
                this.regionHosts[(uint)regionHandle].TriggerExpectUser(agent);
                return true;
            }

            // region not found
            return false;
        }
    }
}
