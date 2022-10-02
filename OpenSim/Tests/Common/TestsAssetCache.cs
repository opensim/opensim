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
using System.Runtime.Caching;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using log4net;

namespace OpenSim.Tests.Common
{
    public class TestsAssetCache : ISharedRegionModule, IAssetCache
    {

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled;
        public MemoryCache m_Cache;

        public string Name
        {
            get { return "TestsAssetCache"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
            m_Cache = MemoryCache.Default;
            m_Enabled = true;
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
            AssetBase asset;
            if (!Get(id, out asset))
                return false;
            return asset != null;
        }

        public void Cache(AssetBase asset, bool replace = true)
        {
            if (asset != null)
            {
                //CacheItemPolicy policy = new CacheItemPolicy();
                //m_Cache.Set(asset.ID, asset, policy);
            }
        }

        public void CacheNegative(string id)
        {
            // We don't do negative caching
        }

        public bool Get(string id, out AssetBase asset)
        {
            //asset = (AssetBase)m_Cache.Get(id);
            asset = null;
            return true;
        }

        public bool GetFromMemory(string id, out AssetBase asset)
        {
            //asset = (AssetBase)m_Cache.Get(id);
            asset = null;
            return true;
        }

        public AssetBase GetCached(string id)
        {
            //return (AssetBase)m_Cache.Get(id);
            return null;
        }

        public void Expire(string id)
        {
            //m_Cache.Remove(id);
        }

        public void Clear()
        {
        }
        /*
        public bool UpdateContent(string id, byte[] data)
        {
            return false;
        }
        */
    }
}
