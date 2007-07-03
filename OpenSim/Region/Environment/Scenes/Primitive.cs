using System;
using System.Collections.Generic;
using Axiom.MathLib;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Types;

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
        public bool isRootPrim;
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
                if (isRootPrim)
                {
                    m_Parent.Pos = value;
                }
                this.m_pos = value - m_Parent.Pos;
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

        public LLVector3 Scale
        {
            set
            {
                this.m_Shape.Scale = value;
            }
            get
            {
                return this.m_Shape.Scale;
            }
        }

        public Primitive(ulong regionHandle, Scene world, ObjectAddPacket addPacket, LLUUID ownerID, uint localID, bool isRoot, EntityBase parent, SceneObject rootObject)
        {
            m_regionHandle = regionHandle;
            m_world = world;
            inventoryItems = new Dictionary<LLUUID, InventoryItem>();
            this.m_Parent = parent;
            this.isRootPrim = isRoot;
            this.m_RootParent = rootObject;
            this.CreateFromPacket(addPacket, ownerID, localID);
            this.rotation = Axiom.MathLib.Quaternion.Identity;
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

            foreach (EntityBase child in children)
            {
                child.update();
            }
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="linkObject"></param>
        public void AddNewChildren(SceneObject linkObject)
        {
            // Console.WriteLine("linking new prims " + linkObject.rootLocalID + " to me (" + this.LocalId + ")");
            //TODO check permissions
            this.children.Add(linkObject.rootPrimitive);
            linkObject.rootPrimitive.SetNewParent(this, this.m_RootParent);

            this.m_world.DeleteEntity(linkObject.rootUUID);
            linkObject.DeleteAllChildren();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newParent"></param>
        /// <param name="rootParent"></param>
        public void SetNewParent(Primitive newParent, SceneObject rootParent)
        {
            LLVector3 oldPos = new LLVector3(this.Pos.X, this.Pos.Y, this.Pos.Z);
            this.isRootPrim = false;
            this.m_Parent = newParent;
            this.ParentID = newParent.LocalId;
            this.SetRootParent(rootParent);
            this.Pos = oldPos;
            Axiom.MathLib.Vector3 axPos = new Axiom.MathLib.Vector3(this.m_pos.X, m_pos.Y, m_pos.Z);
            axPos = this.m_Parent.rotation.Inverse() * axPos;
            this.m_pos = new LLVector3(axPos.x, axPos.y, axPos.z);
            this.rotation = this.rotation * this.m_Parent.rotation.Inverse();
            this.updateFlag = 1;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newRoot"></param>
        public void SetRootParent(SceneObject newRoot)
        {
            this.m_RootParent = newRoot;
            this.m_RootParent.AddChildToList(this);
            foreach (Primitive child in children)
            {
                child.SetRootParent(newRoot);
            }
        }

        public void AddOffsetToChildren(LLVector3 offset)
        {
            foreach (Primitive prim in this.children)
            {
                prim.m_pos += offset;
                prim.updateFlag = 2;
            }
        }

        #region Resizing/Scale
        public void ResizeGoup(LLVector3 scale)
        {
            LLVector3 offset = (scale - this.m_Shape.Scale);
            offset.X /= 2;
            offset.Y /= 2;
            offset.Z /= 2;
            if (this.isRootPrim)
            {
                this.m_Parent.Pos += offset;
            }
            else
            {
                this.m_pos += offset;
            }

            this.AddOffsetToChildren(new LLVector3(-offset.X, -offset.Y, -offset.Z));
            this.m_Shape.Scale = scale;

            this.updateFlag = 1;
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        public void UpdatePosition(LLVector3 pos)
        {
            LLVector3 newPos = new LLVector3(pos.X, pos.Y, pos.Z);

            this.Pos = newPos;
            this.updateFlag = 2;
        }

        public void UpdateRotation(LLQuaternion rot)
        {
            this.rotation = new Axiom.MathLib.Quaternion(rot.W, rot.X, rot.Y, rot.Z);
            this.updateFlag = 2;
        }

        public void UpdateGroupMouseRotation(LLVector3 pos,  LLQuaternion rot)
        {
            this.rotation = new Axiom.MathLib.Quaternion(rot.W, rot.X, rot.Y, rot.Z);
            this.Pos = pos;
            this.updateFlag = 2;
        }

        public void UpdateShape(ObjectShapePacket.ObjectDataBlock shapeBlock)
        {
            this.m_Shape.PathBegin = shapeBlock.PathBegin;
            this.m_Shape.PathEnd = shapeBlock.PathEnd;
            this.m_Shape.PathScaleX = shapeBlock.PathScaleX;
            this.m_Shape.PathScaleY = shapeBlock.PathScaleY;
            this.m_Shape.PathShearX = shapeBlock.PathShearX;
            this.m_Shape.PathShearY = shapeBlock.PathShearY;
            this.m_Shape.PathSkew = shapeBlock.PathSkew;
            this.m_Shape.ProfileBegin = shapeBlock.ProfileBegin;
            this.m_Shape.ProfileEnd = shapeBlock.ProfileEnd;
            this.m_Shape.PathCurve = shapeBlock.PathCurve;
            this.m_Shape.ProfileCurve = shapeBlock.ProfileCurve;
            this.m_Shape.ProfileHollow = shapeBlock.ProfileHollow;
            this.m_Shape.PathRadiusOffset = shapeBlock.PathRadiusOffset;
            this.m_Shape.PathRevolutions = shapeBlock.PathRevolutions;
            this.m_Shape.PathTaperX = shapeBlock.PathTaperX;
            this.m_Shape.PathTaperY = shapeBlock.PathTaperY;
            this.m_Shape.PathTwist = shapeBlock.PathTwist;
            this.m_Shape.PathTwistBegin = shapeBlock.PathTwistBegin;
            this.updateFlag = 1;
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
            LLQuaternion lRot;
            lRot = new LLQuaternion(this.rotation.x, this.rotation.y, this.rotation.z, this.rotation.w);

            remoteClient.SendPrimitiveToClient(this.m_regionHandle, 64096, this.LocalId, this.m_Shape, lPos, lRot, new LLUUID("00000000-0000-0000-9999-000000000005"), this.flags, this.uuid, this.OwnerID, this.Text, this.ParentID);
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
            Quaternion lRot;

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
