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
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Xml;
using System.Xml.Serialization;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes.Scripting;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Framework.Scenes
{
    #region Enumerations

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
        OWNER = 128,
        REGION_RESTART = 256,
        REGION = 512,
        TELEPORT = 1024
    }

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

    #endregion Enumerations

    public class SceneObjectPart : IScriptHost
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // use only one serializer to give the runtime a chance to optimize it (it won't do that if you
        // use a new instance every time)
        private static XmlSerializer serializer = new XmlSerializer(typeof (SceneObjectPart));

        #region Fields

        public bool AllowedDrop;

        [XmlIgnore]
        public bool DIE_AT_EDGE;

        // TODO: This needs to be persisted in next XML version update!
        [XmlIgnore]
        public readonly int[] PayPrice = {-2,-2,-2,-2,-2};
        [XmlIgnore]
        public PhysicsActor PhysActor;

        //Xantor 20080528 Sound stuff:
        //  Note: This isn't persisted in the database right now, as the fields for that aren't just there yet.
        //        Not a big problem as long as the script that sets it remains in the prim on startup.
        //        for SL compatibility it should be persisted though (set sound / displaytext / particlesystem, kill script)
        [XmlIgnore]
        public UUID Sound;
        
        [XmlIgnore]
        public byte SoundFlags;
        
        [XmlIgnore]
        public double SoundGain;
        
        [XmlIgnore]
        public double SoundRadius;
        
        [XmlIgnore]
        public uint TimeStampFull;
        
        [XmlIgnore]
        public uint TimeStampLastActivity; // Will be used for AutoReturn
        
        [XmlIgnore]
        public uint TimeStampTerse;

        [XmlIgnore]
        public UUID FromItemID;
               
        /// <value>
        /// The UUID of the user inventory item from which this object was rezzed if this is a root part.
        /// If UUID.Zero then either this is not a root part or there is no connection with a user inventory item.
        /// </value>
        private UUID m_fromUserInventoryItemID;
        
        [XmlIgnore]
        public UUID FromUserInventoryItemID
        {
            get { return m_fromUserInventoryItemID; }
        }

        [XmlIgnore]
        public bool IsAttachment;

        [XmlIgnore]
        public scriptEvents AggregateScriptEvents;

        [XmlIgnore]
        public UUID AttachedAvatar;

        [XmlIgnore]
        public Vector3 AttachedPos;

        [XmlIgnore]
        public uint AttachmentPoint;
        
        [XmlIgnore]
        public PhysicsVector RotationAxis = new PhysicsVector(1f, 1f, 1f);

        [XmlIgnore]
        public bool VolumeDetectActive; // XmlIgnore set to avoid problems with persistance until I come to care for this
                                        // Certainly this must be a persistant setting finally

        [XmlIgnore]
        public bool IsWaitingForFirstSpinUpdatePacket;

        [XmlIgnore]
        public Quaternion SpinOldOrientation = Quaternion.Identity;

        /// <summary>
        /// This part's inventory
        /// </summary>
        [XmlIgnore]
        public IEntityInventory Inventory
        {
            get { return m_inventory; }
        }
        protected SceneObjectPartInventory m_inventory;

        [XmlIgnore]
        public bool Undoing;

        [XmlIgnore]
        private PrimFlags LocalFlags;
        [XmlIgnore]
        private float m_damage = -1.0f;
        private byte[] m_TextureAnimation;
        private byte m_clickAction;
        private Color m_color = Color.Black;
        private string m_description = String.Empty;
        private readonly List<uint> m_lastColliders = new List<uint>();
        private int m_linkNum;
        [XmlIgnore]
        private int m_scriptAccessPin;
        [XmlIgnore]
        private readonly Dictionary<UUID, scriptEvents> m_scriptEvents = new Dictionary<UUID, scriptEvents>();
        private string m_sitName = String.Empty;
        private Quaternion m_sitTargetOrientation = Quaternion.Identity;
        private Vector3 m_sitTargetPosition;
        private string m_sitAnimation = "SIT";
        private string m_text = String.Empty;
        private string m_touchName = String.Empty;
        private readonly UndoStack<UndoState> m_undo = new UndoStack<UndoState>(5);
        private UUID _creatorID;

        private bool m_passTouches;

        /// <summary>
        /// Only used internally to schedule client updates.
        /// 0 - no update is scheduled
        /// 1 - terse update scheduled
        /// 2 - full update scheduled
        ///
        /// TODO - This should be an enumeration
        /// </summary>
        private byte m_updateFlag;

        protected Vector3 m_acceleration;
        protected Vector3 m_angularVelocity;

        //unkown if this will be kept, added as a way of removing the group position from the group class
        protected Vector3 m_groupPosition;
        protected uint m_localId;
        protected Material m_material = OpenMetaverse.Material.Wood;
        protected string m_name;
        protected Vector3 m_offsetPosition;

        // FIXME, TODO, ERROR: 'ParentGroup' can't be in here, move it out.
        protected SceneObjectGroup m_parentGroup;
        protected byte[] m_particleSystem = Utils.EmptyBytes;
        protected ulong m_regionHandle;
        protected Quaternion m_rotationOffset;
        protected PrimitiveBaseShape m_shape;
        protected UUID m_uuid;
        protected Vector3 m_velocity;

        // TODO: Those have to be changed into persistent properties at some later point,
        // or sit-camera on vehicles will break on sim-crossing.
        private Vector3 m_cameraEyeOffset;
        private Vector3 m_cameraAtOffset;
        private bool m_forceMouselook;

        // TODO: Collision sound should have default.
        private UUID m_collisionSound;
        private float m_collisionSoundVolume;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// No arg constructor called by region restore db code
        /// </summary>
        public SceneObjectPart()
        {
            // It's not necessary to persist this
            m_TextureAnimation = Utils.EmptyBytes;
            m_particleSystem = Utils.EmptyBytes;
            Rezzed = DateTime.UtcNow;
            
            m_inventory = new SceneObjectPartInventory(this);
        }

        /// <summary>
        /// Create a completely new SceneObjectPart (prim).  This will need to be added separately to a SceneObjectGroup
        /// </summary>
        /// <param name="ownerID"></param>
        /// <param name="shape"></param>
        /// <param name="position"></param>
        /// <param name="rotationOffset"></param>
        /// <param name="offsetPosition"></param>
        public SceneObjectPart(
            UUID ownerID, PrimitiveBaseShape shape, Vector3 groupPosition, 
            Quaternion rotationOffset, Vector3 offsetPosition)
        {
            m_name = "Primitive";

            Rezzed = DateTime.UtcNow;
            _creationDate = (int)Utils.DateTimeToUnixTime(Rezzed);
            _ownerID = ownerID;
            _creatorID = _ownerID;
            _lastOwnerID = UUID.Zero;
            UUID = UUID.Random();
            Shape = shape;
            // Todo: Add More Object Parameter from above!
            _ownershipCost = 0;
            _objectSaleType = 0;
            _salePrice = 0;
            _category = 0;
            _lastOwnerID = _creatorID;
            // End Todo: ///
            GroupPosition = groupPosition;
            OffsetPosition = offsetPosition;
            RotationOffset = rotationOffset;
            Velocity = Vector3.Zero;
            AngularVelocity = Vector3.Zero;
            Acceleration = Vector3.Zero;
            m_TextureAnimation = Utils.EmptyBytes;
            m_particleSystem = Utils.EmptyBytes;

            // Prims currently only contain a single folder (Contents).  From looking at the Second Life protocol,
            // this appears to have the same UUID (!) as the prim.  If this isn't the case, one can't drag items from
            // the prim into an agent inventory (Linden client reports that the "Object not found for drop" in its log

            _flags = 0;
            _flags |= PrimFlags.CreateSelected;

            TrimPermissions();
            //m_undo = new UndoStack<UndoState>(ParentGroup.GetSceneMaxUndo());
            
            m_inventory = new SceneObjectPartInventory(this);
        }

        #endregion Constructors

        #region XML Schema

        private UUID _lastOwnerID;
        private UUID _ownerID;
        private UUID _groupID;
        private int _ownershipCost;
        private byte _objectSaleType;
        private int _salePrice;
        private uint _category;
        private Int32 _creationDate;
        private uint _parentID = 0;
        private UUID m_sitTargetAvatar = UUID.Zero;
        private uint _baseMask = (uint)PermissionMask.All;
        private uint _ownerMask = (uint)PermissionMask.All;
        private uint _groupMask = (uint)PermissionMask.None;
        private uint _everyoneMask = (uint)PermissionMask.None;
        private uint _nextOwnerMask = (uint)PermissionMask.All;
        private PrimFlags _flags = 0;
        private DateTime m_expires;
        private DateTime m_rezzed;

        public UUID CreatorID 
        {
            get
            {
                return _creatorID;
            }
            set
            {
                _creatorID = value;
            }
        }

        /// <summary>
        /// A relic from when we we thought that prims contained folder objects. In 
        /// reality, prim == folder
        /// Exposing this is not particularly good, but it's one of the least evils at the moment to see
        /// folder id from prim inventory item data, since it's not (yet) actually stored with the prim.
        /// </summary>
        public UUID FolderID
        {
            get { return UUID; }
            set { } // Don't allow assignment, or legacy prims wil b0rk - but we need the setter for legacy serialization.
        }

        /// <value>
        /// Access should be via Inventory directly - this property temporarily remains for xml serialization purposes
        /// </value>
        public uint InventorySerial
        {
            get { return m_inventory.Serial; }
            set { m_inventory.Serial = value; }
        }

        /// <value>
        /// Access should be via Inventory directly - this property temporarily remains for xml serialization purposes
        /// </value>
        public TaskInventoryDictionary TaskInventory
        {
            get { return m_inventory.Items; }
            set { m_inventory.Items = value; }
        }

        public uint ObjectFlags
        {
            get { return (uint)_flags; }
            set { _flags = (PrimFlags)value; }
        }

        public UUID UUID
        {
            get { return m_uuid; }
            set { m_uuid = value; }
        }

        public uint LocalId
        {
            get { return m_localId; }
            set { m_localId = value; }
        }

        public virtual string Name
        {
            get { return m_name; }
            set 
            { 
                m_name = value;
                if (PhysActor != null)
                {
                    PhysActor.SOPName = value;
                }
            }
        }

        public byte Material
        {
            get { return (byte) m_material; }
            set
            {
                m_material = (Material)value;
                if (PhysActor != null)
                {
                    PhysActor.SetMaterial((int)value);
                }
            }
        }

        public bool PassTouches
        {
            get { return m_passTouches; }
            set
            {
                m_passTouches = value;
                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
            }
        }

        public ulong RegionHandle
        {
            get { return m_regionHandle; }
            set { m_regionHandle = value; }
        }

        public int ScriptAccessPin
        {
            get { return m_scriptAccessPin; }
            set { m_scriptAccessPin = (int)value; }
        }

        [XmlIgnore]
        public Byte[] TextureAnimation
        {
            get { return m_TextureAnimation; }
            set { m_TextureAnimation = value; }
        }

        [XmlIgnore]
        public Byte[] ParticleSystem
        {
            get { return m_particleSystem; }
            set { m_particleSystem = value; }
        }

        [XmlIgnore]
        public DateTime Expires
        {
            get { return m_expires; }
            set { m_expires = value; }
        }

        [XmlIgnore]
        public DateTime Rezzed
        {
            get { return m_rezzed; }
            set { m_rezzed = value; }
        }

        [XmlIgnore]
        public float Damage
        {
            get { return m_damage; }
            set { m_damage = value; }
        }

        /// <summary>
        /// The position of the entire group that this prim belongs to.
        /// </summary>
        public Vector3 GroupPosition
        {
            get
            {
                // If this is a linkset, we don't want the physics engine mucking up our group position here.
                if (PhysActor != null && _parentID == 0)
                {
                    m_groupPosition.X = PhysActor.Position.X;
                    m_groupPosition.Y = PhysActor.Position.Y;
                    m_groupPosition.Z = PhysActor.Position.Z;
                }

                if (IsAttachment)
                {
                    ScenePresence sp = m_parentGroup.Scene.GetScenePresence(AttachedAvatar);
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
                        if (_parentID == 0)
                        {
                            PhysActor.Position = new PhysicsVector(value.X, value.Y, value.Z);
                        }
                        else
                        {
                            // To move the child prim in respect to the group position and rotation we have to calculate
                            Vector3 resultingposition = GetWorldPosition();
                            PhysActor.Position = new PhysicsVector(resultingposition.X, resultingposition.Y, resultingposition.Z);
                            Quaternion resultingrot = GetWorldRotation();
                            PhysActor.Orientation = resultingrot;
                        }

                        // Tell the physics engines that this prim changed.
                        m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
                    }
                    catch (Exception e)
                    {
                        m_log.Error("[SCENEOBJECTPART]: GROUP POSITION. " + e.Message);
                    }
                }
                
                // TODO if we decide to do sitting in a more SL compatible way (multiple avatars per prim), this has to be fixed, too
                if (m_sitTargetAvatar != UUID.Zero)
                {
                    if (m_parentGroup != null) // TODO can there be a SOP without a SOG?
                    {
                        ScenePresence avatar;
                        if (m_parentGroup.Scene.TryGetAvatar(m_sitTargetAvatar, out avatar))
                        {
                            avatar.ParentPosition = GetWorldPosition();
                        }
                    }
                }
            }
        }

        public Vector3 OffsetPosition
        {
            get { return m_offsetPosition; }
            set
            {
                StoreUndoState();
                m_offsetPosition = value;

                if (ParentGroup != null && !ParentGroup.IsDeleted)
                {
                     if (_parentID != 0 && PhysActor != null)
                    {
                        Vector3 resultingposition = GetWorldPosition();
                        PhysActor.Position = new PhysicsVector(resultingposition.X, resultingposition.Y, resultingposition.Z);
                        Quaternion resultingrot = GetWorldRotation();
                        PhysActor.Orientation = resultingrot;

                        // Tell the physics engines that this prim changed.
                        m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
                    }
                }
            }
        }

        public Quaternion RotationOffset
        {
            get
            {
                // We don't want the physics engine mucking up the rotations in a linkset
                if ((_parentID == 0) && (Shape.PCode != 9 || Shape.State == 0)  && (PhysActor != null))
                {
                    if (PhysActor.Orientation.X != 0 || PhysActor.Orientation.Y != 0
                        || PhysActor.Orientation.Z != 0 || PhysActor.Orientation.W != 0)
                    {
                        m_rotationOffset = PhysActor.Orientation;
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
                        if (_parentID == 0)
                        {
                            PhysActor.Orientation = value;
                            //m_log.Info("[PART]: RO1:" + PhysActor.Orientation.ToString());
                        }
                        else
                        {
                            // Child prim we have to calculate it's world rotationwel
                            Quaternion resultingrotation = GetWorldRotation();
                            PhysActor.Orientation = resultingrotation;
                            //m_log.Info("[PART]: RO2:" + PhysActor.Orientation.ToString());
                        }
                        m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
                        //}
                    }
                    catch (Exception ex)
                    {
                        m_log.Error("[SCENEOBJECTPART]: ROTATIONOFFSET" + ex.Message);
                    }
                }

            }
        }

        /// <summary></summary>
        public Vector3 Velocity
        {
            get
            {
                //if (PhysActor.Velocity.X != 0 || PhysActor.Velocity.Y != 0
                //|| PhysActor.Velocity.Z != 0)
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

            set
            {
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

        public Vector3 RotationalVelocity
        {
            get { return AngularVelocity; }
            set { AngularVelocity = value; }
        }

        /// <summary></summary>
        public Vector3 AngularVelocity
        {
            get
            {
                if ((PhysActor != null) && PhysActor.IsPhysical)
                {
                    m_angularVelocity.FromBytes(PhysActor.RotationalVelocity.GetBytes(), 0);
                }
                return m_angularVelocity;
            }
            set { m_angularVelocity = value; }
        }

        /// <summary></summary>
        public Vector3 Acceleration
        {
            get { return m_acceleration; }
            set { m_acceleration = value; }
        }

        public string Description
        {
            get { return m_description; }
            set 
            {
                m_description = value;
                if (PhysActor != null)
                {
                    PhysActor.SOPDescription = value;
                }
            }
        }

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


        public string SitName
        {
            get { return m_sitName; }
            set { m_sitName = value; }
        }

        public string TouchName
        {
            get { return m_touchName; }
            set { m_touchName = value; }
        }

        public int LinkNum
        {
            get { return m_linkNum; }
            set { m_linkNum = value; }
        }

        public byte ClickAction
        {
            get { return m_clickAction; }
            set
            {
                m_clickAction = value;
            }
        }

        public PrimitiveBaseShape Shape
        {
            get { return m_shape; }
            set
            {
                bool shape_changed = false;
                // TODO: this should really be restricted to the right
                // set of attributes on shape change.  For instance,
                // changing the lighting on a shape shouldn't cause
                // this.
                if (m_shape != null)
                    shape_changed = true;

                m_shape = value;

                if (shape_changed)
                    TriggerScriptChangedEvent(Changed.SHAPE);
            }
        }
        
        public Vector3 Scale
        {
            get { return m_shape.Scale; }
            set
            {
                StoreUndoState();
if (m_shape != null) {
                m_shape.Scale = value;

                if (PhysActor != null && m_parentGroup != null)
                {
                    if (m_parentGroup.Scene != null)
                    {
                        if (m_parentGroup.Scene.PhysicsScene != null)
                        {
                            PhysActor.Size = new PhysicsVector(m_shape.Scale.X, m_shape.Scale.Y, m_shape.Scale.Z);
                            m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
                        }
                    }
                }
}
                TriggerScriptChangedEvent(Changed.SCALE);
            }
        }
        public byte UpdateFlag
        {
            get { return m_updateFlag; }
            set { m_updateFlag = value; }
        }

        #endregion

//---------------
#region Public Properties with only Get

        public Vector3 AbsolutePosition
        {
            get {
                if (IsAttachment)
                    return GroupPosition;

                return m_offsetPosition + m_groupPosition; }
        }

        public SceneObjectGroup ParentGroup
        {
            get { return m_parentGroup; }
        }

        public scriptEvents ScriptEvents
        {
            get { return AggregateScriptEvents; }
        }


        public Quaternion SitTargetOrientation
        {
            get { return m_sitTargetOrientation; }
            set { m_sitTargetOrientation = value; }
        }


        public Vector3 SitTargetPosition
        {
            get { return m_sitTargetPosition; }
            set { m_sitTargetPosition = value; }
        }

        // This sort of sucks, but I'm adding these in to make some of
        // the mappings more consistant.
        public Vector3 SitTargetPositionLL
        {
            get { return new Vector3(m_sitTargetPosition.X, m_sitTargetPosition.Y,m_sitTargetPosition.Z); }
            set { m_sitTargetPosition = value; }
        }

        public Quaternion SitTargetOrientationLL
        {
            get
            {
                return new Quaternion(
                                        m_sitTargetOrientation.X,
                                        m_sitTargetOrientation.Y,
                                        m_sitTargetOrientation.Z,
                                        m_sitTargetOrientation.W
                                        );
            }

            set { m_sitTargetOrientation = new Quaternion(value.X, value.Y, value.Z, value.W); }
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

        public uint ParentID
        {
            get { return _parentID; }
            set { _parentID = value; }
        }

        public int CreationDate
        {
            get { return _creationDate; }
            set { _creationDate = value; }
        }

        public uint Category
        {
            get { return _category; }
            set { _category = value; }
        }

        public int SalePrice
        {
            get { return _salePrice; }
            set { _salePrice = value; }
        }

        public byte ObjectSaleType
        {
            get { return _objectSaleType; }
            set { _objectSaleType = value; }
        }

        public int OwnershipCost
        {
            get { return _ownershipCost; }
            set { _ownershipCost = value; }
        }

        public UUID GroupID
        {
            get { return _groupID; }
            set { _groupID = value; }
        }

        public UUID OwnerID
        {
            get { return _ownerID; }
            set { _ownerID = value; }
        }

        public UUID LastOwnerID
        {
            get { return _lastOwnerID; }
            set { _lastOwnerID = value; }
        }

        public uint BaseMask
        {
            get { return _baseMask; }
            set { _baseMask = value; }
        }

        public uint OwnerMask
        {
            get { return _ownerMask; }
            set { _ownerMask = value; }
        }

        public uint GroupMask
        {
            get { return _groupMask; }
            set { _groupMask = value; }
        }

        public uint EveryoneMask
        {
            get { return _everyoneMask; }
            set { _everyoneMask = value; }
        }

        public uint NextOwnerMask
        {
            get { return _nextOwnerMask; }
            set { _nextOwnerMask = value; }
        }

        public PrimFlags Flags
        {
            get { return _flags; }
            set { _flags = value; }
        }

        [XmlIgnore]
        public UUID SitTargetAvatar
        {
            get { return m_sitTargetAvatar; }
            set { m_sitTargetAvatar = value; }
        }

        [XmlIgnore]
        public virtual UUID RegionID
        {
            get
            {
                if (ParentGroup != null && ParentGroup.Scene != null)
                    return ParentGroup.Scene.RegionInfo.RegionID;
                else
                    return UUID.Zero;
            }
            set {} // read only
        }

        private UUID _parentUUID = UUID.Zero;
        [XmlIgnore]
        public UUID ParentUUID
        {
            get
            {
                if (ParentGroup != null)
                {
                    _parentUUID = ParentGroup.UUID;
                }
                return _parentUUID;
            }
            set { _parentUUID = value; }
        }

        [XmlIgnore]
        public string SitAnimation
        {
            get { return m_sitAnimation; }
            set { m_sitAnimation = value; }
        }

        public UUID CollisionSound
        {
            get { return m_collisionSound; }
            set
            {
                m_collisionSound = value;
                aggregateScriptEvents();
            }
        }

        public float CollisionSoundVolume
        {
            get { return m_collisionSoundVolume; }
            set { m_collisionSoundVolume = value; }
        }

        #endregion Public Properties with only Get

        

        #region Private Methods

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

        /// <summary>
        /// Clear all pending updates of parts to clients
        /// </summary>
        private void ClearUpdateSchedule()
        {
            m_updateFlag = 0;
        }

        private void SendObjectPropertiesToClient(UUID AgentID)
        {
            ScenePresence[] avatars = m_parentGroup.Scene.GetScenePresences();
            for (int i = 0; i < avatars.Length; i++)
            {
                // Ugly reference :(
                if (avatars[i].UUID == AgentID)
                {
                    m_parentGroup.GetProperties(avatars[i].ControllingClient);
                }
            }
        }

        // TODO: unused:
        // private void handleTimerAccounting(uint localID, double interval)
        // {
        //     if (localID == LocalId)
        //     {
        //         float sec = (float)interval;
        //         if (m_parentGroup != null)
        //         {
        //             if (sec == 0)
        //             {
        //                 if (m_parentGroup.scriptScore + 0.001f >= float.MaxValue - 0.001)
        //                     m_parentGroup.scriptScore = 0;
        //
        //                 m_parentGroup.scriptScore += 0.001f;
        //                 return;
        //             }
        //
        //             if (m_parentGroup.scriptScore + (0.001f / sec) >= float.MaxValue - (0.001f / sec))
        //                 m_parentGroup.scriptScore = 0;
        //             m_parentGroup.scriptScore += (0.001f / sec);
        //         }
        //     }
        // }

        #endregion Private Methods

        #region Public Methods

        public void ResetExpire()
        {
            Expires = DateTime.Now + new TimeSpan(600000000);
        }

        public void AddFlag(PrimFlags flag)
        {
            // PrimFlags prevflag = Flags;
            if ((ObjectFlags & (uint) flag) == 0)
            {
                //m_log.Debug("Adding flag: " + ((PrimFlags) flag).ToString());
                _flags |= flag;

                if (flag == PrimFlags.TemporaryOnRez)
                    ResetExpire();
            }
            // m_log.Debug("Aprev: " + prevflag.ToString() + " curr: " + Flags.ToString());
        }

        /// <summary>
        /// Tell all scene presences that they should send updates for this part to their clients
        /// </summary>
        public void AddFullUpdateToAllAvatars()
        {
            ScenePresence[] avatars = m_parentGroup.Scene.GetScenePresences();
            for (int i = 0; i < avatars.Length; i++)
            {
                avatars[i].SceneViewer.QueuePartForUpdate(this);
            }
        }

        public void AddFullUpdateToAvatar(ScenePresence presence)
        {
            presence.SceneViewer.QueuePartForUpdate(this);
        }

        public void AddNewParticleSystem(Primitive.ParticleSystem pSystem)
        {
            m_particleSystem = pSystem.GetBytes();
        }

        public void RemoveParticleSystem()
        {
            m_particleSystem = new byte[0];
        }

        /// Terse updates
        public void AddTerseUpdateToAllAvatars()
        {
            ScenePresence[] avatars = m_parentGroup.Scene.GetScenePresences();
            for (int i = 0; i < avatars.Length; i++)
            {
                avatars[i].SceneViewer.QueuePartForUpdate(this);
            }
        }

        public void AddTerseUpdateToAvatar(ScenePresence presence)
        {
            presence.SceneViewer.QueuePartForUpdate(this);
        }

        public void AddTextureAnimation(Primitive.TextureAnimation pTexAnim)
        {
            byte[] data = new byte[16];
            int pos = 0;

            // The flags don't like conversion from uint to byte, so we have to do
            // it the crappy way.  See the above function :(

            data[pos] = ConvertScriptUintToByte((uint)pTexAnim.Flags); pos++;
            data[pos] = (byte)pTexAnim.Face; pos++;
            data[pos] = (byte)pTexAnim.SizeX; pos++;
            data[pos] = (byte)pTexAnim.SizeY; pos++;

            Utils.FloatToBytes(pTexAnim.Start).CopyTo(data, pos);
            Utils.FloatToBytes(pTexAnim.Length).CopyTo(data, pos + 4);
            Utils.FloatToBytes(pTexAnim.Rate).CopyTo(data, pos + 8);

            m_TextureAnimation = data;
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

        /// <summary>
        /// hook to the physics scene to apply impulse
        /// This is sent up to the group, which then finds the root prim
        /// and applies the force on the root prim of the group
        /// </summary>
        /// <param name="impulsei">Vector force</param>
        /// <param name="localGlobalTF">true for the local frame, false for the global frame</param>
        public void ApplyImpulse(Vector3 impulsei, bool localGlobalTF)
        {
            PhysicsVector impulse = new PhysicsVector(impulsei.X, impulsei.Y, impulsei.Z);

            if (localGlobalTF)
            {
                Quaternion grot = GetWorldRotation();
                Quaternion AXgrot = grot;
                Vector3 AXimpulsei = impulsei;
                Vector3 newimpulse = AXimpulsei * AXgrot;
                impulse = new PhysicsVector(newimpulse.X, newimpulse.Y, newimpulse.Z);
            }

            if (m_parentGroup != null)
            {
                m_parentGroup.applyImpulse(impulse);
            }
        }

        /// <summary>
        /// hook to the physics scene to apply angular impulse
        /// This is sent up to the group, which then finds the root prim
        /// and applies the force on the root prim of the group
        /// </summary>
        /// <param name="impulsei">Vector force</param>
        /// <param name="localGlobalTF">true for the local frame, false for the global frame</param>
        public void ApplyAngularImpulse(Vector3 impulsei, bool localGlobalTF)
        {
            PhysicsVector impulse = new PhysicsVector(impulsei.X, impulsei.Y, impulsei.Z);

            if (localGlobalTF)
            {
                Quaternion grot = GetWorldRotation();
                Quaternion AXgrot = grot;
                Vector3 AXimpulsei = impulsei;
                Vector3 newimpulse = AXimpulsei * AXgrot;
                impulse = new PhysicsVector(newimpulse.X, newimpulse.Y, newimpulse.Z);
            }

            if (m_parentGroup != null)
            {
                m_parentGroup.applyAngularImpulse(impulse);
            }
        }

        /// <summary>
        /// hook to the physics scene to apply angular impulse
        /// This is sent up to the group, which then finds the root prim
        /// and applies the force on the root prim of the group
        /// </summary>
        /// <param name="impulsei">Vector force</param>
        /// <param name="localGlobalTF">true for the local frame, false for the global frame</param>
        public void SetAngularImpulse(Vector3 impulsei, bool localGlobalTF)
        {
            PhysicsVector impulse = new PhysicsVector(impulsei.X, impulsei.Y, impulsei.Z);

            if (localGlobalTF)
            {
                Quaternion grot = GetWorldRotation();
                Quaternion AXgrot = grot;
                Vector3 AXimpulsei = impulsei;
                Vector3 newimpulse = AXimpulsei * AXgrot;
                impulse = new PhysicsVector(newimpulse.X, newimpulse.Y, newimpulse.Z);
            }

            if (m_parentGroup != null)
            {
                m_parentGroup.setAngularImpulse(impulse);
            }
        }

        public Vector3 GetTorque()
        {
            if (m_parentGroup != null)
            {
                m_parentGroup.GetTorque();
            }
            return Vector3.Zero;
        }

        /// <summary>
        /// Apply physics to this part.
        /// </summary>
        /// <param name="rootObjectFlags"></param>
        /// <param name="m_physicalPrim"></param>
        public void ApplyPhysics(uint rootObjectFlags, bool VolumeDetectActive, bool m_physicalPrim)
        {
            bool isPhysical = (((rootObjectFlags & (uint) PrimFlags.Physics) != 0) && m_physicalPrim);
            bool isPhantom = ((rootObjectFlags & (uint) PrimFlags.Phantom) != 0);

            if (IsJoint())
            {
                DoPhysicsPropertyUpdate(isPhysical, true);
            }
            else
            {
                // Special case for VolumeDetection: If VolumeDetection is set, the phantom flag is locally ignored
                if (VolumeDetectActive)
                    isPhantom = false;

                // Added clarification..   since A rigid body is an object that you can kick around, etc.
                bool RigidBody = isPhysical && !isPhantom;

                // The only time the physics scene shouldn't know about the prim is if it's phantom or an attachment, which is phantom by definition
                // or flexible
                if (!isPhantom && !IsAttachment && !(Shape.PathCurve == (byte) Extrusion.Flexible))
                {
                    PhysActor = m_parentGroup.Scene.PhysicsScene.AddPrimShape(
                        Name,
                        Shape,
                        new PhysicsVector(AbsolutePosition.X, AbsolutePosition.Y, AbsolutePosition.Z),
                        new PhysicsVector(Scale.X, Scale.Y, Scale.Z),
                        RotationOffset,
                        RigidBody);

                    // Basic Physics returns null..  joy joy joy.
                    if (PhysActor != null)
                    {
                        PhysActor.SOPName = this.Name; // save object name and desc into the PhysActor so ODE internals know the joint/body info
                        PhysActor.SOPDescription = this.Description;
                        PhysActor.LocalID = LocalId;
                        DoPhysicsPropertyUpdate(RigidBody, true);
                        PhysActor.SetVolumeDetect(VolumeDetectActive ? 1 : 0);
                    }
                }
            }
        }

        public void ClearUndoState()
        {
            lock (m_undo)
            {
                m_undo.Clear();
            }
            StoreUndoState();
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

        /// <summary>
        /// Duplicates this part.
        /// </summary>
        /// <returns></returns>
        public SceneObjectPart Copy(uint localID, UUID AgentID, UUID GroupID, int linkNum, bool userExposed)
        {
            SceneObjectPart dupe = (SceneObjectPart)MemberwiseClone();
            dupe.m_shape = m_shape.Copy();
            dupe.m_regionHandle = m_regionHandle;
            if (userExposed)
                dupe.UUID = UUID.Random();

            //memberwiseclone means it also clones the physics actor reference
            // This will make physical prim 'bounce' if not set to null.
            if (!userExposed)
                dupe.PhysActor = null;

            dupe._ownerID = AgentID;
            dupe._groupID = GroupID;
            dupe.GroupPosition = GroupPosition;
            dupe.OffsetPosition = OffsetPosition;
            dupe.RotationOffset = RotationOffset;
            dupe.Velocity = new Vector3(0, 0, 0);
            dupe.Acceleration = new Vector3(0, 0, 0);
            dupe.AngularVelocity = new Vector3(0, 0, 0);
            dupe.ObjectFlags = ObjectFlags;

            dupe._ownershipCost = _ownershipCost;
            dupe._objectSaleType = _objectSaleType;
            dupe._salePrice = _salePrice;
            dupe._category = _category;
            dupe.m_rezzed = m_rezzed;

            dupe.m_inventory = new SceneObjectPartInventory(dupe);
            dupe.m_inventory.Items = (TaskInventoryDictionary)m_inventory.Items.Clone();

            if (userExposed)
            {
                dupe.ResetIDs(linkNum);
                dupe.m_inventory.HasInventoryChanged = true;
            }
            else
            {
                dupe.m_inventory.HasInventoryChanged = m_inventory.HasInventoryChanged;
            }

            // Move afterwards ResetIDs as it clears the localID
            dupe.LocalId = localID;
            // This may be wrong...    it might have to be applied in SceneObjectGroup to the object that's being duplicated.
            dupe._lastOwnerID = OwnerID;

            byte[] extraP = new byte[Shape.ExtraParams.Length];
            Array.Copy(Shape.ExtraParams, extraP, extraP.Length);
            dupe.Shape.ExtraParams = extraP;

            if (userExposed)
            {
                if (dupe.m_shape.SculptEntry && dupe.m_shape.SculptTexture != UUID.Zero)
                {
                    m_parentGroup.Scene.AssetService.Get(dupe.m_shape.SculptTexture.ToString(), dupe, AssetReceived); 
                }
                
                bool UsePhysics = ((dupe.ObjectFlags & (uint)PrimFlags.Physics) != 0);
                dupe.DoPhysicsPropertyUpdate(UsePhysics, true);
            }
            
            return dupe;
        }

        protected void AssetReceived(string id, Object sender, AssetBase asset)
        {
            if (asset != null)
            {
                SceneObjectPart sop = (SceneObjectPart)sender;
                if (sop != null)
                    sop.SculptTextureCallback(asset.FullID, asset);
            }
        }

        public static SceneObjectPart Create()
        {
            SceneObjectPart part = new SceneObjectPart();
            part.UUID = UUID.Random();

            PrimitiveBaseShape shape = PrimitiveBaseShape.Create();
            part.Shape = shape;

            part.Name = "Primitive";
            part._ownerID = UUID.Random();

            return part;
        }

        public void DoPhysicsPropertyUpdate(bool UsePhysics, bool isNew)
        {
            if (IsJoint())
            {
                if (UsePhysics)
                {
                    // by turning a joint proxy object physical, we cause creation of a joint in the ODE scene.
                    // note that, as a special case, joints have no bodies or geoms in the physics scene, even though they are physical.

                    PhysicsJointType jointType;
                    if (IsHingeJoint())
                    {
                        jointType = PhysicsJointType.Hinge;
                    }
                    else if (IsBallJoint())
                    {
                        jointType = PhysicsJointType.Ball;
                    }
                    else
                    {
                        jointType = PhysicsJointType.Ball;
                    }

                    List<string> bodyNames = new List<string>();
                    string RawParams = Description;
                    string[] jointParams = RawParams.Split(" ".ToCharArray(), System.StringSplitOptions.RemoveEmptyEntries);
                    string trackedBodyName = null;
                    if (jointParams.Length >= 2)
                    {
                        for (int iBodyName = 0; iBodyName < 2; iBodyName++)
                        {
                            string bodyName = jointParams[iBodyName];
                            bodyNames.Add(bodyName);
                            if (bodyName != "NULL")
                            {
                                if (trackedBodyName == null)
                                {
                                    trackedBodyName = bodyName;
                                }
                            }
                        }
                    }

                    SceneObjectPart trackedBody = m_parentGroup.Scene.GetSceneObjectPart(trackedBodyName); // FIXME: causes a sequential lookup
                    Quaternion localRotation = Quaternion.Identity;
                    if (trackedBody != null)
                    {
                        localRotation = Quaternion.Inverse(trackedBody.RotationOffset) * this.RotationOffset;
                    }
                    else
                    {
                        // error, output it below
                    }

                    PhysicsJoint joint;

                    joint = m_parentGroup.Scene.PhysicsScene.RequestJointCreation(Name, jointType,
                        new PhysicsVector(AbsolutePosition.X, AbsolutePosition.Y, AbsolutePosition.Z),
                        this.RotationOffset,
                        Description,
                        bodyNames,
                        trackedBodyName,
                        localRotation);

                    if (trackedBody == null)
                    {
                        ParentGroup.Scene.jointErrorMessage(joint, "warning: tracked body name not found! joint location will not be updated properly. joint: " + Name);
                    }

                }
                else
                {
                    if (isNew)
                    {
                        // if the joint proxy is new, and it is not physical, do nothing. There is no joint in ODE to
                        // delete, and if we try to delete it, due to asynchronous processing, the deletion request
                        // will get processed later at an indeterminate time, which could cancel a later-arriving
                        // joint creation request.
                    }
                    else
                    {
                        // here we turn off the joint object, so remove the joint from the physics scene
                        m_parentGroup.Scene.PhysicsScene.RequestJointDeletion(Name); // FIXME: what if the name changed?

                        // make sure client isn't interpolating the joint proxy object
                        Velocity = new Vector3(0, 0, 0);
                        RotationalVelocity = new Vector3(0, 0, 0);
                        Acceleration = new Vector3(0, 0, 0);
                    }
                }
            }
            else
            {
                if (PhysActor != null)
                {
                    if (UsePhysics != PhysActor.IsPhysical || isNew)
                    {
                        if (PhysActor.IsPhysical) // implies UsePhysics==false for this block
                        {
                            if (!isNew)
                                ParentGroup.Scene.RemovePhysicalPrim(1);

                            PhysActor.OnRequestTerseUpdate -= PhysicsRequestingTerseUpdate;
                            PhysActor.OnOutOfBounds -= PhysicsOutOfBounds;
                            PhysActor.delink();

                            if (ParentGroup.Scene.PhysicsScene.SupportsNINJAJoints && (!isNew))
                            {
                                // destroy all joints connected to this now deactivated body
                                m_parentGroup.Scene.PhysicsScene.RemoveAllJointsConnectedToActorThreadLocked(PhysActor);
                            }

                            // stop client-side interpolation of all joint proxy objects that have just been deleted
                            // this is done because RemoveAllJointsConnectedToActor invokes the OnJointDeactivated callback,
                            // which stops client-side interpolation of deactivated joint proxy objects.
                        }

                        if (!UsePhysics && !isNew)
                        {
                            // reset velocity to 0 on physics switch-off. Without that, the client thinks the
                            // prim still has velocity and continues to interpolate its position along the old
                            // velocity-vector.
                            Velocity = new Vector3(0, 0, 0);
                            Acceleration = new Vector3(0, 0, 0);
                            AngularVelocity = new Vector3(0, 0, 0);
                            //RotationalVelocity = new Vector3(0, 0, 0);
                        }

                        PhysActor.IsPhysical = UsePhysics;


                        // If we're not what we're supposed to be in the physics scene, recreate ourselves.
                        //m_parentGroup.Scene.PhysicsScene.RemovePrim(PhysActor);
                        /// that's not wholesome.  Had to make Scene public
                        //PhysActor = null;

                        if ((ObjectFlags & (uint)PrimFlags.Phantom) == 0)
                        {
                            if (UsePhysics)
                            {
                                ParentGroup.Scene.AddPhysicalPrim(1);

                                PhysActor.OnRequestTerseUpdate += PhysicsRequestingTerseUpdate;
                                PhysActor.OnOutOfBounds += PhysicsOutOfBounds;
                                if (_parentID != 0 && _parentID != LocalId)
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
        }

        /// <summary>
        /// Restore this part from the serialized xml representation.
        /// </summary>
        /// <param name="xmlReader"></param>
        /// <returns></returns>
        public static SceneObjectPart FromXml(XmlReader xmlReader)
        {
            return FromXml(UUID.Zero, xmlReader);
        }

        /// <summary>
        /// Restore this part from the serialized xml representation.
        /// </summary>
        /// <param name="fromUserInventoryItemId">The inventory id from which this part came, if applicable</param>
        /// <param name="xmlReader"></param>
        /// <returns></returns>
        public static SceneObjectPart FromXml(UUID fromUserInventoryItemId, XmlReader xmlReader)
        {
            SceneObjectPart part = (SceneObjectPart)serializer.Deserialize(xmlReader);
            part.m_fromUserInventoryItemID = fromUserInventoryItemId;

            // for tempOnRez objects, we have to fix the Expire date.
            if ((part.Flags & PrimFlags.TemporaryOnRez) != 0) part.ResetExpire();

            return part;
        }

        public UUID GetAvatarOnSitTarget()
        {
            return m_sitTargetAvatar;
        }

        public bool GetDieAtEdge()
        {
            if (m_parentGroup == null)
                return false;
            if (m_parentGroup.IsDeleted)
                return false;

            return m_parentGroup.RootPart.DIE_AT_EDGE;
        }

        public double GetDistanceTo(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public uint GetEffectiveObjectFlags()
        {
            PrimFlags f = _flags;
            if (m_parentGroup == null || m_parentGroup.RootPart == this)
                f &= ~(PrimFlags.Touch | PrimFlags.Money);

            return (uint)_flags | (uint)LocalFlags;
        }

        public Vector3 GetGeometricCenter()
        {
            if (PhysActor != null)
            {
                return new Vector3(PhysActor.CenterOfMass.X, PhysActor.CenterOfMass.Y, PhysActor.CenterOfMass.Z);
            }
            else
            {
                return new Vector3(0, 0, 0);
            }
        }

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

        public PhysicsVector GetForce()
        {
            if (PhysActor != null)
                return PhysActor.Force;
            else
                return new PhysicsVector();
        }

        public void GetProperties(IClientAPI client)
        {
            client.SendObjectPropertiesReply(
                m_fromUserInventoryItemID, (ulong)_creationDate, _creatorID, UUID.Zero, UUID.Zero,
                _groupID, (short)InventorySerial, _lastOwnerID, UUID, _ownerID,
                ParentGroup.RootPart.TouchName, new byte[0], ParentGroup.RootPart.SitName, Name, Description,
                ParentGroup.RootPart._ownerMask, ParentGroup.RootPart._nextOwnerMask, ParentGroup.RootPart._groupMask, ParentGroup.RootPart._everyoneMask,
                ParentGroup.RootPart._baseMask,
                ParentGroup.RootPart.ObjectSaleType,
                ParentGroup.RootPart.SalePrice);
        }

        public UUID GetRootPartUUID()
        {
            if (m_parentGroup != null)
            {
                return m_parentGroup.UUID;
            }
            return UUID.Zero;
        }

        /// <summary>
        /// Method for a prim to get it's world position from the group.
        /// Remember, the Group Position simply gives the position of the group itself
        /// </summary>
        /// <returns>A Linked Child Prim objects position in world</returns>
        public Vector3 GetWorldPosition()
        {
            Quaternion parentRot = ParentGroup.RootPart.RotationOffset;

            Vector3 axPos = OffsetPosition;

            axPos *= parentRot;
            Vector3 translationOffsetPosition = axPos;
            return GroupPosition + translationOffsetPosition;
        }

        /// <summary>
        /// Gets the rotation of this prim offset by the group rotation
        /// </summary>
        /// <returns></returns>
        public Quaternion GetWorldRotation()
        {
            Quaternion newRot;

            if (this.LinkNum == 0)
            {
                newRot = RotationOffset;
            }
            else
            {
                Quaternion parentRot = ParentGroup.RootPart.RotationOffset;
                Quaternion oldRot = RotationOffset;
                newRot = parentRot * oldRot;
            }

            return newRot;
        }

        public void MoveToTarget(Vector3 target, float tau)
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

        /// <summary>
        /// Uses a PID to attempt to clamp the object on the Z axis at the given height over tau seconds.
        /// </summary>
        /// <param name="height">Height to hover.  Height of zero disables hover.</param>
        /// <param name="hoverType">Determines what the height is relative to </param>
        /// <param name="tau">Number of seconds over which to reach target</param>
        public void SetHoverHeight(float height, PIDHoverType hoverType, float tau)
        {
            m_parentGroup.SetHoverHeight(height, hoverType, tau);
        }

        public void StopHover()
        {
            m_parentGroup.SetHoverHeight(0f, PIDHoverType.Ground, 0f);
        }

        public virtual void OnGrab(Vector3 offsetPos, IClientAPI remoteClient)
        {
        }

        public void PhysicsCollision(EventArgs e)
        {
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

            //add the items that started colliding this time to the last colliders list.
            foreach (uint localID in startedColliders)
            {
                m_lastColliders.Add(localID);
            }
            // remove things that ended colliding from the last colliders list
            foreach (uint localID in endedColliders)
            {
                m_lastColliders.Remove(localID);
            }
            if (m_parentGroup == null)
                return;
            if (m_parentGroup.IsDeleted)
                return;

            // play the sound.
            if (startedColliders.Count > 0 && CollisionSound != UUID.Zero && CollisionSoundVolume > 0.0f)
            {
                SendSound(CollisionSound.ToString(), CollisionSoundVolume, true, (byte)0);
            }

            if ((m_parentGroup.RootPart.ScriptEvents & scriptEvents.collision_start) != 0)
            {
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
                            detobj.ownerUUID = obj._ownerID;
                            detobj.posVector = obj.AbsolutePosition;
                            detobj.rotQuat = obj.GetWorldRotation();
                            detobj.velVector = obj.Velocity;
                            detobj.colliderType = 0;
                            detobj.groupUUID = obj._groupID;
                            colliding.Add(detobj);
                        }
                        else
                        {
                            ScenePresence[] avlist = m_parentGroup.Scene.GetScenePresences();

                            for (int i = 0; i < avlist.Length; i++)
                            {
                                ScenePresence av = avlist[i];

                                if (av.LocalId == localId)
                                {
                                    DetectedObject detobj = new DetectedObject();
                                    detobj.keyUUID = av.UUID;
                                    detobj.nameStr = av.ControllingClient.Name;
                                    detobj.ownerUUID = av.UUID;
                                    detobj.posVector = av.AbsolutePosition;
                                    detobj.rotQuat = av.Rotation;
                                    detobj.velVector = av.Velocity;
                                    detobj.colliderType = 0;
                                    detobj.groupUUID = av.ControllingClient.ActiveGroupId;
                                    colliding.Add(detobj);
                                }
                            }
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
            }
            
            if ((m_parentGroup.RootPart.ScriptEvents & scriptEvents.collision) != 0)
            {
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
                            detobj.ownerUUID = obj._ownerID;
                            detobj.posVector = obj.AbsolutePosition;
                            detobj.rotQuat = obj.GetWorldRotation();
                            detobj.velVector = obj.Velocity;
                            detobj.colliderType = 0;
                            detobj.groupUUID = obj._groupID;
                            colliding.Add(detobj);
                        }
                        else
                        {
                            ScenePresence[] avlist = m_parentGroup.Scene.GetScenePresences();
                            
                            for (int i = 0; i < avlist.Length; i++)
                            {
                                ScenePresence av = avlist[i];

                                if (av.LocalId == localId)
                                {
                                    DetectedObject detobj = new DetectedObject();
                                    detobj.keyUUID = av.UUID;
                                    detobj.nameStr = av.Name;
                                    detobj.ownerUUID = av.UUID;
                                    detobj.posVector = av.AbsolutePosition;
                                    detobj.rotQuat = av.Rotation;
                                    detobj.velVector = av.Velocity;
                                    detobj.colliderType = 0;
                                    detobj.groupUUID = av.ControllingClient.ActiveGroupId;
                                    colliding.Add(detobj);
                                }
                            }
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
                        
                        m_parentGroup.Scene.EventManager.TriggerScriptColliding(LocalId, CollidingMessage);
                    }
                }
            }
            
            if ((m_parentGroup.RootPart.ScriptEvents & scriptEvents.collision_end) != 0)
            {
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
                            detobj.ownerUUID = obj._ownerID;
                            detobj.posVector = obj.AbsolutePosition;
                            detobj.rotQuat = obj.GetWorldRotation();
                            detobj.velVector = obj.Velocity;
                            detobj.colliderType = 0;
                            detobj.groupUUID = obj._groupID;
                            colliding.Add(detobj);
                        }
                        else
                        {
                            ScenePresence[] avlist = m_parentGroup.Scene.GetScenePresences();

                            for (int i = 0; i < avlist.Length; i++)
                            {
                                ScenePresence av = avlist[i];

                                if (av.LocalId == localId)
                                {
                                    DetectedObject detobj = new DetectedObject();
                                    detobj.keyUUID = av.UUID;
                                    detobj.nameStr = av.Name;
                                    detobj.ownerUUID = av.UUID;
                                    detobj.posVector = av.AbsolutePosition;
                                    detobj.rotQuat = av.Rotation;
                                    detobj.velVector = av.Velocity;
                                    detobj.colliderType = 0;
                                    detobj.groupUUID = av.ControllingClient.ActiveGroupId;
                                    colliding.Add(detobj);
                                }
                            }
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
                        
                        m_parentGroup.Scene.EventManager.TriggerScriptCollidingEnd(LocalId, EndCollidingMessage);
                    }
                }
            }
        }

        public void PhysicsOutOfBounds(PhysicsVector pos)
        {
            m_log.Error("[PHYSICS]: Physical Object went out of bounds.");
            
            RemFlag(PrimFlags.Physics);
            DoPhysicsPropertyUpdate(false, true);
            //m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
        }

        public void PhysicsRequestingTerseUpdate()
        {
            if (PhysActor != null)
            {
                Vector3 newpos = new Vector3(PhysActor.Position.GetBytes(), 0);
                
                if (m_parentGroup.Scene.TestBorderCross(newpos, Cardinals.N) | m_parentGroup.Scene.TestBorderCross(newpos, Cardinals.S) | m_parentGroup.Scene.TestBorderCross(newpos, Cardinals.E) | m_parentGroup.Scene.TestBorderCross(newpos, Cardinals.W))
                {
                    m_parentGroup.AbsolutePosition = newpos;
                    return;
                }
            }
            ScheduleTerseUpdate();

            //SendTerseUpdateToAllClients();
        }

        public void PreloadSound(string sound)
        {
            // UUID ownerID = OwnerID;
            UUID objectID = UUID;
            UUID soundID = UUID.Zero;

            if (!UUID.TryParse(sound, out soundID))
            {
                //Trys to fetch sound id from prim's inventory.
                //Prim's inventory doesn't support non script items yet
                
                lock (TaskInventory)
                {
                    foreach (KeyValuePair<UUID, TaskInventoryItem> item in TaskInventory)
                    {
                        if (item.Value.Name == sound)
                        {
                            soundID = item.Value.ItemID;
                            break;
                        }
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

        public void RemFlag(PrimFlags flag)
        {
            // PrimFlags prevflag = Flags;
            if ((ObjectFlags & (uint) flag) != 0)
            {
                //m_log.Debug("Removing flag: " + ((PrimFlags)flag).ToString());
                _flags &= ~flag;
            }
            //m_log.Debug("prev: " + prevflag.ToString() + " curr: " + Flags.ToString());
            //ScheduleFullUpdate();
        }

        public void RemoveScriptEvents(UUID scriptid)
        {
            lock (m_scriptEvents)
            {
                if (m_scriptEvents.ContainsKey(scriptid))
                {
                    scriptEvents oldparts = scriptEvents.None;
                    oldparts = (scriptEvents) m_scriptEvents[scriptid];

                    // remove values from aggregated script events
                    AggregateScriptEvents &= ~oldparts;
                    m_scriptEvents.Remove(scriptid);
                    aggregateScriptEvents();
                }
            }
        }

        /// <summary>
        /// Reset UUIDs for this part.  This involves generate this part's own UUID and
        /// generating new UUIDs for all the items in the inventory.
        /// </summary>
        /// <param name="linkNum">Link number for the part</param>
        public void ResetIDs(int linkNum)
        {
            UUID = UUID.Random();
            LinkNum = linkNum;
            LocalId = 0;
            Inventory.ResetInventoryIDs();
        }

        /// <summary>
        /// Resize this part.
        /// </summary>
        /// <param name="scale"></param>
        public void Resize(Vector3 scale)
        {
            StoreUndoState();
            m_shape.Scale = scale;

            ParentGroup.HasGroupChanged = true;
            ScheduleFullUpdate();
        }

        /// <summary>
        /// Schedules this prim for a full update
        /// </summary>
        public void ScheduleFullUpdate()
        {
            if (m_parentGroup != null)
            {
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

        public void ScriptSetPhantomStatus(bool Phantom)
        {
            if (m_parentGroup != null)
            {
                m_parentGroup.ScriptSetPhantomStatus(Phantom);
            }
        }

        public void ScriptSetTemporaryStatus(bool Temporary)
        {
            if (m_parentGroup != null)
            {
                m_parentGroup.ScriptSetTemporaryStatus(Temporary);
            }
        }

        public void ScriptSetPhysicsStatus(bool UsePhysics)
        {
            if (m_parentGroup == null)
                DoPhysicsPropertyUpdate(UsePhysics, false);
            else
                m_parentGroup.ScriptSetPhysicsStatus(UsePhysics);
        }

        public void ScriptSetVolumeDetect(bool SetVD)
        {

            if (m_parentGroup != null)
            {
                m_parentGroup.ScriptSetVolumeDetect(SetVD);
            }
        }


        public void SculptTextureCallback(UUID textureID, AssetBase texture)
        {
            if (m_shape.SculptEntry)
            {
                // commented out for sculpt map caching test - null could mean a cached sculpt map has been found
                //if (texture != null)
                {
                    if (texture != null)
                        m_shape.SculptData = texture.Data;

                    if (PhysActor != null)
                    {
                        // Tricks physics engine into thinking we've changed the part shape.
                        PrimitiveBaseShape m_newshape = m_shape.Copy();
                        PhysActor.Shape = m_newshape;
                        m_shape = m_newshape;

                        m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
                    }
                }
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
        ///
        /// </summary>
        public void SendFullUpdateToAllClients()
        {
            ScenePresence[] avatars = m_parentGroup.Scene.GetScenePresences();
            for (int i = 0; i < avatars.Length; i++)
            {
                // Ugly reference :(
                m_parentGroup.SendPartFullUpdate(avatars[i].ControllingClient, this,
                                                 avatars[i].GenerateClientFlags(UUID));
            }
        }

        public void SendFullUpdateToAllClientsExcept(UUID agentID)
        {
            ScenePresence[] avatars = m_parentGroup.Scene.GetScenePresences();
            for (int i = 0; i < avatars.Length; i++)
            {
                // Ugly reference :(
                if (avatars[i].UUID != agentID)
                {
                    m_parentGroup.SendPartFullUpdate(avatars[i].ControllingClient, this,
                                                    avatars[i].GenerateClientFlags(UUID));
                }
            }
        }

        /// <summary>
        /// Sends a full update to the client
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="clientFlags"></param>
        public void SendFullUpdateToClient(IClientAPI remoteClient, uint clientflags)
        {
            Vector3 lPos;
            lPos = OffsetPosition;
            SendFullUpdateToClient(remoteClient, lPos, clientflags);
        }

        /// <summary>
        /// Sends a full update to the client
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="lPos"></param>
        /// <param name="clientFlags"></param>
        public void SendFullUpdateToClient(IClientAPI remoteClient, Vector3 lPos, uint clientFlags)
        {
            // Suppress full updates during attachment editing
            //
            if (ParentGroup.IsSelected && IsAttachment)
                return;
            
            if (ParentGroup.IsDeleted)
                return;

            clientFlags &= ~(uint) PrimFlags.CreateSelected;

            if (remoteClient.AgentId == _ownerID)
            {
                if ((uint) (_flags & PrimFlags.CreateSelected) != 0)
                {
                    clientFlags |= (uint) PrimFlags.CreateSelected;
                    _flags &= ~PrimFlags.CreateSelected;
                }
            }
            //bool isattachment = IsAttachment;
            //if (LocalId != ParentGroup.RootPart.LocalId)
                //isattachment = ParentGroup.RootPart.IsAttachment;

            byte[] color = new byte[] {m_color.R, m_color.G, m_color.B, m_color.A};
            remoteClient.SendPrimitiveToClient(new SendPrimitiveData(m_regionHandle, (ushort)(m_parentGroup.GetTimeDilation() * (float)ushort.MaxValue), LocalId, m_shape,
                                               lPos, Velocity, Acceleration, RotationOffset, RotationalVelocity, clientFlags, m_uuid, _ownerID,
                                               m_text, color, _parentID, m_particleSystem, m_clickAction, (byte)m_material, m_TextureAnimation, IsAttachment,
                                               AttachmentPoint,FromItemID, Sound, SoundGain, SoundFlags, SoundRadius, ParentGroup.GetUpdatePriority(remoteClient)));
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
                //if ((ObjectFlags & (uint)PrimFlags.Physics) != 0)
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

        /// <summary>
        /// Trigger or play an attached sound in this part's inventory.
        /// </summary>
        /// <param name="sound"></param>
        /// <param name="volume"></param>
        /// <param name="triggered"></param>
        /// <param name="flags"></param>
        public void SendSound(string sound, double volume, bool triggered, byte flags)
        {
            if (volume > 1)
                volume = 1;
            if (volume < 0)
                volume = 0;

            UUID ownerID = _ownerID;
            UUID objectID = UUID;
            UUID parentID = GetRootPartUUID();
            UUID soundID = UUID.Zero;
            Vector3 position = AbsolutePosition; // region local
            ulong regionHandle = m_parentGroup.Scene.RegionInfo.RegionHandle;

            if (!UUID.TryParse(sound, out soundID))
            {
                // search sound file from inventory
                lock (TaskInventory)
                {
                    foreach (KeyValuePair<UUID, TaskInventoryItem> item in TaskInventory)
                    {
                        if (item.Value.Name == sound && item.Value.Type == (int)AssetType.Sound)
                        {
                            soundID = item.Value.ItemID;
                            break;
                        }
                    }
                }
            }

            if (soundID == UUID.Zero)
                return;

            ISoundModule soundModule = m_parentGroup.Scene.RequestModuleInterface<ISoundModule>();
            if (soundModule != null)
            {
                if (triggered)
                    soundModule.TriggerSound(soundID, ownerID, objectID, parentID, volume, position, regionHandle);
                else
                    soundModule.PlayAttachedSound(soundID, ownerID, objectID, volume, position, flags);
            }
        }

        /// <summary>
        /// Send a terse update to all clients
        /// </summary>
        public void SendTerseUpdateToAllClients()
        {
            ScenePresence[] avatars = m_parentGroup.Scene.GetScenePresences();
            for (int i = 0; i < avatars.Length; i++)
            {
                SendTerseUpdateToClient(avatars[i].ControllingClient);
            }
        }

        public void SetAttachmentPoint(uint AttachmentPoint)
        {
            this.AttachmentPoint = AttachmentPoint;

            if (AttachmentPoint != 0)
            {
                IsAttachment = true;
            }
            else
            {
                IsAttachment = false;
            }

            // save the attachment point.
            //if (AttachmentPoint != 0)
            //{
                m_shape.State = (byte)AttachmentPoint;
            //}
        }

        public void SetAvatarOnSitTarget(UUID avatarID)
        {
            m_sitTargetAvatar = avatarID;
            if (ParentGroup != null)
                ParentGroup.TriggerScriptChangedEvent(Changed.LINK);
        }

        public void SetAxisRotation(int axis, int rotate)
        {
            if (m_parentGroup != null)
            {
                m_parentGroup.SetAxisRotation(axis, rotate);
            }
        }

        public void SetBuoyancy(float fvalue)
        {
            if (PhysActor != null)
            {
                PhysActor.Buoyancy = fvalue;
            }
        }

        public void SetDieAtEdge(bool p)
        {
            if (m_parentGroup == null)
                return;
            if (m_parentGroup.IsDeleted)
                return;

            m_parentGroup.RootPart.DIE_AT_EDGE = p;
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

        public void SetForce(PhysicsVector force)
        {
            if (PhysActor != null)
            {
                PhysActor.Force = force;
            }
        }

        public void SetVehicleType(int type)
        {
            if (PhysActor != null)
            {
                PhysActor.VehicleType = type;
            }
        }

        public void SetVehicleFloatParam(int param, float value)
        {
            if (PhysActor != null)
            {
                PhysActor.VehicleFloatParam(param, value);
            }
        }

        public void SetVehicleVectorParam(int param, PhysicsVector value)
        {
            if (PhysActor != null)
            {
                PhysActor.VehicleVectorParam(param, value);
            }
        }

        public void SetVehicleRotationParam(int param, Quaternion rotation)
        {
            if (PhysActor != null)
            {
                PhysActor.VehicleRotationParam(param, rotation);
            }
        }

        public void SetGroup(UUID groupID, IClientAPI client)
        {
            _groupID = groupID;
            if (client != null)
                GetProperties(client);
            m_updateFlag = 2;
        }

        /// <summary>
        ///
        /// </summary>
        public void SetParent(SceneObjectGroup parent)
        {
            m_parentGroup = parent;
        }

        // Use this for attachments!  LocalID should be avatar's localid
        public void SetParentLocalId(uint localID)
        {
            _parentID = localID;
        }

        public void SetPhysicsAxisRotation()
        {
            if (PhysActor != null)
            {
                PhysActor.LockAngularMotion(RotationAxis);
                m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
            }
        }

        public void SetScriptEvents(UUID scriptid, int events)
        {
            // scriptEvents oldparts;
            lock (m_scriptEvents)
            {
                if (m_scriptEvents.ContainsKey(scriptid))
                {
                    // oldparts = m_scriptEvents[scriptid];

                    // remove values from aggregated script events
                    if (m_scriptEvents[scriptid] == (scriptEvents) events)
                        return;
                    m_scriptEvents[scriptid] = (scriptEvents) events;
                }
                else
                {
                    m_scriptEvents.Add(scriptid, (scriptEvents) events);
                }
            }
            aggregateScriptEvents();
        }

        /// <summary>
        /// Set the text displayed for this part.
        /// </summary>
        /// <param name="text"></param>
        public void SetText(string text)
        {
            Text = text;

            ParentGroup.HasGroupChanged = true;
            ScheduleFullUpdate();
        }

        /// <summary>
        /// Set the text displayed for this part.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="color"></param>
        /// <param name="alpha"></param>
        public void SetText(string text, Vector3 color, double alpha)
        {
            Color = Color.FromArgb(0xff - (int) (alpha*0xff),
                                   (int) (color.X*0xff),
                                   (int) (color.Y*0xff),
                                   (int) (color.Z*0xff));
            SetText(text);
        }

        public void StopMoveToTarget()
        {
            m_parentGroup.stopMoveToTarget();

            m_parentGroup.ScheduleGroupForTerseUpdate();
            //m_parentGroup.ScheduleGroupForFullUpdate();
        }

        public void StoreUndoState()
        {
            if (!Undoing)
            {
                if (m_parentGroup != null)
                {
                    lock (m_undo)
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
        }

        public EntityIntersection TestIntersection(Ray iray, Quaternion parentrot)
        {
            // In this case we're using a sphere with a radius of the largest dimension of the prim
            // TODO: Change to take shape into account

            EntityIntersection result = new EntityIntersection();
            Vector3 vAbsolutePosition = AbsolutePosition;
            Vector3 vScale = Scale;
            Vector3 rOrigin = iray.Origin;
            Vector3 rDirection = iray.Direction;

            //rDirection = rDirection.Normalize();
            // Buidling the first part of the Quadratic equation
            Vector3 r2ndDirection = rDirection*rDirection;
            float itestPart1 = r2ndDirection.X + r2ndDirection.Y + r2ndDirection.Z;

            // Buidling the second part of the Quadratic equation
            Vector3 tmVal2 = rOrigin - vAbsolutePosition;
            Vector3 r2Direction = rDirection*2.0f;
            Vector3 tmVal3 = r2Direction*tmVal2;

            float itestPart2 = tmVal3.X + tmVal3.Y + tmVal3.Z;

            // Buidling the third part of the Quadratic equation
            Vector3 tmVal4 = rOrigin*rOrigin;
            Vector3 tmVal5 = vAbsolutePosition*vAbsolutePosition;

            Vector3 tmVal6 = vAbsolutePosition*rOrigin;

            // Set Radius to the largest dimension of the prim
            float radius = 0f;
            if (vScale.X > radius)
                radius = vScale.X;
            if (vScale.Y > radius)
                radius = vScale.Y;
            if (vScale.Z > radius)
                radius = vScale.Z;

            // the second part of this is the default prim size
            // once we factor in the aabb of the prim we're adding we can
            // change this to;
            // radius = (radius / 2) - 0.01f;
            //
            radius = (radius / 2) + (0.5f / 2) - 0.1f;

            //radius = radius;

            float itestPart3 = tmVal4.X + tmVal4.Y + tmVal4.Z + tmVal5.X + tmVal5.Y + tmVal5.Z -
                               (2.0f*(tmVal6.X + tmVal6.Y + tmVal6.Z + (radius*radius)));

            // Yuk Quadradrics..    Solve first
            float rootsqr = (itestPart2*itestPart2) - (4.0f*itestPart1*itestPart3);
            if (rootsqr < 0.0f)
            {
                // No intersection
                return result;
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
                    return result;
                }
            }

            // We got an intersection.  putting together an EntityIntersection object with the
            // intersection information
            Vector3 ipoint =
                new Vector3(iray.Origin.X + (iray.Direction.X*root), iray.Origin.Y + (iray.Direction.Y*root),
                            iray.Origin.Z + (iray.Direction.Z*root));

            result.HitTF = true;
            result.ipoint = ipoint;

            // Normal is calculated by the difference and then normalizing the result
            Vector3 normalpart = ipoint - vAbsolutePosition;
            result.normal = normalpart / normalpart.Length();

            // It's funny how the Vector3 object has a Distance function, but the Axiom.Math object doesn't.
            // I can write a function to do it..    but I like the fact that this one is Static.

            Vector3 distanceConvert1 = new Vector3(iray.Origin.X, iray.Origin.Y, iray.Origin.Z);
            Vector3 distanceConvert2 = new Vector3(ipoint.X, ipoint.Y, ipoint.Z);
            float distance = (float) Util.GetDistanceTo(distanceConvert1, distanceConvert2);

            result.distance = distance;

            return result;
        }

        public EntityIntersection TestIntersectionOBB(Ray iray, Quaternion parentrot, bool frontFacesOnly, bool faceCenters)
        {
            // In this case we're using a rectangular prism, which has 6 faces and therefore 6 planes
            // This breaks down into the ray---> plane equation.
            // TODO: Change to take shape into account
            Vector3[] vertexes = new Vector3[8];

            // float[] distance = new float[6];
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

            Vector3 pos = GetWorldPosition();
            Quaternion rot = GetWorldRotation();

            // Variables prefixed with AX are Axiom.Math copies of the LL variety.

            Quaternion AXrot = rot;
            AXrot.Normalize();

            Vector3 AXpos = pos;

            // tScale is the offset to derive the vertex based on the scale.
            // it's different for each vertex because we've got to rotate it
            // to get the world position of the vertex to produce the Oriented Bounding Box

            Vector3 tScale = Vector3.Zero;

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
            tScale = new Vector3(AXscale.X, -AXscale.Y, AXscale.Z);
            rScale = tScale * AXrot;
            vertexes[0] = (new Vector3((pos.X + rScale.X), (pos.Y + rScale.Y), (pos.Z + rScale.Z)));
               // vertexes[0].X = pos.X + vertexes[0].X;
            //vertexes[0].Y = pos.Y + vertexes[0].Y;
            //vertexes[0].Z = pos.Z + vertexes[0].Z;

            FaceA[0] = vertexes[0];
            FaceB[3] = vertexes[0];
            FaceA[4] = vertexes[0];

            tScale = AXscale;
            rScale = tScale * AXrot;
            vertexes[1] = (new Vector3((pos.X + rScale.X), (pos.Y + rScale.Y), (pos.Z + rScale.Z)));

               // vertexes[1].X = pos.X + vertexes[1].X;
               // vertexes[1].Y = pos.Y + vertexes[1].Y;
            //vertexes[1].Z = pos.Z + vertexes[1].Z;

            FaceB[0] = vertexes[1];
            FaceA[1] = vertexes[1];
            FaceC[4] = vertexes[1];

            tScale = new Vector3(AXscale.X, -AXscale.Y, -AXscale.Z);
            rScale = tScale * AXrot;

            vertexes[2] = (new Vector3((pos.X + rScale.X), (pos.Y + rScale.Y), (pos.Z + rScale.Z)));

            //vertexes[2].X = pos.X + vertexes[2].X;
            //vertexes[2].Y = pos.Y + vertexes[2].Y;
            //vertexes[2].Z = pos.Z + vertexes[2].Z;

            FaceC[0] = vertexes[2];
            FaceD[3] = vertexes[2];
            FaceC[5] = vertexes[2];

            tScale = new Vector3(AXscale.X, AXscale.Y, -AXscale.Z);
            rScale = tScale * AXrot;
            vertexes[3] = (new Vector3((pos.X + rScale.X), (pos.Y + rScale.Y), (pos.Z + rScale.Z)));

            //vertexes[3].X = pos.X + vertexes[3].X;
               // vertexes[3].Y = pos.Y + vertexes[3].Y;
               // vertexes[3].Z = pos.Z + vertexes[3].Z;

            FaceD[0] = vertexes[3];
            FaceC[1] = vertexes[3];
            FaceA[5] = vertexes[3];

            tScale = new Vector3(-AXscale.X, AXscale.Y, AXscale.Z);
            rScale = tScale * AXrot;
            vertexes[4] = (new Vector3((pos.X + rScale.X), (pos.Y + rScale.Y), (pos.Z + rScale.Z)));

               // vertexes[4].X = pos.X + vertexes[4].X;
               // vertexes[4].Y = pos.Y + vertexes[4].Y;
               // vertexes[4].Z = pos.Z + vertexes[4].Z;

            FaceB[1] = vertexes[4];
            FaceA[2] = vertexes[4];
            FaceD[4] = vertexes[4];

            tScale = new Vector3(-AXscale.X, AXscale.Y, -AXscale.Z);
            rScale = tScale * AXrot;
            vertexes[5] = (new Vector3((pos.X + rScale.X), (pos.Y + rScale.Y), (pos.Z + rScale.Z)));

               // vertexes[5].X = pos.X + vertexes[5].X;
               // vertexes[5].Y = pos.Y + vertexes[5].Y;
               // vertexes[5].Z = pos.Z + vertexes[5].Z;

            FaceD[1] = vertexes[5];
            FaceC[2] = vertexes[5];
            FaceB[5] = vertexes[5];

            tScale = new Vector3(-AXscale.X, -AXscale.Y, AXscale.Z);
            rScale = tScale * AXrot;
            vertexes[6] = (new Vector3((pos.X + rScale.X), (pos.Y + rScale.Y), (pos.Z + rScale.Z)));

               // vertexes[6].X = pos.X + vertexes[6].X;
               // vertexes[6].Y = pos.Y + vertexes[6].Y;
               // vertexes[6].Z = pos.Z + vertexes[6].Z;

            FaceB[2] = vertexes[6];
            FaceA[3] = vertexes[6];
            FaceB[4] = vertexes[6];

            tScale = new Vector3(-AXscale.X, -AXscale.Y, -AXscale.Z);
            rScale = tScale * AXrot;
            vertexes[7] = (new Vector3((pos.X + rScale.X), (pos.Y + rScale.Y), (pos.Z + rScale.Z)));

               // vertexes[7].X = pos.X + vertexes[7].X;
               // vertexes[7].Y = pos.Y + vertexes[7].Y;
               // vertexes[7].Z = pos.Z + vertexes[7].Z;

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

                cross = Vector3.Cross(AmBb, AmBa);

                // normalize the cross product to get the normal.
                normals[i] = cross / cross.Length();

                //m_log.Info("[NORMALS]: normals[ " + i + "]" + normals[i].ToString());
                //distance[i] = (normals[i].X * AmBa.X + normals[i].Y * AmBa.Y + normals[i].Z * AmBa.Z) * -1;
            }

            EntityIntersection result = new EntityIntersection();

            result.distance = 1024;
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
                        //return result;
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
                            //return result;
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
                            //return result;
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
                d = Vector3.Dot(normals[i], FaceB[i]);

                //if (faceCenters)
                //{
                //    c = normals[i].Dot(normals[i]);
                //}
                //else
                //{
                c = Vector3.Dot(iray.Direction, normals[i]);
                //}
                if (c == 0)
                    continue;

                a = (d - Vector3.Dot(iray.Origin, normals[i])) / c;

                if (a < 0)
                    continue;

                // If the normal is pointing outside the object
                if (Vector3.Dot(iray.Direction, normals[i]) < 0 || !frontFacesOnly)
                {
                    //if (faceCenters)
                    //{   //(FaceA[i] + FaceB[i] + FaceC[1] + FaceD[i]) / 4f;
                    //    q =  iray.Origin + a * normals[i];
                    //}
                    //else
                    //{
                        q = iray.Origin + iray.Direction * a;
                    //}

                    float distance2 = (float)GetDistanceTo(q, AXpos);
                    // Is this the closest hit to the object's origin?
                    //if (faceCenters)
                    //{
                    //    distance2 = (float)GetDistanceTo(q, iray.Origin);
                    //}

                    if (distance2 < result.distance)
                    {
                        result.distance = distance2;
                        result.HitTF = true;
                        result.ipoint = q;
                        //m_log.Info("[FACE]:" + i.ToString());
                        //m_log.Info("[POINT]: " + q.ToString());
                        //m_log.Info("[DIST]: " + distance2.ToString());
                        if (faceCenters)
                        {
                            result.normal = AAfacenormals[i] * AXrot;

                            Vector3 scaleComponent = AAfacenormals[i];
                            float ScaleOffset = 0.5f;
                            if (scaleComponent.X != 0) ScaleOffset = AXscale.X;
                            if (scaleComponent.Y != 0) ScaleOffset = AXscale.Y;
                            if (scaleComponent.Z != 0) ScaleOffset = AXscale.Z;
                            ScaleOffset = Math.Abs(ScaleOffset);
                            Vector3 offset = result.normal * ScaleOffset;
                            result.ipoint = AXpos + offset;

                            ///pos = (intersectionpoint + offset);
                        }
                        else
                        {
                            result.normal = normals[i];
                        }
                        result.AAfaceNormal = AAfacenormals[i];
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Serialize this part to xml.
        /// </summary>
        /// <param name="xmlWriter"></param>
        public void ToXml(XmlWriter xmlWriter)
        {
            serializer.Serialize(xmlWriter, this);
        }

        public void TriggerScriptChangedEvent(Changed val)
        {
            if (m_parentGroup != null && m_parentGroup.Scene != null)
                m_parentGroup.Scene.EventManager.TriggerOnScriptChangedEvent(LocalId, (uint)val);
        }

        public void TrimPermissions()
        {
            _baseMask &= (uint)PermissionMask.All;
            _ownerMask &= (uint)PermissionMask.All;
            _groupMask &= (uint)PermissionMask.All;
            _everyoneMask &= (uint)PermissionMask.All;
            _nextOwnerMask &= (uint)PermissionMask.All;
        }

        public void Undo()
        {
            lock (m_undo)
            {
                if (m_undo.Count > 0)
                    {
                        UndoState goback = m_undo.Pop();
                        if (goback != null)
                            goback.PlaybackState(this);
                }
            }
        }

        public void UpdateExtraParam(ushort type, bool inUse, byte[] data)
        {
            m_shape.ReadInUpdateExtraParam(type, inUse, data);

            if (type == 0x30)
            {
                if (m_shape.SculptEntry && m_shape.SculptTexture != UUID.Zero)
                {
                    m_parentGroup.Scene.AssetService.Get(m_shape.SculptTexture.ToString(), this, AssetReceived);
                }
            }

            ParentGroup.HasGroupChanged = true;
            ScheduleFullUpdate();
        }

        public void UpdateGroupPosition(Vector3 pos)
        {
            if ((pos.X != GroupPosition.X) ||
                (pos.Y != GroupPosition.Y) ||
                (pos.Z != GroupPosition.Z))
            {
                Vector3 newPos = new Vector3(pos.X, pos.Y, pos.Z);
                GroupPosition = newPos;
                ScheduleTerseUpdate();
            }
        }

        public virtual void UpdateMovement()
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="pos"></param>
        public void UpdateOffSet(Vector3 pos)
        {
            if ((pos.X != OffsetPosition.X) ||
                (pos.Y != OffsetPosition.Y) ||
                (pos.Z != OffsetPosition.Z))
            {
                Vector3 newPos = new Vector3(pos.X, pos.Y, pos.Z);
                OffsetPosition = newPos;
                ScheduleTerseUpdate();
            }
        }

        public void UpdatePermissions(UUID AgentID, byte field, uint localID, uint mask, byte addRemTF)
        {
            bool set = addRemTF == 1;
            bool god = m_parentGroup.Scene.Permissions.IsGod(AgentID);

            uint baseMask = _baseMask;
            if (god)
                baseMask = 0x7ffffff0;

            // Are we the owner?
            if ((AgentID == _ownerID) || god)
            {
                switch (field)
                {
                    case 1:
                        if (god)
                        {
                            _baseMask = ApplyMask(_baseMask, set, mask);
                            Inventory.ApplyGodPermissions(_baseMask);
                        }

                        break;
                    case 2:
                        _ownerMask = ApplyMask(_ownerMask, set, mask) &
                                baseMask;
                        break;
                    case 4:
                        _groupMask = ApplyMask(_groupMask, set, mask) &
                                baseMask;
                        break;
                    case 8:
                        _everyoneMask = ApplyMask(_everyoneMask, set, mask) &
                                baseMask;
                        break;
                    case 16:
                        _nextOwnerMask = ApplyMask(_nextOwnerMask, set, mask) &
                                baseMask;
                        break;
                }
                SendFullUpdateToAllClients();

                SendObjectPropertiesToClient(AgentID);

            }
        }

        public bool IsHingeJoint()
        {
            // For now, we use the NINJA naming scheme for identifying joints.
            // In the future, we can support other joint specification schemes such as a 
            // custom checkbox in the viewer GUI.
            if (m_parentGroup.Scene.PhysicsScene.SupportsNINJAJoints)
            {
                string hingeString = "hingejoint";
                return (Name.Length >= hingeString.Length && Name.Substring(0, hingeString.Length) == hingeString);
            }
            else
            {
                return false;
            }
        }

        public bool IsBallJoint()
        {
            // For now, we use the NINJA naming scheme for identifying joints.
            // In the future, we can support other joint specification schemes such as a 
            // custom checkbox in the viewer GUI.
            if (m_parentGroup.Scene.PhysicsScene.SupportsNINJAJoints)
            {
                string ballString = "balljoint";
                return (Name.Length >= ballString.Length && Name.Substring(0, ballString.Length) == ballString);
            }
            else
            {
                return false;
            }
        }

        public bool IsJoint()
        {
            // For now, we use the NINJA naming scheme for identifying joints.
            // In the future, we can support other joint specification schemes such as a 
            // custom checkbox in the viewer GUI.
            if (m_parentGroup.Scene.PhysicsScene.SupportsNINJAJoints)
            {
                return IsHingeJoint() || IsBallJoint();
            }
            else
            {
                return false;
            }
        }

        public void UpdatePrimFlags(bool UsePhysics, bool IsTemporary, bool IsPhantom, bool IsVD)
        {
            bool wasUsingPhysics = ((ObjectFlags & (uint) PrimFlags.Physics) != 0);
            bool wasTemporary = ((ObjectFlags & (uint)PrimFlags.TemporaryOnRez) != 0);
            bool wasPhantom = ((ObjectFlags & (uint)PrimFlags.Phantom) != 0);
            bool wasVD = VolumeDetectActive;

            if ((UsePhysics == wasUsingPhysics) && (wasTemporary == IsTemporary) && (wasPhantom == IsPhantom) && (IsVD==wasVD))
            {
                return;
            }

            // Special cases for VD. VD can only be called from a script 
            // and can't be combined with changes to other states. So we can rely
            // that...
            // ... if VD is changed, all others are not.
            // ... if one of the others is changed, VD is not.
            if (IsVD) // VD is active, special logic applies
            {
                // State machine logic for VolumeDetect
                // More logic below
                bool phanReset = (IsPhantom != wasPhantom) && !IsPhantom;

                if (phanReset) // Phantom changes from on to off switch VD off too
                {
                    IsVD = false;               // Switch it of for the course of this routine
                    VolumeDetectActive = false; // and also permanently
                    if (PhysActor != null)
                        PhysActor.SetVolumeDetect(0);   // Let physics know about it too
                }
                else
                {
                    IsPhantom = false;
                    // If volumedetect is active we don't want phantom to be applied.
                    // If this is a new call to VD out of the state "phantom"
                    // this will also cause the prim to be visible to physics
                }

            }

            if (UsePhysics && IsJoint())
            {
                IsPhantom = true;
            }

            if (UsePhysics)
            {
                AddFlag(PrimFlags.Physics);
                if (!wasUsingPhysics)
                {
                    DoPhysicsPropertyUpdate(UsePhysics, false);
                    if (m_parentGroup != null)
                    {
                        if (!m_parentGroup.IsDeleted)
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
                RemFlag(PrimFlags.Physics);
                if (wasUsingPhysics)
                {
                    DoPhysicsPropertyUpdate(UsePhysics, false);
                }
            }


            if (IsPhantom || IsAttachment || (Shape.PathCurve == (byte)Extrusion.Flexible)) // note: this may have been changed above in the case of joints
            {
                AddFlag(PrimFlags.Phantom);
                if (PhysActor != null)
                {
                    m_parentGroup.Scene.PhysicsScene.RemovePrim(PhysActor);
                    /// that's not wholesome.  Had to make Scene public
                    PhysActor = null;
                }
            }
            else // Not phantom
            {
                RemFlag(PrimFlags.Phantom);

                PhysicsActor pa = PhysActor;
                if (pa == null)
                {
                    // It's not phantom anymore. So make sure the physics engine get's knowledge of it
                    PhysActor = m_parentGroup.Scene.PhysicsScene.AddPrimShape(
                        Name,
                        Shape,
                        new PhysicsVector(AbsolutePosition.X, AbsolutePosition.Y, AbsolutePosition.Z),
                        new PhysicsVector(Scale.X, Scale.Y, Scale.Z),
                        RotationOffset,
                        UsePhysics);

                    pa = PhysActor;
                    if (pa != null)
                    {
                        pa.LocalID = LocalId;
                        DoPhysicsPropertyUpdate(UsePhysics, true);
                        if (m_parentGroup != null)
                        {
                            if (!m_parentGroup.IsDeleted)
                            {
                                if (LocalId == m_parentGroup.RootPart.LocalId)
                                {
                                    m_parentGroup.CheckSculptAndLoad();
                                }
                            }
                        }
                        if (
                            ((AggregateScriptEvents & scriptEvents.collision) != 0) ||
                            ((AggregateScriptEvents & scriptEvents.collision_end) != 0) ||
                            ((AggregateScriptEvents & scriptEvents.collision_start) != 0) ||
                            (CollisionSound != UUID.Zero)
                            )
                        {
                                PhysActor.OnCollisionUpdate += PhysicsCollision;
                                PhysActor.SubscribeEvents(1000);
                        }
                    }
                }
                else // it already has a physical representation
                {
                    pa.IsPhysical = UsePhysics;

                    DoPhysicsPropertyUpdate(UsePhysics, false); // Update physical status. If it's phantom this will remove the prim
                    if (m_parentGroup != null)
                    {
                        if (!m_parentGroup.IsDeleted)
                        {
                            if (LocalId == m_parentGroup.RootPart.LocalId)
                            {
                                m_parentGroup.CheckSculptAndLoad();
                            }
                        }
                    }
                }
            }

            if (IsVD)
            {
                // If the above logic worked (this is urgent candidate to unit tests!)
                // we now have a physicsactor.
                // Defensive programming calls for a check here.
                // Better would be throwing an exception that could be catched by a unit test as the internal 
                // logic should make sure, this Physactor is always here.
                if (this.PhysActor != null)
                {
                    PhysActor.SetVolumeDetect(1);
                    AddFlag(PrimFlags.Phantom); // We set this flag also if VD is active
                    this.VolumeDetectActive = true;
                }
            }
            else
            {   // Remove VolumeDetect in any case. Note, it's safe to call SetVolumeDetect as often as you like
                // (mumbles, well, at least if you have infinte CPU powers :-))
                PhysicsActor pa = this.PhysActor;
                if (pa != null)
                {
                    PhysActor.SetVolumeDetect(0);
                }
                this.VolumeDetectActive = false;
            }


            if (IsTemporary)
            {
                AddFlag(PrimFlags.TemporaryOnRez);
            }
            else
            {
                RemFlag(PrimFlags.TemporaryOnRez);
            }
            //            m_log.Debug("Update:  PHY:" + UsePhysics.ToString() + ", T:" + IsTemporary.ToString() + ", PHA:" + IsPhantom.ToString() + " S:" + CastsShadows.ToString());

            ParentGroup.HasGroupChanged = true;
            ScheduleFullUpdate();
        }

        public void UpdateRotation(Quaternion rot)
        {
            if ((rot.X != RotationOffset.X) ||
                (rot.Y != RotationOffset.Y) ||
                (rot.Z != RotationOffset.Z) ||
                (rot.W != RotationOffset.W))
            {
                //StoreUndoState();
                RotationOffset = rot;
                ParentGroup.HasGroupChanged = true;
                ScheduleTerseUpdate();
            }
        }

        /// <summary>
        /// Update the shape of this part.
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
                m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
            }

            // This is what makes vehicle trailers work
            // A script in a child prim re-issues
            // llSetPrimitiveParams(PRIM_TYPE) every few seconds. That
            // prevents autoreturn. This is not well known. It also works
            // in SL.
            //
            if (ParentGroup.RootPart != this)
                ParentGroup.RootPart.Rezzed = DateTime.UtcNow;

            ParentGroup.HasGroupChanged = true;
            ScheduleFullUpdate();
        }

        // Added to handle bug in libsecondlife's TextureEntry.ToBytes()
        // not handling RGBA properly. Cycles through, and "fixes" the color
        // info
        public void UpdateTexture(Primitive.TextureEntry tex)
        {
            //Color4 tmpcolor;
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
            UpdateTextureEntry(tex.GetBytes());
        }

        /// <summary>
        /// Update the texture entry for this part.
        /// </summary>
        /// <param name="textureEntry"></param>
        public void UpdateTextureEntry(byte[] textureEntry)
        {
            m_shape.TextureEntry = textureEntry;
            TriggerScriptChangedEvent(Changed.TEXTURE);

            ParentGroup.HasGroupChanged = true;
            //This is madness..
            //ParentGroup.ScheduleGroupForFullUpdate();
            //This is sparta
            ScheduleFullUpdate();
        }

        public void aggregateScriptEvents()
        {
            AggregateScriptEvents = 0;

            // Aggregate script events
            lock (m_scriptEvents)
            {
                foreach (scriptEvents s in m_scriptEvents.Values)
                {
                    AggregateScriptEvents |= s;
                }
            }

            uint objectflagupdate = 0;

            if (
                ((AggregateScriptEvents & scriptEvents.touch) != 0) ||
                ((AggregateScriptEvents & scriptEvents.touch_end) != 0) ||
                ((AggregateScriptEvents & scriptEvents.touch_start) != 0)
                )
            {
                objectflagupdate |= (uint) PrimFlags.Touch;
            }

            if ((AggregateScriptEvents & scriptEvents.money) != 0)
            {
                objectflagupdate |= (uint) PrimFlags.Money;
            }

            if (AllowedDrop)
            {
                objectflagupdate |= (uint) PrimFlags.AllowInventoryDrop;
            }

            if (
                ((AggregateScriptEvents & scriptEvents.collision) != 0) ||
                ((AggregateScriptEvents & scriptEvents.collision_end) != 0) ||
                ((AggregateScriptEvents & scriptEvents.collision_start) != 0) ||
                (CollisionSound != UUID.Zero)
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

            if (m_parentGroup == null)
            {
                ScheduleFullUpdate();
                return;
            }

            //if ((GetEffectiveObjectFlags() & (uint)PrimFlags.Scripted) != 0)
            //{
            //    m_parentGroup.Scene.EventManager.OnScriptTimerEvent += handleTimerAccounting;
            //}
            //else
            //{
            //    m_parentGroup.Scene.EventManager.OnScriptTimerEvent -= handleTimerAccounting;
            //}

            LocalFlags=(PrimFlags)objectflagupdate;

            if (m_parentGroup != null && m_parentGroup.RootPart == this)
                m_parentGroup.aggregateScriptEvents();
            else
                ScheduleFullUpdate();
        }

        public int registerTargetWaypoint(Vector3 target, float tolerance)
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

        public void SetCameraAtOffset(Vector3 v)
        {
            m_cameraAtOffset = v;
        }

        public void SetCameraEyeOffset(Vector3 v)
        {
            m_cameraEyeOffset = v;
        }

        public void SetForceMouselook(bool force)
        {
            m_forceMouselook = force;
        }

        public Vector3 GetCameraAtOffset()
        {
            return m_cameraAtOffset;
        }

        public Vector3 GetCameraEyeOffset()
        {
            return m_cameraEyeOffset;
        }

        public bool GetForceMouselook()
        {
            return m_forceMouselook;
        }
        
        public override string ToString()
        {
            return String.Format("{0} {1} (parent {2}))", Name, UUID, ParentGroup);
        }

        #endregion Public Methods

        public void SendTerseUpdateToClient(IClientAPI remoteClient)
        {
            if (ParentGroup == null || ParentGroup.IsDeleted)
                return;

            Vector3 lPos = OffsetPosition;

            byte state = Shape.State;
            if (IsAttachment)
            {
                if (ParentGroup.RootPart != this)
                    return;

                lPos = ParentGroup.RootPart.AttachedPos;
                state = (byte)AttachmentPoint;
            }
            else
            {
                if (ParentGroup.RootPart == this)
                    lPos = AbsolutePosition;
            }
            
            // Causes this thread to dig into the Client Thread Data.
            // Remember your locking here!
            remoteClient.SendPrimTerseUpdate(new SendPrimitiveTerseData(m_regionHandle,
                    (ushort)(m_parentGroup.GetTimeDilation() *
                    (float)ushort.MaxValue), LocalId, lPos,
                    RotationOffset, Velocity, Acceleration,
                    RotationalVelocity, state, FromItemID,
                    OwnerID, (int)AttachmentPoint, null, ParentGroup.GetUpdatePriority(remoteClient)));
        }
                
        public void AddScriptLPS(int count)
        {
            m_parentGroup.AddScriptLPS(count);
        }
        
        public void ApplyNextOwnerPermissions()
        {
            _baseMask &= _nextOwnerMask;
            _ownerMask &= _nextOwnerMask;
            _everyoneMask &= _nextOwnerMask;

            Inventory.ApplyNextOwnerPermissions();
        }

        public bool CanBeDeleted()
        {
            return Inventory.CanBeDeleted();
        }
    }
}
