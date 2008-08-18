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
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Globalization;
using libsecondlife;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes.Scripting;

namespace OpenSim.Region.ScriptEngine.Common.ScriptEngineBase
{
    /// <summary>
    /// Because every thread needs some data set for it (time started to execute current function), it will do its work within a class
    /// </summary>
    public class EventQueueThreadClass : iScriptEngineFunctionModule
    {
        // private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// How many ms to sleep if queue is empty
        /// </summary>
        private static int nothingToDoSleepms;// = 50;
        private static ThreadPriority MyThreadPriority;

        public long LastExecutionStarted;
        public bool InExecution = false;
        public bool KillCurrentScript = false;

        //private EventQueueManager eventQueueManager;
        public Thread EventQueueThread;
        private static int ThreadCount = 0;

        private string ScriptEngineName = "ScriptEngine.Common";

        public EventQueueThreadClass()//EventQueueManager eqm
        {
            //eventQueueManager = eqm;
            ReadConfig();
            Start();
        }

        ~EventQueueThreadClass()
        {
            Stop();
        }

        public void ReadConfig()
        {
            lock (ScriptEngine.ScriptEngines)
            {
                foreach (ScriptEngine m_ScriptEngine in ScriptEngine.ScriptEngines)
                {
                    ScriptEngineName = m_ScriptEngine.ScriptEngineName;
                    nothingToDoSleepms = m_ScriptEngine.ScriptConfigSource.GetInt("SleepTimeIfNoScriptExecutionMs", 50);

                    // Later with ScriptServer we might want to ask OS for stuff too, so doing this a bit manually
                    string pri = m_ScriptEngine.ScriptConfigSource.GetString("ScriptThreadPriority", "BelowNormal");
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
                            m_ScriptEngine.Log.Error("[ScriptEngineBase]: Unknown priority type \"" + pri +
                                                     "\" in config file. Defaulting to \"BelowNormal\".");
                            break;
                    }
                }
            }
            // Now set that priority
            if (EventQueueThread != null)
                if (EventQueueThread.IsAlive)
                    EventQueueThread.Priority = MyThreadPriority;
        }

        /// <summary>
        /// Start thread
        /// </summary>
        private void Start()
        {
            EventQueueThread = new Thread(EventQueueThreadLoop);
            EventQueueThread.IsBackground = true;

            EventQueueThread.Priority = MyThreadPriority;
            EventQueueThread.Name = "EventQueueManagerThread_" + ThreadCount;
            EventQueueThread.Start();
            ThreadTracker.Add(EventQueueThread);

            // Look at this... Don't you wish everyone did that solid coding everywhere? :P
            if (ThreadCount == int.MaxValue)
                ThreadCount = 0;
            ThreadCount++;
        }

        public void Stop()
        {
            //PleaseShutdown = true;                  // Set shutdown flag
            //Thread.Sleep(100);                       // Wait a bit
            if (EventQueueThread != null && EventQueueThread.IsAlive == true)
            {
                try
                {
                    EventQueueThread.Abort();               // Send abort
                    //EventQueueThread.Join();                // Wait for it
                }
                catch (Exception)
                {
                    //myScriptEngine.Log.Info("[" + ScriptEngineName + "]: EventQueueManager Exception killing worker thread: " + e.ToString());
                }
            }
        }

        private EventQueueManager.QueueItemStruct BlankQIS = new EventQueueManager.QueueItemStruct();
        private ScriptEngine lastScriptEngine;
        /// <summary>
        /// Queue processing thread loop
        /// </summary>
        private void EventQueueThreadLoop()
        {
            CultureInfo USCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = USCulture;

            //myScriptEngine.Log.Info("[" + ScriptEngineName + "]: EventQueueManager Worker thread spawned");
            try
            {
                while (true)
                {
                    try
                    {
                        while (true)
                        {
                            DoProcessQueue();
                        }
                    }
                    catch (ThreadAbortException)
                    {
                        if (lastScriptEngine != null)
                        lastScriptEngine.Log.Info("[" + ScriptEngineName + "]: ThreadAbortException while executing function.");
                    }
                    catch (Exception e)
                    {
                        if (lastScriptEngine != null)
                        lastScriptEngine.Log.Error("[" + ScriptEngineName + "]: Exception in EventQueueThreadLoop: " + e.ToString());
                    }
                }
            }
            catch (ThreadAbortException)
            {
                //myScriptEngine.Log.Info("[" + ScriptEngineName + "]: EventQueueManager Worker thread killed: " + tae.Message);
            }
        }

