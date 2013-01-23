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

namespace OpenSim.Framework.Servers.HttpServer
{
    public class PollServiceRequestManager
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly BaseHttpServer m_server;
        private static Queue m_requests = Queue.Synchronized(new Queue());
        private uint m_WorkerThreadCount = 0;
        private Thread[] m_workerThreads;
        private PollServiceWorkerThread[] m_PollServiceWorkerThreads;
        private volatile bool m_running = true;
        private int m_pollTimeout;

        public PollServiceRequestManager(BaseHttpServer pSrv, uint pWorkerThreadCount, int pTimeout)
        {
            m_server = pSrv;
            m_WorkerThreadCount = pWorkerThreadCount;
            m_pollTimeout = pTimeout;
        }

        public void Start()
        {
            m_running = true;
            m_workerThreads = new Thread[m_WorkerThreadCount];
            m_PollServiceWorkerThreads = new PollServiceWorkerThread[m_WorkerThreadCount];

            //startup worker threads
            for (uint i = 0; i < m_WorkerThreadCount; i++)
            {
                m_PollServiceWorkerThreads[i] = new PollServiceWorkerThread(m_server, m_pollTimeout);
                m_PollServiceWorkerThreads[i].ReQueue += ReQueueEvent;

                m_workerThreads[i]
                    = Watchdog.StartThread(
                        m_PollServiceWorkerThreads[i].ThreadStart,
                        String.Format("PollServiceWorkerThread{0}", i),
                        ThreadPriority.Normal,
                        false,
                        true,
                        null,
                        int.MaxValue);
            }

            Watchdog.StartThread(
                this.ThreadStart,
                "PollServiceWatcherThread",
                ThreadPriority.Normal,
                false,
                true,
                null,
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
        }

        public void ThreadStart()
        {
            while (m_running)
            {
                Watchdog.UpdateThread();
                ProcessQueuedRequests();
                Thread.Sleep(1000);
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

        public void Stop()
        {
            m_running = false;

            foreach (object o in m_requests)
            {
                PollServiceHttpRequest req = (PollServiceHttpRequest) o;
                PollServiceWorkerThread.DoHTTPGruntWork(
                    m_server, req, req.PollServiceArgs.NoEvents(req.RequestID, req.PollServiceArgs.Id));
            }

            m_requests.Clear();

            foreach (Thread t in m_workerThreads)
            {
                t.Abort();
            }
        }
    }
}