using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Tests.Common.Mock
{
    public class TestOSHttpResponse : OSHttpResponse
    {
        private int m_statusCode;
        public override int StatusCode
        {
            get { return m_statusCode; }
            set { m_statusCode = value; }
        }

        private string m_contentType;
        public override string ContentType
        {
            get { return m_contentType; }
            set { m_contentType = value; }
        }
    }
}
