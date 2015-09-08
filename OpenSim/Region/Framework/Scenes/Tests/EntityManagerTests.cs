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
using System.Reflection;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    [TestFixture, LongRunning]
    public class EntityManagerTests : OpenSimTestCase
    {
        static public Random random;
        SceneObjectGroup found;
        Scene scene = new SceneHelpers().SetupScene();

        [Test]
        public void T010_AddObjects()
        {
            TestHelpers.InMethod();
            
            random = new Random();
            SceneObjectGroup found;
            EntityManager entman = new EntityManager();
            SceneObjectGroup sog = NewSOG();
            UUID obj1 = sog.UUID;
            uint li1 = sog.LocalId;
            entman.Add(sog);
            sog = NewSOG();
            UUID obj2 = sog.UUID;
            uint li2 = sog.LocalId;
            entman.Add(sog);
            
            found = (SceneObjectGroup)entman[obj1];
            Assert.That(found.UUID ,Is.EqualTo(obj1));
            found = (SceneObjectGroup)entman[li1];
            Assert.That(found.UUID ,Is.EqualTo(obj1));
            found = (SceneObjectGroup)entman[obj2];
            Assert.That(found.UUID ,Is.EqualTo(obj2));
            found = (SceneObjectGroup)entman[li2];
            Assert.That(found.UUID ,Is.EqualTo(obj2));

            entman.Remove(obj1);
            entman.Remove(li2);

            Assert.That(entman.ContainsKey(obj1), Is.False);
            Assert.That(entman.ContainsKey(li1), Is.False);
            Assert.That(entman.ContainsKey(obj2), Is.False);
            Assert.That(entman.ContainsKey(li2), Is.False);
        }

        [Test]
        public void T011_ThreadAddRemoveTest()
        {
            TestHelpers.InMethod();
            
            // This test adds and removes with mutiple threads, attempting to break the 
            // uuid and localid dictionary coherence.
            EntityManager entman = new EntityManager();
            SceneObjectGroup sog = NewSOG();
            for (int j=0; j<20; j++)
            {
                List<Thread> trdlist = new List<Thread>();
                
                for (int i=0; i<4; i++)
                {
                    // Adds scene object
                    NewTestThreads test = new NewTestThreads(entman,sog);
                    Thread start = new Thread(new ThreadStart(test.TestAddSceneObject));
                    start.Start();
                    trdlist.Add(start);
                        
                    // Removes it
                    test = new NewTestThreads(entman,sog);
                    start = new Thread(new ThreadStart(test.TestRemoveSceneObject));
                    start.Start();
                    trdlist.Add(start);
                }
                foreach (Thread thread in trdlist) 
                {
                    thread.Join();
                }
                if (entman.ContainsKey(sog.UUID) || entman.ContainsKey(sog.LocalId)) {
                    found = (SceneObjectGroup)entman[sog.UUID];
                    Assert.That(found.UUID,Is.EqualTo(sog.UUID));
                    found = (SceneObjectGroup)entman[sog.LocalId];
                    Assert.That(found.UUID,Is.EqualTo(sog.UUID));
                }
            }
        }

        private SceneObjectGroup NewSOG()
        {
            SceneObjectPart sop = new SceneObjectPart(UUID.Random(), PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero);
            sop.Name = RandomName();
            sop.Description = sop.Name;
            sop.Text = RandomName();
            sop.SitName = RandomName();
            sop.TouchName = RandomName();
            sop.Flags |= PrimFlags.Phantom;

            SceneObjectGroup sog = new SceneObjectGroup(sop);
            scene.AddNewSceneObject(sog, false);
            
            return sog;
        }
                
        private static string RandomName()
        {
            StringBuilder name = new StringBuilder();
            int size = random.Next(40,80); 
            char ch ;
            for (int i=0; i<size; i++)
            {
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65))) ;
                name.Append(ch);
            }
            return name.ToString();
        }
    }

    public class NewTestThreads
    {
        private EntityManager entman;
        private SceneObjectGroup sog;
        private Random random;
        
        public NewTestThreads(EntityManager entman, SceneObjectGroup sog)
        {
            this.entman = entman;
            this.sog = sog;
            this.random = new Random();
        }
        public void TestAddSceneObject()
        {
            Thread.Sleep(random.Next(0,50));
            entman.Add(sog);
        }
        public void TestRemoveSceneObject()
        {
            Thread.Sleep(random.Next(0,50));
            entman.Remove(sog.UUID);
        }
    }
}