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
using System.Text;
using log4net;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Avatar.AvatarFactory;
using OpenSim.Region.OptionalModules.World.NPC;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;
using OpenSim.Region.ScriptEngine.Shared.Instance;
using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;

namespace OpenSim.Region.ScriptEngine.Shared.Tests
{
    [TestFixture]
    public class LSL_ApiObjectTests : OpenSimTestCase
    {
        private const double VECTOR_COMPONENT_ACCURACY = 0.0000005d;
        private const float FLOAT_ACCURACY = 0.00005f;

        protected Scene m_scene;
        protected XEngine.XEngine m_engine;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            IConfigSource initConfigSource = new IniConfigSource();
            IConfig config = initConfigSource.AddConfig("XEngine");
            config.Set("Enabled", "true");

            m_scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(m_scene, initConfigSource);

            m_engine = new XEngine.XEngine();
            m_engine.Initialise(initConfigSource);
            m_engine.AddRegion(m_scene);
        }

        [Test]
        public void TestllGetLinkPrimitiveParams()
        {
            TestHelpers.InMethod();
            TestHelpers.EnableLogging();

            UUID ownerId = TestHelpers.ParseTail(0x1);

            SceneObjectGroup grp1 = SceneHelpers.CreateSceneObject(2, ownerId, "grp1-", 0x10);
            grp1.AbsolutePosition = new Vector3(10, 11, 12);
            m_scene.AddSceneObject(grp1);

            LSL_Api apiGrp1 = new LSL_Api();
            apiGrp1.Initialize(m_engine, grp1.RootPart, null);

            // Check simple 1 prim case
            {
                LSL_List resList 
                    = apiGrp1.llGetLinkPrimitiveParams(1, new LSL_List(new LSL_Integer(ScriptBaseClass.PRIM_ROTATION)));

                Assert.That(resList.Length, Is.EqualTo(1));
            }

            // Check 2 prim case
            {
                LSL_List resList 
                    = apiGrp1.llGetLinkPrimitiveParams(
                        1, 
                        new LSL_List(
                            new LSL_Integer(ScriptBaseClass.PRIM_ROTATION), 
                            new LSL_Integer(ScriptBaseClass.PRIM_LINK_TARGET),
                            new LSL_Integer(2),
                            new LSL_Integer(ScriptBaseClass.PRIM_ROTATION)));

                Assert.That(resList.Length, Is.EqualTo(2));
            }

            // Check invalid parameters are ignored
            {
                LSL_List resList 
                    = apiGrp1.llGetLinkPrimitiveParams(3, new LSL_List(new LSL_Integer(ScriptBaseClass.PRIM_ROTATION)));

                Assert.That(resList.Length, Is.EqualTo(0));
            }

            // Check all parameters are ignored if an initial bad link is given
            {
                LSL_List resList 
                    = apiGrp1.llGetLinkPrimitiveParams(
                        3, 
                        new LSL_List(
                            new LSL_Integer(ScriptBaseClass.PRIM_ROTATION), 
                            new LSL_Integer(ScriptBaseClass.PRIM_LINK_TARGET),
                            new LSL_Integer(1),
                            new LSL_Integer(ScriptBaseClass.PRIM_ROTATION)));

                Assert.That(resList.Length, Is.EqualTo(0));
            }

            // Check only subsequent parameters are ignored when we hit the first bad link number
            {
                LSL_List resList 
                    = apiGrp1.llGetLinkPrimitiveParams(
                        1, 
                        new LSL_List(
                            new LSL_Integer(ScriptBaseClass.PRIM_ROTATION), 
                            new LSL_Integer(ScriptBaseClass.PRIM_LINK_TARGET),
                            new LSL_Integer(3),
                            new LSL_Integer(ScriptBaseClass.PRIM_ROTATION)));

                Assert.That(resList.Length, Is.EqualTo(1));
            }
        }

