using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife.Packets;

namespace OpenSim.world
{
    partial class Avatar
    {
        private List<ImprovedTerseObjectUpdatePacket.ObjectDataBlock> updateList = new List<ImprovedTerseObjectUpdatePacket.ObjectDataBlock>();
        
        public void SendPacketToViewer(Packet packet)
        {
            this.ControllingClient.OutPacket(packet);
        }

        public void AddTerseUpdateToViewersList(ImprovedTerseObjectUpdatePacket.ObjectDataBlock terseBlock)
        {

        }

        public void SendUpdateListToViewer()
        {

        }
    }
}
