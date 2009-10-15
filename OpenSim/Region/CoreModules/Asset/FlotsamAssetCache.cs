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
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;


[assembly: Addin("FlotsamAssetCache", "1.1")]
[assembly: AddinDependency("OpenSim", "0.5")]

namespace Flotsam.RegionModules.AssetCache
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class FlotsamAssetCache : ISharedRegionModule, IImprovedAssetCache, IAssetService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;

        private const string m_ModuleName = "FlotsamAssetCache";
        private const string m_DefaultCacheDirectory = m_ModuleName;
        private string m_CacheDirectory = m_DefaultCacheDirectory;


        private List<char> m_InvalidChars = new List<char>();

        private int m_LogLevel = 1;
        private ulong m_HitRateDisplay = 1; // How often to display hit statistics, given in requests

        private static ulong m_Requests = 0;
        private static ulong m_RequestsForInprogress = 0;
        private static ulong m_DiskHits = 0;
        private static ulong m_MemoryHits = 0;
        private static double m_HitRateMemory = 0.0;
        private static double m_HitRateFile = 0.0;

#if WAIT_ON_INPROGRESS_REQUESTS
        private Dictionary<string, ManualResetEvent> m_CurrentlyWriting = new Dictionary<string, ManualResetEvent>();
        private int m_WaitOnInprogressTimeout = 3000;
#else
        private List<string> m_CurrentlyWriting = new List<string>();
