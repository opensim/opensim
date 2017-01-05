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
using System.Diagnostics;

namespace OpenSim.Framework
{
    /// <summary>
    /// Cenome memory based cache to store key/value pairs (elements) limited time and/or limited size.
    /// </summary>
    /// <typeparam name="TKey">
    /// The type of keys in the cache.
    /// </typeparam>
    /// <typeparam name="TValue">
    /// The type of values in the dictionary.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// Cenome memory cache stores elements to hash table generations. When new element is being added to cache, and new size would exceed
    /// maximal allowed size or maximal amount of allowed element count, then elements in oldest generation are deleted. Last access time
    /// is also tracked in generation level - thus it is possible that some elements are staying in cache far beyond their expiration time.
    /// If elements in older generations are accessed through <see cref="TryGetValue"/> method, they are moved to newest generation.
    /// </para>
    /// </remarks>
    public class CnmMemoryCache<TKey, TValue> : ICnmCache<TKey, TValue>
    {
        /// <summary>
        /// Default maximal count.
        /// </summary>
        /// <seealso cref="MaxCount"/>
        public const int DefaultMaxCount = 4096;

        /// <summary>
        /// Default maximal size.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 128MB = 128 * 1024^2 = 134 217 728 bytes.
        /// </para>
        /// </remarks>
        /// <seealso cref="MaxSize"/>
        public const long DefaultMaxSize = 134217728;

        /// <summary>
        /// How many operations between time checks.
        /// </summary>
        private const int DefaultOperationsBetweenTimeChecks = 40;

        /// <summary>
        /// Default expiration time.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 30 minutes.
        /// </para>
        /// </remarks>
        public static readonly TimeSpan DefaultExpirationTime = TimeSpan.FromMinutes(30.0);

        /// <summary>
        /// Minimal allowed expiration time.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 5 minutes.
        /// </para>
        /// </remarks>
        public static readonly TimeSpan MinExpirationTime = TimeSpan.FromSeconds(10.0);

        /// <summary>
        /// Comparer used to compare element keys.
        /// </summary>
        /// <remarks>
        /// Comparer is initialized by constructor.
        /// </remarks>
        /// <seealso cref="CnmMemoryCache{TKey,TValue}"/>
        public readonly IEqualityComparer<TKey> Comparer;

        /// <summary>
        /// Expiration time.
        /// </summary>
        private TimeSpan m_expirationTime = DefaultExpirationTime;

        /// <summary>
        /// Generation bucket count.
        /// </summary>
        private int m_generationBucketCount;

        /// <summary>
        /// Generation entry count.
        /// </summary>
        private int m_generationElementCount;

        /// <summary>
        /// Generation max size.
        /// </summary>
        private long m_generationMaxSize;

        /// <summary>
        /// Maximal allowed count of elements.
        /// </summary>
        private int m_maxCount;

        /// <summary>
        /// Maximal allowed total size of elements.
        /// </summary>
        private long m_maxElementSize;

        /// <summary>
        /// Maximal size.
        /// </summary>
        private long m_maxSize;

        /// <summary>
        /// New generation.
        /// </summary>
        private IGeneration m_newGeneration;

        /// <summary>
        /// Old generation.
        /// </summary>
        private IGeneration m_oldGeneration;

        /// <summary>
        /// Operations between time check.
        /// </summary>
        private int m_operationsBetweenTimeChecks = DefaultOperationsBetweenTimeChecks;

        /// <summary>
        /// Synchronization root object, should always be private and exists always
        /// </summary>
        private readonly object m_syncRoot = new object();

        /// <summary>
        /// Version of cache.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Updated every time when cache has been changed (element removed, expired, added, replaced).
        /// </para>
        /// </remarks>
        private int m_version;

