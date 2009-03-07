using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public interface IHeightmap
    {
        int Height { get; }
        int Width { get; }
        double Get(int x, int y);
        void Set(int x, int y, double val);
    }
}
