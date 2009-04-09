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
    public class MySQLUserTest : BasicUserTest
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
                db = new MySQLUserData();
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
            // if a new table is added, it has to be dropped here
            if (database != null)
            {
                database.ExecuteSql("drop table migrations");
                database.ExecuteSql("drop table users");
                database.ExecuteSql("drop table userfriends");
                database.ExecuteSql("drop table agents");
                database.ExecuteSql("drop table avatarappearance");
                database.ExecuteSql("drop table avatarattachments");
            }
        }
    }
}
