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
    public class AssetService : AssetServiceBase, IAssetService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        protected static AssetService m_RootInstance;

        public AssetService(IConfigSource config)
            : this(config, "AssetService")
        {
        }

        public AssetService(IConfigSource config, string configName) : base(config, configName)
        {
            if (m_RootInstance == null)
            {
                m_RootInstance = this;

                if (m_AssetLoader != null)
                {
                    IConfig assetConfig = config.Configs[m_ConfigName];
                    if (assetConfig == null)
                        throw new Exception("No " + m_ConfigName + " configuration");

                    string loaderArgs = assetConfig.GetString("AssetLoaderArgs",
                            String.Empty);

                    bool assetLoaderEnabled = assetConfig.GetBoolean("AssetLoaderEnabled", true);

                    if (assetLoaderEnabled)
                    {
                        m_log.DebugFormat("[ASSET SERVICE]: Loading default asset set from {0}", loaderArgs);

                        m_AssetLoader.ForEachDefaultXmlAsset(
                            loaderArgs,
                            delegate(AssetBase a)
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

                    m_log.Debug("[ASSET SERVICE]: Local asset service enabled");
                }
            }
        }

        public virtual AssetBase Get(string id)
        {
//            m_log.DebugFormat("[ASSET SERVICE]: Get asset for {0}", id);

            UUID assetID;

            if (!UUID.TryParse(id, out assetID))
            {
                m_log.WarnFormat("[ASSET SERVICE]: Could not parse requested asset id {0}", id);
                return null;
            }

            try
            {
                return m_Database.GetAsset(assetID);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[ASSET SERVICE]: Exception getting asset {0} {1}", assetID, e);
                return null;
            }
        }

        public virtual AssetBase GetCached(string id)
        {
            return Get(id);
        }

        public virtual AssetMetadata GetMetadata(string id)
        {
//            m_log.DebugFormat("[ASSET SERVICE]: Get asset metadata for {0}", id);

            AssetBase asset = Get(id);

            if (asset != null)
                return asset.Metadata;
            else
                return null;
        }

        public virtual byte[] GetData(string id)
        {
//            m_log.DebugFormat("[ASSET SERVICE]: Get asset data for {0}", id);

            AssetBase asset = Get(id);

            if (asset != null)
                return asset.Data;
            else
                return null;
        }

        public virtual bool Get(string id, Object sender, AssetRetrieved handler)
        {
            //m_log.DebugFormat("[AssetService]: Get asset async {0}", id);

            handler(id, sender, Get(id));

            return true;
        }

        public virtual bool[] AssetsExist(string[] ids)
        {
            try
            {
                UUID[] uuid = Array.ConvertAll(ids, id => UUID.Parse(id));
                return m_Database.AssetsExist(uuid);
            }
            catch (Exception e)
            {
                m_log.Error("[ASSET SERVICE]: Exception getting assets ", e);
                return new bool[ids.Length];
            }
        }

        public virtual string Store(AssetBase asset)
        {
            bool exists = m_Database.AssetsExist(new[] { asset.FullID })[0];
            if (!exists)
            {
//                m_log.DebugFormat(
//                    "[ASSET SERVICE]: Storing asset {0} {1}, bytes {2}", asset.Name, asset.FullID, asset.Data.Length);
               if (!m_Database.StoreAsset(asset))
                {
                return UUID.Zero.ToString();
                }
            }
//            else
//            {
//                m_log.DebugFormat(
//                    "[ASSET SERVICE]: Not storing asset {0} {1}, bytes {2} as it already exists", asset.Name, asset.FullID, asset.Data.Length);
//            }

            return asset.ID;
        }

        public bool UpdateContent(string id, byte[] data)
        {
            return false;
        }

        public virtual bool Delete(string id)
        {
//            m_log.DebugFormat("[ASSET SERVICE]: Deleting asset {0}", id);

            UUID assetID;
            if (!UUID.TryParse(id, out assetID))
                return false;

            return m_Database.Delete(id);
        }
    }
}
