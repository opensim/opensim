using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Console;

namespace OpenSim.Grid.ScriptServer
{
    // Maintains connection and communication to a region
    public class RegionConnectionManager: RegionBase
    {
       private LogBase m_log;
        private ScriptServerMain m_ScriptServerMain;
        private object m_Connection;
        public RegionConnectionManager(ScriptServerMain scm, LogBase logger, object Connection)
        {
            m_ScriptServerMain = scm;
            m_log = logger;
            m_Connection = Connection;
        }

        private void DataReceived(object objectID, object scriptID)
        {
            // TODO: HOW DO WE RECEIVE DATA? ASYNC?
            // ANYHOW WE END UP HERE?

            // NEW SCRIPT? ASK SCRIPTENGINE TO INITIALIZE IT
            ScriptRez();

            // EVENT? DELIVER EVENT DIRECTLY TO SCRIPTENGINE
            touch_start();

        }

    }
}
