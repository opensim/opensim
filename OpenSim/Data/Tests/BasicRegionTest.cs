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
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Modules.World.Land;
using OpenMetaverse;

namespace OpenSim.Data.Tests
{
    public class BasicRegionTest
    {
        public IRegionDataStore db;
        public UUID zero = UUID.Zero;
        public UUID region1;
        public UUID region2;
        public UUID region3;
        public UUID region4;
        public UUID prim1;
        public UUID prim2;
        public UUID prim3;
        public UUID prim4;
        public UUID prim5;
        public UUID prim6;
        public UUID item1;
        public UUID item2;
        public UUID item3;

        public static Random random;        
        
        public string itemname1 = "item1";

        public uint localID;
        
        public double height1;
        public double height2;

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
            region3 = UUID.Random();
            region4 = UUID.Random();
            prim1 = UUID.Random();
            prim2 = UUID.Random();
            prim3 = UUID.Random();
            prim4 = UUID.Random();
            prim5 = UUID.Random();
            prim6 = UUID.Random();
            item1 = UUID.Random();
            item2 = UUID.Random();
            item3 = UUID.Random();
            random = new Random();
            localID = 1;
            height1 = 20;
            height2 = 100;
        }

        // Test Plan
        // Prims
        //  - empty test - 001
        //  - store / retrieve basic prims (most minimal we can make) - 010, 011
        //  - store / retrieve parts in a scenegroup 012
        //  - store a prim with complete information for consistency check 013
        //  - update existing prims, make sure it sticks - 014
        //  - add inventory items to prims make - 015
        //  - remove inventory items make sure it sticks - 016
        //  - remove prim, make sure it sticks - 020

        [Test]
        public void T001_LoadEmpty()
        {
            List<SceneObjectGroup> objs = db.LoadObjects(region1);
            List<SceneObjectGroup> objs3 = db.LoadObjects(region3);
            List<LandData> land = db.LoadLandObjects(region1);

            Assert.That(objs.Count, Is.EqualTo(0));
            Assert.That(objs3.Count, Is.EqualTo(0));
            Assert.That(land.Count, Is.EqualTo(0));
        }
        
        // SOG round trips
        //  * store objects, make sure they save
        //  * update 

        [Test]
        public void T010_StoreSimpleObject()
        {
            SceneObjectGroup sog = NewSOG("object1", prim1);
            SceneObjectGroup sog2 = NewSOG("object2", prim2);

            // in case the objects don't store
            try 
            {
                db.StoreObject(sog, region1);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Exception in storing object {0} {1}", sog.ToString(), e);
                Assert.Fail();
            }
                    
            try 
            {
                db.StoreObject(sog2, region1);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Exception in storing object {0} {1}", sog2.ToString(), e);
                Assert.Fail();
            }

            // This tests the ADO.NET driver
            List<SceneObjectGroup> objs = db.LoadObjects(region1);
            
            Assert.That(objs.Count, Is.EqualTo(2));
        }
        
        [Test]
        public void T011_ObjectNames()
        {
            List<SceneObjectGroup> objs = db.LoadObjects(region1);
            foreach (SceneObjectGroup sog in objs)
            {
                SceneObjectPart p = sog.RootPart;
                Assert.That("", Is.Not.EqualTo(p.Name));
                Assert.That(p.Name, Is.EqualTo(p.Description));
            }
        }
        
        [Test]
        public void T012_SceneParts()
        {
            UUID tmp0 = UUID.Random();
            UUID tmp1 = UUID.Random();
            UUID tmp2 = UUID.Random();
            UUID tmp3 = UUID.Random();            
            UUID newregion = UUID.Random();
            SceneObjectPart p1 = NewSOP("SoP 1",tmp1);
            SceneObjectPart p2 = NewSOP("SoP 2",tmp2);
            SceneObjectPart p3 = NewSOP("SoP 3",tmp3);
            SceneObjectGroup sog = NewSOG("Sop 0",tmp0);
            sog.AddPart(p1);
            sog.AddPart(p2);
            sog.AddPart(p3);
            
            Console.WriteLine("Test 10 has prims {0} and {1} in region {2}",prim1,prim2,region1);
            Console.WriteLine("The prims are {0}, {1}, {2} and {3} and region is {4}",tmp0,tmp1,tmp2,tmp3,newregion);
            SceneObjectPart[] parts = sog.GetParts();
            Console.WriteLine("Before Insertion:");
            Assert.That(parts.Length,Is.EqualTo(4));
            Console.WriteLine("PASSED BEFORE");
            
            db.StoreObject(sog, newregion);
            List<SceneObjectGroup> sogs = db.LoadObjects(newregion);
            Assert.That(sogs.Count,Is.EqualTo(1));
            SceneObjectGroup newsog = sogs[0];
                        
            SceneObjectPart[] newparts = newsog.GetParts();
            Console.WriteLine("After Insertion:");
            Assert.That(newparts.Length,Is.EqualTo(4));
            Console.WriteLine("PASSED AFTER!");            
            
            Assert.That(newsog.HasChildPrim(tmp0));
            Assert.That(newsog.HasChildPrim(tmp1));
            Assert.That(newsog.HasChildPrim(tmp2));
            Assert.That(newsog.HasChildPrim(tmp3));
        }
        
