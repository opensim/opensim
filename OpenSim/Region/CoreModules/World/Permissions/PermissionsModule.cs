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
using System.Linq;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;

using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

using Mono.Addins;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Region.CoreModules.World.Permissions
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "DefaultPermissionsModule")]
    public class DefaultPermissionsModule : INonSharedRegionModule, IPermissionsModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
                
        protected Scene m_scene;
        protected bool m_Enabled;

        private InventoryFolderImpl m_libraryRootFolder;
        protected InventoryFolderImpl LibraryRootFolder
        {
            get
            {
                if (m_libraryRootFolder != null)
                    return m_libraryRootFolder;

                ILibraryService lib = m_scene.RequestModuleInterface<ILibraryService>();
                if (lib != null)
                {
                    m_libraryRootFolder = lib.LibraryRootFolder;
                }
                return m_libraryRootFolder;
            }
        }

        #region Constants
        // These are here for testing.  They will be taken out

        //private uint PERM_ALL = (uint)2147483647;
        private uint PERM_COPY = (uint)32768;
        //private uint PERM_MODIFY = (uint)16384;
        private uint PERM_MOVE = (uint)524288;
        private uint PERM_TRANS = (uint)8192;
        private uint PERM_LOCKED = (uint)540672;
        
        /// <value>
        /// Different user set names that come in from the configuration file.
        /// </value>
        enum UserSet
        {
            All,
            Administrators
        };

        #endregion

        #region Bypass Permissions / Debug Permissions Stuff

        // Bypasses the permissions engine
        private bool m_bypassPermissions = true;
        private bool m_bypassPermissionsValue = true;
        private bool m_propagatePermissions = false;
        private bool m_debugPermissions = false;
        private bool m_allowGridGods = false;
        private bool m_RegionOwnerIsGod = false;
        private bool m_RegionManagerIsGod = false;
        private bool m_ParcelOwnerIsGod = false;

        private bool m_SimpleBuildPermissions = false;

        /// <value>
        /// The set of users that are allowed to create scripts.  This is only active if permissions are not being
        /// bypassed.  This overrides normal permissions.
        /// </value>
        private UserSet m_allowedScriptCreators = UserSet.All;

        /// <value>
        /// The set of users that are allowed to edit (save) scripts.  This is only active if 
        /// permissions are not being bypassed.  This overrides normal permissions.-
        /// </value>
        private UserSet m_allowedScriptEditors = UserSet.All;
        
        private Dictionary<string, bool> GrantLSL = new Dictionary<string, bool>();
        private Dictionary<string, bool> GrantCS = new Dictionary<string, bool>();
        private Dictionary<string, bool> GrantVB = new Dictionary<string, bool>();
        private Dictionary<string, bool> GrantJS = new Dictionary<string, bool>();
        private Dictionary<string, bool> GrantYP = new Dictionary<string, bool>();
        
        private IFriendsModule m_friendsModule;
        private IFriendsModule FriendsModule
        {
            get
            {
                if (m_friendsModule == null)
                    m_friendsModule = m_scene.RequestModuleInterface<IFriendsModule>();
                return m_friendsModule;
            }
        }
        private IGroupsModule m_groupsModule;
        private IGroupsModule GroupsModule
        {
            get
            {
                if (m_groupsModule == null)
                    m_groupsModule = m_scene.RequestModuleInterface<IGroupsModule>();
                return m_groupsModule;
            }
        }

        private IMoapModule m_moapModule;
        private IMoapModule MoapModule
        {
            get
            {
                if (m_moapModule == null)
                    m_moapModule = m_scene.RequestModuleInterface<IMoapModule>();
                return m_moapModule;
            }
        }
        #endregion

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
            string permissionModules = Util.GetConfigVarFromSections<string>(config, "permissionmodules",
                new string[] { "Startup", "Permissions" }, "DefaultPermissionsModule");

            List<string> modules = new List<string>(permissionModules.Split(',').Select(m => m.Trim()));

            if (!modules.Contains("DefaultPermissionsModule"))
                return;

            m_Enabled = true;

            m_allowGridGods = Util.GetConfigVarFromSections<bool>(config, "allow_grid_gods",
                new string[] { "Startup", "Permissions" }, false); 
            m_bypassPermissions = !Util.GetConfigVarFromSections<bool>(config, "serverside_object_permissions",
                new string[] { "Startup", "Permissions" }, true); 
            m_propagatePermissions = Util.GetConfigVarFromSections<bool>(config, "propagate_permissions",
                new string[] { "Startup", "Permissions" }, true); 
            m_RegionOwnerIsGod = Util.GetConfigVarFromSections<bool>(config, "region_owner_is_god",
                new string[] { "Startup", "Permissions" }, true); 
            m_RegionManagerIsGod = Util.GetConfigVarFromSections<bool>(config, "region_manager_is_god",
                new string[] { "Startup", "Permissions" }, false); 
            m_ParcelOwnerIsGod = Util.GetConfigVarFromSections<bool>(config, "parcel_owner_is_god",
                new string[] { "Startup", "Permissions" }, true);

            m_SimpleBuildPermissions = Util.GetConfigVarFromSections<bool>(config, "simple_build_permissions",
                new string[] { "Startup", "Permissions" }, false); 

            m_allowedScriptCreators
                = ParseUserSetConfigSetting(config, "allowed_script_creators", m_allowedScriptCreators);
            m_allowedScriptEditors
                = ParseUserSetConfigSetting(config, "allowed_script_editors", m_allowedScriptEditors);

            if (m_bypassPermissions)
                m_log.Info("[PERMISSIONS]: serverside_object_permissions = false in ini file so disabling all region service permission checks");
            else
                m_log.Debug("[PERMISSIONS]: Enabling all region service permission checks");

            string grant = Util.GetConfigVarFromSections<string>(config, "GrantLSL",
                new string[] { "Startup", "Permissions" }, string.Empty);
            if (grant.Length > 0)
            {
                foreach (string uuidl in grant.Split(','))
                {
                    string uuid = uuidl.Trim(" \t".ToCharArray());
                    GrantLSL.Add(uuid, true);
                }
            }

            grant = Util.GetConfigVarFromSections<string>(config, "GrantCS",
                new string[] { "Startup", "Permissions" }, string.Empty); 
            if (grant.Length > 0)
            {
                foreach (string uuidl in grant.Split(','))
                {
                    string uuid = uuidl.Trim(" \t".ToCharArray());
                    GrantCS.Add(uuid, true);
                }
            }

            grant = Util.GetConfigVarFromSections<string>(config, "GrantVB",
                new string[] { "Startup", "Permissions" }, string.Empty);
            if (grant.Length > 0)
            {
                foreach (string uuidl in grant.Split(','))
                {
                    string uuid = uuidl.Trim(" \t".ToCharArray());
                    GrantVB.Add(uuid, true);
                }
            }

            grant = Util.GetConfigVarFromSections<string>(config, "GrantJS",
                new string[] { "Startup", "Permissions" }, string.Empty);
            if (grant.Length > 0)
            {
                foreach (string uuidl in grant.Split(','))
                {
                    string uuid = uuidl.Trim(" \t".ToCharArray());
                    GrantJS.Add(uuid, true);
                }
            }

            grant = Util.GetConfigVarFromSections<string>(config, "GrantYP",
                new string[] { "Startup", "Permissions" }, string.Empty);
            if (grant.Length > 0)
            {
                foreach (string uuidl in grant.Split(','))
                {
                    string uuid = uuidl.Trim(" \t".ToCharArray());
                    GrantYP.Add(uuid, true);
                }
            }
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_scene = scene;

            scene.RegisterModuleInterface<IPermissionsModule>(this);

            //Register functions with Scene External Checks!
            m_scene.Permissions.OnBypassPermissions += BypassPermissions;
            m_scene.Permissions.OnSetBypassPermissions += SetBypassPermissions;
            m_scene.Permissions.OnPropagatePermissions += PropagatePermissions;
            m_scene.Permissions.OnGenerateClientFlags += GenerateClientFlags;
            m_scene.Permissions.OnAbandonParcel += CanAbandonParcel;
            m_scene.Permissions.OnReclaimParcel += CanReclaimParcel;
            m_scene.Permissions.OnDeedParcel += CanDeedParcel;
            m_scene.Permissions.OnDeedObject += CanDeedObject;
            m_scene.Permissions.OnIsGod += IsGod;
            m_scene.Permissions.OnIsGridGod += IsGridGod;
            m_scene.Permissions.OnIsAdministrator += IsAdministrator;
            m_scene.Permissions.OnDuplicateObject += CanDuplicateObject;
            m_scene.Permissions.OnDeleteObject += CanDeleteObject; 
            m_scene.Permissions.OnEditObject += CanEditObject; 
            m_scene.Permissions.OnEditParcelProperties += CanEditParcelProperties; 
            m_scene.Permissions.OnInstantMessage += CanInstantMessage;
            m_scene.Permissions.OnInventoryTransfer += CanInventoryTransfer; 
            m_scene.Permissions.OnIssueEstateCommand += CanIssueEstateCommand; 
            m_scene.Permissions.OnMoveObject += CanMoveObject; 
            m_scene.Permissions.OnObjectEntry += CanObjectEntry;
            m_scene.Permissions.OnReturnObjects += CanReturnObjects; 
            m_scene.Permissions.OnRezObject += CanRezObject; 
            m_scene.Permissions.OnRunConsoleCommand += CanRunConsoleCommand;
            m_scene.Permissions.OnRunScript += CanRunScript; 
            m_scene.Permissions.OnCompileScript += CanCompileScript;
            m_scene.Permissions.OnSellParcel += CanSellParcel;
            m_scene.Permissions.OnTakeObject += CanTakeObject;
            m_scene.Permissions.OnTakeCopyObject += CanTakeCopyObject;
            m_scene.Permissions.OnTerraformLand += CanTerraformLand;
            m_scene.Permissions.OnLinkObject += CanLinkObject; 
            m_scene.Permissions.OnDelinkObject += CanDelinkObject; 
            m_scene.Permissions.OnBuyLand += CanBuyLand; 
            
            m_scene.Permissions.OnViewNotecard += CanViewNotecard; 
            m_scene.Permissions.OnViewScript += CanViewScript; 
            m_scene.Permissions.OnEditNotecard += CanEditNotecard; 
            m_scene.Permissions.OnEditScript += CanEditScript; 
            
            m_scene.Permissions.OnCreateObjectInventory += CanCreateObjectInventory;
            m_scene.Permissions.OnEditObjectInventory += CanEditObjectInventory;
            m_scene.Permissions.OnCopyObjectInventory += CanCopyObjectInventory; 
            m_scene.Permissions.OnDeleteObjectInventory += CanDeleteObjectInventory; 
            m_scene.Permissions.OnResetScript += CanResetScript;
            
            m_scene.Permissions.OnCreateUserInventory += CanCreateUserInventory; 
            m_scene.Permissions.OnCopyUserInventory += CanCopyUserInventory; 
            m_scene.Permissions.OnEditUserInventory += CanEditUserInventory; 
            m_scene.Permissions.OnDeleteUserInventory += CanDeleteUserInventory; 
            
            m_scene.Permissions.OnTeleport += CanTeleport; 
            
            m_scene.Permissions.OnControlPrimMedia += CanControlPrimMedia;
            m_scene.Permissions.OnInteractWithPrimMedia += CanInteractWithPrimMedia;

            m_scene.AddCommand("Users", this, "bypass permissions",
                    "bypass permissions <true / false>",
                    "Bypass permission checks",
                    HandleBypassPermissions);

            m_scene.AddCommand("Users", this, "force permissions",
                    "force permissions <true / false>",
                    "Force permissions on or off",
                    HandleForcePermissions);

            m_scene.AddCommand("Debug", this, "debug permissions",
                    "debug permissions <true / false>",
                    "Turn on permissions debugging",
                    HandleDebugPermissions);                    
                    
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_scene.UnregisterModuleInterface<IPermissionsModule>(this);
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "DefaultPermissionsModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        #region Console command handlers

        public void HandleBypassPermissions(string module, string[] args)
        {
            if (m_scene.ConsoleScene() != null &&
                m_scene.ConsoleScene() != m_scene)
            {
                return;
            }

            if (args.Length > 2)
            {
                bool val;

                if (!bool.TryParse(args[2], out val))
                    return;

                m_bypassPermissions = val;

                m_log.InfoFormat(
                    "[PERMISSIONS]: Set permissions bypass to {0} for {1}",
                    m_bypassPermissions, m_scene.RegionInfo.RegionName);
            }
        }

        public void HandleForcePermissions(string module, string[] args)
        {
            if (m_scene.ConsoleScene() != null &&
                m_scene.ConsoleScene() != m_scene)
            {
                return;
            }

            if (!m_bypassPermissions)
            {
                m_log.Error("[PERMISSIONS] Permissions can't be forced unless they are bypassed first");
                return;
            }

            if (args.Length > 2)
            {
                bool val;

                if (!bool.TryParse(args[2], out val))
                    return;

                m_bypassPermissionsValue = val;

                m_log.InfoFormat("[PERMISSIONS] Forced permissions to {0} in {1}", m_bypassPermissionsValue, m_scene.RegionInfo.RegionName);
            }
        }

        public void HandleDebugPermissions(string module, string[] args)
        {
            if (m_scene.ConsoleScene() != null &&
                m_scene.ConsoleScene() != m_scene)
            {
                return;
            }

            if (args.Length > 2)
            {
                bool val;

                if (!bool.TryParse(args[2], out val))
                    return;

                m_debugPermissions = val;

                m_log.InfoFormat("[PERMISSIONS] Set permissions debugging to {0} in {1}", m_debugPermissions, m_scene.RegionInfo.RegionName);
            }
        }

        #endregion

        #region Helper Functions
        protected void SendPermissionError(UUID user, string reason)
        {
            m_scene.EventManager.TriggerPermissionError(user, reason);
        }
        
        protected void DebugPermissionInformation(string permissionCalled)
        {
            if (m_debugPermissions)
                m_log.Debug("[PERMISSIONS]: " + permissionCalled + " was called from " + m_scene.RegionInfo.RegionName);
        }

        /// <summary>
        /// Checks if the given group is active and if the user is a group member
        /// with the powers requested (powers = 0 for no powers check)
        /// </summary>
        /// <param name="groupID"></param>
        /// <param name="userID"></param>
        /// <param name="powers"></param>
        /// <returns></returns>
        protected bool IsGroupMember(UUID groupID, UUID userID, ulong powers)
        {
            if (null == GroupsModule)
                return false;

            GroupMembershipData gmd = GroupsModule.GetMembershipData(groupID, userID);

            if (gmd != null)
            {
                if (((gmd.GroupPowers != 0) && powers == 0) || (gmd.GroupPowers & powers) == powers)
                    return true;
            }

            return false;
        }
         
        /// <summary>
        /// Parse a user set configuration setting
        /// </summary>
        /// <param name="config"></param>
        /// <param name="settingName"></param>
        /// <param name="defaultValue">The default value for this attribute</param>
        /// <returns>The parsed value</returns>
        private static UserSet ParseUserSetConfigSetting(IConfigSource config, string settingName, UserSet defaultValue)
        {
            UserSet userSet = defaultValue;

            string rawSetting = Util.GetConfigVarFromSections<string>(config, settingName, 
                new string[] {"Startup", "Permissions"}, defaultValue.ToString()); 
            
            // Temporary measure to allow 'gods' to be specified in config for consistency's sake.  In the long term
            // this should disappear.
            if ("gods" == rawSetting.ToLower())
                rawSetting = UserSet.Administrators.ToString();
            
            // Doing it this was so that we can do a case insensitive conversion
            try
            {
                userSet = (UserSet)Enum.Parse(typeof(UserSet), rawSetting, true);
            }
            catch 
            {
                m_log.ErrorFormat(
                    "[PERMISSIONS]: {0} is not a valid {1} value, setting to {2}",
                    rawSetting, settingName, userSet);
            }
            
            m_log.DebugFormat("[PERMISSIONS]: {0} {1}", settingName, userSet);
            
            return userSet;
        }

        /// <summary>
        /// Is the user regarded as an administrator?
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        protected bool IsAdministrator(UUID user)
        {
            if (user == UUID.Zero)
                return false;

            if (m_scene.RegionInfo.EstateSettings.EstateOwner == user && m_RegionOwnerIsGod)
                return true;
            
            if (IsEstateManager(user) && m_RegionManagerIsGod)
                return true;

            if (IsGridGod(user, null))
                return true;

            return false;
        }

        /// <summary>
        /// Is the given user a God throughout the grid (not just in the current scene)?
        /// </summary>
        /// <param name="user">The user</param>
        /// <param name="scene">Unused, can be null</param>
        /// <returns></returns>
        protected bool IsGridGod(UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (user == UUID.Zero) return false;

            if (m_allowGridGods)
            {
                ScenePresence sp = m_scene.GetScenePresence(user);
                if (sp != null)
                    return (sp.UserLevel >= 200);

                UserAccount account = m_scene.UserAccountService.GetUserAccount(m_scene.RegionInfo.ScopeID, user);
                if (account != null)
                    return (account.UserLevel >= 200);
            }

            return false;
        }

        protected bool IsFriendWithPerms(UUID user, UUID objectOwner)
        {            
            if (user == UUID.Zero)
                return false;

            if (FriendsModule == null)
                return false;

            int friendPerms = FriendsModule.GetRightsGrantedByFriend(user, objectOwner);
            return (friendPerms & (int)FriendRights.CanModifyObjects) != 0;
        }

        protected bool IsEstateManager(UUID user)
        {
            if (user == UUID.Zero) return false;
        
            return m_scene.RegionInfo.EstateSettings.IsEstateManagerOrOwner(user);
        }

