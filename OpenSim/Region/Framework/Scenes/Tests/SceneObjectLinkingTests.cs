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
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Tests.Common.Mock;
using OpenSim.Tests.Common.Setup;

namespace OpenSim.Region.Environment.Scenes.Tests
{
    /// <summary>
    /// Linking tests
    /// </summary>
    [TestFixture]    
    public class SceneObjectLinkingTests
    {
        [Test]
        public void TestLinkDelink2SceneObjects()
        {
            bool debugtest = false; 

            Scene scene = SceneSetupHelpers.SetupScene();
            SceneObjectPart part1 = SceneSetupHelpers.AddSceneObject(scene);
            SceneObjectGroup grp1 = part1.ParentGroup;
            SceneObjectPart part2 = SceneSetupHelpers.AddSceneObject(scene);
            SceneObjectGroup grp2 = part2.ParentGroup;

            grp1.AbsolutePosition = new Vector3(10, 10, 10);
            grp2.AbsolutePosition = Vector3.Zero;
            
            // <90,0,0>
            grp1.Rotation = (Quaternion.CreateFromEulers(90 * Utils.DEG_TO_RAD, 0, 0));

            // <180,0,0>
            grp2.UpdateGroupRotation(Quaternion.CreateFromEulers(180 * Utils.DEG_TO_RAD, 0, 0));
            
            // Required for linking
            grp1.RootPart.UpdateFlag = 0;
            grp2.RootPart.UpdateFlag = 0;

            // Link grp2 to grp1.   part2 becomes child prim to grp1. grp2 is eliminated.
            grp1.LinkToGroup(grp2);

            // FIXME: Can't do this test yet since group 2 still has its root part!  We can't yet null this since
            // it might cause SOG.ProcessBackup() to fail due to the race condition.  This really needs to be fixed.
            Assert.That(grp2.IsDeleted, "SOG 2 was not registered as deleted after link.");
            Assert.That(grp2.Children.Count, Is.EqualTo(0), "Group 2 still contained children after delink.");
            Assert.That(grp1.Children.Count == 2);

            if (debugtest)
            {
                System.Console.WriteLine("parts: {0}", grp1.Children.Count);
                System.Console.WriteLine("Group1: Pos:{0}, Rot:{1}", grp1.AbsolutePosition, grp1.Rotation);
                System.Console.WriteLine("Group1: Prim1: OffsetPosition:{0}, OffsetRotation:{1}", part1.OffsetPosition, part1.RotationOffset);
                System.Console.WriteLine("Group1: Prim2: OffsetPosition:{0}, OffsetRotation:{1}", part2.OffsetPosition, part2.RotationOffset);
            }

            // root part should have no offset position or rotation
            Assert.That(part1.OffsetPosition == Vector3.Zero && part1.RotationOffset == Quaternion.Identity);

            // offset position should be root part position - part2.absolute position.
            Assert.That(part2.OffsetPosition == new Vector3(-10, -10, -10));

            float roll = 0;
            float pitch = 0;
            float yaw = 0;

            // There's a euler anomoly at 180, 0, 0 so expect 180 to turn into -180.
            part1.RotationOffset.GetEulerAngles(out roll, out pitch, out yaw);
            Vector3 rotEuler1 = new Vector3(roll * Utils.RAD_TO_DEG, pitch * Utils.RAD_TO_DEG, yaw * Utils.RAD_TO_DEG);
            
            if (debugtest)
                System.Console.WriteLine(rotEuler1);

            part2.RotationOffset.GetEulerAngles(out roll, out pitch, out yaw);
            Vector3 rotEuler2 = new Vector3(roll * Utils.RAD_TO_DEG, pitch * Utils.RAD_TO_DEG, yaw * Utils.RAD_TO_DEG);
             
            if (debugtest)
                System.Console.WriteLine(rotEuler2);

            Assert.That(rotEuler2.ApproxEquals(new Vector3(-180, 0, 0), 0.001f) || rotEuler2.ApproxEquals(new Vector3(180, 0, 0), 0.001f));

            // Delink part 2
            grp1.DelinkFromGroup(part2.LocalId);

            if (debugtest)
                System.Console.WriteLine("Group2: Prim2: OffsetPosition:{0}, OffsetRotation:{1}", part2.AbsolutePosition, part2.RotationOffset);

            Assert.That(grp1.Children.Count, Is.EqualTo(1), "Group 1 still contained part2 after delink.");
            Assert.That(part2.AbsolutePosition == Vector3.Zero);            
        }

