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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;

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

        private static MaterialData[] m_materialdata = {
            new MaterialData(0.8f,0.4f), // Stone
            new MaterialData(0.3f,0.4f), // Metal
            new MaterialData(0.2f,0.7f), // Glass
            new MaterialData(0.6f,0.5f), // Wood
            new MaterialData(0.9f,0.3f), // Flesh
            new MaterialData(0.4f,0.7f), // Plastic
            new MaterialData(0.9f,0.95f), // Rubber
            new MaterialData(0.0f,0.0f) // light ??
        };

        public static Material MaxMaterial
        {
            get { return (Material)(m_materialdata.Length - 1); }
        }

        public static float friction(Material material)
        {
            int indx = (int)material;
            if (indx < m_materialdata.Length)
                return (m_materialdata[indx].friction);
            else
                return 0;
        }

        public static float bounce(Material material)
        {
            int indx = (int)material;
            if (indx < m_materialdata.Length)
                return (m_materialdata[indx].bounce);
            else
                return 0;
        }
    }

    public class FaceMaterial
    {
        public UUID     ID;
        public UUID     NormalMapID = UUID.Zero;
        public float    NormalOffsetX = 0.0f;
	    public float    NormalOffsetY = 0.0f;
	    public float	NormalRepeatX = 1.0f;
	    public float	NormalRepeatY = 1.0f;
	    public float	NormalRotation = 0.0f;

	    public UUID     SpecularMapID = UUID.Zero;
    	public float	SpecularOffsetX = 0.0f;
    	public float	SpecularOffsetY = 0.0f;
    	public float	SpecularRepeatX = 1.0f;
    	public float	SpecularRepeatY = 1.0f;
    	public float	SpecularRotation = 0.0f;

	    public Color4	SpecularLightColor = new Color4(255,255,255,255);
    	public Byte		SpecularLightExponent = 51;
	    public Byte		EnvironmentIntensity = 0;
	    public Byte		DiffuseAlphaMode = 1;
	    public Byte		AlphaMaskCutoff = 0;

        public FaceMaterial()
        { }

        public FaceMaterial(UUID pID, OSDMap mat)
        {
            ID = pID;
            if(mat == null)
                return;
            float scale = 0.0001f;
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

	        SpecularLightColor = mat["SpecColor"].AsColor4();
    	    SpecularLightExponent = (Byte)mat["SpecExp"].AsUInteger();
	        EnvironmentIntensity = (Byte)mat["EnvIntensity"].AsUInteger();
	        DiffuseAlphaMode = (Byte)mat["DiffuseAlphaMode"].AsUInteger();
	        AlphaMaskCutoff = (Byte)mat["AlphaMaskCutoff"].AsUInteger();
        }

        public OSDMap toOSD()
        {
            OSDMap mat = new OSDMap();
            float scale = 10000f;

            mat["NormMap"] = NormalMapID;
            mat["NormOffsetX"] = (int) (scale * NormalOffsetX);
	        mat["NormOffsetY"] = (int) (scale * NormalOffsetY);
	        mat["NormRepeatX"] = (int) (scale * NormalRepeatX);
	        mat["NormRepeatY"] = (int) (scale * NormalRepeatY);
	        mat["NormRotation"] = (int) (scale * NormalRotation);

	        mat["SpecMap"] = SpecularMapID;
    	    mat["SpecOffsetX"] = (int) (scale * SpecularOffsetX);
    	    mat["SpecOffsetY"] = (int) (scale * SpecularOffsetY);
    	    mat["SpecRepeatX"] = (int) (scale * SpecularRepeatX);
    	    mat["SpecRepeatY"] = (int) (scale * SpecularRepeatY);
    	    mat["SpecRotation"] = (int) (scale * SpecularRotation);

            mat["SpecColor"] = SpecularLightColor;
    	    mat["SpecExp"] = SpecularLightExponent;
	        mat["EnvIntensity"] = EnvironmentIntensity;
	        mat["DiffuseAlphaMode"] = DiffuseAlphaMode;
	        mat["AlphaMaskCutoff"] = AlphaMaskCutoff;

            return mat;
        }
    }
}