#endregion

        public bool PropagatePermissions()
        {
            if (m_bypassPermissions)
                return false;

            return m_propagatePermissions;
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

        public uint GenerateClientFlags(UUID user, UUID objID)
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

            SceneObjectPart task = m_scene.GetSceneObjectPart(objID);

            // this shouldn't ever happen..     return no permissions/objectflags.
            if (task == null)
                return (uint)0;

            uint objflags = task.GetEffectiveObjectFlags();
            UUID objectOwner = task.OwnerID;


            // Remove any of the objectFlags that are temporary.  These will get added back if appropriate
            // in the next bit of code

            // libomv will moan about PrimFlags.ObjectYouOfficer being
            // deprecated
#pragma warning disable 0612
            objflags &= (uint)
                ~(PrimFlags.ObjectCopy | // Tells client you can copy the object
                    PrimFlags.ObjectModify | // tells client you can modify the object
                    PrimFlags.ObjectMove |   // tells client that you can move the object (only, no mod)
                    PrimFlags.ObjectTransfer | // tells the client that you can /take/ the object if you don't own it
                    PrimFlags.ObjectYouOwner | // Tells client that you're the owner of the object
                    PrimFlags.ObjectAnyOwner | // Tells client that someone owns the object
                    PrimFlags.ObjectOwnerModify | // Tells client that you're the owner of the object
                    PrimFlags.ObjectYouOfficer // Tells client that you've got group object editing permission. Used when ObjectGroupOwned is set
                    );
#pragma warning restore 0612

            // Creating the three ObjectFlags options for this method to choose from.
            // Customize the OwnerMask
            uint objectOwnerMask = ApplyObjectModifyMasks(task.OwnerMask, objflags);
            objectOwnerMask |= (uint)PrimFlags.ObjectYouOwner | (uint)PrimFlags.ObjectAnyOwner | (uint)PrimFlags.ObjectOwnerModify;

            // Customize the GroupMask
            uint objectGroupMask = ApplyObjectModifyMasks(task.GroupMask, objflags);

            // Customize the EveryoneMask
            uint objectEveryoneMask = ApplyObjectModifyMasks(task.EveryoneMask, objflags);
            if (objectOwner != UUID.Zero)
                objectEveryoneMask |= (uint)PrimFlags.ObjectAnyOwner;

            PermissionClass permissionClass = GetPermissionClass(user, task);

            switch (permissionClass)
            {
                case PermissionClass.Owner:
                    return objectOwnerMask;
                case PermissionClass.Group:
                    return objectGroupMask | objectEveryoneMask;
                case PermissionClass.Everyone:
                default:
                    return objectEveryoneMask;
            }
        }

        private uint ApplyObjectModifyMasks(uint setPermissionMask, uint objectFlagsMask)
        {
            // We are adding the temporary objectflags to the object's objectflags based on the
            // permission flag given.  These change the F flags on the client.

            if ((setPermissionMask & (uint)PermissionMask.Copy) != 0)
            {
                objectFlagsMask |= (uint)PrimFlags.ObjectCopy;
            }

            if ((setPermissionMask & (uint)PermissionMask.Move) != 0)
            {
                objectFlagsMask |= (uint)PrimFlags.ObjectMove;
            }

            if ((setPermissionMask & (uint)PermissionMask.Modify) != 0)
            {
                objectFlagsMask |= (uint)PrimFlags.ObjectModify;
            }

            if ((setPermissionMask & (uint)PermissionMask.Transfer) != 0)
            {
                objectFlagsMask |= (uint)PrimFlags.ObjectTransfer;
            }

            return objectFlagsMask;
        }

        public PermissionClass GetPermissionClass(UUID user, SceneObjectPart obj)
        {
            if (obj == null)
                return PermissionClass.Everyone;

            if (m_bypassPermissions)
                return PermissionClass.Owner;

            // Object owners should be able to edit their own content
            UUID objectOwner = obj.OwnerID;
            if (user == objectOwner)
                return PermissionClass.Owner;

            if (IsFriendWithPerms(user, objectOwner) && !obj.ParentGroup.IsAttachment)
                return PermissionClass.Owner;

            // Estate users should be able to edit anything in the sim if RegionOwnerIsGod is set
            if (m_RegionOwnerIsGod && IsEstateManager(user) && !IsAdministrator(objectOwner))
                return PermissionClass.Owner;

            // Admin should be able to edit anything in the sim (including admin objects)
            if (IsAdministrator(user))
                return PermissionClass.Owner;

            // Users should be able to edit what is over their land.
            Vector3 taskPos = obj.AbsolutePosition;
            ILandObject parcel = m_scene.LandChannel.GetLandObject(taskPos.X, taskPos.Y);
            if (parcel != null && parcel.LandData.OwnerID == user && m_ParcelOwnerIsGod)
            {
                // Admin objects should not be editable by the above
                if (!IsAdministrator(objectOwner))
                    return PermissionClass.Owner;
            }

            // Group permissions
            if ((obj.GroupID != UUID.Zero) && IsGroupMember(obj.GroupID, user, 0))
                return PermissionClass.Group;

            return PermissionClass.Everyone;
        }

        /// <summary>
        /// General permissions checks for any operation involving an object.  These supplement more specific checks
        /// implemented by callers.
        /// </summary>
        /// <param name="currentUser"></param>
        /// <param name="objId">This is a scene object group UUID</param>
        /// <param name="denyOnLocked"></param>
        /// <returns></returns>
        protected bool GenericObjectPermission(UUID currentUser, UUID objId, bool denyOnLocked)
        {
            // Default: deny
            bool permission = false;
            bool locked = false;

            SceneObjectPart part = m_scene.GetSceneObjectPart(objId);

            if (part == null)
                return false;

            SceneObjectGroup group = part.ParentGroup;

            UUID objectOwner = group.OwnerID;
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
                // there is no way that later code can change this back to false
                // so just return true immediately and short circuit the more
                // expensive group checks
                return true;
                
                //permission = true;
            }
            else if (group.IsAttachment)
            {
                permission = false;
            }

