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
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using log4net;
using OpenSim.Framework;

namespace OpenSim.Framework.Monitoring
{
    public class JobEngine
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public int LogLevel { get; set; }

        private object JobLock = new object();

        public string Name { get; private set; }

        public string LoggingName { get; private set; }

        /// <summary>
        /// Is this engine running?
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// The current job that the engine is running.
        /// </summary>
        /// <remarks>
        /// Will be null if no job is currently running.
        /// </remarks>
        private Job m_currentJob;
        public Job CurrentJob { get { return m_currentJob;} }

        /// <summary>
        /// Number of jobs waiting to be processed.
        /// </summary>
        public int JobsWaiting { get { return m_jobQueue.Count; } }

        /// <summary>
        /// The timeout in milliseconds to wait for at least one event to be written when the recorder is stopping.
        /// </summary>
        public int RequestProcessTimeoutOnStop { get; set; }

        /// <summary>
        /// Controls whether we need to warn in the log about exceeding the max queue size.
        /// </summary>
        /// <remarks>
        /// This is flipped to false once queue max has been exceeded and back to true when it falls below max, in
        /// order to avoid spamming the log with lots of warnings.
        /// </remarks>
        private bool m_warnOverMaxQueue = true;

        private BlockingCollection<Job> m_jobQueue = new BlockingCollection<Job>(5000);

        private CancellationTokenSource m_cancelSource;

        private int m_timeout = -1;

        private bool m_threadRunnig = false;

        public JobEngine(string name, string loggingName, int timeout = -1)
        {
            Name = name;
            LoggingName = loggingName;
            m_timeout = timeout;
            RequestProcessTimeoutOnStop = 5000;
        }

        public void Start()
        {
            lock (JobLock)
            {
                if (IsRunning)
                    return;

                IsRunning = true;

                m_cancelSource = new CancellationTokenSource();
                WorkManager.RunInThreadPool(ProcessRequests, null, Name, false);
                m_threadRunnig = true;
            }
        }

        public void Stop()
        {
            lock (JobLock)
            {
                try
                {
                    if (!IsRunning)
                        return;

                    m_log.DebugFormat("[JobEngine] Stopping {0}", Name);

                    IsRunning = false;
                    if(m_threadRunnig)
                    {
                        m_cancelSource.Cancel();
                        m_threadRunnig = false;
                    }
                }
                finally
                {
                    if(m_cancelSource != null)
                        m_cancelSource.Dispose();
                }
            }
        }

        /// <summary>
        /// Make a job.
        /// </summary>
        /// <remarks>
        /// We provide this method to replace the constructor so that we can later pool job objects if necessary to
        /// reduce memory churn.  Normally one would directly call QueueJob() with parameters anyway.
        /// </remarks>
        /// <returns></returns>
        /// <param name="name">Name.</param>
        /// <param name="action">Action.</param>
        /// <param name="commonId">Common identifier.</param>
        public static Job MakeJob(string name, Action action, string commonId = null)
        {
            return Job.MakeJob(name, action, commonId);
        }

        /// <summary>
        /// Remove the next job queued for processing.
        /// </summary>
        /// <remarks>
        /// Returns null if there is no next job.
        /// Will not remove a job currently being performed.
        /// </remarks>
        public Job RemoveNextJob()
        {
            Job nextJob;
            m_jobQueue.TryTake(out nextJob);

            return nextJob;
        }

        /// <summary>
        /// Queue the job for processing.
        /// </summary>
        /// <returns><c>true</c>, if job was queued, <c>false</c> otherwise.</returns>
        /// <param name="name">Name of job.  This appears on the console and in logging.</param>
        /// <param name="action">Action to perform.</param>
        /// <param name="commonId">
        /// Common identifier for a set of jobs.  This is allows a set of jobs to be removed
        /// if required (e.g. all jobs for a given agent.  Optional.
        /// </param>
        public bool QueueJob(string name, Action action, string commonId = null)
        {
            return QueueJob(MakeJob(name, action, commonId));
        }

        /// <summary>
        /// Queue the job for processing.
        /// </summary>
        /// <returns><c>true</c>, if job was queued, <c>false</c> otherwise.</returns>
        /// <param name="job">The job</param>
        /// </param>
        public bool QueueJob(Job job)
        {
            lock(JobLock)
            {
                if(!IsRunning)
                    return false;
                
                if(!m_threadRunnig)
                {
                     WorkManager.RunInThreadPool(ProcessRequests, null, Name, false);
                     m_threadRunnig = true;
                }
            }

            if (m_jobQueue.Count < m_jobQueue.BoundedCapacity)
            {
                m_jobQueue.Add(job);

                if (!m_warnOverMaxQueue)
                    m_warnOverMaxQueue = true;

                return true;
            }
            else
            {
                if (m_warnOverMaxQueue)
                {
                    m_log.WarnFormat(
                        "[{0}]: Job queue at maximum capacity, not recording job from {1} in {2}",
                        LoggingName, job.Name, Name);

                    m_warnOverMaxQueue = false;
                }
                return false;
            }
        }

        private void ProcessRequests(Object o)
        {
            while(IsRunning)
            {
                try
                {
                    if(!m_jobQueue.TryTake(out m_currentJob, m_timeout, m_cancelSource.Token))
                    {
                        lock(JobLock)
                            m_threadRunnig = false;
                        break;
                    }
                }
                catch(ObjectDisposedException)
                {
                    m_log.DebugFormat("[JobEngine] {0} stopping ignoring {1} jobs in queue",
                        Name,m_jobQueue.Count);
                    break;
                }
                catch(OperationCanceledException)
                {
                    break;
                }

                if(LogLevel >= 1)
                    m_log.DebugFormat("[{0}]: Processing job {1}",LoggingName,m_currentJob.Name);

                try
                {
                    m_currentJob.Action();
                }
                catch(Exception e)
                {
                    m_log.Error(
                        string.Format(
                        "[{0}]: Job {1} failed, continuing.  Exception  ",LoggingName,m_currentJob.Name),e);
                }

                if(LogLevel >= 1)
                    m_log.DebugFormat("[{0}]: Processed job {1}",LoggingName,m_currentJob.Name);

                m_currentJob = null;
            }
        }

        public class Job
        {
            /// <summary>
            /// Name of the job.
            /// </summary>
            /// <remarks>
            /// This appears on console and debug output.
            /// </remarks>
            public string Name { get; private set; }

            /// <summary>
            /// Common ID for this job.
            /// </summary>
            /// <remarks>
            /// This allows all jobs with a certain common ID (e.g. a client UUID) to be removed en-masse if required.
            /// Can be null if this is not required.
            /// </remarks>
            public string CommonId { get; private set; }

            /// <summary>
            /// Action to perform when this job is processed.
            /// </summary>
            public Action Action { get; private set; }

            private Job(string name, string commonId, Action action)
            {
                Name = name;
                CommonId = commonId;
                Action = action;
            }

            /// <summary>
            /// Make a job.  It needs to be separately queued.
            /// </summary>
            /// <remarks>
            /// We provide this method to replace the constructor so that we can pool job objects if necessary to
            /// to reduce memory churn.  Normally one would directly call JobEngine.QueueJob() with parameters anyway.
            /// </remarks>
            /// <returns></returns>
            /// <param name="name">Name.</param>
            /// <param name="action">Action.</param>
            /// <param name="commonId">Common identifier.</param>
            public static Job MakeJob(string name, Action action, string commonId = null)
            {
                return new Job(name, commonId, action);
            }
        }
    }
}
