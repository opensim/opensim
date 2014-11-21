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
        public const double WATCHDOG_INTERVAL_MS = 2500.0d;

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

            /// <summary>
            /// Stat structure associated with this thread.
            /// </summary>
            public Stat Stat { get; set; }

            public ThreadWatchdogInfo(Thread thread, int timeout, string name)
            {
                Thread = thread;
                Timeout = timeout;
                FirstTick = Environment.TickCount & Int32.MaxValue;
                LastTick = FirstTick;

                Stat 
                    = new Stat(
                        name,
                        string.Format("Last update of thread {0}", name),
                        "",
                        "ms",
                        "server",
                        "thread",
                        StatType.Pull,
                        MeasuresOfInterest.None,
                        stat => stat.Value = Environment.TickCount & Int32.MaxValue - LastTick,
                        StatVerbosity.Debug);

                StatsManager.RegisterStat(Stat);
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

            public void Cleanup()
            {
                StatsManager.DeregisterStat(Stat);
            }
        }

        /// <summary>
        /// This event is called whenever a tracked thread is
        /// stopped or has not called UpdateThread() in time<
        /// /summary>
        public static event Action<ThreadWatchdogInfo> OnWatchdogTimeout;

        public static JobEngine JobEngine { get; private set; }

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
            JobEngine = new JobEngine();
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
        /// <param name="log">If true then creation of thread is logged.</param>
        /// <returns>The newly created Thread object</returns>
        public static Thread StartThread(
            ThreadStart start, string name, ThreadPriority priority, bool isBackground, bool alarmIfTimeout, bool log = true)
        {
            return StartThread(start, name, priority, isBackground, alarmIfTimeout, null, DEFAULT_WATCHDOG_TIMEOUT_MS, log);
        }

        /// <summary>
        /// Start a new thread that is tracked by the watchdog
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
        /// <param name="log">If true then creation of thread is logged.</param>
        /// <returns>The newly created Thread object</returns>
        public static Thread StartThread(
            ThreadStart start, string name, ThreadPriority priority, bool isBackground,
            bool alarmIfTimeout, Func<string> alarmMethod, int timeout, bool log = true)
        {
            Thread thread = new Thread(start);
            thread.Priority = priority;
            thread.IsBackground = isBackground;
            
            ThreadWatchdogInfo twi
                = new ThreadWatchdogInfo(thread, timeout, name)
                    { AlarmIfTimeout = alarmIfTimeout, AlarmMethod = alarmMethod };

            if (log)
                m_log.DebugFormat(
                    "[WATCHDOG]: Started tracking thread {0}, ID {1}", name, twi.Thread.ManagedThreadId);

            lock (m_threads)
                m_threads.Add(twi.Thread.ManagedThreadId, twi);
            
            thread.Start();
            thread.Name = name;


            return thread;
        }

        /// <summary>
        /// Run the callback in a new thread immediately.  If the thread exits with an exception log it but do
        /// not propogate it.
        /// </summary>
        /// <param name="callback">Code for the thread to execute.</param>
        /// <param name="name">Name of the thread</param>
        /// <param name="obj">Object to pass to the thread.</param>
        public static void RunInThread(WaitCallback callback, string name, object obj, bool log = false)
        {
            if (Util.FireAndForgetMethod == FireAndForgetMethod.RegressionTest)           
            {
                Culture.SetCurrentCulture();
                callback(obj);
                return;
            }

            ThreadStart ts = new ThreadStart(delegate()
            {
                try
                {
                    Culture.SetCurrentCulture();
                    callback(obj);
                    Watchdog.RemoveThread(log:false);
                }
                catch (Exception e)
                {
                    m_log.Error(string.Format("[WATCHDOG]: Exception in thread {0}.", name), e);
                }
            });

            StartThread(ts, name, ThreadPriority.Normal, true, false, log:log);
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
        /// <param name="log">If true then normal events in thread removal are not logged.</param>
        /// <returns>
        /// True if the thread was removed from the list of tracked
        /// threads, otherwise false
        /// </returns>
        public static bool RemoveThread(bool log = true)
        {
            return RemoveThread(Thread.CurrentThread.ManagedThreadId, log);
        }

        private static bool RemoveThread(int threadID, bool log = true)
        {
            lock (m_threads)
            {
                ThreadWatchdogInfo twi;
                if (m_threads.TryGetValue(threadID, out twi))
                {
                    if (log)
                        m_log.DebugFormat(
                            "[WATCHDOG]: Removing thread {0}, ID {1}", twi.Thread.Name, twi.Thread.ManagedThreadId);

                    twi.Cleanup();
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

            ChecksManager.CheckChecks();
            StatsManager.RecordStats();

            m_watchdogTimer.Start();
        }

        /// <summary>
        /// Run a job.
        /// </summary>
        /// <remarks>
        /// This differs from direct scheduling (e.g. Util.FireAndForget) in that a job can be run in the job
        /// engine if it is running, where all jobs are currently performed in sequence on a single thread.  This is
        /// to prevent observed overload and server freeze problems when there are hundreds of connections which all attempt to 
        /// perform work at once (e.g. in conference situations).  With lower numbers of connections, the small
        /// delay in performing jobs in sequence rather than concurrently has not been notiecable in testing, though a future more 
        /// sophisticated implementation could perform jobs concurrently when the server is under low load.
        /// 
        /// However, be advised that some callers of this function rely on all jobs being performed in sequence if any
        /// jobs are performed in sequence (i.e. if jobengine is active or not).  Therefore, expanding the jobengine
        /// beyond a single thread will require considerable thought.
        /// 
        /// Also, any jobs submitted must be guaranteed to complete within a reasonable timeframe (e.g. they cannot
        /// incorporate a network delay with a long timeout).  At the moment, work that could suffer such issues 
        /// should still be run directly with RunInThread(), Util.FireAndForget(), etc.  This is another area where
        /// the job engine could be improved and so CPU utilization improved by better management of concurrency within
        /// OpenSimulator.
        /// </remarks>
        /// <param name="jobType">General classification for the job (e.g. "RezAttachments").</param>
        /// <param name="callback">Callback for job.</param>
        /// <param name="name">Specific name of job (e.g. "RezAttachments for Joe Bloggs"</param>
        /// <param name="obj">Object to pass to callback when run</param>
        /// <param name="canRunInThisThread">If set to true then the job may be run in ths calling thread.</param>
        /// <param name="mustNotTimeout">If the true then the job must never timeout.</param>
        /// <param name="log">If set to true then extra logging is performed.</param>
        public static void RunJob(
            string jobType, WaitCallback callback, string name, object obj, 
            bool canRunInThisThread = false, bool mustNotTimeout = false, 
            bool log = false)
        {
            if (Util.FireAndForgetMethod == FireAndForgetMethod.RegressionTest)           
            {
                Culture.SetCurrentCulture();
                callback(obj);
                return;
            }

            if (JobEngine.IsRunning)
                JobEngine.QueueRequest(name, callback, obj);
            else if (canRunInThisThread)
                callback(obj);
            else if (mustNotTimeout)
                RunInThread(callback, name, obj, log);
            else
                Util.FireAndForget(callback, obj, name);
        }
    }
}