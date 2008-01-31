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
* 
*/

using OpenSim.Framework;

namespace OpenSim.Framework.Statistics
{  
    public class SimExtraStatsReporter
    {
        private long assetsInCache;
        private long texturesInCache;        
        private long assetCacheMemoryUsage;
        private long textureCacheMemoryUsage;
        
        public long AssetsInCache { get { return assetsInCache; } }
        public long TexturesInCache { get { return texturesInCache; } }
        public long AssetCacheMemoryUsage { get { return assetCacheMemoryUsage; } }
        public long TextureCacheMemoryUsage { get { return textureCacheMemoryUsage; } }
        
        public void AddAsset(AssetBase asset)
        {
            assetsInCache++;
            assetCacheMemoryUsage += asset.Data.Length;
        }
        
        public void AddTexture(AssetBase image)
        {
            texturesInCache++;
            textureCacheMemoryUsage += image.Data.Length;
        }        

        /// <summary>
        /// Report back collected statistical information.
        /// </summary>
        /// <returns></returns>
        public string Report()
        {            
            return string.Format(
@"Asset   cache contains {0,6} assets   using {1,10:0.000}K
Texture cache contains {2,6} textures using {3,10:0.000}K",
                AssetsInCache, AssetCacheMemoryUsage / 1024.0, 
                TexturesInCache, TextureCacheMemoryUsage / 1024.0);
        }        
    }
}
