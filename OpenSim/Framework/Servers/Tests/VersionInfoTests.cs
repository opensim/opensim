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
            foreach (VersionInfo.Flavour flavour in Enum.GetValues(typeof(VersionInfo.Flavour)))
            {
                Assert.AreEqual(VersionInfo.VERSIONINFO_VERSION_LENGTH, VersionInfo.GetVersionString("0.0.0", flavour).Length, "0.0.0/" + flavour + " failed");
            }
        }
    }
}
