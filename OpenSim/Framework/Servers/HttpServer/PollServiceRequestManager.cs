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
using OpenSim.Framework.Monitoring;
using Amib.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace OpenSim.Framework.Servers.HttpServer
{
    public class PollServiceRequestManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly ConcurrentQueue<PollServiceHttpRequest> m_retryRequests = new();
        private readonly int m_WorkerThreadCount = 0;
        private ObjectJobEngine m_workerPool;
        private Thread m_retrysThread;

        private bool m_running = false;

        public PollServiceRequestManager(bool performResponsesAsync, uint pWorkerThreadCount, int pTimeout)
        {
            m_WorkerThreadCount = (int)pWorkerThreadCount;
        }

        public void Start()
        {
            if(m_running)
                return;

            m_running = true;
            m_workerPool = new ObjectJobEngine(PoolWorkerJob, "PollServiceWorker", 4000, m_WorkerThreadCount);

            m_retrysThread = WorkManager.StartThread(
                CheckRetries,
                string.Format("PollServiceWatcherThread"),
                ThreadPriority.Normal,
                true,
                true,
                null,
                1000 * 60 * 10);
        }

        private void ReQueueEvent(PollServiceHttpRequest req)
        {
            if (m_running)
                m_retryRequests.Enqueue(req);
        }

        public void Enqueue(PollServiceHttpRequest req)
        {
            if(m_running)
                m_workerPool.Enqueue(req);
        }

        private void CheckRetries()
        {
            while (m_running)
            {
                Thread.Sleep(100);
                Watchdog.UpdateThread();
                while (m_running && m_retryRequests.TryDequeue(out PollServiceHttpRequest preq))
                    m_workerPool.Enqueue(preq);
            }
        }

        public void Stop()
        {
            if(!m_running)
                return;

            m_running = false;

            Thread.Sleep(100); // let the world move

            try
            {
                while (m_retryRequests.TryDequeue(out PollServiceHttpRequest req))
                    req.DoHTTPstop();
            }
            catch
            {
            }

            int count = 10;
            while(-- count > 0 && m_workerPool.Count > 0)
                Thread.Sleep(100);

            m_workerPool.Dispose();
            m_workerPool = null;
        }

        // work threads

        private void PoolWorkerJob(object o)
        {
            if (o is not PollServiceHttpRequest req)
                return;
            try
            {
                if (!req.Request.Context.CanSend())
                {
                    req.PollServiceArgs.Drop(req.RequestID, req.PollServiceArgs.Id);
                    return;
                }

                if(!m_running)
                {
                    req.DoHTTPstop();
                    return;
                }

                if (req.Request.Context.IsSending())
                {
                    ReQueueEvent(req);
                    return;
                }

                if (req.PollServiceArgs.HasEvents(req.RequestID, req.PollServiceArgs.Id))
                {
                    try
                    {
                        Hashtable responsedata = req.PollServiceArgs.GetEvents(req.RequestID, req.PollServiceArgs.Id);
                        req.DoHTTPGruntWork(responsedata);
                    }
                    catch { }
                }
                else
                {
                    if ((Environment.TickCount - req.RequestTime) > req.PollServiceArgs.TimeOutms)
                    {
                        try
                        {
                            req.DoHTTPGruntWork(req.PollServiceArgs.NoEvents(req.RequestID, req.PollServiceArgs.Id));
                        }
                        catch { }
                    }
                    else
                    {
                        ReQueueEvent(req);
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error($"Exception in poll service thread: {e}");
            }
        }
    }
}