        /// <summary>
        /// Initializes a new instance of the <see cref="CnmMemoryCache{TKey,TValue}"/> class.
        /// </summary>
        public CnmMemoryCache()
            : this(DefaultMaxSize)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CnmMemoryCache{TKey,TValue}"/> class.
        /// </summary>
        /// <param name="maximalSize">
        /// Maximal cache size.
        /// </param>
        public CnmMemoryCache(long maximalSize)
            : this(maximalSize, DefaultMaxCount)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CnmMemoryCache{TKey,TValue}"/> class.
        /// </summary>
        /// <param name="maximalSize">
        /// Maximal cache size.
        /// </param>
        /// <param name="maximalCount">
        /// Maximal element count.
        /// </param>
        public CnmMemoryCache(long maximalSize, int maximalCount)
            : this(maximalSize, maximalCount, DefaultExpirationTime)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CnmMemoryCache{TKey,TValue}"/> class.
        /// </summary>
        /// <param name="maximalSize">
        /// Maximal cache size.
        /// </param>
        /// <param name="maximalCount">
        /// Maximal element count.
        /// </param>
        /// <param name="expirationTime">
        /// Elements expiration time.
        /// </param>
        public CnmMemoryCache(long maximalSize, int maximalCount, TimeSpan expirationTime)
            : this(maximalSize, maximalCount, expirationTime, EqualityComparer<TKey>.Default)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CnmMemoryCache{TKey,TValue}"/> class.
        /// </summary>
        /// <param name="maximalSize">
        /// Maximal cache size.
        /// </param>
        /// <param name="maximalCount">
        /// Maximal element count.
        /// </param>
        /// <param name="expirationTime">
        /// Elements expiration time.
        /// </param>
        /// <param name="comparer">
        /// Comparer used for comparing elements.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <see cref="comparer"/>is <see langword="null"/> reference.
        /// </exception>
        public CnmMemoryCache(long maximalSize,
            int maximalCount,
            TimeSpan expirationTime,
            IEqualityComparer<TKey> comparer)
        {
            if (comparer == null)
                throw new ArgumentNullException("comparer");

            if (expirationTime < MinExpirationTime)
                expirationTime = MinExpirationTime;
            if (maximalCount < 8)
                maximalCount = 8;
            if (maximalSize < 8)
                maximalSize = 8;
            if (maximalCount > maximalSize)
                maximalCount = (int) maximalSize;

            Comparer = comparer;
            m_expirationTime = expirationTime;
            m_maxSize = maximalSize;
            m_maxCount = maximalCount;

            Initialize();
        }

        /// <summary>
        /// Add element to new generation.
        /// </summary>
        /// <param name="bucketIndex">
        /// The bucket index.
        /// </param>
        /// <param name="key">
        /// The element's key.
        /// </param>
        /// <param name="value">
        /// The element's value.
        /// </param>
        /// <param name="size">
        /// The element's size.
        /// </param>
        protected virtual void AddToNewGeneration(int bucketIndex, TKey key, TValue value, long size)
        {
            // Add to newest generation
            if (!m_newGeneration.Set(bucketIndex, key, value, size))
            {
                // Failed to add new generation
                RecycleGenerations();
                m_newGeneration.Set(bucketIndex, key, value, size);
            }

            m_version++;
        }

        /// <summary>
        /// <para>
        /// Get keys bucket index.
        /// </para>
        /// </summary>
        /// <param name="key">
        /// <para>
        /// Key which bucket index is being retrieved.
        /// </para>
        /// </param>
        /// <returns>
        /// <para>
        /// Bucket index.
        /// </para>
        /// </returns>
        /// <remarks>
        /// <para>
        /// Method uses <see cref="Comparer"/> to calculate <see cref="key"/> hash code.
        /// </para>
        /// <para>
        /// Bucket index is remainder when element key's hash value is divided by bucket count.
        /// </para>
        /// <para>
        /// For example: key's hash is 72, bucket count is 5, element's bucket index is 72 % 5 = 2.
        /// </para>
        /// </remarks>
        protected virtual int GetBucketIndex(TKey key)
        {
            return (Comparer.GetHashCode(key) & 0x7FFFFFFF) % m_generationBucketCount;
        }

        /// <summary>
        /// Purge generation from the cache.
        /// </summary>
        /// <param name="generation">
        /// The generation that is purged.
        /// </param>
        protected virtual void PurgeGeneration(IGeneration generation)
        {
            generation.Clear();
            m_version++;
        }

        /// <summary>
        /// check expired.
        /// </summary>
        private void CheckExpired()
        {
            // Do this only one in every m_operationsBetweenTimeChecks
            // Fetching time is using several millisecons - it is better not to do all time.
            m_operationsBetweenTimeChecks--;
            if (m_operationsBetweenTimeChecks <= 0)
                PurgeExpired();
        }

        /// <summary>
        /// Initialize cache.
        /// </summary>
        private void Initialize()
        {
            m_version++;

            m_generationMaxSize = MaxSize / 2;
            MaxElementSize = MaxSize / 8;
            m_generationElementCount = MaxCount / 2;

            // Buckets need to be prime number to get better spread of hash values
            m_generationBucketCount = PrimeNumberHelper.GetPrime(m_generationElementCount);

            m_newGeneration = new HashGeneration(this);
            m_oldGeneration = new HashGeneration(this);
            m_oldGeneration.MakeOld();
        }

        /// <summary>
        /// Recycle generations.
        /// </summary>
        private void RecycleGenerations()
        {
            // Rotate old generation to new generation, new generation to old generation
            IGeneration temp = m_newGeneration;
            m_newGeneration = m_oldGeneration;
            m_newGeneration.Clear();
            m_oldGeneration = temp;
            m_oldGeneration.MakeOld();
        }

        #region Nested type: Enumerator

        /// <summary>
        /// Key and value pair enumerator.
        /// </summary>
        private class Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
        {
            /// <summary>
            /// Current enumerator.
            /// </summary>
            private int m_currentEnumerator = -1;

