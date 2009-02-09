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
using System.Drawing;
using System.Text;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenSim.Framework;
using OpenSim.Data;
using OpenMetaverse;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Data.Tests
{
    public class BasicEstateTest
    {
        public IEstateDataStore db;
        public IRegionDataStore regionDb;
        public UUID prim1;
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
            prim1 = UUID.Random();
            random = new Random();

        }
        
        [Test]
        public void T010_StoreEstateSettings()
        {
            // Initializing field values. Avoid randomness. For checking ranges use different parameter sets
            // for mix and max values. If you use random values the tests are not _repeatable_.
            string estateName = "test-estate";
            uint parentEstateID = 2;
            float billableFactor = 3;
            int priceMeter = 4;
            int redirectGridX = 5;
            int redirectGridY = 6;
            bool useGlobalTime = true;
            bool fixedSun = true;
            double sunPosition = 7;
            bool allowVoice = true;
            bool allowDirectTeleport = true;
            bool denyAnonymous = true;
            bool denyIdentified = true;
            bool denyTransacted = true;
            bool abuseEmailtoEstateOwner = true;
            bool blockDwell = true;
            bool estateskipScripts = true;
            bool taxFree = true;
            bool publicAccess = true;
            string abuseMail = "test-email@nowhere.com";
            UUID estateOwner = new UUID("250d214e-1c7e-4f9b-a488-87c5e53feed7");
            bool denyMinors = (random.NextDouble() > 0.5)? true : false;

            // Lets choose random region ID
            UUID regionId = new UUID("250d214e-1c7e-4f9b-a488-87c5e53feed7");

            // Letting estate store generate rows to database for us
            EstateSettings es = db.LoadEstateSettings(regionId);

            // Setting field values to the on demand create settings object.
            es.EstateName = estateName;
            es.ParentEstateID = parentEstateID;
            es.BillableFactor = billableFactor;
            es.PricePerMeter = priceMeter;
            es.RedirectGridX = redirectGridX;
            es.RedirectGridY = redirectGridY;
            es.UseGlobalTime = useGlobalTime;
            es.FixedSun = fixedSun;
            es.SunPosition = sunPosition;
            es.AllowVoice = allowVoice;
            es.AllowDirectTeleport = allowDirectTeleport;
            es.DenyAnonymous = denyAnonymous;
            es.DenyIdentified = denyIdentified;
            es.DenyTransacted = denyTransacted;
            es.AbuseEmailToEstateOwner = abuseEmailtoEstateOwner;
            es.BlockDwell = blockDwell;
            es.EstateSkipScripts = estateskipScripts;
            es.TaxFree = taxFree;
            es.PublicAccess = publicAccess;
            es.AbuseEmail = abuseMail;
            es.EstateOwner = estateOwner;
            es.DenyMinors = denyMinors;

            // Saving settings.
            db.StoreEstateSettings(es);

            // Loading settings to another instance variable.
            EstateSettings nes = db.LoadEstateSettings(regionId);

            // Checking that loaded values are correct.
            Assert.That(estateName, Is.EqualTo(nes.EstateName));
            Assert.That(parentEstateID, Is.EqualTo(nes.ParentEstateID));
            Assert.That(billableFactor, Is.EqualTo(nes.BillableFactor));
            Assert.That(priceMeter, Is.EqualTo(nes.PricePerMeter));
            Assert.That(redirectGridX, Is.EqualTo(nes.RedirectGridX));
            Assert.That(redirectGridY, Is.EqualTo(nes.RedirectGridY));
            Assert.That(useGlobalTime, Is.EqualTo(nes.UseGlobalTime));
            Assert.That(fixedSun, Is.EqualTo(nes.FixedSun));
            Assert.That(sunPosition, Is.EqualTo(nes.SunPosition));
            Assert.That(allowVoice, Is.EqualTo(nes.AllowVoice));
            Assert.That(allowDirectTeleport, Is.EqualTo(nes.AllowDirectTeleport));
            Assert.That(denyAnonymous, Is.EqualTo(nes.DenyAnonymous));
            Assert.That(denyIdentified, Is.EqualTo(nes.DenyIdentified));
            Assert.That(denyTransacted, Is.EqualTo(nes.DenyTransacted));
            Assert.That(abuseEmailtoEstateOwner, Is.EqualTo(nes.AbuseEmailToEstateOwner));
            Assert.That(blockDwell, Is.EqualTo(nes.BlockDwell));
            Assert.That(estateskipScripts, Is.EqualTo(nes.EstateSkipScripts));
            Assert.That(taxFree, Is.EqualTo(nes.TaxFree));
            Assert.That(publicAccess, Is.EqualTo(nes.PublicAccess));
            Assert.That(abuseMail, Is.EqualTo(nes.AbuseEmail));
            Assert.That(estateOwner, Is.EqualTo(nes.EstateOwner));
            Assert.That(denyMinors, Is.EqualTo(nes.DenyMinors));

        }
        
    }
}
