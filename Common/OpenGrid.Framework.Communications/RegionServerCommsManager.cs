using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenGrid.Framework.Communications
{
    public class RegionServerCommsManager
    {

        public RegionServerCommsManager()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public virtual IRegionCommsHost RegisterRegion(RegionInfo regionInfo)
        {
            return null;
        }

        public virtual bool InformNeighbourChildAgent()
        {
            return false;
        }
    }
}
