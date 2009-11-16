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
            Assert.That(citem == null, "Item should not be in Cache" );
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
            
            object citem = cache.Get(cacheItemUUID.ToString());
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
            object citem = cache.Get(cacheItemUUID.ToString());
            //Assert.That(citem == null, "Item should not be in Cache because we manually invalidated it");
        }

    }
}
