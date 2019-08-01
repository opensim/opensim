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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Security.Policy;
using System.Text;
using System.Threading;
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

using System.Diagnostics; //for [DebuggerNonUserCode]

namespace OpenSim.Region.ScriptEngine.Shared.Instance
{
    public class ScriptInstance : MarshalByRefObject, IScriptInstance
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public bool StatePersistedHere { get { return m_AttachedAvatar == UUID.Zero; } }

        /// <summary>
        /// The current work item if an event for this script is running or waiting to run,
        /// </summary>
        /// <remarks>
        /// Null if there is no running or waiting to run event.  Must be changed only under an EventQueue lock.
        /// </remarks>
        private IScriptWorkItem m_CurrentWorkItem;

        private IScript m_Script;
        private DetectParams[] m_DetectParams;
        private bool m_TimerQueued;
        private DateTime m_EventStart;
        private bool m_InEvent;
        private string m_assemblyPath;
        private string m_dataPath;
        private string m_CurrentEvent = String.Empty;
        private bool m_InSelfDelete;
        private int m_MaxScriptQueue;
        private bool m_SaveState;
        private int m_ControlEventsInQueue;
        private int m_LastControlLevel;
        private bool m_CollisionInQueue;
        private bool m_StateChangeInProgress;

        // The following is for setting a minimum delay between events
        private double m_minEventDelay;

        private long m_eventDelayTicks;
        private long m_nextEventTimeTicks;
        private bool m_startOnInit = true;
        private UUID m_AttachedAvatar;
        private StateSource m_stateSource;
        private bool m_postOnRez;
        private bool m_startedFromSavedState;
        private UUID m_CurrentStateHash;
        private UUID m_RegionID;

        public int DebugLevel { get; set; }

        public WaitHandle CoopWaitHandle { get; private set; }
        public Stopwatch ExecutionTimer { get; private set; }

        public Dictionary<KeyValuePair<int, int>, KeyValuePair<int, int>> LineMap { get; set; }

        private Dictionary<string,IScriptApi> m_Apis = new Dictionary<string,IScriptApi>();

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
            get { return m_running; }

