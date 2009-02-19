using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Statistics;

namespace OpenSim.Grid.AssetServer
{
    public class GetAssetStreamHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // private OpenAsset_Main m_assetManager;
        private IAssetDataPlugin m_assetProvider;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="assetManager"></param>
        /// <param name="assetProvider"></param>
        public GetAssetStreamHandler(IAssetDataPlugin assetProvider)
            : base("GET", "/assets")
        {
            m_log.Info("[REST]: In Get Request");
            // m_assetManager = assetManager;
            m_assetProvider = assetProvider;
        }

        public override byte[] Handle(string path, Stream request,
                                      OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            string param = GetParam(path);
            byte[] result = new byte[] {};

            string[] p = param.Split(new char[] {'/', '?', '&'}, StringSplitOptions.RemoveEmptyEntries);

            if (p.Length > 0)
            {
                UUID assetID = UUID.Zero;

                if (!UUID.TryParse(p[0], out assetID))
                {
                    m_log.InfoFormat(
                        "[REST]: GET:/asset ignoring request with malformed UUID {0}", p[0]);
                    return result;
                }

                if (StatsManager.AssetStats != null)
                    StatsManager.AssetStats.AddRequest();

                AssetBase asset = m_assetProvider.FetchAsset(assetID);
                if (asset != null)
                {
                    XmlSerializer xs = new XmlSerializer(typeof (AssetBase));
                    MemoryStream ms = new MemoryStream();
                    XmlTextWriter xw = new XmlTextWriter(ms, Encoding.UTF8);
                    xw.Formatting = Formatting.Indented;
                    xs.Serialize(xw, asset);
                    xw.Flush();

                    ms.Seek(0, SeekOrigin.Begin);
                    //StreamReader sr = new StreamReader(ms);

                    result = ms.GetBuffer();

//Ckrinke 1/11/09 Commenting out the succesful REST message as under heavy use there
//are multiple messages in a second and that is usually (in my experience) meaning
//the logging itself is slowing down the program. Leaving the unsuccesful message
//as we need to know about that path.
//                    m_log.InfoFormat(
//                        "[REST]: GET:/asset found {0} with name {1}, size {2} bytes",
//                        assetID, asset.Name, result.Length);

                    Array.Resize<byte>(ref result, (int) ms.Length);
                }
                else
                {
                    if (StatsManager.AssetStats != null)
                        StatsManager.AssetStats.AddNotFoundRequest();

                    m_log.InfoFormat("[REST]: GET:/asset failed to find {0}", assetID);
                }
            }

            return result;
        }
    }
}