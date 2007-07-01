using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Types;
using OpenSim.Framework.Inventory;

namespace OpenSim.Region.Environment.Scenes
{
    public class Primitive : EntityBase
    {
        private const uint FULL_MASK_PERMISSIONS = 2147483647;

        private LLVector3 positionLastFrame = new LLVector3(0, 0, 0);
        private ulong m_regionHandle;
        private byte updateFlag = 0;
        private uint flags = 32 + 65536 + 131072 + 256 + 4 + 8 + 2048 + 524288 + 268435456 + 128;

        private Dictionary<LLUUID, InventoryItem> inventoryItems;

        public LLUUID OwnerID;
        public Int32 CreationDate;
        public uint OwnerMask = FULL_MASK_PERMISSIONS;
        public uint NextOwnerMask = FULL_MASK_PERMISSIONS;
        public uint GroupMask = FULL_MASK_PERMISSIONS;
        public uint EveryoneMask = FULL_MASK_PERMISSIONS;
        public uint BaseMask = FULL_MASK_PERMISSIONS;

        private PrimitiveBaseShape m_Shape;

        private SceneObject m_RootParent;
        private bool isRootPrim;
        private EntityBase m_Parent;

        public override LLVector3 Pos
        {
            get
            {
                if (isRootPrim)
                {
                    return this.m_pos + m_Parent.Pos;
                }
                else
                {
                    return this.m_pos;
                }
            }
            set
            {
                this.m_pos = value - m_Parent.Pos; //should we being subtracting the parent position
            }

        }

        public Primitive(ulong regionHandle, Scene world, ObjectAddPacket addPacket, LLUUID ownerID, uint localID, bool isRoot, EntityBase parent , SceneObject rootObject)
        {
            m_regionHandle = regionHandle;
            m_world = world;
            inventoryItems = new Dictionary<LLUUID, InventoryItem>();
            this.m_Parent = parent;
            this.isRootPrim = isRoot;
            this.m_RootParent = rootObject;
            this.CreateFromPacket(addPacket, ownerID, localID);
        }

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

            base.update();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addPacket"></param>
        /// <param name="ownerID"></param>
        /// <param name="localID"></param>
        public void CreateFromPacket(ObjectAddPacket addPacket, LLUUID ownerID, uint localID)
        {
            this.CreationDate = (Int32)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            this.OwnerID = ownerID;
            this.Pos = addPacket.ObjectData.RayEnd;
            this.uuid = LLUUID.Random();
            this.m_localId = (uint)(localID);

            PrimitiveBaseShape pShape = new PrimitiveBaseShape();
            this.m_Shape = pShape;

            pShape.PCode = addPacket.ObjectData.PCode;
            pShape.PathBegin = addPacket.ObjectData.PathBegin;
            pShape.PathEnd = addPacket.ObjectData.PathEnd;
            pShape.PathScaleX = addPacket.ObjectData.PathScaleX;
            pShape.PathScaleY = addPacket.ObjectData.PathScaleY;
            pShape.PathShearX = addPacket.ObjectData.PathShearX;
            pShape.PathShearY = addPacket.ObjectData.PathShearY;
            pShape.PathSkew = addPacket.ObjectData.PathSkew;
            pShape.ProfileBegin = addPacket.ObjectData.ProfileBegin;
            pShape.ProfileEnd = addPacket.ObjectData.ProfileEnd;
            pShape.Scale = addPacket.ObjectData.Scale;
            pShape.PathCurve = addPacket.ObjectData.PathCurve;
            pShape.ProfileCurve = addPacket.ObjectData.ProfileCurve;
            pShape.ParentID = 0;
            pShape.ProfileHollow = addPacket.ObjectData.ProfileHollow;
            pShape.PathRadiusOffset = addPacket.ObjectData.PathRadiusOffset;
            pShape.PathRevolutions = addPacket.ObjectData.PathRevolutions;
            pShape.PathTaperX = addPacket.ObjectData.PathTaperX;
            pShape.PathTaperY = addPacket.ObjectData.PathTaperY;
            pShape.PathTwist = addPacket.ObjectData.PathTwist;
            pShape.PathTwistBegin = addPacket.ObjectData.PathTwistBegin;

            this.updateFlag = 1;
        }

        public void AddToChildren(SceneObject linkObject)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        public void UpdatePosition(LLVector3 pos)
        {
            LLVector3 newPos = new LLVector3(pos.X, pos.Y, pos.Z);
            if (this.isRootPrim)
            {
                this.m_Parent.Pos = newPos;
            }
            this.Pos = newPos;
            this.updateFlag = 2;
        }

        #region Client Update Methods

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
            lPos = this.Pos;

            remoteClient.SendPrimitiveToClient2(this.m_regionHandle, 64096, this.LocalId, this.m_Shape, lPos, new LLUUID("00000000-0000-0000-9999-000000000005"), this.flags, this.uuid, this.OwnerID);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendFullUpdateToAllClients()
        {
            List<ScenePresence> avatars = this.m_world.RequestAvatarList();
            for (int i = 0; i < avatars.Count; i++)
            {
                this.SendFullUpdateToClient(avatars[i].ControllingClient);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        public void SendTerseUpdateForAllChildren(IClientAPI remoteClient)
        {
            this.SendTerseUpdateToClient(remoteClient);
            for (int i = 0; i < this.children.Count; i++)
            {
                if (this.children[i] is Primitive)
                {
                    ((Primitive)this.children[i]).SendTerseUpdateForAllChildren(remoteClient);
                }
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

            lPos = this.Pos;
            lRot = this.rotation;

            LLQuaternion mRot = new LLQuaternion(lRot.x, lRot.y, lRot.z, lRot.w);
            RemoteClient.SendPrimTerseUpdate(this.m_regionHandle, 64096, this.LocalId, lPos, mRot);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendTerseUpdateToALLClients()
        {
            List<ScenePresence> avatars = this.m_world.RequestAvatarList();
            for (int i = 0; i < avatars.Count; i++)
            {
                this.SendTerseUpdateToClient(avatars[i].ControllingClient);
            }
        }

        #endregion
    }
}
