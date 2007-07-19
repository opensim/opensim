using System.Collections.Generic;
using System.Text;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    // public delegate void PrimCountTaintedDelegate();

    public class AllNewSceneObjectGroup2 : EntityBase
    {
        private Encoding enc = Encoding.ASCII;

        protected AllNewSceneObjectPart2 m_rootPart;
        protected Dictionary<LLUUID, AllNewSceneObjectPart2> m_parts = new Dictionary<LLUUID, AllNewSceneObjectPart2>();

        public event PrimCountTaintedDelegate OnPrimCountTainted;

        /// <summary>
        /// 
        /// </summary>
        public int primCount
        {
            get
            {
                return 1;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public LLVector3 GroupCentrePoint
        {
            get
            {
                return new LLVector3(0, 0, 0);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public AllNewSceneObjectGroup2()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        public void FlagGroupForFullUpdate()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        public void FlagGroupForTerseUpdate()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objectGroup"></param>
        public void LinkToGroup(AllNewSceneObjectGroup2 objectGroup)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primID"></param>
        /// <returns></returns>
        private AllNewSceneObjectPart2 GetChildPrim(LLUUID primID)
        {
            AllNewSceneObjectPart2 childPart = null;
            if (this.m_parts.ContainsKey(primID))
            {
                childPart = this.m_parts[primID];
            }
            return childPart;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <returns></returns>
        private AllNewSceneObjectPart2 GetChildPrim(uint localID)
        {
            foreach (AllNewSceneObjectPart2 part in this.m_parts.Values)
            {
                if (part.m_localID == localID)
                {
                    return part;
                }
            }
            return null;
        }

        public void TriggerTainted()
        {
            if (OnPrimCountTainted != null)
            {
                this.OnPrimCountTainted();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="pos"></param>
        /// <param name="remoteClient"></param>
        public void GrapMovement(LLVector3 offset, LLVector3 pos, IClientAPI remoteClient)
        {
            this.Pos = pos;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public void GetProperites(IClientAPI client)
        {
            ObjectPropertiesPacket proper = new ObjectPropertiesPacket();
            proper.ObjectData = new ObjectPropertiesPacket.ObjectDataBlock[1];
            proper.ObjectData[0] = new ObjectPropertiesPacket.ObjectDataBlock();
            proper.ObjectData[0].ItemID = LLUUID.Zero;
            proper.ObjectData[0].CreationDate = (ulong)this.m_rootPart.CreationDate;
            proper.ObjectData[0].CreatorID = this.m_rootPart.CreatorID;
            proper.ObjectData[0].FolderID = LLUUID.Zero;
            proper.ObjectData[0].FromTaskID = LLUUID.Zero;
            proper.ObjectData[0].GroupID = LLUUID.Zero;
            proper.ObjectData[0].InventorySerial = 0;
            proper.ObjectData[0].LastOwnerID = this.m_rootPart.LastOwnerID;
            proper.ObjectData[0].ObjectID = this.m_uuid;
            proper.ObjectData[0].OwnerID = this.m_rootPart.OwnerID;
            proper.ObjectData[0].TouchName = enc.GetBytes(this.m_rootPart.TouchName + "\0");
            proper.ObjectData[0].TextureID = new byte[0];
            proper.ObjectData[0].SitName = enc.GetBytes(this.m_rootPart.SitName + "\0");
            proper.ObjectData[0].Name = enc.GetBytes(this.m_rootPart.Name + "\0");
            proper.ObjectData[0].Description = enc.GetBytes(this.m_rootPart.Description + "\0");
            proper.ObjectData[0].OwnerMask = this.m_rootPart.OwnerMask;
            proper.ObjectData[0].NextOwnerMask = this.m_rootPart.NextOwnerMask;
            proper.ObjectData[0].GroupMask = this.m_rootPart.GroupMask;
            proper.ObjectData[0].EveryoneMask = this.m_rootPart.EveryoneMask;
            proper.ObjectData[0].BaseMask = this.m_rootPart.BaseMask;

            client.OutPacket(proper);
        }

        #region Shape
        /// <summary>
        /// 
        /// </summary>
        /// <param name="shapeBlock"></param>
        public void UpdateShape(ObjectShapePacket.ObjectDataBlock shapeBlock, uint localID)
        {
            AllNewSceneObjectPart2 part = this.GetChildPrim(localID);
            if (part != null)
            {
                part.UpdateShape(shapeBlock);
            }
        }
        #endregion

        #region Position
        public void UpdateGroupPosition(LLVector3 pos)
        {
            this.m_pos = pos;
        }

        public void UpdateSinglePosition(LLVector3 pos, uint localID)
        {
            AllNewSceneObjectPart2 part = this.GetChildPrim(localID);
            if (part != null)
            {
                if (part.uuid == this.m_rootPart.uuid)
                {
                    this.UpdateRootPosition(pos);
                }
                else
                {
                    part.UpdateOffSet(pos);
                }
            }
        }

        private void UpdateRootPosition(LLVector3 pos)
        {
            LLVector3 newPos = new LLVector3(pos.X, pos.Y, pos.Z);
            LLVector3 oldPos = new LLVector3(this.Pos.X + this.m_rootPart.OffsetPosition.X, this.Pos.Y + this.m_rootPart.OffsetPosition.Y, this.Pos.Z + this.m_rootPart.OffsetPosition.Z);
            LLVector3 diff = oldPos - newPos;
            Axiom.Math.Vector3 axDiff = new Vector3(diff.X, diff.Y, diff.Z);
            Axiom.Math.Quaternion partRotation = new Quaternion(this.m_rootPart.RotationOffset.W, this.m_rootPart.RotationOffset.X, this.m_rootPart.RotationOffset.Y, this.m_rootPart.RotationOffset.Z);
            axDiff = partRotation.Inverse() * axDiff;
            diff.X = axDiff.x;
            diff.Y = axDiff.y;
            diff.Z = axDiff.z;

            foreach (AllNewSceneObjectPart2 obPart in this.m_parts.Values)
            {
                if (obPart.uuid != this.m_rootPart.uuid)
                {
                    obPart.OffsetPosition = obPart.OffsetPosition + diff;
                }
            }
            this.Pos = newPos;
            pos.X = newPos.X;
            pos.Y = newPos.Y;
            pos.Z = newPos.Z;
        }
        #endregion

        #region Roation
        public void UpdateGroupRotation(LLQuaternion rot)
        {
            this.m_rootPart.UpdateRotation(rot);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        public void UpdateGroupRotation(LLVector3 pos, LLQuaternion rot)
        {
            this.m_rootPart.UpdateRotation(rot);
            this.m_pos = pos;
        }

        public void UpdateSingleRotation(LLQuaternion rot, uint localID)
        {
            AllNewSceneObjectPart2 part = this.GetChildPrim(localID);
            if (part != null)
            {
                if (part.uuid == this.m_rootPart.uuid)
                {
                    this.UpdateRootRotation(rot);
                }
                else
                {
                    part.UpdateRotation(rot);
                }
            }
        }
        private void UpdateRootRotation(LLQuaternion rot)
        {
            this.m_rootPart.UpdateRotation(rot);
            Axiom.Math.Quaternion axRot = new Quaternion(rot.W, rot.X, rot.Y, rot.Z);
            Axiom.Math.Quaternion oldParentRot = new Quaternion(this.m_rootPart.RotationOffset.W, this.m_rootPart.RotationOffset.X, this.m_rootPart.RotationOffset.Y, this.m_rootPart.RotationOffset.Z);

            foreach (AllNewSceneObjectPart2 prim in this.m_parts.Values)
            {
                if (prim.uuid != this.m_rootPart.uuid)
                {
                    Vector3 axPos = new Vector3(prim.OffsetPosition.X, prim.OffsetPosition.Y, prim.OffsetPosition.Z);
                    axPos = oldParentRot * axPos;
                    axPos = axRot.Inverse() * axPos;
                    prim.OffsetPosition = new LLVector3(axPos.x, axPos.y, axPos.z);
                    Axiom.Math.Quaternion primsRot = new Quaternion(prim.RotationOffset.W, prim.RotationOffset.X, prim.RotationOffset.Y, prim.RotationOffset.Z);
                    Axiom.Math.Quaternion newRot = oldParentRot * primsRot;
                    newRot = axRot.Inverse() * newRot;
                    prim.RotationOffset = new LLQuaternion(newRot.w, newRot.x, newRot.y, newRot.z);
                }
            }
        }
        #endregion
        /// <summary>
        /// 
        /// </summary>
        /// <param name="part"></param>
        private void SetPartAsRoot(AllNewSceneObjectPart2 part)
        {
            this.m_rootPart = part;
            this.m_uuid = part.uuid;
            this.m_localId = part.m_localID;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="part"></param>
        private void SetPartAsNonRoot(AllNewSceneObjectPart2 part)
        {

        }
    }
}
