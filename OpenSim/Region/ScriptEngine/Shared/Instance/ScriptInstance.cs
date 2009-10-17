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
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
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
using OpenSim.Region.CoreModules;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.Api.Runtime;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.Shared.CodeTools;
using OpenSim.Region.ScriptEngine.Interfaces;

namespace OpenSim.Region.ScriptEngine.Shared.Instance
{
    public class ScriptInstance : MarshalByRefObject, IScriptInstance
    {
        // private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        private IScriptEngine m_Engine;
        private IScriptWorkItem m_CurrentResult = null;
        private Queue m_EventQueue = new Queue(32);
        private bool m_RunEvents = false;
        private UUID m_ItemID;
        private uint m_LocalID;
        private UUID m_ObjectID;
        private UUID m_AssetID;
        private IScript m_Script;
        private UUID m_AppDomain;
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
        private int m_MaxScriptQueue;
        private bool m_SaveState = true;
        private bool m_ShuttingDown = false;
        private int m_ControlEventsInQueue = 0;
        private int m_LastControlLevel = 0;
        private bool m_CollisionInQueue = false;
        private TaskInventoryItem m_thisScriptTask;
        // The following is for setting a minimum delay between events
        private double m_minEventDelay = 0;
        private long m_eventDelayTicks = 0;
        private long m_nextEventTimeTicks = 0;
        private bool m_startOnInit = true;
        private UUID m_AttachedAvatar = UUID.Zero;
        private StateSource m_stateSource;
        private bool m_postOnRez;
        private bool m_startedFromSavedState = false;
        private string m_CurrentState = String.Empty;
        private UUID m_RegionID = UUID.Zero;

        private Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>>
                m_LineMap;

        public Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>>
                LineMap
        {
            get { return m_LineMap; }
            set { m_LineMap = value; }
        }

        private Dictionary<string,IScriptApi> m_Apis = new Dictionary<string,IScriptApi>();

        // Script state
        private string m_State="default";

        public Object[] PluginData = new Object[0];

        /// <summary>
        /// Used by llMinEventDelay to suppress events happening any faster than this speed.
        /// This currently restricts all events in one go. Not sure if each event type has
        /// its own check so take the simple route first.
        /// </summary>
        public double MinEventDelay
        {
            get { return m_minEventDelay; }
            set
            {
                if (value > 0.001)
                    m_minEventDelay = value;
                else 
                    m_minEventDelay = 0.0;
                m_eventDelayTicks = (long)(m_minEventDelay * 10000000L);
                m_nextEventTimeTicks = DateTime.Now.Ticks;
            }
        }

        public bool Running
        {
            get { return m_RunEvents; }
            set { m_RunEvents = value; }
        }

        public bool ShuttingDown
        {
            get { return m_ShuttingDown; }
            set { m_ShuttingDown = value; }
        }

        public string State
        {
            get { return m_State; }
            set { m_State = value; }
        }

        public IScriptEngine Engine
        {
            get { return m_Engine; }
        }

        public UUID AppDomain
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

        public UUID ItemID
        {
            get { return m_ItemID; }
        }

        public UUID ObjectID
        {
            get { return m_ObjectID; }
        }

        public uint LocalID
        {
            get { return m_LocalID; }
        }

        public UUID AssetID
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

        public TaskInventoryItem ScriptTask
        {
            get { return m_thisScriptTask; }
        }

