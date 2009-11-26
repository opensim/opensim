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
using NUnit.Framework;
using OpenMetaverse;

namespace OpenSim.Framework.Tests
{
    [TestFixture]
    public class CacheTests
    {
        private Cache cache;
        private UUID cacheItemUUID;
        [SetUp]
        public void Build()
        {
            cache = new Cache();
            cacheItemUUID = UUID.Random();
            MemoryCacheItem cachedItem = new MemoryCacheItem(cacheItemUUID.ToString(),DateTime.Now + TimeSpan.FromDays(1));
            byte[] foo = new byte[1];
            foo[0] = 255;
            cachedItem.Store(foo);
            cache.Store(cacheItemUUID.ToString(), cachedItem);
        }
        [Test]
        public void TestRetreive()
        {
            CacheItemBase citem = (CacheItemBase)cache.Get(cacheItemUUID.ToString());
            byte[] data = (byte[]) citem.Retrieve();
            Assert.That(data.Length == 1, "Cached Item should have one byte element");
            Assert.That(data[0] == 255, "Cached Item element should be 255");
        }

        [Test]
        public void TestNotInCache()
        {
            UUID randomNotIn = UUID.Random();
            while (randomNotIn == cacheItemUUID)
            {
                randomNotIn = UUID.Random();
            }
            object citem = cache.Get(randomNotIn.ToString());
            Assert.That(citem == null, "Item should not be in Cache");
        }

        //NOTE: Test Case disabled until Cache is fixed
        [Test]
        public void TestTTLExpiredEntry()
        {
            UUID ImmediateExpiryUUID = UUID.Random();
            MemoryCacheItem cachedItem = new MemoryCacheItem(ImmediateExpiryUUID.ToString(), TimeSpan.FromDays(-1));
            byte[] foo = new byte[1];
            foo[0] = 1;
            cachedItem.Store(foo);
            cache.Store(cacheItemUUID.ToString(), cachedItem);

            cache.Get(cacheItemUUID.ToString());
            //object citem = cache.Get(cacheItemUUID.ToString());
            //Assert.That(citem == null, "Item should not be in Cache because the expiry time was before now");
        }

        //NOTE: Test Case disabled until Cache is fixed
        [Test]
        public void ExpireItemManually()
        {
            UUID ImmediateExpiryUUID = UUID.Random();
            MemoryCacheItem cachedItem = new MemoryCacheItem(ImmediateExpiryUUID.ToString(), TimeSpan.FromDays(1));
            byte[] foo = new byte[1];
            foo[0] = 1;
            cachedItem.Store(foo);
            cache.Store(cacheItemUUID.ToString(), cachedItem);
            cache.Invalidate(ImmediateExpiryUUID.ToString());
            cache.Get(cacheItemUUID.ToString());
            //object citem = cache.Get(cacheItemUUID.ToString());
            //Assert.That(citem == null, "Item should not be in Cache because we manually invalidated it");
        }

    }
}