        [Test]
        public void TestLinkDelink2groups4SceneObjects()
        {
            bool debugtest = false;

            Scene scene = SceneSetupHelpers.SetupScene();
            SceneObjectPart part1 = SceneSetupHelpers.AddSceneObject(scene);
            SceneObjectGroup grp1 = part1.ParentGroup;
            SceneObjectPart part2 = SceneSetupHelpers.AddSceneObject(scene);
            SceneObjectGroup grp2 = part2.ParentGroup;
            SceneObjectPart part3 = SceneSetupHelpers.AddSceneObject(scene);
            SceneObjectGroup grp3 = part3.ParentGroup;
            SceneObjectPart part4 = SceneSetupHelpers.AddSceneObject(scene);
            SceneObjectGroup grp4 = part4.ParentGroup;

            grp1.AbsolutePosition = new Vector3(10, 10, 10);
            grp2.AbsolutePosition = Vector3.Zero;
            grp3.AbsolutePosition = new Vector3(20, 20, 20);
            grp4.AbsolutePosition = new Vector3(40, 40, 40);

            // <90,0,0>
            grp1.Rotation = (Quaternion.CreateFromEulers(90 * Utils.DEG_TO_RAD, 0, 0));

            // <180,0,0>
            grp2.UpdateGroupRotation(Quaternion.CreateFromEulers(180 * Utils.DEG_TO_RAD, 0, 0));

            // <270,0,0>
            grp3.Rotation = (Quaternion.CreateFromEulers(270 * Utils.DEG_TO_RAD, 0, 0));

            // <0,90,0>
            grp4.UpdateGroupRotation(Quaternion.CreateFromEulers(0, 90 * Utils.DEG_TO_RAD, 0));

            // Required for linking
            grp1.RootPart.UpdateFlag = 0;
            grp2.RootPart.UpdateFlag = 0;
            grp3.RootPart.UpdateFlag = 0;
            grp4.RootPart.UpdateFlag = 0;

            // Link grp2 to grp1.   part2 becomes child prim to grp1. grp2 is eliminated.
            grp1.LinkToGroup(grp2);

            // Link grp4 to grp3.
            grp3.LinkToGroup(grp4);
            
            // At this point we should have 4 parts total in two groups.            
            Assert.That(grp1.Children.Count == 2);
            Assert.That(grp2.IsDeleted, "Group 2 was not registered as deleted after link.");
            Assert.That(grp2.Children.Count, Is.EqualTo(0), "Group 2 still contained parts after delink.");            
            Assert.That(grp3.Children.Count == 2);
            Assert.That(grp4.IsDeleted, "Group 4 was not registered as deleted after link.");
            Assert.That(grp4.Children.Count, Is.EqualTo(0), "Group 4 still contained parts after delink.");            
            
            if (debugtest)
            {
                System.Console.WriteLine("--------After Link-------");
                System.Console.WriteLine("Group1: parts: {0}", grp1.Children.Count);
                System.Console.WriteLine("Group1: Pos:{0}, Rot:{1}", grp1.AbsolutePosition, grp1.Rotation);
                System.Console.WriteLine("Group1: Prim1: OffsetPosition:{0}, OffsetRotation:{1}", part1.OffsetPosition, part1.RotationOffset);
                System.Console.WriteLine("Group1: Prim2: OffsetPosition:{0}, OffsetRotation:{1}", part2.OffsetPosition, part2.RotationOffset);
                
                System.Console.WriteLine("Group3: parts: {0}", grp3.Children.Count);
                System.Console.WriteLine("Group3: Pos:{0}, Rot:{1}", grp3.AbsolutePosition, grp3.Rotation);
                System.Console.WriteLine("Group3: Prim1: OffsetPosition:{0}, OffsetRotation:{1}", part3.OffsetPosition, part3.RotationOffset);
                System.Console.WriteLine("Group3: Prim2: OffsetPosition:{0}, OffsetRotation:{1}", part4.OffsetPosition, part4.RotationOffset);
            }            

            // Required for linking
            grp1.RootPart.UpdateFlag = 0;
            grp3.RootPart.UpdateFlag = 0;

            // root part should have no offset position or rotation
            Assert.That(part1.OffsetPosition == Vector3.Zero && part1.RotationOffset == Quaternion.Identity);

            // offset position should be root part position - part2.absolute position.
            Assert.That(part2.OffsetPosition == new Vector3(-10, -10, -10));

            float roll = 0;
            float pitch = 0;
            float yaw = 0;

            // There's a euler anomoly at 180, 0, 0 so expect 180 to turn into -180.
            part1.RotationOffset.GetEulerAngles(out roll, out pitch, out yaw);
            Vector3 rotEuler1 = new Vector3(roll * Utils.RAD_TO_DEG, pitch * Utils.RAD_TO_DEG, yaw * Utils.RAD_TO_DEG);

            if (debugtest)
                System.Console.WriteLine(rotEuler1);

            part2.RotationOffset.GetEulerAngles(out roll, out pitch, out yaw);
            Vector3 rotEuler2 = new Vector3(roll * Utils.RAD_TO_DEG, pitch * Utils.RAD_TO_DEG, yaw * Utils.RAD_TO_DEG);

            if (debugtest)
                System.Console.WriteLine(rotEuler2);

            Assert.That(rotEuler2.ApproxEquals(new Vector3(-180, 0, 0), 0.001f) || rotEuler2.ApproxEquals(new Vector3(180, 0, 0), 0.001f));

            // Now we're linking the first group to the third group.  This will make the first group child parts of the third one.
            grp3.LinkToGroup(grp1);

            // Delink parts 2 and 3
            grp3.DelinkFromGroup(part2.LocalId);
            grp3.DelinkFromGroup(part3.LocalId);

            if (debugtest)
            {
                System.Console.WriteLine("--------After De-Link-------");
                System.Console.WriteLine("Group1: parts: {0}", grp1.Children.Count);
                System.Console.WriteLine("Group1: Pos:{0}, Rot:{1}", grp1.AbsolutePosition, grp1.Rotation);
                System.Console.WriteLine("Group1: Prim1: OffsetPosition:{0}, OffsetRotation:{1}", part1.OffsetPosition, part1.RotationOffset);
                System.Console.WriteLine("NoGroup: Prim2: AbsolutePosition:{0}, OffsetRotation:{1}", part2.AbsolutePosition, part2.RotationOffset);

                System.Console.WriteLine("Group3: parts: {0}", grp3.Children.Count);
                System.Console.WriteLine("Group3: Pos:{0}, Rot:{1}", grp3.AbsolutePosition, grp3.Rotation);
                System.Console.WriteLine("Group3: Prim1: OffsetPosition:{0}, OffsetRotation:{1}", part3.OffsetPosition, part3.RotationOffset);
                System.Console.WriteLine("Group3: Prim2: OffsetPosition:{0}, OffsetRotation:{1}", part4.OffsetPosition, part4.RotationOffset);
            }

            Assert.That(part2.AbsolutePosition == Vector3.Zero);
            Assert.That(part4.OffsetPosition == new Vector3(20, 20, 20));
            Quaternion compareQuaternion = new Quaternion(0, 0.7071068f, 0, 0.7071068f);
            Assert.That((part4.RotationOffset.X - compareQuaternion.X < 0.00003) 
                && (part4.RotationOffset.Y - compareQuaternion.Y < 0.00003) 
                && (part4.RotationOffset.Z - compareQuaternion.Z < 0.00003) 
                && (part4.RotationOffset.W - compareQuaternion.W < 0.00003));
        }        
    }
}
