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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Text;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Scenes
{
    public class SceneExternalChecks
    {
        private Scene m_scene;

        public SceneExternalChecks(Scene scene)
        {
            m_scene = scene;
        }

        #region Object Permission Checks

        public delegate uint GenerateClientFlags(UUID userID, UUID objectIDID);
        private List<GenerateClientFlags> GenerateClientFlagsCheckFunctions = new List<GenerateClientFlags>();

        public void addGenerateClientFlags(GenerateClientFlags delegateFunc)
        {
            if (!GenerateClientFlagsCheckFunctions.Contains(delegateFunc))
                GenerateClientFlagsCheckFunctions.Add(delegateFunc);
        }

        public void removeGenerateClientFlags(GenerateClientFlags delegateFunc)
        {
            if (GenerateClientFlagsCheckFunctions.Contains(delegateFunc))
                GenerateClientFlagsCheckFunctions.Remove(delegateFunc);
        }

        public uint ExternalChecksGenerateClientFlags(UUID userID, UUID objectID)
        {
            SceneObjectPart part=m_scene.GetSceneObjectPart(objectID);

            if (part == null)
                return 0;

            // libomv will moan about PrimFlags.ObjectYouOfficer being
            // obsolete...
            #pragma warning disable 0612
            uint perms=part.GetEffectiveObjectFlags() |
                (uint)PrimFlags.ObjectModify |
                (uint)PrimFlags.ObjectCopy |
                (uint)PrimFlags.ObjectMove |
                (uint)PrimFlags.ObjectTransfer |
                (uint)PrimFlags.ObjectYouOwner |
                (uint)PrimFlags.ObjectAnyOwner |
                (uint)PrimFlags.ObjectOwnerModify |
                (uint)PrimFlags.ObjectYouOfficer;
            #pragma warning restore 0612

            foreach (GenerateClientFlags check in GenerateClientFlagsCheckFunctions)
            {
                perms &= check(userID, objectID);
            }
            return perms;
        }

        public delegate void SetBypassPermissions(bool value);
        private List<SetBypassPermissions> SetBypassPermissionsCheckFunctions = new List<SetBypassPermissions>();

        public void addSetBypassPermissions(SetBypassPermissions delegateFunc)
        {
            if (!SetBypassPermissionsCheckFunctions.Contains(delegateFunc))
                SetBypassPermissionsCheckFunctions.Add(delegateFunc);
        }

        public void removeSetBypassPermissions(SetBypassPermissions delegateFunc)
        {
            if (SetBypassPermissionsCheckFunctions.Contains(delegateFunc))
                SetBypassPermissionsCheckFunctions.Remove(delegateFunc);
        }

        public void ExternalChecksSetBypassPermissions(bool value)
        {
            foreach (SetBypassPermissions check in SetBypassPermissionsCheckFunctions)
            {
                check(value);
            }
        }

        public delegate bool BypassPermissions();
        private List<BypassPermissions> BypassPermissionsCheckFunctions = new List<BypassPermissions>();

        public void addBypassPermissions(BypassPermissions delegateFunc)
        {
            if (!BypassPermissionsCheckFunctions.Contains(delegateFunc))
                BypassPermissionsCheckFunctions.Add(delegateFunc);
        }

        public void removeBypassPermissions(BypassPermissions delegateFunc)
        {
            if (BypassPermissionsCheckFunctions.Contains(delegateFunc))
                BypassPermissionsCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksBypassPermissions()
        {
            foreach (BypassPermissions check in BypassPermissionsCheckFunctions)
            {
                if (check() == false)
                {
                    return false;
                }
            }
            return true;
        }

        public delegate bool PropagatePermissions();
        private List<PropagatePermissions> PropagatePermissionsCheckFunctions = new List<PropagatePermissions>();

        public void addPropagatePermissions(PropagatePermissions delegateFunc)
        {
            if (!PropagatePermissionsCheckFunctions.Contains(delegateFunc))
                PropagatePermissionsCheckFunctions.Add(delegateFunc);
        }

        public void removePropagatePermissions(PropagatePermissions delegateFunc)
        {
            if (PropagatePermissionsCheckFunctions.Contains(delegateFunc))
                PropagatePermissionsCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksPropagatePermissions()
        {
            foreach (PropagatePermissions check in PropagatePermissionsCheckFunctions)
            {
                if (check() == false)
                {
                    return false;
                }
            }
            return true;
        }

        #region REZ OBJECT
        public delegate bool CanRezObject(int objectCount, UUID owner, Vector3 objectPosition, Scene scene);
        private List<CanRezObject> CanRezObjectCheckFunctions = new List<CanRezObject>();

        public void addCheckRezObject(CanRezObject delegateFunc)
        {
            if (!CanRezObjectCheckFunctions.Contains(delegateFunc))
                CanRezObjectCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckRezObject(CanRezObject delegateFunc)
        {
            if (CanRezObjectCheckFunctions.Contains(delegateFunc))
                CanRezObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanRezObject(int objectCount, UUID owner, Vector3 objectPosition)
        {
            foreach (CanRezObject check in CanRezObjectCheckFunctions)
            {
                if (check(objectCount, owner,objectPosition, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region DELETE OBJECT
        public delegate bool CanDeleteObject(UUID objectID, UUID deleter, Scene scene);
        private List<CanDeleteObject> CanDeleteObjectCheckFunctions = new List<CanDeleteObject>();

        public void addCheckDeleteObject(CanDeleteObject delegateFunc)
        {
            if (!CanDeleteObjectCheckFunctions.Contains(delegateFunc))
                CanDeleteObjectCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckDeleteObject(CanDeleteObject delegateFunc)
        {
            if (CanDeleteObjectCheckFunctions.Contains(delegateFunc))
                CanDeleteObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanDeleteObject(UUID objectID, UUID deleter)
        {
            foreach (CanDeleteObject check in CanDeleteObjectCheckFunctions)
            {
                if (check(objectID,deleter,m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region TAKE OBJECT
        public delegate bool CanTakeObject(UUID objectID, UUID stealer, Scene scene);
        private List<CanTakeObject> CanTakeObjectCheckFunctions = new List<CanTakeObject>();

        public void addCheckTakeObject(CanTakeObject delegateFunc)
        {
            if (!CanTakeObjectCheckFunctions.Contains(delegateFunc))
                CanTakeObjectCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckTakeObject(CanTakeObject delegateFunc)
        {
            if (CanTakeObjectCheckFunctions.Contains(delegateFunc))
                CanTakeObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanTakeObject(UUID objectID, UUID AvatarTakingUUID)
        {
            foreach (CanTakeObject check in CanTakeObjectCheckFunctions)
            {
                if (check(objectID, AvatarTakingUUID, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region TAKE COPY OBJECT
        public delegate bool CanTakeCopyObject(UUID objectID, UUID userID, Scene inScene);
        private List<CanTakeCopyObject> CanTakeCopyObjectCheckFunctions = new List<CanTakeCopyObject>();

        public void addCheckTakeCopyObject(CanTakeCopyObject delegateFunc)
        {
            if (!CanTakeCopyObjectCheckFunctions.Contains(delegateFunc))
                CanTakeCopyObjectCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckTakeCopyObject(CanTakeCopyObject delegateFunc)
        {
            if (CanTakeCopyObjectCheckFunctions.Contains(delegateFunc))
                CanTakeCopyObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanTakeCopyObject(UUID objectID, UUID userID)
        {
            foreach (CanTakeCopyObject check in CanTakeCopyObjectCheckFunctions)
            {
                if (check(objectID,userID,m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region DUPLICATE OBJECT
        public delegate bool CanDuplicateObject(int objectCount, UUID objectID, UUID owner, Scene scene, Vector3 objectPosition);
        private List<CanDuplicateObject> CanDuplicateObjectCheckFunctions = new List<CanDuplicateObject>();

        public void addCheckDuplicateObject(CanDuplicateObject delegateFunc)
        {
            if (!CanDuplicateObjectCheckFunctions.Contains(delegateFunc))
                CanDuplicateObjectCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckDuplicateObject(CanDuplicateObject delegateFunc)
        {
            if (CanDuplicateObjectCheckFunctions.Contains(delegateFunc))
                CanDuplicateObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanDuplicateObject(int objectCount, UUID objectID, UUID owner, Vector3 objectPosition)
        {
            foreach (CanDuplicateObject check in CanDuplicateObjectCheckFunctions)
            {
                if (check(objectCount, objectID, owner, m_scene, objectPosition) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region EDIT OBJECT
        public delegate bool CanEditObject(UUID objectID, UUID editorID, Scene scene);
        private List<CanEditObject> CanEditObjectCheckFunctions = new List<CanEditObject>();

        public void addCheckEditObject(CanEditObject delegateFunc)
        {
            if (!CanEditObjectCheckFunctions.Contains(delegateFunc))
                CanEditObjectCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckEditObject(CanEditObject delegateFunc)
        {
            if (CanEditObjectCheckFunctions.Contains(delegateFunc))
                CanEditObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanEditObject(UUID objectID, UUID editorID)
        {
            foreach (CanEditObject check in CanEditObjectCheckFunctions)
            {
                if (check(objectID, editorID, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        public delegate bool CanEditObjectInventory(UUID objectID, UUID editorID, Scene scene);
        private List<CanEditObjectInventory> CanEditObjectInventoryCheckFunctions = new List<CanEditObjectInventory>();

        public void addCheckEditObjectInventory(CanEditObjectInventory delegateFunc)
        {
            if (!CanEditObjectInventoryCheckFunctions.Contains(delegateFunc))
                CanEditObjectInventoryCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckEditObjectInventory(CanEditObjectInventory delegateFunc)
        {
            if (CanEditObjectInventoryCheckFunctions.Contains(delegateFunc))
                CanEditObjectInventoryCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanEditObjectInventory(UUID objectID, UUID editorID)
        {
            foreach (CanEditObjectInventory check in CanEditObjectInventoryCheckFunctions)
            {
                if (check(objectID, editorID, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region MOVE OBJECT
        public delegate bool CanMoveObject(UUID objectID, UUID moverID, Scene scene);
        private List<CanMoveObject> CanMoveObjectCheckFunctions = new List<CanMoveObject>();

        public void addCheckMoveObject(CanMoveObject delegateFunc)
        {
            if (!CanMoveObjectCheckFunctions.Contains(delegateFunc))
                CanMoveObjectCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckMoveObject(CanMoveObject delegateFunc)
        {
            if (CanMoveObjectCheckFunctions.Contains(delegateFunc))
                CanMoveObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanMoveObject(UUID objectID, UUID moverID)
        {
            foreach (CanMoveObject check in CanMoveObjectCheckFunctions)
            {
                if (check(objectID,moverID,m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region OBJECT ENTRY
        public delegate bool CanObjectEntry(UUID objectID, Vector3 newPoint, Scene scene);
        private List<CanObjectEntry> CanObjectEntryCheckFunctions = new List<CanObjectEntry>();

        public void addCheckObjectEntry(CanObjectEntry delegateFunc)
        {
            if (!CanObjectEntryCheckFunctions.Contains(delegateFunc))
                CanObjectEntryCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckObjectEntry(CanObjectEntry delegateFunc)
        {
            if (CanObjectEntryCheckFunctions.Contains(delegateFunc))
                CanObjectEntryCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanObjectEntry(UUID objectID, Vector3 newPoint)
        {
            foreach (CanObjectEntry check in CanObjectEntryCheckFunctions)
            {
                if (check(objectID, newPoint, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region RETURN OBJECT
        public delegate bool CanReturnObject(UUID objectID, UUID returnerID, Scene scene);
        private List<CanReturnObject> CanReturnObjectCheckFunctions = new List<CanReturnObject>();

        public void addCheckReturnObject(CanReturnObject delegateFunc)
        {
            if (!CanReturnObjectCheckFunctions.Contains(delegateFunc))
                CanReturnObjectCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckReturnObject(CanReturnObject delegateFunc)
        {
            if (CanReturnObjectCheckFunctions.Contains(delegateFunc))
                CanReturnObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanReturnObject(UUID objectID, UUID returnerID)
        {
            foreach (CanReturnObject check in CanReturnObjectCheckFunctions)
            {
                if (check(objectID,returnerID,m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region INSTANT MESSAGE
        public delegate bool CanInstantMessage(UUID user, UUID target, Scene startScene);
        private List<CanInstantMessage> CanInstantMessageCheckFunctions = new List<CanInstantMessage>();

        public void addCheckInstantMessage(CanInstantMessage delegateFunc)
        {
            if (!CanInstantMessageCheckFunctions.Contains(delegateFunc))
                CanInstantMessageCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckInstantMessage(CanInstantMessage delegateFunc)
        {
            if (CanInstantMessageCheckFunctions.Contains(delegateFunc))
                CanInstantMessageCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanInstantMessage(UUID user, UUID target)
        {
            foreach (CanInstantMessage check in CanInstantMessageCheckFunctions)
            {
                if (check(user, target, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region INVENTORY TRANSFER
        public delegate bool CanInventoryTransfer(UUID user, UUID target, Scene startScene);
        private List<CanInventoryTransfer> CanInventoryTransferCheckFunctions = new List<CanInventoryTransfer>();

        public void addCheckInventoryTransfer(CanInventoryTransfer delegateFunc)
        {
            if (!CanInventoryTransferCheckFunctions.Contains(delegateFunc))
                CanInventoryTransferCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckInventoryTransfer(CanInventoryTransfer delegateFunc)
            {
                if (CanInventoryTransferCheckFunctions.Contains(delegateFunc))
                    CanInventoryTransferCheckFunctions.Remove(delegateFunc);
            }

        public bool ExternalChecksCanInventoryTransfer(UUID user, UUID target)
        {
            foreach (CanInventoryTransfer check in CanInventoryTransferCheckFunctions)
            {
                if (check(user, target, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region VIEW SCRIPT
        public delegate bool CanViewScript(UUID script, UUID objectID, UUID user, Scene scene);
        private List<CanViewScript> CanViewScriptCheckFunctions = new List<CanViewScript>();

        public void addCheckViewScript(CanViewScript delegateFunc)
        {
            if (!CanViewScriptCheckFunctions.Contains(delegateFunc))
                CanViewScriptCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckViewScript(CanViewScript delegateFunc)
        {
            if (CanViewScriptCheckFunctions.Contains(delegateFunc))
                CanViewScriptCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanViewScript(UUID script, UUID objectID, UUID user)
        {
            foreach (CanViewScript check in CanViewScriptCheckFunctions)
            {
                if (check(script, objectID, user, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        public delegate bool CanViewNotecard(UUID script, UUID objectID, UUID user, Scene scene);
        private List<CanViewNotecard> CanViewNotecardCheckFunctions = new List<CanViewNotecard>();

        public void addCheckViewNotecard(CanViewNotecard delegateFunc)
        {
            if (!CanViewNotecardCheckFunctions.Contains(delegateFunc))
                CanViewNotecardCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckViewNotecard(CanViewNotecard delegateFunc)
        {
            if (CanViewNotecardCheckFunctions.Contains(delegateFunc))
                CanViewNotecardCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanViewNotecard(UUID script, UUID objectID, UUID user)
        {
            foreach (CanViewNotecard check in CanViewNotecardCheckFunctions)
            {
                if (check(script, objectID, user, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region EDIT SCRIPT
        public delegate bool CanEditScript(UUID script, UUID objectID, UUID user, Scene scene);
        private List<CanEditScript> CanEditScriptCheckFunctions = new List<CanEditScript>();

        public void addCheckEditScript(CanEditScript delegateFunc)
        {
            if (!CanEditScriptCheckFunctions.Contains(delegateFunc))
                CanEditScriptCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckEditScript(CanEditScript delegateFunc)
        {
            if (CanEditScriptCheckFunctions.Contains(delegateFunc))
                CanEditScriptCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanEditScript(UUID script, UUID objectID, UUID user)
        {
            foreach (CanEditScript check in CanEditScriptCheckFunctions)
            {
                if (check(script, objectID, user, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        public delegate bool CanEditNotecard(UUID notecard, UUID objectID, UUID user, Scene scene);
        private List<CanEditNotecard> CanEditNotecardCheckFunctions = new List<CanEditNotecard>();

        public void addCheckEditNotecard(CanEditNotecard delegateFunc)
        {
            if (!CanEditNotecardCheckFunctions.Contains(delegateFunc))
                CanEditNotecardCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckEditNotecard(CanEditNotecard delegateFunc)
        {
            if (CanEditNotecardCheckFunctions.Contains(delegateFunc))
                CanEditNotecardCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanEditNotecard(UUID script, UUID objectID, UUID user)
            {
                foreach (CanEditNotecard check in CanEditNotecardCheckFunctions)
                {
                    if (check(script, objectID, user, m_scene) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

        #endregion

        #region RUN SCRIPT (When Script Placed in Object)
        public delegate bool CanRunScript(UUID script, UUID objectID, UUID user, Scene scene);
        private List<CanRunScript> CanRunScriptCheckFunctions = new List<CanRunScript>();

        public void addCheckRunScript(CanRunScript delegateFunc)
        {
            if (!CanRunScriptCheckFunctions.Contains(delegateFunc))
                CanRunScriptCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckRunScript(CanRunScript delegateFunc)
        {
            if (CanRunScriptCheckFunctions.Contains(delegateFunc))
                CanRunScriptCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanRunScript(UUID script, UUID objectID, UUID user)
        {
            foreach (CanRunScript check in CanRunScriptCheckFunctions)
            {
                if (check(script, objectID, user, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region START SCRIPT (When Script run box is Checked after placed in object)
        public delegate bool CanStartScript(UUID script, UUID user, Scene scene);
        private List<CanStartScript> CanStartScriptCheckFunctions = new List<CanStartScript>();

        public void addCheckStartScript(CanStartScript delegateFunc)
        {
            if (!CanStartScriptCheckFunctions.Contains(delegateFunc))
                CanStartScriptCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckStartScript(CanStartScript delegateFunc)
        {
            if (CanStartScriptCheckFunctions.Contains(delegateFunc))
                CanStartScriptCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanStartScript(UUID script, UUID user)
        {
            foreach (CanStartScript check in CanStartScriptCheckFunctions)
            {
                if (check(script, user, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region STOP SCRIPT (When Script run box is unchecked after placed in object)
        public delegate bool CanStopScript(UUID script, UUID user, Scene scene);
        private List<CanStopScript> CanStopScriptCheckFunctions = new List<CanStopScript>();

        public void addCheckStopScript(CanStopScript delegateFunc)
        {
            if (!CanStopScriptCheckFunctions.Contains(delegateFunc))
                CanStopScriptCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckStopScript(CanStopScript delegateFunc)
        {
            if (CanStopScriptCheckFunctions.Contains(delegateFunc))
                CanStopScriptCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanStopScript(UUID script, UUID user)
        {
            foreach (CanStopScript check in CanStopScriptCheckFunctions)
            {
                if (check(script, user, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region RESET SCRIPT
        public delegate bool CanResetScript(UUID script, UUID user, Scene scene);
        private List<CanResetScript> CanResetScriptCheckFunctions = new List<CanResetScript>();

        public void addCheckResetScript(CanResetScript delegateFunc)
        {
            if (!CanResetScriptCheckFunctions.Contains(delegateFunc))
                CanResetScriptCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckResetScript(CanResetScript delegateFunc)
        {
            if (CanResetScriptCheckFunctions.Contains(delegateFunc))
                CanResetScriptCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanResetScript(UUID script, UUID user)
        {
            foreach (CanResetScript check in CanResetScriptCheckFunctions)
            {
                if (check(script, user, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region TERRAFORM LAND
        public delegate bool CanTerraformLand(UUID user, Vector3 position, Scene requestFromScene);
        private List<CanTerraformLand> CanTerraformLandCheckFunctions = new List<CanTerraformLand>();

        public void addCheckTerraformLand(CanTerraformLand delegateFunc)
        {
            if (!CanTerraformLandCheckFunctions.Contains(delegateFunc))
                CanTerraformLandCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckTerraformLand(CanTerraformLand delegateFunc)
        {
            if (CanTerraformLandCheckFunctions.Contains(delegateFunc))
                CanTerraformLandCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanTerraformLand(UUID user, Vector3 pos)
        {
            foreach (CanTerraformLand check in CanTerraformLandCheckFunctions)
            {
                if (check(user, pos, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region RUN CONSOLE COMMAND
        public delegate bool CanRunConsoleCommand(UUID user, Scene requestFromScene);
        private List<CanRunConsoleCommand> CanRunConsoleCommandCheckFunctions = new List<CanRunConsoleCommand>();

        public void addCheckRunConsoleCommand(CanRunConsoleCommand delegateFunc)
        {
            if (!CanRunConsoleCommandCheckFunctions.Contains(delegateFunc))
                CanRunConsoleCommandCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckRunConsoleCommand(CanRunConsoleCommand delegateFunc)
        {
            if (CanRunConsoleCommandCheckFunctions.Contains(delegateFunc))
                CanRunConsoleCommandCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanRunConsoleCommand(UUID user)
        {
            foreach (CanRunConsoleCommand check in CanRunConsoleCommandCheckFunctions)
            {
                if (check(user, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region CAN ISSUE ESTATE COMMAND
        public delegate bool CanIssueEstateCommand(UUID user, Scene requestFromScene, bool ownerCommand);
        private List<CanIssueEstateCommand> CanIssueEstateCommandCheckFunctions = new List<CanIssueEstateCommand>();

        public void addCheckIssueEstateCommand(CanIssueEstateCommand delegateFunc)
        {
            if (!CanIssueEstateCommandCheckFunctions.Contains(delegateFunc))
                CanIssueEstateCommandCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckIssueEstateCommand(CanIssueEstateCommand delegateFunc)
        {
            if (CanIssueEstateCommandCheckFunctions.Contains(delegateFunc))
                CanIssueEstateCommandCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanIssueEstateCommand(UUID user, bool ownerCommand)
        {
            foreach (CanIssueEstateCommand check in CanIssueEstateCommandCheckFunctions)
            {
                if (check(user, m_scene, ownerCommand) == false)
                {
                    return false;
                }
            }
            return true;
        }
        #endregion

        #region CAN BE GODLIKE
        public delegate bool CanBeGodLike(UUID user, Scene requestFromScene);
        private List<CanBeGodLike> CanBeGodLikeCheckFunctions = new List<CanBeGodLike>();

        public void addCheckBeGodLike(CanBeGodLike delegateFunc)
        {
            if (!CanBeGodLikeCheckFunctions.Contains(delegateFunc))
                CanBeGodLikeCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckBeGodLike(CanBeGodLike delegateFunc)
        {
            if (CanBeGodLikeCheckFunctions.Contains(delegateFunc))
                CanBeGodLikeCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanBeGodLike(UUID user)
        {
            foreach (CanBeGodLike check in CanBeGodLikeCheckFunctions)
            {
                if (check(user, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }
        #endregion

        #region EDIT PARCEL
        public delegate bool CanEditParcel(UUID user, ILandObject parcel, Scene scene);
        private List<CanEditParcel> CanEditParcelCheckFunctions = new List<CanEditParcel>();

        public void addCheckEditParcel(CanEditParcel delegateFunc)
        {
            if (!CanEditParcelCheckFunctions.Contains(delegateFunc))
                CanEditParcelCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckEditParcel(CanEditParcel delegateFunc)
        {
            if (CanEditParcelCheckFunctions.Contains(delegateFunc))
                CanEditParcelCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanEditParcel(UUID user, ILandObject parcel)
        {
            foreach (CanEditParcel check in CanEditParcelCheckFunctions)
            {
                if (check(user, parcel, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }
        #endregion

        #region SELL PARCEL
        public delegate bool CanSellParcel(UUID user, ILandObject parcel, Scene scene);
        private List<CanSellParcel> CanSellParcelCheckFunctions = new List<CanSellParcel>();

        public void addCheckSellParcel(CanSellParcel delegateFunc)
        {
            if (!CanSellParcelCheckFunctions.Contains(delegateFunc))
                CanSellParcelCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckSellParcel(CanSellParcel delegateFunc)
        {
            if (CanSellParcelCheckFunctions.Contains(delegateFunc))
                CanSellParcelCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanSellParcel(UUID user, ILandObject parcel)
        {
            foreach (CanSellParcel check in CanSellParcelCheckFunctions)
            {
                if (check(user, parcel, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }
        #endregion

        #region ABANDON PARCEL
        public delegate bool CanAbandonParcel(UUID user, ILandObject parcel, Scene scene);
        private List<CanAbandonParcel> CanAbandonParcelCheckFunctions = new List<CanAbandonParcel>();

        public void addCheckAbandonParcel(CanAbandonParcel delegateFunc)
        {
            if (!CanAbandonParcelCheckFunctions.Contains(delegateFunc))
                CanAbandonParcelCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckAbandonParcel(CanAbandonParcel delegateFunc)
        {
            if (CanAbandonParcelCheckFunctions.Contains(delegateFunc))
                CanAbandonParcelCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanAbandonParcel(UUID user, ILandObject parcel)
        {
            foreach (CanAbandonParcel check in CanAbandonParcelCheckFunctions)
            {
                if (check(user, parcel, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }
        #endregion

        public delegate bool CanReclaimParcel(UUID user, ILandObject parcel, Scene scene);
        private List<CanReclaimParcel> CanReclaimParcelCheckFunctions = new List<CanReclaimParcel>();

        public void addCheckReclaimParcel(CanReclaimParcel delegateFunc)
        {
            if (!CanReclaimParcelCheckFunctions.Contains(delegateFunc))
                CanReclaimParcelCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckReclaimParcel(CanReclaimParcel delegateFunc)
        {
            if (CanReclaimParcelCheckFunctions.Contains(delegateFunc))
                CanReclaimParcelCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanReclaimParcel(UUID user, ILandObject parcel)
        {
            foreach (CanReclaimParcel check in CanReclaimParcelCheckFunctions)
            {
                if (check(user, parcel, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }
        public delegate bool CanBuyLand(UUID user, ILandObject parcel, Scene scene);
        private List<CanBuyLand> CanBuyLandCheckFunctions = new List<CanBuyLand>();

        public void addCheckCanBuyLand(CanBuyLand delegateFunc)
        {
            if (!CanBuyLandCheckFunctions.Contains(delegateFunc))
                CanBuyLandCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckCanBuyLand(CanBuyLand delegateFunc)
        {
            if (CanBuyLandCheckFunctions.Contains(delegateFunc))
                CanBuyLandCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanBuyLand(UUID user, ILandObject parcel)
        {
            foreach (CanBuyLand check in CanBuyLandCheckFunctions)
            {
                if (check(user, parcel, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        public delegate bool CanLinkObject(UUID user, UUID objectID);
        private List<CanLinkObject> CanLinkObjectCheckFunctions = new List<CanLinkObject>();

        public void addCheckCanLinkObject(CanLinkObject delegateFunc)
        {
            if (!CanLinkObjectCheckFunctions.Contains(delegateFunc))
                CanLinkObjectCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckCanLinkObject(CanLinkObject delegateFunc)
        {
            if (CanLinkObjectCheckFunctions.Contains(delegateFunc))
                CanLinkObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanLinkObject(UUID user, UUID objectID)
            {
                foreach (CanLinkObject check in CanLinkObjectCheckFunctions)
                {
                    if (check(user, objectID) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

        public delegate bool CanDelinkObject(UUID user, UUID objectID);
        private List<CanDelinkObject> CanDelinkObjectCheckFunctions = new List<CanDelinkObject>();

        public void addCheckCanDelinkObject(CanDelinkObject delegateFunc)
        {
            if (!CanDelinkObjectCheckFunctions.Contains(delegateFunc))
                CanDelinkObjectCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckCanDelinkObject(CanDelinkObject delegateFunc)
        {
            if (CanDelinkObjectCheckFunctions.Contains(delegateFunc))
                CanDelinkObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanDelinkObject(UUID user, UUID objectID)
        {
            foreach (CanDelinkObject check in CanDelinkObjectCheckFunctions)
            {
                if (check(user, objectID) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        public delegate bool CanCreateObjectInventory(int invType, UUID objectID, UUID userID);
        private List<CanCreateObjectInventory> CanCreateObjectInventoryCheckFunctions 
            = new List<CanCreateObjectInventory>();

        
        public void addCheckCanCreateObjectInventory(CanCreateObjectInventory delegateFunc)
        {
            if (!CanCreateObjectInventoryCheckFunctions.Contains(delegateFunc))
                CanCreateObjectInventoryCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckCanCreateObjectInventory(CanCreateObjectInventory delegateFunc)
        {
            if (CanCreateObjectInventoryCheckFunctions.Contains(delegateFunc))
                CanCreateObjectInventoryCheckFunctions.Remove(delegateFunc);
        }

        /// <summary>
        /// Check whether the specified user is allowed to directly create the given inventory type in a prim's
        /// inventory (e.g. the New Script button in the 1.21 Linden Lab client).
        /// </summary>
        /// <param name="invType"></param>
        /// <param name="objectID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>         
        public bool ExternalChecksCanCreateObjectInventory(int invType, UUID objectID, UUID userID)
        {
            foreach (CanCreateObjectInventory check in CanCreateObjectInventoryCheckFunctions)
            {
                if (check(invType, objectID, userID) == false)
                {
                    return false;
                }
            }
            
            return true;
        }

        public delegate bool CanCopyObjectInventory(UUID itemID, UUID objectID, UUID userID);
        private List<CanCopyObjectInventory> CanCopyObjectInventoryCheckFunctions = new List<CanCopyObjectInventory>();

        public void addCheckCanCopyObjectInventory(CanCopyObjectInventory delegateFunc)
        {
            if (!CanCopyObjectInventoryCheckFunctions.Contains(delegateFunc))
                CanCopyObjectInventoryCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckCanCopyObjectInventory(CanCopyObjectInventory delegateFunc)
        {
            if (CanCopyObjectInventoryCheckFunctions.Contains(delegateFunc))
                CanCopyObjectInventoryCheckFunctions.Remove(delegateFunc);
        }
       
        public bool ExternalChecksCanCopyObjectInventory(UUID itemID, UUID objectID, UUID userID)
        {
            foreach (CanCopyObjectInventory check in CanCopyObjectInventoryCheckFunctions)
            {
                if (check(itemID, objectID, userID) == false)
                {
                    return false;
                }
            }
            return true;
        }

        public delegate bool CanDeleteObjectInventory(UUID itemID, UUID objectID, UUID userID);
        private List<CanDeleteObjectInventory> CanDeleteObjectInventoryCheckFunctions 
            = new List<CanDeleteObjectInventory>();

        public void addCheckCanDeleteObjectInventory(CanDeleteObjectInventory delegateFunc)
        {
            if (!CanDeleteObjectInventoryCheckFunctions.Contains(delegateFunc))
                CanDeleteObjectInventoryCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckCanDeleteObjectInventory(CanDeleteObjectInventory delegateFunc)
        {
            if (CanDeleteObjectInventoryCheckFunctions.Contains(delegateFunc))
                CanDeleteObjectInventoryCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanDeleteObjectInventory(UUID itemID, UUID objectID, UUID userID)
        {
            foreach (CanDeleteObjectInventory check in CanDeleteObjectInventoryCheckFunctions)
            {
                if (check(itemID, objectID, userID) == false)
                {
                    return false;
                }
            }
            
            return true;
        }
        
        public delegate bool CanCreateUserInventory(int invType, UUID userID);
        private List<CanCreateUserInventory> CanCreateUserInventoryCheckFunctions 
            = new List<CanCreateUserInventory>();
        
        public void addCheckCanCreateUserInventory(CanCreateUserInventory delegateFunc)
        {
            if (!CanCreateUserInventoryCheckFunctions.Contains(delegateFunc))
                CanCreateUserInventoryCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckCanCreateUserInventory(CanCreateUserInventory delegateFunc)
        {
            if (CanCreateUserInventoryCheckFunctions.Contains(delegateFunc))
                CanCreateUserInventoryCheckFunctions.Remove(delegateFunc);
        }

        /// <summary>
        /// Check whether the specified user is allowed to create the given inventory type in their inventory.
        /// </summary>
        /// <param name="invType"></param>
        /// <param name="userID"></param>
        /// <returns></returns>         
        public bool ExternalChecksCanCreateUserInventory(int invType, UUID userID)
        {
            foreach (CanCreateUserInventory check in CanCreateUserInventoryCheckFunctions)
            {
                if (check(invType, userID) == false)
                {
                    return false;
                }
            }
            
            return true;
        } 
        
        public delegate bool CanEditUserInventory(UUID itemID, UUID userID);
        private List<CanEditUserInventory> CanEditUserInventoryCheckFunctions 
            = new List<CanEditUserInventory>();
        
        public void addCheckCanEditUserInventory(CanEditUserInventory delegateFunc)
        {
            if (!CanEditUserInventoryCheckFunctions.Contains(delegateFunc))
                CanEditUserInventoryCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckCanEditUserInventory(CanEditUserInventory delegateFunc)
        {
            if (CanEditUserInventoryCheckFunctions.Contains(delegateFunc))
                CanEditUserInventoryCheckFunctions.Remove(delegateFunc);
        }

        /// <summary>
        /// Check whether the specified user is allowed to edit the given inventory item within their own inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>         
        public bool ExternalChecksCanEditUserInventory(UUID itemID, UUID userID)
        {
            foreach (CanEditUserInventory check in CanEditUserInventoryCheckFunctions)
            {
                if (check(itemID, userID) == false)
                {
                    return false;
                }
            }
            
            return true;
        }         
        
        public delegate bool CanCopyUserInventory(UUID itemID, UUID userID);
        private List<CanCopyUserInventory> CanCopyUserInventoryCheckFunctions 
            = new List<CanCopyUserInventory>();
        
        public void addCheckCanCopyUserInventory(CanCopyUserInventory delegateFunc)
        {
            if (!CanCopyUserInventoryCheckFunctions.Contains(delegateFunc))
                CanCopyUserInventoryCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckCanCopyUserInventory(CanCopyUserInventory delegateFunc)
        {
            if (CanCopyUserInventoryCheckFunctions.Contains(delegateFunc))
                CanCopyUserInventoryCheckFunctions.Remove(delegateFunc);
        }        
                
        /// <summary>
        /// Check whether the specified user is allowed to copy the given inventory item from their own inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>         
        public bool ExternalChecksCanCopyUserInventory(UUID itemID, UUID userID)
        {
            foreach (CanCopyUserInventory check in CanCopyUserInventoryCheckFunctions)
            {
                if (check(itemID, userID) == false)
                {
                    return false;
                }
            }
            
            return true;
        }         
        
        public delegate bool CanDeleteUserInventory(UUID itemID, UUID userID);
        private List<CanDeleteUserInventory> CanDeleteUserInventoryCheckFunctions 
            = new List<CanDeleteUserInventory>();
        
        public void addCheckCanDeleteUserInventory(CanDeleteUserInventory delegateFunc)
        {
            if (!CanDeleteUserInventoryCheckFunctions.Contains(delegateFunc))
                CanDeleteUserInventoryCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckCanDeleteUserInventory(CanDeleteUserInventory delegateFunc)
        {
            if (CanDeleteUserInventoryCheckFunctions.Contains(delegateFunc))
                CanDeleteUserInventoryCheckFunctions.Remove(delegateFunc);
        }

        /// <summary>
        /// Check whether the specified user is allowed to edit the given inventory item within their own inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>         
        public bool ExternalChecksCanDeleteUserInventory(UUID itemID, UUID userID)
        {
            foreach (CanDeleteUserInventory check in CanDeleteUserInventoryCheckFunctions)
            {
                if (check(itemID, userID) == false)
                {
                    return false;
                }
            }
            
            return true;
        }         
        
        public delegate bool CanTeleport(UUID userID);
        private List<CanTeleport> CanTeleportCheckFunctions = new List<CanTeleport>();

        public void addCheckCanTeleport(CanTeleport delegateFunc)
        {
            if (!CanTeleportCheckFunctions.Contains(delegateFunc))
                CanTeleportCheckFunctions.Add(delegateFunc);
        }

        public void removeCheckCanTeleport(CanTeleport delegateFunc)
        {
            if (CanTeleportCheckFunctions.Contains(delegateFunc))
                CanTeleportCheckFunctions.Remove(delegateFunc);
        }

        public bool ExternalChecksCanTeleport(UUID userID)
        {
            foreach (CanTeleport check in CanTeleportCheckFunctions)
            {
                if (check(userID) == false)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
