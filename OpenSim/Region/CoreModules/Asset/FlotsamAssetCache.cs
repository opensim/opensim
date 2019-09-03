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

// Uncomment to make asset Get requests for existing
// #define WAIT_ON_INPROGRESS_REQUESTS

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Timers;
using log4net;
using Nini.Config;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;


//[assembly: Addin("FlotsamAssetCache", "1.1")]
//[assembly: AddinDependency("OpenSim", "0.8.1")]

namespace OpenSim.Region.CoreModules.Asset
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "FlotsamAssetCache")]
    public class FlotsamAssetCache : ISharedRegionModule, IAssetCache, IAssetService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled;
        private bool m_timerRunning;
        private bool m_cleanupRunning;

        private const string m_ModuleName = "FlotsamAssetCache";
        private const string m_DefaultCacheDirectory = "./assetcache";
        private string m_CacheDirectory = m_DefaultCacheDirectory;

        private readonly List<char> m_InvalidChars = new List<char>();

        private int m_LogLevel = 0;
        private ulong m_HitRateDisplay = 100; // How often to display hit statistics, given in requests

        private static ulong m_Requests;
        private static ulong m_RequestsForInprogress;
        private static ulong m_DiskHits;
        private static ulong m_MemoryHits;
        private static ulong m_weakRefHits;

#if WAIT_ON_INPROGRESS_REQUESTS
        private Dictionary<string, ManualResetEvent> m_CurrentlyWriting = new Dictionary<string, ManualResetEvent>();
        private int m_WaitOnInprogressTimeout = 3000;
#else
        private HashSet<string> m_CurrentlyWriting = new HashSet<string>();
