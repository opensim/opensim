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
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using libsecondlife;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;

namespace OpenSim.Framework.Communications.Cache
{
    public class GridAssetClient : AssetServerBase 
    {
        private string _assetServerUrl;

        public GridAssetClient(string serverUrl)
        {
            _assetServerUrl = serverUrl;
        }

        #region IAssetServer Members

        protected override void RunRequests()
        {
            while (true)
            {
                ARequest req = _assetRequests.Dequeue();

                //MainLog.Instance.Verbose("AssetStorage","Requesting asset: " + req.AssetID);


                Stream s = null;
                try
                {
                    MainLog.Instance.Debug("ASSETCACHE", "Querying for {0}", req.AssetID.ToString());

                    RestClient rc = new RestClient(_assetServerUrl);
                    rc.AddResourcePath("assets");
                    rc.AddResourcePath(req.AssetID.ToString());
                    if (req.IsTexture)
                        rc.AddQueryParameter("texture");

                    rc.RequestMethod = "GET";
                    s = rc.Request();

                    if (s.Length > 0)
                    {
                        XmlSerializer xs = new XmlSerializer(typeof(AssetBase));
                        AssetBase newAsset = (AssetBase)xs.Deserialize(s);

                        _receiver.AssetReceived(newAsset, req.IsTexture);
                    }
                    else
                    {
                        MainLog.Instance.Debug("ASSETCACHE", "Asset not found {0}", req.AssetID.ToString());
                        _receiver.AssetNotFound(req.AssetID);
                    }
                }
                catch (Exception e)
                {
                    MainLog.Instance.Error("ASSETCACHE", e.Message);
                    MainLog.Instance.Debug("ASSETCACHE", "Getting asset {0}", req.AssetID.ToString());
                    MainLog.Instance.Error("ASSETCACHE", e.StackTrace);
                }
               
            }
        }

       

        public override void UpdateAsset(AssetBase asset)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        protected override void StoreAsset(AssetBase asset)
        {
            try
            {
              //  MemoryStream s = new MemoryStream();

               // XmlSerializer xs = new XmlSerializer(typeof(AssetBase));
             //   xs.Serialize(s, asset);
              //  RestClient rc = new RestClient(_assetServerUrl);
		MainLog.Instance.Verbose("ASSET", "Storing asset");
                //rc.AddResourcePath("assets");
               // rc.RequestMethod = "POST";
              //  rc.Request(s);
		//MainLog.Instance.Verbose("ASSET", "Stored {0}", rc);
        RestObjectPoster.BeginPostObject<AssetBase>(_assetRequests + "/assets/", asset);
            }
            catch (Exception e)
            {
                MainLog.Instance.Error("ASSETS", e.Message);
            }
        }

        protected override void CommitAssets()
        {
        }

        public override void Close()
        {
            throw new Exception("The method or operation is not implemented.");
        }

       

        #endregion
    }
}
