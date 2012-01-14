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
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics; //for [DebuggerNonUserCode]
using System.Security;
using System.Security.Policy;
using System.Reflection;
using System.Globalization;
using System.Xml;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using Nini.Config;
using Amib.Threading;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Shared.CodeTools;
using OpenSim.Region.ScriptEngine.Shared.Instance;
using OpenSim.Region.ScriptEngine.Interfaces;

using ScriptCompileQueue = OpenSim.Framework.LocklessQueue<object[]>;

namespace OpenSim.Region.ScriptEngine.XEngine
{
    public class XEngine : INonSharedRegionModule, IScriptModule, IScriptEngine
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private SmartThreadPool m_ThreadPool;
        private int m_MaxScriptQueue;
        private Scene m_Scene;
        private IConfig m_ScriptConfig = null;
        private IConfigSource m_ConfigSource = null;
        private ICompiler m_Compiler;
        private int m_MinThreads;
        private int m_MaxThreads ;
        private int m_IdleTimeout;
        private int m_StackSize;
        private int m_SleepTime;
        private int m_SaveTime;
        private ThreadPriority m_Prio;
        private bool m_Enabled = false;
        private bool m_InitialStartup = true;
        private int m_ScriptFailCount; // Number of script fails since compile queue was last empty
        private string m_ScriptErrorMessage;
        private Dictionary<string, string> m_uniqueScripts = new Dictionary<string, string>();
        private bool m_AppDomainLoading;
        private Dictionary<UUID,ArrayList> m_ScriptErrors =
                new Dictionary<UUID,ArrayList>();

        // disable warning: need to keep a reference to XEngine.EventManager
        // alive to avoid it being garbage collected
#pragma warning disable 414
        private EventManager m_EventManager;
#pragma warning restore 414
        private IXmlRpcRouter m_XmlRpcRouter;
        private int m_EventLimit;
        private bool m_KillTimedOutScripts;
        private string m_ScriptEnginesPath = null;

        /// <summary>
        /// Is the entire simulator in the process of shutting down?
        /// </summary>
        private bool m_SimulatorShuttingDown;

        private static List<XEngine> m_ScriptEngines =
                new List<XEngine>();

        // Maps the local id to the script inventory items in it

        private Dictionary<uint, List<UUID> > m_PrimObjects =
                new Dictionary<uint, List<UUID> >();

        // Maps the UUID above to the script instance

        private Dictionary<UUID, IScriptInstance> m_Scripts =
                new Dictionary<UUID, IScriptInstance>();

        private OpenMetaverse.ReaderWriterLockSlim m_scriptsLock = new OpenMetaverse.ReaderWriterLockSlim();

        // Maps the asset ID to the assembly

        private Dictionary<UUID, string> m_Assemblies =
                new Dictionary<UUID, string>();

        private Dictionary<string, int> m_AddingAssemblies =
                new Dictionary<string, int>();

        // This will list AppDomains by script asset

        private Dictionary<UUID, AppDomain> m_AppDomains =
                new Dictionary<UUID, AppDomain>();

        // List the scripts running in each appdomain

        private Dictionary<UUID, List<UUID> > m_DomainScripts =
                new Dictionary<UUID, List<UUID> >();

        private ScriptCompileQueue m_CompileQueue = new ScriptCompileQueue();
        IWorkItemResult m_CurrentCompile = null;
        private Dictionary<UUID, int> m_CompileDict = new Dictionary<UUID, int>();

        private void lockScriptsForRead(bool locked)
        {
            if (locked)
            {
                if (m_scriptsLock.RecursiveReadCount > 0)
                {
                    m_log.Error("[XEngine.m_Scripts] Recursive read lock requested. This should not happen and means something needs to be fixed. For now though, it's safe to continue.");
                    m_scriptsLock.ExitReadLock();
                }
                if (m_scriptsLock.RecursiveWriteCount > 0)
                {
                    m_log.Error("[XEngine.m_Scripts] Recursive write lock requested. This should not happen and means something needs to be fixed.");
                    m_scriptsLock.ExitWriteLock();
                }

                while (!m_scriptsLock.TryEnterReadLock(60000))
                {
                    m_log.Error("[XEngine.m_Scripts] Thread lock detected while trying to aquire READ lock of m_scripts in XEngine. I'm going to try to solve the thread lock automatically to preserve region stability, but this needs to be fixed.");
                    if (m_scriptsLock.IsWriteLockHeld)
                    {
                        m_scriptsLock = new OpenMetaverse.ReaderWriterLockSlim();
                    }
                }
            }
            else
            {
                if (m_scriptsLock.RecursiveReadCount > 0)
                {
                    m_scriptsLock.ExitReadLock();
                }
            }
        }
        private void lockScriptsForWrite(bool locked)
        {
            if (locked)
            {
                if (m_scriptsLock.RecursiveReadCount > 0)
                {
                    m_log.Error("[XEngine.m_Scripts] Recursive read lock requested. This should not happen and means something needs to be fixed. For now though, it's safe to continue.");
                    m_scriptsLock.ExitReadLock();
                }
                if (m_scriptsLock.RecursiveWriteCount > 0)
                {
                    m_log.Error("[XEngine.m_Scripts] Recursive write lock requested. This should not happen and means something needs to be fixed.");
                    m_scriptsLock.ExitWriteLock();
                }

                while (!m_scriptsLock.TryEnterWriteLock(60000))
                {
                    m_log.Error("[XEngine.m_Scripts] Thread lock detected while trying to aquire WRITE lock of m_scripts in XEngine. I'm going to try to solve the thread lock automatically to preserve region stability, but this needs to be fixed.");
                    if (m_scriptsLock.IsWriteLockHeld)
                    {
                        m_scriptsLock = new OpenMetaverse.ReaderWriterLockSlim();
                    }
                }
            }
            else
            {
                if (m_scriptsLock.RecursiveWriteCount > 0)
                {
                    m_scriptsLock.ExitWriteLock();
                }
            }
        }

        public string ScriptEngineName
        {
            get { return "XEngine"; }
        }

        public Scene World
        {
            get { return m_Scene; }
        }

        public static List<XEngine> ScriptEngines
        {
            get { return m_ScriptEngines; }
        }

        public IScriptModule ScriptModule
        {
            get { return this; }
        }

        // private struct RezScriptParms
        // {
        //     uint LocalID;
        //     UUID ItemID;
        //     string Script;
        // }

        public IConfig Config
        {
            get { return m_ScriptConfig; }
        }

        public string ScriptEnginePath
        {
            get { return m_ScriptEnginesPath; }
        }

        public IConfigSource ConfigSource
        {
            get { return m_ConfigSource; }
        }

        public event ScriptRemoved OnScriptRemoved;
        public event ObjectRemoved OnObjectRemoved;

        //
        // IRegionModule functions
        //
        public void Initialise(IConfigSource configSource)
        {
            if (configSource.Configs["XEngine"] == null)
                return;

            m_ScriptConfig = configSource.Configs["XEngine"];
            m_ConfigSource = configSource;
        }

