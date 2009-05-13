using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Tests.Common.Mock
{
    public class TestOSHttpResponse : OSHttpResponse
    {
        public override int StatusCode { get; set; }
        public override string ContentType { get; set; }
    }
}
