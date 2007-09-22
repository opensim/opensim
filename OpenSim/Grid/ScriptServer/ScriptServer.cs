using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Grid.ScriptServer
{
    class ScriptServer
    {
        public RegionScriptDaemon RegionScriptDaemon;           // Listen for incoming from region
        public RegionsManager RegionManager;                    // Handle regions
        public ScriptEngineLoader ScriptEngineLoader;           // Loads scriptengines

        public ScriptServer()
        {
            RegionScriptDaemon = new RegionScriptDaemon(this);
            RegionManager = new RegionsManager(this);
            //ScriptEngineLoader = new ScriptEngineLoader(this);
        }

        ~ScriptServer()
        {
        }



    }
}
