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
using log4net.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Tests.Common;
using System.Text;
using log4net;
using System.Reflection;
using System.Data.Common;

// DBMS-specific:
using MySql.Data.MySqlClient;
using OpenSim.Data.MySQL;

using Mono.Data.Sqlite;
using OpenSim.Data.SQLite;

namespace OpenSim.Data.Tests
{
    [TestFixture(Description = "Estate store tests (SQLite)")]
    public class SQLiteEstateTests : EstateTests<SqliteConnection, SQLiteEstateStore>
    {
    }

    [TestFixture(Description = "Estate store tests (MySQL)")]
    public class MySqlEstateTests : EstateTests<MySqlConnection, MySQLEstateStore>
    {
    }

    public class EstateTests<TConn, TEstateStore> : BasicDataServiceTest<TConn, TEstateStore>
        where TConn : DbConnection, new()
        where TEstateStore : class, IEstateDataStore, new()
    {
        public IEstateDataStore db;

        public static UUID REGION_ID = new UUID("250d214e-1c7e-4f9b-a488-87c5e53feed7");

        public static UUID USER_ID_1 = new UUID("250d214e-1c7e-4f9b-a488-87c5e53feed1");
        public static UUID USER_ID_2 = new UUID("250d214e-1c7e-4f9b-a488-87c5e53feed2");

        public static UUID MANAGER_ID_1 = new UUID("250d214e-1c7e-4f9b-a488-87c5e53feed3");
        public static UUID MANAGER_ID_2 = new UUID("250d214e-1c7e-4f9b-a488-87c5e53feed4");

        public static UUID GROUP_ID_1 = new UUID("250d214e-1c7e-4f9b-a488-87c5e53feed5");
        public static UUID GROUP_ID_2 = new UUID("250d214e-1c7e-4f9b-a488-87c5e53feed6");

        protected override void InitService(object service)
        {
            ClearDB();
            db = (IEstateDataStore)service;
            db.Initialise(m_connStr);
        }

        private void ClearDB()
        {
            // if a new table is added, it has to be dropped here
            DropTables(
                "estate_managers",
                "estate_groups",
                "estate_users",
                "estateban",
                "estate_settings",
                "estate_map"
            );
            ResetMigrations("EstateStore");
        }

        #region 0Tests

        [Test]
        public void T010_EstateSettingsSimpleStorage_MinimumParameterSet()
        {
            TestHelpers.InMethod();
            
            EstateSettingsSimpleStorage(
                REGION_ID,
                DataTestUtil.STRING_MIN,
                DataTestUtil.UNSIGNED_INTEGER_MIN,
                DataTestUtil.FLOAT_MIN,
                DataTestUtil.INTEGER_MIN,
                DataTestUtil.INTEGER_MIN,
                DataTestUtil.INTEGER_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.DOUBLE_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.STRING_MIN,
                DataTestUtil.UUID_MIN
                );
        }

        [Test]
        public void T011_EstateSettingsSimpleStorage_MaximumParameterSet()
        {
            TestHelpers.InMethod();
            
            EstateSettingsSimpleStorage(
                REGION_ID,
                DataTestUtil.STRING_MAX(64),
                DataTestUtil.UNSIGNED_INTEGER_MAX,
                DataTestUtil.FLOAT_MAX,
                DataTestUtil.INTEGER_MAX,
                DataTestUtil.INTEGER_MAX,
                DataTestUtil.INTEGER_MAX,
                DataTestUtil.BOOLEAN_MAX,
                DataTestUtil.BOOLEAN_MAX,
                DataTestUtil.DOUBLE_MAX,
                DataTestUtil.BOOLEAN_MAX,
                DataTestUtil.BOOLEAN_MAX,
                DataTestUtil.BOOLEAN_MAX,
                DataTestUtil.BOOLEAN_MAX,
                DataTestUtil.BOOLEAN_MAX,
                DataTestUtil.BOOLEAN_MAX,
                DataTestUtil.BOOLEAN_MAX,
                DataTestUtil.BOOLEAN_MAX,
                DataTestUtil.BOOLEAN_MAX,
                DataTestUtil.BOOLEAN_MAX,
                DataTestUtil.BOOLEAN_MAX,
                DataTestUtil.BOOLEAN_MAX,
                DataTestUtil.STRING_MAX(255),
                DataTestUtil.UUID_MAX
                );
        }

        [Test]
        public void T012_EstateSettingsSimpleStorage_AccurateParameterSet()
        {
            TestHelpers.InMethod();
            
            EstateSettingsSimpleStorage(
                REGION_ID,
                DataTestUtil.STRING_MAX(1),
                DataTestUtil.UNSIGNED_INTEGER_MIN,
                DataTestUtil.FLOAT_ACCURATE,
                DataTestUtil.INTEGER_MIN,
                DataTestUtil.INTEGER_MIN,
                DataTestUtil.INTEGER_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.DOUBLE_ACCURATE,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.BOOLEAN_MIN,
                DataTestUtil.STRING_MAX(1),
                DataTestUtil.UUID_MIN
                );
        }

        [Test]
        public void T012_EstateSettingsRandomStorage()
        {
            TestHelpers.InMethod();
            
            // Letting estate store generate rows to database for us
            EstateSettings originalSettings = db.LoadEstateSettings(REGION_ID, true);
            new PropertyScrambler<EstateSettings>()
                .DontScramble(x=>x.EstateID)
                .Scramble(originalSettings);

            // Saving settings.
            db.StoreEstateSettings(originalSettings);

            // Loading settings to another instance variable.
            EstateSettings loadedSettings = db.LoadEstateSettings(REGION_ID, true);

            // Checking that loaded values are correct.
            Assert.That(loadedSettings, Constraints.PropertyCompareConstraint(originalSettings));
        }

        [Test]
        public void T020_EstateSettingsManagerList()
        {
            TestHelpers.InMethod();
            
            // Letting estate store generate rows to database for us
            EstateSettings originalSettings = db.LoadEstateSettings(REGION_ID, true);

            originalSettings.EstateManagers = new UUID[] { MANAGER_ID_1, MANAGER_ID_2 };

            // Saving settings.
            db.StoreEstateSettings(originalSettings);

            // Loading settings to another instance variable.
            EstateSettings loadedSettings = db.LoadEstateSettings(REGION_ID, true);

            Assert.AreEqual(2, loadedSettings.EstateManagers.Length);
            Assert.AreEqual(MANAGER_ID_1, loadedSettings.EstateManagers[0]);
            Assert.AreEqual(MANAGER_ID_2, loadedSettings.EstateManagers[1]);
        }

        [Test]
        public void T021_EstateSettingsUserList()
        {
            TestHelpers.InMethod();
            
            // Letting estate store generate rows to database for us
            EstateSettings originalSettings = db.LoadEstateSettings(REGION_ID, true);

            originalSettings.EstateAccess = new UUID[] { USER_ID_1, USER_ID_2 };

            // Saving settings.
            db.StoreEstateSettings(originalSettings);

            // Loading settings to another instance variable.
            EstateSettings loadedSettings = db.LoadEstateSettings(REGION_ID, true);

            Assert.AreEqual(2, loadedSettings.EstateAccess.Length);
            Assert.AreEqual(USER_ID_1, loadedSettings.EstateAccess[0]);
            Assert.AreEqual(USER_ID_2, loadedSettings.EstateAccess[1]);
        }

        [Test]
        public void T022_EstateSettingsGroupList()
        {
            TestHelpers.InMethod();
            
            // Letting estate store generate rows to database for us
            EstateSettings originalSettings = db.LoadEstateSettings(REGION_ID, true);

            originalSettings.EstateGroups = new UUID[] { GROUP_ID_1, GROUP_ID_2 };

            // Saving settings.
            db.StoreEstateSettings(originalSettings);

            // Loading settings to another instance variable.
            EstateSettings loadedSettings = db.LoadEstateSettings(REGION_ID, true);

            Assert.AreEqual(2, loadedSettings.EstateAccess.Length);
            Assert.AreEqual(GROUP_ID_1, loadedSettings.EstateGroups[0]);
            Assert.AreEqual(GROUP_ID_2, loadedSettings.EstateGroups[1]);
        }

        [Test]
        public void T022_EstateSettingsBanList()
        {
            TestHelpers.InMethod();
            
            // Letting estate store generate rows to database for us
            EstateSettings originalSettings = db.LoadEstateSettings(REGION_ID, true);

            EstateBan estateBan1 = new EstateBan();
            estateBan1.BannedUserID = DataTestUtil.UUID_MIN;

            EstateBan estateBan2 = new EstateBan();
            estateBan2.BannedUserID = DataTestUtil.UUID_MAX;

            originalSettings.EstateBans = new EstateBan[] { estateBan1, estateBan2 };

            // Saving settings.
            db.StoreEstateSettings(originalSettings);

            // Loading settings to another instance variable.
            EstateSettings loadedSettings = db.LoadEstateSettings(REGION_ID, true);

            Assert.AreEqual(2, loadedSettings.EstateBans.Length);
            Assert.AreEqual(DataTestUtil.UUID_MIN, loadedSettings.EstateBans[0].BannedUserID);

            Assert.AreEqual(DataTestUtil.UUID_MAX, loadedSettings.EstateBans[1].BannedUserID);

        }

        #endregion

        #region Parametrizable Test Implementations 

        private void EstateSettingsSimpleStorage(
            UUID regionId,
            string estateName,
            uint parentEstateID,
            float billableFactor,
            int pricePerMeter,
            int redirectGridX,
            int redirectGridY,
            bool useGlobalTime,
            bool fixedSun,
            double sunPosition,
            bool allowVoice,
            bool allowDirectTeleport,
            bool resetHomeOnTeleport,
            bool denyAnonymous,
            bool denyIdentified,
            bool denyTransacted,
            bool denyMinors,
            bool abuseEmailToEstateOwner,
            bool blockDwell,
            bool estateSkipScripts,
            bool taxFree,
            bool publicAccess,
            string abuseEmail,
            UUID estateOwner
            )
        {

            // Letting estate store generate rows to database for us
            EstateSettings originalSettings = db.LoadEstateSettings(regionId, true);

            SetEstateSettings(originalSettings,
                estateName,
                parentEstateID,
                billableFactor,
                pricePerMeter,
                redirectGridX,
                redirectGridY,
                useGlobalTime,
                fixedSun,
                sunPosition,
                allowVoice,
                allowDirectTeleport,
                resetHomeOnTeleport,
                denyAnonymous,
                denyIdentified,
                denyTransacted,
                denyMinors,
                abuseEmailToEstateOwner,
                blockDwell,
                estateSkipScripts,
                taxFree,
                publicAccess,
                abuseEmail,
                estateOwner
                );

            // Saving settings.
            db.StoreEstateSettings(originalSettings);

            // Loading settings to another instance variable.
            EstateSettings loadedSettings = db.LoadEstateSettings(regionId, true);

            // Checking that loaded values are correct.
            ValidateEstateSettings(loadedSettings,
                estateName,
                parentEstateID,
                billableFactor,
                pricePerMeter,
                redirectGridX,
                redirectGridY,
                useGlobalTime,
                fixedSun,
                sunPosition,
                allowVoice,
                allowDirectTeleport,
                resetHomeOnTeleport,
                denyAnonymous,
                denyIdentified,
                denyTransacted,
                denyMinors,
                abuseEmailToEstateOwner,
                blockDwell,
                estateSkipScripts,
                taxFree,
                publicAccess,
                abuseEmail,
                estateOwner
                );

        }

        #endregion

        #region EstateSetting Initialization and Validation Methods

        private void SetEstateSettings(
            EstateSettings estateSettings,
            string estateName,
            uint parentEstateID,
            float billableFactor,
            int pricePerMeter,
            int redirectGridX,
            int redirectGridY,
            bool useGlobalTime,
            bool fixedSun,
            double sunPosition,
            bool allowVoice,
            bool allowDirectTeleport,
            bool resetHomeOnTeleport,
            bool denyAnonymous,
            bool denyIdentified,
            bool denyTransacted,
            bool denyMinors,
            bool abuseEmailToEstateOwner,
            bool blockDwell,
            bool estateSkipScripts,
            bool taxFree,
            bool publicAccess,
            string abuseEmail,
            UUID estateOwner
            )
        {
            estateSettings.EstateName = estateName;
            estateSettings.ParentEstateID = parentEstateID;
            estateSettings.BillableFactor = billableFactor;
            estateSettings.PricePerMeter = pricePerMeter;
            estateSettings.RedirectGridX = redirectGridX;
            estateSettings.RedirectGridY = redirectGridY;
            estateSettings.UseGlobalTime = useGlobalTime;
            estateSettings.FixedSun = fixedSun;
            estateSettings.SunPosition = sunPosition;
            estateSettings.AllowVoice = allowVoice;
            estateSettings.AllowDirectTeleport = allowDirectTeleport;
            estateSettings.ResetHomeOnTeleport = resetHomeOnTeleport;
            estateSettings.DenyAnonymous = denyAnonymous;
            estateSettings.DenyIdentified = denyIdentified;
            estateSettings.DenyTransacted = denyTransacted;
            estateSettings.DenyMinors = denyMinors;
            estateSettings.AbuseEmailToEstateOwner = abuseEmailToEstateOwner;
            estateSettings.BlockDwell = blockDwell;
            estateSettings.EstateSkipScripts = estateSkipScripts;
            estateSettings.TaxFree = taxFree;
            estateSettings.PublicAccess = publicAccess;
            estateSettings.AbuseEmail = abuseEmail;
            estateSettings.EstateOwner = estateOwner;
        }

        private void ValidateEstateSettings(
            EstateSettings estateSettings,
            string estateName,
            uint parentEstateID,
            float billableFactor,
            int pricePerMeter,
            int redirectGridX,
            int redirectGridY,
            bool useGlobalTime,
            bool fixedSun,
            double sunPosition,
            bool allowVoice,
            bool allowDirectTeleport,
            bool resetHomeOnTeleport,
            bool denyAnonymous,
            bool denyIdentified,
            bool denyTransacted,
            bool denyMinors,
            bool abuseEmailToEstateOwner,
            bool blockDwell,
            bool estateSkipScripts,
            bool taxFree,
            bool publicAccess,
            string abuseEmail,
            UUID estateOwner
            )
        {
            Assert.AreEqual(estateName, estateSettings.EstateName);
            Assert.AreEqual(parentEstateID, estateSettings.ParentEstateID);

            DataTestUtil.AssertFloatEqualsWithTolerance(billableFactor, estateSettings.BillableFactor);

            Assert.AreEqual(pricePerMeter, estateSettings.PricePerMeter);
            Assert.AreEqual(redirectGridX, estateSettings.RedirectGridX);
            Assert.AreEqual(redirectGridY, estateSettings.RedirectGridY);
            Assert.AreEqual(useGlobalTime, estateSettings.UseGlobalTime);
            Assert.AreEqual(fixedSun, estateSettings.FixedSun);

            DataTestUtil.AssertDoubleEqualsWithTolerance(sunPosition, estateSettings.SunPosition);

            Assert.AreEqual(allowVoice, estateSettings.AllowVoice);
            Assert.AreEqual(allowDirectTeleport, estateSettings.AllowDirectTeleport);
            Assert.AreEqual(resetHomeOnTeleport, estateSettings.ResetHomeOnTeleport);
            Assert.AreEqual(denyAnonymous, estateSettings.DenyAnonymous);
            Assert.AreEqual(denyIdentified, estateSettings.DenyIdentified);
            Assert.AreEqual(denyTransacted, estateSettings.DenyTransacted);
            Assert.AreEqual(denyMinors, estateSettings.DenyMinors);
            Assert.AreEqual(abuseEmailToEstateOwner, estateSettings.AbuseEmailToEstateOwner);
            Assert.AreEqual(blockDwell, estateSettings.BlockDwell);
            Assert.AreEqual(estateSkipScripts, estateSettings.EstateSkipScripts);
            Assert.AreEqual(taxFree, estateSettings.TaxFree);
            Assert.AreEqual(publicAccess, estateSettings.PublicAccess);
            Assert.AreEqual(abuseEmail, estateSettings.AbuseEmail);
            Assert.AreEqual(estateOwner, estateSettings.EstateOwner);
        }

        #endregion
    }
}