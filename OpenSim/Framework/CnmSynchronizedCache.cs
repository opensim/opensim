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
using System.Threading;

namespace OpenSim.Framework
{
    /// <summary>
    /// Synchronized Cenome cache wrapper.
    /// </summary>
    /// <typeparam name="TKey">
    /// The type of keys in the cache.
    /// </typeparam>
    /// <typeparam name="TValue">
    /// The type of values in the cache.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// Enumerator will block other threads, until enumerator's <see cref="IDisposable.Dispose"/> method is called.
    /// "foreach" statement is automatically calling it.
    /// </para>
    /// </remarks>
    public class CnmSynchronizedCache<TKey, TValue> : ICnmCache<TKey, TValue>
    {
        /// <summary>
        /// The cache object.
        /// </summary>
        private readonly ICnmCache<TKey, TValue> m_cache;

        /// <summary>
        /// Synchronization root.
        /// </summary>
        private readonly object m_syncRoot;

        /// <summary>
        /// Initializes a new instance of the <see cref="CnmSynchronizedCache{TKey,TValue}"/> class.
        /// Initializes a new instance of the <see cref="CnmSynchronizedCache{TKey,TValue}"/> class.
        /// </summary>
        /// <param name="cache">
        /// The cache.
        /// </param>
        private CnmSynchronizedCache(ICnmCache<TKey, TValue> cache)
        {
            m_cache = cache;
            m_syncRoot = m_cache.SyncRoot;
        }

        /// <summary>
        /// Returns a <see cref="ICnmCache{TKey,TValue}"/> wrapper that is synchronized (thread safe).
        /// </summary>
        /// <param name="cache">
        /// The <see cref="ICnmCache{TKey,TValue}"/> to synchronize.
        /// </param>
        /// <returns>
        /// A <see cref="ICnmCache{TKey,TValue}"/> wrapper that is synchronized (thread safe).
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="cache"/>is null.
        /// </exception>
        public static ICnmCache<TKey, TValue> Synchronized(ICnmCache<TKey, TValue> cache)
        {
            if (cache == null)
                throw new ArgumentNullException("cache");
            return cache.IsSynchronized ? cache : new CnmSynchronizedCache<TKey, TValue>(cache);
        }

        #region Nested type: SynchronizedEnumerator

        /// <summary>
        /// Synchronized enumerator.
        /// </summary>
        private class SynchronizedEnumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            /// <summary>
            /// Enumerator that is being synchronized.
            /// </summary>
            private readonly IEnumerator<KeyValuePair<TKey, TValue>> m_enumerator;

            /// <summary>
            /// Synchronization root.
            /// </summary>
            private object m_syncRoot;

            /// <summary>
            /// Initializes a new instance of the <see cref="SynchronizedEnumerator"/> class.
            /// </summary>
            /// <param name="enumerator">
            /// The enumerator that is being synchronized.
            /// </param>
            /// <param name="syncRoot">
            /// The sync root.
            /// </param>
            public SynchronizedEnumerator(IEnumerator<KeyValuePair<TKey, TValue>> enumerator, object syncRoot)
            {
                m_syncRoot = syncRoot;
                m_enumerator = enumerator;
                Monitor.Enter(m_syncRoot);
            }

            /// <summary>
            /// Finalizes an instance of the <see cref="SynchronizedEnumerator"/> class.
            /// </summary>
            ~SynchronizedEnumerator()
            {
                Dispose();
            }

            #region IEnumerator<KeyValuePair<TKey,TValue>> Members

            /// <summary>
            /// Gets the element in the collection at the current position of the enumerator.
            /// </summary>
            /// <returns>
            /// The element in the collection at the current position of the enumerator.
            /// </returns>
            /// <exception cref="InvalidOperationException">
            /// The enumerator has reach end of collection or <see cref="MoveNext"/> is not called.
            /// </exception>
            public KeyValuePair<TKey, TValue> Current
            {
                get { return m_enumerator.Current; }
            }

            /// <summary>
            /// Gets the current element in the collection.
            /// </summary>
            /// <returns>
            /// The current element in the collection.
            /// </returns>
            /// <exception cref="InvalidOperationException">
            /// The enumerator is positioned before the first element of the collection or after the last element.
            /// </exception><filterpriority>2</filterpriority>
            object IEnumerator.Current
            {
                get { return Current; }
            }

