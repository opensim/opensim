using System;
namespace OpenSim.Region.Environment.Interfaces
{
    public interface ITerrainChannel
    {
        int Height { get; }
        double this[int x, int y] { get; set; }
        int Width { get; }
    }
}
