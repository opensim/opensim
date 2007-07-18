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
    public delegate void PrimCountTaintedDelegate();

    public class AllNewSceneObjectGroup : EntityBase
    {
        private Encoding enc = Encoding.ASCII;

        protected AllNewSceneObjectPart m_rootPart;
        protected Dictionary<LLUUID, AllNewSceneObjectPart> m_parts = new Dictionary<LLUUID, AllNewSceneObjectPart>();

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
        public AllNewSceneObjectGroup()
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
        public void LinkToGroup(AllNewSceneObjectGroup objectGroup)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primID"></param>
        /// <returns></returns>
        public AllNewSceneObjectPart HasChildPrim(LLUUID primID)
        {
            AllNewSceneObjectPart childPart = null;
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
        public AllNewSceneObjectPart HasChildPrim(uint localID)
        {
            foreach (AllNewSceneObjectPart part in this.m_parts.Values)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="part"></param>
        private void SetPartAsRoot(AllNewSceneObjectPart part)
        {
            this.m_rootPart = part;
            this.m_uuid = part.uuid;
            this.m_localId = part.m_localID;
            part.ParentID = 0;
            part.UpdateHandler = delegate(ref LLVector3 pos, UpdateType direction, AllNewSceneObjectPart objectPart)
            {
                switch (direction)
                {
                    case UpdateType.InComingNewPosition:
                        this.m_pos = new LLVector3(pos.X, pos.Y, pos.Z);
                        pos.X = 0;
                        pos.Y = 0;
                        pos.Z = 0;
                        break;

                    case UpdateType.SinglePositionEdit:
                        LLVector3 newPos = new LLVector3(pos.X, pos.Y, pos.Z);
                        LLVector3 oldPos = new LLVector3(this.Pos.X + objectPart.OffsetPosition.X, this.Pos.Y + objectPart.OffsetPosition.Y, this.Pos.Z + objectPart.OffsetPosition.Z);
                        LLVector3 diff = oldPos - newPos;
                        Axiom.Math.Vector3 axDiff = new Vector3(diff.X, diff.Y, diff.Z);
                        Axiom.Math.Quaternion partRotation = new Quaternion(objectPart.RotationOffset.W, objectPart.RotationOffset.X, objectPart.RotationOffset.Y, objectPart.RotationOffset.Z);
                        axDiff = partRotation.Inverse() * axDiff;
                        diff.X = axDiff.x;
                        diff.Y = axDiff.y;
                        diff.Z = axDiff.z;

                        foreach (AllNewSceneObjectPart obPart in this.m_parts.Values)
                        {
                            if (obPart.uuid == objectPart.uuid)
                            {
                                obPart.OffsetPosition = obPart.OffsetPosition + diff;
                            }
                        }
                        this.Pos = newPos;
                        pos.X = newPos.X;
                        pos.Y = newPos.Y;
                        pos.Z = newPos.Z;
                        break;

                    case UpdateType.ResizeOffset:
                        this.Pos += pos;
                        LLVector3 offset = new LLVector3(-pos.X, -pos.Y, -pos.Z);
                        foreach (AllNewSceneObjectPart obPart2 in this.m_parts.Values)
                        {
                            if (obPart2.uuid == objectPart.uuid)
                            {
                                obPart2.OffsetPosition = obPart2.OffsetPosition + offset;
                            }
                        }
                        pos.X = 0;
                        pos.Y = 0;
                        pos.Z = 0;
                        break;

                    case UpdateType.SingleRotationEdit:
                        break;
                }


                return pos;
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="part"></param>
        private void SetPartAsNonRoot(AllNewSceneObjectPart part)
        {
            part.ParentID = this.m_rootPart.m_localID;
            part.UpdateHandler = delegate(ref LLVector3 pos, UpdateType direction, AllNewSceneObjectPart objectPart)
            {
                return pos;
            };
        }
    }
}
