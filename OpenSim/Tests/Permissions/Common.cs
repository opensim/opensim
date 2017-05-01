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
        /// - One additional box in A0's inventory which is a copy of MCT, but 
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

            Thread.Sleep(5000);

            InventoryFolderBase objsFolder = UserInventoryHelpers.GetInventoryFolder(m_Scene.InventoryService, m_Avatars[0].UUID, "Objects");
            List<InventoryItemBase> items = m_Scene.InventoryService.GetFolderItems(m_Avatars[0].UUID, objsFolder.ID);
            Assert.That(items.Count, Is.EqualTo(6));
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

            TakeCopyToInventory(box);

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

        public void TakeCopyToInventory(SceneObjectGroup sog)
        {
            InventoryFolderBase objsFolder = UserInventoryHelpers.GetInventoryFolder(m_Scene.InventoryService, sog.OwnerID, "Objects");
            Assert.That(objsFolder, Is.Not.Null);

            List<uint> localIds = new List<uint>(); localIds.Add(sog.LocalId);
            // This is an async operation
            m_Scene.DeRezObjects((IClientAPI)m_Avatars[0].ClientView, localIds, sog.UUID, DeRezAction.TakeCopy, objsFolder.ID);
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
