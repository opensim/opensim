using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Servers
{
    public interface ILlsdMethodHandler
    {
        string Handle(string request, string path);
    }


}
