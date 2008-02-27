using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using OpenSim.Region.Environment.Modules.Terrain;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Modules.Terrain.FileLoaders
{
    public class RAW32 : ITerrainLoader
    {
        #region ITerrainLoader Members

        public ITerrainChannel LoadFile(string filename)
        {
            TerrainChannel retval = new TerrainChannel();

            FileInfo file = new FileInfo(filename);
            FileStream s = file.Open(FileMode.Open, FileAccess.Read);
            BinaryReader bs = new BinaryReader(s);
            int x, y;
            for (y = 0; y < retval.Height; y++)
            {
                for (x = 0; x < retval.Width; x++)
                {
                    retval[x, y] = bs.ReadSingle();
                }
            }

            bs.Close();
            s.Close();

            return retval;
        }

        public void SaveFile(string filename)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
