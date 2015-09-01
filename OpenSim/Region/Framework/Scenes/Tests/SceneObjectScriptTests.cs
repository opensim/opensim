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
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    [TestFixture]
    public class SceneObjectScriptTests : OpenSimTestCase
    {
        [Test]
        public void TestAddScript()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID userId = TestHelpers.ParseTail(0x1);
//            UUID itemId = TestHelpers.ParseTail(0x2);
            string itemName = "Test Script Item";

            Scene scene = new SceneHelpers().SetupScene();
            SceneObjectGroup so = SceneHelpers.CreateSceneObject(1, userId);
            scene.AddNewSceneObject(so, true);

            InventoryItemBase itemTemplate = new InventoryItemBase();
            itemTemplate.Name = itemName;
            itemTemplate.Folder = so.UUID;
            itemTemplate.InvType = (int)InventoryType.LSL;

            SceneObjectPart partWhereScriptAdded = scene.RezNewScript(userId, itemTemplate);

            Assert.That(partWhereScriptAdded, Is.Not.Null);

            IEntityInventory primInventory = partWhereScriptAdded.Inventory;
            Assert.That(primInventory.GetInventoryList().Count, Is.EqualTo(1));
            Assert.That(primInventory.ContainsScripts(), Is.True);

            IList<TaskInventoryItem> primItems = primInventory.GetInventoryItems(itemName);
            Assert.That(primItems.Count, Is.EqualTo(1));
        }
    }
}