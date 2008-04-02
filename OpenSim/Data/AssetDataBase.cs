using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework;

namespace OpenSim.Data
{
    public abstract class AssetDataBase : IAssetProvider
    {
        public abstract AssetBase FetchAsset(LLUUID uuid);
        public abstract void CreateAsset(AssetBase asset);
        public abstract void UpdateAsset(AssetBase asset);
        public abstract bool ExistsAsset(LLUUID uuid);
        public abstract void CommitAssets();

        public abstract string Version { get; }
        public abstract string Name { get; }
        public abstract void Initialise();
    }
}
