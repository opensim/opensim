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
using OpenSim.Region.Physics.BulletSPlugin;
using OpenSim.Region.Physics.Manager;
using OpenSim.Tests.Common;

using OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin.Tests
{
[TestFixture]
public class HullCreation : OpenSimTestCase
{
    // Documentation on attributes: http://www.nunit.org/index.php?p=attributes&r=2.6.1
    // Documentation on assertions: http://www.nunit.org/index.php?p=assertions&r=2.6.1

    BSScene PhysicsScene { get; set; }
    Vector3 ObjectInitPosition;
    float simulationTimeStep = 0.089f;

    [TestFixtureSetUp]
    public void Init()
    {

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

    [TestCase(7, 2, 5f, 5f, 32, 0f)]    /* default hull parameters */
    public void GeomHullConvexDecomp( int maxDepthSplit,
                                        int maxDepthSplitForSimpleShapes,
                                        float concavityThresholdPercent,
                                        float volumeConservationThresholdPercent,
                                        int maxVertices,
                                        float maxSkinWidth)
    {
        // Setup the physics engine to use the C# version of convex decomp
        Dictionary<string, string> engineParams = new Dictionary<string, string>();
        engineParams.Add("MeshSculptedPrim", "true"); // ShouldMeshSculptedPrim
        engineParams.Add("ForceSimplePrimMeshing", "false"); // ShouldForceSimplePrimMeshing
        engineParams.Add("UseHullsForPhysicalObjects", "true"); // ShouldUseHullsForPhysicalObjects
        engineParams.Add("ShouldRemoveZeroWidthTriangles", "true");
        engineParams.Add("ShouldUseBulletHACD", "false");
        engineParams.Add("ShouldUseSingleConvexHullForPrims", "true");
        engineParams.Add("ShouldUseGImpactShapeForPrims", "false");
        engineParams.Add("ShouldUseAssetHulls", "true");

        engineParams.Add("CSHullMaxDepthSplit", maxDepthSplit.ToString());
        engineParams.Add("CSHullMaxDepthSplitForSimpleShapes", maxDepthSplitForSimpleShapes.ToString());
        engineParams.Add("CSHullConcavityThresholdPercent", concavityThresholdPercent.ToString());
        engineParams.Add("CSHullVolumeConservationThresholdPercent", volumeConservationThresholdPercent.ToString());
        engineParams.Add("CSHullMaxVertices", maxVertices.ToString());
        engineParams.Add("CSHullMaxSkinWidth", maxSkinWidth.ToString());

        PhysicsScene = BulletSimTestsUtil.CreateBasicPhysicsEngine(engineParams);

        PrimitiveBaseShape pbs;
        Vector3 pos;
        Vector3 size;
        Quaternion rot;
        bool isPhys;

        // Cylinder
        pbs = PrimitiveBaseShape.CreateCylinder();
        pos = new Vector3(100.0f, 100.0f, 0f);
        pos.Z = PhysicsScene.TerrainManager.GetTerrainHeightAtXYZ(pos) + 10f;
        ObjectInitPosition = pos;
        size = new Vector3(2f, 2f, 2f);
        pbs.Scale = size;
        rot = Quaternion.Identity;
        isPhys = true;
        uint cylinderLocalID = 123;
        PhysicsScene.AddPrimShape("testCylinder", pbs, pos, size, rot, isPhys, cylinderLocalID);
        BSPrim primTypeCylinder = (BSPrim)PhysicsScene.PhysObjects[cylinderLocalID];

        // Hollow Cylinder
        pbs = PrimitiveBaseShape.CreateCylinder();
        pbs.ProfileHollow = (ushort)(0.70f * 50000);
        pos = new Vector3(110.0f, 110.0f, 0f);
        pos.Z = PhysicsScene.TerrainManager.GetTerrainHeightAtXYZ(pos) + 10f;
        ObjectInitPosition = pos;
        size = new Vector3(2f, 2f, 2f);
        pbs.Scale = size;
        rot = Quaternion.Identity;
        isPhys = true;
        uint hollowCylinderLocalID = 124;
        PhysicsScene.AddPrimShape("testHollowCylinder", pbs, pos, size, rot, isPhys, hollowCylinderLocalID);
        BSPrim primTypeHollowCylinder = (BSPrim)PhysicsScene.PhysObjects[hollowCylinderLocalID];

        // Torus
        // ProfileCurve = Circle, PathCurve = Curve1
        pbs = PrimitiveBaseShape.CreateSphere();
        pbs.ProfileShape = (byte)ProfileShape.Circle;
        pbs.PathCurve = (byte)Extrusion.Curve1;
        pbs.PathScaleX = 100;   // default hollow info as set in the viewer
        pbs.PathScaleY = (int)(.25f / 0.01f) + 200;
        pos = new Vector3(120.0f, 120.0f, 0f);
        pos.Z = PhysicsScene.TerrainManager.GetTerrainHeightAtXYZ(pos) + 10f;
        ObjectInitPosition = pos;
        size = new Vector3(2f, 4f, 4f);
        pbs.Scale = size;
        rot = Quaternion.Identity;
        isPhys = true;
        uint torusLocalID = 125;
        PhysicsScene.AddPrimShape("testTorus", pbs, pos, size, rot, isPhys, torusLocalID);
        BSPrim primTypeTorus = (BSPrim)PhysicsScene.PhysObjects[torusLocalID];
        
        // The actual prim shape creation happens at taint time
        PhysicsScene.ProcessTaints();

        // Check out the created hull shapes and report their characteristics
        ReportShapeGeom(primTypeCylinder);
        ReportShapeGeom(primTypeHollowCylinder);
        ReportShapeGeom(primTypeTorus);
    }

    [TestCase]
    public void GeomHullBulletHACD()
    {
        // Cylinder
        // Hollow Cylinder
        // Torus
    }

    private void ReportShapeGeom(BSPrim prim)
    {
        if (prim != null)
        {
            if (prim.PhysShape.HasPhysicalShape)
            {
                BSShape physShape = prim.PhysShape;
                string shapeType = physShape.GetType().ToString();
                switch (shapeType)
                {
                    case "OpenSim.Region.Physics.BulletSPlugin.BSShapeNative":
                        BSShapeNative nShape = physShape as BSShapeNative;
                        prim.PhysScene.DetailLog("{0}, type={1}", prim.Name, shapeType);
                        break;
                    case "OpenSim.Region.Physics.BulletSPlugin.BSShapeMesh":
                        BSShapeMesh mShape = physShape as BSShapeMesh;
                        prim.PhysScene.DetailLog("{0}, mesh, shapeInfo={1}", prim.Name, mShape.shapeInfo);
                        break;
                    case "OpenSim.Region.Physics.BulletSPlugin.BSShapeHull":
                        // BSShapeHull hShape = physShape as BSShapeHull;
                        // prim.PhysScene.DetailLog("{0}, hull, shapeInfo={1}", prim.Name, hShape.shapeInfo);
                        break;
                    case "OpenSim.Region.Physics.BulletSPlugin.BSShapeConvexHull":
                        BSShapeConvexHull chShape = physShape as BSShapeConvexHull;
                        prim.PhysScene.DetailLog("{0}, convexHull, shapeInfo={1}", prim.Name, chShape.shapeInfo);
                        break;
                    case "OpenSim.Region.Physics.BulletSPlugin.BSShapeCompound":
                        BSShapeCompound cShape = physShape as BSShapeCompound;
                        prim.PhysScene.DetailLog("{0}, type={1}", prim.Name, shapeType);
                        break;
                    default:
                        prim.PhysScene.DetailLog("{0}, type={1}", prim.Name, shapeType);
                        break;
                }
            }
        }
    }
}
}