using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Grid.ScriptServer
{
    // Maintains all regions
    class RegionsManager
    {
        private List<RegionConnectionManager> Regions = new List<RegionConnectionManager>();

                public ScriptServerMain m_ScriptServer;
        public RegionsManager(ScriptServerMain scriptServer)
        {
            m_ScriptServer = scriptServer;
        }

    }
}
