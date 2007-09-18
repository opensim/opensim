using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;

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

        public void Remove(uint id)
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

        public void ConnectionClosed(uint circuitCode)
        {
            IClientAPI client;

            if (m_clients.TryGetValue(circuitCode, out client))
            {
                m_clients.Remove(circuitCode);
                client.Close();

                // TODO: Now remove all local childagents too
            }
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
    }
}
