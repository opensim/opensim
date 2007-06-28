using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Scripting
{
    public class ScriptManager
    {
        List<IScript> scripts = new List<IScript>();
        OpenSim.Region.Scenes.Scene scene;
        Dictionary<string, IScriptCompiler> compilers = new Dictionary<string, IScriptCompiler>();

        private void LoadFromCompiler(Dictionary<string, IScript> compiledscripts)
        {
            foreach (KeyValuePair<string, IScript> script in compiledscripts)
            {
                ScriptInfo scriptInfo = new ScriptInfo(scene); // Since each script could potentially corrupt their access with a stray assignment, making a new one for each script.
                OpenSim.Framework.Console.MainLog.Instance.Verbose("Loading " + script.Key);
                script.Value.Initialise(scriptInfo);
                scripts.Add(script.Value);
            }
            OpenSim.Framework.Console.MainLog.Instance.Verbose("Finished loading " + compiledscripts.Count.ToString() + " script(s)");
        }

        public ScriptManager(OpenSim.Region.Scenes.Scene world)
        {
            scene = world;

            // Defualt Engines
            CSharpScriptEngine csharpCompiler = new CSharpScriptEngine();
            compilers.Add(csharpCompiler.fileExt(),csharpCompiler);
        }

        public void Compile(string filename)
        {
            foreach (KeyValuePair<string, IScriptCompiler> compiler in compilers)
            {
                if (filename.EndsWith(compiler.Key))
                {
                    LoadFromCompiler(compiler.Value.compile(filename));
                    break;
                }
            }
        }

        public void RunScriptCmd(string[] args)
        {
            switch (args[0])
            {
                case "load":
                    Compile(args[1]);
                    break;

                default:
                    OpenSim.Framework.Console.MainLog.Instance.Error("Unknown script command");
                    break;
            }
        }
    }

    interface IScriptCompiler
    {
        Dictionary<string,IScript> compile(string filename);
        string fileExt();
    }
}
