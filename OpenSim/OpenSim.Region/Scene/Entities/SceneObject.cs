/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Types;
using OpenSim.Framework.Inventory;

namespace OpenSim.Region
{
    public class SceneObject : Entity
    {
        private LLUUID rootUUID;
        //private Dictionary<LLUUID, Primitive> ChildPrimitives = new Dictionary<LLUUID, Primitive>();
        protected Primitive rootPrimitive;
        private World m_world;
        protected ulong regionHandle;

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
            this.rootPrimitive = new Primitive( this.regionHandle, this.m_world, addPacket, agentID, localID);
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
            //needs changing
            ObjectPropertiesPacket proper = new ObjectPropertiesPacket();
            proper.ObjectData = new ObjectPropertiesPacket.ObjectDataBlock[1];
            proper.ObjectData[0] = new ObjectPropertiesPacket.ObjectDataBlock();
            proper.ObjectData[0].ItemID = LLUUID.Zero;
            proper.ObjectData[0].CreationDate = (ulong)this.rootPrimitive.primData.CreationDate;
            proper.ObjectData[0].CreatorID = this.rootPrimitive.primData.OwnerID;
            proper.ObjectData[0].FolderID = LLUUID.Zero;
            proper.ObjectData[0].FromTaskID = LLUUID.Zero;
            proper.ObjectData[0].GroupID = LLUUID.Zero;
            proper.ObjectData[0].InventorySerial = 0;
            proper.ObjectData[0].LastOwnerID = LLUUID.Zero;
            proper.ObjectData[0].ObjectID = this.uuid;
            proper.ObjectData[0].OwnerID = this.rootPrimitive.primData.OwnerID;
            proper.ObjectData[0].TouchName = new byte[0];
            proper.ObjectData[0].TextureID = new byte[0];
            proper.ObjectData[0].SitName = new byte[0];
            proper.ObjectData[0].Name = new byte[0];
            proper.ObjectData[0].Description = new byte[0];
            proper.ObjectData[0].OwnerMask = this.rootPrimitive.primData.OwnerMask;
            proper.ObjectData[0].NextOwnerMask = this.rootPrimitive.primData.NextOwnerMask;
            proper.ObjectData[0].GroupMask = this.rootPrimitive.primData.GroupMask;
            proper.ObjectData[0].EveryoneMask = this.rootPrimitive.primData.EveryoneMask;
            proper.ObjectData[0].BaseMask = this.rootPrimitive.primData.BaseMask;

            client.OutPacket(proper);
            
        }

    }
}
