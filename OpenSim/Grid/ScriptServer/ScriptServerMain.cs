using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using OpenSim.Framework.Console;
using OpenSim.Framework.Utilities;

namespace OpenSim.Grid.ScriptServer
{
    class ScriptServerMain : conscmd_callback
    {
        private readonly string m_logFilename = ("region-console.log");
        public RegionScriptDaemon RegionScriptDaemon;           // Listen for incoming from region
        public RegionsManager RegionManager;                    // Handle regions
        public ScriptEngineLoader ScriptEngineLoader;           // Loads scriptengines
        private LogBase m_log;

        public ScriptServerMain()
        {
            m_log = CreateLog();

            RegionScriptDaemon = new RegionScriptDaemon(this);
            RegionManager = new RegionsManager(this);
            ScriptEngineLoader = new ScriptEngineLoader(m_log);
        }

        ~ScriptServerMain()
        {
        }

        protected LogBase CreateLog()
        {
            if (!Directory.Exists(Util.logDir()))
            {
                Directory.CreateDirectory(Util.logDir());
            }

            return new LogBase((Path.Combine(Util.logDir(), m_logFilename)), "Region", this, false);
        }

        public void RunCmd(string command, string[] cmdparams)
        {
        }
        public void Show(string ShowWhat)
        {
        }

    }
}
