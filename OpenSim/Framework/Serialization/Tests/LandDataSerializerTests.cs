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
using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Framework.Serialization.External;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using NUnit.Framework;

namespace OpenSim.Framework.Serialization.Tests
{
    [TestFixture]
    public class LandDataSerializerTest
    {
        private LandData land;
        private LandData landWithParcelAccessList;

        private static string preSerialized = "<?xml version=\"1.0\" encoding=\"utf-16\"?>\n<LandData>\n  <Area>128</Area>\n  <AuctionID>0</AuctionID>\n  <AuthBuyerID>00000000-0000-0000-0000-000000000000</AuthBuyerID>\n  <Category>10</Category>\n  <ClaimDate>0</ClaimDate>\n  <ClaimPrice>0</ClaimPrice>\n  <GlobalID>54ff9641-dd40-4a2c-b1f1-47dd3af24e50</GlobalID>\n  <GroupID>d740204e-bbbf-44aa-949d-02c7d739f6a5</GroupID>\n  <IsGroupOwned>False</IsGroupOwned>\n  <Bitmap>AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=</Bitmap>\n  <Description>land data to test LandDataSerializer</Description>\n  <Flags>536870944</Flags>\n  <LandingType>2</LandingType>\n  <Name>LandDataSerializerTest Land</Name>\n  <Status>0</Status>\n  <LocalID>0</LocalID>\n  <MediaAutoScale>1</MediaAutoScale>\n  <MediaID>d4452578-2f25-4b97-a81b-819af559cfd7</MediaID>\n  <MediaURL>http://videos.opensimulator.org/bumblebee.mp4</MediaURL>\n  <MusicURL />\n  <OwnerID>1b8eedf9-6d15-448b-8015-24286f1756bf</OwnerID>\n  <ParcelAccessList />\n  <PassHours>0</PassHours>\n  <PassPrice>0</PassPrice>\n  <SalePrice>0</SalePrice>\n  <SnapshotID>00000000-0000-0000-0000-000000000000</SnapshotID>\n  <UserLocation>&lt;0, 0, 0&gt;</UserLocation>\n  <UserLookAt>&lt;0, 0, 0&gt;</UserLookAt>\n  <Dwell>0</Dwell>\n  <OtherCleanTime>0</OtherCleanTime>\n</LandData>";
        private static string preSerializedWithParcelAccessList = "<?xml version=\"1.0\" encoding=\"utf-16\"?>\n<LandData>\n  <Area>128</Area>\n  <AuctionID>0</AuctionID>\n  <AuthBuyerID>00000000-0000-0000-0000-000000000000</AuthBuyerID>\n  <Category>10</Category>\n  <ClaimDate>0</ClaimDate>\n  <ClaimPrice>0</ClaimPrice>\n  <GlobalID>54ff9641-dd40-4a2c-b1f1-47dd3af24e50</GlobalID>\n  <GroupID>d740204e-bbbf-44aa-949d-02c7d739f6a5</GroupID>\n  <IsGroupOwned>False</IsGroupOwned>\n  <Bitmap>AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=</Bitmap>\n  <Description>land data to test LandDataSerializer</Description>\n  <Flags>536870944</Flags>\n  <LandingType>2</LandingType>\n  <Name>LandDataSerializerTest Land</Name>\n  <Status>0</Status>\n  <LocalID>0</LocalID>\n  <MediaAutoScale>1</MediaAutoScale>\n  <MediaID>d4452578-2f25-4b97-a81b-819af559cfd7</MediaID>\n  <MediaURL>http://videos.opensimulator.org/bumblebee.mp4</MediaURL>\n  <MusicURL />\n  <OwnerID>1b8eedf9-6d15-448b-8015-24286f1756bf</OwnerID>\n  <ParcelAccessList>\n    <ParcelAccessEntry>\n      <AgentID>62d65d45-c91a-4f77-862c-46557d978b6c</AgentID>\n      <Time>2009-10-01T00:00:00</Time>\n      <AccessList>2</AccessList>\n    </ParcelAccessEntry>\n    <ParcelAccessEntry>\n      <AgentID>ec2a8d18-2378-4fe0-8b68-2a31b57c481e</AgentID>\n      <Time>2010-10-20T00:00:00</Time>\n      <AccessList>1</AccessList>\n    </ParcelAccessEntry>\n  </ParcelAccessList>\n  <PassHours>0</PassHours>\n  <PassPrice>0</PassPrice>\n  <SalePrice>0</SalePrice>\n  <SnapshotID>00000000-0000-0000-0000-000000000000</SnapshotID>\n  <UserLocation>&lt;0, 0, 0&gt;</UserLocation>\n  <UserLookAt>&lt;0, 0, 0&gt;</UserLookAt>\n  <Dwell>0</Dwell>\n  <OtherCleanTime>0</OtherCleanTime>\n</LandData>";


  

