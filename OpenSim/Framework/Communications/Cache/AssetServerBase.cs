using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Communications.Cache
{
    public abstract class AssetServerBase : IAssetServer
    {
        protected IAssetReceiver _receiver;
        protected BlockingQueue<ARequest> _assetRequests;
        protected Thread _localAssetServerThread;
        protected IAssetProvider m_assetProviderPlugin;
        protected object syncLock = new object();

        protected abstract void StoreAsset(AssetBase asset);
        protected abstract void CommitAssets();

        protected abstract void RunRequests();

        public void LoadDefaultAssets()
        {
            MainLog.Instance.Verbose("ASSETSERVER", "Setting up asset database");

            ForEachDefaultAsset(StoreAsset);
            ForEachXmlAsset(StoreAsset);

            CommitAssets();
        }


        public AssetServerBase()
        {
            MainLog.Instance.Verbose("ASSETSERVER", "Starting asset storage system");
            _assetRequests = new BlockingQueue<ARequest>();

            _localAssetServerThread = new Thread(RunRequests);
            _localAssetServerThread.IsBackground = true;
            _localAssetServerThread.Start();
        }

        public void LoadAsset(AssetBase info, bool image, string filename)
        {
            //should request Asset from storage manager
            //but for now read from file

            string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets"); //+ folder;
            string fileName = Path.Combine(dataPath, filename);
            FileInfo fInfo = new FileInfo(fileName);
            long numBytes = fInfo.Length;
            FileStream fStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            byte[] idata = new byte[numBytes];
            BinaryReader br = new BinaryReader(fStream);
            idata = br.ReadBytes((int) numBytes);
            br.Close();
            fStream.Close();
            info.Data = idata;
            //info.loaded=true;
        }

        public void SetReceiver(IAssetReceiver receiver)
        {
            _receiver = receiver;
        }

        public void RequestAsset(LLUUID assetID, bool isTexture)
        {
            ARequest req = new ARequest();
            req.AssetID = assetID;
            req.IsTexture = isTexture;
	    MainLog.Instance.Verbose("ASSET","Adding {0} to request queue", assetID);
            _assetRequests.Enqueue(req);
	    MainLog.Instance.Verbose("ASSET","Added {0} to request queue", assetID);
        }

        public virtual void UpdateAsset(AssetBase asset)
        {
            lock (syncLock)
            {
                m_assetProviderPlugin.UpdateAsset(asset);
                m_assetProviderPlugin.CommitAssets();
            }
        }

        public void StoreAndCommitAsset(AssetBase asset)
        {
            lock (syncLock)
            {
                StoreAsset(asset);
                CommitAssets();
            }
        }

        public virtual void Close()
        {
            _localAssetServerThread.Abort();
        }

        public void SetServerInfo(string ServerUrl, string ServerKey)
        {
        }

        public virtual List<AssetBase> GetDefaultAssets()
        {
            List<AssetBase> assets = new List<AssetBase>();
            return assets;
        }

        public AssetBase CreateImageAsset(string assetIdStr, string name, string filename)
        {
            return CreateAsset(assetIdStr, name, filename, true);
        }

        public void ForEachDefaultAsset(Action<AssetBase> action)
        {
            List<AssetBase> assets = GetDefaultAssets();
            assets.ForEach(action);
        }

        public AssetBase CreateAsset(string assetIdStr, string name, string filename, bool isImage)
        {
            AssetBase asset = new AssetBase(
                new LLUUID(assetIdStr),
                name
                );

            if (!String.IsNullOrEmpty(filename))
            {
                MainLog.Instance.Verbose("ASSETS", "Loading: [{0}][{1}]", name, filename);

                LoadAsset(asset, isImage, filename);
            }
            else
            {
                MainLog.Instance.Verbose("ASSETS", "Instantiated: [{0}]", name);
            }

            return asset;
        }

        public void ForEachXmlAsset(Action<AssetBase> action)
        {
            List<AssetBase> assets = new List<AssetBase>();
            // System.Console.WriteLine("trying loading asset into database");
            string filePath = Path.Combine(Util.configDir(), "OpenSimAssetSet.xml");
            if (File.Exists(filePath))
            {
                XmlConfigSource source = new XmlConfigSource(filePath);

                for (int i = 0; i < source.Configs.Count; i++)
                {
                    // System.Console.WriteLine("loading asset into database");
                    string assetIdStr = source.Configs[i].GetString("assetID", LLUUID.Random().ToStringHyphenated());
                    string name = source.Configs[i].GetString("name", "");
                    sbyte type = (sbyte) source.Configs[i].GetInt("assetType", 0);
                    sbyte invType = (sbyte) source.Configs[i].GetInt("inventoryType", 0);
                    string fileName = source.Configs[i].GetString("fileName", "");

                    AssetBase newAsset = CreateAsset(assetIdStr, name, fileName, false);

                    newAsset.Type = type;
                    newAsset.InvType = invType;
                    assets.Add(newAsset);
                }
            }
            assets.ForEach(action);
        }
    }
}
