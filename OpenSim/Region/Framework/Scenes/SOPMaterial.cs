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

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography; // for computing md5 hash

namespace OpenSim.Region.Framework.Scenes
{
    public static class SOPMaterialData
    {
        public enum SopMaterial : int // redundante and not in use for now
        {
            Stone = 0,
            Metal = 1,
            Glass = 2,
            Wood = 3,
            Flesh = 4,
            Plastic = 5,
            Rubber = 6,
            light = 7 // compatibility with old viewers
        }

        private struct MaterialData
        {
            public float friction;
            public float bounce;
            public MaterialData(float f, float b)
            {
                friction = f;
                bounce = b;
            }
        }

        private static readonly MaterialData[] m_materialdata = {
            new(0.8f,0.4f), // Stone
            new(0.3f,0.4f), // Metal
            new(0.2f,0.7f), // Glass
            new(0.6f,0.5f), // Wood
            new(0.9f,0.3f), // Flesh
            new(0.4f,0.7f), // Plastic
            new(0.9f,0.95f), // Rubber
            new(0.0f,0.0f) // light ??
        };

        public static Material MaxMaterial
        {
            get { return (Material)(m_materialdata.Length - 1); }
        }

        public static float friction(Material material)
        {
            if((int)material < m_materialdata.Length)
            {
                ref MaterialData m = ref MemoryMarshal.GetArrayDataReference(m_materialdata);
                m = ref Unsafe.Add(ref m, (int)material);
                return m.friction;
            }
            return 0;
        }

