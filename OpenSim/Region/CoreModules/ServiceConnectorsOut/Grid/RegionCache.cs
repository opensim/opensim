using System;
using System.Collections.Generic;
using System.Reflection;

using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using log4net;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Grid
{
    public class RegionCache
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;
        private Dictionary<ulong, GridRegion> m_neighbours = new Dictionary<ulong, GridRegion>();

        public string RegionName
        {
            get { return m_scene.RegionInfo.RegionName; }
        }

        public RegionCache(Scene s)
        {
            m_scene = s;
            m_scene.EventManager.OnRegionUp += OnRegionUp;
        }

        private void OnRegionUp(GridRegion otherRegion)
        {
            m_log.DebugFormat("[REGION CACHE]: (on region {0}) Region {1} is up @ {2}-{3}",
                m_scene.RegionInfo.RegionName, otherRegion.RegionName, otherRegion.RegionLocX, otherRegion.RegionLocY);

            m_neighbours[otherRegion.RegionHandle] = otherRegion;
        }

        public void Clear()
        {
            m_scene.EventManager.OnRegionUp -= OnRegionUp;
            m_neighbours.Clear();
        }

        public List<GridRegion> GetNeighbours()
        {
            return new List<GridRegion>(m_neighbours.Values);
        }
    }
}
