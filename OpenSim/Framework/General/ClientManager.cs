using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using libsecondlife;

namespace OpenSim.Framework
{
    public delegate void ForEachClientDelegate(IClientAPI client);
    public class ClientManager
    {
        private Dictionary<uint, IClientAPI> m_clients;

        public void ForEachClient(ForEachClientDelegate whatToDo)
        {
            foreach (IClientAPI client in m_clients.Values)
            {
                whatToDo(client);
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

        public void InPacket(uint circuitCode, libsecondlife.Packets.Packet packet)
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

        public void CloseAllCircuits( LLUUID agentId )
        {
            uint[] circuits = GetAllCircuits(agentId);
            foreach (uint circuit in circuits )
            {
                IClientAPI client;
                if (m_clients.TryGetValue(circuit, out client))
                {
                    Remove(circuit);
                    client.Close();
                }
            }            
        }

        private uint[] GetAllCircuits(LLUUID agentId)
        {
            List<uint> circuits = new List<uint>();

            foreach (KeyValuePair<uint, IClientAPI> pair in m_clients)
            {
                if( pair.Value.AgentId == agentId )
                {
                    circuits.Add( pair.Key );
                }
            }

            return circuits.ToArray();
        }

      
        public void ViewerEffectHandler(IClientAPI sender, ViewerEffectPacket.EffectBlock[] effectBlock)
        {
            ViewerEffectPacket packet = new ViewerEffectPacket();
            packet.Effect = effectBlock;

            foreach (IClientAPI client in m_clients.Values)
            {
                if (client.AgentId != sender.AgentId)
                {
                    packet.AgentData.AgentID = client.AgentId;
                    packet.AgentData.SessionID = client.SessionId;
                    client.OutPacket(packet);
                }
            }
        }

        public bool TryGetClient(uint circuitId, out IClientAPI user)
        {
            return m_clients.TryGetValue(circuitId, out user);
        }
    }
}