#endif

        private ExpiringCache<string, AssetBase> m_MemoryCache = new ExpiringCache<string, AssetBase>();
        private bool m_MemoryCacheEnabled = true;

        // Expiration is expressed in hours.
        private const double m_DefaultMemoryExpiration = 1.0; 
        private const double m_DefaultFileExpiration = 48;
        private TimeSpan m_MemoryExpiration = TimeSpan.Zero;
        private TimeSpan m_FileExpiration = TimeSpan.Zero;
        private TimeSpan m_FileExpirationCleanupTimer = TimeSpan.Zero;

        private static int m_CacheDirectoryTiers = 1;
        private static int m_CacheDirectoryTierLen = 3;
        private static int m_CacheWarnAt = 30000;

        private System.Timers.Timer m_CachCleanTimer = new System.Timers.Timer();

        private IAssetService m_AssetService = null;
        private List<Scene> m_Scenes = new List<Scene>();

        private bool m_DeepScanBeforePurge = false;

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
                string name = moduleConfig.GetString("AssetCaching", "");

                if (name == Name)
                {
                    m_Enabled = true;
                    m_log.InfoFormat("[FLOTSAM ASSET CACHE]: {0} enabled", this.Name);

                    IConfig assetConfig = source.Configs["AssetCache"];
                    if (assetConfig == null)
                    {
                        m_log.Warn("[FLOTSAM ASSET CACHE]: AssetCache missing from OpenSim.ini, using defaults.");
                        m_log.InfoFormat("[FLOTSAM ASSET CACHE]: Cache Directory", m_DefaultCacheDirectory);
                        return;
                    }

                    m_CacheDirectory = assetConfig.GetString("CacheDirectory", m_DefaultCacheDirectory);
                    m_log.InfoFormat("[FLOTSAM ASSET CACHE]: Cache Directory", m_DefaultCacheDirectory);

                    m_MemoryCacheEnabled = assetConfig.GetBoolean("MemoryCacheEnabled", false);
                    m_MemoryExpiration = TimeSpan.FromHours(assetConfig.GetDouble("MemoryCacheTimeout", m_DefaultMemoryExpiration));

#if WAIT_ON_INPROGRESS_REQUESTS
                    m_WaitOnInprogressTimeout = assetConfig.GetInt("WaitOnInprogressTimeout", 3000);
#endif

                    m_LogLevel = assetConfig.GetInt("LogLevel", 1);
                    m_HitRateDisplay = (ulong)assetConfig.GetInt("HitRateDisplay", 1000);

                    m_FileExpiration = TimeSpan.FromHours(assetConfig.GetDouble("FileCacheTimeout", m_DefaultFileExpiration));
                    m_FileExpirationCleanupTimer = TimeSpan.FromHours(assetConfig.GetDouble("FileCleanupTimer", m_DefaultFileExpiration));
                    if ((m_FileExpiration > TimeSpan.Zero) && (m_FileExpirationCleanupTimer > TimeSpan.Zero))
                    {
                        m_CachCleanTimer.Interval = m_FileExpirationCleanupTimer.TotalMilliseconds;
                        m_CachCleanTimer.AutoReset = true;
                        m_CachCleanTimer.Elapsed += CleanupExpiredFiles;
                        m_CachCleanTimer.Enabled = true;
                        lock (m_CachCleanTimer)
                        {
                            m_CachCleanTimer.Start();
                        }
                    }
                    else
                    {
                        lock (m_CachCleanTimer)
                        {
                            m_CachCleanTimer.Enabled = false;
                        }
                    }

                    m_CacheDirectoryTiers = assetConfig.GetInt("CacheDirectoryTiers", 1);
                    if (m_CacheDirectoryTiers < 1)
                    {
                        m_CacheDirectoryTiers = 1;
                    }
                    else if (m_CacheDirectoryTiers > 3)
                    {
                        m_CacheDirectoryTiers = 3;
                    }

                    m_CacheDirectoryTierLen = assetConfig.GetInt("CacheDirectoryTierLength", 3);
                    if (m_CacheDirectoryTierLen < 1)
                    {
                        m_CacheDirectoryTierLen = 1;
                    }
                    else if (m_CacheDirectoryTierLen > 4)
                    {
                        m_CacheDirectoryTierLen = 4;
                    }

                    m_CacheWarnAt = assetConfig.GetInt("CacheWarnAt", 30000);

                    m_DeepScanBeforePurge = assetConfig.GetBoolean("DeepScanBeforePurge", false);

                    MainConsole.Instance.Commands.AddCommand(this.Name, true, "fcache status", "fcache status", "Display cache status", HandleConsoleCommand);
                    MainConsole.Instance.Commands.AddCommand(this.Name, true, "fcache clear",  "fcache clear [file] [memory]", "Remove all assets in the file and/or memory cache", HandleConsoleCommand);
                    MainConsole.Instance.Commands.AddCommand(this.Name, true, "fcache assets", "fcache assets", "Attempt a deep scan and cache of all assets in all scenes", HandleConsoleCommand);
                    MainConsole.Instance.Commands.AddCommand(this.Name, true, "fcache expire", "fcache expire <datetime>", "Purge cached assets older then the specified date/time", HandleConsoleCommand);

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
                scene.RegisterModuleInterface<IImprovedAssetCache>(this);
                m_Scenes.Add(scene);

                if (m_AssetService == null)
                {
                    m_AssetService = scene.RequestModuleInterface<IAssetService>();

                }
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_Enabled)
            {
                scene.UnregisterModuleInterface<IImprovedAssetCache>(this);
                m_Scenes.Remove(scene);
            }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        ////////////////////////////////////////////////////////////
        // IImprovedAssetCache
        //

        private void UpdateMemoryCache(string key, AssetBase asset)
        {
            if (m_MemoryCacheEnabled)
            {
                if (m_MemoryExpiration > TimeSpan.Zero)
                {
                    m_MemoryCache.AddOrUpdate(key, asset, m_MemoryExpiration);
                }
                else
                {
                    m_MemoryCache.AddOrUpdate(key, asset, DateTime.MaxValue);
                }
            }
        }

        public void Cache(AssetBase asset)
        {
            // TODO: Spawn this off to some seperate thread to do the actual writing
            if (asset != null)
            {
                UpdateMemoryCache(asset.ID, asset);

                string filename = GetFileName(asset.ID);

                try
                {
                    // If the file is already cached, don't cache it, just touch it so access time is updated
                    if (File.Exists(filename))
                    {
                        File.SetLastAccessTime(filename, DateTime.Now);
                    } else { 
                        
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

                        ThreadPool.QueueUserWorkItem(
                            delegate
                            {
                                WriteFileCache(filename, asset);
                            }
                        );
                    }
                }
                catch (Exception e)
                {
                    LogException(e);
                }
            }
        }

        public AssetBase Get(string id)
        {
            m_Requests++;

            AssetBase asset = null;

            if (m_MemoryCacheEnabled && m_MemoryCache.TryGetValue(id, out asset))
            {
                m_MemoryHits++;
            }
            else
            {
                string filename = GetFileName(id);
                if (File.Exists(filename))
                {
                    FileStream stream = null;
                    try
                    {
                        stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        BinaryFormatter bformatter = new BinaryFormatter();

                        asset = (AssetBase)bformatter.Deserialize(stream);

                        UpdateMemoryCache(id, asset);

                        m_DiskHits++;
                    }
                    catch (System.Runtime.Serialization.SerializationException e)
                    {
                        LogException(e);

                        // If there was a problem deserializing the asset, the asset may 
                        // either be corrupted OR was serialized under an old format 
                        // {different version of AssetBase} -- we should attempt to
                        // delete it and re-cache
                        File.Delete(filename);
                    }
                    catch (Exception e)
                    {
                        LogException(e);
                    }
                    finally
                    {
                        if (stream != null)
                            stream.Close();
                    }
                }


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
                }
#endif
            }

            if (((m_LogLevel >= 1)) && (m_HitRateDisplay != 0) && (m_Requests % m_HitRateDisplay == 0))
            {
                m_HitRateFile = (double)m_DiskHits / m_Requests * 100.0;

                m_log.InfoFormat("[FLOTSAM ASSET CACHE]: Cache Get :: {0} :: {1}", id, asset == null ? "Miss" : "Hit");
                m_log.InfoFormat("[FLOTSAM ASSET CACHE]: File Hit Rate {0}% for {1} requests", m_HitRateFile.ToString("0.00"), m_Requests);

                if (m_MemoryCacheEnabled)
                {
                    m_HitRateMemory = (double)m_MemoryHits / m_Requests * 100.0;
                    m_log.InfoFormat("[FLOTSAM ASSET CACHE]: Memory Hit Rate {0}% for {1} requests", m_HitRateMemory.ToString("0.00"), m_Requests);
                }

                m_log.InfoFormat("[FLOTSAM ASSET CACHE]: {0} unnessesary requests due to requests for assets that are currently downloading.", m_RequestsForInprogress);
                
            }

            return asset;
        }

        public void Expire(string id)
        {
            if (m_LogLevel >= 2)
                m_log.DebugFormat("[FLOTSAM ASSET CACHE]: Expiring Asset {0}.", id);

            try
            {
                string filename = GetFileName(id);
                if (File.Exists(filename))
                {
                    File.Delete(filename);
                }

                if (m_MemoryCacheEnabled)
                    m_MemoryCache.Remove(id);
            }
            catch (Exception e)
            {
                LogException(e);
            }
        }

        public void Clear()
        {
            if (m_LogLevel >= 2)
                m_log.Debug("[FLOTSAM ASSET CACHE]: Clearing Cache.");

            foreach (string dir in Directory.GetDirectories(m_CacheDirectory))
            {
                Directory.Delete(dir);
            }

            if (m_MemoryCacheEnabled)
                m_MemoryCache.Clear();
        }

        private void CleanupExpiredFiles(object source, ElapsedEventArgs e)
        {
            if (m_LogLevel >= 2)
                m_log.DebugFormat("[FLOTSAM ASSET CACHE]: Checking for expired files older then {0}.", m_FileExpiration.ToString());

            // Purge all files last accessed prior to this point
            DateTime purgeLine = DateTime.Now - m_FileExpiration;

            // An optional deep scan at this point will ensure assets present in scenes,
            // or referenced by objects in the scene, but not recently accessed 
            // are not purged.
            if (m_DeepScanBeforePurge)
            {
                CacheScenes();
            }

            foreach (string dir in Directory.GetDirectories(m_CacheDirectory))
            {
                CleanExpiredFiles(dir, purgeLine);
            }
        }

        /// <summary>
        /// Recurses through specified directory checking for asset files last 
        /// accessed prior to the specified purge line and deletes them.  Also 
        /// removes empty tier directories.
        /// </summary>
        /// <param name="dir"></param>
        private void CleanExpiredFiles(string dir, DateTime purgeLine)
        {

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
                m_log.WarnFormat("[FLOTSAM ASSET CACHE]: Cache folder exceeded CacheWarnAt limit {0} {1}.  Suggest increasing tiers, tier length, or reducing cache expiration", dir, dirSize);
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
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                stream = File.Open(tempname, FileMode.Create);
                BinaryFormatter bformatter = new BinaryFormatter();
                bformatter.Serialize(stream, asset);
                stream.Close();

                // Now that it's written, rename it so that it can be found.
                File.Move(tempname, filename);

                if (m_LogLevel >= 2)
                    m_log.DebugFormat("[FLOTSAM ASSET CACHE]: Cache Stored :: {0}", asset.ID);
            }
            catch (Exception e)
            {
                LogException(e);
            }
            finally
            {
                if (stream != null)
                    stream.Close();

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
                    if (m_CurrentlyWriting.Contains(filename))
                    {
                        m_CurrentlyWriting.Remove(filename);
                    }
#endif
                }

            }
        }

        private static void LogException(Exception e)
        {
            string[] text = e.ToString().Split(new char[] { '\n' });
            foreach (string t in text)
            {
                m_log.ErrorFormat("[FLOTSAM ASSET CACHE]: {0} ", t);
            }
        }

        /// <summary>
        /// Scan through the file cache, and return number of assets currently cached.
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        private int GetFileCacheCount(string dir)
        {
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
        /// <param name="RegionID"></param>
        private void StampRegionStatusFile(UUID RegionID)
        {
            string RegionCacheStatusFile = Path.Combine(m_CacheDirectory, "RegionStatus_" + RegionID.ToString() + ".fac");
            if (File.Exists(RegionCacheStatusFile))
            {
                File.SetLastWriteTime(RegionCacheStatusFile, DateTime.Now);
            }
            else
            {
                File.WriteAllText(RegionCacheStatusFile, "Please do not delete this file unless you are manually clearing your Flotsam Asset Cache.");
            }
        }

        /// <summary>
        /// Iterates through all Scenes, doing a deep scan through assets 
        /// to cache all assets present in the scene or referenced by assets 
        /// in the scene
        /// </summary>
        /// <returns></returns>
        private int CacheScenes()
        {
            UuidGatherer gatherer = new UuidGatherer(m_AssetService);

            Dictionary<UUID, int> assets = new Dictionary<UUID, int>();
            foreach (Scene s in m_Scenes)
            {
                StampRegionStatusFile(s.RegionInfo.RegionID);

                s.ForEachSOG(delegate(SceneObjectGroup e)
                {
                    gatherer.GatherAssetUuids(e, assets);
                }
                );
            }

            foreach (UUID assetID in assets.Keys)
            {
                string filename = GetFileName(assetID.ToString());

                if (File.Exists(filename))
                {
                    File.SetLastAccessTime(filename, DateTime.Now);
                }
                else
                {
                    m_AssetService.Get(assetID.ToString());
                }
            }

            return assets.Keys.Count;
        }

        /// <summary>
        /// Deletes all cache contents
        /// </summary>
        private void ClearFileCache()
        {
            foreach (string dir in Directory.GetDirectories(m_CacheDirectory))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (Exception e)
                {
                    LogException(e);
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
                    LogException(e);
                }
            }
        }

        #region Console Commands
        private void HandleConsoleCommand(string module, string[] cmdparams)
        {
            if (cmdparams.Length >= 2)
            {
                string cmd = cmdparams[1];
                switch (cmd)
                {
                    case "status":
                        m_log.InfoFormat("[FLOTSAM ASSET CACHE] Memory Cache : {0} assets", m_MemoryCache.Count);

                        int fileCount = GetFileCacheCount(m_CacheDirectory);
                        m_log.InfoFormat("[FLOTSAM ASSET CACHE] File Cache : {0} assets", fileCount);

                        foreach ( string s in Directory.GetFiles(m_CacheDirectory, "*.fac" ) )
                        {
                            m_log.Info("[FLOTSAM ASSET CACHE] Deep Scans were performed on the following regions:");

                            string RegionID = s.Remove(0,s.IndexOf("_")).Replace(".fac","");
                            DateTime RegionDeepScanTMStamp = File.GetLastWriteTime(s);
                            m_log.InfoFormat("[FLOTSAM ASSET CACHE] Region: {0}, {1}", RegionID, RegionDeepScanTMStamp.ToString("MM/dd/yyyy hh:mm:ss"));
                        }

                        break;

                    case "clear":
                        if (cmdparams.Length < 3)
                        {
                            m_log.Warn("[FLOTSAM ASSET CACHE] Please specify memory and/or file cache.");
                            break;
                        }
                        foreach (string s in cmdparams)
                        {
                            if (s.ToLower() == "memory")
                            {
                                m_MemoryCache.Clear();
                                m_log.Info("[FLOTSAM ASSET CACHE] Memory cache cleared.");
                            }
                            else if (s.ToLower() == "file")
                            {
                                ClearFileCache();
                                m_log.Info("[FLOTSAM ASSET CACHE] File cache cleared.");
                            }
                        }
                        break;


                    case "assets":
                        m_log.Info("[FLOTSAM ASSET CACHE] Caching all assets, in all scenes.");

                        Util.FireAndForget(delegate {
                            int assetsCached = CacheScenes();
                            m_log.InfoFormat("[FLOTSAM ASSET CACHE] Completed Scene Caching, {0} assets found.", assetsCached);

                        });                        

                        break;

                    case "expire":


                        if (cmdparams.Length >= 3)
                        {
                            m_log.InfoFormat("[FLOTSAM ASSET CACHE] Invalid parameters for Expire, please specify a valid date & time", cmd);
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
                            m_log.InfoFormat("[FLOTSAM ASSET CACHE] {0} is not a valid date & time", cmd);
                            break;
                        }

                        CleanExpiredFiles(m_CacheDirectory, expirationDate);

                        break;
                    default:
                        m_log.InfoFormat("[FLOTSAM ASSET CACHE] Unknown command {0}", cmd);
                        break;
                }
            }
            else if (cmdparams.Length == 1)
            {
                m_log.InfoFormat("[FLOTSAM ASSET CACHE] flotsamcache status - Display cache status");
                m_log.InfoFormat("[FLOTSAM ASSET CACHE] flotsamcache clearmem - Remove all assets cached in memory");
                m_log.InfoFormat("[FLOTSAM ASSET CACHE] flotsamcache clearfile - Remove all assets cached on disk");
                m_log.InfoFormat("[FLOTSAM ASSET CACHE] flotsamcache cachescenes - Attempt a deep cache of all assets in all scenes");
                m_log.InfoFormat("[FLOTSAM ASSET CACHE] flotsamcache <datetime> - Purge assets older then the specified date & time");

            }
        }

        #endregion

        #region IAssetService Members


        public AssetMetadata GetMetadata(string id)
        {
            AssetBase asset = Get(id);
            return asset.Metadata;
        }

        public byte[] GetData(string id)
        {
            AssetBase asset = Get(id);
            return asset.Data;
        }

        public bool Get(string id, object sender, AssetRetrieved handler)
        {
            AssetBase asset = Get(id);
            handler(id, sender, asset);
            return true;
        }

        public string Store(AssetBase asset)
        {
            if ((asset.FullID == null) || (asset.FullID == UUID.Zero))
            {
                asset.FullID = UUID.Random();
            }

            Cache(asset);

            return asset.ID;

        }

        public bool UpdateContent(string id, byte[] data)
        {
            AssetBase asset = Get(id);
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