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
    public delegate uint GenerateClientFlagsHandler(UUID userID, UUID objectID);
    public delegate void SetBypassPermissionsHandler(bool value);
    public delegate bool BypassPermissionsHandler();
    public delegate bool PropagatePermissionsHandler();
    public delegate bool RezObjectHandler(int objectCount, UUID owner, Vector3 objectPosition, Scene scene);
    public delegate bool DeleteObjectHandler(UUID objectID, UUID deleter, Scene scene);
    public delegate bool TransferObjectHandler(UUID objectID, UUID recipient, Scene scene);
    public delegate bool TakeObjectHandler(UUID objectID, UUID stealer, Scene scene);
    public delegate bool TakeCopyObjectHandler(UUID objectID, UUID userID, Scene inScene);
    public delegate bool DuplicateObjectHandler(int objectCount, UUID objectID, UUID owner, Scene scene, Vector3 objectPosition);
    public delegate bool EditObjectHandler(UUID objectID, UUID editorID, Scene scene);
    public delegate bool EditObjectInventoryHandler(UUID objectID, UUID editorID, Scene scene);
    public delegate bool MoveObjectHandler(UUID objectID, UUID moverID, Scene scene);
    public delegate bool ObjectEntryHandler(UUID objectID, bool enteringRegion, Vector3 newPoint, Scene scene);
    public delegate bool ReturnObjectsHandler(ILandObject land, UUID user, List<SceneObjectGroup> objects, Scene scene);
    public delegate bool InstantMessageHandler(UUID user, UUID target, Scene startScene);
    public delegate bool InventoryTransferHandler(UUID user, UUID target, Scene startScene);
    public delegate bool ViewScriptHandler(UUID script, UUID objectID, UUID user, Scene scene);
    public delegate bool ViewNotecardHandler(UUID script, UUID objectID, UUID user, Scene scene);
    public delegate bool EditScriptHandler(UUID script, UUID objectID, UUID user, Scene scene);
    public delegate bool EditNotecardHandler(UUID notecard, UUID objectID, UUID user, Scene scene);
    public delegate bool RunScriptHandler(UUID script, UUID objectID, UUID user, Scene scene);
    public delegate bool CompileScriptHandler(UUID ownerUUID, int scriptType, Scene scene);
    public delegate bool StartScriptHandler(UUID script, UUID user, Scene scene);
    public delegate bool StopScriptHandler(UUID script, UUID user, Scene scene);
    public delegate bool ResetScriptHandler(UUID prim, UUID script, UUID user, Scene scene);
    public delegate bool TerraformLandHandler(UUID user, Vector3 position, Scene requestFromScene);
    public delegate bool RunConsoleCommandHandler(UUID user, Scene requestFromScene);
    public delegate bool IssueEstateCommandHandler(UUID user, Scene requestFromScene, bool ownerCommand);
    public delegate bool IsGodHandler(UUID user, Scene requestFromScene);
    public delegate bool IsGridGodHandler(UUID user, Scene requestFromScene);
    public delegate bool IsAdministratorHandler(UUID user);
    public delegate bool EditParcelHandler(UUID user, ILandObject parcel, Scene scene);
    public delegate bool EditParcelPropertiesHandler(UUID user, ILandObject parcel, GroupPowers p, Scene scene, bool allowManager);
    public delegate bool SellParcelHandler(UUID user, ILandObject parcel, Scene scene);
    public delegate bool AbandonParcelHandler(UUID user, ILandObject parcel, Scene scene);
    public delegate bool ReclaimParcelHandler(UUID user, ILandObject parcel, Scene scene);
    public delegate bool DeedParcelHandler(UUID user, ILandObject parcel, Scene scene);
    public delegate bool DeedObjectHandler(UUID user, UUID group, Scene scene);
    public delegate bool BuyLandHandler(UUID user, ILandObject parcel, Scene scene);
    public delegate bool LinkObjectHandler(UUID user, UUID objectID);
    public delegate bool DelinkObjectHandler(UUID user, UUID objectID);
    public delegate bool CreateObjectInventoryHandler(int invType, UUID objectID, UUID userID);
    public delegate bool CopyObjectInventoryHandler(UUID itemID, UUID objectID, UUID userID);
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
        public event DeleteObjectHandler OnDeleteObject;
        public event TransferObjectHandler OnTransferObject;
        public event TakeObjectHandler OnTakeObject;
        public event TakeCopyObjectHandler OnTakeCopyObject;
        public event DuplicateObjectHandler OnDuplicateObject;
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
        public event RunScriptHandler OnRunScript;
        public event CompileScriptHandler OnCompileScript;
        public event StartScriptHandler OnStartScript;
        public event StopScriptHandler OnStopScript;
        public event ResetScriptHandler OnResetScript;
        public event TerraformLandHandler OnTerraformLand;
        public event RunConsoleCommandHandler OnRunConsoleCommand;
        public event IssueEstateCommandHandler OnIssueEstateCommand;
        public event IsGodHandler OnIsGod;
        public event IsGridGodHandler OnIsGridGod;
        public event IsAdministratorHandler OnIsAdministrator;
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

        public uint GenerateClientFlags(UUID userID, UUID objectID)
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
                PrimFlags.ObjectOwnerModify |
                PrimFlags.ObjectYouOfficer;
