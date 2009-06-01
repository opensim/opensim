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
 *     * Neither the name of the OpenSimulator Project nor the
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
 */

using System;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using log4net;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Framework.Communications.Cache
{
    public class GridAssetClient : AssetServerBase
    {
        #region IPlugin

        public override string Name
        {
            get { return "Grid"; }
        }

        public override string Version
        {
            get { return "1.0"; }
        }

        public override void Initialise(ConfigSettings p_set, string p_url)
        {
            m_log.Debug("[GRID ASSET CLIENT]: Plugin configured initialisation");
            Initialise(p_url);
        }

        #endregion

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string _assetServerUrl;

        public GridAssetClient() {}

        public GridAssetClient(string p_url)
        {
            m_log.Debug("[GRID ASSET CLIENT]: Direct constructor");
            Initialise(p_url);
        }

        public void Initialise(string serverUrl)
        {
            _assetServerUrl = serverUrl;
        }

        #region IAssetServer Members

        protected override AssetBase GetAsset(AssetRequest req)
        {
            #if DEBUG
            //m_log.DebugFormat("[GRID ASSET CLIENT]: Querying for {0}", req.AssetID.ToString());
            #endif

            RestClient rc = new RestClient(_assetServerUrl);
            rc.AddResourcePath("assets");
            rc.AddResourcePath(req.AssetID.ToString());

            rc.RequestMethod = "GET";

            Stream s = rc.Request();

            if (s == null)
                return null;

            if (s.Length > 0)
            {
                XmlSerializer xs = new XmlSerializer(typeof (AssetBase));

                return (AssetBase) xs.Deserialize(s);
            }

            return null;
        }

        public override void UpdateAsset(AssetBase asset)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override void StoreAsset(AssetBase asset)
        {
            try
            {
                //  MemoryStream s = new MemoryStream();

                // XmlSerializer xs = new XmlSerializer(typeof(AssetBase));
                //   xs.Serialize(s, asset);
                //  RestClient rc = new RestClient(_assetServerUrl);

                string assetUrl = _assetServerUrl + "/assets/";

                //rc.AddResourcePath("assets");

                // rc.RequestMethod = "POST";
                //  rc.Request(s);
                //m_log.InfoFormat("[ASSET]: Stored {0}", rc);

                m_log.InfoFormat("[GRID ASSET CLIENT]: Sending store request for asset {0}", asset.FullID);

                RestObjectPoster.BeginPostObject<AssetBase>(assetUrl, asset);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[GRID ASSET CLIENT]: {0}", e);
            }
        }

        #endregion
    }
}
