/*
* Copyright (c) Contributors, http://opensimulator.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using Microsoft.JScript;
using OpenSim.Framework.Console;

namespace OpenSim.Region.ExtensionsScriptModule.JScript
{
    public class JScriptEngine : IScriptCompiler
    {
        public string FileExt()
        {
            return ".js";
        }

        private Dictionary<string, IScript> LoadDotNetScript(CodeDomProvider compiler, string filename)
        {
            CompilerParameters compilerParams = new CompilerParameters();
            CompilerResults compilerResults;
            compilerParams.GenerateExecutable = false;
            compilerParams.GenerateInMemory = true;
            compilerParams.IncludeDebugInformation = false;
            compilerParams.ReferencedAssemblies.Add("OpenSim.Region.ClientStack.dll");
            compilerParams.ReferencedAssemblies.Add("OpenSim.Region.Environment.dll");
            compilerParams.ReferencedAssemblies.Add("OpenSim.Region.ExtensionsScriptModule.dll");
            compilerParams.ReferencedAssemblies.Add("OpenSim.Framework.dll");
            compilerParams.ReferencedAssemblies.Add("libsecondlife.dll");
            compilerParams.ReferencedAssemblies.Add("System.dll");

            compilerResults = compiler.CompileAssemblyFromFile(compilerParams, filename);

            if (compilerResults.Errors.Count > 0)
            {
                MainLog.Instance.Error("Compile errors");
                foreach (CompilerError error in compilerResults.Errors)
                {
                    MainLog.Instance.Error(error.Line.ToString() + ": " + error.ErrorText.ToString());
                }
            }
            else
            {
                Dictionary<string, IScript> scripts = new Dictionary<string, IScript>();

                foreach (Type pluginType in compilerResults.CompiledAssembly.GetExportedTypes())
                {
                    Type testInterface = pluginType.GetInterface("IScript", true);

                    if (testInterface != null)
                    {
                        IScript script = (IScript)compilerResults.CompiledAssembly.CreateInstance(pluginType.ToString());

                        string scriptName = "JS.NET/" + script.Name;
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

        public Dictionary<string, IScript> compile(string filename)
        {
            JScriptCodeProvider jscriptProvider = new JScriptCodeProvider();
            return LoadDotNetScript(jscriptProvider, filename);
        }
    }
}
