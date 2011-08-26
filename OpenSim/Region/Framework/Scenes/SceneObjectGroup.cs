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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.Framework.Scenes.Serialization;

namespace OpenSim.Region.Framework.Scenes
{
    [Flags]
    public enum scriptEvents
    {
        None = 0,
        attach = 1,
        collision = 16,
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
        at_rot_target = 16777216,
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
        public Vector3 targetPos;
        public float tolerance;
        public uint handle;
    }

    struct scriptRotTarget
    {
        public Quaternion targetRot;
        public float tolerance;
        public uint handle;
    }

    public delegate void PrimCountTaintedDelegate();

    /// <summary>
    /// A scene object group is conceptually an object in the scene.  The object is constituted of SceneObjectParts
    /// (often known as prims), one of which is considered the root part.
    /// </summary>
    public partial class SceneObjectGroup : EntityBase, ISceneObject
    {
        // private PrimCountTaintedDelegate handlerPrimCountTainted = null;

        /// <summary>
        /// Signal whether the non-inventory attributes of any prims in the group have changed
        /// since the group's last persistent backup
        /// </summary>
        private bool m_hasGroupChanged = false;
        private long timeFirstChanged;
        private long timeLastChanged;

        public bool HasGroupChanged
        {
            set
            {
                if (value)
                {
                    timeLastChanged = DateTime.Now.Ticks;
                    if (!m_hasGroupChanged)
                        timeFirstChanged = DateTime.Now.Ticks;
                }
                m_hasGroupChanged = value;
                
//                m_log.DebugFormat(
//                    "[SCENE OBJECT GROUP]: HasGroupChanged set to {0} for {1} {2}", m_hasGroupChanged, Name, LocalId);
            }

            get { return m_hasGroupChanged; }
        }
        
        /// <summary>
        /// Has the group changed due to an unlink operation?  We record this in order to optimize deletion, since
        /// an unlinked group currently has to be persisted to the database before we can perform an unlink operation.
        /// </summary>
        public bool HasGroupChangedDueToDelink { get; private set; }

        private bool isTimeToPersist()
        {
            if (IsSelected || IsDeleted || IsAttachment)
                return false;
            if (!m_hasGroupChanged)
                return false;
            if (m_scene.ShuttingDown)
                return true;
            long currentTime = DateTime.Now.Ticks;
            if (currentTime - timeLastChanged > m_scene.m_dontPersistBefore || currentTime - timeFirstChanged > m_scene.m_persistAfter)
                return true;
            return false;
        }

        /// <summary>
        /// Is this scene object acting as an attachment?
        /// </summary>
        public bool IsAttachment { get; set; }

        /// <summary>
        /// The avatar to which this scene object is attached.
        /// </summary>
        /// <remarks>
        /// If we're not attached to an avatar then this is UUID.Zero
        /// </remarks>
        public UUID AttachedAvatar { get; set; }

        /// <summary>
        /// Attachment point of this scene object to an avatar.
        /// </summary>
        /// <remarks>
        /// 0 if we're not attached to anything
        /// </remarks>
        public uint AttachmentPoint
        {
            get
            {
                return m_rootPart.Shape.State;
            }

            set
            {
                IsAttachment = value != 0;
                m_rootPart.Shape.State = (byte)value;
            }
        }

        public void ClearPartAttachmentData()
        {
            AttachmentPoint = 0;
        }

        /// <summary>
        /// Is this scene object phantom?
        /// </summary>
        /// <remarks>
        /// Updating must currently take place through UpdatePrimFlags()
        /// </remarks>
        public bool IsPhantom
        {
            get { return (RootPart.Flags & PrimFlags.Phantom) != 0; }
        }

        /// <summary>
        /// Does this scene object use physics?
        /// </summary>
        /// <remarks>
        /// Updating must currently take place through UpdatePrimFlags()
        /// </remarks>
        public bool UsesPhysics
        {
            get { return (RootPart.Flags & PrimFlags.TemporaryOnRez) != 0; }
        }

        /// <summary>
        /// Is this scene object temporary?
        /// </summary>
        /// <remarks>
        /// Updating must currently take place through UpdatePrimFlags()
        /// </remarks>
        public bool IsTemporary
        {
            get { return (RootPart.Flags & PrimFlags.TemporaryOnRez) != 0; }
        }

        public bool IsVolumeDetect
        {
            get { return RootPart.VolumeDetectActive; }
        }

        public float scriptScore;

        private Vector3 lastPhysGroupPos;
        private Quaternion lastPhysGroupRot;

        private bool m_isBackedUp;

        protected MapAndArray<UUID, SceneObjectPart> m_parts = new MapAndArray<UUID, SceneObjectPart>();

        protected ulong m_regionHandle;
        protected SceneObjectPart m_rootPart;
        // private Dictionary<UUID, scriptEvents> m_scriptEvents = new Dictionary<UUID, scriptEvents>();

        private Dictionary<uint, scriptPosTarget> m_targets = new Dictionary<uint, scriptPosTarget>();
        private Dictionary<uint, scriptRotTarget> m_rotTargets = new Dictionary<uint, scriptRotTarget>();

        private bool m_scriptListens_atTarget;
        private bool m_scriptListens_notAtTarget;

        private bool m_scriptListens_atRotTarget;
        private bool m_scriptListens_notAtRotTarget;

        internal Dictionary<UUID, string> m_savedScriptState;

        #region Properties

        /// <summary>
        /// The name of an object grouping is always the same as its root part
        /// </summary>
        public override string Name
        {
            get {
                if (RootPart == null)
                    return String.Empty;
                return RootPart.Name;
            }
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

        protected Quaternion m_rotation = Quaternion.Identity;

        public virtual Quaternion Rotation
        {
            get { return m_rotation; }
            set { m_rotation = value; }
        }

        public Quaternion GroupRotation
        {
            get { return m_rootPart.RotationOffset; }
        }

        public Vector3 GroupScale
        {
            get
            {
                Vector3 minScale = new Vector3(Constants.RegionSize, Constants.RegionSize, Constants.RegionSize);
                Vector3 maxScale = Vector3.Zero;
                Vector3 finalScale = new Vector3(0.5f, 0.5f, 0.5f);
    
                SceneObjectPart[] parts = m_parts.GetArray();
                for (int i = 0; i < parts.Length; i++)
                {
                    SceneObjectPart part = parts[i];
                    Vector3 partscale = part.Scale;
                    Vector3 partoffset = part.OffsetPosition;
    
                    minScale.X = (partscale.X + partoffset.X < minScale.X) ? partscale.X + partoffset.X : minScale.X;
                    minScale.Y = (partscale.Y + partoffset.Y < minScale.Y) ? partscale.Y + partoffset.Y : minScale.Y;
                    minScale.Z = (partscale.Z + partoffset.Z < minScale.Z) ? partscale.Z + partoffset.Z : minScale.Z;
    
                    maxScale.X = (partscale.X + partoffset.X > maxScale.X) ? partscale.X + partoffset.X : maxScale.X;
                    maxScale.Y = (partscale.Y + partoffset.Y > maxScale.Y) ? partscale.Y + partoffset.Y : maxScale.Y;
                    maxScale.Z = (partscale.Z + partoffset.Z > maxScale.Z) ? partscale.Z + partoffset.Z : maxScale.Z;
                }
    
                finalScale.X = (minScale.X > maxScale.X) ? minScale.X : maxScale.X;
                finalScale.Y = (minScale.Y > maxScale.Y) ? minScale.Y : maxScale.Y;
                finalScale.Z = (minScale.Z > maxScale.Z) ? minScale.Z : maxScale.Z;
    
                return finalScale;
            }
        }

        public UUID GroupID
        {
            get { return m_rootPart.GroupID; }
            set { m_rootPart.GroupID = value; }
        }

        public SceneObjectPart[] Parts
        {
            get { return m_parts.GetArray(); }
        }

        public bool ContainsPart(UUID partID)
        {
            return m_parts.ContainsKey(partID);
        }

        /// <value>
        /// The root part of this scene object
        /// </value>
        public SceneObjectPart RootPart
        {
            get { return m_rootPart; }
        }

        public ulong RegionHandle
        {
            get { return m_regionHandle; }
            set
            {
                m_regionHandle = value;
                SceneObjectPart[] parts = m_parts.GetArray();
                for (int i = 0; i < parts.Length; i++)
                    parts[i].RegionHandle = value;
            }
        }

        /// <summary>
        /// Check both the attachment property and the relevant properties of the underlying root part.
        /// </summary>
        /// <remarks>
        /// This is necessary in some cases, particularly when a scene object has just crossed into a region and doesn't
        /// have the IsAttachment property yet checked.
        /// 
        /// FIXME: However, this should be fixed so that this property
        /// propertly reflects the underlying status.
        /// </remarks>
        /// <returns></returns>
        public bool IsAttachmentCheckFull()
        {
            return (IsAttachment || (m_rootPart.Shape.PCode == 9 && m_rootPart.Shape.State != 0));
        }
        
        /// <summary>
        /// The absolute position of this scene object in the scene
        /// </summary>
        public override Vector3 AbsolutePosition
        {
            get { return m_rootPart.GroupPosition; }
            set
            {
                Vector3 val = value;

                if (Scene != null)
                {
                    if ((Scene.TestBorderCross(val - Vector3.UnitX, Cardinals.E) || Scene.TestBorderCross(val + Vector3.UnitX, Cardinals.W)
                        || Scene.TestBorderCross(val - Vector3.UnitY, Cardinals.N) || Scene.TestBorderCross(val + Vector3.UnitY, Cardinals.S)) 
                        && !IsAttachmentCheckFull() && (!Scene.LoadingPrims))
                    {
                        m_scene.CrossPrimGroupIntoNewRegion(val, this, true);
                    }
                }
                
                if (RootPart.GetStatusSandbox())
                {
                    if (Util.GetDistanceTo(RootPart.StatusSandboxPos, value) > 10)
                    {
                        RootPart.ScriptSetPhysicsStatus(false);
                        
                        if (Scene != null)
                            Scene.SimChat(Utils.StringToBytes("Hit Sandbox Limit"),
                                  ChatTypeEnum.DebugChannel, 0x7FFFFFFF, RootPart.AbsolutePosition, Name, UUID, false);
                        
                        return;
                    }
                }

                SceneObjectPart[] parts = m_parts.GetArray();
                for (int i = 0; i < parts.Length; i++)
                    parts[i].GroupPosition = val;

                //if (m_rootPart.PhysActor != null)
                //{
                //m_rootPart.PhysActor.Position =
                //new PhysicsVector(m_rootPart.GroupPosition.X, m_rootPart.GroupPosition.Y,
                //m_rootPart.GroupPosition.Z);
                //m_scene.PhysicsScene.AddPhysicsActorTaint(m_rootPart.PhysActor);
                //}
                
                if (Scene != null)
                    Scene.EventManager.TriggerParcelPrimCountTainted();
            }
        }

        public override uint LocalId
        {
            get { return m_rootPart.LocalId; }
            set { m_rootPart.LocalId = value; }
        }

        public override UUID UUID
        {
            get { return m_rootPart.UUID; }
            set 
            {
                lock (m_parts.SyncRoot)
                {
                    m_parts.Remove(m_rootPart.UUID);
                    m_rootPart.UUID = value;
                    m_parts.Add(value, m_rootPart);
                }
            }
        }

        public UUID OwnerID
        {
            get { return m_rootPart.OwnerID; }
            set { m_rootPart.OwnerID = value; }
        }

        public float Damage
        {
            get { return m_rootPart.Damage; }
            set { m_rootPart.Damage = value; }
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
        
        private bool m_passCollision;
        public bool PassCollision
        {
            get { return m_passCollision; }
            set
            {
                m_passCollision = value;
                HasGroupChanged = true;
            }
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
                    SceneObjectPart[] parts = m_parts.GetArray();
                    for (int i = 0; i < parts.Length; i++)
                    {
                        SceneObjectPart child = parts[i];
                        if (child.PhysActor != null)
                            child.PhysActor.Selected = value;
                    }
                }
            }
        }