        [Test]
        [Ignore("Make sure 12 works first")]
        public void T013_ObjectConsistency()
        {
            UUID creator,uuid = new UUID();
            creator = UUID.Random();
            uint iserial = (uint) random.Next();
            TaskInventoryDictionary dic = new TaskInventoryDictionary();
            uint objf = (uint) random.Next();
            uuid = prim4;
            uint localid = localID+1;
            localID = localID + 1;
            string name = "Adam  West";
            byte material = (byte) random.Next(255);
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
            //Color color = Color.Brown;
            Color color = Color.FromArgb(255, 165, 42, 42);
            string text = "All Your Base Are Belong to Us";
            string sitname = "SitName";
            string touchname = "TouchName";
            int linknum = random.Next();
            byte clickaction = (byte) random.Next(255);
            PrimitiveBaseShape pbshap = new PrimitiveBaseShape();
            pbshap = PrimitiveBaseShape.Default;
            Vector3 scale = new Vector3(random.Next(),random.Next(),random.Next());
            byte updatef = (byte) random.Next(255);
            
            SceneObjectPart sop = new SceneObjectPart();
            sop.RegionHandle = regionh;
            sop.UUID = uuid;
            sop.LocalId = localid;
            sop.Shape = pbshap;
            sop.GroupPosition = groupos;
            sop.RotationOffset = rotoff;
            sop.CreatorID = creator;            
            sop.InventorySerial = iserial;
            sop.TaskInventory = dic;
            sop.ObjectFlags = objf;
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
            sop.UpdateFlag = updatef;

            //Tests if local part accepted the parameters:
            Console.WriteLine("Test -0");
            Assert.That(regionh,Is.EqualTo(sop.RegionHandle));
            Console.WriteLine("Test -1  localid é: {0} e LocalId é {1}",localid,sop.LocalId);
            Assert.That(localid,Is.EqualTo(sop.LocalId));
            Console.WriteLine("Test -2");
            //**Assert.That(pbshap,Is.EqualTo(sop.Shape));
            Assert.That(groupos,Is.EqualTo(sop.GroupPosition));
            Console.WriteLine("Test -3");
            Assert.That(name,Is.EqualTo(sop.Name));
            Console.WriteLine("Test -4");
            Assert.That(rotoff,Is.EqualTo(sop.RotationOffset));
            Console.WriteLine("Test 0 - uuid is {0}",uuid);
            Assert.That(uuid,Is.EqualTo(sop.UUID));
            Console.WriteLine("Test 1");
            Assert.That(creator,Is.EqualTo(sop.CreatorID));
            Console.WriteLine("Test 2 - iserial is {0}",iserial);
            Assert.That(iserial,Is.EqualTo(sop.InventorySerial));
            Console.WriteLine("Test 3");
            Assert.That(dic,Is.EqualTo(sop.TaskInventory));
            Console.WriteLine("Test 4");
            Assert.That(objf,Is.EqualTo(sop.ObjectFlags));
            Console.WriteLine("Test 5");
            Assert.That(name,Is.EqualTo(sop.Name));
            Console.WriteLine("Test 6");
            Assert.That(material,Is.EqualTo(sop.Material));
            Console.WriteLine("Test 7");
            Assert.That(pin,Is.EqualTo(sop.ScriptAccessPin));
            Console.WriteLine("Test 8");
            Assert.That(textani,Is.EqualTo(sop.TextureAnimation));
            Console.WriteLine("Test 9");
            Assert.That(partsys,Is.EqualTo(sop.ParticleSystem));
            Console.WriteLine("Test 9.1");
            Assert.That(expires,Is.EqualTo(sop.Expires));
            Console.WriteLine("Test 9.2");
            Assert.That(rezzed,Is.EqualTo(sop.Rezzed));
            Console.WriteLine("Test 10");            
            Assert.That(offset,Is.EqualTo(sop.OffsetPosition));
            Assert.That(velocity,Is.EqualTo(sop.Velocity));                       
            Console.WriteLine("Test 12");            
            Assert.That(angvelo,Is.EqualTo(sop.AngularVelocity));
            Console.WriteLine("Test 13");            
            Assert.That(accel,Is.EqualTo(sop.Acceleration));
            Console.WriteLine("Test 14");
            Assert.That(description,Is.EqualTo(sop.Description));
            Assert.That(color,Is.EqualTo(sop.Color));
            Assert.That(text,Is.EqualTo(sop.Text));
            Assert.That(sitname,Is.EqualTo(sop.SitName));
            Console.WriteLine("Test 15");
            Assert.That(touchname,Is.EqualTo(sop.TouchName));
            Console.WriteLine("Test 16");
            Assert.That(linknum,Is.EqualTo(sop.LinkNum));
            Console.WriteLine("Test 17");
            Assert.That(clickaction,Is.EqualTo(sop.ClickAction));
            Console.WriteLine("Test 18");
            Assert.That(scale,Is.EqualTo(sop.Scale));
            Console.WriteLine("Test 19");
            Assert.That(updatef,Is.EqualTo(sop.UpdateFlag));
            Console.WriteLine("Test 20");
            
            // This is necessary or object will not be inserted in DB            
            sop.ObjectFlags = 0;            
                        
            SceneObjectGroup sog = new SceneObjectGroup();
            sog.AddPart(sop);
            sog.RootPart = sop;            
            
            // Inserts group in DB
            db.StoreObject(sog,region3);
            List<SceneObjectGroup> sogs = db.LoadObjects(region3);
            Assert.That(sogs.Count, Is.EqualTo(1));
            // Makes sure there are no double insertions:
            /*
            db.StoreObject(sog,region3);
            sogs = db.LoadObjects(region3);
            Assert.That(sogs.Count, Is.EqualTo(1));            
            */            

            // Tests if the parameters were inserted correctly
            SceneObjectPart p = sogs[0].RootPart;               
            Console.WriteLine("Test -0");
            Assert.That(regionh,Is.EqualTo(p.RegionHandle));
            Console.WriteLine("Test -1  localid é: {0} e LocalId é {1}",localid,p.LocalId);
            //Assert.That(localid,Is.EqualTo(p.LocalId));
            Console.WriteLine("Test -2");
            //Assert.That(pbshap,Is.EqualTo(p.Shape));
            Assert.That(groupos,Is.EqualTo(p.GroupPosition));
            Console.WriteLine("Test -3");
            Assert.That(name,Is.EqualTo(p.Name));
            Console.WriteLine("Test -4");
            Assert.That(rotoff,Is.EqualTo(p.RotationOffset));
            Console.WriteLine("Test 0 - uuid is {0}",uuid);
            Assert.That(uuid,Is.EqualTo(p.UUID));
            Console.WriteLine("Test 1");
            Assert.That(creator,Is.EqualTo(p.CreatorID));
            Console.WriteLine("Test 2 - iserial is {0}",iserial);
            //Assert.That(iserial,Is.EqualTo(p.InventorySerial));
            Console.WriteLine("Test 3");
            Assert.That(dic,Is.EqualTo(p.TaskInventory));
            Console.WriteLine("Test 4");
            //Assert.That(objf,Is.EqualTo(p.ObjectFlags));
            Console.WriteLine("Test 5");
            Assert.That(name,Is.EqualTo(p.Name));
            Console.WriteLine("Test 6");
            Assert.That(material,Is.EqualTo(p.Material));
            Console.WriteLine("Test 7");
            Assert.That(pin,Is.EqualTo(p.ScriptAccessPin));
            Console.WriteLine("Test 8");
            Assert.That(textani,Is.EqualTo(p.TextureAnimation));
            Console.WriteLine("Test 9");
            Assert.That(partsys,Is.EqualTo(p.ParticleSystem));
            Console.WriteLine("Test 9.1 - Expires in {0}",expires);
            //Assert.That(expires,Is.EqualTo(p.Expires));
            Console.WriteLine("Test 9.2 - Rezzed in {0}",rezzed);
            //Assert.That(rezzed,Is.EqualTo(p.Rezzed));
            Console.WriteLine("Test 10");            
            Assert.That(offset,Is.EqualTo(p.OffsetPosition));
            Assert.That(velocity,Is.EqualTo(p.Velocity));
            Console.WriteLine("Test 12");            
            Assert.That(angvelo,Is.EqualTo(p.AngularVelocity));
            Console.WriteLine("Test 13");            
            Assert.That(accel,Is.EqualTo(p.Acceleration));
            Console.WriteLine("Test 14");
            Assert.That(description,Is.EqualTo(p.Description));
            Assert.That(color,Is.EqualTo(p.Color));
            Assert.That(text,Is.EqualTo(p.Text));
            Assert.That(sitname,Is.EqualTo(p.SitName));
            Console.WriteLine("Test 15");
            Assert.That(touchname,Is.EqualTo(p.TouchName));
            Console.WriteLine("Test 16");
            //Assert.That(linknum,Is.EqualTo(p.LinkNum));
            Console.WriteLine("Test 17");
            Assert.That(clickaction,Is.EqualTo(p.ClickAction));
            Console.WriteLine("Test 18");
            Assert.That(scale,Is.EqualTo(p.Scale));
            Console.WriteLine("Test 19");
            //Assert.That(updatef,Is.EqualTo(p.UpdateFlag));
            Console.WriteLine("Test 20");
            
        }
        
