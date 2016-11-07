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
    /// <summary>
    /// Tests the LSL_Types.list class.
    /// </summary>
    [TestFixture]
    public class LSL_TypesTestList : OpenSimTestCase
    {
        /// <summary>
        /// Tests concatenating a string to a list.
        /// </summary>
        [Test]
        public void TestConcatenateString()
        {
            TestHelpers.InMethod();

            LSL_Types.list testList = new LSL_Types.list(new LSL_Types.LSLInteger(1), new LSL_Types.LSLInteger('a'), new LSL_Types.LSLString("test"));
            testList += new LSL_Types.LSLString("addition");

            Assert.AreEqual(4, testList.Length);
            Assert.AreEqual(new LSL_Types.LSLString("addition"), testList.Data[3]);
            Assert.AreEqual(typeof(LSL_Types.LSLString), testList.Data[3].GetType());

            LSL_Types.list secondTestList = testList + new LSL_Types.LSLString("more");

            Assert.AreEqual(5, secondTestList.Length);
            Assert.AreEqual(new LSL_Types.LSLString("more"), secondTestList.Data[4]);
            Assert.AreEqual(typeof(LSL_Types.LSLString), secondTestList.Data[4].GetType());
        }

        /// <summary>
        /// Tests concatenating an integer to a list.
        /// </summary>
        [Test]
        public void TestConcatenateInteger()
        {
            TestHelpers.InMethod();

            LSL_Types.list testList = new LSL_Types.list(new LSL_Types.LSLInteger(1), new LSL_Types.LSLInteger('a'), new LSL_Types.LSLString("test"));
            testList += new LSL_Types.LSLInteger(20);

            Assert.AreEqual(4, testList.Length);
            Assert.AreEqual(new LSL_Types.LSLInteger(20), testList.Data[3]);
            Assert.AreEqual(typeof(LSL_Types.LSLInteger), testList.Data[3].GetType());

            LSL_Types.list secondTestList = testList + new LSL_Types.LSLInteger(2);

            Assert.AreEqual(5, secondTestList.Length);
            Assert.AreEqual(new LSL_Types.LSLInteger(2), secondTestList.Data[4]);
            Assert.AreEqual(typeof(LSL_Types.LSLInteger), secondTestList.Data[4].GetType());
        }

        /// <summary>
        /// Tests concatenating a float to a list.
        /// </summary>
        [Test]
        public void TestConcatenateDouble()
        {
            TestHelpers.InMethod();

            LSL_Types.list testList = new LSL_Types.list(new LSL_Types.LSLInteger(1), new LSL_Types.LSLInteger('a'), new LSL_Types.LSLString("test"));
            testList += new LSL_Types.LSLFloat(2.0f);

            Assert.AreEqual(4, testList.Length);
            Assert.AreEqual(new LSL_Types.LSLFloat(2.0f), testList.Data[3]);
            Assert.AreEqual(typeof(LSL_Types.LSLFloat), testList.Data[3].GetType());

            LSL_Types.list secondTestList = testList + new LSL_Types.LSLFloat(0.04f);

            Assert.AreEqual(5, secondTestList.Length);
            Assert.AreEqual(new LSL_Types.LSLFloat(0.04f), secondTestList.Data[4]);
            Assert.AreEqual(typeof(LSL_Types.LSLFloat), secondTestList.Data[4].GetType());
        }

        /// <summary>
        /// Tests casting LSLInteger item to LSLInteger.
        /// </summary>
        [Test]
        public void TestCastLSLIntegerItemToLSLInteger()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLInteger testValue = new LSL_Types.LSLInteger(123);
            LSL_Types.list testList = new LSL_Types.list(testValue);

            Assert.AreEqual(testValue, (LSL_Types.LSLInteger)testList.Data[0]);
        }

        /// <summary>
        /// Tests casting LSLFloat item to LSLFloat.
        /// </summary>
        [Test]
        public void TestCastLSLFloatItemToLSLFloat()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testValue = new LSL_Types.LSLFloat(123.45678987);
            LSL_Types.list testList = new LSL_Types.list(testValue);

            Assert.AreEqual(testValue, (LSL_Types.LSLFloat)testList.Data[0]);
        }

        /// <summary>
        /// Tests casting LSLString item to LSLString.
        /// </summary>
        [Test]
        public void TestCastLSLStringItemToLSLString()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLString testValue = new LSL_Types.LSLString("hello there");
            LSL_Types.list testList = new LSL_Types.list(testValue);

            Assert.AreEqual(testValue, (LSL_Types.LSLString)testList.Data[0]);
        }

        /// <summary>
        /// Tests casting Vector3 item to Vector3.
        /// </summary>
        [Test]
        public void TestCastVector3ItemToVector3()
        {
            TestHelpers.InMethod();

            LSL_Types.Vector3 testValue = new LSL_Types.Vector3(12.34, 56.987654, 0.00987);
            LSL_Types.list testList = new LSL_Types.list(testValue);

            Assert.AreEqual(testValue, (LSL_Types.Vector3)testList.Data[0]);
        }
        /// <summary>
        /// Tests casting Quaternion item to Quaternion.
        /// </summary>
        [Test]
        public void TestCastQuaternionItemToQuaternion()
        {
            TestHelpers.InMethod();

            LSL_Types.Quaternion testValue = new LSL_Types.Quaternion(12.34, 56.44323, 765.983421, 0.00987);
            LSL_Types.list testList = new LSL_Types.list(testValue);

            Assert.AreEqual(testValue, (LSL_Types.Quaternion)testList.Data[0]);
        }

