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
using System.Drawing;
using System.Text;
using log4net.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;
using log4net;
using System.Reflection;
using System.Data.Common;
using System.Threading;

// DBMS-specific:
using MySql.Data.MySqlClient;
using OpenSim.Data.MySQL;

using Mono.Data.Sqlite;
using OpenSim.Data.SQLite;

namespace OpenSim.Data.Tests
{
    [TestFixture(Description = "Region store tests (SQLite)")]
    public class SQLiteRegionTests : RegionTests<SqliteConnection, SQLiteSimulationData>
    {
    }

    [TestFixture(Description = "Region store tests (MySQL)")]
    public class MySqlRegionTests : RegionTests<MySqlConnection, MySQLSimulationData>
    {
    }

    public class RegionTests<TConn, TRegStore> : BasicDataServiceTest<TConn, TRegStore>
        where TConn : DbConnection, new()
        where TRegStore : class, ISimulationDataStore, new()
    {
        bool m_rebuildDB;

        public ISimulationDataStore db;
        public UUID zero = UUID.Zero;
        public UUID region1 = UUID.Random();
        public UUID region2 = UUID.Random();
        public UUID region3 = UUID.Random();
        public UUID region4 = UUID.Random();
        public UUID prim1 = UUID.Random();
        public UUID prim2 = UUID.Random();
        public UUID prim3 = UUID.Random();
        public UUID prim4 = UUID.Random();
        public UUID prim5 = UUID.Random();
        public UUID prim6 = UUID.Random();
        public UUID item1 = UUID.Random();
        public UUID item2 = UUID.Random();
        public UUID item3 = UUID.Random();

        public static Random random = new Random();

        public string itemname1 = "item1";

        public uint localID = 1;

        public double height1 = 20;
        public double height2 = 100;

        public RegionTests(string conn, bool rebuild)
            : base(conn)
        {
            m_rebuildDB = rebuild;
        }

        public RegionTests() : this("", true) { }
        public RegionTests(string conn) : this(conn, true) {}
        public RegionTests(bool rebuild): this("", rebuild) {}


        protected override void InitService(object service)
        {
            ClearDB();
            db = (ISimulationDataStore)service;
            db.Initialise(m_connStr);
        }

        private void ClearDB()
        {
            string[] reg_tables = new string[] {
                "prims", "primshapes", "primitems", "terrain", "land", "landaccesslist", "regionban", "regionsettings"
            };

            if (m_rebuildDB)
            {
                DropTables(reg_tables);
                ResetMigrations("RegionStore");
            }
            else
            {
                ClearTables(reg_tables);
            }
        }

        // Test Plan
        // Prims
        //  - empty test - 001
        //  - store / retrieve basic prims (most minimal we can make) - 010, 011
        //  - store / retrieve parts in a scenegroup 012
        //  - store a prim with complete information for consistency check 013
        //  - update existing prims, make sure it sticks - 014
        //  - tests empty inventory - 020
        //  - add inventory items to prims make - 021
        //  - retrieves the added item - 022
        //  - update inventory items to prims - 023
        //  - remove inventory items make sure it sticks - 024
        //  - checks if all parameters are persistent - 025
        //  - adds many items and see if it is handled correctly - 026

        [Test]
        public void T001_LoadEmpty()
        {
            TestHelpers.InMethod();

            List<SceneObjectGroup> objs = db.LoadObjects(region1);
            List<SceneObjectGroup> objs3 = db.LoadObjects(region3);
            List<LandData> land = db.LoadLandObjects(region1);

            Assert.That(objs.Count, Is.EqualTo(0), "Assert.That(objs.Count, Is.EqualTo(0))");
            Assert.That(objs3.Count, Is.EqualTo(0), "Assert.That(objs3.Count, Is.EqualTo(0))");
            Assert.That(land.Count, Is.EqualTo(0), "Assert.That(land.Count, Is.EqualTo(0))");
        }

        // SOG round trips
        //  * store objects, make sure they save
        //  * update

        [Test]
        public void T010_StoreSimpleObject()
        {
            TestHelpers.InMethod();

            SceneObjectGroup sog = NewSOG("object1", prim1, region1);
            SceneObjectGroup sog2 = NewSOG("object2", prim2, region1);

            // in case the objects don't store
            try
            {
                db.StoreObject(sog, region1);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                Assert.Fail();
            }

            try
            {
                db.StoreObject(sog2, region1);
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
                Assert.Fail();
            }

            // This tests the ADO.NET driver
            List<SceneObjectGroup> objs = db.LoadObjects(region1);

            Assert.That(objs.Count, Is.EqualTo(2), "Assert.That(objs.Count, Is.EqualTo(2))");
        }

        [Test]
        public void T011_ObjectNames()
        {
            TestHelpers.InMethod();

            List<SceneObjectGroup> objs = db.LoadObjects(region1);
            foreach (SceneObjectGroup sog in objs)
            {
                SceneObjectPart p = sog.RootPart;
                Assert.That("", Is.Not.EqualTo(p.Name), "Assert.That(\"\", Is.Not.EqualTo(p.Name))");
                Assert.That(p.Name, Is.EqualTo(p.Description), "Assert.That(p.Name, Is.EqualTo(p.Description))");
            }
        }

        [Test]
        public void T012_SceneParts()
        {
            TestHelpers.InMethod();

            UUID tmp0 = UUID.Random();
            UUID tmp1 = UUID.Random();
            UUID tmp2 = UUID.Random();
            UUID tmp3 = UUID.Random();
            UUID newregion = UUID.Random();
            SceneObjectPart p1 = NewSOP("SoP 1",tmp1);
            SceneObjectPart p2 = NewSOP("SoP 2",tmp2);
            SceneObjectPart p3 = NewSOP("SoP 3",tmp3);
            SceneObjectGroup sog = NewSOG("Sop 0", tmp0, newregion);
            sog.AddPart(p1);
            sog.AddPart(p2);
            sog.AddPart(p3);

            SceneObjectPart[] parts = sog.Parts;
            Assert.That(parts.Length,Is.EqualTo(4), "Assert.That(parts.Length,Is.EqualTo(4))");

            db.StoreObject(sog, newregion);
            List<SceneObjectGroup> sogs = db.LoadObjects(newregion);
            Assert.That(sogs.Count,Is.EqualTo(1), "Assert.That(sogs.Count,Is.EqualTo(1))");
            SceneObjectGroup newsog = sogs[0];

            SceneObjectPart[] newparts = newsog.Parts;
            Assert.That(newparts.Length,Is.EqualTo(4), "Assert.That(newparts.Length,Is.EqualTo(4))");

            Assert.That(newsog.ContainsPart(tmp0), "Assert.That(newsog.ContainsPart(tmp0))");
            Assert.That(newsog.ContainsPart(tmp1), "Assert.That(newsog.ContainsPart(tmp1))");
            Assert.That(newsog.ContainsPart(tmp2), "Assert.That(newsog.ContainsPart(tmp2))");
            Assert.That(newsog.ContainsPart(tmp3), "Assert.That(newsog.ContainsPart(tmp3))");
        }

        [Test]
        public void T013_DatabasePersistency()
        {
            TestHelpers.InMethod();

            // Sets all ScenePart parameters, stores and retrieves them, then check for consistency with initial data
            // The commented Asserts are the ones that are unchangeable (when storing on the database, their "Set" values are ignored
            // The ObjectFlags is an exception, if it is entered incorrectly, the object IS REJECTED on the database silently.
            UUID creator,uuid = new UUID();
            creator = UUID.Random();
            uint iserial = (uint)random.Next();
            TaskInventoryDictionary dic = new TaskInventoryDictionary();
            uint objf = (uint) random.Next();
            uuid = prim4;
            uint localid = localID+1;
            localID = localID + 1;
            string name = "Adam  West";
            byte material = (byte) random.Next((int)SOPMaterialData.MaxMaterial);
            ulong regionh = (ulong)random.NextDouble() * (ulong)random.Next();
            int pin = random.Next();
            Byte[] partsys = new byte[8];
            Byte[] textani = new byte[8];
            random.NextBytes(textani);
            random.NextBytes(partsys);
            DateTime expires = new DateTime(2008, 12, 20);
            DateTime rezzed = new DateTime(2009, 07, 15);
            Vector3 groupos = new Vector3(random.Next(),random.Next(),random.Next());
            Vector3 offset = new Vector3(random.Next(),random.Next(),random.Next());
            Quaternion rotoff = new Quaternion(random.Next(),random.Next(),random.Next(),random.Next());
            Vector3 velocity = new Vector3(random.Next(),random.Next(),random.Next());
            Vector3 angvelo = new Vector3(random.Next(),random.Next(),random.Next());
            Vector3 accel = new Vector3(random.Next(),random.Next(),random.Next());
            string description = name;
            Color color = Color.FromArgb(255, 165, 50, 100);
            string text = "All Your Base Are Belong to Us";
            string sitname = "SitName";
            string touchname = "TouchName";
            int linknum = random.Next();
            byte clickaction = (byte) random.Next(127);
            PrimitiveBaseShape pbshap = new PrimitiveBaseShape();
            pbshap = PrimitiveBaseShape.Default;
            pbshap.PathBegin = ushort.MaxValue;
            pbshap.PathEnd = ushort.MaxValue;
            pbshap.ProfileBegin = ushort.MaxValue;
            pbshap.ProfileEnd = ushort.MaxValue;
            pbshap.ProfileHollow = ushort.MaxValue;
            Vector3 scale = new Vector3(random.Next(),random.Next(),random.Next());

            RegionInfo regionInfo = new RegionInfo();
            regionInfo.RegionID = region3;
            regionInfo.RegionLocX = 0;
            regionInfo.RegionLocY = 0;

            SceneObjectPart sop = new SceneObjectPart();
            SceneObjectGroup sog = new SceneObjectGroup(sop);

            sop.RegionHandle = regionh;
            sop.UUID = uuid;
            sop.LocalId = localid;
            sop.Shape = pbshap;
            sop.GroupPosition = groupos;
            sop.RotationOffset = rotoff;
            sop.CreatorID = creator;
            sop.InventorySerial = iserial;
            sop.TaskInventory = dic;
            sop.Flags = (PrimFlags)objf;
            sop.Name = name;
            sop.Material = material;
            sop.ScriptAccessPin = pin;
            sop.TextureAnimation = textani;
            sop.ParticleSystem = partsys;
            sop.Expires = expires;
            sop.Rezzed = rezzed;
            sop.OffsetPosition = offset;
            sop.Velocity = velocity;
            sop.AngularVelocity = angvelo;
            sop.Acceleration = accel;
            sop.Description = description;
            sop.Color = color;
            sop.Text = text;
            sop.SitName = sitname;
            sop.TouchName = touchname;
            sop.LinkNum = linknum;
            sop.ClickAction = clickaction;
            sop.Scale = scale;

            //Tests if local part accepted the parameters:
            Assert.That(regionh,Is.EqualTo(sop.RegionHandle), "Assert.That(regionh,Is.EqualTo(sop.RegionHandle))");
            Assert.That(localid,Is.EqualTo(sop.LocalId), "Assert.That(localid,Is.EqualTo(sop.LocalId))");
            Assert.That(groupos,Is.EqualTo(sop.GroupPosition), "Assert.That(groupos,Is.EqualTo(sop.GroupPosition))");
            Assert.That(name,Is.EqualTo(sop.Name), "Assert.That(name,Is.EqualTo(sop.Name))");
            Assert.That(rotoff,Is.EqualTo(sop.RotationOffset), "Assert.That(rotoff,Is.EqualTo(sop.RotationOffset))");
            Assert.That(uuid,Is.EqualTo(sop.UUID), "Assert.That(uuid,Is.EqualTo(sop.UUID))");
            Assert.That(creator,Is.EqualTo(sop.CreatorID), "Assert.That(creator,Is.EqualTo(sop.CreatorID))");
            // Modified in-class
            // Assert.That(iserial,Is.EqualTo(sop.InventorySerial), "Assert.That(iserial,Is.EqualTo(sop.InventorySerial))");
            Assert.That(dic,Is.EqualTo(sop.TaskInventory), "Assert.That(dic,Is.EqualTo(sop.TaskInventory))");
            Assert.That(objf, Is.EqualTo((uint)sop.Flags), "Assert.That(objf,Is.EqualTo(sop.Flags))");
            Assert.That(name,Is.EqualTo(sop.Name), "Assert.That(name,Is.EqualTo(sop.Name))");
            Assert.That(material,Is.EqualTo(sop.Material), "Assert.That(material,Is.EqualTo(sop.Material))");
            Assert.That(pin,Is.EqualTo(sop.ScriptAccessPin), "Assert.That(pin,Is.EqualTo(sop.ScriptAccessPin))");
            Assert.That(textani,Is.EqualTo(sop.TextureAnimation), "Assert.That(textani,Is.EqualTo(sop.TextureAnimation))");
            Assert.That(partsys,Is.EqualTo(sop.ParticleSystem), "Assert.That(partsys,Is.EqualTo(sop.ParticleSystem))");
            Assert.That(expires,Is.EqualTo(sop.Expires), "Assert.That(expires,Is.EqualTo(sop.Expires))");
            Assert.That(rezzed,Is.EqualTo(sop.Rezzed), "Assert.That(rezzed,Is.EqualTo(sop.Rezzed))");
            Assert.That(offset,Is.EqualTo(sop.OffsetPosition), "Assert.That(offset,Is.EqualTo(sop.OffsetPosition))");
            Assert.That(velocity,Is.EqualTo(sop.Velocity), "Assert.That(velocity,Is.EqualTo(sop.Velocity))");
            Assert.That(angvelo,Is.EqualTo(sop.AngularVelocity), "Assert.That(angvelo,Is.EqualTo(sop.AngularVelocity))");
            Assert.That(accel,Is.EqualTo(sop.Acceleration), "Assert.That(accel,Is.EqualTo(sop.Acceleration))");
            Assert.That(description,Is.EqualTo(sop.Description), "Assert.That(description,Is.EqualTo(sop.Description))");
            Assert.That(color,Is.EqualTo(sop.Color), "Assert.That(color,Is.EqualTo(sop.Color))");
            Assert.That(text,Is.EqualTo(sop.Text), "Assert.That(text,Is.EqualTo(sop.Text))");
            Assert.That(sitname,Is.EqualTo(sop.SitName), "Assert.That(sitname,Is.EqualTo(sop.SitName))");
            Assert.That(touchname,Is.EqualTo(sop.TouchName), "Assert.That(touchname,Is.EqualTo(sop.TouchName))");
            Assert.That(linknum,Is.EqualTo(sop.LinkNum), "Assert.That(linknum,Is.EqualTo(sop.LinkNum))");
            Assert.That(clickaction,Is.EqualTo(sop.ClickAction), "Assert.That(clickaction,Is.EqualTo(sop.ClickAction))");
            Assert.That(scale,Is.EqualTo(sop.Scale), "Assert.That(scale,Is.EqualTo(sop.Scale))");

            // This is necessary or object will not be inserted in DB
            sop.Flags = PrimFlags.None;

            // Inserts group in DB
            db.StoreObject(sog,region3);
            List<SceneObjectGroup> sogs = db.LoadObjects(region3);
            Assert.That(sogs.Count, Is.EqualTo(1), "Assert.That(sogs.Count, Is.EqualTo(1))");
            // Makes sure there are no double insertions:
            db.StoreObject(sog,region3);
            sogs = db.LoadObjects(region3);
            Assert.That(sogs.Count, Is.EqualTo(1), "Assert.That(sogs.Count, Is.EqualTo(1))");


            // Tests if the parameters were inserted correctly
            SceneObjectPart p = sogs[0].RootPart;
            Assert.That(regionh,Is.EqualTo(p.RegionHandle), "Assert.That(regionh,Is.EqualTo(p.RegionHandle))");
            //Assert.That(localid,Is.EqualTo(p.LocalId), "Assert.That(localid,Is.EqualTo(p.LocalId))");
            Assert.That(groupos,Is.EqualTo(p.GroupPosition), "Assert.That(groupos,Is.EqualTo(p.GroupPosition))");
            Assert.That(name,Is.EqualTo(p.Name), "Assert.That(name,Is.EqualTo(p.Name))");
            Assert.That(rotoff,Is.EqualTo(p.RotationOffset), "Assert.That(rotoff,Is.EqualTo(p.RotationOffset))");
            Assert.That(uuid,Is.EqualTo(p.UUID), "Assert.That(uuid,Is.EqualTo(p.UUID))");
            Assert.That(creator,Is.EqualTo(p.CreatorID), "Assert.That(creator,Is.EqualTo(p.CreatorID))");
            //Assert.That(iserial,Is.EqualTo(p.InventorySerial), "Assert.That(iserial,Is.EqualTo(p.InventorySerial))");
            Assert.That(dic,Is.EqualTo(p.TaskInventory), "Assert.That(dic,Is.EqualTo(p.TaskInventory))");
            //Assert.That(objf, Is.EqualTo((uint)p.Flags), "Assert.That(objf,Is.EqualTo(p.Flags))");
            Assert.That(name,Is.EqualTo(p.Name), "Assert.That(name,Is.EqualTo(p.Name))");
            Assert.That(material,Is.EqualTo(p.Material), "Assert.That(material,Is.EqualTo(p.Material))");
            Assert.That(pin,Is.EqualTo(p.ScriptAccessPin), "Assert.That(pin,Is.EqualTo(p.ScriptAccessPin))");
            Assert.That(textani,Is.EqualTo(p.TextureAnimation), "Assert.That(textani,Is.EqualTo(p.TextureAnimation))");
            Assert.That(partsys,Is.EqualTo(p.ParticleSystem), "Assert.That(partsys,Is.EqualTo(p.ParticleSystem))");
            //Assert.That(expires,Is.EqualTo(p.Expires), "Assert.That(expires,Is.EqualTo(p.Expires))");
            //Assert.That(rezzed,Is.EqualTo(p.Rezzed), "Assert.That(rezzed,Is.EqualTo(p.Rezzed))");
            Assert.That(offset,Is.EqualTo(p.OffsetPosition), "Assert.That(offset,Is.EqualTo(p.OffsetPosition))");
            Assert.That(velocity,Is.EqualTo(p.Velocity), "Assert.That(velocity,Is.EqualTo(p.Velocity))");
            Assert.That(angvelo,Is.EqualTo(p.AngularVelocity), "Assert.That(angvelo,Is.EqualTo(p.AngularVelocity))");
            Assert.That(accel,Is.EqualTo(p.Acceleration), "Assert.That(accel,Is.EqualTo(p.Acceleration))");
            Assert.That(description,Is.EqualTo(p.Description), "Assert.That(description,Is.EqualTo(p.Description))");
            Assert.That(color,Is.EqualTo(p.Color), "Assert.That(color,Is.EqualTo(p.Color))");
            Assert.That(text,Is.EqualTo(p.Text), "Assert.That(text,Is.EqualTo(p.Text))");
            Assert.That(sitname,Is.EqualTo(p.SitName), "Assert.That(sitname,Is.EqualTo(p.SitName))");
            Assert.That(touchname,Is.EqualTo(p.TouchName), "Assert.That(touchname,Is.EqualTo(p.TouchName))");
            //Assert.That(linknum,Is.EqualTo(p.LinkNum), "Assert.That(linknum,Is.EqualTo(p.LinkNum))");
            Assert.That(clickaction,Is.EqualTo(p.ClickAction), "Assert.That(clickaction,Is.EqualTo(p.ClickAction))");
            Assert.That(scale,Is.EqualTo(p.Scale), "Assert.That(scale,Is.EqualTo(p.Scale))");

            //Assert.That(updatef,Is.EqualTo(p.UpdateFlag), "Assert.That(updatef,Is.EqualTo(p.UpdateFlag))");

            Assert.That(pbshap.PathBegin, Is.EqualTo(p.Shape.PathBegin), "Assert.That(pbshap.PathBegin, Is.EqualTo(p.Shape.PathBegin))");
            Assert.That(pbshap.PathEnd, Is.EqualTo(p.Shape.PathEnd), "Assert.That(pbshap.PathEnd, Is.EqualTo(p.Shape.PathEnd))");
            Assert.That(pbshap.ProfileBegin, Is.EqualTo(p.Shape.ProfileBegin), "Assert.That(pbshap.ProfileBegin, Is.EqualTo(p.Shape.ProfileBegin))");
            Assert.That(pbshap.ProfileEnd, Is.EqualTo(p.Shape.ProfileEnd), "Assert.That(pbshap.ProfileEnd, Is.EqualTo(p.Shape.ProfileEnd))");
            Assert.That(pbshap.ProfileHollow, Is.EqualTo(p.Shape.ProfileHollow), "Assert.That(pbshap.ProfileHollow, Is.EqualTo(p.Shape.ProfileHollow))");
        }

        [Test]
        public void T014_UpdateObject()
        {
            TestHelpers.InMethod();

            string text1 = "object1 text";
            SceneObjectGroup sog = FindSOG("object1", region1);
            sog.RootPart.Text = text1;
            db.StoreObject(sog, region1);

            sog = FindSOG("object1", region1);
            Assert.That(text1, Is.EqualTo(sog.RootPart.Text), "Assert.That(text1, Is.EqualTo(sog.RootPart.Text))");

            // Creates random values
            UUID creator = new UUID();
            creator = UUID.Random();
            TaskInventoryDictionary dic = new TaskInventoryDictionary();
            localID = localID + 1;
            string name = "West  Adam";
            byte material = (byte) random.Next((int)SOPMaterialData.MaxMaterial);
            ulong regionh = (ulong)random.NextDouble() * (ulong)random.Next();
            int pin = random.Next();
            Byte[] partsys = new byte[8];
            Byte[] textani = new byte[8];
            random.NextBytes(textani);
            random.NextBytes(partsys);
            DateTime expires = new DateTime(2010, 12, 20);
            DateTime rezzed = new DateTime(2005, 07, 15);
            Vector3 groupos = new Vector3(random.Next(),random.Next(),random.Next());
            Vector3 offset = new Vector3(random.Next(),random.Next(),random.Next());
            Quaternion rotoff = new Quaternion(random.Next(),random.Next(),random.Next(),random.Next());
            Vector3 velocity = new Vector3(random.Next(),random.Next(),random.Next());
            Vector3 angvelo = new Vector3(random.Next(),random.Next(),random.Next());
            Vector3 accel = new Vector3(random.Next(),random.Next(),random.Next());
            string description = name;
            Color color = Color.FromArgb(255, 255, 255, 0);
            string text = "What You Say?{]\vz~";
            string sitname = RandomName();
            string touchname = RandomName();
            int linknum = random.Next();
            byte clickaction = (byte) random.Next(127);
            PrimitiveBaseShape pbshap = new PrimitiveBaseShape();
            pbshap = PrimitiveBaseShape.Default;
            Vector3 scale = new Vector3(random.Next(),random.Next(),random.Next());

            // Updates the region with new values
            SceneObjectGroup sog2 = FindSOG("Adam  West", region3);
            Assert.That(sog2,Is.Not.Null);
            sog2.RootPart.RegionHandle = regionh;
            sog2.RootPart.Shape = pbshap;
            sog2.RootPart.GroupPosition = groupos;
            sog2.RootPart.RotationOffset = rotoff;
            sog2.RootPart.CreatorID = creator;
            sog2.RootPart.TaskInventory = dic;
            sog2.RootPart.Name = name;
            sog2.RootPart.Material = material;
            sog2.RootPart.ScriptAccessPin = pin;
            sog2.RootPart.TextureAnimation = textani;
            sog2.RootPart.ParticleSystem = partsys;
            sog2.RootPart.Expires = expires;
            sog2.RootPart.Rezzed = rezzed;
            sog2.RootPart.OffsetPosition = offset;
            sog2.RootPart.Velocity = velocity;
            sog2.RootPart.AngularVelocity = angvelo;
            sog2.RootPart.Acceleration = accel;
            sog2.RootPart.Description = description;
            sog2.RootPart.Color = color;
            sog2.RootPart.Text = text;
            sog2.RootPart.SitName = sitname;
            sog2.RootPart.TouchName = touchname;
            sog2.RootPart.LinkNum = linknum;
            sog2.RootPart.ClickAction = clickaction;
            sog2.RootPart.Scale = scale;

            db.StoreObject(sog2, region3);
            List<SceneObjectGroup> sogs = db.LoadObjects(region3);
            Assert.That(sogs.Count, Is.EqualTo(1), "Assert.That(sogs.Count, Is.EqualTo(1))");

            SceneObjectGroup retsog = FindSOG("West  Adam", region3);
            Assert.That(retsog,Is.Not.Null);
            SceneObjectPart p = retsog.RootPart;
            Assert.That(regionh,Is.EqualTo(p.RegionHandle), "Assert.That(regionh,Is.EqualTo(p.RegionHandle))");
            Assert.That(groupos,Is.EqualTo(p.GroupPosition), "Assert.That(groupos,Is.EqualTo(p.GroupPosition))");
            Assert.That(name,Is.EqualTo(p.Name), "Assert.That(name,Is.EqualTo(p.Name))");
            Assert.That(rotoff,Is.EqualTo(p.RotationOffset), "Assert.That(rotoff,Is.EqualTo(p.RotationOffset))");
            Assert.That(creator,Is.EqualTo(p.CreatorID), "Assert.That(creator,Is.EqualTo(p.CreatorID))");
            Assert.That(dic,Is.EqualTo(p.TaskInventory), "Assert.That(dic,Is.EqualTo(p.TaskInventory))");
            Assert.That(name,Is.EqualTo(p.Name), "Assert.That(name,Is.EqualTo(p.Name))");
            Assert.That(material,Is.EqualTo(p.Material), "Assert.That(material,Is.EqualTo(p.Material))");
            Assert.That(pin,Is.EqualTo(p.ScriptAccessPin), "Assert.That(pin,Is.EqualTo(p.ScriptAccessPin))");
            Assert.That(textani,Is.EqualTo(p.TextureAnimation), "Assert.That(textani,Is.EqualTo(p.TextureAnimation))");
            Assert.That(partsys,Is.EqualTo(p.ParticleSystem), "Assert.That(partsys,Is.EqualTo(p.ParticleSystem))");
            Assert.That(offset,Is.EqualTo(p.OffsetPosition), "Assert.That(offset,Is.EqualTo(p.OffsetPosition))");
            Assert.That(velocity,Is.EqualTo(p.Velocity), "Assert.That(velocity,Is.EqualTo(p.Velocity))");
            Assert.That(angvelo,Is.EqualTo(p.AngularVelocity), "Assert.That(angvelo,Is.EqualTo(p.AngularVelocity))");
            Assert.That(accel,Is.EqualTo(p.Acceleration), "Assert.That(accel,Is.EqualTo(p.Acceleration))");
            Assert.That(description,Is.EqualTo(p.Description), "Assert.That(description,Is.EqualTo(p.Description))");
            Assert.That(color,Is.EqualTo(p.Color), "Assert.That(color,Is.EqualTo(p.Color))");
            Assert.That(text,Is.EqualTo(p.Text), "Assert.That(text,Is.EqualTo(p.Text))");
            Assert.That(sitname,Is.EqualTo(p.SitName), "Assert.That(sitname,Is.EqualTo(p.SitName))");
            Assert.That(touchname,Is.EqualTo(p.TouchName), "Assert.That(touchname,Is.EqualTo(p.TouchName))");
            Assert.That(clickaction,Is.EqualTo(p.ClickAction), "Assert.That(clickaction,Is.EqualTo(p.ClickAction))");
            Assert.That(scale,Is.EqualTo(p.Scale), "Assert.That(scale,Is.EqualTo(p.Scale))");
        }

        /// <summary>
        /// Test storage and retrieval of a scene object with a large number of parts.
        /// </summary>
        [Test]
        public void T015_LargeSceneObjects()
        {
            TestHelpers.InMethod();

            UUID id = UUID.Random();
            Dictionary<UUID, SceneObjectPart> mydic = new Dictionary<UUID, SceneObjectPart>();
            SceneObjectGroup sog = NewSOG("Test SOG", id, region4);
            mydic.Add(sog.RootPart.UUID,sog.RootPart);
            for (int i = 0; i < 30; i++)
            {
                UUID tmp = UUID.Random();
                SceneObjectPart sop = NewSOP(("Test SOP " + i.ToString()),tmp);
                Vector3 groupos = new Vector3(random.Next(),random.Next(),random.Next());
                Vector3 offset = new Vector3(random.Next(),random.Next(),random.Next());
                Quaternion rotoff = new Quaternion(random.Next(),random.Next(),random.Next(),random.Next());
                Vector3 velocity = new Vector3(random.Next(),random.Next(),random.Next());
                Vector3 angvelo = new Vector3(random.Next(),random.Next(),random.Next());
                Vector3 accel = new Vector3(random.Next(),random.Next(),random.Next());

                sop.GroupPosition = groupos;
                sop.RotationOffset = rotoff;
                sop.OffsetPosition = offset;
                sop.Velocity = velocity;
                sop.AngularVelocity = angvelo;
                sop.Acceleration = accel;

                mydic.Add(tmp,sop);
                sog.AddPart(sop);
            }

            db.StoreObject(sog, region4);

            SceneObjectGroup retsog = FindSOG("Test SOG", region4);
            SceneObjectPart[] parts = retsog.Parts;
            for (int i = 0; i < 30; i++)
            {
                SceneObjectPart cursop = mydic[parts[i].UUID];
                Assert.That(cursop.GroupPosition,Is.EqualTo(parts[i].GroupPosition), "Assert.That(cursop.GroupPosition,Is.EqualTo(parts[i].GroupPosition))");
                Assert.That(cursop.RotationOffset,Is.EqualTo(parts[i].RotationOffset), "Assert.That(cursop.RotationOffset,Is.EqualTo(parts[i].RotationOffset))");
                Assert.That(cursop.OffsetPosition,Is.EqualTo(parts[i].OffsetPosition), "Assert.That(cursop.OffsetPosition,Is.EqualTo(parts[i].OffsetPosition))");
                Assert.That(cursop.Velocity,Is.EqualTo(parts[i].Velocity), "Assert.That(cursop.Velocity,Is.EqualTo(parts[i].Velocity))");
                Assert.That(cursop.AngularVelocity,Is.EqualTo(parts[i].AngularVelocity), "Assert.That(cursop.AngularVelocity,Is.EqualTo(parts[i].AngularVelocity))");
                Assert.That(cursop.Acceleration,Is.EqualTo(parts[i].Acceleration), "Assert.That(cursop.Acceleration,Is.EqualTo(parts[i].Acceleration))");
            }
        }

        //[Test]
        public void T016_RandomSogWithSceneParts()
        {
            TestHelpers.InMethod();

            PropertyScrambler<SceneObjectPart> scrambler =
                new PropertyScrambler<SceneObjectPart>()
                    .DontScramble(x => x.UUID);
            UUID tmpSog = UUID.Random();
            UUID tmp1 = UUID.Random();
            UUID tmp2 = UUID.Random();
            UUID tmp3 = UUID.Random();
            UUID newregion = UUID.Random();
            SceneObjectPart p1 = new SceneObjectPart();
            SceneObjectPart p2 = new SceneObjectPart();
            SceneObjectPart p3 = new SceneObjectPart();
            p1.Shape = PrimitiveBaseShape.Default;
            p2.Shape = PrimitiveBaseShape.Default;
            p3.Shape = PrimitiveBaseShape.Default;
            p1.UUID = tmp1;
            p2.UUID = tmp2;
            p3.UUID = tmp3;
            scrambler.Scramble(p1);
            scrambler.Scramble(p2);
            scrambler.Scramble(p3);

            SceneObjectGroup sog = NewSOG("Sop 0", tmpSog, newregion);
            PropertyScrambler<SceneObjectGroup> sogScrambler =
                new PropertyScrambler<SceneObjectGroup>()
                    .DontScramble(x => x.UUID);
            sogScrambler.Scramble(sog);
            sog.UUID = tmpSog;
            sog.AddPart(p1);
            sog.AddPart(p2);
            sog.AddPart(p3);

            SceneObjectPart[] parts = sog.Parts;
            Assert.That(parts.Length, Is.EqualTo(4), "Assert.That(parts.Length,Is.EqualTo(4))");

            db.StoreObject(sog, newregion);
            List<SceneObjectGroup> sogs = db.LoadObjects(newregion);
            Assert.That(sogs.Count, Is.EqualTo(1), "Assert.That(sogs.Count,Is.EqualTo(1))");
            SceneObjectGroup newsog = sogs[0];

            SceneObjectPart[] newparts = newsog.Parts;
            Assert.That(newparts.Length, Is.EqualTo(4), "Assert.That(newparts.Length,Is.EqualTo(4))");

            Assert.That(newsog, Constraints.PropertyCompareConstraint(sog)
                .IgnoreProperty(x=>x.LocalId)
                .IgnoreProperty(x=>x.HasGroupChanged)
                .IgnoreProperty(x=>x.IsSelected)
                .IgnoreProperty(x=>x.RegionHandle)
                .IgnoreProperty(x=>x.RegionUUID)
                .IgnoreProperty(x=>x.Scene)
                .IgnoreProperty(x=>x.Parts)
                .IgnoreProperty(x=>x.RootPart));
        }


        private SceneObjectGroup GetMySOG(string name)
        {
            SceneObjectGroup sog = FindSOG(name, region1);
            if (sog == null)
            {
                sog = NewSOG(name, prim1, region1);
                db.StoreObject(sog, region1);
            }
            return sog;
        }

        // NOTE: it is a bad practice to rely on some of the previous tests having been run before.
        // If the tests are run manually, one at a time, each starts with full class init (DB cleared).
        // Even when all tests are run, NUnit 2.5+ no longer guarantee a specific test order.
        // We shouldn't expect to find anything in the DB if we haven't put it there *in the same test*!

        [Test]
        public void T020_PrimInventoryEmpty()
        {
            TestHelpers.InMethod();

            SceneObjectGroup sog = GetMySOG("object1");
            TaskInventoryItem t = sog.GetInventoryItem(sog.RootPart.LocalId, item1);
            Assert.That(t, Is.Null);
        }

        // TODO: Is there any point to call StorePrimInventory on a list, rather than on the prim itself?

        private void StoreInventory(SceneObjectGroup sog)
        {
            List<TaskInventoryItem> list = new List<TaskInventoryItem>();
            // TODO: seriously??? this is the way we need to loop to get this?
            foreach (UUID uuid in sog.RootPart.Inventory.GetInventoryList())
            {
                list.Add(sog.GetInventoryItem(sog.RootPart.LocalId, uuid));
            }

            db.StorePrimInventory(sog.RootPart.UUID, list);
        }

        [Test]
        public void T021_PrimInventoryBasic()
        {
            TestHelpers.InMethod();

            SceneObjectGroup sog = GetMySOG("object1");
            InventoryItemBase i = NewItem(item1, zero, zero, itemname1, zero);

            Assert.That(sog.AddInventoryItem(zero, sog.RootPart.LocalId, i, zero), Is.True);
            TaskInventoryItem t = sog.GetInventoryItem(sog.RootPart.LocalId, item1);
            Assert.That(t.Name, Is.EqualTo(itemname1), "Assert.That(t.Name, Is.EqualTo(itemname1))");

            StoreInventory(sog);

            SceneObjectGroup sog1 = FindSOG("object1", region1);
            Assert.That(sog1, Is.Not.Null);

            TaskInventoryItem t1 = sog1.GetInventoryItem(sog1.RootPart.LocalId, item1);
            Assert.That(t1, Is.Not.Null);
            Assert.That(t1.Name, Is.EqualTo(itemname1), "Assert.That(t.Name, Is.EqualTo(itemname1))");

            // Updating inventory
            t1.Name = "My New Name";
            sog1.UpdateInventoryItem(t1);

            StoreInventory(sog1);

            SceneObjectGroup sog2 = FindSOG("object1", region1);
            TaskInventoryItem t2 = sog2.GetInventoryItem(sog2.RootPart.LocalId, item1);
            Assert.That(t2.Name, Is.EqualTo("My New Name"), "Assert.That(t.Name, Is.EqualTo(\"My New Name\"))");

            // Removing inventory
            List<TaskInventoryItem> list = new List<TaskInventoryItem>();
            db.StorePrimInventory(prim1, list);

            sog = FindSOG("object1", region1);
            t = sog.GetInventoryItem(sog.RootPart.LocalId, item1);
            Assert.That(t, Is.Null);
        }

        [Test]
        public void T025_PrimInventoryPersistency()
        {
            TestHelpers.InMethod();

            InventoryItemBase i = new InventoryItemBase();
            UUID id = UUID.Random();
            i.ID = id;
            UUID folder = UUID.Random();
            i.Folder = folder;
            UUID owner = UUID.Random();
            i.Owner = owner;
            UUID creator = UUID.Random();
            i.CreatorId = creator.ToString();
            string name = RandomName();
            i.Name = name;
            i.Description = name;
            UUID assetid = UUID.Random();
            i.AssetID = assetid;
            int invtype = random.Next();
            i.InvType = invtype;
            uint nextperm = (uint) random.Next();
            i.NextPermissions = nextperm;
            uint curperm = (uint) random.Next();
            i.CurrentPermissions = curperm;
            uint baseperm = (uint) random.Next();
            i.BasePermissions = baseperm;
            uint eoperm = (uint) random.Next();
            i.EveryOnePermissions = eoperm;
            int assettype = random.Next();
            i.AssetType = assettype;
            UUID groupid = UUID.Random();
            i.GroupID = groupid;
            bool groupown = true;
            i.GroupOwned = groupown;
            int saleprice = random.Next();
            i.SalePrice = saleprice;
            byte saletype = (byte) random.Next(127);
            i.SaleType = saletype;
            uint flags = (uint) random.Next();
            i.Flags = flags;
            int creationd = random.Next();
            i.CreationDate = creationd;

            SceneObjectGroup sog = GetMySOG("object1");
            Assert.That(sog.AddInventoryItem(zero, sog.RootPart.LocalId, i, zero), Is.True);
            TaskInventoryItem t = sog.GetInventoryItem(sog.RootPart.LocalId, id);

            Assert.That(t.Name, Is.EqualTo(name), "Assert.That(t.Name, Is.EqualTo(name))");
            Assert.That(t.AssetID,Is.EqualTo(assetid), "Assert.That(t.AssetID,Is.EqualTo(assetid))");
            Assert.That(t.BasePermissions,Is.EqualTo(baseperm), "Assert.That(t.BasePermissions,Is.EqualTo(baseperm))");
            Assert.That(t.CreationDate,Is.EqualTo(creationd), "Assert.That(t.CreationDate,Is.EqualTo(creationd))");
            Assert.That(t.CreatorID,Is.EqualTo(creator), "Assert.That(t.CreatorID,Is.EqualTo(creator))");
            Assert.That(t.Description,Is.EqualTo(name), "Assert.That(t.Description,Is.EqualTo(name))");
            Assert.That(t.EveryonePermissions,Is.EqualTo(eoperm), "Assert.That(t.EveryonePermissions,Is.EqualTo(eoperm))");
            Assert.That(t.Flags,Is.EqualTo(flags), "Assert.That(t.Flags,Is.EqualTo(flags))");
            Assert.That(t.GroupID,Is.EqualTo(sog.RootPart.GroupID), "Assert.That(t.GroupID,Is.EqualTo(sog.RootPart.GroupID))");
            // Where is this group permissions??
            // Assert.That(t.GroupPermissions,Is.EqualTo(), "Assert.That(t.GroupPermissions,Is.EqualTo())");
            Assert.That(t.Type,Is.EqualTo(assettype), "Assert.That(t.Type,Is.EqualTo(assettype))");
            Assert.That(t.InvType, Is.EqualTo(invtype), "Assert.That(t.InvType, Is.EqualTo(invtype))");
            Assert.That(t.ItemID, Is.EqualTo(id), "Assert.That(t.ItemID, Is.EqualTo(id))");
            Assert.That(t.LastOwnerID, Is.EqualTo(sog.RootPart.LastOwnerID), "Assert.That(t.LastOwnerID, Is.EqualTo(sog.RootPart.LastOwnerID))");
            Assert.That(t.NextPermissions, Is.EqualTo(nextperm), "Assert.That(t.NextPermissions, Is.EqualTo(nextperm))");
            // Ownership changes when you drop an object into an object
            // owned by someone else
            Assert.That(t.OwnerID,Is.EqualTo(sog.RootPart.OwnerID), "Assert.That(t.OwnerID,Is.EqualTo(sog.RootPart.OwnerID))");
//            Assert.That(t.CurrentPermissions, Is.EqualTo(curperm | 16), "Assert.That(t.CurrentPermissions, Is.EqualTo(curperm | 8))");
            Assert.That(t.ParentID,Is.EqualTo(sog.RootPart.FolderID), "Assert.That(t.ParentID,Is.EqualTo(sog.RootPart.FolderID))");
            Assert.That(t.ParentPartID,Is.EqualTo(sog.RootPart.UUID), "Assert.That(t.ParentPartID,Is.EqualTo(sog.RootPart.UUID))");
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void T026_PrimInventoryMany()
        {
            TestHelpers.InMethod();

            UUID i1,i2,i3,i4;
            i1 = UUID.Random();
            i2 = UUID.Random();
            i3 = UUID.Random();
            i4 = i3;
            InventoryItemBase ib1 = NewItem(i1, zero, zero, RandomName(), zero);
            InventoryItemBase ib2 = NewItem(i2, zero, zero, RandomName(), zero);
            InventoryItemBase ib3 = NewItem(i3, zero, zero, RandomName(), zero);
            InventoryItemBase ib4 = NewItem(i4, zero, zero, RandomName(), zero);

            SceneObjectGroup sog = FindSOG("object1", region1);

            Assert.That(sog.AddInventoryItem(zero, sog.RootPart.LocalId, ib1, zero), Is.True);
            Assert.That(sog.AddInventoryItem(zero, sog.RootPart.LocalId, ib2, zero), Is.True);
            Assert.That(sog.AddInventoryItem(zero, sog.RootPart.LocalId, ib3, zero), Is.True);
            Assert.That(sog.AddInventoryItem(zero, sog.RootPart.LocalId, ib4, zero), Is.True);

            TaskInventoryItem t1 = sog.GetInventoryItem(sog.RootPart.LocalId, i1);
            Assert.That(t1.Name, Is.EqualTo(ib1.Name), "Assert.That(t1.Name, Is.EqualTo(ib1.Name))");
            TaskInventoryItem t2 = sog.GetInventoryItem(sog.RootPart.LocalId, i2);
            Assert.That(t2.Name, Is.EqualTo(ib2.Name), "Assert.That(t2.Name, Is.EqualTo(ib2.Name))");
            TaskInventoryItem t3 = sog.GetInventoryItem(sog.RootPart.LocalId, i3);
            Assert.That(t3.Name, Is.EqualTo(ib3.Name), "Assert.That(t3.Name, Is.EqualTo(ib3.Name))");
            TaskInventoryItem t4 = sog.GetInventoryItem(sog.RootPart.LocalId, i4);
            Assert.That(t4, Is.Null);
        }

        [Test]
        public void T052_RemoveObject()
        {
            TestHelpers.InMethod();

            db.RemoveObject(prim1, region1);
            SceneObjectGroup sog = FindSOG("object1", region1);
            Assert.That(sog, Is.Null);
        }

        [Test]
        public void T100_DefaultRegionInfo()
        {
            TestHelpers.InMethod();

            RegionSettings r1 = db.LoadRegionSettings(region1);
            Assert.That(r1.RegionUUID, Is.EqualTo(region1), "Assert.That(r1.RegionUUID, Is.EqualTo(region1))");

            RegionSettings r2 = db.LoadRegionSettings(region2);
            Assert.That(r2.RegionUUID, Is.EqualTo(region2), "Assert.That(r2.RegionUUID, Is.EqualTo(region2))");
        }

        [Test]
        public void T101_UpdateRegionInfo()
        {
            TestHelpers.InMethod();

            int agentlimit = random.Next();
            double objectbonus = random.Next();
            int maturity = random.Next();
            UUID tertex1 = UUID.Random();
            UUID tertex2 = UUID.Random();
            UUID tertex3 = UUID.Random();
            UUID tertex4 = UUID.Random();
            double elev1nw = random.Next();
            double elev2nw = random.Next();
            double elev1ne = random.Next();
            double elev2ne = random.Next();
            double elev1se = random.Next();
            double elev2se = random.Next();
            double elev1sw = random.Next();
            double elev2sw = random.Next();
            double waterh = random.Next();
            double terrainraise = random.Next();
            double terrainlower = random.Next();
            Vector3 sunvector = new Vector3((float)Math.Round(random.NextDouble(),5),(float)Math.Round(random.NextDouble(),5),(float)Math.Round(random.NextDouble(),5));
            UUID terimgid = UUID.Random();
            double sunpos = random.Next();
            UUID cov = UUID.Random();

            RegionSettings r1 = db.LoadRegionSettings(region1);
            r1.BlockTerraform = true;
            r1.BlockFly = true;
            r1.AllowDamage = true;
            r1.RestrictPushing = true;
            r1.AllowLandResell = false;
            r1.AllowLandJoinDivide = false;
            r1.BlockShowInSearch = true;
            r1.AgentLimit = agentlimit;
            r1.ObjectBonus = objectbonus;
            r1.Maturity = maturity;
            r1.DisableScripts = true;
            r1.DisableCollisions = true;
            r1.DisablePhysics = true;
            r1.TerrainTexture1 = tertex1;
            r1.TerrainTexture2 = tertex2;
            r1.TerrainTexture3 = tertex3;
            r1.TerrainTexture4 = tertex4;
            r1.Elevation1NW = elev1nw;
            r1.Elevation2NW = elev2nw;
            r1.Elevation1NE = elev1ne;
            r1.Elevation2NE = elev2ne;
            r1.Elevation1SE = elev1se;
            r1.Elevation2SE = elev2se;
            r1.Elevation1SW = elev1sw;
            r1.Elevation2SW = elev2sw;
            r1.WaterHeight = waterh;
            r1.TerrainRaiseLimit = terrainraise;
            r1.TerrainLowerLimit = terrainlower;
            r1.UseEstateSun = false;
            r1.Sandbox = true;
            r1.SunVector = sunvector;
            r1.TerrainImageID = terimgid;
            r1.FixedSun = true;
            r1.SunPosition = sunpos;
            r1.Covenant = cov;

            db.StoreRegionSettings(r1);

            RegionSettings r1a = db.LoadRegionSettings(region1);
            Assert.That(r1a.RegionUUID, Is.EqualTo(region1), "Assert.That(r1a.RegionUUID, Is.EqualTo(region1))");
            Assert.That(r1a.BlockTerraform,Is.True);
            Assert.That(r1a.BlockFly,Is.True);
            Assert.That(r1a.AllowDamage,Is.True);
            Assert.That(r1a.RestrictPushing,Is.True);
            Assert.That(r1a.AllowLandResell,Is.False);
            Assert.That(r1a.AllowLandJoinDivide,Is.False);
            Assert.That(r1a.BlockShowInSearch,Is.True);
            Assert.That(r1a.AgentLimit,Is.EqualTo(agentlimit), "Assert.That(r1a.AgentLimit,Is.EqualTo(agentlimit))");
            Assert.That(r1a.ObjectBonus,Is.EqualTo(objectbonus), "Assert.That(r1a.ObjectBonus,Is.EqualTo(objectbonus))");
            Assert.That(r1a.Maturity,Is.EqualTo(maturity), "Assert.That(r1a.Maturity,Is.EqualTo(maturity))");
            Assert.That(r1a.DisableScripts,Is.True);
            Assert.That(r1a.DisableCollisions,Is.True);
            Assert.That(r1a.DisablePhysics,Is.True);
            Assert.That(r1a.TerrainTexture1,Is.EqualTo(tertex1), "Assert.That(r1a.TerrainTexture1,Is.EqualTo(tertex1))");
            Assert.That(r1a.TerrainTexture2,Is.EqualTo(tertex2), "Assert.That(r1a.TerrainTexture2,Is.EqualTo(tertex2))");
            Assert.That(r1a.TerrainTexture3,Is.EqualTo(tertex3), "Assert.That(r1a.TerrainTexture3,Is.EqualTo(tertex3))");
            Assert.That(r1a.TerrainTexture4,Is.EqualTo(tertex4), "Assert.That(r1a.TerrainTexture4,Is.EqualTo(tertex4))");
            Assert.That(r1a.Elevation1NW,Is.EqualTo(elev1nw), "Assert.That(r1a.Elevation1NW,Is.EqualTo(elev1nw))");
            Assert.That(r1a.Elevation2NW,Is.EqualTo(elev2nw), "Assert.That(r1a.Elevation2NW,Is.EqualTo(elev2nw))");
            Assert.That(r1a.Elevation1NE,Is.EqualTo(elev1ne), "Assert.That(r1a.Elevation1NE,Is.EqualTo(elev1ne))");
            Assert.That(r1a.Elevation2NE,Is.EqualTo(elev2ne), "Assert.That(r1a.Elevation2NE,Is.EqualTo(elev2ne))");
            Assert.That(r1a.Elevation1SE,Is.EqualTo(elev1se), "Assert.That(r1a.Elevation1SE,Is.EqualTo(elev1se))");
            Assert.That(r1a.Elevation2SE,Is.EqualTo(elev2se), "Assert.That(r1a.Elevation2SE,Is.EqualTo(elev2se))");
            Assert.That(r1a.Elevation1SW,Is.EqualTo(elev1sw), "Assert.That(r1a.Elevation1SW,Is.EqualTo(elev1sw))");
            Assert.That(r1a.Elevation2SW,Is.EqualTo(elev2sw), "Assert.That(r1a.Elevation2SW,Is.EqualTo(elev2sw))");
            Assert.That(r1a.WaterHeight,Is.EqualTo(waterh), "Assert.That(r1a.WaterHeight,Is.EqualTo(waterh))");
            Assert.That(r1a.TerrainRaiseLimit,Is.EqualTo(terrainraise), "Assert.That(r1a.TerrainRaiseLimit,Is.EqualTo(terrainraise))");
            Assert.That(r1a.TerrainLowerLimit,Is.EqualTo(terrainlower), "Assert.That(r1a.TerrainLowerLimit,Is.EqualTo(terrainlower))");
            Assert.That(r1a.UseEstateSun,Is.False);
            Assert.That(r1a.Sandbox,Is.True);
            Assert.That(r1a.SunVector,Is.EqualTo(sunvector), "Assert.That(r1a.SunVector,Is.EqualTo(sunvector))");
            //Assert.That(r1a.TerrainImageID,Is.EqualTo(terimgid), "Assert.That(r1a.TerrainImageID,Is.EqualTo(terimgid))");
            Assert.That(r1a.FixedSun,Is.True);
            Assert.That(r1a.SunPosition, Is.EqualTo(sunpos), "Assert.That(r1a.SunPosition, Is.EqualTo(sunpos))");
            Assert.That(r1a.Covenant, Is.EqualTo(cov), "Assert.That(r1a.Covenant, Is.EqualTo(cov))");
        }

        [Test]
        public void T300_NoTerrain()
        {
            TestHelpers.InMethod();

            Assert.That(db.LoadTerrain(zero), Is.Null);
            Assert.That(db.LoadTerrain(region1), Is.Null);
            Assert.That(db.LoadTerrain(region2), Is.Null);
            Assert.That(db.LoadTerrain(UUID.Random()), Is.Null);
        }

        [Test]
        public void T301_CreateTerrain()
        {
            TestHelpers.InMethod();

            double[,] t1 = GenTerrain(height1);
            db.StoreTerrain(t1, region1);

            // store terrain is async
            Thread.Sleep(1000);

            Assert.That(db.LoadTerrain(zero), Is.Null);
            Assert.That(db.LoadTerrain(region1), Is.Not.Null);
            Assert.That(db.LoadTerrain(region2), Is.Null);
            Assert.That(db.LoadTerrain(UUID.Random()), Is.Null);
        }

        [Test]
        public void T302_FetchTerrain()
        {
            TestHelpers.InMethod();

            double[,] baseterrain1 = GenTerrain(height1);
            double[,] baseterrain2 = GenTerrain(height2);
            double[,] t1 = db.LoadTerrain(region1);
            Assert.That(CompareTerrain(t1, baseterrain1), Is.True);
            Assert.That(CompareTerrain(t1, baseterrain2), Is.False);
        }

        [Test]
        public void T303_UpdateTerrain()
        {
            TestHelpers.InMethod();

            double[,] baseterrain1 = GenTerrain(height1);
            double[,] baseterrain2 = GenTerrain(height2);
            db.StoreTerrain(baseterrain2, region1);

            // store terrain is async
            Thread.Sleep(1000);

            double[,] t1 = db.LoadTerrain(region1);
            Assert.That(CompareTerrain(t1, baseterrain1), Is.False);
            Assert.That(CompareTerrain(t1, baseterrain2), Is.True);
        }

        [Test]
        public void T400_EmptyLand()
        {
            TestHelpers.InMethod();

            Assert.That(db.LoadLandObjects(zero).Count, Is.EqualTo(0), "Assert.That(db.LoadLandObjects(zero).Count, Is.EqualTo(0))");
            Assert.That(db.LoadLandObjects(region1).Count, Is.EqualTo(0), "Assert.That(db.LoadLandObjects(region1).Count, Is.EqualTo(0))");
            Assert.That(db.LoadLandObjects(region2).Count, Is.EqualTo(0), "Assert.That(db.LoadLandObjects(region2).Count, Is.EqualTo(0))");
            Assert.That(db.LoadLandObjects(UUID.Random()).Count, Is.EqualTo(0), "Assert.That(db.LoadLandObjects(UUID.Random()).Count, Is.EqualTo(0))");
        }

        // TODO: we should have real land tests, but Land is so
        // intermingled with scene that you can't test it without a
        // valid scene.  That requires some disagregation.


        //************************************************************************************//
        // Extra private methods

        private double[,] GenTerrain(double value)
        {
            double[,] terret = new double[Constants.RegionSize, Constants.RegionSize];
            terret.Initialize();
            for (int x = 0; x < Constants.RegionSize; x++)
                for (int y = 0; y < Constants.RegionSize; y++)
                    terret[x,y] = value;

            return terret;
        }

        private bool CompareTerrain(double[,] one, double[,] two)
        {
            for (int x = 0; x < Constants.RegionSize; x++)
                for (int y = 0; y < Constants.RegionSize; y++)
                    if (one[x,y] != two[x,y])
                        return false;

            return true;
        }

        private SceneObjectGroup FindSOG(string name, UUID r)
        {
            List<SceneObjectGroup> objs = db.LoadObjects(r);
            foreach (SceneObjectGroup sog in objs)
                if (sog.Name == name)
                    return sog;

            return null;
        }

        // This builds a minimalistic Prim, 1 SOG with 1 root SOP.  A
        // common failure case is people adding new fields that aren't
        // initialized, but have non-null db constraints.  We should
        // honestly be passing more and more null things in here.
        //
        // Please note that in Sqlite.BuildPrim there is a commented out inline version
        // of this so you can debug and step through the build process and check the fields
        //
        // Real World Value: Tests for situation where extending a SceneObjectGroup/SceneObjectPart
        //                   causes the application to crash at the database layer because of null values
        //                   in NOT NULL fields
        //
        private SceneObjectGroup NewSOG(string name, UUID uuid, UUID regionId)
        {
            RegionInfo regionInfo = new RegionInfo();
            regionInfo.RegionID = regionId;
            regionInfo.RegionLocX = 0;
            regionInfo.RegionLocY = 0;

            SceneObjectPart sop = new SceneObjectPart();
            sop.Name = name;
            sop.Description = name;
            sop.Text = RandomName();
            sop.SitName = RandomName();
            sop.TouchName = RandomName();
            sop.UUID = uuid;
            sop.Shape = PrimitiveBaseShape.Default;

            SceneObjectGroup sog = new SceneObjectGroup(sop);
//            sog.SetScene(scene);

            return sog;
        }

        private SceneObjectPart NewSOP(string name, UUID uuid)
        {
            SceneObjectPart sop = new SceneObjectPart();
            sop.Name = name;
            sop.Description = name;
            sop.Text = RandomName();
            sop.SitName = RandomName();
            sop.TouchName = RandomName();
            sop.UUID = uuid;
            sop.Shape = PrimitiveBaseShape.Default;
            return sop;
        }

        // These are copied from the Inventory Item tests

        private InventoryItemBase NewItem(UUID id, UUID parent, UUID owner, string name, UUID asset)
        {
            InventoryItemBase i = new InventoryItemBase();
            i.ID = id;
            i.Folder = parent;
            i.Owner = owner;
            i.CreatorId = owner.ToString();
            i.Name = name;
            i.Description = name;
            i.AssetID = asset;
            return i;
        }

        private static string RandomName()
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
//        private InventoryFolderBase NewFolder(UUID id, UUID parent, UUID owner, string name)
//        {
//            InventoryFolderBase f = new InventoryFolderBase();
//            f.ID = id;
//            f.ParentID = parent;
//            f.Owner = owner;
//            f.Name = name;
//            return f;
//        }
    }
}
