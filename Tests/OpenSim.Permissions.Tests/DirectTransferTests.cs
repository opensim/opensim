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

using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Tests.Permissions
{
    /// <summary>
    /// Basic scene object tests (create, read and delete but not update).
    /// </summary>
    [TestFixture]
    public class DirectTransferTests
    {

        [SetUp]
        public void SetUp()
        {
            // In case we're dealing with some older version of nunit
            if (Common.TheInstance == null)
            {
                Common.TheInstance = new Common();
                Common.TheInstance.SetUp();
            }

            Common.TheInstance.DeleteObjectsFolders();
        }

        /// <summary>
        /// Test giving simple objecta with various combinations of next owner perms.
        /// </summary>
        [Test]
        public void TestGiveBox()
        {
            TestHelpers.InMethod();

            // C, CT, MC, MCT, MT, T
            string[] names = new string[6] { "Box C", "Box CT", "Box MC", "Box MCT", "Box MT", "Box T" };
            PermissionMask[] perms = new PermissionMask[6] {
                    PermissionMask.Copy,
                    PermissionMask.Copy | PermissionMask.Transfer,
                    PermissionMask.Modify | PermissionMask.Copy,
                    PermissionMask.Modify | PermissionMask.Copy | PermissionMask.Transfer,
                    PermissionMask.Modify | PermissionMask.Transfer,
                    PermissionMask.Transfer
            };

            for (int i = 0; i < 6; i++)
                TestOneBox(names[i], perms[i]);
        }

        private void TestOneBox(string name, PermissionMask mask)
        {
            InventoryItemBase item = Common.TheInstance.GetItemFromInventory(Common.TheAvatars[0].UUID, "Objects", name);

            Common.TheInstance.GiveInventoryItem(item.ID, Common.TheAvatars[0], Common.TheAvatars[1]);

            item = Common.TheInstance.GetItemFromInventory(Common.TheAvatars[1].UUID, "Objects", name);

            // Check the receiver
            Common.TheInstance.PrintPerms(item);
            Common.TheInstance.AssertPermissions(mask, (PermissionMask)item.BasePermissions, item.Owner.ToString().Substring(34) + " : " + item.Name);

            int nObjects = Common.TheScene.GetSceneObjectGroups().Count;
            // Rez it and check perms in scene too
            Common.TheScene.RezObject(Common.TheAvatars[1].ControllingClient, item.ID, UUID.Zero, Vector3.One, Vector3.Zero, UUID.Zero, 0, false, false, false, UUID.Zero);
            Assert.That(Common.TheScene.GetSceneObjectGroups().Count, Is.EqualTo(nObjects + 1));

            SceneObjectGroup box = Common.TheScene.GetSceneObjectGroups().Find(sog => sog.OwnerID == Common.TheAvatars[1].UUID && sog.Name == name);
            Common.TheInstance.PrintPerms(box);
            Assert.That(box, Is.Not.Null);

            // Check Owner permissions
            Common.TheInstance.AssertPermissions(mask, (PermissionMask)box.EffectiveOwnerPerms, box.OwnerID.ToString().Substring(34) + " : " + box.Name);

            // Check Next Owner permissions
            Common.TheInstance.AssertPermissions(mask, (PermissionMask)box.RootPart.NextOwnerMask, box.OwnerID.ToString().Substring(34) + " : " + box.Name);

        }

        /// <summary>
        /// Test giving simple objecta with variour combinations of next owner perms.
        /// </summary>
        [Test]
        public void TestDoubleGiveWithChange()
        {
            TestHelpers.InMethod();

            string name = "Box MCT-C";
            InventoryItemBase item = Common.TheInstance.GetItemFromInventory(Common.TheAvatars[0].UUID, "Objects", name);

            // Now give the item to A2. We give the original item, not a clone. 
            // The giving methods are supposed to duplicate it.
            Common.TheInstance.GiveInventoryItem(item.ID, Common.TheAvatars[0], Common.TheAvatars[1]);

            item = Common.TheInstance.GetItemFromInventory(Common.TheAvatars[1].UUID, "Objects", name);

            // Check the receiver
            Common.TheInstance.PrintPerms(item);
            Common.TheInstance.AssertPermissions(PermissionMask.Modify | PermissionMask.Transfer,
                (PermissionMask)item.BasePermissions, Common.TheInstance.IdStr(item));

            // ---------------------------
            // Second transfer
            //----------------------------

            // A2 revokes M
            Common.TheInstance.RevokePermission(1, name, PermissionMask.Modify);

            item = Common.TheInstance.GetItemFromInventory(Common.TheAvatars[1].UUID, "Objects", name);

            // Now give the item to A3. We give the original item, not a clone. 
            // The giving methods are supposed to duplicate it.
            Common.TheInstance.GiveInventoryItem(item.ID, Common.TheAvatars[1], Common.TheAvatars[2]);

            item = Common.TheInstance.GetItemFromInventory(Common.TheAvatars[2].UUID, "Objects", name);

            // Check the receiver
            Common.TheInstance.PrintPerms(item);
            Common.TheInstance.AssertPermissions(PermissionMask.Transfer,
                (PermissionMask)item.BasePermissions, Common.TheInstance.IdStr(item));

        }
    }
}