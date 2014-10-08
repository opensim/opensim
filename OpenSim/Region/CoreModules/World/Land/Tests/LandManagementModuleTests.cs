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
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.World.Land.Tests
{
    public class LandManagementModuleTests : OpenSimTestCase
    {
        [Test]
        public void TestAddLandObject()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);

            LandManagementModule lmm = new LandManagementModule();
            Scene scene = new SceneHelpers().SetupScene();            
            SceneHelpers.SetupSceneModules(scene, lmm);             
            
            ILandObject lo = new LandObject(userId, false, scene);
            lo.LandData.Name = "lo1";
            lo.SetLandBitmap(
                lo.GetSquareLandBitmap(0, 0, (int)Constants.RegionSize, (int)Constants.RegionSize));
            lo = lmm.AddLandObject(lo);          

            // TODO: Should add asserts to check that land object was added properly.

            // At the moment, this test just makes sure that we can't add a land object that overlaps the areas that
            // the first still holds.
            ILandObject lo2 = new LandObject(userId, false, scene);
            lo2.SetLandBitmap(
                lo2.GetSquareLandBitmap(0, 0, (int)Constants.RegionSize, (int)Constants.RegionSize));
            lo2.LandData.Name = "lo2";
            lo2 = lmm.AddLandObject(lo2);

            {
                ILandObject loAtCoord = lmm.GetLandObject(0, 0);
                Assert.That(loAtCoord.LandData.LocalID, Is.EqualTo(lo.LandData.LocalID));
                Assert.That(loAtCoord.LandData.GlobalID, Is.EqualTo(lo.LandData.GlobalID));                          
            }

            {
                ILandObject loAtCoord = lmm.GetLandObject((int)Constants.RegionSize - 1, ((int)Constants.RegionSize - 1));
                Assert.That(loAtCoord.LandData.LocalID, Is.EqualTo(lo.LandData.LocalID));
                Assert.That(loAtCoord.LandData.GlobalID, Is.EqualTo(lo.LandData.GlobalID));
            }
        }

        /// <summary>
        /// Test parcels on region when no land data exists to be loaded.
        /// </summary>
        [Test]
        public void TestLoadWithNoParcels()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            SceneHelpers sh = new SceneHelpers();
            LandManagementModule lmm = new LandManagementModule();
            Scene scene = sh.SetupScene();            
            SceneHelpers.SetupSceneModules(scene, lmm);   

            scene.loadAllLandObjectsFromStorage(scene.RegionInfo.RegionID);

            ILandObject loAtCoord1 = lmm.GetLandObject(0, 0);
            Assert.That(loAtCoord1.LandData.LocalID, Is.Not.EqualTo(0));
            Assert.That(loAtCoord1.LandData.GlobalID, Is.Not.EqualTo(UUID.Zero));

            ILandObject loAtCoord2 = lmm.GetLandObject((int)Constants.RegionSize - 1, ((int)Constants.RegionSize - 1));
            Assert.That(loAtCoord2.LandData.LocalID, Is.EqualTo(loAtCoord1.LandData.LocalID));
            Assert.That(loAtCoord2.LandData.GlobalID, Is.EqualTo(loAtCoord1.LandData.GlobalID));
        }

        /// <summary>
        /// Test parcels on region when a single parcel already exists but it does not cover the whole region.
        /// </summary>
        [Test]
        public void TestLoadWithSinglePartialCoveringParcel()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);

            SceneHelpers sh = new SceneHelpers();
            LandManagementModule lmm = new LandManagementModule();
            Scene scene = sh.SetupScene();            
            SceneHelpers.SetupSceneModules(scene, lmm);   

            ILandObject originalLo1 = new LandObject(userId, false, scene);
            originalLo1.LandData.Name = "lo1";
            originalLo1.SetLandBitmap(
                originalLo1.GetSquareLandBitmap(0, 0, (int)Constants.RegionSize, (int)Constants.RegionSize / 2));

            sh.SimDataService.StoreLandObject(originalLo1);

            scene.loadAllLandObjectsFromStorage(scene.RegionInfo.RegionID);

            ILandObject loAtCoord1 = lmm.GetLandObject(0, 0);
            Assert.That(loAtCoord1.LandData.Name, Is.EqualTo(originalLo1.LandData.Name));
            Assert.That(loAtCoord1.LandData.GlobalID, Is.EqualTo(originalLo1.LandData.GlobalID));

            ILandObject loAtCoord2 = lmm.GetLandObject((int)Constants.RegionSize - 1, ((int)Constants.RegionSize - 1));
            Assert.That(loAtCoord2.LandData.LocalID, Is.EqualTo(loAtCoord1.LandData.LocalID));
            Assert.That(loAtCoord2.LandData.GlobalID, Is.EqualTo(loAtCoord1.LandData.GlobalID));
        }

        /// <summary>
        /// Test parcels on region when a single parcel already exists but it does not cover the whole region.
        /// </summary>
        [Test]
        public void TestLoadWithMultiplePartialCoveringParcels()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);

            SceneHelpers sh = new SceneHelpers();
            LandManagementModule lmm = new LandManagementModule();
            Scene scene = sh.SetupScene();            
            SceneHelpers.SetupSceneModules(scene, lmm);   

            ILandObject originalLo1 = new LandObject(userId, false, scene);
            originalLo1.LandData.Name = "lo1";
            originalLo1.SetLandBitmap(
                originalLo1.GetSquareLandBitmap(0, 0, (int)Constants.RegionSize, (int)Constants.RegionSize / 2));

            sh.SimDataService.StoreLandObject(originalLo1);

            ILandObject originalLo2 = new LandObject(userId, false, scene);
            originalLo2.LandData.Name = "lo2";
            originalLo2.SetLandBitmap(
                originalLo2.GetSquareLandBitmap(
                0, (int)Constants.RegionSize / 2, (int)Constants.RegionSize, ((int)Constants.RegionSize / 4) * 3));

            sh.SimDataService.StoreLandObject(originalLo2);

            scene.loadAllLandObjectsFromStorage(scene.RegionInfo.RegionID);

            ILandObject loAtCoord1 = lmm.GetLandObject(0, 0);
            Assert.That(loAtCoord1.LandData.Name, Is.EqualTo(originalLo1.LandData.Name));
            Assert.That(loAtCoord1.LandData.GlobalID, Is.EqualTo(originalLo1.LandData.GlobalID));

            ILandObject loAtCoord2 
                = lmm.GetLandObject((int)Constants.RegionSize - 1, (((int)Constants.RegionSize / 4) * 3) - 1);
            Assert.That(loAtCoord2.LandData.Name, Is.EqualTo(originalLo2.LandData.Name));
            Assert.That(loAtCoord2.LandData.GlobalID, Is.EqualTo(originalLo2.LandData.GlobalID));

            ILandObject loAtCoord3 = lmm.GetLandObject((int)Constants.RegionSize - 1, ((int)Constants.RegionSize - 1));
            Assert.That(loAtCoord3.LandData.LocalID, Is.Not.EqualTo(loAtCoord1.LandData.LocalID));
            Assert.That(loAtCoord3.LandData.LocalID, Is.Not.EqualTo(loAtCoord2.LandData.LocalID));
            Assert.That(loAtCoord3.LandData.GlobalID, Is.Not.EqualTo(loAtCoord1.LandData.GlobalID));
            Assert.That(loAtCoord3.LandData.GlobalID, Is.Not.EqualTo(loAtCoord2.LandData.GlobalID));
        }

        /// <summary>
        /// Test parcels on region when whole region is parcelled (which should normally always be the case).
        /// </summary>
        [Test]
        public void TestLoad()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);

            SceneHelpers sh = new SceneHelpers();
            LandManagementModule lmm = new LandManagementModule();
            Scene scene = sh.SetupScene();            
            SceneHelpers.SetupSceneModules(scene, lmm);   

            ILandObject originalLo1 = new LandObject(userId, false, scene);
            originalLo1.LandData.Name = "lo1";
            originalLo1.SetLandBitmap(
                originalLo1.GetSquareLandBitmap(0, 0, (int)Constants.RegionSize, (int)Constants.RegionSize / 2));

            sh.SimDataService.StoreLandObject(originalLo1);

            ILandObject originalLo2 = new LandObject(userId, false, scene);
            originalLo2.LandData.Name = "lo2";
            originalLo2.SetLandBitmap(
                originalLo2.GetSquareLandBitmap(0, (int)Constants.RegionSize / 2, (int)Constants.RegionSize, (int)Constants.RegionSize));

            sh.SimDataService.StoreLandObject(originalLo2);

            scene.loadAllLandObjectsFromStorage(scene.RegionInfo.RegionID);

            {
                ILandObject loAtCoord = lmm.GetLandObject(0, 0);
                Assert.That(loAtCoord.LandData.Name, Is.EqualTo(originalLo1.LandData.Name));
                Assert.That(loAtCoord.LandData.GlobalID, Is.EqualTo(originalLo1.LandData.GlobalID));                          
            }

            {
                ILandObject loAtCoord = lmm.GetLandObject((int)Constants.RegionSize - 1, ((int)Constants.RegionSize - 1));
                Assert.That(loAtCoord.LandData.Name, Is.EqualTo(originalLo2.LandData.Name));
                Assert.That(loAtCoord.LandData.GlobalID, Is.EqualTo(originalLo2.LandData.GlobalID));
            }
        }

        [Test]
        public void TestSubdivide()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);

            LandManagementModule lmm = new LandManagementModule();
            Scene scene = new SceneHelpers().SetupScene();            
            SceneHelpers.SetupSceneModules(scene, lmm);             
            
            ILandObject lo = new LandObject(userId, false, scene);
            lo.LandData.Name = "lo1";
            lo.SetLandBitmap(
                lo.GetSquareLandBitmap(0, 0, (int)Constants.RegionSize, (int)Constants.RegionSize));
            lo = lmm.AddLandObject(lo);          

            lmm.Subdivide(0, 0, LandManagementModule.LandUnit, LandManagementModule.LandUnit, userId);

            {
                ILandObject loAtCoord = lmm.GetLandObject(0, 0);
                Assert.That(loAtCoord.LandData.LocalID, Is.Not.EqualTo(lo.LandData.LocalID));
                Assert.That(loAtCoord.LandData.GlobalID, Is.Not.EqualTo(lo.LandData.GlobalID));                          
            }

            {
                ILandObject loAtCoord = lmm.GetLandObject(LandManagementModule.LandUnit, LandManagementModule.LandUnit);
                Assert.That(loAtCoord.LandData.LocalID, Is.EqualTo(lo.LandData.LocalID));
                Assert.That(loAtCoord.LandData.GlobalID, Is.EqualTo(lo.LandData.GlobalID));
            }
        }
    }
}