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
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Collections.Generic;
using Nini.Config;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.ExtensionsScriptModule.CSharp;
using OpenSim.Region.ExtensionsScriptModule.JScript;
using OpenSim.Region.ExtensionsScriptModule.JVMEngine;

namespace OpenSim.Region.ExtensionsScriptModule
{
    public class ScriptManager : IRegionModule, IExtensionScriptModule
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly List<IScript> scripts = new List<IScript>();
        private Scene m_scene;
        private readonly Dictionary<string, IScriptCompiler> compilers = new Dictionary<string, IScriptCompiler>();

        private void LoadFromCompiler(Dictionary<string, IScript> compiledscripts)
        {
            foreach (KeyValuePair<string, IScript> script in compiledscripts)
            {
                ScriptInfo scriptInfo = new ScriptInfo(m_scene);
                // Since each script could potentially corrupt their access with a stray assignment, making a new one for each script.
                m_log.Info("[SCRIPT]: Loading " + script.Key);
                script.Value.Initialise(scriptInfo);
                scripts.Add(script.Value);
            }

            m_log.Info("[SCRIPT]: " + string.Format("Finished loading {0} script(s)", compiledscripts.Count));
        }

        public ScriptManager()
        {
            // Default Engines
            CSharpScriptEngine csharpCompiler = new CSharpScriptEngine();
            compilers.Add(csharpCompiler.FileExt(), csharpCompiler);

            JScriptEngine jscriptCompiler = new JScriptEngine();
            compilers.Add(jscriptCompiler.FileExt(), jscriptCompiler);

            JavaEngine javaCompiler = new JavaEngine();
            compilers.Add(javaCompiler.FileExt(), javaCompiler);
        }

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_log.Info("[SCRIPTMODULE]: Initialising Extensions Scripting Module");
            m_scene = scene;

            m_scene.RegisterModuleInterface<IExtensionScriptModule>(this);
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "ExtensionsScriptingModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        public bool Compile(string filename)
        {
            foreach (KeyValuePair<string, IScriptCompiler> compiler in compilers)
            {
                if (filename.EndsWith(compiler.Key))
                {
                    LoadFromCompiler(compiler.Value.compile(filename));
                    break;
                }
            }

            return true;
        }

        public void RunScriptCmd(string[] args)
        {
            switch (args[0])
            {
                case "load":
                    Compile(args[1]);
                    break;

                default:
                    m_log.Error("Unknown script command");
                    break;
            }
        }

        public bool AddPreCompiledScript(IScript script)
        {
            m_log.Info("[SCRIPT]: Loading script " + script.Name);
            ScriptInfo scriptInfo = new ScriptInfo(m_scene);
            // Since each script could potentially corrupt their access with a stray assignment, making a new one for each script.
            script.Initialise(scriptInfo);
            scripts.Add(script);

            return true;
        }
    }

    public interface IExtensionScriptModule
    {
        bool Compile(string filename);
        bool AddPreCompiledScript(IScript script);
    }

    internal interface IScriptCompiler
    {
        Dictionary<string, IScript> compile(string filename);
        string FileExt();
    }
}
