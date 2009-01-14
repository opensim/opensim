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

using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;
using OpenMetaverse;

namespace OpenSim.Data.Tests
{
    public class BasicGridTest
    {
        public GridDataBase db;
        public UUID region1, region2, region3;
        public UUID zero = UUID.Zero;
        public static Random random;


        public void SuperInit()
        {
            try
            {
                log4net.Config.XmlConfigurator.Configure();
            }
            catch (Exception)
            {
                // I don't care, just leave log4net off
            }
            region1 = UUID.Random();
            region2 = UUID.Random();
            region3 = UUID.Random();
            random = new Random();
        }

        [Test]
        public void T001_LoadEmpty()
        {
            Assert.That(db.GetProfileByUUID(region1),Is.Null);
            Assert.That(db.GetProfileByUUID(region2),Is.Null);
            Assert.That(db.GetProfileByUUID(region3),Is.Null);
            Assert.That(db.GetProfileByUUID(zero),Is.Null);
        }

        [Test]
        public void T999_StillNull()
        {
            Assert.That(db.GetProfileByUUID(zero),Is.Null);
        }

        [Test]
        public void T010_SimpleAddRetrieveProfile()
        {
            RegionProfileData reg = new RegionProfileData();
            reg.Uuid = region1;
            reg.RegionName = "My new Region";
            reg.RegionHandle = (ulong) random.Next();
            reg.RegionLocX = 1000;
            reg.RegionLocY = 1000;
            reg.RegionLocZ = 0;

            db.AddProfile(reg);

            RegionProfileData retreg = db.GetProfileByUUID(region1);

            Assert.That(retreg.RegionName, Is.EqualTo("My new Region"));
            Assert.That(retreg.Uuid, Is.EqualTo(region1));
        }

        [Test]
        public void T011_AddRetrieveCompleteTest()
        {
            string regionname = "|<Goth@m Ci1y>|";
            ulong regionhandle = (ulong) random.Next();
            uint regionlocx = (uint) random.Next();
            uint regionlocy = (uint) random.Next();
            uint regionlocz = (uint) random.Next();
            string regionsendkey = RandomName();
            string regionrecvkey = RandomName();
            string regionsecret = RandomName();
            bool regiononline = false;
            string serverip = RandomName();
            uint serverport = (uint) random.Next();
            string serveruri = RandomName();
            uint serverhttpport = (uint) random.Next();
            uint serverremotingport = (uint) random.Next();
            ulong northovrhandle = (ulong) random.Next();
            ulong southovrhandle = (ulong) random.Next();
            ulong eastovrhandle = (ulong) random.Next();
            ulong westovrhandle = (ulong) random.Next();
            string regiondatauri = RandomName();
            string regionasseturi = RandomName();
            string regionassetsendkey = RandomName();
            string regionassetrcvkey = RandomName();
            string regionuseruri = RandomName();
            string regionusersendkey = RandomName();
            string regionuserrcvkey = RandomName();
            UUID regionmaptextureid = UUID.Random();
            UUID owner_uuid = UUID.Random();
            UUID originuuid = UUID.Random();


            RegionProfileData reg = new RegionProfileData();
            reg.Uuid = region2;
            reg.RegionName = regionname;
            reg.RegionHandle = regionhandle;
            reg.RegionLocX = regionlocx;
            reg.RegionLocY = regionlocy;
            reg.RegionLocZ = regionlocz;
            reg.RegionSendKey = regionsendkey;
            reg.RegionRecvKey = regionrecvkey;
            reg.RegionSecret = regionsecret;
            reg.RegionOnline = regiononline;
            reg.OriginUUID = originuuid;
            reg.ServerIP = serverip;
            reg.ServerPort = serverport;
            reg.ServerURI = serveruri;
            reg.ServerHttpPort = serverhttpport;
            reg.ServerRemotingPort = serverremotingport;
            reg.NorthOverrideHandle = northovrhandle;
            reg.SouthOverrideHandle = southovrhandle;
            reg.EastOverrideHandle = eastovrhandle;
            reg.WestOverrideHandle = westovrhandle;
            reg.RegionDataURI = regiondatauri;
            reg.RegionAssetURI = regionasseturi;
            reg.RegionAssetSendKey = regionassetsendkey;
            reg.RegionAssetRecvKey = regionassetrcvkey;
            reg.RegionUserURI = regionuseruri;
            reg.RegionUserSendKey = regionusersendkey;
            reg.RegionUserRecvKey = regionuserrcvkey;
            reg.RegionMapTextureID = regionmaptextureid;
            reg.Owner_uuid = owner_uuid;
            reg.OriginUUID = originuuid;

            db.AddProfile(reg);

            RegionProfileData retreg = db.GetProfileByUUID(region2);

            Assert.That(retreg.RegionName, Is.EqualTo(regionname));
            Assert.That(retreg.Uuid, Is.EqualTo(region2));
            Assert.That(retreg.RegionHandle, Is.EqualTo(regionhandle));
            Assert.That(retreg.RegionLocX, Is.EqualTo(regionlocx));
            Assert.That(retreg.RegionLocY, Is.EqualTo(regionlocy));
            Assert.That(retreg.RegionLocZ, Is.EqualTo(regionlocz));
            Assert.That(retreg.RegionSendKey, Is.EqualTo(regionsendkey));
            Assert.That(retreg.RegionRecvKey, Is.EqualTo(regionrecvkey));
            Assert.That(retreg.RegionSecret, Is.EqualTo(regionsecret));
            Assert.That(retreg.RegionOnline, Is.EqualTo(regiononline));
            Assert.That(retreg.OriginUUID, Is.EqualTo(originuuid));
            Assert.That(retreg.ServerIP, Is.EqualTo(serverip));
            Assert.That(retreg.ServerPort, Is.EqualTo(serverport));
            Assert.That(retreg.ServerURI, Is.EqualTo(serveruri));
            Assert.That(retreg.ServerHttpPort, Is.EqualTo(serverhttpport));
            Assert.That(retreg.ServerRemotingPort, Is.EqualTo(serverremotingport));
            Assert.That(retreg.NorthOverrideHandle, Is.EqualTo(northovrhandle));
            Assert.That(retreg.SouthOverrideHandle, Is.EqualTo(southovrhandle));
            Assert.That(retreg.EastOverrideHandle, Is.EqualTo(eastovrhandle));
            Assert.That(retreg.WestOverrideHandle, Is.EqualTo(westovrhandle));
            Assert.That(retreg.RegionDataURI, Is.EqualTo(regiondatauri));
            Assert.That(retreg.RegionAssetURI, Is.EqualTo(regionasseturi));
            Assert.That(retreg.RegionAssetSendKey, Is.EqualTo(regionassetsendkey));
            Assert.That(retreg.RegionAssetRecvKey, Is.EqualTo(regionassetrcvkey));
            Assert.That(retreg.RegionUserURI, Is.EqualTo(regionuseruri));
            Assert.That(retreg.RegionUserSendKey, Is.EqualTo(regionusersendkey));
            Assert.That(retreg.RegionUserRecvKey, Is.EqualTo(regionuserrcvkey));
            Assert.That(retreg.RegionMapTextureID, Is.EqualTo(regionmaptextureid));
            Assert.That(retreg.Owner_uuid, Is.EqualTo(owner_uuid));
            Assert.That(retreg.OriginUUID, Is.EqualTo(originuuid));

            retreg = db.GetProfileByHandle(regionhandle);
            Assert.That(retreg.Uuid, Is.EqualTo(region2));

            retreg = db.GetProfileByString(regionname);
            Assert.That(retreg.Uuid, Is.EqualTo(region2));

            RegionProfileData[] retregs = db.GetProfilesInRange(regionlocx,regionlocy,regionlocx,regionlocy);
            Assert.That(retregs[0].Uuid, Is.EqualTo(region2));
        }

