using OpenSim.Region.Environment.Modules.Terrain;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.ExportSerialiser
{
    class SerialiseTerrain : IFileSerialiser
    {
        #region IFileSerialiser Members

        public string WriteToFile(Scene scene, string dir)
        {
            ITerrainLoader fileSystemExporter = new Terrain.FileLoaders.RAW32();
            string targetFileName = dir + "heightmap.r32";

            lock (scene.Heightmap)
            {
                fileSystemExporter.SaveFile(targetFileName, scene.Heightmap);
            }

            return "heightmap.r32";
        }

        #endregion
    }
}
