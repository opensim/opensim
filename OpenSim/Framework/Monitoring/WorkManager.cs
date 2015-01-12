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
using System.Reflection;
using System.Threading;
using log4net;

namespace OpenSim.Framework.Monitoring
{
    /// <summary>
    /// Manages various work items in the simulator.
    /// </summary>
    /// <remarks>
    /// Currently, here work can be started 
    ///  * As a long-running and monitored thread.
    ///  * In a thread that will never timeout but where the job is expected to eventually complete.
    ///  * In a threadpool thread that will timeout if it takes a very long time to complete (> 10 mins).
    ///  * As a job which will be run in a single-threaded job engine.  Such jobs must not incorporate delays (sleeps,
    /// network waits, etc.).
    /// 
    /// This is an evolving approach to better manage the work that OpenSimulator is asked to do from a very diverse
    /// range of sources (client actions, incoming network, outgoing network calls, etc.).
    /// 
    /// Util.FireAndForget is still available to insert jobs in the threadpool, though this is equivalent to
    /// WorkManager.RunInThreadPool().
    /// </remarks>
    public static class WorkManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static JobEngine JobEngine { get; private set; }

        static WorkManager()
        {
            JobEngine = new JobEngine("Non-blocking non-critical job engine", "JOB ENGINE");

            StatsManager.RegisterStat(
                new Stat(
                    "JobsWaiting",
                    "Number of jobs waiting for processing.",
                    "",
                    "",
                    "server",
                    "jobengine",
                    StatType.Pull,
                    MeasuresOfInterest.None,
                    stat => stat.Value = JobEngine.JobsWaiting,
                    StatVerbosity.Debug));

            MainConsole.Instance.Commands.AddCommand(
                "Debug",
                false,
                "debug jobengine",
                "debug jobengine <start|stop|status|log>",
                "Start, stop, get status or set logging level of the job engine.",
                "If stopped then all outstanding jobs are processed immediately.",
                HandleControlCommand);
        }

        /// <summary>
        /// Start a new long-lived thread.
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
            return StartThread(start, name, priority, isBackground, alarmIfTimeout, null, Watchdog.DEFAULT_WATCHDOG_TIMEOUT_MS, log);
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

            Watchdog.ThreadWatchdogInfo twi
                = new Watchdog.ThreadWatchdogInfo(thread, timeout, name)
            { AlarmIfTimeout = alarmIfTimeout, AlarmMethod = alarmMethod };

            Watchdog.AddThread(twi, name, log:log);

            thread.Start();
            thread.Name = name;

            return thread;
        }

        /// <summary>
        /// Run the callback in a new thread immediately.  If the thread exits with an exception log it but do
        /// not propogate it.
        /// </summary>
        /// <param name="callback">Code for the thread to execute.</param>
        /// <param name="obj">Object to pass to the thread.</param>
        /// <param name="name">Name of the thread</param>
        public static void RunInThread(WaitCallback callback, object obj, string name, bool log = false)
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
        /// Run the callback via a threadpool thread. 
        /// </summary>
        /// <remarks>
        /// Such jobs may run after some delay but must always complete.
        /// </remarks>
        /// <param name="callback"></param>
        /// <param name="obj"></param>
        /// <param name="name">The name of the job.  This is used in monitoring and debugging.</param>
        public static void RunInThreadPool(System.Threading.WaitCallback callback, object obj, string name)
        {
            Util.FireAndForget(callback, obj, name);
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
        /// <param name="obj">Object to pass to callback when run</param>
        /// <param name="name">Specific name of job (e.g. "RezAttachments for Joe Bloggs"</param>
        /// <param name="canRunInThisThread">If set to true then the job may be run in ths calling thread.</param>
        /// <param name="mustNotTimeout">If the true then the job must never timeout.</param>
        /// <param name="log">If set to true then extra logging is performed.</param>
        public static void RunJob(
            string jobType, WaitCallback callback, object obj, string name,
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
                JobEngine.QueueJob(name, () => callback(obj));
            else if (canRunInThisThread)
                callback(obj);
            else if (mustNotTimeout)
                RunInThread(callback, obj, name, log);
            else
                Util.FireAndForget(callback, obj, name);
        }

        private static void HandleControlCommand(string module, string[] args)
        {
            //            if (SceneManager.Instance.CurrentScene != null && SceneManager.Instance.CurrentScene != m_udpServer.Scene)
            //                return;

            if (args.Length < 3)
            {
                MainConsole.Instance.Output("Usage: debug jobengine <stop|start|status|log>");
                return;
            }

            string subCommand = args[2];

            if (subCommand == "stop")
            {
                JobEngine.Stop();
                MainConsole.Instance.OutputFormat("Stopped job engine.");
            }
            else if (subCommand == "start")
            {
                JobEngine.Start();
                MainConsole.Instance.OutputFormat("Started job engine.");
            }
            else if (subCommand == "status")
            {
                MainConsole.Instance.OutputFormat("Job engine running: {0}", JobEngine.IsRunning);

                JobEngine.Job job = JobEngine.CurrentJob;
                MainConsole.Instance.OutputFormat("Current job {0}", job != null ? job.Name : "none");

                MainConsole.Instance.OutputFormat(
                    "Jobs waiting: {0}", JobEngine.IsRunning ? JobEngine.JobsWaiting.ToString() : "n/a");
                MainConsole.Instance.OutputFormat("Log Level: {0}", JobEngine.LogLevel);
            }
            else if (subCommand == "log")
            {
                //                int logLevel;
                int logLevel = int.Parse(args[3]);
                //                if (ConsoleUtil.TryParseConsoleInt(MainConsole.Instance, args[4], out logLevel))
                //                {                 
                JobEngine.LogLevel = logLevel;
                MainConsole.Instance.OutputFormat("Set debug log level to {0}", JobEngine.LogLevel);
                //                }
            }
            else 
            {
                MainConsole.Instance.OutputFormat("Unrecognized job engine subcommand {0}", subCommand);
            }
        }
    }
}