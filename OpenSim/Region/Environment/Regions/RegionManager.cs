using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.Environment.Regions
{
    public class RegionManager
    {
        private Dictionary<uint, Region> m_regions;

        public RegionManager( )
        {
            m_regions = new Dictionary<uint, Region>( );
        }
    }
}
