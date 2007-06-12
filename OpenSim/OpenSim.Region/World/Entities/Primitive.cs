
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
using OpenSim.Region.types;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Types;
using OpenSim.Framework.Inventory;

namespace OpenSim.Region
{
    public class Primitive : Entity
    {
        internal PrimData primData;
        private LLVector3 positionLastFrame = new LLVector3(0, 0, 0);
       // private Dictionary<uint, IClientAPI> m_clientThreads;
        private ulong m_regionHandle;
        private const uint FULL_MASK_PERMISSIONS = 2147483647;
        private bool physicsEnabled = false;
        private byte updateFlag = 0;

        private Dictionary<LLUUID, InventoryItem> inventoryItems;

        #region Properties

        public LLVector3 Scale
        {
            set
            {
                this.primData.Scale = value;
                //this.dirtyFlag = true;
            }
            get
            {
                return this.primData.Scale;
            }
        }

        public PhysicsActor PhysActor
        {
            set
            {
                this._physActor = value;
            }
        }

        public override LLVector3 Pos
        {
            get
            {
                return base.Pos;
            }
            set
            {
                base.Pos = value;
            }
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clientThreads"></param>
        /// <param name="regionHandle"></param>
        /// <param name="world"></param>
        public Primitive( ulong regionHandle, World world)
        {
           // m_clientThreads = clientThreads;
            m_regionHandle = regionHandle;
            m_world = world;
            inventoryItems = new Dictionary<LLUUID, InventoryItem>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="world"></param>
        /// <param name="addPacket"></param>
        /// <param name="ownerID"></param>
        /// <param name="localID"></param>
        public Primitive(ulong regionHandle, World world, ObjectAddPacket addPacket, LLUUID ownerID, uint localID)
        {
            // m_clientThreads = clientThreads;
            m_regionHandle = regionHandle;
            m_world = world;
            inventoryItems = new Dictionary<LLUUID, InventoryItem>();
            this.CreateFromPacket(addPacket, ownerID, localID);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="clientThreads"></param>
        /// <param name="regionHandle"></param>
        /// <param name="world"></param>
        /// <param name="owner"></param>
        /// <param name="fullID"></param>
        /// <param name="localID"></param>
        public Primitive( ulong regionHandle, World world, LLUUID owner, LLUUID fullID, uint localID)
        {
          //  m_clientThreads = clientThreads;
            m_regionHandle = regionHandle;
            m_world = world;
            inventoryItems = new Dictionary<LLUUID, InventoryItem>();
            this.primData = new PrimData();
            this.primData.CreationDate = (Int32)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            this.primData.OwnerID = owner;
            this.primData.FullID = this.uuid = fullID;
            this.primData.LocalID = this.localid = localID;
        }

        /// <summary>
        /// Constructor to create a default cube 
        /// </summary>
        /// <param name="clientThreads"></param>
        /// <param name="regionHandle"></param>
        /// <param name="world"></param>
        /// <param name="owner"></param>
        /// <param name="localID"></param>
        /// <param name="position"></param>
        public Primitive( ulong regionHandle, World world, LLUUID owner, uint localID, LLVector3 position)
        {
            //m_clientThreads = clientThreads;
            m_regionHandle = regionHandle;
            m_world = world;
            inventoryItems = new Dictionary<LLUUID, InventoryItem>();
            this.primData = PrimData.DefaultCube();
            this.primData.OwnerID = owner;
            this.primData.LocalID = this.localid = localID;
            this.Pos = this.primData.Position = position;

            this.updateFlag = 1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public byte[] GetByteArray()
        {
            byte[] result = null;
            List<byte[]> dataArrays = new List<byte[]>();
            dataArrays.Add(primData.ToBytes());
            foreach (Entity child in children)
            {
                if (child is OpenSim.Region.Primitive)
                {
                    dataArrays.Add(((OpenSim.Region.Primitive)child).GetByteArray());
                }
            }
            byte[] primstart = Helpers.StringToField("<Prim>");
            byte[] primend = Helpers.StringToField("</Prim>");
            int totalLength = primstart.Length + primend.Length;
            for (int i = 0; i < dataArrays.Count; i++)
            {
                totalLength += dataArrays[i].Length;
            }

            result = new byte[totalLength];
            int arraypos = 0;
            Array.Copy(primstart, 0, result, 0, primstart.Length);
            arraypos += primstart.Length;
            for (int i = 0; i < dataArrays.Count; i++)
            {
                Array.Copy(dataArrays[i], 0, result, arraypos, dataArrays[i].Length);
                arraypos += dataArrays[i].Length;
            }
            Array.Copy(primend, 0, result, arraypos, primend.Length);

            return result;
        }

        #region Overridden Methods

        /// <summary>
        /// 
        /// </summary>
        public override void update()
        {
            if (this.updateFlag == 1) // is a new prim just been created/reloaded 
            {
                this.SendFullUpdateToAllClients();
                this.updateFlag = 0;
            }
            if (this.updateFlag == 2) //some change has been made so update the clients
            {
                this.SendTerseUpdateToALLClients();
                this.updateFlag = 0;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public override void BackUp()
        {

        }

        #endregion

        #region Packet handlers

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        public void UpdatePosition(LLVector3 pos)
        {
            this.Pos = new LLVector3(pos.X, pos.Y, pos.Z);
            this.updateFlag = 2;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addPacket"></param>
        public void UpdateShape(ObjectShapePacket.ObjectDataBlock addPacket)
        {
            this.primData.PathBegin = addPacket.PathBegin;
            this.primData.PathEnd = addPacket.PathEnd;
            this.primData.PathScaleX = addPacket.PathScaleX;
            this.primData.PathScaleY = addPacket.PathScaleY;
            this.primData.PathShearX = addPacket.PathShearX;
            this.primData.PathShearY = addPacket.PathShearY;
            this.primData.PathSkew = addPacket.PathSkew;
            this.primData.ProfileBegin = addPacket.ProfileBegin;
            this.primData.ProfileEnd = addPacket.ProfileEnd;
            this.primData.PathCurve = addPacket.PathCurve;
            this.primData.ProfileCurve = addPacket.ProfileCurve;
            this.primData.ProfileHollow = addPacket.ProfileHollow;
            this.primData.PathRadiusOffset = addPacket.PathRadiusOffset;
            this.primData.PathRevolutions = addPacket.PathRevolutions;
            this.primData.PathTaperX = addPacket.PathTaperX;
            this.primData.PathTaperY = addPacket.PathTaperY;
            this.primData.PathTwist = addPacket.PathTwist;
            this.primData.PathTwistBegin = addPacket.PathTwistBegin;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tex"></param>
        public void UpdateTexture(byte[] tex)
        {
            this.primData.Texture = tex;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pack"></param>
        public void UpdateObjectFlags(ObjectFlagUpdatePacket pack)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prim"></param>
        public void AssignToParent(Primitive prim)
        {

        }

        #endregion

        # region Inventory Methods
        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool AddToInventory(InventoryItem item)
        {
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="itemID"></param>
        /// <returns></returns>
        public InventoryItem RemoveFromInventory(LLUUID itemID)
        {
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="simClient"></param>
        /// <param name="packet"></param>
        public void RequestInventoryInfo(IClientAPI simClient, RequestTaskInventoryPacket packet)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="simClient"></param>
        /// <param name="xferID"></param>
        public void RequestXferInventory(IClientAPI simClient, ulong xferID)
        {
            //will only currently work if the total size of the inventory data array is under about 1000 bytes
            SendXferPacketPacket send = new SendXferPacketPacket();

            send.XferID.ID = xferID;
            send.XferID.Packet = 1 + 2147483648;
            send.DataPacket.Data = this.ConvertInventoryToBytes();

            simClient.OutPacket(send);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public byte[] ConvertInventoryToBytes()
        {
            System.Text.Encoding enc = System.Text.Encoding.ASCII;
            byte[] result = new byte[0];
            List<byte[]> inventoryData = new List<byte[]>();
            int totallength = 0;
            foreach (InventoryItem invItem in inventoryItems.Values)
            {
                byte[] data = enc.GetBytes(invItem.ExportString());
                inventoryData.Add(data);
                totallength += data.Length;
            }
            //TODO: copy arrays into the single result array

            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        public void CreateInventoryFromBytes(byte[] data)
        {

        }

        #endregion

        #region Update viewers Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        public void SendFullUpdateForAllChildren(IClientAPI remoteClient)
        {
            this.SendFullUpdateToClient(remoteClient);
            for (int i = 0; i < this.children.Count; i++)
            {
                if (this.children[i] is Primitive)
                {
                    ((Primitive)this.children[i]).SendFullUpdateForAllChildren(remoteClient);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        public void SendFullUpdateToClient(IClientAPI remoteClient)
        {
            LLVector3 lPos;
            if (this._physActor != null && this.physicsEnabled)
            {
                PhysicsVector pPos = this._physActor.Position;
                lPos = new LLVector3(pPos.X, pPos.Y, pPos.Z);
            }
            else
            {
                lPos = this.Pos;
            }

            remoteClient.SendPrimitiveToClient(this.m_regionHandle, 64096, this.localid, this.primData, lPos, new LLUUID("00000000-0000-0000-5005-000000000005"));
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendFullUpdateToAllClients()
        {
            List<Avatar> avatars = this.m_world.RequestAvatarList();
            for (int i = 0; i < avatars.Count; i++)
            {
                this.SendFullUpdateToClient(avatars[i].ControllingClient);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="RemoteClient"></param>
        public void SendTerseUpdateToClient(IClientAPI RemoteClient)
        {
            LLVector3 lPos;
            Axiom.MathLib.Quaternion lRot;
            if (this._physActor != null && this.physicsEnabled)
            {
                PhysicsVector pPos = this._physActor.Position;
                lPos = new LLVector3(pPos.X, pPos.Y, pPos.Z);
                lRot = this._physActor.Orientation;
            }
            else
            {
                lPos = this.Pos;
                lRot = this.rotation;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendTerseUpdateToALLClients()
        {
            List<Avatar> avatars = this.m_world.RequestAvatarList();
            for (int i = 0; i < avatars.Count; i++)
            {
                this.SendTerseUpdateToClient(avatars[i].ControllingClient);
            }
        }

        #endregion

        #region Create Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addPacket"></param>
        /// <param name="ownerID"></param>
        /// <param name="localID"></param>
        public void CreateFromPacket(ObjectAddPacket addPacket, LLUUID ownerID, uint localID)
        {
            PrimData PData = new PrimData();
            this.primData = PData;
            this.primData.CreationDate = (Int32)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

            PData.OwnerID = ownerID;
            PData.PCode = addPacket.ObjectData.PCode;
            PData.PathBegin = addPacket.ObjectData.PathBegin;
            PData.PathEnd = addPacket.ObjectData.PathEnd;
            PData.PathScaleX = addPacket.ObjectData.PathScaleX;
            PData.PathScaleY = addPacket.ObjectData.PathScaleY;
            PData.PathShearX = addPacket.ObjectData.PathShearX;
            PData.PathShearY = addPacket.ObjectData.PathShearY;
            PData.PathSkew = addPacket.ObjectData.PathSkew;
            PData.ProfileBegin = addPacket.ObjectData.ProfileBegin;
            PData.ProfileEnd = addPacket.ObjectData.ProfileEnd;
            PData.Scale = addPacket.ObjectData.Scale;
            PData.PathCurve = addPacket.ObjectData.PathCurve;
            PData.ProfileCurve = addPacket.ObjectData.ProfileCurve;
            PData.ParentID = 0;
            PData.ProfileHollow = addPacket.ObjectData.ProfileHollow;
            PData.PathRadiusOffset = addPacket.ObjectData.PathRadiusOffset;
            PData.PathRevolutions = addPacket.ObjectData.PathRevolutions;
            PData.PathTaperX = addPacket.ObjectData.PathTaperX;
            PData.PathTaperY = addPacket.ObjectData.PathTaperY;
            PData.PathTwist = addPacket.ObjectData.PathTwist;
            PData.PathTwistBegin = addPacket.ObjectData.PathTwistBegin;
            LLVector3 pos1 = addPacket.ObjectData.RayEnd;
            this.primData.FullID = this.uuid = LLUUID.Random();
            this.primData.LocalID = this.localid = (uint)(localID);
            this.primData.Position = this.Pos = pos1;

            this.updateFlag = 1;
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
        /// <param name="primData"></param>
        public void CreateFromPrimData(PrimData primData)
        {
            this.CreateFromPrimData(primData, primData.Position, primData.LocalID, false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primData"></param>
        /// <param name="posi"></param>
        /// <param name="localID"></param>
        /// <param name="newprim"></param>
        public void CreateFromPrimData(PrimData primData, LLVector3 posi, uint localID, bool newprim)
        {

        }

        #endregion

    }
}
