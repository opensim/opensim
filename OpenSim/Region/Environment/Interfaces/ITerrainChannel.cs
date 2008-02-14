using System;
namespace OpenSim.Region.Environment.Interfaces
{
    interface ITerrainChannel
    {
        int Height { get; }
        double this[int x, int y] { get; set; }
        int Width { get; }
    }
}
