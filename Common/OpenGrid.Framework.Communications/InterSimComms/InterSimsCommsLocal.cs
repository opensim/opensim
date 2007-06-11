using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Types;
using OpenSim.Framework;

namespace OpenGrid.Framework.Communications
{
    public class InterSimsCommsLocal : InterSimsCommsBase
    {
        private LocalBackEndServices sandBoxManager;

        public InterSimsCommsLocal(LocalBackEndServices sandManager)
        {
            sandBoxManager = sandManager;
        }

        /// <summary>
        /// Informs a neighbouring sim to expect a child agent
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public override bool InformNeighbourOfChildAgent(ulong regionHandle, AgentCircuitData agentData) //should change from agentCircuitData
        {
            return sandBoxManager.InformNeighbourOfChildAgent(regionHandle, agentData);
        }
    }
}