        private SceneObjectPart m_PlaySoundMasterPrim = null;
        public SceneObjectPart PlaySoundMasterPrim
        {
            get { return m_PlaySoundMasterPrim; }
            set { m_PlaySoundMasterPrim = value; }
        }

        private List<SceneObjectPart> m_PlaySoundSlavePrims = new List<SceneObjectPart>();
        public List<SceneObjectPart> PlaySoundSlavePrims
        {
            get { return m_PlaySoundSlavePrims; }
            set { m_PlaySoundSlavePrims = value; }
        }

        private SceneObjectPart m_LoopSoundMasterPrim = null;
        public SceneObjectPart LoopSoundMasterPrim
        {
            get { return m_LoopSoundMasterPrim; }
            set { m_LoopSoundMasterPrim = value; }
        }

        private List<SceneObjectPart> m_LoopSoundSlavePrims = new List<SceneObjectPart>();
        public List<SceneObjectPart> LoopSoundSlavePrims
        {
            get { return m_LoopSoundSlavePrims; }
            set { m_LoopSoundSlavePrims = value; }
        }

        // The UUID for the Region this Object is in.
        public UUID RegionUUID
        {
            get
            {
                if (m_scene != null)
                {
                    return m_scene.RegionInfo.RegionID;
                }
                return UUID.Zero;
            }
        }

        #endregion

//        ~SceneObjectGroup()
//        {
//            m_log.DebugFormat("[SCENE OBJECT GROUP]: Destructor called for {0}, local id {1}", Name, LocalId);
//        }

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
        public SceneObjectGroup(SceneObjectPart part)
        {
            SetRootPart(part);
        }

        /// <summary>
        /// Constructor.  This object is added to the scene later via AttachToScene()
        /// </summary>
        public SceneObjectGroup(UUID ownerID, Vector3 pos, Quaternion rot, PrimitiveBaseShape shape)
        { 
            SetRootPart(new SceneObjectPart(ownerID, shape, pos, rot, Vector3.Zero));
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public SceneObjectGroup(UUID ownerID, Vector3 pos, PrimitiveBaseShape shape)
            : this(ownerID, pos, Quaternion.Identity, shape)
        {
        }

        public void LoadScriptState(XmlDocument doc)
        {
            XmlNodeList nodes = doc.GetElementsByTagName("SavedScriptState");
            if (nodes.Count > 0)
            {
                if (m_savedScriptState == null)
                    m_savedScriptState = new Dictionary<UUID, string>();
                foreach (XmlNode node in nodes)
                {
                    if (node.Attributes["UUID"] != null)
                    {
                        UUID itemid = new UUID(node.Attributes["UUID"].Value);
                        if (itemid != UUID.Zero)
                            m_savedScriptState[itemid] = node.InnerXml;
                    }
                } 
            }
        }

        public void SetFromItemID(UUID AssetId)
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].FromItemID = AssetId;
        }

        public UUID GetFromItemID()
        {
            return m_rootPart.FromItemID;
        }

        /// <summary>
        /// Hooks this object up to the backup event so that it is persisted to the database when the update thread executes.
        /// </summary>
        public virtual void AttachToBackup()
        {
            if (InSceneBackup)
            {
                //m_log.DebugFormat(
                //    "[SCENE OBJECT GROUP]: Attaching object {0} {1} to scene presistence sweep", Name, UUID);

                if (!m_isBackedUp)
                    m_scene.EventManager.OnBackup += ProcessBackup;
                
                m_isBackedUp = true;
            }
        }
        
        /// <summary>
        /// Attach this object to a scene.  It will also now appear to agents.
        /// </summary>
        /// <param name="scene"></param>
        public void AttachToScene(Scene scene)
        {
            m_scene = scene;
            RegionHandle = m_scene.RegionInfo.RegionHandle;

            if (m_rootPart.Shape.PCode != 9 || m_rootPart.Shape.State == 0)
                m_rootPart.ParentID = 0;
            if (m_rootPart.LocalId == 0)
                m_rootPart.LocalId = m_scene.AllocateLocalId();

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (Object.ReferenceEquals(part, m_rootPart))
                    continue;

                if (part.LocalId == 0)
                    part.LocalId = m_scene.AllocateLocalId();

                part.ParentID = m_rootPart.LocalId;
                //m_log.DebugFormat("[SCENE]: Given local id {0} to part {1}, linknum {2}, parent {3} {4}", part.LocalId, part.UUID, part.LinkNum, part.ParentID, part.ParentUUID);
            }

            ApplyPhysics(m_scene.m_physicalPrim);

