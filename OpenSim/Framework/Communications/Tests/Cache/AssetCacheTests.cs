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

using System.Threading;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Framework.Communications.Tests
{
    /// <summary>
    /// Asset cache tests
    /// </summary>
    [TestFixture] 
    public class AssetCacheTests
    {
        protected UUID m_assetIdReceived;
        protected AssetBase m_assetReceived;
        
        [Test]
        /// <summary>
        /// Test the 'asynchronous' get asset mechanism (though this won't be done asynchronously within this test)
        /// </summary>
        public void TestGetAsset()
        {
            UUID assetId = UUID.Parse("00000000-0000-0000-0000-000000000001");
            byte[] assetData = new byte[] { 3, 2, 1 }; 
            
            AssetBase asset = new AssetBase();
            asset.FullID = assetId;
            asset.Data = assetData;
            
            TestAssetDataPlugin assetPlugin = new TestAssetDataPlugin();
            assetPlugin.CreateAsset(asset);
            
            IAssetCache assetCache = new AssetCache(new SQLAssetServer(assetPlugin));
                        
            lock (this)
            {
                assetCache.GetAsset(assetId, AssetRequestCallback, false);                     
                Monitor.Wait(this, 60000);
            }            
            
            Assert.That(
                assetId, Is.EqualTo(m_assetIdReceived), "Asset id stored differs from asset id received");
            Assert.That(
                assetData, Is.EqualTo(m_assetReceived.Data), "Asset data stored differs from asset data received");
        }
                                
        private void AssetRequestCallback(UUID assetId, AssetBase asset)
        {
            m_assetIdReceived = assetId;
            m_assetReceived = asset;
            
            lock (this)
            {
                Monitor.PulseAll(this);
            }                  
        }
    }
}
