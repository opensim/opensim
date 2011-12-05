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

using System.Drawing;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    class SOPObjectMaterial : System.MarshalByRefObject, IObjectMaterial
    {
        private readonly int m_face;
        private readonly SceneObjectPart m_parent;

        public SOPObjectMaterial(int m_face, SceneObjectPart m_parent)
        {
            this.m_face = m_face;
            this.m_parent = m_parent;
        }

        public Color Color
        {
            get
            {
                Color4 res = GetTexface().RGBA;
                return Color.FromArgb((int) (res.A*255), (int) (res.R*255), (int) (res.G*255), (int) (res.B*255));
            }
            set
            {
                Primitive.TextureEntry tex = m_parent.Shape.Textures;
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)m_face);
                texface.RGBA = new Color4(value.R,value.G,value.B,value.A);
                tex.FaceTextures[m_face] = texface;
                m_parent.UpdateTextureEntry(tex.GetBytes());
            }
        }

        public UUID Texture
        {
            get
            {
                Primitive.TextureEntryFace texface = GetTexface();
                return texface.TextureID;
            }
            set
            {
                Primitive.TextureEntry tex = m_parent.Shape.Textures;
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)m_face);
                texface.TextureID = value;
                tex.FaceTextures[m_face] = texface;
                m_parent.UpdateTextureEntry(tex.GetBytes());
            }
        }

        private Primitive.TextureEntryFace GetTexface()
        {
            Primitive.TextureEntry tex = m_parent.Shape.Textures;
            return tex.GetFace((uint)m_face);
        }

        public TextureMapping Mapping
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool Bright
        {
            get { return GetTexface().Fullbright; }
            set
            {
                Primitive.TextureEntry tex = m_parent.Shape.Textures;
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)m_face);
                texface.Fullbright = value;
                tex.FaceTextures[m_face] = texface;
                m_parent.UpdateTextureEntry(tex.GetBytes());
            }
        }

        public double Bloom
        {
            get { return GetTexface().Glow; }
            set
            {
                Primitive.TextureEntry tex = m_parent.Shape.Textures;
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)m_face);
                texface.Glow = (float) value;
                tex.FaceTextures[m_face] = texface;
                m_parent.UpdateTextureEntry(tex.GetBytes());
            }
        }

        public bool Shiny
        {
            get { return GetTexface().Shiny != Shininess.None; }
            set
            {
                Primitive.TextureEntry tex = m_parent.Shape.Textures;
                Primitive.TextureEntryFace texface = tex.CreateFace((uint)m_face);
                texface.Shiny = value ? Shininess.High : Shininess.None;
                tex.FaceTextures[m_face] = texface;
                m_parent.UpdateTextureEntry(tex.GetBytes());
            }
        }

        public bool BumpMap
        {
            get { return GetTexface().Bump == Bumpiness.None; }
            set { throw new System.NotImplementedException(); }
        }
    }
}