        [Test]
        public void T014_UpdateObject()
        {
            string text = "object1 text";
            SceneObjectGroup sog = FindSOG("object1", region1);
            sog.RootPart.Text = text;
            db.StoreObject(sog, region1);

            sog = FindSOG("object1", region1);
            Assert.That(text, Is.EqualTo(sog.RootPart.Text));
        }

        [Test]
        public void T020_PrimInventoryEmpty()
        {
            SceneObjectGroup sog = FindSOG("object1", region1);
            TaskInventoryItem t = sog.GetInventoryItem(sog.RootPart.LocalId, item1);
            Assert.That(t, Is.Null);
        }

        [Test]
        public void T021_PrimInventoryStore()
        {
            SceneObjectGroup sog = FindSOG("object1", region1);
            InventoryItemBase i = NewItem(item1, zero, zero, itemname1, zero);

            Assert.That(sog.AddInventoryItem(null, sog.RootPart.LocalId, i, zero), Is.True);
            TaskInventoryItem t = sog.GetInventoryItem(sog.RootPart.LocalId, item1);
            Assert.That(t.Name, Is.EqualTo(itemname1));
            
            // TODO: seriously??? this is the way we need to loop to get this?

            List<TaskInventoryItem> list = new List<TaskInventoryItem>();
            foreach (UUID uuid in sog.RootPart.GetInventoryList())
            {
                list.Add(sog.GetInventoryItem(sog.RootPart.LocalId, uuid));
            }
            
            db.StorePrimInventory(prim1, list);
        }

