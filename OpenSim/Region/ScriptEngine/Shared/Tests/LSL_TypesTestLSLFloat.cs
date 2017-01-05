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

using System.Collections.Generic;
using NUnit.Framework;
using OpenSim.Tests.Common;
using OpenSim.Region.ScriptEngine.Shared;

namespace OpenSim.Region.ScriptEngine.Shared.Tests
{
    [TestFixture]
    public class LSL_TypesTestLSLFloat : OpenSimTestCase
    {
        // Used for testing equality of two floats.
        private double _lowPrecisionTolerance = 0.000001;

        private Dictionary<int, double> m_intDoubleSet;
        private Dictionary<double, double> m_doubleDoubleSet;
        private Dictionary<double, int> m_doubleIntSet;
        private Dictionary<double, int> m_doubleUintSet;
        private Dictionary<string, double> m_stringDoubleSet;
        private Dictionary<double, string> m_doubleStringSet;
        private List<int> m_intList;
        private List<double> m_doubleList;

        /// <summary>
        /// Sets up dictionaries and arrays used in the tests.
        /// </summary>
        [TestFixtureSetUp]
        public void SetUpDataSets()
        {
            m_intDoubleSet = new Dictionary<int, double>();
            m_intDoubleSet.Add(2, 2.0);
            m_intDoubleSet.Add(-2, -2.0);
            m_intDoubleSet.Add(0, 0.0);
            m_intDoubleSet.Add(1, 1.0);
            m_intDoubleSet.Add(-1, -1.0);
            m_intDoubleSet.Add(999999999, 999999999.0);
            m_intDoubleSet.Add(-99999999, -99999999.0);

            m_doubleDoubleSet = new Dictionary<double, double>();
            m_doubleDoubleSet.Add(2.0, 2.0);
            m_doubleDoubleSet.Add(-2.0, -2.0);
            m_doubleDoubleSet.Add(0.0, 0.0);
            m_doubleDoubleSet.Add(1.0, 1.0);
            m_doubleDoubleSet.Add(-1.0, -1.0);
            m_doubleDoubleSet.Add(999999999.0, 999999999.0);
            m_doubleDoubleSet.Add(-99999999.0, -99999999.0);
            m_doubleDoubleSet.Add(0.5, 0.5);
            m_doubleDoubleSet.Add(0.0005, 0.0005);
            m_doubleDoubleSet.Add(0.6805, 0.6805);
            m_doubleDoubleSet.Add(-0.5, -0.5);
            m_doubleDoubleSet.Add(-0.0005, -0.0005);
            m_doubleDoubleSet.Add(-0.6805, -0.6805);
            m_doubleDoubleSet.Add(548.5, 548.5);
            m_doubleDoubleSet.Add(2.0005, 2.0005);
            m_doubleDoubleSet.Add(349485435.6805, 349485435.6805);
            m_doubleDoubleSet.Add(-548.5, -548.5);
            m_doubleDoubleSet.Add(-2.0005, -2.0005);
            m_doubleDoubleSet.Add(-349485435.6805, -349485435.6805);

            m_doubleIntSet = new Dictionary<double, int>();
            m_doubleIntSet.Add(2.0, 2);
            m_doubleIntSet.Add(-2.0, -2);
            m_doubleIntSet.Add(0.0, 0);
            m_doubleIntSet.Add(1.0, 1);
            m_doubleIntSet.Add(-1.0, -1);
            m_doubleIntSet.Add(999999999.0, 999999999);
            m_doubleIntSet.Add(-99999999.0, -99999999);
            m_doubleIntSet.Add(0.5, 0);
            m_doubleIntSet.Add(0.0005, 0);
            m_doubleIntSet.Add(0.6805, 0);
            m_doubleIntSet.Add(-0.5, 0);
            m_doubleIntSet.Add(-0.0005, 0);
            m_doubleIntSet.Add(-0.6805, 0);
            m_doubleIntSet.Add(548.5, 548);
            m_doubleIntSet.Add(2.0005, 2);
            m_doubleIntSet.Add(349485435.6805, 349485435);
            m_doubleIntSet.Add(-548.5, -548);
            m_doubleIntSet.Add(-2.0005, -2);
            m_doubleIntSet.Add(-349485435.6805, -349485435);

            m_doubleUintSet = new Dictionary<double, int>();
            m_doubleUintSet.Add(2.0, 2);
            m_doubleUintSet.Add(-2.0, 2);
            m_doubleUintSet.Add(0.0, 0);
            m_doubleUintSet.Add(1.0, 1);
            m_doubleUintSet.Add(-1.0, 1);
            m_doubleUintSet.Add(999999999.0, 999999999);
            m_doubleUintSet.Add(-99999999.0, 99999999);
            m_doubleUintSet.Add(0.5, 0);
            m_doubleUintSet.Add(0.0005, 0);
            m_doubleUintSet.Add(0.6805, 0);
            m_doubleUintSet.Add(-0.5, 0);
            m_doubleUintSet.Add(-0.0005, 0);
            m_doubleUintSet.Add(-0.6805, 0);
            m_doubleUintSet.Add(548.5, 548);
            m_doubleUintSet.Add(2.0005, 2);
            m_doubleUintSet.Add(349485435.6805, 349485435);
            m_doubleUintSet.Add(-548.5, 548);
            m_doubleUintSet.Add(-2.0005, 2);
            m_doubleUintSet.Add(-349485435.6805, 349485435);

            m_stringDoubleSet = new Dictionary<string, double>();
            m_stringDoubleSet.Add("2", 2.0);
            m_stringDoubleSet.Add("-2", -2.0);
            m_stringDoubleSet.Add("1", 1.0);
            m_stringDoubleSet.Add("-1", -1.0);
            m_stringDoubleSet.Add("0", 0.0);
            m_stringDoubleSet.Add("999999999.0", 999999999.0);
            m_stringDoubleSet.Add("-99999999.0", -99999999.0);
            m_stringDoubleSet.Add("0.5", 0.5);
            m_stringDoubleSet.Add("0.0005", 0.0005);
            m_stringDoubleSet.Add("0.6805", 0.6805);
            m_stringDoubleSet.Add("-0.5", -0.5);
            m_stringDoubleSet.Add("-0.0005", -0.0005);
            m_stringDoubleSet.Add("-0.6805", -0.6805);
            m_stringDoubleSet.Add("548.5", 548.5);
            m_stringDoubleSet.Add("2.0005", 2.0005);
            m_stringDoubleSet.Add("349485435.6805", 349485435.6805);
            m_stringDoubleSet.Add("-548.5", -548.5);
            m_stringDoubleSet.Add("-2.0005", -2.0005);
            m_stringDoubleSet.Add("-349485435.6805", -349485435.6805);
            // some oddball combinations and exponents
            m_stringDoubleSet.Add("", 0.0);
            m_stringDoubleSet.Add("1.0E+5", 100000.0);
            m_stringDoubleSet.Add("-1.0E+5", -100000.0);
            m_stringDoubleSet.Add("-1E+5", -100000.0);
            m_stringDoubleSet.Add("-1.E+5", -100000.0);
            m_stringDoubleSet.Add("-1.E+5.0", -100000.0);
            m_stringDoubleSet.Add("1ef", 1.0);
            m_stringDoubleSet.Add("e10", 0.0);
            m_stringDoubleSet.Add("1.e0.0", 1.0);

            m_doubleStringSet = new Dictionary<double, string>();
            m_doubleStringSet.Add(2.0, "2.000000");
            m_doubleStringSet.Add(-2.0, "-2.000000");
            m_doubleStringSet.Add(1.0, "1.000000");
            m_doubleStringSet.Add(-1.0, "-1.000000");
            m_doubleStringSet.Add(0.0, "0.000000");
            m_doubleStringSet.Add(999999999.0, "999999999.000000");
            m_doubleStringSet.Add(-99999999.0, "-99999999.000000");
            m_doubleStringSet.Add(0.5, "0.500000");
            m_doubleStringSet.Add(0.0005, "0.000500");
            m_doubleStringSet.Add(0.6805, "0.680500");
            m_doubleStringSet.Add(-0.5, "-0.500000");
            m_doubleStringSet.Add(-0.0005, "-0.000500");
            m_doubleStringSet.Add(-0.6805, "-0.680500");
            m_doubleStringSet.Add(548.5, "548.500000");
            m_doubleStringSet.Add(2.0005, "2.000500");
            m_doubleStringSet.Add(349485435.6805, "349485435.680500");
            m_doubleStringSet.Add(-548.5, "-548.500000");
            m_doubleStringSet.Add(-2.0005, "-2.000500");
            m_doubleStringSet.Add(-349485435.6805, "-349485435.680500");

            m_doubleList = new List<double>();
            m_doubleList.Add(2.0);
            m_doubleList.Add(-2.0);
            m_doubleList.Add(1.0);
            m_doubleList.Add(-1.0);
            m_doubleList.Add(999999999.0);
            m_doubleList.Add(-99999999.0);
            m_doubleList.Add(0.5);
            m_doubleList.Add(0.0005);
            m_doubleList.Add(0.6805);
            m_doubleList.Add(-0.5);
            m_doubleList.Add(-0.0005);
            m_doubleList.Add(-0.6805);
            m_doubleList.Add(548.5);
            m_doubleList.Add(2.0005);
            m_doubleList.Add(349485435.6805);
            m_doubleList.Add(-548.5);
            m_doubleList.Add(-2.0005);
            m_doubleList.Add(-349485435.6805);

            m_intList = new List<int>();
            m_intList.Add(2);
            m_intList.Add(-2);
            m_intList.Add(0);
            m_intList.Add(1);
            m_intList.Add(-1);
            m_intList.Add(999999999);
            m_intList.Add(-99999999);
        }

