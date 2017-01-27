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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.Framework.Scenes
{
    #region Delegates
    public delegate uint GenerateClientFlagsHandler(SceneObjectPart part, ScenePresence sp, uint curEffectivePerms);
    public delegate void SetBypassPermissionsHandler(bool value);
    public delegate bool BypassPermissionsHandler();
    public delegate bool PropagatePermissionsHandler();
    public delegate bool RezObjectHandler(int objectCount, UUID owner, Vector3 objectPosition);
    public delegate bool DeleteObjectHandlerByIDs(UUID objectID, UUID deleter);
    public delegate bool DeleteObjectHandler(SceneObjectGroup sog, ScenePresence sp);  
    public delegate bool TransferObjectHandler(UUID objectID, UUID recipient);
    public delegate bool TakeObjectHandler(SceneObjectGroup sog, ScenePresence sp);
    public delegate bool SellGroupObjectHandler(UUID userID, UUID groupID);
    public delegate bool TakeCopyObjectHandler(SceneObjectGroup sog, ScenePresence sp);
    public delegate bool DuplicateObjectHandler(SceneObjectGroup sog, ScenePresence sp);
    public delegate bool EditObjectByIDsHandler(UUID objectID, UUID editorID);
    public delegate bool EditObjectHandler(SceneObjectGroup sog, ScenePresence sp);
    public delegate bool EditObjectInventoryHandler(UUID objectID, UUID editorID);
    public delegate bool MoveObjectHandler(SceneObjectGroup sog, ScenePresence sp);
    public delegate bool ObjectEntryHandler(SceneObjectGroup sog, bool enteringRegion, Vector3 newPoint);
    public delegate bool ReturnObjectsHandler(ILandObject land, ScenePresence sp, List<SceneObjectGroup> objects);
    public delegate bool InstantMessageHandler(UUID user, UUID target);
    public delegate bool InventoryTransferHandler(UUID user, UUID target);
    public delegate bool ViewScriptHandler(UUID script, UUID objectID, UUID user);
    public delegate bool ViewNotecardHandler(UUID script, UUID objectID, UUID user);
    public delegate bool EditScriptHandler(UUID script, UUID objectID, UUID user);
    public delegate bool EditNotecardHandler(UUID notecard, UUID objectID, UUID user);
    public delegate bool RunScriptHandlerByIDs(UUID script, UUID objectID, UUID user);
    public delegate bool RunScriptHandler(TaskInventoryItem item, SceneObjectPart part);
    public delegate bool CompileScriptHandler(UUID ownerUUID, int scriptType);
    public delegate bool StartScriptHandler(UUID script, UUID user);
    public delegate bool StopScriptHandler(UUID script, UUID user);
    public delegate bool ResetScriptHandler(UUID prim, UUID script, UUID user);
    public delegate bool TerraformLandHandler(UUID user, Vector3 position);
    public delegate bool RunConsoleCommandHandler(UUID user);
    public delegate bool IssueEstateCommandHandler(UUID user, bool ownerCommand);
    public delegate bool IsGodHandler(UUID user);
    public delegate bool IsGridGodHandler(UUID user);
    public delegate bool IsAdministratorHandler(UUID user);
    public delegate bool IsEstateManagerHandler(UUID user);
    public delegate bool EditParcelHandler(UUID user, ILandObject parcel);
    public delegate bool EditParcelPropertiesHandler(UUID user, ILandObject parcel, GroupPowers p, bool allowManager);
    public delegate bool SellParcelHandler(UUID user, ILandObject parcel);
    public delegate bool AbandonParcelHandler(UUID user, ILandObject parcel);
    public delegate bool ReclaimParcelHandler(UUID user, ILandObject parcel);
    public delegate bool DeedParcelHandler(UUID user, ILandObject parcel);
    public delegate bool DeedObjectHandler(ScenePresence sp, SceneObjectGroup sog, UUID targetGroupID);
    public delegate bool BuyLandHandler(UUID user, ILandObject parcel);
    public delegate bool LinkObjectHandler(UUID user, UUID objectID);
    public delegate bool DelinkObjectHandler(UUID user, UUID objectID);
    public delegate bool CreateObjectInventoryHandler(int invType, UUID objectID, UUID userID);
    public delegate bool CopyObjectInventoryHandler(UUID itemID, UUID objectID, UUID userID);
    public delegate bool DoObjectInvToObjectInv(TaskInventoryItem item, SceneObjectPart sourcePart, SceneObjectPart destPart);
    public delegate bool DoDropInObjectInv(InventoryItemBase item, ScenePresence sp, SceneObjectPart destPart);
    public delegate bool DeleteObjectInventoryHandler(UUID itemID, UUID objectID, UUID userID);
    public delegate bool TransferObjectInventoryHandler(UUID itemID, UUID objectID, UUID userID);
    public delegate bool CreateUserInventoryHandler(int invType, UUID userID);
    public delegate bool EditUserInventoryHandler(UUID itemID, UUID userID);
    public delegate bool CopyUserInventoryHandler(UUID itemID, UUID userID);
    public delegate bool DeleteUserInventoryHandler(UUID itemID, UUID userID);
    public delegate bool TransferUserInventoryHandler(UUID itemID, UUID userID, UUID recipientID);
    public delegate bool TeleportHandler(UUID userID, Scene scene);
    public delegate bool ControlPrimMediaHandler(UUID userID, UUID primID, int face);
    public delegate bool InteractWithPrimMediaHandler(UUID userID, UUID primID, int face);
    #endregion

    public class ScenePermissions
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_scene;

        public ScenePermissions(Scene scene)
        {
            m_scene = scene;
        }

        #region Events
        public event GenerateClientFlagsHandler OnGenerateClientFlags;
        public event SetBypassPermissionsHandler OnSetBypassPermissions;
        public event BypassPermissionsHandler OnBypassPermissions;
        public event PropagatePermissionsHandler OnPropagatePermissions;
        public event RezObjectHandler OnRezObject;
        public event DeleteObjectHandlerByIDs OnDeleteObjectByIDs;
        public event DeleteObjectHandler OnDeleteObject;
        public event TransferObjectHandler OnTransferObject;
        public event TakeObjectHandler OnTakeObject;
        public event SellGroupObjectHandler OnSellGroupObject;
        public event TakeCopyObjectHandler OnTakeCopyObject;
        public event DuplicateObjectHandler OnDuplicateObject;
        public event EditObjectByIDsHandler OnEditObjectByIDs;
        public event EditObjectHandler OnEditObject;
        public event EditObjectInventoryHandler OnEditObjectInventory;
        public event MoveObjectHandler OnMoveObject;
        public event ObjectEntryHandler OnObjectEntry;
        public event ReturnObjectsHandler OnReturnObjects;
        public event InstantMessageHandler OnInstantMessage;
        public event InventoryTransferHandler OnInventoryTransfer;
        public event ViewScriptHandler OnViewScript;
        public event ViewNotecardHandler OnViewNotecard;
        public event EditScriptHandler OnEditScript;
        public event EditNotecardHandler OnEditNotecard;
        public event RunScriptHandlerByIDs OnRunScriptByIDs;
        public event RunScriptHandler OnRunScript;
        public event CompileScriptHandler OnCompileScript;
        public event StartScriptHandler OnStartScript;
        public event StopScriptHandler OnStopScript;
        public event ResetScriptHandler OnResetScript;
        public event TerraformLandHandler OnTerraformLand;
        public event RunConsoleCommandHandler OnRunConsoleCommand;
        public event IssueEstateCommandHandler OnIssueEstateCommand;
        public event IsGridGodHandler OnIsGridGod;
        public event IsAdministratorHandler OnIsAdministrator;
        public event IsEstateManagerHandler OnIsEstateManager;
//        public event EditParcelHandler OnEditParcel;
        public event EditParcelPropertiesHandler OnEditParcelProperties;
        public event SellParcelHandler OnSellParcel;
        public event AbandonParcelHandler OnAbandonParcel;
        public event ReclaimParcelHandler OnReclaimParcel;
        public event DeedParcelHandler OnDeedParcel;
        public event DeedObjectHandler OnDeedObject;
        public event BuyLandHandler OnBuyLand;
        public event LinkObjectHandler OnLinkObject;
        public event DelinkObjectHandler OnDelinkObject;
        public event CreateObjectInventoryHandler OnCreateObjectInventory;
        public event CopyObjectInventoryHandler OnCopyObjectInventory;
        public event DoObjectInvToObjectInv OnDoObjectInvToObjectInv;
        public event DoDropInObjectInv OnDropInObjectInv;
        public event DeleteObjectInventoryHandler OnDeleteObjectInventory;
        public event TransferObjectInventoryHandler OnTransferObjectInventory;
        public event CreateUserInventoryHandler OnCreateUserInventory;
        public event EditUserInventoryHandler OnEditUserInventory;
        public event CopyUserInventoryHandler OnCopyUserInventory;
        public event DeleteUserInventoryHandler OnDeleteUserInventory;
        public event TransferUserInventoryHandler OnTransferUserInventory;
        public event TeleportHandler OnTeleport;
        public event ControlPrimMediaHandler OnControlPrimMedia;
        public event InteractWithPrimMediaHandler OnInteractWithPrimMedia;
        #endregion

        #region Object Permission Checks

        public uint GenerateClientFlags( SceneObjectPart part, ScenePresence sp)
        {
            // libomv will moan about PrimFlags.ObjectYouOfficer being
            // obsolete...
#pragma warning disable 0612
            const PrimFlags DEFAULT_FLAGS =
                PrimFlags.ObjectModify |
                PrimFlags.ObjectCopy |
                PrimFlags.ObjectMove |
                PrimFlags.ObjectTransfer |
                PrimFlags.ObjectYouOwner |
                PrimFlags.ObjectAnyOwner |
                PrimFlags.ObjectOwnerModify;
#pragma warning restore 0612

            if (part == null)
                return 0;

            uint perms = part.GetEffectiveObjectFlags() | (uint)DEFAULT_FLAGS;

            GenerateClientFlagsHandler handlerGenerateClientFlags = OnGenerateClientFlags;
            if (handlerGenerateClientFlags != null)
            {
                Delegate[] list = handlerGenerateClientFlags.GetInvocationList();
                foreach (GenerateClientFlagsHandler check in list)
                {
                    perms &= check(part, sp, perms);
                }
            }
            return perms;
        }

        public void SetBypassPermissions(bool value)
        {
            SetBypassPermissionsHandler handler = OnSetBypassPermissions;
            if (handler != null)
                handler(value);
        }

        public bool BypassPermissions()
        {
            BypassPermissionsHandler handler = OnBypassPermissions;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (BypassPermissionsHandler h in list)
                {
                    if (h() == false)
                        return false;
                }
            }
            return true;
        }

        public bool PropagatePermissions()
        {
            PropagatePermissionsHandler handler = OnPropagatePermissions;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (PropagatePermissionsHandler h in list)
                {
                    if (h() == false)
                        return false;
                }
            }
            return true;
        }

        #region REZ OBJECT
        public bool CanRezObject(int objectCount, UUID owner, Vector3 objectPosition)
        {
            RezObjectHandler handler = OnRezObject;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (RezObjectHandler h in list)
                {
                    if (h(objectCount, owner,objectPosition) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region DELETE OBJECT
        public bool CanDeleteObject(UUID objectID, UUID deleter)
        {
            bool result = true;

            DeleteObjectHandlerByIDs handler = OnDeleteObjectByIDs;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (DeleteObjectHandlerByIDs h in list)
                {
                    if (h(objectID, deleter) == false)
                    {
                        result = false;
                        break;
                    }
                }
            }

            return result;
        }

        public bool CanDeleteObject(SceneObjectGroup sog, IClientAPI client)
        {
            bool result = true;

            DeleteObjectHandler handler = OnDeleteObject;
            if (handler != null)
            {
               if(sog == null || client == null || client.SceneAgent == null)
                    return false;

                ScenePresence sp = client.SceneAgent as ScenePresence;

                Delegate[] list = handler.GetInvocationList();
                foreach (DeleteObjectHandler h in list)
                {
                    if (h(sog, sp) == false)
                    {
                        result = false;
                        break;
                    }
                }
            }

            return result;
        }

        public bool CanTransferObject(UUID objectID, UUID recipient)
        {
            bool result = true;

            TransferObjectHandler handler = OnTransferObject;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (TransferObjectHandler h in list)
                {
                    if (h(objectID, recipient) == false)
                    {
                        result = false;
                        break;
                    }
                }
            }

            return result;
        }

        #endregion

        #region TAKE OBJECT
        public bool CanTakeObject(SceneObjectGroup sog, ScenePresence sp)
        {
            bool result = true;

            TakeObjectHandler handler = OnTakeObject;
            if (handler != null)
            {
                if(sog == null || sp == null)
                    return false;

                Delegate[] list = handler.GetInvocationList();
                foreach (TakeObjectHandler h in list)
                {
                    if (h(sog, sp) == false)
                    {
                        result = false;
                        break;
                    }
                }
            }

//            m_log.DebugFormat(
//                "[SCENE PERMISSIONS]: CanTakeObject() fired for object {0}, taker {1}, result {2}",
//                objectID, AvatarTakingUUID, result);

            return result;
        }

        #endregion

        #region SELL GROUP OBJECT
        public bool CanSellGroupObject(UUID userID, UUID groupID)
        {
            bool result = true;

            SellGroupObjectHandler handler = OnSellGroupObject;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (SellGroupObjectHandler h in list)
                {
                    if (h(userID, groupID) == false)
                    {
                        result = false;
                        break;
                    }
                }
            }

            //m_log.DebugFormat(
            //    "[SCENE PERMISSIONS]: CanSellGroupObject() fired for user {0}, group {1}, result {2}",
            //    userID, groupID, result);

            return result;
        }

        #endregion


        #region TAKE COPY OBJECT
        public bool CanTakeCopyObject(SceneObjectGroup sog, ScenePresence sp)
        {
            bool result = true;

            TakeCopyObjectHandler handler = OnTakeCopyObject;
            if (handler != null)
            {
                if(sog == null || sp == null)
                    return false;
                Delegate[] list = handler.GetInvocationList();
                foreach (TakeCopyObjectHandler h in list)
                {
                    if (h(sog, sp) == false)
                    {
                        result = false;
                        break;
                    }
                }
            }

//            m_log.DebugFormat(
//                "[SCENE PERMISSIONS]: CanTakeCopyObject() fired for object {0}, user {1}, result {2}",
//                objectID, userID, result);

            return result;
        }

        #endregion

        #region DUPLICATE OBJECT
        public bool CanDuplicateObject(SceneObjectGroup sog, UUID agentID)
        {
            DuplicateObjectHandler handler = OnDuplicateObject;
            if (handler != null)
            {
                if(sog == null || sog.IsDeleted)
                    return false;
                ScenePresence sp = m_scene.GetScenePresence(agentID);
                if(sp == null || sp.IsDeleted)
                    return false;
                Delegate[] list = handler.GetInvocationList();
                foreach (DuplicateObjectHandler h in list)
                {
                    if (h(sog, sp) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region persence EDIT or MOVE OBJECT
        private  const uint CANSELECTMASK = (uint)(
            PrimFlags.ObjectMove |
            PrimFlags.ObjectModify |
            PrimFlags.ObjectOwnerModify
            );

        public bool CanChangeSelectedState(SceneObjectPart part, ScenePresence sp)
        {
            uint perms = GenerateClientFlags(part, sp);
            return (perms & CANSELECTMASK) != 0;
        }

        #endregion
        #region EDIT OBJECT
        public bool CanEditObject(UUID objectID, UUID editorID)
        {
            EditObjectByIDsHandler handler = OnEditObjectByIDs;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (EditObjectByIDsHandler h in list)
                {
                    if (h(objectID, editorID) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanEditObject(SceneObjectGroup sog, IClientAPI client)
        {
            EditObjectHandler handler = OnEditObject;
            if (handler != null)
            {
                if(sog == null || client == null || client.SceneAgent == null)
                    return false;

                ScenePresence sp = client.SceneAgent as ScenePresence;

                Delegate[] list = handler.GetInvocationList();
                foreach (EditObjectHandler h in list)
                {
                    if (h(sog, sp) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanEditObjectInventory(UUID objectID, UUID editorID)
        {
            EditObjectInventoryHandler handler = OnEditObjectInventory;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (EditObjectInventoryHandler h in list)
                {
                    if (h(objectID, editorID) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region MOVE OBJECT
        public bool CanMoveObject(SceneObjectGroup sog, IClientAPI client)
        {
            MoveObjectHandler handler = OnMoveObject;
            if (handler != null)
            {
                if(sog == null || client == null || client.SceneAgent == null)
                    return false;

                ScenePresence sp = client.SceneAgent as ScenePresence;

                Delegate[] list = handler.GetInvocationList();
                foreach (MoveObjectHandler h in list)
                {
                    if (h(sog, sp) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region OBJECT ENTRY
        public bool CanObjectEntry(SceneObjectGroup sog, bool enteringRegion, Vector3 newPoint)
        {
            ObjectEntryHandler handler = OnObjectEntry;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (ObjectEntryHandler h in list)
                {
                    if (h(sog, enteringRegion, newPoint) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region RETURN OBJECT
        public bool CanReturnObjects(ILandObject land, IClientAPI client, List<SceneObjectGroup> objects)
        {
            bool result = true;

            ReturnObjectsHandler handler = OnReturnObjects;
            if (handler != null)
            {
                if(objects == null)
                    return false;
                
                ScenePresence sp = null;
                if(client != null && client.SceneAgent != null)
                    sp = client.SceneAgent as ScenePresence;

                Delegate[] list = handler.GetInvocationList();
                foreach (ReturnObjectsHandler h in list)
                {
                    if (h(land, sp, objects) == false)
                    {
                        result = false;
                        break;
                    }
                }
            }

//            m_log.DebugFormat(
//                "[SCENE PERMISSIONS]: CanReturnObjects() fired for user {0} for {1} objects on {2}, result {3}",
//                user, objects.Count, land.LandData.Name, result);

            return result;
        }

        #endregion

        #region INSTANT MESSAGE
        public bool CanInstantMessage(UUID user, UUID target)
        {
            InstantMessageHandler handler = OnInstantMessage;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (InstantMessageHandler h in list)
                {
                    if (h(user, target) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region INVENTORY TRANSFER
        public bool CanInventoryTransfer(UUID user, UUID target)
        {
            InventoryTransferHandler handler = OnInventoryTransfer;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (InventoryTransferHandler h in list)
                {
                    if (h(user, target) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region VIEW SCRIPT
        public bool CanViewScript(UUID script, UUID objectID, UUID user)
        {
            ViewScriptHandler handler = OnViewScript;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (ViewScriptHandler h in list)
                {
                    if (h(script, objectID, user) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanViewNotecard(UUID script, UUID objectID, UUID user)
        {
            ViewNotecardHandler handler = OnViewNotecard;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (ViewNotecardHandler h in list)
                {
                    if (h(script, objectID, user) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region EDIT SCRIPT
        public bool CanEditScript(UUID script, UUID objectID, UUID user)
        {
            EditScriptHandler handler = OnEditScript;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (EditScriptHandler h in list)
                {
                    if (h(script, objectID, user) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanEditNotecard(UUID script, UUID objectID, UUID user)
        {
            EditNotecardHandler handler = OnEditNotecard;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (EditNotecardHandler h in list)
                {
                    if (h(script, objectID, user) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region RUN SCRIPT (When Script Placed in Object)
        public bool CanRunScript(UUID script, UUID objectID, UUID user)
        {
            RunScriptHandlerByIDs handler = OnRunScriptByIDs;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (RunScriptHandlerByIDs h in list)
                {
                    if (h(script, objectID, user) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanRunScript(TaskInventoryItem item, SceneObjectPart part)
        {
            RunScriptHandler handler = OnRunScript;
            if (handler != null)
            {
                if(item == null || part == null)
                    return false;
                Delegate[] list = handler.GetInvocationList();
                foreach (RunScriptHandler h in list)
                {
                    if (h(item, part) == false)
                        return false;
                }
            }
            return true;
        }


        #endregion

        #region COMPILE SCRIPT (When Script needs to get (re)compiled)
        public bool CanCompileScript(UUID ownerUUID, int scriptType)
        {
            CompileScriptHandler handler = OnCompileScript;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (CompileScriptHandler h in list)
                {
                    if (h(ownerUUID, scriptType) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region START SCRIPT (When Script run box is Checked after placed in object)
        public bool CanStartScript(UUID script, UUID user)
        {
            StartScriptHandler handler = OnStartScript;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (StartScriptHandler h in list)
                {
                    if (h(script, user) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region STOP SCRIPT (When Script run box is unchecked after placed in object)
        public bool CanStopScript(UUID script, UUID user)
        {
            StopScriptHandler handler = OnStopScript;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (StopScriptHandler h in list)
                {
                    if (h(script, user) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region RESET SCRIPT
        public bool CanResetScript(UUID prim, UUID script, UUID user)
        {
            ResetScriptHandler handler = OnResetScript;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (ResetScriptHandler h in list)
                {
                    if (h(prim, script, user) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region TERRAFORM LAND
        public bool CanTerraformLand(UUID user, Vector3 pos)
        {
            TerraformLandHandler handler = OnTerraformLand;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (TerraformLandHandler h in list)
                {
                    if (h(user, pos) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region RUN CONSOLE COMMAND
        public bool CanRunConsoleCommand(UUID user)
        {
            RunConsoleCommandHandler handler = OnRunConsoleCommand;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (RunConsoleCommandHandler h in list)
                {
                    if (h(user) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region CAN ISSUE ESTATE COMMAND
        public bool CanIssueEstateCommand(UUID user, bool ownerCommand)
        {
            IssueEstateCommandHandler handler = OnIssueEstateCommand;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (IssueEstateCommandHandler h in list)
                {
                    if (h(user, ownerCommand) == false)
                        return false;
                }
            }
            return true;
        }
        #endregion

        #region CAN BE GODLIKE
        public bool IsGod(UUID user)
        {
            IsAdministratorHandler handler = OnIsAdministrator;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (IsAdministratorHandler h in list)
                {
                    if (h(user) == false)
                        return false;
                }
            }
            return true;
        }

        public bool IsGridGod(UUID user)
        {
            IsGridGodHandler handler = OnIsGridGod;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (IsGridGodHandler h in list)
                {
                    if (h(user) == false)
                        return false;
                }
            }
            return true;
        }

        public bool IsAdministrator(UUID user)
        {
            IsAdministratorHandler handler = OnIsAdministrator;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (IsAdministratorHandler h in list)
                {
                    if (h(user) == false)
                        return false;
                }
            }
            return true;
        }
        #endregion

        public bool IsEstateManager(UUID user)
        {
            IsEstateManagerHandler handler = OnIsEstateManager;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (IsEstateManagerHandler h in list)
                {
                    if (h(user) == false)
                        return false;
                }
            }
            return true;
        }

        #region EDIT PARCEL

        public bool CanEditParcelProperties(UUID user, ILandObject parcel, GroupPowers p, bool allowManager)
        {
            EditParcelPropertiesHandler handler = OnEditParcelProperties;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (EditParcelPropertiesHandler h in list)
                {
                    if (h(user, parcel, p, allowManager) == false)
                        return false;
                }
            }
            return true;
        }
        #endregion

        #region SELL PARCEL
        public bool CanSellParcel(UUID user, ILandObject parcel)
        {
            SellParcelHandler handler = OnSellParcel;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (SellParcelHandler h in list)
                {
                    if (h(user, parcel) == false)
                        return false;
                }
            }
            return true;
        }
        #endregion

        #region ABANDON PARCEL
        public bool CanAbandonParcel(UUID user, ILandObject parcel)
        {
            AbandonParcelHandler handler = OnAbandonParcel;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (AbandonParcelHandler h in list)
                {
                    if (h(user, parcel) == false)
                        return false;
                }
            }
            return true;
        }
        #endregion

        public bool CanReclaimParcel(UUID user, ILandObject parcel)
        {
            ReclaimParcelHandler handler = OnReclaimParcel;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (ReclaimParcelHandler h in list)
                {
                    if (h(user, parcel) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanDeedParcel(UUID user, ILandObject parcel)
        {
            DeedParcelHandler handler = OnDeedParcel;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (DeedParcelHandler h in list)
                {
                    if (h(user, parcel) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanDeedObject(IClientAPI client, SceneObjectGroup sog, UUID targetGroupID)
        {
            DeedObjectHandler handler = OnDeedObject;
            if (handler != null)
            {
               if(sog == null || client == null || client.SceneAgent == null || targetGroupID == UUID.Zero)
                    return false;

                ScenePresence sp = client.SceneAgent as ScenePresence;

                Delegate[] list = handler.GetInvocationList();
                foreach (DeedObjectHandler h in list)
                {
                    if (h(sp, sog, targetGroupID) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanBuyLand(UUID user, ILandObject parcel)
        {
            BuyLandHandler handler = OnBuyLand;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (BuyLandHandler h in list)
                {
                    if (h(user, parcel) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanLinkObject(UUID user, UUID objectID)
        {
            LinkObjectHandler handler = OnLinkObject;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (LinkObjectHandler h in list)
                {
                    if (h(user, objectID) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanDelinkObject(UUID user, UUID objectID)
        {
            DelinkObjectHandler handler = OnDelinkObject;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (DelinkObjectHandler h in list)
                {
                    if (h(user, objectID) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        /// Check whether the specified user is allowed to directly create the given inventory type in a prim's
        /// inventory (e.g. the New Script button in the 1.21 Linden Lab client).
        /// </summary>
        /// <param name="invType"></param>
        /// <param name="objectID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        public bool CanCreateObjectInventory(int invType, UUID objectID, UUID userID)
        {
            CreateObjectInventoryHandler handler = OnCreateObjectInventory;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (CreateObjectInventoryHandler h in list)
                {
                    if (h(invType, objectID, userID) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanCopyObjectInventory(UUID itemID, UUID objectID, UUID userID)
        {
            CopyObjectInventoryHandler handler = OnCopyObjectInventory;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (CopyObjectInventoryHandler h in list)
                {
                    if (h(itemID, objectID, userID) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanDoObjectInvToObjectInv(TaskInventoryItem item, SceneObjectPart sourcePart, SceneObjectPart destPart)
        {
            DoObjectInvToObjectInv handler = OnDoObjectInvToObjectInv;
            if (handler != null)
            {
                if (sourcePart == null || destPart == null || item == null)
                    return false;
                Delegate[] list = handler.GetInvocationList();
                foreach (DoObjectInvToObjectInv h in list)
                {
                    if (h(item, sourcePart, destPart) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanDropInObjectInv(InventoryItemBase item, IClientAPI client, SceneObjectPart destPart)
        {
            DoDropInObjectInv handler = OnDropInObjectInv;
            if (handler != null)
            {
                if (client == null || client.SceneAgent == null|| destPart == null || item == null)
                    return false;

                ScenePresence sp = client.SceneAgent as ScenePresence;
                if(sp == null || sp.IsDeleted)
                    return false;

                Delegate[] list = handler.GetInvocationList();
                foreach (DoDropInObjectInv h in list)
                {
                    if (h(item, sp, destPart) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanDeleteObjectInventory(UUID itemID, UUID objectID, UUID userID)
        {
            DeleteObjectInventoryHandler handler = OnDeleteObjectInventory;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (DeleteObjectInventoryHandler h in list)
                {
                    if (h(itemID, objectID, userID) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanTransferObjectInventory(UUID itemID, UUID objectID, UUID userID)
        {
            TransferObjectInventoryHandler handler = OnTransferObjectInventory;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (TransferObjectInventoryHandler h in list)
                {
                    if (h(itemID, objectID, userID) == false)
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Check whether the specified user is allowed to create the given inventory type in their inventory.
        /// </summary>
        /// <param name="invType"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        public bool CanCreateUserInventory(int invType, UUID userID)
        {
            CreateUserInventoryHandler handler = OnCreateUserInventory;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (CreateUserInventoryHandler h in list)
                {
                    if (h(invType, userID) == false)
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Check whether the specified user is allowed to edit the given inventory item within their own inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        public bool CanEditUserInventory(UUID itemID, UUID userID)
        {
            EditUserInventoryHandler handler = OnEditUserInventory;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (EditUserInventoryHandler h in list)
                {
                    if (h(itemID, userID) == false)
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Check whether the specified user is allowed to copy the given inventory item from their own inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        public bool CanCopyUserInventory(UUID itemID, UUID userID)
        {
            CopyUserInventoryHandler handler = OnCopyUserInventory;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (CopyUserInventoryHandler h in list)
                {
                    if (h(itemID, userID) == false)
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Check whether the specified user is allowed to edit the given inventory item within their own inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        public bool CanDeleteUserInventory(UUID itemID, UUID userID)
        {
            DeleteUserInventoryHandler handler = OnDeleteUserInventory;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (DeleteUserInventoryHandler h in list)
                {
                    if (h(itemID, userID) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanTransferUserInventory(UUID itemID, UUID userID, UUID recipientID)
        {
            TransferUserInventoryHandler handler = OnTransferUserInventory;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (TransferUserInventoryHandler h in list)
                {
                    if (h(itemID, userID, recipientID) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanTeleport(UUID userID)
        {
            TeleportHandler handler = OnTeleport;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (TeleportHandler h in list)
                {
                    if (h(userID, m_scene) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanControlPrimMedia(UUID userID, UUID primID, int face)
        {
            ControlPrimMediaHandler handler = OnControlPrimMedia;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (ControlPrimMediaHandler h in list)
                {
                    if (h(userID, primID, face) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanInteractWithPrimMedia(UUID userID, UUID primID, int face)
        {
            InteractWithPrimMediaHandler handler = OnInteractWithPrimMedia;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (InteractWithPrimMediaHandler h in list)
                {
                    if (h(userID, primID, face) == false)
                        return false;
                }
            }
            return true;
        }
    }
}
