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
using System.Text;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;

namespace OpenSim.Tests.Common.Mock
{
    public class TestAssetCache : BaseAssetRepository, IAssetCache
    {
        public void AssetReceived(AssetBase asset, bool IsTexture)
        {
            throw new NotImplementedException();
        }

        public void AssetNotFound(UUID assetID, bool IsTexture)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public string Version
        {
            get { throw new NotImplementedException(); }
        }

        public string Name
        {
            get { throw new NotImplementedException(); }
        }

        public void Initialise()
        {
            throw new NotImplementedException();
        }

        public IAssetServer AssetServer
        {
            get { throw new NotImplementedException(); }
        }

        public void Initialise(ConfigSettings cs, IAssetServer server)
        {
            throw new NotImplementedException();
        }

        public void ShowState()
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool TryGetCachedAsset(UUID assetID, out AssetBase asset)
        {
            throw new NotImplementedException();
        }

        public void GetAsset(UUID assetID, AssetRequestCallback callback, bool isTexture)
        {
            throw new NotImplementedException();
        }

        public AssetBase GetAsset(UUID assetID, bool isTexture)
        {
            return FetchAsset(assetID);
        }

        public void AddAsset(AssetBase asset)
        {
            CreateAsset(asset);
        }

        public void ExpireAsset(UUID assetID)
        {
            throw new NotImplementedException();
        }

        public void AddAssetRequest(IClientAPI userInfo, TransferRequestPacket transferRequest)
        {
            throw new NotImplementedException();
        }
    }
}