            set
            {
                m_running = value;
                if (m_running)
                    StayStopped = false;
            }
        }
        private bool m_running;

        public bool Suspended
        {
            get { return m_Suspended; }

            set
            {
                // Need to do this inside a lock in order to avoid races with EventProcessor()
                lock (m_Script)
                {
                    bool wasSuspended = m_Suspended;
                    m_Suspended = value;

                    if (wasSuspended && !m_Suspended)
                    {
                        lock (EventQueue)
                        {
                            // Need to place ourselves back in a work item if there are events to process
                            if (EventQueue.Count > 0 && Running && !ShuttingDown)
                                m_CurrentWorkItem = Engine.QueueEventHandler(this);
                        }
                    }
                }
            }
        }
        private bool m_Suspended;

        public bool ShuttingDown { get; set; }

        public string State { get; set; }

        public bool StayStopped { get; set; }

        public IScriptEngine Engine { get; private set; }

        public UUID AppDomain { get; set; }

        public SceneObjectPart Part { get; private set; }

        public string PrimName { get; private set; }

        public string ScriptName { get; private set; }

        public UUID ItemID { get; private set; }

        public UUID ObjectID { get { return Part.UUID; } }

        public uint LocalID { get { return Part.LocalId; } }

        public UUID RootObjectID { get { return Part.ParentGroup.UUID; } }

        public uint RootLocalID { get { return Part.ParentGroup.LocalId; } }

        public UUID AssetID { get; private set; }

        public Queue EventQueue { get; private set; }

        public long EventsQueued
        {
            get
            {
                lock (EventQueue)
                    return EventQueue.Count;
            }
        }

        public long EventsProcessed { get; private set; }

        public int StartParam { get; set; }

        public TaskInventoryItem ScriptTask { get; private set; }

        public DateTime TimeStarted { get; private set; }

        public MetricsCollectorTime ExecutionTime { get; private set; }

        private static readonly int MeasurementWindow = 30 * 1000;   // show the *recent* time used by the script, to find currently active scripts

        private bool m_coopTermination;

        private EventWaitHandle m_coopSleepHandle;

        public void ClearQueue()
        {
            m_TimerQueued = false;
            m_StateChangeInProgress = false;
            EventQueue.Clear();
        }

        public ScriptInstance(
            IScriptEngine engine, SceneObjectPart part, TaskInventoryItem item,
            int startParam, bool postOnRez,
            int maxScriptQueue)
        {
            State = "default";
            EventQueue = new Queue(32);
            ExecutionTimer = new Stopwatch();

            Engine = engine;
            Part = part;
            ScriptTask = item;

            // This is currently only here to allow regression tests to get away without specifying any inventory
            // item when they are testing script logic that doesn't require an item.
            if (ScriptTask != null)
            {
                ScriptName = ScriptTask.Name;
                ItemID = ScriptTask.ItemID;
                AssetID = ScriptTask.AssetID;
            }

            PrimName = part.ParentGroup.Name;
            StartParam = startParam;
            m_MaxScriptQueue = maxScriptQueue;
            m_postOnRez = postOnRez;
            m_AttachedAvatar = part.ParentGroup.AttachedAvatar;
            m_RegionID = part.ParentGroup.Scene.RegionInfo.RegionID;

            m_SaveState = StatePersistedHere;

            ExecutionTime = new MetricsCollectorTime(MeasurementWindow, 10);

//            m_log.DebugFormat(
//                "[SCRIPT INSTANCE]: Instantiated script instance {0} (id {1}) in part {2} (id {3}) in object {4} attached avatar {5} in {6}",
//                ScriptTask.Name, ScriptTask.ItemID, Part.Name, Part.UUID, Part.ParentGroup.Name, m_AttachedAvatar, Engine.World.Name);
        }

        /// <summary>
        /// Load the script from an assembly into an AppDomain.
        /// </summary>
        /// <param name='dom'></param>
        /// <param name='assembly'></param>
        /// <param name='dataPath'>
        /// Path for all script associated data (state, etc.).  In a multi-region set up
        /// with all scripts loading into the same AppDomain this may not be the same place as the DLL itself.
        /// </param>
        /// <param name='stateSource'></param>
        /// <returns>false if load failed, true if suceeded</returns>
        public bool Load(
            IScript script, EventWaitHandle coopSleepHandle, string assemblyPath,
            string dataPath, StateSource stateSource, bool coopTermination)
        {
            m_Script = script;
            m_coopSleepHandle = coopSleepHandle;
            m_assemblyPath = assemblyPath;
            m_dataPath = dataPath;
            m_stateSource = stateSource;
            m_coopTermination = coopTermination;

            if (m_coopTermination)
                CoopWaitHandle = coopSleepHandle;
            else
                CoopWaitHandle = null;

            ApiManager am = new ApiManager();

            foreach (string api in am.GetApis())
            {
                m_Apis[api] = am.CreateApi(api);
                m_Apis[api].Initialize(Engine, Part, ScriptTask);
            }

            try
            {
                foreach (KeyValuePair<string,IScriptApi> kv in m_Apis)
                {
                    m_Script.InitApi(kv.Key, kv.Value);
                }

                //                // m_log.Debug("[Script] Script instance created");

                Part.SetScriptEvents(ItemID, (int)m_Script.GetStateEventFlags(State));
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[SCRIPT INSTANCE]: Not starting script {0} (id {1}) in part {2} (id {3}) in object {4} in {5}.  Error initializing script instance.  Exception {6}{7}",
                    ScriptTask.Name, ScriptTask.ItemID, Part.Name, Part.UUID, Part.ParentGroup.Name, Engine.World.Name, e.Message, e.StackTrace);

                return false;
            }

            // For attachments, XEngine saves the state into a .state file when XEngine.SetXMLState() is called.
            string savedState = Path.Combine(m_dataPath, ItemID.ToString() + ".state");

            if (File.Exists(savedState))
            {
                //                m_log.DebugFormat(
                //                    "[SCRIPT INSTANCE]: Found state for script {0} for {1} ({2}) at {3} in {4}",
                //                    ItemID, savedState, Part.Name, Part.ParentGroup.Name, Part.ParentGroup.Scene.Name);

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
                            Byte[] data = new Byte[size];
                            fs.Read(data, 0, size);

                            xml = Encoding.UTF8.GetString(data);

                            ScriptSerializer.Deserialize(xml, this);

                            AsyncCommandManager.CreateFromData(Engine,
                                                               LocalID, ItemID, ObjectID,
                                                               PluginData);

                            //                            m_log.DebugFormat("[Script] Successfully retrieved state for script {0}.{1}", PrimName, m_ScriptName);

                            Part.SetScriptEvents(ItemID,
                                                 (int)m_Script.GetStateEventFlags(State));

                            if (!Running)
                                m_startOnInit = false;

                            Running = false;

                            // we get new rez events on sim restart, too
                            // but if there is state, then we fire the change
                            // event

                            // We loaded state, don't force a re-save
                            m_SaveState = false;
                            m_startedFromSavedState = true;
                        }

                        // If this script is in an attachment then we no longer need the state file.
                        if (!StatePersistedHere)
                            RemoveState();
                    }
                    //                    else
                    //                    {
                    //                        m_log.WarnFormat(
                    //                            "[SCRIPT INSTANCE]: Not starting script {0} (id {1}) in part {2} (id {3}) in object {4} in {5}.  Unable to load script state file {6}.  Memory limit exceeded.",
                    //                            ScriptTask.Name, ScriptTask.ItemID, Part.Name, Part.UUID, Part.ParentGroup.Name, Engine.World.Name, savedState);
                    //                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[SCRIPT INSTANCE]: Not starting script {0} (id {1}) in part {2} (id {3}) in object {4} in {5}.  Unable to load script state file {6}.  XML is {7}.  Exception {8}{9}",
                        ScriptTask.Name, ScriptTask.ItemID, Part.Name, Part.UUID, Part.ParentGroup.Name, Engine.World.Name, savedState, xml, e.Message, e.StackTrace);
                }
            }
            //            else
            //            {
            //                m_log.DebugFormat(
            //                    "[SCRIPT INSTANCE]: Did not find state for script {0} for {1} ({2}) at {3} in {4}",
            //                    ItemID, savedState, Part.Name, Part.ParentGroup.Name, Part.ParentGroup.Scene.Name);
            //            }

            return true;
        }

        public void Init()
        {
            if (ShuttingDown)
                return;

            if (m_startedFromSavedState)
            {
                if (m_startOnInit)
                    Start();
                if (m_postOnRez)
                {
                    PostEvent(new EventParams("on_rez",
                        new Object[] {new LSL_Types.LSLInteger(StartParam)}, new DetectParams[0]));
                }
                if (m_stateSource == StateSource.AttachedRez)
                {
                    PostEvent(new EventParams("attach",
                        new object[] { new LSL_Types.LSLString(m_AttachedAvatar.ToString()) }, new DetectParams[0]));
                }
                else if (m_stateSource == StateSource.RegionStart)
                {
                    //m_log.Debug("[Script] Posted changed(CHANGED_REGION_RESTART) to script");
                    PostEvent(new EventParams("changed",
                        new Object[] { new LSL_Types.LSLInteger((int)Changed.REGION_RESTART) }, new DetectParams[0]));
                }
                else if (m_stateSource == StateSource.PrimCrossing || m_stateSource == StateSource.Teleporting)
                {
                    // CHANGED_REGION
                    PostEvent(new EventParams("changed",
                        new Object[] { new LSL_Types.LSLInteger((int)Changed.REGION) }, new DetectParams[0]));

                    // CHANGED_TELEPORT
                    if (m_stateSource == StateSource.Teleporting)
                        PostEvent(new EventParams("changed",
                            new Object[] { new LSL_Types.LSLInteger((int)Changed.TELEPORT) }, new DetectParams[0]));
                }
            }
            else
            {
                if (m_startOnInit)
                    Start();
                PostEvent(new EventParams("state_entry",
                                          new Object[0], new DetectParams[0]));
                if (m_postOnRez)
                {
                    PostEvent(new EventParams("on_rez",
                        new Object[] {new LSL_Types.LSLInteger(StartParam)}, new DetectParams[0]));
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
            SceneObjectPart part = Engine.World.GetSceneObjectPart(LocalID);

            if (part != null)
            {
                int permsMask;
                UUID permsGranter;
                part.TaskInventory.LockItemsForRead(true);
                if (!part.TaskInventory.ContainsKey(ItemID))
                {
                    part.TaskInventory.LockItemsForRead(false);
                    return;
                }
                permsGranter = part.TaskInventory[ItemID].PermsGranter;
                permsMask = part.TaskInventory[ItemID].PermsMask;
                part.TaskInventory.LockItemsForRead(false);

                if ((permsMask & ScriptBaseClass.PERMISSION_TAKE_CONTROLS) != 0)
                {
                    ScenePresence presence = Engine.World.GetScenePresence(permsGranter);
                    if (presence != null)
                        presence.UnRegisterControlEventsToScript(LocalID, ItemID);
                }
            }
        }

        public void DestroyScriptInstance()
        {
            ReleaseControls();
            AsyncCommandManager.RemoveScript(Engine, LocalID, ItemID);
            SceneObjectPart part = Engine.World.GetSceneObjectPart(LocalID);
            if (part != null)
                part.RemoveScriptEvents(ItemID);
        }

        public void RemoveState()
        {
            string savedState = Path.Combine(m_dataPath, ItemID.ToString() + ".state");

//            m_log.DebugFormat(
//                "[SCRIPT INSTANCE]: Deleting state {0} for script {1} (id {2}) in part {3} (id {4}) in object {5} in {6}.",
//                savedState, ScriptTask.Name, ScriptTask.ItemID, Part.Name, Part.UUID, Part.ParentGroup.Name, Engine.World.Name);

            try
            {
                File.Delete(savedState);
            }
            catch (Exception e)
            {
                m_log.Warn(
                    string.Format(
                        "[SCRIPT INSTANCE]: Could not delete script state {0} for script {1} (id {2}) in part {3} (id {4}) in object {5} in {6}.  Exception  ",
                        savedState, ScriptTask.Name, ScriptTask.ItemID, Part.Name, Part.UUID, Part.ParentGroup.Name, Engine.World.Name),
                    e);
            }
        }

        public void VarDump(Dictionary<string, object> vars)
        {
            // m_log.Info("Variable dump for script "+ ItemID.ToString());
            // foreach (KeyValuePair<string, object> v in vars)
            // {
                // m_log.Info("Variable: "+v.Key+" = "+v.Value.ToString());
            // }
        }

        public void Start()
        {
            lock (EventQueue)
            {
                if (Running)
                    return;

                Running = true;

                TimeStarted = DateTime.Now;

                // Note: we don't reset ExecutionTime. The reason is that runaway scripts are stopped and restarted
                // automatically, and we *do* want to show that they had high CPU in that case. If we had reset
                // ExecutionTime here then runaway scripts, paradoxically, would never show up in the "Top Scripts" dialog.

                if (EventQueue.Count > 0)
                {
                    if (m_CurrentWorkItem == null)
                        m_CurrentWorkItem = Engine.QueueEventHandler(this);
                    // else
                        // m_log.Error("[Script] Tried to start a script that was already queued");
                }
            }
        }

        public bool Stop(int timeout, bool clearEventQueue = false)
        {
            if (DebugLevel >= 1)
                m_log.DebugFormat(
                    "[SCRIPT INSTANCE]: Stopping script {0} {1} in {2} {3} with timeout {4} {5} {6}",
                    ScriptName, ItemID, PrimName, ObjectID, timeout, m_InSelfDelete, DateTime.Now.Ticks);

            IScriptWorkItem workItem;

            lock (EventQueue)
            {
                if (clearEventQueue)
                    ClearQueue();

                if (!Running)
                    return true;

                // If we're not running or waiting to run an event then we can safely stop.
                if (m_CurrentWorkItem == null)
                {
                    Running = false;
                    return true;
                }

                // If we are waiting to run an event then we can try to cancel it.
                if (m_CurrentWorkItem.Cancel())
                {
                    m_CurrentWorkItem = null;
                    Running = false;
                    return true;
                }

                workItem = m_CurrentWorkItem;
                Running = false;
            }

            // Wait for the current event to complete.
            if (!m_InSelfDelete)
            {
                if (!m_coopTermination)
                {
                    // If we're not co-operative terminating then try and wait for the event to complete before stopping
                    if (workItem.Wait(timeout))
                        return true;
                }
                else
                {
                    if (DebugLevel >= 1)
                        m_log.DebugFormat(
                            "[SCRIPT INSTANCE]: Co-operatively stopping script {0} {1} in {2} {3}",
                            ScriptName, ItemID, PrimName, ObjectID);

                    // This will terminate the event on next handle check by the script.
                    m_coopSleepHandle.Set();

                    // For now, we will wait forever since the event should always cleanly terminate once LSL loop
                    // checking is implemented.  May want to allow a shorter timeout option later.
                    if (workItem.Wait(Timeout.Infinite))
                    {
                        if (DebugLevel >= 1)
                            m_log.DebugFormat(
                                "[SCRIPT INSTANCE]: Co-operatively stopped script {0} {1} in {2} {3}",
                                ScriptName, ItemID, PrimName, ObjectID);

                        return true;
                    }
                }
            }

            lock (EventQueue)
            {
                workItem = m_CurrentWorkItem;
            }

            if (workItem == null)
                return true;

            // If the event still hasn't stopped and we the stop isn't the result of script or object removal, then
            // forcibly abort the work item (this aborts the underlying thread).
            // Co-operative termination should never reach this point.
            if (!m_InSelfDelete)
            {
                m_log.DebugFormat(
                    "[SCRIPT INSTANCE]: Aborting unstopped script {0} {1} in prim {2}, localID {3}, timeout was {4} ms",
                    ScriptName, ItemID, PrimName, LocalID, timeout);

                workItem.Abort();
            }

            lock (EventQueue)
            {
                m_CurrentWorkItem = null;
            }

            return true;
        }

        [DebuggerNonUserCode] //Prevents the debugger from farting in this function
        public void SetState(string state)
        {
            if (state == State)
                return;

            EventParams lastTimerEv = null;

            lock (EventQueue)
            {
                // Remove all queued events, remembering the last timer event
                while (EventQueue.Count > 0)
                {
                    EventParams tempv = (EventParams)EventQueue.Dequeue();
                    if (tempv.EventName == "timer") lastTimerEv = tempv;
                }

                // Post events
                PostEvent(new EventParams("state_exit", new Object[0],
                                           new DetectParams[0]));
                PostEvent(new EventParams("state", new Object[] { state },
                                           new DetectParams[0]));
                PostEvent(new EventParams("state_entry", new Object[0],
                                           new DetectParams[0]));

                // Requeue the timer event after the state changing events
                if (lastTimerEv != null) EventQueue.Enqueue(lastTimerEv);

                // This will stop events from being queued and processed
                // until the new state is started
                m_StateChangeInProgress = true;
            }

            throw new EventAbortException();
        }

        /// <summary>
        /// Post an event to this script instance.
        /// </summary>
        /// <remarks>
        /// The request to run the event is sent
        /// </remarks>
        /// <param name="data"></param>
        public void PostEvent(EventParams data)
        {
//            m_log.DebugFormat("[Script] Posted event {2} in state {3} to {0}.{1}",
//                        PrimName, ScriptName, data.EventName, State);

            if (!Running)
                return;

            // If min event delay is set then ignore any events untill the time has expired
            // This currently only allows 1 event of any type in the given time period.
            // This may need extending to allow for a time for each individual event type.
            if (m_eventDelayTicks != 0 && 
                    data.EventName != "state" && data.EventName != "state_entry" && data.EventName != "state_exit"
                    && data.EventName != "run_time_permissions" && data.EventName != "http_request" && data.EventName != "link_message")
            {
                if (DateTime.Now.Ticks < m_nextEventTimeTicks)
                    return;
                m_nextEventTimeTicks = DateTime.Now.Ticks + m_eventDelayTicks;
            }

            lock (EventQueue)
            {
                // The only events that persist across state changes are timers
                if (m_StateChangeInProgress && data.EventName != "timer")
                    return;

                if (EventQueue.Count >= m_MaxScriptQueue)
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

                EventQueue.Enqueue(data);

                if (m_CurrentWorkItem == null)
                {
                    m_CurrentWorkItem = Engine.QueueEventHandler(this);
                }
            }
        }

        /// <summary>
        /// Process the next event queued for this script
        /// </summary>
        /// <returns></returns>
        public object EventProcessor()
        {
            // We check here as the thread stopping this instance from running may itself hold the m_Script lock.
            if (!Running)
                return 0;

            lock (m_Script)
            {
//                m_log.DebugFormat("[XEngine]: EventProcessor() invoked for {0}.{1}", PrimName, ScriptName);

                if (Suspended)
                    return 0;

                ExecutionTimer.Restart();

                try
                {
                    return EventProcessorInt();
                }
                finally
                {
                    ExecutionTimer.Stop();
                    ExecutionTime.AddSample(ExecutionTimer);
                    Part.ParentGroup.Scene.AddScriptExecutionTime(ExecutionTimer.ElapsedTicks);
                }
            }
        }

        private object EventProcessorInt()
        {
            EventParams data = null;

            lock (EventQueue)
            {
                data = (EventParams)EventQueue.Dequeue();
                if (data == null)
                {
                    // check if a null event was enqueued or if its really empty
                    if (EventQueue.Count > 0 && Running && !ShuttingDown && !m_InSelfDelete)
                    {
                        m_CurrentWorkItem = Engine.QueueEventHandler(this);
                    }
                    else
                    {
                        m_CurrentWorkItem = null;
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

            if (DebugLevel >= 2)
                m_log.DebugFormat(
                    "[SCRIPT INSTANCE]: Processing event {0} for {1}/{2}({3})/{4}({5}) @ {6}/{7}",
                    data.EventName,
                    ScriptName,
                    Part.Name,
                    Part.LocalId,
                    Part.ParentGroup.Name,
                    Part.ParentGroup.UUID,
                    Part.AbsolutePosition,
                    Part.ParentGroup.Scene.Name);

            m_DetectParams = data.DetectParams;

            if (data.EventName == "state") // Hardcoded state change
            {
                State = data.Params[0].ToString();

                if (DebugLevel >= 1)
                    m_log.DebugFormat(
                        "[SCRIPT INSTANCE]: Changing state to {0} for {1}/{2}({3})/{4}({5}) @ {6}/{7}",
                        State,
                        ScriptName,
                        Part.Name,
                        Part.LocalId,
                        Part.ParentGroup.Name,
                        Part.ParentGroup.UUID,
                        Part.AbsolutePosition,
                        Part.ParentGroup.Scene.Name);
                AsyncCommandManager.StateChange(Engine,
                    LocalID, ItemID);
                // we are effectively in the new state now, so we can resume queueing
                // and processing other non-timer events
                m_StateChangeInProgress = false;

                Part.SetScriptEvents(ItemID, (int)m_Script.GetStateEventFlags(State));
            }
            else
            {
                Exception e = null;

                if (Engine.World.PipeEventsForScript(LocalID) ||
                    data.EventName == "control") // Don't freeze avies!
                {
                    //                        m_log.DebugFormat("[Script] Delivered event {2} in state {3} to {0}.{1}",
                    //                                PrimName, ScriptName, data.EventName, State);

                    try
                    {
                        m_CurrentEvent = data.EventName;
                        m_EventStart = DateTime.UtcNow;
                        m_InEvent = true;

                        try
                        {
                            m_Script.ExecuteEvent(State, data.EventName, data.Params);
                        }
                        finally
                        {
                            m_InEvent = false;
                            m_CurrentEvent = String.Empty;
                            lock (EventQueue)
                                m_CurrentWorkItem = null; // no longer in a event that can be canceled
                        }

                        if (m_SaveState)
                        {
                            // This will be the very first event we deliver
                            // (state_entry) in default state
                            //
                            SaveState();

                            m_SaveState = false;
                        }
                    }
                    catch (Exception exx)
                    {
                        e = exx;
                    }

                    if(e != null)
                    {
                        //                            m_log.DebugFormat(
                        //                                "[SCRIPT] Exception in script {0} {1}: {2}{3}",
                        //                                ScriptName, ItemID, e.Message, e.StackTrace);

                        if ((!(e is TargetInvocationException)
                            || (!(e.InnerException is SelfDeleteException)
                                && !(e.InnerException is ScriptDeleteException)
                                && !(e.InnerException is ScriptCoopStopException)))
                            && !(e is ThreadAbortException))
                        {
                            try
                            {

                                if(e.InnerException != null && e.InnerException is ScriptException)
                                {
                                    string text = e.InnerException.Message +
                                                "(script: " + ScriptName +
                                                " event: " + data.EventName +
                                                " primID:" + Part.UUID.ToString() +
                                                " at " + Part.AbsolutePosition + ")";
                                    if (text.Length > 1000)
                                        text = text.Substring(0, 1000);
                                    Engine.World.SimChat(Utils.StringToBytes(text),
                                                           ChatTypeEnum.DebugChannel, 2147483647,
                                                           Part.AbsolutePosition,
                                                           Part.Name, Part.UUID, false);
                                    m_log.Debug(string.Format(
                                        "[SCRIPT ERROR]: {0} (at event {1}, part {2} {3} at {4} in {5}",
                                        e.InnerException.Message,
                                        data.EventName,
                                        PrimName,
                                        Part.UUID,
                                        Part.AbsolutePosition,
                                        Part.ParentGroup.Scene.Name));

                                }
                                else
                                {

                                    // DISPLAY ERROR INWORLD
                                    string text = FormatException(e);

                                    if (text.Length > 1000)
                                        text = text.Substring(0, 1000);
                                    Engine.World.SimChat(Utils.StringToBytes(text),
                                                           ChatTypeEnum.DebugChannel, 2147483647,
                                                           Part.AbsolutePosition,
                                                           Part.Name, Part.UUID, false);


                                    m_log.Debug(string.Format(
                                        "[SCRIPT ERROR]: Runtime error in script {0} (event {1}), part {2} {3} at {4} in {5} ",
                                        ScriptName,
                                        data.EventName,
                                        PrimName,
                                        Part.UUID,
                                        Part.AbsolutePosition,
                                        Part.ParentGroup.Scene.Name),
                                        e);
                                }
                            }
                            catch (Exception)
                            {
                            }
                        }
                        else if ((e is TargetInvocationException) && (e.InnerException is SelfDeleteException))
                        {
                            m_InSelfDelete = true;
                            Engine.World.DeleteSceneObject(Part.ParentGroup, false);
                        }
                        else if ((e is TargetInvocationException) && (e.InnerException is ScriptDeleteException))
                        {
                            m_InSelfDelete = true;
                            Part.Inventory.RemoveInventoryItem(ItemID);
                        }
                        else if ((e is TargetInvocationException) && (e.InnerException is ScriptCoopStopException))
                        {
                            if (DebugLevel >= 1)
                                m_log.DebugFormat(
                                    "[SCRIPT INSTANCE]: Script {0}.{1} in event {2}, state {3} stopped co-operatively.",
                                    PrimName, ScriptName, data.EventName, State);
                        }
                    }
                }
            }

            // If there are more events and we are currently running and not shutting down, then ask the
            // script engine to run the next event.
            lock (EventQueue)
            {
                // Increase processed events counter and prevent wrap;
                if (++EventsProcessed == 1000000)
                    EventsProcessed = 100000;

                if ((EventsProcessed % 100000) == 0 && DebugLevel > 0)
                {
                    m_log.DebugFormat("[SCRIPT INSTANCE]: Script \"{0}\" (Object \"{1}\" {2} @ {3}.{4}, Item ID {5}, Asset {6}) in event {7}: processed {8:n0} script events",
                                    ScriptTask.Name,
                                    Part.ParentGroup.Name, Part.ParentGroup.UUID, Part.ParentGroup.AbsolutePosition, Part.ParentGroup.Scene.Name,
                                    ScriptTask.ItemID, ScriptTask.AssetID, data.EventName, EventsProcessed);
                }

                if (EventQueue.Count > 0 && Running && !ShuttingDown && !m_InSelfDelete)
                {
                    m_CurrentWorkItem = Engine.QueueEventHandler(this);
                }
                else
                {
                    m_CurrentWorkItem = null;
                }
            }

            m_DetectParams = null;

            return 0;
        }

        public int EventTime()
        {
            if (!m_InEvent)
                return 0;

            return (DateTime.UtcNow - m_EventStart).Seconds;
        }

        public void ResetScript(int timeout)
        {
            if (m_Script == null)
                return;

            bool running = Running;

            RemoveState();
            ReleaseControls();

            Stop(timeout);
            SceneObjectPart part = Engine.World.GetSceneObjectPart(LocalID);
            part.Inventory.GetInventoryItem(ItemID).PermsMask = 0;
            part.Inventory.GetInventoryItem(ItemID).PermsGranter = UUID.Zero;
            part.CollisionSound = UUID.Zero;
            AsyncCommandManager.RemoveScript(Engine, LocalID, ItemID);

            m_TimerQueued = false;
            m_StateChangeInProgress = false;
            EventQueue.Clear();

            m_Script.ResetVars();
            StartParam = 0;
            State = "default";


            part.SetScriptEvents(ItemID,
                                 (int)m_Script.GetStateEventFlags(State));
            if (running)
                Start();

            m_SaveState = StatePersistedHere;

            PostEvent(new EventParams("state_entry",
                    new Object[0], new DetectParams[0]));
        }

        [DebuggerNonUserCode] //Stops the VS debugger from farting in this function
        public void ApiResetScript()
        {
            // bool running = Running;

            RemoveState();
            ReleaseControls();

            m_Script.ResetVars();
            SceneObjectPart part = Engine.World.GetSceneObjectPart(LocalID);
            part.Inventory.GetInventoryItem(ItemID).PermsMask = 0;
            part.Inventory.GetInventoryItem(ItemID).PermsGranter = UUID.Zero;
            part.CollisionSound = UUID.Zero;
            AsyncCommandManager.RemoveScript(Engine, LocalID, ItemID);

            m_TimerQueued = false;
            m_StateChangeInProgress = false;
            EventQueue.Clear();
            m_Script.ResetVars();
            string oldState = State;
            StartParam = 0;
            State = "default";

            part.SetScriptEvents(ItemID,
                                 (int)m_Script.GetStateEventFlags(State));

            if (m_CurrentEvent != "state_entry" || oldState != "default")
            {
                m_SaveState = StatePersistedHere;
                PostEvent(new EventParams("state_entry",
                        new Object[0], new DetectParams[0]));
                throw new EventAbortException();
            }
        }

        public Dictionary<string, object> GetVars()
        {
            if (m_Script != null)
                return m_Script.GetVars();
            else
                return new Dictionary<string, object>();
        }

        public void SetVars(Dictionary<string, object> vars)
        {
//            foreach (KeyValuePair<string, object> kvp in vars)
//                m_log.DebugFormat("[SCRIPT INSTANCE]: Setting var {0}={1}", kvp.Key, kvp.Value);

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

        public void SaveState()
        {
            if (!Running && !StayStopped)
                return;

            // We cannot call this inside the EventQueue lock since it will currently take AsyncCommandManager.staticLock.
            // This may already be held by AsyncCommandManager.DoOneCmdHandlerPass() which in turn can take EventQueue
            // lock via ScriptInstance.PostEvent().
            PluginData = AsyncCommandManager.GetSerializationData(Engine, ItemID);

            // We need to lock here to avoid any race with a thread that is removing this script.
            lock (EventQueue)
            {
                // Check again to avoid a race with a thread in Stop()
                if (!Running && !StayStopped)
                    return;

                // If we're currently in an event, just tell it to save upon return
                //
                if (m_InEvent)
                {
                    m_SaveState = true;
                    return;
                }

    //            m_log.DebugFormat(
    //                "[SCRIPT INSTANCE]: Saving state for script {0} (id {1}) in part {2} (id {3}) in object {4} in {5}",
    //                ScriptTask.Name, ScriptTask.ItemID, Part.Name, Part.UUID, Part.ParentGroup.Name, Engine.World.Name);

                string xml = ScriptSerializer.Serialize(this);

                // Compare hash of the state we just just created with the state last written to disk
                // If the state is different, update the disk file.
                UUID hash = UUID.Parse(Utils.MD5String(xml));

                if (hash != m_CurrentStateHash)
                {
                    try
                    {
                        using (FileStream fs = File.Create(Path.Combine(m_dataPath, ItemID.ToString() + ".state")))
                        {
                            Byte[] buf = Util.UTF8NoBomEncoding.GetBytes(xml);
                            fs.Write(buf, 0, buf.Length);
                        }
                    }
                    catch(Exception)
                    {
                        // m_log.Error("Unable to save xml\n"+e.ToString());
                    }
                    //if (!File.Exists(Path.Combine(Path.GetDirectoryName(assembly), ItemID.ToString() + ".state")))
                    //{
                    //    throw new Exception("Completed persistence save, but no file was created");
                    //}
                    m_CurrentStateHash = hash;
                }

                StayStopped = false;
            }
        }

        public IScriptApi GetApi(string name)
        {
            if (m_Apis.ContainsKey(name))
            {
//                m_log.DebugFormat("[SCRIPT INSTANCE]: Found api {0} in {1}@{2}", name, ScriptName, PrimName);

                return m_Apis[name];
            }

//            m_log.DebugFormat("[SCRIPT INSTANCE]: Did not find api {0} in {1}@{2}", name, ScriptName, PrimName);

            return null;
        }

        public override string ToString()
        {
            return String.Format("{0} {1} on {2}", ScriptName, ItemID, PrimName);
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
                                    "({0}): {1}", scriptLine - 1,
                                    e.InnerException.Message);

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
            return m_assemblyPath;
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
            PluginData = AsyncCommandManager.GetSerializationData(Engine, ItemID);

            return ScriptSerializer.Serialize(this);
        }

        public UUID RegionID
        {
            get { return m_RegionID; }
        }

        public void Suspend()
        {
            Suspended = true;
        }

        public void Resume()
        {
            Suspended = false;
        }
    }

    /// <summary>
    /// Xengine event wait handle.
    /// </summary>
    /// <remarks>
    /// This class exists becase XEngineScriptBase gets a reference to this wait handle.  We need to make sure that
    /// when scripts are running in different AppDomains the lease does not expire.
    /// FIXME: Like LSL_Api, etc., this effectively leaks memory since the GC will never collect it.  To avoid this,
    /// proper remoting sponsorship needs to be implemented across the board.
    /// </remarks>
    public class XEngineEventWaitHandle : EventWaitHandle
    {
        public XEngineEventWaitHandle(bool initialState, EventResetMode mode) : base(initialState, mode) {}

        public override Object InitializeLifetimeService()
        {
            return null;
        }
    }
}
