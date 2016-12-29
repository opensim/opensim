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

using OpenSim.Framework;

namespace OpenSim.Framework
{
    public interface IAssetCache
    {
        /// <summary>
        /// Cache the specified asset.
        /// </summary>
        /// <param name='asset'></param>
        void Cache(AssetBase asset);

        /// <summary>
        /// Cache that the specified asset wasn't found.
        /// </summary>
        /// <param name='id'></param>
        /// <summary>
        void CacheNegative(string id);

        /// Get an asset by its id.
        /// </summary>
        /// <param name='id'></param>
        /// <returns>null if the asset does not exist.</returns>
        AssetBase Get(string id);

        /// <summary>
        /// Check whether an asset with the specified id exists in the cache.
        /// </summary>
        /// <param name='id'></param>
        bool Check(string id);

        /// <summary>
        /// Expire an asset from the cache.
        /// </summary>
        /// <param name='id'></param>
        void Expire(string id);

        /// <summary>
        /// Clear the cache.
        /// </summary>
        void Clear();
    }
}