//            m_log.DebugFormat(
//                "[PERMISSIONS]: group.GroupID = {0}, part.GroupMask = {1}, isGroupMember = {2} for {3}", 
//                group.GroupID,
//                m_scene.GetSceneObjectPart(objId).GroupMask, 
//                IsGroupMember(group.GroupID, currentUser, 0), 
//                currentUser);
            
            // Group members should be able to edit group objects
            if ((group.GroupID != UUID.Zero) 
                && ((m_scene.GetSceneObjectPart(objId).GroupMask & (uint)PermissionMask.Modify) != 0) 
                && IsGroupMember(group.GroupID, currentUser, 0))
            {
                // Return immediately, so that the administrator can shares group objects
                return true;
            }

            // Friends with benefits should be able to edit the objects too
            if (IsFriendWithPerms(currentUser, objectOwner))
            {
                // Return immediately, so that the administrator can share objects with friends
                return true;
            }
        
            // Users should be able to edit what is over their land.
            ILandObject parcel = m_scene.LandChannel.GetLandObject(group.AbsolutePosition.X, group.AbsolutePosition.Y);
            if ((parcel != null) && (parcel.LandData.OwnerID == currentUser))
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
        protected bool GenericCommunicationPermission(UUID user, UUID target)
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

        public bool GenericEstatePermission(UUID user)
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

        protected bool GenericParcelPermission(UUID user, ILandObject parcel, ulong groupPowers)
        {
            bool permission = false;

            if (parcel.LandData.OwnerID == user)
            {
                permission = true;
            }

            if ((parcel.LandData.GroupID != UUID.Zero) && IsGroupMember(parcel.LandData.GroupID, user, groupPowers))
            {
                permission = true;
            }

            if (IsEstateManager(user))
            {
                permission = true;
            }

            if (IsAdministrator(user))
            {
                permission = true;
            }

            if (m_SimpleBuildPermissions &&
                (parcel.LandData.Flags & (uint)ParcelFlags.UseAccessList) == 0 && parcel.IsInLandAccessList(user))
                permission = true;

            return permission;
        }
    
        protected bool GenericParcelOwnerPermission(UUID user, ILandObject parcel, ulong groupPowers, bool allowEstateManager)
        {
            if (parcel.LandData.OwnerID == user)
            {
                // Returning immediately so that group deeded objects on group deeded land don't trigger a NRE on
                // the subsequent redundant checks when using lParcelMediaCommandList()
                // See http://opensimulator.org/mantis/view.php?id=3999 for more details
                return true;
            }

            if (parcel.LandData.IsGroupOwned && IsGroupMember(parcel.LandData.GroupID, user, groupPowers))
            {
                return true;
            }
    
            if (allowEstateManager && IsEstateManager(user))
            {
                return true;
            }

            if (IsAdministrator(user))
            {
                return true;
            }

            return false;
        }

        protected bool GenericParcelPermission(UUID user, Vector3 pos, ulong groupPowers)
        {
            ILandObject parcel = m_scene.LandChannel.GetLandObject(pos.X, pos.Y);
            if (parcel == null) return false;
            return GenericParcelPermission(user, parcel, groupPowers);
        }
