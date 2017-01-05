/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using NUnit.Framework;
using OpenSim.Tests.Common;

namespace OpenSim.Framework.Tests
{
    [TestFixture]
    public class LocationTest : OpenSimTestCase
    {
        [Test]
        public void locationRegionHandleRegionHandle()
        {
            //1099511628032000
            // 256000
            // 256000
            Location TestLocation1 = new Location(1099511628032000);
            Location TestLocation2 = new Location(1099511628032000);
            Assert.That(TestLocation1 == TestLocation2);

            TestLocation1 = new Location(1099511628032001);
            TestLocation2 = new Location(1099511628032000);
            Assert.That(TestLocation1 != TestLocation2);
        }

        [Test]
        public void locationXYRegionHandle()
        {
            Location TestLocation1 = new Location(255000,256000);
            Location TestLocation2 = new Location(1095216660736000);
            Assert.That(TestLocation1 == TestLocation2);

            Assert.That(TestLocation1.X == 255000 && TestLocation1.Y == 256000, "Test xy location doesn't match position in the constructor");
            Assert.That(TestLocation2.X == 255000 && TestLocation2.Y == 256000, "Test xy location doesn't match regionhandle provided");

            Assert.That(TestLocation2.RegionHandle == 1095216660736000,
                        "Location RegionHandle Property didn't match regionhandle provided in constructor");

            ulong RegionHandle = TestLocation1.RegionHandle;
            Assert.That(RegionHandle.Equals(1095216660736000), "Equals(regionhandle) failed to match the position in the constructor");

            TestLocation2 = new Location(RegionHandle);
            Assert.That(TestLocation2.Equals(255000, 256000), "Decoded regionhandle failed to match the original position in the constructor");


            TestLocation1 = new Location(255001, 256001);
            TestLocation2 = new Location(1095216660736000);
            Assert.That(TestLocation1 != TestLocation2);

            Assert.That(TestLocation1.Equals(255001, 256001), "Equals(x,y) failed to match the position in the constructor");

            Assert.That(TestLocation2.GetHashCode() == (TestLocation2.X.GetHashCode() ^ TestLocation2.Y.GetHashCode()), "GetHashCode failed to produce the expected hashcode");

            Location TestLocation3;
            object cln = TestLocation2.Clone();
            TestLocation3 = (Location) cln;
            Assert.That(TestLocation3.X == TestLocation2.X && TestLocation3.Y == TestLocation2.Y,
                        "Cloned Location values do not match");

            Assert.That(TestLocation2.Equals(cln), "Cloned object failed .Equals(obj) Test");

        }

    }
}
