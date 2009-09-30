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
    public delegate void ForEachClientDelegate(IClientAPI client);

    public class ClientManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<uint, IClientAPI> m_clients;

        public ClientManager()
        {
            m_clients = new Dictionary<uint, IClientAPI>();
        }

        public void ForEachClient(ForEachClientDelegate whatToDo)
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
                    whatToDo(LocalClients[i]);
                }
                catch (Exception e)
                {
                    m_log.Warn("[CLIENT]: Unable to do ForEachClient for one of the clients" + "\n Reason: " + e.ToString());
                }
            }
        }

        public void Remove(uint id)
        {
            lock (m_clients)
            {
                m_clients.Remove(id);
            }
        }

        public void Add(uint id, IClientAPI client)
        {
            lock (m_clients)
            {
                m_clients.Add(id, client);
            }
        }

        /// <summary>
        /// Pass incoming packet to client.
        /// </summary>
        /// <param name="circuitCode">uint identifying the connection/client.</param>
        /// <param name="packet">object containing the packet.</param>
        public void InPacket(uint circuitCode, object packet)
        {
            IClientAPI client;
            bool tryGetRet = false;
            
            lock (m_clients)
                tryGetRet = m_clients.TryGetValue(circuitCode, out client);
            
            if (tryGetRet)
            {
                client.InPacket(packet);
            }
        }

        public void CloseAllAgents(uint circuitCode)
        {
            IClientAPI client;
            bool tryGetRet = false;
            lock (m_clients)
                tryGetRet = m_clients.TryGetValue(circuitCode, out client);
            if (tryGetRet)
            {
                CloseAllCircuits(client.AgentId);
            }
        }

        public void CloseAllCircuits(UUID agentId)
        {
            uint[] circuits = GetAllCircuits(agentId);
            // We're using a for loop here so changes to the circuits don't cause it to completely fail.

            for (int i = 0; i < circuits.Length; i++)
            {
                IClientAPI client;
                try
                {
                    bool tryGetRet = false;
                    lock (m_clients)
                        tryGetRet = m_clients.TryGetValue(circuits[i], out client);
                    if (tryGetRet)
                    {
                        Remove(client.CircuitCode);
                        client.Close(false);
                    }
                }
                catch (Exception e)
                {
                    m_log.Error(string.Format("[CLIENT]: Unable to shutdown circuit for: {0}\n Reason: {1}", agentId, e));
                }
            }
        }

        // [Obsolete("Using Obsolete to drive development is invalid.  Obsolete presumes that something new has already been created to replace this.")]
        public uint[] GetAllCircuits(UUID agentId)
        {
            List<uint> circuits = new List<uint>();
            // Wasteful, I know
            IClientAPI[] LocalClients = new IClientAPI[0];
            lock (m_clients)
            {
                LocalClients = new IClientAPI[m_clients.Count];
                m_clients.Values.CopyTo(LocalClients, 0);
            }

            for (int i = 0; i < LocalClients.Length; i++)
            {
                if (LocalClients[i].AgentId == agentId)
                {
                    circuits.Add(LocalClients[i].CircuitCode);
                }
            }
            return circuits.ToArray();
        }

        public List<uint> GetAllCircuitCodes()
        {
            List<uint> circuits;

            lock (m_clients)
            {
                circuits = new List<uint>(m_clients.Keys);
            }

            return circuits;
        }

        public void ViewerEffectHandler(IClientAPI sender, List<ViewerEffectEventHandlerArg> args)
        {
            // TODO: don't create new blocks if recycling an old packet
            List<ViewerEffectPacket.EffectBlock> effectBlock = new List<ViewerEffectPacket.EffectBlock>();
            for (int i = 0; i < args.Count; i++)
            {
                ViewerEffectPacket.EffectBlock effect = new ViewerEffectPacket.EffectBlock();
                effect.AgentID = args[i].AgentID;
                effect.Color = args[i].Color;
                effect.Duration = args[i].Duration;
                effect.ID = args[i].ID;
                effect.Type = args[i].Type;
                effect.TypeData = args[i].TypeData;
                effectBlock.Add(effect);
            }
            ViewerEffectPacket.EffectBlock[] effectBlockArray = effectBlock.ToArray();

            IClientAPI[] LocalClients;
            lock (m_clients)
            {
                LocalClients = new IClientAPI[m_clients.Count];
                m_clients.Values.CopyTo(LocalClients, 0);
            }

            for (int i = 0; i < LocalClients.Length; i++)
            {
                if (LocalClients[i].AgentId != sender.AgentId)
                {
                    LocalClients[i].SendViewerEffect(effectBlockArray);
                }
            }
        }

        public bool TryGetClient(uint circuitId, out IClientAPI user)
        {
            lock (m_clients)
            {
                return m_clients.TryGetValue(circuitId, out user);
            }
        }
    }
}
