using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface IHttpRequests
    {
        LLUUID MakeHttpRequest(string url, string type, string body);
    }
}
