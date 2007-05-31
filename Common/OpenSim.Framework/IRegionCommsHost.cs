using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework
{
    public delegate void ExpectUserDelegate();

    public interface IRegionCommsHost
    {
        event ExpectUserDelegate ExpectUser; 
    }
}