            // Don't trigger the update here - otherwise some client issues occur when multiple updates are scheduled
            // for the same object with very different properties.  The caller must schedule the update.
            //ScheduleGroupForFullUpdate();
        }

        public EntityIntersection TestIntersection(Ray hRay, bool frontFacesOnly, bool faceCenters)
        {
            // We got a request from the inner_scene to raytrace along the Ray hRay
            // We're going to check all of the prim in this group for intersection with the ray
            // If we get a result, we're going to find the closest result to the origin of the ray
            // and send back the intersection information back to the innerscene.

            EntityIntersection result = new EntityIntersection();

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];

                // Temporary commented to stop compiler warning
                //Vector3 partPosition =
                //    new Vector3(part.AbsolutePosition.X, part.AbsolutePosition.Y, part.AbsolutePosition.Z);
                Quaternion parentrotation = GroupRotation;

                // Telling the prim to raytrace.
                //EntityIntersection inter = part.TestIntersection(hRay, parentrotation);

                EntityIntersection inter = part.TestIntersectionOBB(hRay, parentrotation, frontFacesOnly, faceCenters);

                // This may need to be updated to the maximum draw distance possible..
                // We might (and probably will) be checking for prim creation from other sims
                // when the camera crosses the border.
                float idist = Constants.RegionSize;

                if (inter.HitTF)
                {
                    // We need to find the closest prim to return to the testcaller along the ray
                    if (inter.distance < idist)
                    {
                        result.HitTF = true;
                        result.ipoint = inter.ipoint;
                        result.obj = part;
                        result.normal = inter.normal;
                        result.distance = inter.distance;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets a vector representing the size of the bounding box containing all the prims in the group
        /// Treats all prims as rectangular, so no shape (cut etc) is taken into account
        /// offsetHeight is the offset in the Z axis from the centre of the bounding box to the centre of the root prim
        /// </summary>
        /// <returns></returns>
        public void GetAxisAlignedBoundingBoxRaw(out float minX, out float maxX, out float minY, out float maxY, out float minZ, out float maxZ)
        {
            maxX = -256f;
            maxY = -256f;
            maxZ = -256f;
            minX = 256f;
            minY = 256f;
            minZ = 8192f;

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];

                Vector3 worldPos = part.GetWorldPosition();
                Vector3 offset = worldPos - AbsolutePosition;
                Quaternion worldRot;
                if (part.ParentID == 0)
                    worldRot = part.RotationOffset;
                else
                    worldRot = part.GetWorldRotation();

                Vector3 frontTopLeft;
                Vector3 frontTopRight;
                Vector3 frontBottomLeft;
                Vector3 frontBottomRight;

                Vector3 backTopLeft;
                Vector3 backTopRight;
                Vector3 backBottomLeft;
                Vector3 backBottomRight;

                Vector3 orig = Vector3.Zero;

                frontTopLeft.X = orig.X - (part.Scale.X / 2);
                frontTopLeft.Y = orig.Y - (part.Scale.Y / 2);
                frontTopLeft.Z = orig.Z + (part.Scale.Z / 2);

                frontTopRight.X = orig.X - (part.Scale.X / 2);
                frontTopRight.Y = orig.Y + (part.Scale.Y / 2);
                frontTopRight.Z = orig.Z + (part.Scale.Z / 2);

                frontBottomLeft.X = orig.X - (part.Scale.X / 2);
                frontBottomLeft.Y = orig.Y - (part.Scale.Y / 2);
                frontBottomLeft.Z = orig.Z - (part.Scale.Z / 2);

                frontBottomRight.X = orig.X - (part.Scale.X / 2);
                frontBottomRight.Y = orig.Y + (part.Scale.Y / 2);
                frontBottomRight.Z = orig.Z - (part.Scale.Z / 2);

                backTopLeft.X = orig.X + (part.Scale.X / 2);
                backTopLeft.Y = orig.Y - (part.Scale.Y / 2);
                backTopLeft.Z = orig.Z + (part.Scale.Z / 2);

                backTopRight.X = orig.X + (part.Scale.X / 2);
                backTopRight.Y = orig.Y + (part.Scale.Y / 2);
                backTopRight.Z = orig.Z + (part.Scale.Z / 2);

                backBottomLeft.X = orig.X + (part.Scale.X / 2);
                backBottomLeft.Y = orig.Y - (part.Scale.Y / 2);
                backBottomLeft.Z = orig.Z - (part.Scale.Z / 2);

                backBottomRight.X = orig.X + (part.Scale.X / 2);
                backBottomRight.Y = orig.Y + (part.Scale.Y / 2);
                backBottomRight.Z = orig.Z - (part.Scale.Z / 2);

                frontTopLeft = frontTopLeft * worldRot;
                frontTopRight = frontTopRight * worldRot;
                frontBottomLeft = frontBottomLeft * worldRot;
                frontBottomRight = frontBottomRight * worldRot;

                backBottomLeft = backBottomLeft * worldRot;
                backBottomRight = backBottomRight * worldRot;
                backTopLeft = backTopLeft * worldRot;
                backTopRight = backTopRight * worldRot;


                frontTopLeft += offset;
                frontTopRight += offset;
                frontBottomLeft += offset;
                frontBottomRight += offset;

                backBottomLeft += offset;
                backBottomRight += offset;
                backTopLeft += offset;
                backTopRight += offset;

                if (frontTopRight.X > maxX)
                    maxX = frontTopRight.X;
                if (frontTopLeft.X > maxX)
                    maxX = frontTopLeft.X;
                if (frontBottomRight.X > maxX)
                    maxX = frontBottomRight.X;
                if (frontBottomLeft.X > maxX)
                    maxX = frontBottomLeft.X;

                if (backTopRight.X > maxX)
                    maxX = backTopRight.X;
                if (backTopLeft.X > maxX)
                    maxX = backTopLeft.X;
                if (backBottomRight.X > maxX)
                    maxX = backBottomRight.X;
                if (backBottomLeft.X > maxX)
                    maxX = backBottomLeft.X;

                if (frontTopRight.X < minX)
                    minX = frontTopRight.X;
                if (frontTopLeft.X < minX)
                    minX = frontTopLeft.X;
                if (frontBottomRight.X < minX)
                    minX = frontBottomRight.X;
                if (frontBottomLeft.X < minX)
                    minX = frontBottomLeft.X;

                if (backTopRight.X < minX)
                    minX = backTopRight.X;
                if (backTopLeft.X < minX)
                    minX = backTopLeft.X;
                if (backBottomRight.X < minX)
                    minX = backBottomRight.X;
                if (backBottomLeft.X < minX)
                    minX = backBottomLeft.X;

                //
                if (frontTopRight.Y > maxY)
                    maxY = frontTopRight.Y;
                if (frontTopLeft.Y > maxY)
                    maxY = frontTopLeft.Y;
                if (frontBottomRight.Y > maxY)
                    maxY = frontBottomRight.Y;
                if (frontBottomLeft.Y > maxY)
                    maxY = frontBottomLeft.Y;

                if (backTopRight.Y > maxY)
                    maxY = backTopRight.Y;
                if (backTopLeft.Y > maxY)
                    maxY = backTopLeft.Y;
                if (backBottomRight.Y > maxY)
                    maxY = backBottomRight.Y;
                if (backBottomLeft.Y > maxY)
                    maxY = backBottomLeft.Y;

                if (frontTopRight.Y < minY)
                    minY = frontTopRight.Y;
                if (frontTopLeft.Y < minY)
                    minY = frontTopLeft.Y;
                if (frontBottomRight.Y < minY)
                    minY = frontBottomRight.Y;
                if (frontBottomLeft.Y < minY)
                    minY = frontBottomLeft.Y;

                if (backTopRight.Y < minY)
                    minY = backTopRight.Y;
                if (backTopLeft.Y < minY)
                    minY = backTopLeft.Y;
                if (backBottomRight.Y < minY)
                    minY = backBottomRight.Y;
                if (backBottomLeft.Y < minY)
                    minY = backBottomLeft.Y;

                //
                if (frontTopRight.Z > maxZ)
                    maxZ = frontTopRight.Z;
                if (frontTopLeft.Z > maxZ)
                    maxZ = frontTopLeft.Z;
                if (frontBottomRight.Z > maxZ)
                    maxZ = frontBottomRight.Z;
                if (frontBottomLeft.Z > maxZ)
                    maxZ = frontBottomLeft.Z;

                if (backTopRight.Z > maxZ)
                    maxZ = backTopRight.Z;
                if (backTopLeft.Z > maxZ)
                    maxZ = backTopLeft.Z;
                if (backBottomRight.Z > maxZ)
                    maxZ = backBottomRight.Z;
                if (backBottomLeft.Z > maxZ)
                    maxZ = backBottomLeft.Z;

                if (frontTopRight.Z < minZ)
                    minZ = frontTopRight.Z;
                if (frontTopLeft.Z < minZ)
                    minZ = frontTopLeft.Z;
                if (frontBottomRight.Z < minZ)
                    minZ = frontBottomRight.Z;
                if (frontBottomLeft.Z < minZ)
                    minZ = frontBottomLeft.Z;

                if (backTopRight.Z < minZ)
                    minZ = backTopRight.Z;
                if (backTopLeft.Z < minZ)
                    minZ = backTopLeft.Z;
                if (backBottomRight.Z < minZ)
                    minZ = backBottomRight.Z;
                if (backBottomLeft.Z < minZ)
                    minZ = backBottomLeft.Z;
            }
        }

        public Vector3 GetAxisAlignedBoundingBox(out float offsetHeight)
        {
            float minX;
            float maxX;
            float minY;
            float maxY;
            float minZ;
            float maxZ;

            GetAxisAlignedBoundingBoxRaw(out minX, out maxX, out minY, out maxY, out minZ, out maxZ);
            Vector3 boundingBox = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);

            offsetHeight = 0;
            float lower = (minZ * -1);
            if (lower > maxZ)
            {
                offsetHeight = lower - (boundingBox.Z / 2);

            }
            else if (maxZ > lower)
            {
                offsetHeight = maxZ - (boundingBox.Z / 2);
                offsetHeight *= -1;
            }

           // m_log.InfoFormat("BoundingBox is {0} , {1} , {2} ", boundingBox.X, boundingBox.Y, boundingBox.Z);
            return boundingBox;
        }

        #endregion

        public void SaveScriptedState(XmlTextWriter writer)
        {
            XmlDocument doc = new XmlDocument();
            Dictionary<UUID,string> states = new Dictionary<UUID,string>();

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                Dictionary<UUID, string> pstates = parts[i].Inventory.GetScriptStates();
                foreach (KeyValuePair<UUID, string> kvp in pstates)
                    states.Add(kvp.Key, kvp.Value);
            }

            if (states.Count > 0)
            {
                // Now generate the necessary XML wrappings
                writer.WriteStartElement(String.Empty, "GroupScriptStates", String.Empty);
                foreach (UUID itemid in states.Keys)
                {
                    doc.LoadXml(states[itemid]);
                    writer.WriteStartElement(String.Empty, "SavedScriptState", String.Empty);
                    writer.WriteAttributeString(String.Empty, "UUID", String.Empty, itemid.ToString());
                    writer.WriteRaw(doc.DocumentElement.OuterXml); // Writes ScriptState element
                    writer.WriteEndElement(); // End of SavedScriptState
                }
                writer.WriteEndElement(); // End of GroupScriptStates
            }
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
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].UpdateMovement();
        }

        public ushort GetTimeDilation()
        {
            return Utils.FloatToUInt16(m_scene.TimeDilation, 0.0f, 1.0f);
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
        /// Set a part to act as the root part for this scene object
        /// </summary>
        /// <param name="part"></param>
        public void SetRootPart(SceneObjectPart part)
        {
            if (part == null)
                throw new ArgumentNullException("Cannot give SceneObjectGroup a null root SceneObjectPart");

            part.SetParent(this);
            m_rootPart = part;
            if (!IsAttachment)
                part.ParentID = 0;
            part.LinkNum = 0;
            
            m_parts.Add(m_rootPart.UUID, m_rootPart);
        }

        /// <summary>
        /// Add a new part to this scene object.  The part must already be correctly configured.
        /// </summary>
        /// <param name="part"></param>
        public void AddPart(SceneObjectPart part)
        {
            part.SetParent(this);
            part.LinkNum = m_parts.Add(part.UUID, part);
            if (part.LinkNum == 2 && RootPart != null)
                RootPart.LinkNum = 1;
        }

        /// <summary>
        /// Make sure that every non root part has the proper parent root part local id
        /// </summary>
        private void UpdateParentIDs()
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part.UUID != m_rootPart.UUID)
                    part.ParentID = m_rootPart.LocalId;
            }
        }

        public void RegenerateFullIDs()
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].UUID = UUID.Random();
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
        
        // dahlia: I had to uncomment it, without it meshing was failing on some prims and objects
        // at region startup
        
        // teravus: After this was removed from the linking algorithm, Linked prims no longer collided 
        // properly when non-physical if they havn't been moved.   This breaks ALL builds.
        // see: http://opensimulator.org/mantis/view.php?id=3108
        
        // Here's the deal, this is ABSOLUTELY CRITICAL so the physics scene gets the update about the 
        // position of linkset prims.  IF YOU CHANGE THIS, YOU MUST TEST colliding with just linked and 
        // unmoved prims!  As soon as you move a Prim/group, it will collide properly because Absolute 
        // Position has been set!
        
        public void ResetChildPrimPhysicsPositions()
        {
            AbsolutePosition = AbsolutePosition; // could someone in the know please explain how this works?

            // teravus: AbsolutePosition is NOT a normal property!
            // the code in the getter of AbsolutePosition is significantly different then the code in the setter!
            // jhurliman: Then why is it a property instead of two methods?
        }

        public UUID GetPartsFullID(uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
                return part.UUID;
            }
            return UUID.Zero;
        }

        public void ObjectGrabHandler(uint localId, Vector3 offsetPos, IClientAPI remoteClient)
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

        public virtual void OnGrabPart(SceneObjectPart part, Vector3 offsetPos, IClientAPI remoteClient)
        {
//            m_log.DebugFormat(
//                "[SCENE OBJECT GROUP]: Processing OnGrabPart for {0} on {1} {2}, offsetPos {3}",
//                remoteClient.Name, part.Name, part.LocalId, offsetPos);

            part.StoreUndoState();
            part.OnGrab(offsetPos, remoteClient);
        }

        public virtual void OnGrabGroup(Vector3 offsetPos, IClientAPI remoteClient)
        {
            m_scene.EventManager.TriggerGroupGrab(UUID, offsetPos, remoteClient.AgentId);
        }

        /// <summary>
        /// Delete this group from its scene.
        /// </summary>
        /// 
        /// This only handles the in-world consequences of deletion (e.g. any avatars sitting on it are forcibly stood
        /// up and all avatars receive notification of its removal.  Removal of the scene object from database backup
        /// must be handled by the caller.
        /// 
        /// <param name="silent">If true then deletion is not broadcast to clients</param>
        public void DeleteGroupFromScene(bool silent)
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];

                Scene.ForEachScenePresence(delegate(ScenePresence avatar)
                {
                    if (avatar.ParentID == LocalId)
                        avatar.StandUp();

                    if (!silent)
                    {
                        part.UpdateFlag = 0;
                        if (part == m_rootPart)
                            avatar.ControllingClient.SendKillObject(m_regionHandle, part.LocalId);
                    }
                });
            }
        }

        public void AddScriptLPS(int count)
        {
            if (scriptScore + count >= float.MaxValue - count)
                scriptScore = 0;

            scriptScore += (float)count;
            SceneGraph d = m_scene.SceneGraph;
            d.AddToScriptLPS(count);
        }

        public void AddActiveScriptCount(int count)
        {
            SceneGraph d = m_scene.SceneGraph;
            d.AddActiveScripts(count);
        }

        public void aggregateScriptEvents()
        {
            PrimFlags objectflagupdate = (PrimFlags)RootPart.GetEffectiveObjectFlags();

            scriptEvents aggregateScriptEvents = 0;

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part == null)
                    continue;
                if (part != RootPart)
                    part.Flags = objectflagupdate;
                aggregateScriptEvents |= part.AggregateScriptEvents;
            }

            m_scriptListens_atTarget = ((aggregateScriptEvents & scriptEvents.at_target) != 0);
            m_scriptListens_notAtTarget = ((aggregateScriptEvents & scriptEvents.not_at_target) != 0);

            if (!m_scriptListens_atTarget && !m_scriptListens_notAtTarget)
            {
                lock (m_targets)
                    m_targets.Clear();
                m_scene.RemoveGroupTarget(this);
            }
            m_scriptListens_atRotTarget = ((aggregateScriptEvents & scriptEvents.at_rot_target) != 0);
            m_scriptListens_notAtRotTarget = ((aggregateScriptEvents & scriptEvents.not_at_rot_target) != 0);

            if (!m_scriptListens_atRotTarget && !m_scriptListens_notAtRotTarget)
            {
                lock (m_rotTargets)
                    m_rotTargets.Clear();
                m_scene.RemoveGroupTarget(this);
            }

            ScheduleGroupForFullUpdate();
        }

        public void SetText(string text, Vector3 color, double alpha)
        {
            Color = Color.FromArgb(0xff - (int) (alpha * 0xff),
                                   (int) (color.X * 0xff),
                                   (int) (color.Y * 0xff),
                                   (int) (color.Z * 0xff));
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
            // Apply physics to the root prim
            m_rootPart.ApplyPhysics(m_rootPart.GetEffectiveObjectFlags(), m_rootPart.VolumeDetectActive, m_physicalPrim);
            
            // Apply physics to child prims
            SceneObjectPart[] parts = m_parts.GetArray();
            if (parts.Length > 1)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    SceneObjectPart part = parts[i];
                    if (part.LocalId != m_rootPart.LocalId)
                        part.ApplyPhysics(m_rootPart.GetEffectiveObjectFlags(), part.VolumeDetectActive, m_physicalPrim);
                }

                // Hack to get the physics scene geometries in the right spot
                ResetChildPrimPhysicsPositions();
            }
        }

        public void SetOwnerId(UUID userId)
        {
            ForEachPart(delegate(SceneObjectPart part) { part.OwnerID = userId; });
        }

        public void ForEachPart(Action<SceneObjectPart> whatToDo)
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                whatToDo(parts[i]);
        }

        #region Events

        /// <summary>
        /// Processes backup.
        /// </summary>
        /// <param name="datastore"></param>
        public virtual void ProcessBackup(ISimulationDataService datastore, bool forcedBackup)
        {
            if (!m_isBackedUp)
            {
//                m_log.DebugFormat(
//                    "[WATER WARS]: Ignoring backup of {0} {1} since object is not marked to be backed up", Name, UUID);
                return;
            }

            if (IsDeleted || UUID == UUID.Zero)
            {
//                m_log.DebugFormat(
//                    "[WATER WARS]: Ignoring backup of {0} {1} since object is marked as already deleted", Name, UUID);
                return;
            }

            // Since this is the top of the section of call stack for backing up a particular scene object, don't let
            // any exception propogate upwards.
            try
            {
                if (!m_scene.ShuttingDown) // if shutting down then there will be nothing to handle the return so leave till next restart
                {
                    ILandObject parcel = m_scene.LandChannel.GetLandObject(
                            m_rootPart.GroupPosition.X, m_rootPart.GroupPosition.Y);

                    if (parcel != null && parcel.LandData != null &&
                            parcel.LandData.OtherCleanTime != 0)
                    {
                        if (parcel.LandData.OwnerID != OwnerID &&
                                (parcel.LandData.GroupID != GroupID ||
                                parcel.LandData.GroupID == UUID.Zero))
                        {
                            if ((DateTime.UtcNow - RootPart.Rezzed).TotalMinutes >
                                    parcel.LandData.OtherCleanTime)
                            {
                                DetachFromBackup();
                                m_log.DebugFormat(
                                    "[SCENE OBJECT GROUP]: Returning object {0} due to parcel autoreturn", 
                                     RootPart.UUID);
                                m_scene.AddReturn(OwnerID, Name, AbsolutePosition, "parcel autoreturn");
                                m_scene.DeRezObjects(null, new List<uint>() { RootPart.LocalId }, UUID.Zero,
                                        DeRezAction.Return, UUID.Zero);

                                return;
                            }
                        }
                    }
                }

                if (m_scene.UseBackup && HasGroupChanged)
                {
                    // don't backup while it's selected or you're asking for changes mid stream.
                    if (isTimeToPersist() || forcedBackup)
                    {
//                        m_log.DebugFormat(
//                            "[SCENE]: Storing {0}, {1} in {2}",
//                            Name, UUID, m_scene.RegionInfo.RegionName);

                        SceneObjectGroup backup_group = Copy(false);
                        backup_group.RootPart.Velocity = RootPart.Velocity;
                        backup_group.RootPart.Acceleration = RootPart.Acceleration;
                        backup_group.RootPart.AngularVelocity = RootPart.AngularVelocity;
                        backup_group.RootPart.ParticleSystem = RootPart.ParticleSystem;
                        HasGroupChanged = false;
                        HasGroupChangedDueToDelink = false;

                        m_scene.EventManager.TriggerOnSceneObjectPreSave(backup_group, this);
                        datastore.StoreObject(backup_group, m_scene.RegionInfo.RegionID);

                        backup_group.ForEachPart(delegate(SceneObjectPart part) 
                        { 
                            part.Inventory.ProcessInventoryBackup(datastore); 
                        });

                        backup_group = null;
                    }
//                    else
//                    {
//                        m_log.DebugFormat(
//                            "[SCENE]: Did not update persistence of object {0} {1}, selected = {2}",
//                            Name, UUID, IsSelected);
//                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[SCENE]: Storing of {0}, {1} in {2} failed with exception {3}{4}", 
                    Name, UUID, m_scene.RegionInfo.RegionName, e.Message, e.StackTrace);
            }
        }

        #endregion

        public void SendFullUpdateToClient(IClientAPI remoteClient)
        {
            RootPart.SendFullUpdate(
                remoteClient, m_scene.Permissions.GenerateClientFlags(remoteClient.AgentId, RootPart.UUID));

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part != RootPart)
                    part.SendFullUpdate(remoteClient, m_scene.Permissions.GenerateClientFlags(remoteClient.AgentId, part.UUID));
            }
        }

        #region Copying

        /// <summary>
        /// Duplicates this object, including operations such as physics set up and attaching to the backup event.
        /// </summary>
        /// <param name="userExposed">True if the duplicate will immediately be in the scene, false otherwise</param>
        /// <returns></returns>
        public SceneObjectGroup Copy(bool userExposed)
        {
            SceneObjectGroup dupe = (SceneObjectGroup)MemberwiseClone();
            dupe.m_isBackedUp = false;
            dupe.m_parts = new MapAndArray<OpenMetaverse.UUID, SceneObjectPart>();

            // Warning, The following code related to previousAttachmentStatus is needed so that clones of 
            // attachments do not bordercross while they're being duplicated.  This is hacktastic!
            // Normally, setting AbsolutePosition will bordercross a prim if it's outside the region!
            // unless IsAttachment is true!, so to prevent border crossing, we save it's attachment state 
            // (which should be false anyway) set it as an Attachment and then set it's Absolute Position, 
            // then restore it's attachment state

            // This is only necessary when userExposed is false!

            bool previousAttachmentStatus = dupe.IsAttachment;
            
            if (!userExposed)
                dupe.IsAttachment = true;

            dupe.AbsolutePosition = new Vector3(AbsolutePosition.X, AbsolutePosition.Y, AbsolutePosition.Z);

            if (!userExposed)
            {
                dupe.IsAttachment = previousAttachmentStatus;
            }

            dupe.CopyRootPart(m_rootPart, OwnerID, GroupID, userExposed);
            dupe.m_rootPart.LinkNum = m_rootPart.LinkNum;

            if (userExposed)
                dupe.m_rootPart.TrimPermissions();

            List<SceneObjectPart> partList = new List<SceneObjectPart>(m_parts.GetArray());
            
            partList.Sort(delegate(SceneObjectPart p1, SceneObjectPart p2)
                {
                    return p1.LinkNum.CompareTo(p2.LinkNum);
                }
            );

            foreach (SceneObjectPart part in partList)
            {
                SceneObjectPart newPart;
                if (part.UUID != m_rootPart.UUID)
                {
                    newPart = dupe.CopyPart(part, OwnerID, GroupID, userExposed);
                    newPart.LinkNum = part.LinkNum;
                }
                else
                {
                    newPart = dupe.m_rootPart;
                }

                // Need to duplicate the physics actor as well
                if (part.PhysActor != null && userExposed)
                {
                    PrimitiveBaseShape pbs = newPart.Shape;
    
                    newPart.PhysActor
                        = m_scene.PhysicsScene.AddPrimShape(
                            string.Format("{0}/{1}", newPart.Name, newPart.UUID),
                            pbs,
                            newPart.AbsolutePosition,
                            newPart.Scale,
                            newPart.RotationOffset,
                            part.PhysActor.IsPhysical,
                            newPart.LocalId);
    
                    newPart.DoPhysicsPropertyUpdate(part.PhysActor.IsPhysical, true);
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
        /// Copy the given part as the root part of this scene object.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="cAgentID"></param>
        /// <param name="cGroupID"></param>
        public void CopyRootPart(SceneObjectPart part, UUID cAgentID, UUID cGroupID, bool userExposed)
        {
            SetRootPart(part.Copy(m_scene.AllocateLocalId(), OwnerID, GroupID, 0, userExposed));
        }

        public void ScriptSetPhysicsStatus(bool usePhysics)
        {
            UpdatePrimFlags(RootPart.LocalId, usePhysics, IsTemporary, IsPhantom, IsVolumeDetect);
        }

        public void ScriptSetTemporaryStatus(bool makeTemporary)
        {
            UpdatePrimFlags(RootPart.LocalId, UsesPhysics, makeTemporary, IsPhantom, IsVolumeDetect);
        }

        public void ScriptSetPhantomStatus(bool makePhantom)
        {
            UpdatePrimFlags(RootPart.LocalId, UsesPhysics, IsTemporary, makePhantom, IsVolumeDetect);
        }

        public void ScriptSetVolumeDetect(bool makeVolumeDetect)
        {
            UpdatePrimFlags(RootPart.LocalId, UsesPhysics, IsTemporary, IsPhantom, makeVolumeDetect);

            /*
            ScriptSetPhantomStatus(false);  // What ever it was before, now it's not phantom anymore

            if (PhysActor != null) // Should always be the case now
            {
                PhysActor.SetVolumeDetect(param);
            }
            if (param != 0)
                AddFlag(PrimFlags.Phantom);

            ScheduleFullUpdate();
            */
        }

        public void applyImpulse(Vector3 impulse)
        {
            // We check if rootpart is null here because scripts don't delete if you delete the host.
            // This means that unfortunately, we can pass a null physics actor to Simulate!
            // Make sure we don't do that!
            SceneObjectPart rootpart = m_rootPart;
            if (rootpart != null)
            {
                if (IsAttachment)
                {
                    ScenePresence avatar = m_scene.GetScenePresence(AttachedAvatar);
                    if (avatar != null)
                    {
                        avatar.PushForce(impulse);
                    }
                }
                else
                {
                    if (rootpart.PhysActor != null)
                    {
                        rootpart.PhysActor.AddForce(impulse, true);
                        m_scene.PhysicsScene.AddPhysicsActorTaint(rootpart.PhysActor);
                    }
                }
            }
        }

        public void applyAngularImpulse(Vector3 impulse)
        {
            // We check if rootpart is null here because scripts don't delete if you delete the host.
            // This means that unfortunately, we can pass a null physics actor to Simulate!
            // Make sure we don't do that!
            SceneObjectPart rootpart = m_rootPart;
            if (rootpart != null)
            {
                if (rootpart.PhysActor != null)
                {
                    if (!IsAttachment)
                    {
                        rootpart.PhysActor.AddAngularForce(impulse, true);
                        m_scene.PhysicsScene.AddPhysicsActorTaint(rootpart.PhysActor);
                    }
                }
            }
        }

        public void setAngularImpulse(Vector3 impulse)
        {
            // We check if rootpart is null here because scripts don't delete if you delete the host.
            // This means that unfortunately, we can pass a null physics actor to Simulate!
            // Make sure we don't do that!
            SceneObjectPart rootpart = m_rootPart;
            if (rootpart != null)
            {
                if (rootpart.PhysActor != null)
                {
                    if (!IsAttachment)
                    {
                        rootpart.PhysActor.Torque = impulse;
                        m_scene.PhysicsScene.AddPhysicsActorTaint(rootpart.PhysActor);
                    }
                }
            }
        }

        public Vector3 GetTorque()
        {
            // We check if rootpart is null here because scripts don't delete if you delete the host.
            // This means that unfortunately, we can pass a null physics actor to Simulate!
            // Make sure we don't do that!
            SceneObjectPart rootpart = m_rootPart;
            if (rootpart != null)
            {
                if (rootpart.PhysActor != null)
                {
                    if (!IsAttachment)
                    {
                        Vector3 torque = rootpart.PhysActor.Torque;
                        return torque;
                    }
                }
            }
            return Vector3.Zero;
        }

        public void moveToTarget(Vector3 target, float tau)
        {
            SceneObjectPart rootpart = m_rootPart;
            if (rootpart != null)
            {
                if (IsAttachment)
                {
                    ScenePresence avatar = m_scene.GetScenePresence(AttachedAvatar);
                    if (avatar != null)
                    {
                        avatar.MoveToTarget(target, false);
                    }
                }
                else
                {
                    if (rootpart.PhysActor != null)
                    {
                        rootpart.PhysActor.PIDTarget = target;
                        rootpart.PhysActor.PIDTau = tau;
                        rootpart.PhysActor.PIDActive = true;
                    }
                }
            }
        }

        public void stopMoveToTarget()
        {
            SceneObjectPart rootpart = m_rootPart;
            if (rootpart != null)
            {
                if (rootpart.PhysActor != null)
                {
                    rootpart.PhysActor.PIDActive = false;
                }
            }
        }
        
        public void stopLookAt()
        {
            SceneObjectPart rootpart = m_rootPart;
            if (rootpart != null)
            {
                if (rootpart.PhysActor != null)
                {
                    rootpart.PhysActor.APIDActive = false;
                }
            }
        
        }

        /// <summary>
        /// Uses a PID to attempt to clamp the object on the Z axis at the given height over tau seconds.
        /// </summary>
        /// <param name="height">Height to hover.  Height of zero disables hover.</param>
        /// <param name="hoverType">Determines what the height is relative to </param>
        /// <param name="tau">Number of seconds over which to reach target</param>
        public void SetHoverHeight(float height, PIDHoverType hoverType, float tau)
        {
            SceneObjectPart rootpart = m_rootPart;
            if (rootpart != null)
            {
                if (rootpart.PhysActor != null)
                {
                    if (height != 0f)
                    {
                        rootpart.PhysActor.PIDHoverHeight = height;
                        rootpart.PhysActor.PIDHoverType = hoverType;
                        rootpart.PhysActor.PIDTau = tau;
                        rootpart.PhysActor.PIDHoverActive = true;
                    }
                    else
                    {
                        rootpart.PhysActor.PIDHoverActive = false;
                    }
                }
            }
        }

        /// <summary>
        /// Set the owner of the root part.
        /// </summary>
        /// <param name="part"></param>
        /// <param name="cAgentID"></param>
        /// <param name="cGroupID"></param>
        public void SetRootPartOwner(SceneObjectPart part, UUID cAgentID, UUID cGroupID)
        {
            part.LastOwnerID = part.OwnerID;
            part.OwnerID = cAgentID;
            part.GroupID = cGroupID;

            if (part.OwnerID != cAgentID)
            {
                // Apply Next Owner Permissions if we're not bypassing permissions
                if (!m_scene.Permissions.BypassPermissions())
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
        public SceneObjectPart CopyPart(SceneObjectPart part, UUID cAgentID, UUID cGroupID, bool userExposed)
        {
            SceneObjectPart newPart = part.Copy(m_scene.AllocateLocalId(), OwnerID, GroupID, m_parts.Count, userExposed);
            AddPart(newPart);

            SetPartAsNonRoot(newPart);
            return newPart;
        }

        /// <summary>
        /// Reset the UUIDs for all the prims that make up this group.
        ///
        /// This is called by methods which want to add a new group to an existing scene, in order
        /// to ensure that there are no clashes with groups already present.
        /// </summary>
        public void ResetIDs()
        {
            lock (m_parts.SyncRoot)
            {
                List<SceneObjectPart> partsList = new List<SceneObjectPart>(m_parts.GetArray());
                m_parts.Clear();
                foreach (SceneObjectPart part in partsList)
                {
                    part.ResetIDs(part.LinkNum); // Don't change link nums
                    m_parts.Add(part.UUID, part);
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="part"></param>
        public void ServiceObjectPropertiesFamilyRequest(IClientAPI remoteClient, UUID AgentID, uint RequestFlags)
        {
            remoteClient.SendObjectPropertiesFamilyData(RootPart, RequestFlags);
            
//             remoteClient.SendObjectPropertiesFamilyData(RequestFlags, RootPart.UUID, RootPart.OwnerID, RootPart.GroupID, RootPart.BaseMask,
//                                                         RootPart.OwnerMask, RootPart.GroupMask, RootPart.EveryoneMask, RootPart.NextOwnerMask,
//                                                         RootPart.OwnershipCost, RootPart.ObjectSaleType, RootPart.SalePrice, RootPart.Category,
//                                                         RootPart.CreatorID, RootPart.Name, RootPart.Description);
        }

        public void SetPartOwner(SceneObjectPart part, UUID cAgentID, UUID cGroupID)
        {
            part.OwnerID = cAgentID;
            part.GroupID = cGroupID;
        }

        #endregion

        #region Scheduling

        public override void Update()
        {
            // Check that the group was not deleted before the scheduled update
            // FIXME: This is merely a temporary measure to reduce the incidence of failure when
            // an object has been deleted from a scene before update was processed.
            // A more fundamental overhaul of the update mechanism is required to eliminate all
            // the race conditions.
            if (IsDeleted)
                return;

            // Even temporary objects take part in physics (e.g. temp-on-rez bullets)
            //if ((RootPart.Flags & PrimFlags.TemporaryOnRez) != 0)
            //    return;

            bool UsePhysics = ((RootPart.Flags & PrimFlags.Physics) != 0);

            if (UsePhysics && !AbsolutePosition.ApproxEquals(lastPhysGroupPos, 0.02f))
            {
                m_rootPart.UpdateFlag = 1;
                lastPhysGroupPos = AbsolutePosition;
            }

            if (UsePhysics && !GroupRotation.ApproxEquals(lastPhysGroupRot, 0.1f))
            {
                m_rootPart.UpdateFlag = 1;
                lastPhysGroupRot = GroupRotation;
            }

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (!IsSelected)
                    part.UpdateLookAt();
                part.SendScheduledUpdates();
            }
        }

        public void ScheduleFullUpdateToAvatar(ScenePresence presence)
        {
//            m_log.DebugFormat("[SOG]: Scheduling full update for {0} {1} just to avatar {2}", Name, UUID, presence.Name);
            
            RootPart.AddFullUpdateToAvatar(presence);

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part != RootPart)
                    part.AddFullUpdateToAvatar(presence);
            }
        }

        public void ScheduleTerseUpdateToAvatar(ScenePresence presence)
        {
//            m_log.DebugFormat("[SOG]: Scheduling terse update for {0} {1} just to avatar {2}", Name, UUID, presence.Name);

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].AddTerseUpdateToAvatar(presence);
        }

        /// <summary>
        /// Schedule a full update for this scene object
        /// </summary>
        public void ScheduleGroupForFullUpdate()
        {
//            if (IsAttachment)
//                m_log.DebugFormat("[SOG]: Scheduling full update for {0} {1}", Name, LocalId);
            
            checkAtTargets();
            RootPart.ScheduleFullUpdate();

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part != RootPart)
                    part.ScheduleFullUpdate();
            }
        }

        /// <summary>
        /// Schedule a terse update for this scene object
        /// </summary>
        public void ScheduleGroupForTerseUpdate()
        {
//            m_log.DebugFormat("[SOG]: Scheduling terse update for {0} {1}", Name, UUID);

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].ScheduleTerseUpdate();
        }

        /// <summary>
        /// Immediately send a full update for this scene object.
        /// </summary>
        public void SendGroupFullUpdate()
        {
            if (IsDeleted)
                return;

//            m_log.DebugFormat("[SOG]: Sending immediate full group update for {0} {1}", Name, UUID);
            
            RootPart.SendFullUpdateToAllClients();

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                if (part != RootPart)
                    part.SendFullUpdateToAllClients();
            }
        }

        /// <summary>
        /// Immediately send an update for this scene object's root prim only.
        /// This is for updates regarding the object as a whole, and none of its parts in particular.
        /// Note: this may not be used by opensim (it probably should) but it's used by
        /// external modules.
        /// </summary>
        public void SendGroupRootTerseUpdate()
        {
            if (IsDeleted)
                return;

            RootPart.SendTerseUpdateToAllClients();
        }

        public void QueueForUpdateCheck()
        {
            if (m_scene == null) // Need to check here as it's null during object creation
                return;
            
            m_scene.SceneGraph.AddToUpdateList(this);
        }

        /// <summary>
        /// Immediately send a terse update for this scene object.
        /// </summary>
        public void SendGroupTerseUpdate()
        {
            if (IsDeleted)
                return;

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].SendTerseUpdateToAllClients();
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
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].LinkNum == linknum)
                    return parts[i];
            }

            return null;
        }

        /// <summary>
        /// Get a part with a given UUID
        /// </summary>
        /// <param name="primID"></param>
        /// <returns>null if a child part with the primID was not found</returns>
        public SceneObjectPart GetChildPart(UUID primID)
        {
            SceneObjectPart childPart;
            m_parts.TryGetValue(primID, out childPart);
            return childPart;
        }

        /// <summary>
        /// Get a part with a given local ID
        /// </summary>
        /// <param name="localID"></param>
        /// <returns>null if a child part with the local ID was not found</returns>
        public SceneObjectPart GetChildPart(uint localID)
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].LocalId == localID)
                    return parts[i];
            }

            return null;
        }

        /// <summary>
        /// Does this group contain the child prim
        /// should be able to remove these methods once we have a entity index in scene
        /// </summary>
        /// <param name="primID"></param>
        /// <returns></returns>
        public bool HasChildPrim(UUID primID)
        {
            return m_parts.ContainsKey(primID);
        }

        /// <summary>
        /// Does this group contain the child prim
        /// should be able to remove these methods once we have a entity index in scene
        /// </summary>
        /// <param name="localID"></param>
        /// <returns></returns>
        public bool HasChildPrim(uint localID)
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].LocalId == localID)
                    return true;
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
            // Make sure we have sent any pending unlinks or stuff.
            //if (objectGroup.RootPart.UpdateFlag > 0)
            //{
            //    m_log.WarnFormat(
            //        "[SCENE OBJECT GROUP]: Forcing send of linkset {0}, {1} to {2}, {3} as its still waiting.",
            //        objectGroup.RootPart.Name, objectGroup.RootPart.UUID, RootPart.Name, RootPart.UUID);

            //    objectGroup.RootPart.SendScheduledUpdates();
            //}