            /// <summary>
            /// Enumerators to different generations.
            /// </summary>
            private readonly IEnumerator<KeyValuePair<TKey, TValue>>[] m_generationEnumerators =
                new IEnumerator<KeyValuePair<TKey, TValue>>[2];

            /// <summary>
            /// Initializes a new instance of the <see cref="Enumerator"/> class.
            /// </summary>
            /// <param name="cache">
            /// The cache.
            /// </param>
            public Enumerator(CnmMemoryCache<TKey, TValue> cache)
            {
                m_generationEnumerators[ 0 ] = cache.m_newGeneration.GetEnumerator();
                m_generationEnumerators[ 1 ] = cache.m_oldGeneration.GetEnumerator();
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
                get
                {
                    if (m_currentEnumerator == -1 || m_currentEnumerator >= m_generationEnumerators.Length)
                        throw new InvalidOperationException();

                    return m_generationEnumerators[ m_currentEnumerator ].Current;
                }
            }

            /// <summary>
            /// Gets the current element in the collection.
            /// </summary>
            /// <returns>
            /// The current element in the collection.
            /// </returns>
            /// <exception cref="T:System.InvalidOperationException">
            /// The enumerator is positioned before the first element of the collection or after the last element.
            /// </exception><filterpriority>2</filterpriority>
            object IEnumerator.Current
            {
                get { return Current; }
            }

            /// <summary>
            /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
            /// </summary>
            /// <filterpriority>2</filterpriority>
            public void Dispose()
            {
            }

            /// <summary>
            /// Advances the enumerator to the next element of the collection.
            /// </summary>
            /// <returns>
            /// <see langword="true"/>if the enumerator was successfully advanced to the next element; <see langword="false"/> if the enumerator has passed the end of the collection.
            /// </returns>
            /// <exception cref="T:System.InvalidOperationException">
            /// The collection was modified after the enumerator was created.
            /// </exception>
            /// <filterpriority>2</filterpriority>
            public bool MoveNext()
            {
                if (m_currentEnumerator == -1)
                    m_currentEnumerator = 0;

                while (m_currentEnumerator < m_generationEnumerators.Length)
                {
                    if (m_generationEnumerators[ m_currentEnumerator ].MoveNext())
                        return true;

                    m_currentEnumerator++;
                }

                return false;
            }

            /// <summary>
            /// Sets the enumerator to its initial position, which is before the first element in the collection.
            /// </summary>
            /// <exception cref="T:System.InvalidOperationException">
            /// The collection was modified after the enumerator was created.
            /// </exception>
            /// <filterpriority>2</filterpriority>
            public void Reset()
            {
                foreach (IEnumerator<KeyValuePair<TKey, TValue>> enumerator in m_generationEnumerators)
                {
                    enumerator.Reset();
                }

                m_currentEnumerator = -1;
            }

            #endregion
        }

        #endregion

        #region Nested type: HashGeneration

        /// <summary>
        /// Hash generation class
        /// </summary>
        /// <remarks>
        /// <para>
        /// Current implementation is based to separated chaining with move-to-front heuristics. Hash generations have fixed
        /// amount of buckets and it is never rehashed.
        /// </para>
        /// <para>
        /// Read more about hash tables from <a href="http://en.wikipedia.org/wiki/Hash_table">Wiki article</a>.
        /// </para>
        /// </remarks>
        /// <seealso href="http://en.wikipedia.org/wiki/Hash_table"/>
        private class HashGeneration : IGeneration
        {
            /// <summary>
            /// Value indicating whether generation was accessed since last time check.
            /// </summary>
            private bool m_accessedSinceLastTimeCheck;

            /// <summary>
            /// Index of first element's in element chain.
            /// </summary>
            /// <value>
            /// -1 if there is no element in bucket; otherwise first element's index in the element chain.
            /// </value>
            /// <remarks>
            /// Bucket index is remainder when element key's hash value is divided by bucket count.
            /// For example: key's hash is 72, bucket count is 5, element's bucket index is 72 % 5 = 2.
            /// </remarks>
            private readonly int[] m_buckets;

            /// <summary>
            /// Cache object.
            /// </summary>
            private readonly CnmMemoryCache<TKey, TValue> m_cache;

            /// <summary>
            /// Generation's element array.
            /// </summary>
            /// <seealso cref="Element"/>
            private readonly Element[] m_elements;

            /// <summary>
            /// Generation's expiration time.
            /// </summary>
            private DateTime m_expirationTime1;

            /// <summary>
            /// Index to first free element.
            /// </summary>
            private int m_firstFreeElement;

            /// <summary>
            /// Free element count.
            /// </summary>
            /// <remarks>
            /// When generation is cleared or constructed, this is NOT set to element count.
            /// This is only tracking elements that are removed and are currently free.
            /// </remarks>
            private int m_freeCount;