//====================================================================================

        /// <summary>
        /// Tests GetLSLIntegerItem for LSLInteger item.
        /// </summary>
        [Test]
        public void TestGetLSLIntegerItemForLSLIntegerItem()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLInteger testValue = new LSL_Types.LSLInteger(999911);
            LSL_Types.list testList = new LSL_Types.list(testValue);

            Assert.AreEqual(testValue, testList.GetLSLIntegerItem(0));
        }

        /// <summary>
        /// Tests GetLSLFloatItem for LSLFloat item.
        /// </summary>
        [Test]
        public void TestGetLSLFloatItemForLSLFloatItem()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLFloat testValue = new LSL_Types.LSLFloat(321.45687876);
            LSL_Types.list testList = new LSL_Types.list(testValue);

            Assert.AreEqual(testValue, testList.GetLSLFloatItem(0));
        }

        /// <summary>
        /// Tests GetLSLFloatItem for LSLInteger item.
        /// </summary>
        [Test]
        public void TestGetLSLFloatItemForLSLIntegerItem()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLInteger testValue = new LSL_Types.LSLInteger(3060987);
            LSL_Types.LSLFloat testFloatValue = new LSL_Types.LSLFloat(testValue);
            LSL_Types.list testList = new LSL_Types.list(testValue);

            Assert.AreEqual(testFloatValue, testList.GetLSLFloatItem(0));
        }

        /// <summary>
        /// Tests GetLSLStringItem for LSLString item.
        /// </summary>
        [Test]
        public void TestGetLSLStringItemForLSLStringItem()
        {
            TestHelpers.InMethod();

            LSL_Types.LSLString testValue = new LSL_Types.LSLString("hello all");
            LSL_Types.list testList = new LSL_Types.list(testValue);

            Assert.AreEqual(testValue, testList.GetLSLStringItem(0));
        }

        /// <summary>
        /// Tests GetLSLStringItem for key item.
        /// </summary>
        [Test]
        public void TestGetLSLStringItemForKeyItem()
        {
            TestHelpers.InMethod();

            LSL_Types.key testValue
                = new LSL_Types.key("98000000-0000-2222-3333-100000001000");
            LSL_Types.LSLString testStringValue = new LSL_Types.LSLString(testValue);
            LSL_Types.list testList = new LSL_Types.list(testValue);

            Assert.AreEqual(testStringValue, testList.GetLSLStringItem(0));
        }

        /// <summary>
        /// Tests GetVector3Item for Vector3 item.
        /// </summary>
        [Test]
        public void TestGetVector3ItemForVector3Item()
        {
            TestHelpers.InMethod();

            LSL_Types.Vector3 testValue = new LSL_Types.Vector3(92.34, 58.98754, -0.10987);
            LSL_Types.list testList = new LSL_Types.list(testValue);

            Assert.AreEqual(testValue, testList.GetVector3Item(0));
        }
        /// <summary>
        /// Tests GetQuaternionItem for Quaternion item.
        /// </summary>
        [Test]
        public void TestGetQuaternionItemForQuaternionItem()
        {
            TestHelpers.InMethod();

            LSL_Types.Quaternion testValue = new LSL_Types.Quaternion(12.64, 59.43723, 765.3421, 4.00987);
            // make that nonsense a quaternion
            testValue.Normalize();
            LSL_Types.list testList = new LSL_Types.list(testValue);

            Assert.AreEqual(testValue, testList.GetQuaternionItem(0));
        }

        /// <summary>
        /// Tests GetKeyItem for key item.
        /// </summary>
        [Test]
        public void TestGetKeyItemForKeyItem()
        {
            TestHelpers.InMethod();

            LSL_Types.key testValue
                = new LSL_Types.key("00000000-0000-2222-3333-100000001012");
            LSL_Types.list testList = new LSL_Types.list(testValue);

            Assert.AreEqual(testValue, testList.GetKeyItem(0));
        }
    }
}