using System.Drawing;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    class SOPObjectMaterial : IObjectMaterial
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
                m_parent.UpdateTexture(tex);
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
                m_parent.UpdateTexture(tex);
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
            set { throw new System.NotImplementedException(); }
        }

        public double Bloom
        {
            get { return GetTexface().Glow; }
            set { throw new System.NotImplementedException(); }
        }

        public bool Shiny
        {
            get { return GetTexface().Shiny != Shininess.None; }
            set { throw new System.NotImplementedException(); }
        }

        public bool BumpMap
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }
    }
}
