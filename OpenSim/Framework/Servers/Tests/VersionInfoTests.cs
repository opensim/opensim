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
        public void TestVersionLength()
        {
            Assert.AreEqual(VersionInfo.VERSIONINFO_VERSION_LENGTH, VersionInfo.Version.Length," VersionInfo.Version string not " + VersionInfo.VERSIONINFO_VERSION_LENGTH + " chars." );
        }

        [Test]
        public void TestGetVersionStringLength()
        {
            Assert.AreEqual(VersionInfo.VERSIONINFO_VERSION_LENGTH, VersionInfo.GetVersionString("0.0.0").Length, "0.0.0 failed");
            Assert.AreEqual(VersionInfo.VERSIONINFO_VERSION_LENGTH, VersionInfo.GetVersionString("9.99.99").Length, "9.99.99 failed");
        }
    }
}
