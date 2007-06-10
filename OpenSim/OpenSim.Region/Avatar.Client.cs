using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife.Packets;

namespace OpenSim.Region
{
    partial class Avatar
    {
        private List<ImprovedTerseObjectUpdatePacket.ObjectDataBlock> updateList = new List<ImprovedTerseObjectUpdatePacket.ObjectDataBlock>();
        private List<Entity> interestList = new List<Entity>();

        /// <summary>
        ///  Forwards a packet to the Avatar's client (IClientAPI object). 
        ///  Note: Quite likely to be obsolete once the Client API is finished
        /// </summary>
        /// <param name="packet"></param>
        public void SendPacketToViewer(Packet packet)
        {
            this.ControllingClient.OutPacket(packet);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="terseBlock"></param>
        public void AddTerseUpdateToViewersList(ImprovedTerseObjectUpdatePacket.ObjectDataBlock terseBlock)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        public void SendUpdateListToViewer()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        private void UpdateInterestList()
        {

        }
    }
}
