using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes.Scripting;

namespace OpenSim.Region.ScriptEngine.Common.ScriptEngineBase
{
    /// <summary>
    /// Because every thread needs some data set for it (time started to execute current function), it will do its work within a class
    /// </summary>
    public class EventQueueThreadClass
    {
        /// <summary>
        /// How many ms to sleep if queue is empty
        /// </summary>
        private int nothingToDoSleepms;// = 50;

        public DateTime LastExecutionStarted;
        public bool InExecution = false;
        public bool KillCurrentScript = false;

        private EventQueueManager eventQueueManager;
        public Thread EventQueueThread;
        private static int ThreadCount = 0;
        private ThreadPriority MyThreadPriority;

        public EventQueueThreadClass(EventQueueManager eqm)
        {
            eventQueueManager = eqm;
            nothingToDoSleepms = eqm.m_ScriptEngine.ScriptConfigSource.GetInt("SleepTimeIfNoScriptExecutionMs", 50);
            Start();
        }

        ~EventQueueThreadClass()
        {
            Shutdown();
        }

        /// <summary>
        /// Start thread
        /// </summary>
        private void Start()
        {
            // Later with ScriptServer we might want to ask OS for stuff too, so doing this a bit manually
            string pri = eventQueueManager.m_ScriptEngine.ScriptConfigSource.GetString("ScriptThreadPriority", "BelowNormal");
            switch (pri.ToLower())
            {
                case "lowest":
                    MyThreadPriority = ThreadPriority.Lowest;
                    break;
                case "belownormal":
                    MyThreadPriority = ThreadPriority.BelowNormal;
                    break;
                case "normal":
                    MyThreadPriority = ThreadPriority.Normal;
                    break;
                case "abovenormal":
                    MyThreadPriority = ThreadPriority.AboveNormal;
                    break;
                case "highest":
                    MyThreadPriority = ThreadPriority.Highest;
                    break;
                default:
                    MyThreadPriority = ThreadPriority.BelowNormal; // Default
                    eventQueueManager.m_ScriptEngine.Log.Error("ScriptEngineBase", "Unknown priority type \"" + pri + "\" in config file. Defaulting to \"BelowNormal\".");
                    break;
            }


            EventQueueThread = new Thread(EventQueueThreadLoop);
            EventQueueThread.IsBackground = true;
            
            EventQueueThread.Priority = MyThreadPriority;
            EventQueueThread.Name = "EventQueueManagerThread_" + ThreadCount;
            EventQueueThread.Start();

            // Look at this... Don't you wish everyone did that solid coding everywhere? :P
            if (ThreadCount == int.MaxValue)
                ThreadCount = 0;
            ThreadCount++;
        }

        public void Shutdown()
        {
            if (EventQueueThread != null && EventQueueThread.IsAlive == true)
            {
                try
                {
                    EventQueueThread.Abort();
                    EventQueueThread.Join();
                }
                catch (Exception)
                {
                    //myScriptEngine.Log.Verbose("ScriptEngine", "EventQueueManager Exception killing worker thread: " + e.ToString());
                }
            }
        }


