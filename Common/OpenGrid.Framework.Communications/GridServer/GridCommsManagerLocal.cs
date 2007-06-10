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
        private SandBoxManager sandBoxManager;

        public GridCommsManagerLocal(SandBoxManager sandManager)
        {
            sandBoxManager = sandManager;
        }

        public override RegionCommsHostBase RegisterRegion(RegionInfo regionInfo)
        {
            return sandBoxManager.RegisterRegion(regionInfo);
        }

        public override List<RegionInfo> RequestNeighbours(RegionInfo regionInfo)
        {
            return sandBoxManager.RequestNeighbours(regionInfo);
        }
    }
}
