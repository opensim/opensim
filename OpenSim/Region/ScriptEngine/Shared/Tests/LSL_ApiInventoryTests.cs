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
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.ScriptEngine.Shared.Tests
{
    /// <summary>
    /// Tests for inventory functions in LSL
    /// </summary>
    [TestFixture]
    public class LSL_ApiInventoryTests : OpenSimTestCase
    {
        protected Scene m_scene;
        protected XEngine.XEngine m_engine;

        [SetUp]
        public void SetUp()
        {
            IConfigSource initConfigSource = new IniConfigSource();
            IConfig config = initConfigSource.AddConfig("XEngine");
            config.Set("Enabled", "true");

            m_scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(m_scene, initConfigSource);

            m_engine = new XEngine.XEngine();
            m_engine.Initialise(initConfigSource);
            m_engine.AddRegion(m_scene);
        }

        /// <summary>
        /// Test giving inventory from an object to an object where both are owned by the same user.
        /// </summary>
        [Test]
        public void TestLlGiveInventoryO2OSameOwner()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID userId = TestHelpers.ParseTail(0x1);
            string inventoryItemName = "item1";

            SceneObjectGroup so1 = SceneHelpers.CreateSceneObject(1, userId, "so1", 0x10);
            m_scene.AddSceneObject(so1);

            // Create an object embedded inside the first
            UUID itemId = TestHelpers.ParseTail(0x20);
            TaskInventoryHelpers.AddSceneObject(m_scene, so1.RootPart, inventoryItemName, itemId, userId);

            LSL_Api api = new LSL_Api();
            api.Initialize(m_engine, so1.RootPart, null);

            // Create a second object
            SceneObjectGroup so2 = SceneHelpers.CreateSceneObject(1, userId, "so2", 0x100);
            m_scene.AddSceneObject(so2);

            api.llGiveInventory(so2.UUID.ToString(), inventoryItemName);

            // Item has copy permissions so original should stay intact.
            List<TaskInventoryItem> originalItems = so1.RootPart.Inventory.GetInventoryItems();
            Assert.That(originalItems.Count, Is.EqualTo(1));

            List<TaskInventoryItem> copiedItems = so2.RootPart.Inventory.GetInventoryItems(inventoryItemName);
            Assert.That(copiedItems.Count, Is.EqualTo(1));
            Assert.That(copiedItems[0].Name, Is.EqualTo(inventoryItemName));
        }

        /// <summary>
        /// Test giving inventory from an object to an object where they have different owners
        /// </summary>
        [Test]
        public void TestLlGiveInventoryO2ODifferentOwners()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID user1Id = TestHelpers.ParseTail(0x1);
            UUID user2Id = TestHelpers.ParseTail(0x2);
            string inventoryItemName = "item1";

            SceneObjectGroup so1 = SceneHelpers.CreateSceneObject(1, user1Id, "so1", 0x10);
            m_scene.AddSceneObject(so1);
            LSL_Api api = new LSL_Api();
            api.Initialize(m_engine, so1.RootPart, null);

            // Create an object embedded inside the first
            UUID itemId = TestHelpers.ParseTail(0x20);
            TaskInventoryHelpers.AddSceneObject(m_scene, so1.RootPart, inventoryItemName, itemId, user1Id);

            // Create a second object
            SceneObjectGroup so2 = SceneHelpers.CreateSceneObject(1, user2Id, "so2", 0x100);
            m_scene.AddSceneObject(so2);
            LSL_Api api2 = new LSL_Api();
            api2.Initialize(m_engine, so2.RootPart, null);

            // *** Firstly, we test where llAllowInventoryDrop() has not been called. ***
            api.llGiveInventory(so2.UUID.ToString(), inventoryItemName);

            {
                // Item has copy permissions so original should stay intact.
                List<TaskInventoryItem> originalItems = so1.RootPart.Inventory.GetInventoryItems();
                Assert.That(originalItems.Count, Is.EqualTo(1));

                // Should have not copied
                List<TaskInventoryItem> copiedItems = so2.RootPart.Inventory.GetInventoryItems(inventoryItemName);
                Assert.That(copiedItems.Count, Is.EqualTo(0));
            }

            // *** Secondly, we turn on allow inventory drop in the target and retest. ***
            api2.llAllowInventoryDrop(1);
            api.llGiveInventory(so2.UUID.ToString(), inventoryItemName);

            {
                // Item has copy permissions so original should stay intact.
                List<TaskInventoryItem> originalItems = so1.RootPart.Inventory.GetInventoryItems();
                Assert.That(originalItems.Count, Is.EqualTo(1));
    
                // Should now have copied.
                List<TaskInventoryItem> copiedItems = so2.RootPart.Inventory.GetInventoryItems(inventoryItemName);
                Assert.That(copiedItems.Count, Is.EqualTo(1));
                Assert.That(copiedItems[0].Name, Is.EqualTo(inventoryItemName));
            }
        }
    }
}