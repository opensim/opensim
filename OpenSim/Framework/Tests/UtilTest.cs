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

using System;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Tests.Common;

namespace OpenSim.Framework.Tests
{
    [TestFixture]
    public class UtilTests
    {
        [Test]
        public void VectorOperationTests()
        {
            Vector3 v1, v2;
            double expectedDistance;
            double expectedMagnitude;
            double lowPrecisionTolerance = 0.001;

            //Lets test a simple case of <0,0,0> and <5,5,5>
            {
                v1 = new Vector3(0, 0, 0);
                v2 = new Vector3(5, 5, 5);
                expectedDistance = 8.66;
                Assert.That(Util.GetDistanceTo(v1, v2),
                            new DoubleToleranceConstraint(expectedDistance, lowPrecisionTolerance),
                            "Calculated distance between two vectors was not within tolerances.");

                expectedMagnitude = 0;
                Assert.That(Util.GetMagnitude(v1), Is.EqualTo(0), "Magnitude of null vector was not zero.");

                expectedMagnitude = 8.66;
                Assert.That(Util.GetMagnitude(v2),
                            new DoubleToleranceConstraint(expectedMagnitude, lowPrecisionTolerance),
                            "Magnitude of vector was incorrect.");

                TestDelegate d = delegate() { Util.GetNormalizedVector(v1); };
                bool causesArgumentException = TestHelper.AssertThisDelegateCausesArgumentException(d);
                Assert.That(causesArgumentException, Is.True,
                            "Getting magnitude of null vector did not cause argument exception.");

                Vector3 expectedNormalizedVector = new Vector3(.577f, .577f, .577f);
                double expectedNormalizedMagnitude = 1;
                Vector3 normalizedVector = Util.GetNormalizedVector(v2);
                Assert.That(normalizedVector,
                            new VectorToleranceConstraint(expectedNormalizedVector, lowPrecisionTolerance),
                            "Normalized vector generated from vector was not what was expected.");
                Assert.That(Util.GetMagnitude(normalizedVector),
                            new DoubleToleranceConstraint(expectedNormalizedMagnitude, lowPrecisionTolerance),
                            "Normalized vector generated from vector does not have magnitude of 1.");
            }

            //Lets test a simple case of <0,0,0> and <0,0,0>
            {
                v1 = new Vector3(0, 0, 0);
                v2 = new Vector3(0, 0, 0);
                expectedDistance = 0;
                Assert.That(Util.GetDistanceTo(v1, v2),
                            new DoubleToleranceConstraint(expectedDistance, lowPrecisionTolerance),
                            "Calculated distance between two vectors was not within tolerances.");

                expectedMagnitude = 0;
                Assert.That(Util.GetMagnitude(v1), Is.EqualTo(0), "Magnitude of null vector was not zero.");

                expectedMagnitude = 0;
                Assert.That(Util.GetMagnitude(v2),
                            new DoubleToleranceConstraint(expectedMagnitude, lowPrecisionTolerance),
                            "Magnitude of vector was incorrect.");

                TestDelegate d = delegate() { Util.GetNormalizedVector(v1); };
                bool causesArgumentException = TestHelper.AssertThisDelegateCausesArgumentException(d);
                Assert.That(causesArgumentException, Is.True,
                            "Getting magnitude of null vector did not cause argument exception.");

                d = delegate() { Util.GetNormalizedVector(v2); };
                causesArgumentException = TestHelper.AssertThisDelegateCausesArgumentException(d);
                Assert.That(causesArgumentException, Is.True,
                            "Getting magnitude of null vector did not cause argument exception.");
            }

            //Lets test a simple case of <0,0,0> and <-5,-5,-5>
            {
                v1 = new Vector3(0, 0, 0);
                v2 = new Vector3(-5, -5, -5);
                expectedDistance = 8.66;
                Assert.That(Util.GetDistanceTo(v1, v2),
                            new DoubleToleranceConstraint(expectedDistance, lowPrecisionTolerance),
                            "Calculated distance between two vectors was not within tolerances.");

                expectedMagnitude = 0;
                Assert.That(Util.GetMagnitude(v1), Is.EqualTo(0), "Magnitude of null vector was not zero.");

                expectedMagnitude = 8.66;
                Assert.That(Util.GetMagnitude(v2),
                            new DoubleToleranceConstraint(expectedMagnitude, lowPrecisionTolerance),
                            "Magnitude of vector was incorrect.");

                TestDelegate d = delegate() { Util.GetNormalizedVector(v1); };
                bool causesArgumentException = TestHelper.AssertThisDelegateCausesArgumentException(d);
                Assert.That(causesArgumentException, Is.True,
                            "Getting magnitude of null vector did not cause argument exception.");

                Vector3 expectedNormalizedVector = new Vector3(-.577f, -.577f, -.577f);
                double expectedNormalizedMagnitude = 1;
                Vector3 normalizedVector = Util.GetNormalizedVector(v2);
                Assert.That(normalizedVector,
                            new VectorToleranceConstraint(expectedNormalizedVector, lowPrecisionTolerance),
                            "Normalized vector generated from vector was not what was expected.");
                Assert.That(Util.GetMagnitude(normalizedVector),
                            new DoubleToleranceConstraint(expectedNormalizedMagnitude, lowPrecisionTolerance),
                            "Normalized vector generated from vector does not have magnitude of 1.");
            }
        }

        [Test]
        public void UUIDTests()
        {
            Assert.IsTrue(Util.isUUID("01234567-89ab-Cdef-0123-456789AbCdEf"),
                          "A correct UUID wasn't recognized.");
            Assert.IsFalse(Util.isUUID("FOOBAR67-89ab-Cdef-0123-456789AbCdEf"),
                           "UUIDs with non-hex characters are recognized as correct UUIDs.");
            Assert.IsFalse(Util.isUUID("01234567"),
                           "Too short UUIDs are regognized as correct UUIDs.");
            Assert.IsFalse(Util.isUUID("01234567-89ab-Cdef-0123-456789AbCdEf0"),
                           "Too long UUIDs are regognized as correct UUIDs.");
            Assert.IsFalse(Util.isUUID("01234567-89ab-Cdef-0123+456789AbCdEf"),
                          "UUIDs with wrong format are recognized as correct UUIDs.");
        }

        [Test]
        public void GetHashGuidTests()
        {
            string string1 = "This is one string";
            string string2 = "This is another";

            // Two consecutive runs should equal the same
            Assert.AreEqual(Util.GetHashGuid(string1, "secret1"), Util.GetHashGuid(string1, "secret1"));
            Assert.AreEqual(Util.GetHashGuid(string2, "secret1"), Util.GetHashGuid(string2, "secret1"));

            // Varying data should not eqal the same
            Assert.AreNotEqual(Util.GetHashGuid(string1, "secret1"), Util.GetHashGuid(string2, "secret1"));

            // Varying secrets should not eqal the same
            Assert.AreNotEqual(Util.GetHashGuid(string1, "secret1"), Util.GetHashGuid(string1, "secret2"));
        }
    }
}