#endregion

        #region Permission Checks
        private bool CanAbandonParcel(UUID user, ILandObject parcel, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;
        
            return GenericParcelOwnerPermission(user, parcel, (ulong)GroupPowers.LandRelease, false);
        }

        private bool CanReclaimParcel(UUID user, ILandObject parcel, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericParcelOwnerPermission(user, parcel, 0,true);
        }

        private bool CanDeedParcel(UUID user, ILandObject parcel, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (parcel.LandData.OwnerID != user) // Only the owner can deed!
                return false;

            ScenePresence sp = scene.GetScenePresence(user);
            IClientAPI client = sp.ControllingClient;

            if ((client.GetGroupPowers(parcel.LandData.GroupID) & (ulong)GroupPowers.LandDeed) == 0)
                return false;

            return GenericParcelOwnerPermission(user, parcel, (ulong)GroupPowers.LandDeed, false);
        }

        private bool CanDeedObject(UUID user, UUID group, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            ScenePresence sp = scene.GetScenePresence(user);
            IClientAPI client = sp.ControllingClient;

            if ((client.GetGroupPowers(group) & (ulong)GroupPowers.DeedObject) == 0)
                return false;

            return true;
        }

        private bool IsGod(UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return IsAdministrator(user);
        }

        private bool CanDuplicateObject(int objectCount, UUID objectID, UUID owner, Scene scene, Vector3 objectPosition)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (!GenericObjectPermission(owner, objectID, true))
            {
                //They can't even edit the object
                return false;
            }
        
            SceneObjectPart part = scene.GetSceneObjectPart(objectID);
            if (part == null)
                return false;

            if (part.OwnerID == owner)
            {
                if ((part.OwnerMask & PERM_COPY) == 0)
                    return false;
            }
            else if (part.GroupID != UUID.Zero)
            {
                if ((part.OwnerID == part.GroupID) && ((owner != part.LastOwnerID) || ((part.GroupMask & PERM_TRANS) == 0)))
                    return false;

                if ((part.GroupMask & PERM_COPY) == 0)
                    return false;
            }
        
            //If they can rez, they can duplicate
            return CanRezObject(objectCount, owner, objectPosition, scene);
        }

        private bool CanDeleteObject(UUID objectID, UUID deleter, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericObjectPermission(deleter, objectID, false);
        }

        private bool CanEditObject(UUID objectID, UUID editorID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericObjectPermission(editorID, objectID, false);
        }

        private bool CanEditObjectInventory(UUID objectID, UUID editorID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericObjectPermission(editorID, objectID, false);
        }

        private bool CanEditParcelProperties(UUID user, ILandObject parcel, GroupPowers p, Scene scene, bool allowManager)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericParcelOwnerPermission(user, parcel, (ulong)p, false);
        }

        /// <summary>
        /// Check whether the specified user can edit the given script
        /// </summary>
        /// <param name="script"></param>
        /// <param name="objectID"></param>
        /// <param name="user"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        private bool CanEditScript(UUID script, UUID objectID, UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;
                            
            if (m_allowedScriptEditors == UserSet.Administrators && !IsAdministrator(user))
                return false;
            
            // Ordinarily, if you can view it, you can edit it
            // There is no viewing a no mod script
            //
            return CanViewScript(script, objectID, user, scene);
        }

        /// <summary>
        /// Check whether the specified user can edit the given notecard
        /// </summary>
        /// <param name="notecard"></param>
        /// <param name="objectID"></param>
        /// <param name="user"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        private bool CanEditNotecard(UUID notecard, UUID objectID, UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (objectID == UUID.Zero) // User inventory
            {
                IInventoryService invService = m_scene.InventoryService;
                InventoryItemBase assetRequestItem = new InventoryItemBase(notecard, user);
                assetRequestItem = invService.GetItem(assetRequestItem);
                if (assetRequestItem == null && LibraryRootFolder != null) // Library item
                {
                    assetRequestItem = LibraryRootFolder.FindItem(notecard);

                    if (assetRequestItem != null) // Implicitly readable
                        return true;
                }

                // Notecards must be both mod and copy to be saveable
                // This is because of they're not copy, you can't read
                // them, and if they're not mod, well, then they're
                // not mod. Duh.
                //
                if ((assetRequestItem.CurrentPermissions &
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy)) !=
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy))
                    return false;
            }
            else // Prim inventory
            {
                SceneObjectPart part = scene.GetSceneObjectPart(objectID);

                if (part == null)
                    return false;

                if (part.OwnerID != user)
                {
                    if (part.GroupID == UUID.Zero)
                        return false;

                    if (!IsGroupMember(part.GroupID, user, 0))
                        return false;
            
                    if ((part.GroupMask & (uint)PermissionMask.Modify) == 0)
                        return false;
                } 
                else
                {
                    if ((part.OwnerMask & (uint)PermissionMask.Modify) == 0)
                    return false;
                }

                TaskInventoryItem ti = part.Inventory.GetInventoryItem(notecard);

                if (ti == null)
                    return false;

                if (ti.OwnerID != user)
                {
                    if (ti.GroupID == UUID.Zero)
                        return false;

                    if (!IsGroupMember(ti.GroupID, user, 0))
                        return false;
                }

                // Require full perms
                if ((ti.CurrentPermissions &
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy)) !=
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy))
                    return false;
            }

            return true;
        }

        private bool CanInstantMessage(UUID user, UUID target, Scene startScene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            // If the sender is an object, check owner instead
            //
            SceneObjectPart part = startScene.GetSceneObjectPart(user);
            if (part != null)
                user = part.OwnerID;

            return GenericCommunicationPermission(user, target);
        }

        private bool CanInventoryTransfer(UUID user, UUID target, Scene startScene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericCommunicationPermission(user, target);
        }

        private bool CanIssueEstateCommand(UUID user, Scene requestFromScene, bool ownerCommand)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (IsAdministrator(user))
                return true;

            if (m_scene.RegionInfo.EstateSettings.IsEstateOwner(user))
                return true;

            if (ownerCommand)
                return false;

            return GenericEstatePermission(user);
        }

        private bool CanMoveObject(UUID objectID, UUID moverID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions)
            {
                SceneObjectPart part = scene.GetSceneObjectPart(objectID);
                if (part.OwnerID != moverID)
                {
                    if (!part.ParentGroup.IsDeleted)
                    {
                        if (part.ParentGroup.IsAttachment)
                            return false;
                    }
                }
                return m_bypassPermissionsValue;
            }

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


                // UUID taskOwner = null;
                // Added this because at this point in time it wouldn't be wise for
                // the administrator object permissions to take effect.
                // UUID objectOwner = task.OwnerID;

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

                UUID objectOwner = group.OwnerID;
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

        private bool CanObjectEntry(UUID objectID, bool enteringRegion, Vector3 newPoint, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if ((newPoint.X > 257f || newPoint.X < -1f || newPoint.Y > 257f || newPoint.Y < -1f))
            {
                return true;
            }

            SceneObjectGroup task = (SceneObjectGroup)m_scene.Entities[objectID];

            ILandObject land = m_scene.LandChannel.GetLandObject(newPoint.X, newPoint.Y);

            if (!enteringRegion)
            {
                ILandObject fromland = m_scene.LandChannel.GetLandObject(task.AbsolutePosition.X, task.AbsolutePosition.Y);

                if (fromland == land) // Not entering
                    return true;
            }

            if (land == null)
            {
                return false;
            }

            if ((land.LandData.Flags & ((int)ParcelFlags.AllowAPrimitiveEntry)) != 0)
            {
                return true;
            }

            if (!m_scene.Entities.ContainsKey(objectID))
            {
                return false;
            }

            // If it's not an object, we cant edit it.
            if (!(m_scene.Entities[objectID] is SceneObjectGroup))
            {
                return false;
            }


            if (GenericParcelPermission(task.OwnerID, newPoint, 0))
            {
                return true;
            }

            //Otherwise, false!
            return false;
        }

        private bool CanReturnObjects(ILandObject land, UUID user, List<SceneObjectGroup> objects, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            GroupPowers powers;
            ILandObject l;

            ScenePresence sp = scene.GetScenePresence(user);
            if (sp == null)
                return false;

            IClientAPI client = sp.ControllingClient;

            foreach (SceneObjectGroup g in new List<SceneObjectGroup>(objects))
            {
                // Any user can return their own objects at any time
                //
                if (GenericObjectPermission(user, g.UUID, false))
                    continue;

                // This is a short cut for efficiency. If land is non-null,
                // then all objects are on that parcel and we can save
                // ourselves the checking for each prim. Much faster.
                //
                if (land != null)
                {
                    l = land;
                }
                else
                {
                    Vector3 pos = g.AbsolutePosition;

                    l = scene.LandChannel.GetLandObject(pos.X, pos.Y);
                }

                // If it's not over any land, then we can't do a thing
                if (l == null)
                {
                    objects.Remove(g);
                    continue;
                }

                // If we own the land outright, then allow
                //
                if (l.LandData.OwnerID == user)
                    continue;

                // Group voodoo
                //
                if (l.LandData.IsGroupOwned)
                {
                    powers = (GroupPowers)client.GetGroupPowers(l.LandData.GroupID);
                    // Not a group member, or no rights at all
                    //
                    if (powers == (GroupPowers)0)
                    {
                        objects.Remove(g);
                        continue;
                    }

                    // Group deeded object?
                    //
                    if (g.OwnerID == l.LandData.GroupID &&
                        (powers & GroupPowers.ReturnGroupOwned) == (GroupPowers)0)
                    {
                        objects.Remove(g);
                        continue;
                    }

                    // Group set object?
                    //
                    if (g.GroupID == l.LandData.GroupID &&
                        (powers & GroupPowers.ReturnGroupSet) == (GroupPowers)0)
                    {
                        objects.Remove(g);
                        continue;
                    }

                    if ((powers & GroupPowers.ReturnNonGroup) == (GroupPowers)0)
                    {
                        objects.Remove(g);
                        continue;
                    }

                    // So we can remove all objects from this group land.
                    // Fine.
                    //
                    continue;
                }

                // By default, we can't remove
                //
                objects.Remove(g);
            }

            if (objects.Count == 0)
                return false;

            return true;
        }

        private bool CanRezObject(int objectCount, UUID owner, Vector3 objectPosition, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

//            m_log.DebugFormat("[PERMISSIONS MODULE]: Checking rez object at {0} in {1}", objectPosition, m_scene.Name);

            ILandObject parcel = m_scene.LandChannel.GetLandObject(objectPosition.X, objectPosition.Y);
            if (parcel == null)
                return false;

            if ((parcel.LandData.Flags & (uint)ParcelFlags.CreateObjects) != 0)
            {
                return true;
            }
            else if ((owner == parcel.LandData.OwnerID) || IsAdministrator(owner))
            {
                return true;
            }
            else if (((parcel.LandData.Flags & (uint)ParcelFlags.CreateGroupObjects) != 0)
                && (parcel.LandData.GroupID != UUID.Zero) && IsGroupMember(parcel.LandData.GroupID, owner, 0))
            {
                return true;
            }
            else if (parcel.LandData.GroupID != UUID.Zero && IsGroupMember(parcel.LandData.GroupID, owner, (ulong)GroupPowers.AllowRez))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool CanRunConsoleCommand(UUID user, Scene requestFromScene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;


            return IsAdministrator(user);
        }

        private bool CanRunScript(UUID script, UUID objectID, UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanSellParcel(UUID user, ILandObject parcel, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericParcelOwnerPermission(user, parcel, (ulong)GroupPowers.LandSetSale, false);
        }

        private bool CanTakeObject(UUID objectID, UUID stealer, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericObjectPermission(stealer,objectID, false);
        }

        private bool CanTakeCopyObject(UUID objectID, UUID userID, Scene inScene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            bool permission = GenericObjectPermission(userID, objectID, false);

            SceneObjectGroup so = (SceneObjectGroup)m_scene.Entities[objectID];

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

                // UUID taskOwner = null;
                // Added this because at this point in time it wouldn't be wise for
                // the administrator object permissions to take effect.
                // UUID objectOwner = task.OwnerID;

                if ((so.RootPart.EveryoneMask & PERM_COPY) != 0)
                    permission = true;
            }

            if (so.OwnerID != userID)
            {
                if ((so.GetEffectivePermissions() & (PERM_COPY | PERM_TRANS)) != (PERM_COPY | PERM_TRANS))
                    permission = false;
            }
            else
            {
                if ((so.GetEffectivePermissions() & PERM_COPY) != PERM_COPY)
                    permission = false;
            }
            
            return permission;
        }

        private bool CanTerraformLand(UUID user, Vector3 position, Scene requestFromScene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            // Estate override
            if (GenericEstatePermission(user))
                return true;

            float X = position.X;
            float Y = position.Y;

            if (X > ((int)m_scene.RegionInfo.RegionSizeX - 1))
                X = ((int)m_scene.RegionInfo.RegionSizeX - 1);
            if (Y > ((int)m_scene.RegionInfo.RegionSizeY - 1))
                Y = ((int)m_scene.RegionInfo.RegionSizeY - 1);
            if (X < 0)
                X = 0;
            if (Y < 0)
                Y = 0;

            ILandObject parcel = m_scene.LandChannel.GetLandObject(X, Y);
            if (parcel == null)
                return false;

            // Others allowed to terraform?
            if ((parcel.LandData.Flags & ((int)ParcelFlags.AllowTerraform)) != 0)
                return true;

            // Land owner can terraform too
            if (parcel != null && GenericParcelPermission(user, parcel, (ulong)GroupPowers.AllowEditLand))
                return true;

            return false;
        }

        /// <summary>
        /// Check whether the specified user can view the given script
        /// </summary>
        /// <param name="script"></param>
        /// <param name="objectID"></param>
        /// <param name="user"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        private bool CanViewScript(UUID script, UUID objectID, UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (objectID == UUID.Zero) // User inventory
            {
                IInventoryService invService = m_scene.InventoryService;
                InventoryItemBase assetRequestItem = new InventoryItemBase(script, user);
                assetRequestItem = invService.GetItem(assetRequestItem);
                if (assetRequestItem == null && LibraryRootFolder != null) // Library item
                {
                    assetRequestItem = LibraryRootFolder.FindItem(script);

                    if (assetRequestItem != null) // Implicitly readable
                        return true;
                }

                // SL is rather harebrained here. In SL, a script you
                // have mod/copy no trans is readable. This subverts
                // permissions, but is used in some products, most
                // notably Hippo door plugin and HippoRent 5 networked
                // prim counter.
                // To enable this broken SL-ism, remove Transfer from
                // the below expressions.
                // Trying to improve on SL perms by making a script
                // readable only if it's really full perms
                //
                if ((assetRequestItem.CurrentPermissions &
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy |
                        (uint)PermissionMask.Transfer)) !=
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy |
                        (uint)PermissionMask.Transfer))
                    return false;
            }
            else // Prim inventory
            {
                SceneObjectPart part = scene.GetSceneObjectPart(objectID);

                if (part == null)
                    return false;
            
                if (part.OwnerID != user)
                {
                    if (part.GroupID == UUID.Zero)
                        return false;

                    if (!IsGroupMember(part.GroupID, user, 0))
                        return false;
            
                    if ((part.GroupMask & (uint)PermissionMask.Modify) == 0)
                        return false;
                } 
                else 
                {
                    if ((part.OwnerMask & (uint)PermissionMask.Modify) == 0)
                        return false;
                }

                TaskInventoryItem ti = part.Inventory.GetInventoryItem(script);

                if (ti == null)
                    return false;
            
                if (ti.OwnerID != user)
                {
                    if (ti.GroupID == UUID.Zero)
                        return false;
        
                    if (!IsGroupMember(ti.GroupID, user, 0))
                        return false;
                }

                // Require full perms
                if ((ti.CurrentPermissions &
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy |
                        (uint)PermissionMask.Transfer)) !=
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy |
                        (uint)PermissionMask.Transfer))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Check whether the specified user can view the given notecard
        /// </summary>
        /// <param name="script"></param>
        /// <param name="objectID"></param>
        /// <param name="user"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        private bool CanViewNotecard(UUID notecard, UUID objectID, UUID user, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (objectID == UUID.Zero) // User inventory
            {
                IInventoryService invService = m_scene.InventoryService;
                InventoryItemBase assetRequestItem = new InventoryItemBase(notecard, user);
                assetRequestItem = invService.GetItem(assetRequestItem);
                if (assetRequestItem == null && LibraryRootFolder != null) // Library item
                {
                    assetRequestItem = LibraryRootFolder.FindItem(notecard);

                    if (assetRequestItem != null) // Implicitly readable
                        return true;
                }

                // Notecards are always readable unless no copy
                //
                if ((assetRequestItem.CurrentPermissions &
                        (uint)PermissionMask.Copy) !=
                        (uint)PermissionMask.Copy)
                    return false;
            }
            else // Prim inventory
            {
                SceneObjectPart part = scene.GetSceneObjectPart(objectID);

                if (part == null)
                    return false;
            
                if (part.OwnerID != user)
                {
                    if (part.GroupID == UUID.Zero)
                        return false;
        
                    if (!IsGroupMember(part.GroupID, user, 0))
                        return false;
                }

                if ((part.OwnerMask & (uint)PermissionMask.Modify) == 0)
                    return false;

                TaskInventoryItem ti = part.Inventory.GetInventoryItem(notecard);

                if (ti == null)
                    return false;

                if (ti.OwnerID != user)
                {
                    if (ti.GroupID == UUID.Zero)
                        return false;
        
                    if (!IsGroupMember(ti.GroupID, user, 0))
                        return false;
                }

                // Notecards are always readable unless no copy
                //
                if ((ti.CurrentPermissions &
                        (uint)PermissionMask.Copy) !=
                        (uint)PermissionMask.Copy)
                    return false;
            }

            return true;
        }

        #endregion

        private bool CanLinkObject(UUID userID, UUID objectID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericObjectPermission(userID, objectID, false);
        }

        private bool CanDelinkObject(UUID userID, UUID objectID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericObjectPermission(userID, objectID, false);
        }

        private bool CanBuyLand(UUID userID, ILandObject parcel, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanCopyObjectInventory(UUID itemID, UUID objectID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanDeleteObjectInventory(UUID itemID, UUID objectID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        /// <summary>
        /// Check whether the specified user is allowed to directly create the given inventory type in a prim's
        /// inventory (e.g. the New Script button in the 1.21 Linden Lab client).
        /// </summary>
        /// <param name="invType"></param>
        /// <param name="objectID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        private bool CanCreateObjectInventory(int invType, UUID objectID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            SceneObjectPart part = m_scene.GetSceneObjectPart(objectID);
            ScenePresence p = m_scene.GetScenePresence(userID);

            if (part == null || p == null)
                return false;

            if (!IsAdministrator(userID))
            {
                if (part.OwnerID != userID)
                {
                    // Group permissions
                    if ((part.GroupID == UUID.Zero) || (p.ControllingClient.GetGroupPowers(part.GroupID) == 0) || ((part.GroupMask & (uint)PermissionMask.Modify) == 0))
                        return false;
                } else {
                    if ((part.OwnerMask & (uint)PermissionMask.Modify) == 0)
                        return false;
                }
                if ((int)InventoryType.LSL == invType)
                    if (m_allowedScriptCreators == UserSet.Administrators)
                        return false;
            }

            return true;
        }
        
        /// <summary>
        /// Check whether the specified user is allowed to create the given inventory type in their inventory.
        /// </summary>
        /// <param name="invType"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        private bool CanCreateUserInventory(int invType, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if ((int)InventoryType.LSL == invType)
                if (m_allowedScriptCreators == UserSet.Administrators && !IsAdministrator(userID))
                    return false;
            
            return true;
        }
        
        /// <summary>
        /// Check whether the specified user is allowed to copy the given inventory type in their inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        private bool CanCopyUserInventory(UUID itemID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }
        
        /// <summary>
        /// Check whether the specified user is allowed to edit the given inventory item within their own inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        private bool CanEditUserInventory(UUID itemID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }
        
        /// <summary>
        /// Check whether the specified user is allowed to delete the given inventory item from their own inventory.
        /// </summary>
        /// <param name="itemID"></param>
        /// <param name="userID"></param>
        /// <returns></returns>
        private bool CanDeleteUserInventory(UUID itemID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanTeleport(UUID userID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanResetScript(UUID prim, UUID script, UUID agentID, Scene scene)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            SceneObjectPart part = m_scene.GetSceneObjectPart(prim);

            // If we selected a sub-prim to reset, prim won't represent the object, but only a part.
            // We have to check the permissions of the object, though.
            if (part.ParentID != 0) prim = part.ParentUUID;

            // You can reset the scripts in any object you can edit
            return GenericObjectPermission(agentID, prim, false);
        }

        private bool CanCompileScript(UUID ownerUUID, int scriptType, Scene scene) 
        {
             //m_log.DebugFormat("check if {0} is allowed to compile {1}", ownerUUID, scriptType);
            switch (scriptType) {
                case 0:
                    if (GrantLSL.Count == 0 || GrantLSL.ContainsKey(ownerUUID.ToString())) {
                        return(true);
                    }
                    break;
                case 1:
                    if (GrantCS.Count == 0 || GrantCS.ContainsKey(ownerUUID.ToString())) {
                        return(true);
                    }
                    break;
                case 2:
                    if (GrantVB.Count == 0 || GrantVB.ContainsKey(ownerUUID.ToString())) {
                        return(true);
                    }
                    break;
                case 3:
                    if (GrantJS.Count == 0 || GrantJS.ContainsKey(ownerUUID.ToString()))
                    {
                        return (true);
                    }
                    break;
                case 4:
                    if (GrantYP.Count == 0 || GrantYP.ContainsKey(ownerUUID.ToString()))
                    {
                        return (true);
                    }
                    break;
            }
            return(false);
        }
        
        private bool CanControlPrimMedia(UUID agentID, UUID primID, int face)
        {
//            m_log.DebugFormat(
//                "[PERMISSONS]: Performing CanControlPrimMedia check with agentID {0}, primID {1}, face {2}",
//                agentID, primID, face);
            
            if (null == MoapModule)
                return false;
            
            SceneObjectPart part = m_scene.GetSceneObjectPart(primID);
            if (null == part)
                return false;
            
            MediaEntry me = MoapModule.GetMediaEntry(part, face);
            
            // If there is no existing media entry then it can be controlled (in this context, created).
            if (null == me)
                return true;
            
//            m_log.DebugFormat(
//                "[PERMISSIONS]: Checking CanControlPrimMedia for {0} on {1} face {2} with control permissions {3}", 
//                agentID, primID, face, me.ControlPermissions);
            
            return GenericObjectPermission(agentID, part.ParentGroup.UUID, true);
        }
        
        private bool CanInteractWithPrimMedia(UUID agentID, UUID primID, int face)
        {
//            m_log.DebugFormat(
//                "[PERMISSONS]: Performing CanInteractWithPrimMedia check with agentID {0}, primID {1}, face {2}",
//                agentID, primID, face);
            
            if (null == MoapModule)
                return false;
            
            SceneObjectPart part = m_scene.GetSceneObjectPart(primID);
            if (null == part)
                return false;
            
            MediaEntry me = MoapModule.GetMediaEntry(part, face);
            
            // If there is no existing media entry then it can be controlled (in this context, created).
            if (null == me)
                return true;
            
//            m_log.DebugFormat(
//                "[PERMISSIONS]: Checking CanInteractWithPrimMedia for {0} on {1} face {2} with interact permissions {3}", 
//                agentID, primID, face, me.InteractPermissions);
            
            return GenericPrimMediaPermission(part, agentID, me.InteractPermissions);
        }
        
        private bool GenericPrimMediaPermission(SceneObjectPart part, UUID agentID, MediaPermission perms)
        {
//            if (IsAdministrator(agentID))
//                return true;
            
            if ((perms & MediaPermission.Anyone) == MediaPermission.Anyone)
                return true;

            if ((perms & MediaPermission.Owner) == MediaPermission.Owner)
            {
                if (agentID == part.OwnerID)
                    return true;
            }
            
            if ((perms & MediaPermission.Group) == MediaPermission.Group)
            {
                if (IsGroupMember(part.GroupID, agentID, 0))
                    return true;
            }
            
            return false;
        }
    }
}
