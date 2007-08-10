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

    public class SceneObjectGroup : EntityBase
    {
        private Encoding enc = Encoding.ASCII;

        protected SceneObjectPart m_rootPart;
        protected Dictionary<LLUUID, SceneObjectPart> m_parts = new Dictionary<LLUUID, SceneObjectPart>();

        protected ulong m_regionHandle;

        public event PrimCountTaintedDelegate OnPrimCountTainted;

        /// <summary>
        /// 
        /// </summary>
        public int PrimCount
        {
            get { return 1; }
        }

        /// <summary>
        /// 
        /// </summary>
        public LLVector3 GroupCentrePoint
        {
            get { return new LLVector3(0, 0, 0); }
        }

        public Dictionary<LLUUID, SceneObjectPart> Children
        {
            get { return this.m_parts; }
            set { m_parts = value; }
        }

        public SceneObjectPart RootPart
        {
            set { m_rootPart = value; }
        }

        public ulong RegionHandle
        {
            get { return m_regionHandle; }
            set
            {
                m_regionHandle = value;
                lock (this.m_parts)
                {
                    foreach (SceneObjectPart part in this.m_parts.Values)
                    {
                        part.RegionHandle = m_regionHandle;
                    }
                }
            }
        }

        public override LLVector3 Pos
        {
            get { return m_rootPart.GroupPosition; }
            set
            {
                lock (this.m_parts)
                {
                    foreach (SceneObjectPart part in this.m_parts.Values)
                    {
                        part.GroupPosition = value;
                    }
                }
            } 
        }

        public override uint LocalId
        {
            get { return m_rootPart.LocalID; }
            set { m_rootPart.LocalID = value; }
        }

        public override LLUUID UUID
        {
            get { return m_rootPart.UUID; }
            set { m_rootPart.UUID = value; }
        }

        public LLUUID OwnerID
        {
            get { return m_rootPart.OwnerID; }
        }

        /// <summary>
        /// Added because the Parcel code seems to use it
        /// but not sure a object should have this
        /// as what does it tell us? that some avatar has selected it (but not what Avatar/user)
        /// think really there should be a list (or whatever) in each scenepresence
        /// saying what prim(s) that user has selected. 
        /// </summary>
        protected bool m_isSelected = false;
        public bool IsSelected
        {
            get{ return m_isSelected;}
            set { m_isSelected = value; }
        }

        /// <summary>
        /// 
        /// </summary>
        public SceneObjectGroup()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        public SceneObjectGroup(byte[] data)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        public SceneObjectGroup(Scene scene, ulong regionHandle, LLUUID ownerID, uint localID, LLVector3 pos, PrimitiveBaseShape shape)
        {
            m_regionHandle = regionHandle;
            m_scene = scene;

           // this.Pos = pos;
            LLVector3 rootOffset = new LLVector3(0, 0, 0);
            SceneObjectPart newPart = new SceneObjectPart(m_regionHandle, this, ownerID, localID, shape, pos, rootOffset);
            this.m_parts.Add(newPart.UUID, newPart);
            this.SetPartAsRoot(newPart);
            m_scene.EventManager.OnBackup += this.ProcessBackup;
        }


        #region Copying
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public new SceneObjectGroup Copy()
        {
            SceneObjectGroup dupe = (SceneObjectGroup)this.MemberwiseClone();
            dupe.Pos = new LLVector3(Pos.X, Pos.Y, Pos.Z);
            dupe.m_scene = m_scene;
            dupe.m_regionHandle = this.m_regionHandle;

            dupe.CopyRootPart(this.m_rootPart);
            m_scene.EventManager.OnBackup += dupe.ProcessBackup;

            foreach (SceneObjectPart part in this.m_parts.Values)
            {
                if (part.UUID != this.m_rootPart.UUID)
                {
                    dupe.CopyPart(part);
                }
            }
            return dupe;
        }

        /// <summary>
        /// Added as a way for the storage provider to reset the scene, 
        /// most likely a better way to do this sort of thing but for now...
        /// </summary>
        /// <param name="scene"></param>
        public void SetScene(Scene scene)
        {
            m_scene = scene;
            m_scene.EventManager.OnBackup += this.ProcessBackup;
        }

        public void AddPart(SceneObjectPart part)
        {
            part.SetParent(this);
            this.m_parts.Add(part.UUID, part);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="part"></param>
        public void CopyRootPart(SceneObjectPart part)
        {
            SceneObjectPart newPart = part.Copy(m_scene.PrimIDAllocate());
            this.m_parts.Add(newPart.UUID, newPart);
            this.SetPartAsRoot(newPart);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="part"></param>
        public void CopyPart(SceneObjectPart part)
        {
            SceneObjectPart newPart = part.Copy(m_scene.PrimIDAllocate());
            this.m_parts.Add(newPart.UUID, newPart);
            this.SetPartAsNonRoot(newPart);
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public override void Update()
        {
            foreach (SceneObjectPart part in this.m_parts.Values)
            {
                part.SendScheduledUpdates();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ScheduleGroupForFullUpdate()
        {
            foreach (SceneObjectPart part in this.m_parts.Values)
            {
                part.ScheduleFullUpdate();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ScheduleGroupForTerseUpdate()
        {
            foreach (SceneObjectPart part in this.m_parts.Values)
            {
                part.ScheduleTerseUpdate();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendGroupFullUpdate()
        {
            foreach (SceneObjectPart part in this.m_parts.Values)
            {
                part.SendFullUpdateToAllClients();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendGroupTerseUpdate()
        {
            foreach (SceneObjectPart part in this.m_parts.Values)
            {
                part.SendTerseUpdateToAllClients();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objectGroup"></param>
        public void LinkToGroup(SceneObjectGroup objectGroup)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primID"></param>
        /// <returns></returns>
        private SceneObjectPart GetChildPrim(LLUUID primID)
        {
            SceneObjectPart childPart = null;
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
        private SceneObjectPart GetChildPrim(uint localID)
        {
            foreach (SceneObjectPart part in this.m_parts.Values)
            {
                if (part.LocalID == localID)
                {
                    return part;
                }
            }
            return null;
        }

        /// <summary>
        /// Does this group contain the child prim
        /// should be able to remove these methods once we have a entity index in scene
        /// </summary>
        /// <param name="primID"></param>
        /// <returns></returns>
        public bool HasChildPrim(LLUUID primID)
        {
            SceneObjectPart childPart = null;
            if (this.m_parts.ContainsKey(primID))
            {
                childPart = this.m_parts[primID];
                return true;
            }
            return false;
        }

        /// <summary>
        /// Does this group contain the child prim
        /// should be able to remove these methods once we have a entity index in scene
        /// </summary>
        /// <param name="localID"></param>
        /// <returns></returns>
        public bool HasChildPrim(uint localID)
        {
            foreach (SceneObjectPart part in this.m_parts.Values)
            {
                if (part.LocalID == localID)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        public void TriggerTainted()
        {
            if (OnPrimCountTainted != null)
            {
                this.OnPrimCountTainted();
            }
        }
        
         /// <summary>
        /// Processes backup
        /// </summary>
        /// <param name="datastore"></param>
        public void ProcessBackup(OpenSim.Region.Interfaces.IRegionDataStore datastore)
        {
            datastore.StoreObject(this);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="pos"></param>
        /// <param name="remoteClient"></param>
        public void GrabMovement(LLVector3 offset, LLVector3 pos, IClientAPI remoteClient)
        {
            this.Pos = pos;
            this.m_rootPart.SendTerseUpdateToAllClients();
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
            proper.ObjectData[0].ObjectID = this.UUID;
            proper.ObjectData[0].OwnerID = this.m_rootPart.OwnerID;
            proper.ObjectData[0].TouchName = enc.GetBytes(this.m_rootPart.TouchName + "\0");
            proper.ObjectData[0].TextureID = new byte[0];
            proper.ObjectData[0].SitName = enc.GetBytes(this.m_rootPart.SitName + "\0");
            proper.ObjectData[0].Name = enc.GetBytes(this.m_rootPart.PartName + "\0");
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
        /// <param name="name"></param>
        public void SetPartName(string name, uint localID)
        {
             SceneObjectPart part = this.GetChildPrim(localID);
             if (part != null)
             {
                 part.PartName = name;
             }
        }

        public void SetPartDescription(string des, uint localID)
        {
            SceneObjectPart part = this.GetChildPrim(localID);
            if (part != null)
            {
                part.Description = des;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="localID"></param>
        public void GetPartInventory(IClientAPI remoteClient, uint localID)
        {
            SceneObjectPart part = this.GetChildPrim(localID);
            if (part != null)
            {
                part.GetInventory(remoteClient, localID);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="type"></param>
        /// <param name="inUse"></param>
        /// <param name="data"></param>
        public void UpdateExtraParam(uint localID, ushort type, bool inUse, byte[] data)
        {
            SceneObjectPart part = this.GetChildPrim(localID);
            if (part != null)
            {
                part.UpdateExtraParam(type, inUse, data);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="textureEntry"></param>
        public void UpdateTextureEntry(uint localID, byte[] textureEntry)
        {
            SceneObjectPart part = this.GetChildPrim(localID);
            if (part != null)
            {
                part.UpdateTextureEntry(textureEntry);
            }
        }

        #region Shape
        /// <summary>
        /// 
        /// </summary>
        /// <param name="shapeBlock"></param>
        public void UpdateShape(ObjectShapePacket.ObjectDataBlock shapeBlock, uint localID)
        {
            SceneObjectPart part = this.GetChildPrim(localID);
            if (part != null)
            {
                part.UpdateShape(shapeBlock);
            }
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scale"></param>
        /// <param name="localID"></param>
        public void Resize(LLVector3 scale, uint localID)
        {
            SceneObjectPart part = this.GetChildPrim(localID);
            if (part != null)
            {
                part.Resize(scale);
            }
        }

        #region Position
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        public void UpdateGroupPosition(LLVector3 pos)
        {
            this.Pos = pos;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="localID"></param>
        public void UpdateSinglePosition(LLVector3 pos, uint localID)
        {
            SceneObjectPart part = this.GetChildPrim(localID);
            if (part != null)
            {
                if (part.UUID == this.m_rootPart.UUID)
                {
                    this.UpdateRootPosition(pos);
                }
                else
                {
                    part.UpdateOffSet(pos);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
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

            foreach (SceneObjectPart obPart in this.m_parts.Values)
            {
                if (obPart.UUID != this.m_rootPart.UUID)
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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="rot"></param>
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
            this.Pos = pos;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rot"></param>
        /// <param name="localID"></param>
        public void UpdateSingleRotation(LLQuaternion rot, uint localID)
        {
            SceneObjectPart part = this.GetChildPrim(localID);
            if (part != null)
            {
                if (part.UUID == this.m_rootPart.UUID)
                {
                    this.UpdateRootRotation(rot);
                }
                else
                {
                    part.UpdateRotation(rot);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rot"></param>
        private void UpdateRootRotation(LLQuaternion rot)
        {
            this.m_rootPart.UpdateRotation(rot);
            Axiom.Math.Quaternion axRot = new Quaternion(rot.W, rot.X, rot.Y, rot.Z);
            Axiom.Math.Quaternion oldParentRot = new Quaternion(this.m_rootPart.RotationOffset.W, this.m_rootPart.RotationOffset.X, this.m_rootPart.RotationOffset.Y, this.m_rootPart.RotationOffset.Z);

            foreach (SceneObjectPart prim in this.m_parts.Values)
            {
                if (prim.UUID != this.m_rootPart.UUID)
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
        private void SetPartAsRoot(SceneObjectPart part)
        {
            this.m_rootPart = part;
            //this.m_uuid= part.UUID;
           // this.m_localId = part.LocalID;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="part"></param>
        private void SetPartAsNonRoot(SceneObjectPart part)
        {
            part.ParentID = this.m_rootPart.LocalID;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> RequestSceneAvatars()
        {
            return m_scene.RequestAvatarList();
        }

        public void SendFullUpdateToClient(IClientAPI remoteClient)
        {
            lock (this.m_parts)
            {
                foreach (SceneObjectPart part in this.m_parts.Values)
                {
                    this.SendPartFullUpdate(remoteClient, part);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="part"></param>
        internal void SendPartFullUpdate(IClientAPI remoteClient, SceneObjectPart part)
        {
            if( m_rootPart == part )
            {
                part.SendFullUpdateToClient( remoteClient, Pos );
            }
            else
            {
                part.SendFullUpdateToClient( remoteClient );
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="part"></param>
        internal void SendPartTerseUpdate(IClientAPI remoteClient, SceneObjectPart part)
        {
            if (m_rootPart == part)
            {
                part.SendTerseUpdateToClient(remoteClient, Pos);
            }
            else
            {
                part.SendTerseUpdateToClient(remoteClient);
            }
        }
    }
}
