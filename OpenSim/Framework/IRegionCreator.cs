using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework
{
    public delegate void NewRegionCreated(IScene scene);
    public interface IRegionCreator
    {
        event NewRegionCreated OnNewRegionCreated;
    }
}
