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
using System.Threading;
using System.Reflection;
using log4net;
using HttpServer;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using Amib.Threading;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace OpenSim.Framework.Servers.HttpServer
{
    public class PollServiceRequestManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Is the poll service request manager running?
        /// </summary>
        /// <remarks>
        /// Can be running either synchronously or asynchronously
        /// </remarks>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Is the poll service performing responses asynchronously (with its own threads) or synchronously (via
        /// external calls)?
        /// </summary>
        public bool PerformResponsesAsync { get; private set; }

        /// <summary>
        /// Number of responses actually processed and sent to viewer (or aborted due to error).
        /// </summary>
        public int ResponsesProcessed { get; private set; }

        private readonly BaseHttpServer m_server;

        private Dictionary<PollServiceHttpRequest, Queue<PollServiceHttpRequest>> m_bycontext;
        private BlockingQueue<PollServiceHttpRequest> m_requests = new BlockingQueue<PollServiceHttpRequest>();
        private static Queue<PollServiceHttpRequest> m_slowRequests = new Queue<PollServiceHttpRequest>();
        private static Queue<PollServiceHttpRequest> m_retryRequests = new Queue<PollServiceHttpRequest>();

        private uint m_WorkerThreadCount = 0;
        private Thread[] m_workerThreads;
        private Thread m_retrysThread;

<<<<<<< HEAD
        private SmartThreadPool m_threadPool = new SmartThreadPool(20000, 12, 2);

//        private int m_timeout = 1000;   //  increase timeout 250; now use the event one
=======
        private bool m_running = true;
        private int slowCount = 0;

        private SmartThreadPool m_threadPool;
>>>>>>> avn/ubitvar

        public PollServiceRequestManager(
            BaseHttpServer pSrv, bool performResponsesAsync, uint pWorkerThreadCount, int pTimeout)
        {
            m_server = pSrv;
            PerformResponsesAsync = performResponsesAsync;
            m_WorkerThreadCount = pWorkerThreadCount;
            m_workerThreads = new Thread[m_WorkerThreadCount];

<<<<<<< HEAD
            StatsManager.RegisterStat(
                new Stat(
                    "QueuedPollResponses",
                    "Number of poll responses queued for processing.",
                    "",
                    "",
                    "httpserver",
                    m_server.Port.ToString(),
                    StatType.Pull,
                    MeasuresOfInterest.AverageChangeOverTime,
                    stat => stat.Value = m_requests.Count(),
                    StatVerbosity.Debug));

            StatsManager.RegisterStat(
                new Stat(
                    "ProcessedPollResponses",
                    "Number of poll responses processed.",
                    "",
                    "",
                    "httpserver",
                    m_server.Port.ToString(),
                    StatType.Pull,
                    MeasuresOfInterest.AverageChangeOverTime,
                    stat => stat.Value = ResponsesProcessed,
                    StatVerbosity.Debug));
=======
            PollServiceHttpRequestComparer preqCp = new PollServiceHttpRequestComparer();
            m_bycontext = new Dictionary<PollServiceHttpRequest, Queue<PollServiceHttpRequest>>(preqCp);

            STPStartInfo startInfo = new STPStartInfo();
            startInfo.IdleTimeout = 30000;
            startInfo.MaxWorkerThreads = 15;
            startInfo.MinWorkerThreads = 1;
            startInfo.ThreadPriority = ThreadPriority.Normal;
            startInfo.StartSuspended = true;
            startInfo.ThreadPoolName = "PoolService";

            m_threadPool = new SmartThreadPool(startInfo);
>>>>>>> avn/ubitvar
        }

        public void Start()
        {
<<<<<<< HEAD
            IsRunning = true;

            if (PerformResponsesAsync)
            {
                //startup worker threads
                for (uint i = 0; i < m_WorkerThreadCount; i++)
                {
                    m_workerThreads[i]
                        = WorkManager.StartThread(
                            PoolWorkerJob,
                            string.Format("PollServiceWorkerThread{0}:{1}", i, m_server.Port),
                            ThreadPriority.Normal,
                            false,
                            false,
                            null,
                            int.MaxValue);
                }

                WorkManager.StartThread(
                    this.CheckLongPollThreads,
                    string.Format("LongPollServiceWatcherThread:{0}", m_server.Port),
                    ThreadPriority.Normal,
                    false,
                    true,
                    null,
                    1000 * 60 * 10);
            }
=======
            m_threadPool.Start();
            //startup worker threads
            for (uint i = 0; i < m_WorkerThreadCount; i++)
            {
                m_workerThreads[i]
                    = Watchdog.StartThread(
                        PoolWorkerJob,
                        string.Format("PollServiceWorkerThread {0}:{1}", i, m_server.Port),
                        ThreadPriority.Normal,
                        false,
                        false,
                        null,
                        int.MaxValue);
            }

            m_retrysThread = Watchdog.StartThread(
                this.CheckRetries,
                string.Format("PollServiceWatcherThread:{0}", m_server.Port),
                ThreadPriority.Normal,
                false,
                true,
                null,
                1000 * 60 * 10);
>>>>>>> avn/ubitvar
        }

        private void ReQueueEvent(PollServiceHttpRequest req)
        {
            if (IsRunning)
            {
                lock (m_retryRequests)
                    m_retryRequests.Enqueue(req);
            }
        }

        public void Enqueue(PollServiceHttpRequest req)
        {
            lock (m_bycontext)
            {
                Queue<PollServiceHttpRequest> ctxQeueue;
                if (m_bycontext.TryGetValue(req, out ctxQeueue))
                {
                    ctxQeueue.Enqueue(req);
                }
                else
                {
                    ctxQeueue = new Queue<PollServiceHttpRequest>();
                    m_bycontext[req] = ctxQeueue;
                    EnqueueInt(req);
                }
            }
        }

        public void byContextDequeue(PollServiceHttpRequest req)
        {
            Queue<PollServiceHttpRequest> ctxQeueue;
            lock (m_bycontext)
            {
                if (m_bycontext.TryGetValue(req, out ctxQeueue))
                {
                    if (ctxQeueue.Count > 0)
                    {
                        PollServiceHttpRequest newreq = ctxQeueue.Dequeue();
                        EnqueueInt(newreq);
                    }
                    else
                    {
                        m_bycontext.Remove(req);
                    }
                }
            }
        }


        public void EnqueueInt(PollServiceHttpRequest req)
        {
            if (IsRunning)
            {
                if (req.PollServiceArgs.Type != PollServiceEventArgs.EventType.LongPoll)
                {
                    m_requests.Enqueue(req);
                }
                else
                {
                    lock (m_slowRequests)
                        m_slowRequests.Enqueue(req);
                }
            }
        }

        private void CheckRetries()
        {
<<<<<<< HEAD
            // The only purpose of this thread is to check the EQs for events.
            // If there are events, that thread will be placed in the "ready-to-serve" queue, m_requests.
            // If there are no events, that thread will be back to its "waiting" queue, m_longPollRequests.
            // All other types of tasks (Inventory handlers, http-in, etc) don't have the long-poll nature,
            // so if they aren't ready to be served by a worker thread (no events), they are placed 
            // directly back in the "ready-to-serve" queue by the worker thread.
            while (IsRunning)
=======
            while (m_running)
>>>>>>> avn/ubitvar
            {
                Thread.Sleep(100); // let the world move  .. back to faster rate
                Watchdog.UpdateThread();
                lock (m_retryRequests)
                {
<<<<<<< HEAD
                    if (m_longPollRequests.Count > 0 && IsRunning)
                    {
                        List<PollServiceHttpRequest> ready = m_longPollRequests.FindAll(req =>
                            (req.PollServiceArgs.HasEvents(req.RequestID, req.PollServiceArgs.Id) || // there are events in this EQ
                            (Environment.TickCount - req.RequestTime) > req.PollServiceArgs.TimeOutms) // no events, but timeout
                            );

                        ready.ForEach(req =>
                            {
                                m_requests.Enqueue(req);
                                m_longPollRequests.Remove(req);
                            });
=======
                    while (m_retryRequests.Count > 0 && m_running)
                        m_requests.Enqueue(m_retryRequests.Dequeue());
                }
                slowCount++;
                if (slowCount >= 10)
                {
                    slowCount = 0;
>>>>>>> avn/ubitvar

                    lock (m_slowRequests)
                    {
                        while (m_slowRequests.Count > 0 && m_running)
                            m_requests.Enqueue(m_slowRequests.Dequeue());
                    }
                }
            }
        }

        public void Stop()
        {
<<<<<<< HEAD
            IsRunning = false;
//            m_timeout = -10000; // cause all to expire
=======
            m_running = false;
>>>>>>> avn/ubitvar
            Thread.Sleep(1000); // let the world move

            foreach (Thread t in m_workerThreads)
                Watchdog.AbortThread(t.ManagedThreadId);

            // any entry in m_bycontext should have a active request on the other queues
            // so just delete contents to easy GC
            foreach (Queue<PollServiceHttpRequest> qu in m_bycontext.Values)
                qu.Clear();
            m_bycontext.Clear();

            try
            {
                foreach (PollServiceHttpRequest req in m_retryRequests)
                {
                    req.DoHTTPstop(m_server);
                }
            }
            catch
            {
            }

            PollServiceHttpRequest wreq;
            m_retryRequests.Clear();

            lock (m_slowRequests)
            {
<<<<<<< HEAD
                if (m_longPollRequests.Count > 0 && IsRunning)
                    m_longPollRequests.ForEach(req => m_requests.Enqueue(req));
=======
                while (m_slowRequests.Count > 0)
                    m_requests.Enqueue(m_slowRequests.Dequeue());
>>>>>>> avn/ubitvar
            }

            while (m_requests.Count() > 0)
            {
                try
                {
                    wreq = m_requests.Dequeue(0);
<<<<<<< HEAD
                    ResponsesProcessed++;
                    wreq.DoHTTPGruntWork(
                        m_server, wreq.PollServiceArgs.NoEvents(wreq.RequestID, wreq.PollServiceArgs.Id));
=======
                    wreq.DoHTTPstop(m_server);
>>>>>>> avn/ubitvar
                }
                catch
                {
                }
            }

            m_requests.Clear();
        }

        // work threads

        private void PoolWorkerJob()
        {
            while (IsRunning)
            {
<<<<<<< HEAD
=======
                PollServiceHttpRequest req = m_requests.Dequeue(5000);

>>>>>>> avn/ubitvar
                Watchdog.UpdateThread();
                WaitPerformResponse();
            }
        }

        public void WaitPerformResponse()
        {
            PollServiceHttpRequest req = m_requests.Dequeue(5000);
//            m_log.DebugFormat("[YYY]: Dequeued {0}", (req == null ? "null" : req.PollServiceArgs.Type.ToString()));

            if (req != null)
            {
                try
                {
                    if (req.PollServiceArgs.HasEvents(req.RequestID, req.PollServiceArgs.Id))
                    {
                        Hashtable responsedata = req.PollServiceArgs.GetEvents(req.RequestID, req.PollServiceArgs.Id);

<<<<<<< HEAD
                        if (responsedata == null)
                            return;

                        // This is the event queue.
                        // Even if we're not running we can still perform responses by explicit request.
                        if (req.PollServiceArgs.Type == PollServiceEventArgs.EventType.LongPoll 
                            || !PerformResponsesAsync) 
                        {
                            try
                            {
                                ResponsesProcessed++;
                                req.DoHTTPGruntWork(m_server, responsedata);
                            }
                            catch (ObjectDisposedException e) // Browser aborted before we could read body, server closed the stream
                            {
                                // Ignore it, no need to reply
                                m_log.Error(e);
                            }
                        }
                        else
                        {
                            m_threadPool.QueueWorkItem(x =>
=======
                            if (req.PollServiceArgs.Type == PollServiceEventArgs.EventType.LongPoll) // This is the event queue
>>>>>>> avn/ubitvar
                            {
                                try
                                {
                                    ResponsesProcessed++;
                                    req.DoHTTPGruntWork(m_server, responsedata);
                                    byContextDequeue(req);
                                }
                                catch (ObjectDisposedException e) // Browser aborted before we could read body, server closed the stream
                                {
                                    // Ignore it, no need to reply
                                    m_log.Error(e);
                                }
                                catch (Exception e)
                                {
<<<<<<< HEAD
                                    m_log.Error(e);
                                }

                                return null;
                            }, null);
                        }
                    }
                    else
                    {
                        if ((Environment.TickCount - req.RequestTime) > req.PollServiceArgs.TimeOutms)
                        {
                            ResponsesProcessed++;
                            req.DoHTTPGruntWork(
                                m_server, req.PollServiceArgs.NoEvents(req.RequestID, req.PollServiceArgs.Id));
                        }
                        else
                        {
                            ReQueueEvent(req);
=======
                                    try
                                    {
                                        req.DoHTTPGruntWork(m_server, responsedata);
                                        byContextDequeue(req);
                                    }
                                    catch (ObjectDisposedException) // Browser aborted before we could read body, server closed the stream
                                    {
                                        // Ignore it, no need to reply
                                    }

                                    return null;
                                }, null);
                            }
                        }
                        else
                        {
                            if ((Environment.TickCount - req.RequestTime) > req.PollServiceArgs.TimeOutms)
                            {
                                req.DoHTTPGruntWork(m_server, 
                                    req.PollServiceArgs.NoEvents(req.RequestID, req.PollServiceArgs.Id));
                                byContextDequeue(req);
                            }
                            else
                            {
                                ReQueueEvent(req);
                            }
>>>>>>> avn/ubitvar
                        }
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("Exception in poll service thread: " + e.ToString());
                }
            }
        }

    }
}

