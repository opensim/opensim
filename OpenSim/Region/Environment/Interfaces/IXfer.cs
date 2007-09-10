using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface IXfer
    {
        bool AddNewFile(string fileName, byte[] data);
    }
}
