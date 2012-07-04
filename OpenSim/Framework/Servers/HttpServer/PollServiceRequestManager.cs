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


/*
namespace OpenSim.Framework.Servers.HttpServer
{

    public class PollServiceRequestManager
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly BaseHttpServer m_server;
        private static Queue m_requests = Queue.Synchronized(new Queue());
        private static ManualResetEvent m_ev = new ManualResetEvent(false);
        private uint m_WorkerThreadCount = 0;
        private Thread[] m_workerThreads;
        private PollServiceWorkerThread[] m_PollServiceWorkerThreads;
        private bool m_running = true;

        public PollServiceRequestManager(BaseHttpServer pSrv, uint pWorkerThreadCount, int pTimeout)
        {
            m_server = pSrv;
            m_WorkerThreadCount = pWorkerThreadCount;
            m_workerThreads = new Thread[m_WorkerThreadCount];
            m_PollServiceWorkerThreads = new PollServiceWorkerThread[m_WorkerThreadCount];

            //startup worker threads
            for (uint i = 0; i < m_WorkerThreadCount; i++)
            {
                m_PollServiceWorkerThreads[i] = new PollServiceWorkerThread(m_server, pTimeout);
                m_PollServiceWorkerThreads[i].ReQueue += ReQueueEvent;

                m_workerThreads[i]
                    = Watchdog.StartThread(
                        m_PollServiceWorkerThreads[i].ThreadStart,
                        String.Format("PollServiceWorkerThread{0}", i),
                        ThreadPriority.Normal,
                        false,
                        true,
                        int.MaxValue);
            }

            Watchdog.StartThread(
                this.ThreadStart,
                "PollServiceWatcherThread",
                ThreadPriority.Normal,
                false,
                true,
                1000 * 60 * 10);
        }

        internal void ReQueueEvent(PollServiceHttpRequest req)
        {
            // Do accounting stuff here
            Enqueue(req);
        }

        public void Enqueue(PollServiceHttpRequest req)
        {
            lock (m_requests)
                m_requests.Enqueue(req);
            m_ev.Set();
        }

        public void ThreadStart()
        {
            while (m_running)
            {
                m_ev.WaitOne(1000);
                m_ev.Reset();
                Watchdog.UpdateThread();
                ProcessQueuedRequests();
            }
        }

        private void ProcessQueuedRequests()
        {
            lock (m_requests)
            {
                if (m_requests.Count == 0)
                    return;

//                m_log.DebugFormat("[POLL SERVICE REQUEST MANAGER]: Processing {0} requests", m_requests.Count);

                int reqperthread = (int) (m_requests.Count/m_WorkerThreadCount) + 1;

                // For Each WorkerThread
                for (int tc = 0; tc < m_WorkerThreadCount && m_requests.Count > 0; tc++)
                {
                    //Loop over number of requests each thread handles.
                    for (int i = 0; i < reqperthread && m_requests.Count > 0; i++)
                    {
                        try
                        {
                            m_PollServiceWorkerThreads[tc].Enqueue((PollServiceHttpRequest)m_requests.Dequeue());
                        }
                        catch (InvalidOperationException)
                        {
                            // The queue is empty, we did our calculations wrong!
                            return;
                        }
                        
                    }
                }
            }
            
        }

        ~PollServiceRequestManager()
        {
            foreach (object o in m_requests)
            {
                PollServiceHttpRequest req = (PollServiceHttpRequest) o;
                m_server.DoHTTPGruntWork(
                    req.PollServiceArgs.NoEvents(req.RequestID, req.PollServiceArgs.Id),
                    new OSHttpResponse(new HttpResponse(req.HttpContext, req.Request), req.HttpContext));
            }

            m_requests.Clear();

            foreach (Thread t in m_workerThreads)
            {
                t.Abort();
            }
            m_running = false;
        }
    }
}
 */

using System.IO;
using System.Text;
using System.Collections.Generic;

namespace OpenSim.Framework.Servers.HttpServer
{
    public class PollServiceRequestManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly BaseHttpServer m_server;

        private BlockingQueue<PollServiceHttpRequest> m_requests = new BlockingQueue<PollServiceHttpRequest>();
        private static Queue<PollServiceHttpRequest> m_retry_requests = new Queue<PollServiceHttpRequest>();

        private uint m_WorkerThreadCount = 0;
        private Thread[] m_workerThreads;
        private Thread m_retrysThread;

        private bool m_running = true;

