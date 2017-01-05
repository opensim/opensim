/*
 * Copyright (c) 2009, openmetaverse.org
 * All rights reserved.
 *
 * - Redistribution and use in source and binary forms, with or without
 *   modification, are permitted provided that the following conditions are met:
 *
 * - Redistributions of source code must retain the above copyright notice, this
 *   list of conditions and the following disclaimer.
 * - Neither the name of the openmetaverse.org nor the names
 *   of its contributors may be used to endorse or promote products derived from
 *   this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Threading;

namespace OpenSim.Framework
{
    public class LocklessQueue<T>
    {
        private sealed class SingleLinkNode
        {
            public SingleLinkNode Next;
            public T Item;
        }

        SingleLinkNode head;
        SingleLinkNode tail;
        int count;

        public virtual int Count { get { return count; } }

        public LocklessQueue()
        {
            Init();
        }

        public void Enqueue(T item)
        {
            SingleLinkNode oldTail = null;
            SingleLinkNode oldTailNext;

            SingleLinkNode newNode = new SingleLinkNode();
            newNode.Item = item;

            bool newNodeWasAdded = false;

            while (!newNodeWasAdded)
            {
                oldTail = tail;
                oldTailNext = oldTail.Next;

                if (tail == oldTail)
                {
                    if (oldTailNext == null)
                        newNodeWasAdded = CAS(ref tail.Next, null, newNode);
                    else
                        CAS(ref tail, oldTail, oldTailNext);
                }
            }

            CAS(ref tail, oldTail, newNode);
            Interlocked.Increment(ref count);
        }

        public virtual bool Dequeue(out T item)
        {
            item = default(T);
            SingleLinkNode oldHead = null;
            bool haveAdvancedHead = false;

            while (!haveAdvancedHead)
            {
                oldHead = head;
                SingleLinkNode oldTail = tail;
                SingleLinkNode oldHeadNext = oldHead.Next;

                if (oldHead == head)
                {
                    if (oldHead == oldTail)
                    {
                        if (oldHeadNext == null)
                        {
                            count = 0;
                            return false;
                        }

                        CAS(ref tail, oldTail, oldHeadNext);
                    }
                    else
                    {
                        item = oldHeadNext.Item;
                        haveAdvancedHead = CAS(ref head, oldHead, oldHeadNext);
                        if (haveAdvancedHead)
                        {
                            oldHeadNext.Item = default(T);
                            oldHead.Next = null;
                        }
                    }
                }
            }

            Interlocked.Decrement(ref count);
            return true;
        }

        public void Clear()
        {
            // ugly
            T item;
            while(Dequeue(out item));
            Init();
        }

        private void Init()
        {
            count = 0;
            head = tail = new SingleLinkNode();
        }

        private static bool CAS(ref SingleLinkNode location, SingleLinkNode comparand, SingleLinkNode newValue)
        {
            return
                (object)comparand ==
                (object)Interlocked.CompareExchange<SingleLinkNode>(ref location, newValue, comparand);
        }
    }
}
