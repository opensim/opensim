using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Types;
using OpenSim.Framework;

namespace OpenGrid.Framework.Communications.GridServer
{
    public class GridCommsManagerBase
    {
        public GridCommsManagerBase()
        {
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
        public virtual bool InformNeighbourOfChildAgent(ulong regionHandle, AgentCircuitData agentData) //should change from agentCircuitData
        {
           return false;
        }

        public virtual bool AddNewSession(ulong regionHandle, Login loginData)
        {
            return false;
        }

       
    }
}
