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
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Xml;
using System.Xml.Serialization;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    // I don't really know where to put this except here.
    // Can't access the OpenSim.Region.ScriptEngine.Common.LSL_BaseClass.Changed constants
    [Flags]
    public enum ExtraParamType
    {
        Something1 = 1,
        Something2 = 2,
        Something3 = 4,
        Something4 = 8,
        Flexible = 16,
        Light = 32,
        Sculpt = 48,
        Something5 = 64,
        Something6 = 128
    }
    [Flags]
    public enum Changed : uint
    {
        INVENTORY = 1,
        COLOR = 2,
        SHAPE = 4,
        SCALE = 8,
        TEXTURE = 16,
        LINK = 32,
        ALLOWED_DROP = 64,
        OWNER = 128
    }
    [Flags]
    public enum TextureAnimFlags : byte
    {
        NONE = 0x00,
        ANIM_ON = 0x01,
        LOOP = 0x02,
        REVERSE = 0x04,
        PING_PONG = 0x08,
        SMOOTH = 0x10,
        ROTATE = 0x20,
        SCALE = 0x40
    }


    [Serializable]
    public partial class SceneObjectPart : IScriptHost, ISerializable
    {
        [XmlIgnore] public PhysicsActor PhysActor = null;

        public LLUUID LastOwnerID;
        public LLUUID OwnerID;
        public LLUUID GroupID;
        public int OwnershipCost;
        public byte ObjectSaleType;
        public int SalePrice;
        public uint Category;

        // TODO: This needs to be persisted in next XML version update!
        [XmlIgnore] public int[] PayPrice = {-2,-2,-2,-2,-2};
        [XmlIgnore] private Dictionary<LLUUID, scriptEvents> m_scriptEvents = new Dictionary<LLUUID, scriptEvents>();
        [XmlIgnore] public scriptEvents m_aggregateScriptEvents=0;
        [XmlIgnore] private LLObject.ObjectFlags LocalFlags = LLObject.ObjectFlags.None;
        [XmlIgnore] public bool DIE_AT_EDGE = false;

        [XmlIgnore] public bool m_IsAttachment = false;
        [XmlIgnore] public uint m_attachmentPoint = (byte)0;
        [XmlIgnore] public LLUUID m_attachedAvatar = LLUUID.Zero;
        [XmlIgnore] public LLVector3 m_attachedPos = LLVector3.Zero;
        [XmlIgnore] public LLUUID fromAssetID = LLUUID.Zero;

        [XmlIgnore] public bool m_undoing = false;

        public Int32 CreationDate;
        public uint ParentID = 0;

        private List<uint> m_lastColliders = new List<uint>();

        private PhysicsVector m_lastRotationalVelocity = PhysicsVector.Zero;
        private Vector3 m_sitTargetPosition = new Vector3(0, 0, 0);
        private Quaternion m_sitTargetOrientation = new Quaternion(0, 0, 0, 1);
        public  LLUUID m_sitTargetAvatar = LLUUID.Zero;
        [XmlIgnore] public PhysicsVector m_rotationAxis = new PhysicsVector(1f,1f,1f);

        #region Permissions

        public uint BaseMask = (uint)PermissionMask.All;
        public uint OwnerMask = (uint)PermissionMask.All;
        public uint GroupMask = (uint)PermissionMask.None;
        public uint EveryoneMask = (uint)PermissionMask.None;
        public uint NextOwnerMask = (uint)PermissionMask.All;

        private UndoStack<UndoState> m_undo = new UndoStack<UndoState>(5);

        public LLObject.ObjectFlags Flags = LLObject.ObjectFlags.None;

        public uint ObjectFlags
        {
            get { return (uint)Flags; }
            set { Flags = (LLObject.ObjectFlags)value; }
        }

        #endregion

        protected byte[] m_particleSystem = new byte[0];

        [XmlIgnore] public uint TimeStampFull = 0;
        [XmlIgnore] public uint TimeStampTerse = 0;
        [XmlIgnore] public uint TimeStampLastActivity = 0; // Will be used for AutoReturn

        /// <summary>
        /// Only used internally to schedule client updates.
        /// 0 - no update is scheduled
        /// 1 - terse update scheduled
        /// 2 - full update scheduled
        ///
        /// TODO - This should be an enumeration
        /// </summary>
        private byte m_updateFlag;

        #region Properties

        public LLUUID CreatorID;

        public LLUUID ObjectCreator
        {
            get { return CreatorID; }
        }

        protected LLUUID m_uuid;

        public LLUUID UUID
        {
            get { return m_uuid; }
            set { m_uuid = value; }
        }

        protected uint m_localId;

        public uint LocalId
        {
            get { return m_localId; }
            set { m_localId = value; }
        }

        protected string m_name;

        public virtual string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }

        public scriptEvents ScriptEvents
        {
            get { return m_aggregateScriptEvents; }
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

        public uint GetEffectiveObjectFlags()
        {
            LLObject.ObjectFlags f = Flags;
            if (m_parentGroup == null || m_parentGroup.RootPart == this)
                f &= ~(LLObject.ObjectFlags.Touch | LLObject.ObjectFlags.Money);

            return (uint)Flags | (uint)LocalFlags;
        }

        //unkown if this will be kept, added as a way of removing the group position from the group class
        protected LLVector3 m_groupPosition;

        /// <summary>
        /// Method for a prim to get it's world position from the group.
        /// Remember, the Group Position simply gives the position of the group itself
        /// </summary>
        /// <returns>A Linked Child Prim objects position in world</returns>
        public LLVector3 GetWorldPosition()
        {

            Quaternion parentRot = new Quaternion(
            ParentGroup.RootPart.RotationOffset.W,
            ParentGroup.RootPart.RotationOffset.X,
            ParentGroup.RootPart.RotationOffset.Y,
            ParentGroup.RootPart.RotationOffset.Z);

            Vector3 axPos
                = new Vector3(
                    OffsetPosition.X,
                    OffsetPosition.Y,
                    OffsetPosition.Z);

            axPos = parentRot * axPos;
            LLVector3 translationOffsetPosition = new LLVector3(axPos.x, axPos.y, axPos.z);
            return GroupPosition + translationOffsetPosition;

            //return (new LLVector3(axiomPos.x, axiomPos.y, axiomPos.z) + AbsolutePosition);
        }

        /// <summary>
        /// Gets the rotation of this prim offset by the group rotation
        /// </summary>
        /// <returns></returns>
        public LLQuaternion GetWorldRotation()
        {

            Quaternion newRot;

            if (this.LinkNum == 0)
            {
                newRot = new Quaternion(RotationOffset.W,RotationOffset.X,RotationOffset.Y,RotationOffset.Z);

            }
            else
            {
                Quaternion parentRot = new Quaternion(
                ParentGroup.RootPart.RotationOffset.W,
                ParentGroup.RootPart.RotationOffset.X,
                ParentGroup.RootPart.RotationOffset.Y,
                ParentGroup.RootPart.RotationOffset.Z);

                Quaternion oldRot
                    = new Quaternion(
                        RotationOffset.W,
                        RotationOffset.X,
                        RotationOffset.Y,
                        RotationOffset.Z);

                newRot = parentRot * oldRot;
            }
            return new LLQuaternion(newRot.x, newRot.y, newRot.z, newRot.w);

            //return new LLQuaternion(axiomPartRotation.x, axiomPartRotation.y, axiomPartRotation.z, axiomPartRotation.w);

        }

        public void StoreUndoState()
        {
            if (!m_undoing)
            {
                if (m_parentGroup != null)
                {
                    if (m_undo.Count > 0)
                    {
                        UndoState last = m_undo.Peek();
                        if (last != null)
                        {
                            if (last.Compare(this))
                                return;
                        }
                    }


                    if (m_parentGroup.GetSceneMaxUndo() > 0)
                    {
                        UndoState nUndo = new UndoState(this);

                        m_undo.Push(nUndo);

                    }
                }
            }
        }

        public void ClearUndoState()
        {
            m_undo.Clear();
            StoreUndoState();
        }

        public LLVector3 GroupPosition
        {
            get
            {
                // If this is a linkset, we don't want the physics engine mucking up our group position here.
                if (PhysActor != null && ParentID == 0)
                {
                    m_groupPosition.X = PhysActor.Position.X;
                    m_groupPosition.Y = PhysActor.Position.Y;
                    m_groupPosition.Z = PhysActor.Position.Z;
                }
                if (m_IsAttachment)
                {
                    ScenePresence sp = m_parentGroup.Scene.GetScenePresence(m_attachedAvatar);
                    if (sp != null)
                    {
                        return sp.AbsolutePosition;
                    }
                }

                return m_groupPosition;
            }
            set
            {
                StoreUndoState();

                m_groupPosition = value;

                if (PhysActor != null)
                {
                    try
                    {

                        // Root prim actually goes at Position
                        if (ParentID == 0)
                        {
                            PhysActor.Position = new PhysicsVector(value.X, value.Y, value.Z);

                        }
                        else
                        {

                            // To move the child prim in respect to the group position and rotation we have to calculate

                            LLVector3 resultingposition = GetWorldPosition();
                            PhysActor.Position = new PhysicsVector(resultingposition.X, resultingposition.Y, resultingposition.Z);
                            LLQuaternion resultingrot = GetWorldRotation();
                            PhysActor.Orientation = new Quaternion(resultingrot.W, resultingrot.X, resultingrot.Y, resultingrot.Z);
                        }

                        // Tell the physics engines that this prim changed.
                        m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }

            }
        }

        private byte[] m_TextureAnimation;

        protected LLVector3 m_offsetPosition;

        public LLVector3 OffsetPosition
        {
            get { return m_offsetPosition; }
            set
            {
                StoreUndoState();
                m_offsetPosition = value;
            try
            {
                // Hack to get the child prim to update world positions in the physics engine
                ParentGroup.ResetChildPrimPhysicsPositions();

            }
            catch (NullReferenceException)
            {
                // Ignore, and skip over.
            }
            //m_log.Info("[PART]: OFFSET:" + m_offsetPosition.ToString());
            }
        }

        public LLVector3 AbsolutePosition
        {
            get {
                if (m_IsAttachment)
                    return GroupPosition;

                return m_offsetPosition + m_groupPosition; }
        }

        protected LLQuaternion m_rotationOffset;

        public LLQuaternion RotationOffset
        {
            get
            {
                // We don't want the physics engine mucking up the rotations in a linkset
                if (PhysActor != null && ParentID == 0)
                {
                    if (PhysActor.Orientation.x != 0 || PhysActor.Orientation.y != 0
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
                StoreUndoState();
                m_rotationOffset = value;

                if (PhysActor != null)
                {
                    try
                    {
                        // Root prim gets value directly
                        if (ParentID == 0)
                        {
                            PhysActor.Orientation = new Quaternion(value.W, value.X, value.Y, value.Z);
                            //m_log.Info("[PART]: RO1:" + PhysActor.Orientation.ToString());
                        }
                        else
                        {
                            // Child prim we have to calculate it's world rotationwel
                            LLQuaternion resultingrotation = GetWorldRotation();
                            PhysActor.Orientation = new Quaternion(resultingrotation.W, resultingrotation.X, resultingrotation.Y, resultingrotation.Z);
                            //m_log.Info("[PART]: RO2:" + PhysActor.Orientation.ToString());
                        }
                        m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
                        //}
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

            }
        }

        protected LLVector3 m_velocity;
        protected LLVector3 m_rotationalvelocity;

        /// <summary></summary>
        public LLVector3 Velocity
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
                        m_velocity.X = PhysActor.Velocity.X;
                        m_velocity.Y = PhysActor.Velocity.Y;
                        m_velocity.Z = PhysActor.Velocity.Z;
                    }
                }

                return m_velocity;
            }
            set {

                m_velocity = value;
                if (PhysActor != null)
                {
                    if (PhysActor.IsPhysical)
                    {
                        PhysActor.Velocity = new PhysicsVector(value.X, value.Y, value.Z);
                        m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
                    }
                }

            }
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
                        m_rotationalvelocity.FromBytes(PhysActor.RotationalVelocity.GetBytes(),0);
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

        private string m_description = String.Empty;

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
                TriggerScriptChangedEvent(Changed.COLOR);

                /* ScheduleFullUpdate() need not be called b/c after
                 * setting the color, the text will be set, so then
                 * ScheduleFullUpdate() will be called. */
                //ScheduleFullUpdate();
            }
        }

        private string m_text = String.Empty;

        public Vector3 SitTargetPosition
        {
            get { return m_sitTargetPosition; }
        }

        public Quaternion SitTargetOrientation
        {
            get { return m_sitTargetOrientation; }
        }

        public string Text
        {
            get
            {
                string returnstr = m_text;
                if (returnstr.Length > 255)
                {
                    returnstr = returnstr.Substring(0, 254);
                }
                return returnstr;
            }
            set
            {
                m_text = value;
            }
        }

        //Xantor 20080528 Sound stuff:
        //  Note: This isn't persisted in the database right now, as the fields for that aren't just there yet.
        //        Not a big problem as long as the script that sets it remains in the prim on startup.
        //        for SL compatibility it should be persisted though (set sound / displaytext / particlesystem, kill script)
        [XmlIgnore]
        public LLUUID Sound;
        [XmlIgnore]
        public byte SoundFlags;
        [XmlIgnore]
        public double SoundGain;
        [XmlIgnore]
        public double SoundRadius;


        private string m_sitName = String.Empty;

        public string SitName
        {
            get { return m_sitName; }
            set { m_sitName = value; }
        }

        private string m_touchName = String.Empty;

        public string TouchName
        {
            get { return m_touchName; }
            set { m_touchName = value; }
        }

        private int m_linkNum = 0;

        public int LinkNum
        {
            get { return m_linkNum; }
            set
            {
                m_linkNum = value;
                TriggerScriptChangedEvent(Changed.LINK);

            }
        }

        private byte m_clickAction = 0;

        public byte ClickAction
        {
            get { return m_clickAction; }
            set
            {
                m_clickAction = value;
            }
        }

        protected PrimitiveBaseShape m_shape;

        /// <summary>
        /// hook to the physics scene to apply impulse
        /// This is sent up to the group, which then finds the root prim
        /// and applies the force on the root prim of the group
        /// </summary>
        /// <param name="impulsei">Vector force</param>
        /// <param name="localGlobalTF">true for the local frame, false for the global frame</param>
        public void ApplyImpulse(LLVector3 impulsei, bool localGlobalTF)
        {
            PhysicsVector impulse = new PhysicsVector(impulsei.X, impulsei.Y, impulsei.Z);

            if (localGlobalTF)
            {

                LLQuaternion grot = GetWorldRotation();
                Quaternion AXgrot = new Quaternion(grot.W,grot.X,grot.Y,grot.Z);
                Vector3 AXimpulsei = new Vector3(impulsei.X, impulsei.Y, impulsei.Z);
                Vector3 newimpulse = AXgrot * AXimpulsei;
                impulse = new PhysicsVector(newimpulse.x, newimpulse.y, newimpulse.z);

            }
            else
            {

                if (m_parentGroup != null)
                {
                    m_parentGroup.applyImpulse(impulse);
                }
            }
        }

        public void MoveToTarget(LLVector3 target, float tau)
        {
            if (tau > 0)
            {
                m_parentGroup.moveToTarget(target, tau);
            }
            else
            {
                StopMoveToTarget();
            }

        }

        public void StopMoveToTarget()
        {
            m_parentGroup.stopMoveToTarget();
        }

        public void TriggerScriptChangedEvent(Changed val)
        {
            if (m_parentGroup != null)
            {
                if (m_parentGroup.Scene != null)
                    m_parentGroup.Scene.TriggerObjectChanged(LocalId, (uint)val);
            }

        }

        public PrimitiveBaseShape Shape
        {
            get { return m_shape; }
            set
            {
                m_shape = value;
                TriggerScriptChangedEvent(Changed.SHAPE);
            }
        }

        public LLVector3 Scale
        {
            get { return m_shape.Scale; }
            set
            {
                StoreUndoState();
                m_shape.Scale = value;
                TriggerScriptChangedEvent(Changed.SCALE);
            }
        }

        public bool Stopped
        {
            get {
                double threshold = 0.02;
                return (Math.Abs(Velocity.X) < threshold &&
                        Math.Abs(Velocity.Y) < threshold &&
                        Math.Abs(Velocity.Z) < threshold &&
                        Math.Abs(AngularVelocity.X) < threshold &&
                        Math.Abs(AngularVelocity.Y) < threshold &&
                        Math.Abs(AngularVelocity.Z) < threshold);
            }
        }

        #endregion

        public LLUUID ObjectOwner
        {
            get { return OwnerID; }
        }

        // FIXME, TODO, ERROR: 'ParentGroup' can't be in here, move it out.
        protected SceneObjectGroup m_parentGroup;

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
        /// No arg constructor called by region restore db code
        /// </summary>
        public SceneObjectPart()
        {
            // It's not necessary to persist this
            m_TextureAnimation = new byte[0];
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
            LocalId = (uint) (localID);
            Shape = shape;
            // Todo: Add More Object Parameter from above!
            OwnershipCost = 0;
            ObjectSaleType = (byte) 0;
            SalePrice = 0;
            Category = (uint) 0;
            LastOwnerID = CreatorID;
            // End Todo: ///
            GroupPosition = groupPosition;
            OffsetPosition = offsetPosition;
            RotationOffset = rotationOffset;
            Velocity = new LLVector3(0, 0, 0);
            m_rotationalvelocity = new LLVector3(0, 0, 0);
            AngularVelocity = new LLVector3(0, 0, 0);
            Acceleration = new LLVector3(0, 0, 0);
            m_TextureAnimation = new byte[0];

            // Prims currently only contain a single folder (Contents).  From looking at the Second Life protocol,
            // this appears to have the same UUID (!) as the prim.  If this isn't the case, one can't drag items from
            // the prim into an agent inventory (Linden client reports that the "Object not found for drop" in its log

            Flags = 0;
            Flags |= LLObject.ObjectFlags.CreateSelected;

            TrimPermissions();
            //m_undo = new UndoStack<UndoState>(ParentGroup.GetSceneMaxUndo());

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
            TimeStampTerse = (uint) Util.UnixTimeSinceEpoch();
            CreationDate = creationDate;
            OwnerID = ownerID;
            CreatorID = creatorID;
            LastOwnerID = lastOwnerID;
            UUID = LLUUID.Random();
            LocalId = (uint) (localID);
            Shape = shape;
            OwnershipCost = 0;
            ObjectSaleType = (byte) 0;
            SalePrice = 0;
            Category = (uint) 0;
            LastOwnerID = CreatorID;
            OffsetPosition = position;
            RotationOffset = rotation;
            ObjectFlags = flags;

            // Since we don't store script state, this is only a 'temporary' objectflag now
            // If the object is scripted, the script will get loaded and this will be set again
            ObjectFlags &= ~(uint)(LLObject.ObjectFlags.Scripted | LLObject.ObjectFlags.Touch);

            TrimPermissions();
            // ApplyPhysics();

            ScheduleFullUpdate();
        }

        #endregion

        /// <summary>
        /// Restore this part from the serialized xml representation.
        /// </summary>
        /// <param name="xmlreader"></param>
        /// <returns></returns>
        public static SceneObjectPart FromXml(XmlReader xmlReader)
        {
            // It's not necessary to persist this

            XmlSerializer serializer = new XmlSerializer(typeof (SceneObjectPart));
            SceneObjectPart newobject = (SceneObjectPart) serializer.Deserialize(xmlReader);
            return newobject;
        }

        public void ApplyPhysics(uint rootObjectFlags, bool m_physicalPrim)
        {

            bool isPhysical = (((rootObjectFlags & (uint) LLObject.ObjectFlags.Physics) != 0) && m_physicalPrim);
            bool isPhantom = ((rootObjectFlags & (uint) LLObject.ObjectFlags.Phantom) != 0);

            // Added clarification..   since A rigid body is an object that you can kick around, etc.
            bool RigidBody = isPhysical && !isPhantom;

            // The only time the physics scene shouldn't know about the prim is if it's phantom
            if (!isPhantom)
            {
                PhysActor = m_parentGroup.Scene.PhysicsScene.AddPrimShape(
                    Name,
                    Shape,
                    new PhysicsVector(AbsolutePosition.X, AbsolutePosition.Y,
                                      AbsolutePosition.Z),
                    new PhysicsVector(Scale.X, Scale.Y, Scale.Z),
                    new Quaternion(RotationOffset.W, RotationOffset.X,
                                   RotationOffset.Y, RotationOffset.Z), RigidBody);

                // Basic Physics returns null..  joy joy joy.
                if (PhysActor != null)
                {
                    PhysActor.LocalID = LocalId;
                    DoPhysicsPropertyUpdate(RigidBody, true);
                }
            }
        }

        public void TrimPermissions()
        {

            BaseMask &= (uint)PermissionMask.All;
            OwnerMask &= (uint)PermissionMask.All;
            GroupMask &= (uint)PermissionMask.All;
            EveryoneMask &= (uint)PermissionMask.All;
            NextOwnerMask &= (uint)PermissionMask.All;

        }

        /// <summary>
        /// Serialize this part to xml.
        /// </summary>
        /// <param name="xmlWriter"></param>
        public void ToXml(XmlWriter xmlWriter)
        {
            XmlSerializer serializer = new XmlSerializer(typeof (SceneObjectPart));
            serializer.Serialize(xmlWriter, this);
        }

        public EntityIntersection TestIntersection(Ray iray, Quaternion parentrot)
        {
            // In this case we're using a sphere with a radius of the largest dimention of the prim
            // TODO: Change to take shape into account


            EntityIntersection returnresult = new EntityIntersection();
            Vector3 vAbsolutePosition = new Vector3(AbsolutePosition.X, AbsolutePosition.Y, AbsolutePosition.Z);

            Vector3 vScale = new Vector3(Scale.X, Scale.Y, Scale.Z);
            Quaternion qRotation =
                new Quaternion(RotationOffset.W, RotationOffset.X, RotationOffset.Y, RotationOffset.Z);


            //Quaternion worldRotation = (qRotation*parentrot);
            //Matrix3 worldRotM = worldRotation.ToRotationMatrix();


            Vector3 rOrigin = iray.Origin;
            Vector3 rDirection = iray.Direction;



            //rDirection = rDirection.Normalize();
            // Buidling the first part of the Quadratic equation
            Vector3 r2ndDirection = rDirection*rDirection;
            float itestPart1 = r2ndDirection.x + r2ndDirection.y + r2ndDirection.z;

            // Buidling the second part of the Quadratic equation
            Vector3 tmVal2 = rOrigin - vAbsolutePosition;
            Vector3 r2Direction = rDirection*2.0f;
            Vector3 tmVal3 = r2Direction*tmVal2;

            float itestPart2 = tmVal3.x + tmVal3.y + tmVal3.z;

            // Buidling the third part of the Quadratic equation
            Vector3 tmVal4 = rOrigin*rOrigin;
            Vector3 tmVal5 = vAbsolutePosition*vAbsolutePosition;

            Vector3 tmVal6 = vAbsolutePosition*rOrigin;


            // Set Radius to the largest dimention of the prim
            float radius = 0f;
            if (vScale.x > radius)
                radius = vScale.x;
            if (vScale.y > radius)
                radius = vScale.y;
            if (vScale.z > radius)
                radius = vScale.z;

            // the second part of this is the default prim size
            // once we factor in the aabb of the prim we're adding we can
            // change this to;
            // radius = (radius / 2) - 0.01f;
            //
            radius = (radius / 2) + (0.5f / 2) - 0.1f;

            //radius = radius;

            float itestPart3 = tmVal4.x + tmVal4.y + tmVal4.z + tmVal5.x + tmVal5.y + tmVal5.z -
                               (2.0f*(tmVal6.x + tmVal6.y + tmVal6.z + (radius*radius)));

            // Yuk Quadradrics..    Solve first
            float rootsqr = (itestPart2*itestPart2) - (4.0f*itestPart1*itestPart3);
            if (rootsqr < 0.0f)
            {
                // No intersection
                return returnresult;
            }
            float root = ((-itestPart2) - (float) Math.Sqrt((double) rootsqr))/(itestPart1*2.0f);

            if (root < 0.0f)
            {
                // perform second quadratic root solution
                root = ((-itestPart2) + (float) Math.Sqrt((double) rootsqr))/(itestPart1*2.0f);

                // is there any intersection?
                if (root < 0.0f)
                {
                    // nope, no intersection
                    return returnresult;
                }
            }

            // We got an intersection.  putting together an EntityIntersection object with the
            // intersection information
            Vector3 ipoint =
                new Vector3(iray.Origin.x + (iray.Direction.x*root), iray.Origin.y + (iray.Direction.y*root),
                            iray.Origin.z + (iray.Direction.z*root));

            returnresult.HitTF = true;
            returnresult.ipoint = ipoint;

            // Normal is calculated by the difference and then normalizing the result
            Vector3 normalpart = ipoint - vAbsolutePosition;
            returnresult.normal = normalpart / normalpart.Length;

            // It's funny how the LLVector3 object has a Distance function, but the Axiom.Math object doesn't.
            // I can write a function to do it..    but I like the fact that this one is Static.

            LLVector3 distanceConvert1 = new LLVector3(iray.Origin.x, iray.Origin.y, iray.Origin.z);
            LLVector3 distanceConvert2 = new LLVector3(ipoint.x, ipoint.y, ipoint.z);
            float distance = (float) Util.GetDistanceTo(distanceConvert1, distanceConvert2);

            returnresult.distance = distance;

            return returnresult;
        }

        public double GetDistanceTo(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            float dz = a.z - b.z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public EntityIntersection TestIntersectionOBB(Ray iray, Quaternion parentrot, bool frontFacesOnly, bool faceCenters)
        {
            // In this case we're using a rectangular prism, which has 6 faces and therefore 6 planes
            // This breaks down into the ray---> plane equation.
            // TODO: Change to take shape into account
            Vector3[] vertexes = new Vector3[8];

            float[] distance = new float[6];
            Vector3[] FaceA = new Vector3[6]; // vertex A for Facei
            Vector3[] FaceB = new Vector3[6]; // vertex B for Facei
            Vector3[] FaceC = new Vector3[6]; // vertex C for Facei
            Vector3[] FaceD = new Vector3[6]; // vertex D for Facei

            Vector3[] normals = new Vector3[6]; // Normal for Facei
            Vector3[] AAfacenormals = new Vector3[6]; // Axis Aligned face normals

            AAfacenormals[0] = new Vector3(1, 0, 0);
            AAfacenormals[1] = new Vector3(0, 1, 0);
            AAfacenormals[2] = new Vector3(-1, 0, 0);
            AAfacenormals[3] = new Vector3(0, -1, 0);
            AAfacenormals[4] = new Vector3(0, 0, 1);
            AAfacenormals[5] = new Vector3(0, 0, -1);

            Vector3 AmBa = new Vector3(0, 0, 0); // Vertex A - Vertex B
            Vector3 AmBb = new Vector3(0, 0, 0); // Vertex B - Vertex C
            Vector3 cross = new Vector3();

            LLVector3 pos = GetWorldPosition();
            LLQuaternion rot = GetWorldRotation();

            // Variables prefixed with AX are Axiom.Math copies of the LL variety.

            Quaternion AXrot = new Quaternion(rot.W,rot.X,rot.Y,rot.Z);
            AXrot.Normalize();

            Vector3 AXpos = new Vector3(pos.X, pos.Y, pos.Z);

            // tScale is the offset to derive the vertex based on the scale.
            // it's different for each vertex because we've got to rotate it
            // to get the world position of the vertex to produce the Oriented Bounding Box

            Vector3 tScale = new Vector3();

            Vector3 AXscale = new Vector3(m_shape.Scale.X * 0.5f, m_shape.Scale.Y * 0.5f, m_shape.Scale.Z * 0.5f);

            //Vector3 pScale = (AXscale) - (AXrot.Inverse() * (AXscale));
            //Vector3 nScale = (AXscale * -1) - (AXrot.Inverse() * (AXscale * -1));

            // rScale is the rotated offset to find a vertex based on the scale and the world rotation.
            Vector3 rScale = new Vector3();

            // Get Vertexes for Faces Stick them into ABCD for each Face
            // Form: Face<vertex>[face] that corresponds to the below diagram
            #region ABCD Face Vertex Map Comment Diagram
            //                   A _________ B
            //                    |         |
            //                    |  4 top  |
            //                    |_________|
            //                   C           D

            //                   A _________ B
            //                    |  Back   |
            //                    |    3    |
            //                    |_________|
            //                   C           D

            //   A _________ B                     B _________ A
            //    |  Left   |                       |  Right  |
            //    |    0    |                       |    2    |
            //    |_________|                       |_________|
            //   C           D                     D           C

            //                   A _________ B
            //                    |  Front  |
            //                    |    1    |
            //                    |_________|
            //                   C           D

            //                   C _________ D
            //                    |         |
            //                    |  5 bot  |
            //                    |_________|
            //                   A           B
            #endregion

            #region Plane Decomposition of Oriented Bounding Box
            tScale = new Vector3(AXscale.x, -AXscale.y, AXscale.z);
            rScale = ((AXrot * tScale));
            vertexes[0] = (new Vector3((pos.X + rScale.x), (pos.Y + rScale.y), (pos.Z + rScale.z)));
           // vertexes[0].x = pos.X + vertexes[0].x;
            //vertexes[0].y = pos.Y + vertexes[0].y;
            //vertexes[0].z = pos.Z + vertexes[0].z;

            FaceA[0] = vertexes[0];
            FaceB[3] = vertexes[0];
            FaceA[4] = vertexes[0];

            tScale = AXscale;
            rScale = ((AXrot * tScale));
            vertexes[1] = (new Vector3((pos.X + rScale.x), (pos.Y + rScale.y), (pos.Z + rScale.z)));

           // vertexes[1].x = pos.X + vertexes[1].x;
           // vertexes[1].y = pos.Y + vertexes[1].y;
            //vertexes[1].z = pos.Z + vertexes[1].z;

            FaceB[0] = vertexes[1];
            FaceA[1] = vertexes[1];
            FaceC[4] = vertexes[1];

            tScale = new Vector3(AXscale.x, -AXscale.y, -AXscale.z);
            rScale = ((AXrot * tScale));

            vertexes[2] = (new Vector3((pos.X + rScale.x), (pos.Y + rScale.y), (pos.Z + rScale.z)));

            //vertexes[2].x = pos.X + vertexes[2].x;
            //vertexes[2].y = pos.Y + vertexes[2].y;
            //vertexes[2].z = pos.Z + vertexes[2].z;

            FaceC[0] = vertexes[2];
            FaceD[3] = vertexes[2];
            FaceC[5] = vertexes[2];

            tScale = new Vector3(AXscale.x, AXscale.y, -AXscale.z);
            rScale = ((AXrot * tScale));
            vertexes[3] = (new Vector3((pos.X + rScale.x), (pos.Y + rScale.y), (pos.Z + rScale.z)));

            //vertexes[3].x = pos.X + vertexes[3].x;
           // vertexes[3].y = pos.Y + vertexes[3].y;
           // vertexes[3].z = pos.Z + vertexes[3].z;

            FaceD[0] = vertexes[3];
            FaceC[1] = vertexes[3];
            FaceA[5] = vertexes[3];

            tScale = new Vector3(-AXscale.x, AXscale.y, AXscale.z);
            rScale = ((AXrot * tScale));
            vertexes[4] = (new Vector3((pos.X + rScale.x), (pos.Y + rScale.y), (pos.Z + rScale.z)));

           // vertexes[4].x = pos.X + vertexes[4].x;
           // vertexes[4].y = pos.Y + vertexes[4].y;
           // vertexes[4].z = pos.Z + vertexes[4].z;

            FaceB[1] = vertexes[4];
            FaceA[2] = vertexes[4];
            FaceD[4] = vertexes[4];

            tScale = new Vector3(-AXscale.x, AXscale.y, -AXscale.z);
            rScale = ((AXrot * tScale));
            vertexes[5] = (new Vector3((pos.X + rScale.x), (pos.Y + rScale.y), (pos.Z + rScale.z)));

           // vertexes[5].x = pos.X + vertexes[5].x;
           // vertexes[5].y = pos.Y + vertexes[5].y;
           // vertexes[5].z = pos.Z + vertexes[5].z;

            FaceD[1] = vertexes[5];
            FaceC[2] = vertexes[5];
            FaceB[5] = vertexes[5];

            tScale = new Vector3(-AXscale.x, -AXscale.y, AXscale.z);
            rScale = ((AXrot * tScale));
            vertexes[6] = (new Vector3((pos.X + rScale.x), (pos.Y + rScale.y), (pos.Z + rScale.z)));

           // vertexes[6].x = pos.X + vertexes[6].x;
           // vertexes[6].y = pos.Y + vertexes[6].y;
           // vertexes[6].z = pos.Z + vertexes[6].z;

            FaceB[2] = vertexes[6];
            FaceA[3] = vertexes[6];
            FaceB[4] = vertexes[6];

            tScale = new Vector3(-AXscale.x, -AXscale.y, -AXscale.z);
            rScale = ((AXrot * tScale));
            vertexes[7] = (new Vector3((pos.X + rScale.x), (pos.Y + rScale.y), (pos.Z + rScale.z)));

           // vertexes[7].x = pos.X + vertexes[7].x;
           // vertexes[7].y = pos.Y + vertexes[7].y;
           // vertexes[7].z = pos.Z + vertexes[7].z;

            FaceD[2] = vertexes[7];
            FaceC[3] = vertexes[7];
            FaceD[5] = vertexes[7];
            #endregion

            // Get our plane normals
            for (int i = 0; i < 6; i++)
            {
                //m_log.Info("[FACECALCULATION]: FaceA[" + i + "]=" + FaceA[i] + " FaceB[" + i + "]=" + FaceB[i] + " FaceC[" + i + "]=" + FaceC[i] + " FaceD[" + i + "]=" + FaceD[i]);

                // Our Plane direction
                AmBa = FaceA[i] - FaceB[i];
                AmBb = FaceB[i] - FaceC[i];

                cross = AmBb.Cross(AmBa);

                // normalize the cross product to get the normal.
                normals[i] = cross / cross.Length;

                //m_log.Info("[NORMALS]: normals[ " + i + "]" + normals[i].ToString());
                //distance[i] = (normals[i].x * AmBa.x + normals[i].y * AmBa.y + normals[i].z * AmBa.z) * -1;
            }

            EntityIntersection returnresult = new EntityIntersection();

            returnresult.distance = 1024;
            float c = 0;
            float a = 0;
            float d = 0;
            Vector3 q = new Vector3();

            #region OBB Version 2 Experiment
            //float fmin = 999999;
            //float fmax = -999999;
            //float s = 0;

            //for (int i=0;i<6;i++)
            //{
                //s = iray.Direction.Dot(normals[i]);
                //d = normals[i].Dot(FaceB[i]);

                //if (s == 0)
                //{
                    //if (iray.Origin.Dot(normals[i]) > d)
                    //{
                        //return returnresult;
                    //}
                   // else
                    //{
                        //continue;
                    //}
                //}
                //a = (d - iray.Origin.Dot(normals[i])) / s;
                //if (iray.Direction.Dot(normals[i]) < 0)
                //{
                    //if (a > fmax)
                    //{
                        //if (a > fmin)
                        //{
                            //return returnresult;
                        //}
                        //fmax = a;
                    //}

                //}
                //else
                //{
                    //if (a < fmin)
                    //{
                        //if (a < 0 || a < fmax)
                        //{
                            //return returnresult;
                        //}
                        //fmin = a;
                    //}
                //}
            //}
            //if (fmax > 0)
            //    a= fmax;
            //else
           //     a=fmin;

            //q = iray.Origin + a * iray.Direction;
            #endregion

            // Loop over faces (6 of them)
            for (int i = 0; i < 6; i++)
            {
                AmBa = FaceA[i] - FaceB[i];
                AmBb = FaceB[i] - FaceC[i];
                d = normals[i].Dot(FaceB[i]);

                if (faceCenters)
                {
                    c = normals[i].Dot(normals[i]);
                }
                else
                {
                    c = iray.Direction.Dot(normals[i]);
                }
                if (c == 0)
                    continue;

                a = (d - iray.Origin.Dot(normals[i])) / c;

                if (a < 0)
                    continue;

                // If the normal is pointing outside the object



                if (iray.Direction.Dot(normals[i]) < 0 || !frontFacesOnly)
                {

                    if (faceCenters)
                    {   //(FaceA[i] + FaceB[i] + FaceC[1] + FaceD[i]) / 4f;
                        q =  iray.Origin + a * normals[i];
                    }
                    else
                    {
                        q = iray.Origin + a * iray.Direction;
                    }

                    float distance2 = (float)GetDistanceTo(q, AXpos);
                    // Is this the closest hit to the object's origin?
                    if (faceCenters)
                    {
                        distance2 = (float)GetDistanceTo(q, iray.Origin);
                    }


                    if (distance2 < returnresult.distance)
                    {
                        returnresult.distance = distance2;
                        returnresult.HitTF = true;
                        returnresult.ipoint = q;
                        m_log.Info("[FACE]:" + i.ToString());
                        m_log.Info("[POINT]: " + q.ToString());
                        m_log.Info("[DIST]: " + distance2.ToString());
                        returnresult.normal = normals[i];
                        returnresult.AAfaceNormal = AAfacenormals[i];

                    }
                }

            }
            return returnresult;
        }

        // Use this for attachments!  LocalID should be avatar's localid
        public void SetParentLocalId(uint localID)
        {
            ParentID = localID;
        }

        public void SetAttachmentPoint(uint AttachmentPoint)
        {
            m_attachmentPoint = AttachmentPoint;

            // save the attachment point.
            //if (AttachmentPoint != 0)
            //{
                m_shape.State = (byte)AttachmentPoint;
            //}

        }
        /// <summary>
        ///
        /// </summary>
        public void SetParent(SceneObjectGroup parent)
        {
            m_parentGroup = parent;
        }

        public void SetSitTarget(Vector3 offset, Quaternion orientation)
        {
            m_sitTargetPosition = offset;
            m_sitTargetOrientation = orientation;
        }

        public void SetBuoyancy(float fvalue)
        {
            if (PhysActor != null)
            {
                PhysActor.Buoyancy = fvalue;
            }
        }

        public void SetAxisRotation(int axis, int rotate)
        {
            if (m_parentGroup != null)
            {
                m_parentGroup.SetAxisRotation(axis, rotate);

            }
        }

        public void SetPhysicsAxisRotation()
        {
            PhysActor.LockAngularMotion(m_rotationAxis);
            m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
        }

        public void SetFloatOnWater(int floatYN)
        {
            if (PhysActor != null)
            {
                if (floatYN == 1)
                {
                    PhysActor.FloatOnWater = true;
                }
                else
                {
                    PhysActor.FloatOnWater = false;
                }

            }
        }


        public LLVector3 GetSitTargetPositionLL()
        {
            return new LLVector3(m_sitTargetPosition.x, m_sitTargetPosition.y, m_sitTargetPosition.z);
        }

        public LLQuaternion GetSitTargetOrientationLL()
        {
            return
                new LLQuaternion(m_sitTargetOrientation.x, m_sitTargetOrientation.y, m_sitTargetOrientation.z,
                                 m_sitTargetOrientation.w);
        }

        // Utility function so the databases don't have to reference axiom.math
        public void SetSitTargetLL(LLVector3 offset, LLQuaternion orientation)
        {
            if (
                !(offset.X == 0 && offset.Y == 0 && offset.Z == 0 && (orientation.W == 0 || orientation.W == 1) &&
                  orientation.X == 0 && orientation.Y == 0 && orientation.Z == 0))
            {
                m_sitTargetPosition = new Vector3(offset.X, offset.Y, offset.Z);
                m_sitTargetOrientation = new Quaternion(orientation.W, orientation.X, orientation.Y, orientation.Z);
            }
        }

        public Vector3 GetSitTargetPosition()
        {
            return m_sitTargetPosition;
        }

        public Quaternion GetSitTargetOrientation()
        {
            return m_sitTargetOrientation;
        }

        public void SetAvatarOnSitTarget(LLUUID avatarID)
        {
            m_sitTargetAvatar = avatarID;
            TriggerScriptChangedEvent(Changed.LINK);
        }

        public LLUUID GetAvatarOnSitTarget()
        {
            return m_sitTargetAvatar;
        }


        public LLUUID GetRootPartUUID()
        {
            if (m_parentGroup != null)
            {
                return m_parentGroup.UUID;
            }
            return LLUUID.Zero;
        }

        public static SceneObjectPart Create()
        {
            SceneObjectPart part = new SceneObjectPart();
            part.UUID = LLUUID.Random();

            PrimitiveBaseShape shape = PrimitiveBaseShape.Create();
            part.Shape = shape;

            part.Name = "Primitive";
            part.OwnerID = LLUUID.Random();

            return part;
        }

        #region Copying

        /// <summary>
        /// Duplicates this part.
        /// </summary>
        /// <returns></returns>
        public SceneObjectPart Copy(uint localID, LLUUID AgentID, LLUUID GroupID, int linkNum, bool userExposed)
        {
            SceneObjectPart dupe = (SceneObjectPart) MemberwiseClone();
            dupe.m_shape = m_shape.Copy();
            dupe.m_regionHandle = m_regionHandle;
            if (userExposed)
                dupe.UUID = LLUUID.Random();
            
            dupe.LocalId = localID;
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

            dupe.TaskInventory = (TaskInventoryDictionary)dupe.TaskInventory.Clone();

            if (userExposed)
                dupe.ResetIDs(linkNum);

            // This may be wrong...    it might have to be applied in SceneObjectGroup to the object that's being duplicated.
            dupe.LastOwnerID = ObjectOwner;

            byte[] extraP = new byte[Shape.ExtraParams.Length];
            Array.Copy(Shape.ExtraParams, extraP, extraP.Length);
            dupe.Shape.ExtraParams = extraP;

            if (userExposed)
            {
                if (dupe.m_shape.SculptEntry && dupe.m_shape.SculptTexture != LLUUID.Zero)
                {
                    m_parentGroup.Scene.AssetCache.GetAsset(dupe.m_shape.SculptTexture, dupe.SculptTextureCallback, true);
                }
                bool UsePhysics = ((dupe.ObjectFlags & (uint)LLObject.ObjectFlags.Physics) != 0);
                dupe.DoPhysicsPropertyUpdate(UsePhysics, true);
            }
            return dupe;
        }

        #endregion

        /// <summary>
        /// Reset LLUUIDs for this part.  This involves generate this part's own LLUUID and
        /// generating new LLUUIDs for all the items in the inventory.
        /// </summary>
        /// <param name="linkNum">Link number for the part</param>
        public void ResetIDs(int linkNum)
        {
            UUID = LLUUID.Random();
            LinkNum = linkNum;

            ResetInventoryIDs();
        }

        #region Update Scheduling

        /// <summary>
        /// Clear all pending updates
        /// </summary>
        private void ClearUpdateSchedule()
        {
            m_updateFlag = 0;
        }

        /// <summary>
        /// Schedules this prim for a full update
        /// </summary>
        public void ScheduleFullUpdate()
        {
            if (m_parentGroup != null)
            {
                m_parentGroup.HasGroupChanged = true;
                m_parentGroup.QueueForUpdateCheck();
            }

            int timeNow = Util.UnixTimeSinceEpoch();

            // If multiple updates are scheduled on the same second, we still need to perform all of them
            // So we'll force the issue by bumping up the timestamp so that later processing sees these need
            // to be performed.
            if (timeNow <= TimeStampFull)
            {
                TimeStampFull += 1;
            }
            else
            {
                TimeStampFull = (uint)timeNow;
            }

            m_updateFlag = 2;

//            m_log.DebugFormat(
//                "[SCENE OBJECT PART]: Scheduling full  update for {0}, {1} at {2}",
//                UUID, Name, TimeStampFull);
        }

        public void AddFlag(LLObject.ObjectFlags flag)
        {
            LLObject.ObjectFlags prevflag = Flags;
            //uint objflags = Flags;
            if ((ObjectFlags & (uint) flag) == 0)
            {
                //Console.WriteLine("Adding flag: " + ((LLObject.ObjectFlags) flag).ToString());
                Flags |= flag;
            }
            //uint currflag = (uint)Flags;
            //System.Console.WriteLine("Aprev: " + prevflag.ToString() + " curr: " + Flags.ToString());
            //ScheduleFullUpdate();
        }

        public void RemFlag(LLObject.ObjectFlags flag)
        {
            LLObject.ObjectFlags prevflag = Flags;
            if ((ObjectFlags & (uint) flag) != 0)
            {
                //Console.WriteLine("Removing flag: " + ((LLObject.ObjectFlags)flag).ToString());
                Flags &= ~flag;
            }
            //System.Console.WriteLine("prev: " + prevflag.ToString() + " curr: " + Flags.ToString());
            //ScheduleFullUpdate();
        }

        /// <summary>
        /// Schedule a terse update for this prim.  Terse updates only send position,
        /// rotation, velocity, rotational velocity and shape information.
        /// </summary>
        public void ScheduleTerseUpdate()
        {
            if (m_updateFlag < 1)
            {
                if (m_parentGroup != null)
                {
                    m_parentGroup.HasGroupChanged = true;
                    m_parentGroup.QueueForUpdateCheck();
                }
                TimeStampTerse = (uint) Util.UnixTimeSinceEpoch();
                m_updateFlag = 1;

//                m_log.DebugFormat(
//                    "[SCENE OBJECT PART]: Scheduling terse update for {0}, {1} at {2}",
//                    UUID, Name, TimeStampTerse);
            }
        }

        /// <summary>
        /// Tell all the prims which have had updates scheduled
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
            if (PhysActor != null)
            {
                PhysActor.Shape = m_shape;
            }
            ScheduleFullUpdate();
        }

        #endregion

        #region ExtraParams

        public void UpdatePrimFlags(ushort type, bool inUse, byte[] data)
        {


            //m_log.Info("TSomething1:" + ((type & (ushort)ExtraParamType.Something1) == (ushort)ExtraParamType.Something1));
            //m_log.Info("TSomething2:" + ((type & (ushort)ExtraParamType.Something2) == (ushort)ExtraParamType.Something2));
            //m_log.Info("TSomething3:" + ((type & (ushort)ExtraParamType.Something3) == (ushort)ExtraParamType.Something3));
            //m_log.Info("TSomething4:" + ((type & (ushort)ExtraParamType.Something4) == (ushort)ExtraParamType.Something4));
            //m_log.Info("TSomething5:" + ((type & (ushort)ExtraParamType.Something5) == (ushort)ExtraParamType.Something5));
            //m_log.Info("TSomething6:" + ((type & (ushort)ExtraParamType.Something6) == (ushort)ExtraParamType.Something6));

            bool usePhysics = false;
            bool IsTemporary = false;
            bool IsPhantom = false;
            bool castsShadows = false;
            bool wasUsingPhysics = ((ObjectFlags & (uint) LLObject.ObjectFlags.Physics) != 0);
            //bool IsLocked = false;
            int i = 0;


            try
            {
                i += 46;
                //IsLocked = (data[i++] != 0) ? true : false;
                usePhysics = ((data[i++] != 0) && m_parentGroup.Scene.m_physicalPrim) ? true : false;
                //System.Console.WriteLine("U" + packet.ToBytes().Length.ToString());
                IsTemporary = (data[i++] != 0) ? true : false;
                IsPhantom = (data[i++] != 0) ? true : false;
                castsShadows = (data[i++] != 0) ? true : false;
            }
            catch (Exception)
            {
                Console.WriteLine("Ignoring invalid Packet:");
                //Silently ignore it - TODO: FIXME Quick
            }

            if (usePhysics)
            {
                AddFlag(LLObject.ObjectFlags.Physics);
                if (!wasUsingPhysics)
                {
                    DoPhysicsPropertyUpdate(usePhysics, false);
                    if (m_parentGroup != null)
                    {
                        if (m_parentGroup.RootPart != null)
                        {
                            if (LocalId == m_parentGroup.RootPart.LocalId)
                            {
                                m_parentGroup.CheckSculptAndLoad();
                            }
                        }
                    }
                }
            }
            else
            {
                RemFlag(LLObject.ObjectFlags.Physics);
                if (wasUsingPhysics)
                {
                    DoPhysicsPropertyUpdate(usePhysics, false);
                }
            }


            if (IsPhantom)
            {
                AddFlag(LLObject.ObjectFlags.Phantom);
                if (PhysActor != null)
                {
                    m_parentGroup.Scene.PhysicsScene.RemovePrim(PhysActor);
                    /// that's not wholesome.  Had to make Scene public
                    PhysActor = null;
                }
            }
            else
            {
                RemFlag(LLObject.ObjectFlags.Phantom);
                if (PhysActor == null)
                {
                    PhysActor = m_parentGroup.Scene.PhysicsScene.AddPrimShape(
                        Name,
                        Shape,
                        new PhysicsVector(AbsolutePosition.X, AbsolutePosition.Y,
                                          AbsolutePosition.Z),
                        new PhysicsVector(Scale.X, Scale.Y, Scale.Z),
                        new Quaternion(RotationOffset.W, RotationOffset.X,
                                       RotationOffset.Y, RotationOffset.Z), usePhysics);

                    if (PhysActor != null)
                    {
                        PhysActor.LocalID = LocalId;
                        DoPhysicsPropertyUpdate(usePhysics, true);
                        if (m_parentGroup != null)
                        {
                            if (m_parentGroup.RootPart != null)
                            {
                                if (LocalId == m_parentGroup.RootPart.LocalId)
                                {
                                    m_parentGroup.CheckSculptAndLoad();
                                }
                            }
                        }
                    }
                }
                else
                {
                    PhysActor.IsPhysical = usePhysics;
                    DoPhysicsPropertyUpdate(usePhysics, false);
                    if (m_parentGroup != null)
                    {
                        if (m_parentGroup.RootPart != null)
                        {
                            if (LocalId == m_parentGroup.RootPart.LocalId)
                            {
                                m_parentGroup.CheckSculptAndLoad();
                            }
                        }
                    }
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
        public void ScriptSetPhysicsStatus(bool UsePhysics)
        {
            if (m_parentGroup != null)
            {
                m_parentGroup.ScriptSetPhysicsStatus(UsePhysics);
            }
        }
        public void ScriptSetPhantomStatus(bool Phantom)
        {
            if (m_parentGroup != null)
            {
                m_parentGroup.ScriptSetPhantomStatus(Phantom);
            }
        }
        public void DoPhysicsPropertyUpdate(bool UsePhysics, bool isNew)
        {
            if (PhysActor != null)
            {
                if (UsePhysics != PhysActor.IsPhysical || isNew)
                {
                    if (PhysActor.IsPhysical)
                    {
                        if (!isNew)
                            ParentGroup.Scene.RemovePhysicalPrim(1);

                        PhysActor.OnRequestTerseUpdate -= PhysicsRequestingTerseUpdate;
                        PhysActor.OnOutOfBounds -= PhysicsOutOfBounds;
                        PhysActor.delink();
                    }

                    PhysActor.IsPhysical = UsePhysics;


                    // If we're not what we're supposed to be in the physics scene, recreate ourselves.
                    //m_parentGroup.Scene.PhysicsScene.RemovePrim(PhysActor);
                    /// that's not wholesome.  Had to make Scene public
                    //PhysActor = null;


                    if ((ObjectFlags & (uint) LLObject.ObjectFlags.Phantom) == 0)
                    {
                        //PhysActor = m_parentGroup.Scene.PhysicsScene.AddPrimShape(
                        //Name,
                        //Shape,
                        //new PhysicsVector(AbsolutePosition.X, AbsolutePosition.Y,
                        //AbsolutePosition.Z),
                        //new PhysicsVector(Scale.X, Scale.Y, Scale.Z),
                        //new Quaternion(RotationOffset.W, RotationOffset.X,
                        //RotationOffset.Y, RotationOffset.Z), UsePhysics);
                        if (UsePhysics)
                        {
                            ParentGroup.Scene.AddPhysicalPrim(1);

                            PhysActor.OnRequestTerseUpdate += PhysicsRequestingTerseUpdate;
                            PhysActor.OnOutOfBounds += PhysicsOutOfBounds;
                            if (ParentID != 0 && ParentID != LocalId)
                            {
                                if (ParentGroup.RootPart.PhysActor != null)
                                {
                                    PhysActor.link(ParentGroup.RootPart.PhysActor);
                                }
                            }

                        }
                    }
                }
                m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
            }
        }

        public void UpdateExtraParam(ushort type, bool inUse, byte[] data)
        {
            m_shape.ReadInUpdateExtraParam(type, inUse, data);
            
            if (type == 0x30)
            {
                if (m_shape.SculptEntry && m_shape.SculptTexture != LLUUID.Zero)
                {
                    //AssetBase tx = m_parentGroup.Scene.getase
                    m_parentGroup.Scene.AssetCache.GetAsset(m_shape.SculptTexture, SculptTextureCallback, true);
                }
            }
            
            ScheduleFullUpdate();
        }
        
        public void SculptTextureCallback(LLUUID textureID, AssetBase texture)
        {
            if (m_shape.SculptEntry)
            {
                if (texture != null)
                {
                    m_shape.SculptData = texture.Data;
                    if (PhysActor != null)
                    {
                        // Tricks physics engine into thinking we've changed the part shape.
                        PrimitiveBaseShape m_newshape = m_shape.Copy();
                        PhysActor.Shape = m_newshape;
                        m_shape = m_newshape;
                    }
                }
            }

        }

        #endregion

        #region Physics

        public float GetMass()
        {
            if (PhysActor != null)
            {
                return PhysActor.Mass;
            }
            else
            {
                return 0;
            }
        }

        public LLVector3 GetGeometricCenter()
        {
            if (PhysActor != null)
            {
                return new LLVector3(PhysActor.CenterOfMass.X, PhysActor.CenterOfMass.Y, PhysActor.CenterOfMass.Z);
            }
            else
            {
                return new LLVector3(0, 0, 0);
            }
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
            TriggerScriptChangedEvent(Changed.TEXTURE);
            ScheduleFullUpdate();
        }

        // Added to handle bug in libsecondlife's TextureEntry.ToBytes()
        // not handling RGBA properly. Cycles through, and "fixes" the color
        // info
        public void UpdateTexture(LLObject.TextureEntry tex)
        {
            //LLColor tmpcolor;
            //for (uint i = 0; i < 32; i++)
            //{
            //    if (tex.FaceTextures[i] != null)
            //    {
            //        tmpcolor = tex.GetFace((uint) i).RGBA;
            //        tmpcolor.A = tmpcolor.A*255;
            //        tmpcolor.R = tmpcolor.R*255;
            //        tmpcolor.G = tmpcolor.G*255;
            //        tmpcolor.B = tmpcolor.B*255;
            //        tex.FaceTextures[i].RGBA = tmpcolor;
            //    }
            //}
            //tmpcolor = tex.DefaultTexture.RGBA;
            //tmpcolor.A = tmpcolor.A*255;
            //tmpcolor.R = tmpcolor.R*255;
            //tmpcolor.G = tmpcolor.G*255;
            //tmpcolor.B = tmpcolor.B*255;
            //tex.DefaultTexture.RGBA = tmpcolor;
            UpdateTextureEntry(tex.ToBytes());
        }

        public byte ConvertScriptUintToByte(uint indata)
        {
            byte outdata = (byte)TextureAnimFlags.NONE;
            if ((indata & 1) != 0) outdata |= (byte)TextureAnimFlags.ANIM_ON;
            if ((indata & 2) != 0) outdata |= (byte)TextureAnimFlags.LOOP;
            if ((indata & 4) != 0) outdata |= (byte)TextureAnimFlags.REVERSE;
            if ((indata & 8) != 0) outdata |= (byte)TextureAnimFlags.PING_PONG;
            if ((indata & 16) != 0) outdata |= (byte)TextureAnimFlags.SMOOTH;
            if ((indata & 32) != 0) outdata |= (byte)TextureAnimFlags.ROTATE;
            if ((indata & 64) != 0) outdata |= (byte)TextureAnimFlags.SCALE;
            return outdata;
        }

        public void AddTextureAnimation(Primitive.TextureAnimation pTexAnim)
        {
            byte[] data = new byte[16];
            int pos = 0;

            // The flags don't like conversion from uint to byte, so we have to do
            // it the crappy way.  See the above function :(

            data[pos] = ConvertScriptUintToByte(pTexAnim.Flags); pos++;
            data[pos] = (byte)pTexAnim.Face; pos++;
            data[pos] = (byte)pTexAnim.SizeX; pos++;
            data[pos] = (byte)pTexAnim.SizeY; pos++;

            Helpers.FloatToBytes(pTexAnim.Start).CopyTo(data, pos);
            Helpers.FloatToBytes(pTexAnim.Length).CopyTo(data, pos + 4);
            Helpers.FloatToBytes(pTexAnim.Rate).CopyTo(data, pos + 8);

            m_TextureAnimation = data;
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
            if ((pos.X != OffsetPosition.X) ||
                (pos.Y != OffsetPosition.Y) ||
                (pos.Z != OffsetPosition.Z))
            {
                LLVector3 newPos = new LLVector3(pos.X, pos.Y, pos.Z);
                OffsetPosition = newPos;
                ScheduleTerseUpdate();
            }
        }

        public void UpdateGroupPosition(LLVector3 pos)
        {
            if ((pos.X != GroupPosition.X) ||
                (pos.Y != GroupPosition.Y) ||
                (pos.Z != GroupPosition.Z))
            {
                LLVector3 newPos = new LLVector3(pos.X, pos.Y, pos.Z);
                GroupPosition = newPos;
                ScheduleTerseUpdate();
            }
        }

        #endregion

        #region rotation

        public void UpdateRotation(LLQuaternion rot)
        {
            if ((rot.X != RotationOffset.X) ||
                (rot.Y != RotationOffset.Y) ||
                (rot.Z != RotationOffset.Z) ||
                (rot.W != RotationOffset.W))
            {
                //StoreUndoState();
                RotationOffset = new LLQuaternion(rot.X, rot.Y, rot.Z, rot.W);
                ScheduleTerseUpdate();
            }
        }

        #endregion

        #region Sound
        public void PreloadSound(string sound)
        {
            LLUUID ownerID = OwnerID;
            LLUUID objectID = UUID;
            LLUUID soundID = LLUUID.Zero;

            if (!LLUUID.TryParse(sound, out soundID))
            {
                //Trys to fetch sound id from prim's inventory.
                //Prim's inventory doesn't support non script items yet
                SceneObjectPart op = this;
                foreach (KeyValuePair<LLUUID, TaskInventoryItem> item in op.TaskInventory)
                {
                    if (item.Value.Name == sound)
                    {
                        soundID = item.Value.ItemID;
                        break;
                    }
                }
            }

            List<ScenePresence> avatarts = m_parentGroup.Scene.GetAvatars();
            foreach (ScenePresence p in avatarts)
            {
                // TODO: some filtering by distance of avatar

                p.ControllingClient.SendPreLoadSound(objectID, objectID, soundID);
            }
        }

        public void AdjustSoundGain(double volume)
        {
            if (volume > 1)
                volume = 1;
            if (volume < 0)
                volume = 0;

            List<ScenePresence> avatarts = m_parentGroup.Scene.GetAvatars();
            foreach (ScenePresence p in avatarts)
            {
                p.ControllingClient.SendAttachedSoundGainChange(UUID, (float)volume);
            }
        }

        public void SendSound(string sound, double volume, bool triggered, byte flags)
        {
            if (volume > 1)
                volume = 1;
            if (volume < 0)
                volume = 0;

            LLUUID ownerID = OwnerID;
            LLUUID objectID = UUID;
            LLUUID parentID = GetRootPartUUID();
            LLUUID soundID = LLUUID.Zero;
            LLVector3 position = AbsolutePosition; // region local
            ulong regionHandle = m_parentGroup.Scene.RegionInfo.RegionHandle;

            //byte flags = 0;

            if (!LLUUID.TryParse(sound, out soundID))
            {
                // search sound file from inventory
                SceneObjectPart op = this;
                foreach (KeyValuePair<LLUUID, TaskInventoryItem> item in op.TaskInventory)
                {
                    if (item.Value.Name == sound && item.Value.Type == (int)AssetType.Sound)
                    {
                        soundID = item.Value.ItemID;
                        break;
                    }
                }
            }

            if (soundID == LLUUID.Zero)
                return;

            List<ScenePresence> avatarts = m_parentGroup.Scene.GetAvatars();
            foreach (ScenePresence p in avatarts)
            {
                double dis=Util.GetDistanceTo(p.AbsolutePosition, position);
                if (dis > 100.0) // Max audio distance
                    continue;

                // Scale by distance
                volume*=((100.0-dis)/100.0);

                if (triggered)
                {
                    p.ControllingClient.SendTriggeredSound(soundID, ownerID, objectID, parentID, regionHandle, position, (float)volume);
                }
                else
                {
                    p.ControllingClient.SendPlayAttachedSound(soundID, objectID, ownerID, (float)volume, flags);
                }
            }
        }

        #endregion

        #region Resizing/Scale

        /// <summary>
        ///
        /// </summary>
        /// <param name="scale"></param>
        public void Resize(LLVector3 scale)
        {
            StoreUndoState();
            m_shape.Scale = scale;

            ScheduleFullUpdate();
        }

        #endregion

        public void UpdatePermissions(LLUUID AgentID, byte field, uint localID, uint mask, byte addRemTF)
        {
            bool set = addRemTF == 1;

            // Are we the owner?
            if (AgentID == OwnerID)
            {
                switch (field)
                {
                    case 2:
                        OwnerMask = ApplyMask(OwnerMask, set, mask);
                        break;
                    case 4:
                        GroupMask = ApplyMask(GroupMask, set, mask);
                        break;
                    case 8:
                        EveryoneMask = ApplyMask(EveryoneMask, set, mask);
                        break;
                    case 16:
                        NextOwnerMask = ApplyMask(NextOwnerMask, set, mask);
                        break;
                }
                SendFullUpdateToAllClients();

                SendObjectPropertiesToClient(AgentID);

            }
        }

        private void SendObjectPropertiesToClient(LLUUID AgentID)
        {
            List<ScenePresence> avatars = m_parentGroup.Scene.GetScenePresences();
            for (int i = 0; i < avatars.Count; i++)
            {
                // Ugly reference :(
                if (avatars[i].UUID == AgentID)
                {
                    m_parentGroup.GetProperties(avatars[i].ControllingClient);
                }
            }
        }

        private uint ApplyMask(uint val, bool set, uint mask)
        {
            if (set)
            {
                return val |= mask;
            }
            else
            {
                return val &= ~mask;
            }
        }

        #region Client Update Methods

        /// <summary>
        /// Tell all scene presences that they should send updates for this part to their clients
        /// </summary>
        public void AddFullUpdateToAllAvatars()
        {
            List<ScenePresence> avatars = m_parentGroup.Scene.GetScenePresences();
            for (int i = 0; i < avatars.Count; i++)
            {
                avatars[i].QueuePartForUpdate(this);
            }
        }

        public void SendFullUpdateToAllClientsExcept(LLUUID agentID)
        {
            List<ScenePresence> avatars = m_parentGroup.Scene.GetScenePresences();
            for (int i = 0; i < avatars.Count; i++)
            {
                // Ugly reference :(
                if (avatars[i].UUID != agentID)
                {
                    m_parentGroup.SendPartFullUpdate(avatars[i].ControllingClient, this,
                                                    avatars[i].GenerateClientFlags(UUID));
                }
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
            List<ScenePresence> avatars = m_parentGroup.Scene.GetScenePresences();
            for (int i = 0; i < avatars.Count; i++)
            {
                // Ugly reference :(
                m_parentGroup.SendPartFullUpdate(avatars[i].ControllingClient, this,
                                                 avatars[i].GenerateClientFlags(UUID));
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="remoteClient"></param>
        public void SendFullUpdate(IClientAPI remoteClient, uint clientFlags)
        {
            m_parentGroup.SendPartFullUpdate(remoteClient, this, clientFlags);
        }

        /// <summary>
        /// Sends a full update to the client
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="clientFlags"></param>
        public void SendFullUpdateToClient(IClientAPI remoteClient, uint clientflags)
        {
            LLVector3 lPos;
            lPos = OffsetPosition;
            SendFullUpdateToClient(remoteClient, lPos, clientflags);
        }

        /// <summary>
        /// Sends a full update to the client
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="lPos"></param>
        /// <param name="clientFlags"></param>
        public void SendFullUpdateToClient(IClientAPI remoteClient, LLVector3 lPos, uint clientFlags)
        {
            clientFlags &= ~(uint) LLObject.ObjectFlags.CreateSelected;

            if (remoteClient.AgentId == OwnerID)
            {
                if ((uint) (Flags & LLObject.ObjectFlags.CreateSelected) != 0)
                {
                    clientFlags |= (uint) LLObject.ObjectFlags.CreateSelected;
                    Flags &= ~LLObject.ObjectFlags.CreateSelected;
                }
            }


            byte[] color = new byte[] {m_color.R, m_color.G, m_color.B, m_color.A};
            remoteClient.SendPrimitiveToClient(
                                               m_regionHandle, (ushort)(m_parentGroup.GetTimeDilation() * (float)ushort.MaxValue), LocalId, m_shape,
                                               lPos, Velocity, Acceleration, RotationOffset, RotationalVelocity, clientFlags, m_uuid,
                                               OwnerID,
                                               m_text, color, ParentID, m_particleSystem, m_clickAction, m_TextureAnimation, m_IsAttachment, m_attachmentPoint,fromAssetID, Sound, SoundGain, SoundFlags, SoundRadius);
        }

        /// Terse updates
        public void AddTerseUpdateToAllAvatars()
        {
            List<ScenePresence> avatars = m_parentGroup.Scene.GetScenePresences();
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
            List<ScenePresence> avatars = m_parentGroup.Scene.GetScenePresences();
            for (int i = 0; i < avatars.Count; i++)
            {
                m_parentGroup.SendPartTerseUpdate(avatars[i].ControllingClient, this);
            }
        }

        /// <summary>
        /// Send a terse update to the client.
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
            // TODO: I have no idea why we are making this check.  This should be sorted out
            if ((ObjectFlags & (uint) LLObject.ObjectFlags.Physics) == 0)
            {
                remoteClient.SendPrimTerseUpdate(m_regionHandle, (ushort)(m_parentGroup.GetTimeDilation() * (float)ushort.MaxValue), LocalId, lPos, mRot, Velocity, RotationalVelocity, Shape.State, fromAssetID);
            }
            else
            {
                remoteClient.SendPrimTerseUpdate(m_regionHandle, (ushort)(m_parentGroup.GetTimeDilation() * (float)ushort.MaxValue), LocalId, lPos, mRot, Velocity,
                                                 RotationalVelocity);
                //System.Console.WriteLine("LID: " + LocalID + " RVel:" + RotationalVelocity.ToString() + " TD: " + ((ushort)(m_parentGroup.Scene.TimeDilation * 500000f)).ToString() + ":" + m_parentGroup.Scene.TimeDilation.ToString());
            }
        }

        public void SendTerseUpdateToClient(IClientAPI remoteClient, LLVector3 lPos)
        {
            LLQuaternion mRot = RotationOffset;
            if (m_IsAttachment)
            {
                remoteClient.SendPrimTerseUpdate(m_regionHandle, (ushort)(m_parentGroup.GetTimeDilation() * (float)ushort.MaxValue), LocalId, lPos, mRot, Velocity, RotationalVelocity, (byte)((m_attachmentPoint % 16) * 16 + (m_attachmentPoint / 16)),fromAssetID);
            }
            else
            {
                if ((ObjectFlags & (uint)LLObject.ObjectFlags.Physics) == 0)
                {
                    remoteClient.SendPrimTerseUpdate(m_regionHandle, (ushort)(m_parentGroup.GetTimeDilation() * (float)ushort.MaxValue), LocalId, lPos, mRot, Velocity, RotationalVelocity, Shape.State, fromAssetID);
                }
                else
                {
                    remoteClient.SendPrimTerseUpdate(m_regionHandle, (ushort)(m_parentGroup.GetTimeDilation() * (float)ushort.MaxValue), LocalId, lPos, mRot, Velocity,
                                                     RotationalVelocity);
                    //System.Console.WriteLine("LID: " + LocalID + "RVel:" + RotationalVelocity.ToString() + " TD: " + ((ushort)(m_parentGroup.Scene.TimeDilation * 500000f)).ToString() + ":" + m_parentGroup.Scene.TimeDilation.ToString());
                }
            }
        }

        #endregion

        public virtual void UpdateMovement()
        {
        }

        #region Events

        public void PhysicsRequestingTerseUpdate()
        {
            if (PhysActor != null)
            {
                LLVector3 newpos = new LLVector3(PhysActor.Position.GetBytes(), 0);
                if (newpos.X > 257f || newpos.X < -1f || newpos.Y > 257f || newpos.Y < -1f)
                {
                    m_parentGroup.AbsolutePosition = newpos;
                    return;
                }

            }
            ScheduleTerseUpdate();

            //SendTerseUpdateToAllClients();
        }

        #endregion

        public void PhysicsOutOfBounds(PhysicsVector pos)
        {
            m_log.Info("[PHYSICS]: Physical Object went out of bounds.");
            RemFlag(LLObject.ObjectFlags.Physics);
            DoPhysicsPropertyUpdate(false, true);
            //m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
        }

        public virtual void OnGrab(LLVector3 offsetPos, IClientAPI remoteClient)
        {
        }


        public void SetText(string text)
        {
            Text = text;
            ScheduleFullUpdate();
        }

        public void SetText(string text, Vector3 color, double alpha)
        {
            Color = Color.FromArgb(0xff - (int) (alpha*0xff),
                                   (int) (color.x*0xff),
                                   (int) (color.y*0xff),
                                   (int) (color.z*0xff));
            SetText(text);
        }

        public int registerTargetWaypoint(LLVector3 target, float tolerance)
        {
            if (m_parentGroup != null)
            {
                return m_parentGroup.registerTargetWaypoint(target, tolerance);
            }
            return 0;
        }
        public void unregisterTargetWaypoint(int handle)
        {
            if (m_parentGroup != null)
            {
                m_parentGroup.unregisterTargetWaypoint(handle);
            }
        }
        protected SceneObjectPart(SerializationInfo info, StreamingContext context)
        {
            //System.Console.WriteLine("SceneObjectPart Deserialize BGN");

            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            /*
            m_queue = (Queue<SceneObjectPart>)info.GetValue("m_queue", typeof(Queue<SceneObjectPart>));
            m_ids = (List<LLUUID>)info.GetValue("m_ids", typeof(List<LLUUID>));
            */

            //System.Console.WriteLine("SceneObjectPart Deserialize END");
        }

        [SecurityPermission(SecurityAction.LinkDemand,
            Flags = SecurityPermissionFlag.SerializationFormatter)]
        public virtual void GetObjectData(
                        SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            info.AddValue("m_inventoryFileName", GetInventoryFileName());
            info.AddValue("m_folderID", UUID);
            info.AddValue("PhysActor", PhysActor);

            Dictionary<Guid, TaskInventoryItem> TaskInventory_work = new Dictionary<Guid, TaskInventoryItem>();

            foreach (LLUUID id in TaskInventory.Keys)
            {
                TaskInventory_work.Add(id.UUID, TaskInventory[id]);
            }

            info.AddValue("TaskInventory", TaskInventory_work);

            info.AddValue("LastOwnerID", LastOwnerID.UUID);
            info.AddValue("OwnerID", OwnerID.UUID);
            info.AddValue("GroupID", GroupID.UUID);

            info.AddValue("OwnershipCost", OwnershipCost);
            info.AddValue("ObjectSaleType", ObjectSaleType);
            info.AddValue("SalePrice", SalePrice);
            info.AddValue("Category", Category);

            info.AddValue("CreationDate", CreationDate);
            info.AddValue("ParentID", ParentID);

            info.AddValue("OwnerMask", OwnerMask);
            info.AddValue("NextOwnerMask", NextOwnerMask);
            info.AddValue("GroupMask", GroupMask);
            info.AddValue("EveryoneMask", EveryoneMask);
            info.AddValue("BaseMask", BaseMask);

            info.AddValue("m_particleSystem", m_particleSystem);

            info.AddValue("TimeStampFull", TimeStampFull);
            info.AddValue("TimeStampTerse", TimeStampTerse);
            info.AddValue("TimeStampLastActivity", TimeStampLastActivity);

            info.AddValue("m_updateFlag", m_updateFlag);
            info.AddValue("CreatorID", CreatorID.UUID);

            info.AddValue("m_inventorySerial", m_inventorySerial);
            info.AddValue("m_uuid", m_uuid.UUID);
            info.AddValue("m_localID", m_localId);
            info.AddValue("m_name", m_name);
            info.AddValue("m_flags", Flags);
            info.AddValue("m_material", m_material);
            info.AddValue("m_regionHandle", m_regionHandle);

            info.AddValue("m_groupPosition.X", m_groupPosition.X);
            info.AddValue("m_groupPosition.Y", m_groupPosition.Y);
            info.AddValue("m_groupPosition.Z", m_groupPosition.Z);

            info.AddValue("m_offsetPosition.X", m_offsetPosition.X);
            info.AddValue("m_offsetPosition.Y", m_offsetPosition.Y);
            info.AddValue("m_offsetPosition.Z", m_offsetPosition.Z);

            info.AddValue("m_rotationOffset.W", m_rotationOffset.W);
            info.AddValue("m_rotationOffset.X", m_rotationOffset.X);
            info.AddValue("m_rotationOffset.Y", m_rotationOffset.Y);
            info.AddValue("m_rotationOffset.Z", m_rotationOffset.Z);

            info.AddValue("m_velocity.X", m_velocity.X);
            info.AddValue("m_velocity.Y", m_velocity.Y);
            info.AddValue("m_velocity.Z", m_velocity.Z);

            info.AddValue("m_rotationalvelocity.X", m_rotationalvelocity.X);
            info.AddValue("m_rotationalvelocity.Y", m_rotationalvelocity.Y);
            info.AddValue("m_rotationalvelocity.Z", m_rotationalvelocity.Z);

            info.AddValue("m_angularVelocity.X", m_angularVelocity.X);
            info.AddValue("m_angularVelocity.Y", m_angularVelocity.Y);
            info.AddValue("m_angularVelocity.Z", m_angularVelocity.Z);

            info.AddValue("m_acceleration.X", m_acceleration.X);
            info.AddValue("m_acceleration.Y", m_acceleration.Y);
            info.AddValue("m_acceleration.Z", m_acceleration.Z);

            info.AddValue("m_description", m_description);
            info.AddValue("m_color", m_color);
            info.AddValue("m_text", m_text);
            info.AddValue("m_sitName", m_sitName);
            info.AddValue("m_touchName", m_touchName);
            info.AddValue("m_clickAction", m_clickAction);
            info.AddValue("m_shape", m_shape);
            info.AddValue("m_parentGroup", m_parentGroup);
            info.AddValue("PayPrice", PayPrice);
        }


        public void Undo()
        {
            if (m_undo.Count > 0)
            {
                UndoState goback = m_undo.Pop();
                if (goback != null)
                    goback.PlaybackState(this);
            }
        }

        public void SetScriptEvents(LLUUID scriptid, int events)
        {
            scriptEvents oldparts;
            lock (m_scriptEvents)
            {
                if (m_scriptEvents.ContainsKey(scriptid))
                {
                    oldparts = m_scriptEvents[scriptid];

                    // remove values from aggregated script events
                    m_scriptEvents[scriptid] = (scriptEvents) events;
                }
                else
                {
                    m_scriptEvents.Add(scriptid, (scriptEvents) events);
                }
            }
            aggregateScriptEvents();
        }

        public void RemoveScriptEvents(LLUUID scriptid)
        {
            lock (m_scriptEvents)
            {
                if (m_scriptEvents.ContainsKey(scriptid))
                {
                    scriptEvents oldparts = scriptEvents.None;
                    oldparts = (scriptEvents) m_scriptEvents[scriptid];

                    // remove values from aggregated script events
                    m_aggregateScriptEvents &= ~oldparts;
                    m_scriptEvents.Remove(scriptid);
                }
            }
            aggregateScriptEvents();
        }

        public void aggregateScriptEvents()
        {
            // Aggregate script events
            lock (m_scriptEvents)
            {
                foreach (scriptEvents s in m_scriptEvents.Values)
                {
                    m_aggregateScriptEvents |= s;
                }
            }

            uint objectflagupdate = 0;

            if (
                ((m_aggregateScriptEvents & scriptEvents.touch) != 0) ||
                ((m_aggregateScriptEvents & scriptEvents.touch_end) != 0) ||
                ((m_aggregateScriptEvents & scriptEvents.touch_start) != 0)
                )
            {
                objectflagupdate |= (uint) LLObject.ObjectFlags.Touch;
            }

            if ((m_aggregateScriptEvents & scriptEvents.money) != 0)
            {
                objectflagupdate |= (uint) LLObject.ObjectFlags.Money;
            }

            if (
                ((m_aggregateScriptEvents & scriptEvents.collision) != 0) ||
                ((m_aggregateScriptEvents & scriptEvents.collision_end) != 0) ||
                ((m_aggregateScriptEvents & scriptEvents.collision_start) != 0)
                )
            {
                // subscribe to physics updates.
                if (PhysActor != null)
                {
                    PhysActor.OnCollisionUpdate += PhysicsCollision;
                    PhysActor.SubscribeEvents(1000);

                }
            }
            else
            {
                if (PhysActor != null)
                {
                    PhysActor.UnSubscribeEvents();
                    PhysActor.OnCollisionUpdate -= PhysicsCollision;
                }
            }
            if ((GetEffectiveObjectFlags() & (uint)LLObject.ObjectFlags.Scripted) != 0)
            {
                m_parentGroup.Scene.EventManager.OnScriptTimerEvent += handleTimerAccounting;
            }
            else
            {
                m_parentGroup.Scene.EventManager.OnScriptTimerEvent -= handleTimerAccounting;
            }

            LocalFlags=(LLObject.ObjectFlags)objectflagupdate;

            if (m_parentGroup != null && m_parentGroup.RootPart == this)
                m_parentGroup.aggregateScriptEvents();
            else
                ScheduleFullUpdate();
        }
        public void PhysicsCollision(EventArgs e)
        {
            //return;

            // single threaded here
            if (e == null)
            {
                return;
            }
            
            CollisionEventUpdate a = (CollisionEventUpdate)e;
            Dictionary<uint, float> collissionswith = a.m_objCollisionList;
            List<uint> thisHitColliders = new List<uint>();
            List<uint> endedColliders = new List<uint>();
            List<uint> startedColliders = new List<uint>();

            // calculate things that started colliding this time
            // and build up list of colliders this time
            foreach (uint localid in collissionswith.Keys)
            {
                if (localid != 0)
                {
                    thisHitColliders.Add(localid);
                    if (!m_lastColliders.Contains(localid))
                    {
                        startedColliders.Add(localid);
                    }


                    //m_log.Debug("[OBJECT]: Collided with:" + localid.ToString() + " at depth of: " + collissionswith[localid].ToString());
                }
            }

            // calculate things that ended colliding
            foreach (uint localID in m_lastColliders)
            {
                if (!thisHitColliders.Contains(localID))
                {
                    endedColliders.Add(localID);
                }
            }
            // remove things that ended colliding from the last colliders list
            foreach (uint localID in endedColliders)
            {
                m_lastColliders.Remove(localID);
            }

            //add the items that started colliding this time to the last colliders list.
            foreach (uint localID in startedColliders)
            {
                m_lastColliders.Add(localID);
            }
            // do event notification
            if (startedColliders.Count > 0)
            {
                ColliderArgs StartCollidingMessage = new ColliderArgs();
                List<DetectedObject> colliding = new List<DetectedObject>();
                foreach (uint localId in startedColliders)
                {
                    // always running this check because if the user deletes the object it would return a null reference.
                    if (m_parentGroup == null)
                        return;
                    if (m_parentGroup.Scene == null)
                        return;
                    SceneObjectPart obj = m_parentGroup.Scene.GetSceneObjectPart(localId);
                    if (obj != null)
                    {
                        DetectedObject detobj = new DetectedObject();
                        detobj.keyUUID = obj.UUID;
                        detobj.nameStr = obj.Name;
                        detobj.ownerUUID = obj.OwnerID;
                        detobj.posVector = obj.AbsolutePosition;
                        detobj.rotQuat = obj.GetWorldRotation();
                        detobj.velVector = obj.Velocity;
                        detobj.colliderType = 0;
                        detobj.groupUUID = obj.GroupID;
                        colliding.Add(detobj);
                    }
                }
                if (colliding.Count > 0)
                {
                    StartCollidingMessage.Colliders = colliding;
                    // always running this check because if the user deletes the object it would return a null reference.
                    if (m_parentGroup == null)
                        return;
                    if (m_parentGroup.Scene == null)
                        return;
                    m_parentGroup.Scene.EventManager.TriggerScriptCollidingStart(LocalId, StartCollidingMessage);
                }

            }
            if (m_lastColliders.Count > 0)
            {
                ColliderArgs CollidingMessage = new ColliderArgs();
                List<DetectedObject> colliding = new List<DetectedObject>();
                foreach (uint localId in m_lastColliders)
                {
                    // always running this check because if the user deletes the object it would return a null reference.
                    if (localId == 0)
                        continue;

                    if (m_parentGroup == null)
                        return;
                    if (m_parentGroup.Scene == null)
                        return;
                    SceneObjectPart obj = m_parentGroup.Scene.GetSceneObjectPart(localId);
                    if (obj != null)
                    {
                        DetectedObject detobj = new DetectedObject();
                        detobj.keyUUID = obj.UUID;
                        detobj.nameStr = obj.Name;
                        detobj.ownerUUID = obj.OwnerID;
                        detobj.posVector = obj.AbsolutePosition;
                        detobj.rotQuat = obj.GetWorldRotation();
                        detobj.velVector = obj.Velocity;
                        detobj.colliderType = 0;
                        detobj.groupUUID = obj.GroupID;
                        colliding.Add(detobj);
                    }
                }
                if (colliding.Count > 0)
                {
                    CollidingMessage.Colliders = colliding;
                    // always running this check because if the user deletes the object it would return a null reference.
                    if (m_parentGroup == null)
                        return;
                    if (m_parentGroup.Scene == null)
                        return;
                    m_parentGroup.Scene.EventManager.TriggerScriptCollidingStart(LocalId, CollidingMessage);
                }

            }
            if (endedColliders.Count > 0)
            {
                ColliderArgs EndCollidingMessage = new ColliderArgs();
                List<DetectedObject> colliding = new List<DetectedObject>();
                foreach (uint localId in endedColliders)
                {
                    if (localId == 0)
                        continue;

                    // always running this check because if the user deletes the object it would return a null reference.
                    if (m_parentGroup == null)
                        return;
                    if (m_parentGroup.Scene == null)
                        return;
                    SceneObjectPart obj = m_parentGroup.Scene.GetSceneObjectPart(localId);
                    if (obj != null)
                    {
                        DetectedObject detobj = new DetectedObject();
                        detobj.keyUUID = obj.UUID;
                        detobj.nameStr = obj.Name;
                        detobj.ownerUUID = obj.OwnerID;
                        detobj.posVector = obj.AbsolutePosition;
                        detobj.rotQuat = obj.GetWorldRotation();
                        detobj.velVector = obj.Velocity;
                        detobj.colliderType = 0;
                        detobj.groupUUID = obj.GroupID;
                        colliding.Add(detobj);
                    }
                }
                if (colliding.Count > 0)
                {
                    EndCollidingMessage.Colliders = colliding;
                    // always running this check because if the user deletes the object it would return a null reference.
                    if (m_parentGroup == null)
                        return;
                    if (m_parentGroup.Scene == null)
                        return;
                    m_parentGroup.Scene.EventManager.TriggerScriptCollidingStart(LocalId, EndCollidingMessage);
                }

            }
        }



        public void SetDieAtEdge(bool p)
        {
            if (m_parentGroup == null)
                return;
            if (m_parentGroup.RootPart == null)
                return;

            m_parentGroup.RootPart.DIE_AT_EDGE = p;
        }
        public bool GetDieAtEdge()
        {
            if (m_parentGroup == null)
                return false;
            if (m_parentGroup.RootPart == null)
                return false;

            return m_parentGroup.RootPart.DIE_AT_EDGE;
        }

        public void GetProperties(IClientAPI client)
        {

            client.SendObjectPropertiesReply(LLUUID.Zero, (ulong)CreationDate, CreatorID, LLUUID.Zero, LLUUID.Zero,
                                               GroupID, (short)InventorySerial, LastOwnerID, UUID, OwnerID,
                                               ParentGroup.RootPart.TouchName, new byte[0], ParentGroup.RootPart.SitName, Name, Description,
                                               ParentGroup.RootPart.OwnerMask, ParentGroup.RootPart.NextOwnerMask, ParentGroup.RootPart.GroupMask, ParentGroup.RootPart.EveryoneMask,
                                               ParentGroup.RootPart.BaseMask);

        }
        public void SetGroup(LLUUID groupID, IClientAPI client)
        {
            GroupID = groupID;
            GetProperties(client);
            m_updateFlag = 2;
        }
        private void handleTimerAccounting(uint localID, double interval)
        {
            if (localID == LocalId)
            {

                float sec = (float)interval;
                if (m_parentGroup != null)
                {
                    if (sec == 0)
                    {
                        if (m_parentGroup.scriptScore + 0.001f >= float.MaxValue - 0.001)
                            m_parentGroup.scriptScore = 0;

                        m_parentGroup.scriptScore += 0.001f;
                        return;
                    }

                    if (m_parentGroup.scriptScore + (0.001f / sec) >= float.MaxValue - (0.001f / sec))
                        m_parentGroup.scriptScore = 0;
                    m_parentGroup.scriptScore += (0.001f / sec);
                }

            }
        }

    }
}
