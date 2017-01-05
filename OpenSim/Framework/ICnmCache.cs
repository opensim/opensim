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

namespace OpenSim.Framework
{
    /// <summary>
    /// Represent generic cache to store key/value pairs (elements) limited by time, size and count of elements.
    /// </summary>
    /// <typeparam name="TKey">
    /// The type of keys in the cache.
    /// </typeparam>
    /// <typeparam name="TValue">
    /// The type of values in the cache.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// Cache store limitations:
    /// </para>
    /// <list type="table">
    /// <listheader>
    /// <term>Limitation</term>
    /// <description>Description</description>
    /// </listheader>
    /// <item>
    /// <term>Time</term>
    /// <description>
    /// Element that is not accessed through <see cref="TryGetValue"/> or <see cref="Set"/> in last <see cref="ExpirationTime"/> are
    /// removed from the cache automatically. Depending on implementation of the cache some of elements may stay longer in cache.
    /// <see cref="IsTimeLimited"/> returns <see langword="true"/>, if cache is limited by time.
    /// </description>
    /// </item>
    /// <item>
    /// <term>Count</term>
    /// <description>
    /// When adding an new element to cache that already have <see cref="MaxCount"/> of elements, cache will remove less recently
    /// used element(s) from the cache, until element fits to cache.
    /// <see cref="IsCountLimited"/> returns <see langword="true"/>, if cache is limiting element count.
    /// </description>
    /// </item>
    /// <item>
    /// <term>Size</term>
    /// <description>
    /// <description>
    /// When adding an new element to cache that already have <see cref="MaxSize"/> of elements, cache will remove less recently
    /// used element(s) from the cache, until element fits to cache.
    /// <see cref="IsSizeLimited"/> returns <see langword="true"/>, if cache is limiting total size of elements.
    /// Normally size is bytes used by element in the cache. But it can be any other suitable unit of measure.
    /// </description>
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    public interface ICnmCache<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    {
        /// <summary>
        /// Gets current count of elements stored to <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When adding an new element to <see cref="ICnmCache{TKey,TValue}"/> that is limiting element count,
        /// <see cref="ICnmCache{TKey,TValue}"/> will remove less recently used elements until it can fit an new element.
        /// </para>
        /// </remarks>
        /// <seealso cref="MaxCount"/>
        /// <seealso cref="IsCountLimited"/>
        /// <seealso cref="IsSizeLimited"/>
        /// <seealso cref="IsTimeLimited"/>
        int Count { get; }

        /// <summary>
        /// Gets or sets elements expiration time.
        /// </summary>
        /// <value>
        /// Elements expiration time.
        /// </value>
        /// <remarks>
        /// <para>
        /// When element has been stored in <see cref="ICnmCache{TKey,TValue}"/> longer than <see cref="ExpirationTime"/>
        /// and it is not accessed through <see cref="TryGetValue"/> method or element's value is
        /// not replaced by <see cref="Set"/> method, then it is automatically removed from the
        /// <see cref="ICnmCache{TKey,TValue}"/>.
        /// </para>
        /// <para>
        /// It is possible that <see cref="ICnmCache{TKey,TValue}"/> implementation removes element before it's expiration time,
        /// because total size or count of elements stored to cache is larger than <see cref="MaxSize"/> or <see cref="MaxCount"/>.
        /// </para>
        /// <para>
        /// It is also possible that element stays in cache longer than <see cref="ExpirationTime"/>.
        /// </para>
        /// <para>
        /// Calling <see cref="PurgeExpired"/> try to remove all elements that are expired.
        /// </para>
        /// <para>
        /// To disable time limit in cache, set <see cref="ExpirationTime"/> to <see cref="DateTime.MaxValue"/>.
        /// </para>
        /// </remarks>
        /// <seealso cref="IsTimeLimited"/>
        /// <seealso cref="IsCountLimited"/>
        /// <seealso cref="IsSizeLimited"/>
        /// <seealso cref="PurgeExpired"/>
        /// <seealso cref="Count"/>
        /// <seealso cref="MaxCount"/>
        /// <seealso cref="MaxSize"/>
        /// <seealso cref="Size"/>
        TimeSpan ExpirationTime { get; set; }

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
        /// <seealso cref="SyncRoot"/>
        /// <seealso cref="CnmSynchronizedCache{TKey,TValue}"/>
        bool IsSynchronized { get; }

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
        /// <seealso cref="Count"/>
        /// <seealso cref="MaxCount"/>
        /// <seealso cref="IsSizeLimited"/>
        /// <seealso cref="IsTimeLimited"/>
        bool IsCountLimited { get; }

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
        /// <seealso cref="MaxElementSize"/>
        /// <seealso cref="Size"/>
        /// <seealso cref="MaxSize"/>
        /// <seealso cref="IsCountLimited"/>
        /// <seealso cref="IsTimeLimited"/>
        bool IsSizeLimited { get; }

        /// <summary>
        /// Gets a value indicating whether elements stored to <see cref="ICnmCache{TKey,TValue}"/> have limited inactivity time.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the <see cref="ICnmCache{TKey,TValue}"/> has a fixed total size of elements;
        /// otherwise, <see langword="false"/>.
        /// </value>
        /// <remarks>
        /// If <see cref="ICnmCache{TKey,TValue}"/> have limited inactivity time and element is not accessed through <see cref="Set"/>
        /// or <see cref="TryGetValue"/> methods in <see cref="ExpirationTime"/> , then element is automatically removed from
        /// the cache. Depending on implementation of the <see cref="ICnmCache{TKey,TValue}"/>, some of the elements may
        /// stay longer in cache.
        /// </remarks>
        /// <seealso cref="ExpirationTime"/>
        /// <seealso cref="PurgeExpired"/>
        /// <seealso cref="IsCountLimited"/>
        /// <seealso cref="IsSizeLimited"/>
        bool IsTimeLimited { get; }

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
        int MaxCount { get; set; }

        /// <summary>
        /// <para>Gets maximal allowed element size.</para>
        /// </summary>
        /// <value>
        /// Maximal allowed element size.
        /// </value>
        /// <remarks>
        /// <para>
        /// If element's size is larger than <see cref="MaxElementSize"/>, then element is
        /// not added to the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </para>
        /// </remarks>
        /// <seealso cref="Set"/>
        /// <seealso cref="IsSizeLimited"/>
        /// <seealso cref="Size"/>
        /// <seealso cref="MaxSize"/>
        long MaxElementSize { get; }

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
        /// <seealso cref="MaxElementSize"/>
        /// <seealso cref="IsSizeLimited"/>
        /// <seealso cref="Size"/>
        long MaxSize { get; set; }

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
        /// Element's size is given when element is added or replaced by <see cref="Set"/> method.
        /// </para>
        /// <para>
        /// When adding an new element to <see cref="ICnmCache{TKey,TValue}"/> that is limiting total size of elements,
        /// <see cref="ICnmCache{TKey,TValue}"/> will remove less recently used elements until it can fit an new element.
        /// </para>
        /// </remarks>
        /// <seealso cref="MaxElementSize"/>
        /// <seealso cref="IsSizeLimited"/>
        /// <seealso cref="MaxSize"/>
        /// <seealso cref="IsCountLimited"/>
        /// <seealso cref="ExpirationTime"/>
        long Size { get; }

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
        /// <seealso cref="IsSynchronized"/>
        /// <seealso cref="CnmSynchronizedCache{TKey,TValue}"/>
        object SyncRoot { get; }

        /// <summary>
        /// Removes all elements from the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <seealso cref="Set"/>
        /// <seealso cref="Remove"/>
        /// <seealso cref="RemoveRange"/>
        /// <seealso cref="TryGetValue"/>
        /// <seealso cref="PurgeExpired"/>
        void Clear();

        /// <summary>
        /// Purge expired elements from the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Element becomes expired when last access time to it has been longer time than <see cref="ExpirationTime"/>.
        /// </para>
        /// <para>
        /// Depending on <see cref="ICnmCache{TKey,TValue}"/> implementation, some of expired elements
        /// may stay longer than <see cref="ExpirationTime"/> in the cache.
        /// </para>
        /// </remarks>
        /// <seealso cref="IsTimeLimited"/>
        /// <seealso cref="ExpirationTime"/>
        /// <seealso cref="Set"/>
        /// <seealso cref="Remove"/>
        /// <seealso cref="RemoveRange"/>
        /// <seealso cref="TryGetValue"/>
        /// <seealso cref="Clear"/>
        void PurgeExpired();

        /// <summary>
        /// Removes element associated with <paramref name="key"/> from the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <param name="key">
        /// The key that is associated with element to remove from the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="key"/> is <see langword="null"/>.
        /// </exception>
        /// <seealso cref="Set"/>
        /// <seealso cref="RemoveRange"/>
        /// <seealso cref="TryGetValue"/>
        /// <seealso cref="Clear"/>
        /// <seealso cref="PurgeExpired"/>
        void Remove(TKey key);

        /// <summary>
        /// Removes elements that are associated with one of <paramref name="keys"/> from the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </summary>
        /// <param name="keys">
        /// The keys that are associated with elements to remove from the <see cref="ICnmCache{TKey,TValue}"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="keys"/> is <see langword="null"/>.
        /// </exception>
        /// <seealso cref="Set"/>
        /// <seealso cref="Remove"/>
        /// <seealso cref="TryGetValue"/>
        /// <seealso cref="Clear"/>
        /// <seealso cref="PurgeExpired"/>
        void RemoveRange(IEnumerable<TKey> keys);

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
        /// <see langword="true"/> if element has been added successfully to the <see cref="ICnmCache{TKey,TValue}"/>;
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
        /// If element's <paramref name="size"/> is larger than <see cref="MaxElementSize"/>, then element is
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
        /// <seealso cref="IsSizeLimited"/>
        /// <seealso cref="IsCountLimited"/>
        /// <seealso cref="Remove"/>
        /// <seealso cref="RemoveRange"/>
        /// <seealso cref="TryGetValue"/>
        /// <seealso cref="Clear"/>
        /// <seealso cref="PurgeExpired"/>
        bool Set(TKey key, TValue value, long size);

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
        /// <seealso cref="Set"/>
        /// <seealso cref="Remove"/>
        /// <seealso cref="RemoveRange"/>
        /// <seealso cref="Clear"/>
        /// <seealso cref="PurgeExpired"/>
        bool TryGetValue(TKey key, out TValue value);
    }
}
