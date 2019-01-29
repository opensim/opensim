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
    /// This adapts OpenMetaverse.DoubleDictionary to be thread-abort safe by acquiring ReaderWriterLockSlim within
    /// a finally section (which can't be interrupted by Thread.Abort()).
    /// </remarks>
    public class DoubleDictionaryThreadAbortSafe<TKey1, TKey2, TValue>
    {
        Dictionary<TKey1, TValue> Dictionary1;
        Dictionary<TKey2, TValue> Dictionary2;
        private TValue[] m_array;

        ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();

        public DoubleDictionaryThreadAbortSafe()
        {
            Dictionary1 = new Dictionary<TKey1,TValue>();
            Dictionary2 = new Dictionary<TKey2,TValue>();
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
            if(rwLock != null)
                rwLock.Dispose();
        }

        public void Add(TKey1 key1, TKey2 key2, TValue value)
        {
            bool gotLock = false;

            try
            {
                // Avoid an asynchronous Thread.Abort() from possibly never existing an acquired lock by placing
                // the acquision inside the main try.  The inner finally block is needed because thread aborts cannot
                // interrupt code in these blocks (hence gotLock is guaranteed to be set correctly).
                try {}
                finally
                {
                    rwLock.EnterWriteLock();
                    gotLock = true;
                }
/*
                    if (Dictionary1.ContainsKey(key1))
                    {
                        if (!Dictionary2.ContainsKey(key2))
                            throw new ArgumentException("key1 exists in the dictionary but not key2");
                    }
                    else if (Dictionary2.ContainsKey(key2))
                    {
                        if (!Dictionary1.ContainsKey(key1))
                            throw new ArgumentException("key2 exists in the dictionary but not key1");
                    }
*/
                    Dictionary1[key1] = value;
                    Dictionary2[key2] = value;
                    m_array = null;
            }
            finally
            {
                if (gotLock)
                    rwLock.ExitWriteLock();
            }
        }

        public bool Remove(TKey1 key1, TKey2 key2)
        {
            bool success;
            bool gotLock = false;

            try
            {
                // Avoid an asynchronous Thread.Abort() from possibly never existing an acquired lock by placing
                // the acquision inside the main try.  The inner finally block is needed because thread aborts cannot
                // interrupt code in these blocks (hence gotLock is guaranteed to be set correctly).
                try {}
                finally
                {
                    rwLock.EnterWriteLock();
                    gotLock = true;
                }
                success = Dictionary1.Remove(key1);
                success &= Dictionary2.Remove(key2);
                m_array = null;
            }
            finally
            {
                if (gotLock)
                    rwLock.ExitWriteLock();
            }

            return success;
        }

        public bool Remove(TKey1 key1)
        {
            bool found = false;
            bool gotLock = false;

            try
            {
                // Avoid an asynchronous Thread.Abort() from possibly never existing an acquired lock by placing
                // the acquision inside the main try.  The inner finally block is needed because thread aborts cannot
                // interrupt code in these blocks (hence gotLock is guaranteed to be set correctly).
                try {}
                finally
                {
                    rwLock.EnterWriteLock();
                    gotLock = true;
                }

                // This is an O(n) operation!
                TValue value;
                if (Dictionary1.TryGetValue(key1, out value))
                {
                    foreach (KeyValuePair<TKey2, TValue> kvp in Dictionary2)
                    {
                        if (kvp.Value.Equals(value))
                        {
                            try { }
                            finally
                            {
                                Dictionary1.Remove(key1);
                                Dictionary2.Remove(kvp.Key);
                                m_array = null;
                            }
                            found = true;
                            break;
                        }
                    }
                }
            }
            finally
            {
                if (gotLock)
                    rwLock.ExitWriteLock();
            }

            return found;
        }

        public bool Remove(TKey2 key2)
        {
            bool found = false;
            bool gotLock = false;

            try
            {
                // Avoid an asynchronous Thread.Abort() from possibly never existing an acquired lock by placing
                // the acquision inside the main try.  The inner finally block is needed because thread aborts cannot
                // interrupt code in these blocks (hence gotLock is guaranteed to be set correctly).
                try {}
                finally
                {
                    rwLock.EnterWriteLock();
                    gotLock = true;
                }

                // This is an O(n) operation!
                TValue value;
                if (Dictionary2.TryGetValue(key2, out value))
                {
                    foreach (KeyValuePair<TKey1, TValue> kvp in Dictionary1)
                    {
                        if (kvp.Value.Equals(value))
                        {
                            try { }
                            finally
                            {
                                Dictionary2.Remove(key2);
                                Dictionary1.Remove(kvp.Key);
                                m_array = null;
                            }
                            found = true;
                            break;
                        }
                    }
                }
            }
            finally
            {
                if (gotLock)
                    rwLock.ExitWriteLock();
            }

            return found;
        }

        public void Clear()
        {
            bool gotLock = false;

            try
            {
                // Avoid an asynchronous Thread.Abort() from possibly never existing an acquired lock by placing
                // the acquision inside the main try.  The inner finally block is needed because thread aborts cannot
                // interrupt code in these blocks (hence gotLock is guaranteed to be set correctly).
                try {}
                finally
                {
                    rwLock.EnterWriteLock();
                    gotLock = true;
                    Dictionary1.Clear();
                    Dictionary2.Clear();
                    m_array = null;
                }
            }
            finally
            {
                if (gotLock)
                    rwLock.ExitWriteLock();
            }
        }

        public int Count
        {
            get { return Dictionary1.Count; }
        }

        public bool ContainsKey(TKey1 key)
        {
            return Dictionary1.ContainsKey(key);
        }

        public bool ContainsKey(TKey2 key)
        {
            return Dictionary2.ContainsKey(key);
        }

        public bool TryGetValue(TKey1 key, out TValue value)
        {
            bool success;
            bool gotLock = false;

            try
            {
                // Avoid an asynchronous Thread.Abort() from possibly never existing an acquired lock by placing
                // the acquision inside the main try.  The inner finally block is needed because thread aborts cannot
                // interrupt code in these blocks (hence gotLock is guaranteed to be set correctly).
                try {}
                finally
                {
                    rwLock.EnterReadLock();
                    gotLock = true;
                }

                success = Dictionary1.TryGetValue(key, out value);
            }
            finally
            {
                if (gotLock)
                    rwLock.ExitReadLock();
            }

            return success;
        }

        public bool TryGetValue(TKey2 key, out TValue value)
        {
            bool success;
            bool gotLock = false;

            try
            {
                // Avoid an asynchronous Thread.Abort() from possibly never existing an acquired lock by placing
                // the acquision inside the main try.  The inner finally block is needed because thread aborts cannot
                // interrupt code in these blocks (hence gotLock is guaranteed to be set correctly).
                try {}
                finally
                {
                    rwLock.EnterReadLock();
                    gotLock = true;
                }

                success = Dictionary2.TryGetValue(key, out value);
            }
            finally
            {
                if (gotLock)
                    rwLock.ExitReadLock();
            }

            return success;
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
            bool gotLock = false;

            try
            {
                // Avoid an asynchronous Thread.Abort() from possibly never existing an acquired lock by placing
                // the acquision inside the main try.  The inner finally block is needed because thread aborts cannot
                // interrupt code in these blocks (hence gotLock is guaranteed to be set correctly).
                try {}
                finally
                {
                    rwLock.EnterReadLock();
                    gotLock = true;
                }

                foreach (KeyValuePair<TKey1, TValue> entry in Dictionary1)
                    action(entry);
            }
            finally
            {
                if (gotLock)
                    rwLock.ExitReadLock();
            }
        }

        public void ForEach(Action<KeyValuePair<TKey2, TValue>> action)
        {
            bool gotLock = false;

            try
            {
                // Avoid an asynchronous Thread.Abort() from possibly never existing an acquired lock by placing
                // the acquision inside the main try.  The inner finally block is needed because thread aborts cannot
                // interrupt code in these blocks (hence gotLock is guaranteed to be set correctly).
                try {}
                finally
                {
                    rwLock.EnterReadLock();
                    gotLock = true;
                }

                foreach (KeyValuePair<TKey2, TValue> entry in Dictionary2)
                    action(entry);
            }
            finally
            {
                if (gotLock)
                    rwLock.ExitReadLock();
            }
        }

        public TValue FindValue(Predicate<TValue> predicate)
        {
            TValue[] values = GetArray();
            int len = values.Length;
            for (int i = 0; i < len; ++i)
            {
                if (predicate(values[i]))
                    return values[i];
            }

            return default(TValue);
        }

        public IList<TValue> FindAll(Predicate<TValue> predicate)
        {
            IList<TValue> list = new List<TValue>();
            TValue[] values = GetArray();

            int len = values.Length;
            for (int i = 0; i < len; ++i)
            {
                if (predicate(values[i]))
                    list.Add(values[i]);
            }
            return list;
        }

        public int RemoveAll(Predicate<TValue> predicate)
        {
            IList<TKey1> list = new List<TKey1>();
            bool gotUpgradeableLock = false;

            try
            {
                // Avoid an asynchronous Thread.Abort() from possibly never existing an acquired lock by placing
                // the acquision inside the main try.  The inner finally block is needed because thread aborts cannot
                // interrupt code in these blocks (hence gotLock is guaranteed to be set correctly).
                try {}
                finally
                {
                    rwLock.EnterUpgradeableReadLock();
                    gotUpgradeableLock = true;
                }

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

                bool gotWriteLock = false;

                try
                {
                    try {}
                    finally
                    {
                        rwLock.EnterWriteLock();
                        gotWriteLock = true;

                        for (int i = 0; i < list.Count; i++)
                            Dictionary1.Remove(list[i]);

                        for (int i = 0; i < list2.Count; i++)
                            Dictionary2.Remove(list2[i]);
                        m_array = null;
                    }
                }
                finally
                {
                    if (gotWriteLock)
                        rwLock.ExitWriteLock();
                }
            }
            finally
            {
                if (gotUpgradeableLock)
                    rwLock.ExitUpgradeableReadLock();
            }

            return list.Count;
        }

        public TValue[] GetArray()
        {
            bool gotupLock = false;
            try
            {
                try { }
                finally
                {
                    rwLock.EnterUpgradeableReadLock();
                    gotupLock = true;
                }

                if (m_array == null)
                {
                    bool gotwritelock = false;
                    try
                    {
                        try { }
                        finally
                        {
                            rwLock.EnterWriteLock();
                            gotwritelock = true;
                        }

                        m_array = new TValue[Dictionary1.Count];
                        Dictionary1.Values.CopyTo(m_array, 0);
                    }
                    finally
                    {
                        if (gotwritelock)
                            rwLock.ExitWriteLock();
                    }
                }
                return m_array;
            }
            catch
            {
                return new TValue[0];
            }
            finally
            {
                if (gotupLock)
                    rwLock.ExitUpgradeableReadLock();
            }
        }
    }
}