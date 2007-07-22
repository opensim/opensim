using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.Capabilities
{
    public delegate TResponse LLSDMethod<TRequest, TResponse>(TRequest request);
}
