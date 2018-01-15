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

        BSScene _physicsScene { get; set; }
        BSPrim _targetSphere { get; set; }
        Vector3 _targetSpherePosition { get; set; }
//        float _simulationTimeStep = 0.089f;

        uint _targetLocalID = 123;

        [TestFixtureSetUp]
        public void Init()
        {
            Dictionary<string, string> engineParams = new Dictionary<string, string>();
            engineParams.Add("UseBulletRaycast", "true");
            _physicsScene = BulletSimTestsUtil.CreateBasicPhysicsEngine(engineParams);

            PrimitiveBaseShape pbs = PrimitiveBaseShape.CreateSphere();
            Vector3 pos = new Vector3(100.0f, 100.0f, 50f);
            _targetSpherePosition = pos;
            Vector3 size = new Vector3(10f, 10f, 10f);
            pbs.Scale = size;
            Quaternion rot = Quaternion.Identity;
            bool isPhys = false;

            _physicsScene.AddPrimShape("TargetSphere", pbs, pos, size, rot, isPhys, _targetLocalID);
            _targetSphere = (BSPrim)_physicsScene.PhysObjects[_targetLocalID];
            // The actual prim shape creation happens at taint time
            _physicsScene.ProcessTaints();

        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            if (_physicsScene != null)
            {
                // The Dispose() will also free any physical objects in the scene
                _physicsScene.Dispose();
                _physicsScene = null;
            }
        }

        // There is a 10x10x10 sphere at <100,100,50>
        // Shoot rays around the sphere and verify it hits and doesn't hit
        // TestCase parameters are <x,y,z> of start and <x,y,z> of end and expected result
        [TestCase(100f, 50f, 50f, 100f, 150f, 50f, true, "Pass through sphere from front")]
        [TestCase(50f, 100f, 50f, 150f, 100f, 50f, true, "Pass through sphere from side")]
        [TestCase(50f, 50f, 50f, 150f, 150f, 50f, true, "Pass through sphere diaginally")]
        [TestCase(100f, 100f, 100f, 100f, 100f, 20f, true, "Pass through sphere from above")]
        [TestCase(20f, 20f, 50f, 80f, 80f, 50f, false, "Not reach sphere")]
        [TestCase(50f, 50f, 65f, 150f, 150f, 65f, false, "Passed over sphere")]
        public void RaycastAroundObject(float fromX, float fromY, float fromZ, float toX, float toY, float toZ, bool expected, string msg) {
            Vector3 fromPos = new Vector3(fromX, fromY, fromZ);
            Vector3 toPos = new Vector3(toX, toY, toZ);
            Vector3 direction = toPos - fromPos;
            float len = Vector3.Distance(fromPos, toPos);

            List<ContactResult> results = _physicsScene.RaycastWorld(fromPos, direction, len, 1);

            if (expected) {
                // The  test coordinates should generate a hit
                Assert.True(results.Count != 0, msg + ": Did not return a hit but expected to.");
                Assert.True(results.Count == 1, msg + ": Raycast returned not just one hit result.");
                Assert.True(results[0].ConsumerID == _targetLocalID, msg + ": Raycast returned a collision object other than the target");
            }
            else
            {
                // The test coordinates should not generate a hit
                if (results.Count > 0)
                {
                    Assert.False(results.Count > 0, msg + ": Returned a hit at " + results[0].Pos.ToString());
                }
            }
        }
    }
}