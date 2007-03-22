using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife.Packets;

namespace OpenSim
{
    public class QueItem
    {
        public QueItem()
        {
        }

        public Packet Packet;
        public bool Incoming;
    }

}
