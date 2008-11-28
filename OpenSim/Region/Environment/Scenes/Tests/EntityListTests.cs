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
 *     * Neither the name of the OpenSim Project nor the
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

using NUnit.Framework.SyntaxHelpers;
using System;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using Nini.Config;
using NUnit.Framework;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Environment.Scenes;
using OpenMetaverse;

namespace OpenSim.Region.Environment.Scenes.Tests
{
    /// <summary>
    /// Scene oriented tests
    /// </summary>
    [TestFixture]
    public class EntityListTests
    {        
        static public Random random;
        SceneObjectGroup found;
        Scene scene = SceneTestUtils.SetupScene();

        [Test]
        public void T010_AddObjects()
        {
            random = new Random();
            SceneObjectGroup found;
            EntityList entlist = new EntityList();
            SceneObjectGroup sog = NewSOG();
            UUID obj1 = sog.UUID;
            uint li1 = sog.LocalId;
            entlist.Add(sog);
            sog = NewSOG();
            UUID obj2 = sog.UUID;
            uint li2 = sog.LocalId;
            entlist.Add(sog);

            found = entlist.FindObject(obj1);
            Assert.That(found.UUID ,Is.EqualTo(obj1) );
            found = entlist.FindObject(li1);
            Assert.That(found.UUID ,Is.EqualTo(obj1) );
            found = entlist.FindObject(obj2);
            Assert.That(found.UUID ,Is.EqualTo(obj2) );
            found = entlist.FindObject(li2);
            Assert.That(found.UUID ,Is.EqualTo(obj2) );

            entlist.RemoveObject(obj1);
            entlist.RemoveObject(obj2);

            found = entlist.FindObject(obj1);
            Assert.That(found, Is.Null);
            found = entlist.FindObject(obj2);
            Assert.That(found, Is.Null);
        }

        [Test]
        public void T011_ThreadAddRemoveTest()
        {   
            EntityList entlist = new EntityList();
            Dictionary<UUID, uint> dict = new Dictionary<UUID,uint>();
            List<Thread> trdlist = new List<Thread>();
            for (int i=0; i<80; i++)
            {
                SceneObjectGroup sog = NewSOG();
                TestThreads test = new TestThreads(entlist,sog);
                Thread start = new Thread(new ThreadStart(test.TestAddSceneObject));
                start.Start();
                trdlist.Add(start);
                dict.Add(sog.UUID, sog.LocalId);
            }
            foreach (Thread thread in trdlist) 
            {
                thread.Join();
            }
            foreach (KeyValuePair<UUID, uint> item in dict)
            {
                found = entlist.FindObject(item.Key);
                Assert.That(found.UUID,Is.EqualTo(item.Key));
                found = entlist.FindObject(item.Value);
                Assert.That(found.UUID,Is.EqualTo(item.Key));
                
                // Start Removing
                TestThreads test = new TestThreads(entlist,found);
                Thread start = new Thread(new ThreadStart(test.TestRemoveSceneObject));
                start.Start();
                trdlist.Add(start);
            }
            foreach (Thread thread in trdlist) 
            {
                thread.Join();
            }
            foreach (KeyValuePair<UUID, uint> item in dict)
            {
                found = entlist.FindObject(item.Key);
                Assert.That(found,Is.Null);
                found = entlist.FindObject(item.Value);
                Assert.That(found,Is.Null);
            }
        }

        [Test]
        public void T012_MultipleUUIDEntry()
        {
            EntityList entlist = new EntityList();
            UUID id = UUID.Random();
            //int exceptions = 0;
            //Dictionary<UUID, uint> dict = new Dictionary<UUID,uint>();
            List<Thread> trdlist = new List<Thread>();
            SceneObjectGroup sog = NewSOG(id);
            uint lid = sog.LocalId;
            for (int i=0; i<30; i++)
            {
                try
                {
                    TestThreads test = new TestThreads(entlist,sog);
                    Thread start = new Thread(new ThreadStart(test.TestAddSceneObject));
                    start.Start();
                    trdlist.Add(start);
                }
                catch
                {
                }
            }
            foreach (Thread thread in trdlist) 
            {
                thread.Join();
            }
            found = entlist.FindObject(sog.UUID);
            Assert.That(found.UUID,Is.EqualTo(sog.UUID));
            found = entlist.FindObject(lid);
            Assert.That(found.UUID,Is.EqualTo(sog.UUID));
            
            entlist.RemoveObject(id);
            found = entlist.FindObject(id);
            Assert.That(found,Is.Null);
        }
        
        private SceneObjectGroup NewSOG()
        {
            SceneObjectGroup sog = new SceneObjectGroup();
            SceneObjectPart sop = new SceneObjectPart(UUID.Random(), PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero);
            sop.Name = RandomName();
            sop.Description = sop.Name;
            sop.Text = RandomName();
            sop.SitName = RandomName();
            sop.TouchName = RandomName();
            sop.ObjectFlags |= (uint)PrimFlags.Phantom;
            
            sog.SetRootPart(sop);

            scene.AddNewSceneObject(sog, false);
            
            return sog;
        }
        
        private SceneObjectGroup NewSOG(UUID id)
        {
            SceneObjectGroup sog = new SceneObjectGroup();            
            SceneObjectPart sop = new SceneObjectPart(UUID.Random(), PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero);
            sop.UUID = id;
            sop.Name = RandomName();
            sop.Description = sop.Name;
            sop.Text = RandomName();
            sop.SitName = RandomName();
            sop.TouchName = RandomName();
            sop.ObjectFlags |= (uint)PrimFlags.Phantom;
            
            sog.SetRootPart(sop);

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

    public class TestThreads
    {
        private EntityList entlist;
        private SceneObjectGroup sog;
        
        public TestThreads(EntityList entlist, SceneObjectGroup sog)
        {
            this.entlist = entlist;
            this.sog = sog;
        }
        public void TestAddSceneObject()
        {
            entlist.Add(sog);
        }
        public void TestRemoveSceneObject()
        {
            entlist.RemoveObject(sog.UUID);
        }
    }
}    