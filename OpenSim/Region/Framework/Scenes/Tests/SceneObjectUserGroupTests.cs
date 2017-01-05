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
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Avatar.InstantMessage;
using OpenSim.Region.CoreModules.World.Permissions;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.OptionalModules.Avatar.XmlRpcGroups;
using OpenSim.Tests.Common;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    [TestFixture]
    public class SceneObjectUserGroupTests
    {
        /// <summary>
        /// Test share with group object functionality
        /// </summary>
        /// <remarks>This test is not yet fully implemented</remarks>
        [Test]
        public void TestShareWithGroup()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID userId = UUID.Parse("10000000-0000-0000-0000-000000000001");

            TestScene scene = new SceneHelpers().SetupScene();
            IConfigSource configSource = new IniConfigSource();

            IConfig startupConfig = configSource.AddConfig("Startup");
            startupConfig.Set("serverside_object_permissions", true);

            IConfig groupsConfig = configSource.AddConfig("Groups");
            groupsConfig.Set("Enabled", true);
            groupsConfig.Set("Module", "GroupsModule");
            groupsConfig.Set("DebugEnabled", true);

            SceneHelpers.SetupSceneModules(
                scene, configSource, new object[]
                   { new DefaultPermissionsModule(),
                     new GroupsModule(),
                     new MockGroupsServicesConnector() });

            IClientAPI client = SceneHelpers.AddScenePresence(scene, userId).ControllingClient;

            IGroupsModule groupsModule = scene.RequestModuleInterface<IGroupsModule>();

            groupsModule.CreateGroup(client, "group1", "To boldly go", true, UUID.Zero, 5, true, true, true);
        }
    }
}
