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

        private string description = "";

        public string SitName = "";
        public string TouchName = "";
        public string Text = "";

        public LLUUID CreatorID;
        public LLUUID OwnerID;
        public LLUUID LastOwnerID;
        public Int32 CreationDate;

        public uint ParentID = 0;

        public uint OwnerMask = FULL_MASK_PERMISSIONS;
        public uint NextOwnerMask = FULL_MASK_PERMISSIONS;
        public uint GroupMask = FULL_MASK_PERMISSIONS;
        public uint EveryoneMask = FULL_MASK_PERMISSIONS;
        public uint BaseMask = FULL_MASK_PERMISSIONS;

        private PrimitiveBaseShape m_Shape;

        public SceneObject m_RootParent;
        public  bool isRootPrim;
        public EntityBase m_Parent;

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
                this.m_pos = m_Parent.Pos - value; //should we being subtracting the parent position
            }

        }

        public string Description
        {
            get
            {
                return this.description;
            }
            set
            {
                this.description = value;
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
            if (this.updateFlag == 1) // is a new prim just been created/reloaded or has major changes
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
            this.CreatorID = this.OwnerID;
            this.LastOwnerID = LLUUID.Zero;
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
            pShape.ProfileHollow = addPacket.ObjectData.ProfileHollow;
            pShape.PathRadiusOffset = addPacket.ObjectData.PathRadiusOffset;
            pShape.PathRevolutions = addPacket.ObjectData.PathRevolutions;
            pShape.PathTaperX = addPacket.ObjectData.PathTaperX;
            pShape.PathTaperY = addPacket.ObjectData.PathTaperY;
            pShape.PathTwist = addPacket.ObjectData.PathTwist;
            pShape.PathTwistBegin = addPacket.ObjectData.PathTwistBegin;

            this.updateFlag = 1;
        }

        public void AddNewChildren(SceneObject linkObject)
        {
           // Console.WriteLine("linking new prims " + linkObject.rootLocalID + " to me (" + this.LocalId + ")");
            //TODO check permissions
            this.children.Add(linkObject.rootPrimitive);
            linkObject.rootPrimitive.SetNewParent(this, this.m_RootParent);

            this.m_world.DeleteEntity(linkObject.rootUUID);
            linkObject.DeleteAllChildren();
        }

        public void SetNewParent(Primitive newParent, SceneObject rootParent)
        {
            LLVector3 oldPos = new LLVector3(this.Pos.X, this.Pos.Y, this.Pos.Z);
            //Console.WriteLine("have a new parent and my old position is " + this.Pos.X + " , " + this.Pos.Y + " , " + this.Pos.Z);
            this.isRootPrim = false;
            this.m_Parent = newParent;
            this.ParentID = newParent.LocalId;
            this.SetRootParent(rootParent);
          // Console.WriteLine("have a new parent and its position is " + this.m_Parent.Pos.X + " , " + this.m_Parent.Pos.Y + " , " + this.m_Parent.Pos.Z);
            this.Pos = oldPos;
          //  Console.WriteLine("have a new parent so my new offset position is " + this.Pos.X + " , " + this.Pos.Y + " , " + this.Pos.Z);
            this.updateFlag = 1;

        }

        public void SetRootParent(SceneObject newRoot)
        {
            this.m_RootParent = newRoot;
            foreach (Primitive child in children)
            {
                child.SetRootParent(newRoot);
            }
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

            remoteClient.SendPrimitiveToClient(this.m_regionHandle, 64096, this.LocalId, this.m_Shape, lPos, new LLUUID("00000000-0000-0000-9999-000000000005"), this.flags, this.uuid, this.OwnerID, this.Text, this.ParentID);
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
