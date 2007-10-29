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
using System.Reflection;
using System.Xml.Serialization;

using libsecondlife;

using Nini.Config;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Communications;

namespace OpenSim.Framework.Communications.Cache
{
    public class GridAssetClient : IAssetServer
    {
        private string _assetServerUrl;
        private IAssetReceiver _receiver;

        public GridAssetClient(string serverUrl)
        {
            _assetServerUrl = serverUrl;
        }

        #region IAssetServer Members

        public void SetReceiver(IAssetReceiver receiver)
        {
            _receiver = receiver;
        }

        public void FetchAsset(LLUUID assetID, bool isTexture)
        {
            Stream s = null;
            try
            {

                MainLog.Instance.Debug("ASSETCACHE", "Querying for {0}", assetID.ToString());

                RestClient rc = new RestClient(_assetServerUrl);
                rc.AddResourcePath("assets");
                rc.AddResourcePath(assetID.ToString());
                if (isTexture)
                    rc.AddQueryParameter("texture");

                rc.RequestMethod = "GET";
                s = rc.Request();

                if (s.Length > 0)
                {
                    XmlSerializer xs = new XmlSerializer(typeof(AssetBase));
                    AssetBase asset = (AssetBase)xs.Deserialize(s);

                    _receiver.AssetReceived(asset, isTexture);
                }
                else
                {
                    MainLog.Instance.Debug("ASSETCACHE", "Asset not found {0}", assetID.ToString());
                    _receiver.AssetNotFound(assetID);
                }
            }
            catch (Exception e)
            {
                MainLog.Instance.Error("ASSETCACHE", e.Message);
                MainLog.Instance.Error("ASSETCACHE", e.StackTrace);
            }
        }

        public void UpdateAsset(AssetBase asset)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void StoreAndCommitAsset(AssetBase asset)
        {
            try
            {
                MemoryStream s = new MemoryStream();

                XmlSerializer xs = new XmlSerializer(typeof(AssetBase));
                xs.Serialize(s, asset);
                RestClient rc = new RestClient(_assetServerUrl);
                rc.AddResourcePath("assets");
                rc.RequestMethod = "POST";
                rc.Request(s);
            }
            catch (Exception e)
            {
                MainLog.Instance.Error("ASSETS", e.Message);
            }
        }

        public void Close()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void LoadAsset(AssetBase info, bool image, string filename)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public System.Collections.Generic.List<AssetBase> GetDefaultAssets()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public AssetBase CreateImageAsset(string assetIdStr, string name, string filename)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void ForEachDefaultAsset(Action<AssetBase> action)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public AssetBase CreateAsset(string assetIdStr, string name, string filename, bool isImage)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void ForEachXmlAsset(Action<AssetBase> action)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }
}
