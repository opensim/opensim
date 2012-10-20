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
using System.Linq;
using System.Threading;
using log4net;

namespace OpenSim.Framework.Monitoring
{
    /// <summary>
    /// Manages launching threads and keeping watch over them for timeouts
    /// </summary>
    public static class Watchdog
    {
        /// <summary>Timer interval in milliseconds for the watchdog timer</summary>
        const double WATCHDOG_INTERVAL_MS = 2500.0d;

        /// <summary>Default timeout in milliseconds before a thread is considered dead</summary>
        public const int DEFAULT_WATCHDOG_TIMEOUT_MS = 5000;

        [System.Diagnostics.DebuggerDisplay("{Thread.Name}")]
        public class ThreadWatchdogInfo
        {
            public Thread Thread { get; private set; }

            /// <summary>
            /// Approximate tick when this thread was started.
            /// </summary>
            /// <remarks>
            /// Not terribly good since this quickly wraps around.
            /// </remarks>
            public int FirstTick { get; private set; }

            /// <summary>
            /// Last time this heartbeat update was invoked
            /// </summary>
            public int LastTick { get; set; }

            /// <summary>
            /// Number of milliseconds before we notify that the thread is having a problem.
            /// </summary>
            public int Timeout { get; set; }

            /// <summary>
            /// Is this thread considered timed out?
            /// </summary>
            public bool IsTimedOut { get; set; }

            /// <summary>
            /// Will this thread trigger the alarm function if it has timed out?
            /// </summary>
            public bool AlarmIfTimeout { get; set; }

            /// <summary>
            /// Method execute if alarm goes off.  If null then no alarm method is fired.
            /// </summary>
            public Func<string> AlarmMethod { get; set; }

            public ThreadWatchdogInfo(Thread thread, int timeout)
            {
                Thread = thread;
                Timeout = timeout;
                FirstTick = Environment.TickCount & Int32.MaxValue;
                LastTick = FirstTick;
            }

            public ThreadWatchdogInfo(ThreadWatchdogInfo previousTwi)
            {
                Thread = previousTwi.Thread;
                FirstTick = previousTwi.FirstTick;
                LastTick = previousTwi.LastTick;
                Timeout = previousTwi.Timeout;
                IsTimedOut = previousTwi.IsTimedOut;
                AlarmIfTimeout = previousTwi.AlarmIfTimeout;
                AlarmMethod = previousTwi.AlarmMethod;
            }
        }

        /// <summary>
        /// This event is called whenever a tracked thread is
        /// stopped or has not called UpdateThread() in time<
        /// /summary>
        public static event Action<ThreadWatchdogInfo> OnWatchdogTimeout;

        /// <summary>
        /// Is this watchdog active?
        /// </summary>
        public static bool Enabled
        {
            get { return m_enabled; }
            set
            {
//                m_log.DebugFormat("[MEMORY WATCHDOG]: Setting MemoryWatchdog.Enabled to {0}", value);

                if (value == m_enabled)
                    return;

                m_enabled = value;

                if (m_enabled)
                {
                    // Set now so we don't get alerted on the first run
                    LastWatchdogThreadTick = Environment.TickCount & Int32.MaxValue;
                }

                m_watchdogTimer.Enabled = m_enabled;
            }
        }
        private static bool m_enabled;

        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static Dictionary<int, ThreadWatchdogInfo> m_threads;
        private static System.Timers.Timer m_watchdogTimer;

        /// <summary>
        /// Last time the watchdog thread ran.
        /// </summary>
        /// <remarks>
        /// Should run every WATCHDOG_INTERVAL_MS
        /// </remarks>
        public static int LastWatchdogThreadTick { get; private set; }

        static Watchdog()
        {
            m_threads = new Dictionary<int, ThreadWatchdogInfo>();
            m_watchdogTimer = new System.Timers.Timer(WATCHDOG_INTERVAL_MS);
            m_watchdogTimer.AutoReset = false;
            m_watchdogTimer.Elapsed += WatchdogTimerElapsed;
        }

        /// <summary>
        /// Start a new thread that is tracked by the watchdog timer.
        /// </summary>
        /// <param name="start">The method that will be executed in a new thread</param>
        /// <param name="name">A name to give to the new thread</param>
        /// <param name="priority">Priority to run the thread at</param>
        /// <param name="isBackground">True to run this thread as a background thread, otherwise false</param>
        /// <param name="alarmIfTimeout">Trigger an alarm function is we have timed out</param>
        /// <returns>The newly created Thread object</returns>
        public static Thread StartThread(
            ThreadStart start, string name, ThreadPriority priority, bool isBackground, bool alarmIfTimeout)
        {
            return StartThread(start, name, priority, isBackground, alarmIfTimeout, null, DEFAULT_WATCHDOG_TIMEOUT_MS);
        }

