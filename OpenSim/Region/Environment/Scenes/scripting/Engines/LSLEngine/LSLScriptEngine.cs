using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Region.Scripting;
using OpenSim.Region.Scripting.LSL;

namespace OpenSim.Region.Scripting
{
    public class LSLEngine : IScriptCompiler
    {
        public string FileExt()
        {
            return ".lso";
        }

        public Dictionary<string, IScript> compile(string filename)
        {
            LSLScript script = new LSLScript(filename, libsecondlife.LLUUID.Zero);
            Dictionary<string, IScript> returns = new Dictionary<string, IScript>();

            returns.Add(filename, script);

            return returns;
        }
    }
}