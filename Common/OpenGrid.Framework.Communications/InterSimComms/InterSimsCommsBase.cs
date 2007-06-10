using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Types;
using OpenSim.Framework;

namespace OpenGrid.Framework.Communications
{
    public abstract class InterSimsCommsBase
    {
        /// <summary>
        /// Informs a neighbouring sim to expect a child agent
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agentData"></param>
        /// <returns></returns>
        public abstract bool InformNeighbourOfChildAgent(ulong regionHandle, AgentCircuitData agentData);
    }
}