        /// <summary>
        /// Start a new thread that is tracked by the watchdog timer
        /// </summary>
        /// <param name="start">The method that will be executed in a new thread</param>
        /// <param name="name">A name to give to the new thread</param>
        /// <param name="priority">Priority to run the thread at</param>
        /// <param name="isBackground">True to run this thread as a background
        /// thread, otherwise false</param>
        /// <param name="alarmIfTimeout">Trigger an alarm function is we have timed out</param>
        /// <param name="alarmMethod">
        /// Alarm method to call if alarmIfTimeout is true and there is a timeout.
        /// Normally, this will just return some useful debugging information.
        /// </param>
        /// <param name="timeout">Number of milliseconds to wait until we issue a warning about timeout.</param>
        /// <returns>The newly created Thread object</returns>
        public static Thread StartThread(
            ThreadStart start, string name, ThreadPriority priority, bool isBackground,
            bool alarmIfTimeout, Func<string> alarmMethod, int timeout)
        {
            Thread thread = new Thread(start);
            thread.Name = name;
            thread.Priority = priority;
            thread.IsBackground = isBackground;
            
            ThreadWatchdogInfo twi
                = new ThreadWatchdogInfo(thread, timeout)
                    { AlarmIfTimeout = alarmIfTimeout, AlarmMethod = alarmMethod };

            m_log.DebugFormat(
                "[WATCHDOG]: Started tracking thread {0}, ID {1}", twi.Thread.Name, twi.Thread.ManagedThreadId);

            lock (m_threads)
                m_threads.Add(twi.Thread.ManagedThreadId, twi);

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
        /// <returns>
        /// True if the thread was removed from the list of tracked
        /// threads, otherwise false
        /// </returns>
        public static bool RemoveThread()
        {
            return RemoveThread(Thread.CurrentThread.ManagedThreadId);
        }

        private static bool RemoveThread(int threadID)
        {
            lock (m_threads)
            {
                ThreadWatchdogInfo twi;
                if (m_threads.TryGetValue(threadID, out twi))
                {
                    m_log.DebugFormat(
                        "[WATCHDOG]: Removing thread {0}, ID {1}", twi.Thread.Name, twi.Thread.ManagedThreadId);

                    m_threads.Remove(threadID);

                    return true;
                }
                else
                {
                    m_log.WarnFormat(
                        "[WATCHDOG]: Requested to remove thread with ID {0} but this is not being monitored", threadID);

                    return false;
                }
            }
        }

        public static bool AbortThread(int threadID)
        {
            lock (m_threads)
            {
                if (m_threads.ContainsKey(threadID))
                {
                    ThreadWatchdogInfo twi = m_threads[threadID];
                    twi.Thread.Abort();
                    RemoveThread(threadID);

                    return true;
                }
                else
                {
                    return false;
                }
            }
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
                {
                    threadInfo.LastTick = Environment.TickCount & Int32.MaxValue;
                    threadInfo.IsTimedOut = false;
                }
                else
                {
                    m_log.WarnFormat("[WATCHDOG]: Asked to update thread {0} which is not being monitored", threadID);
                }
            }
            catch { }
        }
        
        /// <summary>
        /// Get currently watched threads for diagnostic purposes
        /// </summary>
        /// <returns></returns>
        public static ThreadWatchdogInfo[] GetThreadsInfo()
        {
            lock (m_threads)
                return m_threads.Values.ToArray();
        }

        /// <summary>
        /// Return the current thread's watchdog info.
        /// </summary>
        /// <returns>The watchdog info.  null if the thread isn't being monitored.</returns>
        public static ThreadWatchdogInfo GetCurrentThreadInfo()
        {
            lock (m_threads)
            {
                if (m_threads.ContainsKey(Thread.CurrentThread.ManagedThreadId))
                    return m_threads[Thread.CurrentThread.ManagedThreadId];
            }

            return null;
        }

        /// <summary>
        /// Check watched threads.  Fire alarm if appropriate.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void WatchdogTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            int now = Environment.TickCount & Int32.MaxValue;
            int msElapsed = now - LastWatchdogThreadTick;

            if (msElapsed > WATCHDOG_INTERVAL_MS * 2)
                m_log.WarnFormat(
                    "[WATCHDOG]: {0} ms since Watchdog last ran.  Interval should be approximately {1} ms",
                    msElapsed, WATCHDOG_INTERVAL_MS);

            LastWatchdogThreadTick = Environment.TickCount & Int32.MaxValue;

            Action<ThreadWatchdogInfo> callback = OnWatchdogTimeout;

            if (callback != null)
            {
                List<ThreadWatchdogInfo> callbackInfos = null;

                lock (m_threads)
                {
                    foreach (ThreadWatchdogInfo threadInfo in m_threads.Values)
                    {
                        if (threadInfo.Thread.ThreadState == ThreadState.Stopped)
                        {
                            RemoveThread(threadInfo.Thread.ManagedThreadId);

                            if (callbackInfos == null)
                                callbackInfos = new List<ThreadWatchdogInfo>();

                            callbackInfos.Add(threadInfo);
                        }
                        else if (!threadInfo.IsTimedOut && now - threadInfo.LastTick >= threadInfo.Timeout)
                        {
                            threadInfo.IsTimedOut = true;

                            if (threadInfo.AlarmIfTimeout)
                            {
                                if (callbackInfos == null)
                                    callbackInfos = new List<ThreadWatchdogInfo>();

                                // Send a copy of the watchdog info to prevent race conditions where the watchdog
                                // thread updates the monitoring info after an alarm has been sent out.
                                callbackInfos.Add(new ThreadWatchdogInfo(threadInfo));
                            }
                        }
                    }
                }

                if (callbackInfos != null)
                    foreach (ThreadWatchdogInfo callbackInfo in callbackInfos)
                        callback(callbackInfo);
            }

            if (MemoryWatchdog.Enabled)
                MemoryWatchdog.Update();

            m_watchdogTimer.Start();
        }
    }
}