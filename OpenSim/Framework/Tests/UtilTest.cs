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
 *     * Neither the name of the OpenSim Project nor the
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

using OpenMetaverse;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
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
    }
}
