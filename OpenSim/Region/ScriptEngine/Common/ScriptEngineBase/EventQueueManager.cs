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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/


using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes.Scripting;

namespace OpenSim.Region.ScriptEngine.Common.ScriptEngineBase
{
    /// <summary>
    /// EventQueueManager handles event queues
    /// Events are queued and executed in separate thread
    /// </summary>
    [Serializable]
    public class EventQueueManager
    {

        //
        // Class is instanced in "ScriptEngine" and used by "EventManager" also instanced in "ScriptEngine".
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
        // * This class contains the number of threads used for script executions. Since we are not microthreading scripts yet,
        //   increase number of threads to allow more concurrent script executions in OpenSim.
        //


        /// <summary>
        /// List of threads processing event queue
        /// </summary>
        private List<EventQueueThreadClass> eventQueueThreads;// = new List<EventQueueThreadClass>();
        private object eventQueueThreadsLock;// = new object();

        private static List<EventQueueThreadClass> staticEventQueueThreads;// = new List<EventQueueThreadClass>();
        private static object staticEventQueueThreadsLock;// = new object();

        public object queueLock = new object(); // Mutex lock object

        /// <summary>
        /// How many threads to process queue with
        /// </summary>
        private int numberOfThreads;


        /// <summary>
        /// Maximum time one function can use for execution before we perform a thread kill
        /// </summary>
        private int maxFunctionExecutionTimems;
        private bool EnforceMaxExecutionTime;
        private bool KillScriptOnMaxFunctionExecutionTime;


        /// <summary>
        /// Queue containing events waiting to be executed
        /// </summary>
        public Queue<QueueItemStruct> eventQueue = new Queue<QueueItemStruct>();

        /// <summary>
        /// Queue item structure
        /// </summary>
        public struct QueueItemStruct
        {
            public uint localID;
            public LLUUID itemID;
            public string functionName;
            public Queue_llDetectParams_Struct llDetectParams;
            public object[] param;
        }

        /// <summary>
        /// Shared empty llDetectNull
        /// </summary>
        public readonly static Queue_llDetectParams_Struct llDetectNull = new Queue_llDetectParams_Struct();

        /// <summary>
        /// Structure to hold data for llDetect* commands
        /// </summary>
        [Serializable]
        public struct Queue_llDetectParams_Struct
        {
            // More or less just a placeholder for the actual moving of additional data
            // should be fixed to something better :)
            public LSL_Types.key[] _key;
            public LSL_Types.Quaternion[] _Quaternion;
            public LSL_Types.Vector3[] _Vector3;
            public bool[] _bool;
            public int[] _int;
            public string[] _string;
        }

        /// <summary>
        /// List of localID locks for mutex processing of script events
        /// </summary>
        private List<uint> objectLocks = new List<uint>();

        private object tryLockLock = new object(); // Mutex lock object

        public ScriptEngine m_ScriptEngine;

        public Thread ExecutionTimeoutEnforcingThread;

        public EventQueueManager(ScriptEngine _ScriptEngine)
        {
            m_ScriptEngine = _ScriptEngine;


            // Create thread pool list and lock object
            // Determine from config if threads should be dedicated to regions or shared
            if (m_ScriptEngine.ScriptConfigSource.GetBoolean("PrivateRegionThreads", false))
            {
                // PRIVATE THREAD POOL PER REGION
                eventQueueThreads = new List<EventQueueThreadClass>();
                eventQueueThreadsLock = new object();
            }
            else
            {
                // SHARED THREAD POOL
                // Crate the objects in statics
                if (staticEventQueueThreads == null)
                    staticEventQueueThreads = new List<EventQueueThreadClass>();
                if (staticEventQueueThreadsLock == null)
                    staticEventQueueThreadsLock = new object();

                // Create local reference to them
                eventQueueThreads = staticEventQueueThreads;
                eventQueueThreadsLock = staticEventQueueThreadsLock;
            }

            numberOfThreads = m_ScriptEngine.ScriptConfigSource.GetInt("NumberOfScriptThreads", 2);

            maxFunctionExecutionTimems = m_ScriptEngine.ScriptConfigSource.GetInt("MaxEventExecutionTimeMs", 5000);
            EnforceMaxExecutionTime = m_ScriptEngine.ScriptConfigSource.GetBoolean("EnforceMaxEventExecutionTime", false);
            KillScriptOnMaxFunctionExecutionTime = m_ScriptEngine.ScriptConfigSource.GetBoolean("DeactivateScriptOnTimeout", false);


            // Start function max exec time enforcement thread
            if (EnforceMaxExecutionTime)
            {
                ExecutionTimeoutEnforcingThread = new Thread(ExecutionTimeoutEnforcingLoop);
                ExecutionTimeoutEnforcingThread.Name = "ExecutionTimeoutEnforcingThread";
                ExecutionTimeoutEnforcingThread.IsBackground = true;
                ExecutionTimeoutEnforcingThread.Start();
            }

            //
            // Start event queue processing threads (worker threads)
            //

            lock (eventQueueThreadsLock)
            {
                for (int ThreadCount = eventQueueThreads.Count; ThreadCount < numberOfThreads; ThreadCount++)
                {
                    StartNewThreadClass();
                }
            }
        }

