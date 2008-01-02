using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.ScriptEngine.DotNetEngine;

namespace OpenSim.Grid.ScriptServer
{
    public class RemotingObject : MarshalByRefObject 
    {
        // This object will be exposed over remoting. It is a singleton, so it exists only in as one instance.

        // Expose ScriptEngine directly for now ... this is not very secure :)
        // NOTE! CURRENTLY JUST HARDWIRED DOTNETENGINE!
        public OpenSim.Region.ScriptEngine.DotNetEngine.ScriptEngine ScriptEngine = new OpenSim.Region.ScriptEngine.DotNetEngine.ScriptEngine();


        /// <summary>
        /// Receives calls from remote grids.
        /// </summary>
        /// <returns></returns>
        public OpenSim.Region.ScriptEngine.DotNetEngine.ScriptEngine GetScriptEngine()
        {
            return ScriptEngine;
        }
    }
}
