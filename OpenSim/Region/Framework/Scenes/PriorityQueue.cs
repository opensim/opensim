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
using System.Collections.Generic;
using System.Collections.Concurrent;

using OpenSim.Framework;

namespace OpenSim.Region.Framework.Scenes
{
    public class PriorityQueue
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public delegate bool UpdatePriorityHandler(ref int priority, ISceneEntity entity);

        /// <summary>
        /// Total number of queues (priorities) available
        /// </summary>

        public const int NumberOfQueues = 13; // includes immediate queues, m_queueCounts need to be set acording

        /// <summary>
        /// Number of queuest (priorities) that are processed immediately
        /// </summary.
        public const int NumberOfImmediateQueues = 2;
        // first queues are immediate, so no counts
        private static readonly int[] m_queueCounts = {0, 0, 8, 8, 5, 4, 3, 2, 1, 1, 1, 1, 1 };
        // this is                     ava, ava, attach, <10m, 20,40,80,160m,320,640,1280, +

        private PriorityMinHeap[] m_heaps = new PriorityMinHeap[NumberOfQueues];
        private ConcurrentDictionary<uint, EntityUpdate> m_lookupTable;

        // internal state used to ensure the deqeues are spread across the priority
        // queues "fairly". queuecounts is the amount to pull from each queue in
        // each pass. weighted towards the higher priority queues
        private int m_nextQueue = 0;
        private int m_countFromQueue = 0;

        // next request is a counter of the number of updates queued, it provides
        // a total ordering on the updates coming through the queue and is more
        // lightweight (and more discriminating) than tick count
        private ulong m_nextRequest = 0;

        /// <summary>
        /// Lock for enqueue and dequeue operations on the priority queue
        /// </summary>
        private object m_mainLock = new object();
        public object syncRoot
        {
            get { return m_mainLock; }
        }

#region constructor
        public PriorityQueue(int capacity)
        {
            capacity /= 4;
            for (int i = 0; i < m_heaps.Length; ++i)
                m_heaps[i] = new PriorityMinHeap(capacity);

            m_lookupTable = new ConcurrentDictionary<uint, EntityUpdate>();
            m_nextQueue = NumberOfImmediateQueues;
            m_countFromQueue = m_queueCounts[m_nextQueue];
        }
#endregion Constructor

#region PublicMethods
        public void Close()
        {
            PriorityMinHeap[] tmpheaps;
            lock (m_mainLock)
            {
                tmpheaps = m_heaps;
                m_heaps = null;
                m_lookupTable.Clear();
                m_lookupTable = null;
            }

            for (int i = 0; i < tmpheaps.Length; ++i)
            {
                tmpheaps[i].Clear();
                tmpheaps[i] = null;
            }
        }

        /// <summary>
        /// Return the number of items in the queues
        /// </summary>
        public int Count
        {
            get
            {
                return m_lookupTable.Count;
            }
        }

        /// <summary>
        /// Enqueue an item into the specified priority queue
        /// </summary>
        public bool Enqueue(int pqueue, EntityUpdate value)
        {
            uint localid = value.Entity.LocalId;
            try
            {
                lock (m_mainLock)
                {
                    if (m_lookupTable.TryGetValue(localid, out EntityUpdate existentup))
                    {
                        int eqqueue = existentup.PriorityQueue;

                        existentup.UpdateFromNew(value, pqueue);
                        value.Free();

                        if (pqueue != eqqueue)
                        {
                            m_heaps[eqqueue].RemoveAt(existentup.PriorityQueueIndex);
                            m_heaps[pqueue].Add(existentup);
                        }
                        return true;
                    }

                    ulong entry = m_nextRequest++;
                    value.Update(pqueue, entry);

                    m_heaps[pqueue].Add(value);
                    m_lookupTable[localid] = value;
                }
                return true;
            }
            catch
            {
                return true;
            }
        }

