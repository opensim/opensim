using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Interfaces
{
    public interface IRegionGridClient
    {
        bool ExpectUser(string toRegionID, string name);
    }
}
