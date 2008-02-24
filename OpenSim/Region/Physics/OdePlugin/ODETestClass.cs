using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Axiom.Math;
using Ode.NET;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Physics.Manager;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;

namespace OpenSim.Region.Physics.OdePlugin
{
    [TestFixture]
    public class ODETestClass
    {
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
            ps = cbt.GetScene();
            // Initializing Physics Scene.
            ps.Initialise(imp.GetMesher());
            float[] _heightmap = new float[256 * 256];
            for (int i = 0; i<(256*256);i++)
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
            PhysicsVector position = new PhysicsVector(128, 128, 128);
            PhysicsVector size = new PhysicsVector(0.5f, 0.5f, 0.5f);
            Quaternion rot = new Quaternion(1, 0, 0, 0);
            PhysicsActor prim = ps.AddPrimShape("CoolShape", newcube, position, size, rot, true);
            OdePrim oprim = (OdePrim)prim;
            OdeScene pscene = (OdeScene) ps;

            Assert.That(oprim.m_taintadd);

            prim.LocalID = 5;

            

            for (int i = 0; i < 38; i++)
            {
                ps.Simulate(0.133f);

                Assert.That(oprim.prim_geom != (IntPtr)0);

                Assert.That(oprim.m_targetSpace != (IntPtr)0);

                //Assert.That(oprim.m_targetSpace == pscene.space);
                Console.WriteLine("TargetSpace: " + oprim.m_targetSpace + " - SceneMainSpace: " + pscene.space);

                Assert.That(!oprim.m_taintadd);
                Console.WriteLine("Prim Position (" + oprim.m_localID +  "): " + prim.Position.ToString());
                
                // Make sure we're above the ground
                Assert.That(prim.Position.Z > 20f);
                Console.WriteLine("PrimCollisionScore (" + oprim.m_localID + "): " + oprim.m_collisionscore);
                
                // Make sure we've got a Body
                Assert.That(oprim.Body != (IntPtr)0);
                //Console.WriteLine(
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
