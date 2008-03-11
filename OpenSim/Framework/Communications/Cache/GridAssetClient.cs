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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
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
using System.Xml.Serialization;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;

namespace OpenSim.Framework.Communications.Cache
{
    public class GridAssetClient : AssetServerBase
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private string _assetServerUrl;

        public GridAssetClient(string serverUrl)
        {
            _assetServerUrl = serverUrl;
        }

        #region IAssetServer Members

        protected override AssetBase GetAsset(AssetRequest req)
        {
            Stream s = null;
            try
            {
                #if DEBUG
                //m_log.DebugFormat("[GRID ASSET CLIENT]: Querying for {0}", req.AssetID.ToString());
                #endif

                RestClient rc = new RestClient(_assetServerUrl);
                rc.AddResourcePath("assets");
                rc.AddResourcePath(req.AssetID.ToString());
                if (req.IsTexture)
                    rc.AddQueryParameter("texture");

                rc.RequestMethod = "GET";
                s = rc.Request();

                if (s.Length > 0)
                {
                    XmlSerializer xs = new XmlSerializer(typeof (AssetBase));

                    return (AssetBase) xs.Deserialize(s);
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[GRID ASSET CLIENT]: Failed to get asset {0}, {1}", req.AssetID, e);
            }

            return null;
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
                m_log.Info("[GRID ASSET CLIENT]: Storing asset");
                //rc.AddResourcePath("assets");

                // rc.RequestMethod = "POST";
                //  rc.Request(s);
                //m_log.InfoFormat("[ASSET]: Stored {0}", rc);
                m_log.Info("[GRID ASSET CLIENT]: Sending to " + _assetServerUrl + "/assets/");
                RestObjectPoster.BeginPostObject<AssetBase>(_assetServerUrl + "/assets/", asset);

            }
            catch (Exception e)
            {
                m_log.Error("[GRID ASSET CLIENT]: " + e.Message);
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