        public ScriptInstance(IScriptEngine engine, SceneObjectPart part,
                UUID itemID, UUID assetID, string assembly,
                AppDomain dom, string primName, string scriptName,
                int startParam, bool postOnRez, StateSource stateSource,
                int maxScriptQueue)
        {
            m_Engine = engine;

            m_LocalID = part.LocalId;
            m_ObjectID = part.UUID;
            m_ItemID = itemID;
            m_AssetID = assetID;
            m_PrimName = primName;
            m_ScriptName = scriptName;
            m_Assembly = assembly;
            m_StartParam = startParam;
            m_MaxScriptQueue = maxScriptQueue;
            m_stateSource = stateSource;
            m_postOnRez = postOnRez;
            m_AttachedAvatar = part.AttachedAvatar;
            m_RegionID = part.ParentGroup.Scene.RegionInfo.RegionID;

            if (part != null)
            {
                lock (part.TaskInventory)
                {
                    if (part.TaskInventory.ContainsKey(m_ItemID))
                    {
                        m_thisScriptTask = part.TaskInventory[m_ItemID];
                    }
                }
            }

            ApiManager am = new ApiManager();

            foreach (string api in am.GetApis())
            {
                m_Apis[api] = am.CreateApi(api);
                m_Apis[api].Initialize(engine, part, m_LocalID, itemID);
            }

            try
            {
                m_Script = (IScript)dom.CreateInstanceAndUnwrap(
                    Path.GetFileNameWithoutExtension(assembly),
                    "SecondLife.Script");

                //ILease lease = (ILease)RemotingServices.GetLifetimeService(m_Script as ScriptBaseClass);
                RemotingServices.GetLifetimeService(m_Script as ScriptBaseClass);
//                lease.Register(this);
            }
            catch (Exception)
            {
                // m_log.ErrorFormat("[Script] Error loading assembly {0}\n"+e.ToString(), assembly);
            }

            try
            {
                foreach (KeyValuePair<string,IScriptApi> kv in m_Apis)
                {
                    m_Script.InitApi(kv.Key, kv.Value);
                }

//                // m_log.Debug("[Script] Script instance created");

                part.SetScriptEvents(m_ItemID,
                                     (int)m_Script.GetStateEventFlags(State));
            }
            catch (Exception)
            {
                // m_log.Error("[Script] Error loading script instance\n"+e.ToString());
                return;
            }

            m_SaveState = true;

            string savedState = Path.Combine(Path.GetDirectoryName(assembly),
                    m_ItemID.ToString() + ".state");
            if (File.Exists(savedState))
            {
                string xml = String.Empty;

                try
                {
                    FileInfo fi = new FileInfo(savedState);
                    int size = (int)fi.Length;
                    if (size < 512000)
                    {
                        using (FileStream fs = File.Open(savedState,
                                                         FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            System.Text.UTF8Encoding enc =
                                new System.Text.UTF8Encoding();

                            Byte[] data = new Byte[size];
                            fs.Read(data, 0, size);

                            xml = enc.GetString(data);

                            ScriptSerializer.Deserialize(xml, this);

                            AsyncCommandManager.CreateFromData(m_Engine,
                                m_LocalID, m_ItemID, m_ObjectID,
                                PluginData);

//                            m_log.DebugFormat("[Script] Successfully retrieved state for script {0}.{1}", m_PrimName, m_ScriptName);

                            part.SetScriptEvents(m_ItemID,
                                    (int)m_Script.GetStateEventFlags(State));

                            if (m_RunEvents && (!m_ShuttingDown))
                            {
                                m_RunEvents = false;
                            } 
                            else 
                            {
                                m_RunEvents = false;
                                m_startOnInit = false;
                            }

                            // we get new rez events on sim restart, too
                            // but if there is state, then we fire the change
                            // event

                            // We loaded state, don't force a re-save
                            m_SaveState = false;
                            m_startedFromSavedState = true;

                        }
                    }
                    else
                    {
                        // m_log.Error("[Script] Unable to load script state: Memory limit exceeded");
                    }
                }
                catch (Exception)
                {
                    // m_log.ErrorFormat("[Script] Unable to load script state from xml: {0}\n"+e.ToString(), xml);
                }
            }
//            else
//            {
//                ScenePresence presence = m_Engine.World.GetScenePresence(part.OwnerID);

//                if (presence != null && (!postOnRez))
//                    presence.ControllingClient.SendAgentAlertMessage("Compile successful", false);

//            }
        }

        public void Init()
        {
            if (!m_startOnInit) return;

            if (m_startedFromSavedState) 
            {
                Start();
                if (m_postOnRez) 
                {
                    PostEvent(new EventParams("on_rez",
                        new Object[] {new LSL_Types.LSLInteger(m_StartParam)}, new DetectParams[0]));
                }

                if (m_stateSource == StateSource.AttachedRez)
                {
                    PostEvent(new EventParams("attach",
                        new object[] { new LSL_Types.LSLString(m_AttachedAvatar.ToString()) }, new DetectParams[0]));
                }
                else if (m_stateSource == StateSource.NewRez)
                {
//                    m_log.Debug("[Script] Posted changed(CHANGED_REGION_RESTART) to script");
                    PostEvent(new EventParams("changed",
                                              new Object[] {new LSL_Types.LSLInteger(256)}, new DetectParams[0]));
                }
                else if (m_stateSource == StateSource.PrimCrossing)
                {
                    // CHANGED_REGION
                    PostEvent(new EventParams("changed",
                                              new Object[] {new LSL_Types.LSLInteger(512)}, new DetectParams[0]));
                }
            } 
            else 
            {
                Start();
                PostEvent(new EventParams("state_entry",
                                          new Object[0], new DetectParams[0]));
                if (m_postOnRez) 
                {
                    PostEvent(new EventParams("on_rez",
                        new Object[] {new LSL_Types.LSLInteger(m_StartParam)}, new DetectParams[0]));
                }

                if (m_stateSource == StateSource.AttachedRez)
                {
                    PostEvent(new EventParams("attach",
                        new object[] { new LSL_Types.LSLString(m_AttachedAvatar.ToString()) }, new DetectParams[0]));
                }

            }
        }

        private void ReleaseControls()
        {
            SceneObjectPart part = m_Engine.World.GetSceneObjectPart(m_LocalID);
            
            if (part != null)
            {
                int permsMask;
                UUID permsGranter;
                lock (part.TaskInventory)
                {
                    if (!part.TaskInventory.ContainsKey(m_ItemID))
                        return;

                    permsGranter = part.TaskInventory[m_ItemID].PermsGranter;
                    permsMask = part.TaskInventory[m_ItemID].PermsMask;
                }

                if ((permsMask & ScriptBaseClass.PERMISSION_TAKE_CONTROLS) != 0)
                {
                    ScenePresence presence = m_Engine.World.GetScenePresence(permsGranter);
                    if (presence != null)
                        presence.UnRegisterControlEventsToScript(m_LocalID, m_ItemID);
                }
            }
        }

        public void DestroyScriptInstance()
        {
            ReleaseControls();
            AsyncCommandManager.RemoveScript(m_Engine, m_LocalID, m_ItemID);
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
            // m_log.Info("Variable dump for script "+ m_ItemID.ToString());
            // foreach (KeyValuePair<string, object> v in vars)
            // {
                // m_log.Info("Variable: "+v.Key+" = "+v.Value.ToString());
            // }
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
                    // else
                        // m_log.Error("[Script] Tried to start a script that was already queued");
                }
            }
        }

