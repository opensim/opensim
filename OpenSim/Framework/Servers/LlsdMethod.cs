using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Servers
{
    public delegate object LlsdMethod(object request, string path, string param);
    public delegate TResponse LlsdMethod<TResponse, TRequest>( TRequest request );
}
