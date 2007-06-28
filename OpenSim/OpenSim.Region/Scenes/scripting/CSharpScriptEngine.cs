using System;
using System.Collections.Generic;
using System.Text;

// Compilation stuff
using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CSharp;

namespace OpenSim.Scripting
{
    public class CSharpScriptEngine : IScriptCompiler
    {
        public string fileExt()
        {
            return ".cs";
        }

        private Dictionary<string,IScript> LoadDotNetScript(ICodeCompiler compiler, string filename)
        {
            CompilerParameters compilerParams = new CompilerParameters();
            CompilerResults compilerResults;
            compilerParams.GenerateExecutable = false;
            compilerParams.GenerateInMemory = true;
            compilerParams.IncludeDebugInformation = false;
            compilerParams.ReferencedAssemblies.Add("OpenSim.Region.dll");
            compilerParams.ReferencedAssemblies.Add("OpenSim.Framework.dll");
            compilerParams.ReferencedAssemblies.Add("libsecondlife.dll");
            compilerParams.ReferencedAssemblies.Add("System.dll");

            compilerResults = compiler.CompileAssemblyFromFile(compilerParams, filename);

            if (compilerResults.Errors.Count > 0)
            {
                OpenSim.Framework.Console.MainLog.Instance.Error("Compile errors");
                foreach (CompilerError error in compilerResults.Errors)
                {
                    OpenSim.Framework.Console.MainLog.Instance.Error(error.Line.ToString() + ": " + error.ErrorText.ToString());
                }
            }
            else
            {
                Dictionary<string,IScript> scripts = new Dictionary<string,IScript>();

                foreach (Type pluginType in compilerResults.CompiledAssembly.GetExportedTypes())
                {
                    Type testInterface = pluginType.GetInterface("IScript", true);

                    if (testInterface != null)
                    {
                        IScript script = (IScript)compilerResults.CompiledAssembly.CreateInstance(pluginType.ToString());

                        string scriptName = "C#/" + script.getName();
                        Console.WriteLine("Script: " + scriptName + " loaded.");

                        if (!scripts.ContainsKey(scriptName))
                        {
                            scripts.Add(scriptName, script);
                        }
                        else
                        {
                            scripts[scriptName] = script;
                        }
                    }
                }
                return scripts;
            }
            return null;
        }

        public Dictionary<string,IScript> compile(string filename)
        {
            CSharpCodeProvider csharpProvider = new CSharpCodeProvider();
            return LoadDotNetScript(csharpProvider.CreateCompiler(), filename);
        }
    }
}
