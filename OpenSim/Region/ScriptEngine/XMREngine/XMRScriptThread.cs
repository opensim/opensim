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

using OpenSim.Framework.Monitoring;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OpenSim.Region.ScriptEngine.XMREngine
{

    /**
     * @brief There are NUMSCRIPTHREADWKRS of these.
     *        Each sits in a loop checking the Start and Yield queues for 
     *        a script to run and calls the script as a microthread.
     */
    public class XMRScriptThread
    {
        private static int    m_WakeUpOne  = 0;
        public  static object m_WakeUpLock = new object();
        private static Dictionary<Thread,XMRScriptThread> m_AllThreads = new Dictionary<Thread,XMRScriptThread> ();

        /**
         * @brief Something was just added to the Start or Yield queue so
         *        wake one of the XMRScriptThread instances to run it.
         */
        public static void WakeUpOne()
        {
            lock (m_WakeUpLock)
            {
                m_WakeUpOne ++;
                Monitor.Pulse (m_WakeUpLock);
            }
        }

        public static XMRScriptThread CurrentScriptThread ()
        {
            XMRScriptThread st;
            lock (m_AllThreads) {
                m_AllThreads.TryGetValue (Thread.CurrentThread, out st);
            }
            return st;
        }

        private bool        m_Exiting = false;
        private bool        m_SuspendScriptThreadFlag = false;
        private bool        m_WakeUpThis = false;
        public  DateTime    m_LastRanAt = DateTime.MinValue;
        public  int         m_ScriptThreadTID = 0;
        public  long        m_ScriptExecTime = 0;
        private Thread      thd;
        private XMREngine   engine;
        public  XMRInstance m_RunInstance = null;

        public XMRScriptThread(XMREngine eng, int i)
        {
            engine = eng;
            if(i < 0)
                thd = XMREngine.StartMyThread (RunScriptThread, "xmrengine script", ThreadPriority.Normal);
            else
                thd = XMREngine.StartMyThread (RunScriptThread, "xmrengineExec" + i.ToString(), ThreadPriority.Normal);
            lock (m_AllThreads)
                m_AllThreads.Add (thd, this);
        }

        public void SuspendThread()
        {
            m_SuspendScriptThreadFlag = true;
            WakeUpScriptThread();
        }

        public void ResumeThread()
        {
            m_SuspendScriptThreadFlag = false;
            WakeUpScriptThread();
        }

        public void Terminate()
        {
            m_Exiting = true;
            WakeUpScriptThread();
            if(!thd.Join(250))
                thd.Abort();
            lock (m_AllThreads)
                m_AllThreads.Remove (thd);

            thd = null;
        }

        public void TimeSlice()
        {
            XMRInstance instance = m_RunInstance;
            if (instance != null)
                instance.suspendOnCheckRunTemp = true;
        }

        /**
         * @brief Wake up this XMRScriptThread instance.
         */
        private void WakeUpScriptThread()
        {
            lock (m_WakeUpLock)
            {
                m_WakeUpThis = true;
                Monitor.PulseAll (m_WakeUpLock);
            }
        }

        /**
         * @brief Thread that runs the scripts.
         */
        private void RunScriptThread()
        {
            XMRInstance inst;
            m_ScriptThreadTID = System.Threading.Thread.CurrentThread.ManagedThreadId;

            while (!m_Exiting)
            {
                XMREngine.UpdateMyThread ();

                /*
                 * Handle 'xmr resume/suspend' commands.
                 */
                if (m_SuspendScriptThreadFlag)
                {
                    lock (m_WakeUpLock) {
                        while (m_SuspendScriptThreadFlag &&
                               !m_Exiting &&
                               (engine.m_ThunkQueue.Count == 0))
                        {
                            Monitor.Wait (m_WakeUpLock, Watchdog.DEFAULT_WATCHDOG_TIMEOUT_MS / 2);
                            XMREngine.UpdateMyThread ();
                        }
                    }
                }

                /*
                 * Maybe there are some scripts waiting to be migrated in or out.
                 */
                ThreadStart thunk = null;
                lock (m_WakeUpLock)
                {
                    if (engine.m_ThunkQueue.Count > 0)
                        thunk = engine.m_ThunkQueue.Dequeue ();
                }
                if (thunk != null)
                {
                    inst = (XMRInstance)thunk.Target;
                    thunk ();
                    continue;
                }

                if (engine.m_StartProcessing)
                {
                     // If event just queued to any idle scripts
                     // start them right away.  But only start so
                     // many so we can make some progress on yield
                     // queue.

                    int numStarts;
                    for (numStarts = 5; -- numStarts >= 0;)
                    {
                        lock (engine.m_StartQueue)
                        {
                            inst = engine.m_StartQueue.RemoveHead();
                        }
                        if (inst == null) break;
                        if (inst.m_IState != XMRInstState.ONSTARTQ) throw new Exception("bad state");
                        RunInstance (inst);
                    }

                     // If there is something to run, run it
                     // then rescan from the beginning in case
                     // a lot of things have changed meanwhile.
                     //
                     // These are considered lower priority than
                     // m_StartQueue as they have been taking at
                     // least one quantum of CPU time and event
                     // handlers are supposed to be quick.

                    lock (engine.m_YieldQueue)
                    {
                        inst = engine.m_YieldQueue.RemoveHead();
                    }
                    if (inst != null)
                    {
                        if (inst.m_IState != XMRInstState.ONYIELDQ) throw new Exception("bad state");
                        RunInstance(inst);
                        numStarts = -1;
                    }

                     // If we left something dangling in the m_StartQueue or m_YieldQueue, go back to check it.
                    if (numStarts < 0)
                        continue;
                }

                 // Nothing to do, sleep.

                lock (m_WakeUpLock)
                {
                    if (!m_WakeUpThis && (m_WakeUpOne <= 0) && !m_Exiting)
                        Monitor.Wait(m_WakeUpLock, Watchdog.DEFAULT_WATCHDOG_TIMEOUT_MS / 2);

                    m_WakeUpThis = false;
                    if ((m_WakeUpOne > 0) && (-- m_WakeUpOne > 0))
                        Monitor.Pulse (m_WakeUpLock);
                }
            }
            XMREngine.MyThreadExiting ();
        }

        /**
         * @brief A script instance was just removed from the Start or Yield Queue.
         *        So run it for a little bit then stick in whatever queue it should go in.
         */
        private void RunInstance (XMRInstance inst)
        {
            m_LastRanAt = DateTime.UtcNow;
            m_ScriptExecTime -= (long)(m_LastRanAt - DateTime.MinValue).TotalMilliseconds;
            inst.m_IState = XMRInstState.RUNNING;
            m_RunInstance = inst;
            XMRInstState newIState = inst.RunOne();
            m_RunInstance = null;
            engine.HandleNewIState(inst, newIState);
            m_ScriptExecTime += (long)(DateTime.UtcNow - DateTime.MinValue).TotalMilliseconds;
        }
    }
}
