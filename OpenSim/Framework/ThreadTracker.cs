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
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using log4net;

namespace OpenSim.Framework
{
    public static class ThreadTracker
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        private static readonly long ThreadTimeout = 30 * 10000000;
        public static List<ThreadTrackerItem> m_Threads;
        public static Thread ThreadTrackerThread;

        static ThreadTracker()
        {
#if DEBUG
            m_Threads = new List<ThreadTrackerItem>();
            ThreadTrackerThread = new Thread(ThreadTrackerThreadLoop);
            ThreadTrackerThread.Name = "ThreadTrackerThread";
            ThreadTrackerThread.IsBackground = true;
            ThreadTrackerThread.Priority = ThreadPriority.BelowNormal;
            ThreadTrackerThread.Start();
            Add(ThreadTrackerThread);
#endif
        }

        private static void ThreadTrackerThreadLoop()
        {
            try
            {
                while (true)
                {
                    Thread.Sleep(5000);
                    CleanUp();
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[THREAD TRACKER]: Thread tracker cleanup thread terminating with exception.  Please report this error.  Exception is {0}", 
                    e);
            }
        }

        public static void Add(Thread thread)
        {
#if DEBUG
            if (thread != null)
            {
                lock (m_Threads)
                {
                    ThreadTrackerItem tti = new ThreadTrackerItem();
                    tti.Thread = thread;
                    tti.LastSeenActive = DateTime.Now.Ticks;
                    m_Threads.Add(tti);
                }
            }
#endif
        }

        public static void Remove(Thread thread)
        {
#if DEBUG
            lock (m_Threads)
            {
                foreach (ThreadTrackerItem tti in new ArrayList(m_Threads))
                {
                    if (tti.Thread == thread)
                        m_Threads.Remove(tti);
                }
            }
#endif
        }

        public static void CleanUp()
        {
            lock (m_Threads)
            {
                foreach (ThreadTrackerItem tti in new ArrayList(m_Threads))
                {
                    try
                    {
                        
                    
                        if (tti.Thread.IsAlive)
                        {
                            // Its active
                            tti.LastSeenActive = DateTime.Now.Ticks;
                        }
                        else
                        {
                            // Its not active -- if its expired then remove it
                            if (tti.LastSeenActive + ThreadTimeout < DateTime.Now.Ticks)
                                m_Threads.Remove(tti);
                        }
                    }
                    catch (NullReferenceException)
                    {
                        m_Threads.Remove(tti);
                    }
                }
            }
        }

        public static List<Thread> GetThreads()
        {
            if (m_Threads == null)
                return null;

            List<Thread> threads = new List<Thread>();
            lock (m_Threads)
            {
                foreach (ThreadTrackerItem tti in new ArrayList(m_Threads))
                {
                    threads.Add(tti.Thread);
                }
            }
            return threads;
        }

        #region Nested type: ThreadTrackerItem

        public class ThreadTrackerItem
        {
            public long LastSeenActive;
            public Thread Thread;
        }

        #endregion
    }
}
