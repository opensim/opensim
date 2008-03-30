using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework
{
    [Serializable]
    public class RegionUpData
    {
        private uint m_X = 0;
        private uint m_Y = 0;
        private string m_ipaddr = "";
        private int m_port = 0;
        public RegionUpData(uint X, uint Y, string ipaddr, int port)
        {
            m_X = X;
            m_Y = Y;
            m_ipaddr = ipaddr;
            m_port = port;
        }

        public uint X
        {
            get { return m_X; }
        }
        public uint Y
        {
            get { return m_Y; }
        }
        public string IPADDR
        {
            get { return m_ipaddr; }
        }
        public int PORT
        {
            get { return m_port; } 
        }
            
    }
}
