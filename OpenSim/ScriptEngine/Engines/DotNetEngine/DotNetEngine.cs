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
using System.Text;
using System.Text.RegularExpressions;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.ApplicationPlugins.ScriptEngine;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.ScriptEngine.Components.DotNetEngine.Events;
using OpenSim.ScriptEngine.Components.DotNetEngine.Scheduler;
using OpenSim.ScriptEngine.Shared;
using ComponentFactory = OpenSim.ApplicationPlugins.ScriptEngine.ComponentFactory;

namespace OpenSim.ScriptEngine.Engines.DotNetEngine
{
    // This is a sample engine
    public partial class DotNetEngine : IScriptEngine
    {

        //private string[] _ComponentNames = new string[] {
        //            "Commands_LSL",
        //            "Commands_OSSL",
        //            "Compiler_CS",
        //            "Compiler_JS",
        //            "Compiler_LSL",
        //            "Compiler_VB",
        //            "LSLEventProvider",
        //            "Scheduler"
        //        };
        internal static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public string Name { get { return "SECS.DotNetEngine"; } }
        //public bool IsSharedModule { get { return true; } }
        internal RegionInfoStructure RegionInfo;

        private string[] commandNames = new string[]
            {
                "Commands_LSL"
            };

        private string[] compilerNames = new string[]
            {
                "Compiler_CS",
                "Compiler_JS",
                "Compiler_LSL",
                "Compiler_YP",
                "Compiler_VB"
            };
        private string[] schedulerNames = new string[]
            {
                "Scheduler"
            };

        //internal IScriptLoader m_ScriptLoader;
        internal LSLEventProvider m_LSLEventProvider;
        //internal IScriptExecutor m_Executor;
        //internal Dictionary<string, IScriptCompiler> Compilers = new Dictionary<string, IScriptCompiler>();
        internal Dictionary<string, IScriptScheduler> Schedulers = new Dictionary<string, IScriptScheduler>();
        public static Dictionary<string, IScriptCompiler> Compilers = new Dictionary<string, IScriptCompiler>();
//        private static bool haveInitialized = false;

        public DotNetEngine()
        {
            RegionInfo = new RegionInfoStructure();
            RegionInfo.Compilers = Compilers;
            RegionInfo.Schedulers = Schedulers;
            RegionInfo.Executors = new Dictionary<string, IScriptExecutor>();
            RegionInfo.CommandProviders = new Dictionary<string, IScriptCommandProvider>();
            RegionInfo.EventProviders = new Dictionary<string, IScriptEventProvider>();
        }

        public void Initialise(Scene scene, IConfigSource source)
        {
            RegionInfo.Scene = scene;
            RegionInfo.ConfigSource = source;

            m_log.DebugFormat("[{0}] Initializing components", Name);
            InitializeComponents();
        }

        public void PostInitialise() { }

        /// <summary>
        /// Called on region close
        /// </summary>
        public void Close()
        {
            ComponentClose();
        }
        #region Initialize the Script Engine Components we need
        public void InitializeComponents()
        {
            string cname = "";
            m_log.DebugFormat("[{0}] Component initialization", Name);
            // Initialize an instance of all module we want
            try
            {
                cname = "ScriptManager";
                m_log.DebugFormat("[{0}] Executor: {1}", Name, cname);
                RegionInfo.Executors.Add(cname,
                                         ComponentFactory.GetComponentInstance(RegionInfo, cname) as IScriptExecutor);

                cname = "ScriptLoader";
                m_log.DebugFormat("[{0}] ScriptLoader: {1}", Name, cname);
                RegionInfo.ScriptLoader =
                    ComponentFactory.GetComponentInstance(RegionInfo, cname) as IScriptExecutor as ScriptLoader;

                // CommandProviders
                foreach (string cn in commandNames)
                {
                    cname = cn;
                    m_log.DebugFormat("[{0}] CommandProvider: {1}", Name, cname);
                    RegionInfo.CommandProviders.Add(cname,
                                             ComponentFactory.GetComponentInstance(RegionInfo, cname) as
                                             IScriptCommandProvider);
                }

                // Compilers
                foreach (string cn in compilerNames)
                {
                    cname = cn;
                    m_log.DebugFormat("[{0}] Compiler: {1}", Name, cname);
                    RegionInfo.Compilers.Add(cname,
                                             ComponentFactory.GetComponentInstance(RegionInfo, cname) as
                                             IScriptCompiler);
                }

                // Schedulers
                foreach (string cn in schedulerNames)
                {
                    cname = cn;
                    m_log.DebugFormat("[{0}] Scheduler: {1}", Name, cname);
                    RegionInfo.Schedulers.Add(cname,
                                              ComponentFactory.GetComponentInstance(RegionInfo, cname) as
                                              IScriptScheduler);
                }

                // Event provider
                cname = "LSLEventProvider";
                m_log.DebugFormat("[{0}] EventProvider: {1}", Name, cname);
                IScriptEventProvider sep = ComponentFactory.GetComponentInstance(RegionInfo, cname) as IScriptEventProvider;
                RegionInfo.EventProviders.Add(cname, sep);
                m_LSLEventProvider = sep as LSLEventProvider;

                // Hook up events
                m_LSLEventProvider.RezScript += Events_RezScript;
                m_LSLEventProvider.RemoveScript += Events_RemoveScript;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[{0}] Exception while loading \"{1}\": {2}", Name, cname, e.ToString());
            }
        }

        private void ComponentClose()
        {
            // Close schedulers
            foreach (IScriptScheduler scheduler in RegionInfo.Schedulers.Values)
            {
                scheduler.Close();
            }
        }

        #endregion

    }
}
