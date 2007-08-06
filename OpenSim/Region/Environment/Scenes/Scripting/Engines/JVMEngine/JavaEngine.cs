using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Region.Scripting;
using OpenSim.Region.Scripting.EmbeddedJVM;

namespace OpenSim.Region.Scripting
{
    public class JavaEngine : IScriptCompiler
    {
        public string FileExt()
        {
            return ".java";
        }

        public Dictionary<string, IScript> compile(string filename)
        {
            JVMScript script = new JVMScript();
            Dictionary<string, IScript> returns = new Dictionary<string, IScript>();

            script.LoadScript(filename);

            returns.Add(filename, script);

            return returns;
        }
    }
}
