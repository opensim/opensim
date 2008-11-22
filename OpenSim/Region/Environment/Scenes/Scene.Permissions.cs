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
    public class ScenePermissions
    {
        private Scene m_scene;

        public ScenePermissions(Scene scene)
        {
            m_scene = scene;
        }

        #region Object Permission Checks

        public delegate uint GenerateClientFlagsHandler(UUID userID, UUID objectIDID);
        private List<GenerateClientFlagsHandler> GenerateClientFlagsCheckFunctions = new List<GenerateClientFlagsHandler>();

        public void AddGenerateClientFlagsHandler(GenerateClientFlagsHandler delegateFunc)
        {
            if (!GenerateClientFlagsCheckFunctions.Contains(delegateFunc))
                GenerateClientFlagsCheckFunctions.Add(delegateFunc);
        }

        public void RemoveGenerateClientFlagsHandler(GenerateClientFlagsHandler delegateFunc)
        {
            if (GenerateClientFlagsCheckFunctions.Contains(delegateFunc))
                GenerateClientFlagsCheckFunctions.Remove(delegateFunc);
        }

        public uint GenerateClientFlags(UUID userID, UUID objectID)
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

            foreach (GenerateClientFlagsHandler check in GenerateClientFlagsCheckFunctions)
            {
                perms &= check(userID, objectID);
            }
            return perms;
        }

        public delegate void SetBypassPermissionsHandler(bool value);
        private List<SetBypassPermissionsHandler> SetBypassPermissionsCheckFunctions = new List<SetBypassPermissionsHandler>();

        public void AddSetBypassPermissionsHandler(SetBypassPermissionsHandler delegateFunc)
        {
            if (!SetBypassPermissionsCheckFunctions.Contains(delegateFunc))
                SetBypassPermissionsCheckFunctions.Add(delegateFunc);
        }

        public void RemoveSetBypassPermissionsHandler(SetBypassPermissionsHandler delegateFunc)
        {
            if (SetBypassPermissionsCheckFunctions.Contains(delegateFunc))
                SetBypassPermissionsCheckFunctions.Remove(delegateFunc);
        }

        public void SetBypassPermissions(bool value)
        {
            foreach (SetBypassPermissionsHandler check in SetBypassPermissionsCheckFunctions)
            {
                check(value);
            }
        }

        public delegate bool BypassPermissionsHandler();
        private List<BypassPermissionsHandler> BypassPermissionsCheckFunctions = new List<BypassPermissionsHandler>();

        public void AddBypassPermissionsHandler(BypassPermissionsHandler delegateFunc)
        {
            if (!BypassPermissionsCheckFunctions.Contains(delegateFunc))
                BypassPermissionsCheckFunctions.Add(delegateFunc);
        }

        public void RemoveBypassPermissionsHandler(BypassPermissionsHandler delegateFunc)
        {
            if (BypassPermissionsCheckFunctions.Contains(delegateFunc))
                BypassPermissionsCheckFunctions.Remove(delegateFunc);
        }

        public bool BypassPermissions()
        {
            foreach (BypassPermissionsHandler check in BypassPermissionsCheckFunctions)
            {
                if (check() == false)
                {
                    return false;
                }
            }
            return true;
        }

        public delegate bool PropagatePermissionsHandler();
        private List<PropagatePermissionsHandler> PropagatePermissionsCheckFunctions = new List<PropagatePermissionsHandler>();

        public void AddPropagatePermissionsHandler(PropagatePermissionsHandler delegateFunc)
        {
            if (!PropagatePermissionsCheckFunctions.Contains(delegateFunc))
                PropagatePermissionsCheckFunctions.Add(delegateFunc);
        }

        public void RemovePropagatePermissionsHandler(PropagatePermissionsHandler delegateFunc)
        {
            if (PropagatePermissionsCheckFunctions.Contains(delegateFunc))
                PropagatePermissionsCheckFunctions.Remove(delegateFunc);
        }

        public bool PropagatePermissions()
        {
            foreach (PropagatePermissionsHandler check in PropagatePermissionsCheckFunctions)
            {
                if (check() == false)
                {
                    return false;
                }
            }
            return true;
        }

        #region REZ OBJECT
        public delegate bool CanRezObjectHandler(int objectCount, UUID owner, Vector3 objectPosition, Scene scene);
        private List<CanRezObjectHandler> CanRezObjectCheckFunctions = new List<CanRezObjectHandler>();

        public void AddRezObjectHandler(CanRezObjectHandler delegateFunc)
        {
            if (!CanRezObjectCheckFunctions.Contains(delegateFunc))
                CanRezObjectCheckFunctions.Add(delegateFunc);
        }

        public void RemoveRezObjectHandler(CanRezObjectHandler delegateFunc)
        {
            if (CanRezObjectCheckFunctions.Contains(delegateFunc))
                CanRezObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool CanRezObject(int objectCount, UUID owner, Vector3 objectPosition)
        {
            foreach (CanRezObjectHandler check in CanRezObjectCheckFunctions)
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
        public delegate bool CanDeleteObjectHandler(UUID objectID, UUID deleter, Scene scene);
        private List<CanDeleteObjectHandler> CanDeleteObjectCheckFunctions = new List<CanDeleteObjectHandler>();

        public void AddDeleteObjectHandler(CanDeleteObjectHandler delegateFunc)
        {
            if (!CanDeleteObjectCheckFunctions.Contains(delegateFunc))
                CanDeleteObjectCheckFunctions.Add(delegateFunc);
        }

        public void RemoveDeleteObjectHandler(CanDeleteObjectHandler delegateFunc)
        {
            if (CanDeleteObjectCheckFunctions.Contains(delegateFunc))
                CanDeleteObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool CanDeleteObject(UUID objectID, UUID deleter)
        {
            foreach (CanDeleteObjectHandler check in CanDeleteObjectCheckFunctions)
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
        public delegate bool CanTakeObjectHandler(UUID objectID, UUID stealer, Scene scene);
        private List<CanTakeObjectHandler> CanTakeObjectCheckFunctions = new List<CanTakeObjectHandler>();

        public void AddTakeObjectHandler(CanTakeObjectHandler delegateFunc)
        {
            if (!CanTakeObjectCheckFunctions.Contains(delegateFunc))
                CanTakeObjectCheckFunctions.Add(delegateFunc);
        }

        public void RemoveTakeObjectHandler(CanTakeObjectHandler delegateFunc)
        {
            if (CanTakeObjectCheckFunctions.Contains(delegateFunc))
                CanTakeObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool CanTakeObject(UUID objectID, UUID AvatarTakingUUID)
        {
            foreach (CanTakeObjectHandler check in CanTakeObjectCheckFunctions)
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
        public delegate bool CanTakeCopyObjectHandler(UUID objectID, UUID userID, Scene inScene);
        private List<CanTakeCopyObjectHandler> CanTakeCopyObjectCheckFunctions = new List<CanTakeCopyObjectHandler>();

        public void AddTakeCopyObjectHandler(CanTakeCopyObjectHandler delegateFunc)
        {
            if (!CanTakeCopyObjectCheckFunctions.Contains(delegateFunc))
                CanTakeCopyObjectCheckFunctions.Add(delegateFunc);
        }

        public void RemoveTakeCopyObjectHandler(CanTakeCopyObjectHandler delegateFunc)
        {
            if (CanTakeCopyObjectCheckFunctions.Contains(delegateFunc))
                CanTakeCopyObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool CanTakeCopyObject(UUID objectID, UUID userID)
        {
            foreach (CanTakeCopyObjectHandler check in CanTakeCopyObjectCheckFunctions)
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
        public delegate bool CanDuplicateObjectHandler(int objectCount, UUID objectID, UUID owner, Scene scene, Vector3 objectPosition);
        private List<CanDuplicateObjectHandler> CanDuplicateObjectCheckFunctions = new List<CanDuplicateObjectHandler>();

        public void AddDuplicateObjectHandler(CanDuplicateObjectHandler delegateFunc)
        {
            if (!CanDuplicateObjectCheckFunctions.Contains(delegateFunc))
                CanDuplicateObjectCheckFunctions.Add(delegateFunc);
        }

        public void RemoveDuplicateObjectHandler(CanDuplicateObjectHandler delegateFunc)
        {
            if (CanDuplicateObjectCheckFunctions.Contains(delegateFunc))
                CanDuplicateObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool CanDuplicateObject(int objectCount, UUID objectID, UUID owner, Vector3 objectPosition)
        {
            foreach (CanDuplicateObjectHandler check in CanDuplicateObjectCheckFunctions)
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
        public delegate bool CanEditObjectHandler(UUID objectID, UUID editorID, Scene scene);
        private List<CanEditObjectHandler> CanEditObjectCheckFunctions = new List<CanEditObjectHandler>();

        public void AddEditObjectHandler(CanEditObjectHandler delegateFunc)
        {
            if (!CanEditObjectCheckFunctions.Contains(delegateFunc))
                CanEditObjectCheckFunctions.Add(delegateFunc);
        }

        public void RemoveEditObjectHandler(CanEditObjectHandler delegateFunc)
        {
            if (CanEditObjectCheckFunctions.Contains(delegateFunc))
                CanEditObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool CanEditObject(UUID objectID, UUID editorID)
        {
            foreach (CanEditObjectHandler check in CanEditObjectCheckFunctions)
            {
                if (check(objectID, editorID, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        public delegate bool CanEditObjectInventoryHandler(UUID objectID, UUID editorID, Scene scene);
        private List<CanEditObjectInventoryHandler> CanEditObjectInventoryCheckFunctions = new List<CanEditObjectInventoryHandler>();

        public void AddEditObjectInventoryHandler(CanEditObjectInventoryHandler delegateFunc)
        {
            if (!CanEditObjectInventoryCheckFunctions.Contains(delegateFunc))
                CanEditObjectInventoryCheckFunctions.Add(delegateFunc);
        }

        public void RemoveEditObjectInventoryHandler(CanEditObjectInventoryHandler delegateFunc)
        {
            if (CanEditObjectInventoryCheckFunctions.Contains(delegateFunc))
                CanEditObjectInventoryCheckFunctions.Remove(delegateFunc);
        }

        public bool CanEditObjectInventory(UUID objectID, UUID editorID)
        {
            foreach (CanEditObjectInventoryHandler check in CanEditObjectInventoryCheckFunctions)
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
        public delegate bool CanMoveObjectHandler(UUID objectID, UUID moverID, Scene scene);
        private List<CanMoveObjectHandler> CanMoveObjectCheckFunctions = new List<CanMoveObjectHandler>();

        public void AddMoveObjectHandler(CanMoveObjectHandler delegateFunc)
        {
            if (!CanMoveObjectCheckFunctions.Contains(delegateFunc))
                CanMoveObjectCheckFunctions.Add(delegateFunc);
        }

        public void RemoveMoveObjectHandler(CanMoveObjectHandler delegateFunc)
        {
            if (CanMoveObjectCheckFunctions.Contains(delegateFunc))
                CanMoveObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool CanMoveObject(UUID objectID, UUID moverID)
        {
            foreach (CanMoveObjectHandler check in CanMoveObjectCheckFunctions)
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
        public delegate bool CanObjectEntryHandler(UUID objectID, bool enteringRegion, Vector3 newPoint, Scene scene);
        private List<CanObjectEntryHandler> CanObjectEntryCheckFunctions = new List<CanObjectEntryHandler>();

        public void AddObjectEntryHandler(CanObjectEntryHandler delegateFunc)
        {
            if (!CanObjectEntryCheckFunctions.Contains(delegateFunc))
                CanObjectEntryCheckFunctions.Add(delegateFunc);
        }

        public void RemoveObjectEntryHandler(CanObjectEntryHandler delegateFunc)
        {
            if (CanObjectEntryCheckFunctions.Contains(delegateFunc))
                CanObjectEntryCheckFunctions.Remove(delegateFunc);
        }

        public bool CanObjectEntry(UUID objectID, bool enteringRegion, Vector3 newPoint)
        {
            foreach (CanObjectEntryHandler check in CanObjectEntryCheckFunctions)
            {
                if (check(objectID, enteringRegion, newPoint, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region RETURN OBJECT
        public delegate bool CanReturnObjectHandler(UUID objectID, UUID returnerID, Scene scene);
        private List<CanReturnObjectHandler> CanReturnObjectCheckFunctions = new List<CanReturnObjectHandler>();

        public void AddReturnObjectHandler(CanReturnObjectHandler delegateFunc)
        {
            if (!CanReturnObjectCheckFunctions.Contains(delegateFunc))
                CanReturnObjectCheckFunctions.Add(delegateFunc);
        }

        public void RemoveReturnObjectHandler(CanReturnObjectHandler delegateFunc)
        {
            if (CanReturnObjectCheckFunctions.Contains(delegateFunc))
                CanReturnObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool CanReturnObject(UUID objectID, UUID returnerID)
        {
            foreach (CanReturnObjectHandler check in CanReturnObjectCheckFunctions)
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
        public delegate bool CanInstantMessageHandler(UUID user, UUID target, Scene startScene);
        private List<CanInstantMessageHandler> CanInstantMessageCheckFunctions = new List<CanInstantMessageHandler>();

        public void AddInstantMessageHandler(CanInstantMessageHandler delegateFunc)
        {
            if (!CanInstantMessageCheckFunctions.Contains(delegateFunc))
                CanInstantMessageCheckFunctions.Add(delegateFunc);
        }

        public void RemoveInstantMessageHandler(CanInstantMessageHandler delegateFunc)
        {
            if (CanInstantMessageCheckFunctions.Contains(delegateFunc))
                CanInstantMessageCheckFunctions.Remove(delegateFunc);
        }

        public bool CanInstantMessage(UUID user, UUID target)
        {
            foreach (CanInstantMessageHandler check in CanInstantMessageCheckFunctions)
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
        public delegate bool CanInventoryTransferHandler(UUID user, UUID target, Scene startScene);
        private List<CanInventoryTransferHandler> CanInventoryTransferCheckFunctions = new List<CanInventoryTransferHandler>();

        public void AddInventoryTransferHandler(CanInventoryTransferHandler delegateFunc)
        {
            if (!CanInventoryTransferCheckFunctions.Contains(delegateFunc))
                CanInventoryTransferCheckFunctions.Add(delegateFunc);
        }

        public void RemoveInventoryTransferHandler(CanInventoryTransferHandler delegateFunc)
            {
                if (CanInventoryTransferCheckFunctions.Contains(delegateFunc))
                    CanInventoryTransferCheckFunctions.Remove(delegateFunc);
            }

        public bool CanInventoryTransfer(UUID user, UUID target)
        {
            foreach (CanInventoryTransferHandler check in CanInventoryTransferCheckFunctions)
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
        public delegate bool CanViewScriptHandler(UUID script, UUID objectID, UUID user, Scene scene);
        private List<CanViewScriptHandler> CanViewScriptCheckFunctions = new List<CanViewScriptHandler>();

        public void AddViewScriptHandler(CanViewScriptHandler delegateFunc)
        {
            if (!CanViewScriptCheckFunctions.Contains(delegateFunc))
                CanViewScriptCheckFunctions.Add(delegateFunc);
        }

        public void RemoveViewScriptHandler(CanViewScriptHandler delegateFunc)
        {
            if (CanViewScriptCheckFunctions.Contains(delegateFunc))
                CanViewScriptCheckFunctions.Remove(delegateFunc);
        }

        public bool CanViewScript(UUID script, UUID objectID, UUID user)
        {
            foreach (CanViewScriptHandler check in CanViewScriptCheckFunctions)
            {
                if (check(script, objectID, user, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        public delegate bool CanViewNotecardHandler(UUID script, UUID objectID, UUID user, Scene scene);
        private List<CanViewNotecardHandler> CanViewNotecardCheckFunctions = new List<CanViewNotecardHandler>();

        public void AddViewNotecardHandler(CanViewNotecardHandler delegateFunc)
        {
            if (!CanViewNotecardCheckFunctions.Contains(delegateFunc))
                CanViewNotecardCheckFunctions.Add(delegateFunc);
        }

        public void RemoveViewNotecardHandler(CanViewNotecardHandler delegateFunc)
        {
            if (CanViewNotecardCheckFunctions.Contains(delegateFunc))
                CanViewNotecardCheckFunctions.Remove(delegateFunc);
        }

        public bool CanViewNotecard(UUID script, UUID objectID, UUID user)
        {
            foreach (CanViewNotecardHandler check in CanViewNotecardCheckFunctions)
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
        public delegate bool CanEditScriptHandler(UUID script, UUID objectID, UUID user, Scene scene);
        private List<CanEditScriptHandler> CanEditScriptCheckFunctions = new List<CanEditScriptHandler>();

        public void AddEditScriptHandler(CanEditScriptHandler delegateFunc)
        {
            if (!CanEditScriptCheckFunctions.Contains(delegateFunc))
                CanEditScriptCheckFunctions.Add(delegateFunc);
        }

        public void RemoveEditScriptHandler(CanEditScriptHandler delegateFunc)
        {
            if (CanEditScriptCheckFunctions.Contains(delegateFunc))
                CanEditScriptCheckFunctions.Remove(delegateFunc);
        }

        public bool CanEditScript(UUID script, UUID objectID, UUID user)
        {
            foreach (CanEditScriptHandler check in CanEditScriptCheckFunctions)
            {
                if (check(script, objectID, user, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        public delegate bool CanEditNotecardHandler(UUID notecard, UUID objectID, UUID user, Scene scene);
        private List<CanEditNotecardHandler> CanEditNotecardCheckFunctions = new List<CanEditNotecardHandler>();

        public void AddEditNotecardHandler(CanEditNotecardHandler delegateFunc)
        {
            if (!CanEditNotecardCheckFunctions.Contains(delegateFunc))
                CanEditNotecardCheckFunctions.Add(delegateFunc);
        }

        public void RemoveEditNotecardHandler(CanEditNotecardHandler delegateFunc)
        {
            if (CanEditNotecardCheckFunctions.Contains(delegateFunc))
                CanEditNotecardCheckFunctions.Remove(delegateFunc);
        }

        public bool CanEditNotecard(UUID script, UUID objectID, UUID user)
            {
                foreach (CanEditNotecardHandler check in CanEditNotecardCheckFunctions)
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
        public delegate bool CanRunScriptHandler(UUID script, UUID objectID, UUID user, Scene scene);
        private List<CanRunScriptHandler> CanRunScriptCheckFunctions = new List<CanRunScriptHandler>();

        public void AddRunScriptHandler(CanRunScriptHandler delegateFunc)
        {
            if (!CanRunScriptCheckFunctions.Contains(delegateFunc))
                CanRunScriptCheckFunctions.Add(delegateFunc);
        }

        public void RemoveRunScriptHandler(CanRunScriptHandler delegateFunc)
        {
            if (CanRunScriptCheckFunctions.Contains(delegateFunc))
                CanRunScriptCheckFunctions.Remove(delegateFunc);
        }

        public bool CanRunScript(UUID script, UUID objectID, UUID user)
        {
            foreach (CanRunScriptHandler check in CanRunScriptCheckFunctions)
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
        public delegate bool CanStartScriptHandler(UUID script, UUID user, Scene scene);
        private List<CanStartScriptHandler> CanStartScriptCheckFunctions = new List<CanStartScriptHandler>();

        public void AddStartScriptHandler(CanStartScriptHandler delegateFunc)
        {
            if (!CanStartScriptCheckFunctions.Contains(delegateFunc))
                CanStartScriptCheckFunctions.Add(delegateFunc);
        }

        public void RemoveStartScriptHandler(CanStartScriptHandler delegateFunc)
        {
            if (CanStartScriptCheckFunctions.Contains(delegateFunc))
                CanStartScriptCheckFunctions.Remove(delegateFunc);
        }

        public bool CanStartScript(UUID script, UUID user)
        {
            foreach (CanStartScriptHandler check in CanStartScriptCheckFunctions)
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
        public delegate bool CanStopScriptHandler(UUID script, UUID user, Scene scene);
        private List<CanStopScriptHandler> CanStopScriptCheckFunctions = new List<CanStopScriptHandler>();

        public void AddStopScriptHandler(CanStopScriptHandler delegateFunc)
        {
            if (!CanStopScriptCheckFunctions.Contains(delegateFunc))
                CanStopScriptCheckFunctions.Add(delegateFunc);
        }

        public void RemoveStopScriptHandler(CanStopScriptHandler delegateFunc)
        {
            if (CanStopScriptCheckFunctions.Contains(delegateFunc))
                CanStopScriptCheckFunctions.Remove(delegateFunc);
        }

        public bool CanStopScript(UUID script, UUID user)
        {
            foreach (CanStopScriptHandler check in CanStopScriptCheckFunctions)
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
        public delegate bool CanResetScriptHandler(UUID script, UUID user, Scene scene);
        private List<CanResetScriptHandler> CanResetScriptCheckFunctions = new List<CanResetScriptHandler>();

        public void AddResetScriptHandler(CanResetScriptHandler delegateFunc)
        {
            if (!CanResetScriptCheckFunctions.Contains(delegateFunc))
                CanResetScriptCheckFunctions.Add(delegateFunc);
        }

        public void RemoveResetScriptHandler(CanResetScriptHandler delegateFunc)
        {
            if (CanResetScriptCheckFunctions.Contains(delegateFunc))
                CanResetScriptCheckFunctions.Remove(delegateFunc);
        }

        public bool CanResetScript(UUID script, UUID user)
        {
            foreach (CanResetScriptHandler check in CanResetScriptCheckFunctions)
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
        public delegate bool CanTerraformLandHandler(UUID user, Vector3 position, Scene requestFromScene);
        private List<CanTerraformLandHandler> CanTerraformLandCheckFunctions = new List<CanTerraformLandHandler>();

        public void AddTerraformLandHandler(CanTerraformLandHandler delegateFunc)
        {
            if (!CanTerraformLandCheckFunctions.Contains(delegateFunc))
                CanTerraformLandCheckFunctions.Add(delegateFunc);
        }

        public void RemoveTerraformLandHandler(CanTerraformLandHandler delegateFunc)
        {
            if (CanTerraformLandCheckFunctions.Contains(delegateFunc))
                CanTerraformLandCheckFunctions.Remove(delegateFunc);
        }

        public bool CanTerraformLand(UUID user, Vector3 pos)
        {
            foreach (CanTerraformLandHandler check in CanTerraformLandCheckFunctions)
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
        public delegate bool CanRunConsoleCommandHandler(UUID user, Scene requestFromScene);
        private List<CanRunConsoleCommandHandler> CanRunConsoleCommandCheckFunctions = new List<CanRunConsoleCommandHandler>();

        public void AddRunConsoleCommandHandler(CanRunConsoleCommandHandler delegateFunc)
        {
            if (!CanRunConsoleCommandCheckFunctions.Contains(delegateFunc))
                CanRunConsoleCommandCheckFunctions.Add(delegateFunc);
        }

        public void RemoveRunConsoleCommandHandler(CanRunConsoleCommandHandler delegateFunc)
        {
            if (CanRunConsoleCommandCheckFunctions.Contains(delegateFunc))
                CanRunConsoleCommandCheckFunctions.Remove(delegateFunc);
        }

        public bool CanRunConsoleCommand(UUID user)
        {
            foreach (CanRunConsoleCommandHandler check in CanRunConsoleCommandCheckFunctions)
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
        public delegate bool CanIssueEstateCommandHandler(UUID user, Scene requestFromScene, bool ownerCommand);
        private List<CanIssueEstateCommandHandler> CanIssueEstateCommandCheckFunctions = new List<CanIssueEstateCommandHandler>();

        public void AddIssueEstateCommandHandler(CanIssueEstateCommandHandler delegateFunc)
        {
            if (!CanIssueEstateCommandCheckFunctions.Contains(delegateFunc))
                CanIssueEstateCommandCheckFunctions.Add(delegateFunc);
        }

        public void RemoveIssueEstateCommandHandler(CanIssueEstateCommandHandler delegateFunc)
        {
            if (CanIssueEstateCommandCheckFunctions.Contains(delegateFunc))
                CanIssueEstateCommandCheckFunctions.Remove(delegateFunc);
        }

        public bool CanIssueEstateCommand(UUID user, bool ownerCommand)
        {
            foreach (CanIssueEstateCommandHandler check in CanIssueEstateCommandCheckFunctions)
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
        public delegate bool IsGodHandler(UUID user, Scene requestFromScene);
        private List<IsGodHandler> IsGodCheckFunctions = new List<IsGodHandler>();

        public void AddIsGodHandler(IsGodHandler delegateFunc)
        {
            if (!IsGodCheckFunctions.Contains(delegateFunc))
                IsGodCheckFunctions.Add(delegateFunc);
        }

        public void RemoveIsGodHandler(IsGodHandler delegateFunc)
        {
            if (IsGodCheckFunctions.Contains(delegateFunc))
                IsGodCheckFunctions.Remove(delegateFunc);
        }

        public bool IsGod(UUID user)
        {
            foreach (IsGodHandler check in IsGodCheckFunctions)
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
        public delegate bool CanEditParcelHandler(UUID user, ILandObject parcel, Scene scene);
        private List<CanEditParcelHandler> CanEditParcelCheckFunctions = new List<CanEditParcelHandler>();

        public void AddEditParcelHandler(CanEditParcelHandler delegateFunc)
        {
            if (!CanEditParcelCheckFunctions.Contains(delegateFunc))
                CanEditParcelCheckFunctions.Add(delegateFunc);
        }

        public void RemoveEditParcelHandler(CanEditParcelHandler delegateFunc)
        {
            if (CanEditParcelCheckFunctions.Contains(delegateFunc))
                CanEditParcelCheckFunctions.Remove(delegateFunc);
        }

        public bool CanEditParcel(UUID user, ILandObject parcel)
        {
            foreach (CanEditParcelHandler check in CanEditParcelCheckFunctions)
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
        public delegate bool CanSellParcelHandler(UUID user, ILandObject parcel, Scene scene);
        private List<CanSellParcelHandler> CanSellParcelCheckFunctions = new List<CanSellParcelHandler>();

        public void AddSellParcelHandler(CanSellParcelHandler delegateFunc)
        {
            if (!CanSellParcelCheckFunctions.Contains(delegateFunc))
                CanSellParcelCheckFunctions.Add(delegateFunc);
        }

        public void RemoveSellParcelHandler(CanSellParcelHandler delegateFunc)
        {
            if (CanSellParcelCheckFunctions.Contains(delegateFunc))
                CanSellParcelCheckFunctions.Remove(delegateFunc);
        }

        public bool CanSellParcel(UUID user, ILandObject parcel)
        {
            foreach (CanSellParcelHandler check in CanSellParcelCheckFunctions)
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
        public delegate bool CanAbandonParcelHandler(UUID user, ILandObject parcel, Scene scene);
        private List<CanAbandonParcelHandler> CanAbandonParcelCheckFunctions = new List<CanAbandonParcelHandler>();

        public void AddAbandonParcelHandler(CanAbandonParcelHandler delegateFunc)
        {
            if (!CanAbandonParcelCheckFunctions.Contains(delegateFunc))
                CanAbandonParcelCheckFunctions.Add(delegateFunc);
        }

        public void RemoveAbandonParcelHandler(CanAbandonParcelHandler delegateFunc)
        {
            if (CanAbandonParcelCheckFunctions.Contains(delegateFunc))
                CanAbandonParcelCheckFunctions.Remove(delegateFunc);
        }

        public bool CanAbandonParcel(UUID user, ILandObject parcel)
        {
            foreach (CanAbandonParcelHandler check in CanAbandonParcelCheckFunctions)
            {
                if (check(user, parcel, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }
        #endregion

        public delegate bool CanReclaimParcelHandler(UUID user, ILandObject parcel, Scene scene);
        private List<CanReclaimParcelHandler> CanReclaimParcelCheckFunctions = new List<CanReclaimParcelHandler>();

        public void AddReclaimParcelHandler(CanReclaimParcelHandler delegateFunc)
        {
            if (!CanReclaimParcelCheckFunctions.Contains(delegateFunc))
                CanReclaimParcelCheckFunctions.Add(delegateFunc);
        }

        public void RemoveReclaimParcelHandler(CanReclaimParcelHandler delegateFunc)
        {
            if (CanReclaimParcelCheckFunctions.Contains(delegateFunc))
                CanReclaimParcelCheckFunctions.Remove(delegateFunc);
        }

        public bool CanReclaimParcel(UUID user, ILandObject parcel)
        {
            foreach (CanReclaimParcelHandler check in CanReclaimParcelCheckFunctions)
            {
                if (check(user, parcel, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }
        public delegate bool CanBuyLandHandler(UUID user, ILandObject parcel, Scene scene);
        private List<CanBuyLandHandler> CanBuyLandCheckFunctions = new List<CanBuyLandHandler>();

        public void AddCanBuyLandHandler(CanBuyLandHandler delegateFunc)
        {
            if (!CanBuyLandCheckFunctions.Contains(delegateFunc))
                CanBuyLandCheckFunctions.Add(delegateFunc);
        }

        public void RemoveCanBuyLandHandler(CanBuyLandHandler delegateFunc)
        {
            if (CanBuyLandCheckFunctions.Contains(delegateFunc))
                CanBuyLandCheckFunctions.Remove(delegateFunc);
        }

        public bool CanBuyLand(UUID user, ILandObject parcel)
        {
            foreach (CanBuyLandHandler check in CanBuyLandCheckFunctions)
            {
                if (check(user, parcel, m_scene) == false)
                {
                    return false;
                }
            }
            return true;
        }

        public delegate bool CanLinkObjectHandler(UUID user, UUID objectID);
        private List<CanLinkObjectHandler> CanLinkObjectCheckFunctions = new List<CanLinkObjectHandler>();

        public void AddCanLinkObjectHandler(CanLinkObjectHandler delegateFunc)
        {
            if (!CanLinkObjectCheckFunctions.Contains(delegateFunc))
                CanLinkObjectCheckFunctions.Add(delegateFunc);
        }

        public void RemoveCanLinkObjectHandler(CanLinkObjectHandler delegateFunc)
        {
            if (CanLinkObjectCheckFunctions.Contains(delegateFunc))
                CanLinkObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool CanLinkObject(UUID user, UUID objectID)
            {
                foreach (CanLinkObjectHandler check in CanLinkObjectCheckFunctions)
                {
                    if (check(user, objectID) == false)
                    {
                        return false;
                    }
                }
                return true;
            }

        public delegate bool CanDelinkObjectHandler(UUID user, UUID objectID);
        private List<CanDelinkObjectHandler> CanDelinkObjectCheckFunctions = new List<CanDelinkObjectHandler>();

        public void AddCanDelinkObjectHandler(CanDelinkObjectHandler delegateFunc)
        {
            if (!CanDelinkObjectCheckFunctions.Contains(delegateFunc))
                CanDelinkObjectCheckFunctions.Add(delegateFunc);
        }

        public void RemoveCanDelinkObjectHandler(CanDelinkObjectHandler delegateFunc)
        {
            if (CanDelinkObjectCheckFunctions.Contains(delegateFunc))
                CanDelinkObjectCheckFunctions.Remove(delegateFunc);
        }

        public bool CanDelinkObject(UUID user, UUID objectID)
        {
            foreach (CanDelinkObjectHandler check in CanDelinkObjectCheckFunctions)
            {
                if (check(user, objectID) == false)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        public delegate bool CanCreateObjectInventoryHandler(int invType, UUID objectID, UUID userID);
        private List<CanCreateObjectInventoryHandler> CanCreateObjectInventoryCheckFunctions 
            = new List<CanCreateObjectInventoryHandler>();

        
        public void AddCanCreateObjectInventoryHandler(CanCreateObjectInventoryHandler delegateFunc)
        {
            if (!CanCreateObjectInventoryCheckFunctions.Contains(delegateFunc))
                CanCreateObjectInventoryCheckFunctions.Add(delegateFunc);
        }

        public void RemoveCanCreateObjectInventoryHandler(CanCreateObjectInventoryHandler delegateFunc)
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
        public bool CanCreateObjectInventory(int invType, UUID objectID, UUID userID)
        {
            foreach (CanCreateObjectInventoryHandler check in CanCreateObjectInventoryCheckFunctions)
            {
                if (check(invType, objectID, userID) == false)
                {
                    return false;
                }
            }
            
            return true;
        }

        public delegate bool CanCopyObjectInventoryHandler(UUID itemID, UUID objectID, UUID userID);
        private List<CanCopyObjectInventoryHandler> CanCopyObjectInventoryCheckFunctions = new List<CanCopyObjectInventoryHandler>();

        public void AddCanCopyObjectInventoryHandler(CanCopyObjectInventoryHandler delegateFunc)
        {
            if (!CanCopyObjectInventoryCheckFunctions.Contains(delegateFunc))
                CanCopyObjectInventoryCheckFunctions.Add(delegateFunc);
        }

        public void RemoveCanCopyObjectInventoryHandler(CanCopyObjectInventoryHandler delegateFunc)
        {
            if (CanCopyObjectInventoryCheckFunctions.Contains(delegateFunc))
                CanCopyObjectInventoryCheckFunctions.Remove(delegateFunc);
        }
       
        public bool CanCopyObjectInventory(UUID itemID, UUID objectID, UUID userID)
        {
            foreach (CanCopyObjectInventoryHandler check in CanCopyObjectInventoryCheckFunctions)
            {
                if (check(itemID, objectID, userID) == false)
                {
                    return false;
                }
            }
            return true;
        }

        public delegate bool CanDeleteObjectInventoryHandler(UUID itemID, UUID objectID, UUID userID);
        private List<CanDeleteObjectInventoryHandler> CanDeleteObjectInventoryCheckFunctions 
            = new List<CanDeleteObjectInventoryHandler>();

        public void AddCanDeleteObjectInventoryHandler(CanDeleteObjectInventoryHandler delegateFunc)
        {
            if (!CanDeleteObjectInventoryCheckFunctions.Contains(delegateFunc))
                CanDeleteObjectInventoryCheckFunctions.Add(delegateFunc);
        }

        public void RemoveCanDeleteObjectInventoryHandler(CanDeleteObjectInventoryHandler delegateFunc)
        {
            if (CanDeleteObjectInventoryCheckFunctions.Contains(delegateFunc))
                CanDeleteObjectInventoryCheckFunctions.Remove(delegateFunc);
        }

        public bool CanDeleteObjectInventory(UUID itemID, UUID objectID, UUID userID)
        {
            foreach (CanDeleteObjectInventoryHandler check in CanDeleteObjectInventoryCheckFunctions)
            {
                if (check(itemID, objectID, userID) == false)
                {
                    return false;
                }
            }
            
            return true;
        }
        
        public delegate bool CanCreateUserInventoryHandler(int invType, UUID userID);
        private List<CanCreateUserInventoryHandler> CanCreateUserInventoryCheckFunctions 
            = new List<CanCreateUserInventoryHandler>();
        
        public void AddCanCreateUserInventoryHandler(CanCreateUserInventoryHandler delegateFunc)
        {
            if (!CanCreateUserInventoryCheckFunctions.Contains(delegateFunc))
                CanCreateUserInventoryCheckFunctions.Add(delegateFunc);
        }

        public void RemoveCanCreateUserInventoryHandler(CanCreateUserInventoryHandler delegateFunc)
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
        public bool CanCreateUserInventory(int invType, UUID userID)
        {
            foreach (CanCreateUserInventoryHandler check in CanCreateUserInventoryCheckFunctions)
            {
                if (check(invType, userID) == false)
                {
                    return false;
                }
            }
            
            return true;
        } 
        
        public delegate bool CanEditUserInventoryHandler(UUID itemID, UUID userID);
        private List<CanEditUserInventoryHandler> CanEditUserInventoryCheckFunctions 
            = new List<CanEditUserInventoryHandler>();
        
        public void AddCanEditUserInventoryHandler(CanEditUserInventoryHandler delegateFunc)
        {
            if (!CanEditUserInventoryCheckFunctions.Contains(delegateFunc))
                CanEditUserInventoryCheckFunctions.Add(delegateFunc);
        }

        public void RemoveCanEditUserInventoryHandler(CanEditUserInventoryHandler delegateFunc)
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
        public bool CanEditUserInventory(UUID itemID, UUID userID)
        {
            foreach (CanEditUserInventoryHandler check in CanEditUserInventoryCheckFunctions)
            {
                if (check(itemID, userID) == false)
                {
                    return false;
                }
            }
            
            return true;
        }         
        
        public delegate bool CanCopyUserInventoryHandler(UUID itemID, UUID userID);
        private List<CanCopyUserInventoryHandler> CanCopyUserInventoryCheckFunctions 
            = new List<CanCopyUserInventoryHandler>();
        
        public void AddCanCopyUserInventoryHandler(CanCopyUserInventoryHandler delegateFunc)
        {
            if (!CanCopyUserInventoryCheckFunctions.Contains(delegateFunc))
                CanCopyUserInventoryCheckFunctions.Add(delegateFunc);
        }

        public void RemoveCanCopyUserInventoryHandler(CanCopyUserInventoryHandler delegateFunc)
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
        public bool CanCopyUserInventory(UUID itemID, UUID userID)
        {
            foreach (CanCopyUserInventoryHandler check in CanCopyUserInventoryCheckFunctions)
            {
                if (check(itemID, userID) == false)
                {
                    return false;
                }
            }
            
            return true;
        }         
        
        public delegate bool CanDeleteUserInventoryHandler(UUID itemID, UUID userID);
        private List<CanDeleteUserInventoryHandler> CanDeleteUserInventoryCheckFunctions 
            = new List<CanDeleteUserInventoryHandler>();
        
        public void AddCanDeleteUserInventoryHandler(CanDeleteUserInventoryHandler delegateFunc)
        {
            if (!CanDeleteUserInventoryCheckFunctions.Contains(delegateFunc))
                CanDeleteUserInventoryCheckFunctions.Add(delegateFunc);
        }

        public void RemoveCanDeleteUserInventoryHandler(CanDeleteUserInventoryHandler delegateFunc)
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
        public bool CanDeleteUserInventory(UUID itemID, UUID userID)
        {
            foreach (CanDeleteUserInventoryHandler check in CanDeleteUserInventoryCheckFunctions)
            {
                if (check(itemID, userID) == false)
                {
                    return false;
                }
            }
            
            return true;
        }         
        
        public delegate bool CanTeleportHandler(UUID userID);
        private List<CanTeleportHandler> CanTeleportCheckFunctions = new List<CanTeleportHandler>();

        public void AddCanTeleportHandler(CanTeleportHandler delegateFunc)
        {
            if (!CanTeleportCheckFunctions.Contains(delegateFunc))
                CanTeleportCheckFunctions.Add(delegateFunc);
        }

        public void RemoveCanTeleportHandler(CanTeleportHandler delegateFunc)
        {
            if (CanTeleportCheckFunctions.Contains(delegateFunc))
                CanTeleportCheckFunctions.Remove(delegateFunc);
        }

        public bool CanTeleport(UUID userID)
        {
            foreach (CanTeleportHandler check in CanTeleportCheckFunctions)
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
