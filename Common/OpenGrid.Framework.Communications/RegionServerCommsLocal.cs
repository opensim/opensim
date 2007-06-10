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
    public class RegionServerCommsLocal : RegionServerCommsManager
    {
        public SandBoxManager SandManager = new SandBoxManager();
        public RegionServerCommsLocal()
        {
            userServer = new UserServer.UserCommsManagerLocal(); //Local User Server
            gridServer = new GridServer.GridCommsManagerLocal(SandManager); //Locl Grid Server
        }

        /// <summary>
        /// informs a neighbouring sim to expect a child agent
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public override bool InformNeighbourOfChildAgent(ulong regionHandle, AgentCircuitData agentData) //should change from agentCircuitData
        {
            return SandManager.InformNeighbourOfChildAgent(regionHandle, agentData);
        }
    }
}
