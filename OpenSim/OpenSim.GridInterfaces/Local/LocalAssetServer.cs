using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Console;
using libsecondlife;
using Db4objects.Db4o;
using Db4objects.Db4o.Query;

namespace OpenSim.GridInterfaces.Local
{
    public class LocalAssetPlugin : IAssetPlugin
    {
        public LocalAssetPlugin()
        {

        }

        public IAssetServer GetAssetServer()
        {
            return (new LocalAssetServer());
        }
    }

    public class LocalAssetServer : IAssetServer
    {
        private IAssetReceiver _receiver;
        private BlockingQueue<ARequest> _assetRequests;
        private IObjectContainer db;
        private Thread _localAssetServerThread;

        public LocalAssetServer()
        {
            bool yapfile;
            this._assetRequests = new BlockingQueue<ARequest>();
            yapfile = System.IO.File.Exists("assets.yap");

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(LogPriority.VERBOSE,"Local Asset Server class created");
            try
            {
                db = Db4oFactory.OpenFile("assets.yap");
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(LogPriority.VERBOSE,"Db4 Asset database  creation");
            }
            catch (Exception e)
            {
                db.Close();
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(LogPriority.MEDIUM,"Db4 Asset server :Constructor - Exception occured");
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, e.ToString());
            }
            if (!yapfile)
            {
                this.SetUpAssetDatabase();
            }
            this._localAssetServerThread = new Thread(new ThreadStart(RunRequests));
            this._localAssetServerThread.IsBackground = true;
            this._localAssetServerThread.Start();

        }

        public void SetReceiver(IAssetReceiver receiver)
        {
            this._receiver = receiver;
        }

        public void RequestAsset(LLUUID assetID, bool isTexture)
        {
            ARequest req = new ARequest();
            req.AssetID = assetID;
            req.IsTexture = isTexture;
            this._assetRequests.Enqueue(req);
        }

        public void UpdateAsset(AssetBase asset)
        {

        }

        public void UploadNewAsset(AssetBase asset)
        {
            AssetStorage store = new AssetStorage();
            store.Data = asset.Data;
            store.Name = asset.Name;
            store.UUID = asset.FullID;
            db.Set(store);
            db.Commit();
        }

        public void SetServerInfo(string ServerUrl, string ServerKey)
        {

        }
        public void Close()
        {
            if (db != null)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(LogPriority.VERBOSE, "Closing local asset server database");
                db.Close();
            }
        }

        private void RunRequests()
        {
            while (true)
            {
                byte[] idata = null;
                bool found = false;
                AssetStorage foundAsset = null;
                ARequest req = this._assetRequests.Dequeue();
                IObjectSet result = db.Query(new AssetUUIDQuery(req.AssetID));
                if (result.Count > 0)
                {
                    foundAsset = (AssetStorage)result.Next();
                    found = true;
                }

                AssetBase asset = new AssetBase();
                if (found)
                {
                    asset.FullID = foundAsset.UUID;
                    asset.Type = foundAsset.Type;
                    asset.InvType = foundAsset.Type;
                    asset.Name = foundAsset.Name;
                    idata = foundAsset.Data;
                }
                else
                {
                    asset.FullID = LLUUID.Zero;
                }
                asset.Data = idata;
                _receiver.AssetReceived(asset, req.IsTexture);
            }

        }

        private void SetUpAssetDatabase()
        {
            try
            {

                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(LogPriority.VERBOSE, "Setting up asset database");

                AssetBase Image = new AssetBase();
                Image.FullID = new LLUUID("00000000-0000-0000-9999-000000000001");
                Image.Name = "Bricks";
                this.LoadAsset(Image, true, "bricks.jp2");
                AssetStorage store = new AssetStorage();
                store.Data = Image.Data;
                store.Name = Image.Name;
                store.UUID = Image.FullID;
                db.Set(store);
                db.Commit();

                Image = new AssetBase();
                Image.FullID = new LLUUID("00000000-0000-0000-9999-000000000002");
                Image.Name = "Plywood";
                this.LoadAsset(Image, true, "plywood.jp2");
                store = new AssetStorage();
                store.Data = Image.Data;
                store.Name = Image.Name;
                store.UUID = Image.FullID;
                db.Set(store);
                db.Commit();

                Image = new AssetBase();
                Image.FullID = new LLUUID("00000000-0000-0000-9999-000000000003");
                Image.Name = "Rocks";
                this.LoadAsset(Image, true, "rocks.jp2");
                store = new AssetStorage();
                store.Data = Image.Data;
                store.Name = Image.Name;
                store.UUID = Image.FullID;
                db.Set(store);
                db.Commit();

                Image = new AssetBase();
                Image.FullID = new LLUUID("00000000-0000-0000-9999-000000000004");
                Image.Name = "Granite";
                this.LoadAsset(Image, true, "granite.jp2");
                store = new AssetStorage();
                store.Data = Image.Data;
                store.Name = Image.Name;
                store.UUID = Image.FullID;
                db.Set(store);
                db.Commit();

                Image = new AssetBase();
                Image.FullID = new LLUUID("00000000-0000-0000-9999-000000000005");
                Image.Name = "Hardwood";
                this.LoadAsset(Image, true, "hardwood.jp2");
                store = new AssetStorage();
                store.Data = Image.Data;
                store.Name = Image.Name;
                store.UUID = Image.FullID;
                db.Set(store);
                db.Commit();

                Image = new AssetBase();
                Image.FullID = new LLUUID("00000000-0000-0000-5005-000000000005");
                Image.Name = "Prim Base Texture";
                this.LoadAsset(Image, true, "plywood.jp2");
                store = new AssetStorage();
                store.Data = Image.Data;
                store.Name = Image.Name;
                store.UUID = Image.FullID;
                db.Set(store);
                db.Commit();

                Image = new AssetBase();
                Image.FullID = new LLUUID("00000000-0000-0000-9999-000000000006");
                Image.Name = "Map Base Texture";
                this.LoadAsset(Image, true, "map_base.jp2");
                store = new AssetStorage();
                store.Data = Image.Data;
                store.Name = Image.Name;
                store.UUID = Image.FullID;
                db.Set(store);
                db.Commit();

                Image = new AssetBase();
                Image.FullID = new LLUUID("00000000-0000-0000-9999-000000000007");
                Image.Name = "Map Texture";
                this.LoadAsset(Image, true, "map1.jp2");
                store = new AssetStorage();
                store.Data = Image.Data;
                store.Name = Image.Name;
                store.UUID = Image.FullID;
                db.Set(store);
                db.Commit();

                Image = new AssetBase();
                Image.FullID = new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");
                Image.Name = "Shape";
                this.LoadAsset(Image, false, "base_shape.dat");
                store = new AssetStorage();
                store.Data = Image.Data;
                store.Name = Image.Name;
                store.UUID = Image.FullID;
                db.Set(store);
                db.Commit();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

        }

        private void LoadAsset(AssetBase info, bool image, string filename)
        {
            //should request Asset from storage manager
            //but for now read from file

            string dataPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "assets"); //+ folder;
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
    }
}
