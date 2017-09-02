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
using System.Linq;
using System.Text;

using NUnit.Framework;
using log4net;

using OpenSim.Framework;
using OpenSim.Region.PhysicsModule.BulletS;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenSim.Tests.Common;

using OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.BulletS.Tests
{
    [TestFixture]
    public class BulletSimRaycast : OpenSimTestCase
    {
        // Documentation on attributes: http://www.nunit.org/index.php?p=attributes&r=2.6.1
        // Documentation on assertions: http://www.nunit.org/index.php?p=assertions&r=2.6.1

        BSScene PhysicsScene { get; set; }
        BSPrim TargetSphere { get; set; }
        Vector3 TargetSpherePosition { get; set; }
        float simulationTimeStep = 0.089f;

        [TestFixtureSetUp]
        public void Init()
        {
            Dictionary<string, string> engineParams = new Dictionary<string, string>();
            engineParams.Add("UseBulletRaycast", "true");
            PhysicsScene = BulletSimTestsUtil.CreateBasicPhysicsEngine(engineParams);

            PrimitiveBaseShape pbs = PrimitiveBaseShape.CreateSphere();
            Vector3 pos = new Vector3(100.0f, 100.0f, 50f);
            pos.Z = PhysicsScene.TerrainManager.GetTerrainHeightAtXYZ(pos) + 2f;
            TargetSpherePosition = pos;
            Vector3 size = new Vector3(10f, 10f, 10f);
            pbs.Scale = size;
            Quaternion rot = Quaternion.Identity;
            bool isPhys = false;
            uint localID = 123;

            PhysicsScene.AddPrimShape("TargetSphere", pbs, pos, size, rot, isPhys, localID);
            TargetSphere = (BSPrim)PhysicsScene.PhysObjects[localID];
            // The actual prim shape creation happens at taint time
            PhysicsScene.ProcessTaints();

        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            if (PhysicsScene != null)
            {
                // The Dispose() will also free any physical objects in the scene
                PhysicsScene.Dispose();
                PhysicsScene = null;
            }
        }

        // There is a 10x10x10 sphere at <100,100,50>
        // Shoot rays around the sphere and verify it hits and doesn't hit
        // TestCase parameters are <x,y,z> of start and <x,y,z> of end and expected result
        [TestCase(20f, 20f, 50f, 50f, 50f, 50f, true)]      // in front to sphere
        [TestCase(20f, 20f, 100f, 50f, 50f, 50f, true)]     // from above to sphere
        [TestCase(50f, 50f, 50f, 150f, 150f, 50f, true)]    // through sphere
        [TestCase(50f, 50f, 65f, 150f, 150f, 65f, false)]   // pass over sphere
        public void RaycastAroundObject(float fromX, float fromY, float fromZ, float toX, float toY, float toZ, bool expected) {
            Vector3 fromPos = new Vector3(fromX, fromY, fromZ);
            Vector3 toPos = new Vector3(toX, toY, toZ);
            Vector3 direction = toPos - fromPos;
            float len = Vector3.Distance(fromPos, toPos);

            List<ContactResult> results = PhysicsScene.RaycastWorld(fromPos, direction, len, 1);

            if (expected) {
                Assert.True(results.Count > 0);
            }
            else
            {
                Assert.False(results.Count > 0);
            }
        }
    }
}