        /// <summary>
        /// Tests constructing a LSLFloat from an integer.
        /// </summary>
        [Test]
        public void TestConstructFromInt()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testFloat;

            foreach (KeyValuePair<int, double> number in m_intDoubleSet)
            {
                testFloat = new LSL_Types.LSLFloat(number.Key);
                Assert.That(testFloat.value, new DoubleToleranceConstraint(number.Value, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests constructing a LSLFloat from a double.
        /// </summary>
        [Test]
        public void TestConstructFromDouble()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testFloat;

            foreach (KeyValuePair<double, double> number in m_doubleDoubleSet)
            {
                testFloat = new LSL_Types.LSLFloat(number.Key);
                Assert.That(testFloat.value, new DoubleToleranceConstraint(number.Value, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests LSLFloat is correctly cast explicitly to integer.
        /// </summary>
        [Test]
        public void TestExplicitCastLSLFloatToInt()
        {
            TestHelpers.InMethod();

            int testNumber;

            foreach (KeyValuePair<double, int> number in m_doubleIntSet)
            {
                testNumber = (int) new LSL_Types.LSLFloat(number.Key);
                Assert.AreEqual(number.Value, testNumber, "Converting double " + number.Key + ", expecting int " + number.Value);
            }
        }

        /// <summary>
        /// Tests LSLFloat is correctly cast explicitly to unsigned integer.
        /// </summary>
        [Test]
        public void TestExplicitCastLSLFloatToUint()
        {
            TestHelpers.InMethod();

            uint testNumber;

            foreach (KeyValuePair<double, int> number in m_doubleUintSet)
            {
                testNumber = (uint) new LSL_Types.LSLFloat(number.Key);
                Assert.AreEqual(number.Value, testNumber, "Converting double " + number.Key + ", expecting uint " + number.Value);
            }
        }

        /// <summary>
        /// Tests LSLFloat is correctly cast implicitly to Boolean if non-zero.
        /// </summary>
        [Test]
        public void TestImplicitCastLSLFloatToBooleanTrue()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testFloat;
            bool testBool;

            foreach (double number in m_doubleList)
            {
                testFloat = new LSL_Types.LSLFloat(number);
                testBool = testFloat;

                Assert.IsTrue(testBool);
            }
        }

        /// <summary>
        /// Tests LSLFloat is correctly cast implicitly to Boolean if zero.
        /// </summary>
        [Test]
        public void TestImplicitCastLSLFloatToBooleanFalse()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testFloat = new LSL_Types.LSLFloat(0.0);
            bool testBool = testFloat;

            Assert.IsFalse(testBool);
        }

        /// <summary>
        /// Tests integer is correctly cast implicitly to LSLFloat.
        /// </summary>
        [Test]
        public void TestImplicitCastIntToLSLFloat()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testFloat;

            foreach (int number in m_intList)
            {
                testFloat = number;
                Assert.That(testFloat.value, new DoubleToleranceConstraint(number, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests LSLInteger is correctly cast implicitly to LSLFloat.
        /// </summary>
        [Test]
        public void TestImplicitCastLSLIntegerToLSLFloat()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testFloat;

            foreach (int number in m_intList)
            {
                testFloat = new LSL_Types.LSLInteger(number);
                Assert.That(testFloat.value, new DoubleToleranceConstraint(number, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests LSLInteger is correctly cast explicitly to LSLFloat.
        /// </summary>
        [Test]
        public void TestExplicitCastLSLIntegerToLSLFloat()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testFloat;

            foreach (int number in m_intList)
            {
                testFloat = (LSL_Types.LSLFloat) new LSL_Types.LSLInteger(number);
                Assert.That(testFloat.value, new DoubleToleranceConstraint(number, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests string is correctly cast explicitly to LSLFloat.
        /// </summary>
        [Test]
        public void TestExplicitCastStringToLSLFloat()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testFloat;

            foreach (KeyValuePair<string, double> number in m_stringDoubleSet)
            {
                testFloat = (LSL_Types.LSLFloat) number.Key;
                Assert.That(testFloat.value, new DoubleToleranceConstraint(number.Value, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests LSLString is correctly cast implicitly to LSLFloat.
        /// </summary>
        [Test]
        public void TestExplicitCastLSLStringToLSLFloat()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testFloat;

            foreach (KeyValuePair<string, double> number in m_stringDoubleSet)
            {
                testFloat = (LSL_Types.LSLFloat) new LSL_Types.LSLString(number.Key);
                Assert.That(testFloat.value, new DoubleToleranceConstraint(number.Value, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests double is correctly cast implicitly to LSLFloat.
        /// </summary>
        [Test]
        public void TestImplicitCastDoubleToLSLFloat()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testFloat;

            foreach (double number in m_doubleList)
            {
                testFloat = number;
                Assert.That(testFloat.value, new DoubleToleranceConstraint(number, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests LSLFloat is correctly cast implicitly to double.
        /// </summary>
        [Test]
        public void TestImplicitCastLSLFloatToDouble()
        {
            TestHelpers.InMethod();

            double testNumber;
            LSL_Types.LSLFloat testFloat;

            foreach (double number in m_doubleList)
            {
                testFloat = new LSL_Types.LSLFloat(number);
                testNumber = testFloat;

                Assert.That(testNumber, new DoubleToleranceConstraint(number, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests LSLFloat is correctly cast explicitly to float
        /// </summary>
        [Test]
        public void TestExplicitCastLSLFloatToFloat()
        {
            TestHelpers.InMethod();

            float testFloat;
            float numberAsFloat;
            LSL_Types.LSLFloat testLSLFloat;

            foreach (double number in m_doubleList)
            {
                testLSLFloat = new LSL_Types.LSLFloat(number);
                numberAsFloat = (float)number;
                testFloat = (float)testLSLFloat;

                Assert.That((double)testFloat, new DoubleToleranceConstraint((double)numberAsFloat, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests the equality (==) operator.
        /// </summary>
        [Test]
        public void TestEqualsOperator()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testFloatA, testFloatB;

            foreach (double number in m_doubleList)
            {
                testFloatA = new LSL_Types.LSLFloat(number);
                testFloatB = new LSL_Types.LSLFloat(number);
                Assert.IsTrue(testFloatA == testFloatB);

                testFloatB = new LSL_Types.LSLFloat(number + 1.0);
                Assert.IsFalse(testFloatA == testFloatB);
            }
        }

        /// <summary>
        /// Tests the inequality (!=) operator.
        /// </summary>
        [Test]
        public void TestNotEqualOperator()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testFloatA, testFloatB;

            foreach (double number in m_doubleList)
            {
                testFloatA = new LSL_Types.LSLFloat(number);
                testFloatB = new LSL_Types.LSLFloat(number + 1.0);
                Assert.IsTrue(testFloatA != testFloatB);

                testFloatB = new LSL_Types.LSLFloat(number);
                Assert.IsFalse(testFloatA != testFloatB);
            }
        }

        /// <summary>
        /// Tests the increment operator.
        /// </summary>
        [Test]
        public void TestIncrementOperator()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testFloat;
            double testNumber;

            foreach (double number in m_doubleList)
            {
                testFloat = new LSL_Types.LSLFloat(number);

                testNumber = testFloat++;
                Assert.That(testNumber, new DoubleToleranceConstraint(number, _lowPrecisionTolerance));

                testNumber = testFloat;
                Assert.That(testNumber, new DoubleToleranceConstraint(number + 1.0, _lowPrecisionTolerance));

                testNumber = ++testFloat;
                Assert.That(testNumber, new DoubleToleranceConstraint(number + 2.0, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests the decrement operator.
        /// </summary>
        [Test]
        public void TestDecrementOperator()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testFloat;
            double testNumber;

            foreach (double number in m_doubleList)
            {
                testFloat = new LSL_Types.LSLFloat(number);

                testNumber = testFloat--;
                Assert.That(testNumber, new DoubleToleranceConstraint(number, _lowPrecisionTolerance));

                testNumber = testFloat;
                Assert.That(testNumber, new DoubleToleranceConstraint(number - 1.0, _lowPrecisionTolerance));

                testNumber = --testFloat;
                Assert.That(testNumber, new DoubleToleranceConstraint(number - 2.0, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests LSLFloat.ToString().
        /// </summary>
        [Test]
        public void TestToString()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testFloat;

            foreach (KeyValuePair<double, string> number in m_doubleStringSet)
            {
                testFloat = new LSL_Types.LSLFloat(number.Key);
                Assert.AreEqual(number.Value, testFloat.ToString());
            }
        }

        /// <summary>
        /// Tests addition of two LSLFloats.
        /// </summary>
        [Test]
        public void TestAddTwoLSLFloats()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testResult;

            foreach (KeyValuePair<double, double> number in m_doubleDoubleSet)
            {
                testResult = new LSL_Types.LSLFloat(number.Key) + new LSL_Types.LSLFloat(number.Value);
                Assert.That(testResult.value, new DoubleToleranceConstraint(number.Key + number.Value, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests subtraction of two LSLFloats.
        /// </summary>
        [Test]
        public void TestSubtractTwoLSLFloats()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testResult;

            foreach (KeyValuePair<double, double> number in m_doubleDoubleSet)
            {
                testResult = new LSL_Types.LSLFloat(number.Key) - new LSL_Types.LSLFloat(number.Value);
                Assert.That(testResult.value, new DoubleToleranceConstraint(number.Key - number.Value, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests multiplication of two LSLFloats.
        /// </summary>
        [Test]
        public void TestMultiplyTwoLSLFloats()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testResult;

            foreach (KeyValuePair<double, double> number in m_doubleDoubleSet)
            {
                testResult = new LSL_Types.LSLFloat(number.Key) * new LSL_Types.LSLFloat(number.Value);
                Assert.That(testResult.value, new DoubleToleranceConstraint(number.Key * number.Value, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests division of two LSLFloats.
        /// </summary>
        [Test]
        public void TestDivideTwoLSLFloats()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testResult;

            foreach (KeyValuePair<double, double> number in m_doubleDoubleSet)
            {
                if (number.Value != 0.0) // Let's avoid divide by zero.
                {
                    testResult = new LSL_Types.LSLFloat(number.Key) / new LSL_Types.LSLFloat(number.Value);
                    Assert.That(testResult.value, new DoubleToleranceConstraint(number.Key / number.Value, _lowPrecisionTolerance));
                }
            }
        }

        /// <summary>
        /// Tests boolean correctly cast implicitly to LSLFloat.
        /// </summary>
        [Test]
        public void TestImplicitCastBooleanToLSLFloat()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testFloat;

            testFloat = (1 == 0);
            Assert.That(testFloat.value, new DoubleToleranceConstraint(0.0, _lowPrecisionTolerance));

            testFloat = (1 == 1);
            Assert.That(testFloat.value, new DoubleToleranceConstraint(1.0, _lowPrecisionTolerance));

            testFloat = false;
            Assert.That(testFloat.value, new DoubleToleranceConstraint(0.0, _lowPrecisionTolerance));

            testFloat = true;
            Assert.That(testFloat.value, new DoubleToleranceConstraint(1.0, _lowPrecisionTolerance));
        }
    }
}