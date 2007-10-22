using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;

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
            MainLog.Instance.Verbose("SQL ASSET SERVER", "Setting up asset database");

            ForEachDefaultAsset(StoreAsset );
            ForEachXmlAsset(StoreAsset );

            CommitAssets();
        }


        public AssetServerBase()
        {
            OpenSim.Framework.Console.MainLog.Instance.Verbose("ASSETSERVER","Starting Db4o asset storage system");
            this._assetRequests = new BlockingQueue<ARequest>();

            this._localAssetServerThread = new Thread( RunRequests );
            this._localAssetServerThread.IsBackground = true;
            this._localAssetServerThread.Start();
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
            idata = br.ReadBytes((int)numBytes);
            br.Close();
            fStream.Close();
            info.Data = idata;
            //info.loaded=true;
        }

        public void SetReceiver(IAssetReceiver receiver)
        {
            this._receiver = receiver;
        }

        public void FetchAsset(LLUUID assetID, bool isTexture)
        {
            ARequest req = new ARequest();
            req.AssetID = assetID;
            req.IsTexture = isTexture;
            this._assetRequests.Enqueue(req);
        }

        public void UpdateAsset(AssetBase asset)
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
        _localAssetServerThread.Abort( );
    }

        public void SetServerInfo(string ServerUrl, string ServerKey)
        {
           
        }

        public virtual List<AssetBase> GetDefaultAssets()
        {
            List<AssetBase> assets = new List<AssetBase>();

            assets.Add(CreateImageAsset("00000000-0000-0000-9999-000000000001", "Bricks", "bricks.jp2"));
            assets.Add(CreateImageAsset("00000000-0000-0000-9999-000000000002", "Plywood", "plywood.jp2"));
            assets.Add(CreateImageAsset("00000000-0000-0000-9999-000000000003", "Rocks", "rocks.jp2"));
            assets.Add(CreateImageAsset("00000000-0000-0000-9999-000000000004", "Granite", "granite.jp2"));
            assets.Add(CreateImageAsset("00000000-0000-0000-9999-000000000005", "Hardwood", "hardwood.jp2"));
            assets.Add(CreateImageAsset("00000000-0000-0000-5005-000000000005", "Prim Base Texture", "plywood.jp2"));
            assets.Add(CreateImageAsset("00000000-0000-0000-9999-000000000006", "Map Base Texture", "map_base.jp2"));
            assets.Add(CreateImageAsset("00000000-0000-0000-9999-000000000007", "Map Texture", "map1.jp2"));
            assets.Add(CreateImageAsset("00000000-0000-0000-9999-000000000010", "Female Body Texture", "femalebody.jp2"));
            assets.Add(CreateImageAsset("00000000-0000-0000-9999-000000000011", "Female Bottom Texture", "femalebottom.jp2"));
            assets.Add(CreateImageAsset("00000000-0000-0000-9999-000000000012", "Female Face Texture", "femaleface.jp2"));

            assets.Add(CreateAsset("77c41e39-38f9-f75a-024e-585989bbabbb", "Skin", "base_skin.dat", false));
            assets.Add(CreateAsset("66c41e39-38f9-f75a-024e-585989bfab73", "Shape", "base_shape.dat", false));
            assets.Add(CreateAsset("00000000-38f9-1111-024e-222222111110", "Shirt", "newshirt.dat", false));
            assets.Add(CreateAsset("00000000-38f9-1111-024e-222222111120", "Shirt", "newpants.dat", false));

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
                MainLog.Instance.Verbose("ASSETS", "Loading: [{0}][{1}]", name, filename );

                LoadAsset(asset, isImage, filename);
            }
            else
            {
                MainLog.Instance.Verbose("ASSETS", "Instantiated: [{0}]", name );                
            }

            return asset;
        }

        public void ForEachXmlAsset(Action<AssetBase> action)
        {
            string filePath = Path.Combine(Util.configDir(), "OpenSimAssetSet.xml");
            if (File.Exists(filePath))
            {
                XmlConfigSource source = new XmlConfigSource(filePath);

                for (int i = 0; i < source.Configs.Count; i++)
                {
                    string assetIdStr = source.Configs[i].GetString("assetID", LLUUID.Random().ToStringHyphenated());
                    string name = source.Configs[i].GetString("name", "");
                    sbyte type = (sbyte)source.Configs[i].GetInt("assetType", 0);
                    sbyte invType = (sbyte)source.Configs[i].GetInt("inventoryType", 0);
                    string fileName = source.Configs[i].GetString("fileName", "");

                    AssetBase newAsset = CreateAsset(assetIdStr, name, fileName, false);

                    newAsset.Type = type;
                    newAsset.InvType = invType;

                }
            }
        }
    }
}