        public void AddRegion(Scene scene)
        {
            if (m_ScriptConfig == null)
                return;
            m_ScriptFailCount = 0;
            m_ScriptErrorMessage = String.Empty;

            if (m_ScriptConfig == null)
            {
//                m_log.ErrorFormat("[XEngine] No script configuration found. Scripts disabled");
                return;
            }

            m_Enabled = m_ScriptConfig.GetBoolean("Enabled", true);

            if (!m_Enabled)
                return;

            AppDomain.CurrentDomain.AssemblyResolve +=
                OnAssemblyResolve;

            m_log.InfoFormat("[XEngine] Initializing scripts in region {0}",
                             scene.RegionInfo.RegionName);
            m_Scene = scene;

            m_MinThreads = m_ScriptConfig.GetInt("MinThreads", 2);
            m_MaxThreads = m_ScriptConfig.GetInt("MaxThreads", 100);
            m_IdleTimeout = m_ScriptConfig.GetInt("IdleTimeout", 60);
            string priority = m_ScriptConfig.GetString("Priority", "BelowNormal");
            m_MaxScriptQueue = m_ScriptConfig.GetInt("MaxScriptEventQueue",300);
            m_StackSize = m_ScriptConfig.GetInt("ThreadStackSize", 262144);
            m_SleepTime = m_ScriptConfig.GetInt("MaintenanceInterval", 10) * 1000;
            m_AppDomainLoading = m_ScriptConfig.GetBoolean("AppDomainLoading", true);

            m_EventLimit = m_ScriptConfig.GetInt("EventLimit", 30);
            m_KillTimedOutScripts = m_ScriptConfig.GetBoolean("KillTimedOutScripts", false);
            m_SaveTime = m_ScriptConfig.GetInt("SaveInterval", 120) * 1000;
            m_ScriptEnginesPath = m_ScriptConfig.GetString("ScriptEnginesPath", "ScriptEngines");

            m_Prio = ThreadPriority.BelowNormal;
            switch (priority)
            {
                case "Lowest":
                    m_Prio = ThreadPriority.Lowest;
                    break;
                case "BelowNormal":
                    m_Prio = ThreadPriority.BelowNormal;
                    break;
                case "Normal":
                    m_Prio = ThreadPriority.Normal;
                    break;
                case "AboveNormal":
                    m_Prio = ThreadPriority.AboveNormal;
                    break;
                case "Highest":
                    m_Prio = ThreadPriority.Highest;
                    break;
                default:
                    m_log.ErrorFormat("[XEngine] Invalid thread priority: '{0}'. Assuming BelowNormal", priority);
                    break;
            }

            lock (m_ScriptEngines)
            {
                m_ScriptEngines.Add(this);
            }

            // Needs to be here so we can queue the scripts that need starting
            //
            m_Scene.EventManager.OnRezScript += OnRezScript;

            // Complete basic setup of the thread pool
            //
            SetupEngine(m_MinThreads, m_MaxThreads, m_IdleTimeout, m_Prio,
                        m_MaxScriptQueue, m_StackSize);

            m_Scene.StackModuleInterface<IScriptModule>(this);

            m_XmlRpcRouter = m_Scene.RequestModuleInterface<IXmlRpcRouter>();
            if (m_XmlRpcRouter != null)
            {
                OnScriptRemoved += m_XmlRpcRouter.ScriptRemoved;
                OnObjectRemoved += m_XmlRpcRouter.ObjectRemoved;
            }

            MainConsole.Instance.Commands.AddCommand(
                "scripts", false, "scripts show", "scripts show [<script-item-uuid>]", "Show script information",
                "Show information on all scripts known to the script engine."
                    + "If a <script-item-uuid> is given then only information on that script will be shown.",
                HandleShowScripts);

            MainConsole.Instance.Commands.AddCommand(
                "scripts", false, "show scripts", "show scripts [<script-item-uuid>]", "Show script information",
                "Synonym for scripts show command", HandleShowScripts);

            MainConsole.Instance.Commands.AddCommand(
                "scripts", false, "scripts suspend", "scripts suspend [<script-item-uuid>]", "Suspends all running scripts",
                "Suspends all currently running scripts.  This only suspends event delivery, it will not suspend a"
                    + " script that is currently processing an event.\n"
                    + "Suspended scripts will continue to accumulate events but won't process them.\n"
                    + "If a <script-item-uuid> is given then only that script will be suspended.  Otherwise, all suitable scripts are suspended.",
                 (module, cmdparams) => HandleScriptsAction(cmdparams, HandleSuspendScript));

            MainConsole.Instance.Commands.AddCommand(
                "scripts", false, "scripts resume", "scripts resume [<script-item-uuid>]", "Resumes all suspended scripts",
                "Resumes all currently suspended scripts.\n"
                    + "Resumed scripts will process all events accumulated whilst suspended."
                    + "If a <script-item-uuid> is given then only that script will be resumed.  Otherwise, all suitable scripts are resumed.",
                (module, cmdparams) => HandleScriptsAction(cmdparams, HandleResumeScript));

            MainConsole.Instance.Commands.AddCommand(
                "scripts", false, "scripts stop", "scripts stop [<script-item-uuid>]", "Stops all running scripts",
                "Stops all running scripts."
                    + "If a <script-item-uuid> is given then only that script will be stopped.  Otherwise, all suitable scripts are stopped.",
                (module, cmdparams) => HandleScriptsAction(cmdparams, HandleStopScript));

            MainConsole.Instance.Commands.AddCommand(
                "scripts", false, "scripts start", "scripts start [<script-item-uuid>]", "Starts all stopped scripts",
                "Starts all stopped scripts."
                    + "If a <script-item-uuid> is given then only that script will be started.  Otherwise, all suitable scripts are started.",
                (module, cmdparams) => HandleScriptsAction(cmdparams, HandleStartScript));
        }

        /// <summary>
        /// Parse the raw item id into a script instance from the command params if it's present.
        /// </summary>
        /// <param name="cmdparams"></param>
        /// <param name="instance"></param>
        /// <returns>true if we're okay to proceed, false if not.</returns>
        private void HandleScriptsAction(string[] cmdparams, Action<IScriptInstance> action)
        {
            lock (m_Scripts)
            {
                string rawItemId;
                UUID itemId = UUID.Zero;
    
                if (cmdparams.Length == 2)
                {
                    foreach (IScriptInstance instance in m_Scripts.Values)
                        action(instance);

                    return;
                }
    
                rawItemId = cmdparams[2];
    
                if (!UUID.TryParse(rawItemId, out itemId))
                {
                    MainConsole.Instance.OutputFormat("Error - {0} is not a valid UUID", rawItemId);
                    return;
                }
    
                if (itemId != UUID.Zero)
                {
                    IScriptInstance instance = GetInstance(itemId);
                    if (instance == null)
                    {
                        // Commented out for now since this will cause false reports on simulators with more than
                        // one scene where the current command line set region is 'root' (which causes commands to
                        // go to both regions... (sigh)
//                        MainConsole.Instance.OutputFormat("Error - No item found with id {0}", itemId);
                        return;
                    }
                    else
                    {
                        action(instance);
                        return;
                    }
                }
            }
        }