            /// <summary>
            /// Is this generation "new generation".
            /// </summary>
            private bool m_newGeneration;

            /// <summary>
            /// Next unused entry.
            /// </summary>
            private int m_nextUnusedElement;

            /// <summary>
            /// Size of data stored to generation.
            /// </summary>
            private long m_size;

            /// <summary>
            /// Initializes a new instance of the <see cref="HashGeneration"/> class.
            /// </summary>
            /// <param name="cache">
            /// The cache.
            /// </param>
            public HashGeneration(CnmMemoryCache<TKey, TValue> cache)
            {
                m_cache = cache;
                m_elements = new Element[m_cache.m_generationElementCount];
                m_buckets = new int[m_cache.m_generationBucketCount];
                Clear();
            }

            /// <summary>
            /// Find element's index
            /// </summary>
            /// <param name="bucketIndex">
            /// The element's bucket index.
            /// </param>
            /// <param name="key">
            /// The element's key.
            /// </param>
            /// <param name="moveToFront">
            /// Move element to front of elements.
            /// </param>
            /// <param name="previousIndex">
            /// The previous element's index.
            /// </param>
            /// <returns>
            /// Element's index, if found from the generation; -1 otherwise (if element is not found the generation).
            /// </returns>
            private int FindElementIndex(int bucketIndex, TKey key, bool moveToFront, out int previousIndex)
            {
                previousIndex = -1;
                int elementIndex = m_buckets[ bucketIndex ];
                while (elementIndex >= 0)
                {
                    if (m_cache.Comparer.Equals(key, m_elements[ elementIndex ].Key))
                    {
                        // Found match
                        if (moveToFront && previousIndex >= 0)
                        {
                            // Move entry to front
                            m_elements[ previousIndex ].Next = m_elements[ elementIndex ].Next;
                            m_elements[ elementIndex ].Next = m_buckets[ bucketIndex ];
                            m_buckets[ bucketIndex ] = elementIndex;
                            previousIndex = 0;
                        }

                        return elementIndex;
                    }

                    previousIndex = elementIndex;
                    elementIndex = m_elements[ elementIndex ].Next;
                }

                return -1;
            }

            /// <summary>
            /// Remove element front the generation.
            /// </summary>
            /// <param name="bucketIndex">
            /// The bucket index.
            /// </param>
            /// <param name="entryIndex">
            /// The element index.
            /// </param>
            /// <param name="previousIndex">
            /// The element's previous index.
            /// </param>
            private void RemoveElement(int bucketIndex, int entryIndex, int previousIndex)
            {
                if (previousIndex >= 0)
                    m_elements[ previousIndex ].Next = m_elements[ entryIndex ].Next;
                else
                    m_buckets[ bucketIndex ] = m_elements[ entryIndex ].Next;

                Size -= m_elements[ entryIndex ].Size;
                m_elements[ entryIndex ].Value = default(TValue);
                m_elements[ entryIndex ].Key = default(TKey);

                // Add element to free elements list
                m_elements[ entryIndex ].Next = m_firstFreeElement;
                m_firstFreeElement = entryIndex;
                m_freeCount++;
            }

            #region Nested type: Element

            /// <summary>
            /// Element that stores key, next element in chain, size and value.
            /// </summary>
            private struct Element
            {
                /// <summary>
                /// Element's key.
                /// </summary>
                public TKey Key;

                /// <summary>
                /// Next element in chain.
                /// </summary>
                /// <remarks>
                /// When element have value (something is stored to it), this is index of
                /// next element with same bucket index. When element is free, this
                /// is index of next element in free element's list.
                /// </remarks>
                public int Next;

                /// <summary>
                /// Size of element.
                /// </summary>
                /// <value>
                /// 0 if element is free; otherwise larger than 0.
                /// </value>
                public long Size;

                /// <summary>
                /// Element's value.
                /// </summary>
                /// <remarks>
                /// It is possible that this value is <see langword="null"/> even when element
                /// have value - element's value is then <see langword="null"/> reference.
                /// </remarks>
                public TValue Value;

                /// <summary>
                /// Gets a value indicating whether element is free or have value.
                /// </summary>
                /// <value>
                /// <see langword="true"/> when element is free; otherwise <see langword="false"/>.
                /// </value>
                public bool IsFree
                {
                    get { return Size == 0; }
                }
            }

            #endregion

            #region Nested type: Enumerator

            /// <summary>
            /// Key value pair enumerator for <see cref="HashGeneration"/> object.
            /// </summary>
            private class Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
            {
                /// <summary>
                /// Current element.
                /// </summary>
                private KeyValuePair<TKey, TValue> m_current;

                /// <summary>
                /// Current index.
                /// </summary>
                private int m_currentIndex;

