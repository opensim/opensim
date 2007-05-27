using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Interfaces
{
    public interface IGridServerHost
    {
        void ConnectSim(string name);
        string RequestSimURL(uint regionHandle);
    }
}
