using System;
using libTerrain;
using OpenSim.Terrain;

/// <summary>
/// Summary description for Class1
/// </summary>
public class DemoFilter : ITerrainFilter
{
    public void Filter(Channel heightmap, string[] args)
    {
        Console.WriteLine("Hello world");
    }

    public string Register()
    {
        return "demofilter";
    }
}
