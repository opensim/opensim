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
using System.Xml;
using System.Xml.Serialization;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    // I don't really know where to put this except here.  
    // Can't access the OpenSim.Region.ScriptEngine.Common.LSL_BaseClass.Changed constants

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

    public partial class SceneObjectPart : IScriptHost
    {

        [XmlIgnore] public PhysicsActor PhysActor = null;
        
        public LLUUID LastOwnerID;
        public LLUUID OwnerID;
        public LLUUID GroupID;
        public int OwnershipCost;
        public byte ObjectSaleType;
        public int SalePrice;
        public uint Category;

        public Int32 CreationDate;
        public uint ParentID = 0;

        private Vector3 m_sitTargetPosition = new Vector3(0, 0, 0);
        private Quaternion m_sitTargetOrientation = new Quaternion(0, 0, 0, 1);
        private LLUUID m_sitTargetAvatar = LLUUID.Zero;

        #region Permissions

        public uint BaseMask = (uint)PermissionMask.All;
        public uint OwnerMask = (uint)PermissionMask.All;
        public uint GroupMask = (uint)PermissionMask.None;
        public uint EveryoneMask = (uint)PermissionMask.None;
        public uint NextOwnerMask = (uint)PermissionMask.All;

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
        /// Only used internally to schedule client updates
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
                return m_groupPosition;
            }
            set
            {   
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
            set { m_offsetPosition = value;
            try
            {
                // Hack to get the child prim to update world positions in the physics engine
                ParentGroup.ResetChildPrimPhysicsPositions();
                
            }
            catch (System.NullReferenceException)
            {
                // Ignore, and skip over.
            }
            //MainLog.Instance.Verbose("PART", "OFFSET:" + m_offsetPosition, ToString());
            }
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
                m_rotationOffset = value;

                if (PhysActor != null)
                {
                    try
                    {
                        // Root prim gets value directly
                        if (ParentID == 0)
                        {
                            PhysActor.Orientation = new Quaternion(value.W, value.X, value.Y, value.Z);
                            //MainLog.Instance.Verbose("PART", "RO1:" + PhysActor.Orientation.ToString());
                        }
                        else
                        {
                            // Child prim we have to calculate it's world rotationwel
                            LLQuaternion resultingrotation = GetWorldRotation();
                            PhysActor.Orientation = new Quaternion(resultingrotation.W, resultingrotation.X, resultingrotation.Y, resultingrotation.Z);
                            //MainLog.Instance.Verbose("PART", "RO2:" + PhysActor.Orientation.ToString());
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
            get { return m_text; }
            set
            {
                m_text = value;
                ScheduleFullUpdate();
            }
        }

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
                ScheduleFullUpdate();
            }
        }

        protected PrimitiveBaseShape m_shape;

        /// <summary>
        /// hook to the physics scene to apply impulse
        /// This is sent up to the group, which then finds the root prim
        /// and applies the force on the root prim of the group
        /// </summary>
        /// <param name="impulse">Vector force</param>
        public void ApplyImpulse(LLVector3 impulsei)
        {
            PhysicsVector impulse = new PhysicsVector(impulsei.X, impulsei.Y, impulsei.Z);
            if (m_parentGroup != null)
            {
                m_parentGroup.applyImpulse(impulse);
            }
        }

        public void TriggerScriptChangedEvent(Changed val)
        {
            if (m_parentGroup != null)
            {
                if (m_parentGroup.Scene != null)
                    m_parentGroup.Scene.TriggerObjectChanged(LocalID, (uint)val);
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
            m_inventoryFileName = "taskinventory" + LLUUID.Random().ToString();
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
            LocalID = (uint) (localID);
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
            m_inventoryFileName = "taskinventory" + LLUUID.Random().ToString();
            m_folderID = LLUUID.Random();

            Flags = 0;
            Flags |= LLObject.ObjectFlags.Touch |
                       LLObject.ObjectFlags.AllowInventoryDrop |
                       LLObject.ObjectFlags.CreateSelected;

            TrimPermissions();

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
            LocalID = (uint) (localID);
            Shape = shape;
            OwnershipCost = 0;
            ObjectSaleType = (byte) 0;
            SalePrice = 0;
            Category = (uint) 0;
            LastOwnerID = CreatorID;
            OffsetPosition = position;
            RotationOffset = rotation;
            ObjectFlags = flags;

            TrimPermissions();
            // ApplyPhysics();

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


                DoPhysicsPropertyUpdate(RigidBody, true);
            }
        }

        public void ApplyNextOwnerPermissions()
        {
            BaseMask = NextOwnerMask;
            OwnerMask = NextOwnerMask;

            TriggerScriptChangedEvent(Changed.OWNER);
            
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
        /// 
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
            returnresult.normal = normalpart.Normalize();

            // It's funny how the LLVector3 object has a Distance function, but the Axiom.Math object doesnt.
            // I can write a function to do it..    but I like the fact that this one is Static.

            LLVector3 distanceConvert1 = new LLVector3(iray.Origin.x, iray.Origin.y, iray.Origin.z);
            LLVector3 distanceConvert2 = new LLVector3(ipoint.x, ipoint.y, ipoint.z);
            float distance = (float) Util.GetDistanceTo(distanceConvert1, distanceConvert2);

            returnresult.distance = distance;

            return returnresult;
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
        public SceneObjectPart Copy(uint localID, LLUUID AgentID, LLUUID GroupID, int linkNum)
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
            
            dupe.TaskInventory = (TaskInventoryDictionary)dupe.TaskInventory.Clone();
            
            dupe.ResetIDs(linkNum);

            // This may be wrong...    it might have to be applied in SceneObjectGroup to the object that's being duplicated.
            dupe.LastOwnerID = ObjectOwner;

            byte[] extraP = new byte[Shape.ExtraParams.Length];
            Array.Copy(Shape.ExtraParams, extraP, extraP.Length);
            dupe.Shape.ExtraParams = extraP;
            bool UsePhysics = ((dupe.ObjectFlags & (uint) LLObject.ObjectFlags.Physics) != 0);
            dupe.DoPhysicsPropertyUpdate(UsePhysics, true);

            return dupe;
        }

        #endregion
        
        /// <summary>
        /// Reset LLUUIDs for this part.  This involves generate this part's own LLUUID and
        /// generating new LLUUIDs for all the items in the inventory.
        /// </summary>
        /// <param name="linkNum'>Link number for the part</param>
        public void ResetIDs(int linkNum)
        {
            UUID = LLUUID.Random();
            LinkNum = linkNum;      
            
            ResetInventoryIDs();
        }

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
                m_parentGroup.HasGroupChanged = true;
            }
            TimeStampFull = (uint) Util.UnixTimeSinceEpoch();
            m_updateFlag = 2;
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
        /// 
        /// </summary>
        public void ScheduleTerseUpdate()
        {
            if (m_updateFlag < 1)
            {
                if (m_parentGroup != null)
                {
                    m_parentGroup.HasGroupChanged = true;
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

        #region ExtraParams

        public void UpdatePrimFlags(ushort type, bool inUse, byte[] data)
        {
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
                    DoPhysicsPropertyUpdate(usePhysics, true);
                }
                else
                {
                    PhysActor.IsPhysical = usePhysics;
                    DoPhysicsPropertyUpdate(usePhysics, false);
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
                        }
                    }
                }
                m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
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
            data[pos] = (byte)pTexAnim.SizeX; pos++;

            Helpers.FloatToBytes(pTexAnim.Start).CopyTo(data, pos);
            Helpers.FloatToBytes(pTexAnim.Length ).CopyTo(data, pos + 4);
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

        public void UpdatePermissions(LLUUID AgentID, byte field, uint localID, uint mask, byte addRemTF)
        {
            // Are we the owner?
            if (AgentID == OwnerID)
            {
                MainLog.Instance.Verbose("PERMISSIONS",
                                         "field: " + field.ToString() + ", mask: " + mask.ToString() + " addRemTF: " +
                                         addRemTF.ToString());

                //Field 8 = EveryoneMask
                if (field == (byte) 8)
                {
                    MainLog.Instance.Verbose("PERMISSIONS", "Left over: " + (OwnerMask - EveryoneMask));
                    if (addRemTF == (byte) 0)
                    {
                        //EveryoneMask = (uint)0;
                        EveryoneMask &= ~mask;
                        //EveryoneMask &= ~(uint)57344;
                    }
                    else
                    {
                        //EveryoneMask = (uint)0;
                        EveryoneMask |= mask;
                        //EveryoneMask |= (uint)57344;
                    }
                    //ScheduleFullUpdate();
                    SendFullUpdateToAllClients();
                }
                //Field 16 = NextownerMask
                if (field == (byte) 16)
                {
                    if (addRemTF == (byte) 0)
                    {
                        NextOwnerMask &= ~mask;
                    }
                    else
                    {
                        NextOwnerMask |= mask;
                    }
                    SendFullUpdateToAllClients();
                }
            }
        }

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
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        public void SendFullUpdateToClient(IClientAPI remoteClient, uint clientflags)
        {
            LLVector3 lPos;
            lPos = OffsetPosition;
            SendFullUpdateToClient(remoteClient, lPos, clientflags);
        }
   
        public void SendFullUpdateToClient(IClientAPI remoteClient, LLVector3 lPos, uint clientFlags)
        {
            LLQuaternion lRot;
            lRot = RotationOffset;
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
            remoteClient.SendPrimitiveToClient(m_regionHandle, 64096, LocalID, m_shape, lPos, clientFlags, m_uuid,
                                               OwnerID,
                                               m_text, color, ParentID, m_particleSystem, lRot, m_clickAction, m_TextureAnimation);
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
            if ((ObjectFlags & (uint) LLObject.ObjectFlags.Physics) == 0)
            {
                remoteClient.SendPrimTerseUpdate(m_regionHandle, 64096, LocalID, lPos, mRot);
            }
            else
            {
                remoteClient.SendPrimTerseUpdate(m_regionHandle, 64096, LocalID, lPos, mRot, Velocity,
                                                 RotationalVelocity);
            }
        }

        public void SendTerseUpdateToClient(IClientAPI remoteClient, LLVector3 lPos)
        {
            LLQuaternion mRot = RotationOffset;
            if ((ObjectFlags & (uint) LLObject.ObjectFlags.Physics) == 0)
            {
                remoteClient.SendPrimTerseUpdate(m_regionHandle, 64096, LocalID, lPos, mRot);
            }
            else
            {
                remoteClient.SendPrimTerseUpdate(m_regionHandle, 64096, LocalID, lPos, mRot, Velocity,
                                                 RotationalVelocity);
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
            MainLog.Instance.Verbose("PHYSICS", "Physical Object went out of bounds.");
            RemFlag(LLObject.ObjectFlags.Physics);
            DoPhysicsPropertyUpdate(false, true);
            m_parentGroup.Scene.PhysicsScene.AddPhysicsActorTaint(PhysActor);
        }

        public virtual void OnGrab(LLVector3 offsetPos, IClientAPI remoteClient)
        {
        }

        public void SetText(string text, Vector3 color, double alpha)
        {
            Color = Color.FromArgb(0xff - (int) (alpha*0xff),
                                   (int) (color.x*0xff),
                                   (int) (color.y*0xff),
                                   (int) (color.z*0xff));
            Text = text;
        }
    }
}