        [Test]
        public void T022_PrimInventoryRetrieve()
        {
            SceneObjectGroup sog = FindSOG("object1", region1);
            TaskInventoryItem t = sog.GetInventoryItem(sog.RootPart.LocalId, item1);

            Assert.That(t.Name, Is.EqualTo(itemname1));
        }

        [Test]
        public void T022_PrimInvetoryRemove()
        {
            List<TaskInventoryItem> list = new List<TaskInventoryItem>();
            db.StorePrimInventory(prim1, list);

            SceneObjectGroup sog = FindSOG("object1", region1);
            TaskInventoryItem t = sog.GetInventoryItem(sog.RootPart.LocalId, item1);
            Assert.That(t, Is.Null);
        }

        [Test]
        public void T051_RemoveObjectWrongRegion()
        {
            db.RemoveObject(prim1, UUID.Random());
            SceneObjectGroup sog = FindSOG("object1", region1);
            Assert.That(sog, Is.Not.Null);
        }

        [Test]
        public void T052_RemoveObject()
        {
            db.RemoveObject(prim1, region1);
            SceneObjectGroup sog = FindSOG("object1", region1);
            Assert.That(sog, Is.Null);
        }


        [Test]
        public void T100_DefaultRegionInfo()
        {
            RegionSettings r1 = db.LoadRegionSettings(region1);
            Assert.That(r1.RegionUUID, Is.EqualTo(region1));

            RegionSettings r2 = db.LoadRegionSettings(region2);
            Assert.That(r2.RegionUUID, Is.EqualTo(region2));
        }

