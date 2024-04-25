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
using System.Net;
using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// Maps from client AgentID and RemoteEndPoint values to IClientAPI
    /// references for all of the connected clients
    /// </summary>
    public class ClientManager
    {
        /// <summary>A dictionary mapping from <seealso cref="UUID"/>
        /// to <seealso cref="IClientAPI"/> references</summary>
        private readonly Dictionary<UUID, IClientAPI> m_dictbyUUID = new();
        /// <summary>A dictionary mapping from <seealso cref="IPEndPoint"/>
        /// to <seealso cref="IClientAPI"/> references</summary>
        private readonly Dictionary<IPEndPoint, IClientAPI> m_dictbyIPe= new();
        /// <summary>snapshot collection of current <seealso cref="IClientAPI"/>
        /// references</summary>
        private IClientAPI[] m_array = null;
        /// <summary>Synchronization object for writing to the collections</summary>
        private readonly object m_syncRoot = new();

        /// <summary>Number of clients in the collection</summary>
        public int Count
        {
            get
            {
                lock (m_syncRoot) 
                    return m_dictbyUUID.Count;
            }
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public ClientManager()
        {
        }

        /// <summary>
        /// Add a client reference to the collection if it does not already
        /// exist
        /// </summary>
        /// <param name="value">Reference to the client object</param>
        /// <returns>True if the client reference was successfully added,
        /// otherwise false if the given key already existed in the collection</returns>
        public bool Add(IClientAPI value)
        {
            lock (m_syncRoot)
            {
                m_dictbyUUID[value.AgentId] = value;
                m_dictbyIPe[value.RemoteEndPoint] = value;
                m_array = null;
            }

            return true;
        }

        /// <summary>
        /// Remove a client from the collection
        /// </summary>
        /// <param name="key">UUID of the client to remove</param>
        /// <returns>True if a client was removed, or false if the given UUID
        /// was not present in the collection</returns>
        public bool Remove(UUID key)
        {
            lock (m_syncRoot)
            {
                if (m_dictbyUUID.Remove(key, out IClientAPI value))
                {
                    m_dictbyIPe.Remove(value.RemoteEndPoint);
                    m_array = null;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Resets the client collection
        /// </summary>
        public void Clear()
        {
            lock (m_syncRoot)
            {
                m_dictbyUUID.Clear();
                m_dictbyIPe.Clear();
                m_array = null;
            }
        }

        /// <summary>
        /// Checks if a UUID is in the collection
        /// </summary>
        /// <param name="key">UUID to check for</param>
        /// <returns>True if the UUID was found in the collection, otherwise false</returns>
        public bool ContainsKey(UUID key)
        {
            lock (m_syncRoot)
                return m_dictbyUUID.ContainsKey(key);
        }

        /// <summary>
        /// Checks if an endpoint is in the collection
        /// </summary>
        /// <param name="key">Endpoint to check for</param>
        /// <returns>True if the endpoint was found in the collection, otherwise false</returns>
        public bool ContainsKey(IPEndPoint key)
        {
            lock (m_syncRoot)
                return m_dictbyIPe.ContainsKey(key);
        }

        /// <summary>
        /// Attempts to fetch a value out of the collection
        /// </summary>
        /// <param name="key">UUID of the client to retrieve</param>
        /// <param name="value">Retrieved client, or null on lookup failure</param>
        /// <returns>True if the lookup succeeded, otherwise false</returns>
        public bool TryGetValue(UUID key, out IClientAPI value)
        {
            try
            {
                lock (m_syncRoot)
                    return m_dictbyUUID.TryGetValue(key, out value);
            }
            catch
            {
                value = null;
                return false;
            }
        }

        /// <summary>
        /// Attempts to fetch a value out of the collection
        /// </summary>
        /// <param name="key">Endpoint of the client to retrieve</param>
        /// <param name="value">Retrieved client, or null on lookup failure</param>
        /// <returns>True if the lookup succeeded, otherwise false</returns>
        public bool TryGetValue(IPEndPoint key, out IClientAPI value)
        {
            try
            {
                lock (m_syncRoot)
                    return m_dictbyIPe.TryGetValue(key, out value);
            }
            catch
            {
                value = null;
                return false;
            }
        }

        /// <summary>
        /// Performs a given task synchronously for each of the elements in
        /// the collection
        /// </summary>
        /// <param name="action">Action to perform on each element</param>
        public void ForEach(Action<IClientAPI> action)
        {
            IClientAPI[] localArray;
            lock (m_syncRoot)
            {
                if (m_array is null)
                {
                    if (m_dictbyUUID.Count == 0)
                        return;

                    m_array = new IClientAPI[m_dictbyUUID.Count];
                    m_dictbyUUID.Values.CopyTo(m_array, 0);
                }
                localArray = m_array;
            }

            for (int i = 0; i < localArray.Length; i++)
                action(localArray[i]);
        }
    }
}
