using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Interfaces;

namespace OpenSim.Framework
{
    public delegate void ForEachClientDelegate( IClientAPI client );
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

        public void Add(uint id, IClientAPI client )
        {
            m_clients.Add( id, client );
        }
    }
}
