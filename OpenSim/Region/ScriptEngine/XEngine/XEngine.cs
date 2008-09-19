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

using System;
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Security.Policy;
using System.Reflection;
using System.Globalization;
using System.Xml;
using OpenMetaverse;
using log4net;
using Nini.Config;
using Amib.Threading;
using OpenSim.Framework;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Shared.CodeTools;
using OpenSim.Region.ScriptEngine.Shared.Instance;
using OpenSim.Region.ScriptEngine.Interfaces;

namespace OpenSim.Region.ScriptEngine.XEngine
{
    public class XEngine : IScriptModule, IScriptEngine
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private SmartThreadPool m_ThreadPool;
        private int m_MaxScriptQueue;
        private Scene m_Scene;
        private IConfig m_ScriptConfig;
        private Compiler m_Compiler;

// disable warning: need to keep a reference to XEngine.EventManager
// alive to avoid it being garbage collected
#pragma warning disable 414
        private EventManager m_EventManager;
#pragma warning restore 414
        private int m_EventLimit;
        private bool m_KillTimedOutScripts;
        private AsyncCommandManager m_AsyncCommands;
        bool m_firstStart = true;

        private static List<XEngine> m_ScriptEngines =
                new List<XEngine>();

        // Maps the local id to the script inventory items in it

        private Dictionary<uint, List<UUID> > m_PrimObjects =
                new Dictionary<uint, List<UUID> >();

        // Maps the UUID above to the script instance

        private Dictionary<UUID, IScriptInstance> m_Scripts =
                new Dictionary<UUID, IScriptInstance>();

        // Maps the asset ID to the assembly

        private Dictionary<UUID, string> m_Assemblies =
                new Dictionary<UUID, string>();

        // This will list AppDomains by script asset

        private Dictionary<UUID, AppDomain> m_AppDomains =
                new Dictionary<UUID, AppDomain>();

        // List the scripts running in each appdomain

        private Dictionary<UUID, List<UUID> > m_DomainScripts =
                new Dictionary<UUID, List<UUID> >();

        private Queue m_CompileQueue = new Queue(100);
        IWorkItemResult m_CurrentCompile = null;

        public string ScriptEngineName
        {
            get { return "XEngine"; }
        }

        public Scene World
        {
            get { return m_Scene; }
        }

        public ILog Log
        {
            get { return m_log; }
        }

