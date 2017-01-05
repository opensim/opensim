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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Services.AssetService
{
    /// <summary>
    /// A de-duplicating asset service.
    /// </summary>
    [Obsolete]
    public class XAssetService : XAssetServiceBase, IAssetService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected static XAssetService m_RootInstance;

        public XAssetService(IConfigSource config) : this(config, "AssetService") {}

        public XAssetService(IConfigSource config, string configName) : base(config, configName)
        {
            if (m_RootInstance == null)
            {
                m_RootInstance = this;

                if (m_AssetLoader != null)
                {
                    IConfig assetConfig = config.Configs[configName];
                    if (assetConfig == null)
                        throw new Exception("No AssetService configuration");

                    string loaderArgs = assetConfig.GetString("AssetLoaderArgs", String.Empty);

                    bool assetLoaderEnabled = assetConfig.GetBoolean("AssetLoaderEnabled", true);

                    if (assetLoaderEnabled && !HasChainedAssetService)
                    {
                        m_log.DebugFormat("[XASSET SERVICE]: Loading default asset set from {0}", loaderArgs);

                        m_AssetLoader.ForEachDefaultXmlAsset(
                            loaderArgs,
                            a =>
                            {
                                AssetBase existingAsset = Get(a.ID);
//                                AssetMetadata existingMetadata = GetMetadata(a.ID);

                                if (existingAsset == null || Util.SHA1Hash(existingAsset.Data) != Util.SHA1Hash(a.Data))
                                {
//                                    m_log.DebugFormat("[ASSET]: Storing {0} {1}", a.Name, a.ID);
                                    Store(a);
                                }
                            });
                    }

                    m_log.Debug("[XASSET SERVICE]: Local asset service enabled");
                    m_log.Error("[XASSET SERVICE]: THIS ASSET SERVICE HAS BEEN MARKED OBSOLETE. PLEASE USE FSAssetService");
                }
            }
        }

        public virtual AssetBase Get(string id)
        {
//            m_log.DebugFormat("[ASSET SERVICE]: Get asset for {0}", id);

            UUID assetID;

            if (!UUID.TryParse(id, out assetID))
            {
                m_log.WarnFormat("[XASSET SERVICE]: Could not parse requested asset id {0}", id);
                return null;
            }

            try
            {
                AssetBase asset = m_Database.GetAsset(assetID);

                if (asset != null)
                {
                    return asset;
                }
                else if (HasChainedAssetService)
                {
                    asset = m_ChainedAssetService.Get(id);

                    if (asset != null)
                        MigrateFromChainedService(asset);

                    return asset;
                }

                return null;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[XASSET SERVICE]: Exception getting asset {0} {1}", assetID, e);
                return null;
            }
        }

        public virtual AssetBase GetCached(string id)
        {
            return Get(id);
        }

        public virtual AssetMetadata GetMetadata(string id)
        {
//            m_log.DebugFormat("[XASSET SERVICE]: Get asset metadata for {0}", id);

            AssetBase asset = Get(id);

            if (asset != null)
                return asset.Metadata;
            else
                return null;
        }

        public virtual byte[] GetData(string id)
        {
//            m_log.DebugFormat("[XASSET SERVICE]: Get asset data for {0}", id);

            AssetBase asset = Get(id);

            if (asset != null)
                return asset.Data;
            else
                return null;
        }

        public virtual bool Get(string id, Object sender, AssetRetrieved handler)
        {
            //m_log.DebugFormat("[XASSET SERVICE]: Get asset async {0}", id);

            UUID assetID;

            if (!UUID.TryParse(id, out assetID))
                return false;

            AssetBase asset = Get(id);

            //m_log.DebugFormat("[XASSET SERVICE]: Got asset {0}", asset);

            handler(id, sender, asset);

            return true;
        }

        public virtual bool[] AssetsExist(string[] ids)
        {
            UUID[] uuid = Array.ConvertAll(ids, id => UUID.Parse(id));
            return m_Database.AssetsExist(uuid);
        }

        public virtual string Store(AssetBase asset)
        {
            bool exists = m_Database.AssetsExist(new[] { asset.FullID })[0];
            if (!exists)
            {
//                m_log.DebugFormat(
//                    "[XASSET SERVICE]: Storing asset {0} {1}, bytes {2}", asset.Name, asset.FullID, asset.Data.Length);
                m_Database.StoreAsset(asset);
            }
//            else
//            {
//                m_log.DebugFormat(
//                    "[XASSET SERVICE]: Not storing asset {0} {1}, bytes {2} as it already exists", asset.Name, asset.FullID, asset.Data.Length);
//            }

            return asset.ID;
        }

        public bool UpdateContent(string id, byte[] data)
        {
            return false;
        }

        public virtual bool Delete(string id)
        {
//            m_log.DebugFormat("[XASSET SERVICE]: Deleting asset {0}", id);

            UUID assetID;
            if (!UUID.TryParse(id, out assetID))
                return false;

            if (HasChainedAssetService)
                m_ChainedAssetService.Delete(id);

            return m_Database.Delete(id);
        }

        private void MigrateFromChainedService(AssetBase asset)
        {
            Store(asset);
            m_ChainedAssetService.Delete(asset.ID);
        }
    }
}