        [Test]
        public void T012_DeleteProfile()
        {
            db.DeleteProfile(region1.ToString());
            RegionProfileData retreg = db.GetProfileByUUID(region1);
            Assert.That(retreg,Is.Null);
        }

        [Test]
        public void T013_UpdateProfile()
        {
            RegionProfileData retreg = db.GetProfileByUUID(region2);
            retreg.regionName = "Gotham City";

            db.UpdateProfile(retreg);

            retreg = db.GetProfileByUUID(region2);
            Assert.That(retreg.RegionName, Is.EqualTo("Gotham City"));
        }

        [Test]
        public void T014_RegionList()
        {
            RegionProfileData retreg = db.GetProfileByUUID(region2);
            retreg.RegionName = "Gotham Town";
            retreg.Uuid = region1;

            db.AddProfile(retreg);

            retreg = db.GetProfileByUUID(region2);
            retreg.RegionName = "Gothan Town";
            retreg.Uuid = region3;

            db.AddProfile(retreg);

            List<RegionProfileData> listreg = db.GetRegionsByName("Gotham",10);

            Assert.That(listreg.Count,Is.EqualTo(2));
            Assert.That(listreg[0].Uuid,Is.Not.EqualTo(listreg[1].Uuid));
            Assert.That(listreg[0].Uuid, Is.EqualTo(region1) | Is.EqualTo(region2));
            Assert.That(listreg[1].Uuid, Is.EqualTo(region1) | Is.EqualTo(region2));
        }

        [Test]
        public static string RandomName()
        {
            StringBuilder name = new StringBuilder();
            int size = random.Next(5,12);
            char ch ;
            for (int i=0; i<size; i++)
            {
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65))) ;
                name.Append(ch);
            }
            return name.ToString();
        }
    }
}