            /// <summary>
            /// Releases synchronization lock.
            /// </summary>
            public void Dispose()
            {
                if (m_syncRoot != null)
                {
                    Monitor.Exit(m_syncRoot);
                    m_syncRoot = null;
                }

                m_enumerator.Dispose();
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// Advances the enumerator to the next element of the collection.
            /// </summary>
            /// <returns>
            /// true if the enumerator was successfully advanced to the next element; false if the enumerator has passed the end of the collection.
            /// </returns>
            /// <exception cref="InvalidOperationException">
            /// The collection was modified after the enumerator was created.
            /// </exception>
            public bool MoveNext()
            {
                return m_enumerator.MoveNext();
            }

            /// <summary>
            /// Sets the enumerator to its initial position, which is before the first element in the collection.
            /// </summary>
            /// <exception cref="InvalidOperationException">
            /// The collection was modified after the enumerator was created.
            /// </exception>
            public void Reset()
            {
                m_enumerator.Reset();
            }

            #endregion
        }

        #endregion

        #region ICnmCache<TKey,TValue> Members

        /// <summary>
        /// Gets current count of elements stored to <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When adding an new element to <see cref="ICnmCache{TKey,TValue}"/> that is limiting element count,
        /// <see cref="ICnmCache{TKey,TValue}"/> will remove less recently used elements until it can fit an new element.
        /// </para>
        /// </remarks>
        /// <seealso cref="ICnmCache{TKey,TValue}.MaxCount"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsCountLimited"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsSizeLimited"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsTimeLimited"/>
        public int Count
        {
            get
            {
                lock (m_syncRoot)
                {
                    return m_cache.Count;
                }
            }
        }

