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
using System.IO;
using System.Reflection;
using log4net;
using NDesk.Options;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Console;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Mono.Addins;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Archiver
{
    /// <summary>
    /// This module loads and saves OpenSimulator inventory archives
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "InventoryArchiverModule")]
    public class InventoryArchiverModule : ISharedRegionModule, IInventoryArchiverModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <value>
        /// Enable or disable checking whether the iar user is actually logged in
        /// </value>
//        public bool DisablePresenceChecks { get; set; }

        public event InventoryArchiveSaved OnInventoryArchiveSaved;
        public event InventoryArchiveLoaded OnInventoryArchiveLoaded;

        /// <summary>
        /// The file to load and save inventory if no filename has been specified
        /// </summary>
        protected const string DEFAULT_INV_BACKUP_FILENAME = "user-inventory.iar";

        /// <value>
        /// Pending save and load completions initiated from the console
        /// </value>
        protected List<UUID> m_pendingConsoleTasks = new List<UUID>();

        /// <value>
        /// All scenes that this module knows about
        /// </value>
        private Dictionary<UUID, Scene> m_scenes = new Dictionary<UUID, Scene>();
        private Scene m_aScene;

        private IUserAccountService m_UserAccountService;
        protected IUserAccountService UserAccountService
        {
            get
            {
                if (m_UserAccountService == null)
                    // What a strange thing to do...
                    foreach (Scene s in m_scenes.Values)
                    {
                        m_UserAccountService = s.RequestModuleInterface<IUserAccountService>();
                        break;
                    }

                return m_UserAccountService;
            }
        }


        public InventoryArchiverModule() {}