        public void HandleShowScripts(string module, string[] cmdparams)
        {
            if (cmdparams.Length == 2)
            {
                lock (m_Scripts)
                {
                    MainConsole.Instance.OutputFormat(
                        "Showing {0} scripts in {1}", m_Scripts.Count, m_Scene.RegionInfo.RegionName);
                }
            }

            HandleScriptsAction(cmdparams, HandleShowScript);
        }

        private void HandleShowScript(IScriptInstance instance)
        {
            SceneObjectPart sop = m_Scene.GetSceneObjectPart(instance.ObjectID);
            string status;

            if (instance.ShuttingDown)
            {
                status = "shutting down";
            }
            else if (instance.Suspended)
            {
                status = "suspended";
            }
            else if (!instance.Running)
            {
                status = "stopped";
            }
            else
            {
                status = "running";
            }

            MainConsole.Instance.OutputFormat(
                "{0}.{1}, item UUID {2}, prim UUID {3} @ {4} ({5})",
                instance.PrimName, instance.ScriptName, instance.ItemID, instance.ObjectID,
                sop.AbsolutePosition, status);
        }

        private void HandleSuspendScript(IScriptInstance instance)
        {
            if (!instance.Suspended)
            {
                instance.Suspend();

                SceneObjectPart sop = m_Scene.GetSceneObjectPart(instance.ObjectID);
                MainConsole.Instance.OutputFormat(
                    "Suspended {0}.{1}, item UUID {2}, prim UUID {3} @ {4}",
                    instance.PrimName, instance.ScriptName, instance.ItemID, instance.ObjectID, sop.AbsolutePosition);
            }
        }

        private void HandleResumeScript(IScriptInstance instance)
        {
            if (instance.Suspended)
            {
                instance.Resume();

                SceneObjectPart sop = m_Scene.GetSceneObjectPart(instance.ObjectID);
                MainConsole.Instance.OutputFormat(
                    "Resumed {0}.{1}, item UUID {2}, prim UUID {3} @ {4}",
                    instance.PrimName, instance.ScriptName, instance.ItemID, instance.ObjectID, sop.AbsolutePosition);
            }
        }

        private void HandleStartScript(IScriptInstance instance)
        {
            if (!instance.Running)
            {
                instance.Start();

                SceneObjectPart sop = m_Scene.GetSceneObjectPart(instance.ObjectID);
                MainConsole.Instance.OutputFormat(
                    "Started {0}.{1}, item UUID {2}, prim UUID {3} @ {4}",
                    instance.PrimName, instance.ScriptName, instance.ItemID, instance.ObjectID, sop.AbsolutePosition);
            }
        }

        private void HandleStopScript(IScriptInstance instance)
        {
            if (instance.Running)
            {
                instance.Stop(0);

                SceneObjectPart sop = m_Scene.GetSceneObjectPart(instance.ObjectID);
                MainConsole.Instance.OutputFormat(
                    "Stopped {0}.{1}, item UUID {2}, prim UUID {3} @ {4}",
                    instance.PrimName, instance.ScriptName, instance.ItemID, instance.ObjectID, sop.AbsolutePosition);
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;
            lockScriptsForRead(true);
            foreach (IScriptInstance instance in m_Scripts.Values)
            {
                // Force a final state save
                //
                if (m_Assemblies.ContainsKey(instance.AssetID))
                {
                    string assembly = m_Assemblies[instance.AssetID];
                    instance.SaveState(assembly);
                }

                // Clear the event queue and abort the instance thread
                //
                instance.ClearQueue();
                instance.Stop(0);

                // Release events, timer, etc
                //
                instance.DestroyScriptInstance();

                // Unload scripts and app domains
                // Must be done explicitly because they have infinite
                // lifetime
                //
                if (!m_SimulatorShuttingDown)
                {
                    m_DomainScripts[instance.AppDomain].Remove(instance.ItemID);
                    if (m_DomainScripts[instance.AppDomain].Count == 0)
                    {
                        m_DomainScripts.Remove(instance.AppDomain);
                        UnloadAppDomain(instance.AppDomain);
                    }
                }

                m_Scripts.Clear();
                m_PrimObjects.Clear();
                m_Assemblies.Clear();
                m_DomainScripts.Clear();
            }
            lockScriptsForRead(false);
            lockScriptsForWrite(true);
            m_Scripts.Clear();
            lockScriptsForWrite(false);
            m_PrimObjects.Clear();
            m_Assemblies.Clear();
            m_DomainScripts.Clear();
           
            lock (m_ScriptEngines)
            {
                m_ScriptEngines.Remove(this);
            }
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_EventManager = new EventManager(this);

            m_Compiler = new Compiler(this);

            m_Scene.EventManager.OnRemoveScript += OnRemoveScript;
            m_Scene.EventManager.OnScriptReset += OnScriptReset;
            m_Scene.EventManager.OnStartScript += OnStartScript;
            m_Scene.EventManager.OnStopScript += OnStopScript;
            m_Scene.EventManager.OnGetScriptRunning += OnGetScriptRunning;
            m_Scene.EventManager.OnShutdown += OnShutdown;

            if (m_SleepTime > 0)
            {
                m_ThreadPool.QueueWorkItem(new WorkItemCallback(this.DoMaintenance),
                                           new Object[]{ m_SleepTime });
            }

            if (m_SaveTime > 0)
            {
                m_ThreadPool.QueueWorkItem(new WorkItemCallback(this.DoBackup),
                                           new Object[] { m_SaveTime });
            }
        }

        public void StartProcessing()
        {
            m_ThreadPool.Start();
        }

        public void Close()
        {
            if (!m_Enabled)
                return;
            
            lock (m_ScriptEngines)
            {
                if (m_ScriptEngines.Contains(this))
                    m_ScriptEngines.Remove(this);
            }
        }

        public object DoBackup(object o)
        {
            Object[] p = (Object[])o;
            int saveTime = (int)p[0];

            if (saveTime > 0)
                System.Threading.Thread.Sleep(saveTime);

//            m_log.Debug("[XEngine] Backing up script states");

            List<IScriptInstance> instances = new List<IScriptInstance>();

            lockScriptsForRead(true);
                 foreach (IScriptInstance instance in m_Scripts.Values)
                    instances.Add(instance);
            lockScriptsForRead(false);

            foreach (IScriptInstance i in instances)
            {
                string assembly = String.Empty;

                
                    if (!m_Assemblies.ContainsKey(i.AssetID))
                        continue;
                    assembly = m_Assemblies[i.AssetID];
                

                i.SaveState(assembly);
            }

            instances.Clear();

            if (saveTime > 0)
                m_ThreadPool.QueueWorkItem(new WorkItemCallback(this.DoBackup),
                                           new Object[] { saveTime });

            return 0;
        }

        public void SaveAllState()
        {
            foreach (IScriptInstance inst in m_Scripts.Values)
            {
                if (inst.EventTime() > m_EventLimit)
                {
                    inst.Stop(100);
                    if (!m_KillTimedOutScripts)
                        inst.Start();
                }
            }
        }

        public object DoMaintenance(object p)
        {
            object[] parms = (object[])p;
            int sleepTime = (int)parms[0];

            SaveAllState();

            System.Threading.Thread.Sleep(sleepTime);

            m_ThreadPool.QueueWorkItem(new WorkItemCallback(this.DoMaintenance),
                                       new Object[]{ sleepTime });

            return 0;
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "XEngine"; }
        }

