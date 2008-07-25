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
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    [Flags]
    public enum scriptEvents
    {
        None = 0,
        attach = 1,
        collision = 15,
        collision_end = 32,
        collision_start = 64,
        control = 128,
        dataserver = 256,
        email = 512,
        http_response = 1024,
        land_collision = 2048,
        land_collision_end = 4096,
        land_collision_start = 8192,
        at_target = 16384,
        listen = 32768,
        money = 65536,
        moving_end = 131072,
        moving_start = 262144,
        not_at_rot_target = 524288,
        not_at_target = 1048576,
        remote_data = 8388608,
        run_time_permissions = 268435456,
        state_entry = 1073741824,
        state_exit = 2,
        timer = 4,
        touch = 8,
        touch_end = 536870912,
        touch_start = 2097152,
        object_rez = 4194304
    }

    struct scriptPosTarget
    {
        public LLVector3 targetPos;
        public float tolerance;
    }

    public delegate void PrimCountTaintedDelegate();

    /// <summary>
    /// A scene object group is conceptually an object in the scene.  The object is constituted of SceneObjectParts
    /// (often known as prims), one of which is considered the root part.
    /// </summary>
    public partial class SceneObjectGroup : EntityBase
    {
        // private PrimCountTaintedDelegate handlerPrimCountTainted = null;

        /// <summary>
        /// Signal whether the non-inventory attributes of any prims in the group have changed
        /// since the group's last persistent backup
        /// </summary>
        public bool HasGroupChanged = false;
        
        public float scriptScore = 0f;

        private LLVector3 lastPhysGroupPos;
        private LLQuaternion lastPhysGroupRot;

        /// <summary>
        /// The constituent parts of this group
        /// </summary>
        protected Dictionary<LLUUID, SceneObjectPart> m_parts = new Dictionary<LLUUID, SceneObjectPart>();

        protected ulong m_regionHandle;
        protected SceneObjectPart m_rootPart;
        // private Dictionary<LLUUID, scriptEvents> m_scriptEvents = new Dictionary<LLUUID, scriptEvents>();

        private Dictionary<uint, scriptPosTarget> m_targets = new Dictionary<uint, scriptPosTarget>();

        private bool m_scriptListens_atTarget = false;
        private bool m_scriptListens_notAtTarget = false;

        #region Properties
        
        /// <summary>
        /// The name of an object grouping is always the same as its root part
        /// </summary>
        public override string Name
        {
            get { return RootPart.Name; }
            set { RootPart.Name = value; }
        } 

        /// <summary>
        /// Added because the Parcel code seems to use it
        /// but not sure a object should have this
        /// as what does it tell us? that some avatar has selected it (but not what Avatar/user)
        /// think really there should be a list (or whatever) in each scenepresence
        /// saying what prim(s) that user has selected.
        /// </summary>
        protected bool m_isSelected = false;

        /// <summary>
        /// Number of prims in this group
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
            get
            {
                if (m_rootPart == null)
                {
                    throw new NullReferenceException(
                        string.Format("[SCENE OBJECT GROUP]: Object {0} has no root part.", m_uuid));
                }

                return m_rootPart.GroupPosition;
            }
            set
            {
                LLVector3 val = value;
                if ((val.X > 257f || val.X < -1f || val.Y > 257f || val.Y < -1f) && !m_rootPart.m_IsAttachment)
                {
                    m_scene.CrossPrimGroupIntoNewRegion(val, this);
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
            get
            {
                if (m_rootPart == null)
                {
                    m_log.Error("[SCENE OBJECT GROUP]: Unable to find the rootpart for a LocalId Request!");
                    return 0;
                }

                return m_rootPart.LocalId;
            }
            set { m_rootPart.LocalId = value; }
        }

        public override LLUUID UUID
        {
            get { return m_rootPart.UUID; }
            set { m_rootPart.UUID = value; }
        }

        public LLUUID OwnerID
        {
            get
            {
                if (m_rootPart == null)
                    return LLUUID.Zero;

                return m_rootPart.OwnerID;
            }
            set { m_rootPart.OwnerID = value; }
        }

        public Color Color
        {
            get { return m_rootPart.Color; }
            set { m_rootPart.Color = value; }
        }

        public string Text
        {
            get {
                string returnstr = m_rootPart.Text;
                if (returnstr.Length  > 255)
                {
                    returnstr = returnstr.Substring(0, 255);
                }
                return returnstr;
            }
            set { m_rootPart.Text = value; }
        }

        protected virtual bool InSceneBackup
        {
            get { return true; }
        }

        public bool IsSelected
        {
            get { return m_isSelected; }
            set
            {
                m_isSelected = value;
                // Tell physics engine that group is selected
                if (m_rootPart.PhysActor != null)
                {
                    m_rootPart.PhysActor.Selected = value;
                    // Pass it on to the children.
                    foreach (SceneObjectPart child in Children.Values)
                    {
                        if (child.PhysActor != null)
                        {
                            child.PhysActor.Selected = value;
                        }
                    }
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
        /// Constructor
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
        }

        /// <summary>
        /// Create an object using serialized data in OpenSim's original xml format.
        /// </summary>
        public SceneObjectGroup(Scene scene, ulong regionHandle, string xmlData)
        {
            m_scene = scene;
            m_regionHandle = regionHandle;

            StringReader sr = new StringReader(xmlData);
            XmlTextReader reader = new XmlTextReader(sr);
            try
            {
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
                                part.LocalId = m_scene.PrimIDAllocate();
                                AddPart(part);
                                part.RegionHandle = m_regionHandle;

                                part.TrimPermissions();
                                part.StoreUndoState();
                            }
                            break;
                        case XmlNodeType.EndElement:
                            break;
                    }
                }
            }
            catch (XmlException)
            {
                m_log.ErrorFormat("[SCENE OBJECT GROUP]: Deserialization of following xml failed, {0}", xmlData);

                // Let's see if carrying on does anything for us
            }

            reader.Close();
            sr.Close();

            m_rootPart.LocalId = m_scene.PrimIDAllocate();
            m_rootPart.ParentID = 0;
            m_rootPart.RegionHandle = m_regionHandle;
            UpdateParentIDs();
        }

        /// <summary>
        /// Create an object using serialized data in OpenSim's xml2 format.
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
                            if (m_rootPart.LinkNum == 0)
                                m_rootPart.LinkNum++;
                            AddPart(Part);
                            Part.LinkNum = m_parts.Count;
                            Part.StoreUndoState();
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
            newPart.LinkNum = 0;
            m_parts.Add(newPart.UUID, newPart);
            SetPartAsRoot(newPart);
            
            // one of these is a proxy.
            if (shape.PCode != (byte)PCode.None && shape.PCode != (byte)PCode.ParticleSystem)
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

        public void SetFromAssetID(LLUUID AssetId)
        {
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    part.fromAssetID = AssetId;
                }
            }
        }

        public LLUUID GetFromAssetID()
        {
            if (m_rootPart != null)
            {
                return m_rootPart.fromAssetID;
            }
            return LLUUID.Zero;
        }

        /// <summary>
        /// Hooks this object up to the backup event so that it is persisted to the database when the update thread executes.
        /// </summary>
        public void AttachToBackup()
        {
            if (InSceneBackup)
            {
                //m_log.DebugFormat(
                //    "[SCENE OBJECT GROUP]: Attaching object {0} {1} to scene presistence sweep", Name, UUID);

                m_scene.EventManager.OnBackup += ProcessBackup;
            }
        }

        public LLVector3 GroupScale()
        {
            LLVector3 minScale = new LLVector3(Constants.RegionSize,Constants.RegionSize,Constants.RegionSize);
            LLVector3 maxScale = new LLVector3(0f,0f,0f);
            LLVector3 finalScale = new LLVector3(0.5f, 0.5f, 0.5f);

            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    LLVector3 partscale = part.Scale;
                    LLVector3 partoffset = part.OffsetPosition;

                    minScale.X = (partscale.X + partoffset.X < minScale.X) ? partscale.X + partoffset.X : minScale.X;
                    minScale.Y = (partscale.Y + partoffset.Y < minScale.Y) ? partscale.X + partoffset.Y : minScale.Y;
                    minScale.Z = (partscale.Z + partoffset.Z < minScale.Z) ? partscale.X + partoffset.Z : minScale.Z;

                    maxScale.X = (partscale.X + partoffset.X > maxScale.X) ? partscale.X + partoffset.X : maxScale.X;
                    maxScale.Y = (partscale.Y + partoffset.Y > maxScale.Y) ? partscale.Y + partoffset.Y : maxScale.Y;
                    maxScale.Z = (partscale.Z + partoffset.Z > maxScale.Z) ? partscale.Z + partoffset.Z : maxScale.Z;
                }
            }
            finalScale.X = (minScale.X > maxScale.X) ? minScale.X : maxScale.X;
            finalScale.Y = (minScale.Y > maxScale.Y) ? minScale.Y : maxScale.Y;
            finalScale.Z = (minScale.Z > maxScale.Z) ? minScale.Z : maxScale.Z;
            return finalScale;

        }
        public EntityIntersection TestIntersection(Ray hRay, bool frontFacesOnly, bool faceCenters)
        {
            // We got a request from the inner_scene to raytrace along the Ray hRay
            // We're going to check all of the prim in this group for intersection with the ray
            // If we get a result, we're going to find the closest result to the origin of the ray
            // and send back the intersection information back to the innerscene.

            EntityIntersection returnresult = new EntityIntersection();

            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    // Temporary commented to stop compiler warning
                    //Vector3 partPosition =
                    //    new Vector3(part.AbsolutePosition.X, part.AbsolutePosition.Y, part.AbsolutePosition.Z);
                    Quaternion parentrotation =
                        new Quaternion(GroupRotation.W, GroupRotation.X, GroupRotation.Y, GroupRotation.Z);

                    // Telling the prim to raytrace.
                    //EntityIntersection inter = part.TestIntersection(hRay, parentrotation);

                    EntityIntersection inter = part.TestIntersectionOBB(hRay, parentrotation,frontFacesOnly, faceCenters);

                    // This may need to be updated to the maximum draw distance possible..
                    // We might (and probably will) be checking for prim creation from other sims
                    // when the camera crosses the border.
                    float idist = Constants.RegionSize;


                    if (inter.HitTF)
                    {
                        // We need to find the closest prim to return to the testcaller along the ray
                        if (inter.distance < idist)
                        {
                            returnresult.HitTF = true;
                            returnresult.ipoint = inter.ipoint;
                            returnresult.obj = part;
                            returnresult.normal = inter.normal;
                            returnresult.distance = inter.distance;
                        }
                    }
                }
            }
            return returnresult;
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

            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    if (part.UUID != m_rootPart.UUID)
                    {
                        writer.WriteStartElement(String.Empty, "Part", String.Empty);
                        part.ToXml(writer);
                        writer.WriteEndElement();
                    }
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

            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    if (part.UUID != m_rootPart.UUID)
                    {
                        part.ToXml(writer);
                    }
                }
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="part"></param>
        private void SetPartAsRoot(SceneObjectPart part)
        {
            m_rootPart = part;
        }

        /// <summary>
        /// Attach this scene object to the given avatar.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="attachmentpoint"></param>
        /// <param name="AttachOffset"></param>
        public void AttachToAgent(LLUUID agentID, uint attachmentpoint, LLVector3 AttachOffset)
        {
            ScenePresence avatar = m_scene.GetScenePresence(agentID);
            if (avatar != null)
            {
                DetachFromBackup(this);
                m_rootPart.m_attachedAvatar = agentID;


                if (m_rootPart.PhysActor != null)
                {
                    m_scene.PhysicsScene.RemovePrim(m_rootPart.PhysActor);
                    m_rootPart.PhysActor = null;

                }

                AbsolutePosition = AttachOffset;
                m_rootPart.m_attachedPos = AttachOffset;
                m_rootPart.m_IsAttachment = true;

                m_rootPart.SetParentLocalId(avatar.LocalId);
                lock (m_parts)
                {
                    foreach (SceneObjectPart part in m_parts.Values)
                    {
                        part.SetAttachmentPoint(attachmentpoint);
                    }
                }

                avatar.AddAttachment(this);
                m_rootPart.ScheduleFullUpdate();
            }
        }
        public byte GetAttachmentPoint()
        {
            if (m_rootPart != null)
            {
                return m_rootPart.Shape.State;
            }
            return (byte)0;
        }

        public void ClearPartAttachmentData()
        {
            foreach (SceneObjectPart part in m_parts.Values)
            {
                part.SetAttachmentPoint((Byte)0);
            }
        }

        public void DetachToGround()
        {
            ScenePresence avatar = m_scene.GetScenePresence(m_rootPart.m_attachedAvatar);
            LLVector3 detachedpos = new LLVector3(127f,127f,127f);
            if (avatar != null)
            {
                detachedpos = avatar.AbsolutePosition;
                avatar.RemoveAttachment(this);
            }
            AbsolutePosition = detachedpos;
            m_rootPart.m_attachedAvatar = LLUUID.Zero;
            m_rootPart.SetParentLocalId(0);
            m_rootPart.SetAttachmentPoint((byte)0);
            m_rootPart.m_IsAttachment = false;
            m_rootPart.ApplyPhysics(m_rootPart.GetEffectiveObjectFlags(), m_scene.m_physicalPrim);
            HasGroupChanged = true;
            AttachToBackup();
            m_rootPart.ScheduleFullUpdate();
            m_rootPart.ClearUndoState();
        }
        
        public void DetachToInventoryPrep()
        {
            ScenePresence avatar = m_scene.GetScenePresence(m_rootPart.m_attachedAvatar);
            //LLVector3 detachedpos = new LLVector3(127f, 127f, 127f);
            if (avatar != null)
            {
                //detachedpos = avatar.AbsolutePosition;
                avatar.RemoveAttachment(this);
            }

            m_rootPart.m_attachedAvatar = LLUUID.Zero;
            m_rootPart.SetParentLocalId(0);
            //m_rootPart.SetAttachmentPoint((byte)0);
            m_rootPart.m_IsAttachment = false;
            AbsolutePosition = m_rootPart.m_attachedPos;
            //m_rootPart.ApplyPhysics(m_rootPart.GetEffectiveObjectFlags(), m_scene.m_physicalPrim);
            //AttachToBackup();
            //m_rootPart.ScheduleFullUpdate();

        }
        /// <summary>
        ///
        /// </summary>
        /// <param name="part"></param>
        private void SetPartAsNonRoot(SceneObjectPart part)
        {
            part.ParentID = m_rootPart.LocalId;
            part.ClearUndoState();
        }

        public override void UpdateMovement()
        {
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    part.UpdateMovement();
                }
            }
        }

        public float GetTimeDilation()
        {
            return m_scene.TimeDilation;
        }

        /// <summary>
        /// Added as a way for the storage provider to reset the scene,
        /// most likely a better way to do this sort of thing but for now...
        /// </summary>
        /// <param name="scene"></param>
        public void SetScene(Scene scene)
        {
            m_scene = scene;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="part"></param>
        public void AddPart(SceneObjectPart part)
        {
            lock (m_parts)
            {
                part.SetParent(this);

                try
                {
                    m_parts.Add(part.UUID, part);

                }
                catch (Exception e)
                {
                    m_log.Error("Failed to add scene object part", e);
                }

                part.LinkNum = m_parts.Count;

                if (part.LinkNum == 2 && RootPart != null)
                    RootPart.LinkNum = 1;
            }
        }

        /// <summary>
        /// Make sure that every non root part has the proper parent root part local id
        /// </summary>
        public void UpdateParentIDs()
        {
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    if (part.UUID != m_rootPart.UUID)
                    {
                        part.ParentID = m_rootPart.LocalId;
                    }
                }
            }
        }

        public void RegenerateFullIDs()
        {
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    part.UUID = LLUUID.Random();

                }
            }
        }
        // helper provided for parts.
        public int GetSceneMaxUndo()
        {
            if (m_scene != null)
                return m_scene.MaxUndoCount;
            return 5;
        }
        
        // justincc: I don't believe this hack is needed any longer, especially since the physics 
        // parts of set AbsolutePosition were already commented out.  By changing HasGroupChanged to false
        // this method was preventing proper reload of scene objects.
        //public void ResetChildPrimPhysicsPositions()
        //{
        //    AbsolutePosition = AbsolutePosition;
        //   HasGroupChanged = false;
        //}

        public LLUUID GetPartsFullID(uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                return part.UUID;
            }
            return null;
        }

        public void ObjectGrabHandler(uint localId, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            if (m_rootPart.LocalId == localId)
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
            part.StoreUndoState();
            part.OnGrab(offsetPos, remoteClient);

        }

        public virtual void OnGrabGroup(LLVector3 offsetPos, IClientAPI remoteClient)
        {
            m_scene.EventManager.TriggerGroupGrab(UUID, offsetPos, remoteClient.AgentId);
        }

        /// <summary>
        /// Delete this group from its scene and tell all the scene presences about that deletion.
        /// </summary>
        public void DeleteGroup()
        {
            // We need to keep track of this state in case this group is still queued for backup.            
            // FIXME: This is a poor temporary solution, since it still leaves plenty of scope for race
            // conditions where a user deletes an entity while it is being stored.  Really, the update
            // code needs a redesign.            
            m_isDeleted = true;
            
            DetachFromBackup(this);                       

            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    part.RemoveScriptInstances();
                
                    List<ScenePresence> avatars = Scene.GetScenePresences();
                    for (int i = 0; i < avatars.Count; i++)
                    {
                        if (avatars[i].ParentID == LocalId)
                        {
                            avatars[i].StandUp();
                        }

                        avatars[i].ControllingClient.SendKillObject(m_regionHandle, part.LocalId);
                    }
                }
                
                m_rootPart = null;
                m_parts.Clear();                 
            }
        }
        
        public void FakeDeleteGroup()
        {
            foreach (SceneObjectPart part in m_parts.Values)
            {
                List<ScenePresence> avatars = Scene.GetScenePresences();
                for (int i = 0; i < avatars.Count; i++)
                {
                    if (avatars[i].ParentID == LocalId)
                    {
                        avatars[i].StandUp();
                    }

                    avatars[i].ControllingClient.SendKillObject(m_regionHandle, part.LocalId);
                }
            }
        }

        public void AddScriptLPS(int count)
        {
            if (scriptScore + count >= float.MaxValue - count)
                scriptScore = 0;

            scriptScore += (float)count;
            InnerScene d = m_scene.m_innerScene;
            d.AddToScriptLPS(count);
        }

        public void AddActiveScriptCount(int count)
        {
            InnerScene d = m_scene.m_innerScene;
            d.AddActiveScripts(count);
        }

        public void aggregateScriptEvents()
        {
            uint objectflagupdate=(uint)RootPart.GetEffectiveObjectFlags();

            scriptEvents aggregateScriptEvents=0;

            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    if (part == null)
                        continue;
                    if (part != RootPart)
                        part.ObjectFlags = objectflagupdate;
                    aggregateScriptEvents |= part.m_aggregateScriptEvents;
                }
            }

            if ((aggregateScriptEvents & scriptEvents.at_target) != 0)
            {
                m_scriptListens_atTarget = true;
            }
            else
            {
                m_scriptListens_atTarget = false;
            }

            if ((aggregateScriptEvents & scriptEvents.not_at_target) != 0)
            {
                m_scriptListens_notAtTarget = true;
            }
            else
            {
                m_scriptListens_notAtTarget = false;
            }

            if (m_scriptListens_atTarget || m_scriptListens_notAtTarget)
            {
            }
            else
            {
                lock (m_targets)
                    m_targets.Clear();
            }
            
            ScheduleGroupForFullUpdate();
        }

        public override void SetText(string text, Vector3 color, double alpha)
        {
            Color = Color.FromArgb(0xff - (int) (alpha * 0xff),
                                   (int) (color.x * 0xff),
                                   (int) (color.y * 0xff),
                                   (int) (color.z * 0xff));
            Text = text;

            HasGroupChanged = true;
            m_rootPart.ScheduleFullUpdate();
        }

        /// <summary>
        /// Apply physics to this group
        /// </summary>
        /// <param name="m_physicalPrim"></param>
        public void ApplyPhysics(bool m_physicalPrim)
        {
            lock (m_parts)
            {
                if (m_parts.Count > 1)
                {
                    m_rootPart.ApplyPhysics(m_rootPart.GetEffectiveObjectFlags(), m_physicalPrim);
                    foreach (SceneObjectPart part in m_parts.Values)
                    {
                        if (part.LocalId != m_rootPart.LocalId)
                        {
                            part.ApplyPhysics(m_rootPart.GetEffectiveObjectFlags(), m_physicalPrim);
                        }
                        
                        // Hack to get the physics scene geometries in the right spot
                        //ResetChildPrimPhysicsPositions();
                    }
                }
                else
                {
                    m_rootPart.ApplyPhysics(m_rootPart.GetEffectiveObjectFlags(), m_physicalPrim);
                }
            }
        }

        public void SetOwnerId(LLUUID userId)
        {
            ForEachPart(delegate(SceneObjectPart part) { part.OwnerID = userId; });
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

        #region Events

        /// <summary>
        /// Processes backup
        /// </summary>
        /// <param name="datastore"></param>
        public void ProcessBackup(IRegionDataStore datastore)
        {            
            if (HasGroupChanged)
            {
                // don't backup while it's selected or you're asking for changes mid stream.
                if ((!IsSelected) && (RootPart != null))
                {
                    m_log.InfoFormat(
                        "[SCENE]: Storing object {0}, {1} in {2}", 
                        Name, UUID, m_scene.RegionInfo.RegionName);

                    SceneObjectGroup backup_group = Copy(OwnerID, GroupID, false);

                    datastore.StoreObject(backup_group, m_scene.RegionInfo.RegionID);
                    HasGroupChanged = false;

                    backup_group.ForEachPart(delegate(SceneObjectPart part) { part.ProcessInventoryBackup(datastore); });

                    backup_group = null;
                }
//                else
//                {
//                    m_log.DebugFormat(
//                        "[SCENE]: Did not update persistence of object {0} {1}, selected = {2}",
//                        Name, UUID, IsSelected);
//                }
            }
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
        /// Send a full update to the client for the given part
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="part"></param>
        internal void SendPartFullUpdate(IClientAPI remoteClient, SceneObjectPart part, uint clientFlags)
        {
            if (m_rootPart.UUID == part.UUID)
            {
                if (m_rootPart.m_IsAttachment)
                {
                    part.SendFullUpdateToClient(remoteClient, m_rootPart.m_attachedPos, clientFlags);
                }
                else
                {
                    part.SendFullUpdateToClient(remoteClient, AbsolutePosition, clientFlags);
                }
            }
            else
            {
                part.SendFullUpdateToClient(remoteClient, clientFlags);
            }
        }

        /// <summary>
        /// Send a terse update to the client for the given part
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="part"></param>
        internal void SendPartTerseUpdate(IClientAPI remoteClient, SceneObjectPart part)
        {
            if (m_rootPart.UUID == part.UUID)
            {
                if (m_rootPart.m_IsAttachment)
                {
                    part.SendTerseUpdateToClient(remoteClient, m_rootPart.m_attachedPos);
                }
                else
                {
                    part.SendTerseUpdateToClient(remoteClient, AbsolutePosition);
                }
            }
            else
            {
                part.SendTerseUpdateToClient(remoteClient);
            }
        }

        #endregion

        #region Copying

        /// <summary>
        /// Duplicates this object, including operations such as physics set up and attaching to the backup event.
        /// </summary>
        /// <returns></returns>
        public SceneObjectGroup Copy(LLUUID cAgentID, LLUUID cGroupID, bool userExposed)
        {
            SceneObjectGroup dupe = (SceneObjectGroup) MemberwiseClone();
            dupe.m_parts = new Dictionary<LLUUID, SceneObjectPart>();
            dupe.m_parts.Clear();
            //dupe.OwnerID = AgentID;
            //dupe.GroupID = GroupID;
            dupe.AbsolutePosition = new LLVector3(AbsolutePosition.X, AbsolutePosition.Y, AbsolutePosition.Z);
            dupe.m_scene = m_scene;
            dupe.m_regionHandle = m_regionHandle;

            dupe.CopyRootPart(m_rootPart, OwnerID, GroupID, userExposed);

            if (userExposed)
                dupe.m_rootPart.TrimPermissions();

            /// may need to create a new Physics actor.
            if (dupe.RootPart.PhysActor != null && userExposed)
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

                dupe.RootPart.PhysActor.LocalID = dupe.RootPart.LocalId;

                dupe.RootPart.DoPhysicsPropertyUpdate(dupe.RootPart.PhysActor.IsPhysical, true);
            }
            
            // Now we've made a copy that replaces this one, we need to
            // switch the owner to the person who did the copying
            // Second Life copies an object and duplicates the first one in it's place
            // So, we have to make a copy of this one, set it in it's place then set the owner on this one
            if (userExposed)
            {
                SetRootPartOwner(m_rootPart, cAgentID, cGroupID);
                m_rootPart.ScheduleFullUpdate();
            }

            List<SceneObjectPart> partList = new List<SceneObjectPart>(m_parts.Values);
            foreach (SceneObjectPart part in partList)
            {
                if (part.UUID != m_rootPart.UUID)
                {
                    dupe.CopyPart(part, OwnerID, GroupID, userExposed);

                    if (userExposed)
                    {
                        SetPartOwner(part, cAgentID, cGroupID);
                        part.ScheduleFullUpdate();
                    }
                }
            }

            if (userExposed)
            {
                dupe.UpdateParentIDs();
                dupe.HasGroupChanged = true;
                dupe.AttachToBackup();
                
                ScheduleGroupForFullUpdate();
            }

            return dupe;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="part"></param>
        /// <param name="cAgentID"></param>
        /// <param name="cGroupID"></param>
        public void CopyRootPart(SceneObjectPart part, LLUUID cAgentID, LLUUID cGroupID, bool userExposed)
        {
            SceneObjectPart newPart = part.Copy(m_scene.PrimIDAllocate(), OwnerID, GroupID, m_parts.Count, userExposed);
            newPart.SetParent(this);

            lock (m_parts)
            {
                m_parts.Add(newPart.UUID, newPart);
            }

            SetPartAsRoot(newPart);
        }

        public void ScriptSetPhysicsStatus(bool UsePhysics)
        {
            if (m_scene.m_physicalPrim)
            {
                lock (m_parts)
                {
                    foreach (SceneObjectPart part in m_parts.Values)
                    {
                        if (UsePhysics)
                            part.AddFlag(LLObject.ObjectFlags.Physics);
                        else
                            part.RemFlag(LLObject.ObjectFlags.Physics);

                        part.DoPhysicsPropertyUpdate(UsePhysics, false);
                        IsSelected = false;
                    }
                }
            }
        }

        public void ScriptSetPhantomStatus(bool PhantomStatus)
        {
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    if (PhantomStatus)
                    {
                        part.AddFlag(LLObject.ObjectFlags.Phantom);
                        if (part.PhysActor != null)
                        {
                            m_scene.PhysicsScene.RemovePrim(part.PhysActor);
                        }
                    }
                    else
                    {
                        part.RemFlag(LLObject.ObjectFlags.Phantom);
                        if ((part.GetEffectiveObjectFlags() & (int) LLObject.ObjectFlags.Physics) != 0)
                        {
                            part.DoPhysicsPropertyUpdate(true, false);
                        }
                    }
                }
            }
        }

        public void applyImpulse(PhysicsVector impulse)
        {
            // We check if rootpart is null here because scripts don't delete if you delete the host.
            // This means that unfortunately, we can pass a null physics actor to Simulate!
            // Make sure we don't do that!
            SceneObjectPart rootpart = m_rootPart;
            if (rootpart != null)
            {
                if (rootpart.PhysActor != null)
                {
                    if (rootpart.m_IsAttachment)
                    {
                        ScenePresence avatar = m_scene.GetScenePresence(rootpart.m_attachedAvatar);
                        if (avatar != null)
                        {
                            avatar.PushForce(impulse);
                        }
                    }
                    else
                    {
                        rootpart.PhysActor.AddForce(impulse,true);
                        m_scene.PhysicsScene.AddPhysicsActorTaint(rootpart.PhysActor);
                    }
                }
            }
        }

        public void moveToTarget(LLVector3 target, float tau)
        {
            SceneObjectPart rootpart = m_rootPart;
            if (rootpart != null)
            {
                if (rootpart.PhysActor != null)
                {
                    rootpart.PhysActor.PIDTarget = new PhysicsVector(target.X, target.Y, target.Z);
                    rootpart.PhysActor.PIDTau = tau;
                    rootpart.PhysActor.PIDActive = true;
                }
            }
        }

        public void stopMoveToTarget()
        {
            SceneObjectPart rootpart = m_rootPart;
            if (rootpart != null)
            {
                rootpart.PhysActor.PIDActive = false;
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
                if (!m_scene.ExternalChecks.ExternalChecksBypassPermissions())
                    ApplyNextOwnerPermissions();
            }

            part.ScheduleFullUpdate();
        }

        /// <summary>
        /// Make a copy of the given part.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="cAgentID"></param>
        /// <param name="cGroupID"></param>
        public void CopyPart(SceneObjectPart part, LLUUID cAgentID, LLUUID cGroupID, bool userExposed)
        {
            SceneObjectPart newPart = part.Copy(m_scene.PrimIDAllocate(), OwnerID, GroupID, m_parts.Count, userExposed);
            newPart.SetParent(this);

            lock (m_parts)
            {
                m_parts.Add(newPart.UUID, newPart);
            }

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
            // As this is only ever called for prims which are not currently part of the scene (and hence
            // not accessible by clients), there should be no need to lock
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

            remoteClient.SendObjectPropertiesFamilyData(RequestFlags, RootPart.UUID, RootPart.ObjectOwner, RootPart.GroupID, RootPart.BaseMask,
                                                        RootPart.OwnerMask, RootPart.GroupMask, RootPart.EveryoneMask, RootPart.NextOwnerMask,
                                                        RootPart.OwnershipCost, RootPart.ObjectSaleType, RootPart.SalePrice, RootPart.Category,
                                                        RootPart.CreatorID, RootPart.Name, RootPart.Description);
        }

        public void SetPartOwner(SceneObjectPart part, LLUUID cAgentID, LLUUID cGroupID)
        {
            part.OwnerID = cAgentID;
            part.GroupID = cGroupID;
        }

        #endregion

        #region Scheduling

        /// <summary>
        /// Examine this object's parts to see if they've changed sufficiently to warrant an update
        /// </summary>
        public override void Update()
        {

            lock (m_parts)
            {
                //if (m_rootPart.m_IsAttachment)
                //{
                    //foreach (SceneObjectPart part in m_parts.Values)
                    //{
                        //part.SendScheduledUpdates();
                    //}
                    //return;
                //}

                if (Util.GetDistanceTo(lastPhysGroupPos, AbsolutePosition) > 0.02)
                {
                    m_rootPart.UpdateFlag = 1;
                    lastPhysGroupPos = AbsolutePosition;
                }
                    //foreach (SceneObjectPart part in m_parts.Values)
                    //{
                        //if (part.UpdateFlag == 0) part.UpdateFlag = 1;
                    //}



                    checkAtTargets();


                if ((Math.Abs(lastPhysGroupRot.W - GroupRotation.W) > 0.1)
                    || (Math.Abs(lastPhysGroupRot.X - GroupRotation.X) > 0.1)
                    || (Math.Abs(lastPhysGroupRot.Y - GroupRotation.Y) > 0.1)
                    || (Math.Abs(lastPhysGroupRot.Z - GroupRotation.Z) > 0.1))
                {
                    m_rootPart.UpdateFlag = 1;

                    lastPhysGroupRot = GroupRotation;
                }

                foreach (SceneObjectPart part in m_parts.Values)
                {
                    part.SendScheduledUpdates();
                }
            }
        }

        public void ScheduleFullUpdateToAvatar(ScenePresence presence)
        {
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    part.AddFullUpdateToAvatar(presence);
                }
            }
        }

        public void ScheduleTerseUpdateToAvatar(ScenePresence presence)
        {
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    part.AddTerseUpdateToAvatar(presence);
                }
            }
        }

        /// <summary>
        /// Schedule a full update for this scene object
        /// </summary>
        public void ScheduleGroupForFullUpdate()
        {
            checkAtTargets();
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    part.ScheduleFullUpdate();
                }
            }
        }

        /// <summary>
        /// Schedule a terse update for this scene object
        /// </summary>
        public void ScheduleGroupForTerseUpdate()
        {
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    part.ScheduleTerseUpdate();
                }
            }
        }

        /// <summary>
        /// Immediately send a full update for this scene object.
        /// </summary>
        public void SendGroupFullUpdate()
        {
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    part.SendFullUpdateToAllClients();
                }
            }
        }

        public void QueueForUpdateCheck()
        {
            m_scene.m_innerScene.AddToUpdateList(this);
        }

        /// <summary>
        /// Immediately send a terse update for this scene object.
        /// </summary>
        public void SendGroupTerseUpdate()
        {
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    part.SendTerseUpdateToAllClients();
                }
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
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    if (part.LinkNum == linknum)
                    {
                        return part;
                    }
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
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    if (part.LocalId == localID)
                    {
                        return part;
                    }
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
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    if (part.LocalId == localID)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        #endregion

        #region Packet Handlers

        /// <summary>
        /// Link the prims in a given group to this group
        /// </summary>
        /// <param name="objectGroup">The group of prims which should be linked to this group</param>
        public void LinkToGroup(SceneObjectGroup objectGroup)
        {
            if (objectGroup.RootPart.UpdateFlag > 0)
            {
                // I've never actually seen this happen, though I think it's theoretically possible
                m_log.WarnFormat(
                    "[SCENE OBJECT GROUP]: Aborted linking {0}, {1} to {2}, {3} as it has yet to finish delinking",
                    objectGroup.RootPart.Name, objectGroup.RootPart.UUID, RootPart.Name, RootPart.UUID);

                return;
            }

//            m_log.DebugFormat(
//                "[SCENE OBJECT GROUP]: Linking group with root part {0}, {1} to group with root part {2}, {3}",
//                objectGroup.RootPart.Name, objectGroup.RootPart.UUID, RootPart.Name, RootPart.UUID);

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
            axPos = parentRot.Inverse() * axPos;

            linkPart.OffsetPosition = new LLVector3(axPos.x, axPos.y, axPos.z);
            Quaternion oldRot =
                new Quaternion(linkPart.RotationOffset.W, linkPart.RotationOffset.X, linkPart.RotationOffset.Y,
                               linkPart.RotationOffset.Z);
            Quaternion newRot = parentRot.Inverse() * oldRot;
            linkPart.RotationOffset = new LLQuaternion(newRot.x, newRot.y, newRot.z, newRot.w);

            linkPart.ParentID = m_rootPart.LocalId;
            if (m_rootPart.LinkNum == 0)
                m_rootPart.LinkNum = 1;

            lock (m_parts)
            {
                m_parts.Add(linkPart.UUID, linkPart);
            }

            linkPart.LinkNum = m_parts.Count;

            linkPart.SetParent(this);
           linkPart.AddFlag(LLObject.ObjectFlags.CreateSelected);

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
                part.ClearUndoState();
            }

            m_scene.UnlinkSceneObject(objectGroup.UUID, true);

            // TODO Deleting the original group object may cause problems later on if they have already
            // made it into the update queue.  However, sending out updates for those parts is now
            // spurious, so it would be good not to send them at some point.
            // The traffic caused is always going to be pretty minor, so it's not high priority
            //objectGroup.DeleteGroup();

            HasGroupChanged = true;
            ScheduleGroupForFullUpdate();
        }

        /// <summary>
        /// Delink the given prim from this group.  The delinked prim is established as
        /// an independent SceneObjectGroup.
        /// </summary>
        /// <param name="partID"></param>
        public void DelinkFromGroup(uint partID)
        {
            DelinkFromGroup(partID, true);
        }

        public void DelinkFromGroup(uint partID, bool sendEvents)
        {
            SceneObjectPart linkPart = GetChildPart(partID);

            if (null != linkPart)
            {
                linkPart.ClearUndoState();
//                m_log.DebugFormat(
//                    "[SCENE OBJECT GROUP]: Delinking part {0}, {1} from group with root part {2}, {3}",
//                    linkPart.Name, linkPart.UUID, RootPart.Name, RootPart.UUID);

                LLQuaternion worldRot = linkPart.GetWorldRotation();

                // Remove the part from this object
                lock (m_parts)
                {
                    m_parts.Remove(linkPart.UUID);
                }

                if (m_parts.Count == 1 && RootPart != null) //Single prim is left
                    RootPart.LinkNum = 0;
                else
                {
                    foreach (SceneObjectPart p in m_parts.Values)
                    {
                        if (p.LinkNum > linkPart.LinkNum)
                            p.LinkNum--;
                    }
                }

                linkPart.ParentID = 0;
                linkPart.LinkNum = 0;

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

                axPos = parentRot * axPos;
                linkPart.OffsetPosition = new LLVector3(axPos.x, axPos.y, axPos.z);
                linkPart.GroupPosition = AbsolutePosition + linkPart.OffsetPosition;
                linkPart.OffsetPosition = new LLVector3(0, 0, 0);

                linkPart.RotationOffset = worldRot;

                SceneObjectGroup objectGroup = new SceneObjectGroup(m_scene, m_regionHandle, linkPart);

                m_scene.AddNewSceneObject(objectGroup, true);

                if (sendEvents)
                    linkPart.TriggerScriptChangedEvent(Changed.LINK);

                HasGroupChanged = true;
                ScheduleGroupForFullUpdate();
            }
            else
            {
                m_log.InfoFormat("[SCENE OBJECT GROUP]: " +
                                 "DelinkFromGroup(): Child prim {0} not found in object {1}, {2}",
                                 partID, LocalId, UUID);
            }
        }

        /// <summary>
        /// Stop this object from being persisted over server restarts.
        /// </summary>
        /// <param name="objectGroup"></param>
        public void DetachFromBackup(SceneObjectGroup objectGroup)
        {
            m_scene.EventManager.OnBackup -= objectGroup.ProcessBackup;
        }

        private void LinkNonRootPart(SceneObjectPart part, Vector3 oldGroupPosition, Quaternion oldGroupRotation)
        {
            part.SetParent(this);
            part.ParentID = m_rootPart.LocalId;

            lock (m_parts)
            {
                m_parts.Add(part.UUID, part);
            }

            part.LinkNum = m_parts.Count;

            Vector3 axiomOldPos = new Vector3(part.OffsetPosition.X, part.OffsetPosition.Y, part.OffsetPosition.Z);
            axiomOldPos = oldGroupRotation * axiomOldPos;
            axiomOldPos += oldGroupPosition;
            LLVector3 oldAbsolutePosition = new LLVector3(axiomOldPos.x, axiomOldPos.y, axiomOldPos.z);
            part.OffsetPosition = oldAbsolutePosition - AbsolutePosition;

            Quaternion axiomRootRotation =
                new Quaternion(m_rootPart.RotationOffset.W, m_rootPart.RotationOffset.X, m_rootPart.RotationOffset.Y,
                               m_rootPart.RotationOffset.Z);

            Vector3 axiomPos = new Vector3(part.OffsetPosition.X, part.OffsetPosition.Y, part.OffsetPosition.Z);
            axiomPos = axiomRootRotation.Inverse() * axiomPos;
            part.OffsetPosition = new LLVector3(axiomPos.x, axiomPos.y, axiomPos.z);

            Quaternion axiomPartRotation =
                new Quaternion(part.RotationOffset.W, part.RotationOffset.X, part.RotationOffset.Y,
                               part.RotationOffset.Z);

            axiomPartRotation = oldGroupRotation * axiomPartRotation;
            axiomPartRotation = axiomRootRotation.Inverse() * axiomPartRotation;
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
                        m_rootPart.PhysActor.AddForce(grabforce,true);
                        m_scene.PhysicsScene.AddPhysicsActorTaint(m_rootPart.PhysActor);
                    }
                    else
                    {
                        //NonPhysicalGrabMovement(pos);
                    }
                }
                else
                {
                    //NonPhysicalGrabMovement(pos);
                }
            }
        }

        public void NonPhysicalGrabMovement(LLVector3 pos)
        {
            AbsolutePosition = pos;
            m_rootPart.SendTerseUpdateToAllClients();
        }

        /// <summary>
        /// Return metadata about a prim (name, description, sale price, etc.)
        /// </summary>
        /// <param name="client"></param>
        public void GetProperties(IClientAPI client)
        {
            m_rootPart.GetProperties(client);
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
                part.SetText(text);
            }
        }

        public void SetPartText(string text, LLUUID partID)
        {
            SceneObjectPart part = GetChildPart(partID);
            if (part != null)
            {
                part.SetText(text);
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
        /// Update prim flags for this group.
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="type"></param>
        /// <param name="inUse"></param>
        /// <param name="data"></param>
        public void UpdatePrimFlags(uint localID, ushort type, bool inUse, byte[] data)
        {
            SceneObjectPart selectionPart = GetChildPart(localID);
            
            if (selectionPart != null)
            {
                lock (m_parts)
                {
                    foreach (SceneObjectPart part in m_parts.Values)
                    {
                        if (part.Scale.X > 10.0 || part.Scale.Y > 10.0 || part.Scale.Z > 10.0)
                        {
                            data[47] = 0; // Reset physics
                            break;
                        }
                    }

                    foreach (SceneObjectPart part in m_parts.Values)
                    {
                        part.UpdatePrimFlags(type, inUse, data);
                    }
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
        /// Get the parts of this scene object
        /// </summary>
        /// <returns></returns>
        public SceneObjectPart[] GetParts()
        {
            int numParts = Children.Count;
            SceneObjectPart[] partArray = new SceneObjectPart[numParts];
            Children.Values.CopyTo(partArray, 0);
            return partArray;
        }

        /// <summary>
        /// Update the texture entry for this part
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
            HasGroupChanged = true;
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

                if (part.PhysActor != null)
                    m_scene.PhysicsScene.AddPhysicsActorTaint(part.PhysActor);
            }
        }

        #endregion

        #region Resize

        /// <summary>
        /// Resize the given part
        /// </summary>
        /// <param name="scale"></param>
        /// <param name="localID"></param>
        public void Resize(LLVector3 scale, uint localID)
        {
            if (scale.X > m_scene.m_maxNonphys)
                scale.X = m_scene.m_maxNonphys;
            if (scale.Y > m_scene.m_maxNonphys)
                scale.Y = m_scene.m_maxNonphys;
            if (scale.Z > m_scene.m_maxNonphys)
                scale.Z = m_scene.m_maxNonphys;

            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                part.Resize(scale);
                if (part.PhysActor != null)
                {
                    if (part.PhysActor.IsPhysical)
                    {
                        if (scale.X > m_scene.m_maxPhys)
                            scale.X = m_scene.m_maxPhys;
                        if (scale.Y > m_scene.m_maxPhys)
                            scale.Y = m_scene.m_maxPhys;
                        if (scale.Z > m_scene.m_maxPhys)
                            scale.Z = m_scene.m_maxPhys;
                    }
                    part.PhysActor.Size =
                        new PhysicsVector(scale.X, scale.Y, scale.Z);
                    m_scene.PhysicsScene.AddPhysicsActorTaint(part.PhysActor);
                }
                //if (part.UUID != m_rootPart.UUID)
                
                HasGroupChanged = true;
                ScheduleGroupForFullUpdate();

                //if (part.UUID == m_rootPart.UUID)
                //{
                //if (m_rootPart.PhysActor != null)
                //{
                //m_rootPart.PhysActor.Size =
                //new PhysicsVector(m_rootPart.Scale.X, m_rootPart.Scale.Y, m_rootPart.Scale.Z);
                //m_scene.PhysicsScene.AddPhysicsActorTaint(m_rootPart.PhysActor);
                //}
                //}
            }
        }

        public void GroupResize(LLVector3 scale, uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                if (scale.X > m_scene.m_maxNonphys)
                    scale.X = m_scene.m_maxNonphys;
                if (scale.Y > m_scene.m_maxNonphys)
                    scale.Y = m_scene.m_maxNonphys;
                if (scale.Z > m_scene.m_maxNonphys)
                    scale.Z = m_scene.m_maxNonphys;
                if (part.PhysActor != null && part.PhysActor.IsPhysical)
                {
                    if (scale.X > m_scene.m_maxPhys)
                        scale.X = m_scene.m_maxPhys;
                    if (scale.Y > m_scene.m_maxPhys)
                        scale.Y = m_scene.m_maxPhys;
                    if (scale.Z > m_scene.m_maxPhys)
                        scale.Z = m_scene.m_maxPhys;
                }
                float x = (scale.X / part.Scale.X);
                float y = (scale.Y / part.Scale.Y);
                float z = (scale.Z / part.Scale.Z);

                lock (m_parts)
                {
                    if (x > 1.0f || y > 1.0f || z > 1.0f)
                    {
                        foreach (SceneObjectPart obPart in m_parts.Values)
                        {
                            if (obPart.UUID != m_rootPart.UUID)
                            {
                                LLVector3 oldSize = new LLVector3(obPart.Scale);

                                float f = 1.0f;
                                float a = 1.0f;

                                if (part.PhysActor != null && part.PhysActor.IsPhysical)
                                {
                                    if (oldSize.X*x > m_scene.m_maxPhys)
                                    {
                                        f = m_scene.m_maxPhys / oldSize.X;
                                        a = f / x;
                                        x *= a;
                                        y *= a;
                                        z *= a;
                                    }
                                    if (oldSize.Y*y > m_scene.m_maxPhys)
                                    {
                                        f = m_scene.m_maxPhys / oldSize.Y;
                                        a = f / y;
                                        x *= a;
                                        y *= a;
                                        z *= a;
                                    }
                                    if (oldSize.Z*z > m_scene.m_maxPhys)
                                    {
                                        f = m_scene.m_maxPhys / oldSize.Z;
                                        a = f / z;
                                        x *= a;
                                        y *= a;
                                        z *= a;
                                    }
                                }
                                else
                                {
                                    if (oldSize.X*x > m_scene.m_maxNonphys)
                                    {
                                        f = m_scene.m_maxNonphys / oldSize.X;
                                        a = f / x;
                                        x *= a;
                                        y *= a;
                                        z *= a;
                                    }
                                    if (oldSize.Y*y > m_scene.m_maxNonphys)
                                    {
                                        f = m_scene.m_maxNonphys / oldSize.Y;
                                        a = f / y;
                                        x *= a;
                                        y *= a;
                                        z *= a;
                                    }
                                    if (oldSize.Z*z > m_scene.m_maxNonphys)
                                    {
                                        f = m_scene.m_maxNonphys / oldSize.Z;
                                        a = f / z;
                                        x *= a;
                                        y *= a;
                                        z *= a;
                                    }
                                }
                            }
                        }
                    }
                }

                LLVector3 prevScale = part.Scale;
                prevScale.X *= x;
                prevScale.Y *= y;
                prevScale.Z *= z;
                part.Resize(prevScale);

                lock (m_parts)
                {
                    foreach (SceneObjectPart obPart in m_parts.Values)
                    {
                        if (obPart.UUID != m_rootPart.UUID)
                        {
                            LLVector3 currentpos = new LLVector3(obPart.OffsetPosition);
                            currentpos.X *= x;
                            currentpos.Y *= y;
                            currentpos.Z *= z;
                            LLVector3 newSize = new LLVector3(obPart.Scale);
                            newSize.X *= x;
                            newSize.Y *= y;
                            newSize.Z *= z;
                            obPart.Resize(newSize);
                            obPart.UpdateOffSet(currentpos);
                        }
                    }
                }

                if (part.PhysActor != null)
                {
                    part.PhysActor.Size =
                        new PhysicsVector(prevScale.X, prevScale.Y, prevScale.Z);
                    m_scene.PhysicsScene.AddPhysicsActorTaint(part.PhysActor);
                }

                HasGroupChanged = true;
                ScheduleGroupForTerseUpdate();
            }
        }

        #endregion

        #region Position

        /// <summary>
        /// Move this scene object
        /// </summary>
        /// <param name="pos"></param>
        public void UpdateGroupPosition(LLVector3 pos)
        {
            if (m_scene.EventManager.TriggerGroupMove(UUID, pos))
            {
                if (m_rootPart.m_IsAttachment)
                {
                    m_rootPart.m_attachedPos = pos;
                }

                AbsolutePosition = pos;
                
                HasGroupChanged = true;
            }
            
            //we need to do a terse update even if the move wasn't allowed
            // so that the position is reset in the client (the object snaps back)
            ScheduleGroupForTerseUpdate();
        }

        /// <summary>
        /// Update the position of a single part of this scene object
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
                
                HasGroupChanged = true;
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
            axDiff = partRotation.Inverse() * axDiff;
            diff.X = axDiff.x;
            diff.Y = axDiff.y;
            diff.Z = axDiff.z;

            lock (m_parts)
            {
                foreach (SceneObjectPart obPart in m_parts.Values)
                {
                    if (obPart.UUID != m_rootPart.UUID)
                    {
                        obPart.OffsetPosition = obPart.OffsetPosition + diff;
                    }
                }
            }

            AbsolutePosition = newPos;
            
            HasGroupChanged = true;
            ScheduleGroupForTerseUpdate();
        }

        public void OffsetForNewRegion(LLVector3 offset)
        {
            m_rootPart.GroupPosition = offset;
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
            
            HasGroupChanged = true;
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
            
            HasGroupChanged = true;
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

            lock (m_parts)
            {
                foreach (SceneObjectPart prim in m_parts.Values)
                {
                    if (prim.UUID != m_rootPart.UUID)
                    {
                        Vector3 axPos = new Vector3(prim.OffsetPosition.X, prim.OffsetPosition.Y, prim.OffsetPosition.Z);
                        axPos = oldParentRot * axPos;
                        axPos = axRot.Inverse() * axPos;
                        prim.OffsetPosition = new LLVector3(axPos.x, axPos.y, axPos.z);
                        Quaternion primsRot =
                            new Quaternion(prim.RotationOffset.W, prim.RotationOffset.X, prim.RotationOffset.Y,
                                           prim.RotationOffset.Z);
                        Quaternion newRot = oldParentRot * primsRot;
                        newRot = axRot.Inverse() * newRot;
                        prim.RotationOffset = new LLQuaternion(newRot.x, newRot.y, newRot.z, newRot.w);
                        prim.ScheduleTerseUpdate();
                    }
                }
            }

            m_rootPart.ScheduleTerseUpdate();
        }

        #endregion

        internal void SetAxisRotation(int axis, int rotate10)
        {
            bool setX = false;
            bool setY = false;
            bool setZ = false;

            int xaxis = 2;
            int yaxis = 4;
            int zaxis = 8;

            if (m_rootPart != null)
            {
                setX = ((axis & xaxis) != 0) ? true : false;
                setY = ((axis & yaxis) != 0) ? true : false;
                setZ = ((axis & zaxis) != 0) ? true : false;

                float setval = (rotate10 > 0) ? 1f : 0f;

                if (setX)
                    m_rootPart.m_rotationAxis.X = setval;
                if (setY)
                    m_rootPart.m_rotationAxis.Y = setval;
                if (setZ)
                    m_rootPart.m_rotationAxis.Z = setval;

                if (setX || setY || setZ)
                {
                    m_rootPart.SetPhysicsAxisRotation();
                }

            }
        }

        public int registerTargetWaypoint(LLVector3 target, float tolerance)
        {
            scriptPosTarget waypoint = new scriptPosTarget();
            waypoint.targetPos = target;
            waypoint.tolerance = tolerance;
            uint handle = m_scene.PrimIDAllocate();
            lock (m_targets)
            {
                m_targets.Add(handle, waypoint);
            }
            return (int)handle;
        }
        public void unregisterTargetWaypoint(int handle)
        {
            lock (m_targets)
            {
                if (m_targets.ContainsKey((uint)handle))
                    m_targets.Remove((uint)handle);
            }
        }

        private void checkAtTargets()
        {
            if (m_scriptListens_atTarget || m_scriptListens_notAtTarget)
            {
                if (m_targets.Count > 0)
                {
                    bool at_target = false;
                    //LLVector3 targetPos;
                    //uint targetHandle;
                    Dictionary<uint, scriptPosTarget> atTargets = new Dictionary<uint, scriptPosTarget>();
                    lock (m_targets)
                    {
                        foreach (uint idx in m_targets.Keys)
                        {
                            scriptPosTarget target = m_targets[idx];
                            if (Util.GetDistanceTo(target.targetPos, m_rootPart.GroupPosition) <= target.tolerance)
                            {
                                // trigger at_target
                                if (m_scriptListens_atTarget)
                                {
                                    // Reusing att.tolerance to hold the index of the target in the targets dictionary
                                    // to avoid deadlocking the sim.
                                    at_target = true;
                                    scriptPosTarget att = new scriptPosTarget();
                                    att.targetPos = target.targetPos;
                                    att.tolerance = (float)idx;
                                    atTargets.Add(idx, att);
                                }
                            }
                        }
                    }
                    if (atTargets.Count > 0)
                    {
                        uint[] localids = new uint[0];
                        lock (m_parts)
                        {
                            localids = new uint[m_parts.Count];
                            int cntr = 0;
                            foreach (SceneObjectPart part in m_parts.Values)
                            {
                                localids[cntr] = part.LocalId;
                                cntr++;
                            }
                        }
                        for (int ctr = 0; ctr < localids.Length; ctr++)
                        {
                            foreach (uint target in atTargets.Keys)
                            {
                                scriptPosTarget att = atTargets[target];
                                // Reusing att.tolerance to hold the index of the target in the targets dictionary
                                // to avoid deadlocking the sim.
                                m_scene.TriggerAtTargetEvent(localids[ctr], (uint)att.tolerance, att.targetPos, m_rootPart.GroupPosition);


                            }
                        }
                        return;
                    }
                    if (m_scriptListens_notAtTarget && !at_target)
                    {
                        //trigger not_at_target
                        uint[] localids = new uint[0];
                        lock (m_parts)
                        {
                            localids = new uint[m_parts.Count];
                            int cntr = 0;
                            foreach (SceneObjectPart part in m_parts.Values)
                            {
                                localids[cntr] = part.LocalId;
                                cntr++;
                            }
                        }
                        for (int ctr = 0; ctr < localids.Length; ctr++)
                        {
                            m_scene.TriggerNotAtTargetEvent(localids[ctr]);
                        }
                    }
                }
            }
        }
        public float GetMass()
        {
            float retmass = 0f;
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    retmass += part.GetMass();
                }
            }
            return retmass;
        }
        public void CheckSculptAndLoad()
        {
            lock (m_parts)
            {
                if (RootPart != null)
                {
                    if ((RootPart.GetEffectiveObjectFlags() & (uint)LLObject.ObjectFlags.Phantom) == 0)
                    {
                        foreach (SceneObjectPart part in m_parts.Values)
                        {
                            if (part.Shape.SculptEntry && part.Shape.SculptTexture != LLUUID.Zero)
                            {
                                m_scene.AssetCache.GetAsset(part.Shape.SculptTexture, part.SculptTextureCallback, true);
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Set the user group to which this scene object belongs.
        /// </summary>
        /// <param name="GroupID"></param>
        /// <param name="client"></param>
        public void SetGroup(LLUUID GroupID, IClientAPI client)
        {
            lock (m_parts)
            {
                foreach (SceneObjectPart part in m_parts.Values)
                {
                    part.SetGroup(GroupID, client);
                }
                
                HasGroupChanged = true;
            }
            
            ScheduleGroupForFullUpdate();
        }

        public void TriggerScriptChangedEvent(Changed val)
        {
            foreach (SceneObjectPart part in Children.Values)
            {
                part.TriggerScriptChangedEvent(val);
            }
        }
    }
}