//        public InventoryArchiverModule(bool disablePresenceChecks)
//        {
//            DisablePresenceChecks = disablePresenceChecks;
        //        }

        #region ISharedRegionModule

        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(Scene scene)
        {
            if (m_scenes.Count == 0)
            {
                scene.RegisterModuleInterface<IInventoryArchiverModule>(this);
                OnInventoryArchiveSaved += SaveInvConsoleCommandCompleted;
                OnInventoryArchiveLoaded += LoadInvConsoleCommandCompleted;

                scene.AddCommand(
                    "Archiving", this, "load iar",
                    "load iar [-m|--merge] <first> <last> <inventory path> <password> [<IAR path>]",
                    "Load user inventory archive (IAR).",
                    "-m|--merge is an option which merges the loaded IAR with existing inventory folders where possible, rather than always creating new ones"
                    + "<first> is user's first name." + Environment.NewLine
                    + "<last> is user's last name." + Environment.NewLine
                    + "<inventory path> is the path inside the user's inventory where the IAR should be loaded." + Environment.NewLine
                    + "<password> is the user's password." + Environment.NewLine
                    + "<IAR path> is the filesystem path or URI from which to load the IAR."
                    + string.Format("  If this is not given then the filename {0} in the current directory is used", DEFAULT_INV_BACKUP_FILENAME),
                    HandleLoadInvConsoleCommand);

                scene.AddCommand(
                    "Archiving", this, "save iar",
                    "save iar [-h|--home=<url>] [--noassets] <first> <last> <inventory path> <password> [<IAR path>] [-c|--creators] [-e|--exclude=<name/uuid>] [-f|--excludefolder=<foldername/uuid>] [-v|--verbose]",
                    "Save user inventory archive (IAR).",
                    "<first> is the user's first name.\n"
                    + "<last> is the user's last name.\n"
                    + "<inventory path> is the path inside the user's inventory for the folder/item to be saved.\n"
                    + "<IAR path> is the filesystem path at which to save the IAR."
                    + string.Format("  If this is not given then the filename {0} in the current directory is used.\n", DEFAULT_INV_BACKUP_FILENAME)
                    + "-h|--home=<url> adds the url of the profile service to the saved user information.\n"
                    + "-c|--creators preserves information about foreign creators.\n"
                    + "-e|--exclude=<name/uuid> don't save the inventory item in archive" + Environment.NewLine
                    + "-f|--excludefolder=<folder/uuid> don't save contents of the folder in archive" + Environment.NewLine
                    + "-v|--verbose extra debug messages.\n"
                    + "--noassets stops assets being saved to the IAR."
                    + "--perm=<permissions> stops items with insufficient permissions from being saved to the IAR.\n"
                    + "   <permissions> can contain one or more of these characters: \"C\" = Copy, \"T\" = Transfer, \"M\" = Modify.\n",
                    HandleSaveInvConsoleCommand);

                m_aScene = scene;
            }

            m_scenes[scene.RegionInfo.RegionID] = scene;
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void Close() {}

        public void RegionLoaded(Scene scene)
        {
        }

        public void PostInitialise()
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name { get { return "Inventory Archiver Module"; } }

        #endregion 

        /// <summary>
        /// Trigger the inventory archive saved event.
        /// </summary>
        protected internal void TriggerInventoryArchiveSaved(
            UUID id, bool succeeded, UserAccount userInfo, string invPath, Stream saveStream,
            Exception reportedException, int SaveCount, int FilterCount)
        {
            InventoryArchiveSaved handlerInventoryArchiveSaved = OnInventoryArchiveSaved;
            if (handlerInventoryArchiveSaved != null)
                handlerInventoryArchiveSaved(id, succeeded, userInfo, invPath, saveStream, reportedException, SaveCount , FilterCount);
        }

        /// <summary>
        /// Trigger the inventory archive loaded event.
        /// </summary>
        protected internal void TriggerInventoryArchiveLoaded(
            UUID id, bool succeeded, UserAccount userInfo, string invPath, Stream loadStream,
            Exception reportedException, int LoadCount)
        {
            InventoryArchiveLoaded handlerInventoryArchiveLoaded = OnInventoryArchiveLoaded;
            if (handlerInventoryArchiveLoaded != null)
                handlerInventoryArchiveLoaded(id, succeeded, userInfo, invPath, loadStream, reportedException, LoadCount);
        }

        public bool ArchiveInventory(
             UUID id, string firstName, string lastName, string invPath, string pass, Stream saveStream)
        {
            return ArchiveInventory(id, firstName, lastName, invPath, pass, saveStream, new Dictionary<string, object>());
        }

        public bool ArchiveInventory(
            UUID id, string firstName, string lastName, string invPath, string pass, Stream saveStream,
            Dictionary<string, object> options)
        {
            if (m_scenes.Count > 0)
            {
                UserAccount userInfo = GetUserInfo(firstName, lastName, pass);

                if (userInfo != null)
                {
//                    if (CheckPresence(userInfo.PrincipalID))
//                    {
                        try
                        {
                            new InventoryArchiveWriteRequest(id, this, m_aScene, userInfo, invPath, saveStream).Execute(options, UserAccountService);
                        }
                        catch (EntryPointNotFoundException e)
                        {
                            m_log.ErrorFormat(
                                "[INVENTORY ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream."
                                    + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                            m_log.Error(e);

                            return false;
                        }

                        return true;
//                    }
//                    else
//                    {
//                        m_log.ErrorFormat(
//                            "[INVENTORY ARCHIVER]: User {0} {1} {2} not logged in to this region simulator",
//                            userInfo.FirstName, userInfo.LastName, userInfo.PrincipalID);
//                    }
                }
            }

            return false;
        }

        public bool ArchiveInventory(
            UUID id, string firstName, string lastName, string invPath, string pass, string savePath,
            Dictionary<string, object> options)
        {
//            if (!ConsoleUtil.CheckFileDoesNotExist(MainConsole.Instance, savePath))
//                return false;

            if (m_scenes.Count > 0)
            {
                UserAccount userInfo = GetUserInfo(firstName, lastName, pass);

                if (userInfo != null)
                {
//                    if (CheckPresence(userInfo.PrincipalID))
//                    {
                        try
                        {
                            new InventoryArchiveWriteRequest(id, this, m_aScene, userInfo, invPath, savePath).Execute(options, UserAccountService);
                        }
                        catch (EntryPointNotFoundException e)
                        {
                            m_log.ErrorFormat(
                                "[INVENTORY ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream."
                                    + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                            m_log.Error(e);

                            return false;
                        }

                        return true;
//                    }
//                    else
//                    {
//                        m_log.ErrorFormat(
//                            "[INVENTORY ARCHIVER]: User {0} {1} {2} not logged in to this region simulator",
//                            userInfo.FirstName, userInfo.LastName, userInfo.PrincipalID);
//                    }
                }
            }

            return false;
        }

        public bool DearchiveInventory(UUID id, string firstName, string lastName, string invPath, string pass, Stream loadStream)
        {
            return DearchiveInventory(id, firstName, lastName, invPath, pass, loadStream, new Dictionary<string, object>());
        }

        public bool DearchiveInventory(
            UUID id, string firstName, string lastName, string invPath, string pass, Stream loadStream,
            Dictionary<string, object> options)
        {
            if (m_scenes.Count > 0)
            {
                UserAccount userInfo = GetUserInfo(firstName, lastName, pass);

                if (userInfo != null)
                {
//                    if (CheckPresence(userInfo.PrincipalID))
//                    {
                        InventoryArchiveReadRequest request;
                        bool merge = (options.ContainsKey("merge") ? (bool)options["merge"] : false);

                        try
                        {
                            request = new InventoryArchiveReadRequest(id, this, m_aScene.InventoryService, m_aScene.AssetService, m_aScene.UserAccountService, userInfo, invPath, loadStream, merge);
                        }
                        catch (EntryPointNotFoundException e)
                        {
                            m_log.ErrorFormat(
                                "[INVENTORY ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream."
                                    + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                            m_log.Error(e);

                            return false;
                        }

                        UpdateClientWithLoadedNodes(userInfo, request.Execute());

                        return true;
//                    }
//                    else
//                    {
//                        m_log.ErrorFormat(
//                            "[INVENTORY ARCHIVER]: User {0} {1} {2} not logged in to this region simulator",
//                            userInfo.FirstName, userInfo.LastName, userInfo.PrincipalID);
//                    }
                }
                else
                    m_log.ErrorFormat("[INVENTORY ARCHIVER]: User {0} {1} not found",
                            firstName, lastName);
            }

            return false;
        }

        public bool DearchiveInventory(
             UUID id, string firstName, string lastName, string invPath, string pass, string loadPath,
             Dictionary<string, object> options)
        {
            if (m_scenes.Count > 0)
            {
                UserAccount userInfo = GetUserInfo(firstName, lastName, pass);

                if (userInfo != null)
                {
//                    if (CheckPresence(userInfo.PrincipalID))
//                    {
                        InventoryArchiveReadRequest request;
                        bool merge = (options.ContainsKey("merge") ? (bool)options["merge"] : false);

                        try
                        {
                            request = new InventoryArchiveReadRequest(id, this, m_aScene.InventoryService, m_aScene.AssetService, m_aScene.UserAccountService, userInfo, invPath, loadPath, merge);
                        }
                        catch (EntryPointNotFoundException e)
                        {
                            m_log.ErrorFormat(
                                "[INVENTORY ARCHIVER]: Mismatch between Mono and zlib1g library version when trying to create compression stream."
                                    + "If you've manually installed Mono, have you appropriately updated zlib1g as well?");
                            m_log.Error(e);

                            return false;
                        }

                        UpdateClientWithLoadedNodes(userInfo, request.Execute());

                        return true;
//                    }
//                    else
//                    {
//                        m_log.ErrorFormat(
//                            "[INVENTORY ARCHIVER]: User {0} {1} {2} not logged in to this region simulator",
//                            userInfo.FirstName, userInfo.LastName, userInfo.PrincipalID);
//                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Load inventory from an inventory file archive
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void HandleLoadInvConsoleCommand(string module, string[] cmdparams)
        {
            try
            {
                UUID id = UUID.Random();

                Dictionary<string, object> options = new Dictionary<string, object>();
                OptionSet optionSet = new OptionSet().Add("m|merge", delegate (string v) { options["merge"] = v != null; });

                List<string> mainParams = optionSet.Parse(cmdparams);

                if (mainParams.Count < 6)
                {
                    m_log.Error(
                        "[INVENTORY ARCHIVER]: usage is load iar [-m|--merge] <first name> <last name> <inventory path> <user password> [<load file path>]");
                    return;
                }

                string firstName = mainParams[2];
                string lastName = mainParams[3];
                string invPath = mainParams[4];
                string pass = mainParams[5];
                string loadPath = (mainParams.Count > 6 ? mainParams[6] : DEFAULT_INV_BACKUP_FILENAME);

                m_log.InfoFormat(
                    "[INVENTORY ARCHIVER]: Loading archive {0} to inventory path {1} for {2} {3}",
                    loadPath, invPath, firstName, lastName);

                lock (m_pendingConsoleTasks)
                    m_pendingConsoleTasks.Add(id);

                DearchiveInventory(id, firstName, lastName, invPath, pass, loadPath, options);
            }
            catch (InventoryArchiverException e)
            {
                m_log.ErrorFormat("[INVENTORY ARCHIVER]: {0}", e.Message);
            }
        }

        /// <summary>
        /// Save inventory to a file archive
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void HandleSaveInvConsoleCommand(string module, string[] cmdparams)
        {
            UUID id = UUID.Random();

            Dictionary<string, object> options = new Dictionary<string, object>();

            OptionSet ops = new OptionSet();
            //ops.Add("v|version=", delegate(string v) { options["version"] = v; });
            ops.Add("h|home=", delegate(string v) { options["home"] = v; });
            ops.Add("v|verbose", delegate(string v) { options["verbose"] = v; });
            ops.Add("c|creators", delegate(string v) { options["creators"] = v; });
            ops.Add("noassets", delegate(string v) { options["noassets"] = v != null; });
            ops.Add("e|exclude=", delegate(string v)
                {
                    if (!options.ContainsKey("exclude"))
                        options["exclude"] = new List<String>();
                    ((List<String>)options["exclude"]).Add(v);
                });
            ops.Add("f|excludefolder=", delegate(string v)
                {
                    if (!options.ContainsKey("excludefolders"))
                        options["excludefolders"] = new List<String>();
                    ((List<String>)options["excludefolders"]).Add(v);
                });
            ops.Add("perm=", delegate(string v) { options["checkPermissions"] = v; });

            List<string> mainParams = ops.Parse(cmdparams);

            try
            {
                if (mainParams.Count < 6)
                {
                    m_log.Error(
                        "[INVENTORY ARCHIVER]: save iar [-h|--home=<url>] [--noassets] <first> <last> <inventory path> <password> [<IAR path>] [-c|--creators] [-e|--exclude=<name/uuid>] [-f|--excludefolder=<foldername/uuid>] [-v|--verbose]");
                    return;
                }

                if (options.ContainsKey("home"))
                    m_log.WarnFormat("[INVENTORY ARCHIVER]: Please be aware that inventory archives with creator information are not compatible with OpenSim 0.7.0.2 and earlier.  Do not use the -home option if you want to produce a compatible IAR");

                string firstName = mainParams[2];
                string lastName = mainParams[3];
                string invPath = mainParams[4];
                string pass = mainParams[5];
                string savePath = (mainParams.Count > 6 ? mainParams[6] : DEFAULT_INV_BACKUP_FILENAME);

                m_log.InfoFormat(
                    "[INVENTORY ARCHIVER]: Saving archive {0} using inventory path {1} for {2} {3}",
                    savePath, invPath, firstName, lastName);

                lock (m_pendingConsoleTasks)
                    m_pendingConsoleTasks.Add(id);

                ArchiveInventory(id, firstName, lastName, invPath, pass, savePath, options);
            }
            catch (InventoryArchiverException e)
            {
                m_log.ErrorFormat("[INVENTORY ARCHIVER]: {0}", e.Message);
            }
        }

        private void SaveInvConsoleCommandCompleted(
            UUID id, bool succeeded, UserAccount userInfo, string invPath, Stream saveStream,
            Exception reportedException, int SaveCount, int FilterCount)
        {
            lock (m_pendingConsoleTasks)
            {
                if (m_pendingConsoleTasks.Contains(id))
                    m_pendingConsoleTasks.Remove(id);
                else
                    return;
            }

            if (succeeded)
            {
                // Report success and include item count and filter count (Skipped items due to --perm or --exclude switches)
                if(FilterCount == 0)
                    m_log.InfoFormat("[INVENTORY ARCHIVER]: Saved archive with {0} items for {1} {2}", SaveCount, userInfo.FirstName, userInfo.LastName);
                else
                    m_log.InfoFormat("[INVENTORY ARCHIVER]: Saved archive with {0} items for {1} {2}. Skipped {3} items due to exclude and/or perm switches", SaveCount, userInfo.FirstName, userInfo.LastName, FilterCount);
            }
            else
            {
                m_log.ErrorFormat(
                    "[INVENTORY ARCHIVER]: Archive save for {0} {1} failed - {2}",
                    userInfo.FirstName, userInfo.LastName, reportedException.Message);
            }
        }

        private void LoadInvConsoleCommandCompleted(
            UUID id, bool succeeded, UserAccount userInfo, string invPath, Stream loadStream,
            Exception reportedException, int LoadCount)
        {
            lock (m_pendingConsoleTasks)
            {
                if (m_pendingConsoleTasks.Contains(id))
                    m_pendingConsoleTasks.Remove(id);
                else
                    return;
            }

            if (succeeded)
            {
                m_log.InfoFormat("[INVENTORY ARCHIVER]: Loaded {0} items from archive {1} for {2} {3}", LoadCount, invPath, userInfo.FirstName, userInfo.LastName);
            }
            else
            {
                m_log.ErrorFormat(
                    "[INVENTORY ARCHIVER]: Archive load for {0} {1} failed - {2}",
                    userInfo.FirstName, userInfo.LastName, reportedException.Message);
            }
        }

        /// <summary>
        /// Get user information for the given name.
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="pass">User password</param>
        /// <returns></returns>
        protected UserAccount GetUserInfo(string firstName, string lastName, string pass)
        {
            UserAccount account
                = m_aScene.UserAccountService.GetUserAccount(m_aScene.RegionInfo.ScopeID, firstName, lastName);

            if (null == account)
            {
                m_log.ErrorFormat(
                    "[INVENTORY ARCHIVER]: Failed to find user info for {0} {1}",
                    firstName, lastName);
                return null;
            }

            try
            {
                string encpass = Util.Md5Hash(pass);
                if (m_aScene.AuthenticationService.Authenticate(account.PrincipalID, encpass, 1) != string.Empty)
                {
                    return account;
                }
                else
                {
                    m_log.ErrorFormat(
                        "[INVENTORY ARCHIVER]: Password for user {0} {1} incorrect.  Please try again.",
                        firstName, lastName);
                    return null;
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[INVENTORY ARCHIVER]: Could not authenticate password, {0}", e);
                return null;
            }
        }

        /// <summary>
        /// Notify the client of loaded nodes if they are logged in
        /// </summary>
        /// <param name="loadedNodes">Can be empty.  In which case, nothing happens</param>
        private void UpdateClientWithLoadedNodes(UserAccount userInfo, HashSet<InventoryNodeBase> loadedNodes)
        {
            if (loadedNodes.Count == 0)
                return;

            foreach (Scene scene in m_scenes.Values)
            {
                ScenePresence user = scene.GetScenePresence(userInfo.PrincipalID);

                if (user != null && !user.IsChildAgent)
                {
                    foreach (InventoryNodeBase node in loadedNodes)
                    {
//                        m_log.DebugFormat(
//                            "[INVENTORY ARCHIVER]: Notifying {0} of loaded inventory node {1}",
//                            user.Name, node.Name);

                        user.ControllingClient.SendBulkUpdateInventory(node);
                    }

                    break;
                }
            }
        }

//        /// <summary>
//        /// Check if the given user is present in any of the scenes.
//        /// </summary>
//        /// <param name="userId">The user to check</param>
//        /// <returns>true if the user is in any of the scenes, false otherwise</returns>
//        protected bool CheckPresence(UUID userId)
//        {
//            if (DisablePresenceChecks)
//                return true;
//
//            foreach (Scene scene in m_scenes.Values)
//            {
//                ScenePresence p;
//                if ((p = scene.GetScenePresence(userId)) != null)
//                {
//                    p.ControllingClient.SendAgentAlertMessage("Inventory operation has been started", false);
//                    return true;
//                }
//            }
//
//            return false;
//        }
    }
}
