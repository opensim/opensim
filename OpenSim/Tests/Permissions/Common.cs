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
using System.Threading;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.CoreModules.World.Permissions;
using OpenSim.Region.CoreModules.Avatar.Inventory.Transfer;
using OpenSim.Region.CoreModules.Framework.InventoryAccess;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Tests.Permissions
{
    [SetUpFixture]
    public class Common : OpenSimTestCase
    {
        public static Common TheInstance;

        public static TestScene TheScene
        {
            get { return TheInstance.m_Scene; }
        }

        public static ScenePresence[] TheAvatars
        {
            get { return TheInstance.m_Avatars;  }
        }

        private static string Perms = "Owner: {0}; Group: {1}; Everyone: {2}; Next: {3}";
        private TestScene m_Scene;
        private ScenePresence[] m_Avatars = new ScenePresence[3];

        [SetUp]
        public override void SetUp()
        {
            if (TheInstance == null)
                TheInstance = this;

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

            config.AddConfig("Groups");
            config.Configs["Groups"].Set("Enabled", "true");
            config.Configs["Groups"].Set("Module", "Groups Module V2");
            config.Configs["Groups"].Set("StorageProvider", "OpenSim.Tests.Common.dll:TestGroupsDataPlugin");
            config.Configs["Groups"].Set("ServicesConnectorModule", "Groups Local Service Connector");
            config.Configs["Groups"].Set("LocalService", "local");

            m_Scene = new SceneHelpers().SetupScene("Test", UUID.Random(), 1000, 1000, config);
            // Add modules
            SceneHelpers.SetupSceneModules(m_Scene, config, new DefaultPermissionsModule(), new InventoryTransferModule(), new BasicInventoryAccessModule());

            SetUpBasicEnvironment();
        }

        /// <summary>
        /// The basic environment consists of:
        /// - 3 avatars: A1, A2, A3
        /// - 6 simple boxes inworld belonging to A0 and with Next Owner perms:
        ///   C, CT, MC, MCT, MT, T
        /// - Copies of all of these boxes in A0's inventory in the Objects folder
        /// - One additional box inworld and in A0's inventory which is a copy of MCT, but 
        ///   with C removed in inventory. This one is called MCT-C
        /// </summary>
        private void SetUpBasicEnvironment()
        {
            Console.WriteLine("===> SetUpBasicEnvironment <===");

            // Add 3 avatars
            for (int i = 0; i < 3; i++)
            {
                UUID id = TestHelpers.ParseTail(i + 1);

                m_Avatars[i] = AddScenePresence("Bot", "Bot_" + (i+1), id);
                Assert.That(m_Avatars[i], Is.Not.Null);
                Assert.That(m_Avatars[i].IsChildAgent, Is.False);
                Assert.That(m_Avatars[i].UUID, Is.EqualTo(id));
                Assert.That(m_Scene.GetScenePresences().Count, Is.EqualTo(i + 1));
            }

            AddA1Object("Box C", 10, PermissionMask.Copy);
            AddA1Object("Box CT", 11, PermissionMask.Copy | PermissionMask.Transfer);
            AddA1Object("Box MC", 12, PermissionMask.Modify | PermissionMask.Copy);
            AddA1Object("Box MCT", 13, PermissionMask.Modify | PermissionMask.Copy | PermissionMask.Transfer);
            AddA1Object("Box MT", 14, PermissionMask.Modify | PermissionMask.Transfer);
            AddA1Object("Box T", 15, PermissionMask.Transfer);

            // MCT-C
            AddA1Object("Box MCT-C", 16, PermissionMask.Modify | PermissionMask.Copy | PermissionMask.Transfer);

            Thread.Sleep(5000);

            InventoryFolderBase objsFolder = UserInventoryHelpers.GetInventoryFolder(m_Scene.InventoryService, m_Avatars[0].UUID, "Objects");
            List<InventoryItemBase> items = m_Scene.InventoryService.GetFolderItems(m_Avatars[0].UUID, objsFolder.ID);
            Assert.That(items.Count, Is.EqualTo(7));

            RevokePermission(0, "Box MCT-C", PermissionMask.Copy);
        }

        private ScenePresence AddScenePresence(string first, string last, UUID id)
        {
            UserAccount ua1 = UserAccountHelpers.CreateUserWithInventory(m_Scene, first, last, id, "pw");
            ScenePresence sp = SceneHelpers.AddScenePresence(m_Scene, id);
            Assert.That(m_Scene.AuthenticateHandler.GetAgentCircuitData(id), Is.Not.Null);

            return sp;
        }

        private void AddA1Object(string name, int suffix, PermissionMask nextOwnerPerms)
        {
            // Create a Box. Default permissions are just T
            SceneObjectGroup box = AddSceneObject(name, suffix, 1, m_Avatars[0].UUID);
            Assert.True((box.RootPart.NextOwnerMask & (int)PermissionMask.Copy) == 0);
            Assert.True((box.RootPart.NextOwnerMask & (int)PermissionMask.Modify) == 0);
            Assert.True((box.RootPart.NextOwnerMask & (int)PermissionMask.Transfer) != 0);

            // field = 16 is NextOwner 
            // set = 1 means add the permission; set = 0 means remove permission

            if ((nextOwnerPerms & PermissionMask.Copy) != 0)
                m_Scene.HandleObjectPermissionsUpdate((IClientAPI)m_Avatars[0].ClientView, m_Avatars[0].UUID,
                    ((IClientAPI)(m_Avatars[0].ClientView)).SessionId, 16, box.LocalId, (uint)PermissionMask.Copy, 1);

            if ((nextOwnerPerms & PermissionMask.Modify) != 0)
                m_Scene.HandleObjectPermissionsUpdate((IClientAPI)m_Avatars[0].ClientView, m_Avatars[0].UUID,
                    ((IClientAPI)(m_Avatars[0].ClientView)).SessionId, 16, box.LocalId, (uint)PermissionMask.Modify, 1);

            if ((nextOwnerPerms & PermissionMask.Transfer) == 0)
                m_Scene.HandleObjectPermissionsUpdate((IClientAPI)m_Avatars[0].ClientView, m_Avatars[0].UUID,
                    ((IClientAPI)(m_Avatars[0].ClientView)).SessionId, 16, box.LocalId, (uint)PermissionMask.Transfer, 0);

            PrintPerms(box);
            AssertPermissions(nextOwnerPerms, (PermissionMask)box.RootPart.NextOwnerMask, box.OwnerID.ToString().Substring(34) + " : " + box.Name);

            TakeCopyToInventory(0, box);

        }

        public void RevokePermission(int ownerIndex, string name, PermissionMask perm)
        {
            InventoryItemBase item = Common.TheInstance.GetItemFromInventory(m_Avatars[ownerIndex].UUID, "Objects", name);
            Assert.That(item, Is.Not.Null);

            // Clone it, so to avoid aliasing -- just like the viewer does.
            InventoryItemBase clone = Common.TheInstance.CloneInventoryItem(item);
            // Revoke the permission in this copy
            clone.NextPermissions &= ~(uint)perm;
            Common.TheInstance.AssertPermissions((PermissionMask)clone.NextPermissions & ~perm,
                (PermissionMask)clone.NextPermissions, Common.TheInstance.IdStr(clone));
            Assert.That(clone.ID == item.ID);

            // Update properties of the item in inventory. This should affect the original item above.
            Common.TheScene.UpdateInventoryItemAsset(m_Avatars[ownerIndex].ControllingClient, UUID.Zero, clone.ID, clone);

            item = Common.TheInstance.GetItemFromInventory(m_Avatars[ownerIndex].UUID, "Objects", name);
            Assert.That(item, Is.Not.Null);
            Common.TheInstance.PrintPerms(item);
            Common.TheInstance.AssertPermissions((PermissionMask)item.NextPermissions & ~perm,
                (PermissionMask)item.NextPermissions, Common.TheInstance.IdStr(item));

        }

        public void PrintPerms(SceneObjectGroup sog)
        {
            Console.WriteLine("SOG " + sog.Name + " (" + sog.OwnerID.ToString().Substring(34) + "): " + 
                String.Format(Perms, (PermissionMask)sog.EffectiveOwnerPerms,
                    (PermissionMask)sog.EffectiveGroupPerms, (PermissionMask)sog.EffectiveEveryOnePerms, (PermissionMask)sog.RootPart.NextOwnerMask));

        }

        public void PrintPerms(InventoryItemBase item)
        {
            Console.WriteLine("Inv " + item.Name + " (" + item.Owner.ToString().Substring(34) + "): " + 
                String.Format(Perms, (PermissionMask)item.BasePermissions,
                    (PermissionMask)item.GroupPermissions, (PermissionMask)item.EveryOnePermissions, (PermissionMask)item.NextPermissions));

        }

        public void AssertPermissions(PermissionMask desired, PermissionMask actual, string message)
        {
            if ((desired & PermissionMask.Copy) != 0)
                Assert.True((actual & PermissionMask.Copy) != 0, message);
            else
                Assert.True((actual & PermissionMask.Copy) == 0, message);

            if ((desired & PermissionMask.Modify) != 0)
                Assert.True((actual & PermissionMask.Modify) != 0, message);
            else
                Assert.True((actual & PermissionMask.Modify) == 0, message);

            if ((desired & PermissionMask.Transfer) != 0)
                Assert.True((actual & PermissionMask.Transfer) != 0, message);
            else
                Assert.True((actual & PermissionMask.Transfer) == 0, message);

        }

        public SceneObjectGroup AddSceneObject(string name, int suffix, int partsToTestCount, UUID ownerID)
        {
            SceneObjectGroup so = SceneHelpers.CreateSceneObject(partsToTestCount, ownerID, name, suffix);
            so.Name = name;
            so.Description = name;

            Assert.That(m_Scene.AddNewSceneObject(so, false), Is.True);
            SceneObjectGroup retrievedSo = m_Scene.GetSceneObjectGroup(so.UUID);

            // If the parts have the same UUID then we will consider them as one and the same
            Assert.That(retrievedSo.PrimCount, Is.EqualTo(partsToTestCount));

            return so;
        }

        public void TakeCopyToInventory(int userIndex, SceneObjectGroup sog)
        {
            InventoryFolderBase objsFolder = UserInventoryHelpers.GetInventoryFolder(m_Scene.InventoryService, m_Avatars[userIndex].UUID, "Objects");
            Assert.That(objsFolder, Is.Not.Null);

            List<uint> localIds = new List<uint>(); localIds.Add(sog.LocalId);
            // This is an async operation
            m_Scene.DeRezObjects((IClientAPI)m_Avatars[userIndex].ClientView, localIds, m_Avatars[userIndex].UUID, DeRezAction.TakeCopy, objsFolder.ID);
        }

        public InventoryItemBase GetItemFromInventory(UUID userID, string folderName, string itemName)
        {
            InventoryFolderBase objsFolder = UserInventoryHelpers.GetInventoryFolder(m_Scene.InventoryService, userID, folderName);
            Assert.That(objsFolder, Is.Not.Null);
            List<InventoryItemBase> items = m_Scene.InventoryService.GetFolderItems(userID, objsFolder.ID);
            InventoryItemBase item = items.Find(i => i.Name == itemName);
            Assert.That(item, Is.Not.Null);

            return item;
        }

        public InventoryItemBase CloneInventoryItem(InventoryItemBase item)
        {
            InventoryItemBase clone = new InventoryItemBase(item.ID);
            clone.Name = item.Name;
            clone.Description = item.Description;
            clone.AssetID = item.AssetID;
            clone.AssetType = item.AssetType;
            clone.BasePermissions = item.BasePermissions;
            clone.CreatorId = item.CreatorId;
            clone.CurrentPermissions = item.CurrentPermissions;
            clone.EveryOnePermissions = item.EveryOnePermissions;
            clone.Flags = item.Flags;
            clone.Folder = item.Folder;
            clone.GroupID = item.GroupID;
            clone.GroupOwned = item.GroupOwned;
            clone.GroupPermissions = item.GroupPermissions;
            clone.InvType = item.InvType;
            clone.NextPermissions = item.NextPermissions;
            clone.Owner = item.Owner;

            return clone;
        }

        public void DeleteObjectsFolders()
        {
            // Delete everything in A2 and A3's Objects folders, so we can restart
            for (int i = 1; i < 3; i++)
            {
                InventoryFolderBase objsFolder = UserInventoryHelpers.GetInventoryFolder(Common.TheScene.InventoryService, Common.TheAvatars[i].UUID, "Objects");
                Assert.That(objsFolder, Is.Not.Null);

                List<InventoryItemBase> items = Common.TheScene.InventoryService.GetFolderItems(Common.TheAvatars[i].UUID, objsFolder.ID);
                List<UUID> ids = new List<UUID>();
                foreach (InventoryItemBase it in items)
                    ids.Add(it.ID);

                Common.TheScene.InventoryService.DeleteItems(Common.TheAvatars[i].UUID, ids);
                items = Common.TheScene.InventoryService.GetFolderItems(Common.TheAvatars[i].UUID, objsFolder.ID);
                Assert.That(items.Count, Is.EqualTo(0), "A" + (i + 1));
            }

        }

        public string IdStr(InventoryItemBase item)
        {
            return item.Owner.ToString().Substring(34) + " : " + item.Name;
        }

        public string IdStr(SceneObjectGroup sog)
        {
            return sog.OwnerID.ToString().Substring(34) + " : " + sog.Name;
        }

        public void GiveInventoryItem(UUID itemId, ScenePresence giverSp, ScenePresence receiverSp)
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
    }
}