        [Test]
        // llSetPrimitiveParams and llGetPrimitiveParams test.
        public void TestllSetPrimitiveParams()
        {
            TestHelpers.InMethod();

            // Create Prim1.
            Scene scene = new SceneHelpers().SetupScene();
            string obj1Name = "Prim1";
            UUID objUuid = new UUID("00000000-0000-0000-0000-000000000001");
            SceneObjectPart part1 =
                new SceneObjectPart(UUID.Zero, PrimitiveBaseShape.Default,
                Vector3.Zero, Quaternion.Identity,
                Vector3.Zero) { Name = obj1Name, UUID = objUuid };
            Assert.That(scene.AddNewSceneObject(new SceneObjectGroup(part1), false), Is.True);

            LSL_Api apiGrp1 = new LSL_Api();
            apiGrp1.Initialize(m_engine, part1, null);

            // Note that prim hollow check is passed with the other prim params in order to allow the
            // specification of a different check value from the prim param. A cylinder, prism, sphere,
            // torus or ring, with a hole shape of square, is limited to a hollow of 70%. Test 5 below
            // specifies a value of 95% and checks to see if 70% was properly returned.

            // Test a sphere.
            CheckllSetPrimitiveParams(
                apiGrp1,
                "test 1",                                   // Prim test identification string
                new LSL_Types.Vector3(6.0d, 9.9d, 9.9d),    // Prim size
                ScriptBaseClass.PRIM_TYPE_SPHERE,           // Prim type
                ScriptBaseClass.PRIM_HOLE_DEFAULT,          // Prim hole type
                new LSL_Types.Vector3(0.0d, 0.075d, 0.0d),  // Prim cut
                0.80f,                                      // Prim hollow
                new LSL_Types.Vector3(0.0d, 0.0d, 0.0d),    // Prim twist
                new LSL_Types.Vector3(0.32d, 0.76d, 0.0d),  // Prim dimple
                0.80f);                                     // Prim hollow check

            // Test a prism.
            CheckllSetPrimitiveParams(
                apiGrp1,
                "test 2",                                   // Prim test identification string
                new LSL_Types.Vector3(3.5d, 3.5d, 3.5d),    // Prim size
                ScriptBaseClass.PRIM_TYPE_PRISM,            // Prim type
                ScriptBaseClass.PRIM_HOLE_CIRCLE,           // Prim hole type
                new LSL_Types.Vector3(0.0d, 1.0d, 0.0d),    // Prim cut
                0.90f,                                      // Prim hollow
                new LSL_Types.Vector3(0.0d, 0.0d, 0.0d),    // Prim twist
                new LSL_Types.Vector3(2.0d, 1.0d, 0.0d),    // Prim taper 
                new LSL_Types.Vector3(0.0d, 0.0d, 0.0d),    // Prim shear
                0.90f);                                     // Prim hollow check

            // Test a box.
            CheckllSetPrimitiveParams(
                apiGrp1,
                "test 3",                                   // Prim test identification string
                new LSL_Types.Vector3(3.5d, 3.5d, 3.5d),    // Prim size
                ScriptBaseClass.PRIM_TYPE_BOX,              // Prim type
                ScriptBaseClass.PRIM_HOLE_TRIANGLE,         // Prim hole type
                new LSL_Types.Vector3(0.0d, 1.0d, 0.0d),    // Prim cut
                0.99f,                                      // Prim hollow
                new LSL_Types.Vector3(1.0d, 0.0d, 0.0d),    // Prim twist
                new LSL_Types.Vector3(1.0d, 1.0d, 0.0d),    // Prim taper 
                new LSL_Types.Vector3(0.0d, 0.0d, 0.0d),    // Prim shear
                0.99f);                                     // Prim hollow check

            // Test a tube.
            CheckllSetPrimitiveParams(
                apiGrp1,
                "test 4",                                   // Prim test identification string
                new LSL_Types.Vector3(4.2d, 4.2d, 4.2d),    // Prim size
                ScriptBaseClass.PRIM_TYPE_TUBE,             // Prim type
                ScriptBaseClass.PRIM_HOLE_SQUARE,           // Prim hole type
                new LSL_Types.Vector3(0.0d, 1.0d, 0.0d),    // Prim cut
                0.00f,                                      // Prim hollow
                new LSL_Types.Vector3(1.0d, -1.0d, 0.0d),   // Prim twist
                new LSL_Types.Vector3(1.0d, 0.05d, 0.0d),   // Prim hole size
                // Expression for y selected to test precision problems during byte
                // cast in SetPrimitiveShapeParams.
                new LSL_Types.Vector3(0.0d, 0.35d + 0.1d, 0.0d),    // Prim shear
                new LSL_Types.Vector3(0.0d, 1.0d, 0.0d),    // Prim profile cut
                // Expression for y selected to test precision problems during sbyte
                // cast in SetPrimitiveShapeParams.
                new LSL_Types.Vector3(-1.0d, 0.70d + 0.1d + 0.1d, 0.0d),    // Prim taper
                1.11f,                                      // Prim revolutions
                0.88f,                                      // Prim radius
                0.95f,                                      // Prim skew
                0.00f);                                     // Prim hollow check

            // Test a prism.
            CheckllSetPrimitiveParams(
                apiGrp1,
                "test 5",                                   // Prim test identification string
                new LSL_Types.Vector3(3.5d, 3.5d, 3.5d),    // Prim size
                ScriptBaseClass.PRIM_TYPE_PRISM,            // Prim type
                ScriptBaseClass.PRIM_HOLE_SQUARE,           // Prim hole type
                new LSL_Types.Vector3(0.0d, 1.0d, 0.0d),    // Prim cut
                0.99f,                                      // Prim hollow
                // Expression for x selected to test precision problems during sbyte
                // cast in SetPrimitiveShapeBlockParams.
                new LSL_Types.Vector3(0.7d + 0.2d, 0.0d, 0.0d),     // Prim twist
                // Expression for y selected to test precision problems during sbyte
                // cast in SetPrimitiveShapeParams.
                new LSL_Types.Vector3(2.0d, (1.3d + 0.1d), 0.0d),   // Prim taper 
                new LSL_Types.Vector3(0.0d, 0.0d, 0.0d),    // Prim shear
                0.70f);                                     // Prim hollow check

            // Test a sculpted prim.
            CheckllSetPrimitiveParams(
                apiGrp1,
                "test 6",                                   // Prim test identification string
                new LSL_Types.Vector3(2.0d, 2.0d, 2.0d),    // Prim size
                ScriptBaseClass.PRIM_TYPE_SCULPT,           // Prim type
                "be293869-d0d9-0a69-5989-ad27f1946fd4",     // Prim map
                ScriptBaseClass.PRIM_SCULPT_TYPE_SPHERE);   // Prim sculpt type
        }

