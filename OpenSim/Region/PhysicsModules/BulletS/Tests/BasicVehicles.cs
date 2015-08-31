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
public class BasicVehicles : OpenSimTestCase
{
    // Documentation on attributes: http://www.nunit.org/index.php?p=attributes&r=2.6.1
    // Documentation on assertions: http://www.nunit.org/index.php?p=assertions&r=2.6.1

    BSScene PhysicsScene { get; set; }
    BSPrim TestVehicle { get; set; }
    Vector3 TestVehicleInitPosition { get; set; }
    float simulationTimeStep = 0.089f;

    [TestFixtureSetUp]
    public void Init()
    {
        Dictionary<string, string> engineParams = new Dictionary<string, string>();
        engineParams.Add("VehicleEnableAngularVerticalAttraction", "true");
        engineParams.Add("VehicleAngularVerticalAttractionAlgorithm", "1");
        PhysicsScene = BulletSimTestsUtil.CreateBasicPhysicsEngine(engineParams);

        PrimitiveBaseShape pbs = PrimitiveBaseShape.CreateSphere();
        Vector3 pos = new Vector3(100.0f, 100.0f, 0f);
        pos.Z = PhysicsScene.TerrainManager.GetTerrainHeightAtXYZ(pos) + 2f;
        TestVehicleInitPosition = pos;
        Vector3 size = new Vector3(1f, 1f, 1f);
        pbs.Scale = size;
        Quaternion rot = Quaternion.Identity;
        bool isPhys = false;
        uint localID = 123;

        PhysicsScene.AddPrimShape("testPrim", pbs, pos, size, rot, isPhys, localID);
        TestVehicle = (BSPrim)PhysicsScene.PhysObjects[localID];
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

    [TestCase(2f, 0.2f,  0.25f,  0.25f,  0.25f)]
    [TestCase(2f, 0.2f, -0.25f,  0.25f,  0.25f)]
    [TestCase(2f, 0.2f,  0.25f, -0.25f,  0.25f)]
    [TestCase(2f, 0.2f, -0.25f, -0.25f,  0.25f)]
    // [TestCase(2f, 0.2f, 0.785f,   0.0f,  0.25f) /*, "Leaning 45 degrees to the side" */]
    // [TestCase(2f, 0.2f, 1.650f,   0.0f,  0.25f) /*, "Leaning more than 90 degrees to the side" */]
    // [TestCase(2f, 0.2f, 2.750f,   0.0f,  0.25f) /*, "Almost upside down, tipped right" */]
    // [TestCase(2f, 0.2f,-2.750f,   0.0f,  0.25f) /*, "Almost upside down, tipped left" */]
    // [TestCase(2f, 0.2f,   0.0f, 0.785f,  0.25f) /*, "Tipped back 45 degrees" */]
    // [TestCase(2f, 0.2f,   0.0f, 1.650f,  0.25f) /*, "Tipped back more than 90 degrees" */]
    // [TestCase(2f, 0.2f,   0.0f, 2.750f,  0.25f) /*, "Almost upside down, tipped back" */]
    // [TestCase(2f, 0.2f,   0.0f,-2.750f,  0.25f) /*, "Almost upside down, tipped forward" */]
    public void AngularVerticalAttraction(float timeScale, float efficiency, float initRoll, float initPitch, float initYaw)
    {
        // Enough simulation steps to cover the timescale the operation should take
        int simSteps = (int)(timeScale / simulationTimeStep) + 1;

        // Tip the vehicle
        Quaternion initOrientation = Quaternion.CreateFromEulers(initRoll, initPitch, initYaw);
        TestVehicle.Orientation = initOrientation;

        TestVehicle.Position = TestVehicleInitPosition;

        // The vehicle controller is not enabled directly (by setting a vehicle type).
        //    Instead the appropriate values are set and calls are made just the parts of the
        //    controller we want to exercise. Stepping the physics engine then applies
        //    the actions of that one feature.
        BSDynamics vehicleActor = TestVehicle.GetVehicleActor(true /* createIfNone */);
        if (vehicleActor != null)
        {
            vehicleActor.ProcessFloatVehicleParam(Vehicle.VERTICAL_ATTRACTION_EFFICIENCY, efficiency);
            vehicleActor.ProcessFloatVehicleParam(Vehicle.VERTICAL_ATTRACTION_TIMESCALE, timeScale);
            // vehicleActor.enableAngularVerticalAttraction = true;

            TestVehicle.IsPhysical = true;
            PhysicsScene.ProcessTaints();

            // Step the simulator a bunch of times and vertical attraction should orient the vehicle up
            for (int ii = 0; ii < simSteps; ii++)
            {
                vehicleActor.ForgetKnownVehicleProperties();
                vehicleActor.ComputeAngularVerticalAttraction();
                vehicleActor.PushKnownChanged();

                PhysicsScene.Simulate(simulationTimeStep);
            }
        }

        TestVehicle.IsPhysical = false;
        PhysicsScene.ProcessTaints();

        // After these steps, the vehicle should be upright
        /*
        float finalRoll, finalPitch, finalYaw;
        TestVehicle.Orientation.GetEulerAngles(out finalRoll, out finalPitch, out finalYaw);
        Assert.That(finalRoll, Is.InRange(-0.01f, 0.01f));
        Assert.That(finalPitch, Is.InRange(-0.01f, 0.01f));
        Assert.That(finalYaw, Is.InRange(initYaw - 0.1f, initYaw + 0.1f));
         */

        Vector3 upPointer = Vector3.UnitZ * TestVehicle.Orientation;
        Assert.That(upPointer.Z, Is.GreaterThan(0.99f));
    }
}
}