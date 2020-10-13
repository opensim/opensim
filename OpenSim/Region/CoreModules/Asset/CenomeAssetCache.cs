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
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Asset
{
    /// <summary>
    /// Cenome memory asset cache.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cache is enabled by setting "AssetCaching" configuration to value "CenomeMemoryAssetCache".
    /// When cache is successfully enable log should have message
    /// "[ASSET CACHE]: Cenome asset cache enabled (MaxSize = XXX bytes, MaxCount = XXX, ExpirationTime = XXX)".
    /// </para>
    /// <para>
    /// Cache's size is limited by two parameters:
    /// maximal allowed size in bytes and maximal allowed asset count. When new asset
    /// is added to cache that have achieved either size or count limitation, cache
    /// will automatically remove less recently used assets from cache. Additionally
    /// asset's lifetime is controlled by expiration time.
    /// </para>
    /// <para>
    /// <list type="table">
    /// <listheader>
    /// <term>Configuration</term>
    /// <description>Description</description>
    /// </listheader>
    /// <item>
    /// <term>MaxSize</term>
    /// <description>Maximal size of the cache in bytes. Default value: 128MB (134 217 728 bytes).</description>
    /// </item>
    /// <item>
    /// <term>MaxCount</term>
    /// <description>Maximal count of assets stored to cache. Default value: 4096 assets.</description>
    /// </item>
    /// <item>
    /// <term>ExpirationTime</term>
    /// <description>Asset's expiration time in minutes. Default value: 30 minutes.</description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// Enabling Cenome Asset Cache:
    /// <code>
    /// [Modules]
    /// AssetCaching = "CenomeMemoryAssetCache"
    /// </code>
    /// Setting size and expiration time limitations:
    /// <code>
    /// [AssetCache]
    /// ; 256 MB (default: 134217728)
    /// MaxSize =  268435456
    /// ; How many assets it is possible to store cache (default: 4096)
    /// MaxCount = 16384
    /// ; Expiration time - 1 hour (default: 30 minutes)
    /// ExpirationTime = 60
    /// </code>
    /// </example>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "CenomeMemoryAssetCache")]
    public class CenomeMemoryAssetCache : IAssetCache, ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Cache's default maximal asset count.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Assuming that average asset size is about 32768 bytes.
        /// </para>
        /// </remarks>
        public const int DefaultMaxCount = 4096;

        /// <summary>
        /// Default maximal size of the cache in bytes
        /// </summary>
        /// <remarks>
        /// <para>
        /// 128MB = 128 * 1024^2 = 134 217 728 bytes.
        /// </para>
        /// </remarks>
        public const long DefaultMaxSize = 134217728;

        /// <summary>
        /// Asset's default expiration time in the cache.
        /// </summary>
        public static readonly TimeSpan DefaultExpirationTime = TimeSpan.FromMinutes(30.0);

        /// <summary>
        /// Cache object.
        /// </summary>
        private ICnmCache<string, AssetBase> m_cache;

        /// <summary>
        /// Count of cache commands
        /// </summary>
        private int m_cachedCount;

        /// <summary>
        /// How many gets before dumping statistics
        /// </summary>
        /// <remarks>
        /// If 0 or less, then disabled.
        /// </remarks>
        private int m_debugEpoch;

        /// <summary>
        /// Is Cenome asset cache enabled.
        /// </summary>
        private bool m_enabled;

        /// <summary>
        /// Count of get requests
        /// </summary>
        private int m_getCount;

        /// <summary>
        /// How many hits
        /// </summary>
        private int m_hitCount;

        /// <summary>
        /// Initialize asset cache module, with custom parameters.
        /// </summary>
        /// <param name="maximalSize">
        /// Cache's maximal size in bytes.
        /// </param>
        /// <param name="maximalCount">
        /// Cache's maximal count of assets.
        /// </param>
        /// <param name="expirationTime">
        /// Asset's expiration time.
        /// </param>
        protected void Initialize(long maximalSize, int maximalCount, TimeSpan expirationTime)
        {
            if (maximalSize <= 0 || maximalCount <= 0)
            {
                //m_log.Debug("[ASSET CACHE]: Cenome asset cache is not enabled.");
                m_enabled = false;
                return;
            }

            if (expirationTime <= TimeSpan.Zero)
            {
                // Disable expiration time
                expirationTime = TimeSpan.MaxValue;
            }

            // Create cache and add synchronization wrapper over it
            m_cache =
                CnmSynchronizedCache<string, AssetBase>.Synchronized(new CnmMemoryCache<string, AssetBase>(
                    maximalSize, maximalCount, expirationTime));
            m_enabled = true;
            m_log.DebugFormat(
                "[ASSET CACHE]: Cenome asset cache enabled (MaxSize = {0} bytes, MaxCount = {1}, ExpirationTime = {2})",
                maximalSize,
                maximalCount,
                expirationTime);
        }

        #region IAssetCache Members

        public bool Check(string id)
        {
            AssetBase asset;

            // XXX:This is probably not an efficient implementation.
            return m_cache.TryGetValue(id, out asset);
        }

        /// <summary>
        /// Cache asset.
        /// </summary>
        /// <param name="asset">
        /// The asset that is being cached.
        /// </param>
        public void Cache(AssetBase asset, bool replace = true)
        {
            if (asset != null)
            {
//                m_log.DebugFormat("[CENOME ASSET CACHE]: Caching asset {0}", asset.ID);

                long size = asset.Data != null ? asset.Data.Length : 1;
                m_cache.Set(asset.ID, asset, size);
                m_cachedCount++;
            }

        }

        public void CacheNegative(string id)
        {
            // We don't do negative caching
        }

        /// <summary>
        /// Clear asset cache.
        /// </summary>
        public void Clear()
        {
            m_cache.Clear();
        }

        /// <summary>
        /// Expire (remove) asset stored to cache.
        /// </summary>
        /// <param name="id">
        /// The expired asset's id.
        /// </param>
        public void Expire(string id)
        {
            m_cache.Remove(id);
        }

        /// <summary>
        /// Get asset stored
        /// </summary>
        /// <param name="id">
        /// The asset's id.
        /// </param>
        /// <returns>
        /// Asset if it is found from cache; otherwise <see langword="null"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Caller should always check that is return value <see langword="null"/>.
        /// Cache doesn't guarantee in any situation that asset is stored to it.
        /// </para>
        /// </remarks>
        public bool Get(string id, out AssetBase assetBase)
        {
            m_getCount++;
            if (m_cache.TryGetValue(id, out assetBase))
                m_hitCount++;

            if (m_getCount == m_debugEpoch)
            {
                m_log.DebugFormat(
                    "[ASSET CACHE]: Cached = {0}, Get = {1}, Hits = {2}%, Size = {3} bytes, Avg. A. Size = {4} bytes",
                    m_cachedCount,
                    m_getCount,
                    ((double) m_hitCount / m_getCount) * 100.0,
                    m_cache.Size,
                    m_cache.Size / m_cache.Count);
                m_getCount = 0;
                m_hitCount = 0;
                m_cachedCount = 0;
            }

//            if (null == assetBase)
//                m_log.DebugFormat("[CENOME ASSET CACHE]: Asset {0} not in cache", id);

            return true;
        }

        #endregion

        #region ISharedRegionModule Members

        /// <summary>
        /// Gets region module's name.
        /// </summary>
        public string Name
        {
            get { return "CenomeMemoryAssetCache"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        /// <summary>
        /// New region is being added to server.
        /// </summary>
        /// <param name="scene">
        /// Region's scene.
        /// </param>
        public void AddRegion(Scene scene)
        {
            if (m_enabled)
                scene.RegisterModuleInterface<IAssetCache>(this);
        }

        /// <summary>
        /// Close region module.
        /// </summary>
        public void Close()
        {
            if (m_enabled)
            {
                m_enabled = false;
                m_cache.Clear();
                m_cache = null;
            }
        }

        /// <summary>
        /// Initialize region module.
        /// </summary>
        /// <param name="source">
        /// Configuration source.
        /// </param>
        public void Initialise(IConfigSource source)
        {
            m_cache = null;
            m_enabled = false;

            IConfig moduleConfig = source.Configs[ "Modules" ];
            if (moduleConfig == null)
                return;

            string name = moduleConfig.GetString("AssetCaching");
            //m_log.DebugFormat("[XXX] name = {0} (this module's name: {1}", name, Name);

            if (name != Name)
                return;

            long maxSize = DefaultMaxSize;
            int maxCount = DefaultMaxCount;
            TimeSpan expirationTime = DefaultExpirationTime;

            IConfig assetConfig = source.Configs["AssetCache"];
            if (assetConfig != null)
            {
                // Get optional configurations
                maxSize = assetConfig.GetLong("MaxSize", DefaultMaxSize);
                maxCount = assetConfig.GetInt("MaxCount", DefaultMaxCount);
                expirationTime =
                    TimeSpan.FromMinutes(assetConfig.GetInt("ExpirationTime", (int)DefaultExpirationTime.TotalMinutes));

                // Debugging purposes only
                m_debugEpoch = assetConfig.GetInt("DebugEpoch", 0);
            }

            Initialize(maxSize, maxCount, expirationTime);
        }

        /// <summary>
        /// Initialization post handling.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Modules can use this to initialize connection with other modules.
        /// </para>
        /// </remarks>
        public void PostInitialise()
        {
        }

        /// <summary>
        /// Region has been loaded.
        /// </summary>
        /// <param name="scene">
        /// Region's scene.
        /// </param>
        /// <remarks>
        /// <para>
        /// This is needed for all module types. Modules will register
        /// Interfaces with scene in AddScene, and will also need a means
        /// to access interfaces registered by other modules. Without
        /// this extra method, a module attempting to use another modules'
        /// interface would be successful only depending on load order,
        /// which can't be depended upon, or modules would need to resort
        /// to ugly kludges to attempt to request interfaces when needed
        /// and unnecessary caching logic repeated in all modules.
        /// The extra function stub is just that much cleaner.
        /// </para>
        /// </remarks>
        public void RegionLoaded(Scene scene)
        {
        }

        /// <summary>
        /// Region is being removed.
        /// </summary>
        /// <param name="scene">
        /// Region scene that is being removed.
        /// </param>
        public void RemoveRegion(Scene scene)
        {
        }

        #endregion
    }
}
