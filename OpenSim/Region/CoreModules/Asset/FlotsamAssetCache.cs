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
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Timers;


using GlynnTucker.Cache;
using log4net;
using Nini.Config;
using Mono.Addins;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

[assembly: Addin("FlotsamAssetCache", "1.0")]
[assembly: AddinDependency("OpenSim", "0.5")]

namespace OpenSim.Region.CoreModules.Asset
{
    /// <summary>
    /// OpenSim.ini Options:
    /// -------
    /// [Modules]
    ///     AssetCaching = "FlotsamAssetCache"
    ///
    /// [AssetCache]
    ///    ; cache directory can be shared by multiple instances
    ///    CacheDirectory = /directory/writable/by/OpenSim/instance
    ///    
    ///    ; Set to false for disk cache only.
    ///    MemoryCacheEnabled = true
    ///    
    ///    ; How long {in hours} to keep assets cached in memory, .5 == 30 minutes
    ///    MemoryCacheTimeout = 2
    ///    
    ///    ; How long {in hours} to keep assets cached on disk, .5 == 30 minutes
    ///    ; Specify 0 if you do not want your disk cache to expire
    ///    FileCacheTimeout = 0
    ///    
    ///    ; How often {in hours} should the disk be checked for expired filed
    ///    ; Specify 0 to disable expiration checking
    ///    FileCleanupTimer = .166  ;roughly every 10 minutes
    /// -------
    /// </summary>

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class FlotsamAssetCache : ISharedRegionModule, IImprovedAssetCache
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;

        private const string m_ModuleName = "FlotsamAssetCache";
        private const string m_DefaultCacheDirectory = m_ModuleName;
        private string m_CacheDirectory = m_DefaultCacheDirectory;


        private List<char> m_InvalidChars = new List<char>();

        private uint m_DebugRate = 1; // How often to display hit statistics, given in requests

        private static ulong m_Requests = 0;
        private static ulong m_FileHits = 0;
        private static ulong m_MemoryHits = 0;
        private static double m_HitRateMemory = 0.0;
        private static double m_HitRateFile = 0.0;

        private List<string> m_CurrentlyWriting = new List<string>();

        delegate void AsyncWriteDelegate(string file, AssetBase obj);

        private ICache m_MemoryCache = new GlynnTucker.Cache.SimpleMemoryCache();
        private bool m_MemoryCacheEnabled = true;

        // Expiration is expressed in hours.
        private const double m_DefaultMemoryExpiration = 1.0; 
        private const double m_DefaultFileExpiration = 48;
        private TimeSpan m_MemoryExpiration = TimeSpan.Zero;
        private TimeSpan m_FileExpiration = TimeSpan.Zero;
        private TimeSpan m_FileExpirationCleanupTimer = TimeSpan.Zero;

        private System.Timers.Timer m_CachCleanTimer = new System.Timers.Timer();