        /// <summary>
        /// Gets or sets elements expiration time.
        /// </summary>
        /// <value>
        /// Elements expiration time.
        /// </value>
        /// <remarks>
        /// <para>
        /// When element has been stored in <see cref="ICnmCache{TKey,TValue}"/> longer than <see cref="ICnmCache{TKey,TValue}.ExpirationTime"/>
        /// and it is not accessed through <see cref="ICnmCache{TKey,TValue}.TryGetValue"/> method or element's value is
        /// not replaced by <see cref="ICnmCache{TKey,TValue}.Set"/> method, then it is automatically removed from the
        /// <see cref="ICnmCache{TKey,TValue}"/>.
        /// </para>
        /// <para>
        /// It is possible that <see cref="ICnmCache{TKey,TValue}"/> implementation removes element before it's expiration time,
        /// because total size or count of elements stored to cache is larger than <see cref="ICnmCache{TKey,TValue}.MaxSize"/> or <see cref="ICnmCache{TKey,TValue}.MaxCount"/>.
        /// </para>
        /// <para>
        /// It is also possible that element stays in cache longer than <see cref="ICnmCache{TKey,TValue}.ExpirationTime"/>.
        /// </para>
        /// <para>
        /// Calling <see cref="ICnmCache{TKey,TValue}.PurgeExpired"/> try to remove all elements that are expired.
        /// </para>
        /// <para>
        /// To disable time limit in cache, set <see cref="ICnmCache{TKey,TValue}.ExpirationTime"/> to <see cref="DateTime.MaxValue"/>.
        /// </para>
        /// </remarks>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsTimeLimited"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsCountLimited"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsSizeLimited"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.PurgeExpired"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Count"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.MaxCount"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.MaxSize"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Size"/>
        public TimeSpan ExpirationTime
        {
            get
            {
                lock (m_syncRoot)
                {
                    return m_cache.ExpirationTime;
                }
            }

            set
            {
                lock (m_syncRoot)
                {
                    m_cache.ExpirationTime = value;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether <see cref="ICnmCache{TKey,TValue}"/> is limiting count of elements.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the <see cref="ICnmCache{TKey,TValue}"/> count of elements is limited;
        /// otherwise, <see langword="false"/>.
        /// </value>
        /// <remarks>
        /// <para>
        /// When adding an new element to <see cref="ICnmCache{TKey,TValue}"/> that is limiting element count,
        /// <see cref="ICnmCache{TKey,TValue}"/> will remove less recently used elements until it can fit an new element.
        /// </para>
        /// </remarks>
        /// <seealso cref="ICnmCache{TKey,TValue}.Count"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.MaxCount"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsSizeLimited"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsTimeLimited"/>
        public bool IsCountLimited
        {
            get
            {
                lock (m_syncRoot)
                {
                    return m_cache.IsCountLimited;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether <see cref="ICnmCache{TKey,TValue}"/> is limiting size of elements.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the <see cref="ICnmCache{TKey,TValue}"/> total size of elements is limited;
        /// otherwise, <see langword="false"/>.
        /// </value>
        /// <remarks>
        /// <para>
        /// When adding an new element to <see cref="ICnmCache{TKey,TValue}"/> that is limiting total size of elements,
        /// <see cref="ICnmCache{TKey,TValue}"/> will remove less recently used elements until it can fit an new element.
        /// </para>
        /// </remarks>
        /// <seealso cref="ICnmCache{TKey,TValue}.MaxElementSize"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Size"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.MaxSize"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsCountLimited"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsTimeLimited"/>
        public bool IsSizeLimited
        {
            get
            {
                lock (m_syncRoot)
                {
                    return m_cache.IsSizeLimited;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether or not access to the <see cref="ICnmCache{TKey,TValue}"/> is synchronized (thread safe).
        /// </summary>
        /// <value>
        /// <see langword="true"/> if access to the <see cref="ICnmCache{TKey,TValue}"/> is synchronized (thread safe);
        /// otherwise, <see langword="false"/>.
        /// </value>
        /// <remarks>
        /// <para>
        /// To get synchronized (thread safe) access to <see cref="ICnmCache{TKey,TValue}"/> object, use
        /// <see cref="CnmSynchronizedCache{TKey,TValue}.Synchronized"/> in <see cref="CnmSynchronizedCache{TKey,TValue}"/> class
        /// to retrieve synchronized wrapper for <see cref="ICnmCache{TKey,TValue}"/> object.
        /// </para>
        /// </remarks>
        /// <seealso cref="ICnmCache{TKey,TValue}.SyncRoot"/>
        /// <seealso cref="CnmSynchronizedCache{TKey,TValue}"/>
        public bool IsSynchronized
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether elements stored to <see cref="ICnmCache{TKey,TValue}"/> have limited inactivity time.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the <see cref="ICnmCache{TKey,TValue}"/> has a fixed total size of elements;
        /// otherwise, <see langword="false"/>.
        /// </value>
        /// <remarks>
        /// If <see cref="ICnmCache{TKey,TValue}"/> have limited inactivity time and element is not accessed through <see cref="ICnmCache{TKey,TValue}.Set"/>
        /// or <see cref="ICnmCache{TKey,TValue}.TryGetValue"/> methods in <see cref="ICnmCache{TKey,TValue}.ExpirationTime"/> , then element is automatically removed from
        /// the cache. Depending on implementation of the <see cref="ICnmCache{TKey,TValue}"/>, some of the elements may
        /// stay longer in cache.
        /// </remarks>
        /// <seealso cref="ICnmCache{TKey,TValue}.ExpirationTime"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.PurgeExpired"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsCountLimited"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsSizeLimited"/>
        public bool IsTimeLimited
        {
            get
            {
                lock (m_syncRoot)
                {
                    return m_cache.IsTimeLimited;
                }
            }
        }

        /// <summary>
        /// Gets or sets maximal allowed count of elements that can be stored to <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <value>
        /// <see cref="int.MaxValue"/>, if <see cref="ICnmCache{TKey,TValue}"/> is not limited by count of elements;
        /// otherwise maximal allowed count of elements.
        /// </value>
        /// <remarks>
        /// <para>
        /// When adding an new element to <see cref="ICnmCache{TKey,TValue}"/> that is limiting element count,
        /// <see cref="ICnmCache{TKey,TValue}"/> will remove less recently used elements until it can fit an new element.
        /// </para>
        /// </remarks>
        public int MaxCount
        {
            get
            {
                lock (m_syncRoot)
                {
                    return m_cache.MaxCount;
                }
            }

            set
            {
                lock (m_syncRoot)
                {
                    m_cache.MaxCount = value;
                }
            }
        }

        /// <summary>
        /// <para>Gets maximal allowed element size.</para>
        /// </summary>
        /// <value>
        /// Maximal allowed element size.
        /// </value>
        /// <remarks>
        /// <para>
        /// If element's size is larger than <see cref="ICnmCache{TKey,TValue}.MaxElementSize"/>, then element is
        /// not added to the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </para>
        /// </remarks>
        /// <seealso cref="ICnmCache{TKey,TValue}.Set"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsSizeLimited"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Size"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.MaxSize"/>
        public long MaxElementSize
        {
            get
            {
                lock (m_syncRoot)
                {
                    return m_cache.MaxElementSize;
                }
            }
        }

        /// <summary>
        /// Gets or sets maximal allowed total size for elements stored to <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <value>
        /// Maximal allowed total size for elements stored to <see cref="ICnmCache{TKey,TValue}"/>.
        /// </value>
        /// <remarks>
        /// <para>
        /// Normally size is total bytes used by elements in the cache. But it can be any other suitable unit of measure.
        /// </para>
        /// <para>
        /// When adding an new element to <see cref="ICnmCache{TKey,TValue}"/> that is limiting total size of elements,
        /// <see cref="ICnmCache{TKey,TValue}"/> will remove less recently used elements until it can fit an new element.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">value is less than 0.</exception>
        /// <seealso cref="ICnmCache{TKey,TValue}.MaxElementSize"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsSizeLimited"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Size"/>
        public long MaxSize
        {
            get
            {
                lock (m_syncRoot)
                {
                    return m_cache.MaxSize;
                }
            }

            set
            {
                lock (m_syncRoot)
                {
                    m_cache.MaxSize = value;
                }
            }
        }

        /// <summary>
        /// Gets total size of elements stored to <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <value>
        /// Total size of elements stored to <see cref="ICnmCache{TKey,TValue}"/>.
        /// </value>
        /// <remarks>
        /// <para>
        /// Normally bytes, but can be any suitable unit of measure.
        /// </para>
        /// <para>
        /// Element's size is given when element is added or replaced by <see cref="ICnmCache{TKey,TValue}.Set"/> method.
        /// </para>
        /// <para>
        /// When adding an new element to <see cref="ICnmCache{TKey,TValue}"/> that is limiting total size of elements,
        /// <see cref="ICnmCache{TKey,TValue}"/> will remove less recently used elements until it can fit an new element.
        /// </para>
        /// </remarks>
        /// <seealso cref="ICnmCache{TKey,TValue}.MaxElementSize"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsSizeLimited"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.MaxSize"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsCountLimited"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.ExpirationTime"/>
        public long Size
        {
            get
            {
                lock (m_syncRoot)
                {
                    return m_cache.Size;
                }
            }
        }

        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <value>
        /// An object that can be used to synchronize access to the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </value>
        /// <remarks>
        /// <para>
        /// To get synchronized (thread safe) access to <see cref="ICnmCache{TKey,TValue}"/>, use <see cref="CnmSynchronizedCache{TKey,TValue}"/>
        /// method <see cref="CnmSynchronizedCache{TKey,TValue}.Synchronized"/> to retrieve synchronized wrapper interface to
        /// <see cref="ICnmCache{TKey,TValue}"/>.
        /// </para>
        /// </remarks>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsSynchronized"/>
        /// <seealso cref="CnmSynchronizedCache{TKey,TValue}"/>
        public object SyncRoot
        {
            get { return m_syncRoot; }
        }

        /// <summary>
        /// Removes all elements from the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <seealso cref="ICnmCache{TKey,TValue}.Set"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Remove"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.RemoveRange"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.TryGetValue"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.PurgeExpired"/>
        public void Clear()
        {
            lock (m_syncRoot)
            {
                m_cache.Clear();
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the elements stored to <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="IEnumerator{T}"/> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            lock (m_syncRoot)
            {
                return new SynchronizedEnumerator(m_cache.GetEnumerator(), m_syncRoot);
            }
        }

        /// <summary>
        /// Purge expired elements from the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Element becomes expired when last access time to it has been longer time than <see cref="ICnmCache{TKey,TValue}.ExpirationTime"/>.
        /// </para>
        /// <para>
        /// Depending on <see cref="ICnmCache{TKey,TValue}"/> implementation, some of expired elements
        /// may stay longer than <see cref="ICnmCache{TKey,TValue}.ExpirationTime"/> in the cache.
        /// </para>
        /// </remarks>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsTimeLimited"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.ExpirationTime"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Set"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Remove"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.RemoveRange"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.TryGetValue"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Clear"/>
        public void PurgeExpired()
        {
            lock (m_syncRoot)
            {
                m_cache.PurgeExpired();
            }
        }

        /// <summary>
        /// Removes element associated with <paramref name="key"/> from the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <param name="key">
        /// The key that is associated with element to remove from the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/>is <see langword="null"/>.
        /// </exception>
        /// <seealso cref="ICnmCache{TKey,TValue}.Set"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.RemoveRange"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.TryGetValue"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Clear"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.PurgeExpired"/>
        public void Remove(TKey key)
        {
            lock (m_syncRoot)
            {
                m_cache.Remove(key);
            }
        }

        /// <summary>
        /// Removes elements that are associated with one of <paramref name="keys"/> from the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <param name="keys">
        /// The keys that are associated with elements to remove from the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="keys"/>is <see langword="null"/>.
        /// </exception>
        /// <seealso cref="ICnmCache{TKey,TValue}.Set"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Remove"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.TryGetValue"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Clear"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.PurgeExpired"/>
        public void RemoveRange(IEnumerable<TKey> keys)
        {
            lock (m_syncRoot)
            {
                m_cache.RemoveRange(keys);
            }
        }

        /// <summary>
        /// Add or replace an element with the provided <paramref name="key"/>, <paramref name="value"/> and <paramref name="size"/> to
        /// <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <param name="key">
        /// The object used as the key of the element. Can't be <see langword="null"/> reference.
        /// </param>
        /// <param name="value">
        /// The object used as the value of the element to add or replace. <see langword="null"/> is allowed.
        /// </param>
        /// <param name="size">
        /// The element's size. Normally bytes, but can be any suitable unit of measure.
        /// </param>
        /// <returns>
        /// <see langword="true"/>if element has been added successfully to the <see cref="ICnmCache{TKey,TValue}"/>;
        /// otherwise <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/>is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The element's <paramref name="size"/> is less than 0.
        /// </exception>
        /// <remarks>
        /// <para>
        /// If element's <paramref name="size"/> is larger than <see cref="ICnmCache{TKey,TValue}.MaxElementSize"/>, then element is
        /// not added to the <see cref="ICnmCache{TKey,TValue}"/>, however - possible older element is
        /// removed from the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </para>
        /// <para>
        /// When adding an new element to <see cref="ICnmCache{TKey,TValue}"/> that is limiting total size of elements,
        /// <see cref="ICnmCache{TKey,TValue}"/>will remove less recently used elements until it can fit an new element.
        /// </para>
        /// <para>
        /// When adding an new element to <see cref="ICnmCache{TKey,TValue}"/> that is limiting element count,
        /// <see cref="ICnmCache{TKey,TValue}"/>will remove less recently used elements until it can fit an new element.
        /// </para>
        /// </remarks>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsSizeLimited"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsCountLimited"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Remove"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.RemoveRange"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.TryGetValue"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Clear"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.PurgeExpired"/>
        public bool Set(TKey key, TValue value, long size)
        {
            lock (m_syncRoot)
            {
                return m_cache.Set(key, value, size);
            }
        }

        /// <summary>
        /// Gets the <paramref name="value"/> associated with the specified <paramref name="key"/>.
        /// </summary>
        /// <returns>
        /// <see langword="true"/>if the <see cref="ICnmCache{TKey,TValue}"/> contains an element with
        /// the specified key; otherwise, <see langword="false"/>.
        /// </returns>
        /// <param name="key">
        /// The key whose <paramref name="value"/> to get.
        /// </param>
        /// <param name="value">
        /// When this method returns, the value associated with the specified <paramref name="key"/>,
        /// if the <paramref name="key"/> is found; otherwise, the
        /// default value for the type of the <paramref name="value"/> parameter. This parameter is passed uninitialized.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/>is <see langword="null"/>.
        /// </exception>
        /// <seealso cref="ICnmCache{TKey,TValue}.Set"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Remove"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.RemoveRange"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Clear"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.PurgeExpired"/>
        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (m_syncRoot)
            {
                return m_cache.TryGetValue(key, out value);
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the elements stored to <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="IEnumerator"/> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
