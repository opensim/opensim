using libsecondlife;
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
            LLVector3 v1, v2;
            double expectedDistance;
            double expectedMagnitude;
            double lowPrecisionTolerance = 0.001;

            //Lets test a simple case of <0,0,0> and <5,5,5>
            {
                v1 = new LLVector3(0, 0, 0);
                v2 = new LLVector3(5, 5, 5);
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

                LLVector3 expectedNormalizedVector = new LLVector3(.577f, .577f, .577f);
                double expectedNormalizedMagnitude = 1;
                LLVector3 normalizedVector = Util.GetNormalizedVector(v2);
                Assert.That(normalizedVector,
                            new VectorToleranceConstraint(expectedNormalizedVector, lowPrecisionTolerance),
                            "Normalized vector generated from vector was not what was expected.");
                Assert.That(Util.GetMagnitude(normalizedVector),
                            new DoubleToleranceConstraint(expectedNormalizedMagnitude, lowPrecisionTolerance),
                            "Normalized vector generated from vector does not have magnitude of 1.");
            }

            //Lets test a simple case of <0,0,0> and <0,0,0>
            {
                v1 = new LLVector3(0, 0, 0);
                v2 = new LLVector3(0, 0, 0);
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
                v1 = new LLVector3(0, 0, 0);
                v2 = new LLVector3(-5, -5, -5);
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

                LLVector3 expectedNormalizedVector = new LLVector3(-.577f, -.577f, -.577f);
                double expectedNormalizedMagnitude = 1;
                LLVector3 normalizedVector = Util.GetNormalizedVector(v2);
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