        [SetUp]
        public void setup()
        {
            // setup LandData object
            this.land = new LandData();
            this.land.AABBMax = new Vector3(0, 0, 0);
            this.land.AABBMin = new Vector3(128, 128, 128);
            this.land.Area = 128;
            this.land.AuctionID = 0;
            this.land.AuthBuyerID = new UUID();
            this.land.Category = ParcelCategory.Residential;
            this.land.ClaimDate = 0;
            this.land.ClaimPrice = 0;
            this.land.GlobalID = new UUID("54ff9641-dd40-4a2c-b1f1-47dd3af24e50");
            this.land.GroupID = new UUID("d740204e-bbbf-44aa-949d-02c7d739f6a5");
            this.land.GroupPrims = 0;
            this.land.Description = "land data to test LandDataSerializer";
            this.land.Flags = (uint)(ParcelFlags.AllowDamage | ParcelFlags.AllowVoiceChat);
            this.land.LandingType = (byte)LandingType.Direct;
            this.land.Name = "LandDataSerializerTest Land";
            this.land.Status = ParcelStatus.Leased;
            this.land.LocalID = 0;
            this.land.MediaAutoScale = (byte)0x01;
            this.land.MediaID = new UUID("d4452578-2f25-4b97-a81b-819af559cfd7");
            this.land.MediaURL = "http://videos.opensimulator.org/bumblebee.mp4";
            this.land.OwnerID = new UUID("1b8eedf9-6d15-448b-8015-24286f1756bf");

            this.landWithParcelAccessList = this.land.Copy();
            this.landWithParcelAccessList.ParcelAccessList.Clear();

            ParcelManager.ParcelAccessEntry pae0 = new ParcelManager.ParcelAccessEntry();
            pae0.AgentID = new UUID("62d65d45-c91a-4f77-862c-46557d978b6c");
            pae0.Flags = AccessList.Ban;
            pae0.Time = new DateTime(2009, 10, 01);
            this.landWithParcelAccessList.ParcelAccessList.Add(pae0);

            ParcelManager.ParcelAccessEntry pae1 = new ParcelManager.ParcelAccessEntry();
            pae1.AgentID = new UUID("ec2a8d18-2378-4fe0-8b68-2a31b57c481e");
            pae1.Flags = AccessList.Access;
            pae1.Time = new DateTime(2010, 10, 20);
            this.landWithParcelAccessList.ParcelAccessList.Add(pae1);
        }

        /// <summary>
        /// Test the LandDataSerializer.Serialize() method
        /// </summary>
        [Test]
        public void LandDataSerializerSerializeTest()
        {
            string serialized = LandDataSerializer.Serialize(this.land);
            Assert.That(serialized.Length > 0, "Serialize(LandData) returned empty string");
            Assert.That(serialized == LandDataSerializerTest.preSerialized, 
                        "result of Serialize(LandData) does not match expected result");

            string serializedWithParcelAccessList = LandDataSerializer.Serialize(this.landWithParcelAccessList);
            Assert.That(serializedWithParcelAccessList.Length > 0, 
                        "Serialize(LandData) returned empty string for LandData object with ParcelAccessList");
            Assert.That(serializedWithParcelAccessList == LandDataSerializerTest.preSerializedWithParcelAccessList, 
                        "result of Serialize(LandData) does not match expected result (pre-serialized with parcel access list");
        }

        /// <summary>
        /// Test the LandDataSerializer.Deserialize() method
        /// </summary>
        [Test]
        public void TestLandDataSerializerDeserializeFromStringTest()
        {
            LandData reifiedLandData = LandDataSerializer.Deserialize(LandDataSerializerTest.preSerialized);
            Assert.That(reifiedLandData != null, "Deserialize(string) returned null");
            Assert.That(reifiedLandData.GlobalID == this.land.GlobalID, "Reified LandData.GlobalID != original LandData.GlobalID");
            Assert.That(reifiedLandData.Name == this.land.Name, "Reified LandData.Name != original LandData.Name");

            LandData reifiedLandDataWithParcelAccessList = LandDataSerializer.Deserialize(LandDataSerializerTest.preSerializedWithParcelAccessList);
            Assert.That(reifiedLandDataWithParcelAccessList != null, 
                        "Deserialize(string) returned null (pre-serialized with parcel access list)");
            Assert.That(reifiedLandDataWithParcelAccessList.GlobalID == this.landWithParcelAccessList.GlobalID, 
                        "Reified LandData.GlobalID != original LandData.GlobalID (pre-serialized with parcel access list)");
            Assert.That(reifiedLandDataWithParcelAccessList.Name == this.landWithParcelAccessList.Name, 
                        "Reified LandData.Name != original LandData.Name (pre-serialized with parcel access list)");
        }
    }
}
