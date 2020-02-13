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
        protected ScenePermissions scenePermissions;
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
        private bool m_allowGridAdmins = false;
        private bool m_RegionOwnerIsAdmin = false;
        private bool m_RegionManagerIsAdmin = false;
        private bool m_forceGridAdminsOnly;
        private bool m_forceAdminModeAlwaysOn;
        private bool m_allowAdminActionsWithoutGodMode;

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

            string[] sections = new string[] { "Startup", "Permissions" };

            m_allowGridAdmins = Util.GetConfigVarFromSections<bool>(config, "allow_grid_gods", sections, false);
            m_bypassPermissions = !Util.GetConfigVarFromSections<bool>(config, "serverside_object_permissions", sections, true);
            m_propagatePermissions = Util.GetConfigVarFromSections<bool>(config, "propagate_permissions", sections, true);

            m_forceGridAdminsOnly = Util.GetConfigVarFromSections<bool>(config, "force_grid_gods_only", sections, false);
            if(!m_forceGridAdminsOnly)
            {            
                m_RegionOwnerIsAdmin = Util.GetConfigVarFromSections<bool>(config, "region_owner_is_god",sections, true);
                m_RegionManagerIsAdmin = Util.GetConfigVarFromSections<bool>(config, "region_manager_is_god",sections, false);
            }
            else
                m_allowGridAdmins = true;

            m_forceAdminModeAlwaysOn = Util.GetConfigVarFromSections<bool>(config, "automatic_gods", sections, false);
            m_allowAdminActionsWithoutGodMode = Util.GetConfigVarFromSections<bool>(config, "implicit_gods", sections, false);
            if(m_allowAdminActionsWithoutGodMode)
                m_forceAdminModeAlwaysOn = false;

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
            scenePermissions = m_scene.Permissions;

            //Register functions with Scene External Checks!
            scenePermissions.OnBypassPermissions += BypassPermissions;
            scenePermissions.OnSetBypassPermissions += SetBypassPermissions;
            scenePermissions.OnPropagatePermissions += PropagatePermissions;

            scenePermissions.OnIsGridGod += IsGridAdministrator;
            scenePermissions.OnIsAdministrator += IsAdministrator;
            scenePermissions.OnIsEstateManager += IsEstateManager;

            scenePermissions.OnGenerateClientFlags += GenerateClientFlags;

            scenePermissions.OnIssueEstateCommand += CanIssueEstateCommand;
            scenePermissions.OnRunConsoleCommand += CanRunConsoleCommand;

            scenePermissions.OnTeleport += CanTeleport;

            scenePermissions.OnInstantMessage += CanInstantMessage;

            scenePermissions.OnAbandonParcel += CanAbandonParcel;
            scenePermissions.OnReclaimParcel += CanReclaimParcel;
            scenePermissions.OnDeedParcel += CanDeedParcel;
            scenePermissions.OnSellParcel += CanSellParcel;
            scenePermissions.OnEditParcelProperties += CanEditParcelProperties;
            scenePermissions.OnTerraformLand += CanTerraformLand;
            scenePermissions.OnBuyLand += CanBuyLand;

            scenePermissions.OnReturnObjects += CanReturnObjects;

            scenePermissions.OnRezObject += CanRezObject;
            scenePermissions.OnObjectEntry += CanObjectEntry;
            scenePermissions.OnObjectEnterWithScripts += OnObjectEnterWithScripts;

            scenePermissions.OnDuplicateObject += CanDuplicateObject;
            scenePermissions.OnDeleteObjectByIDs += CanDeleteObjectByIDs;
            scenePermissions.OnDeleteObject += CanDeleteObject;
            scenePermissions.OnEditObjectByIDs += CanEditObjectByIDs;
            scenePermissions.OnEditObject += CanEditObject;
            scenePermissions.OnEditObjectPerms += CanEditObjectPerms;
            scenePermissions.OnInventoryTransfer += CanInventoryTransfer;
            scenePermissions.OnMoveObject += CanMoveObject;
            scenePermissions.OnTakeObject += CanTakeObject;
            scenePermissions.OnTakeCopyObject += CanTakeCopyObject;
            scenePermissions.OnLinkObject += CanLinkObject;
            scenePermissions.OnDelinkObject += CanDelinkObject;
            scenePermissions.OnDeedObject += CanDeedObject;
            scenePermissions.OnSellGroupObject += CanSellGroupObject;
            scenePermissions.OnSellObjectByUserID += CanSellObjectByUserID;
            scenePermissions.OnSellObject += CanSellObject;
            
            scenePermissions.OnCreateObjectInventory += CanCreateObjectInventory;
            scenePermissions.OnEditObjectInventory += CanEditObjectInventory;
            scenePermissions.OnCopyObjectInventory += CanCopyObjectInventory;
            scenePermissions.OnDeleteObjectInventory += CanDeleteObjectInventory;
            scenePermissions.OnDoObjectInvToObjectInv += CanDoObjectInvToObjectInv;
            scenePermissions.OnDropInObjectInv += CanDropInObjectInv;

            scenePermissions.OnViewNotecard += CanViewNotecard;
            scenePermissions.OnViewScript += CanViewScript;
            scenePermissions.OnEditNotecard += CanEditNotecard;
            scenePermissions.OnEditScript += CanEditScript;
            scenePermissions.OnResetScript += CanResetScript;
            scenePermissions.OnRunScript += CanRunScript;
            scenePermissions.OnCompileScript += CanCompileScript;
            
            scenePermissions.OnCreateUserInventory += CanCreateUserInventory;
            scenePermissions.OnCopyUserInventory += CanCopyUserInventory;
            scenePermissions.OnEditUserInventory += CanEditUserInventory;
            scenePermissions.OnDeleteUserInventory += CanDeleteUserInventory;

            scenePermissions.OnControlPrimMedia += CanControlPrimMedia;
            scenePermissions.OnInteractWithPrimMedia += CanInteractWithPrimMedia;

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

            scenePermissions.OnBypassPermissions -= BypassPermissions;
            scenePermissions.OnSetBypassPermissions -= SetBypassPermissions;
            scenePermissions.OnPropagatePermissions -= PropagatePermissions;

            scenePermissions.OnIsGridGod -= IsGridAdministrator;
            scenePermissions.OnIsAdministrator -= IsAdministrator;
            scenePermissions.OnIsEstateManager -= IsEstateManager;

            scenePermissions.OnGenerateClientFlags -= GenerateClientFlags;

            scenePermissions.OnIssueEstateCommand -= CanIssueEstateCommand;
            scenePermissions.OnRunConsoleCommand -= CanRunConsoleCommand;

            scenePermissions.OnTeleport -= CanTeleport;

            scenePermissions.OnInstantMessage -= CanInstantMessage;

            scenePermissions.OnAbandonParcel -= CanAbandonParcel;
            scenePermissions.OnReclaimParcel -= CanReclaimParcel;
            scenePermissions.OnDeedParcel -= CanDeedParcel;
            scenePermissions.OnSellParcel -= CanSellParcel;
            scenePermissions.OnEditParcelProperties -= CanEditParcelProperties;
            scenePermissions.OnTerraformLand -= CanTerraformLand;
            scenePermissions.OnBuyLand -= CanBuyLand;

            scenePermissions.OnRezObject -= CanRezObject;
            scenePermissions.OnObjectEntry -= CanObjectEntry;
            scenePermissions.OnObjectEnterWithScripts -= OnObjectEnterWithScripts;

            scenePermissions.OnReturnObjects -= CanReturnObjects;

            scenePermissions.OnDuplicateObject -= CanDuplicateObject;
            scenePermissions.OnDeleteObjectByIDs -= CanDeleteObjectByIDs;
            scenePermissions.OnDeleteObject -= CanDeleteObject;
            scenePermissions.OnEditObjectByIDs -= CanEditObjectByIDs;
            scenePermissions.OnEditObject -= CanEditObject;
            scenePermissions.OnEditObjectPerms -= CanEditObjectPerms;
            scenePermissions.OnInventoryTransfer -= CanInventoryTransfer;
            scenePermissions.OnMoveObject -= CanMoveObject;
            scenePermissions.OnTakeObject -= CanTakeObject;
            scenePermissions.OnTakeCopyObject -= CanTakeCopyObject;
            scenePermissions.OnLinkObject -= CanLinkObject;
            scenePermissions.OnDelinkObject -= CanDelinkObject;
            scenePermissions.OnDeedObject -= CanDeedObject;

            scenePermissions.OnSellGroupObject -= CanSellGroupObject;
            scenePermissions.OnSellObjectByUserID -= CanSellObjectByUserID;
            scenePermissions.OnSellObject -= CanSellObject;
            
            scenePermissions.OnCreateObjectInventory -= CanCreateObjectInventory;
            scenePermissions.OnEditObjectInventory -= CanEditObjectInventory;
            scenePermissions.OnCopyObjectInventory -= CanCopyObjectInventory;
            scenePermissions.OnDeleteObjectInventory -= CanDeleteObjectInventory;
            scenePermissions.OnDoObjectInvToObjectInv -= CanDoObjectInvToObjectInv;
            scenePermissions.OnDropInObjectInv -= CanDropInObjectInv;

            scenePermissions.OnViewNotecard -= CanViewNotecard;
            scenePermissions.OnViewScript -= CanViewScript;
            scenePermissions.OnEditNotecard -= CanEditNotecard;
            scenePermissions.OnEditScript -= CanEditScript;
            scenePermissions.OnResetScript -= CanResetScript;
            scenePermissions.OnRunScript -= CanRunScript;
            scenePermissions.OnCompileScript -= CanCompileScript;
            
            scenePermissions.OnCreateUserInventory -= CanCreateUserInventory;
            scenePermissions.OnCopyUserInventory -= CanCopyUserInventory;
            scenePermissions.OnEditUserInventory -= CanEditUserInventory;
            scenePermissions.OnDeleteUserInventory -= CanDeleteUserInventory;

            scenePermissions.OnControlPrimMedia -= CanControlPrimMedia;
            scenePermissions.OnInteractWithPrimMedia -= CanInteractWithPrimMedia;

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

        protected bool GroupMemberPowers(UUID groupID, UUID userID, ref ulong powers)
        {
            powers = 0;
            if (null == GroupsModule)
                return false;

            GroupMembershipData gmd = GroupsModule.GetMembershipData(groupID, userID);
            
            if (gmd != null)
            {
                powers = gmd.GroupPowers;
                return true;
            }
            return false;
        }

        protected bool GroupMemberPowers(UUID groupID, ScenePresence sp, ref ulong powers)
        {
            powers = 0;
            IClientAPI client = sp.ControllingClient;
            if (client == null)
                return false;

            if(!client.IsGroupMember(groupID))
                return false;
            
            powers =  client.GetGroupPowers(groupID);
            return true;
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

            if (m_RegionOwnerIsAdmin && m_scene.RegionInfo.EstateSettings.EstateOwner == user)
                return true;

            if (m_RegionManagerIsAdmin && IsEstateManager(user))
                return true;

            if (IsGridAdministrator(user))
                return true;

            return false;
        }

        /// <summary>
        /// Is the given user a God throughout the grid (not just in the current scene)?
        /// </summary>
        /// <param name="user">The user</param>
        /// <param name="scene">Unused, can be null</param>
        /// <returns></returns>
        protected bool IsGridAdministrator(UUID user)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (user == UUID.Zero)
                return false;

            if (m_allowGridAdmins)
            {
                ScenePresence sp = m_scene.GetScenePresence(user);
                if (sp != null)
                    return (sp.GodController.UserLevel >= 200);

                UserAccount account = m_scene.UserAccountService.GetUserAccount(m_scene.RegionInfo.ScopeID, user);
                if (account != null)
                    return (account.UserLevel >= 200);
            }

            return false;
        }

        protected bool IsFriendWithPerms(UUID user, UUID objectOwner)
        {
            if (FriendsModule == null)
                return false;

            if (user == UUID.Zero)
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

        const uint DEFAULT_FLAGS  = (uint)(
            PrimFlags.ObjectCopy | // Tells client you can copy the object
            PrimFlags.ObjectModify | // tells client you can modify the object
            PrimFlags.ObjectMove |   // tells client that you can move the object (only, no mod)
            PrimFlags.ObjectTransfer | // tells the client that you can /take/ the object if you don't own it
            PrimFlags.ObjectYouOwner | // Tells client that you're the owner of the object
            PrimFlags.ObjectAnyOwner | // Tells client that someone owns the object
            PrimFlags.ObjectOwnerModify // Tells client that you're the owner of the object
            );

        const uint NOT_DEFAULT_FLAGS  = (uint)~(
            PrimFlags.ObjectCopy | // Tells client you can copy the object
            PrimFlags.ObjectModify | // tells client you can modify the object
            PrimFlags.ObjectMove |   // tells client that you can move the object (only, no mod)
            PrimFlags.ObjectTransfer | // tells the client that you can /take/ the object if you don't own it
            PrimFlags.ObjectYouOwner | // Tells client that you're the owner of the object
            PrimFlags.ObjectAnyOwner | // Tells client that someone owns the object
            PrimFlags.ObjectOwnerModify // Tells client that you're the owner of the object
            );

        const uint EXTRAOWNERMASK = (uint)(
                PrimFlags.ObjectYouOwner | 
                PrimFlags.ObjectAnyOwner
                );

        const uint EXTRAGODMASK = (uint)(
                PrimFlags.ObjectYouOwner | 
                PrimFlags.ObjectAnyOwner |
                PrimFlags.ObjectOwnerModify |
                PrimFlags.ObjectModify |
                PrimFlags.ObjectMove
                );

        const uint GOD_FLAGS  = (uint)(
            PrimFlags.ObjectCopy | // Tells client you can copy the object
            PrimFlags.ObjectModify | // tells client you can modify the object
            PrimFlags.ObjectMove |   // tells client that you can move the object (only, no mod)
            PrimFlags.ObjectTransfer | // tells the client that you can /take/ the object if you don't own it
            PrimFlags.ObjectYouOwner | // Tells client that you're the owner of the object
            PrimFlags.ObjectAnyOwner | // Tells client that someone owns the object
            PrimFlags.ObjectOwnerModify // Tells client that you're the owner of the object
            );

        const uint LOCKED_GOD_FLAGS  = (uint)(
            PrimFlags.ObjectCopy | // Tells client you can copy the object
            PrimFlags.ObjectTransfer | // tells the client that you can /take/ the object if you don't own it
            PrimFlags.ObjectYouOwner | // Tells client that you're the owner of the object
            PrimFlags.ObjectAnyOwner // Tells client that someone owns the object
            );

        const uint SHAREDMASK  = (uint)(
            PermissionMask.Move |
            PermissionMask.Modify |
            PermissionMask.Copy
            );

        public uint GenerateClientFlags(SceneObjectPart task, ScenePresence sp, uint curEffectivePerms)
        {
            if(sp == null  || task == null || curEffectivePerms == 0)
                return 0;

            // Remove any of the objectFlags that are temporary.  These will get added back if appropriate
            uint objflags = curEffectivePerms & NOT_DEFAULT_FLAGS ;

            uint returnMask;

            SceneObjectGroup grp = task.ParentGroup;
            if(grp == null)
                return 0;

            UUID taskOwnerID = task.OwnerID;
            UUID spID = sp.UUID;

            bool unlocked = (grp.RootPart.OwnerMask & (uint)PermissionMask.Move) != 0;

            if(sp.IsGod)
            {
                // do locked on objects owned by admin
                if(!unlocked && spID == taskOwnerID)
                    return objflags | LOCKED_GOD_FLAGS;
                else
                    return objflags | GOD_FLAGS;
            }

            //bypass option == owner rights
            if (m_bypassPermissions)
            {
                returnMask = ApplyObjectModifyMasks(task.OwnerMask, objflags, true);  //??
                returnMask |= EXTRAOWNERMASK;
                if((returnMask & (uint)PrimFlags.ObjectModify) != 0)
                    returnMask |= (uint)PrimFlags.ObjectOwnerModify;
                return returnMask;
            }

            // owner
            if (spID == taskOwnerID)
            {
                returnMask = ApplyObjectModifyMasks(grp.EffectiveOwnerPerms, objflags, unlocked);
                returnMask |= EXTRAOWNERMASK;
                if((returnMask & (uint)PrimFlags.ObjectModify) != 0)
                    returnMask |= (uint)PrimFlags.ObjectOwnerModify;
                return returnMask;
            }

            // if not god or owner, do attachments as everyone
            if(task.ParentGroup.IsAttachment)
            {
                returnMask = ApplyObjectModifyMasks(grp.EffectiveEveryOnePerms, objflags, unlocked);
                if (taskOwnerID != UUID.Zero)
                    returnMask |= (uint)PrimFlags.ObjectAnyOwner;
                return returnMask;
            }

            UUID taskGroupID = task.GroupID;
            bool notGroupdOwned = taskOwnerID != taskGroupID;

            // if friends with rights then owner
            if (notGroupdOwned && IsFriendWithPerms(spID, taskOwnerID))
            {
                returnMask = ApplyObjectModifyMasks(grp.EffectiveOwnerPerms, objflags, unlocked);
                returnMask |= EXTRAOWNERMASK;
                if((returnMask & (uint)PrimFlags.ObjectModify) != 0)
                    returnMask |= (uint)PrimFlags.ObjectOwnerModify;
                return returnMask;
            }

            // group owned or shared ?
            IClientAPI client = sp.ControllingClient;
            ulong  powers = 0;
            if(taskGroupID != UUID.Zero && GroupMemberPowers(taskGroupID, sp, ref powers))
            {
                if(notGroupdOwned)
                {
                    // group sharing or everyone
                    returnMask = ApplyObjectModifyMasks(grp.EffectiveGroupOrEveryOnePerms, objflags, unlocked);
                    if (taskOwnerID != UUID.Zero)
                        returnMask |= (uint)PrimFlags.ObjectAnyOwner;
                    return returnMask;
                }

                // object is owned by group, check role powers
                if((powers & (ulong)GroupPowers.ObjectManipulate) == 0)
                {
                    // group sharing or everyone
                    returnMask = ApplyObjectModifyMasks(grp.EffectiveGroupOrEveryOnePerms, objflags, unlocked);
                    returnMask |=
                        (uint)PrimFlags.ObjectGroupOwned |
                        (uint)PrimFlags.ObjectAnyOwner;
                    return returnMask;
                }

                // we may have copy without transfer
                uint grpEffectiveOwnerPerms = grp.EffectiveOwnerPerms;
                if((grpEffectiveOwnerPerms & (uint)PermissionMask.Transfer) == 0)
                    grpEffectiveOwnerPerms &= ~(uint)PermissionMask.Copy;
                returnMask = ApplyObjectModifyMasks(grpEffectiveOwnerPerms, objflags, unlocked);
                returnMask |= 
                    (uint)PrimFlags.ObjectGroupOwned |
                    (uint)PrimFlags.ObjectYouOwner |
                    (uint)PrimFlags.ObjectAnyOwner;
                if ((returnMask & (uint)PrimFlags.ObjectModify) != 0)
                    returnMask |= (uint)PrimFlags.ObjectOwnerModify;
                return returnMask;
            }

            // fallback is everyone rights
            returnMask = ApplyObjectModifyMasks(grp.EffectiveEveryOnePerms, objflags, unlocked);
            if (taskOwnerID != UUID.Zero)
                returnMask |= (uint)PrimFlags.ObjectAnyOwner;
            return returnMask;
        }

        private uint ApplyObjectModifyMasks(uint setPermissionMask, uint objectFlagsMask, bool unlocked)
        {
            // We are adding the temporary objectflags to the object's objectflags based on the
            // permission flag given.  These change the F flags on the client.

            if ((setPermissionMask & (uint)PermissionMask.Copy) != 0)
            {
                objectFlagsMask |= (uint)PrimFlags.ObjectCopy;
            }

            if (unlocked)
            {
                if ((setPermissionMask & (uint)PermissionMask.Move) != 0)
                {
                    objectFlagsMask |= (uint)PrimFlags.ObjectMove;
                }

                if ((setPermissionMask & (uint)PermissionMask.Modify) != 0)
                {
                    objectFlagsMask |= (uint)PrimFlags.ObjectModify;
                }
            }

            if ((setPermissionMask & (uint)PermissionMask.Transfer) != 0)
            {
                objectFlagsMask |= (uint)PrimFlags.ObjectTransfer;
            }

            return objectFlagsMask;
        }

        // OARs still need this method that handles offline users
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

            // Admin should be able to edit anything in the sim (including admin objects)
            if (IsAdministrator(user))
                return PermissionClass.Owner;

            if(!obj.ParentGroup.IsAttachment)
            {
                if (IsFriendWithPerms(user, objectOwner) )
                    return PermissionClass.Owner;

                // Group permissions
                if (obj.GroupID != UUID.Zero && IsGroupMember(obj.GroupID, user, 0))
                    return PermissionClass.Group;
            }

            return PermissionClass.Everyone;
        }

        // get effective object permissions using user UUID. User rights will be fixed
        protected uint GetObjectPermissions(UUID currentUser, SceneObjectGroup group, bool denyOnLocked)
        {
            if (group == null)
                return 0;

            SceneObjectPart root = group.RootPart;
            if (root == null)
                return 0;

            UUID objectOwner = group.OwnerID;
            bool locked = denyOnLocked && ((root.OwnerMask & (uint)PermissionMask.Move) == 0);

            if (IsAdministrator(currentUser))
            {
                // do lock on admin owned objects
                if(locked && currentUser == objectOwner)
                    return (uint)(PermissionMask.AllEffective & ~(PermissionMask.Modify | PermissionMask.Move));
                return (uint)PermissionMask.AllEffective;
            }

            uint lockmask = (uint)PermissionMask.AllEffective;
            if(locked)
                lockmask &= ~(uint)(PermissionMask.Modify | PermissionMask.Move);
           
            if (currentUser == objectOwner)
                return group.EffectiveOwnerPerms & lockmask;
            
            if (group.IsAttachment)
                return 0;

            UUID sogGroupID = group.GroupID;
            bool notgroudOwned = sogGroupID != objectOwner;

            if (notgroudOwned && IsFriendWithPerms(currentUser, objectOwner))
                return group.EffectiveOwnerPerms  & lockmask;

            ulong powers = 0;
            if (sogGroupID != UUID.Zero && GroupMemberPowers(sogGroupID, currentUser, ref powers))
            {
                if(notgroudOwned)
                    return  group.EffectiveGroupOrEveryOnePerms & lockmask;

                if((powers & (ulong)GroupPowers.ObjectManipulate) == 0)
                    return  group.EffectiveGroupOrEveryOnePerms & lockmask;

                uint grpEffectiveOwnerPerms = group.EffectiveOwnerPerms & lockmask;
                if((grpEffectiveOwnerPerms & (uint)PermissionMask.Transfer) == 0)
                    grpEffectiveOwnerPerms &= ~(uint)PermissionMask.Copy;
                return grpEffectiveOwnerPerms;
            }

            return group.EffectiveEveryOnePerms & lockmask;
        }

        // get effective object permissions using present presence. So some may depend on requested rights (ie God)
        protected uint GetObjectPermissions(ScenePresence sp, SceneObjectGroup group, bool denyOnLocked)
        {
            if (sp == null || sp.IsDeleted || group == null || group.IsDeleted)
                return 0;

            SceneObjectPart root = group.RootPart;
            if (root == null)
                return 0;

            UUID spID = sp.UUID;
            UUID objectOwner = group.OwnerID;

            bool locked = denyOnLocked && ((root.OwnerMask & (uint)PermissionMask.Move) == 0);

            if (sp.IsGod)
            {
                if(locked && spID == objectOwner)
                    return (uint)(PermissionMask.AllEffective & ~(PermissionMask.Modify | PermissionMask.Move));
                return (uint)PermissionMask.AllEffective;
            }

            uint lockmask = (uint)PermissionMask.AllEffective;
            if(locked)
                lockmask &= ~(uint)(PermissionMask.Modify | PermissionMask.Move);
           
            if (spID == objectOwner)
                return group.EffectiveOwnerPerms & lockmask;
            
            if (group.IsAttachment)
                return 0;
          
            UUID sogGroupID = group.GroupID;
            bool notgroudOwned = sogGroupID != objectOwner;

            if (notgroudOwned && IsFriendWithPerms(spID, objectOwner))
                return group.EffectiveOwnerPerms  & lockmask;

            ulong powers = 0;
            if (sogGroupID != UUID.Zero && GroupMemberPowers(sogGroupID, sp, ref powers))
            {
                if(notgroudOwned)
                    return  group.EffectiveGroupOrEveryOnePerms & lockmask;

                if((powers & (ulong)GroupPowers.ObjectManipulate) == 0)
                    return  group.EffectiveGroupOrEveryOnePerms & lockmask;

                uint grpEffectiveOwnerPerms = group.EffectiveOwnerPerms & lockmask;
                if((grpEffectiveOwnerPerms & (uint)PermissionMask.Transfer) == 0)
                    grpEffectiveOwnerPerms &= ~(uint)PermissionMask.Copy;
                return grpEffectiveOwnerPerms;
            }

            return group.EffectiveEveryOnePerms & lockmask;
        }

        private uint GetObjectItemPermissions(UUID userID, TaskInventoryItem ti)
        {
            UUID tiOwnerID = ti.OwnerID;
            if(tiOwnerID == userID)
                return ti.CurrentPermissions;
            
            if(IsAdministrator(userID))
                return (uint)PermissionMask.AllEffective;
            // ??           
            if (IsFriendWithPerms(userID, tiOwnerID))
                return ti.CurrentPermissions;

            UUID tiGroupID = ti.GroupID;
            if(tiGroupID != UUID.Zero)
            {
                ulong powers = 0;
                if(GroupMemberPowers(tiGroupID, userID, ref powers))
                {
                    if(tiGroupID == ti.OwnerID)
                    {
                        if((powers & (ulong)GroupPowers.ObjectManipulate) != 0)
                            return ti.CurrentPermissions;
                    }
                    return ti.GroupPermissions;
                } 
            }

            return 0;
        }

        private uint GetObjectItemPermissions(ScenePresence sp, TaskInventoryItem ti, bool notEveryone)
        {
            UUID tiOwnerID = ti.OwnerID;
            UUID spID = sp.UUID;

            if(tiOwnerID == spID)
                return ti.CurrentPermissions;
 
            // ??           
            if (IsFriendWithPerms(spID, tiOwnerID))
                return ti.CurrentPermissions;

            UUID tiGroupID = ti.GroupID;
            if(tiGroupID != UUID.Zero)
            {
                ulong powers = 0;
                if(GroupMemberPowers(tiGroupID, spID, ref powers))
                {
                    if(tiGroupID == ti.OwnerID)
                    {
                        if((powers & (ulong)GroupPowers.ObjectManipulate) != 0)
                            return ti.CurrentPermissions;
                    }
                    uint p = ti.GroupPermissions;
                    if(!notEveryone)
                        p |= ti.EveryonePermissions;
                    return p;
                } 
            }

            if(notEveryone)
                return 0;

            return ti.EveryonePermissions;
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
            // Estate admins should be able to use estate tools
            if (IsEstateManager(user))
                return true;

            // Administrators always have permission
            if (IsAdministrator(user))
                return true;

            return false;
        }

        protected bool GenericParcelOwnerPermission(UUID user, ILandObject parcel, ulong groupPowers, bool allowEstateManager)
        {
            if (parcel.LandData.OwnerID == user)
                return true;

            if (parcel.LandData.IsGroupOwned && IsGroupMember(parcel.LandData.GroupID, user, groupPowers))
                return true;

            if (allowEstateManager && IsEstateManager(user))
                return true;

            if (IsAdministrator(user))
                return true;

            return false;
        }
