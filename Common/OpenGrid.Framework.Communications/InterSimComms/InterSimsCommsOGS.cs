using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Types;
using OpenSim.Framework;

namespace OpenGrid.Framework.Communications
{
    public class InterSimsCommsOGS : InterSimsCommsBase
    {
        public override bool InformNeighbourOfChildAgent(ulong regionHandle, AgentCircuitData agentData) //should change from agentCircuitData
        {
            return false;
        }
    }
}
