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
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    [TestFixture]
    public class SceneGraphTests : OpenSimTestCase
    {
        [Test]
        public void TestDuplicateObject()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            Scene scene = new SceneHelpers().SetupScene();

            UUID ownerId = new UUID("00000000-0000-0000-0000-000000000010");
            string part1Name = "part1";
            UUID part1Id = new UUID("00000000-0000-0000-0000-000000000001");
            string part2Name = "part2";
            UUID part2Id = new UUID("00000000-0000-0000-0000-000000000002");

            SceneObjectPart part1
                = new SceneObjectPart(ownerId, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero) 
                    { Name = part1Name, UUID = part1Id };
            SceneObjectGroup so = new SceneObjectGroup(part1);
            SceneObjectPart part2 
                = new SceneObjectPart(ownerId, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero) 
                    { Name = part2Name, UUID = part2Id }; 
            so.AddPart(part2);

            scene.AddNewSceneObject(so, false);
            
            SceneObjectGroup dupeSo 
                = scene.SceneGraph.DuplicateObject(
                    part1.LocalId, new Vector3(10, 0, 0), 0, ownerId, UUID.Zero, Quaternion.Identity);
            Assert.That(dupeSo.Parts.Length, Is.EqualTo(2));
            
            SceneObjectPart dupePart1 = dupeSo.GetLinkNumPart(1);
            SceneObjectPart dupePart2 = dupeSo.GetLinkNumPart(2);
            Assert.That(dupePart1.LocalId, Is.Not.EqualTo(part1.LocalId));
            Assert.That(dupePart2.LocalId, Is.Not.EqualTo(part2.LocalId));
            
            Assert.That(dupePart1.Flags, Is.EqualTo(part1.Flags));
            Assert.That(dupePart2.Flags, Is.EqualTo(part2.Flags));
            
            /*
            Assert.That(part1.PhysActor, Is.Not.Null);
            Assert.That(part2.PhysActor, Is.Not.Null);
            Assert.That(dupePart1.PhysActor, Is.Not.Null);
            Assert.That(dupePart2.PhysActor, Is.Not.Null);
            */

//            TestHelpers.DisableLogging();
        }
    }
}