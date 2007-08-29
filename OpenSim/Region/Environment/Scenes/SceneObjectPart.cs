using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Framework.Utilities;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{

    public class SceneObjectPart : IScriptHost
    {
        private const uint FULL_MASK_PERMISSIONS = 2147483647;

        private string m_inventoryFileName = "";
        private LLUUID m_folderID = LLUUID.Zero;

        [XmlIgnore]
        public PhysicsActor PhysActor = null;

        protected Dictionary<LLUUID, TaskInventoryItem> TaskInventory = new Dictionary<LLUUID, TaskInventoryItem>();

        public LLUUID CreatorID;
        public LLUUID OwnerID;
        public LLUUID GroupID;
        public LLUUID LastOwnerID;
        public Int32 CreationDate;
        public uint ParentID = 0;

        public uint OwnerMask = FULL_MASK_PERMISSIONS;
        public uint NextOwnerMask = FULL_MASK_PERMISSIONS;
        public uint GroupMask = FULL_MASK_PERMISSIONS;
        public uint EveryoneMask = FULL_MASK_PERMISSIONS;
        public uint BaseMask = FULL_MASK_PERMISSIONS;

        protected byte[] m_particleSystem = new byte[0];

        protected SceneObjectGroup m_parentGroup;

        /// <summary>
        /// Only used internally to schedule client updates
        /// </summary>
        private byte m_updateFlag;

        #region Properties

        /// <summary>
        /// Serial count for inventory file , used to tell if inventory has changed
        /// no need for this to be part of Database backup
        /// </summary>
        protected uint m_inventorySerial = 0;
        public uint InventorySerial
        {
            get { return m_inventorySerial; }
        }

        protected LLUUID m_uuid;
        public LLUUID UUID
        {
            get { return m_uuid; }
            set { m_uuid = value; }
        }

        protected uint m_localID;
        public uint LocalID
        {
            get { return m_localID; }
            set { m_localID = value; }
        }

        protected string m_name;
        public virtual string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }

        protected LLObject.ObjectFlags m_flags;
        public uint ObjectFlags
        {
            get { return (uint)m_flags; }
            set { m_flags = (LLObject.ObjectFlags)value; }
        }

        protected LLObject.MaterialType m_material;
        public byte Material
        {
            get { return (byte)m_material; }
            set { m_material = (LLObject.MaterialType)value; }
        }

        protected ulong m_regionHandle;
        public ulong RegionHandle
        {
            get { return m_regionHandle; }
            set { m_regionHandle = value; }
        }

        //unkown if this will be kept, added as a way of removing the group position from the group class
        protected LLVector3 m_groupPosition;
        public LLVector3 GroupPosition
        {
            get { return m_groupPosition; }
            set { m_groupPosition = value; }
        }

        protected LLVector3 m_offsetPosition;
        public LLVector3 OffsetPosition
        {
            get { return m_offsetPosition; }
            set { m_offsetPosition = value; }
        }

        public LLVector3 AbsolutePosition
        {
            get { return m_offsetPosition + m_groupPosition; }
        }

        protected LLQuaternion m_rotationOffset;
        public LLQuaternion RotationOffset
        {
            get { return m_rotationOffset; }
            set { m_rotationOffset = value; }
        }

        protected LLVector3 m_velocity;
        /// <summary></summary>
        public LLVector3 Velocity
        {
            get { return m_velocity; }
            set { m_velocity = value; }
        }

        protected LLVector3 m_angularVelocity;
        /// <summary></summary>
        public LLVector3 AngularVelocity
        {
            get { return m_angularVelocity; }
            set { m_angularVelocity = value; }
        }

        protected LLVector3 m_acceleration;
        /// <summary></summary>
        public LLVector3 Acceleration
        {
            get { return m_acceleration; }
            set { m_acceleration = value; }
        }

        private string m_description = "";
        public string Description
        {
            get { return this.m_description; }
            set { this.m_description = value; }
        }

        private string m_text = "";
        public string Text
        {
            get { return m_text; }
            set
            {
                m_text = value;
                ScheduleFullUpdate();
            }
        }

        private string m_sitName = "";
        public string SitName
        {
            get { return m_sitName; }
            set { m_sitName = value; }
        }

        private string m_touchName = "";
        public string TouchName
        {
            get { return m_touchName; }
            set { m_touchName = value; }
        }

        protected PrimitiveBaseShape m_shape;
        public PrimitiveBaseShape Shape
        {
            get { return this.m_shape; }
            set { m_shape = value; }
        }

        public LLVector3 Scale
        {
            set { this.m_shape.Scale = value; }
            get { return this.m_shape.Scale; }
        }
        #endregion

        public LLUUID ObjectOwner
        {
            get { return OwnerID; }
        }

        #region Constructors
        /// <summary>
        /// 
        /// </summary>
        public SceneObjectPart()
        {

        }

        /// <summary>
        /// Create a completely new SceneObjectPart (prim)
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="parent"></param>
        /// <param name="ownerID"></param>
        /// <param name="localID"></param>
        /// <param name="shape"></param>
        /// <param name="position"></param>
        public SceneObjectPart(ulong regionHandle, SceneObjectGroup parent, LLUUID ownerID, uint localID, PrimitiveBaseShape shape, LLVector3 groupPosition, LLVector3 offsetPosition)
        {
            this.m_name = "Primitive";
            this.m_regionHandle = regionHandle;
            this.m_parentGroup = parent;

            this.CreationDate = (Int32)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            this.OwnerID = ownerID;
            this.CreatorID = this.OwnerID;
            this.LastOwnerID = LLUUID.Zero;
            this.UUID = LLUUID.Random();
            this.LocalID = (uint)(localID);
            this.Shape = shape;

            this.GroupPosition = groupPosition;
            this.OffsetPosition = offsetPosition;
            this.RotationOffset = LLQuaternion.Identity;
            this.Velocity = new LLVector3(0, 0, 0);
            this.AngularVelocity = new LLVector3(0, 0, 0);
            this.Acceleration = new LLVector3(0, 0, 0);
            
            m_inventoryFileName = "taskinventory" + LLUUID.Random().ToString();
            m_folderID = LLUUID.Random();
            
            m_flags = 0;
            m_flags |= LLObject.ObjectFlags.Physics | 
                LLObject.ObjectFlags.ObjectModify |
                LLObject.ObjectFlags.ObjectCopy |
                LLObject.ObjectFlags.ObjectYouOwner |
                LLObject.ObjectFlags.Touch |
                LLObject.ObjectFlags.ObjectMove |
                LLObject.ObjectFlags.AllowInventoryDrop |
                LLObject.ObjectFlags.ObjectTransfer |
                LLObject.ObjectFlags.ObjectOwnerModify;
            
            ScheduleFullUpdate();
        }
        
        /// <summary>
        /// Re/create a SceneObjectPart (prim)
        /// currently not used, and maybe won't be
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="parent"></param>
        /// <param name="ownerID"></param>
        /// <param name="localID"></param>
        /// <param name="shape"></param>
        /// <param name="position"></param>
        public SceneObjectPart(ulong regionHandle, SceneObjectGroup parent, int creationDate, LLUUID ownerID, LLUUID creatorID, LLUUID lastOwnerID, uint localID, PrimitiveBaseShape shape, LLVector3 position, LLQuaternion rotation, uint flags)
        {
            this.m_regionHandle = regionHandle;
            this.m_parentGroup = parent;

            this.CreationDate = creationDate;
            this.OwnerID = ownerID;
            this.CreatorID = creatorID;
            this.LastOwnerID = lastOwnerID;
            this.UUID = LLUUID.Random();
            this.LocalID = (uint)(localID);
            this.Shape = shape;

            this.OffsetPosition = position;
            this.RotationOffset = rotation;
            this.ObjectFlags = flags;
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlreader"></param>
        /// <returns></returns>
        public static SceneObjectPart FromXml(XmlReader xmlReader)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(SceneObjectPart));
            return (SceneObjectPart)serializer.Deserialize(xmlReader);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlWriter"></param>
        public void ToXml(XmlWriter xmlWriter)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(SceneObjectPart));
            serializer.Serialize(xmlWriter, this);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetParent(SceneObjectGroup parent)
        {
            m_parentGroup = parent;

        }

        #region Copying
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public SceneObjectPart Copy(uint localID)
        {
            SceneObjectPart dupe = (SceneObjectPart)this.MemberwiseClone();
            dupe.m_shape = m_shape.Copy();
            dupe.m_regionHandle = m_regionHandle;
            dupe.UUID = LLUUID.Random();
            dupe.LocalID = localID;
            dupe.GroupPosition = new LLVector3(GroupPosition.X, GroupPosition.Y, GroupPosition.Z);
            dupe.OffsetPosition = new LLVector3(OffsetPosition.X, OffsetPosition.Y, OffsetPosition.Z);
            dupe.RotationOffset = new LLQuaternion(RotationOffset.X, RotationOffset.Y, RotationOffset.Z, RotationOffset.W);
            dupe.Velocity = new LLVector3(0, 0, 0);
            dupe.Acceleration = new LLVector3(0, 0, 0);
            dupe.AngularVelocity = new LLVector3(0, 0, 0);
            dupe.ObjectFlags = this.ObjectFlags;

            byte[] extraP = new byte[this.Shape.ExtraParams.Length];
            Array.Copy(this.Shape.ExtraParams, extraP, extraP.Length);
            dupe.Shape.ExtraParams = extraP;

            return dupe;
        }
        #endregion

        #region Update Scheduling
        /// <summary>
        /// 
        /// </summary>
        private void ClearUpdateSchedule()
        {
            m_updateFlag = 0;
        }

        /// <summary>
        /// 
        /// </summary>
        public void ScheduleFullUpdate()
        {
            m_parentGroup.HasChanged = true;
            m_updateFlag = 2;
        }

        /// <summary>
        /// 
        /// </summary>
        public void ScheduleTerseUpdate()
        {
            if (m_updateFlag < 1)
            {
                m_parentGroup.HasChanged = true;
                m_updateFlag = 1;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendScheduledUpdates()
        {
            if (m_updateFlag == 1) //some change has been made so update the clients
            {
                SendTerseUpdateToAllClients();
                ClearUpdateSchedule();
            }
            else
            {
                if (m_updateFlag == 2) // is a new prim, just created/reloaded or has major changes
                {
                    SendFullUpdateToAllClients();
                    ClearUpdateSchedule();
                }
            }
        }
        #endregion

        #region Shape
        /// <summary>
        /// 
        /// </summary>
        /// <param name="shapeBlock"></param>
        public void UpdateShape(ObjectShapePacket.ObjectDataBlock shapeBlock)
        {
            this.m_shape.PathBegin = shapeBlock.PathBegin;
            this.m_shape.PathEnd = shapeBlock.PathEnd;
            this.m_shape.PathScaleX = shapeBlock.PathScaleX;
            this.m_shape.PathScaleY = shapeBlock.PathScaleY;
            this.m_shape.PathShearX = shapeBlock.PathShearX;
            this.m_shape.PathShearY = shapeBlock.PathShearY;
            this.m_shape.PathSkew = shapeBlock.PathSkew;
            this.m_shape.ProfileBegin = shapeBlock.ProfileBegin;
            this.m_shape.ProfileEnd = shapeBlock.ProfileEnd;
            this.m_shape.PathCurve = shapeBlock.PathCurve;
            this.m_shape.ProfileCurve = shapeBlock.ProfileCurve;
            this.m_shape.ProfileHollow = shapeBlock.ProfileHollow;
            this.m_shape.PathRadiusOffset = shapeBlock.PathRadiusOffset;
            this.m_shape.PathRevolutions = shapeBlock.PathRevolutions;
            this.m_shape.PathTaperX = shapeBlock.PathTaperX;
            this.m_shape.PathTaperY = shapeBlock.PathTaperY;
            this.m_shape.PathTwist = shapeBlock.PathTwist;
            this.m_shape.PathTwistBegin = shapeBlock.PathTwistBegin;
            ScheduleFullUpdate();
        }
        #endregion

        #region Inventory
        public void AddInventoryItem(TaskInventoryItem item)
        {
            item.parent_id = m_folderID;
            item.creation_date = 1000;
            item.ParentPartID = this.UUID;
            this.TaskInventory.Add(item.item_id, item);
            this.m_inventorySerial++;
        }

        public int RemoveInventoryItem(IClientAPI remoteClient, uint localID, LLUUID itemID)
        {
            if (localID == this.LocalID)
            {
                if (this.TaskInventory.ContainsKey(itemID))
                {
                    string type = TaskInventory[itemID].inv_type;
                    this.TaskInventory.Remove(itemID);
                    this.m_inventorySerial++;
                    if (type == "lsltext")
                    {
                        return 10;
                    }
                    else
                    {
                        return 0;
                    }
                }
            }
            return -1;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        /// <param name="localID"></param>
        public bool GetInventoryFileName(IClientAPI client, uint localID)
        {
            if (localID == this.m_localID)
            {
                if (this.m_inventorySerial > 0)
                {
                    client.SendTaskInventory(this.m_uuid, (short)m_inventorySerial, Helpers.StringToField(m_inventoryFileName));
                    return true;
                }
                else
                {
                    client.SendTaskInventory(this.m_uuid, 0, new byte[0]);
                    return false;
                }
            }
            return false;
        }

        public string RequestInventoryFile(ModuleAPIMethod2<bool, string, byte[]> addXferFile)
        {
            byte[] fileData = new byte[0];
            InventoryStringBuilder invString = new InventoryStringBuilder(m_folderID, this.UUID);
            foreach (TaskInventoryItem item in this.TaskInventory.Values)
            {
                invString.AddItemStart();
                invString.AddNameValueLine("item_id", item.item_id.ToStringHyphenated());
                invString.AddNameValueLine("parent_id", item.parent_id.ToStringHyphenated());

                invString.AddPermissionsStart();
                invString.AddNameValueLine("base_mask", "0x7FFFFFFF");
                invString.AddNameValueLine("owner_mask", "0x7FFFFFFF");
                invString.AddNameValueLine("group_mask", "0x7FFFFFFF");
                invString.AddNameValueLine("everyone_mask", "0x7FFFFFFF");
                invString.AddNameValueLine("next_owner_mask", "0x7FFFFFFF");
                invString.AddNameValueLine("creator_id", item.creator_id.ToStringHyphenated());
                invString.AddNameValueLine("owner_id", item.owner_id.ToStringHyphenated());
                invString.AddNameValueLine("last_owner_id", item.last_owner_id.ToStringHyphenated());
                invString.AddNameValueLine("group_id", item.group_id.ToStringHyphenated());
                invString.AddSectionEnd();

                invString.AddNameValueLine("asset_id", item.asset_id.ToStringHyphenated());
                invString.AddNameValueLine("type", item.type);
                invString.AddNameValueLine("inv_type", item.inv_type);
                invString.AddNameValueLine("flags", "0x00");
                invString.AddNameValueLine("name", item.name + "|");
                invString.AddNameValueLine("desc", item.desc + "|");
                invString.AddNameValueLine("creation_date", item.creation_date.ToString());
                invString.AddSectionEnd();
            }
            fileData = Helpers.StringToField(invString.BuildString);
            if (fileData.Length > 2)
            {
                addXferFile(m_inventoryFileName, fileData);
            }
            return "";
        }
        #endregion

        #region ExtraParams
        public void UpdateExtraParam(ushort type, bool inUse, byte[] data)
        {
            this.m_shape.ExtraParams = new byte[data.Length + 7];
            int i = 0;
            uint length = (uint)data.Length;
            this.m_shape.ExtraParams[i++] = 1;
            this.m_shape.ExtraParams[i++] = (byte)(type % 256);
            this.m_shape.ExtraParams[i++] = (byte)((type >> 8) % 256);

            this.m_shape.ExtraParams[i++] = (byte)(length % 256);
            this.m_shape.ExtraParams[i++] = (byte)((length >> 8) % 256);
            this.m_shape.ExtraParams[i++] = (byte)((length >> 16) % 256);
            this.m_shape.ExtraParams[i++] = (byte)((length >> 24) % 256);
            Array.Copy(data, 0, this.m_shape.ExtraParams, i, data.Length);

            this.ScheduleFullUpdate();

        }
        #endregion

        #region Texture
        /// <summary>
        /// 
        /// </summary>
        /// <param name="textureEntry"></param>
        public void UpdateTextureEntry(byte[] textureEntry)
        {
            this.m_shape.TextureEntry = textureEntry;
            ScheduleFullUpdate();
        }
        #endregion

        #region ParticleSystem
        public void AddNewParticleSystem(libsecondlife.Primitive.ParticleSystem pSystem)
        {
            this.m_particleSystem = pSystem.GetBytes();
        }
        #endregion

        #region Position
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        public void UpdateOffSet(LLVector3 pos)
        {
            LLVector3 newPos = new LLVector3(pos.X, pos.Y, pos.Z);
            this.OffsetPosition = newPos;
            ScheduleTerseUpdate();
        }
        #endregion

        #region rotation
        public void UpdateRotation(LLQuaternion rot)
        {
            this.RotationOffset = new LLQuaternion(rot.X, rot.Y, rot.Z, rot.W);
            ScheduleTerseUpdate();
        }
        #endregion

        #region Resizing/Scale
        /// <summary>
        /// 
        /// </summary>
        /// <param name="scale"></param>
        public void Resize(LLVector3 scale)
        {
            this.m_shape.Scale = scale;
            ScheduleFullUpdate();
        }
        #endregion

        #region Client Update Methods
        /// <summary>
        /// 
        /// </summary>
        public void SendFullUpdateToAllClients()
        {
            List<ScenePresence> avatars = this.m_parentGroup.RequestSceneAvatars();
            for (int i = 0; i < avatars.Count; i++)
            {
                m_parentGroup.SendPartFullUpdate(avatars[i].ControllingClient, this);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        public void SendFullUpdate(IClientAPI remoteClient)
        {
            m_parentGroup.SendPartFullUpdate(remoteClient, this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        public void SendFullUpdateToClient(IClientAPI remoteClient)
        {
            LLVector3 lPos;
            lPos = OffsetPosition;
            SendFullUpdateToClient(remoteClient, lPos);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="lPos"></param>
        public void SendFullUpdateToClient(IClientAPI remoteClient, LLVector3 lPos)
        {
            LLQuaternion lRot;
            lRot = RotationOffset;

            remoteClient.SendPrimitiveToClient(m_regionHandle, 64096, LocalID, m_shape, lPos, this.ObjectFlags, m_uuid, OwnerID,
                                               m_text, ParentID, this.m_particleSystem, lRot);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendTerseUpdateToAllClients()
        {
            List<ScenePresence> avatars = this.m_parentGroup.RequestSceneAvatars();
            for (int i = 0; i < avatars.Count; i++)
            {
                m_parentGroup.SendPartTerseUpdate(avatars[i].ControllingClient, this);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        public void SendTerseUpdate(IClientAPI remoteClient)
        {
            m_parentGroup.SendPartTerseUpdate(remoteClient, this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="RemoteClient"></param>
        public void SendTerseUpdateToClient(IClientAPI remoteClient)
        {
            LLVector3 lPos;
            lPos = this.OffsetPosition;
            LLQuaternion mRot = this.RotationOffset;
            remoteClient.SendPrimTerseUpdate(m_regionHandle, 64096, LocalID, lPos, mRot);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="lPos"></param>
        public void SendTerseUpdateToClient(IClientAPI remoteClient, LLVector3 lPos)
        {
            LLQuaternion mRot = this.RotationOffset;
            remoteClient.SendPrimTerseUpdate(m_regionHandle, 64096, LocalID, lPos, mRot);
        }
        #endregion

        public virtual void UpdateMovement()
        {
        }

        public virtual void OnGrab(LLVector3 offsetPos, IClientAPI remoteClient)
        {
        }

        public void SetText(string text, Vector3 color, double alpha)
        {
            Text = text;
        }

        public class InventoryStringBuilder
        {
            public string BuildString = "";

            public InventoryStringBuilder(LLUUID folderID, LLUUID parentID)
            {
                BuildString += "\tinv_object\t0\n\t{\n";
                this.AddNameValueLine("obj_id", folderID.ToStringHyphenated());
                this.AddNameValueLine("parent_id", parentID.ToStringHyphenated());
                this.AddNameValueLine("type", "category");
                this.AddNameValueLine("name", "Contents");
                this.AddSectionEnd();
            }

            public void AddItemStart()
            {
                BuildString += "\tinv_item\t0\n";
                BuildString += "\t{\n";
            }

            public void AddPermissionsStart()
            {
                BuildString += "\tpermissions 0\n";
                BuildString += "\t{\n";
            }

            public void AddSectionEnd()
            {
                BuildString += "\t}\n";
            }

            public void AddLine(string addLine)
            {
                BuildString += addLine;
            }

            public void AddNameValueLine(string name, string value)
            {
                BuildString += "\t\t";
                BuildString += name + "\t";
                BuildString += value + "\n";
            }

            public void Close()
            {
            }
        }

        public class TaskInventoryItem
        {
            public static string[] Types = new string[]
                {
                    "texture",
                    "sound",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "", 
                    "",
                    "lsltext",
                    ""
                };

            public LLUUID item_id = LLUUID.Zero;
            public LLUUID parent_id = LLUUID.Zero; //parent folder id 

            public uint base_mask = FULL_MASK_PERMISSIONS;
            public uint owner_mask = FULL_MASK_PERMISSIONS;
            public uint group_mask = FULL_MASK_PERMISSIONS;
            public uint everyone_mask = FULL_MASK_PERMISSIONS;
            public uint next_owner_mask = FULL_MASK_PERMISSIONS;
            public LLUUID creator_id = LLUUID.Zero;
            public LLUUID owner_id = LLUUID.Zero;
            public LLUUID last_owner_id = LLUUID.Zero;
            public LLUUID group_id = LLUUID.Zero;

            public LLUUID asset_id = LLUUID.Zero;
            public string type = "";
            public string inv_type = "";
            public uint flags = 0;
            public string name = "";
            public string desc = "";
            public uint creation_date = 0;

            public LLUUID ParentPartID = LLUUID.Zero;

            public TaskInventoryItem()
            {
            }
        }
    }
}

