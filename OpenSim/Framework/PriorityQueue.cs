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

using OpenSim.Framework;
using OpenSim.Framework.Client;
using log4net;

namespace OpenSim.Framework
{
    public class PriorityQueue
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public delegate bool UpdatePriorityHandler(ref uint priority, ISceneEntity entity);

        /// <summary>
        /// Total number of queues (priorities) available
        /// </summary>

        public const uint NumberOfQueues = 12; // includes immediate queues, m_queueCounts need to be set acording

        /// <summary>
        /// Number of queuest (priorities) that are processed immediately
        /// </summary.
        public const uint NumberOfImmediateQueues = 2;

        private MinHeap<MinHeapItem>[] m_heaps = new MinHeap<MinHeapItem>[NumberOfQueues];
        private Dictionary<uint, LookupItem> m_lookupTable;

        // internal state used to ensure the deqeues are spread across the priority
        // queues "fairly". queuecounts is the amount to pull from each queue in
        // each pass. weighted towards the higher priority queues
        private uint m_nextQueue = 0;
        private uint m_countFromQueue = 0;
        // first queues are imediate, so no counts
//        private uint[] m_queueCounts = { 0, 0,          8,  4,  4, 2,  2,   2,  2,  1,  1, 1 };
        private uint[] m_queueCounts = {0,   0,   8,      8,  5,  4, 3,  2,   1,  1,  1,  1};
        // this is                     ava, ava, attach, <10m, 20,40,80,160m,320,640,1280, +

        // next request is a counter of the number of updates queued, it provides
        // a total ordering on the updates coming through the queue and is more
        // lightweight (and more discriminating) than tick count
        private UInt64 m_nextRequest = 0;

        /// <summary>
        /// Lock for enqueue and dequeue operations on the priority queue
        /// </summary>
        private object m_syncRoot = new object();
        public object SyncRoot {
            get { return this.m_syncRoot; }
        }

#region constructor
        public PriorityQueue() : this(MinHeap<MinHeapItem>.DEFAULT_CAPACITY) { }

        public PriorityQueue(int capacity)
        {
            m_lookupTable = new Dictionary<uint, LookupItem>(capacity);

            for (int i = 0; i < m_heaps.Length; ++i)
                m_heaps[i] = new MinHeap<MinHeapItem>(capacity);

            m_nextQueue = NumberOfImmediateQueues;
            m_countFromQueue = m_queueCounts[m_nextQueue];
        }
#endregion Constructor

#region PublicMethods
        /// <summary>
        /// Return the number of items in the queues
        /// </summary>
        public int Count
        {
            get
            {
                int count = 0;
                for (int i = 0; i < m_heaps.Length; ++i)
                    count += m_heaps[i].Count;

                return count;
            }
        }

        /// <summary>
        /// Enqueue an item into the specified priority queue
        /// </summary>
        public bool Enqueue(uint pqueue, EntityUpdate value)
        {
            LookupItem lookup;

            uint localid = value.Entity.LocalId;
            UInt64 entry = m_nextRequest++;
            if (m_lookupTable.TryGetValue(localid, out lookup))
            {
                entry = lookup.Heap[lookup.Handle].EntryOrder;
                value.Update(lookup.Heap[lookup.Handle].Value);
                lookup.Heap.Remove(lookup.Handle);
            }

            pqueue = Util.Clamp<uint>(pqueue, 0, NumberOfQueues - 1);
            lookup.Heap = m_heaps[pqueue];
            lookup.Heap.Add(new MinHeapItem(pqueue, entry, value), ref lookup.Handle);
            m_lookupTable[localid] = lookup;

            return true;
        }


        public void Remove(List<uint> ids)
        {
            LookupItem lookup;

            foreach (uint localid in ids)
            {
                if (m_lookupTable.TryGetValue(localid, out lookup))
                {
                    lookup.Heap.Remove(lookup.Handle);
                    m_lookupTable.Remove(localid);
                }
            }
        }

