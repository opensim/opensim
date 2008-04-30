namespace OpenSim.Region.Environment.Modules.World.Terrain
{
    public interface ITerrainModule
    {
        void LoadFromFile(string filename);
        void SaveToFile(string filename);
    }
}