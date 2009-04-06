using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    class LOParcel : IParcel
    {
        private readonly Scene m_scene;
        private readonly int m_parcelID;

        public LOParcel(Scene m_scene, int m_parcelID)
        {
            this.m_scene = m_scene;
            this.m_parcelID = m_parcelID;
        }

        private ILandObject GetLO()
        {
            return m_scene.LandChannel.GetLandObject(m_parcelID);
        }

        public string Name
        {
            get { return GetLO().landData.Name; }
            set { GetLO().landData.Name = value; }
        }

        public string Description
        {
            get { return GetLO().landData.Description; }
            set { GetLO().landData.Description = value; }
        }

        public ISocialEntity Owner
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool[,] Bitmap
        {
            get { return GetLO().landBitmap; }
        }
    }
}
