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
using System.Threading;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.CoreModules.World.Permissions;
using OpenSim.Region.CoreModules.Avatar.Inventory.Transfer;
using OpenSim.Region.CoreModules.Framework.InventoryAccess;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Tests.Permissions
{
    /// <summary>
    /// Basic scene object tests (create, read and delete but not update).
    /// </summary>
    [TestFixture]
    public class DirectTransferTests : OpenSimTestCase
    {
        private static string Perms = "Owner: {0}; Group: {1}; Everyone: {2}; Next: {3}";
        protected TestScene m_Scene;
        private ScenePresence[] m_Avatars = new ScenePresence[3];

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            TestHelpers.EnableLogging();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Messaging");
            config.Configs["Messaging"].Set("InventoryTransferModule", "InventoryTransferModule");
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("InventoryAccessModule", "BasicInventoryAccessModule");
            config.AddConfig("InventoryService");
            config.Configs["InventoryService"].Set("LocalServiceModule", "OpenSim.Services.InventoryService.dll:XInventoryService");
            config.Configs["InventoryService"].Set("StorageProvider", "OpenSim.Tests.Common.dll:TestXInventoryDataPlugin");

            m_Scene = new SceneHelpers().SetupScene("Test", UUID.Random(), 1000, 1000, config);
            // Add modules
            SceneHelpers.SetupSceneModules(m_Scene, config, new DefaultPermissionsModule(), new InventoryTransferModule(), new BasicInventoryAccessModule());

            // Add 3 avatars
            for (int i = 0; i < 3; i++)
            {
                UUID id = TestHelpers.ParseTail(i+1);

                m_Avatars[i] = AddScenePresence("Bot", "Bot_" + i, id);
                Assert.That(m_Avatars[i], Is.Not.Null);
                Assert.That(m_Avatars[i].IsChildAgent, Is.False);
                Assert.That(m_Avatars[i].UUID, Is.EqualTo(id));

                Assert.That(m_Scene.GetScenePresences().Count, Is.EqualTo(i+1));
            }
        }

        /// <summary>
        /// Test adding an object to a scene.
        /// </summary>
        [Test]
        public void TestGiveCBox()
        {
            TestHelpers.InMethod();

            // Create a C Box
            SceneObjectGroup boxC = AddSceneObject("Box C", 10, 1, m_Avatars[0].UUID);

            // field = 16 is NextOwner 
            // set = 1 means add the permission; set = 0 means remove permission
            m_Scene.HandleObjectPermissionsUpdate((IClientAPI)m_Avatars[0].ClientView, m_Avatars[0].UUID,
                ((IClientAPI)(m_Avatars[0].ClientView)).SessionId, 16, boxC.LocalId, (uint)PermissionMask.Copy, 1);

            m_Scene.HandleObjectPermissionsUpdate((IClientAPI)m_Avatars[0].ClientView, m_Avatars[0].UUID,
                ((IClientAPI)(m_Avatars[0].ClientView)).SessionId, 16, boxC.LocalId, (uint)PermissionMask.Transfer, 0);
            PrintPerms(boxC);

            Assert.True((boxC.RootPart.NextOwnerMask & (int)PermissionMask.Copy) != 0);
            Assert.True((boxC.RootPart.NextOwnerMask & (int)PermissionMask.Modify) == 0);
            Assert.True((boxC.RootPart.NextOwnerMask & (int)PermissionMask.Transfer) == 0);

            InventoryItemBase item = TakeCopyToInventory(boxC);

            GiveInventoryItem(item.ID, m_Avatars[0], m_Avatars[1]);

            item = GetItemFromInventory(m_Avatars[1].UUID, "Objects", "Box C");

            // Check the receiver
            PrintPerms(item);
            Assert.True((item.BasePermissions & (int)PermissionMask.Copy) != 0);
            Assert.True((item.BasePermissions & (int)PermissionMask.Modify) == 0);
            Assert.True((item.BasePermissions & (int)PermissionMask.Transfer) == 0);

            // Rez it and check perms in scene too
            m_Scene.RezObject(m_Avatars[1].ControllingClient, item.ID, UUID.Zero, Vector3.One, Vector3.Zero, UUID.Zero, 0, false, false, false, UUID.Zero);
            Assert.That(m_Scene.GetSceneObjectGroups().Count, Is.EqualTo(2));
            SceneObjectGroup copyBoxC = m_Scene.GetSceneObjectGroups().Find(sog => sog.OwnerID == m_Avatars[1].UUID);
            PrintPerms(copyBoxC);
            Assert.That(copyBoxC, Is.Not.Null);

        }

        #region Helper Functions

        private void PrintPerms(SceneObjectGroup sog)
        {
            Console.WriteLine("SOG " + sog.Name + ": " + String.Format(Perms, (PermissionMask)sog.EffectiveOwnerPerms,
                 (PermissionMask)sog.EffectiveGroupPerms, (PermissionMask)sog.EffectiveEveryOnePerms, (PermissionMask)sog.RootPart.NextOwnerMask));

        }

        private void PrintPerms(InventoryItemBase item)
        {
            Console.WriteLine("Inv " + item.Name + ": " + String.Format(Perms, (PermissionMask)item.BasePermissions,
                 (PermissionMask)item.GroupPermissions, (PermissionMask)item.EveryOnePermissions, (PermissionMask)item.NextPermissions));

        }

        private ScenePresence AddScenePresence(string first, string last, UUID id)
        {
            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(m_Scene, first, last, id, "pw");
            ScenePresence sp = SceneHelpers.AddScenePresence(m_Scene, id);
            Assert.That(m_Scene.AuthenticateHandler.GetAgentCircuitData(id), Is.Not.Null);

            return sp;
        }

        private SceneObjectGroup AddSceneObject(string name, int suffix, int partsToTestCount, UUID ownerID)
        {
            TestHelpers.InMethod();

            SceneObjectGroup so = SceneHelpers.CreateSceneObject(partsToTestCount, ownerID, name, suffix);
            so.Name = name;
            so.Description = name;

            Assert.That(m_Scene.AddNewSceneObject(so, false), Is.True);
            SceneObjectGroup retrievedSo = m_Scene.GetSceneObjectGroup(so.UUID);

            // If the parts have the same UUID then we will consider them as one and the same
            Assert.That(retrievedSo.PrimCount, Is.EqualTo(partsToTestCount));

            return so;
        }

        private InventoryItemBase TakeCopyToInventory(SceneObjectGroup sog)
        {
            InventoryFolderBase objsFolder = UserInventoryHelpers.GetInventoryFolder(m_Scene.InventoryService, sog.OwnerID, "Objects");
            Assert.That(objsFolder, Is.Not.Null);

            List<uint> localIds = new List<uint>(); localIds.Add(sog.LocalId);
            m_Scene.DeRezObjects((IClientAPI)m_Avatars[0].ClientView, localIds, sog.UUID, DeRezAction.TakeCopy, objsFolder.ID);
            Thread.Sleep(5000);

            List<InventoryItemBase> items = m_Scene.InventoryService.GetFolderItems(sog.OwnerID, objsFolder.ID);
            InventoryItemBase item = items.Find(i => i.Name == sog.Name);
            Assert.That(item, Is.Not.Null);

            return item;

        }

        private InventoryItemBase GetItemFromInventory(UUID userID, string folderName, string itemName)
        {
            InventoryFolderBase objsFolder = UserInventoryHelpers.GetInventoryFolder(m_Scene.InventoryService, userID, folderName);
            Assert.That(objsFolder, Is.Not.Null);
            List<InventoryItemBase> items = m_Scene.InventoryService.GetFolderItems(userID, objsFolder.ID);
            InventoryItemBase item = items.Find(i => i.Name == itemName);
            Assert.That(item, Is.Not.Null);

            return item;
        }

        private void GiveInventoryItem(UUID itemId, ScenePresence giverSp, ScenePresence receiverSp)
        {
            TestClient giverClient = (TestClient)giverSp.ControllingClient;
            TestClient receiverClient = (TestClient)receiverSp.ControllingClient;

            UUID initialSessionId = TestHelpers.ParseTail(0x10);
            byte[] giveImBinaryBucket = new byte[17];
            byte[] itemIdBytes = itemId.GetBytes();
            Array.Copy(itemIdBytes, 0, giveImBinaryBucket, 1, itemIdBytes.Length);

            GridInstantMessage giveIm
                = new GridInstantMessage(
                    m_Scene,
                    giverSp.UUID,
                    giverSp.Name,
                    receiverSp.UUID,
                    (byte)InstantMessageDialog.InventoryOffered,
                    false,
                    "inventory offered msg",
                    initialSessionId,
                    false,
                    Vector3.Zero,
                    giveImBinaryBucket,
                    true);

            giverClient.HandleImprovedInstantMessage(giveIm);

            // These details might not all be correct.
            GridInstantMessage acceptIm
                = new GridInstantMessage(
                    m_Scene,
                    receiverSp.UUID,
                    receiverSp.Name,
                    giverSp.UUID,
                    (byte)InstantMessageDialog.InventoryAccepted,
                    false,
                    "inventory accepted msg",
                    initialSessionId,
                    false,
                    Vector3.Zero,
                    null,
                    true);

            receiverClient.HandleImprovedInstantMessage(acceptIm);
        }
        #endregion
    }
}