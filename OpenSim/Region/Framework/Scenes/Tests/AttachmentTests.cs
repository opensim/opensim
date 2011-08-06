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
using System.Threading;
using System.Timers;
using Timer=System.Timers.Timer;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    /// <summary>
    /// Attachment tests
    /// </summary>
    [TestFixture]
    public class AttachmentTests
    {
        public Scene scene, scene2;
        public UUID agent1;
        public static Random random;
        public ulong region1, region2;
        public AgentCircuitData acd1;
        public SceneObjectGroup sog1, sog2, sog3;

        [TestFixtureSetUp]
        public void Init()
        {
            TestHelpers.InMethod();
            
            scene = SceneHelpers.SetupScene("Neighbour x", UUID.Random(), 1000, 1000);
            scene2 = SceneHelpers.SetupScene("Neighbour x+1", UUID.Random(), 1001, 1000);

            ISharedRegionModule interregionComms = new LocalSimulationConnectorModule();
            interregionComms.Initialise(new IniConfigSource());
            interregionComms.PostInitialise();
            SceneHelpers.SetupSceneModules(scene, new IniConfigSource(), interregionComms);
            SceneHelpers.SetupSceneModules(scene2, new IniConfigSource(), interregionComms);

            agent1 = UUID.Random();
            random = new Random();
            sog1 = NewSOG(UUID.Random(), scene, agent1);
            sog2 = NewSOG(UUID.Random(), scene, agent1);
            sog3 = NewSOG(UUID.Random(), scene, agent1);

            //ulong neighbourHandle = Utils.UIntsToLong((uint)(neighbourx * Constants.RegionSize), (uint)(neighboury * Constants.RegionSize));
            region1 = scene.RegionInfo.RegionHandle;
            region2 = scene2.RegionInfo.RegionHandle;
            
            SceneHelpers.AddScenePresence(scene, agent1);
        }     
        
        [Test]
        public void T030_TestAddAttachments()
        {
            TestHelpers.InMethod();

            ScenePresence presence = scene.GetScenePresence(agent1);

            presence.AddAttachment(sog1);
            presence.AddAttachment(sog2);
            presence.AddAttachment(sog3);

            Assert.That(presence.HasAttachments(), Is.True);
            Assert.That(presence.ValidateAttachments(), Is.True);
        }

        [Test]
        public void T031_RemoveAttachments()
        {
            TestHelpers.InMethod();

            ScenePresence presence = scene.GetScenePresence(agent1);
            presence.RemoveAttachment(sog1);
            presence.RemoveAttachment(sog2);
            presence.RemoveAttachment(sog3);
            Assert.That(presence.HasAttachments(), Is.False);
        }

        // I'm commenting this test because scene setup NEEDS InventoryService to 
        // be non-null
        //[Test]
        public void T032_CrossAttachments()
        {
            TestHelpers.InMethod();

            ScenePresence presence = scene.GetScenePresence(agent1);
            ScenePresence presence2 = scene2.GetScenePresence(agent1);
            presence2.AddAttachment(sog1);
            presence2.AddAttachment(sog2);

            ISharedRegionModule serialiser = new SerialiserModule();
            SceneHelpers.SetupSceneModules(scene, new IniConfigSource(), serialiser);
            SceneHelpers.SetupSceneModules(scene2, new IniConfigSource(), serialiser);

            Assert.That(presence.HasAttachments(), Is.False, "Presence has attachments before cross");

            //Assert.That(presence2.CrossAttachmentsIntoNewRegion(region1, true), Is.True, "Cross was not successful");
            Assert.That(presence2.HasAttachments(), Is.False, "Presence2 objects were not deleted");
            Assert.That(presence.HasAttachments(), Is.True, "Presence has not received new objects");
        }   
        
        private SceneObjectGroup NewSOG(UUID uuid, Scene scene, UUID agent)
        {
            SceneObjectPart sop = new SceneObjectPart();
            sop.Name = RandomName();
            sop.Description = RandomName();
            sop.Text = RandomName();
            sop.SitName = RandomName();
            sop.TouchName = RandomName();
            sop.UUID = uuid;
            sop.Shape = PrimitiveBaseShape.Default;
            sop.Shape.State = 1;
            sop.OwnerID = agent;

            SceneObjectGroup sog = new SceneObjectGroup(sop);
            sog.SetScene(scene);

            return sog;
        }        
        
        private static string RandomName()
        {
            StringBuilder name = new StringBuilder();
            int size = random.Next(5,12);
            char ch;
            for (int i = 0; i < size; i++)
            {
                ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65))) ;
                name.Append(ch);
            }
            
            return name.ToString();
        }        
    }
}