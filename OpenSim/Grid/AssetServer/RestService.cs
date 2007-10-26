using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Text;

using libsecondlife;
using OpenSim.Framework.Types;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Console;

namespace OpenSim.Grid.AssetServer
{
    public class GetAssetStreamHandler : BaseStreamHandler
    {
        OpenAsset_Main m_assetManager;
        IAssetProvider m_assetProvider;

        override public byte[] Handle(string path, Stream request)
        {
            string param = GetParam(path);
            byte[] result = new byte[] { };
            try {

                string[] p = param.Split(new char[] { '/', '?', '&' }, StringSplitOptions.RemoveEmptyEntries);

                if (p.Length > 0)
                {
                    LLUUID assetID;
                    bool isTexture = false;
                    LLUUID.TryParse(p[0], out assetID);
                    if (p.Length > 1)
                    {
                        if (string.Compare(p[1], "texture", true) == 0)
                            isTexture = true;
                    }


                    AssetBase asset = m_assetProvider.FetchAsset(assetID);
                    if (asset != null)
                    {
                        MainLog.Instance.Debug("REST", "GET:/asset found {0}, {1}", assetID, asset.Name);

                        XmlSerializer xs = new XmlSerializer(typeof(AssetBase));
                        MemoryStream ms = new MemoryStream();
                        XmlTextWriter xw = new XmlTextWriter(ms, Encoding.UTF8);
                        xw.Formatting = Formatting.Indented;
                        xs.Serialize(xw, asset);
                        xw.Flush();

                        ms.Seek(0, SeekOrigin.Begin);
                        StreamReader sr = new StreamReader(ms);

                        result = ms.GetBuffer();
                        Array.Resize<byte>(ref result, (int)ms.Length);
                    }
                    else
                    {
                        MainLog.Instance.Verbose("REST", "GET:/asset failed to find {0}", assetID);
                    }
                }
            }
            catch (Exception e)
            {
                MainLog.Instance.Error(e.ToString());
            }
            return result;
        }

        public GetAssetStreamHandler(OpenAsset_Main assetManager, IAssetProvider assetProvider)
            : base("GET", "/assets" )
        {
            m_assetManager = assetManager;
            m_assetProvider = assetProvider;
        }
    }

    public class PostAssetStreamHandler : BaseStreamHandler
    {
        OpenAsset_Main m_assetManager;
        IAssetProvider m_assetProvider;

        override public byte[] Handle(string path, Stream request)
        {
            string param = GetParam(path);
            
            LLUUID assetId;
            if(param.Length > 0)
                LLUUID.TryParse(param, out assetId);
            byte[] txBuffer = new byte[4096];

            XmlSerializer xs = new XmlSerializer(typeof(AssetBase));
            AssetBase asset = (AssetBase)xs.Deserialize(request);

            MainLog.Instance.Verbose("REST", "StoreAndCommitAsset {0}", asset.FullID);
            m_assetProvider.CreateAsset(asset);

            return new byte[] { };
        }

        public PostAssetStreamHandler(OpenAsset_Main assetManager, IAssetProvider assetProvider)
            : base("POST", "/assets")
        {
            m_assetManager = assetManager;
            m_assetProvider = assetProvider;
        }
    }
}