#endregion

        #region Permission Checks
        private bool CanAbandonParcel(UUID user, ILandObject parcel)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericParcelOwnerPermission(user, parcel, (ulong)GroupPowers.LandRelease, false);
        }

        private bool CanReclaimParcel(UUID user, ILandObject parcel)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericParcelOwnerPermission(user, parcel, 0,true);
        }

        private bool CanDeedParcel(UUID user, ILandObject parcel)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if(parcel.LandData.GroupID == UUID.Zero)
                return false;

            if (IsAdministrator(user))
                return true;

            if (parcel.LandData.OwnerID != user) // Only the owner can deed!
                return false;

            ScenePresence sp = m_scene.GetScenePresence(user);
            if(sp == null)
                return false;

            IClientAPI client = sp.ControllingClient;
            if ((client.GetGroupPowers(parcel.LandData.GroupID) & (ulong)GroupPowers.LandDeed) == 0)
                return false;

            return true;
        }

        private bool CanDeedObject(ScenePresence sp, SceneObjectGroup sog, UUID targetGroupID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if(sog == null || sog.IsDeleted || sp == null || sp.IsDeleted || targetGroupID == UUID.Zero)
                return false;

            // object has group already?
            if(sog.GroupID != targetGroupID)
                return false;

            // is effectivelly shared?            
            if(sog.EffectiveGroupPerms == 0)
                return false;

            if(sp.IsGod)
                return true;

            // owned by requester?
            if(sog.OwnerID != sp.UUID)
                return false;

            // owner can transfer?
            if((sog.EffectiveOwnerPerms & (uint)PermissionMask.Transfer) == 0)
                return false;
            
            // group member ? 
            ulong powers = 0;
            if(!GroupMemberPowers(targetGroupID, sp, ref powers))
                return false;

            // has group rights?
            if ((powers & (ulong)GroupPowers.DeedObject) == 0)
                return false;

            return true;
        }

        private bool CanDuplicateObject(SceneObjectGroup sog, ScenePresence sp)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (sog == null || sog.IsDeleted || sp == null || sp.IsDeleted)
                return false;

            uint perms = GetObjectPermissions(sp, sog, false);
            if((perms & (uint)PermissionMask.Copy) == 0)
                return false;

            if(sog.OwnerID != sp.UUID && (perms & (uint)PermissionMask.Transfer) == 0)
                return false;

            //If they can rez, they can duplicate
            return CanRezObject(0, sp.UUID, sog.AbsolutePosition);
        }

        private bool CanDeleteObject(SceneObjectGroup sog, ScenePresence sp)
        {
            // ignoring locked. viewers should warn and ask for confirmation

            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (sog == null || sog.IsDeleted || sp == null || sp.IsDeleted)
                return false;

            if(sog.IsAttachment)
                return false;

            UUID sogOwnerID = sog.OwnerID;
            UUID spID = sp.UUID;

            if(sogOwnerID == spID)
                return true;

            if (sp.IsGod)
                return true;

            if (IsFriendWithPerms(sog.UUID, sogOwnerID))
                return true;

            UUID sogGroupID = sog.GroupID;
            if (sogGroupID != UUID.Zero)
            {
                ulong powers = 0;
                if(GroupMemberPowers(sogGroupID, sp, ref powers))
                {
                    if(sogGroupID == sogOwnerID)
                    {
                        if((powers & (ulong)GroupPowers.ObjectManipulate) != 0)
                            return true;
                    }
                    return  (sog.EffectiveGroupPerms & (uint)PermissionMask.Modify) != 0;
                } 
            }
            return false;
        }

        private bool CanDeleteObjectByIDs(UUID objectID, UUID userID)
        {
            // ignoring locked. viewers should warn and ask for confirmation

            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            SceneObjectGroup sog = m_scene.GetGroupByPrim(objectID);
            if (sog == null)
                return false;

            if(sog.IsAttachment)
                return false;

            UUID sogOwnerID = sog.OwnerID;

            if(sogOwnerID == userID)
                return true;

            if (IsAdministrator(userID))
                return true;

            if (IsFriendWithPerms(objectID, sogOwnerID))
                return true;

            UUID sogGroupID = sog.GroupID;
            if (sogGroupID != UUID.Zero)
            {
                ulong powers = 0;
                if(GroupMemberPowers(sogGroupID, userID, ref powers))
                {
                    if(sogGroupID == sogOwnerID)
                    {
                        if((powers & (ulong)GroupPowers.ObjectManipulate) != 0)
                            return true;
                    }
                    return  (sog.EffectiveGroupPerms & (uint)PermissionMask.Modify) != 0;
                } 
            }
            return false;
        }

        private bool CanEditObjectByIDs(UUID objectID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            SceneObjectGroup sog = m_scene.GetGroupByPrim(objectID);
            if (sog == null)
                return false;

            uint perms = GetObjectPermissions(userID, sog, true);
            if((perms & (uint)PermissionMask.Modify) == 0)
                return false;
            return true;
        }

        private bool CanEditObject(SceneObjectGroup sog, ScenePresence sp)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if(sog == null || sog.IsDeleted || sp == null || sp.IsDeleted)
                return false;

            uint perms = GetObjectPermissions(sp, sog, true);
            if((perms & (uint)PermissionMask.Modify) == 0)
                return false;
            return true;
        }

        private bool CanEditObjectPerms(SceneObjectGroup sog, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (sog == null)
                return false;

            if(sog.OwnerID == userID || IsAdministrator(userID))
                return true;

            UUID sogGroupID = sog.GroupID;
            if(sogGroupID == UUID.Zero || sogGroupID != sog.OwnerID)
                return false;

            uint perms = sog.EffectiveOwnerPerms;
            if((perms & (uint)PermissionMask.Modify) == 0)
                return false;

            ulong powers = 0;
            if(GroupMemberPowers(sogGroupID, userID, ref powers))
            {
                if((powers & (ulong)GroupPowers.ObjectManipulate) != 0)
                    return true;
            }

            return false;
        }

        private bool CanEditObjectInventory(UUID objectID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            SceneObjectGroup sog = m_scene.GetGroupByPrim(objectID);
            if (sog == null)
                return false;

            uint perms = GetObjectPermissions(userID, sog, true);
            if((perms & (uint)PermissionMask.Modify) == 0)
                return false;
            return true;
        }

        private bool CanEditParcelProperties(UUID userID, ILandObject parcel, GroupPowers p, bool allowManager)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericParcelOwnerPermission(userID, parcel, (ulong)p, false);
        }

        /// <summary>
        /// Check whether the specified user can edit the given script
        /// </summary>
        /// <param name="script"></param>
        /// <param name="objectID"></param>
        /// <param name="user"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        private bool CanEditScript(UUID script, UUID objectID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (m_allowedScriptEditors == UserSet.Administrators && !IsAdministrator(userID))
                return false;

            // Ordinarily, if you can view it, you can edit it
            // There is no viewing a no mod script
            //
            return CanViewScript(script, objectID, userID);
        }

        /// <summary>
        /// Check whether the specified user can edit the given notecard
        /// </summary>
        /// <param name="notecard"></param>
        /// <param name="objectID"></param>
        /// <param name="user"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        private bool CanEditNotecard(UUID notecard, UUID objectID, UUID user)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (objectID == UUID.Zero) // User inventory
            {
                IInventoryService invService = m_scene.InventoryService;
                InventoryItemBase assetRequestItem = invService.GetItem(user, notecard);
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
                SceneObjectPart part = m_scene.GetSceneObjectPart(objectID);
                if (part == null)
                    return false;

                SceneObjectGroup sog = part.ParentGroup;
                if (sog == null)
                    return false;

                // check object mod right
                uint perms = GetObjectPermissions(user, sog, true);
                if((perms & (uint)PermissionMask.Modify) == 0)
                    return false;

                TaskInventoryItem ti = part.Inventory.GetInventoryItem(notecard);
                if (ti == null)
                    return false;
               
                if (ti.OwnerID != user)
                {
                    UUID tiGroupID = ti.GroupID;
                    if (tiGroupID == UUID.Zero)
                        return false;

                    ulong powers = 0;
                    if(!GroupMemberPowers(tiGroupID, user, ref powers))
                        return false;

                    if(tiGroupID == ti.OwnerID && (powers & (ulong)GroupPowers.ObjectManipulate) != 0)
                    {
                        if ((ti.CurrentPermissions & ((uint)PermissionMask.Modify | (uint)PermissionMask.Copy)) ==
                        ((uint)PermissionMask.Modify | (uint)PermissionMask.Copy))
                            return true;
                    }
                    if ((ti.GroupPermissions & ((uint)PermissionMask.Modify | (uint)PermissionMask.Copy)) ==
                        ((uint)PermissionMask.Modify | (uint)PermissionMask.Copy))
                        return true;
                    return false;
                }

                // Require full perms
                if ((ti.CurrentPermissions & ((uint)PermissionMask.Modify | (uint)PermissionMask.Copy)) !=
                        ((uint)PermissionMask.Modify | (uint)PermissionMask.Copy))
                    return false;
            }
            return true;
        }

        private bool CanInstantMessage(UUID user, UUID target)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            // If the sender is an object, check owner instead
            //
            SceneObjectPart part = m_scene.GetSceneObjectPart(user);
            if (part != null)
                user = part.OwnerID;

            return GenericCommunicationPermission(user, target);
        }

        private bool CanInventoryTransfer(UUID user, UUID target)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericCommunicationPermission(user, target);
        }

        private bool CanIssueEstateCommand(UUID user, bool ownerCommand)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (IsAdministrator(user))
                return true;

            if (ownerCommand)
                return m_scene.RegionInfo.EstateSettings.IsEstateOwner(user);

            return IsEstateManager(user);
        }

        private bool CanMoveObject(SceneObjectGroup sog, ScenePresence sp)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);

            if(sog == null || sog.IsDeleted || sp == null || sp.IsDeleted)
                return false;

            if (m_bypassPermissions)
            {
                if (sog.OwnerID != sp.UUID && sog.IsAttachment)
                    return false;
                return m_bypassPermissionsValue;
            }

            uint perms = GetObjectPermissions(sp, sog, true);
            if((perms & (uint)PermissionMask.Move) == 0)
                return false;
            return true;
        }

        private bool CanObjectEntry(SceneObjectGroup sog, bool enteringRegion, Vector3 newPoint)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);

            float newX = newPoint.X;
            float newY = newPoint.Y;

            // allow outside region this is needed for crossings
            if (newX < -1f || newX > (m_scene.RegionInfo.RegionSizeX + 1.0f) ||
                newY < -1f || newY > (m_scene.RegionInfo.RegionSizeY + 1.0f) )
                return true;

            if(sog == null || sog.IsDeleted)
                return false;

            if (m_bypassPermissions)
                return m_bypassPermissionsValue;

            ILandObject parcel = m_scene.LandChannel.GetLandObject(newX, newY);
            if (parcel == null)
                return false;

            if ((parcel.LandData.Flags & ((int)ParcelFlags.AllowAPrimitiveEntry)) != 0)
                return true;

            if (!enteringRegion)
            {
                Vector3 oldPoint = sog.AbsolutePosition;
                ILandObject fromparcel = m_scene.LandChannel.GetLandObject(oldPoint.X, oldPoint.Y);
                if (fromparcel != null && fromparcel.Equals(parcel)) // it already entered parcel ????
                    return true;
            }

            UUID userID = sog.OwnerID;
            LandData landdata = parcel.LandData;

            if (landdata.OwnerID == userID)
                return true;

            if (IsAdministrator(userID))
                return true;

            UUID landGroupID = landdata.GroupID;
            if (landGroupID != UUID.Zero)
            {
                if ((parcel.LandData.Flags & ((int)ParcelFlags.AllowGroupObjectEntry)) != 0)
                    return IsGroupMember(landGroupID, userID, 0);

                 if (landdata.IsGroupOwned && IsGroupMember(landGroupID, userID, (ulong)GroupPowers.AllowRez))
                    return true;
            }

            //Otherwise, false!
            return false;
        }

        private bool OnObjectEnterWithScripts(SceneObjectGroup sog, ILandObject parcel)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);

            if(sog == null || sog.IsDeleted)
                return false;

            if (m_bypassPermissions)
                return m_bypassPermissionsValue;

            if (parcel == null)
                return true;

            int checkflags = ((int)ParcelFlags.AllowAPrimitiveEntry);
            bool scripts = (sog.ScriptCount() > 0);
            if(scripts)
                checkflags |= ((int)ParcelFlags.AllowOtherScripts);

            if ((parcel.LandData.Flags & checkflags) == checkflags)
                return true;

            UUID userID = sog.OwnerID;
            LandData landdata = parcel.LandData;

            if (landdata.OwnerID == userID)
                return true;

            if (IsAdministrator(userID))
                return true;

            UUID landGroupID = landdata.GroupID;
            if (landGroupID != UUID.Zero)
            {
                checkflags = (int)ParcelFlags.AllowGroupObjectEntry;
                if(scripts)
                    checkflags |= ((int)ParcelFlags.AllowGroupScripts);

                if ((parcel.LandData.Flags & checkflags) == checkflags)
                    return IsGroupMember(landGroupID, userID, 0);

                 if (landdata.IsGroupOwned && IsGroupMember(landGroupID, userID, (ulong)GroupPowers.AllowRez))
                    return true;
            }

            //Otherwise, false!
            return false;
        }

        private bool CanReturnObjects(ILandObject land, ScenePresence sp, List<SceneObjectGroup> objects)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if(sp == null)
                return true;  // assuming that in this case rights are as owner

            UUID userID = sp.UUID;
            bool isPrivUser = sp.IsGod || IsEstateManager(userID);

            IClientAPI client = sp.ControllingClient;

            ulong powers = 0;
            ILandObject l;

            foreach (SceneObjectGroup g in new List<SceneObjectGroup>(objects))
            {
                if(g.IsAttachment)
                {
                    objects.Remove(g);
                    continue;
                }

                if (isPrivUser || g.OwnerID == userID)
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
                    l = m_scene.LandChannel.GetLandObject(pos.X, pos.Y);
                }

                // If it's not over any land, then we can't do a thing
                if (l == null || l.LandData == null)
                {
                    objects.Remove(g);
                    continue;
                }

                LandData ldata = l.LandData;
                // If we own the land outright, then allow
                //
                if (ldata.OwnerID == userID)
                    continue;

                // Group voodoo
                //
                if (ldata.IsGroupOwned)
                {
                    UUID lGroupID = ldata.GroupID;
                    // Not a group member, or no rights at all
                    //
                    powers = client.GetGroupPowers(lGroupID);
                    if(powers == 0)
                    {
                        objects.Remove(g);
                        continue;
                    }
 
                    // Group deeded object?
                    //
                    if (g.OwnerID == lGroupID &&
                        (powers & (ulong)GroupPowers.ReturnGroupOwned) == 0)
                    {
                        objects.Remove(g);
                        continue;
                    }

                    // Group set object?
                    //
                    if (g.GroupID == lGroupID &&
                        (powers & (ulong)GroupPowers.ReturnGroupSet) == 0)
                    {
                        objects.Remove(g);
                        continue;
                    }

                    if ((powers & (ulong)GroupPowers.ReturnNonGroup) == 0)
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

        private bool CanRezObject(int objectCount, UUID userID, Vector3 objectPosition)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions)
                return m_bypassPermissionsValue;