                /// <summary>
                /// Generation that is being enumerated.
                /// </summary>
                private readonly HashGeneration m_generation;

                /// <summary>
                /// Cache version.
                /// </summary>
                /// <remarks>
                /// When cache is change, version number is changed.
                /// </remarks>
                /// <seealso cref="CnmMemoryCache{TKey,TValue}.m_version"/>
                private readonly int m_version;

                /// <summary>
                /// Initializes a new instance of the <see cref="Enumerator"/> class.
                /// </summary>
                /// <param name="generation">
                /// The generation.
                /// </param>
                public Enumerator(HashGeneration generation)
                {
                    m_generation = generation;
                    m_version = m_generation.m_cache.m_version;
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
                    get
                    {
                        if (m_currentIndex == 0 || m_currentIndex >= m_generation.Count)
                            throw new InvalidOperationException();

                        return m_current;
                    }
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
                /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
                /// </summary>
                /// <filterpriority>2</filterpriority>
                public void Dispose()
                {
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
                    if (m_version != m_generation.m_cache.m_version)
                        throw new InvalidOperationException();

                    while (m_currentIndex < m_generation.Count)
                    {
                        if (m_generation.m_elements[ m_currentIndex ].IsFree)
                        {
                            m_currentIndex++;
                            continue;
                        }

                        m_current = new KeyValuePair<TKey, TValue>(m_generation.m_elements[ m_currentIndex ].Key,
                            m_generation.m_elements[ m_currentIndex ].Value);
                        m_currentIndex++;
                        return true;
                    }

                    m_current = new KeyValuePair<TKey, TValue>();
                    return false;
                }

                /// <summary>
                /// Sets the enumerator to its initial position, which is before the first element in the collection.
                /// </summary>
                /// <exception cref="InvalidOperationException">
                /// The collection was modified after the enumerator was created.
                /// </exception>
                /// <filterpriority>2</filterpriority>
                public void Reset()
                {
                    if (m_version != m_generation.m_cache.m_version)
                        throw new InvalidOperationException();

                    m_currentIndex = 0;
                }

                #endregion
            }

            #endregion

            #region IGeneration Members

            /// <summary>
            /// Gets or sets a value indicating whether generation was accessed since last time check.
            /// </summary>
            public bool AccessedSinceLastTimeCheck
            {
                get { return m_accessedSinceLastTimeCheck; }

                set { m_accessedSinceLastTimeCheck = value; }
            }

            /// <summary>
            /// Gets element count in generation.
            /// </summary>
            public int Count
            {
                get { return m_nextUnusedElement - m_freeCount; }
            }

            /// <summary>
            /// Gets or sets generation's expiration time.
            /// </summary>
            public DateTime ExpirationTime
            {
                get { return m_expirationTime1; }

                set { m_expirationTime1 = value; }
            }

            /// <summary>
            /// Gets or sets size of data stored to generation.
            /// </summary>
            public long Size
            {
                get { return m_size; }

                private set { m_size = value; }
            }

            /// <summary>
            /// Clear all elements from the generation and make generation new again.
            /// </summary>
            /// <remarks>
            /// When generation is new, it is allowed to add new elements to it and
            /// <see cref="IGeneration.TryGetValue"/>doesn't remove elements from it.
            /// </remarks>
            /// <seealso cref="IGeneration.MakeOld"/>
            public void Clear()
            {
                for (int i = m_buckets.Length - 1 ; i >= 0 ; i--)
                {
                    m_buckets[ i ] = -1;
                }

                Array.Clear(m_elements, 0, m_elements.Length);
                Size = 0;
                m_firstFreeElement = -1;
                m_freeCount = 0;
                m_nextUnusedElement = 0;
                m_newGeneration = true;
                ExpirationTime = DateTime.MaxValue;
            }

            /// <summary>
            /// Determines whether the <see cref="IGeneration"/> contains an element with the specific key.
            /// </summary>
            /// <param name="bucketIndex">
            /// The bucket index for the <see cref="key"/> to locate in <see cref="IGeneration"/>.
            /// </param>
            /// <param name="key">
            /// The key to locate in the <see cref="IGeneration"/>.
            /// </param>
            /// <returns>
            /// <see langword="true"/>if the <see cref="IGeneration"/> contains an element with the <see cref="key"/>;
            /// otherwise <see langword="false"/>.
            /// </returns>
            public bool Contains(int bucketIndex, TKey key)
            {
                int previousIndex;
                if (FindElementIndex(bucketIndex, key, true, out previousIndex) == -1)
                    return false;

                AccessedSinceLastTimeCheck = true;
                return true;
            }

            /// <summary>
            /// Returns an enumerator that iterates through the elements stored <see cref="HashGeneration"/>.
            /// </summary>
            /// <returns>
            /// A <see cref="IEnumerator"/> that can be used to iterate through the <see cref="HashGeneration"/>.
            /// </returns>
            /// <filterpriority>1</filterpriority>
            public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            {
                return new Enumerator(this);
            }

