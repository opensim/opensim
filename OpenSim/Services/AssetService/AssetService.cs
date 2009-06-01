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
using System.Reflection;
using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Services.AssetService
{
    public class AssetService : AssetServiceBase, IAssetService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        public AssetService(IConfigSource config) : base(config)
        {
            if (m_AssetLoader != null)
            {
                IConfig assetConfig = config.Configs["AssetService"];
                if (assetConfig == null)
                    throw new Exception("No AssetService configuration");

                string loaderArgs = assetConfig.GetString("AssetLoaderArgs",
                        String.Empty);

                m_log.InfoFormat("[ASSET]: Loading default asset set from {0}", loaderArgs);
                m_AssetLoader.ForEachDefaultXmlAsset(loaderArgs,
                        delegate(AssetBase a)
                        {
                            Store(a);
                        });
                
                m_log.Info("[ASSET CONNECTOR]: Local asset service enabled");
            }
        }

        public AssetBase Get(string id)
        {
            //m_log.DebugFormat("[ASSET SERVICE]: Get asset {0}", id);
            UUID assetID;

            if (!UUID.TryParse(id, out assetID))
                return null;

            return m_Database.FetchAsset(assetID);
        }

        public AssetMetadata GetMetadata(string id)
        {
            UUID assetID;

            if (!UUID.TryParse(id, out assetID))
                return null;

            AssetBase asset = m_Database.FetchAsset(assetID);
            return asset.Metadata;
        }

        public byte[] GetData(string id)
        {
            UUID assetID;

            if (!UUID.TryParse(id, out assetID))
                return null;

            AssetBase asset = m_Database.FetchAsset(assetID);
            return asset.Data;
        }

        public bool Get(string id, Object sender, AssetRetrieved handler)
        {
            //m_log.DebugFormat("[AssetService]: Get asset async {0}", id);
            
            UUID assetID;

            if (!UUID.TryParse(id, out assetID))
                return false;

            AssetBase asset = m_Database.FetchAsset(assetID);

            //m_log.DebugFormat("[AssetService]: Got asset {0}", asset);
            
            handler(id, sender, asset);

            return true;
        }

        public string Store(AssetBase asset)
        {
            //m_log.DebugFormat("[ASSET SERVICE]: Store asset {0} {1}", asset.Name, asset.ID);
            m_Database.CreateAsset(asset);

            return asset.ID;
        }

        public bool UpdateContent(string id, byte[] data)
        {
            return false;
        }

        public bool Delete(string id)
        {
            return false;
        }
    }
}