        public bool Stop(int timeout)
        {
            IScriptWorkItem result;

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

            if (result.Wait(new TimeSpan((long)timeout * 100000)))
            {
                return true;
            }

            lock (m_EventQueue)
            {
                result = m_CurrentResult;
            }

            if (result == null)
                return true;

            if (!m_InSelfDelete)
                result.Abort();

            lock (m_EventQueue)
            {
                m_CurrentResult = null;
            }

            return true;
        }

        public void SetState(string state)
        {
            if (state == State)
                return;

            PostEvent(new EventParams("state_exit", new Object[0],
                                       new DetectParams[0]));
            PostEvent(new EventParams("state", new Object[] { state },
                                       new DetectParams[0]));
            PostEvent(new EventParams("state_entry", new Object[0],
                                       new DetectParams[0]));

            throw new EventAbortException();
        }

        public void PostEvent(EventParams data)
        {
//            m_log.DebugFormat("[Script] Posted event {2} in state {3} to {0}.{1}",
//                        m_PrimName, m_ScriptName, data.EventName, m_State);

            if (!Running)
                return;

            // If min event delay is set then ignore any events untill the time has expired
            // This currently only allows 1 event of any type in the given time period.
            // This may need extending to allow for a time for each individual event type.
            if (m_eventDelayTicks != 0)
            {
                if (DateTime.Now.Ticks < m_nextEventTimeTicks)
                    return;
                m_nextEventTimeTicks = DateTime.Now.Ticks + m_eventDelayTicks;
            }

            lock (m_EventQueue)
            {
                if (m_EventQueue.Count >= m_MaxScriptQueue)
                    return;

                if (data.EventName == "timer")
                {
                    if (m_TimerQueued)
                        return;
                    m_TimerQueued = true;
                }

                if (data.EventName == "control")
                {
                    int held = ((LSL_Types.LSLInteger)data.Params[1]).value;
                    // int changed = ((LSL_Types.LSLInteger)data.Params[2]).value;

                    // If the last message was a 0 (nothing held)
                    // and this one is also nothing held, drop it
                    //
                    if (m_LastControlLevel == held && held == 0)
                        return;

                    // If there is one or more queued, then queue
                    // only changed ones, else queue unconditionally
                    //
                    if (m_ControlEventsInQueue > 0)
                    {
                        if (m_LastControlLevel == held)
                            return;
                    }

                    m_LastControlLevel = held;
                    m_ControlEventsInQueue++;
                }

                if (data.EventName == "collision")
                {
                    if (m_CollisionInQueue)
                        return;
                    if (data.DetectParams == null)
                        return;

                    m_CollisionInQueue = true;
                }

                m_EventQueue.Enqueue(data);

                if (m_CurrentResult == null)
                {
                    m_CurrentResult = m_Engine.QueueEventHandler(this);
                }
            }
        }

