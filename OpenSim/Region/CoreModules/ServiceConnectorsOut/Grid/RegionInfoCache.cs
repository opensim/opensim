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
using System.Runtime.InteropServices;
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

        private static RegionsExpiringCache m_Cache;
        private int numberInstances;

        public RegionInfoCache()
        {
            if(m_Cache == null)
                m_Cache = new RegionsExpiringCache();
            numberInstances++;
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

            m_Cache.AddOrUpdate(scopeID, rinfo, CACHE_EXPIRATION_SECONDS);
        }

        public void Cache(UUID scopeID, GridRegion rinfo, float expireSeconds)
        {
            if (rinfo == null)
                return;

            m_Cache.AddOrUpdate(scopeID, rinfo, expireSeconds);
        }

        public void Remove(UUID scopeID, GridRegion rinfo)
        {
            m_Cache.Remove(scopeID, rinfo);
        }

        public void Remove(UUID scopeID, ulong regionHandle)
        {
            m_Cache.Remove(scopeID, regionHandle);
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

        public GridRegion Get(UUID scopeID, uint x, uint y, out bool inCache)
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

    //  dont care about endianess
    [StructLayout(LayoutKind.Explicit, Size = 8, Pack = 8)]
    public class fastRegionHandle
    {
        [FieldOffset(0)] public ulong handle;
        [FieldOffset(0)] public uint y;
        [FieldOffset(4)] public uint x;

        public fastRegionHandle(ulong h)
        {
            handle = h;
        }

        public fastRegionHandle(uint px, uint py)
        {
            y = py & 0xffffff00;
            x = px & 0xffffff00;
        }
        // actually do care
        public ulong toHandle()
        {
            if(BitConverter.IsLittleEndian)
                return handle;
            return  (ulong) x << 32 | (ulong)y ;
        }

        public static bool operator ==(fastRegionHandle value1, fastRegionHandle value2)
        {
            return value1.handle == value2.handle;
        }
        public static bool operator !=(fastRegionHandle value1, fastRegionHandle value2)
        {
            return value1.handle != value2.handle;
        }
        public override int GetHashCode()
        {
            return handle.GetHashCode();
        }
        public override bool Equals(Object obj)
        {
            if(obj == null)
                return false;
            fastRegionHandle p = obj as fastRegionHandle;
            return p.handle == handle;
        }
    }

/*
    [StructLayout(LayoutKind.Explicit, Size = 8, Pack = 8)]
    public class regionHandle
    {
        [FieldOffset(0)] private ulong handle;
        [FieldOffset(0)] public uint a;
        [FieldOffset(4)] public uint b;

        public regionHandle(ulong h)
        {
            handle = h;
        }

        public regionHandle(uint px, uint py)
        {
            if(BitConverter.IsLittleEndian)
            {
                a = py & 0xffffff00;
                b = px & 0xffffff00;
            }
            else
            {
                a = px & 0xffffff00;
                b = py & 0xffffff00;
            }
        }

        public uint x
        {
            get
            {
                if(BitConverter.IsLittleEndian)
                    return b;
                return a;
            }
            set
            {
                if(BitConverter.IsLittleEndian)
                    b = value & 0xffffff00;
                else
                    a = value & 0xffffff00;
             }
        }

        public uint y
        {
            get
            {
                if(BitConverter.IsLittleEndian)
                    return a;
                return b;
            }
            set
            {
                if(BitConverter.IsLittleEndian)
                    a = value;
                else
                    b = value;
             }
        }

        public static bool operator ==(regionHandle value1, regionHandle value2)
        {
            return value1.handle == value2.handle;
        }
        public static bool operator !=(regionHandle value1, regionHandle value2)
        {
            return value1.handle != value2.handle;
        }
        public override int GetHashCode()
        {
            return handle.GetHashCode();
        }
        public override bool Equals(Object obj)
        {
            if(obj == null)
                return false;
            regionHandle p = obj as regionHandle;
            return p.handle == handle;
        }
    }
*/

    public class RegionInfoForScope
    {
        public const ulong HANDLEMASK = 0xffffff00ffffff00ul;
        public const ulong HANDLECOORDMASK = 0xffffff00ul;

        private Dictionary<ulong, GridRegion> storage;
        private Dictionary<ulong, DateTime> expires;
        private Dictionary<string, ulong> byname;
        private Dictionary<UUID, ulong> byuuid;
        // includes handles to the inside of large regions
        private Dictionary<ulong, ulong> innerHandles = new Dictionary<ulong, ulong>();

        public RegionInfoForScope()
        {
            storage = new Dictionary<ulong, GridRegion>();
            expires = new Dictionary<ulong, DateTime>();
            byname = new Dictionary<string, ulong>();
            byuuid = new Dictionary<UUID, ulong>();
        }

        public RegionInfoForScope(GridRegion region, DateTime expire)
        {
           storage = new Dictionary<ulong, GridRegion>();
           expires = new Dictionary<ulong, DateTime>();
           byname = new Dictionary<string, ulong>();
           byuuid = new Dictionary<UUID, ulong>();

           ulong handle = region.RegionHandle & HANDLEMASK;
           storage[handle] = region;
           expires[handle] = expire;
           byname[region.RegionName] = handle;
           byuuid[region.RegionID] = handle;
           addToInner(region);
        }

        public void Add(GridRegion region, DateTime expire)
        {
            ulong handle = region.RegionHandle & HANDLEMASK;

            if(storage != null && storage.ContainsKey(handle))
                return;

            if(storage == null)
               storage = new Dictionary<ulong, GridRegion>();
            if(expires == null)
               expires = new Dictionary<ulong, DateTime>();
            if(byname == null)
                byname = new Dictionary<string, ulong>();
            if(byuuid == null)
                byuuid = new Dictionary<UUID, ulong>();

            storage[handle] = region;
            expires[handle] = expire;
            byname[region.RegionName] = handle;
            byuuid[region.RegionID] = handle;

            addToInner(region);
        }

        public void AddUpdate(GridRegion region, DateTime expire)
        {
            if(storage == null)
               storage = new Dictionary<ulong, GridRegion>();
            if(expires == null)
               expires = new Dictionary<ulong, DateTime>();
            if(byname == null)
                byname = new Dictionary<string, ulong>();
            if(byuuid == null)
                byuuid = new Dictionary<UUID, ulong>();

            ulong handle = region.RegionHandle & HANDLEMASK;

            if(expires.ContainsKey(handle))
            {
                if(expires[handle] < expire)
                    expires[handle] = expire;
                if(storage.ContainsKey(handle))
                {
                    GridRegion oldr = storage[handle];
                    if (oldr.RegionSizeX != region.RegionSizeX
                        || oldr.RegionSizeY != region.RegionSizeY)
                    {
                        removeFromInner(oldr);
                        addToInner(region);
                    }
                }
            }
            else
            {
                expires[handle] = expire;
                addToInner(region);
            }
            storage[handle] = region;
            byname[region.RegionName] = handle;
            byuuid[region.RegionID] = handle;

        }

        public void Remove(GridRegion region)
        {
            if(region == null)
                return;

            if(byname != null)
                byname.Remove(region.RegionName);
            if(byuuid != null)
                byuuid.Remove(region.RegionID);

            ulong handle = region.RegionHandle & HANDLEMASK;
            if(storage != null)
               storage.Remove(handle);
            removeFromInner(region);
            if(expires != null)
            {
                expires.Remove(handle);
                if(expires.Count == 0)
                    Clear();
            }
        }

        public void Remove(ulong handle)
        {
            handle &= HANDLEMASK;

            if(storage != null)
            {
                if(storage.ContainsKey(handle))
                {
                    GridRegion r = storage[handle];
                    if(byname != null)
                        byname.Remove(r.RegionName);
                    if(byuuid != null)
                        byuuid.Remove(r.RegionID);
                    removeFromInner(r);
                }
                storage.Remove(handle);
            }
            if(expires != null)
            {
                expires.Remove(handle);
                if(expires.Count == 0)
                    Clear();
            }
        }

        public void Clear()
        {
            if(expires != null)
                expires.Clear();
            if(storage != null)
                storage.Clear();
            if(byname != null)
                byname.Clear();
            if(byuuid != null)
                byuuid.Clear();
            byname = null;
            byuuid = null;
            storage = null;
            expires = null;
            innerHandles.Clear();
        }

        public bool Contains(GridRegion region)
        {
            if(storage == null)
                return false;
            if(region == null)
                return false;

            ulong handle = region.RegionHandle & HANDLEMASK;
            return storage.ContainsKey(handle);
        }

        public bool Contains(ulong handle)
        {
            if(storage == null)
                return false;

            handle &= HANDLEMASK;
            return storage.ContainsKey(handle);
        }

        public GridRegion get(ulong handle)
        {
            if(storage == null)
                return null;

            handle &= HANDLEMASK;
            if(storage.ContainsKey(handle))
                return storage[handle];

            if(!innerHandles.ContainsKey(handle))
                return null;

            ulong rhandle = innerHandles[handle];
            if(storage.ContainsKey(rhandle))
                return storage[rhandle];

            return null;
        }

        public GridRegion get(string name)
        {
            if(byname == null || !byname.ContainsKey(name))
                return null;

            ulong handle = byname[name];
            if(storage.ContainsKey(handle))
                return storage[handle];
            return null;
        }

        public GridRegion get(UUID id)
        {
            if(byuuid == null || !byuuid.ContainsKey(id))
                return null;

            ulong handle = byuuid[id];
            if(storage.ContainsKey(handle))
                return storage[handle];
            return null;
        }

        public GridRegion get(uint x, uint y)
        {
            if(storage == null)
                return null;

            // look for a handle first this should find normal size regions
            ulong handle = (ulong)x & HANDLECOORDMASK;
            handle <<= 32;
            handle |= ((ulong)y & HANDLECOORDMASK);

            if(storage.ContainsKey(handle))
                return storage[handle];

            if(!innerHandles.ContainsKey(handle))
                return null;

            ulong rhandle = innerHandles[handle];
            if(!storage.ContainsKey(rhandle))
                return null;

            GridRegion r = storage[rhandle];
            if(r == null)
                return null;

            // extra check, possible redundant

            int test = r.RegionLocX;
            if(x < test)
                return null;
            test += r.RegionSizeX;
            if(x >= test)
                return null;
            test = r.RegionLocY;
            if (y < test)
                return null;
            test +=  r.RegionSizeY;
            if (y < test)
                return r;

/*
            // next do the harder work
            foreach(KeyValuePair<ulong, GridRegion> kvp in storage)
            {
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
                if (y < test)
                    continue;
                test +=  r.RegionSizeY;
                if (y < test)
                    return r;
            }
*/
            return null;
        }

        public int expire(DateTime now )
        {
            if(expires == null || expires.Count == 0)
                return 0;

            List<ulong> toexpire = new List<ulong>();
            foreach(KeyValuePair<ulong, DateTime> kvp in expires)
            {
                if(kvp.Value < now)
                    toexpire.Add(kvp.Key);
            }

            if(toexpire.Count == 0)
                return expires.Count;

            if(toexpire.Count == expires.Count)
            {
                Clear();
                return 0;
            }

            foreach(ulong h in toexpire)
            {
                if(storage != null)
                {
                    if(storage.ContainsKey(h))
                    {
                        GridRegion r = storage[h];
                        if(byname != null)
                            byname.Remove(r.RegionName);
                        if(byuuid != null)
                            byuuid.Remove(r.RegionID);
                        removeFromInner(r);
                    }
                   storage.Remove(h);
                }
                if(expires != null)
                   expires.Remove(h);
            }

            if(expires.Count == 0)
            {
                byname = null;
                byuuid = null;
                storage = null;
                expires = null;
                return 0;
            }

            return expires.Count;
        }

        public int Count()
        {
            if(byname == null)
                return 0;
            else
                return byname.Count;
        }

        private void addToInner(GridRegion region)
        {
            int rsx = region.RegionSizeX;
            int rsy = region.RegionSizeY;

            if(rsx < 512 && rsy < 512)
                return;

            rsx >>= 8;
            rsy >>= 8;

            ulong handle = region.RegionHandle & HANDLEMASK;
            fastRegionHandle fh = new fastRegionHandle(handle);
            uint startY = fh.y;
            for(int i = 0; i < rsx; i++)
            {
                for(int j = 0; j < rsy ; j++)
                {
                    innerHandles[fh.toHandle()] = handle;
                    fh.y += 256;
                }

                fh.y = startY;
                fh.x += 256;
            }
        }

        private void removeFromInner(GridRegion region)
        {
            int rsx = region.RegionSizeX;
            int rsy = region.RegionSizeY;

            if(rsx < 512 && rsy < 512)
                return;

            rsx >>= 8;
            rsy >>= 8;
            ulong handle = region.RegionHandle & HANDLEMASK;
            fastRegionHandle fh = new fastRegionHandle(handle);
            uint startY = fh.y;
            for(int i = 0; i < rsx; i++)
            {
                for(int j = 0; j < rsy ; j++)
                {
                    innerHandles.Remove(fh.toHandle());
                    fh.y += 256;
                }

                fh.y = startY;
                fh.x += 256;
            }
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

        Dictionary<UUID, RegionInfoForScope> InfobyScope = new Dictionary<UUID, RegionInfoForScope>();
        private System.Timers.Timer timer = new System.Timers.Timer(TimeSpan.FromSeconds(CACHE_PURGE_HZ).TotalMilliseconds);

        public RegionsExpiringCache()
        {
            timer.Elapsed += PurgeCache;
            timer.Start();
        }

        public bool AddOrUpdate(UUID scope, GridRegion region, float expirationSeconds)
        {
            if(region == null)
                return false;

            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");

            try
            {
                DateTime expire = DateTime.UtcNow + TimeSpan.FromSeconds(expirationSeconds);

                RegionInfoForScope ris = null;
                if(!InfobyScope.TryGetValue(scope, out ris) || ris == null)
                {
                    ris = new RegionInfoForScope(region, expire);
                    InfobyScope[scope] = ris;
                }
                else
                    ris.AddUpdate(region, expire);

                return true;
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public void Clear()
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                foreach(RegionInfoForScope ris in InfobyScope.Values)
                     ris.Clear();
                InfobyScope.Clear();
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public bool Contains(UUID scope, GridRegion region)
        {
            if(region == null)
                return false;

            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");

            try
            {
                RegionInfoForScope ris = null;
                if(!InfobyScope.TryGetValue(scope, out ris) || ris == null)
                    return false;

                return ris.Contains(region);
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public bool Contains(UUID scope, ulong handle)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");

            try
            {
                RegionInfoForScope ris = null;
                if(!InfobyScope.TryGetValue(scope, out ris) || ris == null)
                    return false;

                return ris.Contains(handle);
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public int Count()
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");

            try
            {
                int count = 0;
                foreach(RegionInfoForScope ris in InfobyScope.Values)
                    count += ris.Count();
                return count;
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public bool Remove(UUID scope, ulong handle)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                RegionInfoForScope ris = null;
                if(!InfobyScope.TryGetValue(scope, out ris) || ris == null)
                    return false;

                ris.Remove(handle);
                if(ris.Count() == 0)
                    InfobyScope.Remove(scope);
                return true;
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public bool Remove(UUID scope, GridRegion region)
        {
            if(region == null)
                return false;

            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");
            try
            {
                RegionInfoForScope ris = null;
                if(!InfobyScope.TryGetValue(scope, out ris) || ris == null)
                    return false;

                ris.Remove(region);
                if(ris.Count() == 0)
                    InfobyScope.Remove(scope);
                return true;
            }
            finally { Monitor.Exit(syncRoot); }
        }

        public bool TryGetValue(UUID scope, ulong handle, out GridRegion value)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");

            value = null;
            try
            {
                RegionInfoForScope ris = null;
                if(!InfobyScope.TryGetValue(scope, out ris) || ris == null)
                    return false;
                value = ris.get(handle);
            }
            finally { Monitor.Exit(syncRoot); }

            return value != null;
        }

        public bool TryGetValue(UUID scope, string name, out GridRegion value)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");

            value = null;
            try
            {
                RegionInfoForScope ris = null;
                if(!InfobyScope.TryGetValue(scope, out ris) || ris == null)
                    return false;
                value = ris.get(name);
            }
            finally { Monitor.Exit(syncRoot); }

            return value != null;
        }

        public bool TryGetValue(UUID scope, UUID id, out GridRegion value)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");

            value = null;
            try
            {
                RegionInfoForScope ris = null;
                if(!InfobyScope.TryGetValue(scope, out ris) || ris == null)
                    return false;
                value = ris.get(id);
            }
            finally { Monitor.Exit(syncRoot); }

            return value != null;
        }

        // gets a region that contains world position (x,y)
        // hopefull will not take ages
        public bool TryGetValue(UUID scope, uint x, uint y, out GridRegion value)
        {
            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");

            value = null;
            try
            {
                RegionInfoForScope ris = null;
                if(!InfobyScope.TryGetValue(scope, out ris) || ris == null)
                    return false;

                value = ris.get(x, y);
            }
            finally { Monitor.Exit(syncRoot); }

            return value != null;
        }

        public bool Update(UUID scope, GridRegion region, double expirationSeconds)
        {
            if(region == null)
                return false;

            if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                throw new ApplicationException("Lock could not be acquired after " + MAX_LOCK_WAIT + "ms");

            try
            {
                RegionInfoForScope ris = null;
                if(!InfobyScope.TryGetValue(scope, out ris) || ris == null)
                    return false;

                DateTime expire = DateTime.UtcNow + TimeSpan.FromSeconds(expirationSeconds);
                ris.AddUpdate(region,expire);
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

            DateTime now = DateTime.UtcNow;

            try
            {
                // If we fail to acquire a lock on the synchronization root after MAX_LOCK_WAIT, skip this purge cycle
                if (!Monitor.TryEnter(syncRoot, MAX_LOCK_WAIT))
                    return;
                try
                {
                    List<UUID> expiredscopes = new List<UUID>();

                    foreach (KeyValuePair<UUID, RegionInfoForScope> kvp in InfobyScope)
                    {
                        if (kvp.Value.expire(now) == 0)
                            expiredscopes.Add(kvp.Key);
                    }

                    if (expiredscopes.Count > 0)
                    {
                        foreach (UUID sid in expiredscopes)
                            InfobyScope.Remove(sid);
                    }
                }
                finally { Monitor.Exit(syncRoot); }
            }
            finally { Monitor.Exit(isPurging); }
        }
    }
}
