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
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.Framework
{
    public delegate void ForEachClientDelegate(IClientAPI client);

    public class ClientManager
    {
        private Dictionary<uint, IClientAPI> m_clients;

        public void ForEachClient(ForEachClientDelegate whatToDo)
        {

            // Wasteful, I know
            IClientAPI[] LocalClients = new IClientAPI[0];
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
                catch (System.Exception e)
                {
                    OpenSim.Framework.Console.MainLog.Instance.Warn("CLIENT", "Unable to do ForEachClient for one of the clients" + "\n Reason: " + e.ToString());
                }
            }
        }

        public ClientManager()
        {
            m_clients = new Dictionary<uint, IClientAPI>();
        }

        private void Remove(uint id)
        {
            m_clients.Remove(id);
        }

        public void Add(uint id, IClientAPI client)
        {
            m_clients.Add(id, client);
        }

        public void InPacket(uint circuitCode, Packet packet)
        {
            IClientAPI client;

            if (m_clients.TryGetValue(circuitCode, out client))
            {
                client.InPacket(packet);
            }
        }

        public void CloseAllAgents(uint circuitCode)
        {
            IClientAPI client;

            if (m_clients.TryGetValue(circuitCode, out client))
            {
                CloseAllCircuits(client.AgentId);
            }
        }

        public void CloseAllCircuits(LLUUID agentId)
        {
            uint[] circuits = GetAllCircuits(agentId);
            // We're using a for loop here so changes to the circuits don't cause it to completely fail.

            for (int i = 0; i < circuits.Length; i++)
            {
                IClientAPI client;
                try
                {

                    if (m_clients.TryGetValue(circuits[i], out client))
                    {
                        Remove(client.CircuitCode);
                        client.Close();
                    }
                }
                catch (System.Exception e)
                {
                    OpenSim.Framework.Console.MainLog.Instance.Error("CLIENT", "Unable to shutdown circuit for: " + agentId.ToString() + "\n Reason: " + e.ToString());
                }
            }

            
        }

        private uint[] GetAllCircuits(LLUUID agentId)
        {
            List<uint> circuits = new List<uint>();
            // Wasteful, I know
            IClientAPI[] LocalClients = new IClientAPI[0];
            lock (m_clients)
            {
                LocalClients = new IClientAPI[m_clients.Count];
                m_clients.Values.CopyTo(LocalClients, 0);
            }


            for (int i = 0; i < LocalClients.Length; i++ )
            {
                if (LocalClients[i].AgentId == agentId)
                {
                    circuits.Add(LocalClients[i].CircuitCode);
                }
            }
            return circuits.ToArray();
        }


        public void ViewerEffectHandler(IClientAPI sender, ViewerEffectPacket.EffectBlock[] effectBlock)
        {
            ViewerEffectPacket packet = new ViewerEffectPacket();
            packet.Effect = effectBlock;

            // Wasteful, I know
            IClientAPI[] LocalClients = new IClientAPI[0];
            lock (m_clients)
            {
                LocalClients = new IClientAPI[m_clients.Count];
                m_clients.Values.CopyTo(LocalClients, 0);
            }


            for (int i = 0; i < LocalClients.Length; i++)
            {
                if (LocalClients[i].AgentId != sender.AgentId)
                {
                    packet.AgentData.AgentID = LocalClients[i].AgentId;
                    packet.AgentData.SessionID = LocalClients[i].SessionId;
                    LocalClients[i].OutPacket(packet, ThrottleOutPacketType.Task);
                }

            }
        }

        public bool TryGetClient(uint circuitId, out IClientAPI user)
        {
            return m_clients.TryGetValue(circuitId, out user);
        }
    }
}