//            m_log.DebugFormat(
//                "[SCENE OBJECT GROUP]: Linking group with root part {0}, {1} to group with root part {2}, {3}",
//                objectGroup.RootPart.Name, objectGroup.RootPart.UUID, RootPart.Name, RootPart.UUID);

            SceneObjectPart linkPart = objectGroup.m_rootPart;

            Vector3 oldGroupPosition = linkPart.GroupPosition;
            Quaternion oldRootRotation = linkPart.RotationOffset;

            linkPart.OffsetPosition = linkPart.GroupPosition - AbsolutePosition;
            linkPart.GroupPosition = AbsolutePosition;
            Vector3 axPos = linkPart.OffsetPosition;

            Quaternion parentRot = m_rootPart.RotationOffset;
            axPos *= Quaternion.Inverse(parentRot);

            linkPart.OffsetPosition = axPos;
            Quaternion oldRot = linkPart.RotationOffset;
            Quaternion newRot = Quaternion.Inverse(parentRot) * oldRot;
            linkPart.RotationOffset = newRot;

            linkPart.ParentID = m_rootPart.LocalId;
            if (m_rootPart.LinkNum == 0)
                m_rootPart.LinkNum = 1;

            lock (m_parts.SyncRoot)
            {
                m_parts.Add(linkPart.UUID, linkPart);

                // Insert in terms of link numbers, the new links
                // before the current ones (with the exception of 
                // the root prim. Shuffle the old ones up
                SceneObjectPart[] parts = m_parts.GetArray();
                for (int i = 0; i < parts.Length; i++)
                {
                    SceneObjectPart part = parts[i];
                    if (part.LinkNum != 1)
                    {
                        // Don't update root prim link number
                        part.LinkNum += objectGroup.PrimCount;
                    }
                }

                linkPart.LinkNum = 2;

                linkPart.SetParent(this);
                linkPart.CreateSelected = true;

                //if (linkPart.PhysActor != null)
                //{
                // m_scene.PhysicsScene.RemovePrim(linkPart.PhysActor);

                //linkPart.PhysActor = null;
                //}

                //TODO: rest of parts
                int linkNum = 3;
                SceneObjectPart[] ogParts = objectGroup.Parts;
                for (int i = 0; i < ogParts.Length; i++)
                {
                    SceneObjectPart part = ogParts[i];
                    if (part.UUID != objectGroup.m_rootPart.UUID)
                        LinkNonRootPart(part, oldGroupPosition, oldRootRotation, linkNum++);
                    part.ClearUndoState();
                }
            }

            m_scene.UnlinkSceneObject(objectGroup, true);
            objectGroup.IsDeleted = true;

            objectGroup.m_parts.Clear();
            
            // Can't do this yet since backup still makes use of the root part without any synchronization
