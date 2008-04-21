namespace OpenSim.Region.Environment.Modules.Terrain
{
    public interface ITerrainModule
    {
        void LoadFromFile(string filename);
        void SaveToFile(string filename);
    }
}