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
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenSim.Framework;
using OpenSim.Data;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenMetaverse;

namespace OpenSim.Data.Tests
{
    public class BasicRegionTest
    {
        public IRegionDataStore db;
        public UUID region1;
        public UUID region2;
        public UUID prim1;
        public UUID prim2;
        public UUID prim3;
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
            prim1 = UUID.Random();
            prim2 = UUID.Random();
            prim3 = UUID.Random();
            height1 = 20;
            height2 = 100;
        }

        // Test Plan
        // Prims
        //  - empty test - 001
        //  - store / retrieve basic prims (most minimal we can make) - 010, 011
        //  - update existing prims, make sure it sticks - 012
        //  - add inventory items to prims make - 013
        //  - remove inventory items make sure it sticks - 014
        //  - remove prim, make sure it sticks - 020

        [Test]
        public void T001_LoadEmpty()
        {
            List<SceneObjectGroup> objs = db.LoadObjects(region1);
            List<LandData> land = db.LoadLandObjects(region1);

            Assert.That(objs.Count, Is.EqualTo(0));
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
        public void T012_UpdateObject()
        {
            string text = "object1 text";
            SceneObjectGroup sog = FindSOG("object1", region1);
            sog.RootPart.Text = text;
            db.StoreObject(sog, region1);

            sog = FindSOG("object1", region1);
            Assert.That(text, Is.EqualTo(sog.RootPart.Text));
        }

        [Test]
        public void T013_PrimInventory()
        {
            
        }

        [Test]
        public void T021_RemoveObjectWrongRegion()
        {
            db.RemoveObject(prim1, UUID.Random());
            SceneObjectGroup sog = FindSOG("object1", region1);
            Assert.That(sog, Is.Not.Null);
        }

        [Test]
        public void T022_RemoveObject()
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
            db.LoadTerrain(region1);
        }

        [Test]
        public void T301_CreateTerrain()
        {
            double[,] t1 = GenTerrain(height1);
            db.StoreTerrain(t1, region1);
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
            sop.LocalId = 1;
            sop.Name = name;
            sop.Description = name;
            sop.Text = "";
            sop.SitName = "";
            sop.TouchName = "";
            sop.UUID = uuid;
            sop.Shape = PrimitiveBaseShape.Default;

            SceneObjectGroup sog = new SceneObjectGroup();
            sog.AddPart(sop);
            sog.RootPart = sop; 
            
            return sog;
        }

    }
}