        // Set prim params for a box, cylinder or prism and check results.
        public void CheckllSetPrimitiveParams(LSL_Api api, string primTest,
            LSL_Types.Vector3 primSize, int primType, int primHoleType, LSL_Types.Vector3 primCut,
            float primHollow, LSL_Types.Vector3 primTwist, LSL_Types.Vector3 primTaper, LSL_Types.Vector3 primShear,
            float primHollowCheck)
        {
            // Set the prim params.
            api.llSetPrimitiveParams(new LSL_Types.list(ScriptBaseClass.PRIM_SIZE, primSize,
                ScriptBaseClass.PRIM_TYPE, primType, primHoleType,
                primCut, primHollow, primTwist, primTaper, primShear));

            // Get params for prim to validate settings.
            LSL_Types.list primParams =
                api.llGetPrimitiveParams(new LSL_Types.list(ScriptBaseClass.PRIM_SIZE, ScriptBaseClass.PRIM_TYPE));

            // Validate settings.
            CheckllSetPrimitiveParamsVector(primSize, api.llList2Vector(primParams, 0), primTest + " prim size");
            Assert.AreEqual(primType, api.llList2Integer(primParams, 1),
                "TestllSetPrimitiveParams " + primTest + " prim type check fail");
            Assert.AreEqual(primHoleType, api.llList2Integer(primParams, 2),
                "TestllSetPrimitiveParams " + primTest + " prim hole default check fail");
            CheckllSetPrimitiveParamsVector(primCut, api.llList2Vector(primParams, 3), primTest + " prim cut");
            Assert.AreEqual(primHollowCheck, api.llList2Float(primParams, 4), FLOAT_ACCURACY,
                "TestllSetPrimitiveParams " + primTest + " prim hollow check fail");
            CheckllSetPrimitiveParamsVector(primTwist, api.llList2Vector(primParams, 5), primTest + " prim twist");
            CheckllSetPrimitiveParamsVector(primTaper, api.llList2Vector(primParams, 6), primTest + " prim taper");
            CheckllSetPrimitiveParamsVector(primShear, api.llList2Vector(primParams, 7), primTest + " prim shear");
        }

