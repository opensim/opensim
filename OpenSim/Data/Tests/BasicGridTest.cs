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
using System.Text;
using log4net.Config;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using log4net;
using System.Reflection;

namespace OpenSim.Data.Tests
{
    public class BasicGridTest
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public GridDataBase db;
        public UUID region1, region2, region3;
        public UUID zero = UUID.Zero;
        public static Random random;

        [TearDown]
        public void removeAllRegions()
        {
            // Clean up all the regions.
            List<RegionProfileData> regions = db.GetRegionsByName("", 100);
            if(regions != null)
            {
                foreach (RegionProfileData region in regions)
                {
                    db.DeleteProfile(region.Uuid.ToString());
                }
            }
        }

        public void SuperInit()
        {
            try
            {
                XmlConfigurator.Configure();
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

        protected RegionProfileData createRegion(UUID regionUUID, string regionName)
        {
            RegionProfileData reg = new RegionProfileData();
            ScrambleForTesting.Scramble(reg);
            reg.Uuid = regionUUID;
            reg.RegionName = regionName;

            db.AddProfile(reg);

            return reg;
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
        public void T011_AddRetrieveCompleteTest()
        {
            RegionProfileData newreg = createRegion(region2, "|<Goth@m Ci1y>|");
            RegionProfileData retreg = db.GetProfileByUUID(region2);

            Assert.That(retreg, Constraints.PropertyCompareConstraint(newreg).IgnoreProperty(x => x.RegionOnline));

            retreg = db.GetProfileByHandle(newreg.RegionHandle);
            Assert.That(retreg.Uuid, Is.EqualTo(region2), "Assert.That(retreg.Uuid, Is.EqualTo(region2))");

            retreg = db.GetProfileByString(newreg.RegionName);
            Assert.That(retreg.Uuid, Is.EqualTo(region2), "Assert.That(retreg.Uuid, Is.EqualTo(region2))");

            RegionProfileData[] retregs = db.GetProfilesInRange(newreg.RegionLocX,newreg.RegionLocY,newreg.RegionLocX,newreg.RegionLocY);
            Assert.That(retregs[0].Uuid, Is.EqualTo(region2), "Assert.That(retregs[0].Uuid, Is.EqualTo(region2))");
        }

        [Test]
        public void T012_DeleteProfile()
        {
            createRegion(region1, "doesn't matter");

            db.DeleteProfile(region1.ToString());
            RegionProfileData retreg = db.GetProfileByUUID(region1);
            Assert.That(retreg,Is.Null);
        }

        [Test]
        public void T013_UpdateProfile()
        {
            createRegion(region2, "|<Goth@m Ci1y>|");

            RegionProfileData retreg = db.GetProfileByUUID(region2);
            retreg.regionName = "Gotham City";

            db.UpdateProfile(retreg);

            retreg = db.GetProfileByUUID(region2);
            Assert.That(retreg.RegionName, Is.EqualTo("Gotham City"), "Assert.That(retreg.RegionName, Is.EqualTo(\"Gotham City\"))");
        }

        [Test]
        public void T014_RegionList()
        {
            createRegion(region2, "Gotham City");

            RegionProfileData retreg = db.GetProfileByUUID(region2);
            retreg.RegionName = "Gotham Town";
            retreg.Uuid = region1;

            db.AddProfile(retreg);

            retreg = db.GetProfileByUUID(region2);
            retreg.RegionName = "Gothan Town";
            retreg.Uuid = region3;

            db.AddProfile(retreg);

            List<RegionProfileData> listreg = db.GetRegionsByName("Gotham",10);

            Assert.That(listreg.Count,Is.EqualTo(2), "Assert.That(listreg.Count,Is.EqualTo(2))");
            Assert.That(listreg[0].Uuid,Is.Not.EqualTo(listreg[1].Uuid), "Assert.That(listreg[0].Uuid,Is.Not.EqualTo(listreg[1].Uuid))");
            Assert.That(listreg[0].Uuid, Is.EqualTo(region1) | Is.EqualTo(region2), "Assert.That(listreg[0].Uuid, Is.EqualTo(region1) | Is.EqualTo(region2))");
            Assert.That(listreg[1].Uuid, Is.EqualTo(region1) | Is.EqualTo(region2), "Assert.That(listreg[1].Uuid, Is.EqualTo(region1) | Is.EqualTo(region2))");
        }

        [Test]
        public void T999_StillNull()
        {
            Assert.That(db.GetProfileByUUID(zero), Is.Null);
        }

        protected static string RandomName()
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
