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
                this.Cache(rinfo.ScopeID, rinfo);
        }
        
        public void Cache(UUID scopeID, GridRegion rinfo)
        {
            if (rinfo == null)
                return;
            
            m_Cache.AddOrUpdate(scopeID, rinfo, CACHE_EXPIRATION_SECONDS);
        }

        public void CacheLocal(GridRegion rinfo)
        {
            if (rinfo == null)
                return;
            
            m_Cache.AddOrUpdate(rinfo.ScopeID, rinfo, 1e7f);
        }

        public void CacheNearNeighbour(UUID scopeID, GridRegion rinfo)
        {
            if (rinfo == null)
                return;
            
//            m_Cache.AddOrUpdate(scopeID, rinfo, CACHE_EXPIRATION_SECONDS);
            m_Cache.Add(scopeID, rinfo, CACHE_EXPIRATION_SECONDS); // don't override local regions
        }

        public void Cache(UUID scopeID, GridRegion rinfo, float expireSeconds)
        {
            if (rinfo == null)
                return;
            
            m_Cache.AddOrUpdate(scopeID, rinfo, expireSeconds);
        }

        public void Remove(UUID scopeID, UUID regionID)
        {
            m_Cache.Remove(scopeID, regionID);
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
    public class RegionKey 
    {
        public UUID ScopeID;
        public UUID RegionID;

        public RegionKey(UUID scopeID, UUID id)
        {
            ScopeID = scopeID;
            RegionID = id;
        }
  
        public override int GetHashCode()
        {
            int hash = ScopeID.GetHashCode();
            hash += hash * 23 + RegionID.GetHashCode();
            return hash;
        }

        public override bool Equals(Object b)
        {
            if(b == null)
                return false;
            RegionKey kb = b as RegionKey;
            return (ScopeID == kb.ScopeID && RegionID == kb.RegionID);    
        }
    }

    class RegionKeyEqual : EqualityComparer<RegionKey>
    {
        public override int GetHashCode(RegionKey rk)
        {
            return rk.GetHashCode();
        }

        public override bool Equals(RegionKey a, RegionKey b)
        {
            return (a.ScopeID == b.ScopeID && a.RegionID == b.RegionID);    
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
  
    public class RegionsExpiringCache
    {
        const double CACHE_PURGE_HZ = 60; // seconds
        const int MAX_LOCK_WAIT = 10000; // milliseconds
 
        /// <summary>For thread safety</summary>
        object syncRoot = new object();
        /// <summary>For thread safety</summary>
        object isPurging = new object();

        static RegionKeyEqual keyequal = new RegionKeyEqual();
        Dictionary<RegionKey, GridRegion> timedStorage = new Dictionary<RegionKey, GridRegion>(keyequal);
        Dictionary<RegionKey, DateTime> timedExpires = new Dictionary<RegionKey, DateTime>();
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

                DateTime expire = DateTime.UtcNow + TimeSpan.FromSeconds(expirationSeconds);
                timedStorage[key] = region;
                timedExpires[key] = expire;

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
                DateTime expire = DateTime.UtcNow + TimeSpan.FromSeconds(expirationSeconds);

                if (timedStorage.ContainsKey(key))
                {
                    timedStorage[key] = region;
                    if(expire > timedExpires[key])
                        timedExpires[key] = expire;

                    if(!InfobyScope.ContainsKey(scope))
                    {
                        RegionInfoByScope ris = new RegionInfoByScope(region, key);
                        InfobyScope[scope] = ris;
                    }
                    return false;
                }
                else
                {
                    timedStorage[key] = region;
                    timedExpires[key] = expire;
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
                timedExpires.Clear();
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
            return Remove(scope, region.RegionID);
        }
        public bool Remove(UUID scope, UUID regionID)
        {
            RegionKey key = new RegionKey(scope, regionID);

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
                    timedExpires.Remove(key);
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

                DateTime expire = DateTime.UtcNow + TimeSpan.FromSeconds(expirationSeconds);
                timedStorage[key] = region;
                if(expire > timedExpires[key])
                    timedExpires[key] = expire;

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
                    List<RegionKey> expiredkeys = new List<RegionKey>();

                    foreach (KeyValuePair<RegionKey, DateTime> kvp in timedExpires)
                    {
                        if (kvp.Value < signalTime)
                            expiredkeys.Add(kvp.Key);
                    }
                    
                    if (expiredkeys.Count > 0)
                    {
                        RegionInfoByScope ris;
                        foreach (RegionKey key in expiredkeys)
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
                            timedExpires.Remove(key);
                        }
                    }
                }
                finally { Monitor.Exit(syncRoot); }
            }
            finally { Monitor.Exit(isPurging); }
        }
    }
}
