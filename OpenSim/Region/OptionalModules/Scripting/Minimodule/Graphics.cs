using System.Drawing;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    class Graphics : IGraphics
    {
        private readonly Scene m_scene;

        public Graphics(Scene m_scene)
        {
            this.m_scene = m_scene;
        }

        public UUID SaveBitmap(Bitmap data)
        {
            return SaveBitmap(data, false, true);
        }

        public UUID SaveBitmap(Bitmap data, bool lossless, bool temporary)
        {
            AssetBase asset = new AssetBase();
            asset.FullID = UUID.Random();
            asset.Data = OpenJPEG.EncodeFromImage(data, lossless);
            asset.Name = "MRMDynamicImage";
            asset.Type = 0;
            asset.Description = "MRM Image";
            asset.Local = false;
            asset.Temporary = temporary;
            m_scene.CommsManager.AssetCache.AddAsset(asset);

            return asset.FullID;
        }

        public Bitmap LoadBitmap(UUID assetID)
        {
            AssetBase bmp = m_scene.CommsManager.AssetCache.GetAsset(assetID, true);
            ManagedImage outimg;
            Image img;
            OpenJPEG.DecodeToImage(bmp.Data, out outimg, out img);

            return new Bitmap(img);
        }
    }
}