#pragma warning restore 0612

            SceneObjectPart part = m_scene.GetSceneObjectPart(objectID);

            if (part == null)
                return 0;

            uint perms = part.GetEffectiveObjectFlags() | (uint)DEFAULT_FLAGS;

            GenerateClientFlagsHandler handlerGenerateClientFlags = OnGenerateClientFlags;
            if (handlerGenerateClientFlags != null)
            {
                Delegate[] list = handlerGenerateClientFlags.GetInvocationList();
                foreach (GenerateClientFlagsHandler check in list)
                {
                    perms &= check(userID, objectID);
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
                    if (h(objectCount, owner,objectPosition, m_scene) == false)
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
            
            DeleteObjectHandler handler = OnDeleteObject;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (DeleteObjectHandler h in list)
                {
                    if (h(objectID, deleter, m_scene) == false)
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
                    if (h(objectID, recipient, m_scene) == false)
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
        public bool CanTakeObject(UUID objectID, UUID AvatarTakingUUID)
        {
            bool result = true;
            
            TakeObjectHandler handler = OnTakeObject;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (TakeObjectHandler h in list)
                {
                    if (h(objectID, AvatarTakingUUID, m_scene) == false)
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

        #region TAKE COPY OBJECT
        public bool CanTakeCopyObject(UUID objectID, UUID userID)
        {
            bool result = true;
            
            TakeCopyObjectHandler handler = OnTakeCopyObject;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (TakeCopyObjectHandler h in list)
                {
                    if (h(objectID, userID, m_scene) == false)
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
        public bool CanDuplicateObject(int objectCount, UUID objectID, UUID owner, Vector3 objectPosition)
        {
            DuplicateObjectHandler handler = OnDuplicateObject;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (DuplicateObjectHandler h in list)
                {
                    if (h(objectCount, objectID, owner, m_scene, objectPosition) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region EDIT OBJECT
        public bool CanEditObject(UUID objectID, UUID editorID)
        {
            EditObjectHandler handler = OnEditObject;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (EditObjectHandler h in list)
                {
                    if (h(objectID, editorID, m_scene) == false)
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
                    if (h(objectID, editorID, m_scene) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region MOVE OBJECT
        public bool CanMoveObject(UUID objectID, UUID moverID)
        {
            MoveObjectHandler handler = OnMoveObject;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (MoveObjectHandler h in list)
                {
                    if (h(objectID, moverID, m_scene) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region OBJECT ENTRY
        public bool CanObjectEntry(UUID objectID, bool enteringRegion, Vector3 newPoint)
        {
            ObjectEntryHandler handler = OnObjectEntry;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (ObjectEntryHandler h in list)
                {
                    if (h(objectID, enteringRegion, newPoint, m_scene) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region RETURN OBJECT
        public bool CanReturnObjects(ILandObject land, UUID user, List<SceneObjectGroup> objects)
        {
            bool result = true;
            
            ReturnObjectsHandler handler = OnReturnObjects;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (ReturnObjectsHandler h in list)
                {
                    if (h(land, user, objects, m_scene) == false)
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
                    if (h(user, target, m_scene) == false)
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
                    if (h(user, target, m_scene) == false)
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
                    if (h(script, objectID, user, m_scene) == false)
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
                    if (h(script, objectID, user, m_scene) == false)
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
                    if (h(script, objectID, user, m_scene) == false)
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
                    if (h(script, objectID, user, m_scene) == false)
                        return false;
                }
            }
            return true;
        }

        #endregion

        #region RUN SCRIPT (When Script Placed in Object)
        public bool CanRunScript(UUID script, UUID objectID, UUID user)
        {
            RunScriptHandler handler = OnRunScript;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (RunScriptHandler h in list)
                {
                    if (h(script, objectID, user, m_scene) == false)
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
                    if (h(ownerUUID, scriptType, m_scene) == false)
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
                    if (h(script, user, m_scene) == false)
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
                    if (h(script, user, m_scene) == false)
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
                    if (h(prim, script, user, m_scene) == false)
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
                    if (h(user, pos, m_scene) == false)
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
                    if (h(user, m_scene) == false)
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
                    if (h(user, m_scene, ownerCommand) == false)
                        return false;
                }
            }
            return true;
        }
        #endregion

        #region CAN BE GODLIKE
        public bool IsGod(UUID user)
        {
            IsGodHandler handler = OnIsGod;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (IsGodHandler h in list)
                {
                    if (h(user, m_scene) == false)
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
                    if (h(user, m_scene) == false)
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

        #region EDIT PARCEL

        public bool CanEditParcelProperties(UUID user, ILandObject parcel, GroupPowers p, bool allowManager)
        {
            EditParcelPropertiesHandler handler = OnEditParcelProperties;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (EditParcelPropertiesHandler h in list)
                {
                    if (h(user, parcel, p, m_scene, allowManager) == false)
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
                    if (h(user, parcel, m_scene) == false)
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
                    if (h(user, parcel, m_scene) == false)
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
                    if (h(user, parcel, m_scene) == false)
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
                    if (h(user, parcel, m_scene) == false)
                        return false;
                }
            }
            return true;
        }

        public bool CanDeedObject(UUID user, UUID group)
        {
            DeedObjectHandler handler = OnDeedObject;
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();
                foreach (DeedObjectHandler h in list)
                {
                    if (h(user, group, m_scene) == false)
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
                    if (h(user, parcel, m_scene) == false)
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
