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
            CreateAsset( asset );
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
