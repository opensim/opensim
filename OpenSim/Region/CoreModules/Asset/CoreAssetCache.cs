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

using log4net;
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Asset
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "CoreAssetCache")]
    public class CoreAssetCache : ISharedRegionModule, IAssetCache
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled;
        private Cache m_Cache;

        public string Name
        {
            get { return "CoreAssetCache"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];

            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("AssetCaching");
                //m_log.DebugFormat("[XXX] name = {0} (this module's name: {1}", name, Name);

                if (name == Name)
                {
                    IConfig assetConfig = source.Configs["AssetCache"];
                    if (assetConfig == null)
                    {
                        m_log.Error("[ASSET CACHE]: AssetCache missing from OpenSim.ini");
                        return;
                    }

                    m_Cache = new Cache(CacheMedium.Memory, CacheStrategy.Aggressive, CacheFlags.AllowUpdate);
                    m_Enabled = true;

                    m_log.Info("[ASSET CACHE]: Core asset cache enabled");

                    m_Cache.Size = assetConfig.GetInt("CacheBuckets", 32768);
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (m_Enabled)
                scene.RegisterModuleInterface<IAssetCache>(this);
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }

        ////////////////////////////////////////////////////////////
        // IAssetCache
        //
        public bool Check(string id)
        {
            // XXX This is probably not an efficient implementation.
            return Get(id) != null;
        }

        public void Cache(AssetBase asset)
        {
            if (asset != null)
                m_Cache.Store(asset.ID, asset);
        }

        public void CacheNegative(string id)
        {
            // We don't do negative caching
        }

        public AssetBase Get(string id)
        {
            return (AssetBase)m_Cache.Get(id);
        }

        public void Expire(string id)
        {
            m_Cache.Invalidate(id);
        }

        public void Clear()
        {
            m_Cache.Clear();
        }
    }
}
