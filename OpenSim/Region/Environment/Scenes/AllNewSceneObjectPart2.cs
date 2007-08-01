using System.Collections.Generic;
using System.Text;
using System;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenSim.Region.Environment.Scenes
{

    public class AllNewSceneObjectPart2
    {
        private const uint FULL_MASK_PERMISSIONS = 2147483647;

        public string SitName = "";
        public string TouchName = "";
        public string Text = "";

        public LLUUID CreatorID;
        public LLUUID OwnerID;
        public LLUUID GroupID;
        public LLUUID LastOwnerID;
        public Int32 CreationDate;
        public uint ParentID = 0;

        public uint OwnerMask = FULL_MASK_PERMISSIONS;
        public uint NextOwnerMask = FULL_MASK_PERMISSIONS;
        public uint GroupMask = FULL_MASK_PERMISSIONS;
        public uint EveryoneMask = FULL_MASK_PERMISSIONS;
        public uint BaseMask = FULL_MASK_PERMISSIONS;

        protected PrimitiveBaseShape m_Shape;
        protected byte[] m_particleSystem = new byte[0];

        protected AllNewSceneObjectGroup2 m_parentGroup;

        #region Properties
        
        protected LLUUID m_uuid;
        public LLUUID UUID
        {
            get
            {
                return m_uuid;
            }
            set
            {
                value = m_uuid;
            }
        }

        protected uint m_localID;
        public uint LocalID
        {
            get
            {
                return m_localID;
            }
            set
            {
                m_localID = value;
            }
        }

        protected string m_name;
        /// <summary>
        /// 
        /// </summary>
        public virtual string Name
        {
            get { return m_name; }
            set { m_name = value; }
        }

        protected LLObject.ObjectFlags m_flags = (LLObject.ObjectFlags) 32 + 65536 + 131072 + 256 + 4 + 8 + 2048 + 524288 + 268435456 + 128;
        public uint ObjectFlags
        {
            get
            {
                return (uint)m_flags;
            }
            set
            {
                m_flags =(LLObject.ObjectFlags) value;
            }
        }

        protected LLObject.MaterialType m_material;
        public byte Material
        {
            get
            {
                return (byte)m_material;
            }
            set
            {
                m_material = (LLObject.MaterialType) value;
            }
        }

        protected ulong m_regionHandle;
        public ulong RegionHandle
        {
            get
            {
                return m_regionHandle;
            }
            set
            {
                m_regionHandle = value;
            }
        }

        protected LLVector3 m_offset;
        public LLVector3 OffsetPosition
        {
            get
            {
                return m_offset;
            }
            set
            {
                m_offset = value;
            }
        }

        protected LLQuaternion m_rotationOffset;
        public LLQuaternion RotationOffset
        {
            get
            {
                return m_rotationOffset;
            }
            set
            {
                m_rotationOffset = value;
            }
        }

        protected LLVector3 m_velocity;
        /// <summary></summary>
        public LLVector3 Velocity
        {
            get
            {
                return m_velocity;
            }
            set
            {
                m_velocity = value;
            }
        }

        protected LLVector3 m_angularVelocity;
        /// <summary></summary>
        public LLVector3 AngularVelocity
        {
            get
            {
                return m_angularVelocity;
            }
            set
            {
                m_angularVelocity = value;
            }
        }

        protected LLVector3 m_acceleration;
        /// <summary></summary>
        public LLVector3 Acceleration
        {
            get
            {
                return m_acceleration;
            }
            set
            {
                m_acceleration = value;
            }
        }

        private string m_description = "";
        public string Description
        {
            get
            {
                return this.m_description;
            }
            set
            {
                this.m_description = value;
            }
        }

        public PrimitiveBaseShape Shape
        {
            get
            {
                return this.m_Shape;
            }
            set
            {
                m_Shape = value;
            }
        }

        public LLVector3 Scale
        {
            set
            {
                this.m_Shape.Scale = value;
            }
            get
            {
                return this.m_Shape.Scale;
            }
        }
        #endregion

        #region Constructors
        public AllNewSceneObjectPart2(ulong regionHandle, AllNewSceneObjectGroup2 parent, LLUUID ownerID, uint localID, PrimitiveBaseShape shape, LLVector3 position)
        {
            this.m_regionHandle = regionHandle;
            this.m_parentGroup = parent;

            this.CreationDate = (Int32)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            this.OwnerID = ownerID;
            this.CreatorID = this.OwnerID;
            this.LastOwnerID = LLUUID.Zero;
            this.UUID = LLUUID.Random();
            this.LocalID = (uint)(localID);
            this.m_Shape = shape;

            this.OffsetPosition = position;

            //temporary code just so the m_flags field doesn't give a compiler warning
            if (m_flags ==LLObject.ObjectFlags.AllowInventoryDrop)
            {

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
            this.m_Shape.PathBegin = shapeBlock.PathBegin;
            this.m_Shape.PathEnd = shapeBlock.PathEnd;
            this.m_Shape.PathScaleX = shapeBlock.PathScaleX;
            this.m_Shape.PathScaleY = shapeBlock.PathScaleY;
            this.m_Shape.PathShearX = shapeBlock.PathShearX;
            this.m_Shape.PathShearY = shapeBlock.PathShearY;
            this.m_Shape.PathSkew = shapeBlock.PathSkew;
            this.m_Shape.ProfileBegin = shapeBlock.ProfileBegin;
            this.m_Shape.ProfileEnd = shapeBlock.ProfileEnd;
            this.m_Shape.PathCurve = shapeBlock.PathCurve;
            this.m_Shape.ProfileCurve = shapeBlock.ProfileCurve;
            this.m_Shape.ProfileHollow = shapeBlock.ProfileHollow;
            this.m_Shape.PathRadiusOffset = shapeBlock.PathRadiusOffset;
            this.m_Shape.PathRevolutions = shapeBlock.PathRevolutions;
            this.m_Shape.PathTaperX = shapeBlock.PathTaperX;
            this.m_Shape.PathTaperY = shapeBlock.PathTaperY;
            this.m_Shape.PathTwist = shapeBlock.PathTwist;
            this.m_Shape.PathTwistBegin = shapeBlock.PathTwistBegin;
        }
        #endregion

        #region Inventory
        public void GetInventory(IClientAPI client, uint localID)
        {
            if (localID == this.m_localID)
            {
                client.SendTaskInventory(this.m_uuid, 0, new byte[0]);
            }
        }
        #endregion

        public void UpdateExtraParam(ushort type, bool inUse, byte[] data)
        {
            this.m_Shape.ExtraParams = new byte[data.Length + 7];
            int i = 0;
            uint length = (uint)data.Length;
            this.m_Shape.ExtraParams[i++] = 1;
            this.m_Shape.ExtraParams[i++] = (byte)(type % 256);
            this.m_Shape.ExtraParams[i++] = (byte)((type >> 8) % 256);

            this.m_Shape.ExtraParams[i++] = (byte)(length % 256);
            this.m_Shape.ExtraParams[i++] = (byte)((length >> 8) % 256);
            this.m_Shape.ExtraParams[i++] = (byte)((length >> 16) % 256);
            this.m_Shape.ExtraParams[i++] = (byte)((length >> 24) % 256);
            Array.Copy(data, 0, this.m_Shape.ExtraParams, i, data.Length);

            //this.ScheduleFullUpdate();
        }


        #region Texture
        /// <summary>
        /// 
        /// </summary>
        /// <param name="textureEntry"></param>
        public void UpdateTextureEntry(byte[] textureEntry)
        {
            this.m_Shape.TextureEntry = textureEntry;
        }
        #endregion

        public void AddNewParticleSystem(libsecondlife.Primitive.ParticleSystem pSystem)
        {
            this.m_particleSystem = pSystem.GetBytes();
        }


        #region Position
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        public void UpdateOffSet(LLVector3 pos)
        {
            LLVector3 newPos = new LLVector3(pos.X, pos.Y, pos.Z);
            this.OffsetPosition = newPos;
        }
        #endregion

        #region rotation
        public void UpdateRotation(LLQuaternion rot)
        {
            this.RotationOffset = new LLQuaternion(rot.X, rot.Y, rot.Z, rot.W);
        }
        #endregion

        #region Resizing/Scale
        /// <summary>
        /// 
        /// </summary>
        /// <param name="scale"></param>
        public void Resize(LLVector3 scale)
        {
            LLVector3 offset = (scale - this.m_Shape.Scale);
            offset.X /= 2;
            offset.Y /= 2;
            offset.Z /= 2;

            this.OffsetPosition += offset;
            this.m_Shape.Scale = scale;
        }
        #endregion
    }
}