        public void OnRezScript(uint localID, UUID itemID, string script, int startParam, bool postOnRez, string engine, int stateSource)
        {
            if (script.StartsWith("//MRM:"))
                return;

            List<IScriptModule> engines = new List<IScriptModule>(m_Scene.RequestModuleInterfaces<IScriptModule>());

            List<string> names = new List<string>();
            foreach (IScriptModule m in engines)
                names.Add(m.ScriptEngineName);

            int lineEnd = script.IndexOf('\n');

            if (lineEnd > 1)
            {
                string firstline = script.Substring(0, lineEnd).Trim();

                int colon = firstline.IndexOf(':');
                if (firstline.Length > 2 && firstline.Substring(0, 2) == "//" && colon != -1)
                {
                    string engineName = firstline.Substring(2, colon-2);

                    if (names.Contains(engineName))
                    {
                        engine = engineName;
                        script = "//" + script.Substring(script.IndexOf(':')+1);
                    }
                    else
                    {
                        if (engine == ScriptEngineName)
                        {
                            SceneObjectPart part =
                                    m_Scene.GetSceneObjectPart(
                                    localID);
                         
                            TaskInventoryItem item =
                                    part.Inventory.GetInventoryItem(itemID);

                            ScenePresence presence = 
                                    m_Scene.GetScenePresence(
                                    item.OwnerID);

                            if (presence != null)
                            {
                               presence.ControllingClient.SendAgentAlertMessage(
                                        "Selected engine unavailable. "+
                                        "Running script on "+
                                        ScriptEngineName,
                                        false);
                            }
                        }
                    }
                }
            }

            if (engine != ScriptEngineName)
                return;

            // If we've seen this exact script text before, use that reference instead
            if (m_uniqueScripts.ContainsKey(script))
                script = m_uniqueScripts[script];
            else
                m_uniqueScripts[script] = script;

            Object[] parms = new Object[]{localID, itemID, script, startParam, postOnRez, (StateSource)stateSource};

            if (stateSource == (int)StateSource.ScriptedRez)
            {
                lock (m_CompileDict)
                {
                    m_CompileDict[itemID] = 0;
                }

                DoOnRezScript(parms);
            }
            else
            {
                m_CompileQueue.Enqueue(parms);
                lock (m_CompileDict)
                {
                    m_CompileDict[itemID] = 0;
                }

                if (m_CurrentCompile == null)
                {
                    // NOTE: Although we use a lockless queue, the lock here
                    // is required. It ensures that there are never two
                    // compile threads running, which, due to a race
                    // conndition, might otherwise happen
                    //
                    lock (m_CompileQueue)
                    {
                        if (m_CurrentCompile == null)
                            m_CurrentCompile = m_ThreadPool.QueueWorkItem(DoOnRezScriptQueue, null);
                    }
                }
            }
        }

        public Object DoOnRezScriptQueue(Object dummy)
        {
            if (m_InitialStartup)
            {
                m_InitialStartup = false;
                System.Threading.Thread.Sleep(15000);

                if (m_CompileQueue.Count == 0)
                {
                    // No scripts on region, so won't get triggered later
                    // by the queue becoming empty so we trigger it here
                    m_Scene.EventManager.TriggerEmptyScriptCompileQueue(0, String.Empty);
                }
            }

            object[] o;
            while (m_CompileQueue.Dequeue(out o))
                DoOnRezScript(o);

            // NOTE: Despite having a lockless queue, this lock is required
            // to make sure there is never no compile thread while there
            // are still scripts to compile. This could otherwise happen
            // due to a race condition
            //
            lock (m_CompileQueue)
            {
                m_CurrentCompile = null;
            }
            m_Scene.EventManager.TriggerEmptyScriptCompileQueue(m_ScriptFailCount,
                                                                m_ScriptErrorMessage);
            m_ScriptFailCount = 0;

            return null;
        }

