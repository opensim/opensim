using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Physics.Manager;

namespace OpenSim.world
{
    partial class Avatar
    {
        public override void update()
        {
            

        }

        public void SendUpdateToOtherClient(Avatar remoteAvatar)
        {
          
        }

        public ObjectUpdatePacket CreateUpdatePacket()
        {
          
        }

        public void SendInitialPosition()
        {
           
        }

        public void SendOurAppearance()
        {
           
        }

        public void SendOurAppearance(ClientView OurClient)
        {
           
        }

        public void SendAppearanceToOtherAgent(Avatar avatarInfo)
        {
            
        }

        public void SetAppearance(byte[] texture, AgentSetAppearancePacket.VisualParamBlock[] visualParam)
        {
           
        }

        public void StopMovement()
        {
           
        }

        public ImprovedTerseObjectUpdatePacket.ObjectDataBlock CreateTerseBlock()
        {
            
        }

        // Sends animation update
        public void SendAnimPack(LLUUID animID, int seq)
        {
            
          
        }

        public void SendAnimPack()
        {
           
        }

    }
}
