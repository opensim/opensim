using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using OpenSim.Tests.Common;

namespace OpenSim.Framework.Servers.HttpServer.Tests
{
    [TestFixture]
    public class BaseRequestHandlerTests
    {
        private const string BASE_PATH = "/testpath";

        private class BaseRequestHandlerImpl : BaseRequestHandler
        {
            public BaseRequestHandlerImpl(string httpMethod, string path) : base(httpMethod, path)
            {
            }
        }

        [Test]
        public void TestConstructor()
        {
            BaseRequestHandlerImpl handler = new BaseRequestHandlerImpl( null, null );
        }

        [Test]
        public void TestGetParams()
        {
            BaseRequestHandlerImpl handler = new BaseRequestHandlerImpl(null, BASE_PATH);

            BaseRequestHandlerTestHelper.BaseTestGetParams(handler, BASE_PATH);
        }

        [Test]
        public void TestSplitParams()
        {
            BaseRequestHandlerImpl handler = new BaseRequestHandlerImpl(null, BASE_PATH);

            BaseRequestHandlerTestHelper.BaseTestSplitParams(handler, BASE_PATH);
        }
    }
}
