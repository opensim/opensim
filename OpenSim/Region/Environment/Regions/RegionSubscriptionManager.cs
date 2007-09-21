using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.Environment.Regions
{
    public class RegionSubscriptionManager
    {
        private Dictionary<uint, Region> m_regions;

        public RegionSubscriptionManager( )
        {
            m_regions = new Dictionary<uint, Region>( );
        }
    }
}
