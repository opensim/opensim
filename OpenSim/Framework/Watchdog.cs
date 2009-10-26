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
using System.Collections.Generic;
using System.Threading;
using log4net;

namespace OpenSim.Framework
{
    /// <summary>
    /// Manages launching threads and keeping watch over them for timeouts
    /// </summary>
    public static class Watchdog
    {
        /// <summary>Timer interval in milliseconds for the watchdog timer</summary>
        const double WATCHDOG_INTERVAL_MS = 2500.0d;
        /// <summary>Maximum timeout in milliseconds before a thread is considered dead</summary>
        const int WATCHDOG_TIMEOUT_MS = 5000;

        [System.Diagnostics.DebuggerDisplay("{Thread.Name}")]
        private class ThreadWatchdogInfo
        {
            public Thread Thread;
            public int LastTick;

            public ThreadWatchdogInfo(Thread thread)
            {
                Thread = thread;
                LastTick = Environment.TickCount & Int32.MaxValue;
            }
        }

        /// <summary>
        /// This event is called whenever a tracked thread is stopped or
        /// has not called UpdateThread() in time
        /// </summary>
        /// <param name="thread">The thread that has been identified as dead</param>
        /// <param name="lastTick">The last time this thread called UpdateThread()</param>
        public delegate void WatchdogTimeout(Thread thread, int lastTick);

        /// <summary>This event is called whenever a tracked thread is
        /// stopped or has not called UpdateThread() in time</summary>
        public static event WatchdogTimeout OnWatchdogTimeout;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static Dictionary<int, ThreadWatchdogInfo> m_threads;
        private static System.Timers.Timer m_watchdogTimer;

        static Watchdog()
        {
            m_threads = new Dictionary<int, ThreadWatchdogInfo>();
            m_watchdogTimer = new System.Timers.Timer(WATCHDOG_INTERVAL_MS);
            m_watchdogTimer.AutoReset = false;
            m_watchdogTimer.Elapsed += WatchdogTimerElapsed;
            m_watchdogTimer.Start();
        }

        /// <summary>
        /// Start a new thread that is tracked by the watchdog timer
        /// </summary>
        /// <param name="start">The method that will be executed in a new thread</param>
        /// <param name="name">A name to give to the new thread</param>
        /// <param name="priority">Priority to run the thread at</param>
        /// <param name="isBackground">True to run this thread as a background
        /// thread, otherwise false</param>
        /// <returns>The newly created Thread object</returns>
        public static Thread StartThread(ThreadStart start, string name, ThreadPriority priority, bool isBackground)
        {
            Thread thread = new Thread(start);
            thread.Name = name;
            thread.Priority = priority;
            thread.IsBackground = isBackground;
            thread.Start();

            return thread;
        }

        /// <summary>
        /// Marks the current thread as alive
        /// </summary>
        public static void UpdateThread()
        {
            UpdateThread(Thread.CurrentThread.ManagedThreadId);
        }

        /// <summary>
        /// Stops watchdog tracking on the current thread
        /// </summary>
        /// <returns>True if the thread was removed from the list of tracked
        /// threads, otherwise false</returns>
        public static bool RemoveThread()
        {
            return RemoveThread(Thread.CurrentThread.ManagedThreadId);
        }

        private static void AddThread(ThreadWatchdogInfo threadInfo)
        {
            m_log.Debug("[WATCHDOG]: Started tracking thread \"" + threadInfo.Thread.Name + "\" (ID " + threadInfo.Thread.ManagedThreadId + ")");

            lock (m_threads)
                m_threads.Add(threadInfo.Thread.ManagedThreadId, threadInfo);
        }

        private static bool RemoveThread(int threadID)
        {
            lock (m_threads)
                return m_threads.Remove(threadID);
        }

        private static void UpdateThread(int threadID)
        {
            ThreadWatchdogInfo threadInfo;

            // Although TryGetValue is not a thread safe operation, we use a try/catch here instead
            // of a lock for speed. Adding/removing threads is a very rare operation compared to
            // UpdateThread(), and a single UpdateThread() failure here and there won't break
            // anything
            try
            {
                if (m_threads.TryGetValue(threadID, out threadInfo))
                    threadInfo.LastTick = Environment.TickCount & Int32.MaxValue;
                else
                    AddThread(new ThreadWatchdogInfo(Thread.CurrentThread));
            }
            catch { }
        }

        private static void WatchdogTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            WatchdogTimeout callback = OnWatchdogTimeout;

            if (callback != null)
            {
                ThreadWatchdogInfo timedOut = null;

                lock (m_threads)
                {
                    int now = Environment.TickCount & Int32.MaxValue;

                    foreach (ThreadWatchdogInfo threadInfo in m_threads.Values)
                    {
                        if (threadInfo.Thread.ThreadState == ThreadState.Stopped || now - threadInfo.LastTick >= WATCHDOG_TIMEOUT_MS)
                        {
                            timedOut = threadInfo;
                            m_threads.Remove(threadInfo.Thread.ManagedThreadId);
                            break;
                        }
                    }
                }

                if (timedOut != null)
                    callback(timedOut.Thread, timedOut.LastTick);
            }

            m_watchdogTimer.Start();
        }
    }
}