        // Set prim params for a sphere and check results.
        public void CheckllSetPrimitiveParams(LSL_Api api, string primTest,
            LSL_Types.Vector3 primSize, int primType, int primHoleType, LSL_Types.Vector3 primCut,
            float primHollow, LSL_Types.Vector3 primTwist, LSL_Types.Vector3 primDimple, float primHollowCheck)
        {
            // Set the prim params.
            api.llSetPrimitiveParams(new LSL_Types.list(ScriptBaseClass.PRIM_SIZE, primSize,
                ScriptBaseClass.PRIM_TYPE, primType, primHoleType,
                primCut, primHollow, primTwist, primDimple));

            // Get params for prim to validate settings.
            LSL_Types.list primParams =
                api.llGetPrimitiveParams(new LSL_Types.list(ScriptBaseClass.PRIM_SIZE, ScriptBaseClass.PRIM_TYPE));

            // Validate settings.
            CheckllSetPrimitiveParamsVector(primSize, api.llList2Vector(primParams, 0), primTest + " prim size");
            Assert.AreEqual(primType, api.llList2Integer(primParams, 1),
                "TestllSetPrimitiveParams " + primTest + " prim type check fail");
            Assert.AreEqual(primHoleType, api.llList2Integer(primParams, 2),
                "TestllSetPrimitiveParams " + primTest + " prim hole default check fail");
            CheckllSetPrimitiveParamsVector(primCut, api.llList2Vector(primParams, 3), primTest + " prim cut");
            Assert.AreEqual(primHollowCheck, api.llList2Float(primParams, 4), FLOAT_ACCURACY,
                "TestllSetPrimitiveParams " + primTest + " prim hollow check fail");
            CheckllSetPrimitiveParamsVector(primTwist, api.llList2Vector(primParams, 5), primTest + " prim twist");
            CheckllSetPrimitiveParamsVector(primDimple, api.llList2Vector(primParams, 6), primTest + " prim dimple");
        }

