using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Types;
using libsecondlife;

namespace OpenSim.Framework.Interfaces
{
    public interface IAssetProvider :  IPlugin
    {
        AssetBase FetchAsset(LLUUID uuid);
        void CreateAsset(AssetBase asset);
        void UpdateAsset(AssetBase asset);
        bool ExistsAsset(LLUUID uuid);
        void CommitAssets(); // force a sync to the database
    }
}