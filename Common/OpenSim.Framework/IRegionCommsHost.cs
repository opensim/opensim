using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Interfaces;

namespace OpenSim.Framework
{
    public delegate void ExpectUserDelegate();

    public interface IRegionCommsHost
    {
        event ExpectUserDelegate OnExpectUser;
        event GenericCall2 OnExpectChildAgent;
        event GenericCall2 OnAvatarCrossingIntoRegion;
    }
}