        public FlotsamAssetCache()
        {
            m_InvalidChars.AddRange(Path.GetInvalidPathChars());
            m_InvalidChars.AddRange(Path.GetInvalidFileNameChars());
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
                string name = moduleConfig.GetString("AssetCaching", this.Name);
                m_log.DebugFormat("[XXX] name = {0} (this module's name: {1}", name, Name);

                if (name == Name)
                {
                    IConfig assetConfig = source.Configs["AssetCache"];
                    if (assetConfig == null)
                    {
                        m_log.Error("[ASSET CACHE]: AssetCache missing from OpenSim.ini");
                        return;
                    }

                    m_Enabled = true;

                    m_log.InfoFormat("[ASSET CACHE]: {0} enabled", this.Name);

                    m_CacheDirectory = assetConfig.GetString("CacheDirectory", m_DefaultCacheDirectory);
                    m_log.InfoFormat("[ASSET CACHE]: Cache Directory", m_DefaultCacheDirectory);

                    m_MemoryCacheEnabled = assetConfig.GetBoolean("MemoryCacheEnabled", true);
                    m_MemoryExpiration = TimeSpan.FromHours(assetConfig.GetDouble("MemoryCacheTimeout", m_DefaultMemoryExpiration));


                    m_FileExpiration = TimeSpan.FromHours(assetConfig.GetDouble("FileCacheTimeout", m_DefaultFileExpiration));
                    m_FileExpirationCleanupTimer = TimeSpan.FromHours(assetConfig.GetDouble("FileCleanupTimer", m_DefaultFileExpiration));
                    if ((m_FileExpiration > TimeSpan.Zero) && (m_FileExpirationCleanupTimer > TimeSpan.Zero))
                    {
                        m_CachCleanTimer.Interval = m_FileExpirationCleanupTimer.TotalMilliseconds;
                        m_CachCleanTimer.AutoReset = true;
                        m_CachCleanTimer.Elapsed += CleanupExpiredFiles;
                        m_CachCleanTimer.Enabled = true;
                        m_CachCleanTimer.Start();
                    }
                    else
                    {
                        m_CachCleanTimer.Enabled = false;
                    }
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
                scene.RegisterModuleInterface<IImprovedAssetCache>(this);
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }

        ////////////////////////////////////////////////////////////
        // IImprovedAssetCache
        //

        private void UpdateMemoryCache(string key, AssetBase asset)
        {
            if( m_MemoryCacheEnabled )
            {
                if (m_MemoryExpiration > TimeSpan.Zero)
                {
                    m_MemoryCache.AddOrUpdate(key, asset, m_MemoryExpiration);
                }
                else
                {
                    m_MemoryCache.AddOrUpdate(key, asset);
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
                            if (m_CurrentlyWriting.Contains(filename))
                            {
                                return;
                            }
                            else
                            {
                                m_CurrentlyWriting.Add(filename);
                            }
                        }

                        // Setup the actual writing so that it happens asynchronously
                        AsyncWriteDelegate awd = delegate( string file, AssetBase obj )
                        {
                            WriteFileCache(file, obj);
                        };

                        // Go ahead and cache it to disk
                        awd.BeginInvoke(filename, asset, null, null);
                    }
                }
                catch (Exception e)
                {
                    string[] text = e.ToString().Split(new char[] { '\n' });
                    foreach (string t in text)
                    {
                        m_log.InfoFormat("[ASSET CACHE]: {0} ", t);
                    }
                }
            }
        }

        public AssetBase Get(string id)
        {
            m_Requests++;

            AssetBase asset = null;

            object obj;
            if (m_MemoryCacheEnabled && m_MemoryCache.TryGet(id, out obj))
            {
                asset = (AssetBase)obj;
                m_MemoryHits++;
            }
            else
            {
                try
                {
                    string filename = GetFileName(id);
                    if (File.Exists(filename))
                    {
                        FileStream stream = File.Open(filename, FileMode.Open);
                        BinaryFormatter bformatter = new BinaryFormatter();

                        asset = (AssetBase)bformatter.Deserialize(stream);
                        stream.Close();

                        UpdateMemoryCache(id, asset);

                        m_FileHits++;
                    }
                }
                catch (Exception e)
                {
                    string[] text = e.ToString().Split(new char[] { '\n' });
                    foreach (string t in text)
                    {
                        m_log.InfoFormat("[ASSET CACHE]: {0} ", t);
                    }
                }
            }

            if (m_Requests % m_DebugRate == 0)
            {
                m_HitRateFile = (double)m_FileHits / m_Requests * 100.0;

                m_log.DebugFormat("[ASSET CACHE]: Cache Get :: {0} :: {1}", id, asset == null ? "Miss" : "Hit");
                m_log.DebugFormat("[ASSET CACHE]: File Hit Rate {0}% for {1} requests", m_HitRateFile.ToString("0.00"), m_Requests);

                if (m_MemoryCacheEnabled)
                {
                    m_HitRateMemory = (double)m_MemoryHits / m_Requests * 100.0;
                    m_log.DebugFormat("[ASSET CACHE]: Memory Hit Rate {0}% for {1} requests", m_HitRateMemory.ToString("0.00"), m_Requests);
                }
            }

            return asset;
        }

        public void Expire(string id)
        {
            try
            {
                string filename = GetFileName(id);
                if (File.Exists(filename))
                {
                    File.Delete(filename);
                }

                if( m_MemoryCacheEnabled )
                    m_MemoryCache.Remove(id);
            }
            catch (Exception e)
            {
                string[] text = e.ToString().Split(new char[] { '\n' });
                foreach (string t in text)
                {
                    m_log.InfoFormat("[ASSET CACHE]: {0} ", t);
                }
            }
        }

        public void Clear()
        {
            foreach (string dir in Directory.GetDirectories(m_CacheDirectory))
            {
                Directory.Delete(dir);
            }

            if( m_MemoryCacheEnabled )
                m_MemoryCache.Clear();
        }

        private void CleanupExpiredFiles(object source, ElapsedEventArgs e)
        {
            foreach (string dir in Directory.GetDirectories(m_CacheDirectory))
            {
                foreach (string file in Directory.GetFiles(dir))
                {
                    if (DateTime.Now - File.GetLastAccessTime(file) > m_FileExpiration)
                    {
                        File.Delete(file);
                    }
                }
            }
        }

        private string GetFileName(string id)
        {
            // Would it be faster to just hash the darn thing?
            foreach (char c in m_InvalidChars)
            {
                id = id.Replace(c, '_');
            }

            string p = id.Substring(id.Length - 4);
            p = Path.Combine(p, id);
            return Path.Combine(m_CacheDirectory, p);
        }

        private void WriteFileCache(string filename, AssetBase asset)
        {
            try
            {
                // Make sure the target cache directory exists
                string directory = Path.GetDirectoryName(filename);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write file first to a temp name, so that it doesn't look 
                // like it's already cached while it's still writing.
                string tempname = Path.Combine(directory, Path.GetRandomFileName());
                Stream stream = File.Open(tempname, FileMode.Create);
                BinaryFormatter bformatter = new BinaryFormatter();
                bformatter.Serialize(stream, asset);
                stream.Close();

                // Now that it's written, rename it so that it can be found.
                File.Move(tempname, filename);

                m_log.DebugFormat("[ASSET CACHE]: Cache Stored :: {0}", asset.ID);
            }
            catch (Exception e)
            {
                string[] text = e.ToString().Split(new char[] { '\n' });
                foreach (string t in text)
                {
                    m_log.InfoFormat("[ASSET CACHE]: {0} ", t);
                }

            }
            finally
            {
                // Even if the write fails with an exception, we need to make sure
                // that we release the lock on that file, otherwise it'll never get
                // cached
                lock (m_CurrentlyWriting)
                {
                    if (m_CurrentlyWriting.Contains(filename))
                    {
                        m_CurrentlyWriting.Remove(filename);
                    }
                }

            }
        }
    }
}