        // Set prim params for a torus, tube or ring and check results.
        public void CheckllSetPrimitiveParams(LSL_Api api, string primTest,
            LSL_Types.Vector3 primSize, int primType, int primHoleType, LSL_Types.Vector3 primCut,
            float primHollow, LSL_Types.Vector3 primTwist, LSL_Types.Vector3 primHoleSize,
            LSL_Types.Vector3 primShear, LSL_Types.Vector3 primProfCut, LSL_Types.Vector3 primTaper,
            float primRev, float primRadius, float primSkew, float primHollowCheck)
        {
            // Set the prim params.
            api.llSetPrimitiveParams(new LSL_Types.list(ScriptBaseClass.PRIM_SIZE, primSize,
                ScriptBaseClass.PRIM_TYPE, primType, primHoleType,
                primCut, primHollow, primTwist, primHoleSize, primShear, primProfCut,
                primTaper, primRev, primRadius, primSkew));

            // Get params for prim to validate settings.
            LSL_Types.list primParams =
                api.llGetPrimitiveParams(new LSL_Types.list(ScriptBaseClass.PRIM_SIZE, ScriptBaseClass.PRIM_TYPE));

            // Valdate settings.
            CheckllSetPrimitiveParamsVector(primSize, api.llList2Vector(primParams, 0), primTest + " prim size");
            Assert.AreEqual(primType, api.llList2Integer(primParams, 1),
                "TestllSetPrimitiveParams " + primTest + " prim type check fail");
            Assert.AreEqual(primHoleType, api.llList2Integer(primParams, 2),
                "TestllSetPrimitiveParams " + primTest + " prim hole default check fail");
            CheckllSetPrimitiveParamsVector(primCut, api.llList2Vector(primParams, 3), primTest + " prim cut");
            Assert.AreEqual(primHollowCheck, api.llList2Float(primParams, 4), FLOAT_ACCURACY,
                "TestllSetPrimitiveParams " + primTest + " prim hollow check fail");
            CheckllSetPrimitiveParamsVector(primTwist, api.llList2Vector(primParams, 5), primTest + " prim twist");
            CheckllSetPrimitiveParamsVector(primHoleSize, api.llList2Vector(primParams, 6), primTest + " prim hole size");
            CheckllSetPrimitiveParamsVector(primShear, api.llList2Vector(primParams, 7), primTest + " prim shear");
            CheckllSetPrimitiveParamsVector(primProfCut, api.llList2Vector(primParams, 8), primTest + " prim profile cut");
            CheckllSetPrimitiveParamsVector(primTaper, api.llList2Vector(primParams, 9), primTest + " prim taper");
            Assert.AreEqual(primRev, api.llList2Float(primParams, 10), FLOAT_ACCURACY,
                "TestllSetPrimitiveParams " + primTest + " prim revolutions fail");
            Assert.AreEqual(primRadius, api.llList2Float(primParams, 11), FLOAT_ACCURACY,
                "TestllSetPrimitiveParams " + primTest + " prim radius fail");
            Assert.AreEqual(primSkew, api.llList2Float(primParams, 12), FLOAT_ACCURACY,
                "TestllSetPrimitiveParams " + primTest + " prim skew fail");
        }

        // Set prim params for a sculpted prim and check results.
        public void CheckllSetPrimitiveParams(LSL_Api api, string primTest,
            LSL_Types.Vector3 primSize, int primType, string primMap, int primSculptType)
        {
            // Set the prim params.
            api.llSetPrimitiveParams(new LSL_Types.list(ScriptBaseClass.PRIM_SIZE, primSize,
                ScriptBaseClass.PRIM_TYPE, primType, primMap, primSculptType));

            // Get params for prim to validate settings.
            LSL_Types.list primParams =
                api.llGetPrimitiveParams(new LSL_Types.list(ScriptBaseClass.PRIM_SIZE, ScriptBaseClass.PRIM_TYPE));

            // Validate settings.
            CheckllSetPrimitiveParamsVector(primSize, api.llList2Vector(primParams, 0), primTest + " prim size");
            Assert.AreEqual(primType, api.llList2Integer(primParams, 1),
                "TestllSetPrimitiveParams " + primTest + " prim type check fail");
            Assert.AreEqual(primMap, (string)api.llList2String(primParams, 2),
                "TestllSetPrimitiveParams " + primTest + " prim map check fail");
            Assert.AreEqual(primSculptType, api.llList2Integer(primParams, 3),
                "TestllSetPrimitiveParams " + primTest + " prim type scuplt check fail");
        }

        public void CheckllSetPrimitiveParamsVector(LSL_Types.Vector3 vecCheck, LSL_Types.Vector3 vecReturned, string msg)
        {
            // Check each vector component against expected result.
            Assert.AreEqual(vecCheck.x, vecReturned.x, VECTOR_COMPONENT_ACCURACY,
                "TestllSetPrimitiveParams " + msg + " vector check fail on x component");
            Assert.AreEqual(vecCheck.y, vecReturned.y, VECTOR_COMPONENT_ACCURACY,
                "TestllSetPrimitiveParams " + msg + " vector check fail on y component");
            Assert.AreEqual(vecCheck.z, vecReturned.z, VECTOR_COMPONENT_ACCURACY,
                "TestllSetPrimitiveParams " + msg + " vector check fail on z component");
        }

    }
}
