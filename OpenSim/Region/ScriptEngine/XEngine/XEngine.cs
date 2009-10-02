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
using OpenSim.Region.CoreModules.Framework.EventQueue;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Shared.CodeTools;
using OpenSim.Region.ScriptEngine.Shared.Instance;
using OpenSim.Region.ScriptEngine.Interfaces;

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

// disable warning: need to keep a reference to XEngine.EventManager
// alive to avoid it being garbage collected
#pragma warning disable 414
        private EventManager m_EventManager;
#pragma warning restore 414
        private IXmlRpcRouter m_XmlRpcRouter;
        private int m_EventLimit;
        private bool m_KillTimedOutScripts;

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

        private Dictionary<string, int> m_AddingAssemblies =
                new Dictionary<string, int>();

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

            m_EventLimit = m_ScriptConfig.GetInt("EventLimit", 30);
            m_KillTimedOutScripts = m_ScriptConfig.GetBoolean("KillTimedOutScripts", false);
            m_SaveTime = m_ScriptConfig.GetInt("SaveInterval", 120) * 1000;

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
        }

        public void RemoveRegion(Scene scene)
        {
            lock (m_Scripts)
            {
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

            Object[] parms = new Object[]{localID, itemID, script, startParam, postOnRez, (StateSource)stateSource};

            if (stateSource == (int)StateSource.ScriptedRez)
            {
                DoOnRezScript(parms);
            }
            else
            {
                lock (m_CompileQueue)
                {
                    m_CompileQueue.Enqueue(parms);

                    if (m_CurrentCompile == null)
                    {
                        m_CurrentCompile = m_ThreadPool.QueueWorkItem(
                                new WorkItemCallback(this.DoOnRezScriptQueue),
                                new Object[0]);
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
                lock (m_CompileQueue) 
                {
                    if (m_CompileQueue.Count==0)
                        // No scripts on region, so won't get triggered later
                        // by the queue becoming empty so we trigger it here
                        m_Scene.EventManager.TriggerEmptyScriptCompileQueue(0, String.Empty);
                }
            }

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
                    m_Scene.EventManager.TriggerEmptyScriptCompileQueue(m_ScriptFailCount, 
                                                                        m_ScriptErrorMessage);
                    m_ScriptFailCount = 0;
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
            StateSource stateSource = (StateSource)p[5];

            // Get the asset ID of the script, so we can check if we
            // already have it.

            // We must look for the part outside the m_Scripts lock because GetSceneObjectPart later triggers the
            // m_parts lock on SOG.  At the same time, a scene object that is being deleted will take the m_parts lock
            // and then later on try to take the m_scripts lock in this class when it calls OnRemoveScript()
            SceneObjectPart part = m_Scene.GetSceneObjectPart(localID);
            if (part == null)
            {
                m_log.Error("[Script] SceneObjectPart unavailable. Script NOT started.");
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

            try
            {
                lock (m_AddingAssemblies) 
                {
                    assembly = (string)m_Compiler.PerformScriptCompile(script,
                            assetID.ToString(), item.OwnerID);
                    if (!m_AddingAssemblies.ContainsKey(assembly)) {
                        m_AddingAssemblies[assembly] = 1;
                    } else {
                        m_AddingAssemblies[assembly]++;
                    }
                    linemap = m_Compiler.LineMap();
                }

                string[] warnings = m_Compiler.GetWarnings();

                if (warnings != null && warnings.Length != 0)
                {
                    if (presence != null && (!postOnRez))
                        presence.ControllingClient.SendAgentAlertMessage("Script saved with warnings, check debug window!", false);

                    foreach (string warning in warnings)
                    {
                        try
                        {
                            // DISPLAY WARNING INWORLD
                            string text = "Warning:\n" + warning;
                            if (text.Length > 1000)
                                text = text.Substring(0, 1000);
                            World.SimChat(Utils.StringToBytes(text),
                                          ChatTypeEnum.DebugChannel, 2147483647,
                                          part.AbsolutePosition,
                                          part.Name, part.UUID, false);
                        }
                        catch (Exception e2) // LEGIT: User Scripting
                        {
                            m_log.Error("[XEngine]: " +
                                    "Error displaying warning in-world: " +
                                    e2.ToString());
                            m_log.Error("[XEngine]: " +
                                    "Warning:\r\n" +
                                    warning);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (presence != null && (!postOnRez))
                    presence.ControllingClient.SendAgentAlertMessage("Script saved with errors, check debug window!", false);
                try
                {
                    // DISPLAY ERROR INWORLD
                    m_ScriptErrorMessage += "Failed to compile script in object: '" + part.ParentGroup.RootPart.Name + "' Script name: '" + item.Name + "' Error message: " + e.Message.ToString();

                    m_ScriptFailCount++;
                    string text = "Error compiling script:\n" + e.Message.ToString();
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
                ScriptInstance instance = null;
                // Create the object record

                if ((!m_Scripts.ContainsKey(itemID)) ||
                    (m_Scripts[itemID].AssetID != assetID))
                {
                    UUID appDomain = assetID;

                    if (part.ParentGroup.IsAttachment)
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

                            AppDomain sandbox =
                                AppDomain.CreateDomain(
                                    m_Scene.RegionInfo.RegionID.ToString(),
                                    evidence, appSetup);
/*
                            PolicyLevel sandboxPolicy = PolicyLevel.CreateAppDomainLevel();
                            AllMembershipCondition sandboxMembershipCondition = new AllMembershipCondition();
                            PermissionSet sandboxPermissionSet = sandboxPolicy.GetNamedPermissionSet("Internet");
                            PolicyStatement sandboxPolicyStatement = new PolicyStatement(sandboxPermissionSet);
                            CodeGroup sandboxCodeGroup = new UnionCodeGroup(sandboxMembershipCondition, sandboxPolicyStatement);
                            sandboxPolicy.RootCodeGroup = sandboxCodeGroup;
                            sandbox.SetAppDomainPolicy(sandboxPolicy);
*/
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
                    
                    m_log.DebugFormat("[XEngine] Loaded script {0}.{1}, script UUID {2}, prim UUID {3} @ {4}",
                            part.ParentGroup.RootPart.Name, item.Name, assetID, part.UUID, part.ParentGroup.RootPart.AbsolutePosition.ToString());

                    instance.AppDomain = appDomain;
                    instance.LineMap = linemap;

                    m_Scripts[itemID] = instance;
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

                IScriptInstance instance=m_Scripts[itemID];
                m_Scripts.Remove(itemID);

                instance.ClearQueue();
                instance.Stop(0);

                SceneObjectPart part =
                    m_Scene.GetSceneObjectPart(localID);

                if (part != null)
                    part.RemoveScriptEvents(itemID);

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
                    handlerObjectRemoved(part.UUID);

                CleanAssemblies();
            }

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

                AppDomain.Unload(domain);
                domain = null;
//                m_log.DebugFormat("[XEngine] Unloaded app domain {0}", id.ToString());
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
            
            lock (m_PrimObjects)
            {
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
                eq.Enqueue(EventQueueHelper.ScriptRunningReplyEvent(objectID, itemID, GetScriptState(itemID), true),
                           controllingClient.AgentId);
            }
        }

        public string GetAssemblyName(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return "";
            return instance.GetAssemblyName();
        }

        public string GetXMLState(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return "";
            return instance.GetXMLState();
        }

        public bool CanBeDeleted(UUID itemID)
        {
            IScriptInstance instance = GetInstance(itemID);
            if (instance == null)
                return true;

            return instance.CanBeDeleted();
        }
    }
}
