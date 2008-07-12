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

using libsecondlife;
using Nini.Config;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.Framework;
using OpenSim.Region.Environment.Modules.Framework.InterfaceCommander;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.World.Permissions
{
    public class PermissionsModule : IRegionModule, ICommandableModule
    {
        protected Scene m_scene;
        private readonly Commander m_commander = new Commander("Permissions");
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region Constants
        // These are here for testing.  They will be taken out

        //private uint PERM_ALL = (uint)2147483647;
        private uint PERM_COPY = (uint)32768;
        //private uint PERM_MODIFY = (uint)16384;
        private uint PERM_MOVE = (uint)524288;
        //private uint PERM_TRANS = (uint)8192;
        private uint PERM_LOCKED = (uint)540672;

        #endregion

        #region Bypass Permissions / Debug Permissions Stuff

        // Bypasses the permissions engine
        private bool m_bypassPermissions = false;
        private bool m_bypassPermissionsValue = true;
        private bool m_debugPermissions = false;

        #endregion

        #region ICommandableModule Members

        public ICommander CommandInterface
        {
            get { throw new System.NotImplementedException(); }
        }


        private void InterfaceDebugPermissions(Object[] args)
        {
            if ((bool)args[0] == true)
            {
                m_debugPermissions = true;
                m_log.Info("[PERMISSIONS]: Permissions Debugging Enabled.");
            }
            else
            {
                m_debugPermissions = false;
                m_log.Info("[PERMISSIONS]:  Permissions Debugging Disabled.");
            }
        }

        private void InterfaceBypassPermissions(Object[] args)
        {
            if ((bool)args[0] == true)
            {
                m_log.Info("[PERMISSIONS]: Permissions Bypass Enabled.");
                m_bypassPermissionsValue = (bool)args[1];
            }
            else
            {
                m_bypassPermissions = false;
                m_log.Info("[PERMISSIONS]:  Permissions Bypass Disabled. Normal Operation.");
            }
        }

        /// <summary>
        /// Processes commandline input. Do not call directly.
        /// </summary>
        /// <param name="args">Commandline arguments</param>
        private void EventManager_OnPluginConsole(string[] args)
        {
            if (args[0] == "permissions")
            {
                string[] tmpArgs = new string[args.Length - 2];
                int i;
                for (i = 2; i < args.Length; i++)
                    tmpArgs[i - 2] = args[i];

                m_commander.ProcessConsoleCommand(args[1], tmpArgs);
            }
        }

        #endregion

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_scene = scene;

            IConfig myConfig = config.Configs["Startup"];

            string permissionModules = myConfig.GetString("permissionmodules", "DefaultPermissionsModule");

            List<string> modules=new List<string>(permissionModules.Split(','));

            if (!modules.Contains("DefaultPermissionsModule"))
                return;

            m_bypassPermissions = !myConfig.GetBoolean("serverside_object_permissions", true);
            
            if (m_bypassPermissions)
                m_log.Info("[PERMISSIONS]: serviceside_object_permissions = false in ini file so disabling all region service permission checks");
            else
                m_log.Debug("[PERMISSIONS]: Enabling all region service permission checks");

            //Register functions with Scene External Checks!
            m_scene.ExternalChecks.addBypassPermissions(BypassPermissions); //FULLY IMPLEMENTED
            m_scene.ExternalChecks.addSetBypassPermissions(SetBypassPermissions); //FULLY IMPLEMENTED
            m_scene.ExternalChecks.addPropagatePermissions(PropagatePermissions); //FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckAbandonParcel(CanAbandonParcel); //FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckReclaimParcel(CanReclaimParcel); //FULLY IMPLEMENTED
            m_scene.ExternalChecks.addGenerateClientFlags(GenerateClientFlags); //NOT YET FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckBeGodLike(CanBeGodLike); //FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckDuplicateObject(CanDuplicateObject); //FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckDeleteObject(CanDeleteObject); //MAYBE FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckEditObject(CanEditObject);//MAYBE FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckEditParcel(CanEditParcel); //FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckEditScript(CanEditScript); //NOT YET IMPLEMENTED
            m_scene.ExternalChecks.addCheckEditNotecard(CanEditNotecard); //NOT YET IMPLEMENTED
            m_scene.ExternalChecks.addCheckInstantMessage(CanInstantMessage); //FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckInventoryTransfer(CanInventoryTransfer); //NOT YET IMPLEMENTED
            m_scene.ExternalChecks.addCheckIssueEstateCommand(CanIssueEstateCommand); //FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckMoveObject(CanMoveObject); //HOPEFULLY FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckObjectEntry(CanObjectEntry); //FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckReturnObject(CanReturnObject); //NOT YET IMPLEMENTED
            m_scene.ExternalChecks.addCheckRezObject(CanRezObject); //HOPEFULLY FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckRunConsoleCommand(CanRunConsoleCommand); //FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckRunScript(CanRunScript); //NOT YET IMPLEMENTED
            m_scene.ExternalChecks.addCheckSellParcel(CanSellParcel); //FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckTakeObject(CanTakeObject); //FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckTakeCopyObject(CanTakeCopyObject); //FULLY IMPLEMENTED
            m_scene.ExternalChecks.addCheckTerraformLand(CanTerraformLand); //FULL IMPLEMENTED (POINT ONLY!!! NOT AREA!!!)
            m_scene.ExternalChecks.addCheckViewScript(CanViewScript); //NOT YET IMPLEMENTED
            m_scene.ExternalChecks.addCheckViewNotecard(CanViewNotecard); //NOT YET IMPLEMENTED
            m_scene.ExternalChecks.addCheckCanLinkObject(CanLinkObject); //NOT YET IMPLEMENTED
            m_scene.ExternalChecks.addCheckCanDelinkObject(CanDelinkObject); //NOT YET IMPLEMENTED
            m_scene.ExternalChecks.addCheckCanBuyLand(CanBuyLand); //NOT YET IMPLEMENTED
            m_scene.ExternalChecks.addCheckCanCopyInventory(CanCopyInventory); //NOT YET IMPLEMENTED
            m_scene.ExternalChecks.addCheckCanDeleteInventory(CanDeleteInventory); //NOT YET IMPLEMENTED
            m_scene.ExternalChecks.addCheckCanCreateInventory(CanCreateInventory); //NOT YET IMPLEMENTED
            m_scene.ExternalChecks.addCheckCanTeleport(CanTeleport); //NOT YET IMPLEMENTED


            //Register Debug Commands
            Command bypassCommand = new Command("bypass", InterfaceBypassPermissions, "Force the permissions a specific way to test permissions");
            bypassCommand.AddArgument("enable_bypass_perms", "true to enable bypassing all perms", "Boolean");
            bypassCommand.AddArgument("bypass_perms_value", "true/false: true will ignore all perms; false will restrict everything", "Boolean");

            m_commander.RegisterCommand("bypass", bypassCommand);

            Command debugCommand = new Command("debug", InterfaceDebugPermissions, "Force the permissions a specific way to test permissions");
            debugCommand.AddArgument("enable_debug_perms", "true to enable debugging to console all perms", "Boolean");

            m_commander.RegisterCommand("debug", debugCommand);
            m_scene.RegisterModuleCommander("CommanderPermissions", m_commander);

            m_scene.EventManager.OnPluginConsole += new EventManager.OnPluginConsoleDelegate(EventManager_OnPluginConsole);
        }


        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "PermissionsModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion

        #region Helper Functions
        protected void SendPermissionError(LLUUID user, string reason)
        {
            m_scene.EventManager.TriggerPermissionError(user, reason);
        }
        protected void DebugPermissionInformation(string permissionCalled)
        {
            if (m_debugPermissions)
                m_log.Info("[PERMISSIONS]: " + permissionCalled + " was called from " + m_scene.RegionInfo.RegionName);
        }

        protected bool IsAdministrator(LLUUID user)
        {
//            m_log.DebugFormat(
//                "[PERMISSIONS]: Is adminstrator called for {0} where region master avatar is {1}", 
//                user, m_scene.RegionInfo.MasterAvatarAssignedUUID);
            
            // If there is no master avatar, return false
            if (m_scene.RegionInfo.MasterAvatarAssignedUUID != LLUUID.Zero)
            {
                return m_scene.RegionInfo.MasterAvatarAssignedUUID == user;
            }

            return false;
        }

        protected bool IsEstateManager(LLUUID user)
        {
            if (user != LLUUID.Zero)
            {
                LLUUID[] estatemanagers = m_scene.RegionInfo.EstateSettings.estateManagers;
                foreach (LLUUID estatemanager in estatemanagers)
                {
                    if (estatemanager == user)
                        return true;
                }
            }

            return false;
        }
#endregion

        public bool PropagatePermissions()
        {
            return false;
        }

        public bool BypassPermissions()
        {
            return m_bypassPermissions;
        }

        public void SetBypassPermissions(bool value)
        {
            m_bypassPermissions=value;
        }

        #region Object Permissions

        public uint GenerateClientFlags(LLUUID user, LLUUID objID)
        {
            // Here's the way this works,
            // ObjectFlags and Permission flags are two different enumerations
            // ObjectFlags, however, tells the client to change what it will allow the user to do.
            // So, that means that all of the permissions type ObjectFlags are /temporary/ and only
            // supposed to be set when customizing the objectflags for the client.

            // These temporary objectflags get computed and added in this function based on the
            // Permission mask that's appropriate!
            // Outside of this method, they should never be added to objectflags!
            // -teravus

            SceneObjectPart task=m_scene.GetSceneObjectPart(objID);

            // this shouldn't ever happen..     return no permissions/objectflags.
            if (task == null)
                return (uint)0;

            uint objflags = task.GetEffectiveObjectFlags();
            LLUUID objectOwner = task.OwnerID;


            // Remove any of the objectFlags that are temporary.  These will get added back if appropriate
            // in the next bit of code

            objflags &= (uint)
                ~(LLObject.ObjectFlags.ObjectCopy | // Tells client you can copy the object
                  LLObject.ObjectFlags.ObjectModify | // tells client you can modify the object
                  LLObject.ObjectFlags.ObjectMove |   // tells client that you can move the object (only, no mod)
                  LLObject.ObjectFlags.ObjectTransfer | // tells the client that you can /take/ the object if you don't own it
                  LLObject.ObjectFlags.ObjectYouOwner | // Tells client that you're the owner of the object
                  LLObject.ObjectFlags.ObjectAnyOwner | // Tells client that someone owns the object
                  LLObject.ObjectFlags.ObjectOwnerModify | // Tells client that you're the owner of the object
                  LLObject.ObjectFlags.ObjectYouOfficer // Tells client that you've got group object editing permission. Used when ObjectGroupOwned is set
                    );

            // Creating the three ObjectFlags options for this method to choose from.
            // Customize the OwnerMask
            uint objectOwnerMask = ApplyObjectModifyMasks(task.OwnerMask, objflags);
            objectOwnerMask |= (uint)LLObject.ObjectFlags.ObjectYouOwner | (uint)LLObject.ObjectFlags.ObjectAnyOwner | (uint)LLObject.ObjectFlags.ObjectOwnerModify;

            // Customize the GroupMask
            // uint objectGroupMask = ApplyObjectModifyMasks(task.GroupMask, objflags);

            // Customize the EveryoneMask
            uint objectEveryoneMask = ApplyObjectModifyMasks(task.EveryoneMask, objflags);


            // Hack to allow collaboration until Groups and Group Permissions are implemented
            if ((objectEveryoneMask & (uint)LLObject.ObjectFlags.ObjectMove) != 0)
                objectEveryoneMask |= (uint)LLObject.ObjectFlags.ObjectModify;

            if (m_bypassPermissions)
                return objectOwnerMask;

            // Object owners should be able to edit their own content
            if (user == objectOwner)
            {
                return objectOwnerMask;
            }

            // Users should be able to edit what is over their land.
            ILandObject parcel = m_scene.LandChannel.GetLandObject(task.AbsolutePosition.X, task.AbsolutePosition.Y);
            if (parcel != null && parcel.landData.ownerID == user)
                return objectOwnerMask;

            // Admin objects should not be editable by the above
            if (IsAdministrator(objectOwner))
                return objectEveryoneMask;

            // Estate users should be able to edit anything in the sim
            if (IsEstateManager(user))
                return objectOwnerMask;



            // Admin should be able to edit anything in the sim (including admin objects)
            if (IsAdministrator(user))
                return objectOwnerMask;


            return objectEveryoneMask;
        }

        private uint ApplyObjectModifyMasks(uint setPermissionMask, uint objectFlagsMask)
        {
            // We are adding the temporary objectflags to the object's objectflags based on the
            // permission flag given.  These change the F flags on the client.

            if ((setPermissionMask & (uint)PermissionMask.Copy) != 0)
            {
                objectFlagsMask |= (uint)LLObject.ObjectFlags.ObjectCopy;
            }

            if ((setPermissionMask & (uint)PermissionMask.Move) != 0)
            {
                objectFlagsMask |= (uint)LLObject.ObjectFlags.ObjectMove;
            }

            if ((setPermissionMask & (uint)PermissionMask.Modify) != 0)
            {
                objectFlagsMask |= (uint)LLObject.ObjectFlags.ObjectModify;
            }

            if ((setPermissionMask & (uint)PermissionMask.Transfer) != 0)
            {
                objectFlagsMask |= (uint)LLObject.ObjectFlags.ObjectTransfer;
            }

            return objectFlagsMask;
        }

        protected bool GenericObjectPermission(LLUUID currentUser, LLUUID objId, bool denyOnLocked)
        {
            // Default: deny
            bool permission = false;
            bool locked = false;

            if (!m_scene.Entities.ContainsKey(objId))
            {
                return false;
            }

            // If it's not an object, we cant edit it.
            if ((!(m_scene.Entities[objId] is SceneObjectGroup)))
            {
                return false;
            }


            SceneObjectGroup group = (SceneObjectGroup)m_scene.Entities[objId];

            LLUUID objectOwner = group.OwnerID;
            locked = ((group.RootPart.OwnerMask & PERM_LOCKED) == 0);

            // People shouldn't be able to do anything with locked objects, except the Administrator
            // The 'set permissions' runs through a different permission check, so when an object owner
            // sets an object locked, the only thing that they can do is unlock it.
            //
            // Nobody but the object owner can set permissions on an object
            //

            if (locked && (!IsAdministrator(currentUser)) && denyOnLocked)
            {
                return false;
            }

            // Object owners should be able to edit their own content
            if (currentUser == objectOwner)
            {
                permission = true;
            }

            // Users should be able to edit what is over their land.
            ILandObject parcel = m_scene.LandChannel.GetLandObject(group.AbsolutePosition.X, group.AbsolutePosition.Y);
            if ((parcel != null) && (parcel.landData.ownerID == currentUser))
            {
                permission = true;
            }

            // Estate users should be able to edit anything in the sim
            if (IsEstateManager(currentUser))
            {
                permission = true;
            }

            // Admin objects should not be editable by the above
            if (IsAdministrator(objectOwner))
            {
                permission = false;
            }

            // Admin should be able to edit anything in the sim (including admin objects)
            if (IsAdministrator(currentUser))
            {
                permission = true;
            }

            return permission;
        }


        #endregion

        #region Generic Permissions
        protected bool GenericCommunicationPermission(LLUUID user, LLUUID target)
        {
            // Setting this to true so that cool stuff can happen until we define what determines Generic Communication Permission
            bool permission = true;
            string reason = "Only registered users may communicate with another account.";

            // Uhh, we need to finish this before we enable it..   because it's blocking all sorts of goodies and features
            if (IsAdministrator(user))
                permission = true;

            if (IsEstateManager(user))
                permission = true;

            if (!permission)
                SendPermissionError(user, reason);

            return permission;
        }

        public bool GenericEstatePermission(LLUUID user)
        {
            // Default: deny
            bool permission = false;

            // Estate admins should be able to use estate tools
            if (IsEstateManager(user))
                permission = true;

            // Administrators always have permission
            if (IsAdministrator(user))
                permission = true;

            return permission;
        }

        protected bool GenericParcelPermission(LLUUID user, ILandObject parcel)
        {
            bool permission = false;

            if (parcel.landData.ownerID == user)
            {
                permission = true;
            }

            if (parcel.landData.isGroupOwned)
            {
                // TODO: Need to do some extra checks here. Requires group code.
            }

            if (IsEstateManager(user))
            {
                permission = true;
            }

            if (IsAdministrator(user))
            {
                permission = true;
            }

            return permission;
        }

        protected bool GenericParcelPermission(LLUUID user, LLVector3 pos)
        {
            ILandObject parcel = m_scene.LandChannel.GetLandObject(pos.X, pos.Y);
            if (parcel == null) return false;
            return GenericParcelPermission(user, parcel);
        }
#endregion

        #region Permission Checks
            private bool CanAbandonParcel(LLUUID user, ILandObject parcel, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return GenericParcelPermission(user, parcel);
            }

            private bool CanReclaimParcel(LLUUID user, ILandObject parcel, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return GenericParcelPermission(user, parcel);
            }

            private bool CanBeGodLike(LLUUID user, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return IsAdministrator(user);
            }

            private bool CanDuplicateObject(int objectCount, LLUUID objectID, LLUUID owner, Scene scene, LLVector3 objectPosition)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                if (!GenericObjectPermission(owner, objectID, true))
                {
                    //They can't even edit the object
                    return false;
                }
                //If they can rez, they can duplicate
                return CanRezObject(objectCount, owner, objectPosition, scene);
            }

            private bool CanDeleteObject(LLUUID objectID, LLUUID deleter, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return GenericObjectPermission(deleter, objectID, false);
            }

            private bool CanEditObject(LLUUID objectID, LLUUID editorID, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;


                return GenericObjectPermission(editorID, objectID, false);
            }

            private bool CanEditParcel(LLUUID user, ILandObject parcel, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return GenericParcelPermission(user, parcel);
            }

            private bool CanEditScript(LLUUID script, LLUUID objectID, LLUUID user, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return false;
            }

            private bool CanEditNotecard(LLUUID notecard, LLUUID objectID, LLUUID user, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return true;
            }

            private bool CanInstantMessage(LLUUID user, LLUUID target, Scene startScene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;


                return GenericCommunicationPermission(user, target);
            }

            private bool CanInventoryTransfer(LLUUID user, LLUUID target, Scene startScene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return GenericCommunicationPermission(user, target);
            }

            private bool CanIssueEstateCommand(LLUUID user, Scene requestFromScene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return GenericEstatePermission(user);
            }

            private bool CanMoveObject(LLUUID objectID, LLUUID moverID, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                bool permission = GenericObjectPermission(moverID, objectID, true);
                if (!permission)
                {
                    if (!m_scene.Entities.ContainsKey(objectID))
                    {
                        return false;
                    }

                    // The client
                    // may request to edit linked parts, and therefore, it needs
                    // to also check for SceneObjectPart

                    // If it's not an object, we cant edit it.
                    if ((!(m_scene.Entities[objectID] is SceneObjectGroup)))
                    {
                        return false;
                    }


                    SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[objectID];


                    // LLUUID taskOwner = null;
                    // Added this because at this point in time it wouldn't be wise for
                    // the administrator object permissions to take effect.
                    // LLUUID objectOwner = task.OwnerID;

                    // Anyone can move
                    if ((task.RootPart.EveryoneMask & PERM_MOVE) != 0)
                        permission = true;

                    // Locked
                    if ((task.RootPart.OwnerMask & PERM_LOCKED) == 0)
                        permission = false;

                }
                else
                {
                    bool locked = false;
                    if (!m_scene.Entities.ContainsKey(objectID))
                    {
                        return false;
                    }

                    // If it's not an object, we cant edit it.
                    if ((!(m_scene.Entities[objectID] is SceneObjectGroup)))
                    {
                        return false;
                    }


                    SceneObjectGroup group = (SceneObjectGroup)m_scene.Entities[objectID];

                    LLUUID objectOwner = group.OwnerID;
                    locked = ((group.RootPart.OwnerMask & PERM_LOCKED) == 0);


                    // This is an exception to the generic object permission.
                    // Administrators who lock their objects should not be able to move them,
                    // however generic object permission should return true.
                    // This keeps locked objects from being affected by random click + drag actions by accident
                    // and allows the administrator to grab or delete a locked object.

                    // Administrators and estate managers are still able to click+grab locked objects not
                    // owned by them in the scene
                    // This is by design.

                    if (locked && (moverID == objectOwner))
                        return false;
                }
                return permission;
            }

            private bool CanObjectEntry(LLUUID objectID, LLVector3 newPoint, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                if ((newPoint.X > 257f || newPoint.X < -1f || newPoint.Y > 257f || newPoint.Y < -1f))
                {
                    return true;
                }

                ILandObject land = m_scene.LandChannel.GetLandObject(newPoint.X, newPoint.Y);

                if (land == null)
                {
                    return false;
                }

                if ((land.landData.landFlags & ((int)Parcel.ParcelFlags.AllowAllObjectEntry)) != 0)
                {
                    return true;
                }

                //TODO: check for group rights

                if (!m_scene.Entities.ContainsKey(objectID))
                {
                    return false;
                }

                // If it's not an object, we cant edit it.
                if (!(m_scene.Entities[objectID] is SceneObjectGroup))
                {
                    return false;
                }

                SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[objectID];

                if (GenericParcelPermission(task.OwnerID, newPoint))
                {
                    return true;
                }

                //Otherwise, false!
                return false;
            }

            private bool CanReturnObject(LLUUID objectID, LLUUID returnerID, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return GenericObjectPermission(returnerID, objectID, false);
            }

            private bool CanRezObject(int objectCount, LLUUID owner, LLVector3 objectPosition, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                bool permission = false;

                ILandObject land = m_scene.LandChannel.GetLandObject(objectPosition.X, objectPosition.Y);
                if (land == null) return false;

                if ((land.landData.landFlags & ((int)Parcel.ParcelFlags.CreateObjects)) ==
                    (int)Parcel.ParcelFlags.CreateObjects)
                    permission = true;

                //TODO: check for group rights

                if (IsAdministrator(owner))
                {
                    permission = true;
                }

                if (GenericParcelPermission(owner, objectPosition))
                {
                    permission = true;
                }

                return permission;
            }

            private bool CanRunConsoleCommand(LLUUID user, Scene requestFromScene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;


                return IsAdministrator(user);
            }

            private bool CanRunScript(LLUUID script, LLUUID objectID, LLUUID user, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return true;
            }

            private bool CanSellParcel(LLUUID user, ILandObject parcel, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return GenericParcelPermission(user, parcel);
            }

            private bool CanTakeObject(LLUUID objectID, LLUUID stealer, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return GenericObjectPermission(stealer,objectID, false);
            }

            private bool CanTakeCopyObject(LLUUID objectID, LLUUID userID, Scene inScene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                bool permission = GenericObjectPermission(userID, objectID,false);
                if (!permission)
                {
                    if (!m_scene.Entities.ContainsKey(objectID))
                    {
                        return false;
                    }

                    // If it's not an object, we cant edit it.
                    if (!(m_scene.Entities[objectID] is SceneObjectGroup))
                    {
                        return false;
                    }

                    SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[objectID];
                    // LLUUID taskOwner = null;
                    // Added this because at this point in time it wouldn't be wise for
                    // the administrator object permissions to take effect.
                    // LLUUID objectOwner = task.OwnerID;


                    if ((task.RootPart.EveryoneMask & PERM_COPY) != 0)
                        permission = true;
                }
                return permission;
            }

            private bool CanTerraformLand(LLUUID user, LLVector3 position, Scene requestFromScene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                bool permission = false;

                // Estate override
                if (GenericEstatePermission(user))
                    permission = true;

                float X = position.X;
                float Y = position.Y;

                if (X > 255)
                    X = 255;
                if (Y > 255)
                    Y = 255;
                if (X < 0)
                    X = 0;
                if (Y < 0)
                    Y = 0;

                // Land owner can terraform too
                ILandObject parcel = m_scene.LandChannel.GetLandObject(X, Y);
                if (parcel != null && GenericParcelPermission(user, parcel))
                    permission = true;


                return permission;
            }

            private bool CanViewScript(LLUUID script, LLUUID objectID, LLUUID user, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return false;
            }

            private bool CanViewNotecard(LLUUID notecard, LLUUID objectID, LLUUID user, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return false;
            }

        #endregion

            public bool CanLinkObject(LLUUID userID, LLUUID objectID)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return true;
            }

            public bool CanDelinkObject(LLUUID userID, LLUUID objectID)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return true;
            }

            public bool CanBuyLand(LLUUID userID, ILandObject parcel, Scene scene)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return true;
            }

            public bool CanCopyInventory(LLUUID itemID, LLUUID objectID, LLUUID userID)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return true;
            }

            public bool CanDeleteInventory(LLUUID itemID, LLUUID objectID, LLUUID userID)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return true;
            }

            public bool CanCreateInventory(uint invType, LLUUID objectID, LLUUID userID)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return true;
            }

            public bool CanTeleport(LLUUID userID)
            {
                DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
                if (m_bypassPermissions) return m_bypassPermissionsValue;

                return true;
            }
    }

}