        /// <summary>
        /// Remove an item from one of the queues. Specifically, it removes the
        /// oldest item from the next queue in order to provide fair access to
        /// all of the queues
        /// </summary>
        public bool TryDequeue(out EntityUpdate value, out Int32 timeinqueue)
        {
            // If there is anything in imediate queues, return it first no
            // matter what else. Breaks fairness. But very useful.
            for (int iq = 0; iq < NumberOfImmediateQueues; iq++)
            {
                if (m_heaps[iq].Count > 0)
                {
                    MinHeapItem item = m_heaps[iq].RemoveMin();
                    m_lookupTable.Remove(item.Value.Entity.LocalId);
                    timeinqueue = Util.EnvironmentTickCountSubtract(item.EntryTime);
                    value = item.Value;

                    return true;
                }
            }

            // To get the fair queing, we cycle through each of the
            // queues when finding an element to dequeue.
            // We pull (NumberOfQueues - QueueIndex) items from each queue in order
            // to give lower numbered queues a higher priority and higher percentage
            // of the bandwidth.

            // Check for more items to be pulled from the current queue
            if (m_heaps[m_nextQueue].Count > 0 && m_countFromQueue > 0)
            {
                m_countFromQueue--;

                MinHeapItem item = m_heaps[m_nextQueue].RemoveMin();
                m_lookupTable.Remove(item.Value.Entity.LocalId);
                timeinqueue = Util.EnvironmentTickCountSubtract(item.EntryTime);
                value = item.Value;

                return true;
            }

            // Find the next non-immediate queue with updates in it
            for (uint i = NumberOfImmediateQueues; i < NumberOfQueues; ++i)
            {
                m_nextQueue++;
                if(m_nextQueue >= NumberOfQueues)
                    m_nextQueue = NumberOfImmediateQueues;

                m_countFromQueue = m_queueCounts[m_nextQueue];

                if (m_heaps[m_nextQueue].Count > 0)
                {
                    m_countFromQueue--;

                    MinHeapItem item = m_heaps[m_nextQueue].RemoveMin();
                    m_lookupTable.Remove(item.Value.Entity.LocalId);
                    timeinqueue = Util.EnvironmentTickCountSubtract(item.EntryTime);
                    value = item.Value;
                    return true;
                }
            }

            timeinqueue = 0;
            value = default(EntityUpdate);
            return false;
        }

        /// <summary>
        /// Reapply the prioritization function to each of the updates currently
        /// stored in the priority queues.
        /// </summary
        public void Reprioritize(UpdatePriorityHandler handler)
        {
            MinHeapItem item;
            foreach (LookupItem lookup in new List<LookupItem>(this.m_lookupTable.Values))
            {
                if (lookup.Heap.TryGetValue(lookup.Handle, out item))
                {
                    uint pqueue = item.PriorityQueue;
                    uint localid = item.Value.Entity.LocalId;

                    if (handler(ref pqueue, item.Value.Entity))
                    {
                        // unless the priority queue has changed, there is no need to modify
                        // the entry
                        pqueue = Util.Clamp<uint>(pqueue, 0, NumberOfQueues - 1);
                        if (pqueue != item.PriorityQueue)
                        {
                            lookup.Heap.Remove(lookup.Handle);

                            LookupItem litem = lookup;
                            litem.Heap = m_heaps[pqueue];
                            litem.Heap.Add(new MinHeapItem(pqueue, item), ref litem.Handle);
                            m_lookupTable[localid] = litem;
                        }
                    }
                    else
                    {
                        // m_log.WarnFormat("[PQUEUE]: UpdatePriorityHandler returned false for {0}",item.Value.Entity.UUID);
                        lookup.Heap.Remove(lookup.Handle);
                        this.m_lookupTable.Remove(localid);
                    }
                }
            }
        }

        /// <summary>
        /// </summary>
        public override string ToString()
        {
            string s = "";
            for (int i = 0; i < NumberOfQueues; i++)
                s += String.Format("{0,7} ",m_heaps[i].Count);
            return s;
        }

#endregion PublicMethods

#region MinHeapItem
        private struct MinHeapItem : IComparable<MinHeapItem>
        {
            private EntityUpdate value;
            internal EntityUpdate Value {
                get {
                    return this.value;
                }
            }

            private uint pqueue;
            internal uint PriorityQueue {
                get {
                    return this.pqueue;
                }
            }

            private Int32 entrytime;
            internal Int32 EntryTime {
                get {
                    return this.entrytime;
                }
            }

            private UInt64 entryorder;
            internal UInt64 EntryOrder
            {
                get {
                    return this.entryorder;
                }
            }

            internal MinHeapItem(uint pqueue, MinHeapItem other)
            {
                this.entrytime = other.entrytime;
                this.entryorder = other.entryorder;
                this.value = other.value;
                this.pqueue = pqueue;
            }

            internal MinHeapItem(uint pqueue, UInt64 entryorder, EntityUpdate value)
            {
                this.entrytime = Util.EnvironmentTickCount();
                this.entryorder = entryorder;
                this.value = value;
                this.pqueue = pqueue;
            }

            public override string ToString()
            {
                return String.Format("[{0},{1},{2}]",pqueue,entryorder,value.Entity.LocalId);
            }

            public int CompareTo(MinHeapItem other)
            {
                // I'm assuming that the root part of an SOG is added to the update queue
                // before the component parts
                return Comparer<UInt64>.Default.Compare(this.EntryOrder, other.EntryOrder);
            }
        }
#endregion

#region LookupItem
        private struct LookupItem
        {
            internal MinHeap<MinHeapItem> Heap;
            internal IHandle Handle;
        }
#endregion
    }
}
