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

        private ulong m_regionHandle;
        private uint m_flags = 32 + 65536 + 131072 + 256 + 4 + 8 + 2048 + 524288 + 268435456 + 128; // HOUSEKEEPING : Do we really need this?
        //private Dictionary<LLUUID, InventoryItem> inventoryItems;

        public string SitName = "";
        public string TouchName = "";
        public string Text = "";

        public LLUUID CreatorID;
        public LLUUID OwnerID;
        public LLUUID LastOwnerID;
        public Int32 CreationDate;

        public LLUUID uuid;
        public uint m_localID;

        public uint ParentID = 0;

        public uint OwnerMask = FULL_MASK_PERMISSIONS;
        public uint NextOwnerMask = FULL_MASK_PERMISSIONS;
        public uint GroupMask = FULL_MASK_PERMISSIONS;
        public uint EveryoneMask = FULL_MASK_PERMISSIONS;
        public uint BaseMask = FULL_MASK_PERMISSIONS;

        protected PrimitiveBaseShape m_Shape;

        protected AllNewSceneObjectGroup2 m_parentGroup;


        #region Properties
        protected string m_name;
        /// <summary>
        /// 
        /// </summary>
        public virtual string Name
        {
            get { return m_name; }
            set { m_name = value; }
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
            this.uuid = LLUUID.Random();
            this.m_localID = (uint)(localID);
            this.m_Shape = shape;

            this.OffsetPosition = position;
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

