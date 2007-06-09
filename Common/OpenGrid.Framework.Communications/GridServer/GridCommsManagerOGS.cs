using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework;
using OpenSim.Framework.Types;

namespace OpenGrid.Framework.Communications.GridServer
{
    public class GridCommsManagerOGS : GridCommsManagerBase
    {
        public GridCommsManagerOGS()
        {
        }

        public override RegionCommsHostBase RegisterRegion(RegionInfo regionInfo)
        {
            return null;
        }

        public override List<RegionInfo> RequestNeighbours(RegionInfo regionInfo)
        {
            return null;
        }

        public override bool InformNeighbourOfChildAgent(ulong regionHandle, AgentCircuitData agentData) //should change from agentCircuitData
        {
            return false;
        }

        public override bool AddNewSession(ulong regionHandle, Login loginData)
        {
            return false;
        }
    }
}
