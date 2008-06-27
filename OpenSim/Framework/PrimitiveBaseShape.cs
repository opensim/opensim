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
using System.Reflection;
using System.Xml.Serialization;
using libsecondlife;
using log4net;

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

        private static readonly LLObject.TextureEntry m_defaultTexture;

        private byte[] m_textureEntry;

        public ushort PathBegin;
        public byte PathCurve;
        public ushort PathEnd;
        public sbyte PathRadiusOffset;
        public byte PathRevolutions;
        public byte PathScaleX;
        public byte PathScaleY;
        public byte PathShearX;
        public byte PathShearY;
        public sbyte PathSkew;
        public sbyte PathTaperX;
        public sbyte PathTaperY;
        public sbyte PathTwist;
        public sbyte PathTwistBegin;
        public byte PCode;
        public ushort ProfileBegin;

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

                    this.HollowShape = HollowShape.Same;
                }
                else
                {
                    this.HollowShape = (HollowShape)hollowShapeByte;
                }

                // Handle profile shape component
                byte profileShapeByte = (byte)(value & 0xf);

                if (!Enum.IsDefined(typeof(ProfileShape), profileShapeByte))
                {
                    m_log.WarnFormat(
                        "[SHAPE]: Attempt to set a ProfileCurve with a profile shape value of {0}, which isn't a valid enum.  Replacing with square.",
                        profileShapeByte);

                    this.ProfileShape = ProfileShape.Square;
                }
                else
                {
                    this.ProfileShape = (ProfileShape)profileShapeByte;
                }
            }
        }

        public ushort ProfileEnd;
        public ushort ProfileHollow;
        public LLVector3 Scale;
        public byte State;

        // Sculpted
        [XmlIgnore] public LLUUID SculptTexture = LLUUID.Zero;
        [XmlIgnore] public byte SculptType = (byte)0;
        [XmlIgnore] public byte[] SculptData = new byte[0];

        // Flexi
        [XmlIgnore] public int FlexiSoftness = 0;
        [XmlIgnore] public float FlexiTension = 0f;
        [XmlIgnore] public float FlexiDrag = 0f;
        [XmlIgnore] public float FlexiGravity = 0f;
        [XmlIgnore] public float FlexiWind = 0f;
        [XmlIgnore] public float FlexiForceX = 0f;
        [XmlIgnore] public float FlexiForceY = 0f;
        [XmlIgnore] public float FlexiForceZ = 0f;

        //Bright n sparkly
        [XmlIgnore] public float LightColorR = 0f;
        [XmlIgnore] public float LightColorG = 0f;
        [XmlIgnore] public float LightColorB = 0f;
        [XmlIgnore] public float LightColorA = 1f;
        [XmlIgnore] public float LightRadius = 0f;
        [XmlIgnore] public float LightCutoff = 0f;
        [XmlIgnore] public float LightFalloff = 0f;
        [XmlIgnore] public float LightIntensity = 1f;
        [XmlIgnore] public bool FlexiEntry = false;
        [XmlIgnore] public bool LightEntry = false;
        [XmlIgnore] public bool SculptEntry = false;


        static PrimitiveBaseShape()
        {
            m_defaultTexture =
                new LLObject.TextureEntry(new LLUUID("89556747-24cb-43ed-920b-47caed15465f"));
        }

        public PrimitiveBaseShape()
        {
            PCode = (byte) PCodeEnum.Primitive;
            ExtraParams = new byte[1];
            Textures = m_defaultTexture;
        }

        [XmlIgnore]
        public LLObject.TextureEntry Textures
        {
            get { return new LLObject.TextureEntry(m_textureEntry, 0, m_textureEntry.Length); }

            set { m_textureEntry = value.ToBytes(); }
        }

        public byte[] TextureEntry
        {
            get { return m_textureEntry; }

            set { m_textureEntry = value; }
        }

        public ProfileShape ProfileShape;

        public HollowShape HollowShape;

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
            PrimitiveBaseShape shape = new PrimitiveBaseShape();
            return shape;
        }

        public static PrimitiveBaseShape CreateBox()
        {
            PrimitiveBaseShape shape = Create();

            shape.PathCurve = (byte) Extrusion.Straight;
            shape.ProfileShape = ProfileShape.Square;
            shape.PathScaleX = 100;
            shape.PathScaleY = 100;

            return shape;
        }

        public static PrimitiveBaseShape CreateCylinder()
        {
            PrimitiveBaseShape shape = Create();

            shape.PathCurve = (byte) Extrusion.Curve1;
            shape.ProfileShape = ProfileShape.Square;

            shape.PathScaleX = 100;
            shape.PathScaleY = 100;

            return shape;
        }

        public void SetScale(float side)
        {
            Scale = new LLVector3(side, side, side);
        }

        public void SetHeigth(float heigth)
        {
            Scale.Z = heigth;
        }

        public void SetRadius(float radius)
        {
            Scale.X = Scale.Y = radius * 2f;
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

        public void SetPathRange(LLVector3 pathRange)
        {
            PathBegin = LLObject.PackBeginCut(pathRange.X);
            PathEnd = LLObject.PackEndCut(pathRange.Y);
        }

        public void SetSculptData(byte sculptType, LLUUID SculptTextureUUID)
        {
            SculptType = sculptType;
            SculptTexture = SculptTextureUUID;
        }

        public void SetProfileRange(LLVector3 profileRange)
        {
            ProfileBegin = LLObject.PackBeginCut(profileRange.X);
            ProfileEnd = LLObject.PackEndCut(profileRange.Y);
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

        public byte[] ExtraParamsToBytes()
        {
            ushort FlexiEP = 0x10;
            ushort LightEP = 0x20;
            ushort SculptEP = 0x30;

            int i = 0;
            uint TotalBytesLength = 1; // ExtraParamsNum

            uint ExtraParamsNum = 0;
            if (FlexiEntry)
            {
                ExtraParamsNum++;
                TotalBytesLength += 16;// data
                TotalBytesLength += 2 + 4; // type
            }
            if (LightEntry)
            {
                ExtraParamsNum++;
                TotalBytesLength += 16;// data
                TotalBytesLength += 2 + 4; // type
            }
            if (SculptEntry)
            {
                ExtraParamsNum++;
                TotalBytesLength += 17;// data
                TotalBytesLength += 2 + 4; // type
            }

            byte[] returnbytes = new byte[TotalBytesLength];


            // uint paramlength = ExtraParamsNum;

            // Stick in the number of parameters
            returnbytes[i++] = (byte)ExtraParamsNum;

            if (FlexiEntry)
            {
                byte[] FlexiData = GetFlexiBytes();

                returnbytes[i++] = (byte)(FlexiEP % 256);
                returnbytes[i++] = (byte)((FlexiEP >> 8) % 256);

                returnbytes[i++] = (byte)(FlexiData.Length % 256);
                returnbytes[i++] = (byte)((FlexiData.Length >> 8) % 256);
                returnbytes[i++] = (byte)((FlexiData.Length >> 16) % 256);
                returnbytes[i++] = (byte)((FlexiData.Length >> 24) % 256);
                Array.Copy(FlexiData, 0, returnbytes, i, FlexiData.Length);
                i += FlexiData.Length;
            }
            if (LightEntry)
            {
                byte[] LightData = GetLightBytes();

                returnbytes[i++] = (byte)(LightEP % 256);
                returnbytes[i++] = (byte)((LightEP >> 8) % 256);

                returnbytes[i++] = (byte)(LightData.Length % 256);
                returnbytes[i++] = (byte)((LightData.Length >> 8) % 256);
                returnbytes[i++] = (byte)((LightData.Length >> 16) % 256);
                returnbytes[i++] = (byte)((LightData.Length >> 24) % 256);
                Array.Copy(LightData, 0, returnbytes, i, LightData.Length);
                i += LightData.Length;
            }
            if (SculptEntry)
            {
                byte[] SculptData = GetSculptBytes();

                returnbytes[i++] = (byte)(SculptEP % 256);
                returnbytes[i++] = (byte)((SculptEP >> 8) % 256);

                returnbytes[i++] = (byte)(SculptData.Length % 256);
                returnbytes[i++] = (byte)((SculptData.Length >> 8) % 256);
                returnbytes[i++] = (byte)((SculptData.Length >> 16) % 256);
                returnbytes[i++] = (byte)((SculptData.Length >> 24) % 256);
                Array.Copy(SculptData, 0, returnbytes, i, SculptData.Length);
                i += SculptData.Length;
            }

            if (!FlexiEntry && !LightEntry && !SculptEntry)
            {
                byte[] returnbyte = new byte[1];
                returnbyte[0] = 0;
                return returnbyte;
            }


            return returnbytes;
            //m_log.Info("[EXTRAPARAMS]: Length = " + m_shape.ExtraParams.Length.ToString());

        }

        public void ReadInUpdateExtraParam(ushort type, bool inUse, byte[] data)
        {
            const ushort FlexiEP = 0x10;
            const ushort LightEP = 0x20;
            const ushort SculptEP = 0x30;

            switch (type)
            {
                case FlexiEP:
                    if (!inUse)
                    {
                        FlexiEntry = false;
                        return;
                    }
                    ReadFlexiData(data, 0);
                    break;

                case LightEP:
                    if (!inUse)
                    {
                        LightEntry = false;
                        return;
                    }
                    ReadLightData(data, 0);
                    break;

                case SculptEP:
                    if (!inUse)
                    {
                        SculptEntry = false;
                        return;
                    }
                    ReadSculptData(data, 0);
                    break;
            }
        }

        public void ReadInExtraParamsBytes(byte[] data)
        {
            const ushort FlexiEP = 0x10;
            const ushort LightEP = 0x20;
            const ushort SculptEP = 0x30;

            bool lGotFlexi = false;
            bool lGotLight = false;
            bool lGotSculpt = false;

            int i = 0;
            byte extraParamCount = 0;
            if (data.Length > 0)
            {
                extraParamCount = data[i++];
            }


            for (int k = 0; k < extraParamCount; k++)
            {
                ushort epType = Helpers.BytesToUInt16(data, i);

                i += 2;
                // uint paramLength = Helpers.BytesToUIntBig(data, i);

                i += 4;
                switch (epType)
                {
                    case FlexiEP:
                        ReadFlexiData(data, i);
                        i += 16;
                        lGotFlexi = true;
                        break;

                    case LightEP:
                        ReadLightData(data, i);
                        i += 16;
                        lGotLight = true;
                        break;

                    case SculptEP:
                        ReadSculptData(data, i);
                        i += 17;
                        lGotSculpt = true;
                        break;
                }
            }

            if (!lGotFlexi)
                FlexiEntry = false;
            if (!lGotLight)
                LightEntry = false;
            if (!lGotSculpt)
                SculptEntry = false;

        }

        public void ReadSculptData(byte[] data, int pos)
        {
            byte[] SculptTextureUUID = new byte[16];
            LLUUID SculptUUID = LLUUID.Zero;
            byte SculptTypel = data[16+pos];

            if (data.Length+pos >= 17)
            {
                SculptEntry = true;
                SculptTextureUUID = new byte[16];
                SculptTypel = data[16 + pos];
                Array.Copy(data, pos, SculptTextureUUID,0, 16);
                SculptUUID = new LLUUID(SculptTextureUUID, 0);
            }
            else
            {
                SculptEntry = false;
                SculptUUID = LLUUID.Zero;
                SculptTypel = 0x00;
            }

            if (SculptEntry)
            {
                if (SculptType != (byte)1 && SculptType != (byte)2 && SculptType != (byte)3 && SculptType != (byte)4)
                    SculptType = 4;
            }
            SculptTexture = SculptUUID;
            SculptType = SculptTypel;
            //m_log.Info("[SCULPT]:" + SculptUUID.ToString());
        }

        public byte[] GetSculptBytes()
        {
            byte[] data = new byte[17];

            SculptTexture.GetBytes().CopyTo(data, 0);
            data[16] = (byte)SculptType;

            return data;
        }
        
        public void ReadFlexiData(byte[] data, int pos)
        {
            if (data.Length-pos >= 16)
            {
                FlexiEntry = true;
                FlexiSoftness = ((data[pos] & 0x80) >> 6) | ((data[pos + 1] & 0x80) >> 7);

                FlexiTension = (float)(data[pos++] & 0x7F) / 10.0f;
                FlexiDrag = (float)(data[pos++] & 0x7F) / 10.0f;
                FlexiGravity = (float)(data[pos++] / 10.0f) - 10.0f;
                FlexiWind = (float)data[pos++] / 10.0f;
                LLVector3 lForce = new LLVector3(data, pos);
                FlexiForceX = lForce.X;
                FlexiForceY = lForce.Y;
                FlexiForceZ = lForce.Z;
            }
            else
            {
                FlexiEntry = false;
                FlexiSoftness = 0;

                FlexiTension = 0.0f;
                FlexiDrag = 0.0f;
                FlexiGravity = 0.0f;
                FlexiWind = 0.0f;
                FlexiForceX = 0f;
                FlexiForceY = 0f;
                FlexiForceZ = 0f;
            }
        }
        
        public byte[] GetFlexiBytes()
        {
            byte[] data = new byte[16];
            int i = 0;

            // Softness is packed in the upper bits of tension and drag
            data[i] = (byte)((FlexiSoftness & 2) << 6);
            data[i + 1] = (byte)((FlexiSoftness & 1) << 7);

            data[i++] |= (byte)((byte)(FlexiTension * 10.01f) & 0x7F);
            data[i++] |= (byte)((byte)(FlexiDrag * 10.01f) & 0x7F);
            data[i++] = (byte)((FlexiGravity + 10.0f) * 10.01f);
            data[i++] = (byte)(FlexiWind * 10.01f);
            LLVector3 lForce = new LLVector3(FlexiForceX, FlexiForceY, FlexiForceZ);
            lForce.GetBytes().CopyTo(data, i);

            return data;
        }
        
        public void ReadLightData(byte[] data, int pos)
        {
            if (data.Length - pos >= 16)
            {
                LightEntry = true;
                LLColor lColor = new LLColor(data, pos, false);
                LightIntensity = lColor.A;
                LightColorA = 1f;
                LightColorR = lColor.R;
                LightColorG = lColor.G;
                LightColorB = lColor.B;

                LightRadius = Helpers.BytesToFloat(data, pos + 4);
                LightCutoff = Helpers.BytesToFloat(data, pos + 8);
                LightFalloff = Helpers.BytesToFloat(data, pos + 12);
            }
            else
            {
                LightEntry = false;
                LightColorA = 1f;
                LightColorR = 0f;
                LightColorG = 0f;
                LightColorB = 0f;
                LightRadius = 0f;
                LightCutoff = 0f;
                LightFalloff = 0f;
                LightIntensity = 0f;
            }
        }
        
        public byte[] GetLightBytes()
        {
            byte[] data = new byte[16];

            // Alpha channel in color is intensity
            LLColor tmpColor = new LLColor(LightColorR,LightColorG,LightColorB,LightIntensity);

            tmpColor.GetBytes().CopyTo(data, 0);
            Helpers.FloatToBytes(LightRadius).CopyTo(data, 4);
            Helpers.FloatToBytes(LightCutoff).CopyTo(data, 8);
            Helpers.FloatToBytes(LightFalloff).CopyTo(data, 12);

            return data;
        }
    }
}
