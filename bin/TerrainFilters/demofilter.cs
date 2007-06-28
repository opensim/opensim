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

public class SineFilter : ITerrainFilter
{
    public void Filter(Channel heightmap, string[] args)
    {
        double max = heightmap.findMax();

        for (int x = 0; x < heightmap.w; x++)
        {
            for (int y = 0; y < heightmap.h; y++)
            {
                heightmap.set(x,y,((Math.Sin(heightmap.get(x,y) * Convert.ToDouble(args[1])) + 1) / 2) * max);
            }
        }
    }

    public string Register()
    {
        return "sinefilter";
    }

    public string Help()
    {
        return "sinefilter <theta> - Converts the heightmap to the functional output of a sine wave";
    }
}