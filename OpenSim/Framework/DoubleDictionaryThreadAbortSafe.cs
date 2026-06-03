/*
 * Copyright (c) 2008, openmetaverse.org, http://opensimulator.org/
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
using System.Collections.Generic;

namespace OpenSim.Framework
{
    /// <summary>
    /// A double dictionary that is thread abort safe.
    /// </summary>
    /// <remarks>

    public class DoubleDictionaryThreadAbortSafe<TKey1, TKey2, TValue>
    {
        readonly Dictionary<TKey1, TValue> Dictionary1;
        readonly Dictionary<TKey2, TValue> Dictionary2;
        private TValue[] m_array;

        readonly ReaderWriterLockSlim rwLock = new();

        public DoubleDictionaryThreadAbortSafe()
        {
            Dictionary1 = [];
            Dictionary2 = [];
            m_array = null;
        }

        public DoubleDictionaryThreadAbortSafe(int capacity)
        {
            Dictionary1 = new Dictionary<TKey1, TValue>(capacity);
            Dictionary2 = new Dictionary<TKey2, TValue>(capacity);
            m_array = null;
        }

        ~DoubleDictionaryThreadAbortSafe()
        {
            rwLock?.Dispose();
        }

        public void Add(TKey1 key1, TKey2 key2, TValue value)
        {
            rwLock.EnterWriteLock();
            try
            {
                Dictionary1[key1] = value;
                Dictionary2[key2] = value;
                m_array = null;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public bool Remove(TKey1 key1, TKey2 key2)
        {
            rwLock.EnterWriteLock();
            try
            {
                bool success = Dictionary1.Remove(key1);
                success &= Dictionary2.Remove(key2);
                m_array = null;
                return success;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public bool Remove(TKey1 key1, TKey2 key2, out TValue value)
        {
            rwLock.EnterWriteLock();
            try
            {
                bool success = Dictionary1.Remove(key1, out value);
                success &= Dictionary2.Remove(key2);
                m_array = null;
                return success;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public bool Remove(TKey1 key1)
        {
            rwLock.EnterWriteLock();
            try
            {
                // This is an O(n) operation!
                if (Dictionary1.Remove(key1, out TValue value))
                {   
                    m_array = null;
                    foreach (KeyValuePair<TKey2, TValue> kvp in Dictionary2)
                    {
                        if (kvp.Value.Equals(value))
                        {
                            Dictionary2.Remove(kvp.Key);
                            return true;
                        }
                    }
                }
                return false;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public bool Remove(TKey1 key1, out TValue value)
        {
            rwLock.EnterWriteLock();
            try
            {
                // This is an O(n) operation!
                if (Dictionary1.Remove(key1, out value))
                { 
                    m_array = null;
                    foreach (KeyValuePair<TKey2, TValue> kvp in Dictionary2)
                    {
                        if (kvp.Value.Equals(value))
                        {
                            Dictionary2.Remove(kvp.Key);
                            return true;
                        }
                    }
                }
                return false;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public bool Remove(TKey2 key2)
        {
            rwLock.EnterWriteLock();
            try
            {
                // This is an O(n) operation!
                if (Dictionary2.Remove(key2, out TValue value))
                {
                    m_array = null;
                    foreach (KeyValuePair<TKey1, TValue> kvp in Dictionary1)
                    {
                        if (kvp.Value.Equals(value))
                        {
                            try { }
                            finally
                            {
                                Dictionary1.Remove(kvp.Key);
                                
                            }
                            return true;
                        }
                    }
                }
                return false;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public bool Remove(TKey2 key2, out TValue value)
        {
            rwLock.EnterWriteLock();
            try
            {
                // This is an O(n) operation!
                if (Dictionary2.Remove(key2, out value))
                {
                    m_array = null;
                    foreach (KeyValuePair<TKey1, TValue> kvp in Dictionary1)
                    {
                        if (kvp.Value.Equals(value))
                        {
                            Dictionary1.Remove(kvp.Key);
                            return true;
                        }
                    }
                }
                return false;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public void Clear()
        {
            rwLock.EnterWriteLock();
            try
            {
                Dictionary1.Clear();
                Dictionary2.Clear();
                m_array = null;
            }
            finally { rwLock.ExitWriteLock(); }
        }

        public int Count
        {
            get { return Dictionary1.Count; }
        }

        public bool ContainsKey(TKey1 key)
        {
            rwLock.EnterReadLock();
            try
            {
                return Dictionary1.ContainsKey(key);
            }
            finally { rwLock.ExitReadLock(); }
        }

        public bool ContainsKey(TKey2 key)
        {
            rwLock.EnterReadLock();
            try
            {   
                return Dictionary2.ContainsKey(key);
            }
            finally { rwLock.ExitReadLock(); }
        }

        public bool TryGetValue(TKey1 key, out TValue value)
        {
            rwLock.EnterReadLock();
            try
            {
                return Dictionary1.TryGetValue(key, out value);
            }
            finally { rwLock.ExitReadLock(); }
        }

        public bool TryGetValue(TKey2 key, out TValue value)
        {
            rwLock.EnterReadLock();
            try
            {
                return Dictionary2.TryGetValue(key, out value);
            }
            finally { rwLock.ExitReadLock(); }
        }

        public void ForEach(Action<TValue> action)
        {
            TValue[] values = GetArray();
            if(values == null || values.Length == 0)
                return;

            foreach (TValue value in values)
                action(value);
        }

        public void ForEach(Action<KeyValuePair<TKey1, TValue>> action)
        {
            rwLock.EnterReadLock();
            try
            {
                foreach (KeyValuePair<TKey1, TValue> entry in Dictionary1)
                    action(entry);
            }
            finally { rwLock.ExitReadLock(); }
        }

        public void ForEach(Action<KeyValuePair<TKey2, TValue>> action)
        {
            rwLock.EnterReadLock();
            try
            {
                foreach (KeyValuePair<TKey2, TValue> entry in Dictionary2)
                    action(entry);
            }
            finally { rwLock.ExitReadLock(); }
        }

        public TValue FindValue(Predicate<TValue> predicate)
        {
            TValue[] values = GetArray();
            for (int i = 0; i < values.Length; ++i)
            {
                if (predicate(values[i]))
                    return values[i];
            }

            return default;
        }

        public IList<TValue> FindAll(Predicate<TValue> predicate)
        {
            IList<TValue> list = [];
            TValue[] values = GetArray();

            for (int i = 0; i < values.Length; ++i)
            {
                if (predicate(values[i]))
                    list.Add(values[i]);
            }
            return list;
        }

        public int RemoveAll(Predicate<TValue> predicate)
        {
            IList<TKey1> list = [];

            rwLock.EnterUpgradeableReadLock();
            try
            {
                foreach (KeyValuePair<TKey1, TValue> kvp in Dictionary1)
                {
                    if (predicate(kvp.Value))
                        list.Add(kvp.Key);
                }

                IList<TKey2> list2 = new List<TKey2>(list.Count);
                foreach (KeyValuePair<TKey2, TValue> kvp in Dictionary2)
                {
                    if (predicate(kvp.Value))
                        list2.Add(kvp.Key);
                }

                try
                {
                    rwLock.EnterWriteLock();

                    for (int i = 0; i < list.Count; i++)
                        Dictionary1.Remove(list[i]);

                    for (int i = 0; i < list2.Count; i++)
                        Dictionary2.Remove(list2[i]);
                    m_array = null;
                    return list.Count;
                }
                finally { rwLock.ExitWriteLock(); }
            }
            finally { rwLock.ExitUpgradeableReadLock(); }

        }

        public TValue[] GetArray()
        {
            rwLock.EnterUpgradeableReadLock();
            try
            {
                if (m_array == null)
                {
                    rwLock.EnterWriteLock();
                    try
                    {
                        m_array = new TValue[Dictionary1.Count];
                        Dictionary1.Values.CopyTo(m_array, 0);
                    }
                    finally { rwLock.ExitWriteLock(); }
                }
                return m_array;
            }
            catch { return []; }
            finally { rwLock.ExitUpgradeableReadLock(); }
        }
    }
}