        ~EventQueueManager()
        {
            try
            {
                if (ExecutionTimeoutEnforcingThread != null)
                {
                    if (ExecutionTimeoutEnforcingThread.IsAlive)
                    {
                        ExecutionTimeoutEnforcingThread.Abort();
                    }
                }
            }
            catch (Exception ex)
            {
            }


            // Kill worker threads
            lock (eventQueueThreadsLock)
            {
                foreach (EventQueueThreadClass EventQueueThread in new ArrayList(eventQueueThreads))
                {
                    EventQueueThread.Shutdown();
                }
                eventQueueThreads.Clear();
            }
            // Todo: Clean up our queues
            eventQueue.Clear();
        }


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


        /// <summary>
        /// Add event to event execution queue
        /// </summary>
        /// <param name="localID">Region object ID</param>
        /// <param name="FunctionName">Name of the function, will be state + "_event_" + FunctionName</param>
        /// <param name="param">Array of parameters to match event mask</param>
        public void AddToObjectQueue(uint localID, string FunctionName, Queue_llDetectParams_Struct qParams, params object[] param)
        {
            // Determine all scripts in Object and add to their queue
            //myScriptEngine.m_logger.Verbose("ScriptEngine", "EventQueueManager Adding localID: " + localID + ", FunctionName: " + FunctionName);


            // Do we have any scripts in this object at all? If not, return
            if (m_ScriptEngine.m_ScriptManager.Scripts.ContainsKey(localID) == false)
            {
                //Console.WriteLine("Event \String.Empty + FunctionName + "\" for localID: " + localID + ". No scripts found on this localID.");
                return;
            }

            Dictionary<LLUUID, IScript>.KeyCollection scriptKeys =
                m_ScriptEngine.m_ScriptManager.GetScriptKeys(localID);

            foreach (LLUUID itemID in scriptKeys)
            {
                // Add to each script in that object
                // TODO: Some scripts may not subscribe to this event. Should we NOT add it? Does it matter?
                AddToScriptQueue(localID, itemID, FunctionName, qParams, param);
            }
        }

        /// <summary>
        /// Add event to event execution queue
        /// </summary>
        /// <param name="localID">Region object ID</param>
        /// <param name="itemID">Region script ID</param>
        /// <param name="FunctionName">Name of the function, will be state + "_event_" + FunctionName</param>
        /// <param name="param">Array of parameters to match event mask</param>
        public void AddToScriptQueue(uint localID, LLUUID itemID, string FunctionName, Queue_llDetectParams_Struct qParams, params object[] param)
        {
            lock (queueLock)
            {
                // Create a structure and add data
                QueueItemStruct QIS = new QueueItemStruct();
                QIS.localID = localID;
                QIS.itemID = itemID;
                QIS.functionName = FunctionName;
                QIS.llDetectParams = qParams;
                QIS.param = param;

                // Add it to queue
                eventQueue.Enqueue(QIS);
            }
        }

        /// <summary>
        /// A thread should run in this loop and check all running scripts
        /// </summary>
        public void ExecutionTimeoutEnforcingLoop()
        {
            try
            {
                while (true)
                {
                    System.Threading.Thread.Sleep(maxFunctionExecutionTimems);
                    lock (eventQueueThreadsLock)
                    {
                        foreach (EventQueueThreadClass EventQueueThread in new ArrayList(eventQueueThreads))
                        {
                            if (EventQueueThread.InExecution)
                            {
                                if (DateTime.Now.Subtract(EventQueueThread.LastExecutionStarted).Milliseconds >
                                    maxFunctionExecutionTimems)
                                {
                                    // We need to kill this thread!
                                    EventQueueThread.KillCurrentScript = KillScriptOnMaxFunctionExecutionTime;
                                    AbortThreadClass(EventQueueThread);
                                    // Then start another
                                    StartNewThreadClass();
                                }
                            }
                        }
                    }
                }
            }
            catch (ThreadAbortException tae)
            {
            }
        }

        private void AbortThreadClass(EventQueueThreadClass threadClass)
        {
            try
            {
                threadClass.Shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could you please report this to Tedd:");
                Console.WriteLine("Script thread execution timeout kill ended in exception: " + ex.ToString());
            }
            m_ScriptEngine.Log.Debug("DotNetEngine", "Killed script execution thread, count: " + eventQueueThreads.Count);
        }

        private void StartNewThreadClass()
        {
            EventQueueThreadClass eqtc = new EventQueueThreadClass(this);
            eventQueueThreads.Add(eqtc);
            m_ScriptEngine.Log.Debug("DotNetEngine", "Started new script execution thread, count: " + eventQueueThreads.Count);

        }
    }
}