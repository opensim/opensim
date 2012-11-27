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
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using NUnit.Framework;
using OpenSim.Framework;
using OpenSim.Framework.Serialization.External;
using OpenSim.Tests.Common;

namespace OpenSim.Framework.Serialization.Tests
{
    [TestFixture]
    public class RegionSettingsSerializerTests : OpenSimTestCase
    {
        private string m_serializedRs = @"<?xml version=""1.0"" encoding=""utf-16""?>
<RegionSettings>
  <General>
    <AllowDamage>True</AllowDamage>
    <AllowLandResell>True</AllowLandResell>
    <AllowLandJoinDivide>True</AllowLandJoinDivide>
    <BlockFly>True</BlockFly>
    <BlockLandShowInSearch>True</BlockLandShowInSearch>
    <BlockTerraform>True</BlockTerraform>
    <DisableCollisions>True</DisableCollisions>
    <DisablePhysics>True</DisablePhysics>
    <DisableScripts>True</DisableScripts>
    <MaturityRating>1</MaturityRating>
    <RestrictPushing>True</RestrictPushing>
    <AgentLimit>40</AgentLimit>
    <ObjectBonus>1.4</ObjectBonus>
  </General>
  <GroundTextures>
    <Texture1>00000000-0000-0000-0000-000000000020</Texture1>
    <Texture2>00000000-0000-0000-0000-000000000040</Texture2>
    <Texture3>00000000-0000-0000-0000-000000000060</Texture3>
    <Texture4>00000000-0000-0000-0000-000000000080</Texture4>
    <ElevationLowSW>1.9</ElevationLowSW>
    <ElevationLowNW>15.9</ElevationLowNW>
    <ElevationLowSE>49</ElevationLowSE>
    <ElevationLowNE>45.3</ElevationLowNE>
    <ElevationHighSW>2.1</ElevationHighSW>
    <ElevationHighNW>4.5</ElevationHighNW>
    <ElevationHighSE>9.2</ElevationHighSE>
    <ElevationHighNE>19.2</ElevationHighNE>
  </GroundTextures>
  <Terrain>
    <WaterHeight>23</WaterHeight>
    <TerrainRaiseLimit>17.9</TerrainRaiseLimit>
    <TerrainLowerLimit>0.4</TerrainLowerLimit>
    <UseEstateSun>True</UseEstateSun>
    <FixedSun>true</FixedSun>
    <SunPosition>12</SunPosition>
  </Terrain>
  <Telehub>
    <TelehubObject>00000000-0000-0000-0000-111111111111</TelehubObject>
    <SpawnPoint>1,-2,0.33</SpawnPoint>
  </Telehub>
</RegionSettings>";

        private RegionSettings m_rs;

        [SetUp]
        public void Setup()
        {
            m_rs = new RegionSettings();
            m_rs.AgentLimit = 17;
            m_rs.AllowDamage = true;
            m_rs.AllowLandJoinDivide = true;
            m_rs.AllowLandResell = true;
            m_rs.BlockFly = true;
            m_rs.BlockShowInSearch = true;
            m_rs.BlockTerraform = true;
            m_rs.DisableCollisions = true;
            m_rs.DisablePhysics = true;
            m_rs.DisableScripts = true;
            m_rs.Elevation1NW = 15.9;
            m_rs.Elevation1NE = 45.3;
            m_rs.Elevation1SE = 49;
            m_rs.Elevation1SW = 1.9;
            m_rs.Elevation2NW = 4.5;
            m_rs.Elevation2NE = 19.2;
            m_rs.Elevation2SE = 9.2;
            m_rs.Elevation2SW = 2.1;
            m_rs.FixedSun = true;
            m_rs.SunPosition = 12.0;
            m_rs.ObjectBonus = 1.4;
            m_rs.RestrictPushing = true;
            m_rs.TerrainLowerLimit = 0.4;
            m_rs.TerrainRaiseLimit = 17.9;
            m_rs.TerrainTexture1 = UUID.Parse("00000000-0000-0000-0000-000000000020");
            m_rs.TerrainTexture2 = UUID.Parse("00000000-0000-0000-0000-000000000040");
            m_rs.TerrainTexture3 = UUID.Parse("00000000-0000-0000-0000-000000000060");
            m_rs.TerrainTexture4 = UUID.Parse("00000000-0000-0000-0000-000000000080");
            m_rs.UseEstateSun = true;
            m_rs.WaterHeight = 23;
            m_rs.TelehubObject = UUID.Parse("00000000-0000-0000-0000-111111111111");
            m_rs.AddSpawnPoint(SpawnPoint.Parse("1,-2,0.33"));
        }

        [Test]
        public void TestRegionSettingsDeserialize()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            RegionSettings deserRs = RegionSettingsSerializer.Deserialize(m_serializedRs);
            Assert.That(deserRs, Is.Not.Null);
            Assert.That(deserRs.TerrainTexture2, Is.EqualTo(m_rs.TerrainTexture2));
            Assert.That(deserRs.DisablePhysics, Is.EqualTo(m_rs.DisablePhysics));
            Assert.That(deserRs.TerrainLowerLimit, Is.EqualTo(m_rs.TerrainLowerLimit));
            Assert.That(deserRs.TelehubObject, Is.EqualTo(m_rs.TelehubObject));
            Assert.That(deserRs.SpawnPoints()[0].ToString(), Is.EqualTo(m_rs.SpawnPoints()[0].ToString()));
        }
    }
}