            /// <summary>
            /// Make from generation old generation.
            /// </summary>
            /// <remarks>
            /// When generation is old, <see cref="IGeneration.TryGetValue"/> hit removes element from the generation.
            /// </remarks>
            /// <seealso cref="IGeneration.Clear"/>
            public void MakeOld()
            {
                m_newGeneration = false;
            }

            /// <summary>
            /// Remove element associated with the key from the generation.
            /// </summary>
            /// <param name="bucketIndex">
            /// The element's bucket index.
            /// </param>
            /// <param name="key">
            /// The element's key.
            /// </param>
            /// <returns>
            /// <see langword="true"/>, if remove was successful; otherwise <see langword="false"/>.
            /// </returns>
            public bool Remove(int bucketIndex, TKey key)
            {
                int previousIndex;
                int entryIndex = FindElementIndex(bucketIndex, key, false, out previousIndex);
                if (entryIndex != -1)
                {
                    RemoveElement(bucketIndex, entryIndex, previousIndex);
                    AccessedSinceLastTimeCheck = true;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Set or add element to generation.
            /// </summary>
            /// <param name="bucketIndex">
            /// The element's bucket index.
            /// </param>
            /// <param name="key">
            /// The element's key.
            /// </param>
            /// <param name="value">
            /// The element's value.
            /// </param>
            /// <param name="size">
            /// The element's size.
            /// </param>
            /// <returns>
            /// <see langword="true"/>, if setting or adding was successful; otherwise <see langword="false"/>.
            /// </returns>
            /// <remarks>
            /// <para>
            /// If element was already existing in generation and new element size fits to collection limits,
            /// then it's value is replaced with new one and size information is updated. If element didn't
            /// exists in generation before, then generation must have empty space for a new element and
            /// size must fit generation's limits, before element is added to generation.
            /// </para>
            /// </remarks>
            public bool Set(int bucketIndex, TKey key, TValue value, long size)
            {
                Debug.Assert(m_newGeneration, "It is possible to insert new elements only to newest generation.");
                Debug.Assert(size > 0, "New element size should be more than 0.");

                int previousIndex;
                int elementIndex = FindElementIndex(bucketIndex, key, true, out previousIndex);
                if (elementIndex == -1)
                {
                    // New key
                    if (Size + size > m_cache.m_generationMaxSize ||
                        (m_nextUnusedElement == m_cache.m_generationElementCount && m_freeCount == 0))
                    {
                        // Generation is full
                        return false;
                    }

                    // Increase size of generation
                    Size += size;

                    // Get first free entry and update free entry list
                    if (m_firstFreeElement != -1)
                    {
                        // There was entry that was removed
                        elementIndex = m_firstFreeElement;
                        m_firstFreeElement = m_elements[ elementIndex ].Next;
                        m_freeCount--;
                    }
                    else
                    {
                        // No entries removed so far - just take a last one
                        elementIndex = m_nextUnusedElement;
                        m_nextUnusedElement++;
                    }

                    Debug.Assert(m_elements[ elementIndex ].IsFree, "Allocated element is not free.");

                    // Move new entry to front
                    m_elements[ elementIndex ].Next = m_buckets[ bucketIndex ];
                    m_buckets[ bucketIndex ] = elementIndex;

                    // Set key and update count
                    m_elements[ elementIndex ].Key = key;
                }
                else
                {
                    // Existing key
                    if (Size - m_elements[ elementIndex ].Size + size > m_cache.m_generationMaxSize)
                    {
                        // Generation is full
                        // Remove existing element, because generation is going to be recycled to
                        // old generation and element is stored to new generation
                        RemoveElement(bucketIndex, elementIndex, previousIndex);
                        return false;
                    }

                    // Update generation's size
                    Size = Size - m_elements[ elementIndex ].Size + size;
                }

                // Finally set value and size
                m_elements[ elementIndex ].Value = value;
                m_elements[ elementIndex ].Size = size;

                // Success - key was inserterted to generation
                AccessedSinceLastTimeCheck = true;
                return true;
            }

            /// <summary>
            /// Try to get element associated with key.
            /// </summary>
            /// <param name="bucketIndex">
            /// The element's bucket index.
            /// </param>
            /// <param name="key">
            /// The element's key.
            /// </param>
            /// <param name="value">
            /// The element's value.
            /// </param>
            /// <param name="size">
            /// The element's size.
            /// </param>
            /// <returns>
            /// <see langword="true"/>, if element was successful retrieved; otherwise <see langword="false"/>.
            /// </returns>
            /// <remarks>
            /// <para>
            /// If element is not found from generation then <paramref name="value"/> and <paramref name="size"/>
            /// are set to default value (default(TValue) and 0).
            /// </para>
            /// </remarks>
            public bool TryGetValue(int bucketIndex, TKey key, out TValue value, out long size)
            {
                // Find entry index,
                int previousIndex;
                int elementIndex = FindElementIndex(bucketIndex, key, m_newGeneration, out previousIndex);
                if (elementIndex == -1)
                {
                    value = default(TValue);
                    size = 0;
                    return false;
                }

                value = m_elements[ elementIndex ].Value;
                size = m_elements[ elementIndex ].Size;

                if (!m_newGeneration)
                {
                    // Old generation - remove element, because it is moved to new generation
                    RemoveElement(bucketIndex, elementIndex, previousIndex);
                }

                AccessedSinceLastTimeCheck = true;
                return true;
            }

            /// <summary>
            /// Returns an enumerator that iterates through a collection.
            /// </summary>
            /// <returns>
            /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
            /// </returns>
            /// <filterpriority>2</filterpriority>
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            #endregion
        }

        #endregion

        #region Nested type: IGeneration

        /// <summary>
        /// Cache element generation interface
        /// </summary>
        /// <remarks>
        /// <para>
        /// Generation can hold limited count of elements and limited size of data.
        /// </para>
        /// <para>
        /// There are two kind generations: "new generation" and "old generation(s)". All new elements
        /// are added to "new generation".
        /// </para>
        /// </remarks>
        protected interface IGeneration : IEnumerable<KeyValuePair<TKey, TValue>>
        {
            /// <summary>
            /// Gets or sets a value indicating whether generation was accessed since last time check.
            /// </summary>
            bool AccessedSinceLastTimeCheck { get; set; }

            /// <summary>
            /// Gets element count in generation.
            /// </summary>
            int Count { get; }

            /// <summary>
            /// Gets or sets generation's expiration time.
            /// </summary>
            DateTime ExpirationTime { get; set; }

            /// <summary>
            /// Gets size of data stored to generation.
            /// </summary>
            long Size { get; }

            /// <summary>
            /// Clear all elements from the generation and make generation new again.
            /// </summary>
            /// <remarks>
            /// When generation is new, it is allowed to add new elements to it and
            /// <see cref="TryGetValue"/>doesn't remove elements from it.
            /// </remarks>
            /// <seealso cref="MakeOld"/>
            void Clear();

            /// <summary>
            /// Determines whether the <see cref="IGeneration"/> contains an element with the specific key.
            /// </summary>
            /// <param name="bucketIndex">
            /// The bucket index for the <see cref="key"/> to locate in <see cref="IGeneration"/>.
            /// </param>
            /// <param name="key">
            /// The key to locate in the <see cref="IGeneration"/>.
            /// </param>
            /// <returns>
            /// <see langword="true"/>if the <see cref="IGeneration"/> contains an element with the <see cref="key"/>;
            /// otherwise <see langword="false"/>.
            /// </returns>
            bool Contains(int bucketIndex, TKey key);

            /// <summary>
            /// Make from generation old generation.
            /// </summary>
            /// <remarks>
            /// When generation is old, <see cref="TryGetValue"/> hit removes element from the generation.
            /// </remarks>
            /// <seealso cref="Clear"/>
            void MakeOld();

            /// <summary>
            /// Remove element associated with the key from the generation.
            /// </summary>
            /// <param name="bucketIndex">
            /// The element's bucket index.
            /// </param>
            /// <param name="key">
            /// The element's key.
            /// </param>
            /// <returns>
            /// <see langword="true"/>, if remove was successful; otherwise <see langword="false"/>.
            /// </returns>
            bool Remove(int bucketIndex, TKey key);

            /// <summary>
            /// Set or add element to generation.
            /// </summary>
            /// <param name="bucketIndex">
            /// The element's bucket index.
            /// </param>
            /// <param name="key">
            /// The element's key.
            /// </param>
            /// <param name="value">
            /// The element's value.
            /// </param>
            /// <param name="size">
            /// The element's size.
            /// </param>
            /// <returns>
            /// <see langword="true"/>, if setting or adding was successful; otherwise <see langword="false"/>.
            /// </returns>
            /// <remarks>
            /// <para>
            /// If element was already existing in generation and new element size fits to collection limits,
            /// then it's value is replaced with new one and size information is updated. If element didn't
            /// exists in generation before, then generation must have empty space for a new element and
            /// size must fit generation's limits, before element is added to generation.
            /// </para>
            /// </remarks>
            bool Set(int bucketIndex, TKey key, TValue value, long size);

            /// <summary>
            /// Try to get element associated with key.
            /// </summary>
            /// <param name="bucketIndex">
            /// The element's bucket index.
            /// </param>
            /// <param name="key">
            /// The element's key.
            /// </param>
            /// <param name="value">
            /// The element's value.
            /// </param>
            /// <param name="size">
            /// The element's size.
            /// </param>
            /// <returns>
            /// <see langword="true"/>, if element was successful retrieved; otherwise <see langword="false"/>.
            /// </returns>
            /// <remarks>
            /// <para>
            /// If element is not found from generation then <paramref name="value"/> and <paramref name="size"/>
            /// are set to default value (default(TValue) and 0).
            /// </para>
            /// </remarks>
            bool TryGetValue(int bucketIndex, TKey key, out TValue value, out long size);
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
            get { return m_newGeneration.Count + m_oldGeneration.Count; }
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
            get { return m_expirationTime; }

            set
            {
                if (value < MinExpirationTime)
                    value = MinExpirationTime;

                if (m_expirationTime == value)
                    return;

                m_newGeneration.ExpirationTime = (m_newGeneration.ExpirationTime - m_expirationTime) + value;
                m_oldGeneration.ExpirationTime = (m_oldGeneration.ExpirationTime - m_expirationTime) + value;
                m_expirationTime = value;

                PurgeExpired();
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
            get { return true; }
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
            get { return true; }
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
            get { return false; }
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
            get { return ExpirationTime != TimeSpan.MaxValue; }
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
            get { return m_maxCount; }

            set
            {
                if (value < 8)
                    value = 8;
                if (m_maxCount == value)
                    return;

                m_maxCount = value;
                Initialize();
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
            get { return m_maxElementSize; }

            private set { m_maxElementSize = value; }
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
        /// <seealso cref="ICnmCache{TKey,TValue}.MaxElementSize"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.IsSizeLimited"/>
        /// <seealso cref="ICnmCache{TKey,TValue}.Size"/>
        public long MaxSize
        {
            get { return m_maxSize; }

            set
            {
                if (value < 8)
                    value = 8;
                if (m_maxSize == value)
                    return;

                m_maxSize = value;
                Initialize();
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
            get { return m_newGeneration.Size + m_oldGeneration.Size; }
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
            m_newGeneration.Clear();
            m_oldGeneration.Clear();
            m_oldGeneration.MakeOld();
            m_version++;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the elements stored to <see cref="CnmMemoryCache{TKey,TValue}"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="IEnumerator{T}"/> that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return new Enumerator(this);
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
            m_operationsBetweenTimeChecks = DefaultOperationsBetweenTimeChecks;

            if (!IsTimeLimited)
                return;

            DateTime now = DateTime.Now;
            if (m_newGeneration.AccessedSinceLastTimeCheck)
            {
                // New generation has been accessed since last check
                // Update it's expiration time.
                m_newGeneration.ExpirationTime = now + ExpirationTime;
                m_newGeneration.AccessedSinceLastTimeCheck = false;
            }
            else if (m_newGeneration.ExpirationTime < now)
            {
                // New generation has been expired.
                // --> also old generation must be expired.
                PurgeGeneration(m_newGeneration);
                PurgeGeneration(m_oldGeneration);
                return;
            }

            if (m_oldGeneration.ExpirationTime < now)
                PurgeGeneration(m_oldGeneration);
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
            if (key == null)
                throw new ArgumentNullException("key");

            int bucketIndex = GetBucketIndex(key);
            if (!m_newGeneration.Remove(bucketIndex, key))
            {
                if (!m_oldGeneration.Remove(bucketIndex, key))
                {
                    CheckExpired();
                    return;
                }
            }

            CheckExpired();
            m_version++;
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
            if (keys == null)
                throw new ArgumentNullException("keys");

            foreach (TKey key in keys)
            {
                if (key == null)
                    continue;

                int bucketIndex = GetBucketIndex(key);
                if (!m_newGeneration.Remove(bucketIndex, key))
                    m_oldGeneration.Remove(bucketIndex, key);
            }

            CheckExpired();
            m_version++;
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
            if (key == null)
                throw new ArgumentNullException("key");

            if (size < 0)
                throw new ArgumentOutOfRangeException("size", size, "Value's size can't be less than 0.");

            if (size > MaxElementSize)
            {
                // Entry size is too big to fit cache - ignore it
                Remove(key);
                return false;
            }

            if (size == 0)
                size = 1;

            int bucketIndex = GetBucketIndex(key);
            m_oldGeneration.Remove(bucketIndex, key);
            AddToNewGeneration(bucketIndex, key, value, size);
            CheckExpired();

            return true;
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
            if (key == null)
                throw new ArgumentNullException("key");

            int bucketIndex = GetBucketIndex(key);
            long size;
            if (m_newGeneration.TryGetValue(bucketIndex, key, out value, out size))
            {
                CheckExpired();
                return true;
            }

            if (m_oldGeneration.TryGetValue(bucketIndex, key, out value, out size))
            {
                // Move element to new generation
                AddToNewGeneration(bucketIndex, key, value, size);
                CheckExpired();
                return true;
            }

            CheckExpired();
            return false;
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
