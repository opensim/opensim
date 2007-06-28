/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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

            // Default Engines
            CSharpScriptEngine csharpCompiler = new CSharpScriptEngine();
            compilers.Add(csharpCompiler.FileExt(),csharpCompiler);

            JScriptEngine jscriptCompiler = new JScriptEngine();
            compilers.Add(jscriptCompiler.FileExt(), jscriptCompiler);

            JSharpScriptEngine jsharpCompiler = new JSharpScriptEngine();
            compilers.Add(jsharpCompiler.FileExt(), jsharpCompiler);
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
        string FileExt();
    }
}
