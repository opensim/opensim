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
using OpenMetaverse;
using OpenSim.Tests.Common;

namespace OpenSim.Framework.Tests
{
    [TestFixture]
    public class UtilTests : OpenSimTestCase
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
/*
                TestDelegate d = delegate() { Util.GetNormalizedVector(v1); };
                bool causesArgumentException = TestHelpers.AssertThisDelegateCausesArgumentException(d);
                Assert.That(causesArgumentException, Is.True,
                            "Getting magnitude of null vector did not cause argument exception.");
*/
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
/*
                TestDelegate d = delegate() { Util.GetNormalizedVector(v1); };
                bool causesArgumentException = TestHelpers.AssertThisDelegateCausesArgumentException(d);
                Assert.That(causesArgumentException, Is.True,
                            "Getting magnitude of null vector did not cause argument exception.");

                d = delegate() { Util.GetNormalizedVector(v2); };
                causesArgumentException = TestHelpers.AssertThisDelegateCausesArgumentException(d);
                Assert.That(causesArgumentException, Is.True,
                            "Getting magnitude of null vector did not cause argument exception.");
*/
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
/*
                TestDelegate d = delegate() { Util.GetNormalizedVector(v1); };
                bool causesArgumentException = TestHelpers.AssertThisDelegateCausesArgumentException(d);
                Assert.That(causesArgumentException, Is.True,
                            "Getting magnitude of null vector did not cause argument exception.");
*/
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
                           "Too short UUIDs are recognized as correct UUIDs.");
            Assert.IsFalse(Util.isUUID("01234567-89ab-Cdef-0123-456789AbCdEf0"),
                           "Too long UUIDs are recognized as correct UUIDs.");
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

        [Test]
        public void SLUtilTypeConvertTests()
        {
            int[] assettypes = new int[]{-1,0,1,2,3,5,6,7,8,10,11,12,13,17,18,19,20,21,22
                                            ,24,25};
            string[] contenttypes = new string[]
                                        {
                                  "application/octet-stream",
                                  "image/x-j2c",
                                  "audio/ogg",
                                  "application/vnd.ll.callingcard",
                                  "application/vnd.ll.landmark",
                                  "application/vnd.ll.clothing",
                                  "application/vnd.ll.primitive",
                                  "application/vnd.ll.notecard",
                                  "application/vnd.ll.folder",
                                  "application/vnd.ll.lsltext",
                                  "application/vnd.ll.lslbyte",
                                  "image/tga",
                                  "application/vnd.ll.bodypart",
                                  "audio/x-wav",
                                  "image/tga",
                                  "image/jpeg",
                                  "application/vnd.ll.animation",
                                  "application/vnd.ll.gesture",
                                  "application/x-metaverse-simstate",
                                  "application/vnd.ll.link",
                                  "application/vnd.ll.linkfolder",
                    };
            for (int i=0;i<assettypes.Length;i++)
            {
                Assert.That(SLUtil.SLAssetTypeToContentType(assettypes[i]) == contenttypes[i], "Expecting {0} but got {1}", contenttypes[i], SLUtil.SLAssetTypeToContentType(assettypes[i]));
            }

            for (int i = 0; i < contenttypes.Length; i++)
            {
                int expected;
                if (contenttypes[i] == "image/tga")
                    expected = 12;  // if we know only the content-type "image/tga", then we assume the asset type is TextureTGA; not ImageTGA
                else
                    expected = assettypes[i];
                Assert.AreEqual(expected, SLUtil.ContentTypeToSLAssetType(contenttypes[i]),
                            String.Format("Incorrect AssetType mapped from Content-Type {0}", contenttypes[i]));
            }

            int[] inventorytypes = new int[] {-1,0,1,2,3,6,7,8,10,15,17,18,20};
            string[] invcontenttypes = new string[]
                                           {
                                               "application/octet-stream",
                                               "image/x-j2c",
                                               "audio/ogg",
                                               "application/vnd.ll.callingcard",
                                               "application/vnd.ll.landmark",
                                               "application/vnd.ll.primitive",
                                               "application/vnd.ll.notecard",
                                               "application/vnd.ll.rootfolder",
                                               "application/vnd.ll.lsltext",
                                               "image/x-j2c",
                                               "application/vnd.ll.primitive",
                                               "application/vnd.ll.clothing",
                                               "application/vnd.ll.gesture"
                                           };
        
            for (int i=0;i<inventorytypes.Length;i++)
            {
                Assert.AreEqual(invcontenttypes[i], SLUtil.SLInvTypeToContentType(inventorytypes[i]),
                    String.Format("Incorrect Content-Type mapped from InventoryType {0}", inventorytypes[i]));
            }

            invcontenttypes = new string[]
                                  {
                                      "image/x-j2c","image/jp2","image/tga",
                                      "image/jpeg","application/ogg","audio/ogg",
                                      "audio/x-wav","application/vnd.ll.callingcard",
                                      "application/x-metaverse-callingcard",
                                      "application/vnd.ll.landmark",
                                      "application/x-metaverse-landmark",
                                      "application/vnd.ll.clothing",
                                      "application/x-metaverse-clothing","application/vnd.ll.bodypart",
                                      "application/x-metaverse-bodypart","application/vnd.ll.primitive",
                                      "application/x-metaverse-primitive","application/vnd.ll.notecard",
                                      "application/x-metaverse-notecard","application/vnd.ll.folder",
                                      "application/vnd.ll.rootfolder","application/vnd.ll.lsltext",
                                      "application/x-metaverse-lsl","application/vnd.ll.lslbyte",
                                      "application/x-metaverse-lso","application/vnd.ll.trashfolder",
                                      "application/vnd.ll.snapshotfolder",
                                      "application/vnd.ll.lostandfoundfolder","application/vnd.ll.animation",
                                      "application/x-metaverse-animation","application/vnd.ll.gesture",
                                      "application/x-metaverse-gesture","application/x-metaverse-simstate",
                                      "application/octet-stream"
                                  };
            sbyte[] invtypes = new sbyte[]
                                   {
                                       0, 0, 0, 0, 1, 1, 1, 2, 2, 3, 3, 18, 18, 18, 18, 6, 6, 7, 7, -1, 8, 10, 10, 10, 10
                                       , 14, 15, 16, 19, 19, 20, 20, 15, -1
                                   };

            for (int i = 0; i < invtypes.Length; i++)
            {
                Assert.AreEqual(invtypes[i], SLUtil.ContentTypeToSLInvType(invcontenttypes[i]),
                    String.Format("Incorrect InventoryType mapped from Content-Type {0}", invcontenttypes[i]));
            }
        }

        [Test]
        public void FakeParcelIDTests()
        {
            byte[] hexBytes8 = { 0xfe, 0xdc, 0xba, 0x98, 0x76, 0x54, 0x32, 0x10 };
            byte[] hexBytes16 = {
                        0xf0, 0xe1, 0xd2, 0xc3, 0xb4, 0xa5, 0x96, 0x87,
                        0x77, 0x69, 0x5a, 0x4b, 0x3c, 0x2d, 0x1e, 0x0f };
            UInt64 var64Bit = (UInt64)0xfedcba9876543210;

            //Region handle is for location 255000,256000.
            ulong regionHandle1 = 1095216660736000;
            uint  x1 = 100;
            uint  y1 = 200;
            uint  z1 = 22;
            ulong regionHandle2;
            uint  x2, y2, z2;
            UUID fakeParcelID1, uuid;

            ulong bigInt64 = Util.BytesToUInt64Big(hexBytes8);
            Assert.AreEqual(var64Bit, bigInt64,
                    "BytesToUint64Bit conversion of 8 bytes to UInt64 failed.");

            //Test building and decoding using some typical input values
            fakeParcelID1 = Util.BuildFakeParcelID(regionHandle1, x1, y1);
            Util.ParseFakeParcelID(fakeParcelID1, out regionHandle2, out x2, out y2);
            Assert.AreEqual(regionHandle1, regionHandle2,
                    "region handle decoded from FakeParcelID wth X/Y failed.");
            Assert.AreEqual(x1, x2,
                    "X coordinate decoded from FakeParcelID wth X/Y failed.");
            Assert.AreEqual(y1, y2,
                    "Y coordinate decoded from FakeParcelID wth X/Y failed.");

            fakeParcelID1 = Util.BuildFakeParcelID(regionHandle1, x1, y1, z1);
            Util.ParseFakeParcelID(fakeParcelID1, out regionHandle2, out x2, out y2, out z2);
            Assert.AreEqual(regionHandle1, regionHandle2,
                    "region handle decoded from FakeParcelID with X/Y/Z failed.");
            Assert.AreEqual(x1, x2,
                    "X coordinate decoded from FakeParcelID with X/Y/Z failed.");
            Assert.AreEqual(y1, y2,
                    "Y coordinate decoded from FakeParcelID with X/Y/Z failed.");
            Assert.AreEqual(z1, z2,
                    "Z coordinate decoded from FakeParcelID with X/Y/Z failed.");

            //Do some more extreme tests to check the encoding and decoding
            x1 = 0x55aa;
            y1 = 0x9966;
            z1 = 0x5a96;

            fakeParcelID1 = Util.BuildFakeParcelID(var64Bit, x1, y1);
            Util.ParseFakeParcelID(fakeParcelID1, out regionHandle2, out x2, out y2);
            Assert.AreEqual(var64Bit, regionHandle2,
                    "region handle decoded from FakeParcelID with X/Y/Z failed.");
            Assert.AreEqual(x1, x2,
                    "X coordinate decoded from FakeParcelID with X/Y/Z failed.");
            Assert.AreEqual(y1, y2,
                    "Y coordinate decoded from FakeParcelID with X/Y/Z failed.");

            fakeParcelID1 = Util.BuildFakeParcelID(var64Bit, x1, y1, z1);
            Util.ParseFakeParcelID(fakeParcelID1, out regionHandle2, out x2, out y2, out z2);
            Assert.AreEqual(var64Bit, regionHandle2,
                    "region handle decoded from FakeParcelID with X/Y/Z failed.");
            Assert.AreEqual(x1, x2,
                    "X coordinate decoded from FakeParcelID with X/Y/Z failed.");
            Assert.AreEqual(y1, y2,
                    "Y coordinate decoded from FakeParcelID with X/Y/Z failed.");
            Assert.AreEqual(z1, z2,
                    "Z coordinate decoded from FakeParcelID with X/Y/Z failed.");


            x1 = 64;
            y1 = 192;
            fakeParcelID1 = Util.BuildFakeParcelID(regionHandle1, x1, y1);
            Util.FakeParcelIDToGlobalPosition(fakeParcelID1, out x2, out y2);
            Assert.AreEqual(255000+x1, x2,
                    "Global X coordinate decoded from regionHandle failed.");
            Assert.AreEqual(256000+y1, y2,
                    "Global Y coordinate decoded from regionHandle failed.");

            uuid = new UUID("00dd0700-00d1-0700-3800-000032000000");
            Util.FakeParcelIDToGlobalPosition(uuid, out x2, out y2);
        }
    }
}
