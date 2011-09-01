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
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes.Scripting;
using OpenSim.Region.Framework.Scenes.Serialization;
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
        REGION = 256,
        TELEPORT = 512,
        REGION_RESTART = 1024,
        MEDIA = 2048,
        ANIMATION = 16384
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

    #endregion Enumerations

    public class SceneObjectPart : IScriptHost, ISceneEntity
    {
        /// <value>
        /// Denote all sides of the prim
        /// </value>
        public const int ALL_SIDES = -1;
        
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <value>
        /// Is this sop a root part?
        /// </value>
        
        public bool IsRoot 
        {
           get { return ParentGroup.RootPart == this; } 
        }

        #region Fields

        public bool AllowedDrop;

        
        public bool DIE_AT_EDGE;

        
        public bool RETURN_AT_EDGE;

        
        public bool BlockGrab;

        
        public bool StatusSandbox;

        
        public Vector3 StatusSandboxPos;

        [XmlIgnore]
        public int[] PayPrice = {-2,-2,-2,-2,-2};

        [XmlIgnore]
        public PhysicsActor PhysActor
        {
            get { return m_physActor; }
            set
            {
//                m_log.DebugFormat("[SOP]: PhysActor set to {0} for {1} {2}", value, Name, UUID);
                m_physActor = value;
            }
        }

        //Xantor 20080528 Sound stuff:
        //  Note: This isn't persisted in the database right now, as the fields for that aren't just there yet.
        //        Not a big problem as long as the script that sets it remains in the prim on startup.
        //        for SL compatibility it should be persisted though (set sound / displaytext / particlesystem, kill script)
        
        public UUID Sound;
        
        
        public byte SoundFlags;
        
        
        public double SoundGain;
        
        
        public double SoundRadius;
        
        
        public uint TimeStampFull;
        
        
        public uint TimeStampLastActivity; // Will be used for AutoReturn
        
        
        public uint TimeStampTerse;

        
        public UUID FromItemID;

        
        public UUID FromFolderID;

        
        public int STATUS_ROTATE_X;

        
        public int STATUS_ROTATE_Y;

        
        public int STATUS_ROTATE_Z;
        
        
        private Dictionary<int, string> m_CollisionFilter = new Dictionary<int, string>();
               
        /// <value>
        /// The UUID of the user inventory item from which this object was rezzed if this is a root part.
        /// If UUID.Zero then either this is not a root part or there is no connection with a user inventory item.
        /// </value>
        private UUID m_fromUserInventoryItemID;
        
        
        public UUID FromUserInventoryItemID
        {
            get { return m_fromUserInventoryItemID; }
        }
        
        
        public scriptEvents AggregateScriptEvents;

        
        public Vector3 AttachedPos;

        
        public Vector3 RotationAxis = Vector3.One;

        
        public bool VolumeDetectActive; // XmlIgnore set to avoid problems with persistance until I come to care for this
                                        // Certainly this must be a persistant setting finally

        
        public bool IsWaitingForFirstSpinUpdatePacket;

        
        public Quaternion SpinOldOrientation = Quaternion.Identity;

        
        public Quaternion m_APIDTarget = Quaternion.Identity;

        
        public float m_APIDDamp = 0;

        
        public float m_APIDStrength = 0;

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
        
        private PrimFlags LocalFlags;
        
        private float m_damage = -1.0f;
        private byte[] m_TextureAnimation;
        private byte m_clickAction;
        private Color m_color = Color.Black;
        private string m_description = String.Empty;
        private readonly List<uint> m_lastColliders = new List<uint>();
        private int m_linkNum;
        
        private int m_scriptAccessPin;
        
        private readonly Dictionary<UUID, scriptEvents> m_scriptEvents = new Dictionary<UUID, scriptEvents>();
        private string m_sitName = String.Empty;
        private Quaternion m_sitTargetOrientation = Quaternion.Identity;
        private Vector3 m_sitTargetPosition;
        private string m_sitAnimation = "SIT";
        private string m_text = String.Empty;
        private string m_touchName = String.Empty;
        private readonly Stack<UndoState> m_undo = new Stack<UndoState>(5);
        private readonly Stack<UndoState> m_redo = new Stack<UndoState>(5);
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

        private PhysicsActor m_physActor;
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
        protected int m_lastTerseSent;
        
        /// <summary>
        /// Stores media texture data
        /// </summary>
        protected string m_mediaUrl;

        // TODO: Those have to be changed into persistent properties at some later point,
        // or sit-camera on vehicles will break on sim-crossing.
        private Vector3 m_cameraEyeOffset;
        private Vector3 m_cameraAtOffset;
        private bool m_forceMouselook;

        // TODO: Collision sound should have default.
        private UUID m_collisionSound;
        private float m_collisionSoundVolume;

        #endregion Fields

//        ~SceneObjectPart()
//        {
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

            Flags = 0;
            CreateSelected = true;

            TrimPermissions();
            
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
        private PrimFlags _flags = PrimFlags.None;
        private DateTime m_expires;
        private DateTime m_rezzed;
        private bool m_createSelected = false;
        private string m_creatorData = string.Empty;

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
        /// Data about the creator in the form profile_url;name
        /// </summary>
        public string CreatorData 
        {
            get { return m_creatorData; }
            set { m_creatorData = value; }
        }

        /// <summary>
        /// Used by the DB layer to retrieve / store the entire user identification.
        /// The identification can either be a simple UUID or a string of the form
        /// uuid[;profile_url[;name]]
        /// </summary>
        public string CreatorIdentification
        {
            get
            {
                if (m_creatorData != null && m_creatorData != string.Empty)
                    return _creatorID.ToString() + ';' + m_creatorData;
                else
                    return _creatorID.ToString();
            }
            set
            {
                if ((value == null) || (value != null && value == string.Empty))
                {
                    m_creatorData = string.Empty;
                    return;
                }

                if (!value.Contains(";")) // plain UUID
                {
                    UUID uuid = UUID.Zero;
                    UUID.TryParse(value, out uuid);
                    _creatorID = uuid;
                }
                else // <uuid>[;<endpoint>[;name]]
                {
                    string name = "Unknown User";
                    string[] parts = value.Split(';');
                    if (parts.Length >= 1)
                    {
                        UUID uuid = UUID.Zero;
                        UUID.TryParse(parts[0], out uuid);
                        _creatorID = uuid;
                    }
                    if (parts.Length >= 2)
                        m_creatorData = parts[1];
                    if (parts.Length >= 3)
                        name = parts[2];

                    m_creatorData += ';' + name;
                    
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
        /// Access should be via Inventory directly - this property temporarily remains for xml serialization purposes
        /// </value>
        public TaskInventoryDictionary TaskInventory
        {
            get { return m_inventory.Items; }
            set { m_inventory.Items = value; }
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

        
        
        public Dictionary<int, string> CollisionFilter
        {
            get { return m_CollisionFilter; }
            set
            {
                m_CollisionFilter = value;
            }
        }

        
        public Quaternion APIDTarget
        {
            get { return m_APIDTarget; }
            set { m_APIDTarget = value; }
        }

        
        public float APIDDamp
        {
            get { return m_APIDDamp; }
            set { m_APIDDamp = value; }
        }

        
        public float APIDStrength
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

        /// <summary>
        /// The position of the entire group that this prim belongs to.
        /// </summary>
        public Vector3 GroupPosition
        {
            get
            {
                // If this is a linkset, we don't want the physics engine mucking up our group position here.
                PhysicsActor actor = PhysActor;
                if (actor != null && _parentID == 0)
                {
                    m_groupPosition = actor.Position;
                }

                if (m_parentGroup.IsAttachment)
                {
                    ScenePresence sp = m_parentGroup.Scene.GetScenePresence(ParentGroup.AttachedAvatar);
                    if (sp != null)
                        return sp.AbsolutePosition;
                }

                return m_groupPosition;
            }
            set
            {
                m_groupPosition = value;

                PhysicsActor actor = PhysActor;
                if (actor != null)
                {
                    try
                    {
                        // Root prim actually goes at Position
                        if (_parentID == 0)
                        {
                            actor.Position = value;
                        }
                        else
                        {
                            // To move the child prim in respect to the group position and rotation we have to calculate
                            actor.Position = GetWorldPosition();
                            actor.Orientation = GetWorldRotation();
                        }

                        // Tell the physics engines that this prim changed.
                        m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(actor);
                    }
                    catch (Exception e)
                    {
                        m_log.Error("[SCENEOBJECTPART]: GROUP POSITION. " + e.Message);
                    }
                }
                
                // TODO if we decide to do sitting in a more SL compatible way (multiple avatars per prim), this has to be fixed, too
                if (m_sitTargetAvatar != UUID.Zero)
                {
                    ScenePresence avatar;
                    if (m_parentGroup.Scene.TryGetScenePresence(m_sitTargetAvatar, out avatar))
                    {
                        avatar.ParentPosition = GetWorldPosition();
                    }
                }
            }
        }

        public Vector3 OffsetPosition
        {
            get { return m_offsetPosition; }
            set
            {
//                StoreUndoState();
                m_offsetPosition = value;

                if (ParentGroup != null && !ParentGroup.IsDeleted)
                {
                    PhysicsActor actor = PhysActor;
                    if (_parentID != 0 && actor != null)
                    {
                        actor.Position = GetWorldPosition();
                        actor.Orientation = GetWorldRotation();

                        // Tell the physics engines that this prim changed.
                        if (m_parentGroup.Scene != null)
                            m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(actor);
                    }
                }
            }
        }

        public Vector3 RelativePosition
        {
            get
            {
                if (IsRoot)
                {
                    if (m_parentGroup.IsAttachment)
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

        public Quaternion RotationOffset
        {
            get
            {
                // We don't want the physics engine mucking up the rotations in a linkset
                PhysicsActor actor = PhysActor;
                if (_parentID == 0 && (Shape.PCode != 9 || Shape.State == 0)  && actor != null)
                {
                    if (actor.Orientation.X != 0f || actor.Orientation.Y != 0f
                        || actor.Orientation.Z != 0f || actor.Orientation.W != 0f)
                    {
                        m_rotationOffset = actor.Orientation;
                    }
                }
                
                return m_rotationOffset;
            }
            
            set
            {
                StoreUndoState();
                m_rotationOffset = value;

                PhysicsActor actor = PhysActor;
                if (actor != null)
                {
                    try
                    {
                        // Root prim gets value directly
                        if (_parentID == 0)
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

                        if (m_parentGroup != null)
                            m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(actor);
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
                m_velocity = value;

                PhysicsActor actor = PhysActor;
                if (actor != null)
                {
                    if (actor.IsPhysical)
                    {
                        actor.Velocity = value;
                        m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(actor);
                    }
                }
            }
        }

        /// <summary></summary>
        public Vector3 AngularVelocity
        {
            get
            {
                PhysicsActor actor = PhysActor;
                if ((actor != null) && actor.IsPhysical)
                {
                    m_angularVelocity = actor.RotationalVelocity;
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
                PhysicsActor actor = PhysActor;
                if (actor != null)
                {
                    actor.SOPDescription = value;
                }
            }
        }

        /// <value>
        /// Text color.
        /// </value>
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
            set { m_shape = value; }
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
                    StoreUndoState();

                    m_shape.Scale = value;

                    PhysicsActor actor = PhysActor;
                    if (actor != null)
                    {
                        if (m_parentGroup.Scene != null)
                        {
                            if (m_parentGroup.Scene.PhysicsScene != null)
                            {
                                actor.Size = m_shape.Scale;

                                if (Shape.SculptEntry)
                                    CheckSculptAndLoad();
                                else
                                    ParentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
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
                if (m_parentGroup.IsAttachment)
                    return GroupPosition;

                return m_offsetPosition + m_groupPosition;
            }
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

        
        public UUID SitTargetAvatar
        {
            get { return m_sitTargetAvatar; }
            set { m_sitTargetAvatar = value; }
        }

        
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
            m_parentGroup.Scene.ForEachScenePresence(delegate(ScenePresence avatar)
            {
                // Ugly reference :(
                if (avatar.UUID == AgentID)
                {
                    m_parentGroup.GetProperties(avatar.ControllingClient);
                }
            });
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
            if ((Flags & flag) == 0)
            {
                //m_log.Debug("Adding flag: " + ((PrimFlags) flag).ToString());
                Flags |= flag;

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
            m_parentGroup.Scene.ForEachScenePresence(delegate(ScenePresence avatar)
            {
                AddFullUpdateToAvatar(avatar);
            });
        }

        /// <summary>
        /// Tell the scene presence that it should send updates for this part to its client
        /// </summary>
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
            m_parentGroup.Scene.ForEachScenePresence(delegate(ScenePresence avatar)
            {
                AddTerseUpdateToAvatar(avatar);
            });
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

            m_parentGroup.Scene.ForEachScenePresence(delegate(ScenePresence sp)
            {
                if (!sp.IsChildAgent)
                    sp.ControllingClient.SendAttachedSoundGainChange(UUID, (float)volume);
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
            Vector3 impulse = impulsei;

            if (localGlobalTF)
            {
                Quaternion grot = GetWorldRotation();
                Quaternion AXgrot = grot;
                Vector3 AXimpulsei = impulsei;
                Vector3 newimpulse = AXimpulsei * AXgrot;
                impulse = newimpulse;
            }

            m_parentGroup.applyAngularImpulse(impulse);
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
            Vector3 impulse = impulsei;

            if (localGlobalTF)
            {
                Quaternion grot = GetWorldRotation();
                Quaternion AXgrot = grot;
                Vector3 AXimpulsei = impulsei;
                Vector3 newimpulse = AXimpulsei * AXgrot;
                impulse = newimpulse;
            }

            m_parentGroup.setAngularImpulse(impulse);
        }

        /// <summary>
        /// Apply physics to this part.
        /// </summary>
        /// <param name="rootObjectFlags"></param>
        /// <param name="m_physicalPrim"></param>
        public void ApplyPhysics(uint rootObjectFlags, bool VolumeDetectActive, bool m_physicalPrim)
        {
//            m_log.DebugFormat("[SCENE OBJECT PART]: Applying physics to {0} {1} {2}", Name, LocalId, UUID);

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
                if (!isPhantom && !m_parentGroup.IsAttachment && !(Shape.PathCurve == (byte) Extrusion.Flexible))
                {
                    try
                    {
                        PhysActor = m_parentGroup.Scene.PhysicsScene.AddPrimShape(
                                string.Format("{0}/{1}", Name, UUID),
                                Shape,
                                AbsolutePosition,
                                Scale,
                                RotationOffset,
                                RigidBody,
                                m_localId);
                    }
                    catch
                    {
                        m_log.ErrorFormat("[SCENE]: caught exception meshing object {0}. Object set to phantom.", m_uuid);
                        PhysActor = null;
                    }
                    // Basic Physics returns null..  joy joy joy.
                    if (PhysActor != null)
                    {
                        PhysActor.SOPName = this.Name; // save object name and desc into the PhysActor so ODE internals know the joint/body info
                        PhysActor.SOPDescription = this.Description;
                        PhysActor.SetMaterial(Material);
                        DoPhysicsPropertyUpdate(RigidBody, true);
                        PhysActor.SetVolumeDetect(VolumeDetectActive ? 1 : 0);
                    }
                    else
                    {
                        m_log.DebugFormat("[SOP]: physics actor is null for {0} with parent {1}", UUID, this.ParentGroup.UUID);
                    }
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
            dupe.Flags = Flags;

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
                    ParentGroup.Scene.AssetService.Get(
                        dupe.m_shape.SculptTexture.ToString(), dupe, dupe.AssetReceived);
                }
                
                bool UsePhysics = ((dupe.Flags & PrimFlags.Physics) != 0);
                dupe.DoPhysicsPropertyUpdate(UsePhysics, true);
            }
            
            ParentGroup.Scene.EventManager.TriggerOnSceneObjectPartCopy(dupe, this, userExposed);

//            m_log.DebugFormat("[SCENE OBJECT PART]: Clone of {0} {1} finished", Name, UUID);
                          
            return dupe;
        }

        /// <summary>
        /// Called back by asynchronous asset fetch.
        /// </summary>
        /// <param name="id">ID of asset received</param>
        /// <param name="sender">Register</param>
        /// <param name="asset"></param>
        protected void AssetReceived(string id, Object sender, AssetBase asset)
        {
            if (asset != null)
                SculptTextureCallback(asset);
            else
                m_log.WarnFormat(
                    "[SCENE OBJECT PART]: Part {0} {1} requested mesh/sculpt data for asset id {2} from asset service but received no data",
                    Name, LocalId, id);
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
                    m_parentGroup.Scene.PhysicsScene.RequestJointDeletion(Name); // FIXME: what if the name changed?

                    // make sure client isn't interpolating the joint proxy object
                    Velocity = Vector3.Zero;
                    AngularVelocity = Vector3.Zero;
                    Acceleration = Vector3.Zero;
                }
            }
        }

        /// <summary>
        /// Do a physics propery update for this part.
        /// </summary>
        /// <param name="UsePhysics"></param>
        /// <param name="isNew"></param>
        public void DoPhysicsPropertyUpdate(bool UsePhysics, bool isNew)
        {
            if (IsJoint())
            {
                DoPhysicsPropertyUpdateForNinjaJoint(UsePhysics, isNew);
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

                        if ((Flags & PrimFlags.Phantom) == 0)
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

                    // If this part is a sculpt then delay the physics update until we've asynchronously loaded the
                    // mesh data.
                    if (Shape.SculptEntry)
                        CheckSculptAndLoad();
                    else
                        m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
                }
            }
        }

        /// <summary>
        /// Restore this part from the serialized xml representation.
        /// </summary>
        /// <param name="xmlReader"></param>
        /// <returns></returns>
        public static SceneObjectPart FromXml(XmlTextReader xmlReader)
        {
            return FromXml(UUID.Zero, xmlReader);
        }

        /// <summary>
        /// Restore this part from the serialized xml representation.
        /// </summary>
        /// <param name="fromUserInventoryItemId">The inventory id from which this part came, if applicable</param>
        /// <param name="xmlReader"></param>
        /// <returns></returns>
        public static SceneObjectPart FromXml(UUID fromUserInventoryItemId, XmlTextReader xmlReader)
        {
            SceneObjectPart part = SceneObjectSerializer.Xml2ToSOP(xmlReader);
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
            if (m_parentGroup.IsDeleted)
                return false;

            return m_parentGroup.RootPart.DIE_AT_EDGE;
        }

        public bool GetReturnAtEdge()
        {
            if (m_parentGroup.IsDeleted)
                return false;

            return m_parentGroup.RootPart.RETURN_AT_EDGE;
        }

        public void SetReturnAtEdge(bool p)
        {
            if (m_parentGroup.IsDeleted)
                return;

            m_parentGroup.RootPart.RETURN_AT_EDGE = p;
        }

        public bool GetBlockGrab()
        {
            if (m_parentGroup.IsDeleted)
                return false;

            return m_parentGroup.RootPart.BlockGrab;
        }

        public void SetBlockGrab(bool p)
        {
            if (m_parentGroup.IsDeleted)
                return;

            m_parentGroup.RootPart.BlockGrab = p;
        }

        public void SetStatusSandbox(bool p)
        {
            if (m_parentGroup.IsDeleted)
                return;
            StatusSandboxPos = m_parentGroup.RootPart.AbsolutePosition;
            m_parentGroup.RootPart.StatusSandbox = p;
        }

        public bool GetStatusSandbox()
        {
            if (m_parentGroup.IsDeleted)
                return false;

            return m_parentGroup.RootPart.StatusSandbox;
        }

        public int GetAxisRotation(int axis)
        {
            //Cannot use ScriptBaseClass constants as no referance to it currently.
            if (axis == 2)//STATUS_ROTATE_X
                return STATUS_ROTATE_X;
            if (axis == 4)//STATUS_ROTATE_Y
                return STATUS_ROTATE_Y;
            if (axis == 8)//STATUS_ROTATE_Z
                return STATUS_ROTATE_Z;

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

        public Vector3 GetForce()
        {
            if (PhysActor != null)
                return PhysActor.Force;
            else
                return Vector3.Zero;
        }

        public void GetProperties(IClientAPI client)
        {
            client.SendObjectPropertiesReply(this);
        }

        public UUID GetRootPartUUID()
        {
            return m_parentGroup.UUID;
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
            
//            m_log.DebugFormat("[SCENE OBJECT PART]: Found group pos {0} for part {1}", GroupPosition, Name);
            
            Vector3 worldPos = GroupPosition + translationOffsetPosition;
                
//            m_log.DebugFormat("[SCENE OBJECT PART]: Found world pos {0} for part {1}", worldPos, Name);
            
            return worldPos;
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
            Dictionary<uint, ContactPoint> collissionswith = a.m_objCollisionList;
            List<uint> thisHitColliders = new List<uint>();
            List<uint> endedColliders = new List<uint>();
            List<uint> startedColliders = new List<uint>();

            // calculate things that started colliding this time
            // and build up list of colliders this time
            foreach (uint localid in collissionswith.Keys)
            {
                thisHitColliders.Add(localid);
                if (!m_lastColliders.Contains(localid))
                {
                    startedColliders.Add(localid);
                }
                //m_log.Debug("[OBJECT]: Collided with:" + localid.ToString() + " at depth of: " + collissionswith[localid].ToString());
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

            if (m_parentGroup.IsDeleted)
                return;

            // play the sound.
            if (startedColliders.Count > 0 && CollisionSound != UUID.Zero && CollisionSoundVolume > 0.0f)
            {
                SendSound(CollisionSound.ToString(), CollisionSoundVolume, true, (byte)0, 0, false, false);
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
                        if (localId == 0)
                            continue;
                        
                        if (m_parentGroup.Scene == null)
                            return;
                        
                        SceneObjectPart obj = m_parentGroup.Scene.GetSceneObjectPart(localId);
                        string data = "";
                        if (obj != null)
                        {
                            if (m_parentGroup.RootPart.CollisionFilter.ContainsValue(obj.UUID.ToString())
                                || m_parentGroup.RootPart.CollisionFilter.ContainsValue(obj.Name))
                            {
                                bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1,out data);
                                //If it is 1, it is to accept ONLY collisions from this object
                                if (found)
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
                                //If it is 0, it is to not accept collisions from this object
                                else
                                {
                                }
                            }
                            else
                            {
                                bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1,out data);
                                //If it is 1, it is to accept ONLY collisions from this object, so this other object will not work
                                if (!found)
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
                            }
                        }
                        else
                        {
                            m_parentGroup.Scene.ForEachScenePresence(delegate(ScenePresence av)
                            {
                                if (av.LocalId == localId)
                                {
                                    if (m_parentGroup.RootPart.CollisionFilter.ContainsValue(av.UUID.ToString())
                                        || m_parentGroup.RootPart.CollisionFilter.ContainsValue(av.Name))
                                    {
                                        bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1, out data);
                                        //If it is 1, it is to accept ONLY collisions from this avatar
                                        if (found)
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
                                        //If it is 0, it is to not accept collisions from this avatar
                                        else
                                        {
                                        }
                                    }
                                    else
                                    {
                                        bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1, out data);
                                        //If it is 1, it is to accept ONLY collisions from this avatar, so this other avatar will not work
                                        if (!found)
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
                            });
                        }
                    }
                    if (colliding.Count > 0)
                    {
                        StartCollidingMessage.Colliders = colliding;
                        
                        if (m_parentGroup.Scene == null)
                            return;

                        if (m_parentGroup.PassCollision == true)
                        {
                            //TODO: Add pass to root prim!
                        }
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
                        
                        if (m_parentGroup.Scene == null)
                            return;
                        
                        SceneObjectPart obj = m_parentGroup.Scene.GetSceneObjectPart(localId);
                        string data = "";
                        if (obj != null)
                        {
                            if (m_parentGroup.RootPart.CollisionFilter.ContainsValue(obj.UUID.ToString())
                                || m_parentGroup.RootPart.CollisionFilter.ContainsValue(obj.Name))
                            {
                                bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1,out data);
                                //If it is 1, it is to accept ONLY collisions from this object
                                if (found)
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
                                //If it is 0, it is to not accept collisions from this object
                                else
                                {
                                }
                            }
                            else
                            {
                                bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1,out data);
                                //If it is 1, it is to accept ONLY collisions from this object, so this other object will not work
                                if (!found)
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
                            }
                        }
                        else
                        {
                            m_parentGroup.Scene.ForEachScenePresence(delegate(ScenePresence av)
                            {
                                if (av.LocalId == localId)
                                {
                                    if (m_parentGroup.RootPart.CollisionFilter.ContainsValue(av.UUID.ToString())
                                        || m_parentGroup.RootPart.CollisionFilter.ContainsValue(av.Name))
                                    {
                                        bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1, out data);
                                        //If it is 1, it is to accept ONLY collisions from this avatar
                                        if (found)
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
                                        //If it is 0, it is to not accept collisions from this avatar
                                        else
                                        {
                                        }
                                    }
                                    else
                                    {
                                        bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1, out data);
                                        //If it is 1, it is to accept ONLY collisions from this avatar, so this other avatar will not work
                                        if (!found)
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
                            });
                        }
                    }
                    if (colliding.Count > 0)
                    {
                        CollidingMessage.Colliders = colliding;
                        
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

                        if (m_parentGroup.Scene == null)
                            return;

                        SceneObjectPart obj = m_parentGroup.Scene.GetSceneObjectPart(localId);
                        string data = "";
                        if (obj != null)
                        {
                            if (m_parentGroup.RootPart.CollisionFilter.ContainsValue(obj.UUID.ToString()) || m_parentGroup.RootPart.CollisionFilter.ContainsValue(obj.Name))
                            {
                                bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1,out data);
                                //If it is 1, it is to accept ONLY collisions from this object
                                if (found)
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
                                //If it is 0, it is to not accept collisions from this object
                                else
                                {
                                }
                            }
                            else
                            {
                                bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1,out data);
                                //If it is 1, it is to accept ONLY collisions from this object, so this other object will not work
                                if (!found)
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
                            }
                        }
                        else
                        {
                            m_parentGroup.Scene.ForEachScenePresence(delegate(ScenePresence av)
                            {
                                if (av.LocalId == localId)
                                {
                                    if (m_parentGroup.RootPart.CollisionFilter.ContainsValue(av.UUID.ToString())
                                        || m_parentGroup.RootPart.CollisionFilter.ContainsValue(av.Name))
                                    {
                                        bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1, out data);
                                        //If it is 1, it is to accept ONLY collisions from this avatar
                                        if (found)
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
                                        //If it is 0, it is to not accept collisions from this avatar
                                        else
                                        {
                                        }
                                    }
                                    else
                                    {
                                        bool found = m_parentGroup.RootPart.CollisionFilter.TryGetValue(1, out data);
                                        //If it is 1, it is to accept ONLY collisions from this avatar, so this other avatar will not work
                                        if (!found)
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
                            });
                        }
                    }
                    
                    if (colliding.Count > 0)
                    {
                        EndCollidingMessage.Colliders = colliding;
                        
                        if (m_parentGroup.Scene == null)
                            return;
                        
                        m_parentGroup.Scene.EventManager.TriggerScriptCollidingEnd(LocalId, EndCollidingMessage);
                    }
                }
            }

            if ((m_parentGroup.RootPart.ScriptEvents & scriptEvents.land_collision_start) != 0)
            {
                if (startedColliders.Count > 0)
                {
                    ColliderArgs LandStartCollidingMessage = new ColliderArgs();
                    List<DetectedObject> colliding = new List<DetectedObject>();
                    foreach (uint localId in startedColliders)
                    {
                        if (localId == 0)
                        {
                            //Hope that all is left is ground!
                            DetectedObject detobj = new DetectedObject();
                            detobj.keyUUID = UUID.Zero;
                            detobj.nameStr = "";
                            detobj.ownerUUID = UUID.Zero;
                            detobj.posVector = m_parentGroup.RootPart.AbsolutePosition;
                            detobj.rotQuat = Quaternion.Identity;
                            detobj.velVector = Vector3.Zero;
                            detobj.colliderType = 0;
                            detobj.groupUUID = UUID.Zero;
                            colliding.Add(detobj);
                        }
                    }

                    if (colliding.Count > 0)
                    {
                        LandStartCollidingMessage.Colliders = colliding;

                        if (m_parentGroup.Scene == null)
                            return;

                        m_parentGroup.Scene.EventManager.TriggerScriptLandCollidingStart(LocalId, LandStartCollidingMessage);
                    }
                }
            }

            if ((m_parentGroup.RootPart.ScriptEvents & scriptEvents.land_collision) != 0)
            {
                if (m_lastColliders.Count > 0)
                {
                    ColliderArgs LandCollidingMessage = new ColliderArgs();
                    List<DetectedObject> colliding = new List<DetectedObject>();
                    foreach (uint localId in startedColliders)
                    {
                        if (localId == 0)
                        {
                            //Hope that all is left is ground!
                            DetectedObject detobj = new DetectedObject();
                            detobj.keyUUID = UUID.Zero;
                            detobj.nameStr = "";
                            detobj.ownerUUID = UUID.Zero;
                            detobj.posVector = m_parentGroup.RootPart.AbsolutePosition;
                            detobj.rotQuat = Quaternion.Identity;
                            detobj.velVector = Vector3.Zero;
                            detobj.colliderType = 0;
                            detobj.groupUUID = UUID.Zero;
                            colliding.Add(detobj);
                        }
                    }

                    if (colliding.Count > 0)
                    {
                        LandCollidingMessage.Colliders = colliding;

                        if (m_parentGroup.Scene == null)
                            return;

                        m_parentGroup.Scene.EventManager.TriggerScriptLandColliding(LocalId, LandCollidingMessage);
                    }
                }
            }

            if ((m_parentGroup.RootPart.ScriptEvents & scriptEvents.land_collision_end) != 0)
            {
                if (endedColliders.Count > 0)
                {
                    ColliderArgs LandEndCollidingMessage = new ColliderArgs();
                    List<DetectedObject> colliding = new List<DetectedObject>();
                    foreach (uint localId in startedColliders)
                    {
                        if (localId == 0)
                        {
                            //Hope that all is left is ground!
                            DetectedObject detobj = new DetectedObject();
                            detobj.keyUUID = UUID.Zero;
                            detobj.nameStr = "";
                            detobj.ownerUUID = UUID.Zero;
                            detobj.posVector = m_parentGroup.RootPart.AbsolutePosition;
                            detobj.rotQuat = Quaternion.Identity;
                            detobj.velVector = Vector3.Zero;
                            detobj.colliderType = 0;
                            detobj.groupUUID = UUID.Zero;
                            colliding.Add(detobj);
                        }
                    }

                    if (colliding.Count > 0)
                    {
                        LandEndCollidingMessage.Colliders = colliding;

                        if (m_parentGroup.Scene == null)
                            return;

                        m_parentGroup.Scene.EventManager.TriggerScriptLandCollidingEnd(LocalId, LandEndCollidingMessage);
                    }
                }
            }
        }

        public void PhysicsOutOfBounds(Vector3 pos)
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
                
                if (m_parentGroup.Scene.TestBorderCross(newpos, Cardinals.N)
                    | m_parentGroup.Scene.TestBorderCross(newpos, Cardinals.S)
                    | m_parentGroup.Scene.TestBorderCross(newpos, Cardinals.E)
                    | m_parentGroup.Scene.TestBorderCross(newpos, Cardinals.W))
                {
                    m_parentGroup.AbsolutePosition = newpos;
                    return;
                }
                //m_parentGroup.RootPart.m_groupPosition = newpos;
            }
            ScheduleTerseUpdate();

            //SendTerseUpdateToAllClients();
        }

        public void PreloadSound(string sound)
        {
            // UUID ownerID = OwnerID;
            UUID objectID = ParentGroup.RootPart.UUID;
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

            m_parentGroup.Scene.ForEachScenePresence(delegate(ScenePresence sp)
            {
                if (sp.IsChildAgent)
                    return;
                if (!(Util.GetDistanceTo(sp.AbsolutePosition, AbsolutePosition) >= 100))
                    sp.ControllingClient.SendPreLoadSound(objectID, objectID, soundID);
            });
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
            scale.X = Math.Min(scale.X, ParentGroup.Scene.m_maxNonphys);
            scale.Y = Math.Min(scale.Y, ParentGroup.Scene.m_maxNonphys);
            scale.Z = Math.Min(scale.Z, ParentGroup.Scene.m_maxNonphys);

            if (PhysActor != null && PhysActor.IsPhysical)
            {
                scale.X = Math.Min(scale.X, ParentGroup.Scene.m_maxPhys);
                scale.Y = Math.Min(scale.Y, ParentGroup.Scene.m_maxPhys);
                scale.Z = Math.Min(scale.Z, ParentGroup.Scene.m_maxPhys);
            }

//            m_log.DebugFormat("[SCENE OBJECT PART]: Resizing {0} {1} to {2}", Name, LocalId, scale);

            Scale = scale;

            ParentGroup.HasGroupChanged = true;
            ScheduleFullUpdate();
        }
        
        public void RotLookAt(Quaternion target, float strength, float damping)
        {
            rotLookAt(target, strength, damping);
        }

        public void rotLookAt(Quaternion target, float strength, float damping)
        {
            if (m_parentGroup.IsAttachment)
            {
                /*
                    ScenePresence avatar = m_scene.GetScenePresence(rootpart.AttachedAvatar);
                    if (avatar != null)
                    {
                    Rotate the Av?
                    } */
            }
            else
            {
                APIDDamp = damping;
                APIDStrength = strength;
                APIDTarget = target;
            }
        }

        public void startLookAt(Quaternion rot, float damp, float strength)
        {
            APIDDamp = damp;
            APIDStrength = strength;
            APIDTarget = rot;
        }

        public void stopLookAt()
        {
            APIDTarget = Quaternion.Identity;
        }

        /// <summary>
        /// Schedules this prim for a full update
        /// </summary>
        public void ScheduleFullUpdate()
        {
//            m_log.DebugFormat("[SCENE OBJECT PART]: Scheduling full update for {0} {1}", Name, LocalId);

            if (m_parentGroup == null)
                return;

            m_parentGroup.QueueForUpdateCheck();

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
            if (m_parentGroup == null)
                return;

            if (m_updateFlag < 1)
            {
                m_parentGroup.HasGroupChanged = true;
                m_parentGroup.QueueForUpdateCheck();

                TimeStampTerse = (uint) Util.UnixTimeSinceEpoch();
                m_updateFlag = 1;

            //                m_log.DebugFormat(
            //                    "[SCENE OBJECT PART]: Scheduling terse update for {0}, {1} at {2}",
            //                    UUID, Name, TimeStampTerse);
            }
        }

        public void ScriptSetPhysicsStatus(bool UsePhysics)
        {
            m_parentGroup.ScriptSetPhysicsStatus(UsePhysics);
        }

        /// <summary>
        /// Set sculpt and mesh data, and tell the physics engine to process the change.
        /// </summary>
        /// <param name="texture">The mesh itself.</param>
        public void SculptTextureCallback(AssetBase texture)
        {
            if (m_shape.SculptEntry)
            {
                // commented out for sculpt map caching test - null could mean a cached sculpt map has been found
                //if (texture != null)
                {
                    if (texture != null)
                    {
//                        m_log.DebugFormat(
//                            "[SCENE OBJECT PART]: Setting sculpt data for {0} on SculptTextureCallback()", Name);

                        m_shape.SculptData = texture.Data;
                    }

                    if (PhysActor != null)
                    {
                        // Update the physics actor with the new loaded sculpt data and set the taint signal.
                        PhysActor.Shape = m_shape;

                        m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
                    }
                }
            }
        }

        /// <summary>
        /// Send a full update to the client for the given part
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="clientFlags"></param>
        protected internal void SendFullUpdate(IClientAPI remoteClient, uint clientFlags)
        {
            if (m_parentGroup == null)
                return;

//            m_log.DebugFormat(
//                "[SOG]: Sendinging part full update to {0} for {1} {2}", remoteClient.Name, part.Name, part.LocalId);
            
            if (IsRoot)
            {
                if (m_parentGroup.IsAttachment)
                {
                    SendFullUpdateToClient(remoteClient, AttachedPos, clientFlags);
                }
                else
                {
                    SendFullUpdateToClient(remoteClient, AbsolutePosition, clientFlags);
                }
            }
            else
            {
                SendFullUpdateToClient(remoteClient, clientFlags);
            }
        }

        /// <summary>
        /// Send a full update for this part to all clients.
        /// </summary>
        public void SendFullUpdateToAllClients()
        {
            if (m_parentGroup == null)
                return;

            m_parentGroup.Scene.ForEachScenePresence(delegate(ScenePresence avatar)
            {
                SendFullUpdate(avatar.ControllingClient, avatar.GenerateClientFlags(UUID));
            });
        }

        /// <summary>
        /// Send a full update to all clients except the one nominated.
        /// </summary>
        /// <param name="agentID"></param>
        public void SendFullUpdateToAllClientsExcept(UUID agentID)
        {
            if (m_parentGroup == null)
                return;

            m_parentGroup.Scene.ForEachScenePresence(delegate(ScenePresence avatar)
            {
                // Ugly reference :(
                if (avatar.UUID != agentID)
                    SendFullUpdate(avatar.ControllingClient, avatar.GenerateClientFlags(UUID));
            });
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
            if (ParentGroup == null)
                return;

            // Suppress full updates during attachment editing
            //
            if (ParentGroup.IsSelected && ParentGroup.IsAttachment)
                return;
            
            if (ParentGroup.IsDeleted)
                return;

            clientFlags &= ~(uint) PrimFlags.CreateSelected;

            if (remoteClient.AgentId == _ownerID)
            {
                if ((Flags & PrimFlags.CreateSelected) != 0)
                {
                    clientFlags |= (uint) PrimFlags.CreateSelected;
                    Flags &= ~PrimFlags.CreateSelected;
                }
            }
            //bool isattachment = IsAttachment;
            //if (LocalId != ParentGroup.RootPart.LocalId)
                //isattachment = ParentGroup.RootPart.IsAttachment;

            remoteClient.SendPrimUpdate(this, PrimUpdateFlags.FullUpdate);
        }

        /// <summary>
        /// Tell all the prims which have had updates scheduled
        /// </summary>
        public void SendScheduledUpdates()
        {
            const float ROTATION_TOLERANCE = 0.01f;
            const float VELOCITY_TOLERANCE = 0.001f;
            const float POSITION_TOLERANCE = 0.05f;
            const int TIME_MS_TOLERANCE = 3000;

            if (m_updateFlag == 1)
            {
                // Throw away duplicate or insignificant updates
                if (!RotationOffset.ApproxEquals(m_lastRotation, ROTATION_TOLERANCE) ||
                    !Acceleration.Equals(m_lastAcceleration) ||
                    !Velocity.ApproxEquals(m_lastVelocity, VELOCITY_TOLERANCE) ||
                    Velocity.ApproxEquals(Vector3.Zero, VELOCITY_TOLERANCE) ||
                    !AngularVelocity.ApproxEquals(m_lastAngularVelocity, VELOCITY_TOLERANCE) ||
                    !OffsetPosition.ApproxEquals(m_lastPosition, POSITION_TOLERANCE) ||
                    Environment.TickCount - m_lastTerseSent > TIME_MS_TOLERANCE)
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

                    // Update the "last" values
                    m_lastPosition = OffsetPosition;
                    m_lastRotation = RotationOffset;
                    m_lastVelocity = Velocity;
                    m_lastAcceleration = Acceleration;
                    m_lastAngularVelocity = AngularVelocity;
                    m_lastTerseSent = Environment.TickCount;
                }
            }
            else
            {
                if (m_updateFlag == 2) // is a new prim, just created/reloaded or has major changes
                {
                    AddFullUpdateToAllAvatars();
                    ClearUpdateSchedule();
                }
            }
            ClearUpdateSchedule();
        }

        /// <summary>
        /// Trigger or play an attached sound in this part's inventory.
        /// </summary>
        /// <param name="sound"></param>
        /// <param name="volume"></param>
        /// <param name="triggered"></param>
        /// <param name="flags"></param>
        public void SendSound(string sound, double volume, bool triggered, byte flags, float radius, bool useMaster, bool isMaster)
        {
            if (volume > 1)
                volume = 1;
            if (volume < 0)
                volume = 0;

            UUID ownerID = _ownerID;
            UUID objectID = ParentGroup.RootPart.UUID;
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
                if (useMaster)
                {
                    if (isMaster)
                    {
                        if (triggered)
                            soundModule.TriggerSound(soundID, ownerID, objectID, parentID, volume, position, regionHandle, radius);
                        else
                            soundModule.PlayAttachedSound(soundID, ownerID, objectID, volume, position, flags, radius);
                        ParentGroup.PlaySoundMasterPrim = this;
                        ownerID = _ownerID;
                        objectID = ParentGroup.RootPart.UUID;
                        parentID = GetRootPartUUID();
                        position = AbsolutePosition; // region local
                        regionHandle = ParentGroup.Scene.RegionInfo.RegionHandle;
                        if (triggered)
                            soundModule.TriggerSound(soundID, ownerID, objectID, parentID, volume, position, regionHandle, radius);
                        else
                            soundModule.PlayAttachedSound(soundID, ownerID, objectID, volume, position, flags, radius);
                        foreach (SceneObjectPart prim in ParentGroup.PlaySoundSlavePrims)
                        {
                            ownerID = prim._ownerID;
                            objectID = prim.ParentGroup.RootPart.UUID;
                            parentID = prim.GetRootPartUUID();
                            position = prim.AbsolutePosition; // region local
                            regionHandle = prim.ParentGroup.Scene.RegionInfo.RegionHandle;
                            if (triggered)
                                soundModule.TriggerSound(soundID, ownerID, objectID, parentID, volume, position, regionHandle, radius);
                            else
                                soundModule.PlayAttachedSound(soundID, ownerID, objectID, volume, position, flags, radius);
                        }
                        ParentGroup.PlaySoundSlavePrims.Clear();
                        ParentGroup.PlaySoundMasterPrim = null;
                    }
                    else
                    {
                        ParentGroup.PlaySoundSlavePrims.Add(this);
                    }
                }
                else
                {
                    if (triggered)
                        soundModule.TriggerSound(soundID, ownerID, objectID, parentID, volume, position, regionHandle, radius);
                    else
                        soundModule.PlayAttachedSound(soundID, ownerID, objectID, volume, position, flags, radius);
                }
            }
        }

        /// <summary>
        /// Send a terse update to all clients
        /// </summary>
        public void SendTerseUpdateToAllClients()
        {
            m_parentGroup.Scene.ForEachScenePresence(delegate(ScenePresence avatar)
            {
                SendTerseUpdateToClient(avatar.ControllingClient);
            });
        }

        public void SetAxisRotation(int axis, int rotate)
        {
            m_parentGroup.SetAxisRotation(axis, rotate);

            //Cannot use ScriptBaseClass constants as no referance to it currently.
            if (axis == 2)//STATUS_ROTATE_X
                STATUS_ROTATE_X = rotate;

            if (axis == 4)//STATUS_ROTATE_Y
                STATUS_ROTATE_Y = rotate;

            if (axis == 8)//STATUS_ROTATE_Z
                STATUS_ROTATE_Z = rotate;
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

        public void SetForce(Vector3 force)
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

        public void SetVehicleVectorParam(int param, Vector3 value)
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

        /// <summary>
        /// Set the color of prim faces
        /// </summary>
        /// <param name="color"></param>
        /// <param name="face"></param>
        public void SetFaceColor(Vector3 color, int face)
        {
            Primitive.TextureEntry tex = Shape.Textures;
            Color4 texcolor;
            if (face >= 0 && face < GetNumberOfSides())
            {
                texcolor = tex.CreateFace((uint)face).RGBA;
                texcolor.R = Util.Clip((float)color.X, 0.0f, 1.0f);
                texcolor.G = Util.Clip((float)color.Y, 0.0f, 1.0f);
                texcolor.B = Util.Clip((float)color.Z, 0.0f, 1.0f);
                tex.FaceTextures[face].RGBA = texcolor;
                UpdateTexture(tex);
                TriggerScriptChangedEvent(Changed.COLOR);
                return;
            }
            else if (face == ALL_SIDES)
            {
                for (uint i = 0; i < GetNumberOfSides(); i++)
                {
                    if (tex.FaceTextures[i] != null)
                    {
                        texcolor = tex.FaceTextures[i].RGBA;
                        texcolor.R = Util.Clip((float)color.X, 0.0f, 1.0f);
                        texcolor.G = Util.Clip((float)color.Y, 0.0f, 1.0f);
                        texcolor.B = Util.Clip((float)color.Z, 0.0f, 1.0f);
                        tex.FaceTextures[i].RGBA = texcolor;
                    }
                    texcolor = tex.DefaultTexture.RGBA;
                    texcolor.R = Util.Clip((float)color.X, 0.0f, 1.0f);
                    texcolor.G = Util.Clip((float)color.Y, 0.0f, 1.0f);
                    texcolor.B = Util.Clip((float)color.Z, 0.0f, 1.0f);
                    tex.DefaultTexture.RGBA = texcolor;
                }
                UpdateTexture(tex);
                TriggerScriptChangedEvent(Changed.COLOR);
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
                    ret = 1;
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
                if (Shape.PathCurve == (byte)Extrusion.Straight)
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
                if (Shape.PathCurve == (byte)Extrusion.Straight)
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
        
        public void SetVehicleFlags(int param, bool remove)
        {
            if (PhysActor != null)
            {
                PhysActor.VehicleFlags(param, remove);
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
        /// Set the parent group of this prim.
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

        /// <summary>
        /// Set the events that this part will pass on to listeners.
        /// </summary>
        /// <param name="scriptid"></param>
        /// <param name="events"></param>
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

            if (ParentGroup != null)
            {
                ParentGroup.HasGroupChanged = true;
                ScheduleFullUpdate();
            }
        }
        
        public void StopLookAt()
        {
            m_parentGroup.stopLookAt();

            m_parentGroup.ScheduleGroupForTerseUpdate();
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
            m_parentGroup.stopMoveToTarget();

            m_parentGroup.ScheduleGroupForTerseUpdate();
            //m_parentGroup.ScheduleGroupForFullUpdate();
        }

        public void StoreUndoState()
        {
            StoreUndoState(false);
        }

        public void StoreUndoState(bool forGroup)
        {
            if (!Undoing)
            {
                if (!IgnoreUndoUpdate)
                {
                    if (ParentGroup != null)
                    {
                        lock (m_undo)
                        {
                            if (m_undo.Count > 0)
                            {
                                UndoState last = m_undo.Peek();
                                if (last != null)
                                {
                                    // TODO: May need to fix for group comparison
                                    if (last.Compare(this))
                                    {
    //                                        m_log.DebugFormat(
    //                                            "[SCENE OBJECT PART]: Not storing undo for {0} {1} since current state is same as last undo state, initial stack size {2}",
    //                                            Name, LocalId, m_undo.Count);
    
                                        return;
                                    }
                                }
                            }
    
    //                            m_log.DebugFormat(
    //                                "[SCENE OBJECT PART]: Storing undo state for {0} {1}, forGroup {2}, initial stack size {3}",
    //                                Name, LocalId, forGroup, m_undo.Count);
    
                            if (m_parentGroup.GetSceneMaxUndo() > 0)
                            {
                                UndoState nUndo = new UndoState(this, forGroup);
    
                                m_undo.Push(nUndo);
    
                                if (m_redo.Count > 0)
                                    m_redo.Clear();
    
    //                                m_log.DebugFormat(
    //                                    "[SCENE OBJECT PART]: Stored undo state for {0} {1}, forGroup {2}, stack size now {3}",
    //                                    Name, LocalId, forGroup, m_undo.Count);
                            }
                        }
                    }
                }
//                else
//                {
//                    m_log.DebugFormat("[SCENE OBJECT PART]: Ignoring undo store for {0} {1}", Name, LocalId);
//                }
            }
//            else
//            {
//                m_log.DebugFormat(
//                    "[SCENE OBJECT PART]: Ignoring undo store for {0} {1} since already undoing", Name, LocalId);
//            }
        }

        /// <summary>
        /// Return number of undos on the stack.  Here temporarily pending a refactor.
        /// </summary>
        public int UndoCount
        {
            get
            {
                lock (m_undo)
                    return m_undo.Count;
            }
        }

        public void Undo()
        {
            lock (m_undo)
            {
//                m_log.DebugFormat(
//                    "[SCENE OBJECT PART]: Handling undo request for {0} {1}, stack size {2}",
//                    Name, LocalId, m_undo.Count);

                if (m_undo.Count > 0)
                {
                    UndoState goback = m_undo.Pop();

                    if (goback != null)
                    {
                        UndoState nUndo = null;
        
                        if (m_parentGroup.GetSceneMaxUndo() > 0)
                        {
                            nUndo = new UndoState(this, goback.ForGroup);
                        }

                        goback.PlaybackState(this);

                        if (nUndo != null)
                            m_redo.Push(nUndo);
                    }
                }

//                m_log.DebugFormat(
//                    "[SCENE OBJECT PART]: Handled undo request for {0} {1}, stack size now {2}",
//                    Name, LocalId, m_undo.Count);
            }
        }

        public void Redo()
        {
            lock (m_undo)
            {
//                m_log.DebugFormat(
//                    "[SCENE OBJECT PART]: Handling redo request for {0} {1}, stack size {2}",
//                    Name, LocalId, m_redo.Count);

                if (m_redo.Count > 0)
                {
                    UndoState gofwd = m_redo.Pop();
    
                    if (gofwd != null)
                    {
                        if (m_parentGroup.GetSceneMaxUndo() > 0)
                        {
                            UndoState nUndo = new UndoState(this, gofwd.ForGroup);
    
                            m_undo.Push(nUndo);
                        }
    
                        gofwd.PlayfwdState(this);
                    }

//                m_log.DebugFormat(
//                    "[SCENE OBJECT PART]: Handled redo request for {0} {1}, stack size now {2}",
//                    Name, LocalId, m_redo.Count);
                }
            }
        }

        public void ClearUndoState()
        {
//            m_log.DebugFormat("[SCENE OBJECT PART]: Clearing undo and redo stacks in {0} {1}", Name, LocalId);

            lock (m_undo)
            {
                m_undo.Clear();
                m_redo.Clear();
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
        public void ToXml(XmlTextWriter xmlWriter)
        {
            SceneObjectSerializer.SOPToXml2(xmlWriter, this, new Dictionary<string, object>());
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

            if (ParentGroup != null)
            {
                ParentGroup.HasGroupChanged = true;
                ScheduleFullUpdate();
            }
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
                        // Prevent the client from creating no mod, no copy
                        // objects
                        if ((_nextOwnerMask & (uint)PermissionMask.Copy) == 0)
                            _nextOwnerMask |= (uint)PermissionMask.Transfer;

                        _nextOwnerMask |= (uint)PermissionMask.Move;

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

        /// <summary>
        /// Update the flags on this prim.  This covers properties such as phantom, physics and temporary.
        /// </summary>
        /// <param name="UsePhysics"></param>
        /// <param name="SetTemporary"></param>
        /// <param name="SetPhantom"></param>
        /// <param name="SetVD"></param>
        public void UpdatePrimFlags(bool UsePhysics, bool SetTemporary, bool SetPhantom, bool SetVD)
        {
            bool wasUsingPhysics = ((Flags & PrimFlags.Physics) != 0);
            bool wasTemporary = ((Flags & PrimFlags.TemporaryOnRez) != 0);
            bool wasPhantom = ((Flags & PrimFlags.Phantom) != 0);
            bool wasVD = VolumeDetectActive;

            if ((UsePhysics == wasUsingPhysics) && (wasTemporary == SetTemporary) && (wasPhantom == SetPhantom) && (SetVD == wasVD))
            {
                return;
            }

            // Special cases for VD. VD can only be called from a script 
            // and can't be combined with changes to other states. So we can rely
            // that...
            // ... if VD is changed, all others are not.
            // ... if one of the others is changed, VD is not.
            if (SetVD) // VD is active, special logic applies
            {
                // State machine logic for VolumeDetect
                // More logic below
                bool phanReset = (SetPhantom != wasPhantom) && !SetPhantom;

                if (phanReset) // Phantom changes from on to off switch VD off too
                {
                    SetVD = false;               // Switch it of for the course of this routine
                    VolumeDetectActive = false; // and also permanently
                    if (PhysActor != null)
                        PhysActor.SetVolumeDetect(0);   // Let physics know about it too
                }
                else
                {
                    // If volumedetect is active we don't want phantom to be applied.
                    // If this is a new call to VD out of the state "phantom"
                    // this will also cause the prim to be visible to physics
                    SetPhantom = false;
                }
            }

            if (UsePhysics && IsJoint())
            {
                SetPhantom = true;
            }

            if (UsePhysics)
            {
                AddFlag(PrimFlags.Physics);
                if (!wasUsingPhysics)
                {
                    DoPhysicsPropertyUpdate(UsePhysics, false);

                    if (!m_parentGroup.IsDeleted)
                    {
                        if (LocalId == m_parentGroup.RootPart.LocalId)
                        {
                            m_parentGroup.CheckSculptAndLoad();
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

            if (SetPhantom
                || ParentGroup.IsAttachment
                || (Shape.PathCurve == (byte)Extrusion.Flexible)) // note: this may have been changed above in the case of joints
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

                if (ParentGroup.Scene == null)
                    return;

                PhysicsActor pa = PhysActor;

                if (pa == null)
                {
                    // It's not phantom anymore. So make sure the physics engine get's knowledge of it
                    PhysActor = m_parentGroup.Scene.PhysicsScene.AddPrimShape(
                        string.Format("{0}/{1}", Name, UUID),
                        Shape,
                        AbsolutePosition,
                        Scale,
                        RotationOffset,
                        UsePhysics,
                        m_localId);

                    pa = PhysActor;
                    if (pa != null)
                    {
                        PhysActor.SetMaterial(Material);
                        DoPhysicsPropertyUpdate(UsePhysics, true);

                        if (!m_parentGroup.IsDeleted)
                        {
                            if (LocalId == m_parentGroup.RootPart.LocalId)
                            {
                                m_parentGroup.CheckSculptAndLoad();
                            }
                        }

                        if (
                            ((AggregateScriptEvents & scriptEvents.collision) != 0) ||
                            ((AggregateScriptEvents & scriptEvents.collision_end) != 0) ||
                            ((AggregateScriptEvents & scriptEvents.collision_start) != 0) ||
                            ((AggregateScriptEvents & scriptEvents.land_collision_start) != 0) ||
                            ((AggregateScriptEvents & scriptEvents.land_collision) != 0) ||
                            ((AggregateScriptEvents & scriptEvents.land_collision_end) != 0) ||
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

                    if (!m_parentGroup.IsDeleted)
                    {
                        if (LocalId == m_parentGroup.RootPart.LocalId)
                        {
                            m_parentGroup.CheckSculptAndLoad();
                        }
                    }
                }
            }

            if (SetVD)
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
            {
                // Remove VolumeDetect in any case. Note, it's safe to call SetVolumeDetect as often as you like
                // (mumbles, well, at least if you have infinte CPU powers :-))
                PhysicsActor pa = this.PhysActor;
                if (pa != null)
                {
                    PhysActor.SetVolumeDetect(0);
                }

                this.VolumeDetectActive = false;
            }

            if (SetTemporary)
            {
                AddFlag(PrimFlags.TemporaryOnRez);
            }
            else
            {
                RemFlag(PrimFlags.TemporaryOnRez);
            }
            //            m_log.Debug("Update:  PHY:" + UsePhysics.ToString() + ", T:" + IsTemporary.ToString() + ", PHA:" + IsPhantom.ToString() + " S:" + CastsShadows.ToString());

            if (ParentGroup != null)
            {
                ParentGroup.HasGroupChanged = true;
                ScheduleFullUpdate();
            }

//            m_log.DebugFormat("[SCENE OBJECT PART]: Updated PrimFlags on {0} {1} to {2}", Name, LocalId, Flags);
        }

        public void UpdateRotation(Quaternion rot)
        {
            if ((rot.X != RotationOffset.X) ||
                (rot.Y != RotationOffset.Y) ||
                (rot.Z != RotationOffset.Z) ||
                (rot.W != RotationOffset.W))
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
            TriggerScriptChangedEvent(Changed.SHAPE);
            ScheduleFullUpdate();
        }

        /// <summary>
        /// If the part is a sculpt/mesh, retrieve the mesh data and reinsert it into the shape so that the physics
        /// engine can use it.
        /// </summary>
        /// <remarks>
        /// When the physics engine has finished with it, the sculpt data is discarded to save memory.
        /// </remarks>
        public void CheckSculptAndLoad()
        {
//            m_log.DebugFormat("Processing CheckSculptAndLoad for {0} {1}", Name, LocalId);

            if (ParentGroup.IsDeleted)
                return;

            if ((ParentGroup.RootPart.GetEffectiveObjectFlags() & (uint)PrimFlags.Phantom) != 0)
                return;

            if (Shape.SculptEntry && Shape.SculptTexture != UUID.Zero)
            {
                // check if a previously decoded sculpt map has been cached
                // We don't read the file here - the meshmerizer will do that later.
                // TODO: Could we simplify the meshmerizer code by reading and setting the data here?
                if (File.Exists(System.IO.Path.Combine("j2kDecodeCache", "smap_" + Shape.SculptTexture.ToString())))
                {
                    SculptTextureCallback(null);
                }
                else
                {
                    ParentGroup.Scene.AssetService.Get(Shape.SculptTexture.ToString(), this, AssetReceived);
                }
            }
        }

        /// <summary>
        /// Update the textures on the part.
        /// </summary>
        /// <remarks>
        /// Added to handle bug in libsecondlife's TextureEntry.ToBytes()
        /// not handling RGBA properly. Cycles through, and "fixes" the color
        /// info
        /// </remarks>
        /// <param name="tex"></param>
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
                ((AggregateScriptEvents & scriptEvents.land_collision_start) != 0) ||
                ((AggregateScriptEvents & scriptEvents.land_collision) != 0) ||
                ((AggregateScriptEvents & scriptEvents.land_collision_end) != 0) ||
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

            //if ((GetEffectiveObjectFlags() & (uint)PrimFlags.Scripted) != 0)
            //{
            //    m_parentGroup.Scene.EventManager.OnScriptTimerEvent += handleTimerAccounting;
            //}
            //else
            //{
            //    m_parentGroup.Scene.EventManager.OnScriptTimerEvent -= handleTimerAccounting;
            //}

            LocalFlags = (PrimFlags)objectflagupdate;

            if (m_parentGroup != null && m_parentGroup.RootPart == this)
            {
                m_parentGroup.aggregateScriptEvents();
            }
            else
            {
//                m_log.DebugFormat(
//                    "[SCENE OBJECT PART]: Scheduling part {0} {1} for full update in aggregateScriptEvents()", Name, LocalId);
                ScheduleFullUpdate();
            }
        }

        public int registerTargetWaypoint(Vector3 target, float tolerance)
        {
            return m_parentGroup.registerTargetWaypoint(target, tolerance);
        }

        public void unregisterTargetWaypoint(int handle)
        {
            m_parentGroup.unregisterTargetWaypoint(handle);
        }

        public int registerRotTargetWaypoint(Quaternion target, float tolerance)
        {
            return m_parentGroup.registerRotTargetWaypoint(target, tolerance);
        }

        public void unregisterRotTargetWaypoint(int handle)
        {
            m_parentGroup.unregisterRotTargetWaypoint(handle);
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

            if (ParentGroup.IsAttachment && ParentGroup.RootPart != this)
                return;
            
            // Causes this thread to dig into the Client Thread Data.
            // Remember your locking here!
            remoteClient.SendPrimUpdate(this, PrimUpdateFlags.Position | PrimUpdateFlags.Rotation | PrimUpdateFlags.Velocity | PrimUpdateFlags.Acceleration | PrimUpdateFlags.AngularVelocity);
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

        public void UpdateLookAt()
        {
            try
            {
                if (APIDTarget != Quaternion.Identity)
                {
                    if (Single.IsNaN(APIDTarget.W) == true)
                    {
                        APIDTarget = Quaternion.Identity;
                        return;
                    }
                    Quaternion rot = RotationOffset;
                    Quaternion dir = (rot - APIDTarget);
                    float speed = ((APIDStrength / APIDDamp) * (float)(Math.PI / 180.0f));
                    if (dir.Z > speed)
                    {
                        rot.Z -= speed;
                    }
                    if (dir.Z < -speed)
                    {
                        rot.Z += speed;
                    }
                    rot.Normalize();
                    UpdateRotation(rot);
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
    }
}
