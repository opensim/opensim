using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim
{
    public class SimClientBase
    {

        protected virtual void ProcessInPacket(Packet Pack)
        {

        }

        protected virtual void ProcessOutPacket(Packet Pack)
        {

        }

        public virtual void InPacket(Packet NewPack)
        {

        }

        public virtual void OutPacket(Packet NewPack)
        {

        }
    }
}
