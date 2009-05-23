using System;
using System.Collections.Generic;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Region.Framework.Scenes.Hypergrid
{
    public class HGUuidGatherer : UuidGatherer
    {
        protected string m_assetServerURL;
        protected HGAssetMapper m_assetMapper;

        public HGUuidGatherer(HGAssetMapper assMap, IAssetService assetCache, string assetServerURL) : base(assetCache)
        {
            m_assetMapper = assMap;
            m_assetServerURL = assetServerURL;
        }

        protected override AssetBase GetAsset(UUID uuid)
        {
            if (string.Empty == m_assetServerURL)
                return m_assetCache.Get(uuid.ToString());
            else
                return m_assetMapper.FetchAsset(m_assetServerURL, uuid);
        }
    }
}
