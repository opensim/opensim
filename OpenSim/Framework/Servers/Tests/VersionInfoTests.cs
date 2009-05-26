using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace OpenSim.Framework.Servers.Tests
{
    [TestFixture]
    public class VersionInfoTests
    {
        [Test]
        public void TestVersionInfoLength()
        {
            Assert.AreEqual( VersionInfo.VERSIONINFO_VERSION_LENGTH, VersionInfo.Version.Length," VersionInfo.Version string not " + VersionInfo.VERSIONINFO_VERSION_LENGTH + " chars." );
        }
    }
}
