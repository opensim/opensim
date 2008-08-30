using System.IO;
using System.Xml.Serialization;

namespace OpenSim.Framework.Communications.Cache
{
    public class FileAssetClient : AssetServerBase
    {
        private readonly string m_dir;
        private readonly XmlSerializer m_xs = new XmlSerializer(typeof(AssetBase));  

        public FileAssetClient(string dir)
        {
            if(!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            m_dir = dir;
        }
        public override void StoreAsset(AssetBase asset)
        {
            string cdir = m_dir + Path.DirectorySeparatorChar + asset.FullID.Data[0]
                                + Path.DirectorySeparatorChar + asset.FullID.Data[1];

            if (!Directory.Exists(m_dir + Path.DirectorySeparatorChar + asset.FullID.Data[0]))
                Directory.CreateDirectory(m_dir + Path.DirectorySeparatorChar + asset.FullID.Data[0]);

            if (!Directory.Exists(cdir))
                Directory.CreateDirectory(cdir);

            FileStream x = new FileStream(cdir + Path.DirectorySeparatorChar + asset.FullID + ".xml", FileMode.Create);
            m_xs.Serialize(x, asset);

            x.Flush();
            x.Close();
        }

        public override void UpdateAsset(AssetBase asset)
        {
            StoreAsset(asset);
        }

        protected override AssetBase GetAsset(AssetRequest req)
        {
            string cdir = m_dir + Path.DirectorySeparatorChar + req.AssetID.Data[0]
                                + Path.DirectorySeparatorChar + req.AssetID.Data[1];
            if (File.Exists(cdir + Path.DirectorySeparatorChar + req.AssetID + ".xml"))
            {
                FileStream x = File.OpenRead(cdir + Path.DirectorySeparatorChar + req.AssetID + ".xml");
                AssetBase ret = (AssetBase) m_xs.Deserialize(x);
                x.Close();
                return ret;
            }
            return null;
        }
    }
}
