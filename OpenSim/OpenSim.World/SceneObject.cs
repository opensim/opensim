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
        private LLUUID rootUUID;
        private Dictionary<LLUUID, Primitive> ChildPrimitives = new Dictionary<LLUUID, Primitive>();
        private Dictionary<uint, IClientAPI> m_clientThreads;
        private World m_world;

        /// <summary>
        /// 
        /// </summary>
        public SceneObject()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addPacket"></param>
        /// <param name="agentID"></param>
        /// <param name="localID"></param>
        public void CreateFromPacket(ObjectAddPacket addPacket, LLUUID agentID, uint localID)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        public void CreateFromBytes(byte[] data)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        public override void update()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        public override void BackUp()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public void GetProperites(IClientAPI client)
        {
            /*
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
             * */
        }

    }
}
