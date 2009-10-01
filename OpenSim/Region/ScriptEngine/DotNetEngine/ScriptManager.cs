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
using System.Reflection;
using System.Globalization;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using OpenSim.Region.ScriptEngine.Shared.Api.Runtime;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Shared.CodeTools;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    public class InstanceData
    {
        public IScript Script;
        public string State;
        public bool Running;
        public bool Disabled;
        public string Source;
        public int StartParam;
        public AppDomain AppDomain;
        public Dictionary<string, IScriptApi> Apis;
        public Dictionary<KeyValuePair<int,int>, KeyValuePair<int,int>>
                LineMap;
//        public ISponsor ScriptSponsor;
    }

    public class ScriptManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region Declares

        private Thread scriptLoadUnloadThread;
        private static Thread staticScriptLoadUnloadThread = null;
        private Queue<LUStruct> LUQueue = new Queue<LUStruct>();
        private static bool PrivateThread;
        private int LoadUnloadMaxQueueSize;
        private Object scriptLock = new Object();
        private bool m_started = false;
        private Dictionary<InstanceData, DetectParams[]> detparms =
                new Dictionary<InstanceData, DetectParams[]>();

        // Load/Unload structure
        private struct LUStruct
        {
            public uint localID;
            public UUID itemID;
            public string script;
            public LUType Action;
            public int startParam;
            public bool postOnRez;
        }

        private enum LUType
        {
            Unknown = 0,
            Load = 1,
            Unload = 2
        }

        public Dictionary<uint, Dictionary<UUID, InstanceData>> Scripts =
            new Dictionary<uint, Dictionary<UUID, InstanceData>>();

        private Compiler LSLCompiler;

        public Scene World
        {
            get { return m_scriptEngine.World; }
        }

        #endregion

        public void Initialize()
        {
            // Create our compiler
            LSLCompiler = new Compiler(m_scriptEngine);
        }

        public void _StartScript(uint localID, UUID itemID, string Script,
                int startParam, bool postOnRez)
        {
            m_log.DebugFormat(
                "[{0}]: ScriptManager StartScript: localID: {1}, itemID: {2}",
                m_scriptEngine.ScriptEngineName, localID, itemID);

            // We will initialize and start the script.
            // It will be up to the script itself to hook up the correct events.
            string CompiledScriptFile = String.Empty;

            SceneObjectPart m_host = World.GetSceneObjectPart(localID);

            if (null == m_host)
            {
                m_log.ErrorFormat(
                    "[{0}]: Could not find scene object part corresponding "+
                    "to localID {1} to start script",
                    m_scriptEngine.ScriptEngineName, localID);

                return;
            }

            UUID assetID = UUID.Zero;
            TaskInventoryItem taskInventoryItem = new TaskInventoryItem();
            if (m_host.TaskInventory.TryGetValue(itemID, out taskInventoryItem))
                assetID = taskInventoryItem.AssetID;

            ScenePresence presence =
                    World.GetScenePresence(taskInventoryItem.OwnerID);

            CultureInfo USCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = USCulture;

            try
            {
                // Compile (We assume LSL)
                CompiledScriptFile =
                        (string)LSLCompiler.PerformScriptCompile(Script,
                        assetID.ToString(), taskInventoryItem.OwnerID);

                if (presence != null && (!postOnRez))
                    presence.ControllingClient.SendAgentAlertMessage(
                            "Compile successful", false);

                m_log.InfoFormat("[SCRIPT]: Compiled assetID {0}: {1}",
                        assetID, CompiledScriptFile);

                InstanceData id = new InstanceData();

                IScript CompiledScript;
                CompiledScript =
                        m_scriptEngine.m_AppDomainManager.LoadScript(
                        CompiledScriptFile, out id.AppDomain);
                //Register the sponsor
//                ISponsor scriptSponsor = new ScriptSponsor();
//                ILease lease = (ILease)RemotingServices.GetLifetimeService(CompiledScript as MarshalByRefObject);
//                lease.Register(scriptSponsor);
//                id.ScriptSponsor = scriptSponsor;

                id.LineMap = LSLCompiler.LineMap();
                id.Script = CompiledScript;
                id.Source = Script;
                id.StartParam = startParam;
                id.State = "default";
                id.Running = true;
                id.Disabled = false;

                // Add it to our script memstruct
                m_scriptEngine.m_ScriptManager.SetScript(localID, itemID, id);

                id.Apis = new Dictionary<string, IScriptApi>();

                ApiManager am = new ApiManager();

                foreach (string api in am.GetApis())
                {
                    id.Apis[api] = am.CreateApi(api);
                    id.Apis[api].Initialize(m_scriptEngine, m_host,
                            localID, itemID);
                }

                foreach (KeyValuePair<string,IScriptApi> kv in id.Apis)
                {
                    CompiledScript.InitApi(kv.Key, kv.Value);
                }

                // Fire the first start-event
                int eventFlags =
                        m_scriptEngine.m_ScriptManager.GetStateEventFlags(
                        localID, itemID);

                m_host.SetScriptEvents(itemID, eventFlags);

                m_scriptEngine.m_EventQueueManager.AddToScriptQueue(
                        localID, itemID, "state_entry", new DetectParams[0],
                        new object[] { });

                if (postOnRez)
                {
                    m_scriptEngine.m_EventQueueManager.AddToScriptQueue(
                        localID, itemID, "on_rez", new DetectParams[0],
                        new object[] { new LSL_Types.LSLInteger(startParam) });
                }

                string[] warnings = LSLCompiler.GetWarnings();

                if (warnings != null && warnings.Length != 0)
                {
                    if (presence != null && (!postOnRez))
                        presence.ControllingClient.SendAgentAlertMessage(
                                "Script saved with warnings, check debug window!",
                                false);

                    foreach (string warning in warnings)
                    {
                        try
                        {
                            // DISPLAY WARNING INWORLD
                            string text = "Warning:\n" + warning;
                            if (text.Length > 1100)
                                text = text.Substring(0, 1099);

                            World.SimChat(Utils.StringToBytes(text),
                                    ChatTypeEnum.DebugChannel, 2147483647,
                                    m_host.AbsolutePosition, m_host.Name, m_host.UUID,
                                    false);
                        }
                        catch (Exception e2) // LEGIT: User Scripting
                        {
                            m_log.Error("[" +
                                    m_scriptEngine.ScriptEngineName +
                                    "]: Error displaying warning in-world: " +
                                    e2.ToString());
                            m_log.Error("[" +
                                    m_scriptEngine.ScriptEngineName + "]: " +
                                    "Warning:\r\n" +
                                    warning);
                        }
                    }
                }
            }
            catch (Exception e) // LEGIT: User Scripting
            {
                if (presence != null && (!postOnRez))
                    presence.ControllingClient.SendAgentAlertMessage(
                            "Script saved with errors, check debug window!",
                            false);
                try
                {
                    // DISPLAY ERROR INWORLD
                    string text = "Error compiling script:\n" +
                            e.Message.ToString();
                    if (text.Length > 1100)
                        text = text.Substring(0, 1099);

                    World.SimChat(Utils.StringToBytes(text),
                            ChatTypeEnum.DebugChannel, 2147483647,
                            m_host.AbsolutePosition, m_host.Name, m_host.UUID,
                            false);
                }
                catch (Exception e2) // LEGIT: User Scripting
                {
                    m_log.Error("[" +
                                m_scriptEngine.ScriptEngineName +
                                "]: Error displaying error in-world: " +
                                e2.ToString());
                    m_log.Error("[" +
                                m_scriptEngine.ScriptEngineName + "]: " +
                                "Errormessage: Error compiling script:\r\n" + 
                                e2.Message.ToString());
                }
            }
        }

        public void _StopScript(uint localID, UUID itemID)
        {
            InstanceData id = GetScript(localID, itemID);
            if (id == null)
                return;

            m_log.DebugFormat("[{0}]: Unloading script",
                              m_scriptEngine.ScriptEngineName);

            // Stop long command on script
            AsyncCommandManager.RemoveScript(m_scriptEngine, localID, itemID);

            try
            {
                // Get AppDomain
                // Tell script not to accept new requests
                id.Running = false;
                id.Disabled = true;
                AppDomain ad = id.AppDomain;

                // Remove from internal structure
                RemoveScript(localID, itemID);

                // Tell AppDomain that we have stopped script
                m_scriptEngine.m_AppDomainManager.StopScript(ad);
            }
            catch (Exception e) // LEGIT: User Scripting
            {
                m_log.Error("[" +
                            m_scriptEngine.ScriptEngineName +
                            "]: Exception stopping script localID: " +
                            localID + " LLUID: " + itemID.ToString() +
                            ": " + e.ToString());
            }
        }

        public void ReadConfig()
        {
            // TODO: Requires sharing of all ScriptManagers to single thread
            PrivateThread = true;
            LoadUnloadMaxQueueSize = m_scriptEngine.ScriptConfigSource.GetInt(
                    "LoadUnloadMaxQueueSize", 100);
        }

        #region Object init/shutdown

        public ScriptEngine m_scriptEngine;

        public ScriptManager(ScriptEngine scriptEngine)
        {
            m_scriptEngine = scriptEngine;
        }

        public void Setup()
        {
            ReadConfig();
            Initialize();
        }

        public void Start()
        {
            m_started = true;


            AppDomain.CurrentDomain.AssemblyResolve +=
                    new ResolveEventHandler(CurrentDomain_AssemblyResolve);

            //
            // CREATE THREAD
            // Private or shared
            //
            if (PrivateThread)
            {
                // Assign one thread per region
                //scriptLoadUnloadThread = StartScriptLoadUnloadThread();
            }
            else
            {
                // Shared thread - make sure one exist, then assign it to the private
                if (staticScriptLoadUnloadThread == null)
                {
                    //staticScriptLoadUnloadThread =
                    //        StartScriptLoadUnloadThread();
                }
                scriptLoadUnloadThread = staticScriptLoadUnloadThread;
            }
        }

        ~ScriptManager()
        {
            // Abort load/unload thread
            try
            {
                if (scriptLoadUnloadThread != null &&
                        scriptLoadUnloadThread.IsAlive == true)
                {
                    scriptLoadUnloadThread.Abort();
                    //scriptLoadUnloadThread.Join();
                }
            }
            catch
            {
            }
        }

        #endregion

        #region Load / Unload scripts (Thread loop)

        public void DoScriptLoadUnload()
        {
            if (!m_started)
                return;

            lock (LUQueue)
            {
                if (LUQueue.Count > 0)
                {
                    LUStruct item = LUQueue.Dequeue();

                    if (item.Action == LUType.Unload)
                    {
                        _StopScript(item.localID, item.itemID);
                        RemoveScript(item.localID, item.itemID);
                    }
                    else if (item.Action == LUType.Load)
                    {
                        m_log.DebugFormat("[{0}]: Loading script",
                                          m_scriptEngine.ScriptEngineName);
                        _StartScript(item.localID, item.itemID, item.script,
                                     item.startParam, item.postOnRez);
                    }
                }
            }
        }

        #endregion

        #region Helper functions

        private static Assembly CurrentDomain_AssemblyResolve(
                object sender, ResolveEventArgs args)
        {
            return Assembly.GetExecutingAssembly().FullName == args.Name ?
                    Assembly.GetExecutingAssembly() : null;
        }

        #endregion

        #region Start/Stop/Reset script

        /// <summary>
        /// Fetches, loads and hooks up a script to an objects events
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="localID"></param>
        public void StartScript(uint localID, UUID itemID, string Script, int startParam, bool postOnRez)
        {
            lock (LUQueue)
            {
                if ((LUQueue.Count >= LoadUnloadMaxQueueSize) && m_started)
                {
                    m_log.Error("[" +
                                m_scriptEngine.ScriptEngineName +
                                "]: ERROR: Load/unload queue item count is at " +
                                LUQueue.Count +
                                ". Config variable \"LoadUnloadMaxQueueSize\" "+
                                "is set to " + LoadUnloadMaxQueueSize +
                                ", so ignoring new script.");

                    return;
                }

                LUStruct ls = new LUStruct();
                ls.localID = localID;
                ls.itemID = itemID;
                ls.script = Script;
                ls.Action = LUType.Load;
                ls.startParam = startParam;
                ls.postOnRez = postOnRez;
                LUQueue.Enqueue(ls);
            }
        }

        /// <summary>
        /// Disables and unloads a script
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="itemID"></param>
        public void StopScript(uint localID, UUID itemID)
        {
            LUStruct ls = new LUStruct();
            ls.localID = localID;
            ls.itemID = itemID;
            ls.Action = LUType.Unload;
            ls.startParam = 0;
            ls.postOnRez = false;
            lock (LUQueue)
            {
                LUQueue.Enqueue(ls);
            }
        }

        #endregion

        #region Perform event execution in script

        // Execute a LL-event-function in Script
        internal void ExecuteEvent(uint localID, UUID itemID,
                string FunctionName, DetectParams[] qParams, object[] args)
        {
            int ExeStage=0;             //        ;^) Ewe Loon, for debuging
            InstanceData id=null;
            try                         //        ;^) Ewe Loon,fix
            {                           //        ;^) Ewe Loon,fix
                ExeStage = 1;           //        ;^) Ewe Loon, for debuging
                id = GetScript(localID, itemID);
                if (id == null)
                    return;
                ExeStage = 2;           //        ;^) Ewe Loon, for debuging
                if (qParams.Length>0)   //        ;^) Ewe Loon,fix
                    detparms[id] = qParams;
                ExeStage = 3;           //        ;^) Ewe Loon, for debuging
                if (id.Running)
                    id.Script.ExecuteEvent(id.State, FunctionName, args);
                ExeStage = 4;           //        ;^) Ewe Loon, for debuging
                if (qParams.Length>0)   //        ;^) Ewe Loon,fix 
                    detparms.Remove(id);
                ExeStage = 5;           //        ;^) Ewe Loon, for debuging
            }
            catch (Exception e)         //        ;^) Ewe Loon, From here down tis fix
            {
                if ((ExeStage == 3)&&(qParams.Length>0))
                    detparms.Remove(id);
                SceneObjectPart ob = m_scriptEngine.World.GetSceneObjectPart(localID);
                m_log.InfoFormat("[Script Error] ,{0},{1},@{2},{3},{4},{5}", ob.Name , FunctionName, ExeStage, e.Message, qParams.Length, detparms.Count);
                if (ExeStage != 2) throw e;
            }
        }

        public uint GetLocalID(UUID itemID)
        {
            foreach (KeyValuePair<uint, Dictionary<UUID, InstanceData> > k
                    in Scripts)
            {
                if (k.Value.ContainsKey(itemID))
                    return k.Key;
            }
            return 0;
        }

        public int GetStateEventFlags(uint localID, UUID itemID)
        {
            try
            {
                InstanceData id = GetScript(localID, itemID);
                if (id == null)
                {
                    return 0;
                }
                int evflags = id.Script.GetStateEventFlags(id.State);

                return (int)evflags;
            }
            catch (Exception)
            {
            }

            return 0;
        }

        #endregion

        #region Internal functions to keep track of script

        public List<UUID> GetScriptKeys(uint localID)
        {
            if (Scripts.ContainsKey(localID) == false)
                return new List<UUID>();

            Dictionary<UUID, InstanceData> Obj;
            Scripts.TryGetValue(localID, out Obj);

            return new List<UUID>(Obj.Keys);
        }

        public InstanceData GetScript(uint localID, UUID itemID)
        {
            lock (scriptLock)
            {
                InstanceData id = null;

                if (Scripts.ContainsKey(localID) == false)
                    return null;

                Dictionary<UUID, InstanceData> Obj;
                Scripts.TryGetValue(localID, out Obj);
                if (Obj==null) return null;
                if (Obj.ContainsKey(itemID) == false)
                    return null;

                // Get script
                Obj.TryGetValue(itemID, out id);
                return id;
            }
        }

        public void SetScript(uint localID, UUID itemID, InstanceData id)
        {
            lock (scriptLock)
            {
                // Create object if it doesn't exist
                if (Scripts.ContainsKey(localID) == false)
                {
                    Scripts.Add(localID, new Dictionary<UUID, InstanceData>());
                }

                // Delete script if it exists
                Dictionary<UUID, InstanceData> Obj;
                Scripts.TryGetValue(localID, out Obj);
                if (Obj.ContainsKey(itemID) == true)
                    Obj.Remove(itemID);

                // Add to object
                Obj.Add(itemID, id);
            }
        }

        public void RemoveScript(uint localID, UUID itemID)
        {
            if (localID == 0)
                localID = GetLocalID(itemID);

            // Don't have that object?
            if (Scripts.ContainsKey(localID) == false)
                return;

            // Delete script if it exists
            Dictionary<UUID, InstanceData> Obj;
            Scripts.TryGetValue(localID, out Obj);
            if (Obj.ContainsKey(itemID) == true)
                Obj.Remove(itemID);
        }

        #endregion

        public void ResetScript(uint localID, UUID itemID)
        {
            InstanceData id = GetScript(localID, itemID);
            string script = id.Source;
            StopScript(localID, itemID);
            SceneObjectPart part = World.GetSceneObjectPart(localID);
            part.Inventory.GetInventoryItem(itemID).PermsMask = 0;
            part.Inventory.GetInventoryItem(itemID).PermsGranter = UUID.Zero;
            StartScript(localID, itemID, script, id.StartParam, false);
        }

        #region Script serialization/deserialization

        public void GetSerializedScript(uint localID, UUID itemID)
        {
            // Serialize the script and return it
            // Should not be a problem
            FileStream fs = File.Create("SERIALIZED_SCRIPT_" + itemID);
            BinaryFormatter b = new BinaryFormatter();
            b.Serialize(fs, GetScript(localID, itemID));
            fs.Close();
        }

        public void PutSerializedScript(uint localID, UUID itemID)
        {
            // Deserialize the script and inject it into an AppDomain

            // How to inject into an AppDomain?
        }

        #endregion

        public DetectParams[] GetDetectParams(InstanceData id)
        {
            if (detparms.ContainsKey(id))
                return detparms[id];

            return null;
        }

        public int GetStartParameter(UUID itemID)
        {
            uint localID = GetLocalID(itemID);
            InstanceData id = GetScript(localID, itemID);

            if (id == null)
                return 0;

            return id.StartParam;
        }

        public IScriptApi GetApi(UUID itemID, string name)
        {
            uint localID = GetLocalID(itemID);

            InstanceData id = GetScript(localID, itemID);
            if (id == null)
                return null;

            if (id.Apis.ContainsKey(name))
                return id.Apis[name];

            return null;
        }
    }
}
