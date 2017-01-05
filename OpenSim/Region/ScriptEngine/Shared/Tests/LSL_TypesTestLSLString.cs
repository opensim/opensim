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
    public class LSL_TypesTestLSLString : OpenSimTestCase
    {
        private Dictionary<double, string> m_doubleStringSet;

        /// <summary>
        /// Sets up dictionaries and arrays used in the tests.
        /// </summary>
        [TestFixtureSetUp]
        public void SetUpDataSets()
        {
            m_doubleStringSet = new Dictionary<double, string>();
            m_doubleStringSet.Add(2, "2.000000");
            m_doubleStringSet.Add(-2, "-2.000000");
            m_doubleStringSet.Add(0, "0.000000");
            m_doubleStringSet.Add(1, "1.000000");
            m_doubleStringSet.Add(-1, "-1.000000");
            m_doubleStringSet.Add(999999999, "999999999.000000");
            m_doubleStringSet.Add(-99999999, "-99999999.000000");
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
        }

        /// <summary>
        /// Tests constructing a LSLString from an LSLFloat.
        /// </summary>
        [Test]
        public void TestConstructFromLSLFloat()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLString testString;

            foreach (KeyValuePair<double, string> number in m_doubleStringSet)
            {
                testString = new LSL_Types.LSLString(new LSL_Types.LSLFloat(number.Key));
                Assert.AreEqual(number.Value, testString.m_string);
            }
        }

        /// <summary>
        /// Tests constructing a LSLString from an LSLFloat.
        /// </summary>
        [Test]
        public void TestExplicitCastLSLFloatToLSLString()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLString testString;

            foreach (KeyValuePair<double, string> number in m_doubleStringSet)
            {
                testString = (LSL_Types.LSLString) new LSL_Types.LSLFloat(number.Key);
                Assert.AreEqual(number.Value, testString.m_string);
            }
        }

        /// <summary>
        /// Test constructing a Quaternion from a string.
        /// </summary>
        [Test]
        public void TestExplicitCastLSLStringToQuaternion()
        {
            TestHelpers.InMethod();

            string quaternionString = "<0.00000, 0.70711, 0.00000, 0.70711>";
            LSL_Types.LSLString quaternionLSLString = new LSL_Types.LSLString(quaternionString);

            LSL_Types.Quaternion expectedQuaternion = new LSL_Types.Quaternion(0.0, 0.70711, 0.0, 0.70711);
            LSL_Types.Quaternion stringQuaternion = (LSL_Types.Quaternion) quaternionString;
            LSL_Types.Quaternion LSLStringQuaternion = (LSL_Types.Quaternion) quaternionLSLString;

            Assert.AreEqual(expectedQuaternion, stringQuaternion);
            Assert.AreEqual(expectedQuaternion, LSLStringQuaternion);
        }

        /// <summary>
        /// Tests boolean correctly cast explicitly to LSLString.
        /// </summary>
        [Test]
        public void TestImplicitCastBooleanToLSLFloat()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLString testString;

            testString = (LSL_Types.LSLString) (1 == 0);
            Assert.AreEqual("0", testString.m_string);

            testString = (LSL_Types.LSLString) (1 == 1);
            Assert.AreEqual("1", testString.m_string);

            testString = (LSL_Types.LSLString) false;
            Assert.AreEqual("0", testString.m_string);

            testString = (LSL_Types.LSLString) true;
            Assert.AreEqual("1", testString.m_string);
        }
    }
}
