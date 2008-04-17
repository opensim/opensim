using System;
namespace OpenSim.Region.Environment.Modules.Terrain
{
    interface ITerrainModule
    {
        void LoadFromFile(string filename);
        void SaveToFile(string filename);
    }
}
