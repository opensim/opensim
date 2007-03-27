using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.CAPS
{
    public interface IRestHandler
    {
        string HandleREST(string requestBody, string requestURL, string requestMethod);
    }
}
