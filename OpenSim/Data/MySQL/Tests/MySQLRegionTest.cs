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
using NUnit.Framework;
using OpenSim.Data.Tests;
using log4net;
using System.Reflection;
using OpenSim.Tests.Common;

namespace OpenSim.Data.MySQL.Tests
{
    [TestFixture, DatabaseTest]
    public class MySQLRegionTest : BasicRegionTest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public string file;
        public MySQLManager database;
        public string connect = "Server=localhost;Port=3306;Database=opensim-nunit;User ID=opensim-nunit;Password=opensim-nunit;Pooling=false;";
        
        [TestFixtureSetUp]
        public void Init()
        {
            SuperInit();
            // If we manage to connect to the database with the user
            // and password above it is our test database, and run
            // these tests.  If anything goes wrong, ignore these
            // tests.
            try 
            {
                database = new MySQLManager(connect);
                // this is important in case a previous run ended badly
                ClearDB(database);

                db = new MySQLDataStore();
                db.Initialise(connect);
            } 
            catch (Exception e)
            {
                m_log.Error("Exception {0}", e);
                Assert.Ignore();
            }
        }

        [TestFixtureTearDown]
        public void Cleanup()
        {
            if (db != null)
            {
                db.Dispose();
            }
            ClearDB(database);
        }

        private void ClearDB(MySQLManager manager) 
        {
            if (manager != null)
            {
                manager.ExecuteSql("drop table if exists migrations");
                manager.ExecuteSql("drop table if exists prims");
                manager.ExecuteSql("drop table if exists primshapes");
                manager.ExecuteSql("drop table if exists primitems");
                manager.ExecuteSql("drop table if exists terrain");
                manager.ExecuteSql("drop table if exists land");
                manager.ExecuteSql("drop table if exists landaccesslist");
                manager.ExecuteSql("drop table if exists regionban");
                manager.ExecuteSql("drop table if exists regionsettings");
                manager.ExecuteSql("drop table if exists estate_managers");
                manager.ExecuteSql("drop table if exists estate_groups");
                manager.ExecuteSql("drop table if exists estate_users");
                manager.ExecuteSql("drop table if exists estateban");
                manager.ExecuteSql("drop table if exists estate_settings");
                manager.ExecuteSql("drop table if exists estate_map");
            }
        }
    }
}
