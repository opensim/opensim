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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.RegionCombinerModule
{
    public class RegionCombinerPermissionModule
    {
        private Scene m_rootScene;

        public RegionCombinerPermissionModule(Scene RootScene)
        {
            m_rootScene = RootScene;
        }

        #region Permission Override

        public bool BypassPermissions()
        {
            return m_rootScene.Permissions.BypassPermissions();
        }

        public void SetBypassPermissions(bool value)
        {
            m_rootScene.Permissions.SetBypassPermissions(value);
        }

        public bool PropagatePermissions()
        {
            return m_rootScene.Permissions.PropagatePermissions();
        }

        public uint GenerateClientFlags(UUID userid, UUID objectidid)
        {
            return m_rootScene.Permissions.GenerateClientFlags(userid,objectidid);
        }

        public bool CanAbandonParcel(UUID user, ILandObject parcel, Scene scene)
        {
            return m_rootScene.Permissions.CanAbandonParcel(user,parcel);
        }

        public bool CanReclaimParcel(UUID user, ILandObject parcel, Scene scene)
        {
            return m_rootScene.Permissions.CanReclaimParcel(user, parcel);
        }

        public bool CanDeedParcel(UUID user, ILandObject parcel, Scene scene)
        {
            return m_rootScene.Permissions.CanDeedParcel(user, parcel);
        }

        public bool CanDeedObject(UUID user, UUID @group, Scene scene)
        {
            return m_rootScene.Permissions.CanDeedObject(user,@group);
        }

        public bool IsGod(UUID user, Scene requestfromscene)
        {
            return m_rootScene.Permissions.IsGod(user);
        }

        public bool CanDuplicateObject(int objectcount, UUID objectid, UUID owner, Scene scene, Vector3 objectposition)
        {
            return m_rootScene.Permissions.CanDuplicateObject(objectcount, objectid, owner, objectposition);
        }

        public bool CanDeleteObject(UUID objectid, UUID deleter, Scene scene)
        {
            return m_rootScene.Permissions.CanDeleteObject(objectid, deleter);
        }

        public bool CanEditObject(UUID objectid, UUID editorid, Scene scene)
        {
            return m_rootScene.Permissions.CanEditObject(objectid, editorid);
        }

        public bool CanEditParcelProperties(UUID user, ILandObject parcel, GroupPowers g, Scene scene, bool allowManager)
        {
            return m_rootScene.Permissions.CanEditParcelProperties(user, parcel, g, allowManager);
        }

        public bool CanInstantMessage(UUID user, UUID target, Scene startscene)
        {
            return m_rootScene.Permissions.CanInstantMessage(user, target);
        }

        public bool CanInventoryTransfer(UUID user, UUID target, Scene startscene)
        {
            return m_rootScene.Permissions.CanInventoryTransfer(user, target);
        }

        public bool CanIssueEstateCommand(UUID user, Scene requestfromscene, bool ownercommand)
        {
            return m_rootScene.Permissions.CanIssueEstateCommand(user, ownercommand);
        }

        public bool CanMoveObject(UUID objectid, UUID moverid, Scene scene)
        {
            return m_rootScene.Permissions.CanMoveObject(objectid, moverid);
        }

        public bool CanObjectEntry(UUID objectid, bool enteringregion, Vector3 newpoint, Scene scene)
        {
            return m_rootScene.Permissions.CanObjectEntry(objectid, enteringregion, newpoint);
        }

        public bool CanReturnObjects(ILandObject land, UUID user, List<SceneObjectGroup> objects, Scene scene)
        {
            return m_rootScene.Permissions.CanReturnObjects(land, user, objects);
        }

        public bool CanRezObject(int objectcount, UUID owner, Vector3 objectposition, Scene scene)
        {
            return m_rootScene.Permissions.CanRezObject(objectcount, owner, objectposition);
        }

        public bool CanRunConsoleCommand(UUID user, Scene requestfromscene)
        {
            return m_rootScene.Permissions.CanRunConsoleCommand(user);
        }

        public bool CanRunScript(UUID script, UUID objectid, UUID user, Scene scene)
        {
            return m_rootScene.Permissions.CanRunScript(script, objectid, user);
        }

        public bool CanCompileScript(UUID owneruuid, int scripttype, Scene scene)
        {
            return m_rootScene.Permissions.CanCompileScript(owneruuid, scripttype);
        }

        public bool CanSellParcel(UUID user, ILandObject parcel, Scene scene)
        {
            return m_rootScene.Permissions.CanSellParcel(user, parcel);
        }

        public bool CanTakeObject(UUID objectid, UUID stealer, Scene scene)
        {
            return m_rootScene.Permissions.CanTakeObject(objectid, stealer);
        }

        public bool CanTakeCopyObject(UUID objectid, UUID userid, Scene inscene)
        {
            return m_rootScene.Permissions.CanTakeObject(objectid, userid);
        }

        public bool CanTerraformLand(UUID user, Vector3 position, Scene requestfromscene)
        {
            return m_rootScene.Permissions.CanTerraformLand(user, position);
        }

        public bool CanLinkObject(UUID user, UUID objectid)
        {
            return m_rootScene.Permissions.CanLinkObject(user, objectid);
        }

        public bool CanDelinkObject(UUID user, UUID objectid)
        {
            return m_rootScene.Permissions.CanDelinkObject(user, objectid);
        }

        public bool CanBuyLand(UUID user, ILandObject parcel, Scene scene)
        {
            return m_rootScene.Permissions.CanBuyLand(user, parcel);
        }

        public bool CanViewNotecard(UUID script, UUID objectid, UUID user, Scene scene)
        {
            return m_rootScene.Permissions.CanViewNotecard(script, objectid, user);
        }

        public bool CanViewScript(UUID script, UUID objectid, UUID user, Scene scene)
        {
            return m_rootScene.Permissions.CanViewScript(script, objectid, user);
        }

        public bool CanEditNotecard(UUID notecard, UUID objectid, UUID user, Scene scene)
        {
            return m_rootScene.Permissions.CanEditNotecard(notecard, objectid, user);
        }

        public bool CanEditScript(UUID script, UUID objectid, UUID user, Scene scene)
        {
            return m_rootScene.Permissions.CanEditScript(script, objectid, user);
        }

        public bool CanCreateObjectInventory(int invtype, UUID objectid, UUID userid)
        {
            return m_rootScene.Permissions.CanCreateObjectInventory(invtype, objectid, userid);
        }

        public bool CanEditObjectInventory(UUID objectid, UUID editorid, Scene scene)
        {
            return m_rootScene.Permissions.CanEditObjectInventory(objectid, editorid);
        }

        public bool CanCopyObjectInventory(UUID itemid, UUID objectid, UUID userid)
        {
            return m_rootScene.Permissions.CanCopyObjectInventory(itemid, objectid, userid);
        }

        public bool CanDeleteObjectInventory(UUID itemid, UUID objectid, UUID userid)
        {
            return m_rootScene.Permissions.CanDeleteObjectInventory(itemid, objectid, userid);
        }

        public bool CanResetScript(UUID prim, UUID script, UUID user, Scene scene)
        {
            return m_rootScene.Permissions.CanResetScript(prim, script, user);
        }

        public bool CanCreateUserInventory(int invtype, UUID userid)
        {
            return m_rootScene.Permissions.CanCreateUserInventory(invtype, userid);
        }

        public bool CanCopyUserInventory(UUID itemid, UUID userid)
        {
            return m_rootScene.Permissions.CanCopyUserInventory(itemid, userid);
        }

        public bool CanEditUserInventory(UUID itemid, UUID userid)
        {
            return m_rootScene.Permissions.CanEditUserInventory(itemid, userid);
        }

        public bool CanDeleteUserInventory(UUID itemid, UUID userid)
        {
            return m_rootScene.Permissions.CanDeleteUserInventory(itemid, userid);
        }

        public bool CanTeleport(UUID userid, Scene scene)
        {
            return m_rootScene.Permissions.CanTeleport(userid);
        }

        #endregion
    }
}
