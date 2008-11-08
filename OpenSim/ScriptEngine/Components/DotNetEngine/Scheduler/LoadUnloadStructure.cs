using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.ScriptEngine.Shared;

namespace OpenSim.ScriptEngine.Components.DotNetEngine.Scheduler
{
    public struct LoadUnloadStructure
    {
        public ScriptStructure Script;
        public LUType Action;
        public bool PostOnRez;
        public int StartParam;

        public enum LUType
        {
            Unknown = 0,
            Load = 1,
            Unload = 2
        }
    }
}
