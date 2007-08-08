/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.IO;
using System.Threading;
using Db4objects.Db4o;
using Db4objects.Db4o.Query;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;

namespace OpenSim.Region.GridInterfaces.Local
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
            yapfile = File.Exists(Path.Combine(Util.dataDir(),"regionassets.yap"));

            MainLog.Instance.Verbose("Local Asset Server class created");
            db = Db4oFactory.OpenFile(Path.Combine(Util.dataDir(),"regionassets.yap"));
            MainLog.Instance.Verbose("Db4 Asset database  creation");

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
                MainLog.Instance.Verbose("Closing local asset server database");
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
            MainLog.Instance.Verbose("Setting up asset database");

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
            Image.FullID = new LLUUID("00000000-0000-1111-9999-000000000010");
            Image.Name = "Female Body Texture";
            this.LoadAsset(Image, true, "femalebody.jp2");
            store = new AssetStorage();
            store.Data = Image.Data;
            store.Name = Image.Name;
            store.UUID = Image.FullID;
            db.Set(store);
            db.Commit();

            Image = new AssetBase();
            Image.FullID = new LLUUID("00000000-0000-1111-9999-000000000011");
            Image.Name = "Female Bottom Texture";
            this.LoadAsset(Image, true, "femalebottom.jp2");
            store = new AssetStorage();
            store.Data = Image.Data;
            store.Name = Image.Name;
            store.UUID = Image.FullID;
            db.Set(store);
            db.Commit();

            Image = new AssetBase();
            Image.FullID = new LLUUID("00000000-0000-1111-9999-000000000012");
            Image.Name = "Female Face Texture";
            this.LoadAsset(Image, true, "femaleface.jp2");
            store = new AssetStorage();
            store.Data = Image.Data;
            store.Name = Image.Name;
            store.UUID = Image.FullID;
            db.Set(store);
            db.Commit();


            /*
            Image = new AssetBase();
            Image.FullID = new LLUUID("00000000-0000-0000-9999-000000000008");
            Image.Name = "Default Avatar Face";
            this.LoadAsset(Image, true, "femaleface.j2c");
            store = new AssetStorage();
            store.Data = Image.Data;
            store.Name = Image.Name;
            store.UUID = Image.FullID;
            db.Set(store);
            db.Commit();*/

            Image = new AssetBase();
            Image.FullID = new LLUUID("77c41e39-38f9-f75a-024e-585989bbabbb");
            Image.Name = "Skin";
            Image.Type = 13;
            Image.InvType = 13;
            this.LoadAsset(Image, false, "base_skin.dat");
            store = new AssetStorage();
            store.Data = Image.Data;
            store.Name = Image.Name;
            store.UUID = Image.FullID;
            db.Set(store);
            db.Commit();
            

            Image = new AssetBase();
            Image.FullID = new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");
            Image.Name = "Shape";
            Image.Type = 13;
            Image.InvType = 13;
            this.LoadAsset(Image, false, "base_shape.dat");
            store = new AssetStorage();
            store.Data = Image.Data;
            store.Name = Image.Name;
            store.UUID = Image.FullID;
            db.Set(store);
            db.Commit();

            Image = new AssetBase();
            Image.FullID = new LLUUID("00000000-38f9-1111-024e-222222111110");
            Image.Name = "Shirt";
            Image.Type = 5;
            Image.InvType = 18;
            this.LoadAsset(Image, false, "newshirt.dat");
            store = new AssetStorage();
            store.Data = Image.Data;
            store.Name = Image.Name;
            store.UUID = Image.FullID;
            db.Set(store);
            db.Commit();

            Image = new AssetBase();
            Image.FullID = new LLUUID("00000000-38f9-1111-024e-222222111120");
            Image.Name = "Shirt";
            Image.Type = 5;
            Image.InvType = 18;
            this.LoadAsset(Image, false, "newpants.dat");
            store = new AssetStorage();
            store.Data = Image.Data;
            store.Name = Image.Name;
            store.UUID = Image.FullID;
            db.Set(store);
            db.Commit();

            /*Image = new AssetBase();
            Image.FullID = new LLUUID("00000000-0000-2222-3333-000000000001");
            Image.Name = "WelcomeNote";
            Image.Type = 7;
            Image.InvType = 7;
            this.LoadAsset(Image, false, "welcomeNote.dat");
            store = new AssetStorage();
            store.Data = Image.Data;
            store.Name = Image.Name;
            store.UUID = Image.FullID;
            db.Set(store);
            db.Commit();
             */ 

            string filePath = Path.Combine(Util.configDir(), "OpenSimAssetSet.xml");
            if(File.Exists(filePath))
            {
            XmlConfigSource source = new XmlConfigSource(filePath);
            ReadAssetDetails(source);
            }
        }

        protected void ReadAssetDetails(IConfigSource source)
        {
            AssetBase newAsset = null;
            for (int i = 0; i < source.Configs.Count; i++)
            {
                newAsset = new AssetBase();
                newAsset.FullID = new LLUUID(source.Configs[i].GetString("assetID", LLUUID.Random().ToStringHyphenated()));
                newAsset.Name = source.Configs[i].GetString("name", "");
                newAsset.Type =(sbyte) source.Configs[i].GetInt("assetType", 0);
                newAsset.InvType =(sbyte) source.Configs[i].GetInt("inventoryType", 0);
                string fileName = source.Configs[i].GetString("fileName", "");
                if (fileName != "")
                {
                    this.LoadAsset(newAsset, false, fileName);
                    AssetStorage store = new AssetStorage();
                    store.Data = newAsset.Data;
                    store.Name = newAsset.Name;
                    store.UUID = newAsset.FullID;
                    db.Set(store);
                    db.Commit();
                }
            }
        }

        private void LoadAsset(AssetBase info, bool image, string filename)
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
    }
    public class AssetUUIDQuery : Predicate
    {
        private LLUUID _findID;

        public AssetUUIDQuery(LLUUID find)
        {
            _findID = find;
        }
        public bool Match(AssetStorage asset)
        {
            return (asset.UUID == _findID);
        }
    }

}
