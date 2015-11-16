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
using System.Reflection;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;
using log4net;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    [TestFixture]
    public class SceneObjectLinkingTests : OpenSimTestCase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Links to self should be ignored.
        /// </summary>
        [Test]
        public void TestLinkToSelf()
        {
            TestHelpers.InMethod();

            UUID ownerId = TestHelpers.ParseTail(0x1);
            int nParts = 3;

            TestScene scene = new SceneHelpers().SetupScene();
            SceneObjectGroup sog1 = SceneHelpers.CreateSceneObject(nParts, ownerId, "TestLinkToSelf_", 0x10);
            scene.AddSceneObject(sog1);
            scene.LinkObjects(ownerId, sog1.LocalId, new List<uint>() { sog1.Parts[1].LocalId });
//            sog1.LinkToGroup(sog1);

            Assert.That(sog1.Parts.Length, Is.EqualTo(nParts));
        }

        [Test]
        public void TestLinkDelink2SceneObjects()
        {
            TestHelpers.InMethod();
            
            bool debugtest = false; 

            Scene scene = new SceneHelpers().SetupScene();
            SceneObjectGroup grp1 = SceneHelpers.AddSceneObject(scene);
            SceneObjectPart part1 = grp1.RootPart;
            SceneObjectGroup grp2 = SceneHelpers.AddSceneObject(scene);
            SceneObjectPart part2 = grp2.RootPart;

            grp1.AbsolutePosition = new Vector3(10, 10, 10);
            grp2.AbsolutePosition = Vector3.Zero;

            // <90,0,0>
//            grp1.UpdateGroupRotationR(Quaternion.CreateFromEulers(90 * Utils.DEG_TO_RAD, 0, 0));

            // <180,0,0>
            grp2.UpdateGroupRotationR(Quaternion.CreateFromEulers(180 * Utils.DEG_TO_RAD, 0, 0));
            
            // Required for linking
            grp1.RootPart.ClearUpdateSchedule();
            grp2.RootPart.ClearUpdateSchedule();

            // Link grp2 to grp1.   part2 becomes child prim to grp1. grp2 is eliminated.
            Assert.IsFalse(grp1.GroupContainsForeignPrims);
            grp1.LinkToGroup(grp2);
            Assert.IsTrue(grp1.GroupContainsForeignPrims);

            scene.Backup(true);
            Assert.IsFalse(grp1.GroupContainsForeignPrims);

            // FIXME: Can't do this test yet since group 2 still has its root part!  We can't yet null this since
            // it might cause SOG.ProcessBackup() to fail due to the race condition.  This really needs to be fixed.
            Assert.That(grp2.IsDeleted, "SOG 2 was not registered as deleted after link.");
            Assert.That(grp2.Parts.Length, Is.EqualTo(0), "Group 2 still contained children after delink.");
            Assert.That(grp1.Parts.Length == 2);

            if (debugtest)
            {
                m_log.Debug("parts: " + grp1.Parts.Length);
                m_log.Debug("Group1: Pos:"+grp1.AbsolutePosition+", Rot:"+grp1.GroupRotation);
                m_log.Debug("Group1: Prim1: OffsetPosition:"+ part1.OffsetPosition+", OffsetRotation:"+part1.RotationOffset);
                m_log.Debug("Group1: Prim2: OffsetPosition:"+part2.OffsetPosition+", OffsetRotation:"+part2.RotationOffset);
            }

            // root part should have no offset position or rotation
            Assert.That(part1.OffsetPosition == Vector3.Zero && part1.RotationOffset == Quaternion.Identity, 
                "root part should have no offset position or rotation");

            // offset position should be root part position - part2.absolute position.
            Assert.That(part2.OffsetPosition == new Vector3(-10, -10, -10),
                "offset position should be root part position - part2.absolute position.");

            float roll = 0;
            float pitch = 0;
            float yaw = 0;

            // There's a euler anomoly at 180, 0, 0 so expect 180 to turn into -180.
            part1.RotationOffset.GetEulerAngles(out roll, out pitch, out yaw);
            Vector3 rotEuler1 = new Vector3(roll * Utils.RAD_TO_DEG, pitch * Utils.RAD_TO_DEG, yaw * Utils.RAD_TO_DEG);
            
            if (debugtest)
                m_log.Debug(rotEuler1);

            part2.RotationOffset.GetEulerAngles(out roll, out pitch, out yaw);
            Vector3 rotEuler2 = new Vector3(roll * Utils.RAD_TO_DEG, pitch * Utils.RAD_TO_DEG, yaw * Utils.RAD_TO_DEG);
             
            if (debugtest)
                m_log.Debug(rotEuler2);

            Assert.That(rotEuler2.ApproxEquals(new Vector3(-180, 0, 0), 0.001f) || rotEuler2.ApproxEquals(new Vector3(180, 0, 0), 0.001f),
                "Not exactly sure what this is asserting...");

            // Delink part 2
            SceneObjectGroup grp3 = grp1.DelinkFromGroup(part2.LocalId);

            if (debugtest)
                m_log.Debug("Group2: Prim2: OffsetPosition:" + part2.AbsolutePosition + ", OffsetRotation:" + part2.RotationOffset);

            Assert.That(grp1.Parts.Length, Is.EqualTo(1), "Group 1 still contained part2 after delink.");
            Assert.That(part2.AbsolutePosition == Vector3.Zero, "The absolute position should be zero");
            Assert.NotNull(grp3);
        }

        [Test]
        public void TestLinkDelink2groups4SceneObjects()
        {
            TestHelpers.InMethod();
            
            bool debugtest = false;

            Scene scene = new SceneHelpers().SetupScene();
            SceneObjectGroup grp1 = SceneHelpers.AddSceneObject(scene);
            SceneObjectPart part1 = grp1.RootPart;
            SceneObjectGroup grp2 = SceneHelpers.AddSceneObject(scene);
            SceneObjectPart part2 = grp2.RootPart;
            SceneObjectGroup grp3 = SceneHelpers.AddSceneObject(scene);
            SceneObjectPart part3 = grp3.RootPart;
            SceneObjectGroup grp4 = SceneHelpers.AddSceneObject(scene);
            SceneObjectPart part4 = grp4.RootPart;

            grp1.AbsolutePosition = new Vector3(10, 10, 10);
            grp2.AbsolutePosition = Vector3.Zero;
            grp3.AbsolutePosition = new Vector3(20, 20, 20);
            grp4.AbsolutePosition = new Vector3(40, 40, 40);

            // <90,0,0>
//            grp1.UpdateGroupRotationR(Quaternion.CreateFromEulers(90 * Utils.DEG_TO_RAD, 0, 0));

            // <180,0,0>
            grp2.UpdateGroupRotationR(Quaternion.CreateFromEulers(180 * Utils.DEG_TO_RAD, 0, 0));

            // <270,0,0>
//            grp3.UpdateGroupRotationR(Quaternion.CreateFromEulers(270 * Utils.DEG_TO_RAD, 0, 0));

            // <0,90,0>
            grp4.UpdateGroupRotationR(Quaternion.CreateFromEulers(0, 90 * Utils.DEG_TO_RAD, 0));

            // Required for linking
            grp1.RootPart.ClearUpdateSchedule();
            grp2.RootPart.ClearUpdateSchedule();
            grp3.RootPart.ClearUpdateSchedule();
            grp4.RootPart.ClearUpdateSchedule();

            // Link grp2 to grp1.   part2 becomes child prim to grp1. grp2 is eliminated.
            grp1.LinkToGroup(grp2);

            // Link grp4 to grp3.
            grp3.LinkToGroup(grp4);
            
            // At this point we should have 4 parts total in two groups.
            Assert.That(grp1.Parts.Length == 2, "Group1 children count should be 2");
            Assert.That(grp2.IsDeleted, "Group 2 was not registered as deleted after link.");
            Assert.That(grp2.Parts.Length, Is.EqualTo(0), "Group 2 still contained parts after delink.");
            Assert.That(grp3.Parts.Length == 2, "Group3 children count should be 2");
            Assert.That(grp4.IsDeleted, "Group 4 was not registered as deleted after link.");
            Assert.That(grp4.Parts.Length, Is.EqualTo(0), "Group 4 still contained parts after delink.");
            
            if (debugtest)
            {
                m_log.Debug("--------After Link-------");
                m_log.Debug("Group1: parts:" + grp1.Parts.Length);
                m_log.Debug("Group1: Pos:"+grp1.AbsolutePosition+", Rot:"+grp1.GroupRotation);
                m_log.Debug("Group1: Prim1: OffsetPosition:" + part1.OffsetPosition + ", OffsetRotation:" + part1.RotationOffset);
                m_log.Debug("Group1: Prim2: OffsetPosition:"+part2.OffsetPosition+", OffsetRotation:"+ part2.RotationOffset);

                m_log.Debug("Group3: parts:" + grp3.Parts.Length);
                m_log.Debug("Group3: Pos:"+grp3.AbsolutePosition+", Rot:"+grp3.GroupRotation);
                m_log.Debug("Group3: Prim1: OffsetPosition:"+part3.OffsetPosition+", OffsetRotation:"+part3.RotationOffset);
                m_log.Debug("Group3: Prim2: OffsetPosition:"+part4.OffsetPosition+", OffsetRotation:"+part4.RotationOffset);
            }

            // Required for linking
            grp1.RootPart.ClearUpdateSchedule();
            grp3.RootPart.ClearUpdateSchedule();

            // root part should have no offset position or rotation
            Assert.That(part1.OffsetPosition == Vector3.Zero && part1.RotationOffset == Quaternion.Identity,
                "root part should have no offset position or rotation (again)");

            // offset position should be root part position - part2.absolute position.
            Assert.That(part2.OffsetPosition == new Vector3(-10, -10, -10),
                "offset position should be root part position - part2.absolute position (again)");

            float roll = 0;
            float pitch = 0;
            float yaw = 0;

            // There's a euler anomoly at 180, 0, 0 so expect 180 to turn into -180.
            part1.RotationOffset.GetEulerAngles(out roll, out pitch, out yaw);
            Vector3 rotEuler1 = new Vector3(roll * Utils.RAD_TO_DEG, pitch * Utils.RAD_TO_DEG, yaw * Utils.RAD_TO_DEG);

            if (debugtest)
                m_log.Debug(rotEuler1);

            part2.RotationOffset.GetEulerAngles(out roll, out pitch, out yaw);
            Vector3 rotEuler2 = new Vector3(roll * Utils.RAD_TO_DEG, pitch * Utils.RAD_TO_DEG, yaw * Utils.RAD_TO_DEG);

            if (debugtest)
                m_log.Debug(rotEuler2);

            Assert.That(rotEuler2.ApproxEquals(new Vector3(-180, 0, 0), 0.001f) || rotEuler2.ApproxEquals(new Vector3(180, 0, 0), 0.001f),
                "Not sure what this assertion is all about...");

            // Now we're linking the first group to the third group.  This will make the first group child parts of the third one.
            grp3.LinkToGroup(grp1);

            // Delink parts 2 and 3
            grp3.DelinkFromGroup(part2.LocalId);
            grp3.DelinkFromGroup(part3.LocalId);

            if (debugtest)
            {
                m_log.Debug("--------After De-Link-------");
                m_log.Debug("Group1: parts:" + grp1.Parts.Length);
                m_log.Debug("Group1: Pos:" + grp1.AbsolutePosition + ", Rot:" + grp1.GroupRotation);
                m_log.Debug("Group1: Prim1: OffsetPosition:" + part1.OffsetPosition + ", OffsetRotation:" + part1.RotationOffset);
                m_log.Debug("Group1: Prim2: OffsetPosition:" + part2.OffsetPosition + ", OffsetRotation:" + part2.RotationOffset);

                m_log.Debug("Group3: parts:" + grp3.Parts.Length);
                m_log.Debug("Group3: Pos:" + grp3.AbsolutePosition + ", Rot:" + grp3.GroupRotation);
                m_log.Debug("Group3: Prim1: OffsetPosition:" + part3.OffsetPosition + ", OffsetRotation:" + part3.RotationOffset);
                m_log.Debug("Group3: Prim2: OffsetPosition:" + part4.OffsetPosition + ", OffsetRotation:" + part4.RotationOffset);
            }

            Assert.That(part2.AbsolutePosition == Vector3.Zero, "Badness 1");
            Assert.That(part4.OffsetPosition == new Vector3(20, 20, 20), "Badness 2");
            Quaternion compareQuaternion = new Quaternion(0, 0.7071068f, 0, 0.7071068f);
            Assert.That((part4.RotationOffset.X - compareQuaternion.X < 0.00003) 
                && (part4.RotationOffset.Y - compareQuaternion.Y < 0.00003) 
                && (part4.RotationOffset.Z - compareQuaternion.Z < 0.00003) 
                && (part4.RotationOffset.W - compareQuaternion.W < 0.00003),
                "Badness 3");
        }
        
        /// <summary>
        /// Test that a new scene object which is already linked is correctly persisted to the persistence layer.
        /// </summary>
        [Test]
        public void TestNewSceneObjectLinkPersistence()
        {
            TestHelpers.InMethod();
            //log4net.Config.XmlConfigurator.Configure();
            
            TestScene scene = new SceneHelpers().SetupScene();
            
            string rootPartName = "rootpart";
            UUID rootPartUuid = new UUID("00000000-0000-0000-0000-000000000001");
            string linkPartName = "linkpart";
            UUID linkPartUuid = new UUID("00000000-0000-0000-0001-000000000000");

            SceneObjectPart rootPart
                = new SceneObjectPart(UUID.Zero, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero) 
                    { Name = rootPartName, UUID = rootPartUuid };
            SceneObjectPart linkPart
                = new SceneObjectPart(UUID.Zero, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero) 
                    { Name = linkPartName, UUID = linkPartUuid };

            SceneObjectGroup sog = new SceneObjectGroup(rootPart);
            sog.AddPart(linkPart);
            scene.AddNewSceneObject(sog, true);
            
            // In a test, we have to crank the backup handle manually.  Normally this would be done by the timer invoked
            // scene backup thread.
            scene.Backup(true);
            
            List<SceneObjectGroup> storedObjects = scene.SimulationDataService.LoadObjects(scene.RegionInfo.RegionID);
            
            Assert.That(storedObjects.Count, Is.EqualTo(1));
            Assert.That(storedObjects[0].Parts.Length, Is.EqualTo(2));
            Assert.That(storedObjects[0].ContainsPart(rootPartUuid));
            Assert.That(storedObjects[0].ContainsPart(linkPartUuid));
        }
        
        /// <summary>
        /// Test that a delink of a previously linked object is correctly persisted to the database
        /// </summary>
        [Test]
        public void TestDelinkPersistence()
        {
            TestHelpers.InMethod();
            //log4net.Config.XmlConfigurator.Configure();
            
            TestScene scene = new SceneHelpers().SetupScene();
            
            string rootPartName = "rootpart";
            UUID rootPartUuid = new UUID("00000000-0000-0000-0000-000000000001");
            string linkPartName = "linkpart";
            UUID linkPartUuid = new UUID("00000000-0000-0000-0001-000000000000");

            SceneObjectPart rootPart
                = new SceneObjectPart(UUID.Zero, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero) 
                    { Name = rootPartName, UUID = rootPartUuid };
            
            SceneObjectPart linkPart
                = new SceneObjectPart(UUID.Zero, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero) 
                    { Name = linkPartName, UUID = linkPartUuid };
            SceneObjectGroup linkGroup = new SceneObjectGroup(linkPart);
            scene.AddNewSceneObject(linkGroup, true);

            SceneObjectGroup sog = new SceneObjectGroup(rootPart);
            scene.AddNewSceneObject(sog, true);

            Assert.IsFalse(sog.GroupContainsForeignPrims);
            sog.LinkToGroup(linkGroup);
            Assert.IsTrue(sog.GroupContainsForeignPrims);

            scene.Backup(true);
            Assert.AreEqual(1, scene.SimulationDataService.LoadObjects(scene.RegionInfo.RegionID).Count);

            // These changes should occur immediately without waiting for a backup pass
            SceneObjectGroup groupToDelete = sog.DelinkFromGroup(linkPart, false);
            Assert.IsFalse(groupToDelete.GroupContainsForeignPrims);

/* backup is async            
            scene.DeleteSceneObject(groupToDelete, false);

            List<SceneObjectGroup> storedObjects = scene.SimulationDataService.LoadObjects(scene.RegionInfo.RegionID);

            Assert.AreEqual(1, storedObjects.Count);
            Assert.AreEqual(1, storedObjects[0].Parts.Length);
            Assert.IsTrue(storedObjects[0].ContainsPart(rootPartUuid));
*/
        }
    }
}
