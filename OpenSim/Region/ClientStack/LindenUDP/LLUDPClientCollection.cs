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
using OpenSim.Framework;
using OpenMetaverse;
using BclExtras.Collections;

using ReaderWriterLockImpl = OpenMetaverse.ReaderWriterLockSlim;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public sealed class UDPClientCollection
    {
        #region IComparers

        private sealed class IPEndPointComparer : IComparer<IPEndPoint>
        {
            public int Compare(IPEndPoint x, IPEndPoint y)
            {
                int result = x.Address.Address.CompareTo(y.Address.Address);
                if (result == 0) result = x.Port.CompareTo(y.Port);
                return result;
            }
        }

        #endregion IComparers

        private ImmutableMap<IPEndPoint, LLUDPClient> m_dict;

        public UDPClientCollection()
        {
            m_dict = new ImmutableMap<IPEndPoint, LLUDPClient>(new IPEndPointComparer());
        }

        public void Add(IPEndPoint key, LLUDPClient value)
        {
            m_dict = m_dict.Add(key, value);
        }

        public void Remove(IPEndPoint key)
        {
            m_dict = m_dict.Delete(key);
        }

        public void Clear()
        {
            m_dict = new ImmutableMap<IPEndPoint, LLUDPClient>(new IPEndPointComparer());
        }

        public int Count
        {
            get { return m_dict.Count; }
        }

        public bool ContainsKey(IPEndPoint key)
        {
            return m_dict.ContainsKey(key);
        }

        public bool TryGetValue(IPEndPoint key, out LLUDPClient value)
        {
            return m_dict.TryGetValue(key, out value);
        }

        public void ForEach(Action<LLUDPClient> action)
        {
            Parallel.ForEach<LLUDPClient>(m_dict.Values, action); 
        }
    }
}
