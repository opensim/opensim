using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Grid.ScriptServer
{
    // Maintains all regions
    class RegionsManager
    {
        private List<RegionConnectionManager> Regions = new List<RegionConnectionManager>();

                public ScriptServer m_ScriptServer;
        public RegionsManager(ScriptServer scriptServer)
        {
            m_ScriptServer = scriptServer;
        }

    }
}
