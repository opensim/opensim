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
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenSim.Region.PhysicsModule.ODE;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Tests.Common;
using log4net;
using System.Reflection;

namespace OpenSim.Region.PhysicsModule.ODE.Tests
{
    [TestFixture]
    public class ODETestClass : OpenSimTestCase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //private OpenSim.Region.PhysicsModule.ODE.OdePlugin cbt;
        private PhysicsScene pScene;
        private OpenSim.Region.PhysicsModule.ODE.OdeModule odemodule;


        [SetUp]
        public void Initialize()
        {
            IConfigSource openSimINI = new IniConfigSource();
            IConfig startupConfig = openSimINI.AddConfig("Startup");
            startupConfig.Set("physics", "OpenDynamicsEngine");
            startupConfig.Set("DecodedSculptMapPath", "j2kDecodeCache");

            Vector3 regionExtent = new Vector3(Constants.RegionSize, Constants.RegionSize, Constants.RegionHeight);

            //PhysicsScene pScene = physicsPluginManager.GetPhysicsScene(
            //                "BulletSim", "Meshmerizer", openSimINI, "BSTestRegion", regionExtent);
            RegionInfo info = new RegionInfo();
            info.RegionName = "ODETestRegion";
            info.RegionSizeX = info.RegionSizeY = info.RegionSizeZ = Constants.RegionSize;
            OpenSim.Region.Framework.Scenes.Scene scene = new OpenSim.Region.Framework.Scenes.Scene(info);

            //IMesher mesher = new OpenSim.Region.PhysicsModule.Meshing.Meshmerizer();
            //INonSharedRegionModule mod = mesher as INonSharedRegionModule;
            //mod.Initialise(openSimINI);
            //mod.AddRegion(scene);
            //mod.RegionLoaded(scene);

            //            pScene = new OdeScene();
            odemodule = new OpenSim.Region.PhysicsModule.ODE.OdeModule();
            Console.WriteLine("HERE " + (odemodule == null ? "Null" : "Not null"));
            odemodule.Initialise(openSimINI);
            odemodule.AddRegion(scene);
            odemodule.RegionLoaded(scene);

            // Loading ODEPlugin
            //cbt = new OdePlugin();
            // Getting Physics Scene
            //ps = cbt.GetScene("test");
            // Initializing Physics Scene.
            //ps.Initialise(imp.GetMesher(TopConfig), null, Vector3.Zero);
            float[] _heightmap = new float[(int)Constants.RegionSize * (int)Constants.RegionSize];
            for (int i = 0; i < ((int)Constants.RegionSize * (int)Constants.RegionSize); i++)
            {
                _heightmap[i] = 21f;
            }
            pScene = scene.PhysicsScene;
            pScene.SetTerrain(_heightmap);
        }

        [TearDown]
        public void Terminate()
        {
            pScene.DeleteTerrain();
            pScene.Dispose();

        }

        [Test]
        public void CreateAndDropPhysicalCube()
        {
            PrimitiveBaseShape newcube = PrimitiveBaseShape.CreateBox();
            Vector3 position = new Vector3(((float)Constants.RegionSize * 0.5f), ((float)Constants.RegionSize * 0.5f), 128f);
            Vector3 size = new Vector3(0.5f, 0.5f, 0.5f);
            Quaternion rot = Quaternion.Identity;
            PhysicsActor prim = pScene.AddPrimShape("CoolShape", newcube, position, size, rot, true, 0);
            OdePrim oprim = (OdePrim)prim;
            OdeScene pscene = (OdeScene)pScene;

            Assert.That(oprim.m_taintadd);

            prim.LocalID = 5;

            for (int i = 0; i < 58; i++)
            {
                pScene.Simulate(0.133f);

                Assert.That(oprim.prim_geom != (IntPtr)0);

                Assert.That(oprim.m_targetSpace != (IntPtr)0);

                //Assert.That(oprim.m_targetSpace == pscene.space);
                m_log.Info("TargetSpace: " + oprim.m_targetSpace + " - SceneMainSpace: " + pscene.space);

                Assert.That(!oprim.m_taintadd);
                m_log.Info("Prim Position (" + oprim.LocalID + "): " + prim.Position);

                // Make sure we're above the ground
                //Assert.That(prim.Position.Z > 20f);
                //m_log.Info("PrimCollisionScore (" + oprim.m_localID + "): " + oprim.m_collisionscore);

                // Make sure we've got a Body
                Assert.That(oprim.Body != (IntPtr)0);
                //m_log.Info(
            }

            // Make sure we're not somewhere above the ground
            Assert.That(prim.Position.Z < 21.5f);

            pScene.RemovePrim(prim);
            Assert.That(oprim.m_taintremove);
            pScene.Simulate(0.133f);
            Assert.That(oprim.Body == (IntPtr)0);
        }
    }
}
