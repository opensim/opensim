using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using OpenSim.Region.Environment.Modules.Terrain;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Modules.Terrain.FileLoaders
{
    /// <summary>
    /// Terragen File Format Loader
    /// Built from specification at
    /// http://www.planetside.co.uk/terragen/dev/tgterrain.html
    /// </summary>
    class Terragen : ITerrainLoader
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        #region ITerrainLoader Members

        public ITerrainChannel LoadFile(string filename)
        {
            TerrainChannel retval = new TerrainChannel();

            FileInfo file = new FileInfo(filename);
            FileStream s = file.Open(FileMode.Open, FileAccess.Read);
            BinaryReader bs = new BinaryReader(s);

            bool eof = false;

            if (ASCIIEncoding.ASCII.GetString(bs.ReadBytes(16)) == "TERRAGENTERRAIN")
            {
                // Terragen file
                while (eof == false)
                {
                    int w = 256;
                    int h = 256;
                    string tmp = ASCIIEncoding.ASCII.GetString(bs.ReadBytes(4));
                    switch (tmp)
                    {
                        case "SIZE":
                            int sztmp = bs.ReadInt16() + 1;
                            w = sztmp;
                            h = sztmp;
                            bs.ReadInt16();
                            break;
                        case "XPTS":
                            w = bs.ReadInt16();
                            bs.ReadInt16();
                            break;
                        case "YPTS":
                            h = bs.ReadInt16();
                            bs.ReadInt16();
                            break;
                        case "ALTW":
                            eof = true;
                            Int16 heightScale = bs.ReadInt16();
                            Int16 baseHeight = bs.ReadInt16();
                            retval = new TerrainChannel(w, h);
                            int x, y;
                            for (x = 0; x < w; x++)
                            {
                                for (y = 0; y < h; y++)
                                {
                                    retval[x, y] = (double)baseHeight + (double)bs.ReadInt16() * (double)heightScale / 65536.0;
                                }
                            }
                            break;
                        default:
                            bs.ReadInt32();
                            break;
                    }
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
