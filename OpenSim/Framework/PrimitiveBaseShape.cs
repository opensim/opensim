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

using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using System.Runtime.CompilerServices;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework
{
    public enum ProfileShape : byte
    {
        Circle = 0,
        Square = 1,
        IsometricTriangle = 2,
        EquilateralTriangle = 3,
        RightTriangle = 4,
        HalfCircle = 5
    }

    public enum HollowShape : byte
    {
        Same = 0,
        Circle = 16,
        Square = 32,
        Triangle = 48
    }

    public enum PCodeEnum : byte
    {
        Primitive = 9,
        Avatar = 47,
        Grass = 95,
        NewTree = 111,
        ParticleSystem = 143,
        Tree = 255
    }

    public enum Extrusion : byte
    {
        Straight = 16,
        Curve1 = 32,
        Curve2 = 48,
        Flexible = 128
    }

    [Serializable]
    public class PrimitiveBaseShape
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly byte[] DEFAULT_TEXTURE = new Primitive.TextureEntry(new UUID("89556747-24cb-43ed-920b-47caed15465f")).GetBytes();

        private byte[] m_textureEntry;

        private ushort _pathBegin;
        private byte _pathCurve;
        private ushort _pathEnd;
        private sbyte _pathRadiusOffset;
        private byte _pathRevolutions;
        private byte _pathScaleX;
        private byte _pathScaleY;
        private byte _pathShearX;
        private byte _pathShearY;
        private sbyte _pathSkew;
        private sbyte _pathTaperX;
        private sbyte _pathTaperY;
        private sbyte _pathTwist;
        private sbyte _pathTwistBegin;
        private byte _pCode;
        private ushort _profileBegin;
        private ushort _profileEnd;
        private ushort _profileHollow;
        private Vector3 _scale;
        private byte _state;
        private byte _lastattach;
        private ProfileShape _profileShape;
        private HollowShape _hollowShape;

        //extra parameters Sculpted
        [XmlIgnore] private UUID _sculptTexture;
        [XmlIgnore] private byte _sculptType;
        [XmlIgnore] private byte[] _sculptData = Utils.EmptyBytes;

        //extra parameters Flexi
        [XmlIgnore] private int _flexiSoftness;
        [XmlIgnore] private float _flexiTension;
        [XmlIgnore] private float _flexiDrag;
        [XmlIgnore] private float _flexiGravity;
        [XmlIgnore] private float _flexiWind;
        [XmlIgnore] private float _flexiForceX;
        [XmlIgnore] private float _flexiForceY;
        [XmlIgnore] private float _flexiForceZ;

        //extra parameters light
        [XmlIgnore] private float _lightColorR;
        [XmlIgnore] private float _lightColorG;
        [XmlIgnore] private float _lightColorB;
        [XmlIgnore] private float _lightColorA = 1.0f;
        [XmlIgnore] private float _lightRadius;
        [XmlIgnore] private float _lightCutoff;
        [XmlIgnore] private float _lightFalloff;
        [XmlIgnore] private float _lightIntensity = 1.0f;

        //extra parameters Projection
        [XmlIgnore] private UUID _projectionTextureID;
        [XmlIgnore] private float _projectionFOV;
        [XmlIgnore] private float _projectionFocus;
        [XmlIgnore] private float _projectionAmb;

        //extra parameters extramesh/flag
        [XmlIgnore] private uint _meshFlags;

        [XmlIgnore] private bool _flexiEntry;
        [XmlIgnore] private bool _lightEntry;
        [XmlIgnore] private bool _sculptEntry;
        [XmlIgnore] private bool _projectionEntry;
        [XmlIgnore] private bool _meshFlagsEntry;

        [XmlIgnore]
        public Primitive.ReflectionProbe ReflectionProbe = null;

        [XmlIgnore]
        public Primitive.RenderMaterials RenderMaterials = null;
        public bool MeshFlagEntry
        {
            get { return _meshFlagsEntry; }
        }

        public bool AnimeshEnabled
        {
            get
            {
                return(_meshFlagsEntry &&
                        (_meshFlags & 0x01) != 0 &&
                        (_sculptType & 0x07) == (int)OpenMetaverse.SculptType.Mesh);
            }
            set
            {
                if((_sculptType & 0x07) != (int)OpenMetaverse.SculptType.Mesh)
                {
                    _meshFlagsEntry = false;
                    _meshFlags = 0;
                    return;
                }

                if (value)
                {
                    _meshFlagsEntry = true;
                    _meshFlags |= 1;
                }
                else
                    _meshFlags &= 0xfe;
            }
        }

        public byte ProfileCurve
        {
            get { return (byte)((byte)HollowShape | (byte)ProfileShape); }

            set
            {
                // Handle hollow shape component
                byte hollowShapeByte = (byte)(value & 0xf0);

                if (!Enum.IsDefined(typeof(HollowShape), hollowShapeByte))
                {
                    m_log.WarnFormat(
                        "[SHAPE]: Attempt to set a ProfileCurve with a hollow shape value of {0}, which isn't a valid enum.  Replacing with default shape.",
                        hollowShapeByte);

                    _hollowShape = HollowShape.Same;
                }
                else
                {
                    _hollowShape = (HollowShape)hollowShapeByte;
                }

                // Handle profile shape component
                byte profileShapeByte = (byte)(value & 0xf);

                if (!Enum.IsDefined(typeof(ProfileShape), profileShapeByte))
                {
                    m_log.WarnFormat(
                        "[SHAPE]: Attempt to set a ProfileCurve with a profile shape value of {0}, which isn't a valid enum.  Replacing with square.",
                        profileShapeByte);

                    _profileShape = ProfileShape.Square;
                }
                else
                {
                    _profileShape = (ProfileShape)profileShapeByte;
                }
            }
        }

        /// <summary>
        /// Entries to store media textures on each face
        /// </summary>
        /// Do not change this value directly - always do it through an IMoapModule.
        /// Lock before manipulating.
        public MediaList Media { get; set; }

        public PrimitiveBaseShape()
        {
            PCode = (byte)PCodeEnum.Primitive;
            m_textureEntry = DEFAULT_TEXTURE;
        }

        /// <summary>
        /// Construct a PrimitiveBaseShape object from a OpenMetaverse.Primitive object
        /// </summary>
        /// <param name="prim"></param>
        public PrimitiveBaseShape(Primitive prim)
        {
            //m_log.DebugFormat("[PRIMITIVE BASE SHAPE]: Creating from {0}", prim.ID);

            PCode = (byte)prim.PrimData.PCode;

            State = prim.PrimData.State;
            LastAttachPoint = prim.PrimData.State;
            PathBegin = Primitive.PackBeginCut(prim.PrimData.PathBegin);
            PathEnd = Primitive.PackEndCut(prim.PrimData.PathEnd);
            PathScaleX = Primitive.PackPathScale(prim.PrimData.PathScaleX);
            PathScaleY = Primitive.PackPathScale(prim.PrimData.PathScaleY);
            PathShearX = (byte)Primitive.PackPathShear(prim.PrimData.PathShearX);
            PathShearY = (byte)Primitive.PackPathShear(prim.PrimData.PathShearY);
            PathSkew = Primitive.PackPathTwist(prim.PrimData.PathSkew);
            ProfileBegin = Primitive.PackBeginCut(prim.PrimData.ProfileBegin);
            ProfileEnd = Primitive.PackEndCut(prim.PrimData.ProfileEnd);
            Scale = prim.Scale;
            PathCurve = (byte)prim.PrimData.PathCurve;
            ProfileCurve = (byte)prim.PrimData.ProfileCurve;
            ProfileHollow = Primitive.PackProfileHollow(prim.PrimData.ProfileHollow);
            PathRadiusOffset = Primitive.PackPathTwist(prim.PrimData.PathRadiusOffset);
            PathRevolutions = Primitive.PackPathRevolutions(prim.PrimData.PathRevolutions);
            PathTaperX = Primitive.PackPathTaper(prim.PrimData.PathTaperX);
            PathTaperY = Primitive.PackPathTaper(prim.PrimData.PathTaperY);
            PathTwist = Primitive.PackPathTwist(prim.PrimData.PathTwist);
            PathTwistBegin = Primitive.PackPathTwist(prim.PrimData.PathTwistBegin);

            m_textureEntry = prim.Textures.GetBytes();

            if (prim.Sculpt != null)
            {
                SculptEntry = (prim.Sculpt.Type != OpenMetaverse.SculptType.None);
                SculptData = prim.Sculpt.GetBytes();
                SculptTexture = prim.Sculpt.SculptTexture;
                SculptType = (byte)prim.Sculpt.Type;
            }
            else
            {
                SculptType = (byte)OpenMetaverse.SculptType.None;
            }
        }

        [XmlIgnore]
        public Primitive.TextureEntry Textures
        {
            get
            {
                //m_log.DebugFormat("[SHAPE]: get m_textureEntry length {0}", m_textureEntry.Length);
                try { return new Primitive.TextureEntry(m_textureEntry, 0, m_textureEntry.Length); }
                catch { }

                m_log.Warn("[SHAPE]: Failed to decode texture, length=" + ((m_textureEntry != null) ? m_textureEntry.Length : 0));
                return new Primitive.TextureEntry(UUID.Zero);
            }

            set { m_textureEntry = value.GetBytes(); }
        }

        public byte[] TextureEntry
        {
            get { return m_textureEntry; }

            set
            {
                if (value == null)
                    m_textureEntry = new byte[1];
                else
                    m_textureEntry = value;
            }
        }

        public static PrimitiveBaseShape Default
        {
            get
            {
                PrimitiveBaseShape boxShape = CreateBox();
                boxShape.SetScale(0.5f);
                return boxShape;
            }
        }

        public static PrimitiveBaseShape Create()
        {
            return new PrimitiveBaseShape();
        }

        public static PrimitiveBaseShape CreateBox()
        {
            PrimitiveBaseShape shape = new()
            {
                _pathCurve = (byte)Extrusion.Straight,
                _profileShape = ProfileShape.Square,
                _pathScaleX = 100,
                _pathScaleY = 100
            };

            return shape;
        }

        public static PrimitiveBaseShape CreateSphere()
        {
            PrimitiveBaseShape shape = new()
            {
                _pathCurve = (byte)Extrusion.Curve1,
                _profileShape = ProfileShape.HalfCircle,
                _pathScaleX = 100,
                _pathScaleY = 100
            };

            return shape;
        }

        public static PrimitiveBaseShape CreateCylinder()
        {
            PrimitiveBaseShape shape = new()
            {
                _pathCurve = (byte)Extrusion.Curve1,
                _profileShape = ProfileShape.Square,
                _pathScaleX = 100,
                _pathScaleY = 100
            };
            return shape;
        }

        public static PrimitiveBaseShape CreateMesh(int numberOfFaces, UUID meshAssetID)
        {
            PrimitiveBaseShape shape = new()
            {
                _pathScaleX = 100,
                _pathScaleY = 100
            };

            if (numberOfFaces <= 0) // oops ?
                numberOfFaces = 1;

            switch(numberOfFaces)
            {
                case 1: // torus 
                    shape.ProfileCurve = (byte)ProfileShape.Circle | (byte)HollowShape.Triangle;
                    shape.PathCurve = (byte)Extrusion.Curve1;
                    shape._pathScaleY = 150;
                    break;

                case 2: // torus with hollow (a sl viewer whould see 4 faces on a hollow sphere)
                    shape.ProfileCurve = (byte)ProfileShape.Circle | (byte)HollowShape.Triangle;
                    shape.PathCurve = (byte)Extrusion.Curve1;
                    shape.ProfileHollow = 27500;
                    shape._pathScaleY = 150;
                    break;

                case 3: // cylinder
                    shape.ProfileCurve = (byte)ProfileShape.Circle | (byte)HollowShape.Triangle;
                    shape.PathCurve = (byte)Extrusion.Straight;
                    break;

                case 4: // cylinder with hollow
                    shape.ProfileCurve = (byte)ProfileShape.Circle | (byte)HollowShape.Triangle;
                    shape.PathCurve = (byte)Extrusion.Straight;
                    shape.ProfileHollow = 27500;
                    break;

                case 5: // prism
                    shape.ProfileCurve = (byte)ProfileShape.EquilateralTriangle | (byte)HollowShape.Triangle;
                    shape.PathCurve = (byte)Extrusion.Straight;
                    break;

                case 6: // box
                    shape.ProfileCurve = (byte)ProfileShape.Square  | (byte)HollowShape.Triangle;
                    shape.PathCurve = (byte)Extrusion.Straight;
                    break;

                case 7: // box with hollow
                    shape.ProfileCurve = (byte)ProfileShape.Square | (byte)HollowShape.Triangle;
                    shape.PathCurve = (byte)Extrusion.Straight;
                    shape.ProfileHollow = 27500;
                    break;

                default: // 8 faces  box with cut
                    shape.ProfileCurve = (byte)ProfileShape.Square | (byte)HollowShape.Triangle;
                    shape.PathCurve = (byte)Extrusion.Straight;
                    shape.ProfileBegin = 9375;
                    break;
            }

            shape.SculptEntry = true;
            shape.SculptType = (byte)OpenMetaverse.SculptType.Mesh;
            shape.SculptTexture = meshAssetID;

            return shape;
        }

        public void SetScale(float side)
        {
            _scale = new Vector3(side, side, side);
        }

        public void SetHeigth(float height)
        {
            _scale.Z = height;
        }

        public void SetRadius(float radius)
        {
            _scale.X = _scale.Y = radius * 2f;
        }

        // TODO: void returns need to change of course
        public virtual void GetMesh()
        {
        }

        public PrimitiveBaseShape Copy()
        {
            return (PrimitiveBaseShape) MemberwiseClone();
        }

        public static PrimitiveBaseShape CreateCylinder(float radius, float heigth)
        {
            PrimitiveBaseShape shape = CreateCylinder();

            shape.SetHeigth(heigth);
            shape.SetRadius(radius);

            return shape;
        }

        public void SetPathRange(Vector3 pathRange)
        {
            _pathBegin = Primitive.PackBeginCut(pathRange.X);
            _pathEnd = Primitive.PackEndCut(pathRange.Y);
        }

        public void SetPathRange(float begin, float end)
        {
            _pathBegin = Primitive.PackBeginCut(begin);
            _pathEnd = Primitive.PackEndCut(end);
        }

        public void SetSculptProperties(byte sculptType, UUID SculptTextureUUID)
        {
            _sculptType = sculptType;
            _sculptTexture = SculptTextureUUID;
        }

        public void SetProfileRange(Vector3 profileRange)
        {
            _profileBegin = Primitive.PackBeginCut(profileRange.X);
            _profileEnd = Primitive.PackEndCut(profileRange.Y);
        }

        public void SetProfileRange(float begin, float end)
        {
            _profileBegin = Primitive.PackBeginCut(begin);
            _profileEnd = Primitive.PackEndCut(end);
        }

        public byte[] ExtraParams
        {
            get
            {
                return ExtraParamsToBytes();
            }
            set
            {
                ReadInExtraParamsBytes(value);
            }
        }

        public ushort PathBegin
        {
            get { return _pathBegin; }
            set { _pathBegin = value; }
        }

        public byte PathCurve
        {
            get { return _pathCurve; }
            set { _pathCurve = value; }
        }

        public ushort PathEnd
        {
            get { return _pathEnd; }
            set { _pathEnd = value; }
        }

        public sbyte PathRadiusOffset
        {
            get { return _pathRadiusOffset; }
            set { _pathRadiusOffset = value; }
        }

        public byte PathRevolutions
        {
            get { return _pathRevolutions; }
            set { _pathRevolutions = value; }
        }

        public byte PathScaleX
        {
            get { return _pathScaleX; }
            set { _pathScaleX = value; }
        }

        public byte PathScaleY
        {
            get { return _pathScaleY; }
            set { _pathScaleY = value; }
        }

        public byte PathShearX
        {
            get { return _pathShearX; }
            set { _pathShearX = value; }
        }

        public byte PathShearY
        {
            get { return _pathShearY; }
            set { _pathShearY = value; }
        }

        public sbyte PathSkew
        {
            get { return _pathSkew; }
            set { _pathSkew = value; }
        }

        public sbyte PathTaperX
        {
            get { return _pathTaperX; }
            set { _pathTaperX = value; }
        }

        public sbyte PathTaperY
        {
            get { return _pathTaperY; }
            set { _pathTaperY = value; }
        }

        public sbyte PathTwist
        {
            get { return _pathTwist; }
            set { _pathTwist = value; }
        }

        public sbyte PathTwistBegin
        {
            get { return _pathTwistBegin; }
            set { _pathTwistBegin = value; }
        }

        public byte PCode
        {
            get { return _pCode; }
            set { _pCode = value; }
        }

        public ushort ProfileBegin
        {
            get { return _profileBegin; }
            set { _profileBegin = value; }
        }

        public ushort ProfileEnd
        {
            get { return _profileEnd; }
            set { _profileEnd = value; }
        }

        public ushort ProfileHollow
        {
            get { return _profileHollow; }
            set { _profileHollow = value; }
        }

        public Vector3 Scale
        {
            get { return _scale; }
            set { _scale = value; }
        }

        public byte State
        {
            get { return _state; }
            set { _state = value; }
        }

        public byte LastAttachPoint
        {
            get { return _lastattach; }
            set { _lastattach = value; }
        }

        public ProfileShape ProfileShape
        {
            get { return _profileShape; }
            set { _profileShape = value; }
        }

        public HollowShape HollowShape
        {
            get { return _hollowShape; }
            set { _hollowShape = value; }
        }

        public UUID SculptTexture
        {
            get { return _sculptTexture; }
            set { _sculptTexture = value; }
        }

        public byte SculptType
        {
            get { return _sculptType; }
            set { _sculptType = value; }
        }

        // This is only used at runtime. For sculpties this holds the texture data, and for meshes
        // the mesh data.
        public byte[] SculptData
        {
            get { return _sculptData; }
            set { _sculptData = value; }
        }

        public int FlexiSoftness
        {
            get { return _flexiSoftness; }
            set { _flexiSoftness = value; }
        }

        public float FlexiTension
        {
            get { return _flexiTension; }
            set { _flexiTension = value; }
        }

        public float FlexiDrag
        {
            get { return _flexiDrag; }
            set { _flexiDrag = value; }
        }

        public float FlexiGravity
        {
            get { return _flexiGravity; }
            set { _flexiGravity = value; }
        }

        public float FlexiWind
        {
            get { return _flexiWind; }
            set { _flexiWind = value; }
        }

        public float FlexiForceX
        {
            get { return _flexiForceX; }
            set { _flexiForceX = value; }
        }

        public float FlexiForceY
        {
            get { return _flexiForceY; }
            set { _flexiForceY = value; }
        }

        public float FlexiForceZ
        {
            get { return _flexiForceZ; }
            set { _flexiForceZ = value; }
        }

        public float LightColorR
        {
            get { return _lightColorR; }
            set
            {
                if (value < 0)
                    _lightColorR = 0;
                else if (value > 1.0f)
                    _lightColorR = 1.0f;
                else
                    _lightColorR = value;
            }
        }

        public float LightColorG
        {
            get { return _lightColorG; }
            set
            {
                if (value < 0)
                    _lightColorG = 0;
                else if (value > 1.0f)
                    _lightColorG = 1.0f;
                else
                    _lightColorG = value;
            }
        }

        public float LightColorB
        {
            get { return _lightColorB; }
            set
            {
                if (value < 0)
                    _lightColorB = 0;
                else if (value > 1.0f)
                    _lightColorB = 1.0f;
                else
                    _lightColorB = value;
            }
        }

        public float LightColorA
        {
            get { return _lightColorA; }
            set
            {
                if (value < 0)
                    _lightColorA = 0;
                else if (value > 1.0f)
                    _lightColorA = 1.0f;
                else
                    _lightColorA = value;
            }
        }

        public float LightRadius
        {
            get { return _lightRadius; }
            set { _lightRadius = value; }
        }

        public float LightCutoff
        {
            get { return _lightCutoff; }
            set { _lightCutoff = value; }
        }

        public float LightFalloff
        {
            get { return _lightFalloff; }
            set { _lightFalloff = value; }
        }

        public float LightIntensity
        {
            get { return _lightIntensity; }
            set { _lightIntensity = value; }
        }

        // only means we do have flexi data
        public bool FlexiEntry
        {
            get { return _flexiEntry; }
            set { _flexiEntry = value; }
        }

        public bool LightEntry
        {
            get { return _lightEntry; }
            set { _lightEntry = value; }
        }

        public bool SculptEntry
        {
            get { return _sculptEntry; }
            set { _sculptEntry = value; }
        }

        public bool ProjectionEntry
        {
            get { return _projectionEntry; }
            set { _projectionEntry = value; }
        }

        public UUID ProjectionTextureUUID
        {
            get { return _projectionTextureID; }
            set { _projectionTextureID = value; }
        }

        public float ProjectionFOV
        {
            get { return _projectionFOV; }
            set { _projectionFOV = value; }
        }

        public float ProjectionFocus
        {
            get { return _projectionFocus; }
            set { _projectionFocus = value; }
        }

        public float ProjectionAmbiance
        {
            get { return _projectionAmb; }
            set { _projectionAmb = value; }
        }

        public ulong GetMeshKey(Vector3 size, float lod)
        {
            return GetMeshKey(size, lod, false);
        }

        public ulong GetMeshKey(Vector3 size, float lod, bool convex)
        {
            ulong hash = 5381;

            hash = djb2(hash, PathCurve);
            hash = djb2(hash, (byte)((byte)HollowShape | (byte)ProfileShape));
            hash = djb2(hash, PathBegin);
            hash = djb2(hash, PathEnd);
            hash = djb2(hash, PathScaleX);
            hash = djb2(hash, PathScaleY);
            hash = djb2(hash, PathShearX);
            hash = djb2(hash, PathShearY);
            hash = djb2(hash, (byte)PathTwist);
            hash = djb2(hash, (byte)PathTwistBegin);
            hash = djb2(hash, (byte)PathRadiusOffset);
            hash = djb2(hash, (byte)PathTaperX);
            hash = djb2(hash, (byte)PathTaperY);
            hash = djb2(hash, PathRevolutions);
            hash = djb2(hash, (byte)PathSkew);
            hash = djb2(hash, ProfileBegin);
            hash = djb2(hash, ProfileEnd);
            hash = djb2(hash, ProfileHollow);

            // TODO: Separate scale out from the primitive shape data (after
            // scaling is supported at the physics engine level)
            hash = djb2(hash, size.X);
            hash = djb2(hash, size.Y);
            hash = djb2(hash, size.Z);
 
            hash = djb2(hash, lod);

            // include sculpt UUID
            if (SculptEntry)
            {
                byte[] scaleBytes = this.SculptTexture.GetBytes();
                for (int i = 0; i < scaleBytes.Length; i++)
                    hash = djb2(hash, scaleBytes[i]);
            }

            if(convex)
                hash = djb2(hash, 0xa5);

            return hash;
        }

        private static ulong djb2(ulong hash, byte c)
        {
            //return ((hash << 5) + hash) + (ulong)c;
            return 33 * hash + (ulong)c;
        }

        private static ulong djb2(ulong hash, ushort c)
        {
            //hash = ((hash << 5) + hash) + (ulong)((byte)c);
            //return ((hash << 5) + hash) + (ulong)(c >> 8);
            return 33 * hash + c;
        }

        private static ulong djb2(ulong hash, float c)
        {
            //hash = ((hash << 5) + hash) + (ulong)((byte)c);
            //return ((hash << 5) + hash) + (ulong)(c >> 8);
            return 33 * hash + (ulong)c.GetHashCode();
        }

        public unsafe byte[] ExtraParamsToBytes()
        {
            //m_log.DebugFormat("[EXTRAPARAMS]: Called ExtraParamsToBytes()");

            const byte FlexiEP = 0x10;
            const byte LightEP = 0x20;
            const byte SculptEP = 0x30;
            const byte ProjectionEP = 0x40;
            //const byte MeshEP = 0x60;
            const byte MeshFlagsEP = 0x70;
            const byte MaterialsEP = 0x80;
            const byte ReflectionProbeEP = 0x90;

            int TotalBytesLength = 1; // ExtraParamsNum

            uint ExtraParamsNum = 0;
            if (_flexiEntry)
            {
                ExtraParamsNum++;
                TotalBytesLength += 16 + 2 + 4;// data
            }

            if (_lightEntry)
            {
                ExtraParamsNum++;
                TotalBytesLength += 16 + 2 + 4; // data
            }

            if (_sculptEntry)
            {
                ExtraParamsNum++;
                TotalBytesLength += 17 + 2 + 4;// data
            }

            if (_projectionEntry)
            {
                ExtraParamsNum++;
                TotalBytesLength += 28 + 2 + 4; // data
            }

            if (_meshFlagsEntry)
            {
                ExtraParamsNum++;
                TotalBytesLength += 4 + 2 + 4; // data
            }

            if (ReflectionProbe != null)
            {
                ExtraParamsNum++;
                TotalBytesLength += 9 + 2 + 4; // data
            }

            bool hasRenderMaterials = RenderMaterials is not null && RenderMaterials.entries is not null && RenderMaterials.entries.Length > 0;
            if (hasRenderMaterials)
            {
                ExtraParamsNum++;
                TotalBytesLength += 1 + 17 * RenderMaterials.entries.Length + 2 + 4; // data
            }

            byte[] safeReturnBytes = new byte[TotalBytesLength];
            if(TotalBytesLength == 1)
            {
                safeReturnBytes[0] = 0;
                return safeReturnBytes;
            }

            fixed(byte* breturnBytes = &safeReturnBytes[0])
            {
                byte* returnBytes = breturnBytes;

                *returnBytes++ = (byte)ExtraParamsNum;

                if (_flexiEntry)
                {
                    *returnBytes = FlexiEP; returnBytes += 2;// 2 bytes id code
                    *returnBytes = 16; returnBytes += 4;// 4 bytes size


                    // Softness is packed in the upper bits of tension and drag
                    *returnBytes++ = (byte)(((_flexiSoftness & 2) << 6) | ((byte)(_flexiTension * 10.01f) & 0x7F));
                    *returnBytes++ = (byte)(((_flexiSoftness & 1) << 7) | ((byte)(_flexiDrag * 10.01f) & 0x7F));
                    *returnBytes++ = (byte)((_flexiGravity + 10.0f) * 10.01f);
                    *returnBytes++ = (byte)(_flexiWind * 10.01f);
                    Utils.FloatToBytes(_flexiForceX, returnBytes); returnBytes += 4;
                    Utils.FloatToBytes(_flexiForceY, returnBytes); returnBytes += 4;
                    Utils.FloatToBytes(_flexiForceZ, returnBytes); returnBytes += 4;
                }

                if (_lightEntry)
                {
                    *returnBytes = LightEP; returnBytes += 2;
                    *returnBytes = 16; returnBytes += 4;

                    // Alpha channel in color is intensity
                    *returnBytes++ = Utils.FloatZeroOneToByte(_lightColorR);
                    *returnBytes++ = Utils.FloatZeroOneToByte(_lightColorG);
                    *returnBytes++ = Utils.FloatZeroOneToByte(_lightColorB);
                    *returnBytes++ = Utils.FloatZeroOneToByte(_lightIntensity);

                    Utils.FloatToBytes(_lightRadius, returnBytes); returnBytes += 4;
                    Utils.FloatToBytes(_lightCutoff, returnBytes); returnBytes += 4;
                    Utils.FloatToBytes(_lightFalloff, returnBytes); returnBytes += 4;
                }

                if (_sculptEntry)
                {
                    //if(_sculptType == 5)
                    //    *returnBytes = MeshEP; returnBytes += 2;
                    //else
                    *returnBytes = SculptEP; returnBytes += 2;
                    *returnBytes = 17; returnBytes += 4;

                    _sculptTexture.ToBytes(returnBytes); returnBytes += 16;
                    *returnBytes++ = _sculptType;
                }

                if (_projectionEntry)
                {
                    *returnBytes = ProjectionEP; returnBytes += 2;
                    *returnBytes = 28; returnBytes += 4;

                    _projectionTextureID.ToBytes(returnBytes); returnBytes += 16;
                    Utils.FloatToBytes(_projectionFOV, returnBytes); returnBytes += 4;
                    Utils.FloatToBytes(_projectionFocus, returnBytes); returnBytes += 4;
                    Utils.FloatToBytes(_projectionAmb, returnBytes); returnBytes += 4;
                }

                if (_meshFlagsEntry)
                {
                    *returnBytes = MeshFlagsEP; returnBytes += 2;
                    *returnBytes = 4; returnBytes += 4;
                    Utils.UIntToBytes(_meshFlags, returnBytes); returnBytes += 4;
                }

                if (ReflectionProbe != null)
                {
                    *returnBytes = ReflectionProbeEP; returnBytes += 2;
                    *returnBytes = 9; returnBytes += 4;

                    Utils.FloatToBytes(ReflectionProbe.Ambiance, returnBytes); returnBytes += 4;
                    Utils.FloatToBytes(ReflectionProbe.ClipDistance, returnBytes); returnBytes += 4;
                    *returnBytes++ = ReflectionProbe.Flags;
                }

                if (hasRenderMaterials)
                {
                    *returnBytes = MaterialsEP; returnBytes += 2;

                    int len = 1 + 17 * RenderMaterials.entries.Length;
                    *returnBytes++ = (byte)len;
                    *returnBytes++ = (byte)(len >> 8);
                    *returnBytes++ = (byte)(len >> 16);
                    *returnBytes++ = (byte)(len >> 24);

                    *returnBytes++ = (byte)RenderMaterials.entries.Length;

                    for (int j = 0; j < RenderMaterials.entries.Length; ++j)
                    {
                        *returnBytes++ = RenderMaterials.entries[j].te_index;
                        RenderMaterials.entries[j].id.ToBytes(returnBytes); returnBytes += 16;
                    }
                }
            }
            return safeReturnBytes;
        }

        public void ReadInUpdateExtraParam(ushort type, bool inUse, byte[] data)
        {
            const ushort FlexiEP = 0x10;
            const ushort LightEP = 0x20;
            const ushort SculptEP = 0x30;
            const ushort ProjectionEP = 0x40;
            const ushort MeshEP = 0x60;
            const ushort MeshFlagsEP = 0x70;
            const ushort MaterialsEP = 0x80;
            const ushort ReflectionProbeEP = 0x90;

            switch (type)
            {
                case FlexiEP:
                    if (!inUse)
                    {
                        _flexiEntry = false;
                        return;
                    }
                    ReadFlexiData(data, 0);
                    break;

                case LightEP:
                    if (!inUse)
                    {
                        _lightEntry = false;
                        return;
                    }
                    ReadLightData(data, 0);
                    break;

                case MeshEP:
                case SculptEP:
                    if (!inUse)
                    {
                        _sculptEntry = false;
                        return;
                    }
                    ReadSculptData(data, 0);
                    break;
                case ProjectionEP:
                    if (!inUse)
                    {
                        _projectionEntry = false;
                        return;
                    }
                    ReadProjectionData(data, 0);
                    break;
                case MeshFlagsEP:
                    if (!inUse)
                    {
                        _meshFlagsEntry = false;
                        _meshFlags = 0;
                        return;
                    }
                    ReadMeshFlagsData(data, 0);
                    break;
                case ReflectionProbeEP:
                    if (!inUse)
                    {
                        ReflectionProbe = null;
                        return;
                    }
                    ReadReflectionProbe(data, 0);
                    break;
                case MaterialsEP:
                    if (!inUse)
                    {
                        RenderMaterials = null;
                        return;
                    }
                    ReadRenderMaterials(data, 0, data.Length);
                    break;
            }
        }

        public void ReadInExtraParamsBytes(byte[] data)
        {
            if (data == null)
                return;

            _flexiEntry = false;
            _lightEntry = false;
            _sculptEntry = false;
            _projectionEntry = false;
            _meshFlagsEntry = false;
            RenderMaterials = null;
            ReflectionProbe = null;

            if (data.Length == 1)
                return;

            const byte FlexiEP = 0x10;
            const byte LightEP = 0x20;
            const byte SculptEP = 0x30;
            const byte ProjectionEP = 0x40;
            const byte MeshEP = 0x60;
            const byte MeshFlagsEP = 0x70;
            const byte MaterialsEP = 0x80;
            const byte ReflectionProbeEP = 0x90;

            byte extraParamCount = data[0];
            int i = 1;
            for (int k = 0; k < extraParamCount; ++k)
            {
                byte epType = data[i];

                switch (epType)
                {
                    case FlexiEP:
                        i += 6;
                        ReadFlexiData(data, i);
                        i += 16;
                        break;

                    case LightEP:
                        i += 6;
                        ReadLightData(data, i);
                        i += 16;
                        break;

                    case MeshEP:
                    case SculptEP:
                        i += 6;
                        ReadSculptData(data, i);
                        i += 17;
                        break;

                    case ProjectionEP:
                        i += 6;
                        ReadProjectionData(data, i);
                        i += 28;
                        break;

                    case MeshFlagsEP:
                        i += 6;
                        ReadMeshFlagsData(data, i);
                        i += 4;
                        break;

                    case ReflectionProbeEP:
                        i += 6;
                        ReadReflectionProbe(data, i);
                        i += 9;
                        break;

                    case MaterialsEP:
                        i += 2;
                        if (data.Length - i >= 4)
                        {
                            int size = Utils.BytesToInt(data, i);
                            i += 4;
                            i += ReadRenderMaterials(data, i, size);
                        }
                        break;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadSculptData(byte[] data, int pos)
        {
            if (data.Length-pos >= 17)
            {
                _sculptTexture = new UUID(data, pos);
                _sculptType = data[pos + 16];
                _sculptEntry = true;
            }
            else
            {
                _sculptEntry = false;
                _sculptTexture = UUID.Zero;
                _sculptType = 0x00;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadFlexiData(byte[] data, int pos)
        {
            if (data.Length-pos >= 16)
            {
                _flexiEntry = true;
                _flexiSoftness = ((data[pos] & 0x80) >> 6) | ((data[pos + 1] & 0x80) >> 7);

                _flexiTension = (float)(data[pos++] & 0x7F) / 10.0f;
                _flexiDrag = (float)(data[pos++] & 0x7F) / 10.0f;
                _flexiGravity = (float)(data[pos++] / 10.0f) - 10.0f;
                _flexiWind = (float)data[pos++] / 10.0f;
                _flexiForceX = Utils.BytesToFloat(data, pos);
                _flexiForceY = Utils.BytesToFloat(data, pos + 4);
                _flexiForceZ = Utils.BytesToFloat(data, pos + 8);
            }
            else
            {
                _flexiEntry = false;
                _flexiSoftness = 0;

                _flexiTension = 0.0f;
                _flexiDrag = 0.0f;
                _flexiGravity = 0.0f;
                _flexiWind = 0.0f;
                _flexiForceX = 0f;
                _flexiForceY = 0f;
                _flexiForceZ = 0f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadLightData(byte[] data, int pos)
        {
            if (data.Length - pos >= 16)
            {
                _lightEntry = true;
                Color4 lColor = new(data, pos, false);
                _lightIntensity = lColor.A;
                _lightColorA = 1f;
                _lightColorR = lColor.R;
                _lightColorG = lColor.G;
                _lightColorB = lColor.B;

                _lightRadius = Utils.BytesToFloat(data, pos + 4);
                _lightCutoff = Utils.BytesToFloat(data, pos + 8);
                _lightFalloff = Utils.BytesToFloat(data, pos + 12);
            }
            else
            {
                _lightEntry = false;
                _lightColorA = 1f;
                _lightColorR = 0f;
                _lightColorG = 0f;
                _lightColorB = 0f;
                _lightRadius = 0f;
                _lightCutoff = 0f;
                _lightFalloff = 0f;
                _lightIntensity = 0f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadProjectionData(byte[] data, int pos)
        {
            if (data.Length - pos >= 28)
            {
                _projectionEntry = true;
                _projectionTextureID = new UUID(data, pos);
                _projectionFOV = Utils.BytesToFloat(data, pos + 16);
                _projectionFocus = Utils.BytesToFloat(data, pos + 20);
                _projectionAmb = Utils.BytesToFloat(data, pos + 24);
            }
            else
            {
                _projectionEntry = false;
                _projectionTextureID = UUID.Zero;
                _projectionFOV = 0f;
                _projectionFocus = 0f;
                _projectionAmb = 0f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadMeshFlagsData(byte[] data, int pos)
        {
            _meshFlagsEntry = true;
            _meshFlags = data.Length - pos >= 4 ? Utils.BytesToUInt(data, pos) : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ReadReflectionProbe(byte[] data, int pos)
        {
            if (data.Length - pos >= 9)
            {
                ReflectionProbe = new Primitive.ReflectionProbe
                {
                    Ambiance = Utils.Clamp(Utils.BytesToFloat(data, pos), 0, 1.0f),
                    ClipDistance = Utils.Clamp(Utils.BytesToFloat(data, pos + 4), 0, 1024f),
                    Flags = data[pos + 8]
                };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ReadRenderMaterials(byte[] data, int pos, int size)
        {
            if (size > 17)
            {
                int count = data[pos];
                ++pos;
                if (size >= 1 + 17 * count)
                {
                    var entries = new Primitive.RenderMaterials.RenderMaterialEntry[count];
                    for (int i = 0; i < count; ++i)
                    {
                        entries[i].te_index = data[pos++];
                        entries[i].id = new UUID(data, pos);
                        pos += 16;
                    }
                    RenderMaterials ??= new Primitive.RenderMaterials();
                    RenderMaterials.entries = entries;
                }
            }
            return size + 4; 
        }

        /// <summary>
        /// Creates a OpenMetaverse.Primitive and populates it with converted PrimitiveBaseShape values
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Primitive ToOmvPrimitive()
        {
            // position and rotation defaults here since they are not available in PrimitiveBaseShape
            return ToOmvPrimitive(new Vector3(0.0f, 0.0f, 0.0f),
                new Quaternion(0.0f, 0.0f, 0.0f, 1.0f));
        }

        /// <summary>
        /// Creates a OpenMetaverse.Primitive and populates it with converted PrimitiveBaseShape values
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        public Primitive ToOmvPrimitive(Vector3 position, Quaternion rotation)
        {
            OpenMetaverse.Primitive prim = new()
            {
                Scale = this.Scale,
                Position = position,
                Rotation = rotation
            };

            if (SculptEntry)
            {
                prim.Sculpt = new Primitive.SculptData
                {
                    Type = (SculptType)SculptType,
                    SculptTexture = SculptTexture
                };
            }

            prim.PrimData.PathShearX = PathShearX < 128 ? (float)PathShearX * 0.01f : (float)(PathShearX - 256) * 0.01f;
            prim.PrimData.PathShearY = PathShearY < 128 ? (float)PathShearY * 0.01f : (float)(PathShearY - 256) * 0.01f;
            prim.PrimData.PathBegin = (float)PathBegin * 2.0e-5f;
            prim.PrimData.PathEnd = 1.0f - (float)PathEnd * 2.0e-5f;

            prim.PrimData.PathScaleX = (200 - PathScaleX) * 0.01f;
            prim.PrimData.PathScaleY = (200 - PathScaleY) * 0.01f;

            prim.PrimData.PathTaperX = PathTaperX * 0.01f;
            prim.PrimData.PathTaperY = PathTaperY * 0.01f;

            prim.PrimData.PathTwistBegin = PathTwistBegin * 0.01f;
            prim.PrimData.PathTwist = PathTwist * 0.01f;

            prim.PrimData.ProfileBegin = (float)ProfileBegin * 2.0e-5f;
            prim.PrimData.ProfileEnd = 1.0f - (float)ProfileEnd * 2.0e-5f;
            prim.PrimData.ProfileHollow = (float)ProfileHollow * 2.0e-5f;

            prim.PrimData.profileCurve = ProfileCurve;
            prim.PrimData.ProfileHole = (HoleType)HollowShape;

            prim.PrimData.PathCurve = (PathCurve)PathCurve;
            prim.PrimData.PathRadiusOffset = 0.01f * PathRadiusOffset;
            prim.PrimData.PathRevolutions = 1.0f + 0.015f * PathRevolutions;
            prim.PrimData.PathSkew = 0.01f * PathSkew;

            prim.PrimData.PCode = OpenMetaverse.PCode.Prim;
            prim.PrimData.State = 0;

            if (FlexiEntry)
            {
                prim.Flexible = new Primitive.FlexibleData
                {
                    Drag = FlexiDrag,
                    Force = new Vector3(FlexiForceX, FlexiForceY, FlexiForceZ),
                    Gravity = FlexiGravity,
                    Softness = FlexiSoftness,
                    Tension = FlexiTension,
                    Wind = FlexiWind
                };
            }

            if (LightEntry)
            {
                prim.Light = new Primitive.LightData
                {
                    Color = new Color4(LightColorR, LightColorG, LightColorB, LightColorA),
                    Cutoff = LightCutoff,
                    Falloff = LightFalloff,
                    Intensity = LightIntensity,
                    Radius = LightRadius
                };
            }

            prim.Textures = Textures;

            prim.Properties = new Primitive.ObjectProperties
            {
                Name = "Object",
                Description = "",
                CreatorID = UUID.Zero,
                GroupID = UUID.Zero,
                OwnerID = UUID.Zero,
                Permissions = new Permissions(),
                SalePrice = 10,
                SaleType = new SaleType()
            };

            return prim;
        }

        public byte[] RenderMaterialsOvrToRawBin()
        {
            // byte: number of entries 
            // repeat:
            // byte; entry face index
            // byte; low entry override utf8 length
            // byte: high entry override utf8 length
            // utf8 bytes: override 

            if (RenderMaterials is null)
                return null;

            if (RenderMaterials.overrides is null || RenderMaterials.overrides.Length == 0)
                return [0];  // store so outdated viewer caches can be updated

            int nentries = 0;
            for (int i = 0; i < RenderMaterials.overrides.Length; i++)
            {
                if (!string.IsNullOrEmpty(RenderMaterials.overrides[i].data))
                    nentries++;
            }
            if(nentries == 0)
                return [0];

            osUTF8 sb = OSUTF8Cached.Acquire();
            sb.Append((byte)nentries);
            for (int i = 0; i < RenderMaterials.overrides.Length; i++)
            {
                if (string.IsNullOrEmpty(RenderMaterials.overrides[i].data))
                    continue;
                sb.Append(RenderMaterials.overrides[i].te_index);
                int len = RenderMaterials.overrides[i].data.Length;
                sb.Append((byte)(len & 0xff));
                sb.Append((byte)((len >> 8) & 0xff));
                sb.Append(RenderMaterials.overrides[i].data);
            }
            return OSUTF8Cached.GetArrayAndRelease(sb);
        }

        public void RenderMaterialsOvrFromRawBin(byte[] data)
        {
            if (RenderMaterials is not null && RenderMaterials.overrides is not null)
                RenderMaterials.overrides = null;

            if (data is null || data.Length < 1)
                return;

            int nentries = data[0];
            if (nentries > 128)
                return;
            if (nentries == 0) // for outdated viewer caches
            {
                RenderMaterials ??= new Primitive.RenderMaterials();
                return;
            }

            int indx = 1;
            Primitive.RenderMaterials.RenderMaterialOverrideEntry[] overrides = new Primitive.RenderMaterials.RenderMaterialOverrideEntry[nentries];
            try
            {
                for(int i = 0; i < overrides.Length; i++)
                {
                    overrides[i].te_index = data[indx++];
                    int ovrlen = data[indx++];
                    ovrlen += data[indx++] << 8;
                    overrides[i].data = Utils.BytesToString(data,indx, ovrlen);
                    if(overrides[i].data.StartsWith("{\"asset")) // ignore old test data
                        return;
                    indx += ovrlen;
                }
            }
            catch
            {
                return;
            }

            RenderMaterials ??= new Primitive.RenderMaterials();
            RenderMaterials.overrides = overrides;
        }

        /// <summary>
        /// Encapsulates a list of media entries.
        /// </summary>
        /// This class is necessary because we want to replace auto-serialization of MediaEntry with something more
        /// OSD like and less vulnerable to change.
        public class MediaList : List<MediaEntry>, IXmlSerializable
        {
            public const string MEDIA_TEXTURE_TYPE = "sl";

            public MediaList() : base() {}
            public MediaList(IEnumerable<MediaEntry> collection) : base(collection) {}
            public MediaList(int capacity) : base(capacity) {}

            public XmlSchema GetSchema()
            {
                return null;
            }

            public string ToXml()
            {
                lock (this)
                {
                    using (StringWriter sw = new())
                    {
                        using (XmlTextWriter xtw = new(sw))
                        {
                            xtw.WriteStartElement("OSMedia");
                            xtw.WriteAttributeString("type", MEDIA_TEXTURE_TYPE);
                            xtw.WriteAttributeString("version", "0.1");

                            OSDArray meArray = new();
                            foreach (MediaEntry me in this)
                            {
                                OSD osd = (null == me ? new OSD() : me.GetOSD());
                                meArray.Add(osd);
                            }

                            xtw.WriteStartElement("OSData");
                            xtw.WriteRaw(OSDParser.SerializeLLSDXmlString(meArray));
                            xtw.WriteEndElement();

                            xtw.WriteEndElement();

                            xtw.Flush();
                            return sw.ToString();
                        }
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void WriteXml(XmlWriter writer)
            {
                writer.WriteRaw(ToXml());
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static MediaList FromXml(string rawXml)
            {
                MediaList ml = new();
                ml.ReadXml(rawXml);
                if(ml.Count == 0)
                    return null;
                return ml;
            }

            public void ReadXml(string rawXml)
            {
                try
                {
                    using (StringReader sr = new(rawXml))
                    {
                        using (XmlTextReader xtr = new(sr))
                        {
                            xtr.DtdProcessing = DtdProcessing.Ignore;
                            xtr.MoveToContent();

                            string type = xtr.GetAttribute("type");
                            //m_log.DebugFormat("[MOAP]: Loaded media texture entry with type {0}", type);

                            if (type != MEDIA_TEXTURE_TYPE)
                                return;

                            xtr.ReadStartElement("OSMedia");
                            OSD osdp = OSDParser.DeserializeLLSDXml(xtr.ReadInnerXml());
                            if(osdp is not OSDArray osdMeArray)
                                return;

                            if(osdMeArray.Count == 0)
                                return;

                            foreach (OSD osdMe in osdMeArray)
                            {
                                MediaEntry me = (osdMe is OSDMap ? MediaEntry.FromOSD(osdMe) : new MediaEntry());
                                Add(me);
                            }
                        }
                    }
                }
                catch
                {
                    m_log.Debug("PrimitiveBaseShape] error decoding MOAP xml" );
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ReadXml(XmlReader reader)
            {
                if (reader.IsEmptyElement)
                    return;

                ReadXml(reader.ReadInnerXml());
            }
        }
    }
}
