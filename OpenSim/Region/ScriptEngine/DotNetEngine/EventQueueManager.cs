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
using System.Reflection;
using OpenMetaverse;
using OpenSim.Region.ScriptEngine.Shared;
using log4net;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    /// <summary>
    /// EventQueueManager handles event queues
    /// Events are queued and executed in separate thread
    /// </summary>
    [Serializable]
    public class EventQueueManager
    {
        //
        // Class is instanced in "ScriptEngine" and used by "EventManager" which is also instanced in "ScriptEngine".
        //
        // Class purpose is to queue and execute functions that are received by "EventManager":
        //   - allowing "EventManager" to release its event thread immediately, thus not interrupting server execution.
        //   - allowing us to prioritize and control execution of script functions.
        // Class can use multiple threads for simultaneous execution. Mutexes are used for thread safety.
        //
        // 1. Hold an execution queue for scripts
        // 2. Use threads to process queue, each thread executes one script function on each pass.
        // 3. Catch any script error and process it
        //
        //
        // Notes:
        // * Current execution load balancing is optimized for 1 thread, and can cause unfair execute balancing between scripts.
        //   Not noticeable unless server is under high load.
        //

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public ScriptEngine m_ScriptEngine;

        /// <summary>
        /// List of threads (classes) processing event queue
        /// Note that this may or may not be a reference to a static object depending on PrivateRegionThreads config setting.
        /// </summary>
        internal static List<EventQueueThreadClass> eventQueueThreads = new List<EventQueueThreadClass>();                             // Thread pool that we work on
        /// <summary>
        /// Locking access to eventQueueThreads AND staticGlobalEventQueueThreads.
        /// </summary>
//        private object eventQueueThreadsLock = new object();
        // Static objects for referencing the objects above if we don't have private threads:
        //internal static List<EventQueueThreadClass> staticEventQueueThreads;                // A static reference used if we don't use private threads
//        internal static object staticEventQueueThreadsLock;                                 // Statick lock object reference for same reason

        /// <summary>
        /// Global static list of all threads (classes) processing event queue -- used by max enforcment thread
        /// </summary>
        //private List<EventQueueThreadClass> staticGlobalEventQueueThreads = new List<EventQueueThreadClass>();

        /// <summary>
        /// Used internally to specify how many threads should exit gracefully
        /// </summary>
        public static int ThreadsToExit;
        public static object ThreadsToExitLock = new object();


        //public object queueLock = new object(); // Mutex lock object

        /// <summary>
        /// How many threads to process queue with
        /// </summary>
        internal static int numberOfThreads;

        internal static int EventExecutionMaxQueueSize;

        /// <summary>
        /// Maximum time one function can use for execution before we perform a thread kill.
        /// </summary>
        private static int maxFunctionExecutionTimems
        {
            get { return (int)(maxFunctionExecutionTimens / 10000); }
            set { maxFunctionExecutionTimens = value * 10000; }
        }

        /// <summary>
        /// Contains nanoseconds version of maxFunctionExecutionTimems so that it matches time calculations better (performance reasons).
        /// WARNING! ONLY UPDATE maxFunctionExecutionTimems, NEVER THIS DIRECTLY.
        /// </summary>
        public static long maxFunctionExecutionTimens;
        
        /// <summary>
        /// Enforce max execution time
        /// </summary>
        public static bool EnforceMaxExecutionTime;
        
        /// <summary>
        /// Kill script (unload) when it exceeds execution time
        /// </summary>
        private static bool KillScriptOnMaxFunctionExecutionTime;

        /// <summary>
        /// List of localID locks for mutex processing of script events
        /// </summary>
        private List<uint> objectLocks = new List<uint>();
        private object tryLockLock = new object(); // Mutex lock object

        /// <summary>
        /// Queue containing events waiting to be executed
        /// </summary>
        public Queue<QueueItemStruct> eventQueue = new Queue<QueueItemStruct>();

        #region " Queue structures "
        /// <summary>
        /// Queue item structure
        /// </summary>
        public struct QueueItemStruct
        {
            public uint localID;
            public UUID itemID;
            public string functionName;
            public DetectParams[] llDetectParams;
            public object[] param;
            public Dictionary<KeyValuePair<int,int>,KeyValuePair<int,int>>
                    LineMap;
        }

        #endregion

        #region " Initialization / Startup "
        public EventQueueManager(ScriptEngine _ScriptEngine)
        {
            m_ScriptEngine = _ScriptEngine;

            ReadConfig();
            AdjustNumberOfScriptThreads();
        }

        public void ReadConfig()
        {
            // Refresh config
            numberOfThreads = m_ScriptEngine.ScriptConfigSource.GetInt("NumberOfScriptThreads", 2);
            maxFunctionExecutionTimems = m_ScriptEngine.ScriptConfigSource.GetInt("MaxEventExecutionTimeMs", 5000);
            EnforceMaxExecutionTime = m_ScriptEngine.ScriptConfigSource.GetBoolean("EnforceMaxEventExecutionTime", true);
            KillScriptOnMaxFunctionExecutionTime = m_ScriptEngine.ScriptConfigSource.GetBoolean("DeactivateScriptOnTimeout", false);
            EventExecutionMaxQueueSize = m_ScriptEngine.ScriptConfigSource.GetInt("EventExecutionMaxQueueSize", 300);

            // Now refresh config in all threads
            lock (eventQueueThreads)
            {
                foreach (EventQueueThreadClass EventQueueThread in eventQueueThreads)
                {
                    EventQueueThread.ReadConfig();
                }
            }
        }

        #endregion

        #region " Shutdown all threads "
        ~EventQueueManager()
        {
            Stop();
        }

        private void Stop()
        {
            if (eventQueueThreads != null)
            {
                // Kill worker threads
                lock (eventQueueThreads)
                {
                    foreach (EventQueueThreadClass EventQueueThread in new ArrayList(eventQueueThreads))
                    {
                        AbortThreadClass(EventQueueThread);
                    }
                    //eventQueueThreads.Clear();
                    //staticGlobalEventQueueThreads.Clear();
                }
            }

                // Remove all entries from our event queue
                lock (eventQueue)
                {
                    eventQueue.Clear();
                }
        }

        #endregion

        #region " Start / stop script execution threads (ThreadClasses) "
        private void StartNewThreadClass()
        {
            EventQueueThreadClass eqtc = new EventQueueThreadClass();
            eventQueueThreads.Add(eqtc);
            //m_log.Debug("[" + m_ScriptEngine.ScriptEngineName + "]: Started new script execution thread. Current thread count: " + eventQueueThreads.Count);
        }

        private void AbortThreadClass(EventQueueThreadClass threadClass)
        {
            if (eventQueueThreads.Contains(threadClass))
                eventQueueThreads.Remove(threadClass);

            try
            {
                threadClass.Stop();
            }
            catch (Exception)
            {
                //m_log.Error("[" + m_ScriptEngine.ScriptEngineName + ":EventQueueManager]: If you see this, could you please report it to Tedd:");
                //m_log.Error("[" + m_ScriptEngine.ScriptEngineName + ":EventQueueManager]: Script thread execution timeout kill ended in exception: " + ex.ToString());
            }
            //m_log.Debug("[" + m_ScriptEngine.ScriptEngineName + "]: Killed script execution thread. Remaining thread count: " + eventQueueThreads.Count);
        }
        #endregion

        #region " Mutex locks for queue access "
        /// <summary>
        /// Try to get a mutex lock on localID
        /// </summary>
        /// <param name="localID"></param>
        /// <returns></returns>
        public bool TryLock(uint localID)
        {
            lock (tryLockLock)
            {
                if (objectLocks.Contains(localID) == true)
                {
                    return false;
                }
                else
                {
                    objectLocks.Add(localID);
                    return true;
                }
            }
        }

        /// <summary>
        /// Release mutex lock on localID
        /// </summary>
        /// <param name="localID"></param>
        public void ReleaseLock(uint localID)
        {
            lock (tryLockLock)
            {
                if (objectLocks.Contains(localID) == true)
                {
                    objectLocks.Remove(localID);
                }
            }
        }
        #endregion
        
        #region " Check execution queue for a specified Event"
        /// <summary>
        /// checks to see if a specified event type is already in the queue
        /// </summary>
        /// <param name="localID">Region object ID</param>
        /// <param name="FunctionName">Name of the function, will be state + "_event_" + FunctionName</param>
        /// <returns>true if event is found , false if not found</returns>
        /// 
        public bool CheckEeventQueueForEvent(uint localID, string FunctionName)
        {
            if (eventQueue.Count > 0)
            {
                lock (eventQueue)
                {
                    foreach (EventQueueManager.QueueItemStruct QIS in eventQueue)
                    {
                        if ((QIS.functionName == FunctionName) && (QIS.localID == localID))
                            return true;
                    }
                }
            }
            return false;
        }
        #endregion

        #region " Add events to execution queue "
        /// <summary>
        /// Add event to event execution queue
        /// </summary>
        /// <param name="localID">Region object ID</param>
        /// <param name="FunctionName">Name of the function, will be state + "_event_" + FunctionName</param>
        /// <param name="param">Array of parameters to match event mask</param>
        public bool AddToObjectQueue(uint localID, string FunctionName, DetectParams[] qParams, params object[] param)
        {
            // Determine all scripts in Object and add to their queue
            //myScriptEngine.log.Info("[" + ScriptEngineName + "]: EventQueueManager Adding localID: " + localID + ", FunctionName: " + FunctionName);

            // Do we have any scripts in this object at all? If not, return
            if (m_ScriptEngine.m_ScriptManager.Scripts.ContainsKey(localID) == false)
            {
                //m_log.Debug("Event \String.Empty + FunctionName + "\" for localID: " + localID + ". No scripts found on this localID.");
                return false;
            }

            List<UUID> scriptKeys =
                m_ScriptEngine.m_ScriptManager.GetScriptKeys(localID);

            foreach (UUID itemID in scriptKeys)
            {
                // Add to each script in that object
                // TODO: Some scripts may not subscribe to this event. Should we NOT add it? Does it matter?
                AddToScriptQueue(localID, itemID, FunctionName, qParams, param);
            }
            return true;
        }

        /// <summary>
        /// Add event to event execution queue
        /// </summary>
        /// <param name="localID">Region object ID</param>
        /// <param name="itemID">Region script ID</param>
        /// <param name="FunctionName">Name of the function, will be state + "_event_" + FunctionName</param>
        /// <param name="param">Array of parameters to match event mask</param>
        public bool AddToScriptQueue(uint localID, UUID itemID, string FunctionName, DetectParams[] qParams, params object[] param)
        {
            List<UUID> keylist = m_ScriptEngine.m_ScriptManager.GetScriptKeys(localID);

            if (!keylist.Contains(itemID)) // We don't manage that script
            {
                return false;
            }

            lock (eventQueue)
            {
                if (eventQueue.Count >= EventExecutionMaxQueueSize)
                {
                    m_log.Error("[" + m_ScriptEngine.ScriptEngineName + "]: ERROR: Event execution queue item count is at " + eventQueue.Count + ". Config variable \"EventExecutionMaxQueueSize\" is set to " + EventExecutionMaxQueueSize + ", so ignoring new event.");
                    m_log.Error("[" + m_ScriptEngine.ScriptEngineName + "]: Event ignored: localID: " + localID + ", itemID: " + itemID + ", FunctionName: " + FunctionName);
                    return false;
                }

                InstanceData id = m_ScriptEngine.m_ScriptManager.GetScript(
                        localID, itemID);

                // Create a structure and add data
                QueueItemStruct QIS = new QueueItemStruct();
                QIS.localID = localID;
                QIS.itemID = itemID;
                QIS.functionName = FunctionName;
                QIS.llDetectParams = qParams;
                QIS.param = param;
                if (id != null)
                    QIS.LineMap = id.LineMap;

                // Add it to queue
                eventQueue.Enqueue(QIS);
            }
            return true;
        }
        #endregion

        #region " Maintenance thread "

        /// <summary>
        /// Adjust number of script thread classes. It can start new, but if it needs to stop it will just set number of threads in "ThreadsToExit" and threads will have to exit themselves.
        /// Called from MaintenanceThread
        /// </summary>
        public void AdjustNumberOfScriptThreads()
        {
            // Is there anything here for us to do?
            if (eventQueueThreads.Count == numberOfThreads)
                return;

            lock (eventQueueThreads)
            {
                int diff = numberOfThreads - eventQueueThreads.Count;
                // Positive number: Start
                // Negative number: too many are running
                if (diff > 0)
                {
                    // We need to add more threads
                    for (int ThreadCount = eventQueueThreads.Count; ThreadCount < numberOfThreads; ThreadCount++)
                    {
                        StartNewThreadClass();
                    }
                }
                if (diff < 0)
                {
                    // We need to kill some threads
                    lock (ThreadsToExitLock)
                    {
                        ThreadsToExit = Math.Abs(diff);
                    }
                }
            }
        }

        /// <summary>
        /// Check if any thread class has been executing an event too long
        /// </summary>
        public void CheckScriptMaxExecTime()
        {
            // Iterate through all ScriptThreadClasses and check how long their current function has been executing
            lock (eventQueueThreads)
            {
                foreach (EventQueueThreadClass EventQueueThread in eventQueueThreads)
                {
                    // Is thread currently executing anything?
                    if (EventQueueThread.InExecution)
                    {
                        // Has execution time expired?
                        if (DateTime.Now.Ticks - EventQueueThread.LastExecutionStarted >
                            maxFunctionExecutionTimens)
                        {
                            // Yes! We need to kill this thread!

                            // Set flag if script should be removed or not
                            EventQueueThread.KillCurrentScript = KillScriptOnMaxFunctionExecutionTime;

                            // Abort this thread
                            AbortThreadClass(EventQueueThread);

                            // We do not need to start another, MaintenenceThread will do that for us
                            //StartNewThreadClass();
                        }
                    }
                }
            }
        }
        #endregion

        ///// <summary>
        ///// If set to true then threads and stuff should try to make a graceful exit
        ///// </summary>
        //public bool PleaseShutdown
        //{
        //    get { return _PleaseShutdown; }
        //    set { _PleaseShutdown = value; }
        //}
        //private bool _PleaseShutdown = false;
    }
}
