using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace OpenSim.Framework
{
    public static class ThreadTracker
    {
        public static List<ThreadTrackerItem> m_Threads;
        public static System.Threading.Thread ThreadTrackerThread;
        private static readonly long ThreadTimeout = 30 * 10000000;

        static ThreadTracker()
        {
#if DEBUG
            m_Threads = new List<ThreadTrackerItem>();
            ThreadTrackerThread = new Thread(ThreadTrackerThreadLoop);
            ThreadTrackerThread.Name = "ThreadTrackerThread";
            ThreadTrackerThread.IsBackground = true;
            ThreadTrackerThread.Priority = System.Threading.ThreadPriority.BelowNormal;
            ThreadTrackerThread.Start();
#endif
        }

        private static void ThreadTrackerThreadLoop()
        {
            while (true)
            {
                Thread.Sleep(5000);
                CleanUp();
            }
        }

        public static void Add(System.Threading.Thread thread)
        {
#if DEBUG
            lock (m_Threads)
            {
                ThreadTrackerItem tti = new ThreadTrackerItem();
                tti.Thread = thread;
                tti.LastSeenActive = DateTime.Now.Ticks;
                m_Threads.Add(tti);
            }
#endif
        }

        public static void Remove(System.Threading.Thread thread)
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

        public class ThreadTrackerItem
        {
            public System.Threading.Thread Thread;
            public long LastSeenActive;
        }
    }
}
