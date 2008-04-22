using System;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.Terrain;
using OpenSim.Region.Environment.Modules.Terrain.FloodBrushes;

namespace OpenSim.Region.Modules.Terrain.Extensions.DefaultEffects.Effects
{
    public class ChannelDigger : ITerrainEffect
    {
        private readonly int num_h = 4;
        private readonly int num_w = 4;

        private readonly ITerrainFloodEffect raiseFunction = new RaiseArea();
        private readonly ITerrainFloodEffect smoothFunction = new SmoothArea();

        #region ITerrainEffect Members

        public void RunEffect(ITerrainChannel map)
        {
            FillMap(map, 15);
            BuildTiles(map, 7);
            SmoothMap(map, 3);
        }

        #endregion

        private void SmoothMap(ITerrainChannel map, int rounds)
        {
            Boolean[,] bitmap = new bool[map.Width,map.Height];
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    bitmap[x, y] = true;
                }
            }

            for (int i = 0; i < rounds; i++)
            {
                smoothFunction.FloodEffect(map, bitmap, 1.0);
            }
        }

        private void FillMap(ITerrainChannel map, double val)
        {
            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    map[x, y] = val;
        }

        private void BuildTiles(ITerrainChannel map, double height)
        {
            int channelWidth = (int) Math.Floor((map.Width / num_w) * 0.8);
            int channelHeight = (int) Math.Floor((map.Height / num_h) * 0.8);
            int channelXOffset = (map.Width / num_w) - channelWidth;
            int channelYOffset = (map.Height / num_h) - channelHeight;

            for (int x = 0; x < num_w; x++)
            {
                for (int y = 0; y < num_h; y++)
                {
                    int xoff = ((channelXOffset + channelWidth) * x) + (channelXOffset / 2);
                    int yoff = ((channelYOffset + channelHeight) * y) + (channelYOffset / 2);

                    Boolean[,] bitmap = new bool[map.Width,map.Height];

                    for (int dx = 0; dx < channelWidth; dx++)
                    {
                        for (int dy = 0; dy < channelHeight; dy++)
                        {
                            bitmap[dx + xoff, dy + yoff] = true;
                        }
                    }

                    raiseFunction.FloodEffect(map, bitmap, height);
                }
            }
        }
    }
}