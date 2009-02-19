using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;

namespace OpenSim.Framework.Servers
{
    public class PostAssetStreamHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // private OpenAsset_Main m_assetManager;
        private IAssetDataPlugin m_assetProvider;

        public override byte[] Handle(string path, Stream request,
                                      OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            string param = GetParam(path);

            UUID assetId;
            if (param.Length > 0)
                UUID.TryParse(param, out assetId);
            // byte[] txBuffer = new byte[4096];

            XmlSerializer xs = new XmlSerializer(typeof (AssetBase));
            AssetBase asset = (AssetBase) xs.Deserialize(request);

            m_log.InfoFormat("[REST]: Creating asset {0}", asset.FullID);
            m_assetProvider.CreateAsset(asset);

            return new byte[] {};
        }

        public PostAssetStreamHandler(IAssetDataPlugin assetProvider)
            : base("POST", "/assets")
        {
            // m_assetManager = assetManager;
            m_assetProvider = assetProvider;
        }
    }
}