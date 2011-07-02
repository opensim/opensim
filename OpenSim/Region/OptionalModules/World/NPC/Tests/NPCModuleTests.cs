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
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Avatar;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.AvatarService;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.OptionalModules.World.NPC.Tests
{
    [TestFixture]
    public class NPCModuleTests
    {
        [Test]
        public void TestCreate()
        {
            TestHelper.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            IConfigSource config = new IniConfigSource();

            config.AddConfig("Modules");
            config.Configs["Modules"].Set("AvatarServices", "LocalAvatarServicesConnector");
            config.AddConfig("AvatarService");
            config.Configs["AvatarService"].Set("LocalServiceModule", "OpenSim.Services.AvatarService.dll:AvatarService");
            config.Configs["AvatarService"].Set("StorageProvider", "OpenSim.Data.Null.dll");

            TestScene scene = SceneSetupHelpers.SetupScene();
            SceneSetupHelpers.SetupSceneModules(scene, config, new NPCModule(), new LocalAvatarServicesConnector());

            INPCModule npcModule = scene.RequestModuleInterface<INPCModule>();
            UUID npcId = npcModule.CreateNPC("John", "Smith", new Vector3(128, 128, 30), scene, UUID.Zero);

            ScenePresence npc = scene.GetScenePresence(npcId);
            Assert.That(npc, Is.Not.Null);
        }
    }
}