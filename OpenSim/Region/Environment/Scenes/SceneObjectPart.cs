/*
* Copyright (c) Contributors, http://opensimulator.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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
using System.Xml;
using System.Xml.Serialization;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Region.Physics.Manager;
using System.Drawing;

namespace OpenSim.Region.Environment.Scenes
{
    public class SceneObjectPart : IScriptHost
    {
        private const uint FULL_MASK_PERMISSIONS = 2147483647;

        private string m_inventoryFileName = "";
        private LLUUID m_folderID = LLUUID.Zero;

        [XmlIgnore] public PhysicsActor PhysActor = null;

        protected Dictionary<LLUUID, TaskInventoryItem> TaskInventory = new Dictionary<LLUUID, TaskInventoryItem>();
        public LLUUID LastOwnerID;
        public LLUUID OwnerID;
        public LLUUID GroupID;
        public int OwnershipCost;
        public byte ObjectSaleType;
        public int SalePrice;
        public uint Category;

        public Int32 CreationDate;
        public uint ParentID = 0;


        public uint OwnerMask = FULL_MASK_PERMISSIONS;
        public uint NextOwnerMask = FULL_MASK_PERMISSIONS;
        public uint GroupMask = FULL_MASK_PERMISSIONS;
        public uint EveryoneMask = FULL_MASK_PERMISSIONS;
        public uint BaseMask = FULL_MASK_PERMISSIONS;

        protected byte[] m_particleSystem = new byte[0];

        [XmlIgnore] public uint TimeStampFull = 0;
        [XmlIgnore] public uint TimeStampTerse = 0;
        [XmlIgnore] public uint TimeStampLastActivity = 0; // Will be used for AutoReturn

        protected SceneObjectGroup m_parentGroup;

        /// <summary>
        /// Only used internally to schedule client updates
        /// </summary>
        private byte m_updateFlag;

        #region Properties

        public LLUUID CreatorID;

        public LLUUID ObjectCreator
        {
            get { return CreatorID; }
        }

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

        protected LLObject.ObjectFlags m_flags = 0;

        public uint ObjectFlags
        {
            get { return (uint) m_flags;}
            set {m_flags = (LLObject.ObjectFlags) value;}
        }

        protected LLObject.MaterialType m_material = 0;

        public byte Material
        {
            get { return (byte) m_material; }
            set { m_material = (LLObject.MaterialType) value; }
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
            get 
            {
                if (PhysActor != null)
                {
                    m_groupPosition.X = PhysActor.Position.X;
                    m_groupPosition.Y = PhysActor.Position.Y;
                    m_groupPosition.Z = PhysActor.Position.Z;
                }
                return m_groupPosition; 
            }
            set 
            {
                if (PhysActor != null)
                {
                    try
                    {
                        //lock (m_scene.SyncRoot)
                        //{
                        PhysActor.Position = new PhysicsVector(value.X, value.Y, value.Z);
                        //}
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
                m_groupPosition = value; 
            }
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
            get 
            {
                if (PhysActor != null)
                {
                    if(PhysActor.Orientation.x != 0 || PhysActor.Orientation.y != 0 
                    || PhysActor.Orientation.z != 0 || PhysActor.Orientation.w != 0)
                    {
                        m_rotationOffset.X = PhysActor.Orientation.x;
                        m_rotationOffset.Y = PhysActor.Orientation.y;
                        m_rotationOffset.Z = PhysActor.Orientation.z;
                        m_rotationOffset.W = PhysActor.Orientation.w;
                    }
                }
                return m_rotationOffset; 
            }
            set 
            {
                if (PhysActor != null)
                {
                    try
                    {
                        //lock (m_scene.SyncRoot)
                        //{
                        PhysActor.Orientation = new Quaternion(value.W, value.X, value.Y, value.Z);
                        //}
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                m_rotationOffset = value; 
            }
        }

        protected LLVector3 m_velocity;
        protected LLVector3 m_rotationalvelocity;

        /// <summary></summary>
        public LLVector3 Velocity
        {
            get {
                //if (PhysActor.Velocity.x != 0 || PhysActor.Velocity.y != 0
                //|| PhysActor.Velocity.z != 0)
                //{
                if (PhysActor != null)
                {
                    if (PhysActor.IsPhysical)
                    {
                        m_velocity.X = PhysActor.Velocity.X;
                        m_velocity.Y = PhysActor.Velocity.Y;
                        m_velocity.Z = PhysActor.Velocity.Z;
                    }
                }
                
                return m_velocity; 
            }
            set { m_velocity = value; }
        }
        public LLVector3 RotationalVelocity
        {
            get
            {
                //if (PhysActor.Velocity.x != 0 || PhysActor.Velocity.y != 0
                //|| PhysActor.Velocity.z != 0)
                //{
                if (PhysActor != null)
                {
                    if (PhysActor.IsPhysical)
                    {
                        m_rotationalvelocity.X = PhysActor.RotationalVelocity.X;
                        m_rotationalvelocity.Y = PhysActor.RotationalVelocity.Y;
                        m_rotationalvelocity.Z = PhysActor.RotationalVelocity.Z;
                    }
                }

                return m_rotationalvelocity;
            }
            set { m_rotationalvelocity = value; }
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
            get { return m_description; }
            set { m_description = value; }
        }

        private Color m_color = Color.Black;

        public Color Color 
        {
            get { return m_color; }
            set 
            {
                m_color = value;
                /* ScheduleFullUpdate() need not be called b/c after
                 * setting the color, the text will be set, so then
                 * ScheduleFullUpdate() will be called. */
                //ScheduleFullUpdate();
            }
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
            get { return m_shape; }
            set { m_shape = value; }
        }

        public LLVector3 Scale
        {
            set { m_shape.Scale = value; }
            get { return m_shape.Scale; }
        }

        #endregion

        public LLUUID ObjectOwner
        {
            get { return OwnerID; }
        }

        public SceneObjectGroup ParentGroup
        {
            get { return m_parentGroup; }
        }

        public byte UpdateFlag
        {
            get { return m_updateFlag; }
            set { m_updateFlag = value; }
        }

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public SceneObjectPart()
        {
        }

        public SceneObjectPart(ulong regionHandle, SceneObjectGroup parent, LLUUID ownerID, uint localID,
                               PrimitiveBaseShape shape, LLVector3 groupPosition, LLVector3 offsetPosition)
            : this(regionHandle, parent, ownerID, localID, shape, groupPosition, LLQuaternion.Identity, offsetPosition)
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
        public SceneObjectPart(ulong regionHandle, SceneObjectGroup parent, LLUUID ownerID, uint localID,
                               PrimitiveBaseShape shape, LLVector3 groupPosition, LLQuaternion rotationOffset,
                               LLVector3 offsetPosition)
        {
            m_name = "Primitive";
            m_regionHandle = regionHandle;
            m_parentGroup = parent;
            
            CreationDate = (Int32) (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            OwnerID = ownerID;
            CreatorID = OwnerID;
            LastOwnerID = LLUUID.Zero;
            UUID = LLUUID.Random();
            LocalID = (uint) (localID);
            Shape = shape;
            // Todo: Add More Object Parameter from above!
            OwnershipCost = 0;
            ObjectSaleType = (byte)0;
            SalePrice = 0;
            Category = (uint)0;
            LastOwnerID = CreatorID;
            // End Todo: ///
            GroupPosition = groupPosition;
            OffsetPosition = offsetPosition;
            RotationOffset = rotationOffset;
            Velocity = new LLVector3(0, 0, 0);
            m_rotationalvelocity = new LLVector3(0, 0, 0);
            AngularVelocity = new LLVector3(0, 0, 0);
            Acceleration = new LLVector3(0, 0, 0);

            m_inventoryFileName = "taskinventory" + LLUUID.Random().ToString();
            m_folderID = LLUUID.Random();

            m_flags = 0;
            m_flags |= LLObject.ObjectFlags.ObjectModify |
                       LLObject.ObjectFlags.ObjectCopy |
                       LLObject.ObjectFlags.ObjectYouOwner |
                       LLObject.ObjectFlags.Touch |
                       LLObject.ObjectFlags.ObjectMove |
                       LLObject.ObjectFlags.AllowInventoryDrop |
                       LLObject.ObjectFlags.ObjectTransfer |
                       LLObject.ObjectFlags.CreateSelected |
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
        public SceneObjectPart(ulong regionHandle, SceneObjectGroup parent, int creationDate, LLUUID ownerID,
                               LLUUID creatorID, LLUUID lastOwnerID, uint localID, PrimitiveBaseShape shape,
                               LLVector3 position, LLQuaternion rotation, uint flags)
        {
            m_regionHandle = regionHandle;
            m_parentGroup = parent;
            TimeStampTerse = (uint)Util.UnixTimeSinceEpoch();
            CreationDate = creationDate;
            OwnerID = ownerID;
            CreatorID = creatorID;
            LastOwnerID = lastOwnerID;
            UUID = LLUUID.Random();
            LocalID = (uint) (localID);
            // Todo:  Add More parameters from above
            Shape = shape;
            OwnershipCost = 0;
            ObjectSaleType = (byte)0;
            SalePrice = 0;
            Category = (uint)0;
            // End Todo:  ///
            LastOwnerID = CreatorID;
            OffsetPosition = position;
            RotationOffset = rotation;
            ObjectFlags = flags;
            bool UsePhysics = ((ObjectFlags & (uint)LLObject.ObjectFlags.Physics) != 0);
            doPhysicsPropertyUpdate(UsePhysics);
            ScheduleFullUpdate();
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlreader"></param>
        /// <returns></returns>
        public static SceneObjectPart FromXml(XmlReader xmlReader)
        {
            XmlSerializer serializer = new XmlSerializer(typeof (SceneObjectPart));
            SceneObjectPart newobject = (SceneObjectPart) serializer.Deserialize(xmlReader);
            bool UsePhysics = ((newobject.ObjectFlags & (uint)LLObject.ObjectFlags.Physics) != 0);
            newobject.doPhysicsPropertyUpdate(UsePhysics);
            
            return newobject;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xmlWriter"></param>
        public void ToXml(XmlWriter xmlWriter)
        {
            XmlSerializer serializer = new XmlSerializer(typeof (SceneObjectPart));
            serializer.Serialize(xmlWriter, this);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetParent(SceneObjectGroup parent)
        {
            m_parentGroup = parent;
        }

        public LLUUID GetRootPartUUID()
        {
            if (m_parentGroup != null)
            {
                return m_parentGroup.UUID;
            }
            return LLUUID.Zero;
        }

        #region Copying

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public SceneObjectPart Copy(uint localID, LLUUID AgentID, LLUUID GroupID)
        {
            SceneObjectPart dupe = (SceneObjectPart) MemberwiseClone();
            dupe.m_shape = m_shape.Copy();
            dupe.m_regionHandle = m_regionHandle;
            dupe.UUID = LLUUID.Random();
            dupe.LocalID = localID;
            dupe.OwnerID = AgentID;
            dupe.GroupID = GroupID;
            dupe.GroupPosition = new LLVector3(GroupPosition.X, GroupPosition.Y, GroupPosition.Z);
            dupe.OffsetPosition = new LLVector3(OffsetPosition.X, OffsetPosition.Y, OffsetPosition.Z);
            dupe.RotationOffset =
                new LLQuaternion(RotationOffset.X, RotationOffset.Y, RotationOffset.Z, RotationOffset.W);
            dupe.Velocity = new LLVector3(0, 0, 0);
            dupe.Acceleration = new LLVector3(0, 0, 0);
            dupe.AngularVelocity = new LLVector3(0, 0, 0);
            dupe.ObjectFlags = ObjectFlags;

            dupe.OwnershipCost = OwnershipCost;
            dupe.ObjectSaleType = ObjectSaleType;
            dupe.SalePrice = SalePrice;
            dupe.Category = Category;

            // This may be wrong...    it might have to be applied in SceneObjectGroup to the object that's being duplicated.
            dupe.LastOwnerID = ObjectOwner;
            
            byte[] extraP = new byte[Shape.ExtraParams.Length];
            Array.Copy(Shape.ExtraParams, extraP, extraP.Length);
            dupe.Shape.ExtraParams = extraP;
            bool UsePhysics = ((dupe.ObjectFlags & (uint)LLObject.ObjectFlags.Physics) != 0);
            dupe.doPhysicsPropertyUpdate(UsePhysics);

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
            if (m_parentGroup != null)
            {
                m_parentGroup.HasChanged = true;
            }
            TimeStampFull = (uint) Util.UnixTimeSinceEpoch();
            m_updateFlag = 2;
        }

        public void AddFlag(LLObject.ObjectFlags flag)
        {
            LLObject.ObjectFlags prevflag = m_flags;
            //uint objflags = m_flags;
            if ((ObjectFlags & (uint) flag) == 0)
            {
                //Console.WriteLine("Adding flag: " + ((LLObject.ObjectFlags) flag).ToString());
                m_flags |= flag;
            }
            uint currflag = (uint) m_flags;
            //System.Console.WriteLine("Aprev: " + prevflag.ToString() + " curr: " + m_flags.ToString());
            //ScheduleFullUpdate();
        }

        public void RemFlag(LLObject.ObjectFlags flag)
        {
            LLObject.ObjectFlags prevflag = m_flags;
            if ((ObjectFlags & (uint) flag) != 0)
            {
                //Console.WriteLine("Removing flag: " + ((LLObject.ObjectFlags)flag).ToString());
                m_flags &= ~flag;
            }
            //System.Console.WriteLine("prev: " + prevflag.ToString() + " curr: " + m_flags.ToString());
            //ScheduleFullUpdate();
        }

        /// <summary>
        /// 
        /// </summary>
        public void ScheduleTerseUpdate()
        {
            if (m_updateFlag < 1)
            {
                if (m_parentGroup != null)
                {
                    m_parentGroup.HasChanged = true;
                }
                TimeStampTerse = (uint) Util.UnixTimeSinceEpoch();
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
                AddTerseUpdateToAllAvatars();
                ClearUpdateSchedule();

                // This causes the Scene to 'poll' physical objects every couple of frames
                // bad, so it's been replaced by an event driven method.
                //if ((ObjectFlags & (uint)LLObject.ObjectFlags.Physics) != 0)
                //{
                    // Only send the constant terse updates on physical objects!   
                    //ScheduleTerseUpdate();
                //}
            }
            else
            {
                if (m_updateFlag == 2) // is a new prim, just created/reloaded or has major changes
                {
                    AddFullUpdateToAllAvatars();
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
            m_shape.PathBegin = shapeBlock.PathBegin;
            m_shape.PathEnd = shapeBlock.PathEnd;
            m_shape.PathScaleX = shapeBlock.PathScaleX;
            m_shape.PathScaleY = shapeBlock.PathScaleY;
            m_shape.PathShearX = shapeBlock.PathShearX;
            m_shape.PathShearY = shapeBlock.PathShearY;
            m_shape.PathSkew = shapeBlock.PathSkew;
            m_shape.ProfileBegin = shapeBlock.ProfileBegin;
            m_shape.ProfileEnd = shapeBlock.ProfileEnd;
            m_shape.PathCurve = shapeBlock.PathCurve;
            m_shape.ProfileCurve = shapeBlock.ProfileCurve;
            m_shape.ProfileHollow = shapeBlock.ProfileHollow;
            m_shape.PathRadiusOffset = shapeBlock.PathRadiusOffset;
            m_shape.PathRevolutions = shapeBlock.PathRevolutions;
            m_shape.PathTaperX = shapeBlock.PathTaperX;
            m_shape.PathTaperY = shapeBlock.PathTaperY;
            m_shape.PathTwist = shapeBlock.PathTwist;
            m_shape.PathTwistBegin = shapeBlock.PathTwistBegin;
            ScheduleFullUpdate();
        }

        #endregion

        #region Inventory

        public void AddInventoryItem(TaskInventoryItem item)
        {
            item.parent_id = m_folderID;
            item.creation_date = 1000;
            item.ParentPartID = UUID;
            TaskInventory.Add(item.item_id, item);
            m_inventorySerial++;
        }

        public int RemoveInventoryItem(IClientAPI remoteClient, uint localID, LLUUID itemID)
        {
            if (localID == LocalID)
            {
                if (TaskInventory.ContainsKey(itemID))
                {
                    string type = TaskInventory[itemID].inv_type;
                    TaskInventory.Remove(itemID);
                    m_inventorySerial++;
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
            if (localID == m_localID)
            {
                if (m_inventorySerial > 0)
                {
                    client.SendTaskInventory(m_uuid, (short) m_inventorySerial,
                                             Helpers.StringToField(m_inventoryFileName));
                    return true;
                }
                else
                {
                    client.SendTaskInventory(m_uuid, 0, new byte[0]);
                    return false;
                }
            }
            return false;
        }

        public string RequestInventoryFile(IXfer xferManager)
        {
            byte[] fileData = new byte[0];
            InventoryStringBuilder invString = new InventoryStringBuilder(m_folderID, UUID);
            foreach (TaskInventoryItem item in TaskInventory.Values)
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
                xferManager.AddNewFile(m_inventoryFileName, fileData);
            }
            return "";
        }

        #endregion

        #region ExtraParams

        public void UpdatePrimFlags(ushort type, bool inUse, byte[] data)
        {
            bool hasPrim = false;
            bool UsePhysics = false;
            bool IsTemporary = false;
            bool IsPhantom = false;
            bool CastsShadows = false;
            bool wasUsingPhysics = ((ObjectFlags & (uint)LLObject.ObjectFlags.Physics) != 0);
            //bool IsLocked = false;
            int i = 0;


            try
            {
                i += 46;
                //IsLocked = (data[i++] != 0) ? true : false;
                UsePhysics = ((data[i++] != 0) && m_parentGroup.m_scene.m_physicalPrim) ? true : false;
                //System.Console.WriteLine("U" + packet.ToBytes().Length.ToString());
                IsTemporary = (data[i++] != 0) ? true : false;
                IsPhantom = (data[i++] != 0) ? true : false;
                CastsShadows = (data[i++] != 0) ? true : false;
            }
            catch (Exception e)
            {
                Console.WriteLine("Ignoring invalid Packet:");
                //Silently ignore it - TODO: FIXME Quick
            }

            if (UsePhysics )
            {
                AddFlag(LLObject.ObjectFlags.Physics);
                if (!wasUsingPhysics)
                {
                    doPhysicsPropertyUpdate(UsePhysics);
                }

            }
            else
            {
                RemFlag(LLObject.ObjectFlags.Physics);
                if (wasUsingPhysics)
                {
                    doPhysicsPropertyUpdate(UsePhysics);
                }
            }
                

                
            

            if (IsPhantom)
            {
                AddFlag(LLObject.ObjectFlags.Phantom);
                if (PhysActor != null)
                {
                    m_parentGroup.m_scene.PhysScene.RemovePrim(PhysActor);
                        /// that's not wholesome.  Had to make m_scene public
                    PhysActor = null;
                }
            }
            else
            {
                RemFlag(LLObject.ObjectFlags.Phantom);
                if (PhysActor == null)
                {
                    PhysActor = m_parentGroup.m_scene.PhysScene.AddPrimShape(
                        Name,
                        Shape,
                        new PhysicsVector(AbsolutePosition.X, AbsolutePosition.Y,
                                          AbsolutePosition.Z),
                        new PhysicsVector(Scale.X, Scale.Y, Scale.Z),
                        new Quaternion(RotationOffset.W, RotationOffset.X,
                                       RotationOffset.Y, RotationOffset.Z), UsePhysics);
                }
                else
                {
                    PhysActor.IsPhysical = UsePhysics;
                }
            }

            if (IsTemporary)
            {
                AddFlag(LLObject.ObjectFlags.TemporaryOnRez);
            }
            else
            {
                RemFlag(LLObject.ObjectFlags.TemporaryOnRez);
            }
//            System.Console.WriteLine("Update:  PHY:" + UsePhysics.ToString() + ", T:" + IsTemporary.ToString() + ", PHA:" + IsPhantom.ToString() + " S:" + CastsShadows.ToString());
            ScheduleFullUpdate();
        }
        public void doPhysicsPropertyUpdate(bool UsePhysics)
        {
           
            if (PhysActor != null)
            {
                if (PhysActor.IsPhysical)
                {
                    PhysActor.OnRequestTerseUpdate -= PhysicsRequestingTerseUpdate;
                    PhysActor.OnOutOfBounds -= PhysicsOutOfBounds;
                }
                m_parentGroup.m_scene.PhysScene.RemovePrim(PhysActor);
                /// that's not wholesome.  Had to make m_scene public
                PhysActor = null;

                if (!((ObjectFlags & (uint)LLObject.ObjectFlags.Phantom) != 0))
                {
                    PhysActor = m_parentGroup.m_scene.PhysScene.AddPrimShape(
                    Name,
                    Shape,
                    new PhysicsVector(AbsolutePosition.X, AbsolutePosition.Y,
                                      AbsolutePosition.Z),
                    new PhysicsVector(Scale.X, Scale.Y, Scale.Z),
                    new Quaternion(RotationOffset.W, RotationOffset.X,
                                   RotationOffset.Y, RotationOffset.Z), UsePhysics);
                    if (UsePhysics)
                    {
                        PhysActor.OnRequestTerseUpdate += PhysicsRequestingTerseUpdate;
                        PhysActor.OnOutOfBounds += PhysicsOutOfBounds;
                    }
                }

            
               
            }



            

        }

        public void UpdateExtraParam(ushort type, bool inUse, byte[] data)
        {
            m_shape.ExtraParams = new byte[data.Length + 7];
            int i = 0;
            uint length = (uint) data.Length;
            m_shape.ExtraParams[i++] = 1;
            m_shape.ExtraParams[i++] = (byte) (type%256);
            m_shape.ExtraParams[i++] = (byte) ((type >> 8)%256);

            m_shape.ExtraParams[i++] = (byte) (length%256);
            m_shape.ExtraParams[i++] = (byte) ((length >> 8)%256);
            m_shape.ExtraParams[i++] = (byte) ((length >> 16)%256);
            m_shape.ExtraParams[i++] = (byte) ((length >> 24)%256);
            Array.Copy(data, 0, m_shape.ExtraParams, i, data.Length);

            ScheduleFullUpdate();
        }

        #endregion

        #region Texture

        /// <summary>
        /// 
        /// </summary>
        /// <param name="textureEntry"></param>
        public void UpdateTextureEntry(byte[] textureEntry)
        {
            m_shape.TextureEntry = textureEntry;
            ScheduleFullUpdate();
        }

        #endregion

        #region ParticleSystem

        public void AddNewParticleSystem(Primitive.ParticleSystem pSystem)
        {
            m_particleSystem = pSystem.GetBytes();
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
            OffsetPosition = newPos;
            ScheduleTerseUpdate();
        }

        public void UpdateGroupPosition(LLVector3 pos)
        {
            LLVector3 newPos = new LLVector3(pos.X, pos.Y, pos.Z);
            GroupPosition = newPos;
            ScheduleTerseUpdate();
        }

        #endregion

        #region rotation

        public void UpdateRotation(LLQuaternion rot)
        {
            RotationOffset = new LLQuaternion(rot.X, rot.Y, rot.Z, rot.W);
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
            m_shape.Scale = scale;
            ScheduleFullUpdate();
        }

        #endregion

        #region Client Update Methods

        public void AddFullUpdateToAllAvatars()
        {
            List<ScenePresence> avatars = m_parentGroup.GetScenePresences();
            for (int i = 0; i < avatars.Count; i++)
            {
                avatars[i].QueuePartForUpdate(this);
            }
        }

        public void AddFullUpdateToAvatar(ScenePresence presence)
        {
            presence.QueuePartForUpdate(this);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendFullUpdateToAllClients()
        {
            List<ScenePresence> avatars = m_parentGroup.GetScenePresences();
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
            uint clientFlags = ObjectFlags & ~(uint) LLObject.ObjectFlags.CreateSelected;

            List<ScenePresence> avatars = m_parentGroup.GetScenePresences();
            foreach (ScenePresence s in avatars)
            {
                if (s.m_uuid == OwnerID)
                {
                    if (s.ControllingClient == remoteClient)
                    {
                        clientFlags = ObjectFlags;
                        m_flags &= ~LLObject.ObjectFlags.CreateSelected;
                    }
                    break;
                }
            }

            byte[] color = new byte[] { m_color.R, m_color.G, m_color.B, m_color.A };
            remoteClient.SendPrimitiveToClient(m_regionHandle, 64096, LocalID, m_shape, lPos, clientFlags, m_uuid,
                                               OwnerID,
                                               m_text, color, ParentID, m_particleSystem, lRot);
        }

        /// Terse updates
        public void AddTerseUpdateToAllAvatars()
        {
            List<ScenePresence> avatars = m_parentGroup.GetScenePresences();
            for (int i = 0; i < avatars.Count; i++)
            {
                avatars[i].QueuePartForUpdate(this);
            }
        }

        public void AddTerseUpdateToAvatar(ScenePresence presence)
        {
            presence.QueuePartForUpdate(this);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendTerseUpdateToAllClients()
        {
            List<ScenePresence> avatars = m_parentGroup.GetScenePresences();
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

        public void SendTerseUpdateToClient(IClientAPI remoteClient)
        {
            LLVector3 lPos;
            lPos = OffsetPosition;
            LLQuaternion mRot = RotationOffset;
            if ((ObjectFlags & (uint)LLObject.ObjectFlags.Physics) == 0)
            {
                remoteClient.SendPrimTerseUpdate(m_regionHandle, 64096, LocalID, lPos, mRot);
            }
            else
            {
                remoteClient.SendPrimTerseUpdate(m_regionHandle, 64096, LocalID, lPos, mRot, Velocity, RotationalVelocity);
            }
        }

        public void SendTerseUpdateToClient(IClientAPI remoteClient, LLVector3 lPos)
        {
            LLQuaternion mRot = RotationOffset;
            if ((ObjectFlags & (uint)LLObject.ObjectFlags.Physics) == 0)
            {
                remoteClient.SendPrimTerseUpdate(m_regionHandle, 64096, LocalID, lPos, mRot);
            }
            else
            {
                remoteClient.SendPrimTerseUpdate(m_regionHandle, 64096, LocalID, lPos, mRot, Velocity, RotationalVelocity);
                //System.Console.WriteLine("RVel:" + RotationalVelocity);
            }
        }

        #endregion

        public virtual void UpdateMovement()
        {
        }
        #region Events
        public void PhysicsRequestingTerseUpdate()
        {
            ScheduleTerseUpdate();

            //SendTerseUpdateToAllClients();
        }
        #endregion

        public void PhysicsOutOfBounds(PhysicsVector pos)
        {
            OpenSim.Framework.Console.MainLog.Instance.Verbose("PHYSICS", "Physical Object went out of bounds.");
            doPhysicsPropertyUpdate(false);
            ScheduleFullUpdate();
        }


        public virtual void OnGrab(LLVector3 offsetPos, IClientAPI remoteClient)
        {
        }

        public void SetText(string text, Vector3 color, double alpha)
        {
            Color = Color.FromArgb (0xff - (int)(alpha * 0xff),
                                    (int)(color.x * 0xff),
                                    (int)(color.y * 0xff),
                                    (int)(color.z * 0xff));
            Text = text;
        }

        public class InventoryStringBuilder
        {
            public string BuildString = "";

            public InventoryStringBuilder(LLUUID folderID, LLUUID parentID)
            {
                BuildString += "\tinv_object\t0\n\t{\n";
                AddNameValueLine("obj_id", folderID.ToStringHyphenated());
                AddNameValueLine("parent_id", parentID.ToStringHyphenated());
                AddNameValueLine("type", "category");
                AddNameValueLine("name", "Contents");
                AddSectionEnd();
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
