using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using libsecondlife;

namespace OpenGrid.Framework.Communications
{
 
    public class RegionServerCommsManager
    {

        public RegionServerCommsManager()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual RegionInfo LoadRegionConfigFromGridServer(LLUUID regionID)
        {
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public virtual RegionCommsHostBase RegisterRegion(RegionInfo regionInfo)
        {
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public virtual List<RegionInfo> RequestNeighbours(RegionInfo regionInfo)
        {
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual bool InformNeighbourOfChildAgent( ulong regionHandle, AgentCircuitData agentData) //should change from agentCircuitData
        {
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual bool AvatarCrossingToRegion()
        {
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual IList RequestMapBlocks()
        {
            return null;
        }
    }
}
