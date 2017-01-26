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
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Xml;
using System.Xml.Serialization;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes.Scripting;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.PhysicsModules.SharedBase;
using PermissionMask = OpenSim.Framework.PermissionMask;

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
        REGION = 256,
        TELEPORT = 512,
        REGION_RESTART = 1024,
        MEDIA = 2048,
        ANIMATION = 16384,
        POSITION = 32768
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

    public enum PrimType : int
    {
        BOX = 0,
        CYLINDER = 1,
        PRISM = 2,
        SPHERE = 3,
        TORUS = 4,
        TUBE = 5,
        RING = 6,
        SCULPT = 7
    }

    public enum UpdateRequired : byte
    {
        NONE = 0,
        TERSE = 1,
        FULL = 2
    }

    #endregion Enumerations

    public class SceneObjectPart : ISceneEntity
    {
        /// <value>
        /// Denote all sides of the prim
        /// </value>
        public const int ALL_SIDES = -1;

        private const scriptEvents PhysicsNeededSubsEvents = (
                    scriptEvents.collision | scriptEvents.collision_start | scriptEvents.collision_end |
                    scriptEvents.land_collision | scriptEvents.land_collision_start | scriptEvents.land_collision_end
                    );
        private const scriptEvents PhyscicsPhantonSubsEvents = (
                    scriptEvents.land_collision | scriptEvents.land_collision_start | scriptEvents.land_collision_end
                    );
        private const scriptEvents PhyscicsVolumeDtcSubsEvents = (
                    scriptEvents.collision_start | scriptEvents.collision_end
                    );

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Dynamic attributes can be created and deleted as required.
        /// </summary>
        public DAMap DynAttrs { get; set; }

        private DOMap m_dynObjs;

        /// <summary>
        /// Dynamic objects that can be created and deleted as required.
        /// </summary>
        public DOMap DynObjs
        {
            get
            {
                if (m_dynObjs == null)
                    m_dynObjs = new DOMap();

                return m_dynObjs;
            }

            set
            {
                m_dynObjs = value;
            }
        }

        /// <value>
        /// Is this a root part?
        /// </value>
        /// <remarks>
        /// This will return true even if the whole object is attached to an avatar.
        /// </remarks>
        public bool IsRoot
        {
            get { return Object.ReferenceEquals(ParentGroup.RootPart, this); }
        }

        /// <summary>
        /// Is an explicit sit target set for this part?
        /// </summary>
        public bool IsSitTargetSet
        {
            get
            {
                return
                    !(SitTargetPosition == Vector3.Zero
                      && (SitTargetOrientation == Quaternion.Identity // Valid Zero Rotation quaternion
                       || (SitTargetOrientation.W == 0f && SitTargetOrientation.X == 0f && SitTargetOrientation.Y == 0f && SitTargetOrientation.Z == 0f ))); // Invalid Quaternion
            }
        }

        #region Fields

        public bool AllowedDrop;

        public bool DIE_AT_EDGE;

        public bool RETURN_AT_EDGE;

        public bool BlockGrab { get; set; }

        public bool StatusSandbox;

        public Vector3 StatusSandboxPos;

        [XmlIgnore]
        public int[] PayPrice = {-2,-2,-2,-2,-2};

        [XmlIgnore]
        /// <summary>
        /// The representation of this part in the physics scene.
        /// </summary>
        /// <remarks>
        /// If you use this property more than once in a section of code then you must take a reference and use that.
        /// If another thread is simultaneously turning physics off on this part then this refernece could become
        /// null at any time.
        /// </remarks>
        public PhysicsActor PhysActor { get; set; }

        //Xantor 20080528 Sound stuff:
        //  Note: This isn't persisted in the database right now, as the fields for that aren't just there yet.
        //        Not a big problem as long as the script that sets it remains in the prim on startup.
        //        for SL compatibility it should be persisted though (set sound / displaytext / particlesystem, kill script)

        public UUID Sound;

        public byte SoundFlags;

        public double SoundGain;

        public double SoundRadius;

        /// <summary>
        /// Should sounds played from this prim be queued?
        /// </summary>
        /// <remarks>
        /// This should only be changed by sound modules.  It is up to sound modules as to how they interpret this setting.
        /// </remarks>
        public bool SoundQueueing { get; set; }

        public uint TimeStampFull;

        public uint TimeStampLastActivity; // Will be used for AutoReturn

        public uint TimeStampTerse;

        [XmlIgnore]
        public Quaternion AttachRotation = Quaternion.Identity;

        [XmlIgnore]
        public int STATUS_ROTATE_X; // this should not be used

        [XmlIgnore]
        public int STATUS_ROTATE_Y;  // this should not be used

        [XmlIgnore]
        public int STATUS_ROTATE_Z;  // this should not be used

        private Dictionary<int, string> m_CollisionFilter = new Dictionary<int, string>();

        /// <value>
        /// The UUID of the user inventory item from which this object was rezzed if this is a root part.
        /// If UUID.Zero then either this is not a root part or there is no connection with a user inventory item.
        /// </value>
        private UUID m_fromUserInventoryItemID;

        public UUID FromUserInventoryItemID
        {
            get { return m_fromUserInventoryItemID; }
            set { m_fromUserInventoryItemID = value; }
        }

        public scriptEvents AggregateScriptEvents;

        public Vector3 AttachedPos
        {
            get;
            set;
        }

        // rotation locks on local X,Y and or Z axis bit flags
        // bits are as in llSetStatus defined in SceneObjectGroup.axisSelect enum
        // but reversed logic: bit cleared means free to rotate
        public byte RotationAxisLocks = 0;

        // WRONG flag in libOmvPrimFlags
        private const uint primFlagVolumeDetect = (uint)PrimFlags.JointLP2P;

        public bool VolumeDetectActive
        {
            get
            {
                return (Flags & (PrimFlags)primFlagVolumeDetect) != 0;
            }
            set
            {
                if(value)
                    Flags |= (PrimFlags)primFlagVolumeDetect;
                else
                    Flags &= (PrimFlags)(~primFlagVolumeDetect);
            }
        }

        public bool IsWaitingForFirstSpinUpdatePacket;

        public Quaternion SpinOldOrientation = Quaternion.Identity;

        protected bool m_APIDActive = false;
        protected Quaternion m_APIDTarget = Quaternion.Identity;
        protected float m_APIDDamp = 0;
        protected float m_APIDStrength = 0;

        /// <summary>
        /// This part's inventory
        /// </summary>
        public IEntityInventory Inventory
        {
            get { return m_inventory; }
        }
        protected SceneObjectPartInventory m_inventory;

        public bool Undoing;

        public bool IgnoreUndoUpdate = false;

        public PrimFlags LocalFlags;

        private float m_damage = -1.0f;
        private byte[] m_TextureAnimation;
        private byte m_clickAction;
        private Color m_color = Color.Black;
        private readonly List<uint> m_lastColliders = new List<uint>();
        private int m_linkNum;

        private int m_scriptAccessPin;

        private readonly Dictionary<UUID, scriptEvents> m_scriptEvents = new Dictionary<UUID, scriptEvents>();
        private string m_sitName = String.Empty;
        private Quaternion m_sitTargetOrientation = Quaternion.Identity;
        private Vector3 m_sitTargetPosition;
        private string m_sitAnimation = "SIT";
        private bool m_occupied;					// KF if any av is sitting on this prim
        private string m_text = String.Empty;
        private string m_touchName = String.Empty;
        private UndoRedoState m_UndoRedo = null;
        private object m_UndoLock = new object();

        private bool m_passTouches = false;
        private bool m_passCollisions = false;

        protected Vector3 m_acceleration;
        protected Vector3 m_angularVelocity;

        //unkown if this will be kept, added as a way of removing the group position from the group class
        protected Vector3 m_groupPosition;
        protected uint m_localId;
        protected Material m_material = OpenMetaverse.Material.Wood;
        protected string m_name;
        protected Vector3 m_offsetPosition;

        protected SceneObjectGroup m_parentGroup;
        protected byte[] m_particleSystem = Utils.EmptyBytes;
        protected ulong m_regionHandle;
        protected Quaternion m_rotationOffset = Quaternion.Identity;
        protected PrimitiveBaseShape m_shape;
        protected UUID m_uuid;
        protected Vector3 m_velocity;

        protected Vector3 m_lastPosition;
        protected Quaternion m_lastRotation;
        protected Vector3 m_lastVelocity;
        protected Vector3 m_lastAcceleration;
        protected Vector3 m_lastAngularVelocity;
        protected int m_lastUpdateSentTime;
        protected float m_buoyancy = 0.0f;
        protected Vector3 m_force;
        protected Vector3 m_torque;

        protected byte m_physicsShapeType = (byte)PhysShapeType.prim;
        protected float m_density = 1000.0f; // in kg/m^3
        protected float m_gravitymod = 1.0f;
        protected float m_friction = 0.6f; // wood
        protected float m_bounce = 0.5f; // wood


        protected bool m_isSelected = false;

        /// <summary>
        /// Stores media texture data
        /// </summary>
        protected string m_mediaUrl;

        // TODO: Those have to be changed into persistent properties at some later point,
        // or sit-camera on vehicles will break on sim-crossing.
        private Vector3 m_cameraEyeOffset;
        private Vector3 m_cameraAtOffset;
        private bool m_forceMouselook;


        // 0 for default collision sounds, -1 for script disabled sound 1 for script defined sound
        private sbyte m_collisionSoundType = 0;
        private UUID m_collisionSound;
        private float m_collisionSoundVolume;

        private int LastColSoundSentTime;

        private SOPVehicle m_vehicleParams = null;

        public KeyframeMotion KeyframeMotion
        {
            get; set;
        }


        #endregion Fields

//        ~SceneObjectPart()
//        {
//            Console.WriteLine(
//                "[SCENE OBJECT PART]: Destructor called for {0}, local id {1}, parent {2} {3}",
//                Name, LocalId, ParentGroup.Name, ParentGroup.LocalId);
//            m_log.DebugFormat(
//                "[SCENE OBJECT PART]: Destructor called for {0}, local id {1}, parent {2} {3}",
//                Name, LocalId, ParentGroup.Name, ParentGroup.LocalId);
//        }

        #region Constructors

        /// <summary>
        /// No arg constructor called by region restore db code
        /// </summary>
        public SceneObjectPart()
        {
            m_TextureAnimation = Utils.EmptyBytes;
            m_particleSystem = Utils.EmptyBytes;
            Rezzed = DateTime.UtcNow;
            Description = String.Empty;
            DynAttrs = new DAMap();

            // Prims currently only contain a single folder (Contents).  From looking at the Second Life protocol,
            // this appears to have the same UUID (!) as the prim.  If this isn't the case, one can't drag items from
            // the prim into an agent inventory (Linden client reports that the "Object not found for drop" in its log
            m_inventory = new SceneObjectPartInventory(this);
            LastColSoundSentTime = Util.EnvironmentTickCount();
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
            Quaternion rotationOffset, Vector3 offsetPosition) : this()
        {
            m_name = "Object";

            CreationDate = (int)Utils.DateTimeToUnixTime(Rezzed);
            RezzerID = LastOwnerID = CreatorID = OwnerID = ownerID;
            UUID = UUID.Random();
            Shape = shape;
            OwnershipCost = 0;
            ObjectSaleType = 0;
            SalePrice = 0;
            Category = 0;
            GroupPosition = groupPosition;
            OffsetPosition = offsetPosition;
            RotationOffset = rotationOffset;
            Velocity = Vector3.Zero;
            AngularVelocity = Vector3.Zero;
            Acceleration = Vector3.Zero;
            APIDActive = false;
            Flags = 0;
            CreateSelected = true;
            TrimPermissions();
            AggregateInnerPerms();
        }

        #endregion Constructors

        #region XML Schema

        private UUID _rezzerID;
        private UUID _lastOwnerID;
        private UUID _ownerID;
        private UUID _groupID;
        private int _ownershipCost;
        private byte _objectSaleType;
        private int _salePrice;
        private uint _category;
        private Int32 _creationDate;
        private uint _parentID = 0;
        private uint _baseMask = (uint)(PermissionMask.All | PermissionMask.Export);
        private uint _ownerMask = (uint)(PermissionMask.All | PermissionMask.Export);
        private uint _groupMask = (uint)PermissionMask.None;
        private uint _everyoneMask = (uint)PermissionMask.None;
        private uint _nextOwnerMask = (uint)(PermissionMask.Move | PermissionMask.Transfer);
        private PrimFlags _flags = PrimFlags.None;
        private DateTime m_expires;
        private DateTime m_rezzed;
        private bool m_createSelected = false;

        private UUID _creatorID;
        public UUID CreatorID
        {
            get { return _creatorID; }
            set { _creatorID = value; }
        }

        private string m_creatorData = string.Empty;
        /// <summary>
        /// Data about the creator in the form home_url;name
        /// </summary>
        public string CreatorData
        {
            get { return m_creatorData; }
            set { m_creatorData = value; }
        }

        /// <summary>
        /// Used by the DB layer to retrieve / store the entire user identification.
        /// The identification can either be a simple UUID or a string of the form
        /// uuid[;home_url[;name]]
        /// </summary>
        public string CreatorIdentification
        {
            get
            {
                if (!string.IsNullOrEmpty(CreatorData))
                    return CreatorID.ToString() + ';' + CreatorData;
                else
                    return CreatorID.ToString();
            }
            set
            {
                CreatorData = string.Empty;
                if ((value == null) || (value != null && value == string.Empty))
                    return;

                // value is uuid  or uuid;homeuri;firstname lastname
                string[] parts = value.Split(';');
                if (parts.Length > 0)
                {

                    UUID uuid = UUID.Zero;
                    UUID.TryParse(parts[0], out uuid);
                    CreatorID = uuid;

                    if (parts.Length > 1)
                    {
                        CreatorData = parts[1];
                        if (!CreatorData.EndsWith("/"))
                            CreatorData += "/";
                        if (parts.Length > 2)
                            CreatorData += ';' + parts[2];
                        else
                            CreatorData += ";Unknown User";
                    }
                }
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
        /// Get the inventory list
        /// </value>
        public TaskInventoryDictionary TaskInventory
        {
            get {
                return m_inventory.Items;
            }
            set {
                m_inventory.Items = value;
            }
        }

        /// <summary>
        /// This is idential to the Flags property, except that the returned value is uint rather than PrimFlags
        /// </summary>
        [Obsolete("Use Flags property instead")]
        public uint ObjectFlags
        {
            get { return (uint)Flags; }
            set { Flags = (PrimFlags)value; }
        }

        public UUID UUID
        {
            get { return m_uuid; }
            set
            {
                m_uuid = value;

                // This is necessary so that TaskInventoryItem parent ids correctly reference the new uuid of this part
                if (Inventory != null)
                    Inventory.ResetObjectID();
            }
        }

        public uint LocalId
        {
            get { return m_localId; }
            set
            {
                m_localId = value;
//                m_log.DebugFormat("[SCENE OBJECT PART]: Set part {0} to local id {1}", Name, m_localId);
            }
        }

        public virtual string Name
        {
            get { return m_name; }
            set
            {
                m_name = value;

                PhysicsActor pa = PhysActor;

                if (pa != null)
                    pa.SOPName = value;
            }
        }

        [XmlIgnore]
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

        public bool PassCollisions
        {
            get { return m_passCollisions; }
            set
            {
                m_passCollisions = value;

                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
            }
        }

        public bool IsSelected
        {
            get { return m_isSelected; }
            set
            {
                m_isSelected = value;
                if (ParentGroup != null)
                    ParentGroup.PartSelectChanged(value);

            }
        }


        public Dictionary<int, string> CollisionFilter
        {
            get { return m_CollisionFilter; }
            set
            {
                m_CollisionFilter = value;
            }
        }

        protected bool APIDActive
        {
            get { return m_APIDActive; }
            set { m_APIDActive = value; }
        }

        protected Quaternion APIDTarget
        {
            get { return m_APIDTarget; }
            set { m_APIDTarget = value; }
        }


        protected float APIDDamp
        {
            get { return m_APIDDamp; }
            set { m_APIDDamp = value; }
        }


        protected float APIDStrength
        {
            get { return m_APIDStrength; }
            set { m_APIDStrength = value; }
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

        public Byte[] TextureAnimation
        {
            get { return m_TextureAnimation; }
            set { m_TextureAnimation = value; }
        }

        public Byte[] ParticleSystem
        {
            get { return m_particleSystem; }
            set { m_particleSystem = value; }
        }


        public DateTime Expires
        {
            get { return m_expires; }
            set { m_expires = value; }
        }


        public DateTime Rezzed
        {
            get { return m_rezzed; }
            set { m_rezzed = value; }
        }


        public float Damage
        {
            get { return m_damage; }
            set { m_damage = value; }
        }

        public void setGroupPosition(Vector3 pos)
        {
            m_groupPosition = pos;
        }

        /// <summary>
        /// The position of the entire group that this prim belongs to.
        /// </summary>
        ///

        public Vector3 GroupPosition
        {
            get
            {
                // If this is a linkset, we don't want the physics engine mucking up our group position here.
                PhysicsActor actor = PhysActor;
                if (ParentID == 0)
                {
                    if (actor != null)
                        m_groupPosition = actor.Position;
                    return m_groupPosition;
                }

                // If I'm an attachment, my position is reported as the position of who I'm attached to
                if (ParentGroup.IsAttachment)
                {
                    ScenePresence sp = ParentGroup.Scene.GetScenePresence(ParentGroup.AttachedAvatar);
                    if (sp != null)
                        return sp.AbsolutePosition;
                }

                // use root prim's group position. Physics may have updated it
                if (ParentGroup.RootPart != this)
                    m_groupPosition = ParentGroup.RootPart.GroupPosition;
                return m_groupPosition;
            }
            set
            {
                m_groupPosition = value;
                PhysicsActor actor = PhysActor;
                if (actor != null && ParentGroup.Scene.PhysicsScene != null)
                {
                    try
                    {
                        // Root prim actually goes at Position
                        if (ParentID == 0)
                        {
                            actor.Position = value;
                        }
                        else
                        {
                            // The physics engine always sees all objects (root or linked) in world coordinates.
                            actor.Position = GetWorldPosition();
                            actor.Orientation = GetWorldRotation();
                        }

                        // Tell the physics engines that this prim changed.
                        if (ParentGroup != null && ParentGroup.Scene != null && ParentGroup.Scene.PhysicsScene != null)
                            ParentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(actor);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[SCENEOBJECTPART]: GROUP POSITION. {0}", e);
                    }
                }
            }
        }

        public void setOffsetPosition(Vector3 pos)
        {
            m_offsetPosition = pos;
        }

        public Vector3 OffsetPosition
        {
            get { return m_offsetPosition; }
            set
            {
                Vector3 oldpos = m_offsetPosition;
                m_offsetPosition = value;

                if (ParentGroup != null && !ParentGroup.IsDeleted)
                {
                    if((oldpos - m_offsetPosition).LengthSquared() > 1.0f)
                        ParentGroup.InvalidBoundsRadius();

                    PhysicsActor actor = PhysActor;
                    if (ParentID != 0 && actor != null)
                    {
                        actor.Position = GetWorldPosition();
                        actor.Orientation = GetWorldRotation();

                        // Tell the physics engines that this prim changed.
                        if (ParentGroup.Scene != null && ParentGroup.Scene.PhysicsScene != null)
                            ParentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(actor);
                    }

                    if (!m_parentGroup.m_dupeInProgress)
                    {
                        List<ScenePresence> avs = ParentGroup.GetSittingAvatars();
                        foreach (ScenePresence av in avs)
                        {
                            if (av.ParentID == m_localId)
                            {
                                Vector3 offset = (m_offsetPosition - oldpos);
                                av.AbsolutePosition += offset;
//                                av.SendAvatarDataToAllAgents();
                                av.SendTerseUpdateToAllClients();
                            }
                        }
                    }
                }
                TriggerScriptChangedEvent(Changed.POSITION);
            }
        }

        public Vector3 RelativePosition
        {
            get
            {
                if (IsRoot)
                {
                    if (ParentGroup.IsAttachment)
                        return AttachedPos;
                    else
                        return AbsolutePosition;
                }
                else
                {
                    return OffsetPosition;
                }
            }
        }

        public void setRotationOffset(Quaternion q)
        {
            m_rotationOffset = q;
        }

        public Quaternion RotationOffset
        {
            get
            {
                // We don't want the physics engine mucking up the rotations in a linkset
                PhysicsActor actor = PhysActor;
                // If this is a root of a linkset, the real rotation is what the physics engine thinks.
                // If not a root prim, the offset rotation is computed by SOG and is relative to the root.
                if (ParentID == 0 && (Shape.PCode != 9 || Shape.State == 0) && actor != null)
                {
                    if (actor.Orientation.X != 0f || actor.Orientation.Y != 0f
                        || actor.Orientation.Z != 0f || actor.Orientation.W != 0f)
                    {
                        m_rotationOffset = actor.Orientation;
                    }
                }

//                float roll, pitch, yaw = 0;
//                m_rotationOffset.GetEulerAngles(out roll, out pitch, out yaw);
//
//                m_log.DebugFormat(
//                    "[SCENE OBJECT PART]: Got euler {0} for RotationOffset on {1} {2}",
//                    new Vector3(roll, pitch, yaw), Name, LocalId);

                return m_rotationOffset;
            }

            set
            {
//                StoreUndoState();
                m_rotationOffset = value;

                PhysicsActor actor = PhysActor;
                if (actor != null)
                {
                    try
                    {
                        // Root prim gets value directly
                        if (ParentID == 0)
                        {
                            actor.Orientation = value;
                            //m_log.Info("[PART]: RO1:" + actor.Orientation.ToString());
                        }
                        else
                        {
                            // Child prim we have to calculate it's world rotationwel
                            Quaternion resultingrotation = GetWorldRotation();
                            actor.Orientation = resultingrotation;
                            //m_log.Info("[PART]: RO2:" + actor.Orientation.ToString());
                        }

                        if (ParentGroup != null && ParentGroup.Scene != null && ParentGroup.Scene.PhysicsScene != null)
                            ParentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(actor);
                        //}
                    }
                    catch (Exception ex)
                    {
                        m_log.Error("[SCENEOBJECTPART]: ROTATIONOFFSET" + ex.Message);
                    }
                }

//                float roll, pitch, yaw = 0;
//                m_rotationOffset.GetEulerAngles(out roll, out pitch, out yaw);
//
//                m_log.DebugFormat(
//                    "[SCENE OBJECT PART]: Set euler {0} for RotationOffset on {1} {2}",
//                    new Vector3(roll, pitch, yaw), Name, LocalId);
            }
        }

        /// <summary></summary>
        public Vector3 Velocity
        {
            get
            {
                PhysicsActor actor = PhysActor;
                if (actor != null)
                {
                    if (actor.IsPhysical)
                    {
                        m_velocity = actor.Velocity;
                    }
                }

                return m_velocity;
            }

            set
            {
                if (Util.IsNanOrInfinity(value))
                    m_velocity = Vector3.Zero;
                else
                    m_velocity = value;

                PhysicsActor actor = PhysActor;
                if (actor != null)
                {
                    if (actor.IsPhysical)
                    {
                        actor.Velocity = m_velocity;
                        ParentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(actor);
                    }
                }
            }
        }

        /// <summary>Update angular velocity and schedule terse update.</summary>
        public void UpdateAngularVelocity(Vector3 avel)
        {
            AngularVelocity = avel;
            ScheduleTerseUpdate();
            ParentGroup.HasGroupChanged = true;
        }

        /// <summary>Get or set angular velocity. Does not schedule update.</summary>
        public Vector3 AngularVelocity
        {
            get
            {
                PhysicsActor actor = PhysActor;
                if ((actor != null) && actor.IsPhysical && ParentGroup.RootPart == this)
                {
                    m_angularVelocity = actor.RotationalVelocity;
                }
                return m_angularVelocity;
            }
            set
            {
                if (Util.IsNanOrInfinity(value))
                    m_angularVelocity = Vector3.Zero;
                else
                    m_angularVelocity = value;

                PhysicsActor actor = PhysActor;
                if ((actor != null) && actor.IsPhysical && ParentGroup.RootPart == this && VehicleType == (int)Vehicle.TYPE_NONE)
                {
                    actor.RotationalVelocity = m_angularVelocity;
                }
            }
        }

        /// <summary></summary>
        public Vector3 Acceleration
        {
            get
            {
                PhysicsActor actor = PhysActor;
                if (actor != null)
                {
                    m_acceleration = actor.Acceleration;
                }
                return m_acceleration;
            }

            set
            {
                if (Util.IsNanOrInfinity(value))
                    m_acceleration = Vector3.Zero;
                else
                    m_acceleration = value;
            }
        }

        public string Description { get; set; }

        /// <value>
        /// Text color.
        /// </value>
        public Color Color
        {
            get { return m_color; }
            set { m_color = value; }
        }

        public string Text
        {
            get
            {
                if (m_text.Length > 255)
                    return m_text.Substring(0, 254);
                return m_text;
            }
            set { m_text = value; }
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
            set
            {
//                if (ParentGroup != null)
//                {
//                    m_log.DebugFormat(
//                        "[SCENE OBJECT PART]: Setting linknum of {0}@{1} to {2} from {3}",
//                        Name, AbsolutePosition, value, m_linkNum);
//                    Util.PrintCallStack();
//                }

                m_linkNum = value;
            }
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
                m_shape = value;
            }
        }

        /// <summary>
        /// Change the scale of this part.
        /// </summary>
        public Vector3 Scale
        {
            get { return m_shape.Scale; }
            set
            {
                if (m_shape != null)
                {
                    Vector3 oldscale = m_shape.Scale;
                    m_shape.Scale = value;
                    if (ParentGroup != null && ((value - oldscale).LengthSquared() >1.0f))
                        ParentGroup.InvalidBoundsRadius();
                    PhysicsActor actor = PhysActor;
                    if (actor != null)
                    {
                        if (ParentGroup.Scene != null)
                        {
                            if (ParentGroup.Scene.PhysicsScene != null)
                            {
                                actor.Size = m_shape.Scale;
                                ParentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(actor);
                            }
                        }
                    }
                }

                TriggerScriptChangedEvent(Changed.SCALE);
            }
        }

        public float maxSimpleArea()
        {
            float a,b;
            float sx = m_shape.Scale.X;
            float sy = m_shape.Scale.Y;
            float sz = m_shape.Scale.Z;

            if( sx > sy)
            {
                a = sx;
                if(sy  > sz)
                    b = sy;
                else
                    b = sz;
            }
            else
            {
                a = sy;
                if(sx  > sz)
                    b = sx;
                else
                    b = sz;
            }

            return a * b;
        }

        public UpdateRequired UpdateFlag { get; set; }

        /// <summary>
        /// Used for media on a prim.
        /// </summary>
        /// Do not change this value directly - always do it through an IMoapModule.
        public string MediaUrl
        {
            get
            {
                return m_mediaUrl;
            }

            set
            {
                m_mediaUrl = value;

                if (ParentGroup != null)
                    ParentGroup.HasGroupChanged = true;
            }
        }

        public bool CreateSelected
        {
            get { return m_createSelected; }
            set
            {
//                m_log.DebugFormat("[SOP]: Setting CreateSelected to {0} for {1} {2}", value, Name, UUID);
                m_createSelected = value;
            }
        }

        #endregion

//---------------
#region Public Properties with only Get

        public Vector3 AbsolutePosition
        {
            get
            {
                return GroupPosition + (m_offsetPosition * ParentGroup.RootPart.RotationOffset);
            }
        }

        public SceneObjectGroup ParentGroup
        {
            get { return m_parentGroup; }
            private set { m_parentGroup = value; }
        }

        public scriptEvents ScriptEvents
        {
            get { return AggregateScriptEvents; }
        }

        public Quaternion SitTargetOrientation
        {
            get { return m_sitTargetOrientation; }
            set
            {
                m_sitTargetOrientation = value;
//                m_log.DebugFormat("[SCENE OBJECT PART]: Set sit target orientation {0} for {1} {2}", m_sitTargetOrientation, Name, LocalId);
            }
        }

        public Vector3 SitTargetPosition
        {
            get { return m_sitTargetPosition; }
            set
            {
                m_sitTargetPosition = value;
//                m_log.DebugFormat("[SCENE OBJECT PART]: Set sit target position to {0} for {1} {2}", m_sitTargetPosition, Name, LocalId);
            }
        }

        // This sort of sucks, but I'm adding these in to make some of
        // the mappings more consistant.
        public Vector3 SitTargetPositionLL
        {
            get { return m_sitTargetPosition; }
            set { m_sitTargetPosition = value; }
        }

        public Quaternion SitTargetOrientationLL
        {
            get { return m_sitTargetOrientation; }
            set { m_sitTargetOrientation = value; }
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

        /// <summary>
        /// The parent ID of this part.
        /// </summary>
        /// <remarks>
        /// If this is a root part which is not attached to an avatar then the value will be 0.
        /// If this is a root part which is attached to an avatar then the value is the local id of that avatar.
        /// If this is a child part then the value is the local ID of the root part.
        /// </remarks>
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

        public UUID RezzerID
        {
            get { return _rezzerID; }
            set { _rezzerID = value; }
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

        /// <summary>
        /// Property flags.  See OpenMetaverse.PrimFlags
        /// </summary>
        /// <remarks>
        /// Example properties are PrimFlags.Phantom and PrimFlags.DieAtEdge
        /// </remarks>
        public PrimFlags Flags
        {
            get { return _flags; }
            set
            {
//                m_log.DebugFormat("[SOP]: Setting flags for {0} {1} to {2}", UUID, Name, value);
                _flags = value;
            }
        }

        [XmlIgnore]
        public bool IsOccupied				// KF If an av is sittingon this prim
        {
            get { return m_occupied; }
            set { m_occupied = value; }
        }

        /// <summary>
        /// ID of the avatar that is sat on us if we have a sit target.  If there is no such avatar then is UUID.Zero
        /// </summary>
        public UUID SitTargetAvatar { get; set; }

        /// <summary>
        /// IDs of all avatars sat on this part.
        /// </summary>
        /// <remarks>
        /// We need to track this so that we can stop sat upon prims from being attached.
        /// </remarks>
        /// <value>
        /// null if there are no sitting avatars.  This is to save us create a hashset for every prim in a scene.
        /// </value>
        private HashSet<ScenePresence> m_sittingAvatars;

        public virtual UUID RegionID
        {
            get
            {
                if (ParentGroup.Scene != null)
                    return ParentGroup.Scene.RegionInfo.RegionID;
                else
                    return UUID.Zero;
            }
            set {} // read only
        }

        private UUID _parentUUID = UUID.Zero;

        public UUID ParentUUID
        {
            get
            {
                if (ParentGroup != null)
                    _parentUUID = ParentGroup.UUID;

                return _parentUUID;
            }

            set { _parentUUID = value; }
        }

        public string SitAnimation
        {
            get { return m_sitAnimation; }
            set { m_sitAnimation = value; }
        }

        public UUID invalidCollisionSoundUUID = new UUID("ffffffff-ffff-ffff-ffff-ffffffffffff");

        // 0 for default collision sounds, -1 for script disabled sound 1 for script defined sound
        // runtime thing.. do not persist
        [XmlIgnore]
        public sbyte CollisionSoundType
        {
            get
            {
                return m_collisionSoundType;
            }
            set
            {
                m_collisionSoundType = value;
                if (value == -1)
                    m_collisionSound = invalidCollisionSoundUUID;
                else if (value == 0)
                    m_collisionSound = UUID.Zero;
            }
        }

        public UUID CollisionSound
        {
            get { return m_collisionSound; }
            set
            {
                m_collisionSound = value;

                if (value == invalidCollisionSoundUUID)
                    m_collisionSoundType = -1;
                else if (value == UUID.Zero)
                    m_collisionSoundType = 0;
                else
                    m_collisionSoundType = 1;

            }
        }

        public float CollisionSoundVolume
        {
            get { return m_collisionSoundVolume; }
            set { m_collisionSoundVolume = value; }
        }

        public float Buoyancy
        {
            get
            {
                if (ParentGroup.RootPart == this)
                    return m_buoyancy;

                return ParentGroup.RootPart.Buoyancy;
            }
            set
            {
                if (ParentGroup != null && ParentGroup.RootPart != null && ParentGroup.RootPart != this)
                {
                    ParentGroup.RootPart.Buoyancy = value;
                    return;
                }
                m_buoyancy = value;
                if (PhysActor != null)
                    PhysActor.Buoyancy = value;
            }
        }

        public Vector3 Force
        {
            get
            {
                if (ParentGroup.RootPart == this)
                    return m_force;

                return ParentGroup.RootPart.Force;
            }

            set
            {
                if (ParentGroup != null && ParentGroup.RootPart != null && ParentGroup.RootPart != this)
                {
                    ParentGroup.RootPart.Force = value;
                    return;
                }
                m_force = value;
                if (PhysActor != null)
                    PhysActor.Force = value;
            }
        }

        public Vector3 Torque
        {
            get
            {
                if (ParentGroup.RootPart == this)
                    return m_torque;

                return ParentGroup.RootPart.Torque;
            }

            set
            {
                if (ParentGroup != null && ParentGroup.RootPart != null && ParentGroup.RootPart != this)
                {
                    ParentGroup.RootPart.Torque = value;
                    return;
                }
                m_torque = value;
                if (PhysActor != null)
                    PhysActor.Torque = value;
            }
        }

        public byte Material
        {
            get { return (byte)m_material; }
            set
            {
                if (value >= 0 && value <= (byte)SOPMaterialData.MaxMaterial)
                {
                    bool update = false;

                    if (m_material != (Material)value)
                    {
                        update = true;
                        m_material = (Material)value;
                    }

                    if (m_friction != SOPMaterialData.friction(m_material))
                    {
                        update = true;
                        m_friction = SOPMaterialData.friction(m_material);
                    }

                    if (m_bounce != SOPMaterialData.bounce(m_material))
                    {
                        update = true;
                        m_bounce = SOPMaterialData.bounce(m_material);
                    }

                    if (update)
                    {
                        if (PhysActor != null)
                        {
                            PhysActor.SetMaterial((int)value);
                        }
                        if(ParentGroup != null)
                            ParentGroup.HasGroupChanged = true;
                        ScheduleFullUpdateIfNone();
                    }
                }
            }
        }

        // not a propriety to move to methods place later
        private bool HasMesh()
        {
            if (Shape != null && (Shape.SculptType == (byte)SculptType.Mesh))
                return true;
            return false;
        }

        // not a propriety to move to methods place later
        public byte DefaultPhysicsShapeType()
        {
            byte type;

            if (Shape != null && (Shape.SculptType == (byte)SculptType.Mesh))
                type = (byte)PhysShapeType.convex;
            else
                type = (byte)PhysShapeType.prim;

            return type;
        }

        [XmlIgnore]
        public bool UsesComplexCost
        {
            get
            {
                byte pst = PhysicsShapeType;
                if(pst == (byte) PhysShapeType.none || pst == (byte) PhysShapeType.convex || HasMesh())
                    return true;
                return false;
            }
        }

        [XmlIgnore]
        public float PhysicsCost
        {
            get
            {
                if(PhysicsShapeType == (byte)PhysShapeType.none)
                    return 0;

                float cost = 0.1f;
                if (PhysActor != null)
                    cost = PhysActor.PhysicsCost;
                else
                    cost = 0.1f;

                if ((Flags & PrimFlags.Physics) != 0)
                    cost *= (1.0f + 0.01333f * Scale.LengthSquared()); // 0.01333 == 0.04/3
                return cost;
            }
        }

        [XmlIgnore]
        public float StreamingCost
        {
            get
            {
                float cost;
                if (PhysActor != null)
                    cost = PhysActor.StreamCost;
                else
                    cost = 1.0f;
                return 1.0f;
            }
        }

        [XmlIgnore]
        public float SimulationCost
        {
            get
            {
                // ignoring scripts. Don't like considering them for this
                if((Flags & PrimFlags.Physics) != 0)
                    return 1.0f;

                return 0.5f;
            }
        }

        public byte PhysicsShapeType
        {
            get { return m_physicsShapeType; }
            set
            {
                byte oldv = m_physicsShapeType;

                if (value >= 0 && value <= (byte)PhysShapeType.convex)
                {
                    if (value == (byte)PhysShapeType.none && ParentGroup != null && ParentGroup.RootPart == this)
                        m_physicsShapeType = DefaultPhysicsShapeType();
                    else
                        m_physicsShapeType = value;
                }
                else
                    m_physicsShapeType = DefaultPhysicsShapeType();

                if (m_physicsShapeType != oldv && ParentGroup != null)
                {
                    if (m_physicsShapeType == (byte)PhysShapeType.none)
                    {
                        if (PhysActor != null)
                        {
                            ParentGroup.Scene.RemovePhysicalPrim(1);
                            RemoveFromPhysics();
//                            Stop();
                        }
                    }
                    else if (PhysActor == null)
                    {
                        if(oldv == (byte)PhysShapeType.none)
                        {
                            ApplyPhysics((uint)Flags, VolumeDetectActive, false);
                            UpdatePhysicsSubscribedEvents();
                        }
                    }
                    else
                        PhysActor.PhysicsShapeType = m_physicsShapeType;

                    ParentGroup.HasGroupChanged = true;
                }
            }
        }

        public float Density // in kg/m^3
        {
            get { return m_density; }
            set
            {
                if (value >=1 && value <= 22587.0)
                {
                    m_density = value;

                    ScheduleFullUpdateIfNone();

                    if (ParentGroup != null)
                        ParentGroup.HasGroupChanged = true;

                    PhysicsActor pa = PhysActor;
                    if (pa != null)
                        pa.Density = m_density;
                }
            }
        }

        public float GravityModifier
        {
            get { return m_gravitymod; }
            set
            {
                if( value >= -1 && value <=28.0f)
                {
                    m_gravitymod = value;

                    ScheduleFullUpdateIfNone();

                    if (ParentGroup != null)
                        ParentGroup.HasGroupChanged = true;

                    PhysicsActor pa = PhysActor;
                    if (pa != null)
                        pa.GravModifier = m_gravitymod;
                }
            }
        }

        public float Friction
        {
            get { return m_friction; }
            set
            {
                if (value >= 0 && value <= 255.0f)
                {
                    m_friction = value;

                    ScheduleFullUpdateIfNone();

                    if (ParentGroup != null)
                        ParentGroup.HasGroupChanged = true;

                    PhysicsActor pa = PhysActor;
                    if (pa != null)
                        pa.Friction = m_friction;
                }
            }
        }

        public float Restitution
        {
            get { return m_bounce; }
            set
            {
                if (value >= 0 && value <= 1.0f)
                {
                    m_bounce = value;

                    ScheduleFullUpdateIfNone();

                    if (ParentGroup != null)
                        ParentGroup.HasGroupChanged = true;

                    PhysicsActor pa = PhysActor;
                    if (pa != null)
                        pa.Restitution = m_bounce;
                }
            }
        }


        #endregion Public Properties with only Get

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
        public void ClearUpdateSchedule()
        {
            UpdateFlag = UpdateRequired.NONE;
        }

        /// <summary>
        /// Send this part's properties (name, description, inventory serial, base mask, etc.) to a client
        /// </summary>
        /// <param name="client"></param>
        public void SendPropertiesToClient(IClientAPI client)
        {
            client.SendObjectPropertiesReply(this);
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

        #region Public Methods

        public void ResetExpire()
        {
            Expires = DateTime.UtcNow + new TimeSpan(600000000);
        }

        public void AddFlag(PrimFlags flag)
        {
            // PrimFlags prevflag = Flags;
            if ((Flags & flag) == 0)
            {
                //m_log.Debug("Adding flag: " + ((PrimFlags) flag).ToString());
                Flags |= flag;

                if (flag == PrimFlags.TemporaryOnRez)
                    ResetExpire();
            }
            // m_log.Debug("Aprev: " + prevflag.ToString() + " curr: " + Flags.ToString());
        }

        public void AddNewParticleSystem(Primitive.ParticleSystem pSystem)
        {
            m_particleSystem = pSystem.GetBytes();
        }

        public void RemoveParticleSystem()
        {
            m_particleSystem = new byte[0];
        }

        public void AddTextureAnimation(Primitive.TextureAnimation pTexAnim)
        {
            byte[] data;

            if (pTexAnim.Flags == Primitive.TextureAnimMode.ANIM_OFF)
            {
                data = Utils.EmptyBytes;
            }
            else
            {
                data = new byte[16];
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

            }
            m_TextureAnimation = data;
        }

        public void AdjustSoundGain(double volume)
        {
            if (volume > 1)
                volume = 1;
            if (volume < 0)
                volume = 0;

            ParentGroup.Scene.ForEachRootClient(delegate(IClientAPI client)
            {
                client.SendAttachedSoundGainChange(UUID, (float)volume);
            });
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
            Vector3 impulse = impulsei;

            if (localGlobalTF)
            {
                Quaternion grot = GetWorldRotation();
                Quaternion AXgrot = grot;
                Vector3 AXimpulsei = impulsei;
                Vector3 newimpulse = AXimpulsei * AXgrot;
                impulse = newimpulse;
            }

            if (ParentGroup != null)
            {
                ParentGroup.applyImpulse(impulse);
            }
        }

        //      SetVelocity for LSL llSetVelocity..  may need revision if having other uses in future
        public void SetVelocity(Vector3 pVel, bool localGlobalTF)
        {
            if (ParentGroup == null || ParentGroup.IsDeleted)
                return;

            if (ParentGroup.IsAttachment)
                return;                         // don't work on attachments (for now ??)

            SceneObjectPart root = ParentGroup.RootPart;

            if (root.VehicleType != (int)Vehicle.TYPE_NONE) // don't mess with vehicles
                return;

            PhysicsActor pa = root.PhysActor;

            if (pa == null || !pa.IsPhysical)
                return;

            if (localGlobalTF)
            {
                pVel = pVel * GetWorldRotation();
            }

            ParentGroup.Velocity = pVel;
        }

        //      SetAngularVelocity for LSL llSetAngularVelocity..  may need revision if having other uses in future
        public void SetAngularVelocity(Vector3 pAngVel, bool localGlobalTF)
        {
            if (ParentGroup == null || ParentGroup.IsDeleted)
                return;

            if (ParentGroup.IsAttachment)
                return;                         // don't work on attachments (for now ??)

            SceneObjectPart root = ParentGroup.RootPart;

            if (root.VehicleType != (int)Vehicle.TYPE_NONE) // don't mess with vehicles
                return;

            PhysicsActor pa = root.PhysActor;

            if (pa == null || !pa.IsPhysical)
                return;

            if (localGlobalTF)
            {
                pAngVel = pAngVel * GetWorldRotation();
            }

            root.AngularVelocity = pAngVel;
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
            Vector3 impulse = impulsei;

            if (localGlobalTF)
            {
                Quaternion grot = GetWorldRotation();
                Quaternion AXgrot = grot;
                Vector3 AXimpulsei = impulsei;
                Vector3 newimpulse = AXimpulsei * AXgrot;
                impulse = newimpulse;
            }

            ParentGroup.ApplyAngularImpulse(impulse);
        }

        /// <summary>
        /// hook to the physics scene to apply angular impulse
        /// This is sent up to the group, which then finds the root prim
        /// and applies the force on the root prim of the group
        /// </summary>
        /// <param name="impulsei">Vector force</param>
        /// <param name="localGlobalTF">true for the local frame, false for the global frame</param>

        // this is actualy Set Torque.. keeping naming so not to edit lslapi also
        public void SetAngularImpulse(Vector3 torquei, bool localGlobalTF)
        {
            Vector3 torque = torquei;

            if (localGlobalTF)
            {
                torque *= GetWorldRotation();
            }

            Torque = torque;
        }

        /// <summary>
        /// Apply physics to this part.
        /// </summary>
        /// <param name="rootObjectFlags"></param>
        /// <param name="VolumeDetectActive"></param>
        /// <param name="building"></param>

        public void ApplyPhysics(uint _ObjectFlags, bool _VolumeDetectActive, bool building)
        {
            VolumeDetectActive = _VolumeDetectActive;

            if (!ParentGroup.Scene.CollidablePrims)
                return;

            if (PhysicsShapeType == (byte)PhysShapeType.none)
                return;

            bool isPhysical = (_ObjectFlags & (uint) PrimFlags.Physics) != 0;
            bool isPhantom = (_ObjectFlags & (uint)PrimFlags.Phantom) != 0;

            if (_VolumeDetectActive)
                isPhantom = true;

            if (IsJoint())
            {
                DoPhysicsPropertyUpdate(isPhysical, true);
            }
            else
            {
                if ((!isPhantom || isPhysical || _VolumeDetectActive)
                        && !ParentGroup.IsAttachmentCheckFull()
                        && !(Shape.PathCurve == (byte)Extrusion.Flexible))
                {
                    AddToPhysics(isPhysical, isPhantom, building, isPhysical);
                    UpdatePhysicsSubscribedEvents(); // not sure if appliable here
                }
                else
                {
                    PhysActor = null; // just to be sure
                    RemFlag(PrimFlags.CameraDecoupled);
                }
            }
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
        /// <param name="localID"></param>
        /// <param name="AgentID"></param>
        /// <param name="GroupID"></param>
        /// <param name="linkNum"></param>
        /// <param name="userExposed">True if the duplicate will immediately be in the scene, false otherwise</param>
        /// <returns></returns>
        public SceneObjectPart Copy(uint plocalID, UUID AgentID, UUID GroupID, int linkNum, bool userExposed)
        {
            // FIXME: This is dangerous since it's easy to forget to reset some references when necessary and end up
            // with bugs that only occur in some circumstances (e.g. crossing between regions on the same simulator
            // but not between regions on different simulators).  Really, all copying should be done explicitly.
            SceneObjectPart dupe = (SceneObjectPart)MemberwiseClone();

            dupe.m_shape = m_shape.Copy();
            dupe.m_regionHandle = m_regionHandle;
            if (userExposed)
                dupe.UUID = UUID.Random();

            dupe.PhysActor = null;

            dupe.OwnerID = AgentID;
            dupe.GroupID = GroupID;
            dupe.GroupPosition = GroupPosition;
            dupe.OffsetPosition = OffsetPosition;
            dupe.RotationOffset = RotationOffset;
            dupe.Velocity = Velocity;
            dupe.Acceleration = Acceleration;
            dupe.AngularVelocity = AngularVelocity;
            dupe.Flags = Flags;

            dupe.OwnershipCost = OwnershipCost;
            dupe.ObjectSaleType = ObjectSaleType;
            dupe.SalePrice = SalePrice;
            dupe.Category = Category;
            dupe.m_rezzed = m_rezzed;

            dupe.m_UndoRedo = null;
            dupe.m_isSelected = false;

            dupe.IgnoreUndoUpdate = false;
            dupe.Undoing = false;

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
            dupe.LocalId = plocalID;

            // This may be wrong...    it might have to be applied in SceneObjectGroup to the object that's being duplicated.
            if(OwnerID != GroupID)
                dupe.LastOwnerID = OwnerID;
            else
                dupe.LastOwnerID = LastOwnerID; // redundant ?

            dupe.RezzerID = RezzerID;

            byte[] extraP = new byte[Shape.ExtraParams.Length];
            Array.Copy(Shape.ExtraParams, extraP, extraP.Length);
            dupe.Shape.ExtraParams = extraP;

            dupe.m_sittingAvatars = new HashSet<ScenePresence>();
            dupe.SitTargetAvatar = UUID.Zero;
            // safeguard  actual copy is done in sog.copy
            dupe.KeyframeMotion = null;
            dupe.PayPrice = (int[])PayPrice.Clone();

            dupe.DynAttrs.CopyFrom(DynAttrs);

            if (userExposed)
            {
                bool UsePhysics = ((dupe.Flags & PrimFlags.Physics) != 0);
                dupe.DoPhysicsPropertyUpdate(UsePhysics, true);
            }

            if (dupe.PhysActor != null)
                dupe.PhysActor.LocalID = plocalID;

            ParentGroup.Scene.EventManager.TriggerOnSceneObjectPartCopy(dupe, this, userExposed);

//            m_log.DebugFormat("[SCENE OBJECT PART]: Clone of {0} {1} finished", Name, UUID);

            return dupe;
        }

        /// <summary>
        /// Do a physics property update for a NINJA joint.
        /// </summary>
        /// <param name="UsePhysics"></param>
        /// <param name="isNew"></param>
        protected void DoPhysicsPropertyUpdateForNinjaJoint(bool UsePhysics, bool isNew)
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

                SceneObjectPart trackedBody = ParentGroup.Scene.GetSceneObjectPart(trackedBodyName); // FIXME: causes a sequential lookup
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

                joint = ParentGroup.Scene.PhysicsScene.RequestJointCreation(Name, jointType,
                    AbsolutePosition,
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
                    ParentGroup.Scene.PhysicsScene.RequestJointDeletion(Name); // FIXME: what if the name changed?

                    // make sure client isn't interpolating the joint proxy object
                    Stop();
                }
            }
        }

        /// <summary>
        /// Do a physics propery update for this part.
        /// now also updates phantom and volume detector
        /// </summary>
        /// <param name="UsePhysics"></param>
        /// <param name="isNew"></param>
        public void DoPhysicsPropertyUpdate(bool UsePhysics, bool isNew)
        {
            if (ParentGroup.Scene == null)
                return;

            if (!ParentGroup.Scene.PhysicalPrims && UsePhysics)
                return;

            if (IsJoint())
            {
                DoPhysicsPropertyUpdateForNinjaJoint(UsePhysics, isNew);
            }
            else
            {
                PhysicsActor pa = PhysActor;

                if (pa != null)
                {
                    if (UsePhysics != pa.IsPhysical || isNew)
                    {
                        if (pa.IsPhysical) // implies UsePhysics==false for this block
                        {
                            if (!isNew)  // implies UsePhysics==false for this block
                            {
                                ParentGroup.Scene.RemovePhysicalPrim(1);

                                Velocity = new Vector3(0, 0, 0);
                                Acceleration = new Vector3(0, 0, 0);
                                AngularVelocity = new Vector3(0, 0, 0);
                                APIDActive = false;

                                if (pa.Phantom && !VolumeDetectActive)
                                {
                                    RemoveFromPhysics();
                                    return;
                                }

                                pa.IsPhysical = UsePhysics;
                                pa.OnRequestTerseUpdate -= PhysicsRequestingTerseUpdate;
                                pa.OnOutOfBounds -= PhysicsOutOfBounds;
                                pa.delink();
                                if (ParentGroup.Scene.PhysicsScene.SupportsNINJAJoints)
                                {
                                    // destroy all joints connected to this now deactivated body
                                    ParentGroup.Scene.PhysicsScene.RemoveAllJointsConnectedToActorThreadLocked(pa);
                                }
                            }
                        }

                        if (pa.IsPhysical != UsePhysics)
                            pa.IsPhysical = UsePhysics;

                        if (UsePhysics)
                        {
                            if (ParentGroup.RootPart.KeyframeMotion != null)
                                ParentGroup.RootPart.KeyframeMotion.Stop();
                            ParentGroup.RootPart.KeyframeMotion = null;
                            ParentGroup.Scene.AddPhysicalPrim(1);

                            PhysActor.OnRequestTerseUpdate += PhysicsRequestingTerseUpdate;
                            PhysActor.OnOutOfBounds += PhysicsOutOfBounds;

                            if (ParentID != 0 && ParentID != LocalId)
                            {
                                PhysicsActor parentPa = ParentGroup.RootPart.PhysActor;

                                if (parentPa != null)
                                {
                                    pa.link(parentPa);
                                }
                            }
                        }
                    }

                    bool phan = ((Flags & PrimFlags.Phantom) != 0);
                    if (pa.Phantom != phan)
                        pa.Phantom = phan;

// some engines dont' have this check still
//                    if (VolumeDetectActive != pa.IsVolumeDtc)
                    {
                        if (VolumeDetectActive)
                            pa.SetVolumeDetect(1);
                        else
                            pa.SetVolumeDetect(0);
                    }

                    // If this part is a sculpt then delay the physics update until we've asynchronously loaded the
                    // mesh data.
//                    if (Shape.SculptEntry)
//                        CheckSculptAndLoad();
//                    else
                        ParentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(pa);
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
            SceneObjectPart part = SceneObjectSerializer.Xml2ToSOP(xmlReader);

            // for tempOnRez objects, we have to fix the Expire date.
            if ((part.Flags & PrimFlags.TemporaryOnRez) != 0)
                part.ResetExpire();

            return part;
        }

        public bool GetDieAtEdge()
        {
            if (ParentGroup.IsDeleted)
                return false;

            return ParentGroup.RootPart.DIE_AT_EDGE;
        }

        public bool GetReturnAtEdge()
        {
            if (ParentGroup.IsDeleted)
                return false;

            return ParentGroup.RootPart.RETURN_AT_EDGE;
        }

        public void SetReturnAtEdge(bool p)
        {
            if (ParentGroup.IsDeleted)
                return;

            ParentGroup.RootPart.RETURN_AT_EDGE = p;
        }

        public void SetStatusSandbox(bool p)
        {
            if (ParentGroup.IsDeleted)
                return;
            StatusSandboxPos = ParentGroup.RootPart.AbsolutePosition;
            ParentGroup.RootPart.StatusSandbox = p;
        }

        public bool GetStatusSandbox()
        {
            if (ParentGroup.IsDeleted)
                return false;

            return ParentGroup.RootPart.StatusSandbox;
        }

        public int GetAxisRotation(int axis)
        {
           if (!ParentGroup.IsDeleted)
                return ParentGroup.GetAxisRotation(axis);

            return 0;
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
            // Commenting this section of code out since it doesn't actually do anything, as enums are handled by
            // value rather than reference
//            PrimFlags f = _flags;
//            if (m_parentGroup == null || m_parentGroup.RootPart == this)
//                f &= ~(PrimFlags.Touch | PrimFlags.Money);

            return (uint)Flags | (uint)LocalFlags;
        }

        // some of this lines need be moved to other place later

        // effective permitions considering only this part inventory contents perms
        public uint AggregatedInnerOwnerPerms {get; private set; }
        public uint AggregatedInnerGroupPerms {get; private set; }
        public uint AggregatedInnerEveryonePerms {get; private set; }
        private object InnerPermsLock = new object();

        public void AggregateInnerPerms()
        {
            // assuming child prims permissions masks are irrelevant on a linkset
            // root part is handle at SOG since its masks are the sog masks
            const uint mask = (uint)PermissionMask.AllEffective;

            uint owner = mask;
            uint group = mask;
            uint everyone = mask;

            lock(InnerPermsLock) // do we really need this?
            {
                if(Inventory != null)
                    Inventory.AggregateInnerPerms(ref owner, ref group, ref everyone);
            
                AggregatedInnerOwnerPerms = owner & mask;
                AggregatedInnerGroupPerms = group & mask;
                AggregatedInnerEveryonePerms = everyone & mask;
            }
        }

        public Vector3 GetGeometricCenter()
        {
            // this is not real geometric center but a average of positions relative to root prim acording to
            // http://wiki.secondlife.com/wiki/llGetGeometricCenter
            // ignoring tortured prims details since sl also seems to ignore
            // so no real use in doing it on physics
            if (ParentGroup.IsDeleted)
                return new Vector3(0, 0, 0);

            return ParentGroup.GetGeometricCenter();
        }

        public float GetMass()
        {
            PhysicsActor pa = PhysActor;

            if (pa != null)
                return pa.Mass;
            else
                return 0;
        }

        public Vector3 GetCenterOfMass()
        {
            if (ParentGroup.RootPart == this)
            {
                if (ParentGroup.IsDeleted)
                    return AbsolutePosition;
                return ParentGroup.GetCenterOfMass();
            }

            PhysicsActor pa = PhysActor;

            if (pa != null)
            {
                Vector3 tmp = pa.CenterOfMass;
                return tmp;
            }
            else
                return AbsolutePosition;
        }

        public Vector3 GetPartCenterOfMass()
        {
            PhysicsActor pa = PhysActor;

            if (pa != null)
            {
                Vector3 tmp = pa.CenterOfMass;
                return tmp;
            }
            else
                return AbsolutePosition;
        }


        public Vector3 GetForce()
        {
            return Force;
        }

        /// <summary>
        /// Method for a prim to get it's world position from the group.
        /// </summary>
        /// <remarks>
        /// Remember, the Group Position simply gives the position of the group itself
        /// </remarks>
        /// <returns>A Linked Child Prim objects position in world</returns>
        public Vector3 GetWorldPosition()
        {
            Vector3 ret;
            if (_parentID == 0)
                // if a root SOP, my position is what it is
                ret = GroupPosition;
            else
            {
                // If a child SOP, my position is relative to the root SOP so take
                //    my info and add the root's position and rotation to
                //    get my world position.
                Quaternion parentRot = ParentGroup.RootPart.RotationOffset;
                Vector3 translationOffsetPosition = OffsetPosition * parentRot;
                ret = ParentGroup.AbsolutePosition + translationOffsetPosition;
            }
            return ret;
        }

        /// <summary>
        /// Gets the rotation of this prim offset by the group rotation
        /// </summary>
        /// <returns></returns>
        public Quaternion GetWorldRotation()
        {
            Quaternion newRot;

            if (this.LinkNum == 0 || this.LinkNum == 1)
            {
                newRot = RotationOffset;
            }
            else
            {
                // A child SOP's rotation is relative to the root SOP's rotation.
                // Combine them to get my absolute rotation.
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
                ParentGroup.MoveToTarget(target, tau);
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
            ParentGroup.SetHoverHeight(height, hoverType, tau);
        }

        public void StopHover()
        {
            ParentGroup.SetHoverHeight(0f, PIDHoverType.Ground, 0f);
        }

        public virtual void OnGrab(Vector3 offsetPos, IClientAPI remoteClient)
        {
        }

        public bool CollisionFilteredOut(UUID objectID, string objectName)
        {
            if(CollisionFilter.Count == 0)
                return false;

            if (CollisionFilter.ContainsValue(objectID.ToString()) ||
                CollisionFilter.ContainsValue(objectID.ToString() + objectName) ||
                CollisionFilter.ContainsValue(UUID.Zero.ToString() + objectName))
            {
                if (CollisionFilter.ContainsKey(1))
                    return false;
                return true;
            }

            if (CollisionFilter.ContainsKey(1))
                return true;

            return false;
        }

        private DetectedObject CreateDetObject(SceneObjectPart obj)
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
            detobj.linkNumber = LinkNum;
            return detobj;
        }

        private DetectedObject CreateDetObject(ScenePresence av)
        {
            DetectedObject detobj = new DetectedObject();
            detobj.keyUUID = av.UUID;
            detobj.nameStr = av.ControllingClient.Name;
            detobj.ownerUUID = av.UUID;
            detobj.posVector = av.AbsolutePosition;
            detobj.rotQuat = av.Rotation;
            detobj.velVector = av.Velocity;
            detobj.colliderType = av.IsNPC ? 0x20 : 0x1; // OpenSim\Region\ScriptEngine\Shared\Helpers.cs
            if(av.IsSatOnObject)
                detobj.colliderType |= 0x4; //passive
            else if(detobj.velVector != Vector3.Zero)
                detobj.colliderType |= 0x2; //active
            detobj.groupUUID = av.ControllingClient.ActiveGroupId;
            detobj.linkNumber = LinkNum;

            return detobj;
        }

        private DetectedObject CreateDetObjectForGround()
        {
            DetectedObject detobj = new DetectedObject();
            detobj.keyUUID = UUID.Zero;
            detobj.nameStr = "";
            detobj.ownerUUID = UUID.Zero;
            detobj.posVector = ParentGroup.RootPart.AbsolutePosition;
            detobj.rotQuat = Quaternion.Identity;
            detobj.velVector = Vector3.Zero;
            detobj.colliderType = 0;
            detobj.groupUUID = UUID.Zero;
            detobj.linkNumber = LinkNum; // pass my link number not sure needed.. but no harm

            return detobj;
        }

        private ColliderArgs CreateColliderArgs(SceneObjectPart dest, List<uint> colliders)
        {
            ColliderArgs colliderArgs = new ColliderArgs();
            List<DetectedObject> colliding = new List<DetectedObject>();
            foreach (uint localId in colliders)
            {
                if (localId == 0)
                    continue;

                SceneObjectPart obj = ParentGroup.Scene.GetSceneObjectPart(localId);
                if (obj != null)
                {
                    if (!dest.CollisionFilteredOut(obj.UUID, obj.Name))
                        colliding.Add(CreateDetObject(obj));
                }
                else
                {
                    ScenePresence av = ParentGroup.Scene.GetScenePresence(localId);
                    if (av != null && (!av.IsChildAgent))
                    {
                        if (!dest.CollisionFilteredOut(av.UUID, av.Name))
                            colliding.Add(CreateDetObject(av));
                    }
                }
            }

            colliderArgs.Colliders = colliding;

            return colliderArgs;
        }

        private delegate void ScriptCollidingNotification(uint localID, ColliderArgs message);

        private void SendCollisionEvent(scriptEvents ev, List<uint> colliders, ScriptCollidingNotification notify)
        {
            bool sendToRoot = false;
            ColliderArgs CollidingMessage;

            if (colliders.Count > 0)
            {
                if ((ScriptEvents & ev) != 0)
                {
                    CollidingMessage = CreateColliderArgs(this, colliders);

                    if (CollidingMessage.Colliders.Count > 0)
                        notify(LocalId, CollidingMessage);

                    if (PassCollisions)
                        sendToRoot = true;
                }
                else
                {
                    if ((ParentGroup.RootPart.ScriptEvents & ev) != 0)
                        sendToRoot = true;
                }
                if (sendToRoot && ParentGroup.RootPart != this)
                {
                    CollidingMessage = CreateColliderArgs(ParentGroup.RootPart, colliders);
                    if (CollidingMessage.Colliders.Count > 0)
                        notify(ParentGroup.RootPart.LocalId, CollidingMessage);
                }
            }
        }

        private void SendLandCollisionEvent(scriptEvents ev, ScriptCollidingNotification notify)
        {
            bool sendToRoot = true;

            ColliderArgs LandCollidingMessage = new ColliderArgs();
            List<DetectedObject> colliding = new List<DetectedObject>();

            colliding.Add(CreateDetObjectForGround());
            LandCollidingMessage.Colliders = colliding;

            if (Inventory.ContainsScripts())
            {
                if (!PassCollisions)
                    sendToRoot = false;
            }
            if ((ScriptEvents & ev) != 0)
                notify(LocalId, LandCollidingMessage);

            if ((ParentGroup.RootPart.ScriptEvents & ev) != 0 && sendToRoot)
            {
                notify(ParentGroup.RootPart.LocalId, LandCollidingMessage);
            }
        }

        public void PhysicsCollision(EventArgs e)
        {
            if (ParentGroup.Scene == null || ParentGroup.IsDeleted)
                return;

            // this a thread from physics ( heartbeat )

            CollisionEventUpdate a = (CollisionEventUpdate)e;
            Dictionary<uint, ContactPoint> collissionswith = a.m_objCollisionList;
            List<uint> thisHitColliders = new List<uint>();
            List<uint> endedColliders = new List<uint>();
            List<uint> startedColliders = new List<uint>();

            if (collissionswith.Count == 0)
            {
                if (m_lastColliders.Count == 0)
                    return; // nothing to do

                foreach (uint localID in m_lastColliders)
                {
                    endedColliders.Add(localID);
                }
                m_lastColliders.Clear();
            }
            else
            {
                List<CollisionForSoundInfo> soundinfolist = new List<CollisionForSoundInfo>();

                // calculate things that started colliding this time
                // and build up list of colliders this time
                if (!VolumeDetectActive && CollisionSoundType >= 0)
                {
                    CollisionForSoundInfo soundinfo;
                    ContactPoint curcontact;

                    foreach (uint id in collissionswith.Keys)
                    {
                        thisHitColliders.Add(id);
                        if (!m_lastColliders.Contains(id))
                        {
                            startedColliders.Add(id);

                            curcontact = collissionswith[id];
                            if (Math.Abs(curcontact.RelativeSpeed) > 0.2)
                            {
                                soundinfo = new CollisionForSoundInfo();
                                soundinfo.colliderID = id;
                                soundinfo.position = curcontact.Position;
                                soundinfo.relativeVel = curcontact.RelativeSpeed;
                                soundinfolist.Add(soundinfo);
                            }
                        }
                    }
                }
                else
                {
                    foreach (uint id in collissionswith.Keys)
                    {
                        thisHitColliders.Add(id);
                        if (!m_lastColliders.Contains(id))
                            startedColliders.Add(id);
                    }
                }

                // calculate things that ended colliding
                foreach (uint localID in m_lastColliders)
                {
                    if (!thisHitColliders.Contains(localID))
                        endedColliders.Add(localID);
                }

                //add the items that started colliding this time to the last colliders list.
                foreach (uint localID in startedColliders)
                    m_lastColliders.Add(localID);

                // remove things that ended colliding from the last colliders list
                foreach (uint localID in endedColliders)
                    m_lastColliders.Remove(localID);

                // play sounds.
                if (soundinfolist.Count > 0)
                    CollisionSounds.PartCollisionSound(this, soundinfolist);
            }

            SendCollisionEvent(scriptEvents.collision_start, startedColliders, ParentGroup.Scene.EventManager.TriggerScriptCollidingStart);
            if (!VolumeDetectActive)
                SendCollisionEvent(scriptEvents.collision  , m_lastColliders , ParentGroup.Scene.EventManager.TriggerScriptColliding);
            SendCollisionEvent(scriptEvents.collision_end  , endedColliders  , ParentGroup.Scene.EventManager.TriggerScriptCollidingEnd);

            if (startedColliders.Contains(0))
                SendLandCollisionEvent(scriptEvents.land_collision_start, ParentGroup.Scene.EventManager.TriggerScriptLandCollidingStart);
            if (m_lastColliders.Contains(0))
                SendLandCollisionEvent(scriptEvents.land_collision, ParentGroup.Scene.EventManager.TriggerScriptLandColliding);
            if (endedColliders.Contains(0))
                SendLandCollisionEvent(scriptEvents.land_collision_end, ParentGroup.Scene.EventManager.TriggerScriptLandCollidingEnd);
        }

        // The Collision sounds code calls this
        public void SendCollisionSound(UUID soundID, double volume, Vector3 position)
        {
            if (soundID == UUID.Zero)
                return;

            ISoundModule soundModule = ParentGroup.Scene.RequestModuleInterface<ISoundModule>();
            if (soundModule == null)
                return;

            if (volume > 1)
                volume = 1;
            if (volume < 0)
                volume = 0;

            int now = Util.EnvironmentTickCount();
            if(Util.EnvironmentTickCountSubtract(now,LastColSoundSentTime) <200)
                return;

            LastColSoundSentTime = now;

            UUID ownerID = OwnerID;
            UUID objectID = ParentGroup.RootPart.UUID;
            UUID parentID = ParentGroup.UUID;
            ulong regionHandle = ParentGroup.Scene.RegionInfo.RegionHandle;

            soundModule.TriggerSound(soundID, ownerID, objectID, parentID, volume, position, regionHandle, 0 );
        }

        public void PhysicsOutOfBounds(Vector3 pos)
        {
            // Note: This is only being called on the root prim at this time.

            m_log.ErrorFormat(
                "[SCENE OBJECT PART]: Physical object {0}, localID {1} went out of bounds at {2} in {3}.  Stopping at {4} and making non-physical.",
                Name, LocalId, pos, ParentGroup.Scene.Name, AbsolutePosition);

            RemFlag(PrimFlags.Physics);
            DoPhysicsPropertyUpdate(false, true);
        }

        public void PhysicsRequestingTerseUpdate()
        {
            PhysicsActor pa = PhysActor;

            if (pa != null)
            {
                Vector3 newpos = pa.Position;
                if (!ParentGroup.Scene.PositionIsInCurrentRegion(newpos))
                {
                    // Setting position outside current region will start region crossing
                    ParentGroup.AbsolutePosition = newpos;
                    return;
                }
                //ParentGroup.RootPart.m_groupPosition = newpos;
            }
/*
            if (pa != null && ParentID != 0 && ParentGroup != null)
            {
                // Special case where a child object is requesting property updates.
                // This happens when linksets are modified to use flexible links rather than
                //    the default links.
                // The simulator code presumes that child parts are only modified by scripts
                //    so the logic for changing position/rotation/etc does not take into
                //    account the physical object actually moving.
                // This code updates the offset position and rotation of the child and then
                //    lets the update code push the update to the viewer.
                // Since physics engines do not normally generate this event for linkset children,
                //    this code will not be active unless you have a specially configured
                //    physics engine.
                Quaternion invRootRotation = Quaternion.Normalize(Quaternion.Inverse(ParentGroup.RootPart.RotationOffset));
                m_offsetPosition = pa.Position - m_groupPosition;
                RotationOffset = pa.Orientation * invRootRotation;
                // m_log.DebugFormat("{0} PhysicsRequestingTerseUpdate child: pos={1}, rot={2}, offPos={3}, offRot={4}",
                //                     "[SCENE OBJECT PART]", pa.Position, pa.Orientation, m_offsetPosition, RotationOffset);
            }
*/
            ScheduleTerseUpdate();
        }

        public void RemFlag(PrimFlags flag)
        {
            // PrimFlags prevflag = Flags;
            if ((Flags & flag) != 0)
            {
                //m_log.Debug("Removing flag: " + ((PrimFlags)flag).ToString());
                Flags &= ~flag;
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
        /// Set the scale of this part.
        /// </summary>
        /// <remarks>
        /// Unlike the scale property, this checks the new size against scene limits and schedules a full property
        /// update to viewers.
        /// </remarks>
        /// <param name="scale"></param>
        public void Resize(Vector3 scale)
        {
            PhysicsActor pa = PhysActor;

            if (ParentGroup.Scene != null)
            {
                float minsize = ParentGroup.Scene.m_minNonphys;
                float maxsize = ParentGroup.Scene.m_maxNonphys;
                if (pa != null && pa.IsPhysical)
                    {
                    minsize = ParentGroup.Scene.m_minPhys;
                    maxsize = ParentGroup.Scene.m_maxPhys;
                    }
                scale.X = Util.Clamp(scale.X, minsize, maxsize);
                scale.Y = Util.Clamp(scale.Y, minsize, maxsize);
                scale.Z = Util.Clamp(scale.Z, minsize, maxsize);
            }
//            m_log.DebugFormat("[SCENE OBJECT PART]: Resizing {0} {1} to {2}", Name, LocalId, scale);

            Scale = scale;

            ParentGroup.HasGroupChanged = true;
            ScheduleFullUpdate();
        }

        public void RotLookAt(Quaternion target, float strength, float damping)
        {
            if(ParentGroup.IsDeleted)
                return;

            // for now we only handle physics case
            if(!ParentGroup.UsesPhysics || ParentGroup.IsAttachment)
                return;

            // physical is SOG
            if(ParentGroup.RootPart != this)
            {
                ParentGroup.RotLookAt(target, strength, damping);
                return;
            }

            APIDDamp = damping;
            APIDStrength = strength;
            APIDTarget = target;

            if (APIDStrength <= 0)
            {
                m_log.WarnFormat("[SceneObjectPart] Invalid rotation strength {0}",APIDStrength);
                return;
            }

            APIDActive = true;

            // Necessary to get the lookat deltas applied
            ParentGroup.QueueForUpdateCheck();
        }

        public void StartLookAt(Quaternion target, float strength, float damping)
        {
            if(ParentGroup.IsDeleted)
                return;

            // non physical is done on LSL
            if(ParentGroup.IsAttachment || !ParentGroup.UsesPhysics)
                return;

            // physical is SOG
            if(ParentGroup.RootPart != this)
                ParentGroup.RotLookAt(target, strength, damping);
            else
                RotLookAt(target,strength,damping);
        }

        public void StopLookAt()
        {
            if(ParentGroup.IsDeleted)
                return;

            if(ParentGroup.RootPart != this && ParentGroup.UsesPhysics)
                 ParentGroup.StopLookAt();

            // just in case do this always
            if(APIDActive)
                AngularVelocity = Vector3.Zero;

            APIDActive = false;
        }

        public void ScheduleFullUpdateIfNone()
        {
            if (ParentGroup == null)
                return;

// ???            ParentGroup.HasGroupChanged = true;

            if (UpdateFlag != UpdateRequired.FULL)
                ScheduleFullUpdate();
        }

        /// <summary>
        /// Schedules this prim for a full update
        /// </summary>
        public void ScheduleFullUpdate()
        {
//            m_log.DebugFormat("[SCENE OBJECT PART]: Scheduling full update for {0} {1}", Name, LocalId);

            if (ParentGroup == null)
                return;

            ParentGroup.QueueForUpdateCheck();

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

            UpdateFlag = UpdateRequired.FULL;

            //            m_log.DebugFormat(
            //                "[SCENE OBJECT PART]: Scheduling full  update for {0}, {1} at {2}",
            //                UUID, Name, TimeStampFull);

            if (ParentGroup.Scene != null)
                ParentGroup.Scene.EventManager.TriggerSceneObjectPartUpdated(this, true);
        }

        /// <summary>
        /// Schedule a terse update for this prim.  Terse updates only send position,
        /// rotation, velocity and rotational velocity information.
        /// </summary>
        public void ScheduleTerseUpdate()
        {
            if (ParentGroup == null)
                return;

            // This was pulled from SceneViewer. Attachments always receive full updates.
            // This is needed because otherwise if only the root prim changes position, then
            // it looks as if the entire object has moved (including the other prims).
            if (ParentGroup.IsAttachment)
            {
                ScheduleFullUpdate();
                return;
            }

            if (UpdateFlag == UpdateRequired.NONE)
            {
                ParentGroup.HasGroupChanged = true;
                ParentGroup.QueueForUpdateCheck();

                TimeStampTerse = (uint) Util.UnixTimeSinceEpoch();
                UpdateFlag = UpdateRequired.TERSE;

            //                m_log.DebugFormat(
            //                    "[SCENE OBJECT PART]: Scheduling terse update for {0}, {1} at {2}",
            //                    UUID, Name, TimeStampTerse);
            }

            if (ParentGroup.Scene != null)
                ParentGroup.Scene.EventManager.TriggerSceneObjectPartUpdated(this, false);
        }

        public void ScriptSetPhysicsStatus(bool UsePhysics)
        {
            ParentGroup.ScriptSetPhysicsStatus(UsePhysics);
        }

        /// <summary>
        /// Send a full update to the client for the given part
        /// </summary>
        /// <param name="remoteClient"></param>
        protected internal void SendFullUpdate(IClientAPI remoteClient)
        {
            if (ParentGroup == null)
                return;

//            m_log.DebugFormat(
//                "[SOG]: Sendinging part full update to {0} for {1} {2}", remoteClient.Name, part.Name, part.LocalId);


            if (ParentGroup.IsAttachment)
            {
                ScenePresence sp = ParentGroup.Scene.GetScenePresence(ParentGroup.AttachedAvatar);
                if (sp != null)
                {
                    sp.SendAttachmentUpdate(this, UpdateRequired.FULL);
                }
            }

/* this does nothing
SendFullUpdateToClient(remoteClient, Position) ignores position parameter
            if (IsRoot)
            {
                if (ParentGroup.IsAttachment)
                {
                    SendFullUpdateToClient(remoteClient, AttachedPos);
                }
                else
                {
                    SendFullUpdateToClient(remoteClient, AbsolutePosition);
                }
            }
*/
            else
            {
                SendFullUpdateToClient(remoteClient);
            }
        }

        /// <summary>
        /// Send a full update for this part to all clients.
        /// </summary>
        public void SendFullUpdateToAllClientsInternal()
        {
            if (ParentGroup == null)
                return;

            // Update the "last" values
            m_lastPosition = OffsetPosition;
            m_lastRotation = RotationOffset;
            m_lastVelocity = Velocity;
            m_lastAcceleration = Acceleration;
            m_lastAngularVelocity = AngularVelocity;
            m_lastUpdateSentTime = Environment.TickCount;

            ParentGroup.Scene.ForEachScenePresence(delegate(ScenePresence avatar)
            {
                SendFullUpdate(avatar.ControllingClient);
            });
        }

        public void SendFullUpdateToAllClients()
        {
            if (ParentGroup == null)
                return;

            // Update the "last" values
            m_lastPosition = OffsetPosition;
            m_lastRotation = RotationOffset;
            m_lastVelocity = Velocity;
            m_lastAcceleration = Acceleration;
            m_lastAngularVelocity = AngularVelocity;
            m_lastUpdateSentTime = Environment.TickCount;

            if (ParentGroup.IsAttachment)
            {
                ScenePresence sp = ParentGroup.Scene.GetScenePresence(ParentGroup.AttachedAvatar);
                if (sp != null)
                {
                    sp.SendAttachmentUpdate(this, UpdateRequired.FULL);
                }
            }
            else
            {
                ParentGroup.Scene.ForEachScenePresence(delegate(ScenePresence avatar)
                {
                    SendFullUpdate(avatar.ControllingClient);
                });
            }
        }

        /// <summary>
        /// Sends a full update to the client
        /// </summary>
        /// <param name="remoteClient"></param>
        public void SendFullUpdateToClient(IClientAPI remoteClient)
        {
            SendFullUpdateToClient(remoteClient, OffsetPosition);
        }

        /// <summary>
        /// Sends a full update to the client
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="lPos"></param>
        public void SendFullUpdateToClient(IClientAPI remoteClient, Vector3 lPos)
        {
            if (ParentGroup == null)
                return;

            // Suppress full updates during attachment editing
            // sl Does send them
 //           if (ParentGroup.IsSelected && ParentGroup.IsAttachment)
 //               return;

            if (ParentGroup.IsDeleted)
                return;

            if (ParentGroup.IsAttachment
                && ParentGroup.AttachedAvatar != remoteClient.AgentId
                && ParentGroup.HasPrivateAttachmentPoint)
                return;

            if (remoteClient.AgentId == OwnerID)
            {
                if ((Flags & PrimFlags.CreateSelected) != 0)
                    Flags &= ~PrimFlags.CreateSelected;
            }
            //bool isattachment = IsAttachment;
            //if (LocalId != ParentGroup.RootPart.LocalId)
                //isattachment = ParentGroup.RootPart.IsAttachment;

            remoteClient.SendEntityUpdate(this, PrimUpdateFlags.FullUpdate);
            ParentGroup.Scene.StatsReporter.AddObjectUpdates(1);
        }

        /// <summary>
        /// Tell all the prims which have had updates scheduled
        /// </summary>
        public void SendScheduledUpdates()
        {
            const float ROTATION_TOLERANCE = 0.01f;
            const float VELOCITY_TOLERANCE = 0.001f;
            const float POSITION_TOLERANCE = 0.05f; // I don't like this, but I suppose it's necessary
            const int TIME_MS_TOLERANCE = 200; //llSetPos has a 200ms delay. This should NOT be 3 seconds.

            switch (UpdateFlag)
            {
                case UpdateRequired.TERSE:
                {
                    ClearUpdateSchedule();
                    // Throw away duplicate or insignificant updates
                    if (!RotationOffset.ApproxEquals(m_lastRotation, ROTATION_TOLERANCE) ||
                        !Acceleration.Equals(m_lastAcceleration) ||
                        !Velocity.ApproxEquals(m_lastVelocity, VELOCITY_TOLERANCE) ||
                        Velocity.ApproxEquals(Vector3.Zero, VELOCITY_TOLERANCE) ||
                        !AngularVelocity.ApproxEquals(m_lastAngularVelocity, VELOCITY_TOLERANCE) ||
                        !OffsetPosition.ApproxEquals(m_lastPosition, POSITION_TOLERANCE) ||
                        Environment.TickCount - m_lastUpdateSentTime > TIME_MS_TOLERANCE)
                    {
                        SendTerseUpdateToAllClientsInternal();
                    }
                    break;
                }
                case UpdateRequired.FULL:
                {
                    ClearUpdateSchedule();
                    SendFullUpdateToAllClientsInternal();
                    break;
                }
            }
        }


        /// <summary>
        /// Send a terse update to all clients
        /// </summary>
        public void SendTerseUpdateToAllClientsInternal()
        {
            if (ParentGroup == null || ParentGroup.Scene == null)
                return;

            // Update the "last" values
            m_lastPosition = OffsetPosition;
            m_lastRotation = RotationOffset;
            m_lastVelocity = Velocity;
            m_lastAcceleration = Acceleration;
            m_lastAngularVelocity = AngularVelocity;
            m_lastUpdateSentTime = Environment.TickCount;

            ParentGroup.Scene.ForEachClient(delegate(IClientAPI client)
            {
                SendTerseUpdateToClient(client);
            });
        }

        public void SendTerseUpdateToAllClients()
        {
            if (ParentGroup == null || ParentGroup.Scene == null)
                return;

            // Update the "last" values
            m_lastPosition = OffsetPosition;
            m_lastRotation = RotationOffset;
            m_lastVelocity = Velocity;
            m_lastAcceleration = Acceleration;
            m_lastAngularVelocity = AngularVelocity;
            m_lastUpdateSentTime = Environment.TickCount;

            if (ParentGroup.IsAttachment)
            {
                ScenePresence sp = ParentGroup.Scene.GetScenePresence(ParentGroup.AttachedAvatar);
                if (sp != null)
                {
                    sp.SendAttachmentUpdate(this, UpdateRequired.TERSE);
                }
            }
            else
            {
                ParentGroup.Scene.ForEachClient(delegate(IClientAPI client)
                {
                    SendTerseUpdateToClient(client);
                });
            }
        }

        public void SetAxisRotation(int axis, int rotate)
        {
            ParentGroup.SetAxisRotation(axis, rotate);

            //Cannot use ScriptBaseClass constants as no referance to it currently.
            if ((axis & (int)SceneObjectGroup.axisSelect.STATUS_ROTATE_X) != 0)
                STATUS_ROTATE_X = rotate;

            if ((axis & (int)SceneObjectGroup.axisSelect.STATUS_ROTATE_Y) != 0)
                STATUS_ROTATE_Y = rotate;

            if ((axis & (int)SceneObjectGroup.axisSelect.STATUS_ROTATE_Z) != 0)
                STATUS_ROTATE_Z = rotate;
        }

        public void SetBuoyancy(float fvalue)
        {
            Buoyancy = fvalue;
/*
            if (PhysActor != null)
            {
                PhysActor.Buoyancy = fvalue;
            }
 */
        }

        public void SetDieAtEdge(bool p)
        {
            if (ParentGroup.IsDeleted)
                return;

            ParentGroup.RootPart.DIE_AT_EDGE = p;
        }

        public void SetFloatOnWater(int floatYN)
        {
            PhysicsActor pa = PhysActor;

            if (pa != null)
                pa.FloatOnWater = (floatYN == 1);
        }

        public void SetForce(Vector3 force)
        {
            Force = force;
        }

        public SOPVehicle VehicleParams
        {
            get
            {
                return m_vehicleParams;
            }
            set
            {
                m_vehicleParams = value;

            }
        }

        public int VehicleType
        {
            get
            {
                if (m_vehicleParams == null)
                    return (int)Vehicle.TYPE_NONE;
                else
                    return (int)m_vehicleParams.Type;
            }
            set
            {
                SetVehicleType(value);
            }
        }

        public void SetVehicleType(int type)
        {
                m_vehicleParams = null;

                if (type == (int)Vehicle.TYPE_NONE)
                {
                    if (_parentID ==0 && PhysActor != null)
                        PhysActor.VehicleType = (int)Vehicle.TYPE_NONE;
                    return;
                }
                m_vehicleParams = new SOPVehicle();
                m_vehicleParams.ProcessTypeChange((Vehicle)type);
                {
                    if (_parentID ==0 && PhysActor != null)
                        PhysActor.VehicleType = type;
                    return;
                }
        }

        public void SetVehicleFlags(int param, bool remove)
        {
            if (m_vehicleParams == null)
                return;

            m_vehicleParams.ProcessVehicleFlags(param, remove);

            if (_parentID == 0 && PhysActor != null)
            {
                PhysActor.VehicleFlags(param, remove);
            }
        }

        public void SetVehicleFloatParam(int param, float value)
        {
            if (m_vehicleParams == null)
                return;

            m_vehicleParams.ProcessFloatVehicleParam((Vehicle)param, value);

            if (_parentID == 0 && PhysActor != null)
            {
                PhysActor.VehicleFloatParam(param, value);
            }
        }

        public void SetVehicleVectorParam(int param, Vector3 value)
        {
            if (m_vehicleParams == null)
                return;

            m_vehicleParams.ProcessVectorVehicleParam((Vehicle)param, value);

            if (_parentID == 0 && PhysActor != null)
            {
                PhysActor.VehicleVectorParam(param, value);
            }
        }

        public void SetVehicleRotationParam(int param, Quaternion rotation)
        {
            if (m_vehicleParams == null)
                return;

            m_vehicleParams.ProcessRotationVehicleParam((Vehicle)param, rotation);

            if (_parentID == 0 && PhysActor != null)
            {
                PhysActor.VehicleRotationParam(param, rotation);
            }
        }

        /// <summary>
        /// Set the color & alpha of prim faces
        /// </summary>
        /// <param name="face"></param>
        /// <param name="color"></param>
        /// <param name="alpha"></param>
        public void SetFaceColorAlpha(int face, Vector3 color, double ?alpha)
        {
            Vector3 clippedColor = Util.Clip(color, 0.0f, 1.0f);
            float clippedAlpha = alpha.HasValue ?
                Util.Clip((float)alpha.Value, 0.0f, 1.0f) : 0;

            // The only way to get a deep copy/ If we don't do this, we can
            // never detect color changes further down.
            Byte[] buf = Shape.Textures.GetBytes();
            Primitive.TextureEntry tex = new Primitive.TextureEntry(buf, 0, buf.Length);
            Color4 texcolor;
            if (face >= 0 && face < GetNumberOfSides())
            {
                texcolor = tex.CreateFace((uint)face).RGBA;
                texcolor.R = clippedColor.X;
                texcolor.G = clippedColor.Y;
                texcolor.B = clippedColor.Z;
                if (alpha.HasValue)
                {
                    texcolor.A = clippedAlpha;
                }
                tex.FaceTextures[face].RGBA = texcolor;
                UpdateTextureEntry(tex.GetBytes());
                return;
            }
            else if (face == ALL_SIDES)
            {
                for (uint i = 0; i < GetNumberOfSides(); i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        texcolor = tex.FaceTextures[i].RGBA;
                        texcolor.R = clippedColor.X;
                        texcolor.G = clippedColor.Y;
                        texcolor.B = clippedColor.Z;
                        if (alpha.HasValue)
                        {
                            texcolor.A = clippedAlpha;
                        }
                        tex.FaceTextures[i].RGBA = texcolor;
                    }
                    texcolor = tex.DefaultTexture.RGBA;
                    texcolor.R = clippedColor.X;
                    texcolor.G = clippedColor.Y;
                    texcolor.B = clippedColor.Z;
                    if (alpha.HasValue)
                    {
                        texcolor.A = clippedAlpha;
                    }
                    tex.DefaultTexture.RGBA = texcolor;
                }
                UpdateTextureEntry(tex.GetBytes());
                return;
            }
        }

        /// <summary>
        /// Get the number of sides that this part has.
        /// </summary>
        /// <returns></returns>
        public int GetNumberOfSides()
        {
            int ret = 0;
            bool hasCut;
            bool hasHollow;
            bool hasDimple;
            bool hasProfileCut;

            PrimType primType = GetPrimType();
            HasCutHollowDimpleProfileCut(primType, Shape, out hasCut, out hasHollow, out hasDimple, out hasProfileCut);

            switch (primType)
            {
                case PrimType.BOX:
                    ret = 6;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.CYLINDER:
                    ret = 3;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.PRISM:
                    ret = 5;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.SPHERE:
                    ret = 1;
                    if (hasCut) ret += 2;
                    if (hasDimple) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.TORUS:
                    ret = 1;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.TUBE:
                    ret = 4;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.RING:
                    ret = 3;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case PrimType.SCULPT:
                    // Special mesh handling
                    if (Shape.SculptType == (byte)SculptType.Mesh)
                        ret = 8; // if it's a mesh then max 8 faces
                    else
                        ret = 1; // if it's a sculpt then max 1 face
                    break;
            }

            return ret;
        }

        /// <summary>
        /// Tell us what type this prim is
        /// </summary>
        /// <param name="primShape"></param>
        /// <returns></returns>
        public PrimType GetPrimType()
        {
            if (Shape.SculptEntry)
                return PrimType.SCULPT;

            if ((Shape.ProfileCurve & 0x07) == (byte)ProfileShape.Square)
            {
                if (Shape.PathCurve == (byte)Extrusion.Straight)
                    return PrimType.BOX;
                else if (Shape.PathCurve == (byte)Extrusion.Curve1)
                    return PrimType.TUBE;
            }
            else if ((Shape.ProfileCurve & 0x07) == (byte)ProfileShape.Circle)
            {
                if (Shape.PathCurve == (byte)Extrusion.Straight || Shape.PathCurve == (byte)Extrusion.Flexible)
                    return PrimType.CYLINDER;
                // ProfileCurve seems to combine hole shape and profile curve so we need to only compare against the lower 3 bits
                else if (Shape.PathCurve == (byte)Extrusion.Curve1)
                    return PrimType.TORUS;
            }
            else if ((Shape.ProfileCurve & 0x07) == (byte)ProfileShape.HalfCircle)
            {
                if (Shape.PathCurve == (byte)Extrusion.Curve1 || Shape.PathCurve == (byte)Extrusion.Curve2)
                    return PrimType.SPHERE;
            }
            else if ((Shape.ProfileCurve & 0x07) == (byte)ProfileShape.EquilateralTriangle)
            {
                if (Shape.PathCurve == (byte)Extrusion.Straight || Shape.PathCurve == (byte)Extrusion.Flexible)
                    return PrimType.PRISM;
                else if (Shape.PathCurve == (byte)Extrusion.Curve1)
                    return PrimType.RING;
            }

            return PrimType.BOX;
        }

        /// <summary>
        /// Tell us if this object has cut, hollow, dimple, and other factors affecting the number of faces
        /// </summary>
        /// <param name="primType"></param>
        /// <param name="shape"></param>
        /// <param name="hasCut"></param>
        /// <param name="hasHollow"></param>
        /// <param name="hasDimple"></param>
        /// <param name="hasProfileCut"></param>
        protected static void HasCutHollowDimpleProfileCut(PrimType primType, PrimitiveBaseShape shape, out bool hasCut, out bool hasHollow,
            out bool hasDimple, out bool hasProfileCut)
        {
            if (primType == PrimType.BOX
                ||
                primType == PrimType.CYLINDER
                ||
                primType == PrimType.PRISM)

                hasCut = (shape.ProfileBegin > 0) || (shape.ProfileEnd > 0);
            else
                hasCut = (shape.PathBegin > 0) || (shape.PathEnd > 0);

            hasHollow = shape.ProfileHollow > 0;
            hasDimple = (shape.ProfileBegin > 0) || (shape.ProfileEnd > 0); // taken from llSetPrimitiveParms
            hasProfileCut = hasDimple; // is it the same thing?
        }

        public void SetGroup(UUID groupID, IClientAPI client)
        {
            // Scene.AddNewPrims() calls with client == null so can't use this.
//            m_log.DebugFormat(
//                "[SCENE OBJECT PART]: Setting group for {0} to {1} for {2}",
//                Name, groupID, OwnerID);

            GroupID = groupID;
//            if (client != null)
//                SendPropertiesToClient(client);
            UpdateFlag = UpdateRequired.FULL;
        }

        /// <summary>
        /// Set the parent group of this prim.
        /// </summary>
        public void SetParent(SceneObjectGroup parent)
        {
            ParentGroup = parent;
        }

        // Use this for attachments!  LocalID should be avatar's localid
        public void SetParentLocalId(uint localID)
        {
            ParentID = localID;
        }

        public void SetPhysicsAxisRotation()
        {
            PhysicsActor pa = PhysActor;

            if (pa != null)
            {
                pa.LockAngularMotion(RotationAxisLocks);
                ParentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(pa);
            }
        }

        /// <summary>
        /// Set the events that this part will pass on to listeners.
        /// </summary>
        /// <param name="scriptid"></param>
        /// <param name="events"></param>
        public void SetScriptEvents(UUID scriptid, int events)
        {
//            m_log.DebugFormat(
//                "[SCENE OBJECT PART]: Set script events for script with id {0} on {1}/{2} to {3} in {4}",
//                scriptid, Name, ParentGroup.Name, events, ParentGroup.Scene.Name);

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

            if (ParentGroup != null)
            {
                ParentGroup.HasGroupChanged = true;
                ScheduleFullUpdate();
            }
        }

        /// <summary>
        /// Set the text displayed for this part.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="color"></param>
        /// <param name="alpha"></param>
        public void SetText(string text, Vector3 color, double alpha)
        {
            Color = Color.FromArgb((int) (alpha*0xff),
                                   (int) (color.X*0xff),
                                   (int) (color.Y*0xff),
                                   (int) (color.Z*0xff));
            SetText(text);
        }

        public void StopMoveToTarget()
        {
            ParentGroup.StopMoveToTarget();
        }

        public void StoreUndoState(ObjectChangeType change)
        {
            lock (m_UndoLock)
            {
                if (m_UndoRedo == null)
                    m_UndoRedo = new UndoRedoState(5);

                if (!Undoing && !IgnoreUndoUpdate && ParentGroup != null) // just to read better  - undo is in progress, or suspended
                {
                    m_UndoRedo.StoreUndo(this, change);
                }
            }
        }

        /// <summary>
        /// Return number of undos on the stack.  Here temporarily pending a refactor.
        /// </summary>
        public int UndoCount
        {
            get
            {
                if (m_UndoRedo == null)
                    return 0;
                return m_UndoRedo.Count;
            }
        }

        public void Undo()
        {
            lock (m_UndoLock)
            {
                if (m_UndoRedo == null || Undoing || ParentGroup == null)
                    return;

                Undoing = true;
                m_UndoRedo.Undo(this);
                Undoing = false;
            }
        }

        public void Redo()
        {
            lock (m_UndoLock)
            {
                if (m_UndoRedo == null || Undoing || ParentGroup == null)
                    return;

                Undoing = true;
                m_UndoRedo.Redo(this);
                Undoing = false;
            }
        }

        public void ClearUndoState()
        {
            lock (m_UndoLock)
            {
                if (m_UndoRedo == null || Undoing)
                    return;

                m_UndoRedo.Clear();
            }
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
                        result.face = i;
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
        public void ToXml(XmlTextWriter xmlWriter)
        {
            SceneObjectSerializer.SOPToXml2(xmlWriter, this, new Dictionary<string, object>());
        }

        public void TriggerScriptChangedEvent(Changed val)
        {
            if (ParentGroup != null && ParentGroup.Scene != null)
                ParentGroup.Scene.EventManager.TriggerOnScriptChangedEvent(LocalId, (uint)val);
        }

        public void TrimPermissions()
        {
            BaseMask &= (uint)(PermissionMask.All | PermissionMask.Export);
            OwnerMask &= (uint)(PermissionMask.All | PermissionMask.Export);
            GroupMask &= (uint)PermissionMask.All;
            EveryoneMask &= (uint)(PermissionMask.All | PermissionMask.Export);
            NextOwnerMask &= (uint)PermissionMask.All;
        }

        public void UpdateExtraParam(ushort type, bool inUse, byte[] data)
        {
            m_shape.ReadInUpdateExtraParam(type, inUse, data);

            if (ParentGroup != null)
            {
                ParentGroup.HasGroupChanged = true;
                ScheduleFullUpdate();
            }
        }

        public void UpdateGroupPosition(Vector3 newPos)
        {
            Vector3 oldPos = GroupPosition;

            if ((newPos.X != oldPos.X) ||
                (newPos.Y != oldPos.Y) ||
                (newPos.Z != oldPos.Z))
            {
                GroupPosition = newPos;
                ScheduleTerseUpdate();
            }
        }

        /// <summary>
        /// Update this part's offset position.
        /// </summary>
        /// <param name="pos"></param>
        public void UpdateOffSet(Vector3 newPos)
        {
            Vector3 oldPos = OffsetPosition;

            if ((newPos.X != oldPos.X) ||
                (newPos.Y != oldPos.Y) ||
                (newPos.Z != oldPos.Z))
            {
                if (ParentGroup.RootPart.GetStatusSandbox())
                {
                    if (Util.GetDistanceTo(ParentGroup.RootPart.StatusSandboxPos, newPos) > 10)
                    {
                        ParentGroup.RootPart.ScriptSetPhysicsStatus(false);
                        newPos = OffsetPosition;
                        ParentGroup.Scene.SimChat(Utils.StringToBytes("Hit Sandbox Limit"),
                              ChatTypeEnum.DebugChannel, 0x7FFFFFFF, ParentGroup.RootPart.AbsolutePosition, Name, UUID, false);
                    }
                }

                OffsetPosition = newPos;
                ScheduleTerseUpdate();
            }
        }

        /// <summary>
        /// Update permissions on the SOP. Should only be called from SOG.UpdatePermissions because the SOG
        /// will handle the client notifications once all of its parts are updated.
        /// </summary>
        /// <param name="AgentID"></param>
        /// <param name="field"></param>
        /// <param name="localID"></param>
        /// <param name="mask"></param>
        /// <param name="addRemTF"></param>
        public void UpdatePermissions(UUID AgentID, byte field, uint localID, uint mask, byte addRemTF)
        {
            bool set = addRemTF == 1;
            bool god = ParentGroup.Scene.Permissions.IsGod(AgentID);

            uint baseMask = BaseMask;
            if (god)
                baseMask = 0x7ffffff0;

            // Are we the owner?
            if ((AgentID == OwnerID) || god)
            {
                switch (field)
                {
                    case 1:
                        if (god)
                        {
                            BaseMask = ApplyMask(BaseMask, set, mask);
                            Inventory.ApplyGodPermissions(BaseMask);
                        }

                        break;
                    case 2:
                        OwnerMask = ApplyMask(OwnerMask, set, mask) &
                                baseMask;
                        break;
                    case 4:
                        GroupMask = ApplyMask(GroupMask, set, mask) &
                                baseMask;
                        break;
                    case 8:
                        // Trying to set export permissions - extra checks
                        if (set && (mask & (uint)PermissionMask.Export) != 0)
                        {
                            if ((OwnerMask & (uint)PermissionMask.Export) == 0 || (BaseMask & (uint)PermissionMask.Export) == 0 || (NextOwnerMask & (uint)PermissionMask.All) != (uint)PermissionMask.All)
                                mask &= ~(uint)PermissionMask.Export;
                        }
                        EveryoneMask = ApplyMask(EveryoneMask, set, mask) &
                                baseMask;
                        break;
                    case 16:
                        // Force full perm if export
                        if ((EveryoneMask & (uint)PermissionMask.Export) != 0)
                        {
                            NextOwnerMask = (uint)PermissionMask.All;
                            break;
                        }
                        NextOwnerMask = ApplyMask(NextOwnerMask, set, mask) &
                                baseMask;
                        // Prevent the client from creating no copy, no transfer
                        // objects
                        if ((NextOwnerMask & (uint)PermissionMask.Copy) == 0)
                            NextOwnerMask |= (uint)PermissionMask.Transfer;

                        NextOwnerMask |= (uint)PermissionMask.Move;

                        break;
                }
                AggregateInnerPerms();
                SendFullUpdateToAllClients();
            }
        }

        public void ClonePermissions(SceneObjectPart source)
        {
            uint prevOwnerMask = OwnerMask;
            uint prevGroupMask = GroupMask;
            uint prevEveryoneMask = EveryoneMask;
            uint prevNextOwnerMask = NextOwnerMask;

            OwnerMask = source.OwnerMask & BaseMask;
            GroupMask = source.GroupMask & BaseMask;
            EveryoneMask = source.EveryoneMask & BaseMask;
            NextOwnerMask = source.NextOwnerMask & BaseMask;

            AggregateInnerPerms();

            if (OwnerMask != prevOwnerMask ||
                GroupMask != prevGroupMask ||
                EveryoneMask != prevEveryoneMask ||
                NextOwnerMask != prevNextOwnerMask)
                SendFullUpdateToAllClients();
        }

        public bool IsHingeJoint()
        {
            // For now, we use the NINJA naming scheme for identifying joints.
            // In the future, we can support other joint specification schemes such as a
            // custom checkbox in the viewer GUI.
            if (ParentGroup.Scene != null && ParentGroup.Scene.PhysicsScene.SupportsNINJAJoints)
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
            if (ParentGroup.Scene != null && ParentGroup.Scene.PhysicsScene.SupportsNINJAJoints)
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
            if (ParentGroup.Scene != null && ParentGroup.Scene.PhysicsScene != null && ParentGroup.Scene.PhysicsScene.SupportsNINJAJoints)
            {
                return IsHingeJoint() || IsBallJoint();
            }
            else
            {
                return false;
            }
        }

        public void UpdateExtraPhysics(ExtraPhysicsData physdata)
        {
            if (physdata.PhysShapeType == PhysShapeType.invalid || ParentGroup == null)
                return;

            byte newtype = (byte)physdata.PhysShapeType;
            if (PhysicsShapeType != newtype)
                PhysicsShapeType = newtype;

            if(Density != physdata.Density)
                Density = physdata.Density;

            if(GravityModifier != physdata.GravitationModifier)
                GravityModifier = physdata.GravitationModifier;

            if(Friction != physdata.Friction)
                Friction = physdata.Friction;

            if(Restitution != physdata.Bounce)
                Restitution = physdata.Bounce;
        }
        /// <summary>
        /// Update the flags on this prim.  This covers properties such as phantom, physics and temporary.
        /// </summary>
        /// <param name="UsePhysics"></param>
        /// <param name="SetTemporary"></param>
        /// <param name="SetPhantom"></param>
        /// <param name="SetVD"></param>
        public void UpdatePrimFlags(bool UsePhysics, bool SetTemporary, bool SetPhantom, bool SetVD, bool building)
        {
            bool wasUsingPhysics = ((Flags & PrimFlags.Physics) != 0);
            bool wasTemporary = ((Flags & PrimFlags.TemporaryOnRez) != 0);
            bool wasPhantom = ((Flags & PrimFlags.Phantom) != 0);
            bool wasVD = VolumeDetectActive;

            if ((UsePhysics == wasUsingPhysics) && (wasTemporary == SetTemporary) && (wasPhantom == SetPhantom) && (SetVD == wasVD))
                return;

            VolumeDetectActive = SetVD;

            // volume detector implies phantom we need to decouple this mess
            if (SetVD)
                SetPhantom = true;
            else if(wasVD)
                SetPhantom = false;

            if (UsePhysics)
                AddFlag(PrimFlags.Physics);
            else
                RemFlag(PrimFlags.Physics);

            if (SetPhantom)
                AddFlag(PrimFlags.Phantom);
            else
                RemFlag(PrimFlags.Phantom);

            if (SetTemporary)
                AddFlag(PrimFlags.TemporaryOnRez);
            else
                RemFlag(PrimFlags.TemporaryOnRez);

            if (ParentGroup.Scene == null)
                return;

            PhysicsActor pa = PhysActor;

            if (pa != null && building && pa.Building != building)
                pa.Building = building;

            if ((SetPhantom && !UsePhysics && !SetVD) ||  ParentGroup.IsAttachment || PhysicsShapeType == (byte)PhysShapeType.none
                || (Shape.PathCurve == (byte)Extrusion.Flexible))
            {
                if (pa != null)
                {
                    if(wasUsingPhysics)
                        ParentGroup.Scene.RemovePhysicalPrim(1);
                    RemoveFromPhysics();
                }

                Stop();
            }

            else
            {
                if (ParentGroup.Scene.CollidablePrims)
                {
                    if (pa == null)
                    {
                        AddToPhysics(UsePhysics, SetPhantom, building, false);
                        pa = PhysActor;

                        if (pa != null)
                        {
                            pa.SetMaterial(Material);
                            DoPhysicsPropertyUpdate(UsePhysics, true);
                        }
                    }
                    else // it already has a physical representation
                    {

                        DoPhysicsPropertyUpdate(UsePhysics, false); // Update physical status.

                        if(UsePhysics && !SetPhantom &&  m_localId == ParentGroup.RootPart.LocalId &&
                            m_vehicleParams != null && m_vehicleParams.CameraDecoupled)
                        AddFlag(PrimFlags.CameraDecoupled);
                        else
                            RemFlag(PrimFlags.CameraDecoupled);

                        if (pa.Building != building)
                            pa.Building = building;
                    }

                    UpdatePhysicsSubscribedEvents();
                }
            }

            // and last in case we have a new actor and not building

            if (ParentGroup != null)
            {
                ParentGroup.HasGroupChanged = true;
                ScheduleFullUpdate();
            }

//            m_log.DebugFormat("[SCENE OBJECT PART]: Updated PrimFlags on {0} {1} to {2}", Name, LocalId, Flags);
        }

        /// <summary>
        /// Adds this part to the physics scene.
        /// and sets the PhysActor property
        /// </summary>
        /// <param name="isPhysical">Add this prim as physical.</param>
        /// <param name="isPhantom">Add this prim as phantom.</param>
        /// <param name="building">tells physics to delay full construction of object</param>
        /// <param name="applyDynamics">applies velocities, force and torque</param>
        private void AddToPhysics(bool isPhysical, bool isPhantom, bool building, bool applyDynamics)
        {
            PhysicsActor pa;

            Vector3 velocity = Velocity;
            Vector3 rotationalVelocity = AngularVelocity;;

            try
            {
                pa = ParentGroup.Scene.PhysicsScene.AddPrimShape(
                                 string.Format("{0}/{1}", Name, UUID),
                                 Shape,
                                 AbsolutePosition,
                                 Scale,
                                 GetWorldRotation(),
                                 isPhysical,
                                 isPhantom,
                                 PhysicsShapeType,
                                 m_localId);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[SCENE]: caught exception meshing object {0}. Object set to phantom. e={1}", m_uuid, e);
                pa = null;
            }

            if (pa != null)
            {
                pa.SOPName = this.Name; // save object into the PhysActor so ODE internals know the joint/body info
                pa.SetMaterial(Material);

                pa.Density = Density;
                pa.GravModifier = GravityModifier;
                pa.Friction = Friction;
                pa.Restitution = Restitution;
                pa.Buoyancy = Buoyancy;

                if(LocalId == ParentGroup.RootPart.LocalId)
                {
                    pa.LockAngularMotion(RotationAxisLocks);
                }

                if (VolumeDetectActive) // change if not the default only
                    pa.SetVolumeDetect(1);

                if (m_vehicleParams != null && m_localId == ParentGroup.RootPart.LocalId)
                {
                    m_vehicleParams.SetVehicle(pa);
                    if(isPhysical && !isPhantom && m_vehicleParams.CameraDecoupled)
                        AddFlag(PrimFlags.CameraDecoupled);
                    else
                        RemFlag(PrimFlags.CameraDecoupled);
                }
                else
                    RemFlag(PrimFlags.CameraDecoupled);
                // we are going to tell rest of code about physics so better have this here
                PhysActor = pa;

                //                DoPhysicsPropertyUpdate(isPhysical, true);
                // lets expand it here just with what it really needs to do

                if (isPhysical)
                {
                    if (ParentGroup.RootPart.KeyframeMotion != null)
                        ParentGroup.RootPart.KeyframeMotion.Stop();
                    ParentGroup.RootPart.KeyframeMotion = null;
                    ParentGroup.Scene.AddPhysicalPrim(1);

                    pa.OnRequestTerseUpdate += PhysicsRequestingTerseUpdate;
                    pa.OnOutOfBounds += PhysicsOutOfBounds;

                    if (ParentID != 0 && ParentID != LocalId)
                    {
                        PhysicsActor parentPa = ParentGroup.RootPart.PhysActor;

                        if (parentPa != null)
                        {
                            pa.link(parentPa);
                        }
                    }
                }

                if (applyDynamics && LocalId == ParentGroup.RootPart.LocalId)
                    // do independent of isphysical so parameters get setted (at least some)
                {
                    Velocity = velocity;
                    AngularVelocity = rotationalVelocity;

                    // if not vehicle and root part apply force and torque
                    if ((m_vehicleParams == null || m_vehicleParams.Type == Vehicle.TYPE_NONE))
                    {
                        pa.Force = Force;
                        pa.Torque = Torque;
                    }
                }

//                if (Shape.SculptEntry)
//                    CheckSculptAndLoad();
//                else
                    ParentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(pa);

                if (!building)
                    pa.Building = false;
            }

            PhysActor = pa;

            ParentGroup.Scene.EventManager.TriggerObjectAddedToPhysicalScene(this);
        }

        /// <summary>
        /// This removes the part from the physics scene.
        /// </summary>
        /// <remarks>
        /// This isn't the same as turning off physical, since even without being physical the prim has a physics
        /// representation for collision detection.
        /// </remarks>
        public void RemoveFromPhysics()
        {
            PhysicsActor pa = PhysActor;
            if (pa != null)
            {
                pa.OnCollisionUpdate -= PhysicsCollision;
                pa.OnRequestTerseUpdate -= PhysicsRequestingTerseUpdate;
                pa.OnOutOfBounds -= PhysicsOutOfBounds;

                ParentGroup.Scene.PhysicsScene.RemovePrim(pa);

                ParentGroup.Scene.EventManager.TriggerObjectRemovedFromPhysicalScene(this);
            }
            RemFlag(PrimFlags.CameraDecoupled);
            PhysActor = null;
        }

        /// <summary>
        /// This updates the part's rotation and sends out an update to clients if necessary.
        /// </summary>
        /// <param name="rot"></param>
        public void UpdateRotation(Quaternion rot)
        {
            if (rot != RotationOffset)
            {
                RotationOffset = rot;

                if (ParentGroup != null)
                {
                    ParentGroup.HasGroupChanged = true;
                    ScheduleTerseUpdate();
                }
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

            PhysicsActor pa = PhysActor;

            if (pa != null)
            {
                pa.Shape = m_shape;
                ParentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(pa);
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
            TriggerScriptChangedEvent(Changed.SHAPE);
            ScheduleFullUpdate();
        }

        public void UpdateSlice(float begin, float end)
        {
            if (end < begin)
            {
                float temp = begin;
                begin = end;
                end = temp;
            }
            end = Math.Min(1f, Math.Max(0f, end));
            begin = Math.Min(Math.Min(1f, Math.Max(0f, begin)), end - 0.02f);
            if (begin < 0.02f && end < 0.02f)
            {
                begin = 0f;
                end = 0.02f;
            }

            ushort uBegin = (ushort)(50000.0 * begin);
            ushort uEnd = (ushort)(50000.0 * (1f - end));
            bool updatePossiblyNeeded = false;
            PrimType primType = GetPrimType();
            if (primType == PrimType.SPHERE || primType == PrimType.TORUS || primType == PrimType.TUBE || primType == PrimType.RING)
            {
                if (m_shape.ProfileBegin != uBegin || m_shape.ProfileEnd != uEnd)
                {
                    m_shape.ProfileBegin = uBegin;
                    m_shape.ProfileEnd = uEnd;
                    updatePossiblyNeeded = true;
                }
            }
            else if (m_shape.PathBegin != uBegin || m_shape.PathEnd != uEnd)
            {
                m_shape.PathBegin = uBegin;
                m_shape.PathEnd = uEnd;
                updatePossiblyNeeded = true;
            }

            if (updatePossiblyNeeded && ParentGroup != null)
            {
                ParentGroup.HasGroupChanged = true;
            }
            if (updatePossiblyNeeded && PhysActor != null)
            {
                PhysActor.Shape = m_shape;
                ParentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
            }
            if (updatePossiblyNeeded)
            {
                ScheduleFullUpdate();
            }
        }

        /// <summary>
        /// Update the texture entry for this part.
        /// </summary>
        /// <param name="serializedTextureEntry"></param>
        public void UpdateTextureEntry(byte[] serializedTextureEntry)
        {
            UpdateTextureEntry(new Primitive.TextureEntry(serializedTextureEntry, 0, serializedTextureEntry.Length));
        }

        /// <summary>
        /// Update the texture entry for this part.
        /// </summary>
        /// <param name="newTex"></param>
        public void UpdateTextureEntry(Primitive.TextureEntry newTex)
        {
            Primitive.TextureEntry oldTex = Shape.Textures;

            Changed changeFlags = 0;

            Primitive.TextureEntryFace fallbackNewFace = newTex.DefaultTexture;
            Primitive.TextureEntryFace fallbackOldFace = oldTex.DefaultTexture;

            // On Incoming packets, sometimes newText.DefaultTexture is null.  The assumption is that all
            // other prim-sides are set, but apparently that's not always the case.  Lets assume packet/data corruption at this point.
            if (fallbackNewFace == null)
            {
                fallbackNewFace = new Primitive.TextureEntry(Util.BLANK_TEXTURE_UUID).CreateFace(0);
                newTex.DefaultTexture = fallbackNewFace;
            }
            if (fallbackOldFace == null)
            {
                fallbackOldFace = new Primitive.TextureEntry(Util.BLANK_TEXTURE_UUID).CreateFace(0);
                oldTex.DefaultTexture = fallbackOldFace;
            }

            // Materials capable viewers can send a ObjectImage packet
            // when nothing in TE has changed. MaterialID should be updated
            // by the RenderMaterials CAP handler, so updating it here may cause a
            // race condtion. Therefore, if no non-materials TE fields have changed,
            // we should ignore any changes and not update Shape.TextureEntry

            bool otherFieldsChanged = false;

            for (int i = 0 ; i < GetNumberOfSides(); i++)
            {

                Primitive.TextureEntryFace newFace = newTex.DefaultTexture;
                Primitive.TextureEntryFace oldFace = oldTex.DefaultTexture;

                if (oldTex.FaceTextures[i] != null)
                    oldFace = oldTex.FaceTextures[i];
                if (newTex.FaceTextures[i] != null)
                    newFace = newTex.FaceTextures[i];

                Color4 oldRGBA = oldFace.RGBA;
                Color4 newRGBA = newFace.RGBA;

                if (oldRGBA.R != newRGBA.R ||
                    oldRGBA.G != newRGBA.G ||
                    oldRGBA.B != newRGBA.B ||
                    oldRGBA.A != newRGBA.A)
                    changeFlags |= Changed.COLOR;

                if (oldFace.TextureID != newFace.TextureID)
                    changeFlags |= Changed.TEXTURE;

                // Max change, skip the rest of testing
                if (changeFlags == (Changed.TEXTURE | Changed.COLOR))
                    break;

                if (!otherFieldsChanged)
                {
                    if (oldFace.Bump != newFace.Bump) otherFieldsChanged = true;
                    if (oldFace.Fullbright != newFace.Fullbright) otherFieldsChanged = true;
                    if (oldFace.Glow != newFace.Glow) otherFieldsChanged = true;
                    if (oldFace.MediaFlags != newFace.MediaFlags) otherFieldsChanged = true;
                    if (oldFace.OffsetU != newFace.OffsetU) otherFieldsChanged = true;
                    if (oldFace.OffsetV != newFace.OffsetV) otherFieldsChanged = true;
                    if (oldFace.RepeatU != newFace.RepeatU) otherFieldsChanged = true;
                    if (oldFace.RepeatV != newFace.RepeatV) otherFieldsChanged = true;
                    if (oldFace.Rotation != newFace.Rotation) otherFieldsChanged = true;
                    if (oldFace.Shiny != newFace.Shiny) otherFieldsChanged = true;
                    if (oldFace.TexMapType != newFace.TexMapType) otherFieldsChanged = true;
                }
            }

            if (changeFlags != 0 || otherFieldsChanged)
            {
                m_shape.TextureEntry = newTex.GetBytes();
                if (changeFlags != 0)
                    TriggerScriptChangedEvent(changeFlags);
                UpdateFlag = UpdateRequired.FULL;
                ParentGroup.HasGroupChanged = true;

                //This is madness..
                //ParentGroup.ScheduleGroupForFullUpdate();
                //This is sparta
                ScheduleFullUpdate();
            }
        }


        internal void UpdatePhysicsSubscribedEvents()
        {
            PhysicsActor pa = PhysActor;
            if (pa == null)
                return;

            pa.OnCollisionUpdate -= PhysicsCollision;

            bool hassound = (!VolumeDetectActive && CollisionSoundType >= 0 && ((Flags & PrimFlags.Physics) != 0));

            scriptEvents CombinedEvents = AggregateScriptEvents;

            // merge with root part
            if (ParentGroup != null && ParentGroup.RootPart != null)
                CombinedEvents |= ParentGroup.RootPart.AggregateScriptEvents;

            // submit to this part case
            if (VolumeDetectActive)
                CombinedEvents &= PhyscicsVolumeDtcSubsEvents;
            else if ((Flags & PrimFlags.Phantom) != 0)
                CombinedEvents &= PhyscicsPhantonSubsEvents;
            else
                CombinedEvents &= PhysicsNeededSubsEvents;

            if (hassound || CombinedEvents != 0)
            {
                // subscribe to physics updates.
                pa.OnCollisionUpdate += PhysicsCollision;
                pa.SubscribeEvents(50); // 20 reports per second
            }
            else
            {
                pa.UnSubscribeEvents();
            }
        }


        public void aggregateScriptEvents()
        {
            if (ParentGroup == null || ParentGroup.RootPart == null)
                return;

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

            LocalFlags = (PrimFlags)objectflagupdate;

            if (ParentGroup != null && ParentGroup.RootPart == this)
            {
                ParentGroup.aggregateScriptEvents();
            }
            else
            {
//                m_log.DebugFormat(
//                    "[SCENE OBJECT PART]: Scheduling part {0} {1} for full update in aggregateScriptEvents()", Name, LocalId);
                UpdatePhysicsSubscribedEvents();
                ScheduleFullUpdate();
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
            if (ParentGroup.IsDeleted)
                return;

            if (ParentGroup.IsAttachment
                && (ParentGroup.RootPart != this
                    || ParentGroup.AttachedAvatar != remoteClient.AgentId && ParentGroup.HasPrivateAttachmentPoint))
                return;

            // Causes this thread to dig into the Client Thread Data.
            // Remember your locking here!
            remoteClient.SendEntityUpdate(
                this,
                PrimUpdateFlags.Position | PrimUpdateFlags.Rotation | PrimUpdateFlags.Velocity
                    | PrimUpdateFlags.Acceleration | PrimUpdateFlags.AngularVelocity);

            ParentGroup.Scene.StatsReporter.AddObjectUpdates(1);
        }

        public void AddScriptLPS(int count)
        {
            ParentGroup.AddScriptLPS(count);
        }

        /// <summary>
        /// Sets a prim's owner and permissions when it's rezzed.
        /// </summary>
        /// <param name="item">The inventory item from which the item was rezzed</param>
        /// <param name="userInventory">True: the item is being rezzed from the user's inventory. False: from a prim's inventory.</param>
        /// <param name="scene">The scene the prim is being rezzed into</param>
        public void ApplyPermissionsOnRez(InventoryItemBase item, bool userInventory, Scene scene)
        {
            if ((OwnerID != item.Owner) || ((item.CurrentPermissions & (uint)PermissionMask.Slam) != 0) || ((item.Flags & (uint)InventoryItemFlags.ObjectSlamPerm) != 0))
            {
                if (scene.Permissions.PropagatePermissions())
                {
                    if ((item.Flags & (uint)InventoryItemFlags.ObjectHasMultipleItems) == 0)
                    {
                        // Apply the item's permissions to the object
                        //LogPermissions("Before applying item permissions");
                        if (userInventory)
                        {
                            EveryoneMask = item.EveryOnePermissions;
                            NextOwnerMask = item.NextPermissions;
                        }
                        else
                        {
                            if ((item.Flags & (uint)InventoryItemFlags.ObjectOverwriteEveryone) != 0)
                                EveryoneMask = item.EveryOnePermissions;
                            if ((item.Flags & (uint)InventoryItemFlags.ObjectOverwriteNextOwner) != 0)
                                NextOwnerMask = item.NextPermissions;
                            if ((item.Flags & (uint)InventoryItemFlags.ObjectOverwriteGroup) != 0)
                                GroupMask = item.GroupPermissions;
                        }
                        //LogPermissions("After applying item permissions");
                    }
                }

                GroupMask = 0; // DO NOT propagate here
            }

            if (OwnerID != item.Owner)
            {
                if(OwnerID != GroupID)
                    LastOwnerID = OwnerID;
                OwnerID = item.Owner;
                Inventory.ChangeInventoryOwner(item.Owner);

                if (scene.Permissions.PropagatePermissions())
                    ApplyNextOwnerPermissions();
            }
        }

        /// <summary>
        /// Logs the prim's permissions. Useful when debugging permission problems.
        /// </summary>
        /// <param name="message"></param>
        private void LogPermissions(String message)
        {
            PermissionsUtil.LogPermissions(Name, message, BaseMask, OwnerMask, NextOwnerMask);
        }

        public void ApplyNextOwnerPermissions()
        {
            // Export needs to be preserved in the base and everyone
            // mask, but removed in the owner mask as a next owner
            // can never change the export status
            BaseMask &= NextOwnerMask | (uint)PermissionMask.Export;
            OwnerMask &= NextOwnerMask;
            EveryoneMask &= NextOwnerMask | (uint)PermissionMask.Export;
            GroupMask = 0; // Giving an object zaps group permissions

            Inventory.ApplyNextOwnerPermissions();
            AggregateInnerPerms();
        }

        public void UpdateLookAt()
        {
            try
            {
                if (APIDActive)
                {
                    PhysicsActor pa = ParentGroup.RootPart.PhysActor;
                    if (pa == null || !pa.IsPhysical || APIDStrength < 0.04)
                    {
                        StopLookAt();
                        return;
                    }

                    Quaternion currRot = GetWorldRotation();
                    currRot.Normalize();

                    // difference between current orientation and desired orientation
                    Quaternion dR = currRot / APIDTarget;

                    // find axis and angle of rotation to rotate to desired orientation
                    Vector3 axis = Vector3.UnitX;
                    float angle;
                    dR.GetAxisAngle(out axis, out angle);
                    axis = axis * currRot;

                    // clamp strength to avoid overshoot
                    float strength = 1.0f / APIDStrength;
                    if (strength > 1.0) strength = 1.0f;

                    // set angular velocity to rotate to desired orientation
                    // with velocity proportional to strength and angle
                    AngularVelocity = axis * angle * strength * (float)Math.PI;

                    // This ensures that we'll check this object on the next iteration
                    ParentGroup.QueueForUpdateCheck();
                }
            }
            catch (Exception ex)
            {
                m_log.Error("[Physics] " + ex);
            }
        }

        public Color4 GetTextColor()
        {
            Color color = Color;
            return new Color4(color.R, color.G, color.B, (byte)(0xFF - color.A));
        }

        public void ResetOwnerChangeFlag()
        {
            List<UUID> inv = Inventory.GetInventoryList();

            foreach (UUID itemID in inv)
            {
                TaskInventoryItem item = Inventory.GetInventoryItem(itemID);
                item.OwnerChanged = false;
                Inventory.UpdateInventoryItem(item, false, false);
            }
            AggregateInnerPerms();
        }

        /// <summary>
        /// Record an avatar sitting on this part.
        /// </summary>
        /// <remarks>This is called for all the sitting avatars whether there is a sit target set or not.</remarks>
        /// <returns>
        /// true if the avatar was not already recorded, false otherwise.
        /// </returns>
        /// <param name='avatarId'></param>
        protected internal bool AddSittingAvatar(ScenePresence sp)
        {
            lock (ParentGroup.m_sittingAvatars)
            {
                if (IsSitTargetSet && SitTargetAvatar == UUID.Zero)
                    SitTargetAvatar = sp.UUID;

                if (m_sittingAvatars == null)
                    m_sittingAvatars = new HashSet<ScenePresence>();

                if (m_sittingAvatars.Add(sp))
                {
                    if(!ParentGroup.m_sittingAvatars.Contains(sp))
                        ParentGroup.m_sittingAvatars.Add(sp);

                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Remove an avatar recorded as sitting on this part.
        /// </summary>
        /// <remarks>This applies to all sitting avatars whether there is a sit target set or not.</remarks>
        /// <returns>
        /// true if the avatar was present and removed, false if it was not present.
        /// </returns>
        /// <param name='avatarId'></param>
        protected internal bool RemoveSittingAvatar(ScenePresence sp)
        {
            lock (ParentGroup.m_sittingAvatars)
            {
                if (SitTargetAvatar == sp.UUID)
                    SitTargetAvatar = UUID.Zero;

                if (m_sittingAvatars == null)
                    return false;

                if (m_sittingAvatars.Remove(sp))
                {
                    if (m_sittingAvatars.Count == 0)
                        m_sittingAvatars = null;

                    ParentGroup.m_sittingAvatars.Remove(sp);

                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Get a copy of the list of sitting avatars.
        /// </summary>
        /// <remarks>This applies to all sitting avatars whether there is a sit target set or not.</remarks>
        /// <returns>A hashset of the sitting avatars.  Returns null if there are no sitting avatars.</returns>
        public HashSet<ScenePresence> GetSittingAvatars()
        {
            lock (ParentGroup.m_sittingAvatars)
            {
                if (m_sittingAvatars == null)
                    return null;
                else
                    return new HashSet<ScenePresence>(m_sittingAvatars);
            }
        }

        /// <summary>
        /// Gets the number of sitting avatars.
        /// </summary>
        /// <remarks>This applies to all sitting avatars whether there is a sit target set or not.</remarks>
        /// <returns></returns>
        public int GetSittingAvatarsCount()
        {
            lock (ParentGroup.m_sittingAvatars)
            {
                if (m_sittingAvatars == null)
                    return 0;
                else
                    return m_sittingAvatars.Count;
            }
        }

        public void Stop()
        {
            Velocity = Vector3.Zero;
            AngularVelocity = Vector3.Zero;
            Acceleration = Vector3.Zero;
            APIDActive = false;
        }

        // handle osVolumeDetect
        public void ScriptSetVolumeDetect(bool makeVolumeDetect)
        {
            if(_parentID == 0)
            {
                // if root prim do it via SOG
                ParentGroup.ScriptSetVolumeDetect(makeVolumeDetect);
                return;
            }

            bool wasUsingPhysics = ((Flags & PrimFlags.Physics) != 0);
            bool wasTemporary = ((Flags & PrimFlags.TemporaryOnRez) != 0);
            bool wasPhantom = ((Flags & PrimFlags.Phantom) != 0);

            if(PhysActor != null)
                PhysActor.Building = true;
            UpdatePrimFlags(wasUsingPhysics,wasTemporary,wasPhantom,makeVolumeDetect,false);
        }
    }
}