        private bool DoOnRezScript(object[] parms)
        {
            Object[] p = parms;
            uint localID = (uint)p[0];
            UUID itemID = (UUID)p[1];
            string script =(string)p[2];
            int startParam = (int)p[3];
            bool postOnRez = (bool)p[4];
            StateSource stateSource = (StateSource)p[5];

            lock (m_CompileDict)
            {
                if (!m_CompileDict.ContainsKey(itemID))
                    return false;
                m_CompileDict.Remove(itemID);
            }

            // Get the asset ID of the script, so we can check if we
            // already have it.

            // We must look for the part outside the m_Scripts lock because GetSceneObjectPart later triggers the
            // m_parts lock on SOG.  At the same time, a scene object that is being deleted will take the m_parts lock
            // and then later on try to take the m_scripts lock in this class when it calls OnRemoveScript()
            SceneObjectPart part = m_Scene.GetSceneObjectPart(localID);
            if (part == null)
            {
                m_log.ErrorFormat("[Script]: SceneObjectPart with localID {0} unavailable. Script NOT started.", localID);
                m_ScriptErrorMessage += "SceneObjectPart unavailable. Script NOT started.\n";
                m_ScriptFailCount++;
                return false;
            }

            TaskInventoryItem item = part.Inventory.GetInventoryItem(itemID);
            if (item == null)
            {
                m_ScriptErrorMessage += "Can't find script inventory item.\n";
                m_ScriptFailCount++;
                return false;
            }

            UUID assetID = item.AssetID;

            //m_log.DebugFormat("[XEngine] Compiling script {0} ({1} on object {2})",
            //        item.Name, itemID.ToString(), part.ParentGroup.RootPart.Name);

            ScenePresence presence = m_Scene.GetScenePresence(item.OwnerID);

            string assembly = "";

            CultureInfo USCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = USCulture;

            Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> linemap;

            lock (m_ScriptErrors)
            {
                try
                {
                    lock (m_AddingAssemblies) 
                    {
                        m_Compiler.PerformScriptCompile(script, assetID.ToString(), item.OwnerID, out assembly, out linemap);
                        if (!m_AddingAssemblies.ContainsKey(assembly)) {
                            m_AddingAssemblies[assembly] = 1;
                        } else {
                            m_AddingAssemblies[assembly]++;
                        }
                    }

                    string[] warnings = m_Compiler.GetWarnings();

                    if (warnings != null && warnings.Length != 0)
                    {
                        foreach (string warning in warnings)
                        {
                            if (!m_ScriptErrors.ContainsKey(itemID))
                                m_ScriptErrors[itemID] = new ArrayList();

                            m_ScriptErrors[itemID].Add(warning);
    //                        try
    //                        {
    //                            // DISPLAY WARNING INWORLD
    //                            string text = "Warning:\n" + warning;
    //                            if (text.Length > 1000)
    //                                text = text.Substring(0, 1000);
    //                            if (!ShowScriptSaveResponse(item.OwnerID,
    //                                    assetID, text, true))
    //                            {
    //                                if (presence != null && (!postOnRez))
    //                                    presence.ControllingClient.SendAgentAlertMessage("Script saved with warnings, check debug window!", false);
    //
    //                                World.SimChat(Utils.StringToBytes(text),
    //                                              ChatTypeEnum.DebugChannel, 2147483647,
    //                                              part.AbsolutePosition,
    //                                              part.Name, part.UUID, false);
    //                            }
    //                        }
    //                        catch (Exception e2) // LEGIT: User Scripting
    //                        {
    //                            m_log.Error("[XEngine]: " +
    //                                    "Error displaying warning in-world: " +
    //                                    e2.ToString());
    //                            m_log.Error("[XEngine]: " +
    //                                    "Warning:\r\n" +
    //                                    warning);
    //                        }
                        }
                    }
                }
                catch (Exception e)
                {
    //                try
    //                {
                        if (!m_ScriptErrors.ContainsKey(itemID))
                            m_ScriptErrors[itemID] = new ArrayList();
                        // DISPLAY ERROR INWORLD
    //                    m_ScriptErrorMessage += "Failed to compile script in object: '" + part.ParentGroup.RootPart.Name + "' Script name: '" + item.Name + "' Error message: " + e.Message.ToString();
    //
                        m_ScriptFailCount++;
                        m_ScriptErrors[itemID].Add(e.Message.ToString());
    //                    string text = "Error compiling script '" + item.Name + "':\n" + e.Message.ToString();
    //                    if (text.Length > 1000)
    //                        text = text.Substring(0, 1000);
    //                    if (!ShowScriptSaveResponse(item.OwnerID,
    //                            assetID, text, false))
    //                    {
    //                        if (presence != null && (!postOnRez))
    //                            presence.ControllingClient.SendAgentAlertMessage("Script saved with errors, check debug window!", false);
    //                        World.SimChat(Utils.StringToBytes(text),
    //                                      ChatTypeEnum.DebugChannel, 2147483647,
    //                                      part.AbsolutePosition,
    //                                      part.Name, part.UUID, false);
    //                    }
    //                }
    //                catch (Exception e2) // LEGIT: User Scripting
    //                {
    //                    m_log.Error("[XEngine]: "+
    //                            "Error displaying error in-world: " +
    //                            e2.ToString());
    //                    m_log.Error("[XEngine]: " +
    //                            "Errormessage: Error compiling script:\r\n" +
    //                            e.Message.ToString());
    //                }

                    return false;
                }
            }

            ScriptInstance instance = null;
            // Create the object record
            lockScriptsForRead(true);
            if ((!m_Scripts.ContainsKey(itemID)) ||
                (m_Scripts[itemID].AssetID != assetID))
            {
                lockScriptsForRead(false);

                UUID appDomain = assetID;

                if (part.ParentGroup.IsAttachment)
                    appDomain = part.ParentGroup.RootPart.UUID;

                if (!m_AppDomains.ContainsKey(appDomain))
                {
                    try
                    {
                        AppDomainSetup appSetup = new AppDomainSetup();
                        appSetup.PrivateBinPath = Path.Combine(
                                m_ScriptEnginesPath,
                                m_Scene.RegionInfo.RegionID.ToString());

                        Evidence baseEvidence = AppDomain.CurrentDomain.Evidence;
                        Evidence evidence = new Evidence(baseEvidence);

                        AppDomain sandbox;
                        if (m_AppDomainLoading)
                            sandbox = AppDomain.CreateDomain(
                                            m_Scene.RegionInfo.RegionID.ToString(),
                                            evidence, appSetup);
                        else
                            sandbox = AppDomain.CurrentDomain;

                        //PolicyLevel sandboxPolicy = PolicyLevel.CreateAppDomainLevel();
                        //AllMembershipCondition sandboxMembershipCondition = new AllMembershipCondition();
                        //PermissionSet sandboxPermissionSet = sandboxPolicy.GetNamedPermissionSet("Internet");
                        //PolicyStatement sandboxPolicyStatement = new PolicyStatement(sandboxPermissionSet);
                        //CodeGroup sandboxCodeGroup = new UnionCodeGroup(sandboxMembershipCondition, sandboxPolicyStatement);
                        //sandboxPolicy.RootCodeGroup = sandboxCodeGroup;
                        //sandbox.SetAppDomainPolicy(sandboxPolicy);

                        m_AppDomains[appDomain] = sandbox;

                        m_AppDomains[appDomain].AssemblyResolve +=
                            new ResolveEventHandler(
                                AssemblyResolver.OnAssemblyResolve);
                        m_DomainScripts[appDomain] = new List<UUID>();
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[XEngine] Exception creating app domain:\n {0}", e.ToString());
                        m_ScriptErrorMessage += "Exception creating app domain:\n";
                        m_ScriptFailCount++;
                        lock (m_AddingAssemblies)
                        {
                            m_AddingAssemblies[assembly]--;
                        }
                        return false;
                    }
                }
                m_DomainScripts[appDomain].Add(itemID);

                instance = new ScriptInstance(this, part,
                                              itemID, assetID, assembly,
                                              m_AppDomains[appDomain],
                                              part.ParentGroup.RootPart.Name,
                                              item.Name, startParam, postOnRez,
                                              stateSource, m_MaxScriptQueue);

                m_log.DebugFormat(
                        "[XEngine] Loaded script {0}.{1}, script UUID {2}, prim UUID {3} @ {4}.{5}",
                        part.ParentGroup.RootPart.Name, item.Name, assetID, part.UUID, 
                        part.ParentGroup.RootPart.AbsolutePosition, part.ParentGroup.Scene.RegionInfo.RegionName);

                if (presence != null)
                {
                    ShowScriptSaveResponse(item.OwnerID,
                            assetID, "Compile successful", true);
                }

                instance.AppDomain = appDomain;
                instance.LineMap = linemap;
                lockScriptsForWrite(true);
                m_Scripts[itemID] = instance;
                lockScriptsForWrite(false);
            }
            else
            {
                lockScriptsForRead(false);
            }
            lock (m_PrimObjects)
            {
                if (!m_PrimObjects.ContainsKey(localID))
                    m_PrimObjects[localID] = new List<UUID>();

                if (!m_PrimObjects[localID].Contains(itemID))
                    m_PrimObjects[localID].Add(itemID);

            }

            if (!m_Assemblies.ContainsKey(assetID))
                m_Assemblies[assetID] = assembly;

            lock (m_AddingAssemblies) 
            {
                m_AddingAssemblies[assembly]--;
            }

            if (instance!=null) 
                instance.Init();
            
            return true;
        }

