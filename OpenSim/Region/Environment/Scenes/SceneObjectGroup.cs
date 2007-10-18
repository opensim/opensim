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
using System.IO;
using System.Text;
using System.Xml;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    public delegate void PrimCountTaintedDelegate();

    public class SceneObjectGroup : EntityBase
    {
        private Encoding enc = Encoding.ASCII;

        protected SceneObjectPart m_rootPart;
        protected Dictionary<LLUUID, SceneObjectPart> m_parts = new Dictionary<LLUUID, SceneObjectPart>();

        protected ulong m_regionHandle;

        public event PrimCountTaintedDelegate OnPrimCountTainted;

        public bool HasChanged = false;

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public int PrimCount
        {
            get { return 1; }
        }

        public LLQuaternion GroupRotation
        {
            get { return m_rootPart.RotationOffset; }
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
            get { return m_parts; }
            set { m_parts = value; }
        }

        public SceneObjectPart RootPart
        {
            get { return m_rootPart; }
            set { m_rootPart = value; }
        }

        public ulong RegionHandle
        {
            get { return m_regionHandle; }
            set
            {
                m_regionHandle = value;
                lock (m_parts)
                {
                    foreach (SceneObjectPart part in m_parts.Values)
                    {
                        part.RegionHandle = m_regionHandle;
                    }
                }
            }
        }

        public override LLVector3 AbsolutePosition
        {
            get { return m_rootPart.GroupPosition; }
            set
            {
                LLVector3 val = value;
                if (val.X > 255.6f)
                {
                    val.X = 255.6f;
                }
                else if (val.X < 0.4f)
                {
                    val.X = 0.4f;
                }

                if (val.Y > 255.6f)
                {
                    val.Y = 255.6f;
                }
                else if (val.Y < 0.4f)
                {
                    val.Y = 0.4f;
                }

                lock (m_parts)
                {
                    foreach (SceneObjectPart part in m_parts.Values)
                    {
                        part.GroupPosition = val;
                    }
                }
                if (m_rootPart.PhysActor != null)
                {
                    m_rootPart.PhysActor.Position =
                        new PhysicsVector(m_rootPart.GroupPosition.X, m_rootPart.GroupPosition.Y,
                                          m_rootPart.GroupPosition.Z);
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

        public string Text
        {
            get { return m_rootPart.Text; }
            set { m_rootPart.Text = value; }
        }

        /// <summary>
        /// Added because the Parcel code seems to use it
        /// but not sure a object should have this
        /// as what does it tell us? that some avatar has selected it (but not what Avatar/user)
        /// think really there should be a list (or whatever) in each scenepresence
        /// saying what prim(s) that user has selected. 
        /// </summary>
        protected bool m_isSelected = false;

        protected virtual bool InSceneBackup
        {
            get { return true; }
        }

        public bool IsSelected
        {
            get { return m_isSelected; }
            set { m_isSelected = value; }
        }

        // The UUID for the Region this Object is in.
        public LLUUID RegionUUID
        {
            get
            {
                if (m_scene != null)
                {
                    return m_scene.RegionInfo.RegionID;
                }
                return LLUUID.Zero;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 
        /// </summary>
        public SceneObjectGroup()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        public SceneObjectGroup(Scene scene, ulong regionHandle, string xmlData)
        {
            m_scene = scene;
            m_regionHandle = regionHandle;

            StringReader sr = new StringReader(xmlData);
            XmlTextReader reader = new XmlTextReader(sr);
            reader.Read();
            reader.ReadStartElement("SceneObjectGroup");
            reader.ReadStartElement("RootPart");
            m_rootPart = SceneObjectPart.FromXml(reader);
            reader.ReadEndElement();

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (reader.Name == "Part")
                        {
                            reader.Read();
                            SceneObjectPart Part = SceneObjectPart.FromXml(reader);
                            Part.LocalID = m_scene.PrimIDAllocate();
                            AddPart(Part);
                            Part.RegionHandle = m_regionHandle;
                        }
                        break;
                    case XmlNodeType.EndElement:
                        break;
                }
            }
            reader.Close();
            sr.Close();
            m_rootPart.SetParent(this);
            m_parts.Add(m_rootPart.UUID, m_rootPart);
            m_rootPart.LocalID = m_scene.PrimIDAllocate();
            m_rootPart.ParentID = 0;
            m_rootPart.RegionHandle = m_regionHandle;
            UpdateParentIDs();

            AttachToBackup();

            ScheduleGroupForFullUpdate();
        }

        /// <summary>
        /// 
        /// </summary>
        public SceneObjectGroup(string xmlData)
        {
            StringReader sr = new StringReader(xmlData);
            XmlTextReader reader = new XmlTextReader(sr);
            reader.Read();
            reader.ReadStartElement("SceneObjectGroup");
           // reader.ReadStartElement("RootPart");
            m_rootPart = SceneObjectPart.FromXml(reader);
            //reader.ReadEndElement();

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (reader.Name == "SceneObjectPart")
                        {
                           // reader.Read();
                            SceneObjectPart Part = SceneObjectPart.FromXml(reader);
                            AddPart(Part);
                        }
                        break;
                    case XmlNodeType.EndElement:
                        break;
                }
            }
            reader.Close();
            sr.Close();
            m_rootPart.SetParent(this);
            m_parts.Add(m_rootPart.UUID, m_rootPart);
            m_rootPart.ParentID = 0;
            UpdateParentIDs();

            ScheduleGroupForFullUpdate();
        }

        private void AttachToBackup()
        {
            if (InSceneBackup)
            {
                m_scene.EventManager.OnBackup += ProcessBackup;
            }
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
        public SceneObjectGroup(Scene scene, ulong regionHandle, LLUUID ownerID, uint localID, LLVector3 pos,
                                PrimitiveBaseShape shape)
        {
            m_regionHandle = regionHandle;
            m_scene = scene;

            // this.Pos = pos;
            LLVector3 rootOffset = new LLVector3(0, 0, 0);
            SceneObjectPart newPart =
                new SceneObjectPart(m_regionHandle, this, ownerID, localID, shape, pos, rootOffset);
            m_parts.Add(newPart.UUID, newPart);
            SetPartAsRoot(newPart);

            AttachToBackup();
        }

        #endregion

        public string ToXmlString()
        {
            using (StringWriter sw = new StringWriter())
            {
                using (XmlTextWriter writer = new XmlTextWriter(sw))
                {
                    ToXml(writer);
                }

                return sw.ToString();
            }
        }

        public void ToXml(XmlTextWriter writer)
        {
            writer.WriteStartElement(String.Empty, "SceneObjectGroup", String.Empty);
            writer.WriteStartElement(String.Empty, "RootPart", String.Empty);
            m_rootPart.ToXml(writer);
            writer.WriteEndElement();
            writer.WriteStartElement(String.Empty, "OtherParts", String.Empty);
            foreach (SceneObjectPart part in m_parts.Values)
            {
                if (part.UUID != m_rootPart.UUID)
                {
                    writer.WriteStartElement(String.Empty, "Part", String.Empty);
                    part.ToXml(writer);
                    writer.WriteEndElement();
                }
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        #region Copying

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public new SceneObjectGroup Copy()
        {
            SceneObjectGroup dupe = (SceneObjectGroup) MemberwiseClone();
            dupe.m_parts = new Dictionary<LLUUID, SceneObjectPart>();
            dupe.m_parts.Clear();
            dupe.AbsolutePosition = new LLVector3(AbsolutePosition.X, AbsolutePosition.Y, AbsolutePosition.Z);
            dupe.m_scene = m_scene;
            dupe.m_regionHandle = m_regionHandle;
            dupe.CopyRootPart(m_rootPart);

            /// may need to create a new Physics actor.
            if (dupe.RootPart.PhysActor != null)
            {
                PrimitiveBaseShape pbs = dupe.RootPart.Shape;

                dupe.RootPart.PhysActor = m_scene.PhysScene.AddPrimShape(
                        dupe.RootPart.Name,
                        pbs,
                       new PhysicsVector(dupe.RootPart.AbsolutePosition.X, dupe.RootPart.AbsolutePosition.Y, dupe.RootPart.AbsolutePosition.Z),
                       new PhysicsVector(dupe.RootPart.Scale.X, dupe.RootPart.Scale.Y, dupe.RootPart.Scale.Z),
                       new Axiom.Math.Quaternion(dupe.RootPart.RotationOffset.W, dupe.RootPart.RotationOffset.X,
                                                 dupe.RootPart.RotationOffset.Y, dupe.RootPart.RotationOffset.Z));

            }

            List<SceneObjectPart> partList = new List<SceneObjectPart>(m_parts.Values);
            foreach (SceneObjectPart part in partList)
            {
                if (part.UUID != m_rootPart.UUID)
                {
                    dupe.CopyPart(part);
                }
            }
            dupe.UpdateParentIDs();

            dupe.AttachToBackup();

            return dupe;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="part"></param>
        public void CopyRootPart(SceneObjectPart part)
        {
            SceneObjectPart newPart = part.Copy(m_scene.PrimIDAllocate());
            newPart.SetParent(this);
            m_parts.Add(newPart.UUID, newPart);
            SetPartAsRoot(newPart);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="part"></param>
        public void CopyPart(SceneObjectPart part)
        {
            SceneObjectPart newPart = part.Copy(m_scene.PrimIDAllocate());
            newPart.SetParent(this);
            m_parts.Add(newPart.UUID, newPart);
            SetPartAsNonRoot(newPart);
        }

        #endregion

        #region Scheduling

        /// <summary>
        /// 
        /// </summary>
        public override void Update()
        {
            foreach (SceneObjectPart part in m_parts.Values)
            {
                part.SendScheduledUpdates();
            }
        }

        public void ScheduleFullUpdateToAvatar(ScenePresence presence)
        {
            foreach (SceneObjectPart part in m_parts.Values)
            {
                part.AddFullUpdateToAvatar(presence);
            }
        }

        public void ScheduleTerseUpdateToAvatar(ScenePresence presence)
        {
            foreach (SceneObjectPart part in m_parts.Values)
            {
                part.AddTerseUpdateToAvatar(presence);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ScheduleGroupForFullUpdate()
        {
            HasChanged = true;
            foreach (SceneObjectPart part in m_parts.Values)
            {
                part.ScheduleFullUpdate();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void ScheduleGroupForTerseUpdate()
        {
            HasChanged = true;
            foreach (SceneObjectPart part in m_parts.Values)
            {
                part.ScheduleTerseUpdate();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendGroupFullUpdate()
        {
            HasChanged = true;
            foreach (SceneObjectPart part in m_parts.Values)
            {
                part.SendFullUpdateToAllClients();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendGroupTerseUpdate()
        {
            HasChanged = true;
            foreach (SceneObjectPart part in m_parts.Values)
            {
                part.SendTerseUpdateToAllClients();
            }
        }

        #endregion

        #region SceneGroupPart Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="primID"></param>
        /// <returns></returns>
        public SceneObjectPart GetChildPart(LLUUID primID)
        {
            SceneObjectPart childPart = null;
            if (m_parts.ContainsKey(primID))
            {
                childPart = m_parts[primID];
            }
            return childPart;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <returns></returns>
        public SceneObjectPart GetChildPart(uint localID)
        {
            foreach (SceneObjectPart part in m_parts.Values)
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
            if (m_parts.ContainsKey(primID))
            {
                childPart = m_parts[primID];
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
            foreach (SceneObjectPart part in m_parts.Values)
            {
                if (part.LocalID == localID)
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Packet Handlers

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objectGroup"></param>
        public void LinkToGroup(SceneObjectGroup objectGroup)
        {
            SceneObjectPart linkPart = objectGroup.m_rootPart;
            Vector3 oldGroupPosition =
                new Vector3(linkPart.GroupPosition.X, linkPart.GroupPosition.Y, linkPart.GroupPosition.Z);
            Quaternion oldRootRotation =
                new Quaternion(linkPart.RotationOffset.W, linkPart.RotationOffset.X, linkPart.RotationOffset.Y,
                               linkPart.RotationOffset.Z);

            linkPart.OffsetPosition = linkPart.GroupPosition - AbsolutePosition;
            linkPart.GroupPosition = AbsolutePosition;

            Vector3 axPos = new Vector3(linkPart.OffsetPosition.X, linkPart.OffsetPosition.Y, linkPart.OffsetPosition.Z);
            Quaternion parentRot =
                new Quaternion(m_rootPart.RotationOffset.W, m_rootPart.RotationOffset.X, m_rootPart.RotationOffset.Y,
                               m_rootPart.RotationOffset.Z);
            axPos = parentRot.Inverse()*axPos;
            linkPart.OffsetPosition = new LLVector3(axPos.x, axPos.y, axPos.z);
            Quaternion oldRot =
                new Quaternion(linkPart.RotationOffset.W, linkPart.RotationOffset.X, linkPart.RotationOffset.Y,
                               linkPart.RotationOffset.Z);
            Quaternion newRot = parentRot.Inverse()*oldRot;
            linkPart.RotationOffset = new LLQuaternion(newRot.x, newRot.y, newRot.z, newRot.w);
            linkPart.ParentID = m_rootPart.LocalID;
            m_parts.Add(linkPart.UUID, linkPart);
            linkPart.SetParent(this);

            if (linkPart.PhysActor != null)
            {
                m_scene.PhysScene.RemovePrim(linkPart.PhysActor);
                linkPart.PhysActor = null;
            }

            //TODO: rest of parts
            foreach (SceneObjectPart part in objectGroup.Children.Values)
            {
                if (part.UUID != objectGroup.m_rootPart.UUID)
                {
                    LinkNonRootPart(part, oldGroupPosition, oldRootRotation);
                }
            }

            DetachFromBackup(objectGroup);

            m_scene.DeleteEntity(objectGroup.UUID);

            objectGroup.DeleteParts();
            ScheduleGroupForFullUpdate();
        }

        private void DetachFromBackup(SceneObjectGroup objectGroup)
        {
            m_scene.EventManager.OnBackup -= objectGroup.ProcessBackup;
        }


        private void LinkNonRootPart(SceneObjectPart part, Vector3 oldGroupPosition, Quaternion oldGroupRotation)
        {
            part.SetParent(this);
            part.ParentID = m_rootPart.LocalID;
            m_parts.Add(part.UUID, part);

            Vector3 axiomOldPos = new Vector3(part.OffsetPosition.X, part.OffsetPosition.Y, part.OffsetPosition.Z);
            axiomOldPos = oldGroupRotation*axiomOldPos;
            axiomOldPos += oldGroupPosition;
            LLVector3 oldAbsolutePosition = new LLVector3(axiomOldPos.x, axiomOldPos.y, axiomOldPos.z);
            part.OffsetPosition = oldAbsolutePosition - AbsolutePosition;

            Quaternion axiomRootRotation =
                new Quaternion(m_rootPart.RotationOffset.W, m_rootPart.RotationOffset.X, m_rootPart.RotationOffset.Y,
                               m_rootPart.RotationOffset.Z);

            Vector3 axiomPos = new Vector3(part.OffsetPosition.X, part.OffsetPosition.Y, part.OffsetPosition.Z);
            axiomPos = axiomRootRotation.Inverse()*axiomPos;
            part.OffsetPosition = new LLVector3(axiomPos.x, axiomPos.y, axiomPos.z);

            Quaternion axiomPartRotation =
                new Quaternion(part.RotationOffset.W, part.RotationOffset.X, part.RotationOffset.Y,
                               part.RotationOffset.Z);

            axiomPartRotation = oldGroupRotation*axiomPartRotation;
            axiomPartRotation = axiomRootRotation.Inverse()*axiomPartRotation;
            part.RotationOffset =
                new LLQuaternion(axiomPartRotation.x, axiomPartRotation.y, axiomPartRotation.z, axiomPartRotation.w);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="pos"></param>
        /// <param name="remoteClient"></param>
        public void GrabMovement(LLVector3 offset, LLVector3 pos, IClientAPI remoteClient)
        {
            AbsolutePosition = pos;
            m_rootPart.SendTerseUpdateToAllClients();
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
            proper.ObjectData[0].CreationDate = (ulong) m_rootPart.CreationDate;
            proper.ObjectData[0].CreatorID = m_rootPart.CreatorID;
            proper.ObjectData[0].FolderID = LLUUID.Zero;
            proper.ObjectData[0].FromTaskID = LLUUID.Zero;
            proper.ObjectData[0].GroupID = LLUUID.Zero;
            proper.ObjectData[0].InventorySerial = (short) m_rootPart.InventorySerial;
            proper.ObjectData[0].LastOwnerID = m_rootPart.LastOwnerID;
            proper.ObjectData[0].ObjectID = UUID;
            proper.ObjectData[0].OwnerID = m_rootPart.OwnerID;
            proper.ObjectData[0].TouchName = enc.GetBytes(m_rootPart.TouchName + "\0");
            proper.ObjectData[0].TextureID = new byte[0];
            proper.ObjectData[0].SitName = enc.GetBytes(m_rootPart.SitName + "\0");
            proper.ObjectData[0].Name = enc.GetBytes(m_rootPart.Name + "\0");
            proper.ObjectData[0].Description = enc.GetBytes(m_rootPart.Description + "\0");
            proper.ObjectData[0].OwnerMask = m_rootPart.OwnerMask;
            proper.ObjectData[0].NextOwnerMask = m_rootPart.NextOwnerMask;
            proper.ObjectData[0].GroupMask = m_rootPart.GroupMask;
            proper.ObjectData[0].EveryoneMask = m_rootPart.EveryoneMask;
            proper.ObjectData[0].BaseMask = m_rootPart.BaseMask;

            client.OutPacket(proper);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        public void SetPartName(string name, uint localID)
        {
            name = name.Remove(name.Length - 1, 1);
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                part.Name = name;
            }
        }

        public void SetPartDescription(string des, uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                part.Description = des;
            }
        }

        public void SetPartText(string text, uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                part.Text = text;
            }
        }

        public void SetPartText(string text, LLUUID partID)
        {
            SceneObjectPart part = GetChildPart(partID);
            if (part != null)
            {
                part.Text = text;
            }
        }

        public string GetPartName(uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                return part.Name;
            }
            return "";
        }

        public string GetPartDescription(uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                return part.Description;
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="localID"></param>
        public bool GetPartInventoryFileName(IClientAPI remoteClient, uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                return part.GetInventoryFileName(remoteClient, localID);
            }
            return false;
        }

        public string RequestInventoryFile(uint localID, IXfer xferManager)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                part.RequestInventoryFile(xferManager);
            }
            return "";
        }

        public bool AddInventoryItem(IClientAPI remoteClient, uint localID, InventoryItemBase item)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                SceneObjectPart.TaskInventoryItem taskItem = new SceneObjectPart.TaskInventoryItem();
                taskItem.item_id = item.inventoryID;
                taskItem.asset_id = item.assetID;
                taskItem.name = item.inventoryName;
                taskItem.desc = item.inventoryDescription;
                taskItem.owner_id = item.avatarID;
                taskItem.creator_id = item.creatorsID;
                taskItem.type = SceneObjectPart.TaskInventoryItem.Types[item.assetType];
                taskItem.inv_type = SceneObjectPart.TaskInventoryItem.Types[item.invType];
                part.AddInventoryItem(taskItem);
                return true;
            }
            return false;
        }

        public bool AddInventoryItem(IClientAPI remoteClient, uint localID, InventoryItemBase item, LLUUID copyItemID)
        {
            if (copyItemID != LLUUID.Zero)
            {
                SceneObjectPart part = GetChildPart(localID);
                if (part != null)
                {
                    SceneObjectPart.TaskInventoryItem taskItem = new SceneObjectPart.TaskInventoryItem();
                    taskItem.item_id = copyItemID;
                    taskItem.asset_id = item.assetID;
                    taskItem.name = item.inventoryName;
                    taskItem.desc = item.inventoryDescription;
                    taskItem.owner_id = new LLUUID(item.avatarID.ToString());
                    taskItem.creator_id = new LLUUID(item.creatorsID.ToString());
                    taskItem.type = SceneObjectPart.TaskInventoryItem.Types[item.assetType];
                    taskItem.inv_type = SceneObjectPart.TaskInventoryItem.Types[item.invType];
                    part.AddInventoryItem(taskItem);
                    return true;
                }
            }
            else
            {
                return AddInventoryItem(remoteClient, localID, item);
            }
            return false;
        }

        public int RemoveInventoryItem(IClientAPI remoteClient, uint localID, LLUUID itemID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                return part.RemoveInventoryItem(remoteClient, localID, itemID);
            }
            return -1;
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
            SceneObjectPart part = GetChildPart(localID);
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
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                part.UpdateTextureEntry(textureEntry);
            }
        }

        #endregion

        #region Shape

        /// <summary>
        /// 
        /// </summary>
        /// <param name="shapeBlock"></param>
        public void UpdateShape(ObjectShapePacket.ObjectDataBlock shapeBlock, uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                part.UpdateShape(shapeBlock);
            }
            if (m_rootPart.PhysActor != null)
            {
                this.m_scene.PhysScene.RemovePrim(m_rootPart.PhysActor);
                m_rootPart.PhysActor = m_scene.PhysScene.AddPrimShape(
                        m_rootPart.Name,
                        m_rootPart.Shape,
                       new PhysicsVector(m_rootPart.AbsolutePosition.X, m_rootPart.AbsolutePosition.Y, m_rootPart.AbsolutePosition.Z),
                       new PhysicsVector(m_rootPart.Scale.X, m_rootPart.Scale.Y, m_rootPart.Scale.Z),
                       new Axiom.Math.Quaternion(m_rootPart.RotationOffset.W, m_rootPart.RotationOffset.X,
                                                 m_rootPart.RotationOffset.Y, m_rootPart.RotationOffset.Z));
            }
        }

        #endregion

        #region Resize

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scale"></param>
        /// <param name="localID"></param>
        public void Resize(LLVector3 scale, uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                part.Resize(scale);
                if (part.UUID == m_rootPart.UUID)
                {
                    if (m_rootPart.PhysActor != null)
                    {
                        m_rootPart.PhysActor.Size =
                            new PhysicsVector(m_rootPart.Scale.X, m_rootPart.Scale.Y, m_rootPart.Scale.Z);
                    }
                }
            }
        }

        #endregion

        #region Position

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        public void UpdateGroupPosition(LLVector3 pos)
        {
            AbsolutePosition = pos;
            ScheduleGroupForTerseUpdate();

            m_scene.EventManager.TriggerGroupMove(this.UUID, pos);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="localID"></param>
        public void UpdateSinglePosition(LLVector3 pos, uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                if (part.UUID == m_rootPart.UUID)
                {
                    UpdateRootPosition(pos);
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
            LLVector3 oldPos =
                new LLVector3(AbsolutePosition.X + m_rootPart.OffsetPosition.X,
                              AbsolutePosition.Y + m_rootPart.OffsetPosition.Y,
                              AbsolutePosition.Z + m_rootPart.OffsetPosition.Z);
            LLVector3 diff = oldPos - newPos;
            Vector3 axDiff = new Vector3(diff.X, diff.Y, diff.Z);
            Quaternion partRotation =
                new Quaternion(m_rootPart.RotationOffset.W, m_rootPart.RotationOffset.X, m_rootPart.RotationOffset.Y,
                               m_rootPart.RotationOffset.Z);
            axDiff = partRotation.Inverse()*axDiff;
            diff.X = axDiff.x;
            diff.Y = axDiff.y;
            diff.Z = axDiff.z;

            foreach (SceneObjectPart obPart in m_parts.Values)
            {
                if (obPart.UUID != m_rootPart.UUID)
                {
                    obPart.OffsetPosition = obPart.OffsetPosition + diff;
                }
            }
            AbsolutePosition = newPos;
            ScheduleGroupForTerseUpdate();
        }

        #endregion

        #region Rotation

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rot"></param>
        public void UpdateGroupRotation(LLQuaternion rot)
        {
            m_rootPart.UpdateRotation(rot);
            if (m_rootPart.PhysActor != null)
            {
                m_rootPart.PhysActor.Orientation =
                    new Quaternion(m_rootPart.RotationOffset.W, m_rootPart.RotationOffset.X, m_rootPart.RotationOffset.Y,
                                   m_rootPart.RotationOffset.Z);
            }
            ScheduleGroupForTerseUpdate();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        public void UpdateGroupRotation(LLVector3 pos, LLQuaternion rot)
        {
            m_rootPart.UpdateRotation(rot);
            if (m_rootPart.PhysActor != null)
            {
                m_rootPart.PhysActor.Orientation =
                    new Quaternion(m_rootPart.RotationOffset.W, m_rootPart.RotationOffset.X, m_rootPart.RotationOffset.Y,
                                   m_rootPart.RotationOffset.Z);
            }
            AbsolutePosition = pos;
            ScheduleGroupForTerseUpdate();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="rot"></param>
        /// <param name="localID"></param>
        public void UpdateSingleRotation(LLQuaternion rot, uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                if (part.UUID == m_rootPart.UUID)
                {
                    UpdateRootRotation(rot);
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
            Quaternion axRot = new Quaternion(rot.W, rot.X, rot.Y, rot.Z);
            Quaternion oldParentRot =
                new Quaternion(m_rootPart.RotationOffset.W, m_rootPart.RotationOffset.X, m_rootPart.RotationOffset.Y,
                               m_rootPart.RotationOffset.Z);

            m_rootPart.UpdateRotation(rot);
            if (m_rootPart.PhysActor != null)
            {
                m_rootPart.PhysActor.Orientation =
                    new Quaternion(m_rootPart.RotationOffset.W, m_rootPart.RotationOffset.X, m_rootPart.RotationOffset.Y,
                                   m_rootPart.RotationOffset.Z);
            }

            foreach (SceneObjectPart prim in m_parts.Values)
            {
                if (prim.UUID != m_rootPart.UUID)
                {
                    Vector3 axPos = new Vector3(prim.OffsetPosition.X, prim.OffsetPosition.Y, prim.OffsetPosition.Z);
                    axPos = oldParentRot*axPos;
                    axPos = axRot.Inverse()*axPos;
                    prim.OffsetPosition = new LLVector3(axPos.x, axPos.y, axPos.z);
                    Quaternion primsRot =
                        new Quaternion(prim.RotationOffset.W, prim.RotationOffset.X, prim.RotationOffset.Y,
                                       prim.RotationOffset.Z);
                    Quaternion newRot = oldParentRot*primsRot;
                    newRot = axRot.Inverse()*newRot;
                    prim.RotationOffset = new LLQuaternion(newRot.x, newRot.y, newRot.z, newRot.w);
                    prim.ScheduleTerseUpdate();
                }
            }
            m_rootPart.ScheduleTerseUpdate();
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="part"></param>
        private void SetPartAsRoot(SceneObjectPart part)
        {
            m_rootPart = part;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="part"></param>
        private void SetPartAsNonRoot(SceneObjectPart part)
        {
            part.ParentID = m_rootPart.LocalID;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> GetScenePresences()
        {
            return m_scene.GetScenePresences();
        }

        #region Events

        /// <summary>
        /// 
        /// </summary>
        public void TriggerTainted()
        {
            if (OnPrimCountTainted != null)
            {
                OnPrimCountTainted();
            }
        }

        /// <summary>
        /// Processes backup
        /// </summary>
        /// <param name="datastore"></param>
        public void ProcessBackup(IRegionDataStore datastore)
        {
            if (HasChanged)
            {
                datastore.StoreObject(this, m_scene.RegionInfo.RegionID);
                HasChanged = false;
            }
        }

        #endregion

        #region Client Updating

        public void SendFullUpdateToClient(IClientAPI remoteClient)
        {
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    SendPartFullUpdate(remoteClient, part);
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
            if (m_rootPart.UUID == part.UUID)
            {
                part.SendFullUpdateToClient(remoteClient, AbsolutePosition);
            }
            else
            {
                part.SendFullUpdateToClient(remoteClient);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="part"></param>
        internal void SendPartTerseUpdate(IClientAPI remoteClient, SceneObjectPart part)
        {
            if (m_rootPart.UUID == part.UUID)
            {
                part.SendTerseUpdateToClient(remoteClient, AbsolutePosition);
            }
            else
            {
                part.SendTerseUpdateToClient(remoteClient);
            }
        }

        #endregion

        public override void UpdateMovement()
        {
            foreach (SceneObjectPart part in m_parts.Values)
            {
                part.UpdateMovement();
            }

            base.UpdateMovement();
        }

        /// <summary>
        /// Added as a way for the storage provider to reset the scene, 
        /// most likely a better way to do this sort of thing but for now...
        /// </summary>
        /// <param name="scene"></param>
        public void SetScene(Scene scene)
        {
            m_scene = scene;
            AttachToBackup();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="part"></param>
        public void AddPart(SceneObjectPart part)
        {
            part.SetParent(this);
            m_parts.Add(part.UUID, part);
        }

        /// <summary>
        /// 
        /// </summary>
        public void UpdateParentIDs()
        {
            foreach (SceneObjectPart part in m_parts.Values)
            {
                if (part.UUID != m_rootPart.UUID)
                {
                    part.ParentID = m_rootPart.LocalID;
                }
            }
        }

        public void RegenerateFullIDs()
        {
            foreach (SceneObjectPart part in m_parts.Values)
            {
                part.UUID = LLUUID.Random();
            }
        }

        public LLUUID GetPartsFullID(uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                return part.UUID;
            }
            return null;
        }

        public void UpdateText(string text)
        {
            m_rootPart.Text = text;
            m_rootPart.ScheduleTerseUpdate();
        }

        public void ObjectGrabHandler(uint localId, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            if (m_rootPart.LocalID == localId)
            {
                OnGrabGroup(offsetPos, remoteClient);
            }
            else
            {
                SceneObjectPart part = GetChildPart(localId);
                OnGrabPart(part, offsetPos, remoteClient);
            }
        }

        public virtual void OnGrabPart(SceneObjectPart part, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            part.OnGrab(offsetPos, remoteClient);
        }

        public virtual void OnGrabGroup(LLVector3 offsetPos, IClientAPI remoteClient)
        {
            m_scene.EventManager.TriggerGroupGrab(this.UUID, offsetPos, remoteClient.AgentId);
        }

        public void DeleteGroup()
        {
            DetachFromBackup(this);
            foreach (SceneObjectPart part in m_parts.Values)
            {
                List<ScenePresence> avatars = GetScenePresences();
                for (int i = 0; i < avatars.Count; i++)
                {
                    avatars[i].ControllingClient.SendKillObject(m_regionHandle, part.LocalID);
                }
            }
        }

        public void DeleteParts()
        {
            m_rootPart = null;
            m_parts.Clear();
        }

        public override void SetText(string text, Vector3 color, double alpha)
        {
            Text = text;
        }
    }
}
