using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework;
using OpenSim.Framework.Types;

using libsecondlife;

namespace OpenGrid.Framework.Communications.GridServer
{
    public class GridCommsManagerLocal : GridCommsManagerBase
    {
        public GridCommsManagerLocal()
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
