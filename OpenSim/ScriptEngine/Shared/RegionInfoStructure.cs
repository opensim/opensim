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
 *     * Neither the name of the OpenSimulator Project nor the
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

using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.ScriptEngine.Shared;
using EventParams = OpenSim.ScriptEngine.Shared.EventParams;

namespace OpenSim.ScriptEngine.Shared
{
    public struct RegionInfoStructure
    {
        public Scene Scene;
        public IConfigSource ConfigSource;

        public IScriptLoader ScriptLoader;
        public Dictionary<string, IScriptEventProvider> EventProviders;
        public Dictionary<string, IScriptExecutor> Executors;
        public Dictionary<string, IScriptCompiler> Compilers;
        public Dictionary<string, IScriptScheduler> Schedulers;
        public Dictionary<string, IScriptCommandProvider> CommandProviders;

        public void Executors_Execute(EventParams p)
        {
            // Execute a command on all executors
            lock (Executors)
            {
                foreach (IScriptExecutor exec in Executors.Values)
                {
                    exec.ExecuteCommand(p);
                }
            }
        }
        public void Executors_Execute(ScriptStructure scriptContainer, EventParams p)
        {
            // Execute a command on all executors
            lock (Executors)
            {
                foreach (IScriptExecutor exec in Executors.Values)
                {
                    exec.ExecuteCommand(ref scriptContainer, p);
                }
            }
        }

        public IScriptCompiler FindCompiler(ScriptMetaData scriptMetaData)
        {
            string compiler = "Compiler_LSL";
            if (scriptMetaData.ContainsKey("Compiler"))
                compiler = scriptMetaData["Compiler"];

            lock (Compilers)
            {
                if (!Compilers.ContainsKey(compiler))
                    throw new Exception("Requested script compiler \"" + compiler + "\" does not exist.");

                return Compilers[compiler];
            }
        }

        public IScriptScheduler FindScheduler(ScriptMetaData scriptMetaData)
        {
            string scheduler = "Scheduler";
            if (scriptMetaData.ContainsKey("Scheduler"))
                scheduler = scriptMetaData["Scheduler"];

            lock (Schedulers)
            {
                if (!Schedulers.ContainsKey(scheduler))
                    throw new Exception("Requested script scheduler \"" + scheduler + "\" does not exist.");

            return Schedulers[scheduler];
            }
        }

        //public Assembly[] GetCommandProviderAssemblies()
        //{
        //    lock (CommandProviders)
        //    {
        //        Assembly[] ass = new Assembly[CommandProviders.Count];
        //        int i = 0;
        //        foreach (string key in CommandProviders.Keys)
        //        {
        //            ass[i] = CommandProviders[key].GetType().Assembly;
        //            i++;
        //        }
        //        return ass;
        //    }
        //}
    }
}