        public void OnRemoveScript(uint localID, UUID itemID)
        {
            // If it's not yet been compiled, make sure we don't try
            lock (m_CompileDict)
            {
                if (m_CompileDict.ContainsKey(itemID))
                    m_CompileDict.Remove(itemID);
            }

            lockScriptsForRead(true);
            // Do we even have it?
            if (!m_Scripts.ContainsKey(itemID))
            {
                // Do we even have it?
                if (!m_Scripts.ContainsKey(itemID))
                    return;

                lockScriptsForRead(false);
                lockScriptsForWrite(true);
                m_Scripts.Remove(itemID);
                lockScriptsForWrite(false);

                return;
            }
             

            IScriptInstance instance=m_Scripts[itemID];
            lockScriptsForRead(false);
            lockScriptsForWrite(true);
            m_Scripts.Remove(itemID);
            lockScriptsForWrite(false);
            instance.ClearQueue();
            instance.Stop(0);

//                bool objectRemoved = false;

            lock (m_PrimObjects)
            {
                // Remove the script from it's prim
                if (m_PrimObjects.ContainsKey(localID))
                {
                    // Remove inventory item record
                    if (m_PrimObjects[localID].Contains(itemID))
                        m_PrimObjects[localID].Remove(itemID);

                    // If there are no more scripts, remove prim
                    if (m_PrimObjects[localID].Count == 0)
                    {
                        m_PrimObjects.Remove(localID);
//                            objectRemoved = true;
                    }
                }
            }

            instance.RemoveState();
            instance.DestroyScriptInstance();

            m_DomainScripts[instance.AppDomain].Remove(instance.ItemID);
            if (m_DomainScripts[instance.AppDomain].Count == 0)
            {
                m_DomainScripts.Remove(instance.AppDomain);
                UnloadAppDomain(instance.AppDomain);
            }

            instance = null;

            ObjectRemoved handlerObjectRemoved = OnObjectRemoved;
            if (handlerObjectRemoved != null)
            {
                SceneObjectPart part = m_Scene.GetSceneObjectPart(localID);                    
                handlerObjectRemoved(part.UUID);
            }

            CleanAssemblies();
            
            ScriptRemoved handlerScriptRemoved = OnScriptRemoved;
            if (handlerScriptRemoved != null)
                handlerScriptRemoved(itemID);
        }

        public void OnScriptReset(uint localID, UUID itemID)
        {
            ResetScript(itemID);
        }

        public void OnStartScript(uint localID, UUID itemID)
        {
            StartScript(itemID);
        }

        public void OnStopScript(uint localID, UUID itemID)
        {
            StopScript(itemID);
        }

        private void CleanAssemblies()
        {
            List<UUID> assetIDList = new List<UUID>(m_Assemblies.Keys);

            foreach (IScriptInstance i in m_Scripts.Values)
            {
                if (assetIDList.Contains(i.AssetID))
                    assetIDList.Remove(i.AssetID);
            }

            lock (m_AddingAssemblies)
            {
                foreach (UUID assetID in assetIDList)
                {
                    // Do not remove assembly files if another instance of the script
                    // is currently initialising
                    if (!m_AddingAssemblies.ContainsKey(m_Assemblies[assetID])
                        || m_AddingAssemblies[m_Assemblies[assetID]] == 0) 
                    {
//                        m_log.DebugFormat("[XEngine] Removing unreferenced assembly {0}", m_Assemblies[assetID]);
                        try
                        {
                            if (File.Exists(m_Assemblies[assetID]))
                                File.Delete(m_Assemblies[assetID]);
                            
                            if (File.Exists(m_Assemblies[assetID]+".text"))
                                File.Delete(m_Assemblies[assetID]+".text");
                            
                            if (File.Exists(m_Assemblies[assetID]+".mdb"))
                                File.Delete(m_Assemblies[assetID]+".mdb");
                            
                            if (File.Exists(m_Assemblies[assetID]+".map"))
                                File.Delete(m_Assemblies[assetID]+".map");
                        }
                        catch (Exception)
                        {
                        }
                        m_Assemblies.Remove(assetID);
                    }
                }
            }
        }

        private void UnloadAppDomain(UUID id)
        {
            if (m_AppDomains.ContainsKey(id))
            {
                AppDomain domain = m_AppDomains[id];
                m_AppDomains.Remove(id);

                if (domain != AppDomain.CurrentDomain)
                    AppDomain.Unload(domain);
                domain = null;
                // m_log.DebugFormat("[XEngine] Unloaded app domain {0}", id.ToString());
            }
        }

        //
        // Start processing
        //
        private void SetupEngine(int minThreads, int maxThreads,
                                 int idleTimeout, ThreadPriority threadPriority,
                                 int maxScriptQueue, int stackSize)
        {
            m_MaxScriptQueue = maxScriptQueue;

            STPStartInfo startInfo = new STPStartInfo();
            startInfo.IdleTimeout = idleTimeout*1000; // convert to seconds as stated in .ini
            startInfo.MaxWorkerThreads = maxThreads;
            startInfo.MinWorkerThreads = minThreads;
            startInfo.ThreadPriority = threadPriority;;
            startInfo.StackSize = stackSize;
            startInfo.StartSuspended = true;

            m_ThreadPool = new SmartThreadPool(startInfo);
        }

        //
        // Used by script instances to queue event handler jobs
        //
        public IScriptWorkItem QueueEventHandler(object parms)
        {
            return new XWorkItem(m_ThreadPool.QueueWorkItem(
                                     new WorkItemCallback(this.ProcessEventHandler),
                                     parms));
        }
        
        /// <summary>
        /// Process a previously posted script event.
        /// </summary>
        /// <param name="parms"></param>
        /// <returns></returns>
        private object ProcessEventHandler(object parms)
        {
            CultureInfo USCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = USCulture;

            IScriptInstance instance = (ScriptInstance) parms;
            
            //m_log.DebugFormat("[XEngine]: Processing event for {0}", instance);

            return instance.EventProcessor();
        }

        /// <summary>
        /// Post event to an entire prim
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool PostObjectEvent(uint localID, EventParams p)
        {
            bool result = false;
            List<UUID> uuids = null;

            lock (m_PrimObjects)
            {
                if (!m_PrimObjects.ContainsKey(localID))
                    return false;

                uuids = m_PrimObjects[localID];
            

            foreach (UUID itemID in uuids)
            {
                IScriptInstance instance = null;
                try
                {
                    if (m_Scripts.ContainsKey(itemID))
                        instance = m_Scripts[itemID];
                }
                catch { /* ignore race conditions */ }

                if (instance != null)
                {
                    instance.PostEvent(p);
                    result = true;
                }
            }
            }
            
            return result;
        }

        /// <summary>
        /// Post an event to a single script
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool PostScriptEvent(UUID itemID, EventParams p)
        {
            if (m_Scripts.ContainsKey(itemID))
            {
                IScriptInstance instance = m_Scripts[itemID];
                if (instance != null)
                    instance.PostEvent(p);
                return true;
            }
            return false;
        }

        public bool PostScriptEvent(UUID itemID, string name, Object[] p)
        {
            Object[] lsl_p = new Object[p.Length];
            for (int i = 0; i < p.Length ; i++)
            {
                if (p[i] is int)
                    lsl_p[i] = new LSL_Types.LSLInteger((int)p[i]);
                else if (p[i] is string)
                    lsl_p[i] = new LSL_Types.LSLString((string)p[i]);
                else if (p[i] is Vector3)
                    lsl_p[i] = new LSL_Types.Vector3(((Vector3)p[i]).X, ((Vector3)p[i]).Y, ((Vector3)p[i]).Z);
                else if (p[i] is Quaternion)
                    lsl_p[i] = new LSL_Types.Quaternion(((Quaternion)p[i]).X, ((Quaternion)p[i]).Y, ((Quaternion)p[i]).Z, ((Quaternion)p[i]).W);
                else if (p[i] is float)
                    lsl_p[i] = new LSL_Types.LSLFloat((float)p[i]);
                else
                    lsl_p[i] = p[i];
            }

            return PostScriptEvent(itemID, new EventParams(name, lsl_p, new DetectParams[0]));
        }

