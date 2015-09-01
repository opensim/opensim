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
using System.Diagnostics;
using System.Reflection;
using log4net;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Tests.Performance
{
    /// <summary>
    /// Object performance tests
    /// </summary>
    /// <remarks>
    /// Don't rely on the numbers given by these tests - they will vary a lot depending on what is already cached,
    /// how much memory is free, etc.  In some cases, later larger tests will apparently take less time than smaller
    /// earlier tests.
    /// </remarks>
    [TestFixture]
    public class ObjectPerformanceTests : OpenSimTestCase
    {
        [TearDown]
        public void TearDown()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

//        [Test]
//        public void Test0000Clean()
//        {
//            TestHelpers.InMethod();
////            log4net.Config.XmlConfigurator.Configure();
//
//            TestAddObjects(200000);
//        }

        [Test]
        public void Test_0001_10K_1PrimObjects()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            TestAddObjects(1, 10000);
        }

        [Test]
        public void Test_0002_100K_1PrimObjects()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            TestAddObjects(1, 100000);
        }

        [Test]
        public void Test_0003_200K_1PrimObjects()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            TestAddObjects(1, 200000);
        }

        [Test]
        public void Test_0011_100_100PrimObjects()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            TestAddObjects(100, 100);
        }

        [Test]
        public void Test_0012_1K_100PrimObjects()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            TestAddObjects(100, 1000);
        }

        [Test]
        public void Test_0013_2K_100PrimObjects()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            TestAddObjects(100, 2000);
        }

        private void TestAddObjects(int primsInEachObject, int objectsToAdd)
        {
            UUID ownerId = new UUID("F0000000-0000-0000-0000-000000000000");

            // Using a local variable for scene, at least on mono 2.6.7, means that it's much more likely to be garbage
            // collected when we teardown this test.  If it's done in a member variable, even if that is subsequently
            // nulled out, the garbage collect can be delayed.
            TestScene scene = new SceneHelpers().SetupScene();

//            Process process = Process.GetCurrentProcess();
//            long startProcessMemory = process.PrivateMemorySize64;
            long startGcMemory = GC.GetTotalMemory(true);
            DateTime start = DateTime.Now;

            for (int i = 1; i <= objectsToAdd; i++)
            {
                SceneObjectGroup so = SceneHelpers.CreateSceneObject(primsInEachObject, ownerId, "part_", i);
                Assert.That(scene.AddNewSceneObject(so, false), Is.True, string.Format("Object {0} was not created", i));
            }

            TimeSpan elapsed = DateTime.Now - start;
//            long processMemoryAlloc = process.PrivateMemorySize64 - startProcessMemory;
            long endGcMemory = GC.GetTotalMemory(false);

            for (int i = 1; i <= objectsToAdd; i++)
            {
                Assert.That(
                    scene.GetSceneObjectGroup(TestHelpers.ParseTail(i)),
                    Is.Not.Null,
                    string.Format("Object {0} could not be retrieved", i));
            }

            // When a scene object is added to a scene, it is placed in the update list for sending to viewers
            // (though in this case we have none).  When it is deleted, it is not removed from the update which is
            // fine since it will later be ignored.
            //
            // However, that means that we need to manually run an update here to clear out that list so that deleted
            // objects will be clean up by the garbage collector before the next stress test is run.
            scene.Update(1);

            Console.WriteLine(
                "Took {0}ms, {1}MB ({2} - {3}) to create {4} objects each containing {5} prim(s)",
                Math.Round(elapsed.TotalMilliseconds),
                (endGcMemory - startGcMemory) / 1024 / 1024,
                endGcMemory / 1024 / 1024,
                startGcMemory / 1024 / 1024,
                objectsToAdd,
                primsInEachObject);

            scene.Close();
//            scene = null;
        }
    }
}