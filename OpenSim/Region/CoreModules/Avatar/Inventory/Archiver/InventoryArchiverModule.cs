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
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Archiver
{
    /// <summary>
    /// This module loads and saves OpenSimulator inventory archives
    /// </summary>
    public class InventoryArchiverModule : IRegionModule, IInventoryArchiverModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        public string Name { get { return "Inventory Archiver Module"; } }
        
        public bool IsSharedModule { get { return true; } }

        /// <value>
        /// Enable or disable checking whether the iar user is actually logged in 
        /// </value>
        public bool DisablePresenceChecks { get; set; }
        
        public event InventoryArchiveSaved OnInventoryArchiveSaved;
        
        /// <summary>
        /// The file to load and save inventory if no filename has been specified
        /// </summary>
        protected const string DEFAULT_INV_BACKUP_FILENAME = "user-inventory.iar";

        /// <value>
        /// Pending save completions initiated from the console
        /// </value>
        protected List<Guid> m_pendingConsoleSaves = new List<Guid>();
        
        /// <value>
        /// All scenes that this module knows about
        /// </value>
        private Dictionary<UUID, Scene> m_scenes = new Dictionary<UUID, Scene>();
        private Scene m_aScene;

        public InventoryArchiverModule() {}

        public InventoryArchiverModule(bool disablePresenceChecks)
        {
            DisablePresenceChecks = disablePresenceChecks;
        }

        public void Initialise(Scene scene, IConfigSource source)
        {
            if (m_scenes.Count == 0)
            {
                scene.RegisterModuleInterface<IInventoryArchiverModule>(this);
                OnInventoryArchiveSaved += SaveInvConsoleCommandCompleted;
                
                scene.AddCommand(
                    this, "load iar",
                    "load iar <first> <last> <inventory path> <password> [<archive path>]",
                    "Load user inventory archive.  EXPERIMENTAL", HandleLoadInvConsoleCommand); 
                
                scene.AddCommand(
                    this, "save iar",
                    "save iar <first> <last> <inventory path> <password> [<archive path>]",
                    "Save user inventory archive.  EXPERIMENTAL", HandleSaveInvConsoleCommand);

                m_aScene = scene;
            }
                        
            m_scenes[scene.RegionInfo.RegionID] = scene;
        }

        public void PostInitialise() {}

        public void Close() {}
        
        /// <summary>
        /// Trigger the inventory archive saved event.
        /// </summary>
        protected internal void TriggerInventoryArchiveSaved(
            Guid id, bool succeeded, CachedUserInfo userInfo, string invPath, Stream saveStream, 
            Exception reportedException)
        {
            InventoryArchiveSaved handlerInventoryArchiveSaved = OnInventoryArchiveSaved;
            if (handlerInventoryArchiveSaved != null)
                handlerInventoryArchiveSaved(id, succeeded, userInfo, invPath, saveStream, reportedException);
        }

        public bool ArchiveInventory(Guid id, string firstName, string lastName, string invPath, string pass, Stream saveStream)
        {
            if (m_scenes.Count > 0)
            {
                CachedUserInfo userInfo = GetUserInfo(firstName, lastName, pass);

                if (userInfo != null)
                {
                    if (CheckPresence(userInfo.UserProfile.ID))
                    {
                        new InventoryArchiveWriteRequest(id, this, m_aScene, userInfo, invPath, saveStream).Execute();
                        return true;
                    }
                    else
                    {
                        m_log.ErrorFormat(
                            "[INVENTORY ARCHIVER]: User {0} {1} not logged in to this region simulator",
                            userInfo.UserProfile.Name, userInfo.UserProfile.ID);
                    }
                }
            }

            return false;
        }
                        
        public bool ArchiveInventory(Guid id, string firstName, string lastName, string invPath, string pass, string savePath)
        {
            if (m_scenes.Count > 0)
            {
                CachedUserInfo userInfo = GetUserInfo(firstName, lastName, pass);
                
                if (userInfo != null)
                {
                    if (CheckPresence(userInfo.UserProfile.ID))
                    {
                        new InventoryArchiveWriteRequest(id, this, m_aScene, userInfo, invPath, savePath).Execute();
                        return true;
                    }
                    else
                    {
                        m_log.ErrorFormat(
                            "[INVENTORY ARCHIVER]: User {0} {1} not logged in to this region simulator",
                            userInfo.UserProfile.Name, userInfo.UserProfile.ID);
                    }
                }
            }
            
            return false;
        }
              
        public bool DearchiveInventory(string firstName, string lastName, string invPath, string pass, Stream loadStream)
        {
            if (m_scenes.Count > 0)
            {
                CachedUserInfo userInfo = GetUserInfo(firstName, lastName, pass);
                        
                if (userInfo != null)
                {
                    if (CheckPresence(userInfo.UserProfile.ID))
                    {
                        InventoryArchiveReadRequest request = 
                            new InventoryArchiveReadRequest(m_aScene, userInfo, invPath, loadStream);
                        UpdateClientWithLoadedNodes(userInfo, request.Execute());

                        return true;
                    }
                    else
                    {
                        m_log.ErrorFormat(
                            "[INVENTORY ARCHIVER]: User {0} {1} not logged in to this region simulator",
                            userInfo.UserProfile.Name, userInfo.UserProfile.ID);
                    }
                }
            }

            return false;
        }
        
        public bool DearchiveInventory(string firstName, string lastName, string invPath, string pass, string loadPath)
        {
            if (m_scenes.Count > 0)
            {
                CachedUserInfo userInfo = GetUserInfo(firstName, lastName, pass);
                
                if (userInfo != null)
                {
                    if (CheckPresence(userInfo.UserProfile.ID))
                    {
                        InventoryArchiveReadRequest request = 
                            new InventoryArchiveReadRequest(m_aScene, userInfo, invPath, loadPath);
                        UpdateClientWithLoadedNodes(userInfo, request.Execute());

                        return true;
                    }
                    else
                    {
                        m_log.ErrorFormat(
                            "[INVENTORY ARCHIVER]: User {0} {1} not logged in to this region simulator",
                            userInfo.UserProfile.Name, userInfo.UserProfile.ID);
                    }
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
            if (cmdparams.Length < 6)
            {
                m_log.Error(
                    "[INVENTORY ARCHIVER]: usage is load iar <first name> <last name> <inventory path> <user password> [<load file path>]");
                return;
            }

            m_log.Info("[INVENTORY ARCHIVER]: PLEASE NOTE THAT THIS FACILITY IS EXPERIMENTAL.  BUG REPORTS WELCOME.");

            string firstName = cmdparams[2];
            string lastName = cmdparams[3];
            string invPath = cmdparams[4];
            string pass = cmdparams[5];
            string loadPath = (cmdparams.Length > 6 ? cmdparams[6] : DEFAULT_INV_BACKUP_FILENAME);

            m_log.InfoFormat(
                "[INVENTORY ARCHIVER]: Loading archive {0} to inventory path {1} for {2} {3}",
                loadPath, invPath, firstName, lastName);
            
            if (DearchiveInventory(firstName, lastName, invPath, pass, loadPath))
                m_log.InfoFormat(
                    "[INVENTORY ARCHIVER]: Loaded archive {0} for {1} {2}",
                    loadPath, firstName, lastName);
        }
        
        /// <summary>
        /// Save inventory to a file archive
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void HandleSaveInvConsoleCommand(string module, string[] cmdparams)
        {
            if (cmdparams.Length < 6)
            {
                m_log.Error(
                    "[INVENTORY ARCHIVER]: usage is save iar <first name> <last name> <inventory path> <user password> [<save file path>]");
                return;
            }

            m_log.Info("[INVENTORY ARCHIVER]: PLEASE NOTE THAT THIS FACILITY IS EXPERIMENTAL.  BUG REPORTS WELCOME.");

            string firstName = cmdparams[2];
            string lastName = cmdparams[3];
            string invPath = cmdparams[4];
            string pass = cmdparams[5];
            string savePath = (cmdparams.Length > 6 ? cmdparams[6] : DEFAULT_INV_BACKUP_FILENAME);

            m_log.InfoFormat(
                "[INVENTORY ARCHIVER]: Saving archive {0} from inventory path {1} for {2} {3}",
                savePath, invPath, firstName, lastName);

            Guid id = Guid.NewGuid();
            ArchiveInventory(id, firstName, lastName, invPath, pass, savePath);

            lock (m_pendingConsoleSaves)
                m_pendingConsoleSaves.Add(id);
        }
        
        private void SaveInvConsoleCommandCompleted(
            Guid id, bool succeeded, CachedUserInfo userInfo, string invPath, Stream saveStream, 
            Exception reportedException)
        {
            lock (m_pendingConsoleSaves)
            {
                if (m_pendingConsoleSaves.Contains(id))
                    m_pendingConsoleSaves.Remove(id);
                else
                    return;
            }
            
            if (succeeded)
            {
                m_log.InfoFormat("[INVENTORY ARCHIVER]: Saved archive for {0}", userInfo.UserProfile.Name);
            }
            else
            {
                m_log.ErrorFormat(
                    "[INVENTORY ARCHIVER]: Archive save for {0} failed - {1}", 
                    userInfo.UserProfile.Name, reportedException.Message);
            }
        }
        
        /// <summary>
        /// Get user information for the given name.
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="pass">User password</param>
        /// <returns></returns>
        protected CachedUserInfo GetUserInfo(string firstName, string lastName, string pass)
        {
            CachedUserInfo userInfo = m_aScene.CommsManager.UserProfileCacheService.GetUserDetails(firstName, lastName);
            //m_aScene.CommsManager.UserService.GetUserProfile(firstName, lastName);
            if (null == userInfo)
            {
                m_log.ErrorFormat(
                    "[INVENTORY ARCHIVER]: Failed to find user info for {0} {1}", 
                    firstName, lastName);
                return null;
            }

            try
            {
                if (m_aScene.CommsManager.UserService.AuthenticateUserByPassword(userInfo.UserProfile.ID, pass))
                {
                    return userInfo;
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
                m_log.ErrorFormat("[INVENTORY ARCHIVER]: Could not authenticate password, {0}", e.Message);
                return null;
            }
        }
        
        /// <summary>
        /// Notify the client of loaded nodes if they are logged in
        /// </summary>
        /// <param name="loadedNodes">Can be empty.  In which case, nothing happens</param>
        private void UpdateClientWithLoadedNodes(CachedUserInfo userInfo, List<InventoryNodeBase> loadedNodes)
        {
            if (loadedNodes.Count == 0)
                return;
                   
            foreach (Scene scene in m_scenes.Values)
            {
                ScenePresence user = scene.GetScenePresence(userInfo.UserProfile.ID);
                
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

        /// <summary>
        /// Check if the given user is present in any of the scenes.
        /// </summary>
        /// <param name="userId">The user to check</param>
        /// <returns>true if the user is in any of the scenes, false otherwise</returns>
        protected bool CheckPresence(UUID userId)
        {
            if (DisablePresenceChecks)
                return true;
            
            foreach (Scene scene in m_scenes.Values)
            {
                ScenePresence p;
                if ((p = scene.GetScenePresence(userId)) != null)
                {
                    p.ControllingClient.SendAgentAlertMessage("Inventory operation has been started", false);
                    return true;
                }
            }

            return false;
        }
    }
}
