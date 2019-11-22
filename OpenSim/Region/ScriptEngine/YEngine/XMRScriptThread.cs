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

namespace OpenSim.Region.ScriptEngine.Yengine
{

    public partial class Yengine
    {
        private int m_WakeUpOne = 0;
        public object m_WakeUpLock = new object();

        private Dictionary<int, XMRInstance> m_RunningInstances = new Dictionary<int, XMRInstance>();

        private bool m_SuspendScriptThreadFlag = false;
        private bool m_WakeUpThis = false;
        public DateTime m_LastRanAt = DateTime.MinValue;
        public long m_ScriptExecTime = 0;

        [ThreadStatic]
        private static int m_ScriptThreadTID;

        public static bool IsScriptThread
        {
            get
            {
                return m_ScriptThreadTID != 0;
            }
        }

        public void StartThreadWorker(int i, ThreadPriority priority, string sceneName)
        {
            Thread thd;
            if(i >= 0)
                thd = Yengine.StartMyThread(RunScriptThread, "YScript" + i.ToString() + " (" + sceneName +")", priority);
            else
                thd = Yengine.StartMyThread(RunScriptThread, "YScript", priority);
            lock(m_WakeUpLock)
                m_RunningInstances.Add(thd.ManagedThreadId, null);
        }

        public void StopThreadWorkers()
        {
            lock(m_WakeUpLock)
            {
                while(m_RunningInstances.Count != 0)
                {
                    Monitor.PulseAll(m_WakeUpLock);
                    Monitor.Wait(m_WakeUpLock, Watchdog.DEFAULT_WATCHDOG_TIMEOUT_MS / 2);
                }
            }
        }

        /**
         * @brief Something was just added to the Start or Yield queue so
         *        wake one of the RunScriptThread() instances to run it.
         */
        public void WakeUpOne()
        {
            lock(m_WakeUpLock)
            {
                m_WakeUpOne++;
                Monitor.Pulse(m_WakeUpLock);
            }
        }

        public void SuspendThreads()
        {
            lock(m_WakeUpLock)
            {
                m_SuspendScriptThreadFlag = true;
                Monitor.PulseAll(m_WakeUpLock);
            }
        }

        public void ResumeThreads()
        {
            lock(m_WakeUpLock)
            {
                m_SuspendScriptThreadFlag = false;
                Monitor.PulseAll(m_WakeUpLock);
            }
        }

        /**
         * @brief Thread that runs the scripts.
         *
         *        There are NUMSCRIPTHREADWKRS of these.
         *        Each sits in a loop checking the Start and Yield queues for 
         *        a script to run and calls the script as a microthread.
         */
        private void RunScriptThread()
        {
            int tid = System.Threading.Thread.CurrentThread.ManagedThreadId;
            ThreadStart thunk;
            XMRInstance inst;
            bool didevent;
            m_ScriptThreadTID = tid;

            while(!m_Exiting)
            {
                Yengine.UpdateMyThread();

                lock(m_WakeUpLock)
                {
                    // Maybe there are some scripts waiting to be migrated in or out.
                    thunk = null;
                    if(m_ThunkQueue.Count > 0)
                        thunk = m_ThunkQueue.Dequeue();

                    // Handle 'xmr resume/suspend' commands.
                    else if(m_SuspendScriptThreadFlag && !m_Exiting)
                    {
                        Monitor.Wait(m_WakeUpLock, Watchdog.DEFAULT_WATCHDOG_TIMEOUT_MS / 2);
                        Yengine.UpdateMyThread();
                        continue;
                    }
                }

                if(thunk != null)
                {
                    thunk();
                    continue;
                }

                if(m_StartProcessing)
                {
                    // If event just queued to any idle scripts
                    // start them right away.  But only start so
                    // many so we can make some progress on yield
                    // queue.
                    int numStarts;
                    didevent = false;
                    for(numStarts = 5; numStarts >= 0; --numStarts)
                    {
                        lock(m_StartQueue)
                            inst = m_StartQueue.RemoveHead();

                        if(inst == null)
                            break;
                        if (inst.m_IState == XMRInstState.SUSPENDED)
                            continue;
                        if (inst.m_IState != XMRInstState.ONSTARTQ)
                            throw new Exception("bad state");
                        RunInstance(inst, tid);
                        if(m_SuspendScriptThreadFlag || m_Exiting)
                            continue;
                        didevent = true;
                    }

                    // If there is something to run, run it
                    // then rescan from the beginning in case
                    // a lot of things have changed meanwhile.
                    //
                    // These are considered lower priority than
                    // m_StartQueue as they have been taking at
                    // least one quantum of CPU time and event
                    // handlers are supposed to be quick.
                    lock(m_YieldQueue)
                        inst = m_YieldQueue.RemoveHead();

                    if(inst != null)
                    {
                        if (inst.m_IState == XMRInstState.SUSPENDED)
                            continue;
                        if (inst.m_IState != XMRInstState.ONYIELDQ)
                            throw new Exception("bad state");
                        RunInstance(inst, tid);
                        continue;
                    }

                    // If we left something dangling in the m_StartQueue or m_YieldQueue, go back to check it.
                    if(didevent)
                        continue;
                }

                // Nothing to do, sleep.
                lock(m_WakeUpLock)
                {
                    if(!m_WakeUpThis && (m_WakeUpOne <= 0) && !m_Exiting)
                        Monitor.Wait(m_WakeUpLock, Watchdog.DEFAULT_WATCHDOG_TIMEOUT_MS / 2);

                    m_WakeUpThis = false;
                    if((m_WakeUpOne > 0) && (--m_WakeUpOne > 0))
                        Monitor.Pulse(m_WakeUpLock);
                }
            }
            lock(m_WakeUpLock)
                m_RunningInstances.Remove(tid);

            Yengine.MyThreadExiting();
        }

        /**
         * @brief A script instance was just removed from the Start or Yield Queue.
         *        So run it for a little bit then stick in whatever queue it should go in.
         */
        private void RunInstance(XMRInstance inst, int tid)
        {
            m_LastRanAt = DateTime.UtcNow;
            m_ScriptExecTime -= (long)(m_LastRanAt - DateTime.MinValue).TotalMilliseconds;
            inst.m_IState = XMRInstState.RUNNING;

            lock(m_WakeUpLock)
                m_RunningInstances[tid] = inst;

            XMRInstState newIState = inst.RunOne();

            lock(m_WakeUpLock)
                m_RunningInstances[tid] = null;

            HandleNewIState(inst, newIState);
            m_ScriptExecTime += (long)(DateTime.UtcNow - DateTime.MinValue).TotalMilliseconds;
        }
    }
}
