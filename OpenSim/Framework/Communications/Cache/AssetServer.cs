/*
* Copyright (c) Contributors, http://opensimulator.org/
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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Db4objects.Db4o;
using Db4objects.Db4o.Query;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;

namespace OpenSim.Framework.Communications.Cache
{
    public class LocalAssetServer : AssetServerBase
    {
        private IObjectContainer db;

        public LocalAssetServer()
        {
            bool yapfile;
            yapfile = File.Exists(Path.Combine(Util.dataDir(), "regionassets.yap"));

            db = Db4oFactory.OpenFile(Path.Combine(Util.dataDir(), "regionassets.yap"));
            MainLog.Instance.Verbose("Db4 Asset database  creation");

            if (!yapfile)
            {
                SetUpAssetDatabase();
            }
        }

        public void CreateAndCommitAsset(AssetBase asset)
        {
            AssetStorage store = new AssetStorage();
            store.Data = asset.Data;
            store.Name = asset.Name;
            store.UUID = asset.FullID;
            db.Set(store);
            db.Commit();
        }

        override public void Close()
        {
            base.Close();

            if (db != null)
            {
                MainLog.Instance.Verbose("Closing local asset server database");
                db.Close();
            }
        }

        override  protected void RunRequests()
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
                    asset.Data = idata;
                    _receiver.AssetReceived(asset, req.IsTexture);
                }
                else
                {
                    //asset.FullID = ;
                    _receiver.AssetNotFound(req.AssetID);
                }

            }

        }

        override protected void StoreAsset(AssetBase asset)
        {
            AssetStorage store = new AssetStorage();
            store.Data = asset.Data;
            store.Name = asset.Name;
            store.UUID = asset.FullID;
            db.Set(store);

            CommitAssets();
        }

        protected override void CommitAssets()
        {
            db.Commit();
        }

        protected virtual void SetUpAssetDatabase()
        {
            MainLog.Instance.Verbose("LOCAL ASSET SERVER", "Setting up asset database");

            ForEachDefaultAsset(StoreAsset);
            ForEachXmlAsset(StoreAsset);
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