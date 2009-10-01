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
        }

        /// <summary>
        /// </summary>
        [Test]
        public void TestLandDataSerializerSerializeTest()
        {
            string serialized = LandDataSerializer.Serialize(this.land);
            Assert.That(serialized.Length > 0);
        }

        /// <summary>
        /// </summary>
        [Test]
        public void TestLandDataSerializerDeserializeTest()
        {
        }
    }
}
