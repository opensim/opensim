/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
/* Original code: Tedd Hansen */
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Reflection;
using OpenSim.Region.Environment.Scenes.Scripting;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    /// <summary>
    /// EventQueueManager handles event queues
    /// Events are queued and executed in separate thread
    /// </summary>
    [Serializable]
    class EventQueueManager
    {
        /// <summary>
        /// List of threads processing event queue
        /// </summary>
        private List<Thread> EventQueueThreads = new List<Thread>();
        private object QueueLock = new object(); // Mutex lock object
        /// <summary>
        /// How many ms to sleep if queue is empty
        /// </summary>
        private int NothingToDoSleepms = 50;
        /// <summary>
        /// How many threads to process queue with
        /// </summary>
        private int NumberOfThreads = 2;
        /// <summary>
        /// Queue containing events waiting to be executed
        /// </summary>
        private Queue<QueueItemStruct> EventQueue = new Queue<QueueItemStruct>();
        /// <summary>
        /// Queue item structure
        /// </summary>
        private struct QueueItemStruct
        {
            public IScriptHost ObjectID;
            public string ScriptID;
            public string FunctionName;
            public object[] param;
        }

        /// <summary>
        /// List of ObjectID locks for mutex processing of script events
        /// </summary>
        private List<IScriptHost> ObjectLocks = new List<IScriptHost>();
        private object TryLockLock = new object(); // Mutex lock object

        private ScriptEngine myScriptEngine;
        public EventQueueManager(ScriptEngine _ScriptEngine)
        {
            myScriptEngine = _ScriptEngine;

            //
            // Start event queue processing threads (worker threads)
            //
            for (int ThreadCount = 0; ThreadCount <= NumberOfThreads; ThreadCount++)
            {
                Thread EventQueueThread = new Thread(EventQueueThreadLoop);
                EventQueueThreads.Add(EventQueueThread);
                EventQueueThread.IsBackground = true;
                EventQueueThread.Name = "EventQueueManagerThread_" + ThreadCount;
                EventQueueThread.Start();
            }
        }
        ~EventQueueManager()
        {

            // Kill worker threads
            foreach (Thread EventQueueThread in new System.Collections.ArrayList(EventQueueThreads))
            {
                if (EventQueueThread != null && EventQueueThread.IsAlive == true)
                {
                    try
                    {
                        EventQueueThread.Abort();
                        EventQueueThread.Join();
                    }
                    catch (Exception e)
                    {
                        myScriptEngine.Log.Verbose("ScriptEngine", "EventQueueManager Exception killing worker thread: " + e.ToString());
                    }
                }
            }
            EventQueueThreads.Clear();
            // Todo: Clean up our queues
            EventQueue.Clear();

        }

        /// <summary>
        /// Queue processing thread loop
        /// </summary>
        private void EventQueueThreadLoop()
        {
            //myScriptEngine.m_logger.Verbose("ScriptEngine", "EventQueueManager Worker thread spawned");
            try
            {
                QueueItemStruct BlankQIS = new QueueItemStruct();
                while (true)
                {
                    QueueItemStruct QIS = BlankQIS;
                    bool GotItem = false;

                    if (EventQueue.Count == 0)
                    {
                        // Nothing to do? Sleep a bit waiting for something to do
                        Thread.Sleep(NothingToDoSleepms);
                    }
                    else
                    {
                        // Something in queue, process
                        //myScriptEngine.m_logger.Verbose("ScriptEngine", "Processing event for ObjectID: " + QIS.ObjectID + ", ScriptID: " + QIS.ScriptID + ", FunctionName: " + QIS.FunctionName);

                        // OBJECT BASED LOCK - TWO THREADS WORKING ON SAME OBJECT IS NOT GOOD
                        lock (QueueLock)
                        {
                            GotItem = false;
                            for (int qc = 0; qc < EventQueue.Count; qc++)
                            {
                                // Get queue item
                                QIS = EventQueue.Dequeue();

                                // Check if object is being processed by someone else
                                if (TryLock(QIS.ObjectID) == false)
                                {
                                    // Object is already being processed, requeue it
                                    EventQueue.Enqueue(QIS);
                                }
                                else
                                {
                                    // We have lock on an object and can process it
                                    GotItem = true;
                                    break;
                                }
                            } // go through queue
                        } // lock

                        if (GotItem == true)
                        {
                            // Execute function
                            myScriptEngine.myScriptManager.ExecuteEvent(QIS.ObjectID, QIS.ScriptID, QIS.FunctionName, QIS.param);
                            ReleaseLock(QIS.ObjectID);
                        }

                    } // Something in queue
                } // while
            } // try
            catch (ThreadAbortException tae)
            {
                myScriptEngine.Log.Verbose("ScriptEngine", "EventQueueManager Worker thread killed: " + tae.Message);
            }
        }

        /// <summary>
        /// Try to get a mutex lock on ObjectID
        /// </summary>
        /// <param name="ObjectID"></param>
        /// <returns></returns>
        private bool TryLock(IScriptHost ObjectID)
        {
            lock (TryLockLock)
            {
                if (ObjectLocks.Contains(ObjectID) == true)
                {
                    return false;
                }
                else
                {
                    ObjectLocks.Add(ObjectID);
                    return true;
                }
            }
        }

        /// <summary>
        /// Release mutex lock on ObjectID
        /// </summary>
        /// <param name="ObjectID"></param>
        private void ReleaseLock(IScriptHost ObjectID)
        {
            lock (TryLockLock)
            {
                if (ObjectLocks.Contains(ObjectID) == true)
                {
                    ObjectLocks.Remove(ObjectID);
                }
            }
        }

        /// <summary>
        /// Add event to event execution queue
        /// </summary>
        /// <param name="ObjectID"></param>
        /// <param name="FunctionName">Name of the function, will be state + "_event_" + FunctionName</param>
        /// <param name="param">Array of parameters to match event mask</param>
        public void AddToObjectQueue(IScriptHost ObjectID, string FunctionName, object[] param)
        {
            // Determine all scripts in Object and add to their queue
            //myScriptEngine.m_logger.Verbose("ScriptEngine", "EventQueueManager Adding ObjectID: " + ObjectID + ", FunctionName: " + FunctionName);

            lock (QueueLock)
            {

                foreach (string ScriptID in myScriptEngine.myScriptManager.GetScriptKeys(ObjectID))
                {
                    // Add to each script in that object
                    // TODO: Some scripts may not subscribe to this event. Should we NOT add it? Does it matter?

                    // Create a structure and add data
                    QueueItemStruct QIS = new QueueItemStruct();
                    QIS.ObjectID = ObjectID;
                    QIS.ScriptID = ScriptID;
                    QIS.FunctionName = FunctionName;
                    QIS.param = param;

                    // Add it to queue
                    EventQueue.Enqueue(QIS);

                }
            }

        }

    }
}
