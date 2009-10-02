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
using System.Text.RegularExpressions;
using System.Threading;
using System.Globalization;
using OpenMetaverse;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Scripting;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.CodeTools;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    // Because every thread needs some data set for it
    // (time started to execute current function), it will do its work
    // within a class
    public class EventQueueThreadClass
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // How many ms to sleep if queue is empty
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
            CultureInfo USCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = USCulture;

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
                foreach (ScriptEngine m_ScriptEngine in
                        ScriptEngine.ScriptEngines)
                {
                    ScriptEngineName = m_ScriptEngine.ScriptEngineName;
                    nothingToDoSleepms =
                            m_ScriptEngine.ScriptConfigSource.GetInt(
                            "SleepTimeIfNoScriptExecutionMs", 50);

                    string pri = m_ScriptEngine.ScriptConfigSource.GetString(
                            "ScriptThreadPriority", "BelowNormal");

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
                            MyThreadPriority = ThreadPriority.BelowNormal;
                            m_log.Error(
                                "[ScriptEngine.DotNetEngine]: Unknown "+
                                "priority type \"" + pri +
                                "\" in config file. Defaulting to "+
                                "\"BelowNormal\".");
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

            // Look at this... Don't you wish everyone did that solid
            // coding everywhere? :P

            if (ThreadCount == int.MaxValue)
                ThreadCount = 0;

            ThreadCount++;
        }

        public void Stop()
        {
            if (EventQueueThread != null && EventQueueThread.IsAlive == true)
            {
                try
                {
                    EventQueueThread.Abort();               // Send abort
                }
                catch (Exception)
                {
                }
            }
        }

        private EventQueueManager.QueueItemStruct BlankQIS =
                new EventQueueManager.QueueItemStruct();

        private ScriptEngine lastScriptEngine;
        private uint lastLocalID;
        private UUID lastItemID;

        // Queue processing thread loop
        private void EventQueueThreadLoop()
        {
            CultureInfo USCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = USCulture;

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
                        m_log.Info("[" + ScriptEngineName +
                                   "]: ThreadAbortException while executing "+
                                   "function.");
                    }
                    catch (SelfDeleteException) // Must delete SOG
                    {
                        SceneObjectPart part =
                            lastScriptEngine.World.GetSceneObjectPart(
                                lastLocalID);
                        if (part != null && part.ParentGroup != null)
                            lastScriptEngine.World.DeleteSceneObject(
                                part.ParentGroup, false);
                    }
                    catch (ScriptDeleteException) // Must delete item
                    {
                        SceneObjectPart part =
                            lastScriptEngine.World.GetSceneObjectPart(
                                lastLocalID);
                        if (part != null && part.ParentGroup != null)
                            part.Inventory.RemoveInventoryItem(lastItemID);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[{0}]: Exception {1} thrown", ScriptEngineName, e.GetType().ToString());
                        throw e;
                    }
                }
            }
            catch (ThreadAbortException)
            {
            }
            catch (Exception e)
            {
                // TODO: Let users in the sim and those entering it and possibly an external watchdog know what has happened
                m_log.ErrorFormat(
                    "[{0}]: Event queue thread terminating with exception.  PLEASE REBOOT YOUR SIM - SCRIPT EVENTS WILL NOT WORK UNTIL YOU DO.  Exception is {1}", 
                    ScriptEngineName, e);
            }
        }

        public void DoProcessQueue()
        {
            foreach (ScriptEngine m_ScriptEngine in
                     new ArrayList(ScriptEngine.ScriptEngines))
            {
                lastScriptEngine = m_ScriptEngine;

                EventQueueManager.QueueItemStruct QIS = BlankQIS;
                bool GotItem = false;

                //if (PleaseShutdown)
                //    return;

                if (m_ScriptEngine.m_EventQueueManager == null ||
                        m_ScriptEngine.m_EventQueueManager.eventQueue == null)
                    continue;

                if (m_ScriptEngine.m_EventQueueManager.eventQueue.Count == 0)
                {
                    // Nothing to do? Sleep a bit waiting for something to do
                    Thread.Sleep(nothingToDoSleepms);
                }
                else
                {
                    // Something in queue, process

                    // OBJECT BASED LOCK - TWO THREADS WORKING ON SAME
                    // OBJECT IS NOT GOOD
                    lock (m_ScriptEngine.m_EventQueueManager.eventQueue)
                    {
                        GotItem = false;
                        for (int qc = 0; qc < m_ScriptEngine.m_EventQueueManager.eventQueue.Count; qc++)
                        {
                            // Get queue item
                            QIS = m_ScriptEngine.m_EventQueueManager.eventQueue.Dequeue();

                            // Check if object is being processed by
                            // someone else
                            if (m_ScriptEngine.m_EventQueueManager.TryLock(
                                    QIS.localID) == false)
                            {
                                // Object is already being processed, requeue it
                                m_ScriptEngine.m_EventQueueManager.
                                        eventQueue.Enqueue(QIS);
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
                            // Only pipe event if land supports it.
                            if (m_ScriptEngine.World.PipeEventsForScript(
                                    QIS.localID))
                            {
                                lastLocalID = QIS.localID;
                                lastItemID = QIS.itemID;
                                LastExecutionStarted = DateTime.Now.Ticks;
                                KillCurrentScript = false;
                                InExecution = true;
                                m_ScriptEngine.m_ScriptManager.ExecuteEvent(
                                    QIS.localID,
                                    QIS.itemID,
                                    QIS.functionName,
                                    QIS.llDetectParams,
                                    QIS.param);

                                InExecution = false;
                            }
                        }
                        catch (TargetInvocationException tie)
                        {
                            Exception e = tie.InnerException;

                            if (e is SelfDeleteException) // Forward it
                                throw e;

                            InExecution = false;
                            string text = FormatException(tie, QIS.LineMap);
                            
                            // DISPLAY ERROR INWORLD

//                            if (e.InnerException != null)
//                            {
//                                // Send inner exception
//                                string line = " (unknown line)";
//                                Regex rx = new Regex(@"SecondLife\.Script\..+[\s:](?<line>\d+)\.?\r?$", RegexOptions.Compiled);
//                                if (rx.Match(e.InnerException.ToString()).Success)
//                                    line = " (line " + rx.Match(e.InnerException.ToString()).Result("${line}") + ")";
//                                text += e.InnerException.Message.ToString() + line;
//                            }
//                            else
//                            {
//                                text += "\r\n";
//                                // Send normal
//                                text += e.Message.ToString();
//                            }
//                            if (KillCurrentScript)
//                                text += "\r\nScript will be deactivated!";

                            try
                            {
                                if (text.Length >= 1100)
                                    text = text.Substring(0, 1099);
                                IScriptHost m_host =
                                    m_ScriptEngine.World.GetSceneObjectPart(QIS.localID);
                                m_ScriptEngine.World.SimChat(
                                        Utils.StringToBytes(text),
                                        ChatTypeEnum.DebugChannel, 2147483647,
                                        m_host.AbsolutePosition,
                                        m_host.Name, m_host.UUID, false);
                            }
                            catch (Exception)
                            {
                                m_log.Error("[" +
                                            ScriptEngineName + "]: " +
                                            "Unable to send text in-world:\r\n" +
                                            text);
                            }
                            finally
                            {
                                // So we are done sending message in-world
                                if (KillCurrentScript)
                                {
                                    m_ScriptEngine.m_EventQueueManager.
                                            m_ScriptEngine.m_ScriptManager.
                                            StopScript(
                                        QIS.localID, QIS.itemID);
                                }
                            }
                        }
                        catch (Exception)
                        {
                            throw;
                        }
                        finally
                        {
                            InExecution = false;
                            m_ScriptEngine.m_EventQueueManager.ReleaseLock(
                                    QIS.localID);
                        }
                    }
                }
            }
        }

        string FormatException(Exception e, Dictionary<KeyValuePair<int,int>,
                KeyValuePair<int,int>> LineMap)
        {
            if (e.InnerException == null)
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

                            return message;
                        }
                    }
                }
            }

            return message;
        }
    }
}
