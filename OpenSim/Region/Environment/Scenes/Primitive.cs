using System;
using System.Collections.Generic;
using Axiom.Math;
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
        private uint m_flags = 32 + 65536 + 131072 + 256 + 4 + 8 + 2048 + 524288 + 268435456 + 128;

        private Dictionary<LLUUID, InventoryItem> inventoryItems;

        private string m_description = "";

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
        public bool m_isRootPrim;
        public EntityBase m_Parent;

        #region Properties
        /// <summary>
        /// If rootprim, will return world position
        /// otherwise will return local offset from rootprim
        /// </summary>
        public override LLVector3 Pos 
        {
            get
            {
                if (m_isRootPrim)
                {
                    //if we are rootprim then our offset should be zero
                    return this.m_pos + m_Parent.Pos;
                }
                else
                {
                    return this.m_pos;
                }
            }
            set
            {
                if (m_isRootPrim)
                {
                    m_Parent.Pos = value;
                }
                this.m_pos = value - m_Parent.Pos;
            }

        }

        public LLVector3 WorldPos
        {
            get
            {
                if (!this.m_isRootPrim)
                {
                    Primitive parentPrim = (Primitive)this.m_Parent;
                    Axiom.Math.Vector3 offsetPos = new Vector3(this.m_pos.X, this.m_pos.Y, this.m_pos.Z);
                    offsetPos = parentPrim.Rotation * offsetPos;
                    return parentPrim.WorldPos + new LLVector3(offsetPos.x, offsetPos.y, offsetPos.z);
                }
                else
                {
                    return this.Pos;
                }
            }
        }

        public string Description
        {
            get
            {
                return this.m_description;
            }
            set
            {
                this.m_description = value;
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
        #endregion

        #region Constructors
        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="world"></param>
        /// <param name="addPacket"></param>
        /// <param name="ownerID"></param>
        /// <param name="localID"></param>
        /// <param name="isRoot"></param>
        /// <param name="parent"></param>
        /// <param name="rootObject"></param>
        public Primitive(ulong regionHandle, Scene world, ObjectAddPacket addPacket, LLUUID ownerID, uint localID, bool isRoot, EntityBase parent, SceneObject rootObject)
        {
            m_regionHandle = regionHandle;
            m_world = world;
            inventoryItems = new Dictionary<LLUUID, InventoryItem>();
            this.m_Parent = parent;
            this.m_isRootPrim = isRoot;
            this.m_RootParent = rootObject;
            this.CreateFromPacket(addPacket, ownerID, localID);
            this.Rotation = Axiom.Math.Quaternion.Identity;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Empty constructor for duplication</remarks>
        public Primitive()
        {

        }

        #endregion

        #region Duplication

        public Primitive Copy(EntityBase parent, SceneObject rootParent)
        {
            Primitive dupe = (Primitive)this.MemberwiseClone();
            // TODO: Copy this properly.
            dupe.inventoryItems = this.inventoryItems;
            dupe.m_Parent = parent;
            dupe.m_RootParent = rootParent;
            // TODO: Copy this properly.
            dupe.m_Shape = this.m_Shape;

            uint newLocalID = this.m_world.PrimIDAllocate();
            dupe.LocalId = newLocalID;

            dupe.Scale = new LLVector3(this.Scale.X, this.Scale.Y, this.Scale.Z);
            dupe.Rotation = new Quaternion(this.Rotation.w, this.Rotation.x, this.Rotation.y, this.Rotation.z);
            dupe.Pos = new LLVector3(this.Pos.X, this.Pos.Y, this.Pos.Z);

            return dupe;
        }

        #endregion


        #region Override from EntityBase
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
        #endregion

        #region Setup
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
        #endregion

        #region Linking / unlinking
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
            this.m_isRootPrim = false;
            this.m_Parent = newParent;
            this.ParentID = newParent.LocalId;
            this.m_RootParent = rootParent;
            this.m_RootParent.AddChildToList(this);
            this.Pos = oldPos;
            Axiom.Math.Vector3 axPos = new Axiom.Math.Vector3(this.m_pos.X, m_pos.Y, m_pos.Z);
            axPos = this.m_Parent.Rotation.Inverse() * axPos;
            this.m_pos = new LLVector3(axPos.x, axPos.y, axPos.z);
            Axiom.Math.Quaternion oldRot = new Quaternion(this.Rotation.w, this.Rotation.x, this.Rotation.y, this.Rotation.z);
            this.Rotation =  this.m_Parent.Rotation.Inverse() * this.Rotation;
            this.updateFlag = 1;

            foreach (Primitive child in children)
            {
                child.SetRootParent(rootParent, newParent, oldPos, oldRot);
            }
            children.Clear();
           

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newRoot"></param>
        public void SetRootParent(SceneObject newRoot , Primitive newParent, LLVector3 oldParentPosition, Axiom.Math.Quaternion oldParentRotation)
        {
            LLVector3 oldPos = new LLVector3(this.Pos.X, this.Pos.Y, this.Pos.Z);
            Axiom.Math.Vector3 axOldPos = new Vector3(oldPos.X, oldPos.Y, oldPos.Z);
            axOldPos = oldParentRotation * axOldPos;
            oldPos = new LLVector3(axOldPos.x, axOldPos.y, axOldPos.z);
            oldPos += oldParentPosition;
            Axiom.Math.Quaternion oldRot = new Quaternion(this.Rotation.w, this.Rotation.x, this.Rotation.y, this.Rotation.z);
            this.m_isRootPrim = false;
            this.m_Parent = newParent;
            this.ParentID = newParent.LocalId;
            newParent.AddToChildrenList(this);
            this.m_RootParent = newRoot;
            this.m_RootParent.AddChildToList(this);
            this.Pos = oldPos;
            Axiom.Math.Vector3 axPos = new Axiom.Math.Vector3(this.m_pos.X, m_pos.Y, m_pos.Z);
            axPos = this.m_Parent.Rotation.Inverse() * axPos;
            this.m_pos = new LLVector3(axPos.x, axPos.y, axPos.z);
            this.Rotation = oldParentRotation * this.Rotation;
            this.Rotation =  this.m_Parent.Rotation.Inverse()* this.Rotation ;
            this.updateFlag = 1;
            foreach (Primitive child in children)
            {
                child.SetRootParent(newRoot, newParent, oldPos, oldRot);
            }
            children.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="offset"></param>
        public void AddOffsetToChildren(LLVector3 offset)
        {
            foreach (Primitive prim in this.children)
            {
                prim.m_pos += offset;
                prim.updateFlag = 2;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prim"></param>
        public void AddToChildrenList(Primitive prim)
        {
            this.children.Add(prim);
        }
        #endregion

        #region Resizing/Scale
        /// <summary>
        /// 
        /// </summary>
        /// <param name="scale"></param>
        public void ResizeGoup(LLVector3 scale)
        {
            LLVector3 offset = (scale - this.m_Shape.Scale);
            offset.X /= 2;
            offset.Y /= 2;
            offset.Z /= 2;
            if (this.m_isRootPrim)
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

        #region Position
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        public void UpdateGroupPosition(LLVector3 pos)
        {
            LLVector3 newPos = new LLVector3(pos.X, pos.Y, pos.Z);

            this.Pos = newPos;
            this.updateFlag = 2;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        public void UpdateSinglePosition(LLVector3 pos)
        {
           // Console.WriteLine("updating single prim position");
            if (this.m_isRootPrim)
            {
                LLVector3 newPos = new LLVector3(pos.X, pos.Y, pos.Z);
                LLVector3 oldPos = new LLVector3(Pos.X, Pos.Y, Pos.Z);
                LLVector3 diff = oldPos - newPos;
                Axiom.Math.Vector3 axDiff = new Vector3(diff.X, diff.Y, diff.Z);
                axDiff = this.Rotation.Inverse() * axDiff;
                diff.X = axDiff.x;
                diff.Y = axDiff.y;
                diff.Z = axDiff.z;
                this.Pos = newPos;

                foreach (Primitive prim in this.children)
                {
                    prim.m_pos += diff;
                    prim.updateFlag = 2;
                }
                this.updateFlag = 2;
            }
            else
            {
                LLVector3 newPos = new LLVector3(pos.X, pos.Y, pos.Z);
                this.m_pos = newPos;
                this.updateFlag = 2;
            }
        }

        #endregion

        #region Rotation
        /// <summary>
        /// 
        /// </summary>
        /// <param name="rot"></param>
        public void UpdateGroupRotation(LLQuaternion rot)
        {
            this.Rotation = new Axiom.Math.Quaternion(rot.W, rot.X, rot.Y, rot.Z);
            this.updateFlag = 2;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        public void UpdateGroupMouseRotation(LLVector3 pos, LLQuaternion rot)
        {
            this.Rotation = new Axiom.Math.Quaternion(rot.W, rot.X, rot.Y, rot.Z);
            this.Pos = pos;
            this.updateFlag = 2;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rot"></param>
        public void UpdateSingleRotation(LLQuaternion rot)
        {
            //Console.WriteLine("updating single prim rotation");
            Axiom.Math.Quaternion axRot = new Axiom.Math.Quaternion(rot.W, rot.X, rot.Y, rot.Z);
            Axiom.Math.Quaternion oldParentRot = new Quaternion(this.Rotation.w, this.Rotation.x, this.Rotation.y, this.Rotation.z);
            this.Rotation = axRot;
            foreach (Primitive prim in this.children)
            {
                Axiom.Math.Vector3 axPos = new Vector3(prim.m_pos.X, prim.m_pos.Y, prim.m_pos.Z);
                axPos = oldParentRot * axPos;
                axPos = axRot.Inverse() * axPos;
                prim.m_pos = new LLVector3(axPos.x, axPos.y, axPos.z);
                prim.Rotation = oldParentRot * prim.Rotation ;
                prim.Rotation = axRot.Inverse()* prim.Rotation;
                prim.updateFlag = 2;
            }
            this.updateFlag = 2;
        }
        #endregion

        #region Shape
        /// <summary>
        /// 
        /// </summary>
        /// <param name="shapeBlock"></param>
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
        #endregion

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
            lRot = new LLQuaternion(this.Rotation.x, this.Rotation.y, this.Rotation.z, this.Rotation.w);

            remoteClient.SendPrimitiveToClient(this.m_regionHandle, 64096, this.LocalId, this.m_Shape, lPos, lRot, new LLUUID("00000000-0000-0000-9999-000000000005"), this.m_flags, this.uuid, this.OwnerID, this.Text, this.ParentID);
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
            lRot = this.Rotation;

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
