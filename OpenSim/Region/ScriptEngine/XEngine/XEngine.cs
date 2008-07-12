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
using libsecondlife;
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
        public AsyncCommandManager m_AsyncCommands;
        bool m_firstStart = true;

        private static List<XEngine> m_ScriptEngines =
                new List<XEngine>();

        // Maps the local id to the script inventory items in it

        private Dictionary<uint, List<LLUUID> > m_PrimObjects =
                new Dictionary<uint, List<LLUUID> >();

        // Maps the LLUUID above to the script instance

        private Dictionary<LLUUID, XScriptInstance> m_Scripts =
                new Dictionary<LLUUID, XScriptInstance>();

        // Maps the asset ID to the assembly

        private Dictionary<LLUUID, string> m_Assemblies =
                new Dictionary<LLUUID, string>();

        // This will list AppDomains by script asset

        private Dictionary<LLUUID, AppDomain> m_AppDomains =
                new Dictionary<LLUUID, AppDomain>();

        // List the scripts running in each appdomain

        private Dictionary<LLUUID, List<LLUUID> > m_DomainScripts =
                new Dictionary<LLUUID, List<LLUUID> >();

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
        //     LLUUID ItemID;
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
            m_Scene=scene;

            m_ScriptConfig = configSource.Configs["XEngine"];

            if (m_ScriptConfig == null)
            {
                m_log.ErrorFormat("[XEngine] No script configuration found. Scripts disabled");
                return;
            }

            int minThreads = m_ScriptConfig.GetInt("MinThreads", 2);
            int maxThreads = m_ScriptConfig.GetInt("MaxThreads", 2);
            int idleTimeout = m_ScriptConfig.GetInt("IdleTimeout", 60);
            string priority = m_ScriptConfig.GetString("Priority", "BelowNormal");
            int maxScriptQueue = m_ScriptConfig.GetInt("MaxScriptEventQueue",300);
            int stackSize = m_ScriptConfig.GetInt("ThreadStackSize", 262144);
            int sleepTime = m_ScriptConfig.GetInt("MaintenanceInterval",
                    10)*1000;
            m_EventLimit = m_ScriptConfig.GetInt("EventLimit", 30);
            m_KillTimedOutScripts = m_ScriptConfig.GetBoolean(
                    "KillTimedOutScripts", false);
            int saveTime = m_ScriptConfig.GetInt("SaveInterval", 300)*1000;

            ThreadPriority prio = ThreadPriority.BelowNormal;
            switch (priority)
            {
            case "Lowest":
                prio=ThreadPriority.Lowest;
                break;
            case "BelowNormal":
                prio=ThreadPriority.BelowNormal;
                break;
            case "Normal":
                prio=ThreadPriority.Normal;
                break;
            case "AboveNormal":
                prio=ThreadPriority.AboveNormal;
                break;
            case "Highest":
                prio=ThreadPriority.Highest;
                break;
            default:
                m_log.ErrorFormat("[XEngine] Invalid thread priority: '"+
                        priority+"'. Assuming BelowNormal");
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

            m_AsyncCommands = new AsyncCommandManager(this);

            if (sleepTime > 0)
            {
                m_ThreadPool.QueueWorkItem(new WorkItemCallback(
                                               this.DoMaintenance), new Object[]
                    { sleepTime });
            }

            if (saveTime > 0)
            {
                m_ThreadPool.QueueWorkItem(new WorkItemCallback(
                                               this.DoBackup), new Object[] { saveTime });
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

            System.Threading.Thread.Sleep(saveTime);

//            m_log.Debug("[XEngine] Backing up script states");

            List<XScriptInstance> instances = new List<XScriptInstance>();

            lock (m_Scripts)
            {
                foreach (XScriptInstance instance in m_Scripts.Values)
                    instances.Add(instance);
            }

            foreach (XScriptInstance i in instances)
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

            m_ThreadPool.QueueWorkItem(new WorkItemCallback(
                    this.DoBackup), new Object[] { saveTime });

            return 0;
        }

        public object DoMaintenance(object p)
        {
            object[] parms = (object[])p;
            int sleepTime = (int)parms[0];

            foreach (XScriptInstance inst in m_Scripts.Values)
            {
                if (inst.EventTime() > m_EventLimit)
                {
                    inst.Stop(100);
                    if (!m_KillTimedOutScripts)
                        inst.Start();
                }
            }

            System.Threading.Thread.Sleep(sleepTime);

            m_ThreadPool.QueueWorkItem(new WorkItemCallback(
                    this.DoMaintenance), new Object[]
                    { sleepTime });

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

        //
        // XEngine functions
        //
        public int MaxScriptQueue
        {
            get { return m_MaxScriptQueue; }
        }

        public void OnRezScript(uint localID, LLUUID itemID, string script, int startParam, bool postOnRez)
        {
            Object[] parms = new Object[]
                    { localID, itemID, script, startParam, postOnRez};

            lock (m_CompileQueue)
            {
                m_CompileQueue.Enqueue(parms);
                if (m_CurrentCompile == null)
                {
                    if (m_firstStart)
                    {
                        m_firstStart = false;
                        m_CurrentCompile = m_ThreadPool.QueueWorkItem(
                                new WorkItemCallback(
                                this.DoScriptWait), new Object[0]);
                        return;
                    }
                    m_CurrentCompile = m_ThreadPool.QueueWorkItem(
                            new WorkItemCallback(
                            this.DoOnRezScriptQueue), new Object[0]);
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
                            new WorkItemCallback(
                            this.DoOnRezScriptQueue), new Object[0]);
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
                            new WorkItemCallback(
                            this.DoOnRezScriptQueue), new Object[0]);
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
            LLUUID itemID = (LLUUID)p[1];
            string script =(string)p[2];
            int startParam = (int)p[3];
            bool postOnRez = (bool)p[4];

            // Get the asset ID of the script, so we can check if we
            // already have it.

            SceneObjectPart part = m_Scene.GetSceneObjectPart(localID);
            if (part == null)
                return false;

            TaskInventoryItem item = part.GetInventoryItem(itemID);
            if (item == null)
                return false;

            LLUUID assetID = item.AssetID;

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
                    if (text.Length > 1400)
                        text = text.Substring(0, 1400);
                    World.SimChat(Helpers.StringToField(text),
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
                    LLUUID appDomain=assetID;

                    if (part.ParentGroup.RootPart.m_IsAttachment)
                        appDomain = part.ParentGroup.RootPart.UUID;

                    if (!m_AppDomains.ContainsKey(appDomain))
                    {
                        try
                        {
                            AppDomainSetup appSetup = new AppDomainSetup();
//                            appSetup.ApplicationBase = Path.Combine(
//                                    "ScriptEngines",
//                                    m_Scene.RegionInfo.RegionID.ToString());

                            Evidence baseEvidence =
                                AppDomain.CurrentDomain.Evidence;
                            Evidence evidence = new Evidence(baseEvidence);

                            m_AppDomains[appDomain] =
                                AppDomain.CreateDomain(
                                    m_Scene.RegionInfo.RegionID.ToString(),
                                    evidence, appSetup);

                            m_AppDomains[appDomain].AssemblyResolve +=
                                new ResolveEventHandler(
                                    AssemblyResolver.OnAssemblyResolve);
                            m_DomainScripts[appDomain] = new List<LLUUID>();
                        }
                        catch (Exception e)
                        {
                            m_log.Error("[XEngine] Exception creating app domain:\n"+e.ToString());
                            return false;
                        }
                    }
                    m_DomainScripts[appDomain].Add(itemID);

                    XScriptInstance instance = new XScriptInstance(this,localID,
                           part.UUID, itemID, assetID, assembly,
                           m_AppDomains[appDomain],
                           part.ParentGroup.RootPart.Name,
                           item.Name, startParam, postOnRez,
                           XScriptInstance.StateSource.NewRez);

                    m_log.DebugFormat("[XEngine] Loaded script {0}.{1}",
                            part.ParentGroup.RootPart.Name, item.Name);

                    instance.AppDomain = appDomain;

                    m_Scripts[itemID] = instance;
                }

                if (!m_PrimObjects.ContainsKey(localID))
                    m_PrimObjects[localID] = new List<LLUUID>();

                if (!m_PrimObjects[localID].Contains(itemID))
                    m_PrimObjects[localID].Add(itemID);

                if (!m_Assemblies.ContainsKey(assetID))
                    m_Assemblies[assetID] = assembly;
            }
            return true;
        }

        public void OnRemoveScript(uint localID, LLUUID itemID)
        {
            lock (m_Scripts)
            {
                // Do we even have it?
                if (!m_Scripts.ContainsKey(itemID))
                    return;

                m_AsyncCommands.RemoveScript(localID, itemID);

                XScriptInstance instance=m_Scripts[itemID];
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

                instance = null;

                CleanAssemblies();
            }
        }

        public void OnScriptReset(uint localID, LLUUID itemID)
        {
            ResetScript(itemID);
        }

        public void OnStartScript(uint localID, LLUUID itemID)
        {
            StartScript(itemID);
        }

        public void OnStopScript(uint localID, LLUUID itemID)
        {
            StopScript(itemID);
        }

        private void CleanAssemblies()
        {
            List<LLUUID> assetIDList = new List<LLUUID>(m_Assemblies.Keys);

            foreach (XScriptInstance i in m_Scripts.Values)
            {
                if (assetIDList.Contains(i.AssetID))
                    assetIDList.Remove(i.AssetID);
            }

            foreach (LLUUID assetID in assetIDList)
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

        private void UnloadAppDomain(LLUUID id)
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
        public IWorkItemResult QueueEventHandler(object parms)
        {
            return m_ThreadPool.QueueWorkItem(new WorkItemCallback(
                                                  this.ProcessEventHandler), parms);
        }

        //
        // The main script engine worker
        //
        private object ProcessEventHandler(object parms)
        {
            CultureInfo USCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = USCulture;

            XScriptInstance instance = (XScriptInstance) parms;

            return instance.EventProcessor();
        }

        //
        // Post event to an entire prim
        //
        public bool PostObjectEvent(uint localID, EventParams p)
        {
            bool result = false;

            if (!m_PrimObjects.ContainsKey(localID))
                return false;

            foreach (LLUUID itemID in m_PrimObjects[localID])
            {
                if (m_Scripts.ContainsKey(itemID))
                {
                    XScriptInstance instance = m_Scripts[itemID];
                    if (instance != null)
                    {
                        instance.PostEvent(p);
                        result = true;
                    }
                }
            }
            return result;
        }

        //
        // Post an event to a single script
        //
        public bool PostScriptEvent(LLUUID itemID, EventParams p)
        {
            if (m_Scripts.ContainsKey(itemID))
            {
                XScriptInstance instance = m_Scripts[itemID];
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

        private XScriptInstance GetInstance(LLUUID itemID)
        {
            XScriptInstance instance;
            lock (m_Scripts)
            {
                if (!m_Scripts.ContainsKey(itemID))
                    return null;
                instance = m_Scripts[itemID];
            }
            return instance;
        }

        public void SetScriptState(LLUUID itemID, bool running)
        {
            XScriptInstance instance = GetInstance(itemID);
            if (instance != null)
            {
                if (running)
                    instance.Start();
                else
                    instance.Stop(100);
            }
        }

        public bool GetScriptState(LLUUID itemID)
        {
            XScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                return instance.Running;
            return false;
        }

        public void ApiResetScript(LLUUID itemID)
        {
            XScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.ApiResetScript();
        }

        public void ResetScript(LLUUID itemID)
        {
            XScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.ResetScript();
        }

        public void StartScript(LLUUID itemID)
        {
            XScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.Start();
        }

        public void StopScript(LLUUID itemID)
        {
            XScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                instance.Stop(0);
        }

        public DetectParams GetDetectParams(LLUUID itemID, int idx)
        {
            XScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                return instance.GetDetectParams(idx);
            return null;
        }

        public LLUUID GetDetectID(LLUUID itemID, int idx)
        {
            XScriptInstance instance = GetInstance(itemID);
            if (instance != null)
                return instance.GetDetectID(idx);
            return LLUUID.Zero;
        }

        public void SetState(LLUUID itemID, string newState)
        {
            XScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return;
            instance.SetState(newState);
        }
        public string GetState(LLUUID itemID)
        {
            XScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return "default";
            return instance.State;
        }

        public int GetStartParameter(LLUUID itemID)
        {
            XScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return 0;
            return instance.StartParam;
        }

        public bool GetScriptRunning(LLUUID objectID, LLUUID itemID)
        {
            return GetScriptState(itemID);
        }
    }

    public class XScriptInstance
    {
        private XEngine m_Engine;
        private IWorkItemResult m_CurrentResult=null;
        private Queue m_EventQueue = new Queue(32);
        private bool m_RunEvents = false;
        private LLUUID m_ItemID;
        private uint m_LocalID;
        private LLUUID m_ObjectID;
        private LLUUID m_AssetID;
        private IScript m_Script;
        private Executor m_Executor;
        private LLUUID m_AppDomain;
        private DetectParams[] m_DetectParams;
        private bool m_TimerQueued;
        private DateTime m_EventStart;
        private bool m_InEvent;
        private string m_PrimName;
        private string m_ScriptName;
        private string m_Assembly;
        private int m_StartParam = 0;
        private string m_CurrentEvent = String.Empty;
        private bool m_InSelfDelete = false;

        private Dictionary<string,IScriptApi> m_Apis = new Dictionary<string,IScriptApi>();

        public enum StateSource
        {
            NewRez = 0,
            PrimCrossing = 1,
            AttachmentCrossing = 2
        }

        // Script state
        private string m_State="default";

        public Object[] PluginData = new Object[0];

        public bool Running
        {
            get { return m_RunEvents; }
            set { m_RunEvents = value; }
        }

        public string State
        {
            get { return m_State; }
            set { m_State = value; }
        }

        public XEngine Engine
        {
            get { return m_Engine; }
        }

        public LLUUID AppDomain
        {
            get { return m_AppDomain; }
            set { m_AppDomain = value; }
        }

        public string PrimName
        {
            get { return m_PrimName; }
        }

        public string ScriptName
        {
            get { return m_ScriptName; }
        }

        public LLUUID ItemID
        {
            get { return m_ItemID; }
        }

        public LLUUID ObjectID
        {
            get { return m_ObjectID; }
        }

        public uint LocalID
        {
            get { return m_LocalID; }
        }

        public LLUUID AssetID
        {
            get { return m_AssetID; }
        }

        public Queue EventQueue
        {
            get { return m_EventQueue; }
        }

        public void ClearQueue()
        {
            m_TimerQueued = false;
            m_EventQueue.Clear();
        }

        public int StartParam
        {
            get { return m_StartParam; }
            set { m_StartParam = value; }
        }

        public XScriptInstance(XEngine engine, uint localID, LLUUID objectID,
                LLUUID itemID, LLUUID assetID, string assembly, AppDomain dom,
                string primName, string scriptName, int startParam,
                bool postOnRez, StateSource stateSource)
        {
            m_Engine = engine;

            m_LocalID = localID;
            m_ObjectID = objectID;
            m_ItemID = itemID;
            m_AssetID = assetID;
            m_PrimName = primName;
            m_ScriptName = scriptName;
            m_Assembly = assembly;
            m_StartParam = startParam;

            ApiManager am = new ApiManager();

            SceneObjectPart part=engine.World.GetSceneObjectPart(localID);
            if (part == null)
            {
                engine.Log.Error("[XEngine] SceneObjectPart unavailable. Script NOT started.");
                return;
            }

            foreach (string api in am.GetApis())
            {
                m_Apis[api] = am.CreateApi(api);
                m_Apis[api].Initialize(engine, part, localID, itemID);
            }

            try
            {
                m_Script = (IScript)dom.CreateInstanceAndUnwrap(
                    Path.GetFileNameWithoutExtension(assembly),
                    "SecondLife.Script");
            }
            catch (Exception e)
            {
                m_Engine.Log.ErrorFormat("[XEngine] Error loading assembly {0}\n"+e.ToString(), assembly);
            }

            try
            {
                foreach (KeyValuePair<string,IScriptApi> kv in m_Apis)
                {
                    m_Script.InitApi(kv.Key, kv.Value);
                }

                m_Executor = new Executor(m_Script);

//                m_Engine.Log.Debug("[XEngine] Script instance created");

                part.SetScriptEvents(m_ItemID,
                                     (int)m_Executor.GetStateEventFlags(State));
            }
            catch (Exception e)
            {
                m_Engine.Log.Error("[XEngine] Error loading script instance\n"+e.ToString());
                return;
            }

            string savedState = Path.Combine(Path.GetDirectoryName(assembly),
                    m_ItemID.ToString() + ".state");
            if (File.Exists(savedState))
            {
                string xml = String.Empty;

                try
                {
                    FileInfo fi = new FileInfo(savedState);
                    int size=(int)fi.Length;
                    if (size < 512000)
                    {
                        using (FileStream fs = File.Open(savedState,
                                                         FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            System.Text.ASCIIEncoding enc =
                                new System.Text.ASCIIEncoding();

                            Byte[] data = new Byte[size];
                            fs.Read(data, 0, size);

                            xml = enc.GetString(data);

                            ScriptSerializer.Deserialize(xml, this);

                            m_Engine.m_AsyncCommands.CreateFromData(
                                m_LocalID, m_ItemID, m_ObjectID,
                                PluginData);

                            m_Engine.Log.DebugFormat("[XEngine] Successfully retrieved state for script {0}.{1}", m_PrimName, m_ScriptName);

                            if (m_RunEvents)
                            {
                                m_RunEvents = false;
                                Start();
                                if (postOnRez)
                                    PostEvent(new EventParams("on_rez",
                                        new Object[] {new LSL_Types.LSLInteger(startParam)}, new DetectParams[0]));
                            }

                            // we get new rez events on sim restart, too
                            // but if there is state, then we fire the change
                            // event
                            if (stateSource == StateSource.NewRez)
                            {
//                                m_Engine.Log.Debug("[XEngine] Posted changed(CHANGED_REGION_RESTART) to script");
                                PostEvent(new EventParams("changed",
                                    new Object[] {new LSL_Types.LSLInteger(256)}, new DetectParams[0]));
                            }
                        }
                    }
                    else
                    {
                        m_Engine.Log.Error("[XEngine] Unable to load script state: Memory limit exceeded");
                        Start();
                        PostEvent(new EventParams("state_entry",
                                                   new Object[0], new DetectParams[0]));
                        if (postOnRez)
                            PostEvent(new EventParams("on_rez",
                                new Object[] {new LSL_Types.LSLInteger(startParam)}, new DetectParams[0]));

                    }
                }
                catch (Exception e)
                {
                    m_Engine.Log.ErrorFormat("[XEngine] Unable to load script state from xml: {0}\n"+e.ToString(), xml);
                    Start();
                    PostEvent(new EventParams("state_entry",
                                               new Object[0], new DetectParams[0]));
                    if (postOnRez)
                        PostEvent(new EventParams("on_rez",
                                new Object[] {new LSL_Types.LSLInteger(startParam)}, new DetectParams[0]));
                }
            }
            else
            {
//                m_Engine.Log.ErrorFormat("[XEngine] Unable to load script state, file not found");
                Start();
                PostEvent(new EventParams("state_entry",
                                           new Object[0], new DetectParams[0]));

                if (postOnRez)
                    PostEvent(new EventParams("on_rez",
                            new Object[] {new LSL_Types.LSLInteger(startParam)}, new DetectParams[0]));
            }
        }

        public void RemoveState()
        {
            string savedState = Path.Combine(Path.GetDirectoryName(m_Assembly),
                    m_ItemID.ToString() + ".state");
            
            try
            {
                File.Delete(savedState);
            }
            catch(Exception)
            {
            }
        }

        public void VarDump(Dictionary<string, object> vars)
        {
            Console.WriteLine("Variable dump for script {0}", m_ItemID.ToString());
            foreach (KeyValuePair<string, object> v in vars)
            {
                Console.WriteLine("Variable: {0} = '{1}'", v. Key,
                                  v.Value.ToString());
            }
        }

        public void Start()
        {
            lock (m_EventQueue)
            {
                if (Running)
                    return;

                m_RunEvents = true;

                if (m_EventQueue.Count > 0)
                {
                    if (m_CurrentResult == null)
                        m_CurrentResult = m_Engine.QueueEventHandler(this);
                    else
                        m_Engine.Log.Error("[XEngine] Tried to start a script that was already queued");
                }
            }
        }

        public bool Stop(int timeout)
        {
            IWorkItemResult result;

            lock (m_EventQueue)
            {
                if (!Running)
                    return true;

                if (m_CurrentResult == null)
                {
                    m_RunEvents = false;
                    return true;
                }

                if (m_CurrentResult.Cancel())
                {
                    m_CurrentResult = null;
                    m_RunEvents = false;
                    return true;
                }

                result = m_CurrentResult;
                m_RunEvents = false;
            }

            if (SmartThreadPool.WaitAll(new IWorkItemResult[] {result}, new TimeSpan((long)timeout * 100000), false))
            {
                return true;
            }

            lock (m_EventQueue)
            {
                result = m_CurrentResult;
            }

            if (result == null)
                return true;

			if(!m_InSelfDelete)
				result.Abort();

            lock (m_EventQueue)
            {
                m_CurrentResult = null;
            }

            return true;
        }

        public void SetState(string state)
        {
            PostEvent(new EventParams("state_exit", new Object[0],
                                       new DetectParams[0]));
            PostEvent(new EventParams("state", new Object[] { state },
                                       new DetectParams[0]));
            PostEvent(new EventParams("state_entry", new Object[0],
                                       new DetectParams[0]));
        }

        public void PostEvent(EventParams data)
        {
//            m_Engine.Log.DebugFormat("[XEngine] Posted event {2} in state {3} to {0}.{1}",
//                        m_PrimName, m_ScriptName, data.EventName, m_State);

            if (!Running)
                return;

            lock (m_EventQueue)
            {
                if (m_EventQueue.Count >= m_Engine.MaxScriptQueue)
                    return;

                m_EventQueue.Enqueue(data);
                if (data.EventName == "timer")
                {
                    if (m_TimerQueued)
                        return;
                    m_TimerQueued = true;
                }

                if (!m_RunEvents)
                    return;

                if (m_CurrentResult == null)
                {
                    m_CurrentResult = m_Engine.QueueEventHandler(this);
                }
            }
        }

        public object EventProcessor()
        {
            EventParams data = null;

            lock (m_EventQueue)
            {
                data = (EventParams) m_EventQueue.Dequeue();
                if (data == null) // Shouldn't happen
                {
                    m_CurrentResult = null;
                    return 0;
                }
                if (data.EventName == "timer")
                    m_TimerQueued = false;
            }

            m_DetectParams = data.DetectParams;

            if (data.EventName == "state") // Hardcoded state change
            {
//                m_Engine.Log.DebugFormat("[XEngine] Script {0}.{1} state set to {2}",
//                        m_PrimName, m_ScriptName, data.Params[0].ToString());
                m_State=data.Params[0].ToString();
                m_Engine.m_AsyncCommands.RemoveScript(
                    m_LocalID, m_ItemID);

                SceneObjectPart part = m_Engine.World.GetSceneObjectPart(
                    m_LocalID);
                if (part != null)
                {
                    part.SetScriptEvents(m_ItemID,
                                         (int)m_Executor.GetStateEventFlags(State));
                }
            }
            else
            {
                SceneObjectPart part = m_Engine.World.GetSceneObjectPart(
                    m_LocalID);
//                m_Engine.Log.DebugFormat("[XEngine] Delivered event {2} in state {3} to {0}.{1}",
//                        m_PrimName, m_ScriptName, data.EventName, m_State);

                try
                {
                    m_CurrentEvent = data.EventName;
                    m_EventStart = DateTime.Now;
                    m_InEvent = true;

                    m_Executor.ExecuteEvent(State, data.EventName, data.Params);

                    m_InEvent = false;
                    m_CurrentEvent = String.Empty;
                }
                catch (Exception e)
                {
                    m_InEvent = false;
                    m_CurrentEvent = String.Empty;

                    if (!(e is TargetInvocationException) || (!(e.InnerException is EventAbortException) && (!(e.InnerException is SelfDeleteException))))
                    {
                        if (e is System.Threading.ThreadAbortException)
                        {
                            lock (m_EventQueue)
                            {
                                if ((m_EventQueue.Count > 0) && m_RunEvents)
                                {
                                    m_CurrentResult=m_Engine.QueueEventHandler(this);
                                }
                                else
                                {
                                    m_CurrentResult = null;
                                }
                            }

                            m_DetectParams = null;

                            return 0;
                        }

                        try
                        {
                            // DISPLAY ERROR INWORLD
                            string text = "Runtime error:\n" + e.ToString();
                            if (text.Length > 1400)
                                text = text.Substring(0, 1400);
                            m_Engine.World.SimChat(Helpers.StringToField(text),
                                                   ChatTypeEnum.DebugChannel, 2147483647,
                                                   part.AbsolutePosition,
                                                   part.Name, part.UUID, false);
                        }
                        catch (Exception e2) // LEGIT: User Scripting
                        {
                            m_Engine.Log.Error("[XEngine]: "+
                                               "Error displaying error in-world: " +
                                               e2.ToString());
                            m_Engine.Log.Error("[XEngine]: " +
                                               "Errormessage: Error compiling script:\r\n" +
                                               e.ToString());
                        }
                    }
                    else if((e is TargetInvocationException) && (e.InnerException is SelfDeleteException))
                    {
                        m_InSelfDelete = true;
                        if(part != null && part.ParentGroup != null)
                            m_Engine.World.DeleteSceneObject(part.ParentGroup);
                    }
                }
            }

            lock (m_EventQueue)
            {
                if ((m_EventQueue.Count > 0) && m_RunEvents)
                {
                    m_CurrentResult = m_Engine.QueueEventHandler(this);
                }
                else
                {
                    m_CurrentResult = null;
                }
            }

            m_DetectParams = null;

            return 0;
        }

        public int EventTime()
        {
            if (!m_InEvent)
                return 0;

            return (DateTime.Now - m_EventStart).Seconds;
        }

        public void ResetScript()
        {
            bool running = Running;

            RemoveState();

            Stop(0);
            SceneObjectPart part=m_Engine.World.GetSceneObjectPart(m_LocalID);
            part.GetInventoryItem(m_ItemID).PermsMask = 0;
            part.GetInventoryItem(m_ItemID).PermsGranter = LLUUID.Zero;
            m_Engine.m_AsyncCommands.RemoveScript(m_LocalID, m_ItemID);
            m_EventQueue.Clear();
            m_Script.ResetVars();
            m_State = "default";
            if (running)
                Start();
            PostEvent(new EventParams("state_entry",
                    new Object[0], new DetectParams[0]));
        }

        public void ApiResetScript()
        {
            // bool running = Running;

            RemoveState();

            m_Script.ResetVars();
            SceneObjectPart part=m_Engine.World.GetSceneObjectPart(m_LocalID);
            part.GetInventoryItem(m_ItemID).PermsMask = 0;
            part.GetInventoryItem(m_ItemID).PermsGranter = LLUUID.Zero;
            m_Engine.m_AsyncCommands.RemoveScript(m_LocalID, m_ItemID);
            if (m_CurrentEvent != "state_entry")
            {
                PostEvent(new EventParams("state_entry",
                        new Object[0], new DetectParams[0]));
            }
        }

        public Dictionary<string, object> GetVars()
        {
            return m_Script.GetVars();
        }

        public void SetVars(Dictionary<string, object> vars)
        {
            m_Script.SetVars(vars);
        }

        public DetectParams GetDetectParams(int idx)
        {
            if (idx < 0 || idx >= m_DetectParams.Length)
                return null;

            return m_DetectParams[idx];
        }

        public LLUUID GetDetectID(int idx)
        {
            if (idx < 0 || idx >= m_DetectParams.Length)
                return LLUUID.Zero;

            return m_DetectParams[idx].Key;
        }

        public void SaveState(string assembly)
        {
            PluginData =
                m_Engine.m_AsyncCommands.GetSerializationData(
                    m_ItemID);

            string xml = ScriptSerializer.Serialize(this);

            try
            {
                FileStream fs = File.Create(Path.Combine(Path.GetDirectoryName(assembly), m_ItemID.ToString() + ".state"));
                System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
                Byte[] buf = enc.GetBytes(xml);
                fs.Write(buf, 0, buf.Length);
                fs.Close();
            }
            catch(Exception e)
            {
                Console.WriteLine("Unable to save xml\n"+e.ToString());
            }
            if (!File.Exists(Path.Combine(Path.GetDirectoryName(assembly), m_ItemID.ToString() + ".state")))
            {
                throw new Exception("Completed persistence save, but no file was created");
            }
        }
    }

    public class ScriptSerializer
    {
        public static string Serialize(XScriptInstance instance)
        {
            bool running = instance.Running;

            if (running)
                instance.Stop(50);

            XmlDocument xmldoc = new XmlDocument();

            XmlNode xmlnode = xmldoc.CreateNode(XmlNodeType.XmlDeclaration,
                                                "", "");
            xmldoc.AppendChild(xmlnode);

            XmlElement rootElement = xmldoc.CreateElement("", "ScriptState",
                                                          "");
            xmldoc.AppendChild(rootElement);

            XmlElement state = xmldoc.CreateElement("", "State", "");
            state.AppendChild(xmldoc.CreateTextNode(instance.State));

            rootElement.AppendChild(state);

            XmlElement run = xmldoc.CreateElement("", "Running", "");
            run.AppendChild(xmldoc.CreateTextNode(
                    running.ToString()));

            rootElement.AppendChild(run);

            Dictionary<string, Object> vars = instance.GetVars();

            XmlElement variables = xmldoc.CreateElement("", "Variables", "");

            foreach (KeyValuePair<string, Object> var in vars)
                WriteTypedValue(xmldoc, variables, "Variable", var.Key,
                                var.Value);

            rootElement.AppendChild(variables);

            XmlElement queue = xmldoc.CreateElement("", "Queue", "");

            int count = instance.EventQueue.Count;

            while (count > 0)
            {
                EventParams ep = (EventParams)instance.EventQueue.Dequeue();
                instance.EventQueue.Enqueue(ep);
                count--;

                XmlElement item = xmldoc.CreateElement("", "Item", "");
                XmlAttribute itemEvent = xmldoc.CreateAttribute("", "event",
                                                                "");
                itemEvent.Value = ep.EventName;
                item.Attributes.Append(itemEvent);

                XmlElement parms = xmldoc.CreateElement("", "Params", "");

                foreach (Object o in ep.Params)
                    WriteTypedValue(xmldoc, parms, "Param", String.Empty, o);

                item.AppendChild(parms);

                XmlElement detect = xmldoc.CreateElement("", "Detected", "");

                foreach (DetectParams det in ep.DetectParams)
                {
                    XmlElement objectElem = xmldoc.CreateElement("", "Object",
                                                                 "");
                    XmlAttribute pos = xmldoc.CreateAttribute("", "pos", "");
                    pos.Value = det.OffsetPos.ToString();
                    objectElem.Attributes.Append(pos);

                    XmlAttribute d_linkNum = xmldoc.CreateAttribute("",
                            "linkNum", "");
                    d_linkNum.Value = det.LinkNum.ToString();
                    objectElem.Attributes.Append(d_linkNum);

                    XmlAttribute d_group = xmldoc.CreateAttribute("",
                            "group", "");
                    d_group.Value = det.Group.ToString();
                    objectElem.Attributes.Append(d_group);

                    XmlAttribute d_name = xmldoc.CreateAttribute("",
                            "name", "");
                    d_name.Value = det.Name.ToString();
                    objectElem.Attributes.Append(d_name);

                    XmlAttribute d_owner = xmldoc.CreateAttribute("",
                            "owner", "");
                    d_owner.Value = det.Owner.ToString();
                    objectElem.Attributes.Append(d_owner);

                    XmlAttribute d_position = xmldoc.CreateAttribute("",
                            "position", "");
                    d_position.Value = det.Position.ToString();
                    objectElem.Attributes.Append(d_position);

                    XmlAttribute d_rotation = xmldoc.CreateAttribute("",
                            "rotation", "");
                    d_rotation.Value = det.Rotation.ToString();
                    objectElem.Attributes.Append(d_rotation);

                    XmlAttribute d_type = xmldoc.CreateAttribute("",
                            "type", "");
                    d_type.Value = det.Type.ToString();
                    objectElem.Attributes.Append(d_type);

                    XmlAttribute d_velocity = xmldoc.CreateAttribute("",
                            "velocity", "");
                    d_velocity.Value = det.Velocity.ToString();
                    objectElem.Attributes.Append(d_velocity);

                    objectElem.AppendChild(
                        xmldoc.CreateTextNode(det.Key.ToString()));

                    detect.AppendChild(objectElem);
                }

                item.AppendChild(detect);

                queue.AppendChild(item);
            }

            rootElement.AppendChild(queue);

            XmlNode plugins = xmldoc.CreateElement("", "Plugins", "");
            DumpList(xmldoc, plugins,
                     new LSL_Types.list(instance.PluginData));

            rootElement.AppendChild(plugins);

            if (running)
                instance.Start();

            return xmldoc.InnerXml;
        }

        public static void Deserialize(string xml, XScriptInstance instance)
        {
            XmlDocument doc = new XmlDocument();

            Dictionary<string, object> vars = instance.GetVars();

            instance.PluginData = new Object[0];

            doc.LoadXml(xml);

            XmlNodeList rootL = doc.GetElementsByTagName("ScriptState");
            if (rootL.Count != 1)
            {
                return;
            }
            XmlNode rootNode = rootL[0];

            if (rootNode != null)
            {
                object varValue;
                XmlNodeList partL = rootNode.ChildNodes;

                foreach (XmlNode part in partL)
                {
                    switch (part.Name)
                    {
                    case "State":
                        instance.State=part.InnerText;
                        break;
                    case "Running":
                        instance.Running=bool.Parse(part.InnerText);
                        break;
                    case "Variables":
                        XmlNodeList varL = part.ChildNodes;
                        foreach (XmlNode var in varL)
                        {
                            string varName;
                            varValue=ReadTypedValue(var, out varName);

                            if (vars.ContainsKey(varName))
                                vars[varName] = varValue;
                        }
                        instance.SetVars(vars);
                        break;
                    case "Queue":
                        XmlNodeList itemL = part.ChildNodes;
                        foreach (XmlNode item in itemL)
                        {
                            List<Object> parms = new List<Object>();
                            List<DetectParams> detected =
                                    new List<DetectParams>();

                            string eventName =
                                    item.Attributes.GetNamedItem("event").Value;
                            XmlNodeList eventL = item.ChildNodes;
                            foreach (XmlNode evt in eventL)
                            {
                                switch (evt.Name)
                                {
                                case "Params":
                                    XmlNodeList prms = evt.ChildNodes;
                                    foreach (XmlNode pm in prms)
                                        parms.Add(ReadTypedValue(pm));

                                    break;
                                case "Detected":
                                    XmlNodeList detL = evt.ChildNodes;
                                    foreach (XmlNode det in detL)
                                    {
                                        string vect =
                                                det.Attributes.GetNamedItem(
                                                "pos").Value;
                                        LSL_Types.Vector3 v =
                                                new LSL_Types.Vector3(vect);

                                        int d_linkNum=0;
                                        LLUUID d_group = LLUUID.Zero;
                                        string d_name = String.Empty;
                                        LLUUID d_owner = LLUUID.Zero;
                                        LSL_Types.Vector3 d_position =
                                            new LSL_Types.Vector3();
                                        LSL_Types.Quaternion d_rotation =
                                            new LSL_Types.Quaternion();
                                        int d_type = 0;
                                        LSL_Types.Vector3 d_velocity =
                                            new LSL_Types.Vector3();

                                        try
                                        {
                                            string tmp;

                                            tmp = det.Attributes.GetNamedItem(
                                                    "linkNum").Value;
                                            int.TryParse(tmp, out d_linkNum);

                                            tmp = det.Attributes.GetNamedItem(
                                                    "group").Value;
                                            LLUUID.TryParse(tmp, out d_group);

                                            d_name = det.Attributes.GetNamedItem(
                                                    "name").Value;

                                            tmp = det.Attributes.GetNamedItem(
                                                    "owner").Value;
                                            LLUUID.TryParse(tmp, out d_owner);

                                            tmp = det.Attributes.GetNamedItem(
                                                    "position").Value;
                                            d_position =
                                                new LSL_Types.Vector3(tmp);

                                            tmp = det.Attributes.GetNamedItem(
                                                    "rotation").Value;
                                            d_rotation =
                                                new LSL_Types.Quaternion(tmp);

                                            tmp = det.Attributes.GetNamedItem(
                                                    "type").Value;
                                            int.TryParse(tmp, out d_type);

                                            tmp = det.Attributes.GetNamedItem(
                                                    "velocity").Value;
                                            d_velocity =
                                                new LSL_Types.Vector3(tmp);

                                        }
                                        catch (Exception) // Old version XML
                                        {
                                        }

                                        LLUUID uuid = new LLUUID();
                                        LLUUID.TryParse(det.InnerText,
                                                out uuid);

                                        DetectParams d = new DetectParams();
                                        d.Key = uuid;
                                        d.OffsetPos = v;
                                        d.LinkNum = d_linkNum;
                                        d.Group = d_group;
                                        d.Name = d_name;
                                        d.Owner = d_owner;
                                        d.Position = d_position;
                                        d.Rotation = d_rotation;
                                        d.Type = d_type;
                                        d.Velocity = d_velocity;

                                        detected.Add(d);
                                    }
                                    break;
                                }
                            }
                            EventParams ep = new EventParams(
                                    eventName, parms.ToArray(),
                                    detected.ToArray());
                            instance.EventQueue.Enqueue(ep);
                        }
                        break;
                    case "Plugins":
                        instance.PluginData = ReadList(part).Data;
                        break;
                    }
                }
            }
        }

        private static void DumpList(XmlDocument doc, XmlNode parent,
                LSL_Types.list l)
        {
            foreach (Object o in l.Data)
                WriteTypedValue(doc, parent, "ListItem", "", o);
        }

        private static LSL_Types.list ReadList(XmlNode parent)
        {
            List<Object> olist = new List<Object>();

            XmlNodeList itemL = parent.ChildNodes;
            foreach (XmlNode item in itemL)
                olist.Add(ReadTypedValue(item));

            return new LSL_Types.list(olist.ToArray());
        }

        private static void WriteTypedValue(XmlDocument doc, XmlNode parent,
                string tag, string name, object value)
        {
            Type t=value.GetType();
            XmlAttribute typ = doc.CreateAttribute("", "type", "");
            XmlNode n = doc.CreateElement("", tag, "");

            if (value is LSL_Types.list)
            {
                typ.Value = "list";
                n.Attributes.Append(typ);

                DumpList(doc, n, (LSL_Types.list) value);

                if (name != String.Empty)
                {
                    XmlAttribute nam = doc.CreateAttribute("", "name", "");
                    nam.Value = name;
                    n.Attributes.Append(nam);
                }

                parent.AppendChild(n);
                return;
            }

            n.AppendChild(doc.CreateTextNode(value.ToString()));

            typ.Value = t.ToString();
            n.Attributes.Append(typ);
            if (name != String.Empty)
            {
                XmlAttribute nam = doc.CreateAttribute("", "name", "");
                nam.Value = name;
                n.Attributes.Append(nam);
            }

            parent.AppendChild(n);
        }

        private static object ReadTypedValue(XmlNode tag, out string name)
        {
            name = tag.Attributes.GetNamedItem("name").Value;

            return ReadTypedValue(tag);
        }

        private static object ReadTypedValue(XmlNode tag)
        {
            Object varValue;
            string assembly;

            string itemType = tag.Attributes.GetNamedItem("type").Value;

            if (itemType == "list")
                return ReadList(tag);

            if (itemType == "libsecondlife.LLUUID")
            {
                LLUUID val = new LLUUID();
                LLUUID.TryParse(tag.InnerText, out val);

                return val;
            }

            Type itemT = Type.GetType(itemType);
            if (itemT == null)
            {
                Object[] args =
                    new Object[] { tag.InnerText };

                assembly = itemType+", OpenSim.Region.ScriptEngine.Shared";
                itemT = Type.GetType(assembly);
                if (itemT == null)
                    return null;

                varValue = Activator.CreateInstance(itemT, args);

                if (varValue == null)
                    return null;
            }
            else
            {
                varValue = Convert.ChangeType(tag.InnerText, itemT);
            }
            return varValue;
        }
    }
}
