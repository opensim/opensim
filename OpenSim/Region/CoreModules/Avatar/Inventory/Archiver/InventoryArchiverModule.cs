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

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

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
        
        /// <summary>
        /// The file to load and save inventory if no filename has been specified
        /// </summary>
        protected const string DEFAULT_INV_BACKUP_FILENAME = "user-inventory_iar.tar.gz";        
        
        /// <value>
        /// All scenes that this module knows about
        /// </value>
        private Dictionary<UUID, Scene> m_scenes = new Dictionary<UUID, Scene>();
        
        /// <value>
        /// The comms manager we will use for all comms requests
        /// </value>
        private CommunicationsManager m_commsManager;

        public void Initialise(Scene scene, IConfigSource source)
        {
            if (m_scenes.Count == 0)
            {
                scene.RegisterModuleInterface<IInventoryArchiverModule>(this);
                m_commsManager = scene.CommsManager;
                
                scene.AddCommand(
                    this, "load iar",
                    "load iar <first> <last> <inventory path> [<archive path>]",
                    "Load user inventory archive.  EXPERIMENTAL, PLEASE DO NOT USE YET", HandleLoadInvConsoleCommand); 
                
                scene.AddCommand(
                    this, "save iar",
                    "save iar <first> <last> <inventory path> [<archive path>]",
                    "Save user inventory archive.  EXPERIMENTAL, PLEASE DO NOT USE YET", HandleSaveInvConsoleCommand);           
            }
                        
            m_scenes[scene.RegionInfo.RegionID] = scene;            
        }
        
        public void PostInitialise()
        {
        }

        public void Close()
        {
        }
               
        public void DearchiveInventory(string firstName, string lastName, string invPath, Stream loadStream)
        {
            if (m_scenes.Count > 0)
            {            
                InventoryArchiveReadRequest request = 
                    new InventoryArchiveReadRequest(firstName, lastName, invPath, loadStream, m_commsManager);
                
                UpdateClientWithLoadedNodes(firstName, lastName, request.Execute());
            }            
        }        

        public void ArchiveInventory(string firstName, string lastName, string invPath, Stream saveStream)
        {
            if (m_scenes.Count > 0)
            {
                new InventoryArchiveWriteRequest(firstName, lastName, invPath, saveStream, m_commsManager).Execute();
            }              
        }
        
        public void DearchiveInventory(string firstName, string lastName, string invPath, string loadPath)
        {
            if (m_scenes.Count > 0)
            {      
                InventoryArchiveReadRequest request = 
                    new InventoryArchiveReadRequest(firstName, lastName, invPath, loadPath, m_commsManager);
                
                UpdateClientWithLoadedNodes(firstName, lastName, request.Execute());
            }                
        }
                
        public void ArchiveInventory(string firstName, string lastName, string invPath, string savePath)
        {
            if (m_scenes.Count > 0)
            {
                new InventoryArchiveWriteRequest(firstName, lastName, invPath, savePath, m_commsManager).Execute();
            }            
        }        
        
        /// <summary>
        /// Load inventory from an inventory file archive
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void HandleLoadInvConsoleCommand(string module, string[] cmdparams)
        {
            if (cmdparams.Length < 5)
            {
                m_log.Error(
                    "[INVENTORY ARCHIVER]: usage is load iar <first name> <last name> <inventory path> [<load file path>]");
                return;
            }

            string firstName = cmdparams[2];
            string lastName = cmdparams[3];
            string invPath = cmdparams[4];
            string loadPath = (cmdparams.Length > 5 ? cmdparams[5] : DEFAULT_INV_BACKUP_FILENAME);

            DearchiveInventory(firstName, lastName, invPath, loadPath);
        }
        
        /// <summary>
        /// Save inventory to a file archive
        /// </summary>
        /// <param name="cmdparams"></param>
        protected void HandleSaveInvConsoleCommand(string module, string[] cmdparams)
        {
            if (cmdparams.Length < 5)
            {
                m_log.Error(
                    "[INVENTORY ARCHIVER]: usage is save iar <first name> <last name> <inventory path> [<save file path>]");
                return;
            }

            string firstName = cmdparams[2];
            string lastName = cmdparams[3];
            string invPath = cmdparams[4];
            string savePath = (cmdparams.Length > 5 ? cmdparams[5] : DEFAULT_INV_BACKUP_FILENAME);

            ArchiveInventory(firstName, lastName, invPath, savePath);
        }
        
        /// <summary>
        /// Notify the client of loaded nodes if they are logged in
        /// </summary>
        /// <param name="loadedNodes">Can be empty.  In which case, nothing happens</param>
        private void UpdateClientWithLoadedNodes(string firstName, string lastName, List<InventoryNodeBase> loadedNodes)
        {               
            if (loadedNodes.Count == 0)
                return;
            
            UserProfileData userProfile = m_commsManager.UserService.GetUserProfile(firstName, lastName);
            
            if (null == userProfile)
                return;
                   
            foreach (Scene scene in m_scenes.Values)
            {
                ScenePresence user = scene.GetScenePresence(userProfile.ID);
                
                if (user != null && !user.IsChildAgent)
                {        
                    foreach (InventoryNodeBase node in loadedNodes)
                    {
                        m_log.DebugFormat(
                            "[INVENTORY ARCHIVER]: Notifying {0} of loaded inventory node {1}", 
                            user.Name, node.Name);
                        
                        user.ControllingClient.SendBulkUpdateInventory(node);
                    }
                    
                    break;
                }        
            }            
        }
    }
}
