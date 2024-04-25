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

// A pool of jobs or workitems with same method (callback) but diferent argument (as object) to run in main threadpool
// can have up to m_concurrency number of execution threads
// it will hold each thread up to m_threadsHoldtime ms waiting for more work, before releasing it back to the pool.

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using log4net;

namespace OpenSim.Framework
{
    public class ObjectJobEngine : IDisposable
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly object m_mainLock = new object();
        private readonly string m_name;
        private readonly int m_threadsHoldtime;
        private readonly int m_concurrency = 1;

        private BlockingCollection<object> m_jobQueue;
        private CancellationTokenSource m_cancelSource;
        private WaitCallback m_callback;
        private int m_numberThreads = 0;
        private bool m_isRunning;

        public ObjectJobEngine(WaitCallback callback, string name, int threadsHoldtime = 1000, int concurrency = 1)
        {
            m_name = name;
            m_threadsHoldtime = threadsHoldtime;

            if (concurrency < 1)
                m_concurrency = 1;
            else
                m_concurrency = concurrency;

            if (callback !=  null)
            {
                m_callback = callback;
                m_jobQueue = new BlockingCollection<object>();
                m_cancelSource = new CancellationTokenSource();
                m_isRunning = true;
            }
        }

        ~ObjectJobEngine()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            lock(m_mainLock)
            {
                if (!m_isRunning)
                    return;
                m_isRunning = false;

                m_cancelSource.Cancel();
            }

            if (m_numberThreads > 0)
            {
                int cntr = 100;
                while (m_numberThreads > 0 && --cntr > 0)
                    Thread.Yield();
            }

            if (m_jobQueue != null)
            {
                m_jobQueue.Dispose();
                m_jobQueue = null;
            }
            if (m_cancelSource != null)
            {
                m_cancelSource.Dispose();
                m_cancelSource = null;
            }
            m_callback = null;
        }

        /// <summary>
        /// Number of jobs waiting to be processed.
        /// </summary>
        public int Count { get { return m_jobQueue == null ? 0 : m_jobQueue.Count; } }

        public void Cancel()
        {
            if (!m_isRunning || m_jobQueue == null || m_jobQueue.Count == 0)
                return;
            try
            {
                while(m_jobQueue.TryTake(out _));
                m_cancelSource.Cancel();
            }
            catch { }
        }

        /// <summary>
        /// Queue the job for processing.
        /// </summary>
        /// <returns><c>true</c>, if job was queued, <c>false</c> otherwise.</returns>
        /// <param name="job">The job</param>
        /// </param>
        public bool Enqueue(object o)
        {
            if (!m_isRunning)
                return false;

            lock (m_mainLock)
            {
                m_jobQueue?.Add(o);
                if (m_numberThreads < m_concurrency && m_numberThreads < m_jobQueue.Count)
                {
                    Util.FireAndForget(ProcessRequests, null, m_name, false);
                    ++m_numberThreads;
                }
            }
            return true;
        }

        private void ProcessRequests(object o)
        {
            object obj;
            while (m_isRunning)
            {
                try
                {
                    if(!m_jobQueue.TryTake(out obj, m_threadsHoldtime, m_cancelSource.Token))
                    {
                        lock (m_mainLock)
                        {
                            if (m_jobQueue.Count > 0)
                                continue;
                            --m_numberThreads;
                            return;
                        }
                    }
                }
                catch
                {
                    break;
                }

                if(!m_isRunning || m_callback == null)
                    break;
                try
                {
                    m_callback.Invoke(obj);
                    obj = null;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[ObjectJob {0}]: Job failed, continuing.  Exception {1}", m_name, e);
                }
            }
            lock (m_mainLock)
                --m_numberThreads;
        }
    }
}