        /// <summary>
        /// Queue processing thread loop
        /// </summary>
        private void EventQueueThreadLoop()
        {
            //myScriptEngine.m_logger.Verbose("ScriptEngine", "EventQueueManager Worker thread spawned");
            try
            {
                EventQueueManager.QueueItemStruct BlankQIS = new EventQueueManager.QueueItemStruct();
                while (true)
                {
                    try
                    {
                        EventQueueManager.QueueItemStruct QIS = BlankQIS;
                        bool GotItem = false;

                        if (eventQueueManager.eventQueue.Count == 0)
                        {
                            // Nothing to do? Sleep a bit waiting for something to do
                            Thread.Sleep(nothingToDoSleepms);
                        }
                        else
                        {
                            // Something in queue, process
                            //myScriptEngine.m_logger.Verbose("ScriptEngine", "Processing event for localID: " + QIS.localID + ", itemID: " + QIS.itemID + ", FunctionName: " + QIS.FunctionName);

                            // OBJECT BASED LOCK - TWO THREADS WORKING ON SAME OBJECT IS NOT GOOD
                            lock (eventQueueManager.queueLock)
                            {
                                GotItem = false;
                                for (int qc = 0; qc < eventQueueManager.eventQueue.Count; qc++)
                                {
                                    // Get queue item
                                    QIS = eventQueueManager.eventQueue.Dequeue();

                                    // Check if object is being processed by someone else
                                    if (eventQueueManager.TryLock(QIS.localID) == false)
                                    {
                                        // Object is already being processed, requeue it
                                        eventQueueManager.eventQueue.Enqueue(QIS);
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
                                try
                                {
#if DEBUG
                                    eventQueueManager.m_ScriptEngine.Log.Debug("ScriptEngine", "Executing event:\r\n"
                                                                             + "QIS.localID: " + QIS.localID
                                                                             + ", QIS.itemID: " + QIS.itemID
                                                                             + ", QIS.functionName: " + QIS.functionName);
#endif
                                    LastExecutionStarted = DateTime.Now;
                                    KillCurrentScript = false;
                                    InExecution = true;
                                    eventQueueManager.m_ScriptEngine.m_ScriptManager.ExecuteEvent(QIS.localID, QIS.itemID,
                                                                                QIS.functionName, QIS.llDetectParams, QIS.param);
                                    InExecution = false;
                                }
                                catch (Exception e)
                                {
                                    InExecution = false;
                                    // DISPLAY ERROR INWORLD
                                    string text = "Error executing script function \"" + QIS.functionName + "\":\r\n";
                                    if (e.InnerException != null)
                                    {
                                        // Send inner exception
                                        text += e.InnerException.Message.ToString();
                                    }
                                    else
                                    {
                                        text += "\r\n";
                                        // Send normal
                                        text += e.Message.ToString();
                                    }
                                    if (KillCurrentScript)
                                        text += "\r\nScript will be deactivated!";

                                    try
                                    {
                                        if (text.Length > 1500)
                                            text = text.Substring(0, 1500);
                                        IScriptHost m_host = eventQueueManager.m_ScriptEngine.World.GetSceneObjectPart(QIS.localID);
                                        //if (m_host != null)
                                        //{
                                        eventQueueManager.m_ScriptEngine.World.SimChat(Helpers.StringToField(text), ChatTypeEnum.Say, 0,
                                                                     m_host.AbsolutePosition, m_host.Name, m_host.UUID);
                                    }
                                    catch
                                    {
                                        //}
                                        //else
                                        //{
                                        // T oconsole
                                        eventQueueManager.m_ScriptEngine.Log.Error("ScriptEngine",
                                                                 "Unable to send text in-world:\r\n" + text);
                                    }
                                    finally
                                    {
                                        // So we are done sending message in-world
                                        if (KillCurrentScript)
                                        {
                                            eventQueueManager.m_ScriptEngine.m_ScriptManager.RemoveScript(QIS.localID, QIS.itemID);
                                        }
                                    }
                                }
                                finally
                                {
                                    InExecution = false;
                                    eventQueueManager.ReleaseLock(QIS.localID);
                                }
                            }
                        } // Something in queue
                    }
                    catch (ThreadAbortException tae)
                    {
                        eventQueueManager.m_ScriptEngine.Log.Notice("ScriptEngine", "ThreadAbortException while executing function.");
                    }
                    catch (Exception e)
                    {
                        eventQueueManager.m_ScriptEngine.Log.Error("ScriptEngine", "Exception in EventQueueThreadLoop: " + e.ToString());
                    }
                } // while
            } // try
            catch (ThreadAbortException)
            {
                //myScriptEngine.Log.Verbose("ScriptEngine", "EventQueueManager Worker thread killed: " + tae.Message);
            }
        }

    }
}