        public static float bounce(Material material)
        {
            if((int)material < m_materialdata.Length)
            {
                ref MaterialData m = ref MemoryMarshal.GetArrayDataReference(m_materialdata);
                m = ref Unsafe.Add(ref m, (int)material);
                return m.bounce;
            }
            return 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public class FaceMaterial
    {
        // ll material data
        public byte DiffuseAlphaMode = 1;
        public byte AlphaMaskCutoff = 0;
        public byte SpecularLightExponent = 51;
        public byte EnvironmentIntensity = 0;
        // need to have 4 bytes here
        public float NormalOffsetX = 0.0f;
        public float NormalOffsetY = 0.0f;
        public float NormalRepeatX = 1.0f;
        public float NormalRepeatY = 1.0f;
        public float NormalRotation = 0.0f;

        public float SpecularOffsetX = 0.0f;
        public float SpecularOffsetY = 0.0f;
        public float SpecularRepeatX = 1.0f;
        public float SpecularRepeatY = 1.0f;
        public float SpecularRotation = 0.0f;

        public byte SpecularLightColorR = 255;
        public byte SpecularLightColorG = 255;
        public byte SpecularLightColorB = 255;
        public byte SpecularLightColorA = 255;
        // data size 12 ints so far
        public UUID NormalMapID = UUID.Zero;
        public UUID SpecularMapID = UUID.Zero;

        // other data
        public UUID ID;
        private int inthash;
        private bool validinthash;

        public FaceMaterial()
        { }

        public FaceMaterial(FaceMaterial other)
        {
            if (other == null)
                return;

            DiffuseAlphaMode = other.DiffuseAlphaMode;
            AlphaMaskCutoff = other.AlphaMaskCutoff;
            SpecularLightExponent = other.SpecularLightExponent;
            EnvironmentIntensity = other.EnvironmentIntensity;
            NormalOffsetX = other.NormalOffsetX;
            NormalOffsetY = other.NormalOffsetY;
            NormalRepeatX = other.NormalRepeatX;
            NormalRepeatY = other.NormalRepeatY;
            NormalRotation = other.NormalRotation;
            SpecularOffsetX = other.SpecularOffsetX;
            SpecularOffsetY = other.SpecularOffsetY;
            SpecularRepeatX = other.SpecularRepeatX;
            SpecularRepeatY = other.SpecularRepeatY;
            SpecularRotation = other.SpecularRotation;
            SpecularLightColorR = other.SpecularLightColorR;
            SpecularLightColorG = other.SpecularLightColorG;
            SpecularLightColorB = other.SpecularLightColorB;
            NormalMapID = other.NormalMapID;
            SpecularMapID = other.SpecularMapID;
        }

        public FaceMaterial(OSDMap mat)
        {
            if (mat == null)
                return;
            const float scale = 0.0001f;
            NormalMapID = mat["NormMap"].AsUUID();
            NormalOffsetX = scale * (float)mat["NormOffsetX"].AsReal();
            NormalOffsetY = scale * (float)mat["NormOffsetY"].AsReal();
            NormalRepeatX = scale * (float)mat["NormRepeatX"].AsReal();
            NormalRepeatY = scale * (float)mat["NormRepeatY"].AsReal();
            NormalRotation = scale * (float)mat["NormRotation"].AsReal();

            SpecularMapID = mat["SpecMap"].AsUUID();
            SpecularOffsetX = scale * (float)mat["SpecOffsetX"].AsReal();
            SpecularOffsetY = scale * (float)mat["SpecOffsetY"].AsReal();
            SpecularRepeatX = scale * (float)mat["SpecRepeatX"].AsReal();
            SpecularRepeatY = scale * (float)mat["SpecRepeatY"].AsReal();
            SpecularRotation = scale * (float)mat["SpecRotation"].AsReal();

            Color4 SpecularLightColortmp = mat["SpecColor"].AsColor4(); // we can read as color4
            SpecularLightColorR = (byte)(SpecularLightColortmp.R);
            SpecularLightColorG = (byte)(SpecularLightColortmp.G);
            SpecularLightColorB = (byte)(SpecularLightColortmp.B);

            SpecularLightExponent = (Byte)mat["SpecExp"].AsUInteger();
            EnvironmentIntensity = (Byte)mat["EnvIntensity"].AsUInteger();
            DiffuseAlphaMode = (Byte)mat["DiffuseAlphaMode"].AsUInteger();
            AlphaMaskCutoff = (Byte)mat["AlphaMaskCutoff"].AsUInteger();
        }

        public void genID()
        {
            Byte[] data = toLLSDxml();
            using (var md5 = MD5.Create())
                ID = new UUID(md5.ComputeHash(data), 0);
        }

        public unsafe override int GetHashCode()
        {
            if (!validinthash)
            {
                unchecked
                {
                    // if you don't like this, don't read...
                    int* ptr;
                    fixed (byte* ptrbase = &DiffuseAlphaMode)
                    {
                        ptr = (int*)ptrbase;
                        inthash = *ptr;
                        for (int i = 0; i < 11; i++)
                            inthash ^= *ptr++;
                    }
                    inthash ^= NormalMapID.GetHashCode();
                    inthash ^= SpecularMapID.GetHashCode();
                 }
                validinthash = true;
            }
            return inthash;
        }

        public override bool Equals(Object o)
        {
            if (o == null || !(o is FaceMaterial))
                return false;

            FaceMaterial other = (FaceMaterial)o;
            return (
                DiffuseAlphaMode == other.DiffuseAlphaMode
                && AlphaMaskCutoff == other.AlphaMaskCutoff
                && SpecularLightExponent == other.SpecularLightExponent
                && EnvironmentIntensity == other.EnvironmentIntensity
                && NormalMapID == other.NormalMapID
                && NormalOffsetX == other.NormalOffsetX
                && NormalOffsetY == other.NormalOffsetY
                && NormalRepeatX == other.NormalRepeatX
                && NormalRepeatY == other.NormalRepeatY
                && NormalRotation == other.NormalRotation
                && SpecularMapID == other.SpecularMapID
                && SpecularOffsetX == other.SpecularOffsetX
                && SpecularOffsetY == other.SpecularOffsetY
                && SpecularRepeatX == other.SpecularRepeatX
                && SpecularRepeatY == other.SpecularRepeatY
                && SpecularRotation == other.SpecularRotation
                && SpecularLightColorR == other.SpecularLightColorR
                && SpecularLightColorG == other.SpecularLightColorG
                && SpecularLightColorB == other.SpecularLightColorB
                );
        }

        public OSDMap toOSD()
        {
            OSDMap mat = new OSDMap();
            float scale = 10000f;

            mat["NormMap"] = NormalMapID;
            mat["NormOffsetX"] = (int)(scale * NormalOffsetX);
            mat["NormOffsetY"] = (int)(scale * NormalOffsetY);
            mat["NormRepeatX"] = (int)(scale * NormalRepeatX);
            mat["NormRepeatY"] = (int)(scale * NormalRepeatY);
            mat["NormRotation"] = (int)(scale * NormalRotation);

            mat["SpecMap"] = SpecularMapID;
            mat["SpecOffsetX"] = (int)(scale * SpecularOffsetX);
            mat["SpecOffsetY"] = (int)(scale * SpecularOffsetY);
            mat["SpecRepeatX"] = (int)(scale * SpecularRepeatX);
            mat["SpecRepeatY"] = (int)(scale * SpecularRepeatY);
            mat["SpecRotation"] = (int)(scale * SpecularRotation);

            OSDArray carray = new OSDArray(4);
            carray.Add(SpecularLightColorR);
            carray.Add(SpecularLightColorG);
            carray.Add(SpecularLightColorB);
            carray.Add(255); // solid color
            mat["SpecColor"] = carray;
            mat["SpecExp"] = SpecularLightExponent;
            mat["EnvIntensity"] = EnvironmentIntensity;
            mat["DiffuseAlphaMode"] = DiffuseAlphaMode;
            mat["AlphaMaskCutoff"] = AlphaMaskCutoff;

            return mat;
        }

        public byte[] toLLSDxml(osUTF8 sb = null)
        {
            const float scale = 10000f;
            bool fullLLSD = false;
            if (sb == null)
            {

                sb = LLSDxmlEncode2.Start(1024, false);
                fullLLSD = true;
            }

            LLSDxmlEncode2.AddMap(sb);
            LLSDxmlEncode2.AddElem("NormMap", NormalMapID, sb);
            LLSDxmlEncode2.AddElem("NormOffsetX", (int)(scale * NormalOffsetX + 0.5f), sb);
            LLSDxmlEncode2.AddElem("NormOffsetY", (int)(scale * NormalOffsetY + 0.5f), sb);
            LLSDxmlEncode2.AddElem("NormRepeatX", (int)(scale * NormalRepeatX + 0.5f), sb);
            LLSDxmlEncode2.AddElem("NormRepeatY", (int)(scale * NormalRepeatY + 0.5f), sb);
            LLSDxmlEncode2.AddElem("NormRotation", (int)(scale * NormalRotation + 0.5f), sb);

            LLSDxmlEncode2.AddElem("SpecMap", SpecularMapID, sb);
            LLSDxmlEncode2.AddElem("SpecOffsetX", (int)(scale * SpecularOffsetX + 0.5f), sb);
            LLSDxmlEncode2.AddElem("SpecOffsetY", (int)(scale * SpecularOffsetY + 0.5f), sb);
            LLSDxmlEncode2.AddElem("SpecRepeatX", (int)(scale * SpecularRepeatX + 0.5f), sb);
            LLSDxmlEncode2.AddElem("SpecRepeatY", (int)(scale * SpecularRepeatY + 0.5f), sb);
            LLSDxmlEncode2.AddElem("SpecRotation", (int)(scale * SpecularRotation + 0.5f), sb);

            LLSDxmlEncode2.AddArray("SpecColor", sb);
            LLSDxmlEncode2.AddElem(SpecularLightColorR, sb);
            LLSDxmlEncode2.AddElem(SpecularLightColorG, sb);
            LLSDxmlEncode2.AddElem(SpecularLightColorB, sb);
            LLSDxmlEncode2.AddElem(255, sb);
            LLSDxmlEncode2.AddEndArray(sb);

            LLSDxmlEncode2.AddElem("SpecExp", SpecularLightExponent, sb);
            LLSDxmlEncode2.AddElem("EnvIntensity", EnvironmentIntensity, sb);
            LLSDxmlEncode2.AddElem("DiffuseAlphaMode", DiffuseAlphaMode, sb);
            LLSDxmlEncode2.AddElem("AlphaMaskCutoff", AlphaMaskCutoff, sb);

            LLSDxmlEncode2.AddEndMap(sb);

            if (fullLLSD)
            {
                return LLSDxmlEncode2.EndToBytes(sb);
            }
            else
                return Utils.EmptyBytes; // ignored if appending
        }
    }
}