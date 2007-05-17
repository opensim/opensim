using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.types;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Types;
using OpenSim.Framework.Inventory;

namespace OpenSim.world
{
    public class SceneObject : Entity
    {

        public SceneObject()
        {

        }

        public void CreateFromPacket(ObjectAddPacket addPacket, LLUUID agentID, uint localID)
        {
        }

        public void CreateFromBytes(byte[] data)
        {

        }

        public override void update()
        {

        }

        public override void BackUp()
        {

        }

        public void GetProperites(SimClient client)
        {
            ObjectPropertiesPacket proper = new ObjectPropertiesPacket();
            proper.ObjectData = new ObjectPropertiesPacket.ObjectDataBlock[1];
            proper.ObjectData[0] = new ObjectPropertiesPacket.ObjectDataBlock();
            proper.ObjectData[0].ItemID = LLUUID.Zero;
            proper.ObjectData[0].CreationDate = (ulong)this.primData.CreationDate;
            proper.ObjectData[0].CreatorID = this.primData.OwnerID;
            proper.ObjectData[0].FolderID = LLUUID.Zero;
            proper.ObjectData[0].FromTaskID = LLUUID.Zero;
            proper.ObjectData[0].GroupID = LLUUID.Zero;
            proper.ObjectData[0].InventorySerial = 0;
            proper.ObjectData[0].LastOwnerID = LLUUID.Zero;
            proper.ObjectData[0].ObjectID = this.uuid;
            proper.ObjectData[0].OwnerID = primData.OwnerID;
            proper.ObjectData[0].TouchName = new byte[0];
            proper.ObjectData[0].TextureID = new byte[0];
            proper.ObjectData[0].SitName = new byte[0];
            proper.ObjectData[0].Name = new byte[0];
            proper.ObjectData[0].Description = new byte[0];
            proper.ObjectData[0].OwnerMask = this.primData.OwnerMask;
            proper.ObjectData[0].NextOwnerMask = this.primData.NextOwnerMask;
            proper.ObjectData[0].GroupMask = this.primData.GroupMask;
            proper.ObjectData[0].EveryoneMask = this.primData.EveryoneMask;
            proper.ObjectData[0].BaseMask = this.primData.BaseMask;

            client.OutPacket(proper);
        }

    }
}
