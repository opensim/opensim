using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Region.Scripting;
using OpenSim.Region.Scripting.LSL;

namespace OpenSim.Region.Scripting.LSL
{
    class LSLScript : IScript
    {
        ScriptInfo scriptInfo;
        LSL.Engine lindenScriptEngine;

        public LSLScript(string filename, libsecondlife.LLUUID taskObject)
        {
            scriptInfo.CreateTaskAPI(taskObject);

            lindenScriptEngine = new Engine();
            lindenScriptEngine.Start(filename);
        }

        public void Initialise(ScriptInfo info)
        {
            scriptInfo = info;
        }

        public string Name
        {
            get { return "LSL Script"; }
        }
    }
}
