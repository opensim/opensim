using System;
using System.Collections.Generic;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IServiceThrottleModule
    {
        void Enqueue(Action continuation);
    }

}