#endif

        private bool m_FileCacheEnabled = true;

        private ExpiringCache<string, AssetBase> m_MemoryCache;
        private bool m_MemoryCacheEnabled = false;

        private ExpiringCache<string, object> m_negativeCache;
        private bool m_negativeCacheEnabled = true;
        private bool m_negativeCacheSliding = false;

        // Expiration is expressed in hours.
        private double m_MemoryExpiration = 0.016;
        private const double m_DefaultFileExpiration = 48;
        // Negative cache is in seconds
        private int m_negativeExpiration = 120;
        private TimeSpan m_FileExpiration = TimeSpan.FromHours(m_DefaultFileExpiration);
        private TimeSpan m_FileExpirationCleanupTimer = TimeSpan.FromHours(1.0);

        private static int m_CacheDirectoryTiers = 1;
        private static int m_CacheDirectoryTierLen = 3;
        private static int m_CacheWarnAt = 30000;

        private System.Timers.Timer m_CacheCleanTimer;

        private IAssetService m_AssetService;
        private List<Scene> m_Scenes = new List<Scene>();
        private object timerLock = new object();

        private Dictionary<string,WeakReference> weakAssetReferences = new Dictionary<string, WeakReference>();
        private object weakAssetReferencesLock = new object();
        private bool m_updateFileTimeOnCacheHit = false;

        public FlotsamAssetCache()
        {
            m_InvalidChars.AddRange(Path.GetInvalidPathChars());
            m_InvalidChars.AddRange(Path.GetInvalidFileNameChars());
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return m_ModuleName; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];

            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("AssetCaching", String.Empty);

                if (name == Name)
                {
                    m_MemoryCache = new ExpiringCache<string, AssetBase>();
                    m_negativeCache = new ExpiringCache<string, object>();
                    m_Enabled = true;

                    m_log.InfoFormat("[FLOTSAM ASSET CACHE]: {0} enabled", this.Name);

                    IConfig assetConfig = source.Configs["AssetCache"];
                    if (assetConfig == null)
                    {
                        m_log.Debug(
                           "[FLOTSAM ASSET CACHE]: AssetCache section missing from config (not copied config-include/FlotsamCache.ini.example?  Using defaults.");
                    }
                    else
                    {
                        m_FileCacheEnabled = assetConfig.GetBoolean("FileCacheEnabled", m_FileCacheEnabled);
                        m_CacheDirectory = assetConfig.GetString("CacheDirectory", m_DefaultCacheDirectory);

                        m_MemoryCacheEnabled = assetConfig.GetBoolean("MemoryCacheEnabled", m_MemoryCacheEnabled);
                        m_MemoryExpiration = assetConfig.GetDouble("MemoryCacheTimeout", m_MemoryExpiration);
                        m_MemoryExpiration *= 3600.0; // config in hours to seconds

                        m_negativeCacheEnabled = assetConfig.GetBoolean("NegativeCacheEnabled", m_negativeCacheEnabled);
                        m_negativeExpiration = assetConfig.GetInt("NegativeCacheTimeout", m_negativeExpiration);
                        m_negativeCacheSliding = assetConfig.GetBoolean("NegativeCacheSliding", m_negativeCacheSliding);
                        m_updateFileTimeOnCacheHit = assetConfig.GetBoolean("UpdateFileTimeOnCacheHit", m_updateFileTimeOnCacheHit);

    #if WAIT_ON_INPROGRESS_REQUESTS
                        m_WaitOnInprogressTimeout = assetConfig.GetInt("WaitOnInprogressTimeout", 3000);
    #endif

                        m_LogLevel = assetConfig.GetInt("LogLevel", m_LogLevel);
                        m_HitRateDisplay = (ulong)assetConfig.GetLong("HitRateDisplay", (long)m_HitRateDisplay);

                        m_FileExpiration = TimeSpan.FromHours(assetConfig.GetDouble("FileCacheTimeout", m_DefaultFileExpiration));
                        m_FileExpirationCleanupTimer
                            = TimeSpan.FromHours(
                                assetConfig.GetDouble("FileCleanupTimer", m_FileExpirationCleanupTimer.TotalHours));

                        m_CacheDirectoryTiers = assetConfig.GetInt("CacheDirectoryTiers", m_CacheDirectoryTiers);
                        m_CacheDirectoryTierLen = assetConfig.GetInt("CacheDirectoryTierLength", m_CacheDirectoryTierLen);

                        m_CacheWarnAt = assetConfig.GetInt("CacheWarnAt", m_CacheWarnAt);
                    }

                    m_log.InfoFormat("[FLOTSAM ASSET CACHE]: Cache Directory {0}", m_CacheDirectory);


                    if (m_CacheDirectoryTiers < 1)
                    {
                        m_CacheDirectoryTiers = 1;
                    }
                    else if (m_CacheDirectoryTiers > 3)
                    {
                        m_CacheDirectoryTiers = 3;
                    }

                    if (m_CacheDirectoryTierLen < 1)
                    {
                        m_CacheDirectoryTierLen = 1;
                    }
                    else if (m_CacheDirectoryTierLen > 4)
                    {
                        m_CacheDirectoryTierLen = 4;
                    }

                    MainConsole.Instance.Commands.AddCommand("Assets", true, "fcache status", "fcache status", "Display cache status", HandleConsoleCommand);
                    MainConsole.Instance.Commands.AddCommand("Assets", true, "fcache clear",  "fcache clear [file] [memory]", "Remove all assets in the cache.  If file or memory is specified then only this cache is cleared.", HandleConsoleCommand);
                    MainConsole.Instance.Commands.AddCommand("Assets", true, "fcache assets", "fcache assets", "Attempt a deep scan and cache of all assets in all scenes", HandleConsoleCommand);
                    MainConsole.Instance.Commands.AddCommand("Assets", true, "fcache expire", "fcache expire <datetime>", "Purge cached assets older then the specified date/time", HandleConsoleCommand);
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
            {
                scene.RegisterModuleInterface<IAssetCache>(this);
                m_Scenes.Add(scene);
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_Enabled)
            {
                scene.UnregisterModuleInterface<IAssetCache>(this);
                m_Scenes.Remove(scene);
                lock(timerLock)
                {
                    if(m_timerRunning && m_Scenes.Count <= 0)
                    {
                        m_timerRunning = false;
                        m_CacheCleanTimer.Stop();
                        m_CacheCleanTimer.Close();
                    }
                }
            }
        }

        public void RegionLoaded(Scene scene)
        {
            if (m_Enabled)
            {
                if(m_AssetService == null)
                    m_AssetService = scene.RequestModuleInterface<IAssetService>();
                lock(timerLock)
                {
                    if(!m_timerRunning)
                    {
                        if (m_FileCacheEnabled && (m_FileExpiration > TimeSpan.Zero) && (m_FileExpirationCleanupTimer > TimeSpan.Zero))
                        {
                            m_CacheCleanTimer = new System.Timers.Timer(m_FileExpirationCleanupTimer.TotalMilliseconds);
                            m_CacheCleanTimer.AutoReset = false;
                            m_CacheCleanTimer.Elapsed += CleanupExpiredFiles;
                            m_CacheCleanTimer.Start();
                            m_timerRunning = true;
                        }
                    }
                 }
                if (m_MemoryCacheEnabled)
                    m_MemoryCache = new ExpiringCache<string, AssetBase>();

                lock(weakAssetReferencesLock)
                    weakAssetReferences = new Dictionary<string, WeakReference>();
            }
        }

        ////////////////////////////////////////////////////////////
        // IAssetCache
        //
        private void UpdateWeakReference(string key, AssetBase asset)
        {
            WeakReference aref = new WeakReference(asset);
            lock(weakAssetReferencesLock)
                weakAssetReferences[key] = aref;
        }

        private void UpdateMemoryCache(string key, AssetBase asset)
        {
            // NOTE DO NOT USE SLIDEEXPIRE option on current libomv
            m_MemoryCache.AddOrUpdate(key, asset, m_MemoryExpiration);
        }

        private void UpdateFileCache(string key, AssetBase asset)
        {
            string filename = GetFileName(key);

            try
            {
                // If the file is already cached, don't cache it, just touch it so access time is updated
                if (File.Exists(filename))
                {
                    UpdateFileLastAccessTime(filename);
                }
                else
                {
                    // Once we start writing, make sure we flag that we're writing
                    // that object to the cache so that we don't try to write the
                    // same file multiple times.
                    lock (m_CurrentlyWriting)
                    {
#if WAIT_ON_INPROGRESS_REQUESTS
                        if (m_CurrentlyWriting.ContainsKey(filename))
                        {
                            return;
                        }
                        else
                        {
                            m_CurrentlyWriting.Add(filename, new ManualResetEvent(false));
                        }

#else
                        if (m_CurrentlyWriting.Contains(filename))
                        {
                            return;
                        }
                        else
                        {
                            m_CurrentlyWriting.Add(filename);
                        }
#endif

                    }

                    Util.FireAndForget(
                        delegate { WriteFileCache(filename, asset); }, null, "FlotsamAssetCache.UpdateFileCache");
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[FLOTSAM ASSET CACHE]: Failed to update cache for asset {0}.  Exception {1} {2}",
                    asset.ID, e.Message, e.StackTrace);
            }
        }

        public void Cache(AssetBase asset)
        {
            // TODO: Spawn this off to some seperate thread to do the actual writing
            if (asset != null)
            {
                //m_log.DebugFormat("[FLOTSAM ASSET CACHE]: Caching asset with id {0}", asset.ID);
                UpdateWeakReference(asset.ID, asset);

                if (m_MemoryCacheEnabled)
                    UpdateMemoryCache(asset.ID, asset);

                if (m_FileCacheEnabled)
                    UpdateFileCache(asset.ID, asset);
            }
        }

        public void CacheNegative(string id)
        {
            if (m_negativeCacheEnabled)
            {
                if (m_negativeCacheSliding)
                    m_negativeCache.AddOrUpdate(id, null, TimeSpan.FromSeconds(m_negativeExpiration));
                else
                    m_negativeCache.AddOrUpdate(id, null, m_negativeExpiration);
            }
        }

        /// <summary>
        /// Updates the cached file with the current time.
        /// </summary>
        /// <param name="filename">Filename.</param>
        /// <returns><c>true</c>, if the update was successful, false otherwise.</returns>
        private bool UpdateFileLastAccessTime(string filename)
        {
            try
            {
                File.SetLastAccessTime(filename, DateTime.Now);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private AssetBase GetFromWeakReference(string id)
        {
            AssetBase asset = null;
            WeakReference aref;

            lock(weakAssetReferencesLock)
            {
                if (weakAssetReferences.TryGetValue(id, out aref))
                {
                    asset = aref.Target as AssetBase;
                    if(asset == null)
                        weakAssetReferences.Remove(id);
                    else
                        m_weakRefHits++;
                }
            }
            return asset;
        }

        /// <summary>
        /// Try to get an asset from the in-memory cache.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private AssetBase GetFromMemoryCache(string id)
        {
            AssetBase asset = null;

            if (m_MemoryCache.TryGetValue(id, out asset))
                m_MemoryHits++;

            return asset;
        }

        private bool CheckFromMemoryCache(string id)
        {
            return m_MemoryCache.Contains(id);
        }

        /// <summary>
        /// Try to get an asset from the file cache.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>An asset retrieved from the file cache.  null if there was a problem retrieving an asset.</returns>
        private AssetBase GetFromFileCache(string id)
        {
            string filename = GetFileName(id);

#if WAIT_ON_INPROGRESS_REQUESTS
            // Check if we're already downloading this asset.  If so, try to wait for it to
            // download.
            if (m_WaitOnInprogressTimeout > 0)
            {
                m_RequestsForInprogress++;

                ManualResetEvent waitEvent;
                if (m_CurrentlyWriting.TryGetValue(filename, out waitEvent))
                {
                    waitEvent.WaitOne(m_WaitOnInprogressTimeout);
                    return Get(id);
                }
            }
#else
            // Track how often we have the problem that an asset is requested while
            // it is still being downloaded by a previous request.
            if (m_CurrentlyWriting.Contains(filename))
            {
                m_RequestsForInprogress++;
                return null;
            }
#endif

            AssetBase asset = null;

            if (File.Exists(filename))
            {
                try
                {
                    using (FileStream stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (stream.Length == 0) // Empty file will trigger exception below
                            return null;
                        BinaryFormatter bformatter = new BinaryFormatter();

                        asset = (AssetBase)bformatter.Deserialize(stream);

                        m_DiskHits++;
                    }
                }
                catch (System.Runtime.Serialization.SerializationException e)
                {
                    m_log.WarnFormat(
                        "[FLOTSAM ASSET CACHE]: Failed to get file {0} for asset {1}.  Exception {2} {3}",
                        filename, id, e.Message, e.StackTrace);

                    // If there was a problem deserializing the asset, the asset may
                    // either be corrupted OR was serialized under an old format
                    // {different version of AssetBase} -- we should attempt to
                    // delete it and re-cache
                    File.Delete(filename);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat(
                        "[FLOTSAM ASSET CACHE]: Failed to get file {0} for asset {1}.  Exception {2} {3}",
                        filename, id, e.Message, e.StackTrace);
                }
            }

            return asset;
        }

        private bool CheckFromFileCache(string id)
        {
            bool found = false;

            string filename = GetFileName(id);

            if (File.Exists(filename))
            {
                try
                {
                    using (FileStream stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (stream != null)
                            found = true;
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[FLOTSAM ASSET CACHE]: Failed to check file {0} for asset {1}.  Exception {2} {3}",
                        filename, id, e.Message, e.StackTrace);
                }
            }

            return found;
        }

        // For IAssetService
        public AssetBase Get(string id)
        {
            AssetBase asset;
            Get(id, out asset);
            return asset;
        }

        public bool Get(string id, out AssetBase asset)
        {
            asset = null;

            m_Requests++;

            object dummy;
            if (m_negativeCache.TryGetValue(id, out dummy))
            {
                return false;
            }

            asset = GetFromWeakReference(id);
            if (asset != null && m_updateFileTimeOnCacheHit)
            {
                string filename = GetFileName(id);
                UpdateFileLastAccessTime(filename);
            }

            if (m_MemoryCacheEnabled && asset == null)
            {
                asset = GetFromMemoryCache(id);
                if(asset != null)
                {
                    UpdateWeakReference(id,asset);
                    if (m_updateFileTimeOnCacheHit)
                    {
                        string filename = GetFileName(id);
                        UpdateFileLastAccessTime(filename);
                    }
                }
            }

            if (asset == null && m_FileCacheEnabled)
            {
                asset = GetFromFileCache(id);
                if(asset != null)
                    UpdateWeakReference(id,asset);
            }

            if (m_MemoryCacheEnabled && asset != null)
                UpdateMemoryCache(id, asset);

            if (((m_LogLevel >= 1)) && (m_HitRateDisplay != 0) && (m_Requests % m_HitRateDisplay == 0))
            {
                m_log.InfoFormat("[FLOTSAM ASSET CACHE]: Cache Get :: {0} :: {1}", id, asset == null ? "Miss" : "Hit");

                GenerateCacheHitReport().ForEach(l => m_log.InfoFormat("[FLOTSAM ASSET CACHE]: {0}", l));
            }

            return true;
        }

        public bool Check(string id)
        {
            if (m_MemoryCacheEnabled && CheckFromMemoryCache(id))
                return true;

            if (m_FileCacheEnabled && CheckFromFileCache(id))
                return true;
            return false;
        }

        public AssetBase GetCached(string id)
        {
            AssetBase asset;
            Get(id, out asset);
            return asset;
        }

        public void Expire(string id)
        {
            if (m_LogLevel >= 2)
                m_log.DebugFormat("[FLOTSAM ASSET CACHE]: Expiring Asset {0}", id);

            try
            {
                if (m_FileCacheEnabled)
                {
                    string filename = GetFileName(id);
                    if (File.Exists(filename))
                    {
                        File.Delete(filename);
                    }
                }

                if (m_MemoryCacheEnabled)
                    m_MemoryCache.Remove(id);

                lock(weakAssetReferencesLock)
                    weakAssetReferences.Remove(id);
            }
            catch (Exception e)
            {
                m_log.WarnFormat(
                    "[FLOTSAM ASSET CACHE]: Failed to expire cached file {0}.  Exception {1} {2}",
                    id, e.Message, e.StackTrace);
            }
        }

        public void Clear()
        {
            if (m_LogLevel >= 2)
                m_log.Debug("[FLOTSAM ASSET CACHE]: Clearing caches.");

            if (m_FileCacheEnabled && Directory.Exists(m_CacheDirectory))
            {
                foreach (string dir in Directory.GetDirectories(m_CacheDirectory))
                {
                    Directory.Delete(dir);
                }
            }

            if (m_MemoryCacheEnabled)
                m_MemoryCache = new ExpiringCache<string, AssetBase>();
            if (m_negativeCacheEnabled)
                m_negativeCache = new ExpiringCache<string, object>();

            lock(weakAssetReferencesLock)
                weakAssetReferences = new Dictionary<string, WeakReference>();
        }

        private void CleanupExpiredFiles(object source, ElapsedEventArgs e)
        {
            if (m_LogLevel >= 2)
                m_log.DebugFormat("[FLOTSAM ASSET CACHE]: Checking for expired files older then {0}.", m_FileExpiration);

            lock(timerLock)
            {
                if(!m_timerRunning || m_cleanupRunning)
                    return;
                m_cleanupRunning = true;
            }
            // Purge all files last accessed prior to this point
            DateTime purgeLine = DateTime.Now - m_FileExpiration;

            // An asset cache may contain local non-temporary assets that are not in the asset service.  Therefore,
            // before cleaning up expired files we must scan the objects in the scene to make sure that we retain
            // such local assets if they have not been recently accessed.
            TouchAllSceneAssets(false);
            if(Directory.Exists(m_CacheDirectory))
            {
                foreach (string dir in Directory.GetDirectories(m_CacheDirectory))
                    CleanExpiredFiles(dir, purgeLine);
            }

            lock(timerLock)
            {
                if(m_timerRunning)
                    m_CacheCleanTimer.Start();
                m_cleanupRunning = false;
            }
        }

        /// <summary>
        /// Recurses through specified directory checking for asset files last
        /// accessed prior to the specified purge line and deletes them.  Also
        /// removes empty tier directories.
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="purgeLine"></param>
        private void CleanExpiredFiles(string dir, DateTime purgeLine)
        {
            try
            {
                if(!Directory.Exists(dir))
                    return;

                foreach (string file in Directory.GetFiles(dir))
                {
                    if (File.GetLastAccessTime(file) < purgeLine)
                    {
                        File.Delete(file);
                    }
                }

                // Recurse into lower tiers
                foreach (string subdir in Directory.GetDirectories(dir))
                {
                    CleanExpiredFiles(subdir, purgeLine);
                }

                // Check if a tier directory is empty, if so, delete it
                int dirSize = Directory.GetFiles(dir).Length + Directory.GetDirectories(dir).Length;
                if (dirSize == 0)
                {
                    Directory.Delete(dir);
                }
                else if (dirSize >= m_CacheWarnAt)
                {
                    m_log.WarnFormat(
                        "[FLOTSAM ASSET CACHE]: Cache folder exceeded CacheWarnAt limit {0} {1}.  Suggest increasing tiers, tier length, or reducing cache expiration",
                        dir, dirSize);
                }
            }
            catch (DirectoryNotFoundException)
            {
                // If we get here, another node on the same box has
                // already removed the directory. Continue with next.
            }
            catch (Exception e)
            {
                m_log.Warn(
                    string.Format("[FLOTSAM ASSET CACHE]: Could not complete clean of expired files in {0}, exception  ", dir), e);
            }
        }

        /// <summary>
        /// Determines the filename for an AssetID stored in the file cache
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private string GetFileName(string id)
        {
            // Would it be faster to just hash the darn thing?
            foreach (char c in m_InvalidChars)
            {
                id = id.Replace(c, '_');
            }

            string path = m_CacheDirectory;
            for (int p = 1; p <= m_CacheDirectoryTiers; p++)
            {
                string pathPart = id.Substring((p - 1) * m_CacheDirectoryTierLen, m_CacheDirectoryTierLen);
                path = Path.Combine(path, pathPart);
            }

            return Path.Combine(path, id);
        }

        /// <summary>
        /// Writes a file to the file cache, creating any nessesary
        /// tier directories along the way
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="asset"></param>
        private void WriteFileCache(string filename, AssetBase asset)
        {
            Stream stream = null;

            // Make sure the target cache directory exists
            string directory = Path.GetDirectoryName(filename);

            // Write file first to a temp name, so that it doesn't look
            // like it's already cached while it's still writing.
            string tempname = Path.Combine(directory, Path.GetRandomFileName());

            try
            {
                try
                {
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    stream = File.Open(tempname, FileMode.Create);
                    BinaryFormatter bformatter = new BinaryFormatter();
                    bformatter.Serialize(stream, asset);
                }
                catch (IOException e)
                {
                    m_log.WarnFormat(
                        "[FLOTSAM ASSET CACHE]: Failed to write asset {0} to temporary location {1} (final {2}) on cache in {3}.  Exception {4} {5}.",
                        asset.ID, tempname, filename, directory, e.Message, e.StackTrace);

                    return;
                }
                catch (UnauthorizedAccessException)
                {
                }
                finally
                {
                    if (stream != null)
                        stream.Close();
                }

                try
                {
                    // Now that it's written, rename it so that it can be found.
                    //
    //                File.Copy(tempname, filename, true);
    //                File.Delete(tempname);
                    //
                    // For a brief period, this was done as a separate copy and then temporary file delete operation to
                    // avoid an IOException caused by move if some competing thread had already written the file.
                    // However, this causes exceptions on Windows when other threads attempt to read a file
                    // which is still being copied.  So instead, go back to moving the file and swallow any IOException.
                    //
                    // This situation occurs fairly rarely anyway.  We assume in this that moves are atomic on the
                    // filesystem.
                    File.Move(tempname, filename);

                    if (m_LogLevel >= 2)
                        m_log.DebugFormat("[FLOTSAM ASSET CACHE]: Cache Stored :: {0}", asset.ID);
                }
                catch (IOException)
                {
                    // If we see an IOException here it's likely that some other competing thread has written the
                    // cache file first, so ignore.  Other IOException errors (e.g. filesystem full) should be
                    // signally by the earlier temporary file writing code.
                }
            }
            finally
            {
                // Even if the write fails with an exception, we need to make sure
                // that we release the lock on that file, otherwise it'll never get
                // cached
                lock (m_CurrentlyWriting)
                {
#if WAIT_ON_INPROGRESS_REQUESTS
                    ManualResetEvent waitEvent;
                    if (m_CurrentlyWriting.TryGetValue(filename, out waitEvent))
                    {
                        m_CurrentlyWriting.Remove(filename);
                        waitEvent.Set();
                    }
#else
                    m_CurrentlyWriting.Remove(filename);
#endif
                }
            }
        }

        /// <summary>
        /// Scan through the file cache, and return number of assets currently cached.
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        private int GetFileCacheCount(string dir)
        {
            if(!Directory.Exists(dir))
                return 0;

            int count = Directory.GetFiles(dir).Length;

            foreach (string subdir in Directory.GetDirectories(dir))
            {
                count += GetFileCacheCount(subdir);
            }

            return count;
        }

        /// <summary>
        /// This notes the last time the Region had a deep asset scan performed on it.
        /// </summary>
        /// <param name="regionID"></param>
        private void StampRegionStatusFile(UUID regionID)
        {
            string RegionCacheStatusFile = Path.Combine(m_CacheDirectory, "RegionStatus_" + regionID.ToString() + ".fac");

            try
            {
                if (File.Exists(RegionCacheStatusFile))
                {
                    File.SetLastWriteTime(RegionCacheStatusFile, DateTime.Now);
                }
                else
                {
                    File.WriteAllText(
                        RegionCacheStatusFile,
                        "Please do not delete this file unless you are manually clearing your Flotsam Asset Cache.");
                }
            }
            catch (Exception e)
            {
                m_log.Warn(
                    string.Format(
                        "[FLOTSAM ASSET CACHE]: Could not stamp region status file for region {0}.  Exception  ",
                        regionID),
                    e);
            }
        }

        /// <summary>
        /// Iterates through all Scenes, doing a deep scan through assets
        /// to update the access time of all assets present in the scene or referenced by assets
        /// in the scene.
        /// </summary>
        /// <param name="storeUncached">
        /// If true, then assets scanned which are not found in cache are added to the cache.
        /// </param>
        /// <returns>Number of distinct asset references found in the scene.</returns>
        private int TouchAllSceneAssets(bool storeUncached)
        {
            UuidGatherer gatherer = new UuidGatherer(m_AssetService);

            Dictionary<UUID, bool> assetsFound = new Dictionary<UUID, bool>();

            foreach (Scene s in m_Scenes)
            {
                StampRegionStatusFile(s.RegionInfo.RegionID);

                s.ForEachSOG(delegate(SceneObjectGroup e)
                {
                    if(!m_timerRunning && !storeUncached)
                        return;

                    gatherer.AddForInspection(e);
                    gatherer.GatherAll();

                    if(!m_timerRunning && !storeUncached)
                        return;

                    foreach (UUID assetID in gatherer.GatheredUuids.Keys)
                    {
                        if (!assetsFound.ContainsKey(assetID))
                        {
                            string filename = GetFileName(assetID.ToString());

                            if (File.Exists(filename))
                            {
                                UpdateFileLastAccessTime(filename);
                                assetsFound[assetID] = true;
                            }
                            else if (storeUncached)
                            {
                                AssetBase cachedAsset = m_AssetService.Get(assetID.ToString());
                                if (cachedAsset == null && gatherer.GatheredUuids[assetID] != (sbyte)AssetType.Unknown)
                                    assetsFound[assetID] = false;
                                else
                                    assetsFound[assetID] = true;
                            }
                        }
                        else if (!assetsFound[assetID])
                        {
                            m_log.DebugFormat(
                                "[FLOTSAM ASSET CACHE]: Could not find asset {0}, type {1} referenced by object {2} at {3} in scene {4} when pre-caching all scene assets",
                                assetID, gatherer.GatheredUuids[assetID], e.Name, e.AbsolutePosition, s.Name);
                        }
                    }

                    gatherer.GatheredUuids.Clear();
                    if(!m_timerRunning && !storeUncached)
                        return;

                    if(!storeUncached)
                        Thread.Sleep(50);
                });
                if(!m_timerRunning && !storeUncached)
                    break;
            }

            return assetsFound.Count;
        }

        /// <summary>
        /// Deletes all cache contents
        /// </summary>
        private void ClearFileCache()
        {
            if(!Directory.Exists(m_CacheDirectory))
                return;

            foreach (string dir in Directory.GetDirectories(m_CacheDirectory))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat(
                        "[FLOTSAM ASSET CACHE]: Couldn't clear asset cache directory {0} from {1}.  Exception {2} {3}",
                        dir, m_CacheDirectory, e.Message, e.StackTrace);
                }
            }

            foreach (string file in Directory.GetFiles(m_CacheDirectory))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat(
                        "[FLOTSAM ASSET CACHE]: Couldn't clear asset cache file {0} from {1}.  Exception {1} {2}",
                        file, m_CacheDirectory, e.Message, e.StackTrace);
                }
            }
        }

        private List<string> GenerateCacheHitReport()
        {
            List<string> outputLines = new List<string>();

            double invReq = 100.0 / m_Requests;

            double weakHitRate = m_weakRefHits * invReq;
            int weakEntries = weakAssetReferences.Count;

            double fileHitRate = m_DiskHits * invReq;
            double TotalHitRate = weakHitRate + fileHitRate;

            outputLines.Add(
                string.Format("Total requests: {0}", m_Requests));
            outputLines.Add(
                string.Format("unCollected Hit Rate: {0}% ({1} entries)", weakHitRate.ToString("0.00"),weakEntries));
            outputLines.Add(
                string.Format("File Hit Rate: {0}%", fileHitRate.ToString("0.00")));

            if (m_MemoryCacheEnabled)
            {
                double HitRate = m_MemoryHits * invReq;
                outputLines.Add(
                    string.Format("Memory Hit Rate: {0}%", HitRate.ToString("0.00")));

                TotalHitRate += HitRate;
            }
            outputLines.Add(
                string.Format("Total Hit Rate: {0}%", TotalHitRate.ToString("0.00")));

            outputLines.Add(
                string.Format(
                    "Requests overlap during file writing: {0}", m_RequestsForInprogress));

            return outputLines;
        }

        #region Console Commands
        private void HandleConsoleCommand(string module, string[] cmdparams)
        {
            ICommandConsole con = MainConsole.Instance;

            if (cmdparams.Length >= 2)
            {
                string cmd = cmdparams[1];

                switch (cmd)
                {
                    case "status":
                        if (m_MemoryCacheEnabled)
                            con.Output("Memory Cache: {0} assets", null, m_MemoryCache.Count);
                        else
                            con.Output("Memory cache disabled");

                        if (m_FileCacheEnabled)
                        {
                            int fileCount = GetFileCacheCount(m_CacheDirectory);
                            con.Output("File Cache: {0} assets", null, fileCount);
                        }
                        else
                        {
                            con.Output("File cache disabled");
                        }

                        GenerateCacheHitReport().ForEach(l => con.Output(l));

                        if (m_FileCacheEnabled)
                        {
                            con.Output("Deep scans have previously been performed on the following regions:");

                            foreach (string s in Directory.GetFiles(m_CacheDirectory, "*.fac"))
                            {
                                string RegionID = s.Remove(0,s.IndexOf("_")).Replace(".fac","");
                                DateTime RegionDeepScanTMStamp = File.GetLastWriteTime(s);
                                con.Output("Region: {0}, {1}", null, RegionID, RegionDeepScanTMStamp.ToString("MM/dd/yyyy hh:mm:ss"));
                            }
                        }

                        break;

                    case "clear":
                        if (cmdparams.Length < 2)
                        {
                            con.Output("Usage is fcache clear [file] [memory]");
                            break;
                        }

                        bool clearMemory = false, clearFile = false;

                        if (cmdparams.Length == 2)
                        {
                            clearMemory = true;
                            clearFile = true;
                        }
                        foreach (string s in cmdparams)
                        {
                            if (s.ToLower() == "memory")
                                clearMemory = true;
                            else if (s.ToLower() == "file")
                                clearFile = true;
                        }

                        if (clearMemory)
                        {
                            if (m_MemoryCacheEnabled)
                            {
                                m_MemoryCache.Clear();
                                con.Output("Memory cache cleared.");
                            }
                            else
                            {
                                con.Output("Memory cache not enabled.");
                            }
                        }

                        if (clearFile)
                        {
                            if (m_FileCacheEnabled)
                            {
                                ClearFileCache();
                                con.Output("File cache cleared.");
                            }
                            else
                            {
                                con.Output("File cache not enabled.");
                            }
                        }

                        break;

                    case "assets":
                        lock(timerLock)
                        {
                            if(m_cleanupRunning)
                            {
                                con.Output("Flotsam assets check already running");
                                return;
                            }
                            m_cleanupRunning = true;
                        }

                        con.Output("Flotsam Ensuring assets are cached for all scenes.");

                        WorkManager.RunInThreadPool(delegate
                        {
                            bool wasRunning= false;
                            lock(timerLock)
                            {
                                if(m_timerRunning)
                                {
                                    m_CacheCleanTimer.Stop();
                                    m_timerRunning = false;
                                    wasRunning = true;
                                    Thread.Sleep(100);
                                }
                            }
                            int assetReferenceTotal = TouchAllSceneAssets(true);
                            GC.Collect();
                            lock(timerLock)
                            {
                                if(wasRunning)
                                {
                                    m_CacheCleanTimer.Start();
                                    m_timerRunning = true;
                                }
                                m_cleanupRunning = false;
                            }
                            con.Output("Completed check with {0} assets.", null, assetReferenceTotal);
                        }, null, "TouchAllSceneAssets", false);

                        break;

                    case "expire":
                        if (cmdparams.Length < 3)
                        {
                            con.Output("Invalid parameters for Expire, please specify a valid date & time");
                            break;
                        }

                        string s_expirationDate = "";
                        DateTime expirationDate;

                        if (cmdparams.Length > 3)
                        {
                            s_expirationDate = string.Join(" ", cmdparams, 2, cmdparams.Length - 2);
                        }
                        else
                        {
                            s_expirationDate = cmdparams[2];
                        }

                        if (!DateTime.TryParse(s_expirationDate, out expirationDate))
                        {
                            con.Output("{0} is not a valid date & time", null, cmd);
                            break;
                        }

                        if (m_FileCacheEnabled)
                            CleanExpiredFiles(m_CacheDirectory, expirationDate);
                        else
                            con.Output("File cache not active, not clearing.");

                        break;
                    default:
                        con.Output("Unknown command {0}", null, cmd);
                        break;
                }
            }
            else if (cmdparams.Length == 1)
            {
                con.Output("fcache assets - Attempt a deep cache of all assets in all scenes");
                con.Output("fcache expire <datetime> - Purge assets older then the specified date & time");
                con.Output("fcache clear [file] [memory] - Remove cached assets");
                con.Output("fcache status - Display cache status");
            }
        }

        #endregion

        #region IAssetService Members

        public AssetMetadata GetMetadata(string id)
        {
            AssetBase asset;
            Get(id, out asset);
            if (asset == null)
                return null;
            return asset.Metadata;
        }

        public byte[] GetData(string id)
        {
            AssetBase asset;
            Get(id, out asset);
            if (asset == null)
                return null;
            return asset.Data;
        }

        public bool Get(string id, object sender, AssetRetrieved handler)
        {
            AssetBase asset;
            if (!Get(id, out asset))
                return false;
            handler(id, sender, asset);
            return true;
        }

        public bool[] AssetsExist(string[] ids)
        {
            bool[] exist = new bool[ids.Length];

            for (int i = 0; i < ids.Length; i++)
            {
                exist[i] = Check(ids[i]);
            }

            return exist;
        }

        public string Store(AssetBase asset)
        {
            if (asset.FullID == UUID.Zero)
            {
                asset.FullID = UUID.Random();
            }

            Cache(asset);

            return asset.ID;
        }

        public bool UpdateContent(string id, byte[] data)
        {
            AssetBase asset;
            if (!Get(id, out asset))
                return false;
            asset.Data = data;
            Cache(asset);
            return true;
        }

        public bool Delete(string id)
        {
            Expire(id);
            return true;
        }

        #endregion
    }
}