        public static List<XEngine> ScriptEngines
        {
            get { return m_ScriptEngines; }
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

        public Object AsyncCommands
        {
            get { return (Object)m_AsyncCommands; }
        }

        //
        // IRegionModule functions
        //
        public void Initialise(Scene scene, IConfigSource configSource)
        {
            AppDomain.CurrentDomain.AssemblyResolve +=
                OnAssemblyResolve;

            m_log.InfoFormat("[XEngine] Initializing scripts in region {0}",
                             scene.RegionInfo.RegionName);
            m_Scene = scene;

            m_ScriptConfig = configSource.Configs["XEngine"];

            if (m_ScriptConfig == null)
            {
                m_log.ErrorFormat("[XEngine] No script configuration found. Scripts disabled");
                return;
            }

            int minThreads = m_ScriptConfig.GetInt("MinThreads", 2);
            int maxThreads = m_ScriptConfig.GetInt("MaxThreads", 100);
            int idleTimeout = m_ScriptConfig.GetInt("IdleTimeout", 60);
            string priority = m_ScriptConfig.GetString("Priority", "BelowNormal");
            int maxScriptQueue = m_ScriptConfig.GetInt("MaxScriptEventQueue",300);
            int stackSize = m_ScriptConfig.GetInt("ThreadStackSize", 262144);
            int sleepTime = m_ScriptConfig.GetInt("MaintenanceInterval", 10) * 1000;
            m_EventLimit = m_ScriptConfig.GetInt("EventLimit", 30);
            m_KillTimedOutScripts = m_ScriptConfig.GetBoolean("KillTimedOutScripts", false);
            int saveTime = m_ScriptConfig.GetInt("SaveInterval", 120) * 1000;

            ThreadPriority prio = ThreadPriority.BelowNormal;
            switch (priority)
            {
                case "Lowest":
                    prio = ThreadPriority.Lowest;
                    break;
                case "BelowNormal":
                    prio = ThreadPriority.BelowNormal;
                    break;
                case "Normal":
                    prio = ThreadPriority.Normal;
                    break;
                case "AboveNormal":
                    prio = ThreadPriority.AboveNormal;
                    break;
                case "Highest":
                    prio = ThreadPriority.Highest;
                    break;
                default:
                    m_log.ErrorFormat("[XEngine] Invalid thread priority: '{0}'. Assuming BelowNormal", priority);
                    break;
            }

            lock (m_ScriptEngines)
            {
                m_ScriptEngines.Add(this);
            }

            m_EventManager = new EventManager(this);

            StartEngine(minThreads, maxThreads, idleTimeout, prio,
                        maxScriptQueue, stackSize);

            m_Compiler = new Compiler(this);

            m_Scene.EventManager.OnRezScript += OnRezScript;
            m_Scene.EventManager.OnRemoveScript += OnRemoveScript;
            m_Scene.EventManager.OnScriptReset += OnScriptReset;
            m_Scene.EventManager.OnStartScript += OnStartScript;
            m_Scene.EventManager.OnStopScript += OnStopScript;
            m_Scene.EventManager.OnShutdown += OnShutdown;

            m_AsyncCommands = new AsyncCommandManager(this);

            if (sleepTime > 0)
            {
                m_ThreadPool.QueueWorkItem(new WorkItemCallback(this.DoMaintenance),
                                           new Object[]{ sleepTime });
            }

            if (saveTime > 0)
            {
                m_ThreadPool.QueueWorkItem(new WorkItemCallback(this.DoBackup),
                                           new Object[] { saveTime });
            }

            scene.RegisterModuleInterface<IScriptModule>(this);
        }

        public void PostInitialise()
        {
            m_ThreadPool.Start();
        }

        public void Close()
        {
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

            lock (m_Scripts)
            {
                foreach (IScriptInstance instance in m_Scripts.Values)
                    instances.Add(instance);
            }

            foreach (IScriptInstance i in instances)
            {
                string assembly = String.Empty;

                lock (m_Scripts)
                {
                    if (!m_Assemblies.ContainsKey(i.AssetID))
                        continue;
                    assembly = m_Assemblies[i.AssetID];
                }

                i.SaveState(assembly);
            }

            instances.Clear();

            if (saveTime > 0)
                m_ThreadPool.QueueWorkItem(new WorkItemCallback(this.DoBackup),
                                           new Object[] { saveTime });

            return 0;
        }

        public object DoMaintenance(object p)
        {
            object[] parms = (object[])p;
            int sleepTime = (int)parms[0];

            foreach (IScriptInstance inst in m_Scripts.Values)
            {
                if (inst.EventTime() > m_EventLimit)
                {
                    inst.Stop(100);
                    if (!m_KillTimedOutScripts)
                        inst.Start();
                }
            }

            System.Threading.Thread.Sleep(sleepTime);

            m_ThreadPool.QueueWorkItem(new WorkItemCallback(this.DoMaintenance),
                                       new Object[]{ sleepTime });

            return 0;
        }

        public string Name
        {
            get { return "XEngine"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        public void OnRezScript(uint localID, UUID itemID, string script, int startParam, bool postOnRez)
        {
            Object[] parms = new Object[]{localID, itemID, script, startParam, postOnRez};

            lock (m_CompileQueue)
            {
                m_CompileQueue.Enqueue(parms);

                if (m_CurrentCompile == null)
                {
                    if (m_firstStart)
                    {
                        m_firstStart = false;
                        m_CurrentCompile = m_ThreadPool.QueueWorkItem(
                            new WorkItemCallback(this.DoScriptWait),
                            new Object[0]);
                        return;
                    }

                    m_CurrentCompile = m_ThreadPool.QueueWorkItem(
                            new WorkItemCallback(this.DoOnRezScriptQueue),
                            new Object[0]);
                }
            }
        }

        public Object DoScriptWait(Object dummy)
        {
            Thread.Sleep(30000);

            lock (m_CompileQueue)
            {
                if (m_CompileQueue.Count > 0)
                {
                    m_CurrentCompile = m_ThreadPool.QueueWorkItem(
                            new WorkItemCallback(this.DoOnRezScriptQueue),
                            new Object[0]);
                }
                else
                {
                    m_CurrentCompile = null;
                }
            }
            return null;
        }

        public Object DoOnRezScriptQueue(Object dummy)
        {
            Object o;
            lock (m_CompileQueue)
            {
                o = m_CompileQueue.Dequeue();
                if (o == null)
                {
                    m_CurrentCompile = null;
                    return null;
                }
            }

            DoOnRezScript(o);

            lock (m_CompileQueue)
            {
                if (m_CompileQueue.Count > 0)
                {
                    m_CurrentCompile = m_ThreadPool.QueueWorkItem(
                            new WorkItemCallback(this.DoOnRezScriptQueue),
                            new Object[0]);
                }
                else
                {
                    m_CurrentCompile = null;
                }
            }
            return null;
        }

        private bool DoOnRezScript(object parm)
        {
            Object[] p = (Object[])parm;
            uint localID = (uint)p[0];
            UUID itemID = (UUID)p[1];
            string script =(string)p[2];
            int startParam = (int)p[3];
            bool postOnRez = (bool)p[4];

            // Get the asset ID of the script, so we can check if we
            // already have it.

            // We must look for the part outside the m_Scripts lock because GetSceneObjectPart later triggers the
            // m_parts lock on SOG.  At the same time, a scene object that is being deleted will take the m_parts lock
            // and then later on try to take the m_scripts lock in this class when it calls OnRemoveScript()            
            SceneObjectPart part = m_Scene.GetSceneObjectPart(localID);
            if (part == null)
            {
                Log.Error("[Script] SceneObjectPart unavailable. Script NOT started.");
                return false;
            }     

            TaskInventoryItem item = part.GetInventoryItem(itemID);
            if (item == null)
                return false;

            UUID assetID = item.AssetID;

//            m_log.DebugFormat("[XEngine] Compiling script {0} ({1})",
//                    item.Name, itemID.ToString());

            string assembly = "";
            try
            {
                assembly = m_Compiler.PerformScriptCompile(script,
                                                           assetID.ToString());
            }
            catch (Exception e)
            {
                try
                {
                    // DISPLAY ERROR INWORLD
                    string text = "Error compiling script:\r\n" + e.Message.ToString();
                    if (text.Length > 1000)
                        text = text.Substring(0, 1000);
                    World.SimChat(Utils.StringToBytes(text),
                                  ChatTypeEnum.DebugChannel, 2147483647,
                                  part.AbsolutePosition,
                                  part.Name, part.UUID, false);
                }
                catch (Exception e2) // LEGIT: User Scripting
                {
                    m_log.Error("[XEngine]: "+
                            "Error displaying error in-world: " +
                            e2.ToString());
                    m_log.Error("[XEngine]: " +
                            "Errormessage: Error compiling script:\r\n" +
                            e.Message.ToString());
                }

                return false;
            }

            lock (m_Scripts)
            {
                // Create the object record

                if ((!m_Scripts.ContainsKey(itemID)) ||
                    (m_Scripts[itemID].AssetID != assetID))
                {
                    UUID appDomain = assetID;

                    if (part.ParentGroup.RootPart.IsAttachment)
                        appDomain = part.ParentGroup.RootPart.UUID;

                    if (!m_AppDomains.ContainsKey(appDomain))
                    {
                        try
                        {
                            AppDomainSetup appSetup = new AppDomainSetup();
//                            appSetup.ApplicationBase = Path.Combine(
//                                    "ScriptEngines",
//                                    m_Scene.RegionInfo.RegionID.ToString());

                            Evidence baseEvidence = AppDomain.CurrentDomain.Evidence;
                            Evidence evidence = new Evidence(baseEvidence);

                            m_AppDomains[appDomain] =
                                AppDomain.CreateDomain(
                                    m_Scene.RegionInfo.RegionID.ToString(),
                                    evidence, appSetup);

                            m_AppDomains[appDomain].AssemblyResolve +=
                                new ResolveEventHandler(
                                    AssemblyResolver.OnAssemblyResolve);
                            m_DomainScripts[appDomain] = new List<UUID>();
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat("[XEngine] Exception creating app domain:\n {0}", e.ToString());
                            return false;
                        }
                    }
                    m_DomainScripts[appDomain].Add(itemID);

                    ScriptInstance instance =
                        new ScriptInstance(this, part,
                                           itemID, assetID, assembly,
                                           m_AppDomains[appDomain],
                                           part.ParentGroup.RootPart.Name,
                                           item.Name, startParam, postOnRez,
                                           StateSource.NewRez, m_MaxScriptQueue);

                    m_log.DebugFormat("[XEngine] Loaded script {0}.{1}",
                            part.ParentGroup.RootPart.Name, item.Name);

                    instance.AppDomain = appDomain;

                    m_Scripts[itemID] = instance;
                }

                if (!m_PrimObjects.ContainsKey(localID))
                    m_PrimObjects[localID] = new List<UUID>();

                if (!m_PrimObjects[localID].Contains(itemID))
                    m_PrimObjects[localID].Add(itemID);

                if (!m_Assemblies.ContainsKey(assetID))
                    m_Assemblies[assetID] = assembly;
            }
            return true;
        }

        public void OnRemoveScript(uint localID, UUID itemID)
        {
            lock (m_Scripts)
            {
                // Do we even have it?
                if (!m_Scripts.ContainsKey(itemID))
                    return;

                m_AsyncCommands.RemoveScript(localID, itemID);

                IScriptInstance instance=m_Scripts[itemID];
                m_Scripts.Remove(itemID);

                instance.ClearQueue();
                instance.Stop(0);

                SceneObjectPart part =
                    m_Scene.GetSceneObjectPart(localID);

                if (part != null)
                    part.RemoveScriptEvents(itemID);

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
                    }
                }

                m_DomainScripts[instance.AppDomain].Remove(instance.ItemID);
                if (m_DomainScripts[instance.AppDomain].Count == 0)
                {
                    m_DomainScripts.Remove(instance.AppDomain);
                    UnloadAppDomain(instance.AppDomain);
                }

                instance.RemoveState();
                instance.DestroyScriptInstance();

                instance = null;

                CleanAssemblies();
            }
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

            foreach (UUID assetID in assetIDList)
            {
//                m_log.DebugFormat("[XEngine] Removing unreferenced assembly {0}", m_Assemblies[assetID]);
                try
                {
                    if (File.Exists(m_Assemblies[assetID]))
                        File.Delete(m_Assemblies[assetID]);

                    if (File.Exists(m_Assemblies[assetID]+".state"))
                        File.Delete(m_Assemblies[assetID]+".state");

                    if (File.Exists(m_Assemblies[assetID]+".mdb"))
                        File.Delete(m_Assemblies[assetID]+".mdb");
                }
                catch (Exception)
                {
                }
                m_Assemblies.Remove(assetID);
            }
        }

        private void UnloadAppDomain(UUID id)
        {
            if (m_AppDomains.ContainsKey(id))
            {
                AppDomain domain = m_AppDomains[id];
                m_AppDomains.Remove(id);

                AppDomain.Unload(domain);
                domain = null;
//                m_log.DebugFormat("[XEngine] Unloaded app domain {0}", id.ToString());
            }
        }

        //
        // Start processing
        //
        private void StartEngine(int minThreads, int maxThreads,
                                 int idleTimeout, ThreadPriority threadPriority,
                                 int maxScriptQueue, int stackSize)
        {
            m_MaxScriptQueue = maxScriptQueue;

            STPStartInfo startInfo = new STPStartInfo();
            startInfo.IdleTimeout = idleTimeout;
            startInfo.MaxWorkerThreads = maxThreads;
            startInfo.MinWorkerThreads = minThreads;
            startInfo.ThreadPriority = threadPriority;
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
            
            //m_log.DebugFormat("[XENGINE]: Processing event for {0}", instance);

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

            if (!m_PrimObjects.ContainsKey(localID))
                return false;

            foreach (UUID itemID in m_PrimObjects[localID])
            {
                if (m_Scripts.ContainsKey(itemID))
                {
                    IScriptInstance instance = m_Scripts[itemID];
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

        public Assembly OnAssemblyResolve(object sender,
                                          ResolveEventArgs args)
        {
            if (!(sender is System.AppDomain))
                return null;

            string[] pathList = new string[] {"bin", "ScriptEngines",
                                              Path.Combine("ScriptEngines",
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
            lock (m_Scripts)
            {
                if (!m_Scripts.ContainsKey(itemID))
                    return null;
                instance = m_Scripts[itemID];
            }
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

        public UUID GetDetectID(UUID itemID, int idx)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                return instance.GetDetectID(idx);
            return UUID.Zero;
        }

        public void SetState(UUID itemID, string newState)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return;
            instance.SetState(newState);
        }
        public string GetState(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return "default";
            return instance.State;
        }

        public int GetStartParameter(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return 0;
            return instance.StartParam;
        }

        public bool GetScriptRunning(UUID objectID, UUID itemID)
        {
            return GetScriptState(itemID);
        }

        public void OnShutdown()
        {
            List<IScriptInstance> instances = new List<IScriptInstance>();

            lock (m_Scripts)
            {
                foreach (IScriptInstance instance in m_Scripts.Values)
                    instances.Add(instance);
            }

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
    }
}
