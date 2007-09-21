using System.Collections.Generic;
using libsecondlife;

namespace OpenSim.Region.Environment.Regions
{
    public class Region
    {
        private Dictionary<LLUUID, RegionPresence> m_regionPresences;

        public Region()
        {
            m_regionPresences = new Dictionary<LLUUID, RegionPresence>( );
        }
    }
}
