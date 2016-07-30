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
using System.Threading;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using log4net;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Grid
{
    public class RegionInfoCache
    {
        private const float CACHE_EXPIRATION_SECONDS = 120; // 2 minutes  opensim regions change a lot

//        private static readonly ILog m_log =
//                LogManager.GetLogger(
//                MethodBase.GetCurrentMethod().DeclaringType);
        
        private RegionsExpiringCache m_Cache;

        public RegionInfoCache()
        {
            m_Cache = new RegionsExpiringCache();
        }

        public void Cache(GridRegion rinfo)
        {
            if (rinfo != null)
                this.Cache(rinfo.ScopeID,rinfo.RegionID,rinfo);
        }
        
        public void Cache(UUID scopeID, UUID regionID, GridRegion rinfo)
        {
            // for now, do not cache negative results; this is because
            // we need to figure out how to handle regions coming online
            // in a timely way
            if (rinfo == null)
                return;
            
            m_Cache.AddOrUpdate(scopeID, rinfo, CACHE_EXPIRATION_SECONDS);
        }

        public void Cache(UUID scopeID, UUID regionID, GridRegion rinfo, float expireSeconds)
        {
            // for now, do not cache negative results; this is because
            // we need to figure out how to handle regions coming online
            // in a timely way
            if (rinfo == null)
                return;
            
            m_Cache.AddOrUpdate(scopeID, rinfo, expireSeconds);
        }

        public GridRegion Get(UUID scopeID, UUID regionID, out bool inCache)
        {
            inCache = false;

            GridRegion rinfo = null;
            if (m_Cache.TryGetValue(scopeID, regionID, out rinfo))
            {
                inCache = true;
                return rinfo;
            }

            return null;
        }

        public GridRegion Get(UUID scopeID, ulong handle, out bool inCache)
        {
            inCache = false;

            GridRegion rinfo = null;
            if (m_Cache.TryGetValue(scopeID, handle, out rinfo))
            {
                inCache = true;
                return rinfo;
            }

            return null;
        }

        public GridRegion Get(UUID scopeID, string name, out bool inCache)
        {
            inCache = false;

            GridRegion rinfo = null;
            if (m_Cache.TryGetValue(scopeID, name, out rinfo))
            {
                inCache = true;
                return rinfo;
            }
            
            return null;
        }

        public GridRegion Get(UUID scopeID, int x, int y, out bool inCache)
        {
            inCache = false;

            GridRegion rinfo = null;
            if (m_Cache.TryGetValue(scopeID, x, y, out rinfo))
            {
                inCache = true;
                return rinfo;
            }
            
            return null;
        }

    }


    // following code partialy adapted from lib OpenMetaverse
    public class RegionKey : IComparable<RegionKey>
    {
        private UUID m_scopeID;
        private UUID m_RegionID;
        private DateTime m_expirationDate;

        public RegionKey(UUID scopeID, UUID id)
        {
            m_scopeID = scopeID;
            m_RegionID = id;
        }

        public UUID ScopeID
        {
            get { return m_scopeID; }
        }
        public DateTime ExpirationDate
        {
            get { return m_expirationDate; }
            set { m_expirationDate = value; }
        }

        public int GetHaskCode()
        {
            int hash = m_scopeID.GetHashCode();
            hash += hash * 23 + m_RegionID.GetHashCode();
            return hash;
        }

        public int CompareTo(RegionKey other)
        {
            return GetHashCode().CompareTo(other.GetHashCode());
        }
    }

    public class RegionInfoByScope
    {
        private Dictionary<string, RegionKey> byname;
        private Dictionary<ulong, RegionKey> byhandle;

        public RegionInfoByScope()
        {
            byname = new Dictionary<string, RegionKey>();
            byhandle = new Dictionary<ulong, RegionKey>();
        }

        public RegionInfoByScope(GridRegion region, RegionKey key)
        {
           byname = new Dictionary<string, RegionKey>();
           byhandle = new Dictionary<ulong, RegionKey>();

           byname[region.RegionName] = key;
           byhandle[region.RegionHandle] = key;
        }

        public void AddRegion(GridRegion region, RegionKey key)
        {
            if(byname == null)
                byname = new Dictionary<string, RegionKey>();
            if(byhandle == null)
                byhandle = new Dictionary<ulong, RegionKey>();

           byname[region.RegionName] = key;
           byhandle[region.RegionHandle] = key;
        }

        public void RemoveRegion(GridRegion region)
        {
            if(byname != null)
                byname.Remove(region.RegionName);
            if(byhandle != null)
                byhandle.Remove(region.RegionHandle);
        }

        public void Clear()
        {
            if(byname != null)
                byname.Clear();
            if(byhandle != null)
                byhandle.Clear();
            byname = null;
            byhandle = null;
        }

        public RegionKey get(string name)
        {
            if(byname == null || !byname.ContainsKey(name))
                return null;
            return byname[name];
        }

        public RegionKey get(ulong handle)
        {
            if(byhandle == null || !byhandle.ContainsKey(handle))
                return null;
            return byhandle[handle];
        }

        public int Count()
        {
            if(byname == null)
                return 0;
            else
                return byname.Count;
        }
    }
  
    public sealed class RegionsExpiringCache
    {
        const double CACHE_PURGE_HZ = 60; // seconds
        const int MAX_LOCK_WAIT = 10000; // milliseconds
 
        /// <summary>For thread safety</summary>
        object syncRoot = new object();
        /// <summary>For thread safety</summary>
        object isPurging = new object();

        Dictionary<RegionKey, GridRegion> timedStorage = new Dictionary<RegionKey, GridRegion>();
        Dictionary<UUID, RegionInfoByScope> InfobyScope = new Dictionary<UUID, RegionInfoByScope>();
        private System.Timers.Timer timer = new System.Timers.Timer(TimeSpan.FromSeconds(CACHE_PURGE_HZ).TotalMilliseconds);

        public RegionsExpiringCache()
        {
            timer.Elapsed += PurgeCache;
            timer.Start();
        }

        public bool Add(UUID scope, GridRegion region, float expirationSeconds)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");

            RegionKey key = new RegionKey(scope , region.RegionID);
         
            try
            {
                if (timedStorage.ContainsKey(key))
                    return false;

                key.ExpirationDate = DateTime.UtcNow + TimeSpan.FromSeconds(expirationSeconds);
                timedStorage.Add(key, region);

                RegionInfoByScope ris = null;
                if(!InfobyScope.TryGetValue(scope, out ris) || ris == null)
                {
                    ris = new RegionInfoByScope(region, key);
                    InfobyScope[scope] = ris;
                }
                else 
                    ris.AddRegion(region, key);

                return true;
            }
            finally { Monitor.Exit(syncRoot);}
        }

        public bool AddOrUpdate(UUID scope, GridRegion region, float expirationSeconds)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            
            try
            {
                RegionKey key = new RegionKey(scope, region.RegionID);
                key.ExpirationDate = DateTime.UtcNow + TimeSpan.FromSeconds(expirationSeconds);

                if (timedStorage.ContainsKey(key))
                {
                    timedStorage.Remove(key);
                    timedStorage.Add(key, region);

                    if(!InfobyScope.ContainsKey(scope))
                    {
                        RegionInfoByScope ris = new RegionInfoByScope(region, key);
                        InfobyScope[scope] = ris;
                    }
                    return false;
                }
                else
                {
                    timedStorage.Add(key, region);
                    RegionInfoByScope ris = null;
                    if(!InfobyScope.TryGetValue(scope, out ris) || ris == null)
                    {
                        ris = new RegionInfoByScope(region,key);
                        InfobyScope[scope] = ris;
                    }
                    else 
                        ris.AddRegion(region,key);
                    return true;
                }
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public void Clear()
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                timedStorage.Clear();
                InfobyScope.Clear();
            }
            finally { Monitor.Exit(syncRoot); }
        }
        
        public bool Contains(UUID scope, GridRegion region)
        {
            RegionKey key = new RegionKey(scope, region.RegionID);
            return Contains(key);
        }

        public bool Contains(RegionKey key)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                return timedStorage.ContainsKey(key);
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public int Count
        {
            get
            {
                return timedStorage.Count;
            }
        }

        public bool Remove(UUID scope, GridRegion region)
        {
            RegionKey key = new RegionKey(scope, region.RegionID);

            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {               
                if (timedStorage.ContainsKey(key))
                {
                    RegionInfoByScope ris = null;
                    if(InfobyScope.TryGetValue(scope, out ris) && ris != null)
                    {
                        GridRegion r = timedStorage[key];
                        if(r != null)
                            ris.RemoveRegion(r);
                        if(ris.Count() == 0)
                            InfobyScope.Remove(scope);
                    }
                    timedStorage.Remove(key);
                    return true;
                }
                else
                    return false;
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public bool TryGetValue(RegionKey key, out GridRegion value)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                if (timedStorage.ContainsKey(key))
                {
                    value = timedStorage[key];
                    return true;
                }
            }
            finally { Monitor.Exit(syncRoot); }

            value = null;
            return false;
        }

        public bool TryGetValue(UUID scope, UUID id, out GridRegion value)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                RegionKey rk = new RegionKey(scope, id);
                if(timedStorage.ContainsKey(rk))
                {
                    value = timedStorage[rk];
                    return true;
                }
            }
            finally { Monitor.Exit(syncRoot); }

            value = null;
            return false;
        }

        public bool TryGetValue(UUID scope, string name, out GridRegion value)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                value = null;
                RegionInfoByScope ris = null;
                if(!InfobyScope.TryGetValue(scope, out ris) || ris == null)
                    return false;

                RegionKey key = ris.get(name);
                if(key == null)
                    return false;

                if(timedStorage.ContainsKey(key))
                {
                    value = timedStorage[key];
                    return true;
                }
            }
            finally { Monitor.Exit(syncRoot); }

            return false;
        }

        public bool TryGetValue(UUID scope, ulong handle, out GridRegion value)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                value = null;
                RegionInfoByScope ris = null;
                if(!InfobyScope.TryGetValue(scope, out ris) || ris == null)
                    return false;

                RegionKey key = ris.get(handle);
                if(key == null)
                    return false;

                if(timedStorage.ContainsKey(key))
                {
                    value = timedStorage[key];
                    return true;
                }
            }
            finally { Monitor.Exit(syncRoot); }

            value = null;
            return false;
        }

        // gets a region that contains world position (x,y)
        // hopefull will not take ages
        public bool TryGetValue(UUID scope, int x, int y, out GridRegion value)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                value = null;

                if(timedStorage.Count == 0)
                    return false;

                foreach(KeyValuePair<RegionKey, GridRegion> kvp in timedStorage)
                {
                    if(kvp.Key.ScopeID != scope)
                        continue;

                    GridRegion r = kvp.Value;
                    if(r == null) // ??
                        continue;
                    int test = r.RegionLocX;
                    if(x < test)
                        continue;
                    test += r.RegionSizeX;
                    if(x >= test)
                        continue;
                    test = r.RegionLocY;
                    if(y < test)
                        continue;
                    test += r.RegionSizeY;
                    if (y < test)
                    {
                        value = r;
                        return true;
                    }
                 }
            }
            finally { Monitor.Exit(syncRoot); }

            value = null;
            return false;
        }

        public bool Update(UUID scope, GridRegion region, double expirationSeconds)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");

            RegionKey key = new RegionKey(scope, region.RegionID);
            try
            {
                if (!timedStorage.ContainsKey(key))
                    return false;

                timedStorage.Remove(key);
                key.ExpirationDate = DateTime.UtcNow + TimeSpan.FromSeconds(expirationSeconds);
                timedStorage.Add(key, region);
                return true;
            }
            finally { Monitor.Exit(syncRoot); }
        }

        /// <summary>
        /// Purges expired objects from the cache. Called automatically by the purge timer.
        /// </summary>
        private void PurgeCache(object sender, System.Timers.ElapsedEventArgs e)
        {
            // Only let one thread purge at once - a buildup could cause a crash
            // This could cause the purge to be delayed while there are lots of read/write ops 
            // happening on the cache
            if (!Monitor.TryEnter(isPurging))
                return;

            DateTime signalTime = DateTime.UtcNow;

            try
            {
                // If we fail to acquire a lock on the synchronization root after MAX_LOCK_WAIT, skip this purge cycle
                if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                    return;
                try
                {
                    OpenMetaverse.Lazy<List<object>> expiredItems = new OpenMetaverse.Lazy<List<object>>();

                    foreach (RegionKey timedKey in timedStorage.Keys)
                    {
                        if (timedKey.ExpirationDate < signalTime)
                        {
                            // Mark the object for purge
                            expiredItems.Value.Add(timedKey);
                        }
                        else
                        {
                            break;
                        }
                    }


                    RegionInfoByScope ris;
                    if (expiredItems.IsValueCreated)
                    {
                        foreach (RegionKey key in expiredItems.Value)
                        {
                            ris = null;
                            if(InfobyScope.TryGetValue(key.ScopeID, out ris) && ris != null)
                            {
                                GridRegion r = timedStorage[key];
                                if(r != null)
                                    ris.RemoveRegion(r);
                                
                                if(ris.Count() == 0)
                                    InfobyScope.Remove(key.ScopeID);
                            }
                            timedStorage.Remove(key);
                        }
                    }
                }
                finally { Monitor.Exit(syncRoot); }
            }
            finally { Monitor.Exit(isPurging); }
        }
    }
}
