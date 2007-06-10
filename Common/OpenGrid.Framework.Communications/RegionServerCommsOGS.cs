using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework;
using OpenSim.Framework.Types;

namespace OpenGrid.Framework.Communications
{
    public class RegionServerCommsOGS : RegionServerCommsManager
    {
        public RegionServerCommsOGS()
        {
            userServer = new UserServer.UserCommsManagerOGS(); //Remote User Server
            gridServer = new GridServer.GridCommsManagerOGS(); //Remote Grid Server
        }

        public override RegionCommsHostBase RegisterRegion(RegionInfo regionInfo)
        {
            return gridServer.RegisterRegion(regionInfo);
        }

        /// <summary>
        /// In the current class structure this shouldn't be here as it should only be in the gridserver class
        /// but having it there in sandbox mode makes things very difficult, so for now until something is sorted out 
        /// it will have to be here as well
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public override List<RegionInfo> RequestNeighbours(RegionInfo regionInfo)
        {
            return gridServer.RequestNeighbours(regionInfo);
        }

        /// <summary>
        /// informs a neighbouring sim to expect a child agent
        /// I guess if we are going to stick with the current class structure then we need a intersim class
        /// but think we need to really rethink the class structure as currently it makes things very messy for sandbox mode 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public override bool InformNeighbourOfChildAgent(ulong regionHandle, AgentCircuitData agentData) //should change from agentCircuitData
        {
            return false;
        }
    }
}
