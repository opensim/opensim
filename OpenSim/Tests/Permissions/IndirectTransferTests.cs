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

using System.Collections.Generic;
using System.Threading;
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
    public class IndirectTransferTests
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
        public void SimpleTakeCopy()
        {
            TestHelpers.InMethod();

            // The Objects folder of A2
            InventoryFolderBase objsFolder = UserInventoryHelpers.GetInventoryFolder(Common.TheScene.InventoryService, Common.TheAvatars[1].UUID, "Objects");

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

            // Try A2 takes copies of objects that cannot be copied. 
            for (int i = 0; i < 6; i++)
                TakeOneBox(Common.TheScene.GetSceneObjectGroups(), names[i], perms[i]);
            // Ad-hoc. Enough time to let the take work.
            Thread.Sleep(5000);

            List<InventoryItemBase> items = Common.TheScene.InventoryService.GetFolderItems(Common.TheAvatars[1].UUID, objsFolder.ID);
            Assert.That(items.Count, Is.EqualTo(0));

            // A1 makes the objects copyable
            for (int i = 0; i < 6; i++)
                MakeCopyable(Common.TheScene.GetSceneObjectGroups(), names[i]);

            // Try A2 takes copies of objects that can be copied. 
            for (int i = 0; i < 6; i++)
                TakeOneBox(Common.TheScene.GetSceneObjectGroups(), names[i], perms[i]);
            // Ad-hoc. Enough time to let the take work.
            Thread.Sleep(5000);

            items = Common.TheScene.InventoryService.GetFolderItems(Common.TheAvatars[1].UUID, objsFolder.ID);
            Assert.That(items.Count, Is.EqualTo(6));

            for (int i = 0; i < 6; i++)
            {
                InventoryItemBase item = Common.TheInstance.GetItemFromInventory(Common.TheAvatars[1].UUID, "Objects", names[i]);
                Assert.That(item, Is.Not.Null);
                Common.TheInstance.AssertPermissions(perms[i], (PermissionMask)item.BasePermissions, Common.TheInstance.IdStr(item));
            }
        }

        private void TakeOneBox(List<SceneObjectGroup> objs, string name, PermissionMask mask)
        {
            // Find the object inworld
            SceneObjectGroup box = objs.Find(sog => sog.Name == name && sog.OwnerID == Common.TheAvatars[0].UUID);
            Assert.That(box, Is.Not.Null, name);

            // A2's inventory (index 1)
            Common.TheInstance.TakeCopyToInventory(1, box);
        }

        private void MakeCopyable(List<SceneObjectGroup> objs, string name)
        {
            SceneObjectGroup box = objs.Find(sog => sog.Name == name && sog.OwnerID == Common.TheAvatars[0].UUID);
            Assert.That(box, Is.Not.Null, name);

            // field = 8 is Everyone 
            // set = 1 means add the permission; set = 0 means remove permission
            Common.TheScene.HandleObjectPermissionsUpdate((IClientAPI)Common.TheAvatars[0].ClientView, Common.TheAvatars[0].UUID,
                Common.TheAvatars[0].ControllingClient.SessionId, 8, box.LocalId, (uint)PermissionMask.Copy, 1);
            Common.TheInstance.PrintPerms(box);
        }
    }
}