        public bool PostObjectEvent(UUID itemID, string name, Object[] p)
        {
            SceneObjectPart part = m_Scene.GetSceneObjectPart(itemID);
            if (part == null)
                return false;

            Object[] lsl_p = new Object[p.Length];
            for (int i = 0; i < p.Length ; i++)
            {
                if (p[i] is int)
                    lsl_p[i] = new LSL_Types.LSLInteger((int)p[i]);
                else if (p[i] is string)
                    lsl_p[i] = new LSL_Types.LSLString((string)p[i]);
                else if (p[i] is Vector3)
                    lsl_p[i] = new LSL_Types.Vector3(((Vector3)p[i]).X, ((Vector3)p[i]).Y, ((Vector3)p[i]).Z);
                else if (p[i] is Quaternion)
                    lsl_p[i] = new LSL_Types.Quaternion(((Quaternion)p[i]).X, ((Quaternion)p[i]).Y, ((Quaternion)p[i]).Z, ((Quaternion)p[i]).W);
                else if (p[i] is float)
                    lsl_p[i] = new LSL_Types.LSLFloat((float)p[i]);
                else
                    lsl_p[i] = p[i];
            }

            return PostObjectEvent(part.LocalId, new EventParams(name, lsl_p, new DetectParams[0]));
        }

        public Assembly OnAssemblyResolve(object sender,
                                          ResolveEventArgs args)
        {
            if (!(sender is System.AppDomain))
                return null;

            string[] pathList = new string[] {"bin", m_ScriptEnginesPath,
                                              Path.Combine(m_ScriptEnginesPath,
                                                           m_Scene.RegionInfo.RegionID.ToString())};

            string assemblyName = args.Name;
            if (assemblyName.IndexOf(",") != -1)
                assemblyName = args.Name.Substring(0, args.Name.IndexOf(","));

            foreach (string s in pathList)
            {
                string path = Path.Combine(Directory.GetCurrentDirectory(),
                                           Path.Combine(s, assemblyName))+".dll";

                if (File.Exists(path))
                    return Assembly.LoadFrom(path);
            }

            return null;
        }

        private IScriptInstance GetInstance(UUID itemID)
        {
            IScriptInstance instance;
            lockScriptsForRead(true);
            if (!m_Scripts.ContainsKey(itemID))
            {
                lockScriptsForRead(false);
                return null;
            }
            instance = m_Scripts[itemID];
            lockScriptsForRead(false);
            return instance;
        }