        private int m_timeout = 1000;   //  increase timeout 250;

        public PollServiceRequestManager(BaseHttpServer pSrv, uint pWorkerThreadCount, int pTimeout)
        {
            m_server = pSrv;
            m_WorkerThreadCount = pWorkerThreadCount;
            m_workerThreads = new Thread[m_WorkerThreadCount];

            //startup worker threads
            for (uint i = 0; i < m_WorkerThreadCount; i++)
            {
                m_workerThreads[i]
                    = Watchdog.StartThread(
                        poolWorkerJob,
                        String.Format("PollServiceWorkerThread{0}", i),
                        ThreadPriority.Normal,
                        false,
                        true,
                        int.MaxValue);
            }

            m_retrysThread = Watchdog.StartThread(
                this.CheckRetries,
                "PollServiceWatcherThread",
                ThreadPriority.Normal,
                false,
                true,
                1000 * 60 * 10);
        }


        private void ReQueueEvent(PollServiceHttpRequest req)
        {
            if (m_running)
            {
                lock (m_retry_requests)
                    m_retry_requests.Enqueue(req);
            }
        }

        public void Enqueue(PollServiceHttpRequest req)
        {
            if (m_running)
                m_requests.Enqueue(req);
        }

        private void CheckRetries()
        {
            while (m_running)
            {
                Thread.Sleep(100); // let the world move  .. back to faster rate
                Watchdog.UpdateThread();
                lock (m_retry_requests)
                {
                    while (m_retry_requests.Count > 0 && m_running)
                        Enqueue(m_retry_requests.Dequeue());
                }
            }
        }

        ~PollServiceRequestManager()
        {
            m_running = false;
            m_timeout = -10000; // cause all to expire
            Thread.Sleep(1000); // let the world move

            foreach (Thread t in m_workerThreads)
            {
                try
                {
                    t.Abort();
                }
                catch
                {
                }
            }

            try
            {
                foreach (PollServiceHttpRequest req in m_retry_requests)
                {
                    m_server.DoHTTPGruntWork(
                        req.PollServiceArgs.NoEvents(req.RequestID, req.PollServiceArgs.Id),
                        new OSHttpResponse(new HttpResponse(req.HttpContext, req.Request), req.HttpContext));
                }
            }
            catch
            {
            }

            PollServiceHttpRequest wreq;
            m_retry_requests.Clear();

            while (m_requests.Count() > 0)
            {
                try
                {
                    wreq = m_requests.Dequeue(0);
                    m_server.DoHTTPGruntWork(
                        wreq.PollServiceArgs.NoEvents(wreq.RequestID, wreq.PollServiceArgs.Id),
                        new OSHttpResponse(new HttpResponse(wreq.HttpContext, wreq.Request), wreq.HttpContext));
                }
                catch
                {
                }
            }

            m_requests.Clear();
        }

        // work threads

        private void poolWorkerJob()
        {
            PollServiceHttpRequest req;
            StreamReader str;

            while (true)
            {
                req = m_requests.Dequeue(5000);

                Watchdog.UpdateThread();
                if (req != null)
                {
                    try
                    {
                        if (req.PollServiceArgs.HasEvents(req.RequestID, req.PollServiceArgs.Id))
                        {
                            try
                            {
                                str = new StreamReader(req.Request.Body);
                            }
                            catch (System.ArgumentException)
                            {
                                // Stream was not readable means a child agent
                                // was closed due to logout, leaving the
                                // Event Queue request orphaned.
                                continue;
                            }

                            try
                            {
                                Hashtable responsedata = req.PollServiceArgs.GetEvents(req.RequestID, req.PollServiceArgs.Id, str.ReadToEnd());
                                m_server.DoHTTPGruntWork(responsedata,
                                                     new OSHttpResponse(new HttpResponse(req.HttpContext, req.Request), req.HttpContext));
                            }
                            catch (ObjectDisposedException) // Browser aborted before we could read body, server closed the stream
                            {
                                // Ignore it, no need to reply
                            }

                            str.Close();

                        }
                        else
                        {
                            if ((Environment.TickCount - req.RequestTime) > m_timeout)
                            {
                                m_server.DoHTTPGruntWork(req.PollServiceArgs.NoEvents(req.RequestID, req.PollServiceArgs.Id),
                                                         new OSHttpResponse(new HttpResponse(req.HttpContext, req.Request), req.HttpContext));
                            }
                            else
                            {
                                ReQueueEvent(req);
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
}

