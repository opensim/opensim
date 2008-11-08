using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.ScriptEngine.Shared;
using EventParams=OpenSim.ScriptEngine.Shared.EventParams;

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
        public ILog Logger;

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