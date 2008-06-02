using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Grid.MessagingServer
{
    // This is a wrapper for a List<LLUUID> so it can be happily stored in a hashtable.
    public class PresenceBackreferenceEntry
    {
        List<LLUUID> AgentList = new List<LLUUID>();

        public PresenceBackreferenceEntry()
        {

        }

        public void Add(LLUUID item)
        {
            lock (AgentList)
            {
                AgentList.Add(item);
            }
        }

        public LLUUID getitem(int index)
        {
            LLUUID result = null;
            lock (AgentList)
            {
                if (index > 0 && index < AgentList.Count)
                {
                    result = AgentList[index];
                }
            }
            return result;
        }
        
        public int Count
        {
            get
            {
                int count = 0;
                lock (AgentList)
                {
                    count = AgentList.Count;
                }
                return count;
            }
        }

        public void Remove(LLUUID item)
        {
            lock (AgentList)
            {
                if (AgentList.Contains(item))
                    AgentList.Remove(item);
            }
        }

        public bool contains(LLUUID item)
        {
            bool result = false;
            lock (AgentList)
            {
                result = AgentList.Contains(item);
            }
            return result;
        }
    }
}
