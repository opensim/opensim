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
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace OpenSim.Framework
{
    public class ClientManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<uint, IClientAPI> m_clients = new Dictionary<uint, IClientAPI>();

        public void Add(uint circuitCode, IClientAPI client)
        {
            lock (m_clients)
                m_clients.Add(circuitCode, client);
        }

        public bool Remove(uint circuitCode)
        {
            lock (m_clients)
                return m_clients.Remove(circuitCode);
        }

        public bool TryGetClient(uint circuitCode, out IClientAPI user)
        {
            lock (m_clients)
                return m_clients.TryGetValue(circuitCode, out user);
        }

        public void ForEachClient(Action<IClientAPI> action)
        {
            IClientAPI[] LocalClients;
            lock (m_clients)
            {
                LocalClients = new IClientAPI[m_clients.Count];
                m_clients.Values.CopyTo(LocalClients, 0);
            }

            for (int i = 0; i < LocalClients.Length; i++)
            {
                try
                {
                    action(LocalClients[i]);
                }
                catch (Exception e)
                {
                    m_log.Warn("[CLIENT]: Unable to do ForEachClient for one of the clients" + "\n Reason: " + e.ToString());
                }
            }
        }
    }
}
