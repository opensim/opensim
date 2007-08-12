using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.ScriptEngine.DotNetEngine.Compiler;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL
{
    class LSL_BaseClass
    {
        public UInt32 State = 0;
        public LSL_BuiltIn_Commands_Interface LSL_Builtins;

        public void Start(LSL_BuiltIn_Commands_Interface LSLBuiltins)
        {
            LSL_Builtins = LSLBuiltins;
            Common.SendToLog("OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL.LSL_BaseClass.Start() called");

            return;
        }
    }
}
