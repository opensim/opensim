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
using System.Reflection;
using System.Net;
using BclExtras.Collections;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace OpenSim.Framework
{
    /// <summary>
    /// Maps from client AgentID and RemoteEndPoint values to IClientAPI
    /// references for all of the connected clients
    /// </summary>
    public class ClientManager
    {
        #region IComparers

        private sealed class UUIDComparer : IComparer<UUID>
        {
            public int Compare(UUID x, UUID y)
            {
                return x.CompareTo(y);
            }
        }

        private sealed class IPEndPointComparer : IComparer<IPEndPoint>
        {
            public int Compare(IPEndPoint x, IPEndPoint y)
            {
                if (x == null && y == null)
                    return 0;
                else if (x == null)
                    return -1;
                else if (y == null)
                    return 1;

                int result = x.Address.Address.CompareTo(y.Address.Address);
                if (result == 0) result = x.Port.CompareTo(y.Port);

                return result;
            }
        }

        #endregion IComparers

        /// <summary>An immutable dictionary mapping from <seealso cref="UUID"/>
        /// to <seealso cref="IClientAPI"/> references</summary>
        private ImmutableMap<UUID, IClientAPI> m_dict;
        /// <summary>An immutable dictionary mapping from <seealso cref="IPEndPoint"/>
        /// to <seealso cref="IClientAPI"/> references</summary>
        private ImmutableMap<IPEndPoint, IClientAPI> m_dict2;
        /// <summary>Immutability grants thread safety for concurrent reads and
        /// read-writes, but not concurrent writes</summary>
        private object m_writeLock = new object();

        /// <summary>Number of clients in the collection</summary>
        public int Count { get { return m_dict.Count; } }

        /// <summary>
        /// Default constructor
        /// </summary>
        public ClientManager()
        {
            m_dict = new ImmutableMap<UUID, IClientAPI>(new UUIDComparer());
            m_dict2 = new ImmutableMap<IPEndPoint, IClientAPI>(new IPEndPointComparer());
        }

        /// <summary>
        /// Add a client reference to the collection if it does not already
        /// exist
        /// </summary>
        /// <param name="key">UUID of the client</param>
        /// <param name="key2">Remote endpoint of the client</param>
        /// <param name="value">Reference to the client object</param>
        /// <returns>True if the client reference was successfully added,
        /// otherwise false if the given key already existed in the collection</returns>
        public bool Add(UUID key, IPEndPoint key2, IClientAPI value)
        {
            lock (m_writeLock)
            {
                if (!m_dict.ContainsKey(key) && !m_dict2.ContainsKey(key2))
                {
                    m_dict = m_dict.Add(key, value);
                    m_dict2 = m_dict2.Add(key2, value);

                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Remove a client from the collection
        /// </summary>
        /// <param name="key">UUID of the client</param>
        /// <param name="key2">Remote endpoint of the client</param>
        public void Remove(UUID key, IPEndPoint key2)
        {
            lock (m_writeLock)
            {
                m_dict = m_dict.Delete(key);
                m_dict2 = m_dict2.Delete(key2);
            }
        }

        /// <summary>
        /// Resets the client collection
        /// </summary>
        public void Clear()
        {
            lock (m_writeLock)
            {
                m_dict = new ImmutableMap<UUID, IClientAPI>(new UUIDComparer());
                m_dict2 = new ImmutableMap<IPEndPoint, IClientAPI>(new IPEndPointComparer());
            }
        }

        /// <summary>
        /// Checks if a UUID is in the collection
        /// </summary>
        /// <param name="key">UUID to check for</param>
        /// <returns>True if the UUID was found in the collection, otherwise false</returns>
        public bool ContainsKey(UUID key)
        {
            return m_dict.ContainsKey(key);
        }

        /// <summary>
        /// Checks if an endpoint is in the collection
        /// </summary>
        /// <param name="key">Endpoint to check for</param>
        /// <returns>True if the endpoint was found in the collection, otherwise false</returns>
        public bool ContainsKey(IPEndPoint key)
        {
            return m_dict2.ContainsKey(key);
        }

        /// <summary>
        /// Attempts to fetch a value out of the collection
        /// </summary>
        /// <param name="key">UUID of the client to retrieve</param>
        /// <param name="value">Retrieved client, or null on lookup failure</param>
        /// <returns>True if the lookup succeeded, otherwise false</returns>
        public bool TryGetValue(UUID key, out IClientAPI value)
        {
            return m_dict.TryGetValue(key, out value);
        }

        /// <summary>
        /// Attempts to fetch a value out of the collection
        /// </summary>
        /// <param name="key">Endpoint of the client to retrieve</param>
        /// <param name="value">Retrieved client, or null on lookup failure</param>
        /// <returns>True if the lookup succeeded, otherwise false</returns>
        public bool TryGetValue(IPEndPoint key, out IClientAPI value)
        {
            return m_dict2.TryGetValue(key, out value);
        }

        /// <summary>
        /// Performs a given task in parallel for each of the elements in the
        /// collection
        /// </summary>
        /// <param name="action">Action to perform on each element</param>
        public void ForEach(Action<IClientAPI> action)
        {
            Parallel.ForEach<IClientAPI>(m_dict.Values, action);
        }
    }
}
