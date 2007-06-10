using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Types;
using OpenSim.Framework;

namespace OpenGrid.Framework.Communications.GridServer
{
    public class GridCommsManagerBase
    {
        public GridCommsManagerBase()
        {
        }
         /// <summary>
        /// 
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        internal virtual RegionCommsHostBase RegisterRegion(RegionInfo regionInfo)
        {
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public virtual List<RegionInfo> RequestNeighbours(RegionInfo regionInfo)
        {
            return null;
        }
       
    }
}