//            objectGroup.m_rootPart = null;

            AttachToBackup();

            // Here's the deal, this is ABSOLUTELY CRITICAL so the physics scene gets the update about the 
            // position of linkset prims.  IF YOU CHANGE THIS, YOU MUST TEST colliding with just linked and 
            // unmoved prims!
            ResetChildPrimPhysicsPositions();

            //HasGroupChanged = true;
            //ScheduleGroupForFullUpdate();
        }

        /// <summary>
        /// Delink the given prim from this group.  The delinked prim is established as
        /// an independent SceneObjectGroup.
        /// </summary>
        /// <param name="partID"></param>
        /// <returns>The object group of the newly delinked prim.  Null if part could not be found</returns>
        public SceneObjectGroup DelinkFromGroup(uint partID)
        {
            return DelinkFromGroup(partID, true);
        }

        /// <summary>
        /// Delink the given prim from this group.  The delinked prim is established as
        /// an independent SceneObjectGroup.
        /// </summary>
        /// <param name="partID"></param>
        /// <param name="sendEvents"></param>
        /// <returns>The object group of the newly delinked prim.  Null if part could not be found</returns>
        public SceneObjectGroup DelinkFromGroup(uint partID, bool sendEvents)
        {
            SceneObjectPart linkPart = GetChildPart(partID);

            if (linkPart != null)
            {
                return DelinkFromGroup(linkPart, sendEvents);
            }
            else
            {
                m_log.WarnFormat("[SCENE OBJECT GROUP]: " +
                                 "DelinkFromGroup(): Child prim {0} not found in object {1}, {2}",
                                 partID, LocalId, UUID);

                return null;
            }
        }

        /// <summary>
        /// Delink the given prim from this group.  The delinked prim is established as
        /// an independent SceneObjectGroup.
        /// </summary>
        /// <param name="partID"></param>
        /// <param name="sendEvents"></param>
        /// <returns>The object group of the newly delinked prim.</returns>
        public SceneObjectGroup DelinkFromGroup(SceneObjectPart linkPart, bool sendEvents)
        {
//                m_log.DebugFormat(
//                    "[SCENE OBJECT GROUP]: Delinking part {0}, {1} from group with root part {2}, {3}",
//                    linkPart.Name, linkPart.UUID, RootPart.Name, RootPart.UUID);
            
            linkPart.ClearUndoState();

            Quaternion worldRot = linkPart.GetWorldRotation();

            // Remove the part from this object
            lock (m_parts.SyncRoot)
            {
                m_parts.Remove(linkPart.UUID);

                SceneObjectPart[] parts = m_parts.GetArray();

                if (parts.Length == 1 && RootPart != null)
                {
                    // Single prim left
                    RootPart.LinkNum = 0;
                }
                else
                {
                    for (int i = 0; i < parts.Length; i++)
                    {
                        SceneObjectPart part = parts[i];
                        if (part.LinkNum > linkPart.LinkNum)
                            part.LinkNum--;
                    }
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
            Quaternion parentRot = m_rootPart.RotationOffset;

            Vector3 axPos = linkPart.OffsetPosition;

            axPos *= parentRot;
            linkPart.OffsetPosition = new Vector3(axPos.X, axPos.Y, axPos.Z);
            linkPart.GroupPosition = AbsolutePosition + linkPart.OffsetPosition;
            linkPart.OffsetPosition = new Vector3(0, 0, 0);

            linkPart.RotationOffset = worldRot;

            SceneObjectGroup objectGroup = new SceneObjectGroup(linkPart);

            m_scene.AddNewSceneObject(objectGroup, true);

            if (sendEvents)
                linkPart.TriggerScriptChangedEvent(Changed.LINK);

            linkPart.Rezzed = RootPart.Rezzed;

            // When we delete a group, we currently have to force persist to the database if the object id has changed
            // (since delete works by deleting all rows which have a given object id)
            objectGroup.HasGroupChangedDueToDelink = true;

            return objectGroup;
        }

        /// <summary>
        /// Stop this object from being persisted over server restarts.
        /// </summary>
        /// <param name="objectGroup"></param>
        public virtual void DetachFromBackup()
        {
            if (m_isBackedUp && Scene != null)
                m_scene.EventManager.OnBackup -= ProcessBackup;
            
            m_isBackedUp = false;
        }

        private void LinkNonRootPart(SceneObjectPart part, Vector3 oldGroupPosition, Quaternion oldGroupRotation, int linkNum)
        {
            Quaternion parentRot = oldGroupRotation;
            Quaternion oldRot = part.RotationOffset;
            Quaternion worldRot = parentRot * oldRot;

            parentRot = oldGroupRotation;

            Vector3 axPos = part.OffsetPosition;

            axPos *= parentRot;
            part.OffsetPosition = axPos;
            part.GroupPosition = oldGroupPosition + part.OffsetPosition;
            part.OffsetPosition = Vector3.Zero;
            part.RotationOffset = worldRot;

            part.SetParent(this);
            part.ParentID = m_rootPart.LocalId;

            m_parts.Add(part.UUID, part);

            part.LinkNum = linkNum;

            part.OffsetPosition = part.GroupPosition - AbsolutePosition;

            Quaternion rootRotation = m_rootPart.RotationOffset;

            Vector3 pos = part.OffsetPosition;
            pos *= Quaternion.Inverse(rootRotation);
            part.OffsetPosition = pos;

            parentRot = m_rootPart.RotationOffset;
            oldRot = part.RotationOffset;
            Quaternion newRot = Quaternion.Inverse(parentRot) * oldRot;
            part.RotationOffset = newRot;
        }

        /// <summary>
        /// If object is physical, apply force to move it around
        /// If object is not physical, just put it at the resulting location
        /// </summary>
        /// <param name="offset">Always seems to be 0,0,0, so ignoring</param>
        /// <param name="pos">New position.  We do the math here to turn it into a force</param>
        /// <param name="remoteClient"></param>
        public void GrabMovement(Vector3 offset, Vector3 pos, IClientAPI remoteClient)
        {
            if (m_scene.EventManager.TriggerGroupMove(UUID, pos))
            {
                if (m_rootPart.PhysActor != null)
                {
                    if (m_rootPart.PhysActor.IsPhysical)
                    {
                        if (!m_rootPart.BlockGrab)
                        {
                            Vector3 llmoveforce = pos - AbsolutePosition;
                            Vector3 grabforce = llmoveforce;
                            grabforce = (grabforce / 10) * m_rootPart.PhysActor.Mass;
                            m_rootPart.PhysActor.AddForce(grabforce, true);
                            m_scene.PhysicsScene.AddPhysicsActorTaint(m_rootPart.PhysActor);
                        }
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

        public void NonPhysicalGrabMovement(Vector3 pos)
        {
            AbsolutePosition = pos;
            m_rootPart.SendTerseUpdateToAllClients();
        }

        /// <summary>
        /// If object is physical, prepare for spinning torques (set flag to save old orientation)
        /// </summary>
        /// <param name="rotation">Rotation.  We do the math here to turn it into a torque</param>
        /// <param name="remoteClient"></param>
        public void SpinStart(IClientAPI remoteClient)
        {
            if (m_scene.EventManager.TriggerGroupSpinStart(UUID))
            {
                if (m_rootPart.PhysActor != null)
                {
                    if (m_rootPart.PhysActor.IsPhysical)
                    {
                        m_rootPart.IsWaitingForFirstSpinUpdatePacket = true;
                    }
                }
            }
        }

        /// <summary>
        /// If object is physical, apply torque to spin it around
        /// </summary>
        /// <param name="rotation">Rotation.  We do the math here to turn it into a torque</param>
        /// <param name="remoteClient"></param>
        public void SpinMovement(Quaternion newOrientation, IClientAPI remoteClient)
        {
            // The incoming newOrientation, sent by the client, "seems" to be the 
            // desired target orientation. This needs further verification; in particular, 
            // one would expect that the initial incoming newOrientation should be
            // fairly close to the original prim's physical orientation, 
            // m_rootPart.PhysActor.Orientation. This however does not seem to be the
            // case (might just be an issue with different quaternions representing the
            // same rotation, or it might be a coordinate system issue).
            //
            // Since it's not clear what the relationship is between the PhysActor.Orientation
            // and the incoming orientations sent by the client, we take an alternative approach
            // of calculating the delta rotation between the orientations being sent by the 
            // client. (Since a spin is invoked by ctrl+shift+drag in the client, we expect
            // a steady stream of several new orientations coming in from the client.)
            // This ensures that the delta rotations are being calculated from self-consistent
            // pairs of old/new rotations. Given the delta rotation, we apply a torque around
            // the delta rotation axis, scaled by the object mass times an arbitrary scaling
            // factor (to ensure the resulting torque is not "too strong" or "too weak").
            // 
            // Ideally we need to calculate (probably iteratively) the exact torque or series
            // of torques needed to arrive exactly at the destination orientation. However, since 
            // it is not yet clear how to map the destination orientation (provided by the viewer)
            // into PhysActor orientations (needed by the physics engine), we omit this step. 
            // This means that the resulting torque will at least be in the correct direction, 
            // but it will result in over-shoot or under-shoot of the target orientation.
            // For the end user, this means that ctrl+shift+drag can be used for relative,
            // but not absolute, adjustments of orientation for physical prims.
          
            if (m_scene.EventManager.TriggerGroupSpin(UUID, newOrientation))
            {
                if (m_rootPart.PhysActor != null)
                {
                    if (m_rootPart.PhysActor.IsPhysical)
                    {
                        if (m_rootPart.IsWaitingForFirstSpinUpdatePacket)
                        {
                            // first time initialization of "old" orientation for calculation of delta rotations
                            m_rootPart.SpinOldOrientation = newOrientation;
                            m_rootPart.IsWaitingForFirstSpinUpdatePacket = false;
                        }
                        else
                        {
                          // save and update old orientation
                          Quaternion old = m_rootPart.SpinOldOrientation;
                          m_rootPart.SpinOldOrientation = newOrientation;
                          //m_log.Error("[SCENE OBJECT GROUP]: Old orientation is " + old);
                          //m_log.Error("[SCENE OBJECT GROUP]: Incoming new orientation is " + newOrientation);

                          // compute difference between previous old rotation and new incoming rotation
                          Quaternion minimalRotationFromQ1ToQ2 = Quaternion.Inverse(old) * newOrientation;

                          float rotationAngle;
                          Vector3 rotationAxis;
                          minimalRotationFromQ1ToQ2.GetAxisAngle(out rotationAxis, out rotationAngle);
                          rotationAxis.Normalize();

                          //m_log.Error("SCENE OBJECT GROUP]: rotation axis is " + rotationAxis);
                          Vector3 spinforce = new Vector3(rotationAxis.X, rotationAxis.Y, rotationAxis.Z);
                          spinforce = (spinforce/8) * m_rootPart.PhysActor.Mass; // 8 is an arbitrary torque scaling factor
                          m_rootPart.PhysActor.AddAngularForce(spinforce,true);
                          m_scene.PhysicsScene.AddPhysicsActorTaint(m_rootPart.PhysActor);
                        }
                    }
                    else
                    {
                        //NonPhysicalSpinMovement(pos);
                    }
                }
                else
                {
                    //NonPhysicalSpinMovement(pos);
                }
            }
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

        public void SetPartText(string text, UUID partID)
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
        /// <param name="UsePhysics"></param>
        /// <param name="SetTemporary"></param>
        /// <param name="SetPhantom"></param>
        /// <param name="SetVolumeDetect"></param>
        public void UpdatePrimFlags(uint localID, bool UsePhysics, bool SetTemporary, bool SetPhantom, bool SetVolumeDetect)
        {
            SceneObjectPart selectionPart = GetChildPart(localID);

            if (SetTemporary && Scene != null)
            {
                DetachFromBackup();
                // Remove from database and parcel prim count
                //
                m_scene.DeleteFromStorage(UUID);
                m_scene.EventManager.TriggerParcelPrimCountTainted();
            }

            if (selectionPart != null)
            {
                SceneObjectPart[] parts = m_parts.GetArray();
                
                if (Scene != null)
                {
                    for (int i = 0; i < parts.Length; i++)
                    {
                        SceneObjectPart part = parts[i];
                        if (part.Scale.X > m_scene.RegionInfo.PhysPrimMax || 
                            part.Scale.Y > m_scene.RegionInfo.PhysPrimMax || 
                            part.Scale.Z > m_scene.RegionInfo.PhysPrimMax)
                        {
                            UsePhysics = false; // Reset physics
                            break;
                        }
                    }
                }

                for (int i = 0; i < parts.Length; i++)
                    parts[i].UpdatePrimFlags(UsePhysics, SetTemporary, SetPhantom, SetVolumeDetect);
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

        public void UpdatePermissions(UUID AgentID, byte field, uint localID,
                uint mask, byte addRemTF)
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].UpdatePermissions(AgentID, field, localID, mask, addRemTF);

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
        /// Resize the entire group of prims.
        /// </summary>
        /// <param name="scale"></param>
        public void GroupResize(Vector3 scale)
        {
//            m_log.DebugFormat(
//                "[SCENE OBJECT GROUP]: Group resizing {0} {1} from {2} to {3}", Name, LocalId, RootPart.Scale, scale);

            RootPart.StoreUndoState(true);

            scale.X = Math.Min(scale.X, Scene.m_maxNonphys);
            scale.Y = Math.Min(scale.Y, Scene.m_maxNonphys);
            scale.Z = Math.Min(scale.Z, Scene.m_maxNonphys);

            if (RootPart.PhysActor != null && RootPart.PhysActor.IsPhysical)
            {
                scale.X = Math.Min(scale.X, Scene.m_maxPhys);
                scale.Y = Math.Min(scale.Y, Scene.m_maxPhys);
                scale.Z = Math.Min(scale.Z, Scene.m_maxPhys);
            }

            float x = (scale.X / RootPart.Scale.X);
            float y = (scale.Y / RootPart.Scale.Y);
            float z = (scale.Z / RootPart.Scale.Z);

            SceneObjectPart[] parts;
            if (x > 1.0f || y > 1.0f || z > 1.0f)
            {
                parts = m_parts.GetArray();
                for (int i = 0; i < parts.Length; i++)
                {
                    SceneObjectPart obPart = parts[i];
                    if (obPart.UUID != m_rootPart.UUID)
                    {
//                        obPart.IgnoreUndoUpdate = true;
                        Vector3 oldSize = new Vector3(obPart.Scale);

                        float f = 1.0f;
                        float a = 1.0f;

                        if (RootPart.PhysActor != null && RootPart.PhysActor.IsPhysical)
                        {
                            if (oldSize.X * x > m_scene.m_maxPhys)
                            {
                                f = m_scene.m_maxPhys / oldSize.X;
                                a = f / x;
                                x *= a;
                                y *= a;
                                z *= a;
                            }

                            if (oldSize.Y * y > m_scene.m_maxPhys)
                            {
                                f = m_scene.m_maxPhys / oldSize.Y;
                                a = f / y;
                                x *= a;
                                y *= a;
                                z *= a;
                            }

                            if (oldSize.Z * z > m_scene.m_maxPhys)
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
                            if (oldSize.X * x > m_scene.m_maxNonphys)
                            {
                                f = m_scene.m_maxNonphys / oldSize.X;
                                a = f / x;
                                x *= a;
                                y *= a;
                                z *= a;
                            }

                            if (oldSize.Y * y > m_scene.m_maxNonphys)
                            {
                                f = m_scene.m_maxNonphys / oldSize.Y;
                                a = f / y;
                                x *= a;
                                y *= a;
                                z *= a;
                            }

                            if (oldSize.Z * z > m_scene.m_maxNonphys)
                            {
                                f = m_scene.m_maxNonphys / oldSize.Z;
                                a = f / z;
                                x *= a;
                                y *= a;
                                z *= a;
                            }
                        }

//                        obPart.IgnoreUndoUpdate = false;
                    }
                }
            }

            Vector3 prevScale = RootPart.Scale;
            prevScale.X *= x;
            prevScale.Y *= y;
            prevScale.Z *= z;

//            RootPart.IgnoreUndoUpdate = true;
            RootPart.Resize(prevScale);
//            RootPart.IgnoreUndoUpdate = false;

            parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart obPart = parts[i];

                if (obPart.UUID != m_rootPart.UUID)
                {
                    obPart.IgnoreUndoUpdate = true;

                    Vector3 currentpos = new Vector3(obPart.OffsetPosition);
                    currentpos.X *= x;
                    currentpos.Y *= y;
                    currentpos.Z *= z;

                    Vector3 newSize = new Vector3(obPart.Scale);
                    newSize.X *= x;
                    newSize.Y *= y;
                    newSize.Z *= z;

                    obPart.Resize(newSize);
                    obPart.UpdateOffSet(currentpos);

                    obPart.IgnoreUndoUpdate = false;                    
                }

//                obPart.IgnoreUndoUpdate = false;
//                obPart.StoreUndoState();
            }

//            m_log.DebugFormat(
//                "[SCENE OBJECT GROUP]: Finished group resizing {0} {1} to {2}", Name, LocalId, RootPart.Scale);
        }

        #endregion

        #region Position

        /// <summary>
        /// Move this scene object
        /// </summary>
        /// <param name="pos"></param>
        public void UpdateGroupPosition(Vector3 pos)
        {
//            m_log.DebugFormat("[SCENE OBJECT GROUP]: Updating group position on {0} {1} to {2}", Name, LocalId, pos);

            RootPart.StoreUndoState(true);

//            SceneObjectPart[] parts = m_parts.GetArray();
//            for (int i = 0; i < parts.Length; i++)
//                parts[i].StoreUndoState();

            if (m_scene.EventManager.TriggerGroupMove(UUID, pos))
            {
                if (IsAttachment)
                {
                    m_rootPart.AttachedPos = pos;
                }
                if (RootPart.GetStatusSandbox())
                {
                    if (Util.GetDistanceTo(RootPart.StatusSandboxPos, pos) > 10)
                    {
                        RootPart.ScriptSetPhysicsStatus(false);
                        pos = AbsolutePosition;
                        Scene.SimChat(Utils.StringToBytes("Hit Sandbox Limit"),
                              ChatTypeEnum.DebugChannel, 0x7FFFFFFF, RootPart.AbsolutePosition, Name, UUID, false);
                    }
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
        public void UpdateSinglePosition(Vector3 pos, uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);

//            SceneObjectPart[] parts = m_parts.GetArray();
//            for (int i = 0; i < parts.Length; i++)
//                parts[i].StoreUndoState();

            if (part != null)
            {
//                m_log.DebugFormat(
//                    "[SCENE OBJECT GROUP]: Updating single position of {0} {1} to {2}", part.Name, part.LocalId, pos);

                part.StoreUndoState(false);
                part.IgnoreUndoUpdate = true;

                if (part.UUID == m_rootPart.UUID)
                {
                    UpdateRootPosition(pos);
                }
                else
                {
                    part.UpdateOffSet(pos);
                }

                HasGroupChanged = true;
                part.IgnoreUndoUpdate = false;
            }
        }

        /// <summary>
        /// Update just the root prim position in a linkset
        /// </summary>
        /// <param name="pos"></param>
        public void UpdateRootPosition(Vector3 pos)
        {
//            m_log.DebugFormat(
//                "[SCENE OBJECT GROUP]: Updating root position of {0} {1} to {2}", Name, LocalId, pos);

//            SceneObjectPart[] parts = m_parts.GetArray();
//            for (int i = 0; i < parts.Length; i++)
//                parts[i].StoreUndoState();

            Vector3 newPos = new Vector3(pos.X, pos.Y, pos.Z);
            Vector3 oldPos =
                new Vector3(AbsolutePosition.X + m_rootPart.OffsetPosition.X,
                              AbsolutePosition.Y + m_rootPart.OffsetPosition.Y,
                              AbsolutePosition.Z + m_rootPart.OffsetPosition.Z);
            Vector3 diff = oldPos - newPos;
            Vector3 axDiff = new Vector3(diff.X, diff.Y, diff.Z);
            Quaternion partRotation = m_rootPart.RotationOffset;
            axDiff *= Quaternion.Inverse(partRotation);
            diff = axDiff;

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart obPart = parts[i];
                if (obPart.UUID != m_rootPart.UUID)
                    obPart.OffsetPosition = obPart.OffsetPosition + diff;
            }

            AbsolutePosition = newPos;

            HasGroupChanged = true;
            ScheduleGroupForTerseUpdate();
        }

        public void OffsetForNewRegion(Vector3 offset)
        {
            m_rootPart.GroupPosition = offset;
        }

        #endregion

        #region Rotation

        /// <summary>
        ///
        /// </summary>
        /// <param name="rot"></param>
        public void UpdateGroupRotationR(Quaternion rot)
        {
//            m_log.DebugFormat(
//                "[SCENE OBJECT GROUP]: Updating group rotation R of {0} {1} to {2}", Name, LocalId, rot);

//            SceneObjectPart[] parts = m_parts.GetArray();
//            for (int i = 0; i < parts.Length; i++)
//                parts[i].StoreUndoState();

            m_rootPart.StoreUndoState(true);

            m_rootPart.UpdateRotation(rot);

            PhysicsActor actor = m_rootPart.PhysActor;
            if (actor != null)
            {
                actor.Orientation = m_rootPart.RotationOffset;
                m_scene.PhysicsScene.AddPhysicsActorTaint(actor);
            }

            HasGroupChanged = true;
            ScheduleGroupForTerseUpdate();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        public void UpdateGroupRotationPR(Vector3 pos, Quaternion rot)
        {
//            m_log.DebugFormat(
//                "[SCENE OBJECT GROUP]: Updating group rotation PR of {0} {1} to {2}", Name, LocalId, rot);

//            SceneObjectPart[] parts = m_parts.GetArray();
//            for (int i = 0; i < parts.Length; i++)
//                parts[i].StoreUndoState();

            RootPart.StoreUndoState(true);
            RootPart.IgnoreUndoUpdate = true;

            m_rootPart.UpdateRotation(rot);

            PhysicsActor actor = m_rootPart.PhysActor;
            if (actor != null)
            {
                actor.Orientation = m_rootPart.RotationOffset;
                m_scene.PhysicsScene.AddPhysicsActorTaint(actor);
            }

            AbsolutePosition = pos;

            HasGroupChanged = true;
            ScheduleGroupForTerseUpdate();

            RootPart.IgnoreUndoUpdate = false;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="rot"></param>
        /// <param name="localID"></param>
        public void UpdateSingleRotation(Quaternion rot, uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].StoreUndoState();

            if (part != null)
            {
//                m_log.DebugFormat(
//                    "[SCENE OBJECT GROUP]: Updating single rotation of {0} {1} to {2}", part.Name, part.LocalId, rot);

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
        /// <param name="localID"></param>
        public void UpdateSingleRotation(Quaternion rot, Vector3 pos, uint localID)
        {
            SceneObjectPart part = GetChildPart(localID);
            if (part != null)
            {
//                m_log.DebugFormat(
//                    "[SCENE OBJECT GROUP]: Updating single position and rotation of {0} {1} to {2}",
//                    part.Name, part.LocalId, rot);

                part.StoreUndoState();
                part.IgnoreUndoUpdate = true;

                if (part.UUID == m_rootPart.UUID)
                {
                    UpdateRootRotation(rot);
                    AbsolutePosition = pos;
                }
                else
                {
                    part.UpdateRotation(rot);
                    part.OffsetPosition = pos;
                }

                part.IgnoreUndoUpdate = false;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="rot"></param>
        public void UpdateRootRotation(Quaternion rot)
        {
//            m_log.DebugFormat(
//                "[SCENE OBJECT GROUP]: Updating root rotation of {0} {1} to {2}",
//                Name, LocalId, rot);

            Quaternion axRot = rot;
            Quaternion oldParentRot = m_rootPart.RotationOffset;

            m_rootPart.StoreUndoState();
            m_rootPart.UpdateRotation(rot);
            if (m_rootPart.PhysActor != null)
            {
                m_rootPart.PhysActor.Orientation = m_rootPart.RotationOffset;
                m_scene.PhysicsScene.AddPhysicsActorTaint(m_rootPart.PhysActor);
            }

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart prim = parts[i];
                if (prim.UUID != m_rootPart.UUID)
                {
                    prim.IgnoreUndoUpdate = true;
                    Vector3 axPos = prim.OffsetPosition;
                    axPos *= oldParentRot;
                    axPos *= Quaternion.Inverse(axRot);
                    prim.OffsetPosition = axPos;
                    Quaternion primsRot = prim.RotationOffset;
                    Quaternion newRot = primsRot * oldParentRot;
                    newRot *= Quaternion.Inverse(axRot);
                    prim.RotationOffset = newRot;
                    prim.ScheduleTerseUpdate();
                    prim.IgnoreUndoUpdate = false;
                }
            }

//            for (int i = 0; i < parts.Length; i++)
//            {
//                SceneObjectPart childpart = parts[i];
//                if (childpart != m_rootPart)
//                {
////                    childpart.IgnoreUndoUpdate = false;
////                    childpart.StoreUndoState();
//                }
//            }

            m_rootPart.ScheduleTerseUpdate();

//            m_log.DebugFormat(
//                "[SCENE OBJECT GROUP]: Updated root rotation of {0} {1} to {2}",
//                Name, LocalId, rot);
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
                    m_rootPart.RotationAxis.X = setval;
                if (setY)
                    m_rootPart.RotationAxis.Y = setval;
                if (setZ)
                    m_rootPart.RotationAxis.Z = setval;

                if (setX || setY || setZ)
                {
                    m_rootPart.SetPhysicsAxisRotation();
                }

            }
        }
        public int registerRotTargetWaypoint(Quaternion target, float tolerance)
        {
            scriptRotTarget waypoint = new scriptRotTarget();
            waypoint.targetRot = target;
            waypoint.tolerance = tolerance;
            uint handle = m_scene.AllocateLocalId();
            waypoint.handle = handle;
            lock (m_rotTargets)
            {
                m_rotTargets.Add(handle, waypoint);
            }
            m_scene.AddGroupTarget(this);
            return (int)handle;
        }

        public void unregisterRotTargetWaypoint(int handle)
        {
            lock (m_targets)
            {
                m_rotTargets.Remove((uint)handle);
                if (m_targets.Count == 0)
                    m_scene.RemoveGroupTarget(this);
            }
        }

        public int registerTargetWaypoint(Vector3 target, float tolerance)
        {
            scriptPosTarget waypoint = new scriptPosTarget();
            waypoint.targetPos = target;
            waypoint.tolerance = tolerance;
            uint handle = m_scene.AllocateLocalId();
            waypoint.handle = handle;
            lock (m_targets)
            {
                m_targets.Add(handle, waypoint);
            }
            m_scene.AddGroupTarget(this);
            return (int)handle;
        }
        
        public void unregisterTargetWaypoint(int handle)
        {
            lock (m_targets)
            {
                m_targets.Remove((uint)handle);
                if (m_targets.Count == 0)
                    m_scene.RemoveGroupTarget(this);
            }
        }

        public void checkAtTargets()
        {
            if (m_scriptListens_atTarget || m_scriptListens_notAtTarget)
            {
                if (m_targets.Count > 0)
                {
                    bool at_target = false;
                    //Vector3 targetPos;
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
                                    at_target = true;
                                    scriptPosTarget att = new scriptPosTarget();
                                    att.targetPos = target.targetPos;
                                    att.tolerance = target.tolerance;
                                    att.handle = target.handle;
                                    atTargets.Add(idx, att);
                                }
                            }
                        }
                    }
                    
                    if (atTargets.Count > 0)
                    {
                        SceneObjectPart[] parts = m_parts.GetArray();
                        uint[] localids = new uint[parts.Length];
                        for (int i = 0; i < parts.Length; i++)
                            localids[i] = parts[i].LocalId;
                        
                        for (int ctr = 0; ctr < localids.Length; ctr++)
                        {
                            foreach (uint target in atTargets.Keys)
                            {
                                scriptPosTarget att = atTargets[target];
                                m_scene.EventManager.TriggerAtTargetEvent(
                                    localids[ctr], att.handle, att.targetPos, m_rootPart.GroupPosition);
                            }
                        }
                        
                        return;
                    }
                    
                    if (m_scriptListens_notAtTarget && !at_target)
                    {
                        //trigger not_at_target
                        SceneObjectPart[] parts = m_parts.GetArray();
                        uint[] localids = new uint[parts.Length];
                        for (int i = 0; i < parts.Length; i++)
                            localids[i] = parts[i].LocalId;
                        
                        for (int ctr = 0; ctr < localids.Length; ctr++)
                        {
                            m_scene.EventManager.TriggerNotAtTargetEvent(localids[ctr]);
                        }
                    }
                }
            }
            if (m_scriptListens_atRotTarget || m_scriptListens_notAtRotTarget)
            {
                if (m_rotTargets.Count > 0)
                {
                    bool at_Rottarget = false;
                    Dictionary<uint, scriptRotTarget> atRotTargets = new Dictionary<uint, scriptRotTarget>();
                    lock (m_rotTargets)
                    {
                        foreach (uint idx in m_rotTargets.Keys)
                        {
                            scriptRotTarget target = m_rotTargets[idx];
                            double angle = Math.Acos(target.targetRot.X * m_rootPart.RotationOffset.X + target.targetRot.Y * m_rootPart.RotationOffset.Y + target.targetRot.Z * m_rootPart.RotationOffset.Z + target.targetRot.W * m_rootPart.RotationOffset.W) * 2;
                            if (angle < 0) angle = -angle;
                            if (angle > Math.PI) angle = (Math.PI * 2 - angle);
                            if (angle <= target.tolerance)
                            {
                                // trigger at_rot_target
                                if (m_scriptListens_atRotTarget)
                                {
                                    at_Rottarget = true;
                                    scriptRotTarget att = new scriptRotTarget();
                                    att.targetRot = target.targetRot;
                                    att.tolerance = target.tolerance;
                                    att.handle = target.handle;
                                    atRotTargets.Add(idx, att);
                                }
                            }
                        }
                    }

                    if (atRotTargets.Count > 0)
                    {
                        SceneObjectPart[] parts = m_parts.GetArray();
                        uint[] localids = new uint[parts.Length];
                        for (int i = 0; i < parts.Length; i++)
                            localids[i] = parts[i].LocalId;

                        for (int ctr = 0; ctr < localids.Length; ctr++)
                        {
                            foreach (uint target in atRotTargets.Keys)
                            {
                                scriptRotTarget att = atRotTargets[target];
                                m_scene.EventManager.TriggerAtRotTargetEvent(
                                    localids[ctr], att.handle, att.targetRot, m_rootPart.RotationOffset);
                            }
                        }

                        return;
                    }

                    if (m_scriptListens_notAtRotTarget && !at_Rottarget)
                    {
                        //trigger not_at_target
                        SceneObjectPart[] parts = m_parts.GetArray();
                        uint[] localids = new uint[parts.Length];
                        for (int i = 0; i < parts.Length; i++)
                            localids[i] = parts[i].LocalId;

                        for (int ctr = 0; ctr < localids.Length; ctr++)
                        {
                            m_scene.EventManager.TriggerNotAtRotTargetEvent(localids[ctr]);
                        }
                    }
                }
            }
        }
        
        public float GetMass()
        {
            float retmass = 0f;

            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                retmass += parts[i].GetMass();

            return retmass;
        }

        /// <summary>
        /// If the object is a sculpt/mesh, retrieve the mesh data for each part and reinsert it into each shape so that
        /// the physics engine can use it.
        /// </summary>
        /// <remarks>
        /// When the physics engine has finished with it, the sculpt data is discarded to save memory.
        /// </remarks>
        public void CheckSculptAndLoad()
        {
            if (IsDeleted)
                return;

            if ((RootPart.GetEffectiveObjectFlags() & (uint)PrimFlags.Phantom) != 0)
                return;

//            m_log.Debug("Processing CheckSculptAndLoad for {0} {1}", Name, LocalId);

            SceneObjectPart[] parts = m_parts.GetArray();

            for (int i = 0; i < parts.Length; i++)
                parts[i].CheckSculptAndLoad();
        }

        /// <summary>
        /// Set the user group to which this scene object belongs.
        /// </summary>
        /// <param name="GroupID"></param>
        /// <param name="client"></param>
        public void SetGroup(UUID GroupID, IClientAPI client)
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
            {
                SceneObjectPart part = parts[i];
                part.SetGroup(GroupID, client);
                part.Inventory.ChangeInventoryGroup(GroupID);
            }

            // Don't trigger the update here - otherwise some client issues occur when multiple updates are scheduled
            // for the same object with very different properties.  The caller must schedule the update.
            //ScheduleGroupForFullUpdate();
        }

        public void TriggerScriptChangedEvent(Changed val)
        {
            SceneObjectPart[] parts = m_parts.GetArray();
            for (int i = 0; i < parts.Length; i++)
                parts[i].TriggerScriptChangedEvent(val);
        }
        
        public override string ToString()
        {
            return String.Format("{0} {1} ({2})", Name, UUID, AbsolutePosition);
        }

        #region ISceneObject
        
        public virtual ISceneObject CloneForNewScene()
        {
            SceneObjectGroup sog = Copy(false);
            sog.IsDeleted = false;
            return sog;
        }

        public virtual string ToXml2()
        {
            return SceneObjectSerializer.ToXml2Format(this);
        }

        public virtual string ExtraToXmlString()
        {
            return "<ExtraFromItemID>" + GetFromItemID().ToString() + "</ExtraFromItemID>";
        }

        public virtual void ExtraFromXmlString(string xmlstr)
        {
            string id = xmlstr.Substring(xmlstr.IndexOf("<ExtraFromItemID>"));
            id = xmlstr.Replace("<ExtraFromItemID>", "");
            id = id.Replace("</ExtraFromItemID>", "");

            UUID uuid = UUID.Zero;
            UUID.TryParse(id, out uuid);

            SetFromItemID(uuid);
        }

        #endregion
    }
}