        public void SetScriptState(UUID itemID, bool running)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
            {
                if (running)
                    instance.Start();
                else
                    instance.Stop(100);
            }
        }

        public bool GetScriptState(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                return instance.Running;
            return false;
        }

        [DebuggerNonUserCode]
        public void ApiResetScript(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.ApiResetScript();
        }

        public void ResetScript(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.ResetScript();
        }

        public void StartScript(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.Start();
        }

        public void StopScript(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.Stop(0);
        }

        public DetectParams GetDetectParams(UUID itemID, int idx)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                return instance.GetDetectParams(idx);
            return null;
        }

        public void SetMinEventDelay(UUID itemID, double delay)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.MinEventDelay = delay;
        }

        public UUID GetDetectID(UUID itemID, int idx)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                return instance.GetDetectID(idx);
            return UUID.Zero;
        }

        [DebuggerNonUserCode]
        public void SetState(UUID itemID, string newState)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return;
            instance.SetState(newState);
        }

        public int GetStartParameter(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return 0;
            return instance.StartParam;
        }

        public void OnShutdown()
        {
            m_SimulatorShuttingDown = true;

            List<IScriptInstance> instances = new List<IScriptInstance>();

            lockScriptsForRead(true);
            foreach (IScriptInstance instance in m_Scripts.Values)
                    instances.Add(instance);
            lockScriptsForRead(false);

            foreach (IScriptInstance i in instances)
            {
                // Stop the script, even forcibly if needed. Then flag
                // it as shutting down and restore the previous run state
                // for serialization, so the scripts don't come back
                // dead after region restart
                //
                bool prevRunning = i.Running;
                i.Stop(50);
                i.ShuttingDown = true;
                i.Running = prevRunning;
            }

            DoBackup(new Object[] {0});
        }

        public IScriptApi GetApi(UUID itemID, string name)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return null;
            return instance.GetApi(name);
        }

        public void OnGetScriptRunning(IClientAPI controllingClient, UUID objectID, UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return;
            IEventQueue eq = World.RequestModuleInterface<IEventQueue>();
            if (eq == null)
            {
                controllingClient.SendScriptRunningReply(objectID, itemID,
                        GetScriptState(itemID));
            }
            else
            {
                eq.Enqueue(eq.ScriptRunningEvent(objectID, itemID, GetScriptState(itemID), true),
                           controllingClient.AgentId);
            }
        }

        public string GetXMLState(UUID itemID)
        {
//            m_log.DebugFormat("[XEngine]: Getting XML state for {0}", itemID);

            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
            {
//                m_log.DebugFormat("[XEngine]: Found no script for {0}, returning empty string", itemID);
                return "";
            }

            string xml = instance.GetXMLState();

            XmlDocument sdoc = new XmlDocument();
            bool loadedState = true;
            try
            {
                sdoc.LoadXml(xml);
            }
            catch (System.Xml.XmlException)
            {
                loadedState = false;
            }

            XmlNodeList rootL = null;
            XmlNode rootNode = null;
            if (loadedState)
            {
                rootL = sdoc.GetElementsByTagName("ScriptState");
                rootNode = rootL[0];
            }

            // Create <State UUID="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx">
            XmlDocument doc = new XmlDocument();
            XmlElement stateData = doc.CreateElement("", "State", "");
            XmlAttribute stateID = doc.CreateAttribute("", "UUID", "");
            stateID.Value = itemID.ToString();
            stateData.Attributes.Append(stateID);
            XmlAttribute assetID = doc.CreateAttribute("", "Asset", "");
            assetID.Value = instance.AssetID.ToString();
            stateData.Attributes.Append(assetID);
            XmlAttribute engineName = doc.CreateAttribute("", "Engine", "");
            engineName.Value = ScriptEngineName;
            stateData.Attributes.Append(engineName);
            doc.AppendChild(stateData);

            XmlNode xmlstate = null;

            // Add <ScriptState>...</ScriptState>
            if (loadedState)
            {
                xmlstate = doc.ImportNode(rootNode, true);
            }
            else
            {
                xmlstate = doc.CreateElement("", "ScriptState", "");
            }

            stateData.AppendChild(xmlstate);

            string assemName = instance.GetAssemblyName();

            string fn = Path.GetFileName(assemName);

            string assem = String.Empty;

            if (File.Exists(assemName + ".text"))
            {
                FileInfo tfi = new FileInfo(assemName + ".text");

                if (tfi != null)
                {
                    Byte[] tdata = new Byte[tfi.Length];

                    try
                    {
                        using (FileStream tfs = File.Open(assemName + ".text",
                                FileMode.Open, FileAccess.Read))
                        {
                            tfs.Read(tdata, 0, tdata.Length);
                            tfs.Close();
                        }

                        assem = new System.Text.ASCIIEncoding().GetString(tdata);
                    }
                    catch (Exception e)
                    {
                         m_log.DebugFormat("[XEngine]: Unable to open script textfile {0}, reason: {1}", assemName+".text", e.Message);
                    }
                }
            }
            else
            {
                FileInfo fi = new FileInfo(assemName);

                if (fi != null)
                {
                    Byte[] data = new Byte[fi.Length];

                    try
                    {
                        using (FileStream fs = File.Open(assemName, FileMode.Open, FileAccess.Read))
                        {
                            fs.Read(data, 0, data.Length);
                            fs.Close();
                        }

                        assem = System.Convert.ToBase64String(data);
                    }
                    catch (Exception e)
                    {
                        m_log.DebugFormat("[XEngine]: Unable to open script assembly {0}, reason: {1}", assemName, e.Message);
                    }

                }
            }

            string map = String.Empty;

            if (File.Exists(fn + ".map"))
            {
                using (FileStream mfs = File.Open(fn + ".map", FileMode.Open, FileAccess.Read))
                {
                    using (StreamReader msr = new StreamReader(mfs))
                    {
                        map = msr.ReadToEnd();
                        msr.Close();
                    }
                    mfs.Close();
                }
            }

            XmlElement assemblyData = doc.CreateElement("", "Assembly", "");
            XmlAttribute assemblyName = doc.CreateAttribute("", "Filename", "");

            assemblyName.Value = fn;
            assemblyData.Attributes.Append(assemblyName);

            assemblyData.InnerText = assem;

            stateData.AppendChild(assemblyData);

            XmlElement mapData = doc.CreateElement("", "LineMap", "");
            XmlAttribute mapName = doc.CreateAttribute("", "Filename", "");

            mapName.Value = fn + ".map";
            mapData.Attributes.Append(mapName);

            mapData.InnerText = map;

            stateData.AppendChild(mapData);

//            m_log.DebugFormat("[XEngine]: Got XML state for {0}", itemID);

            return doc.InnerXml;
        }

        private bool ShowScriptSaveResponse(UUID ownerID, UUID assetID, string text, bool compiled)
        {
            return false;
        }

        public bool SetXMLState(UUID itemID, string xml)
        {
            if (xml == String.Empty)
                return false;

            XmlDocument doc = new XmlDocument();

            try
            {
                doc.LoadXml(xml);
            }
            catch (Exception)
            {
                m_log.Error("[XEngine]: Exception decoding XML data from region transfer");
                return false;
            }

            XmlNodeList rootL = doc.GetElementsByTagName("State");
            if (rootL.Count < 1)
                return false;

            XmlElement rootE = (XmlElement)rootL[0];

            if (rootE.GetAttribute("Engine") != ScriptEngineName)
                return false;

//          On rez from inventory, that ID will have changed. It was only
//          advisory anyway. So we don't check it anymore.
//
//            if (rootE.GetAttribute("UUID") != itemID.ToString())
//                return;

            XmlNodeList stateL = rootE.GetElementsByTagName("ScriptState");

            if (stateL.Count != 1)
                return false;

            XmlElement stateE = (XmlElement)stateL[0];

            if (World.m_trustBinaries)
            {
                XmlNodeList assemL = rootE.GetElementsByTagName("Assembly");

                if (assemL.Count != 1)
                    return false;

                XmlElement assemE = (XmlElement)assemL[0];

                string fn = assemE.GetAttribute("Filename");
                string base64 = assemE.InnerText;

                string path = Path.Combine(m_ScriptEnginesPath, World.RegionInfo.RegionID.ToString());
                path = Path.Combine(path, fn);

                if (!File.Exists(path))
                {
                    Byte[] filedata = Convert.FromBase64String(base64);

                    try
                    {
                        using (FileStream fs = File.Create(path))
                        {
                            fs.Write(filedata, 0, filedata.Length);
                            fs.Close();
                        }
                    }
                    catch (IOException ex)
                    {
                        // if there already exists a file at that location, it may be locked.
                        m_log.ErrorFormat("[XEngine]: File {0} already exists! {1}", path, ex.Message);
                    }
                    try
                    {
                        using (FileStream fs = File.Create(path + ".text"))
                        {
                            using (StreamWriter sw = new StreamWriter(fs))
                            {
                                sw.Write(base64);
                                sw.Close();
                            }
                            fs.Close();
                        }
                    }
                    catch (IOException ex)
                    {
                        // if there already exists a file at that location, it may be locked.
                        m_log.ErrorFormat("[XEngine]: File {0} already exists! {1}", path, ex.Message);
                    }
                }
            }

            string statepath = Path.Combine(m_ScriptEnginesPath, World.RegionInfo.RegionID.ToString());
            statepath = Path.Combine(statepath, itemID.ToString() + ".state");

            try
            {
                using (FileStream sfs = File.Create(statepath))
                {
                    using (StreamWriter ssw = new StreamWriter(sfs))
                    {
                        ssw.Write(stateE.OuterXml);
                        ssw.Close();
                    }
                    sfs.Close();
                }
            }
            catch (IOException ex)
            {
                // if there already exists a file at that location, it may be locked.
                m_log.ErrorFormat("[XEngine]: File {0} already exists! {1}", statepath, ex.Message);
            }

            XmlNodeList mapL = rootE.GetElementsByTagName("LineMap");
            if (mapL.Count > 0)
            {
                XmlElement mapE = (XmlElement)mapL[0];

                string mappath = Path.Combine(m_ScriptEnginesPath, World.RegionInfo.RegionID.ToString());
                mappath = Path.Combine(mappath, mapE.GetAttribute("Filename"));

                try
                {
                    using (FileStream mfs = File.Create(mappath))
                    {
                        using (StreamWriter msw = new StreamWriter(mfs))
                        {
                            msw.Write(mapE.InnerText);
                            msw.Close();
                        }
                        mfs.Close();
                    }
                }
                catch (IOException ex)
                {
                    // if there already exists a file at that location, it may be locked.
                    m_log.ErrorFormat("[XEngine]: File {0} already exists! {1}", statepath, ex.Message);
                }
            }

            return true;
        }

        public ArrayList GetScriptErrors(UUID itemID)
        {
            System.Threading.Thread.Sleep(1000);

            lock (m_ScriptErrors)
            {
                if (m_ScriptErrors.ContainsKey(itemID))
                {
                    ArrayList ret = m_ScriptErrors[itemID];
                    m_ScriptErrors.Remove(itemID);
                    return ret;
                }
                return new ArrayList();
            }
        }

        public void SuspendScript(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.Suspend();
        }

        public void ResumeScript(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.Resume();
        }

        public bool HasScript(UUID itemID, out bool running)
        {
            running = true;

            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return false;

            running = instance.Running;
            return true;
        }
    }
}
