using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule.Interfaces;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public class MicroScheduler : IMicrothreader 
    {
        private readonly List<IEnumerator> m_threads = new List<IEnumerator>();

        public void Run(IEnumerable microthread)
        {
            lock (m_threads)
                m_threads.Add(microthread.GetEnumerator());
        }

        public void Tick(int count)
        {
            lock (m_threads)
            {
                if(m_threads.Count == 0)
                    return;

                int i = 0;
                while (m_threads.Count > 0 && i < count)
                {
                    i++;
                    bool running = m_threads[i%m_threads.Count].MoveNext();

                    if (!running)
                        m_threads.Remove(m_threads[i%m_threads.Count]);
                }
            }
        }
    }
}
