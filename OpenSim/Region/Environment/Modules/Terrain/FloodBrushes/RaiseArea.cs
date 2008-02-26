using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Environment.Modules.Terrain;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Modules.Terrain.FloodBrushes
{
    class RaiseArea : ITerrainFloodEffect
    {
        #region ITerrainFloodEffect Members

        public void FloodEffect(ITerrainChannel map, bool[,] fillArea, double strength)
        {
            int x, y;
            for (x = 0; x < map.Width; x++)
            {
                for (y = 0; y < map.Height; y++)
                {
                    if (fillArea[x, y] == true)
                    {
                        map[x, y] += strength;
                    }
                }
            }
        }

        #endregion
    }
}
