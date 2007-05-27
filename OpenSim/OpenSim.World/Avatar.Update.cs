using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Interfaces;

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
            return null;
        }

        public void SendInitialPosition()
        {
           
        }

        public void SendOurAppearance()
        {
           
        }

        public void SendOurAppearance(IClientAPI OurClient)
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
            return null;
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