        /// <summary>
        /// Process the next event queued for this script
        /// </summary>
        /// <returns></returns>
        public object EventProcessor()
        {
            lock (m_Script)
            {
                EventParams data = null;

                lock (m_EventQueue)
                {
                    data = (EventParams) m_EventQueue.Dequeue();
                    if (data == null) // Shouldn't happen
                    {
                        if ((m_EventQueue.Count > 0) && m_RunEvents && (!m_ShuttingDown))
                        {
                            m_CurrentResult = m_Engine.QueueEventHandler(this);
                        }
                        else
                        {
                            m_CurrentResult = null;
                        }
                        return 0;
                    }

                    if (data.EventName == "timer")
                        m_TimerQueued = false;
                    if (data.EventName == "control")
                    {
                        if (m_ControlEventsInQueue > 0)
                            m_ControlEventsInQueue--;
                    }
                    if (data.EventName == "collision")
                        m_CollisionInQueue = false;
                }
                
                //m_log.DebugFormat("[XENGINE]: Processing event {0} for {1}", data.EventName, this);

                m_DetectParams = data.DetectParams;

                if (data.EventName == "state") // Hardcoded state change
                {
    //                m_log.DebugFormat("[Script] Script {0}.{1} state set to {2}",
    //                        m_PrimName, m_ScriptName, data.Params[0].ToString());
                    m_State=data.Params[0].ToString();
                    AsyncCommandManager.RemoveScript(m_Engine,
                        m_LocalID, m_ItemID);

                    SceneObjectPart part = m_Engine.World.GetSceneObjectPart(
                        m_LocalID);
                    if (part != null)
                    {
                        part.SetScriptEvents(m_ItemID,
                                             (int)m_Script.GetStateEventFlags(State));
                    }
                }
                else
                {
                    if (m_Engine.World.PipeEventsForScript(m_LocalID) ||
                        data.EventName == "control") // Don't freeze avies!
                    {
                        SceneObjectPart part = m_Engine.World.GetSceneObjectPart(
                            m_LocalID);
        //                m_log.DebugFormat("[Script] Delivered event {2} in state {3} to {0}.{1}",
        //                        m_PrimName, m_ScriptName, data.EventName, m_State);

                        try
                        {
                            m_CurrentEvent = data.EventName;
                            m_EventStart = DateTime.Now;
                            m_InEvent = true;

                            m_Script.ExecuteEvent(State, data.EventName, data.Params);

                            m_InEvent = false;
                            m_CurrentEvent = String.Empty;

                            if (m_SaveState)
                            {
                                // This will be the very first event we deliver
                                // (state_entry) in default state
                                //

                                SaveState(m_Assembly);

                                m_SaveState = false;
                            }
                        }
                        catch (Exception e)
                        {
                            // m_log.DebugFormat("[SCRIPT] Exception: {0}", e.Message);
                            m_InEvent = false;
                            m_CurrentEvent = String.Empty;

                            if ((!(e is TargetInvocationException) || (!(e.InnerException is SelfDeleteException) && !(e.InnerException is ScriptDeleteException))) && !(e is ThreadAbortException))
                            {
                                try
                                {
                                    // DISPLAY ERROR INWORLD
                                    string text = FormatException(e);

                                    if (text.Length > 1000)
                                        text = text.Substring(0, 1000);
                                    m_Engine.World.SimChat(Utils.StringToBytes(text),
                                                           ChatTypeEnum.DebugChannel, 2147483647,
                                                           part.AbsolutePosition,
                                                           part.Name, part.UUID, false);
                                }
                                catch (Exception)
                                {
                                }
                                // catch (Exception e2) // LEGIT: User Scripting
                                // {
                                    // m_log.Error("[SCRIPT]: "+
                                      //           "Error displaying error in-world: " +
                                        //         e2.ToString());
                                 //    m_log.Error("[SCRIPT]: " +
                                   //              "Errormessage: Error compiling script:\r\n" +
                                     //            e.ToString());
                                // }
                            }
                            else if ((e is TargetInvocationException) && (e.InnerException is SelfDeleteException))
                            {
                                m_InSelfDelete = true;
                                if (part != null && part.ParentGroup != null)
                                    m_Engine.World.DeleteSceneObject(part.ParentGroup, false);
                            }
                            else if ((e is TargetInvocationException) && (e.InnerException is ScriptDeleteException))
                            {
                                m_InSelfDelete = true;
                                if (part != null && part.ParentGroup != null)
                                    part.Inventory.RemoveInventoryItem(m_ItemID);
                            }
                        }
                    }
                }

                lock (m_EventQueue)
                {
                    if ((m_EventQueue.Count > 0) && m_RunEvents && (!m_ShuttingDown))
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
        }

        public int EventTime()
        {
            if (!m_InEvent)
                return 0;

            return (DateTime.Now - m_EventStart).Seconds;
        }

        public void ResetScript()
        {
            if (m_Script == null)
                return;

            bool running = Running;

            RemoveState();
            ReleaseControls();

            Stop(0);
            SceneObjectPart part=m_Engine.World.GetSceneObjectPart(m_LocalID);
            part.Inventory.GetInventoryItem(m_ItemID).PermsMask = 0;
            part.Inventory.GetInventoryItem(m_ItemID).PermsGranter = UUID.Zero;
            AsyncCommandManager.RemoveScript(m_Engine, m_LocalID, m_ItemID);
            m_EventQueue.Clear();
            m_Script.ResetVars();
            m_State = "default";

            part.SetScriptEvents(m_ItemID,
                                 (int)m_Script.GetStateEventFlags(State));
            if (running)
                Start();
            m_SaveState = true;
            PostEvent(new EventParams("state_entry",
                    new Object[0], new DetectParams[0]));
        }

        public void ApiResetScript()
        {
            // bool running = Running;

            RemoveState();
            ReleaseControls();

            m_Script.ResetVars();
            SceneObjectPart part=m_Engine.World.GetSceneObjectPart(m_LocalID);
            part.Inventory.GetInventoryItem(m_ItemID).PermsMask = 0;
            part.Inventory.GetInventoryItem(m_ItemID).PermsGranter = UUID.Zero;
            AsyncCommandManager.RemoveScript(m_Engine, m_LocalID, m_ItemID);

            m_EventQueue.Clear();
            m_Script.ResetVars();
            m_State = "default";

            part.SetScriptEvents(m_ItemID,
                                 (int)m_Script.GetStateEventFlags(State));

            if (m_CurrentEvent != "state_entry")
            {
                m_SaveState = true;
                PostEvent(new EventParams("state_entry",
                        new Object[0], new DetectParams[0]));
                throw new EventAbortException();
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
            if (m_DetectParams == null)
                return null;
            if (idx < 0 || idx >= m_DetectParams.Length)
                return null;

            return m_DetectParams[idx];
        }

        public UUID GetDetectID(int idx)
        {
            if (m_DetectParams == null)
                return UUID.Zero;
            if (idx < 0 || idx >= m_DetectParams.Length)
                return UUID.Zero;

            return m_DetectParams[idx].Key;
        }

        public void SaveState(string assembly)
        {


            // If we're currently in an event, just tell it to save upon return
            //
            if (m_InEvent)
            {
                m_SaveState = true;
                return;
            }

            PluginData = AsyncCommandManager.GetSerializationData(m_Engine, m_ItemID);

            string xml = ScriptSerializer.Serialize(this);

            if (m_CurrentState != xml)
            {
                try
                {
                    FileStream fs = File.Create(Path.Combine(Path.GetDirectoryName(assembly), m_ItemID.ToString() + ".state"));
                    System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
                    Byte[] buf = enc.GetBytes(xml);
                    fs.Write(buf, 0, buf.Length);
                    fs.Close();
                }
                catch(Exception)
                {
                    // m_log.Error("Unable to save xml\n"+e.ToString());
                }
                //if (!File.Exists(Path.Combine(Path.GetDirectoryName(assembly), m_ItemID.ToString() + ".state")))
                //{
                //    throw new Exception("Completed persistence save, but no file was created");
                //}
                m_CurrentState = xml;
            }
        }

        public IScriptApi GetApi(string name)
        {
            if (m_Apis.ContainsKey(name))
                return m_Apis[name];
            return null;
        }
        
        public override string ToString()
        {
            return String.Format("{0} {1} on {2}", m_ScriptName, m_ItemID, m_PrimName);
        }

        string FormatException(Exception e)
        {
            if (e.InnerException == null) // Not a normal runtime error
                return e.ToString();

            string message = "Runtime error:\n" + e.InnerException.StackTrace;
            string[] lines = message.Split(new char[] {'\n'});

            foreach (string line in lines)
            {
                if (line.Contains("SecondLife.Script"))
                {
                    int idx = line.IndexOf(':');
                    if (idx != -1)
                    {
                        string val = line.Substring(idx+1);
                        int lineNum = 0;
                        if (int.TryParse(val, out lineNum))
                        {
                            KeyValuePair<int, int> pos =
                                    Compiler.FindErrorPosition(
                                    lineNum, 0, LineMap);

                            int scriptLine = pos.Key;
                            int col = pos.Value;
                            if (scriptLine == 0)
                                scriptLine++;
                            if (col == 0)
                                col++;
                            message = string.Format("Runtime error:\n" +
                                    "Line ({0}): {1}", scriptLine - 1,
                                    e.InnerException.Message);

                            System.Console.WriteLine(e.ToString()+"\n");
                            return message;
                        }
                    }
                }
            }

            // m_log.ErrorFormat("Scripting exception:");
            // m_log.ErrorFormat(e.ToString());

            return e.ToString();
        }

        public string GetAssemblyName()
        {
            return m_Assembly;
        }

        public string GetXMLState()
        {
            bool run = Running;
            Stop(100);
            Running = run;

            // We should not be doing this, but since we are about to
            // dispose this, it really doesn't make a difference
            // This is meant to work around a Windows only race
            //
            m_InEvent = false;

            // Force an update of the in-memory plugin data
            //
            PluginData = AsyncCommandManager.GetSerializationData(m_Engine, m_ItemID);

            return ScriptSerializer.Serialize(this);
        }

        public UUID RegionID
        {
            get { return m_RegionID; }
        }

        public bool CanBeDeleted()
        {
            return true;
        }
    }
}
