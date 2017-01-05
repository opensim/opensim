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
using OpenMetaverse;

namespace OpenSim.Framework
{
    // The delegate we will use for performing fetch from backing store
    //
    public delegate Object FetchDelegate(string index);
    public delegate bool ExpireDelegate(string index);

    // Strategy
    //
    // Conservative = Minimize memory. Expire items quickly.
    // Balanced = Expire items with few hits quickly.
    // Aggressive = Keep cache full. Expire only when over 90% and adding
    //
    public enum CacheStrategy
    {
        Conservative = 0,
        Balanced = 1,
        Aggressive = 2
    }

    // Select classes to store data on different media
    //
    public enum CacheMedium
    {
        Memory = 0,
        File = 1
    }

    public enum CacheFlags
    {
        CacheMissing = 1,
        AllowUpdate = 2
    }

    // The base class of all cache objects. Implements comparison and sorting
    // by the string member.
    //
    // This is not abstract because we need to instantiate it briefly as a
    // method parameter
    //
    public class CacheItemBase : IEquatable<CacheItemBase>, IComparable<CacheItemBase>
    {
        public string uuid;
        public DateTime entered;
        public DateTime lastUsed;
        public DateTime expires = new DateTime(0);
        public int hits = 0;

        public virtual Object Retrieve()
        {
            return null;
        }

        public virtual void Store(Object data)
        {
        }

        public CacheItemBase(string index)
        {
            uuid = index;
            entered = DateTime.UtcNow;
            lastUsed = entered;
        }

        public CacheItemBase(string index, DateTime ttl)
        {
            uuid = index;
            entered = DateTime.UtcNow;
            lastUsed = entered;
            expires = ttl;
        }

        public virtual bool Equals(CacheItemBase item)
        {
            return uuid == item.uuid;
        }

        public virtual int CompareTo(CacheItemBase item)
        {
            return uuid.CompareTo(item.uuid);
        }

        public virtual bool IsLocked()
        {
            return false;
        }
    }

    // Simple in-memory storage. Boxes the object and stores it in a variable
    //
    public class MemoryCacheItem : CacheItemBase
    {
        private Object m_Data;

        public MemoryCacheItem(string index) :
            base(index)
        {
        }

        public MemoryCacheItem(string index, DateTime ttl) :
            base(index, ttl)
        {
        }

        public MemoryCacheItem(string index, Object data) :
            base(index)
        {
            Store(data);
        }

        public MemoryCacheItem(string index, DateTime ttl, Object data) :
            base(index, ttl)
        {
            Store(data);
        }

        public override Object Retrieve()
        {
            return m_Data;
        }

        public override void Store(Object data)
        {
            m_Data = data;
        }
    }

    // Simple persistent file storage
    //
    public class FileCacheItem : CacheItemBase
    {
        public FileCacheItem(string index) :
            base(index)
        {
        }

        public FileCacheItem(string index, DateTime ttl) :
            base(index, ttl)
        {
        }

        public FileCacheItem(string index, Object data) :
            base(index)
        {
            Store(data);
        }

        public FileCacheItem(string index, DateTime ttl, Object data) :
            base(index, ttl)
        {
            Store(data);
        }

        public override Object Retrieve()
        {
            //TODO: Add file access code
            return null;
        }

        public override void Store(Object data)
        {
            //TODO: Add file access code
        }
    }

    // The main cache class. This is the class you instantiate to create
    // a cache
    //
    public class Cache
    {
        /// <summary>
        /// Must only be accessed under lock.
        /// </summary>
        private List<CacheItemBase> m_Index = new List<CacheItemBase>();

        /// <summary>
        /// Must only be accessed under m_Index lock.
        /// </summary>
        private Dictionary<string, CacheItemBase> m_Lookup =
            new Dictionary<string, CacheItemBase>();

        private CacheStrategy m_Strategy;
        private CacheMedium m_Medium;
        private CacheFlags m_Flags = 0;
        private int m_Size = 1024;
        private TimeSpan m_DefaultTTL = new TimeSpan(0);
        private DateTime m_nextExpire;
        private TimeSpan m_expiresTime = new TimeSpan(0,0,30);
        public ExpireDelegate OnExpire;

