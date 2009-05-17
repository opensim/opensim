using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Tests.Common.Mock
{
    public class BaseAssetRepository
    {
        protected Dictionary<UUID, AssetBase> Assets = new Dictionary<UUID, AssetBase>();

        public AssetBase FetchAsset(UUID uuid) 
        {
            if (ExistsAsset(uuid))
                return Assets[uuid];
            else
                return null;
        }

        public void CreateAsset(AssetBase asset) 
        {
            Assets[asset.FullID] = asset;
        }

        public void UpdateAsset(AssetBase asset) 
        {
            CreateAsset(asset);
        }

        public bool ExistsAsset(UUID uuid) 
        { 
            return Assets.ContainsKey(uuid); 
        }
    }
}