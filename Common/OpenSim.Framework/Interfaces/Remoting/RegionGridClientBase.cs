using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Interfaces
{
    public abstract class RegionGridClientBase :IRegionGridClient
    {
        public abstract bool ExpectUser(string toRegionID, string name);
    }
}
