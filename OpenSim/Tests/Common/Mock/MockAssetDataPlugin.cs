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
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;

namespace OpenSim.Tests.Common
{
    /// <summary>
    /// In memory asset data plugin for test purposes.  Could be another dll when properly filled out and when the
    /// mono addin plugin system starts co-operating with the unit test system.  Currently no locking since unit
    /// tests are single threaded.
    /// </summary>
    public class MockAssetDataPlugin : BaseAssetRepository, IAssetDataPlugin
    {
        public string Version { get { return "0"; } }
        public string Name { get { return "MockAssetDataPlugin"; } }

        public void Initialise() {}
        public void Initialise(string connect) {}
        public void Dispose() {}

        private readonly List<AssetBase> assets = new List<AssetBase>();

        public AssetBase GetAsset(UUID uuid)
        {
            return assets.Find(x=>x.FullID == uuid);
        }

        public void StoreAsset(AssetBase asset)
        {
            assets.Add(asset);
        }

        public List<AssetMetadata> FetchAssetMetadataSet(int start, int count) { return new List<AssetMetadata>(count); }

        public bool Delete(string id)
        {
            return false;
        }
    }
}
