using System;
using libTerrain;
using OpenSim.Terrain;

/// <summary>
/// A Demonstration Filter
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

    public string Help()
    {
        return "demofilter - Does nothing\n";
    }
}