        public void DoProcessQueue()
        {
            //lock (ScriptEngine.ScriptEngines)
            //{
                foreach (ScriptEngine m_ScriptEngine in new ArrayList(ScriptEngine.ScriptEngines))
                {
                    lastScriptEngine = m_ScriptEngine;
                    // Every now and then check if we should shut down
                    //if (PleaseShutdown || EventQueueManager.ThreadsToExit > 0)
                    //{
                    //    // Someone should shut down, lets get exclusive lock
                    //    lock (EventQueueManager.ThreadsToExitLock)
                    //    {
                    //        // Lets re-check in case someone grabbed it
                    //        if (EventQueueManager.ThreadsToExit > 0)
                    //        {
                    //            // Its crowded here so we'll shut down
                    //            EventQueueManager.ThreadsToExit--;
                    //            Stop();
                    //            return;
                    //        }
                    //        else
                    //        {
                    //            // We have been asked to shut down
                    //            Stop();
                    //            return;
                    //        }
                    //    }
                    //}

                    //try
                    //  {
                    EventQueueManager.QueueItemStruct QIS = BlankQIS;
                    bool GotItem = false;

                    //if (PleaseShutdown)
                    //    return;

                    if (m_ScriptEngine.m_EventQueueManager == null || m_ScriptEngine.m_EventQueueManager.eventQueue == null)
                        continue;

                    if (m_ScriptEngine.m_EventQueueManager.eventQueue.Count == 0)
                    {
                        // Nothing to do? Sleep a bit waiting for something to do
                        Thread.Sleep(nothingToDoSleepms);
                    }
                    else
                    {
                        // Something in queue, process
                        //myScriptEngine.Log.Info("[" + ScriptEngineName + "]: Processing event for localID: " + QIS.localID + ", itemID: " + QIS.itemID + ", FunctionName: " + QIS.FunctionName);

                        // OBJECT BASED LOCK - TWO THREADS WORKING ON SAME OBJECT IS NOT GOOD
                        lock (m_ScriptEngine.m_EventQueueManager.eventQueue)
                        {
                            GotItem = false;
                            for (int qc = 0; qc < m_ScriptEngine.m_EventQueueManager.eventQueue.Count; qc++)
                            {
                                // Get queue item
                                QIS = m_ScriptEngine.m_EventQueueManager.eventQueue.Dequeue();

                                // Check if object is being processed by someone else
                                if (m_ScriptEngine.m_EventQueueManager.TryLock(QIS.localID) == false)
                                {
                                    // Object is already being processed, requeue it
                                    m_ScriptEngine.m_EventQueueManager.eventQueue.Enqueue(QIS);
                                }
                                else
                                {
                                    // We have lock on an object and can process it
                                    GotItem = true;
                                    break;
                                }
                            }
                        }

                        if (GotItem == true)
                        {
                            // Execute function
                            try
                            {
                                ///cfk 2-7-08 dont need this right now and the default Linux build has DEBUG defined
#if DEBUG
                                //eventQueueManager.m_ScriptEngine.Log.Debug("[" + ScriptEngineName + "]: " +
                                //                                              "Executing event:\r\n"
                                //                                              + "QIS.localID: " + QIS.localID
                                //                                              + ", QIS.itemID: " + QIS.itemID
                                //                                              + ", QIS.functionName: " +
                                //                                              QIS.functionName);
#endif
                                // Only pipe event if land supports it.
                                if (m_ScriptEngine.World.pipeEventsForScript(QIS.localID))
                                {
                                    LastExecutionStarted = DateTime.Now.Ticks;
                                    KillCurrentScript = false;
                                    InExecution = true;
                                    m_ScriptEngine.m_ScriptManager.ExecuteEvent(QIS.localID,
                                                                                QIS.itemID,
                                                                                QIS.functionName,
                                                                                QIS.llDetectParams,
                                                                                QIS.param);
                                    InExecution = false;
                                }
                            }
                            catch (Exception e)
                            {
                                InExecution = false;
                                // DISPLAY ERROR INWORLD
                                string text = "Error executing script function \"" + QIS.functionName +
                                              "\":\r\n";
                                if (e.InnerException != null)
                                {
                                    // Send inner exception
                                    string line = " (unknown line)";
                                    Regex rx = new Regex(@"SecondLife\.Script\..+[\s:](?<line>\d+)\.?\r?$", RegexOptions.Compiled);
                                    if (rx.Match(e.InnerException.ToString()).Success)
                                        line = " (line " + rx.Match(e.InnerException.ToString()).Result("${line}") + ")";
                                    text += e.InnerException.Message.ToString() + line;
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
                                    IScriptHost m_host =
                                        m_ScriptEngine.World.GetSceneObjectPart(QIS.localID);
                                    //if (m_host != null)
                                    //{
                                    m_ScriptEngine.World.SimChat(Helpers.StringToField(text),
                                                                 ChatTypeEnum.DebugChannel, 2147483647,
                                                                 m_host.AbsolutePosition,
                                                                 m_host.Name, m_host.UUID, false);
                                }
                                catch (Exception)
                                {
                                    //}
                                    //else
                                    //{
                                    // T oconsole
                                    m_ScriptEngine.m_EventQueueManager.m_ScriptEngine.Log.Error("[" + ScriptEngineName +
                                                                                                "]: " +
                                                                                                "Unable to send text in-world:\r\n" +
                                                                                                text);
                                }
                                finally
                                {
                                    // So we are done sending message in-world
                                    if (KillCurrentScript)
                                    {
                                        m_ScriptEngine.m_EventQueueManager.m_ScriptEngine.m_ScriptManager.StopScript(
                                            QIS.localID, QIS.itemID);
                                    }
                                }

                                // Pass it on so it's displayed on the console
                                // and in the logs (mikem 2008.06.02).
                                throw e.InnerException;
                            }
                            finally
                            {
                                InExecution = false;
                                m_ScriptEngine.m_EventQueueManager.ReleaseLock(QIS.localID);
                            }
                        }
                    }
                }
           // }
        }

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
