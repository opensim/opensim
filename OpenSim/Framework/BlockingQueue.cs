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

using System.Collections.Generic;
using System.Threading;

namespace OpenSim.Framework
{
    public class BlockingQueue<T>
    {
        private readonly Queue<T> m_pqueue = new Queue<T>();
        private readonly Queue<T> m_queue = new Queue<T>();
        private readonly object m_queueSync = new object();

        public void PriorityEnqueue(T value)
        {
            lock (m_queueSync)
            {
                m_pqueue.Enqueue(value);
                Monitor.Pulse(m_queueSync);
            }
        }

        public void Enqueue(T value)
        {
            lock (m_queueSync)
            {
                m_queue.Enqueue(value);
                Monitor.Pulse(m_queueSync);
            }
        }

        public T Dequeue()
        {
            lock (m_queueSync)
            {
                if (m_queue.Count < 1 && m_pqueue.Count < 1)
                {
                    Monitor.Wait(m_queueSync);
                }

                if (m_pqueue.Count > 0)
                    return m_pqueue.Dequeue();
                
                if (m_queue.Count > 0)
                    return m_queue.Dequeue();
                return default(T);
            }
        }

        public T Dequeue(int msTimeout)
        {
            lock (m_queueSync)
            {
                if (m_queue.Count < 1 && m_pqueue.Count < 1)
                {
                    Monitor.Wait(m_queueSync, msTimeout);
                }

                if (m_pqueue.Count > 0)
                    return m_pqueue.Dequeue();
                if (m_queue.Count > 0)
                    return m_queue.Dequeue();
                return default(T);
            }
        }

        public bool Contains(T item)
        {
            lock (m_queueSync)
            {
                if (m_pqueue.Contains(item))
                    return true;
                return m_queue.Contains(item);
            }
        }

        public int Count()
        {
            lock (m_queueSync)
            {
                return m_queue.Count+m_pqueue.Count;
            }
        }

        public T[] GetQueueArray()
        {
            lock (m_queueSync)
            {
                return m_queue.ToArray();
            }
        }

        public void Clear()
        {
            lock (m_queueSync)
            {
                m_pqueue.Clear();
                m_queue.Clear();
                Monitor.Pulse(m_queueSync);
            }
        }
    }
}