//            m_log.DebugFormat("[PERMISSIONS MODULE]: Checking rez object at {0} in {1}", objectPosition, m_scene.Name);

            ILandObject parcel = m_scene.LandChannel.GetLandObject(objectPosition.X, objectPosition.Y);
            if (parcel == null || parcel.LandData == null)
                return false;

            LandData landdata = parcel.LandData;
            if ((userID == landdata.OwnerID))
                return true;

            if ((landdata.Flags & (uint)ParcelFlags.CreateObjects) != 0)
                return true;

            if(IsAdministrator(userID))
                return true;

            if(landdata.GroupID != UUID.Zero)
            {
                if ((landdata.Flags & (uint)ParcelFlags.CreateGroupObjects) != 0)
                    return IsGroupMember(landdata.GroupID, userID, 0);

                if (landdata.IsGroupOwned && IsGroupMember(landdata.GroupID, userID, (ulong)GroupPowers.AllowRez))
                    return true;
            }

            return false;
        }

        private bool CanRunConsoleCommand(UUID user)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;


            return IsAdministrator(user);
        }

        private bool CanRunScript(TaskInventoryItem scriptitem, SceneObjectPart part)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if(scriptitem == null || part == null)
                return false;

            SceneObjectGroup sog = part.ParentGroup;
            if(sog == null)
                return false;

            Vector3 pos = sog.AbsolutePosition;
            ILandObject parcel = m_scene.LandChannel.GetLandObject(pos.X, pos.Y);
            if (parcel == null)
                return false;

            LandData ldata = parcel.LandData;
            if(ldata == null)
                return false;

            uint lflags = ldata.Flags;
 
            if ((lflags & (uint)ParcelFlags.AllowOtherScripts) != 0)
               return true;

            if ((part.OwnerID == ldata.OwnerID))
                return true;

            if (((lflags & (uint)ParcelFlags.AllowGroupScripts) != 0)
                    && (ldata.GroupID != UUID.Zero) && (ldata.GroupID == part.GroupID))
                return true;
            
            return GenericEstatePermission(part.OwnerID);
        }

        private bool CanSellParcel(UUID user, ILandObject parcel)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return GenericParcelOwnerPermission(user, parcel, (ulong)GroupPowers.LandSetSale, true);
        }

        private bool CanSellGroupObject(UUID userID, UUID groupID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return IsGroupMember(groupID, userID, (ulong)GroupPowers.ObjectSetForSale);
        }

        private bool CanSellObjectByUserID(SceneObjectGroup sog, UUID userID, byte saleType)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (sog == null || sog.IsDeleted || userID == UUID.Zero)
                return false;

            // sell is not a attachment op
            if(sog.IsAttachment)
                return false;

            if(IsAdministrator(userID))
                return true;

            uint sogEffectiveOwnerPerms = sog.EffectiveOwnerPerms;
            if((sogEffectiveOwnerPerms & (uint)PermissionMask.Transfer) == 0)
                return false;

            if(saleType == (byte)SaleType.Copy &&
                    (sogEffectiveOwnerPerms & (uint)PermissionMask.Copy) == 0)
                return false;

            UUID sogOwnerID = sog.OwnerID;

            if(sogOwnerID == userID)
                return true;

            // else only group owned can be sold by members with powers
            UUID sogGroupID = sog.GroupID;
            if(sog.OwnerID != sogGroupID || sogGroupID == UUID.Zero)
                return false;

            return IsGroupMember(sogGroupID, userID, (ulong)GroupPowers.ObjectSetForSale);
        }

        private bool CanSellObject(SceneObjectGroup sog, ScenePresence sp, byte saleType)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (sog == null || sog.IsDeleted || sp == null || sp.IsDeleted)
                return false;

            // sell is not a attachment op
            if(sog.IsAttachment)
                return false;

            if(sp.IsGod)
                return true;

            uint sogEffectiveOwnerPerms = sog.EffectiveOwnerPerms;
            if((sogEffectiveOwnerPerms & (uint)PermissionMask.Transfer) == 0)
                return false;

            if(saleType == (byte)SaleType.Copy &&
                    (sogEffectiveOwnerPerms & (uint)PermissionMask.Copy) == 0)
                return false;

            UUID userID = sp.UUID;
            UUID sogOwnerID = sog.OwnerID;

            if(sogOwnerID == userID)
                return true;

            // else only group owned can be sold by members with powers
            UUID sogGroupID = sog.GroupID;
            if(sog.OwnerID != sogGroupID || sogGroupID == UUID.Zero)
                return false;

            ulong powers = 0;
            if(!GroupMemberPowers(sogGroupID, sp, ref powers))
                return false;

            if((powers & (ulong)GroupPowers.ObjectSetForSale) == 0)
                return false;

            return true;
        }

        private bool CanTakeObject(SceneObjectGroup sog, ScenePresence sp)
        {
            // ignore locked, viewers shell ask for confirmation
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (sog == null || sog.IsDeleted || sp == null || sp.IsDeleted)
                return false;

            // take is not a attachment op
            if(sog.IsAttachment)
                return false;

            UUID sogOwnerID = sog.OwnerID;
            UUID spID = sp.UUID;

            if(sogOwnerID == spID)
                return true;

            if (sp.IsGod)
                return true;

            if((sog.EffectiveOwnerPerms & (uint)PermissionMask.Transfer) == 0)
                return false;
 
            if (IsFriendWithPerms(sog.UUID, sogOwnerID))
                return true;

            UUID sogGroupID = sog.GroupID;
            if (sogGroupID != UUID.Zero)
            {
                ulong powers = 0;
                if(GroupMemberPowers(sogGroupID, sp, ref powers))
                {
                    if(sogGroupID == sogOwnerID)
                    {
                        if((powers & (ulong)GroupPowers.ObjectManipulate) != 0)
                            return true;
                    }
                    return (sog.EffectiveGroupPerms & (uint)PermissionMask.Modify) != 0;
                } 
            }
            return false;
        }

        private bool CanTakeCopyObject(SceneObjectGroup sog, ScenePresence sp)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            if (sog == null || sog.IsDeleted || sp == null || sp.IsDeleted)
                return false;

            // refuse on attachments
            if(sog.IsAttachment && !sp.IsGod)
                return false;

            uint perms = GetObjectPermissions(sp, sog, true);
            if((perms & (uint)PermissionMask.Copy) == 0)
            {
                sp.ControllingClient.SendAgentAlertMessage("Copying this item has been denied by the permissions system", false);
                return false;
            }

            if(sog.OwnerID != sp.UUID && (perms & (uint)PermissionMask.Transfer) == 0)
                 return false;
            return true;
        }

        private bool CanTerraformLand(UUID userID, Vector3 position)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            // Estate override
            if (GenericEstatePermission(userID))
                return true;

            float X = position.X;
            float Y = position.Y;
            int id = (int)position.Z;
            ILandObject parcel;

            if(id >= 0 && X < 0 && Y < 0)
                parcel = m_scene.LandChannel.GetLandObject(id);
            else
            {
                if (X < 0)
                    X = 0;
                else if (X > ((int)m_scene.RegionInfo.RegionSizeX - 1))
                    X = ((int)m_scene.RegionInfo.RegionSizeX - 1);
                if (Y < 0)
                    Y = 0;
                else if (Y > ((int)m_scene.RegionInfo.RegionSizeY - 1))
                    Y = ((int)m_scene.RegionInfo.RegionSizeY - 1);

                parcel = m_scene.LandChannel.GetLandObject(X, Y);
            }

            if (parcel == null)
                return false;

            LandData landdata = parcel.LandData;
            if (landdata == null)
                return false;
            
            if ((landdata.Flags & ((int)ParcelFlags.AllowTerraform)) != 0)
                return true;

            if(landdata.OwnerID == userID)
                return true;
            
            if (landdata.IsGroupOwned && parcel.LandData.GroupID != UUID.Zero &&  
                    IsGroupMember(landdata.GroupID, userID, (ulong)GroupPowers.AllowEditLand))
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
        private bool CanViewScript(UUID script, UUID objectID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            // A god is a god is a god
            if (IsAdministrator(userID))
                return true;

            if (objectID == UUID.Zero) // User inventory
            {
                IInventoryService invService = m_scene.InventoryService;
                InventoryItemBase assetRequestItem = invService.GetItem(userID, script);
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
/*
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy |
                        (uint)PermissionMask.Transfer)) !=
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy |
                        (uint)PermissionMask.Transfer))
*/
                        (uint)(PermissionMask.Modify | PermissionMask.Copy)) !=
                        (uint)(PermissionMask.Modify | PermissionMask.Copy))
                    return false;
            }
            else // Prim inventory
            {
                SceneObjectPart part = m_scene.GetSceneObjectPart(objectID);
                if (part == null)
                    return false;

                SceneObjectGroup sog = part.ParentGroup;
                if (sog == null)
                    return false;

                uint perms = GetObjectPermissions(userID, sog, true);
                if((perms & (uint)PermissionMask.Modify) == 0)
                    return false;

                TaskInventoryItem ti = part.Inventory.GetInventoryItem(script);

//                if (ti == null || ti.InvType != (int)InventoryType.LSL)
                if (ti == null) // legacy may not have type
                    return false;

                uint itperms = GetObjectItemPermissions(userID, ti);

                // Require full perms

                if ((itperms &
/*
                        ((uint)(PermissionMask.Modify |
                        (uint)PermissionMask.Copy |
                        (uint)PermissionMask.Transfer)) !=
                        ((uint)PermissionMask.Modify |
                        (uint)PermissionMask.Copy |
                        (uint)PermissionMask.Transfer))
*/
                        (uint)(PermissionMask.Modify | PermissionMask.Copy)) !=
                        (uint)(PermissionMask.Modify | PermissionMask.Copy))
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
        private bool CanViewNotecard(UUID notecard, UUID objectID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            // A god is a god is a god
            if (IsAdministrator(userID))
                return true;

            if (objectID == UUID.Zero) // User inventory
            {
                IInventoryService invService = m_scene.InventoryService;
                InventoryItemBase assetRequestItem = invService.GetItem(userID, notecard);
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
                SceneObjectPart part = m_scene.GetSceneObjectPart(objectID);
                if (part == null)
                    return false;

                SceneObjectGroup sog = part.ParentGroup;
                if (sog == null)
                    return false;

                uint perms = GetObjectPermissions(userID, sog, true);
                if((perms & (uint)PermissionMask.Modify) == 0)
                    return false;

                TaskInventoryItem ti = part.Inventory.GetInventoryItem(notecard);

//                if (ti == null || ti.InvType != (int)InventoryType.Notecard)
                if (ti == null)
                    return false;

                uint itperms = GetObjectItemPermissions(userID, ti);

                // Notecards are always readable unless no copy
                //
                if ((itperms &
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

            SceneObjectGroup sog = m_scene.GetGroupByPrim(objectID);
            if (sog == null)
                return false;

            uint perms = GetObjectPermissions(userID, sog, true);
            if((perms & (uint)PermissionMask.Modify) == 0)
                return false;
            return true;
        }

        private bool CanDelinkObject(UUID userID, UUID objectID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            SceneObjectGroup sog = m_scene.GetGroupByPrim(objectID);
            if (sog == null)
                return false;

            uint perms = GetObjectPermissions(userID, sog, true);
            if((perms & (uint)PermissionMask.Modify) == 0)
                return false;
            return true;
        }

        private bool CanBuyLand(UUID userID, ILandObject parcel)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            return true;
        }

        private bool CanCopyObjectInventory(UUID itemID, UUID objectID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            SceneObjectPart part = m_scene.GetSceneObjectPart(objectID);
            if (part == null)
                return false;

            SceneObjectGroup sog = part.ParentGroup;
            if (sog == null)
                return false;

            if(sog.OwnerID == userID || IsAdministrator(userID))
                return true;
 
            if(sog.IsAttachment)
                return false;

            UUID sogGroupID = sog.GroupID;

            if(sogGroupID == UUID.Zero || sogGroupID != sog.OwnerID)
                return false;

            TaskInventoryItem ti = part.Inventory.GetInventoryItem(itemID);
            if(ti == null)
                return false;

            ulong powers = 0;
            if(GroupMemberPowers(sogGroupID, userID, ref powers))
            {
                if((powers & (ulong)GroupPowers.ObjectManipulate) != 0)
                    return true;

                if((ti.EveryonePermissions & (uint)PermissionMask.Copy) != 0)
                        return true;
            }
            return false;
        }

        // object inventory to object inventory item drag and drop
        private bool CanDoObjectInvToObjectInv(TaskInventoryItem item, SceneObjectPart sourcePart, SceneObjectPart destPart)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);

            if (sourcePart == null || destPart == null || item == null)
                return false;

            if (m_bypassPermissions)
                return m_bypassPermissionsValue;

            SceneObjectGroup srcsog = sourcePart.ParentGroup;
            SceneObjectGroup destsog = destPart.ParentGroup;
            if (srcsog == null || destsog == null)
                return false;

            // dest is locked
            if((destsog.EffectiveOwnerPerms & (uint)PermissionMask.Move) == 0)
                return false;

            uint itperms = item.CurrentPermissions;

            // if item is no copy the source is modifed
            if((itperms & (uint)PermissionMask.Copy) == 0 && (srcsog.EffectiveOwnerPerms & (uint)PermissionMask.Modify) == 0)
                return false;

            UUID srcOwner = srcsog.OwnerID;
            UUID destOwner = destsog.OwnerID;
            bool notSameOwner = srcOwner != destOwner;

            if(notSameOwner)
            {
                if((itperms & (uint)PermissionMask.Transfer) == 0)
                    return false;

                // scripts can't be droped
                if(item.InvType == (int)InventoryType.LSL)
                    return false;

                if((destsog.RootPart.GetEffectiveObjectFlags() & (uint)PrimFlags.AllowInventoryDrop) == 0)
                    return false;
            }
            else
            {
                if((destsog.RootPart.GetEffectiveObjectFlags() & (uint)PrimFlags.AllowInventoryDrop) == 0 &&
                            (destsog.EffectiveOwnerPerms & (uint)PermissionMask.Modify) == 0)
                    return false;
            }

            return true;
        }

        private bool CanDropInObjectInv(InventoryItemBase item, ScenePresence sp, SceneObjectPart destPart)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);

            if (sp == null || sp.IsDeleted || destPart == null || item == null)
                return false;

            SceneObjectGroup destsog = destPart.ParentGroup;
            if (destsog == null || destsog.IsDeleted)
                return false;

            if (m_bypassPermissions)
                return m_bypassPermissionsValue;

            if(sp.IsGod)
                return true;

            // dest is locked
            if((destsog.EffectiveOwnerPerms & (uint)PermissionMask.Move) == 0)
                return false;

            UUID destOwner = destsog.OwnerID;
            UUID spID = sp.UUID;
            bool spNotOwner = spID != destOwner;

            // scripts can't be droped
            if(spNotOwner && item.InvType == (int)InventoryType.LSL)
                return false;

            if(spNotOwner || item.Owner != destOwner)
            {
                // no copy item will be moved if it has transfer
                uint itperms = item.CurrentPermissions;
                if((itperms & (uint)PermissionMask.Transfer) == 0)
                    return false;
            }

            // allowdrop is a root part thing and does bypass modify rights
            if((destsog.RootPart.GetEffectiveObjectFlags() & (uint)PrimFlags.AllowInventoryDrop) != 0)
                return true;

            uint perms = GetObjectPermissions(spID, destsog, true);
            if((perms & (uint)PermissionMask.Modify) == 0)
                return false;

            return true;
        }

        private bool CanDeleteObjectInventory(UUID itemID, UUID objectID, UUID userID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            SceneObjectPart part = m_scene.GetSceneObjectPart(objectID);
            if (part == null)
                return false;

            SceneObjectGroup sog = part.ParentGroup;
            if (sog == null)
                return false;

            uint perms = GetObjectPermissions(userID, sog, true);
            if((perms & (uint)PermissionMask.Modify) == 0)
                return false;

            TaskInventoryItem ti = part.Inventory.GetInventoryItem(itemID);
            if(ti == null)
                return false;

            //TODO item perm ?
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

            ScenePresence p = m_scene.GetScenePresence(userID);

            if (p == null)
                return false;

            SceneObjectGroup sog = m_scene.GetGroupByPrim(objectID);
            if (sog == null)
                return false;

            uint perms = GetObjectPermissions(userID, sog, true);
            if((perms & (uint)PermissionMask.Modify) == 0)
                return false;

            if ((int)InventoryType.LSL == invType)
            {
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

        private bool CanResetScript(UUID primID, UUID script, UUID agentID)
        {
            DebugPermissionInformation(MethodInfo.GetCurrentMethod().Name);
            if (m_bypassPermissions) return m_bypassPermissionsValue;

            SceneObjectGroup sog = m_scene.GetGroupByPrim(primID);
            if (sog == null)
                return false;

            uint perms = GetObjectPermissions(agentID, sog, false);
            if((perms & (uint)PermissionMask.Modify) == 0) // ??
                return false;
            return true;
        }

        private bool CanCompileScript(UUID ownerUUID, int scriptType)
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

            SceneObjectGroup sog = part.ParentGroup;
            if (sog == null)
                return false;

            uint perms = GetObjectPermissions(agentID, sog, false);
            if((perms & (uint)PermissionMask.Modify) == 0)
                return false;
            return true;
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
