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
using OpenSim.Region.Physics.Manager;
using log4net;
using System.Reflection;

namespace OpenSim.Region.Physics.OdePlugin
{
    [TestFixture]
    public class ODETestClass
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private OdePlugin cbt;
        private PhysicsScene ps;
        private IMeshingPlugin imp;

        [SetUp]
        public void Initialize()
        {
            // Loading ODEPlugin
            cbt = new OdePlugin();
            // Loading Zero Mesher
            imp = new ZeroMesherPlugin();
            // Getting Physics Scene
            ps = cbt.GetScene("test");
            // Initializing Physics Scene.
            ps.Initialise(imp.GetMesher(),null);
            float[] _heightmap = new float[(int)Constants.RegionSize * (int)Constants.RegionSize];
            for (int i = 0; i < ((int)Constants.RegionSize * (int)Constants.RegionSize); i++)
            {
                _heightmap[i] = 21f;
            }
            ps.SetTerrain(_heightmap);
        }

        [TearDown]
        public void Terminate()
        {
            ps.DeleteTerrain();
            ps.Dispose();

        }

        [Test]
        public void CreateAndDropPhysicalCube()
        {
            PrimitiveBaseShape newcube = PrimitiveBaseShape.CreateBox();
            Vector3 position = new Vector3(((float)Constants.RegionSize * 0.5f), ((float)Constants.RegionSize * 0.5f), 128f);
            Vector3 size = new Vector3(0.5f, 0.5f, 0.5f);
            Quaternion rot = Quaternion.Identity;
            PhysicsActor prim = ps.AddPrimShape("CoolShape", newcube, position, size, rot, true);
            OdePrim oprim = (OdePrim)prim;
            OdeScene pscene = (OdeScene) ps;

            Assert.That(oprim.m_taintadd);

            prim.LocalID = 5;

            for (int i = 0; i < 58; i++)
            {
                ps.Simulate(0.133f);

                Assert.That(oprim.prim_geom != (IntPtr)0);

                Assert.That(oprim.m_targetSpace != (IntPtr)0);

                //Assert.That(oprim.m_targetSpace == pscene.space);
                m_log.Info("TargetSpace: " + oprim.m_targetSpace + " - SceneMainSpace: " + pscene.space);

                Assert.That(!oprim.m_taintadd);
                m_log.Info("Prim Position (" + oprim.m_localID + "): " + prim.Position.ToString());

                // Make sure we're above the ground
                //Assert.That(prim.Position.Z > 20f);
                //m_log.Info("PrimCollisionScore (" + oprim.m_localID + "): " + oprim.m_collisionscore);

                // Make sure we've got a Body
                Assert.That(oprim.Body != (IntPtr)0);
                //m_log.Info(
            }

            // Make sure we're not somewhere above the ground
            Assert.That(prim.Position.Z < 21.5f);

            ps.RemovePrim(prim);
            Assert.That(oprim.m_taintremove);
            ps.Simulate(0.133f);
            Assert.That(oprim.Body == (IntPtr)0);
        }
    }
}
