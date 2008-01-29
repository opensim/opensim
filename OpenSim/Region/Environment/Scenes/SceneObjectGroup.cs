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
using System.Drawing;
using System.IO;
using System.Text;
using System.Xml;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    public delegate void PrimCountTaintedDelegate();

    public partial class SceneObjectGroup : EntityBase
    {
        private Encoding enc = Encoding.ASCII;

        protected SceneObjectPart m_rootPart;
        protected Dictionary<LLUUID, SceneObjectPart> m_parts = new Dictionary<LLUUID, SceneObjectPart>();

        protected ulong m_regionHandle;

        public event PrimCountTaintedDelegate OnPrimCountTainted;

        /// <summary>
        /// Signal whether the non-inventory attributes of any prims in the group have changed 
        /// since the group's last persistent backup
        /// </summary>
        public bool HasGroupChanged = false;

        private LLVector3 lastPhysGroupPos;
        private LLQuaternion lastPhysGroupRot;

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public int PrimCount
        {
            get { return m_parts.Count; }
        }

        public LLQuaternion GroupRotation
        {
            get { return m_rootPart.RotationOffset; }
        }
        public LLUUID GroupID
        {
            get { return m_rootPart.GroupID; }
            set { m_rootPart.GroupID = value; }
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
                //if (m_rootPart.PhysActor != null)
                //{
                    //m_rootPart.PhysActor.Position =
                        //new PhysicsVector(m_rootPart.GroupPosition.X, m_rootPart.GroupPosition.Y,
                                          //m_rootPart.GroupPosition.Z);
                    //m_scene.PhysicsScene.AddPhysicsActorTaint(m_rootPart.PhysActor);
                //}
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
            set { m_rootPart.OwnerID = value; }
        }

        public Color Color
        {
            get { return m_rootPart.Color; }
            set { m_rootPart.Color = value; }
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
            set {
                m_isSelected = value;
                // Tell physics engine that group is selected
                if (m_rootPart.PhysActor != null)
                {
                    m_rootPart.PhysActor.Selected = value;
                }
            }
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
        /// This constructor creates a SceneObjectGroup using a pre-existing SceneObjectPart. 
        /// The original SceneObjectPart will be used rather than a copy, preserving 
        /// its existing localID and UUID.
        /// </summary>        
        public SceneObjectGroup(Scene scene, ulong regionHandle, SceneObjectPart part)
        {
            m_scene = scene;
            part.SetParent(this);
            part.ParentID = 0;
            part.LinkNum = 0;
            m_parts.Add(part.UUID, part);

            SetPartAsRoot(part);

            RegionHandle = regionHandle;

            AttachToBackup();

            ApplyPhysics(scene.m_physicalPrim);

            ScheduleGroupForFullUpdate();
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
            AddPart(m_rootPart);
            reader.ReadEndElement();

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (reader.Name == "Part")
                        {
                            reader.Read();
                            SceneObjectPart part = SceneObjectPart.FromXml(reader);
                            part.LocalID = m_scene.PrimIDAllocate();
                            AddPart(part);
                            part.RegionHandle = m_regionHandle;

                            part.TrimPermissions();
                        }
                        break;
                    case XmlNodeType.EndElement:
                        break;
                }
            }
            reader.Close();
            sr.Close();


            m_rootPart.LocalID = m_scene.PrimIDAllocate();
            m_rootPart.ParentID = 0;
            m_rootPart.RegionHandle = m_regionHandle;
            UpdateParentIDs();

            AttachToBackup();

            ApplyPhysics(scene.m_physicalPrim);

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
            m_rootPart = SceneObjectPart.FromXml(reader);
            m_rootPart.SetParent(this);
            m_parts.Add(m_rootPart.UUID, m_rootPart);
            m_rootPart.ParentID = 0;
            m_rootPart.LinkNum = 0;

            reader.Read();
            bool more = true;

            while (more)
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        if (reader.Name == "SceneObjectPart")
                        {
                            SceneObjectPart Part = SceneObjectPart.FromXml(reader);
                            AddPart(Part);
                        }
                        else
                        {
                            Console.WriteLine("found unexpected element: " + reader.Name);
                            reader.Read();
                        }
                        break;
                    case XmlNodeType.EndElement:
                        reader.Read();
                        break;
                }
                more = !reader.EOF;
            }
            reader.Close();
            sr.Close();

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

        public EntityIntersection TestIntersection(Ray hRay)
        {
            // We got a request from the inner_scene to raytrace along the Ray hRay
            // We're going to check all of the prim in this group for intersection with the ray
            // If we get a result, we're going to find the closest result to the origin of the ray
            // and send back the intersection information back to the innerscene.

            EntityIntersection returnresult = new EntityIntersection();

            foreach (SceneObjectPart part in m_parts.Values)
            {
                // Temporary commented to stop compiler warning
                //Vector3 partPosition =
                //    new Vector3(part.AbsolutePosition.X, part.AbsolutePosition.Y, part.AbsolutePosition.Z);
                Quaternion parentrotation =
                    new Quaternion(GroupRotation.W, GroupRotation.X, GroupRotation.Y, GroupRotation.Z);

                // Telling the prim to raytrace.
                EntityIntersection inter = part.TestIntersection(hRay, parentrotation);

                // This may need to be updated to the maximum draw distance possible..  
                // We might (and probably will) be checking for prim creation from other sims
                // when the camera crosses the border.
                float idist = 256f;


                if (inter.HitTF)
                {
                    // We need to find the closest prim to return to the testcaller along the ray
                    if (inter.distance < idist)
                    {
                        idist = inter.distance;
                        returnresult.HitTF = true;
                        returnresult.ipoint = inter.ipoint;
                        returnresult.obj = part;
                        returnresult.normal = inter.normal;
                        returnresult.distance = inter.distance;
                    }
                }
            }
            return returnresult;
        }

        /// <summary>
        /// 
        /// </summary>
        public SceneObjectGroup(Scene scene, ulong regionHandle, LLUUID ownerID, uint localID, LLVector3 pos,
                                LLQuaternion rot, PrimitiveBaseShape shape)
        {
            m_regionHandle = regionHandle;
            m_scene = scene;

            // this.Pos = pos;
            LLVector3 rootOffset = new LLVector3(0, 0, 0);
            SceneObjectPart newPart =
                new SceneObjectPart(m_regionHandle, this, ownerID, localID, shape, pos, rot, rootOffset);
            newPart.LinkNum = m_parts.Count;
            m_parts.Add(newPart.UUID, newPart);
            SetPartAsRoot(newPart);

            AttachToBackup();

            //ApplyPhysics(scene.m_physicalPrim);
        }

        /// <summary>
        /// 
        /// </summary>
        public SceneObjectGroup(Scene scene, ulong regionHandle, LLUUID ownerID, uint localID, LLVector3 pos,
                                PrimitiveBaseShape shape)
            : this(scene, regionHandle, ownerID, localID, pos, LLQuaternion.Identity, shape)
        {
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

        public string ToXmlString2()
        {
            using (StringWriter sw = new StringWriter())
            {
                using (XmlTextWriter writer = new XmlTextWriter(sw))
                {
                    ToXml2(writer);
                }

                return sw.ToString();
            }
        }

        public void ToXml2(XmlTextWriter writer)
        {
            writer.WriteStartElement(String.Empty, "SceneObjectGroup", String.Empty);
            m_rootPart.ToXml(writer);
            writer.WriteStartElement(String.Empty, "OtherParts", String.Empty);
            foreach (SceneObjectPart part in m_parts.Values)
            {
                if (part.UUID != m_rootPart.UUID)
                {
                    part.ToXml(writer);
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
        public SceneObjectGroup Copy(LLUUID cAgentID, LLUUID cGroupID)
        {
            SceneObjectGroup dupe = (SceneObjectGroup) MemberwiseClone();
            dupe.m_parts = new Dictionary<LLUUID, SceneObjectPart>();
            dupe.m_parts.Clear();
            //dupe.OwnerID = AgentID;
            //dupe.GroupID = GroupID;
            dupe.AbsolutePosition = new LLVector3(AbsolutePosition.X, AbsolutePosition.Y, AbsolutePosition.Z);
            dupe.m_scene = m_scene;
            dupe.m_regionHandle = m_regionHandle;

            dupe.CopyRootPart(m_rootPart, OwnerID, GroupID);
            dupe.m_rootPart.TrimPermissions();

            /// may need to create a new Physics actor.
            if (dupe.RootPart.PhysActor != null)
            {
                PrimitiveBaseShape pbs = dupe.RootPart.Shape;

                dupe.RootPart.PhysActor = m_scene.PhysicsScene.AddPrimShape(
                    dupe.RootPart.Name,
                    pbs,
                    new PhysicsVector(dupe.RootPart.AbsolutePosition.X, dupe.RootPart.AbsolutePosition.Y,
                                      dupe.RootPart.AbsolutePosition.Z),
                    new PhysicsVector(dupe.RootPart.Scale.X, dupe.RootPart.Scale.Y, dupe.RootPart.Scale.Z),
                    new Quaternion(dupe.RootPart.RotationOffset.W, dupe.RootPart.RotationOffset.X,
                                   dupe.RootPart.RotationOffset.Y, dupe.RootPart.RotationOffset.Z),
                    dupe.RootPart.PhysActor.IsPhysical);
                dupe.RootPart.DoPhysicsPropertyUpdate(dupe.RootPart.PhysActor.IsPhysical, true);
            }
            // Now we've made a copy that replaces this one, we need to 
            // switch the owner to the person who did the copying
            // Second Life copies an object and duplicates the first one in it's place
            // So, we have to make a copy of this one, set it in it's place then set the owner on this one

            SetRootPartOwner(m_rootPart, cAgentID, cGroupID);


            m_rootPart.ScheduleFullUpdate();

            List<SceneObjectPart> partList = new List<SceneObjectPart>(m_parts.Values);
            foreach (SceneObjectPart part in partList)
            {
                if (part.UUID != m_rootPart.UUID)
                {
                    dupe.CopyPart(part, OwnerID, GroupID);
                    SetPartOwner(part, cAgentID, cGroupID);
                    part.ScheduleFullUpdate();
                }
            }
            dupe.UpdateParentIDs();

            dupe.AttachToBackup();
            ScheduleGroupForFullUpdate();

            return dupe;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="part"></param>
        public void CopyRootPart(SceneObjectPart part, LLUUID cAgentID, LLUUID cGroupID)
        {
            SceneObjectPart newPart = part.Copy(m_scene.PrimIDAllocate(), OwnerID, GroupID, m_parts.Count);
            newPart.SetParent(this);
            m_parts.Add(newPart.UUID, newPart);
            SetPartAsRoot(newPart);
        }

        public void applyImpulse(PhysicsVector impulse)
        {
            SceneObjectPart rootpart = m_rootPart;
            if (m_rootPart.PhysActor != null)
            {
                m_rootPart.PhysActor.AddForce(impulse);
                m_scene.PhysicsScene.AddPhysicsActorTaint(m_rootPart.PhysActor);
            }
        }

        public void SetRootPartOwner(SceneObjectPart part, LLUUID cAgentID, LLUUID cGroupID)
        {
            part.LastOwnerID = part.OwnerID;
            part.OwnerID = cAgentID;
            part.GroupID = cGroupID;


            if (part.OwnerID != cAgentID)
            {
                // Apply Next Owner Permissions if we're not bypassing permissions
                if (!m_scene.PermissionsMngr.BypassPermissions)
                    m_rootPart.ApplyNextOwnerPermissions();
            }

            part.ScheduleFullUpdate();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="part"></param>
        public void CopyPart(SceneObjectPart part, LLUUID cAgentID, LLUUID cGroupID)
        {
            SceneObjectPart newPart = part.Copy(m_scene.PrimIDAllocate(), OwnerID, GroupID, m_parts.Count);
            newPart.SetParent(this);
            m_parts.Add(newPart.UUID, newPart);
            SetPartAsNonRoot(newPart);
        }

        /// <summary>
        /// Reset the LLUUIDs for all the prims that make up this group.
        /// 
        /// This is called by methods which want to add a new group to an existing scene, in order
        /// to ensure that there are no clashes with groups already present.
        /// </summary>
        public void ResetIDs()
        {
            List<SceneObjectPart> partsList = new List<SceneObjectPart>(m_parts.Values);
            m_parts.Clear();
            foreach (SceneObjectPart part in partsList)
            {
                part.ResetIDs(m_parts.Count);                
                m_parts.Add(part.UUID, part);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="part"></param>
        public void ServiceObjectPropertiesFamilyRequest(IClientAPI remoteClient, LLUUID AgentID, uint RequestFlags)
        {
            //RootPart.ServiceObjectPropertiesFamilyRequest(remoteClient, AgentID, RequestFlags);
            ObjectPropertiesFamilyPacket objPropFamilyPack = (ObjectPropertiesFamilyPacket) PacketPool.Instance.GetPacket(PacketType.ObjectPropertiesFamily);
            // TODO: don't create new blocks if recycling an old packet

            ObjectPropertiesFamilyPacket.ObjectDataBlock objPropDB = new ObjectPropertiesFamilyPacket.ObjectDataBlock();
            objPropDB.RequestFlags = RequestFlags;
            objPropDB.ObjectID = RootPart.UUID;
            objPropDB.OwnerID = RootPart.ObjectOwner;
            objPropDB.GroupID = RootPart.GroupID;
            objPropDB.BaseMask = RootPart.BaseMask;
            objPropDB.OwnerMask = RootPart.OwnerMask;
            objPropDB.GroupMask = RootPart.GroupMask;
            objPropDB.EveryoneMask = RootPart.EveryoneMask;
            objPropDB.NextOwnerMask = RootPart.NextOwnerMask;

            // TODO: More properties are needed in SceneObjectPart!
            objPropDB.OwnershipCost = RootPart.OwnershipCost;
            objPropDB.SaleType = RootPart.ObjectSaleType;
            objPropDB.SalePrice = RootPart.SalePrice;
            objPropDB.Category = RootPart.Category;
            objPropDB.LastOwnerID = RootPart.CreatorID;
            objPropDB.Name = Helpers.StringToField(RootPart.Name);
            objPropDB.Description = Helpers.StringToField(RootPart.Description);
            objPropFamilyPack.ObjectData = objPropDB;
            remoteClient.OutPacket(objPropFamilyPack, ThrottleOutPacketType.Task);
        }

        public void SetPartOwner(SceneObjectPart part, LLUUID cAgentID, LLUUID cGroupID)
        {
            part.OwnerID = cAgentID;
            part.GroupID = cGroupID;
        }

        #endregion

        #region Scheduling

        /// <summary>
        /// 
        /// </summary>
        public override void Update()
        {
            if (Util.GetDistanceTo(lastPhysGroupPos, AbsolutePosition) > 0.02)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    if (part.UpdateFlag == 0) part.UpdateFlag = 1;
                }
                lastPhysGroupPos = AbsolutePosition;
            }
            if ((Math.Abs(lastPhysGroupRot.W - GroupRotation.W) > 0.1)
                || (Math.Abs(lastPhysGroupRot.X - GroupRotation.X) > 0.1)
                || (Math.Abs(lastPhysGroupRot.Y - GroupRotation.Y) > 0.1)
                || (Math.Abs(lastPhysGroupRot.Z - GroupRotation.Z) > 0.1))
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    if (part.UpdateFlag == 0) part.UpdateFlag = 1;
                }
                lastPhysGroupRot = GroupRotation;
            }
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
            HasGroupChanged = true;
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
            HasGroupChanged = true;
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
            HasGroupChanged = true;
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
            HasGroupChanged = true;
            foreach (SceneObjectPart part in m_parts.Values)
            {
                part.SendTerseUpdateToAllClients();
            }
        }

        #endregion

        #region SceneGroupPart Methods

        /// <summary>
        /// Get the child part by LinkNum
        /// </summary>
        /// <param name="linknum"></param>
        /// <returns>null if no child part with that linknum or child part</returns>
        public SceneObjectPart GetLinkNumPart(int linknum)
        {
            foreach (SceneObjectPart part in m_parts.Values)
            {
                if (part.LinkNum == linknum)
                {
                    return part;
                }
            }
            return null;
        }

        /// <summary>
        /// Get a child part with a given UUID
        /// </summary>
        /// <param name="primID"></param>
        /// <returns>null if a child part with the primID was not found</returns>
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
        /// Get a child part with a given local ID
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if a child part with the local ID was not found</returns>
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
            if (m_parts.ContainsKey(primID))
            {
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
            linkPart.LinkNum = m_parts.Count;
            m_parts.Add(linkPart.UUID, linkPart);
            linkPart.SetParent(this);

            //if (linkPart.PhysActor != null)
            //{
               // m_scene.PhysicsScene.RemovePrim(linkPart.PhysActor);

                //linkPart.PhysActor = null;
            //}

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
            AbsolutePosition = AbsolutePosition;
            ScheduleGroupForFullUpdate();
        }

        /// <summary>
        /// Delink the given prim from this group.  The delinked prim is established as 
        /// an independent SceneObjectGroup.
        /// </summary>
        /// <param name="partID"></param>
        public void DelinkFromGroup(uint partID)
        {
            SceneObjectPart linkPart = GetChildPart(partID);

            if (null != linkPart)
            {

                // Remove the part from this object
                m_parts.Remove(linkPart.UUID);
                linkPart.ParentID = 0;

                if (linkPart.PhysActor != null)
                {
                    m_scene.PhysicsScene.RemovePrim(linkPart.PhysActor);
                }
                // We need to reset the child part's position 
                // ready for life as a separate object after being a part of another object
                Quaternion parentRot
                    = new Quaternion(
                        m_rootPart.RotationOffset.W,
                        m_rootPart.RotationOffset.X,
                        m_rootPart.RotationOffset.Y,
                        m_rootPart.RotationOffset.Z);

                Vector3 axPos
                    = new Vector3(
                        linkPart.OffsetPosition.X,
                        linkPart.OffsetPosition.Y,
                        linkPart.OffsetPosition.Z);

                axPos = parentRot*axPos;
                linkPart.OffsetPosition = new LLVector3(axPos.x, axPos.y, axPos.z);
                linkPart.GroupPosition = AbsolutePosition + linkPart.OffsetPosition;
                linkPart.OffsetPosition = new LLVector3(0, 0, 0);

                Quaternion oldRot
                    = new Quaternion(
                        linkPart.RotationOffset.W,
                        linkPart.RotationOffset.X,
                        linkPart.RotationOffset.Y,
                        linkPart.RotationOffset.Z);
                Quaternion newRot = parentRot*oldRot;
                linkPart.RotationOffset = new LLQuaternion(newRot.x, newRot.y, newRot.z, newRot.w);

                // Add physics information back to delinked part if appropriate
                // XXX This is messy and should be refactorable with the similar section in
                // SceneObjectPart.UpdatePrimFlags()
                //if (m_rootPart.PhysActor != null)
                //{
                    //linkPart.PhysActor = m_scene.PhysicsScene.AddPrimShape(
                        //linkPart.Name,
                        //linkPart.Shape,
                        //new PhysicsVector(linkPart.AbsolutePosition.X, linkPart.AbsolutePosition.Y,
                                          //linkPart.AbsolutePosition.Z),
                        //new PhysicsVector(linkPart.Scale.X, linkPart.Scale.Y, linkPart.Scale.Z),
                        //new Quaternion(linkPart.RotationOffset.W, linkPart.RotationOffset.X,
                                       //linkPart.RotationOffset.Y, linkPart.RotationOffset.Z),
                        //m_rootPart.PhysActor.IsPhysical);
                    //m_rootPart.DoPhysicsPropertyUpdate(m_rootPart.PhysActor.IsPhysical, true);
                //}

                SceneObjectGroup objectGroup = new SceneObjectGroup(m_scene, m_regionHandle, linkPart);

                m_scene.AddEntity(objectGroup);

                ScheduleGroupForFullUpdate();
            }
            else
            {
                MainLog.Instance.Verbose("SCENE",
                                         "DelinkFromGroup(): Child prim local id {0} not found in object with root prim id {1}",
                                         partID, LocalId);
            }
        }

        private void DetachFromBackup(SceneObjectGroup objectGroup)
        {
            m_scene.EventManager.OnBackup -= objectGroup.ProcessBackup;
        }

        private void LinkNonRootPart(SceneObjectPart part, Vector3 oldGroupPosition, Quaternion oldGroupRotation)
        {
            part.SetParent(this);
            part.ParentID = m_rootPart.LocalID;
            part.LinkNum = m_parts.Count;
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
        /// If object is physical, apply force to move it around
        /// If object is not physical, just put it at the resulting location
        /// </summary>
        /// <param name="offset">Always seems to be 0,0,0, so ignoring</param>
        /// <param name="pos">New position.  We do the math here to turn it into a force</param>
        /// <param name="remoteClient"></param>
        public void GrabMovement(LLVector3 offset, LLVector3 pos, IClientAPI remoteClient)
        {
            
            if (m_scene.EventManager.TriggerGroupMove(UUID, pos))
            {

                if (m_rootPart.PhysActor != null)
                {
                    if (m_rootPart.PhysActor.IsPhysical)
                    {
                        LLVector3 llmoveforce = pos - AbsolutePosition;
                        PhysicsVector grabforce = new PhysicsVector(llmoveforce.X, llmoveforce.Y, llmoveforce.Z);
                        grabforce = (grabforce / 10) * m_rootPart.PhysActor.Mass;
                        m_rootPart.PhysActor.AddForce(grabforce);
                        m_scene.PhysicsScene.AddPhysicsActorTaint(m_rootPart.PhysActor);
                    }
                    else
                    {
                        NonPhysicalGrabMovement(pos);
                    }
                }
                else
                {
                    NonPhysicalGrabMovement(pos);
                }
            }
        }
        public void NonPhysicalGrabMovement(LLVector3 pos)
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
            ObjectPropertiesPacket proper = (ObjectPropertiesPacket) PacketPool.Instance.GetPacket(PacketType.ObjectProperties);
            // TODO: don't create new blocks if recycling an old packet

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
            proper.ObjectData[0].TouchName = Helpers.StringToField(m_rootPart.TouchName);
            proper.ObjectData[0].TextureID = new byte[0];
            proper.ObjectData[0].SitName = Helpers.StringToField(m_rootPart.SitName);
            proper.ObjectData[0].Name = Helpers.StringToField(m_rootPart.Name);
            proper.ObjectData[0].Description = Helpers.StringToField(m_rootPart.Description);
            proper.ObjectData[0].OwnerMask = m_rootPart.OwnerMask;
            proper.ObjectData[0].NextOwnerMask = m_rootPart.NextOwnerMask;
            proper.ObjectData[0].GroupMask = m_rootPart.GroupMask;
            proper.ObjectData[0].EveryoneMask = m_rootPart.EveryoneMask;
            proper.ObjectData[0].BaseMask = m_rootPart.BaseMask;

            client.OutPacket(proper, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// Set the name of a prim
        /// </summary>
        /// <param name="name"></param>
        /// <param name="localID"></param>
        public void SetPartName(string name, uint localID)
        {
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
            return String.Empty;
        }

        public string GetPartDescription(uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                return part.Description;
            }
            return String.Empty;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="type"></param>
        /// <param name="inUse"></param>
        /// <param name="data"></param>
        /// 
        public void UpdatePrimFlags(uint localID, ushort type, bool inUse, byte[] data)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                // If we have children
                if (m_parts.Count > 1)
                {
                    foreach (SceneObjectPart parts in m_parts.Values)
                    {
                        parts.UpdatePrimFlags(type, inUse, data);
                    }
                }
                else
                {
                    part.UpdatePrimFlags(type, inUse, data);
                }
            }
        }

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

        public void UpdatePermissions(LLUUID AgentID, byte field, uint localID, uint mask, byte addRemTF)
        {
            SceneObjectPart updatePart = GetChildPart(localID);
            updatePart.UpdatePermissions(AgentID, field, localID, mask, addRemTF);
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
                m_scene.PhysicsScene.RemovePrim(m_rootPart.PhysActor);
                m_rootPart.PhysActor = m_scene.PhysicsScene.AddPrimShape(
                    m_rootPart.Name,
                    m_rootPart.Shape,
                    new PhysicsVector(m_rootPart.AbsolutePosition.X, m_rootPart.AbsolutePosition.Y,
                                      m_rootPart.AbsolutePosition.Z),
                    new PhysicsVector(m_rootPart.Scale.X, m_rootPart.Scale.Y, m_rootPart.Scale.Z),
                    new Quaternion(m_rootPart.RotationOffset.W, m_rootPart.RotationOffset.X,
                                   m_rootPart.RotationOffset.Y, m_rootPart.RotationOffset.Z),
                    m_rootPart.PhysActor.IsPhysical);
                bool UsePhysics = ((m_rootPart.ObjectFlags & (uint) LLObject.ObjectFlags.Physics) != 0);
                m_rootPart.DoPhysicsPropertyUpdate(UsePhysics, true);

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
                        m_scene.PhysicsScene.AddPhysicsActorTaint(m_rootPart.PhysActor);
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
            if (m_scene.EventManager.TriggerGroupMove(UUID, pos))
            {
                AbsolutePosition = pos;
            }
            //we need to do a terse update even if the move wasn't allowed
            // so that the position is reset in the client (the object snaps back)
            ScheduleGroupForTerseUpdate();
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
                m_scene.PhysicsScene.AddPhysicsActorTaint(m_rootPart.PhysActor);
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
                m_scene.PhysicsScene.AddPhysicsActorTaint(m_rootPart.PhysActor);
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
                m_scene.PhysicsScene.AddPhysicsActorTaint(m_rootPart.PhysActor);
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
            if (HasGroupChanged)
            {
                datastore.StoreObject(this, m_scene.RegionInfo.RegionID);
                HasGroupChanged = false;
            }
            
            ForEachPart(delegate(SceneObjectPart part) { part.ProcessInventoryBackup(datastore); });
        }

        #endregion

        #region Client Updating

        public void SendFullUpdateToClient(IClientAPI remoteClient, uint clientFlags)
        {
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    SendPartFullUpdate(remoteClient, part, clientFlags);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="part"></param>
        internal void SendPartFullUpdate(IClientAPI remoteClient, SceneObjectPart part, uint clientFlags)
        {
            if (m_rootPart.UUID == part.UUID)
            {
                part.SendFullUpdateToClient(remoteClient, AbsolutePosition, clientFlags);
            }
            else
            {
                part.SendFullUpdateToClient(remoteClient, clientFlags);
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
            part.LinkNum = m_parts.Count;
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

        public void ResetChildPrimPhysicsPositions()
        {
            AbsolutePosition = AbsolutePosition;
            HasGroupChanged = false;
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
            m_scene.EventManager.TriggerGroupGrab(UUID, offsetPos, remoteClient.AgentId);
        }

        public void DeleteGroup()
        {
            DetachFromBackup(this);
            foreach (SceneObjectPart part in m_parts.Values)
            {
                List<ScenePresence> avatars = GetScenePresences();
                for (int i = 0; i < avatars.Count; i++)
                {
                    if (avatars[i].ParentID == LocalId)
                    {
                        avatars[i].StandUp();
                    }

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
            Color = Color.FromArgb(0xff - (int) (alpha*0xff),
                                   (int) (color.x*0xff),
                                   (int) (color.y*0xff),
                                   (int) (color.z*0xff));
            Text = text;
        }

        public void ApplyPhysics(bool m_physicalPrim)
        {
            if (m_parts.Count > 1)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    part.ApplyPhysics(m_rootPart.ObjectFlags, m_physicalPrim);
                    
                    // Hack to get the physics scene geometries in the right spot
                    ResetChildPrimPhysicsPositions();
                   
                }
            }
            else
            {
                m_rootPart.ApplyPhysics(m_rootPart.ObjectFlags, m_physicalPrim);
            }
        }

        public void SetOwnerId(LLUUID userId)
        {
            ForEachPart(delegate(SceneObjectPart part)
                           { part.OwnerID = userId; });
        }

        public void ForEachPart(Action<SceneObjectPart> whatToDo)
        {
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    whatToDo(part);
                }
            }
        }
    }
}