        [Test]
        public void T101_UpdateRegionInfo()
        {
            bool blockfly = true;
            double sunpos = 0.5;
            UUID cov = UUID.Random();

            RegionSettings r1 = db.LoadRegionSettings(region1);
            r1.BlockFly = blockfly;
            r1.SunPosition = sunpos;
            r1.Covenant = cov;
            db.StoreRegionSettings(r1);
            
            RegionSettings r2 = db.LoadRegionSettings(region1);
            Assert.That(r2.RegionUUID, Is.EqualTo(region1));
            Assert.That(r2.SunPosition, Is.EqualTo(sunpos));
            Assert.That(r2.BlockFly, Is.EqualTo(blockfly));
            Assert.That(r2.Covenant, Is.EqualTo(cov));
        }

        [Test]
        public void T300_NoTerrain()
        {
            Assert.That(db.LoadTerrain(zero), Is.Null);
            Assert.That(db.LoadTerrain(region1), Is.Null);
            Assert.That(db.LoadTerrain(region2), Is.Null);
            Assert.That(db.LoadTerrain(UUID.Random()), Is.Null);
        }

        [Test]
        public void T301_CreateTerrain()
        {
            double[,] t1 = GenTerrain(height1);
            db.StoreTerrain(t1, region1);
            
            Assert.That(db.LoadTerrain(zero), Is.Null);
            Assert.That(db.LoadTerrain(region1), Is.Not.Null);
            Assert.That(db.LoadTerrain(region2), Is.Null);
            Assert.That(db.LoadTerrain(UUID.Random()), Is.Null);
        }

        [Test]
        public void T302_FetchTerrain()
        {
            double[,] baseterrain1 = GenTerrain(height1);
            double[,] baseterrain2 = GenTerrain(height2);
            double[,] t1 = db.LoadTerrain(region1);
            Assert.That(CompareTerrain(t1, baseterrain1), Is.True);
            Assert.That(CompareTerrain(t1, baseterrain2), Is.False);
        }

        [Test]
        public void T303_UpdateTerrain()
        {
            double[,] baseterrain1 = GenTerrain(height1);
            double[,] baseterrain2 = GenTerrain(height2);
            db.StoreTerrain(baseterrain2, region1);

            double[,] t1 = db.LoadTerrain(region1);
            Assert.That(CompareTerrain(t1, baseterrain1), Is.False);
            Assert.That(CompareTerrain(t1, baseterrain2), Is.True);
        }

        [Test]
        public void T400_EmptyLand()
        {
            Assert.That(db.LoadLandObjects(zero).Count, Is.EqualTo(0));
            Assert.That(db.LoadLandObjects(region1).Count, Is.EqualTo(0));
            Assert.That(db.LoadLandObjects(region2).Count, Is.EqualTo(0));
            Assert.That(db.LoadLandObjects(UUID.Random()).Count, Is.EqualTo(0));
        }

        // TODO: we should have real land tests, but Land is so
        // intermingled with scene that you can't test it without a
        // valid scene.  That requires some disagregation.


        //************************************************************************************//
        // Extra private methods

        private double[,] GenTerrain(double value)
        {
            double[,] terret = new double[256,256];
            terret.Initialize();
            for (int x = 0; x < 256; x++) 
                for (int y = 0; y < 256; y++)
                    terret[x,y] = value;
            
            return terret;
        }
        
        private bool CompareTerrain(double[,] one, double[,] two)
        {
            for (int x = 0; x < 256; x++) 
                for (int y = 0; y < 256; y++)
                    if (one[x,y] != two[x,y]) 
                        return false;

            return true;
        }


        private SceneObjectGroup FindSOG(string name, UUID r)
        {
            List<SceneObjectGroup> objs = db.LoadObjects(r);
            foreach (SceneObjectGroup sog in objs)
            {
                SceneObjectPart p = sog.RootPart;
                if (p.Name == name) {
                    return sog;
                }
            }
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
        private SceneObjectGroup NewSOG(string name, UUID uuid)
        {
            SceneObjectPart sop = new SceneObjectPart();
            //sop.LocalId = 1;
            sop.LocalId = localID;
            localID = localID + 1;
            sop.Name = name;
            sop.Description = name;
            sop.Text = RandomName();
            sop.SitName = RandomName();
            sop.TouchName = RandomName();
            sop.UUID = uuid;
            sop.Shape = PrimitiveBaseShape.Default;
            

            SceneObjectGroup sog = new SceneObjectGroup();
            sog.AddPart(sop);
            sog.RootPart = sop; 
            
            return sog;
        }
        
        private SceneObjectPart NewSOP(string name, UUID uuid)
        {
            SceneObjectPart sop = new SceneObjectPart();           
            //sop.LocalId = 1;
            sop.LocalId = localID;
            localID = localID + 1;
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
            i.Creator = owner;
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