        // Comparison interfaces
        //
        private class SortLRU : IComparer<CacheItemBase>
        {
            public int Compare(CacheItemBase a, CacheItemBase b)
            {
                if (a == null && b == null)
                    return 0;
                if (a == null)
                    return -1;
                if (b == null)
                    return 1;

                return(a.lastUsed.CompareTo(b.lastUsed));
            }
        }
        // same as above, reverse order
        private class SortLRUrev : IComparer<CacheItemBase>
        {
            public int Compare(CacheItemBase a, CacheItemBase b)
            {
                if (a == null && b == null)
                    return 0;
                if (a == null)
                    return -1;
                if (b == null)
                    return 1;

                return(b.lastUsed.CompareTo(a.lastUsed));
            }
        }

        // Convenience constructors
        //
        public Cache()
        {
            m_Strategy = CacheStrategy.Balanced;
            m_Medium = CacheMedium.Memory;
            m_Flags = 0;
            m_nextExpire = DateTime.UtcNow + m_expiresTime;
            m_Strategy = CacheStrategy.Aggressive;
        }

        public Cache(CacheMedium medium) :
            this(medium, CacheStrategy.Balanced)
        {
        }

        public Cache(CacheMedium medium, CacheFlags flags) :
            this(medium, CacheStrategy.Balanced, flags)
        {
        }

        public Cache(CacheMedium medium, CacheStrategy strategy) :
            this(medium, strategy, 0)
        {
        }

        public Cache(CacheStrategy strategy, CacheFlags flags) :
            this(CacheMedium.Memory, strategy, flags)
        {
        }

        public Cache(CacheFlags flags) :
            this(CacheMedium.Memory, CacheStrategy.Balanced, flags)
        {
        }

        public Cache(CacheMedium medium, CacheStrategy strategy,
                CacheFlags flags)
        {
            m_Strategy = strategy;
            m_Medium = medium;
            m_Flags = flags;
        }

        // Count of the items currently in cache
        //
        public int Count
        {
            get { lock (m_Index) { return m_Index.Count; } }
        }

        // Maximum number of items this cache will hold
        //
        public int Size
        {
            get { return m_Size; }
            set { SetSize(value); }
        }

        private void SetSize(int newSize)
        {
            lock (m_Index)
            {
                int target = newSize;
                if(m_Strategy == CacheStrategy.Aggressive)
                    target = (int)(newSize * 0.9);

                if(Count > target)
                {
                    m_Index.Sort(new SortLRUrev());

                    m_Index.RemoveRange(newSize, Count - target);

                    m_Lookup.Clear();

                    foreach (CacheItemBase item in m_Index)
                        m_Lookup[item.uuid] = item;
                }
                m_Size = newSize;

            }
        }

        public TimeSpan DefaultTTL
        {
            get { return m_DefaultTTL; }
            set { m_DefaultTTL = value; }
        }

        // Get an item from cache. Return the raw item, not it's data
        //
        protected virtual CacheItemBase GetItem(string index)
        {
            CacheItemBase item = null;

            lock (m_Index)
            {
                if (m_Lookup.ContainsKey(index))
                    item = m_Lookup[index];

                if (item == null)
                {
                    Expire(true);
                    return null;
                }

                item.hits++;
                item.lastUsed = DateTime.UtcNow;

                Expire(true);
            }

            return item;
        }

        // Get an item from cache. Do not try to fetch from source if not
        // present. Just return null
        //
        public virtual Object Get(string index)
        {
            CacheItemBase item = GetItem(index);

            if (item == null)
                return null;

            return item.Retrieve();
        }

        // Fetch an object from backing store if not cached, serve from
        // cache if it is.
        //
        public virtual Object Get(string index, FetchDelegate fetch)
        {
            CacheItemBase item = GetItem(index);
            if (item != null)
                return item.Retrieve();

            Object data = fetch(index);

            if (data == null && (m_Flags & CacheFlags.CacheMissing) == 0)
                return null;

            lock (m_Index)
            {
               CacheItemBase missing = new CacheItemBase(index);
               if (!m_Index.Contains(missing))
               {
                   m_Index.Add(missing);
                   m_Lookup[index] = missing;
               }
            }

            Store(index, data);
            return data;
        }