        public void Remove(List<uint> ids)
        {
            try
            {
                lock (m_mainLock)
                {
                    foreach (uint localid in ids)
                    {
                        if (m_lookupTable.TryRemove(localid, out EntityUpdate lookup))
                        {
                            m_heaps[lookup.PriorityQueue].RemoveAt(lookup.PriorityQueueIndex);
                            lookup.Free();
                        }
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Remove an item from one of the queues. Specifically, it removes the
        /// oldest item from the next queue in order to provide fair access to
        /// all of the queues
        /// </summary>
        public bool TryDequeue(out EntityUpdate value)
        {
            // If there is anything in immediate queues, return it first no
            // matter what else. Breaks fairness. But very useful.
            try
            {
                lock(m_mainLock)
                {
                    for (int iq = 0; iq < NumberOfImmediateQueues; iq++)
                    {
                        if (m_heaps[iq].Count > 0)
                        {
                            value = m_heaps[iq].RemoveNext();
                            return m_lookupTable.TryRemove(value.Entity.LocalId, out value);
                        }
                    }

                    // To get the fair queing, we cycle through each of the
                    // queues when finding an element to dequeue.
                    // We pull (NumberOfQueues - QueueIndex) items from each queue in order
                    // to give lower numbered queues a higher priority and higher percentage
                    // of the bandwidth.

                    PriorityMinHeap curheap = m_heaps[m_nextQueue];
                    // Check for more items to be pulled from the current queue
                    if (m_countFromQueue > 0 && curheap.Count > 0)
                    {
                        --m_countFromQueue;

                        value = curheap.RemoveNext();
                        return m_lookupTable.TryRemove(value.Entity.LocalId, out value);
                    }

                    // Find the next non-immediate queue with updates in it
                    for (int i = NumberOfImmediateQueues; i < NumberOfQueues; ++i)
                    {
                        m_nextQueue++;
                        if(m_nextQueue >= NumberOfQueues)
                            m_nextQueue = NumberOfImmediateQueues;
 
                        curheap = m_heaps[m_nextQueue];
                        if (curheap.Count == 0)
                            continue;

                        m_countFromQueue = m_queueCounts[m_nextQueue];
                        --m_countFromQueue;

                        value = curheap.RemoveNext();
                        return m_lookupTable.TryRemove(value.Entity.LocalId, out value);
                    }
                }
            }
            catch
            {
            }
            value = null;
            return false;
        }

        public bool TryOrderedDequeue(out EntityUpdate value)
        {
            try
            {
                lock(m_mainLock)
                {
                    for (int iq = 0; iq < NumberOfQueues; ++iq)
                    {
                        PriorityMinHeap curheap = m_heaps[iq];
                        if (curheap.Count > 0)
                        {
                            value = curheap.RemoveNext();
                            return m_lookupTable.TryRemove(value.Entity.LocalId, out value);
                        }
                    }
                }
            }
            catch
            {
            }
            value = null;
            return false;
        }

        /// <summary>
        /// Reapply the prioritization function to each of the updates currently
        /// stored in the priority queues.
        /// </summary
        public void Reprioritize(UpdatePriorityHandler handler)
        {
            int pqueue = 0;
            try
            {
                lock (m_mainLock)
                {
                    foreach (EntityUpdate currentEU in m_lookupTable.Values)
                    {
                        if (handler(ref pqueue, currentEU.Entity))
                        {
                            // unless the priority queue has changed, there is no need to modify
                            // the entry
                            if (pqueue != currentEU.PriorityQueue)
                            {
                                m_heaps[currentEU.PriorityQueue].RemoveAt(currentEU.PriorityQueueIndex);
                                currentEU.PriorityQueue = pqueue;
                                m_heaps[pqueue].Add(currentEU);
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// </summary>
        public override string ToString()
        {
            string s = "";
            for (int i = 0; i < NumberOfQueues; i++)
                s += String.Format("{0,7} ", m_heaps[i].Count);
            return s;
        }

#endregion PublicMethods
    }

    public class PriorityMinHeap
    {
        public const int MIN_CAPACITY = 16;

        private EntityUpdate[] m_items;
        private int m_size;
        private int minCapacity;

        public PriorityMinHeap(int _capacity)
        {
            minCapacity = MIN_CAPACITY;
            m_items = new EntityUpdate[_capacity];
            m_size = 0;
        }

        public int Count { get { return m_size; } }

        private bool BubbleUp(int index)
        {
            EntityUpdate tmp;
            EntityUpdate item = m_items[index];
            ulong itemEntryOrder = item.EntryOrder;
            int current, parent;

            for (current = index, parent = (current - 1) / 2;
                    (current > 0) && m_items[parent].EntryOrder > itemEntryOrder;
                    current = parent, parent = (current - 1) / 2)
            {
                tmp = m_items[parent];
                tmp.PriorityQueueIndex = current;
                m_items[current] = tmp;
            }

            if (current != index)
            {
                item.PriorityQueueIndex = current;
                m_items[current] = item;
                return true;
            }
            return false;
        }

        private void BubbleDown(int index)
        {
            if(m_size < 2)
                return;

            EntityUpdate childItem;
            EntityUpdate childItemR;
            EntityUpdate item = m_items[index];

            ulong itemEntryOrder = item.EntryOrder;
            int current;
            int child;
            int childlimit = m_size - 1;

            for (current = index, child = (2 * current) + 1;
                        current < m_size / 2;
                        current = child, child = (2 * current) + 1)
            {
                childItem = m_items[child];
                if (child < childlimit)
                {
                    childItemR = m_items[child + 1];

                    if(childItem.EntryOrder > childItemR.EntryOrder)
                    {
                        childItem = childItemR;
                        ++child;
                    }
                }
                if (childItem.EntryOrder >= itemEntryOrder)
                    break;

                childItem.PriorityQueueIndex = current;
                m_items[current] = childItem;
            }

            if (current != index)
            {
                item.PriorityQueueIndex = current;
                m_items[current] = item;
            }
        }

        public void Add(EntityUpdate value)
        {
            if (m_size == m_items.Length)
            {
                int newcapacity = (int)((m_items.Length * 200L) / 100L);
                if (newcapacity < (m_items.Length + MIN_CAPACITY))
                    newcapacity = m_items.Length + MIN_CAPACITY;
                Array.Resize<EntityUpdate>(ref m_items, newcapacity);
            }

            value.PriorityQueueIndex = m_size;
            m_items[m_size] = value;

            BubbleUp(m_size);
            ++m_size;
        }

        public void Clear()
        {
            for (int index = 0; index < m_size; ++index)
                m_items[index].Free();
            m_size = 0;
        }

        public void RemoveAt(int index)
        {
            if (m_size == 0)
                throw new InvalidOperationException("Heap is empty");
            if (index >= m_size)
                throw new ArgumentOutOfRangeException("index");

            --m_size;
            if (m_size > 0)
            {
                if (index != m_size)
                {
                    EntityUpdate tmp = m_items[m_size];
                    tmp.PriorityQueueIndex = index;
                    m_items[index] = tmp;

                    m_items[m_size] = null;
                    if (!BubbleUp(index))
                        BubbleDown(index);
                }
            }
            else if (m_items.Length > 4 * minCapacity)
                m_items = new EntityUpdate[minCapacity];
        }

        public EntityUpdate RemoveNext()
        {
            if (m_size == 0)
                throw new InvalidOperationException("Heap is empty");

            EntityUpdate item = m_items[0];
            --m_size;
            if (m_size > 0)
            {
                EntityUpdate tmp = m_items[m_size];
                tmp.PriorityQueueIndex = 0;
                m_items[0] = tmp;
                m_items[m_size] = null;

                BubbleDown(0);
            }
            else if (m_items.Length > 4 * minCapacity)
                m_items = new EntityUpdate[minCapacity];

            return item;
        }

        public bool Remove(EntityUpdate value)
        {
            int index = value.PriorityQueueIndex;
            if (index != -1)
            {
                RemoveAt(index);
                return true;
            }
            return false;
        }
    }
}
