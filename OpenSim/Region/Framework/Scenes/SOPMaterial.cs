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
}