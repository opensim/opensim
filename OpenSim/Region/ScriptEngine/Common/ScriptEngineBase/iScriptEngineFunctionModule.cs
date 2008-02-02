using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.ScriptEngine.Common.ScriptEngineBase
{
    public interface iScriptEngineFunctionModule
    {
        void ReadConfig();
        bool PleaseShutdown { get; set; }
    }
}