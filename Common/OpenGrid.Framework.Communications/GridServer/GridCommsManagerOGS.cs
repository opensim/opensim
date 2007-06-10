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

        internal override RegionCommsHostBase RegisterRegion(RegionInfo regionInfo)
        {
            return null;
        }

        public override List<RegionInfo> RequestNeighbours(RegionInfo regionInfo)
        {
            return null;
        }
    }
}