        // Find an object in cache by delegate.
        //
        public Object Find(Predicate<CacheItemBase> d)
        {
            CacheItemBase item;

            lock (m_Index)
                item = m_Index.Find(d);

            if (item == null)
                return null;

            return item.Retrieve();
        }

        public virtual void Store(string index, Object data)
        {
            Type container;

            switch (m_Medium)
            {
            case CacheMedium.Memory:
                container = typeof(MemoryCacheItem);
                break;
            case CacheMedium.File:
                return;
            default:
                return;
            }

            Store(index, data, container);
        }

        public virtual void Store(string index, Object data, Type container)
        {
            Store(index, data, container, new Object[] { index });
        }

        public virtual void Store(string index, Object data, Type container,
                Object[] parameters)
        {
            CacheItemBase item;

            lock (m_Index)
            {
                Expire(false);

                if (m_Index.Contains(new CacheItemBase(index)))
                {
                    if ((m_Flags & CacheFlags.AllowUpdate) != 0)
                    {
                        item = GetItem(index);

                        item.hits++;
                        item.lastUsed = DateTime.UtcNow;
                        if (m_DefaultTTL.Ticks != 0)
                            item.expires = DateTime.UtcNow + m_DefaultTTL;

                        item.Store(data);
                    }
                    return;
                }

                item = (CacheItemBase)Activator.CreateInstance(container,
                        parameters);

                if (m_DefaultTTL.Ticks != 0)
                    item.expires = DateTime.UtcNow + m_DefaultTTL;

                m_Index.Add(item);
                m_Lookup[index] = item;
            }

            item.Store(data);
        }

        /// <summary>
        /// Expire items as appropriate.
        /// </summary>
        /// <remarks>
        /// Callers must lock m_Index.
        /// </remarks>
        /// <param name='getting'></param>
        protected virtual void Expire(bool getting)
        {
            if (getting && (m_Strategy == CacheStrategy.Aggressive))
                return;

            DateTime now = DateTime.UtcNow;
            if(now < m_nextExpire)
                return;

            m_nextExpire = now + m_expiresTime;

            if (m_DefaultTTL.Ticks != 0)
            {
                foreach (CacheItemBase item in new List<CacheItemBase>(m_Index))
                {
                    if (item.expires.Ticks == 0 ||
                            item.expires <= now)
                    {
                        m_Index.Remove(item);
                        m_Lookup.Remove(item.uuid);
                    }
                }
            }

            switch (m_Strategy)
            {
                case CacheStrategy.Aggressive:
                    int target = (int)((float)Size * 0.9);
                    if (Count < target) // Cover ridiculous cache sizes
                        return;

                    target = (int)((float)Size * 0.8);

                    m_Index.Sort(new SortLRUrev());

                    ExpireDelegate doExpire = OnExpire;

                    if (doExpire != null)
                    {
                        List<CacheItemBase> candidates =
                                m_Index.GetRange(target, Count - target);

                        foreach (CacheItemBase i in candidates)
                        {
                            if (doExpire(i.uuid))
                            {
                                m_Index.Remove(i);
                                m_Lookup.Remove(i.uuid);
                            }
                        }
                    }
                    else
                    {
                        m_Index.RemoveRange(target, Count - target);

                        m_Lookup.Clear();

                        foreach (CacheItemBase item in m_Index)
                            m_Lookup[item.uuid] = item;
                    }

                    break;

                    default:
                        break;
            }
        }

        public void Invalidate(string uuid)
        {
            lock (m_Index)
            {
                if (!m_Lookup.ContainsKey(uuid))
                    return;

                CacheItemBase item = m_Lookup[uuid];
                m_Lookup.Remove(uuid);
                m_Index.Remove(item);
            }
        }

        public void Clear()
        {
            lock (m_Index)
            {
                m_Index.Clear();
                m_Lookup.Clear();
            }
        }
    }
}