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


        /// <summary>
        /// informs a neighbouring sim to expect a child agent
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
