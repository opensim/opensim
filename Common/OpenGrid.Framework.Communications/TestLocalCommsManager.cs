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

        public TestLocalCommsManager()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override RegionInfo LoadRegionConfigFromGridServer(LLUUID regionID)
        {
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public override IRegionCommsHost RegisterRegion(RegionInfo regionInfo)
        {
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
        /// <returns></returns>
        public override bool AvatarCrossingToRegion()
        {
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override IList RequestMapBlocks()
        {
            return null;
        }
    }
}
