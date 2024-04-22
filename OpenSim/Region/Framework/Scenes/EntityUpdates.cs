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
using OpenSim.Framework;

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// Specifies the fields that have been changed when sending a prim or
    /// avatar update
    /// </summary>
    [Flags]
    public enum ObjectPropertyUpdateFlags : byte
    {
        None = 0,
        Family = 1,
        Object = 2,

        NoFamily = unchecked((byte)~Family),
        NoObject = unchecked((byte)~Object)
    }

    public class EntityUpdate
    {
        // for priority queue
        public int PriorityQueue;
        public int PriorityQueueIndex;
        public ulong EntryOrder;

        private ISceneEntity m_entity;
        private PrimUpdateFlags m_flags;
        public ObjectPropertyUpdateFlags m_propsFlags;

        public ObjectPropertyUpdateFlags PropsFlags
        {
            get
            {
                return m_propsFlags;
            }
            set
            {
                m_propsFlags = value;
            }
        }

        public ISceneEntity Entity
        {
            get
            {
                return m_entity;
            }
            internal set
            {
                m_entity = value;
            }
        }

        public PrimUpdateFlags Flags
        {
            get { return m_flags; }
            set { m_flags = value; }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Update(int pqueue, ulong entry)
        {
            if ((m_flags & PrimUpdateFlags.CancelKill) != 0)
            {
                if ((m_flags & PrimUpdateFlags.UpdateProbe) != 0)
                    m_flags = PrimUpdateFlags.UpdateProbe;
                else
                    m_flags = PrimUpdateFlags.FullUpdatewithAnim;
            }

            PriorityQueue = pqueue;
            EntryOrder = entry;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void UpdateFromNew(EntityUpdate newupdate, int pqueue)
        {
            m_propsFlags |= newupdate.PropsFlags;
            PrimUpdateFlags newFlags = newupdate.Flags;

            if ((newFlags & PrimUpdateFlags.UpdateProbe) != 0)
                m_flags &= ~PrimUpdateFlags.UpdateProbe;

            if ((newFlags & PrimUpdateFlags.CancelKill) != 0)
            {
                if ((newFlags & PrimUpdateFlags.UpdateProbe) != 0)
                    m_flags = PrimUpdateFlags.UpdateProbe;
                else
                    m_flags = PrimUpdateFlags.FullUpdatewithAnim;
            }
            else
                m_flags |= newFlags;

            PriorityQueue = pqueue;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Free()
        {
            m_entity = null;
            PriorityQueueIndex = -1;
            EntityUpdatesPool.Free(this);
        }

        public EntityUpdate(ISceneEntity entity, PrimUpdateFlags flags)
        {
            m_entity = entity;
            m_flags = flags;
        }

        public EntityUpdate(ISceneEntity entity, PrimUpdateFlags flags, bool sendfam, bool sendobj)
        {
            m_entity = entity;
            m_flags = flags;

            if (sendfam)
                m_propsFlags |= ObjectPropertyUpdateFlags.Family;

            if (sendobj)
                m_propsFlags |= ObjectPropertyUpdateFlags.Object;
        }

        public override string ToString()
        {
            return String.Format("[{0},{1},{2}]", PriorityQueue, EntryOrder, m_entity.LocalId);
        }
    }

    public static class EntityUpdatesPool
    {
        const int MAXSIZE = 32768;
        const int PREALLOC = 16384;
        private static readonly EntityUpdate[] m_pool = new EntityUpdate[MAXSIZE];
        private static readonly object m_poollock = new object();
        private static int m_poolPtr;
        //private static int m_min = int.MaxValue;
        //private static int m_max = int.MinValue;

        static EntityUpdatesPool()
        {
            for(int i = 0; i < PREALLOC; ++i)
                m_pool[i] = new EntityUpdate(null, 0);
            m_poolPtr = PREALLOC - 1;
        }

        public static EntityUpdate Get(ISceneEntity entity, PrimUpdateFlags flags)
        {
            lock (m_poollock)
            {
                if (m_poolPtr >= 0)
                {
                    EntityUpdate eu = m_pool[m_poolPtr];
                    m_pool[m_poolPtr] = null;
                    m_poolPtr--;
                    //if (m_min > m_poolPtr)
                    //    m_min = m_poolPtr;
                    eu.Entity = entity;
                    eu.Flags = flags;
                    return eu;
                }
            }
            return new EntityUpdate(entity, flags);
        }

        public static EntityUpdate Get(ISceneEntity entity, PrimUpdateFlags flags, bool sendfam, bool sendobj)
        {
            lock (m_poollock)
            {
                if (m_poolPtr >= 0)
                {
                    EntityUpdate eu = m_pool[m_poolPtr];
                    m_pool[m_poolPtr] = null;
                    m_poolPtr--;
                    //if (m_min > m_poolPtr)
                    //    m_min = m_poolPtr;
                    eu.Entity = entity;
                    eu.Flags = flags;
                    ObjectPropertyUpdateFlags tmp = 0;
                    if (sendfam)
                        tmp |= ObjectPropertyUpdateFlags.Family;

                    if (sendobj)
                        tmp |= ObjectPropertyUpdateFlags.Object;

                    eu.PropsFlags = tmp;
                    return eu;
                }
            }
            return new EntityUpdate(entity, flags, sendfam, sendobj);
        }

        public static void Free(EntityUpdate eu)
        {
            lock (m_poollock)
            {
                if (m_poolPtr < MAXSIZE - 1)
                {
                    m_poolPtr++;
                    //if (m_max < m_poolPtr)
                    //    m_max = m_poolPtr;
                    m_pool[m_poolPtr] = eu;
                }
            }
        }
    }
}
