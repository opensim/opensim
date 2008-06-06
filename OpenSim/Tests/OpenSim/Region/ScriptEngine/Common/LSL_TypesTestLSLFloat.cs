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

using System.Collections.Generic;
using NUnit.Framework;
using OpenSim.Tests.Common;
using OpenSim.Region.ScriptEngine.Common;
using System;

namespace OpenSim.Region.ScriptEngine.Common.Tests
{
    [TestFixture]
    public class LSL_TypesTestLSLFloat
    {
        // Used for testing equality of two floats.
        private double _lowPrecisionTolerance = 0.000001;

        /// <summary>
        /// Tests constructing a LSLFloat from an integer.
        /// </summary>
        [Test]
        public void TestConstructFromInt()
        {
            // The numbers we test for.
            Dictionary<int, double> numberSet = new Dictionary<int, double>();
            numberSet.Add(2, 2.0);
            numberSet.Add(-2, -2.0);
            numberSet.Add(0, 0.0);
            numberSet.Add(1, 1.0);
            numberSet.Add(-1, -1.0);
            numberSet.Add(999999999, 999999999.0);
            numberSet.Add(-99999999, -99999999.0);

            LSL_Types.LSLFloat testFloat;

            foreach (KeyValuePair<int, double> number in numberSet)
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
            // The numbers we test for.
            Dictionary<double, double> numberSet = new Dictionary<double, double>();
            numberSet.Add(2.0, 2.0);
            numberSet.Add(-2.0, -2.0);
            numberSet.Add(0.0, 0.0);
            numberSet.Add(1.0, 1.0);
            numberSet.Add(-1.0, -1.0);
            numberSet.Add(999999999.0, 999999999.0);
            numberSet.Add(-99999999.0, -99999999.0);
            numberSet.Add(0.5, 0.5);
            numberSet.Add(0.0005, 0.0005);
            numberSet.Add(0.6805, 0.6805);
            numberSet.Add(-0.5, -0.5);
            numberSet.Add(-0.0005, -0.0005);
            numberSet.Add(-0.6805, -0.6805);
            numberSet.Add(548.5, 548.5);
            numberSet.Add(2.0005, 2.0005);
            numberSet.Add(349485435.6805, 349485435.6805);
            numberSet.Add(-548.5, -548.5);
            numberSet.Add(-2.0005, -2.0005);
            numberSet.Add(-349485435.6805, -349485435.6805);

            LSL_Types.LSLFloat testFloat;

            foreach (KeyValuePair<double, double> number in numberSet)
            {
                testFloat = new LSL_Types.LSLFloat(number.Key);
                Assert.That(testFloat.value, new DoubleToleranceConstraint(number.Value, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests LSLFloat is correctly cast implicitly to integer.
        /// </summary>
        [Test]
        public void TestImplicitCastLSLFloatToInt()
        {
            // The numbers we test for.
            Dictionary<double, int> numberSet = new Dictionary<double, int>();
            numberSet.Add(2.0, 2);
            numberSet.Add(-2.0, -2);
            numberSet.Add(0.0, 0);
            numberSet.Add(1.0, 1);
            numberSet.Add(-1.0, -1);
            numberSet.Add(999999999.0, 999999999);
            numberSet.Add(-99999999.0, -99999999);
            numberSet.Add(0.5, 0);
            numberSet.Add(0.0005, 0);
            numberSet.Add(0.6805, 0);
            numberSet.Add(-0.5, 0);
            numberSet.Add(-0.0005, 0);
            numberSet.Add(-0.6805, 0);
            numberSet.Add(548.5, 548);
            numberSet.Add(2.0005, 2);
            numberSet.Add(349485435.6805, 349485435);
            numberSet.Add(-548.5, -548);
            numberSet.Add(-2.0005, -2);
            numberSet.Add(-349485435.6805, -349485435);

            int testNumber;

            foreach (KeyValuePair<double, int> number in numberSet)
            {
                testNumber = new LSL_Types.LSLFloat(number.Key);
                Assert.AreEqual(number.Value, testNumber, "Converting double " + number.Key + ", expecting int " + number.Value);
            }
        }

        /// <summary>
        /// Tests LSLFloat is correctly cast implicitly to unsigned integer.
        /// </summary>
        [Test]
        public void TestImplicitCastLSLFloatToUint()
        {
            // The numbers we test for.
            Dictionary<double, int> numberSet = new Dictionary<double, int>();
            numberSet.Add(2.0, 2);
            numberSet.Add(-2.0, 2);
            numberSet.Add(0.0, 0);
            numberSet.Add(1.0, 1);
            numberSet.Add(-1.0, 1);
            numberSet.Add(999999999.0, 999999999);
            numberSet.Add(-99999999.0, 99999999);
            numberSet.Add(0.5, 0);
            numberSet.Add(0.0005, 0);
            numberSet.Add(0.6805, 0);
            numberSet.Add(-0.5, 0);
            numberSet.Add(-0.0005, 0);
            numberSet.Add(-0.6805, 0);
            numberSet.Add(548.5, 548);
            numberSet.Add(2.0005, 2);
            numberSet.Add(349485435.6805, 349485435);
            numberSet.Add(-548.5, 548);
            numberSet.Add(-2.0005, 2);
            numberSet.Add(-349485435.6805, 349485435);

            uint testNumber;

            foreach (KeyValuePair<double, int> number in numberSet)
            {
                testNumber = new LSL_Types.LSLFloat(number.Key);
                Assert.AreEqual(number.Value, testNumber, "Converting double " + number.Key + ", expecting uint " + number.Value);
            }
        }

        /// <summary>
        /// Tests LSLFloat is correctly cast implicitly to Boolean if non-zero.
        /// </summary>
        [Test]
        public void TestImplicitCastLSLFloatToBooleanTrue()
        {
            // A bunch of numbers to test with.
            List<double> numberList = new List<double>();
            numberList.Add(2.0);
            numberList.Add(-2.0);
            numberList.Add(1.0);
            numberList.Add(-1.0);
            numberList.Add(999999999.0);
            numberList.Add(-99999999.0);
            numberList.Add(0.5);
            numberList.Add(0.0005);
            numberList.Add(0.6805);
            numberList.Add(-0.5);
            numberList.Add(-0.0005);
            numberList.Add(-0.6805);
            numberList.Add(548.5);
            numberList.Add(2.0005);
            numberList.Add(349485435.6805);
            numberList.Add(-548.5);
            numberList.Add(-2.0005);
            numberList.Add(-349485435.6805);

            LSL_Types.LSLFloat testFloat;
            bool testBool;

            foreach (double number in numberList)
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
            // A bunch of numbers to test with.
            List<int> numberList = new List<int>();
            numberList.Add(2);
            numberList.Add(-2);
            numberList.Add(0);
            numberList.Add(1);
            numberList.Add(-1);
            numberList.Add(999999999);
            numberList.Add(-99999999);

            LSL_Types.LSLFloat testFloat;

            foreach (int number in numberList)
            {
                testFloat = number;
                Assert.That(testFloat.value, new DoubleToleranceConstraint(number, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests string is correctly cast implicitly to LSLFloat.
        /// </summary>
        [Test]
        public void TestImplicitCastStringToLSLFloat()
        {
            // A bunch of numbers to test with.
            Dictionary<string, double> numberSet = new Dictionary<string, double>();
            numberSet.Add("2", 2.0);
            numberSet.Add("-2", -2.0);
            numberSet.Add("1", 1.0);
            numberSet.Add("-1", -1.0);
            numberSet.Add("0", 0.0);
            numberSet.Add("999999999.0", 999999999.0);
            numberSet.Add("-99999999.0", -99999999.0);
            numberSet.Add("0.5", 0.5);
            numberSet.Add("0.0005", 0.0005);
            numberSet.Add("0.6805", 0.6805);
            numberSet.Add("-0.5", -0.5);
            numberSet.Add("-0.0005", -0.0005);
            numberSet.Add("-0.6805", -0.6805);
            numberSet.Add("548.5", 548.5);
            numberSet.Add("2.0005", 2.0005);
            numberSet.Add("349485435.6805", 349485435.6805);
            numberSet.Add("-548.5", -548.5);
            numberSet.Add("-2.0005", -2.0005);
            numberSet.Add("-349485435.6805", -349485435.6805);

            LSL_Types.LSLFloat testFloat;

            foreach (KeyValuePair<string, double> number in numberSet)
            {
                testFloat = number.Key;
                Assert.That(testFloat.value, new DoubleToleranceConstraint(number.Value, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests double is correctly cast implicitly to LSLFloat.
        /// </summary>
        [Test]
        public void TestImplicitCastDoubleToLSLFloat()
        {
            // A bunch of numbers to test with.
            List<double> numberList = new List<double>();
            numberList.Add(2.0);
            numberList.Add(-2.0);
            numberList.Add(1.0);
            numberList.Add(-1.0);
            numberList.Add(0.0);
            numberList.Add(999999999.0);
            numberList.Add(-99999999.0);
            numberList.Add(0.5);
            numberList.Add(0.0005);
            numberList.Add(0.6805);
            numberList.Add(-0.5);
            numberList.Add(-0.0005);
            numberList.Add(-0.6805);
            numberList.Add(548.5);
            numberList.Add(2.0005);
            numberList.Add(349485435.6805);
            numberList.Add(-548.5);
            numberList.Add(-2.0005);
            numberList.Add(-349485435.6805);

            LSL_Types.LSLFloat testFloat;

            foreach (double number in numberList)
            {
                testFloat = number;
                Assert.That(testFloat.value, new DoubleToleranceConstraint(number, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests the equality (==) operator.
        /// </summary>
        [Test]
        public void TestEqualsOperator()
        {
            // A bunch of numbers to test with.
            List<double> numberList = new List<double>();
            numberList.Add(2.0);
            numberList.Add(-2.0);
            numberList.Add(1.0);
            numberList.Add(-1.0);
            numberList.Add(0.0);
            numberList.Add(999999999.0);
            numberList.Add(-99999999.0);
            numberList.Add(0.5);
            numberList.Add(0.0005);
            numberList.Add(0.6805);
            numberList.Add(-0.5);
            numberList.Add(-0.0005);
            numberList.Add(-0.6805);
            numberList.Add(548.5);
            numberList.Add(2.0005);
            numberList.Add(349485435.6805);
            numberList.Add(-548.5);
            numberList.Add(-2.0005);
            numberList.Add(-349485435.6805);

            LSL_Types.LSLFloat testFloatA, testFloatB;

            foreach (double number in numberList)
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
            // A bunch of numbers to test with.
            List<double> numberList = new List<double>();
            numberList.Add(2.0);
            numberList.Add(-2.0);
            numberList.Add(1.0);
            numberList.Add(-1.0);
            numberList.Add(0.0);
            numberList.Add(999999999.0);
            numberList.Add(-99999999.0);
            numberList.Add(0.5);
            numberList.Add(0.0005);
            numberList.Add(0.6805);
            numberList.Add(-0.5);
            numberList.Add(-0.0005);
            numberList.Add(-0.6805);
            numberList.Add(548.5);
            numberList.Add(2.0005);
            numberList.Add(349485435.6805);
            numberList.Add(-548.5);
            numberList.Add(-2.0005);
            numberList.Add(-349485435.6805);

            LSL_Types.LSLFloat testFloatA, testFloatB;

            foreach (double number in numberList)
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
            // A bunch of numbers to test with.
            List<double> numberList = new List<double>();
            numberList.Add(2.0);
            numberList.Add(-2.0);
            numberList.Add(1.0);
            numberList.Add(-1.0);
            numberList.Add(0.0);
            numberList.Add(999999999.0);
            numberList.Add(-99999999.0);
            numberList.Add(0.5);
            numberList.Add(0.0005);
            numberList.Add(0.6805);
            numberList.Add(-0.5);
            numberList.Add(-0.0005);
            numberList.Add(-0.6805);
            numberList.Add(548.5);
            numberList.Add(2.0005);
            numberList.Add(349485435.6805);
            numberList.Add(-548.5);
            numberList.Add(-2.0005);
            numberList.Add(-349485435.6805);

            LSL_Types.LSLFloat testFloat;
            double testNumber;

            foreach (double number in numberList)
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
            // A bunch of numbers to test with.
            List<double> numberList = new List<double>();
            numberList.Add(2.0);
            numberList.Add(-2.0);
            numberList.Add(1.0);
            numberList.Add(-1.0);
            numberList.Add(0.0);
            numberList.Add(999999999.0);
            numberList.Add(-99999999.0);
            numberList.Add(0.5);
            numberList.Add(0.0005);
            numberList.Add(0.6805);
            numberList.Add(-0.5);
            numberList.Add(-0.0005);
            numberList.Add(-0.6805);
            numberList.Add(548.5);
            numberList.Add(2.0005);
            numberList.Add(349485435.6805);
            numberList.Add(-548.5);
            numberList.Add(-2.0005);
            numberList.Add(-349485435.6805);

            LSL_Types.LSLFloat testFloat;
            double testNumber;

            foreach (double number in numberList)
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
        /// Tests LSLFloat is correctly cast implicitly to double.
        /// </summary>
        [Test]
        public void TestImplicitCastLSLFloatToDouble()
        {
            // A bunch of numbers to test with.
            List<double> numberList = new List<double>();
            numberList.Add(2.0);
            numberList.Add(-2.0);
            numberList.Add(1.0);
            numberList.Add(-1.0);
            numberList.Add(0.0);
            numberList.Add(999999999.0);
            numberList.Add(-99999999.0);
            numberList.Add(0.5);
            numberList.Add(0.0005);
            numberList.Add(0.6805);
            numberList.Add(-0.5);
            numberList.Add(-0.0005);
            numberList.Add(-0.6805);
            numberList.Add(548.5);
            numberList.Add(2.0005);
            numberList.Add(349485435.6805);
            numberList.Add(-548.5);
            numberList.Add(-2.0005);
            numberList.Add(-349485435.6805);

            double testNumber;
            LSL_Types.LSLFloat testFloat;

            foreach (double number in numberList)
            {
                testFloat = new LSL_Types.LSLFloat(number);
                testNumber = testFloat;

                Assert.That(testNumber, new DoubleToleranceConstraint(number, _lowPrecisionTolerance));
            }
        }

        /// <summary>
        /// Tests LSLFloat.ToString().
        /// </summary>
        [Test]
        public void TestToString()
        {
            // A bunch of numbers to test with.
            Dictionary<double, string> numberSet = new Dictionary<double, string>();
            numberSet.Add(2.0, "2.000000");
            numberSet.Add(-2.0, "-2.000000");
            numberSet.Add(1.0, "1.000000");
            numberSet.Add(-1.0, "-1.000000");
            numberSet.Add(0.0, "0.000000");
            numberSet.Add(999999999.0, "999999999.000000");
            numberSet.Add(-99999999.0, "-99999999.000000");
            numberSet.Add(0.5, "0.500000");
            numberSet.Add(0.0005, "0.000500");
            numberSet.Add(0.6805, "0.680500");
            numberSet.Add(-0.5, "-0.500000");
            numberSet.Add(-0.0005, "-0.000500");
            numberSet.Add(-0.6805, "-0.680500");
            numberSet.Add(548.5, "548.500000");
            numberSet.Add(2.0005, "2.000500");
            numberSet.Add(349485435.6805, "349485435.680500");
            numberSet.Add(-548.5, "-548.500000");
            numberSet.Add(-2.0005, "-2.000500");
            numberSet.Add(-349485435.6805, "-349485435.680500");

            LSL_Types.LSLFloat testFloat;

            foreach (KeyValuePair<double, string> number in numberSet)
            {
                testFloat = new LSL_Types.LSLFloat(number.Key);
                Assert.AreEqual(number.Value, testFloat.ToString());
            }
        }
    }
}
