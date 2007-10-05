using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Console;

namespace OpenSim.Grid.ScriptServer
{
    internal class ScriptEngineManager
    {
        private LogBase m_log;
        private ScriptEngineLoader ScriptEngineLoader;
        private List<ScriptEngineInterface> scriptEngines = new List<ScriptEngineInterface>();
        private ScriptServerMain m_ScriptServerMain;

        // Initialize
        public ScriptEngineManager(ScriptServerMain scm, LogBase logger)
        {
            m_ScriptServerMain = scm;
            m_log = logger;
            ScriptEngineLoader = new ScriptEngineLoader(m_log);
            
            // Temp - we should not load during initialize... Loading should be done later.
            LoadEngine("DotNetScriptEngine");
        }
        ~ScriptEngineManager()
        {
        }

        public void LoadEngine(string engineName)
        {
            // Load and add to list of ScriptEngines
            ScriptEngineInterface sei = ScriptEngineLoader.LoadScriptEngine(engineName);
            if (sei != null)
            {
                scriptEngines.Add(sei);
            }
        }

        
    }
}
