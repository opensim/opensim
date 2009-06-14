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

using log4net;
using Nini.Config;
using System;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Server.Base;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Inventory
{
    public class HGInventoryBroker : ISharedRegionModule, IInventoryService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;
        private bool m_Initialized = false;
        private Scene m_Scene;
        private UserProfileCacheService m_UserProfileService; // This should change to IUserProfileService

        private IInventoryService m_GridService;
        private ISessionAuthInventoryService m_HGService;

        private string m_LocalGridInventoryURI = string.Empty;
        public string Name
        {
            get { return "HGInventoryBroker"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("InventoryServices", "");
                if (name == Name)
                {
                    IConfig inventoryConfig = source.Configs["InventoryService"];
                    if (inventoryConfig == null)
                    {
                        m_log.Error("[HG INVENTORY CONNECTOR]: InventoryService missing from OpenSim.ini");
                        return;
                    }

                    string localDll = inventoryConfig.GetString("LocalGridInventoryService",
                            String.Empty);
                    string HGDll = inventoryConfig.GetString("HypergridInventoryService",
                            String.Empty);

                    if (localDll == String.Empty)
                    {
                        m_log.Error("[HG INVENTORY CONNECTOR]: No LocalGridInventoryService named in section InventoryService");
                        //return;
                        throw new Exception("Unable to proceed. Please make sure your ini files in config-include are updated according to .example's");
                    }

                    if (HGDll == String.Empty)
                    {
                        m_log.Error("[HG INVENTORY CONNECTOR]: No HypergridInventoryService named in section InventoryService");
                        //return;
                        throw new Exception("Unable to proceed. Please make sure your ini files in config-include are updated according to .example's");
                    }

                    Object[] args = new Object[] { source };
                    m_GridService =
                            ServerUtils.LoadPlugin<IInventoryService>(localDll,
                            args);

                    m_HGService =
                            ServerUtils.LoadPlugin<ISessionAuthInventoryService>(HGDll,
                            args);

                    if (m_GridService == null)
                    {
                        m_log.Error("[HG INVENTORY CONNECTOR]: Can't load local inventory service");
                        return;
                    }
                    if (m_HGService == null)
                    {
                        m_log.Error("[HG INVENTORY CONNECTOR]: Can't load hypergrid inventory service");
                        return;
                    }

                    m_LocalGridInventoryURI = inventoryConfig.GetString("InventoryServerURI", string.Empty);

                    m_Enabled = true;
                    m_log.Info("[HG INVENTORY CONNECTOR]: HG inventory broker enabled");
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            if (!m_Initialized)
            {
                m_Scene = scene;
                // HACK for now. Ugh!
                m_UserProfileService = m_Scene.CommsManager.UserProfileCacheService;
                // ugh!
                m_UserProfileService.SetInventoryService(this);
                scene.CommsManager.UserService.SetInventoryService(this);

                m_Initialized = true;
            }

            scene.RegisterModuleInterface<IInventoryService>(this);
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_log.InfoFormat("[INVENTORY CONNECTOR]: Enabled HG inventory for region {0}", scene.RegionInfo.RegionName);

        }

        #region IInventoryService

        public bool CreateUserInventory(UUID userID)
        {
            return m_GridService.CreateUserInventory(userID);
        }

        public List<InventoryFolderBase> GetInventorySkeleton(UUID userId)
        {
            return m_GridService.GetInventorySkeleton(userId);
        }

        public InventoryCollection GetUserInventory(UUID userID)
        {
            if (IsLocalGridUser(userID))
                return m_GridService.GetUserInventory(userID);
            else
                return null;
        }

        public void GetUserInventory(UUID userID, InventoryReceiptCallback callback)
        {
            if (IsLocalGridUser(userID))
                m_GridService.GetUserInventory(userID, callback);
            else
            {
                UUID sessionID = GetSessionID(userID);
                string uri = GetUserInventoryURI(userID) + "/" + userID.ToString();
                m_HGService.GetUserInventory(uri, sessionID, callback);
            }
        }

        public List<InventoryItemBase> GetFolderItems(UUID userID, UUID folderID)
        {
            return new List<InventoryItemBase>();
        }

        public bool AddFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return false;

            if (IsLocalGridUser(folder.Owner))
                return m_GridService.AddFolder(folder);
            else
            {
                UUID sessionID = GetSessionID(folder.Owner);
                string uri = GetUserInventoryURI(folder.Owner) + "/" + folder.Owner.ToString();
                return m_HGService.AddFolder(uri, folder, sessionID);
            }
        }

        public bool UpdateFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return false;

            if (IsLocalGridUser(folder.Owner))
                return m_GridService.UpdateFolder(folder);
            else
            {
                UUID sessionID = GetSessionID(folder.Owner);
                string uri = GetUserInventoryURI(folder.Owner) + "/" + folder.Owner.ToString();
                return m_HGService.UpdateFolder(uri, folder, sessionID);
            }
        }

        public bool MoveFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return false;

            if (IsLocalGridUser(folder.Owner))
                return m_GridService.MoveFolder(folder);
            else
            {
                UUID sessionID = GetSessionID(folder.Owner);
                string uri = GetUserInventoryURI(folder.Owner) + "/" + folder.Owner.ToString();
                return m_HGService.MoveFolder(uri, folder, sessionID);
            }
        }

        public bool PurgeFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return false;

            if (IsLocalGridUser(folder.Owner))
                return m_GridService.PurgeFolder(folder);
            else
            {
                UUID sessionID = GetSessionID(folder.Owner);
                string uri = GetUserInventoryURI(folder.Owner) + "/" + folder.Owner.ToString();
                return m_HGService.PurgeFolder(uri, folder, sessionID);
            }
        }

        public bool AddItem(InventoryItemBase item)
        {
            if (item == null)
                return false;

            if (IsLocalGridUser(item.Owner))
                return m_GridService.AddItem(item);
            else
            {
                UUID sessionID = GetSessionID(item.Owner);
                string uri = GetUserInventoryURI(item.Owner) + "/" + item.Owner.ToString();
                return m_HGService.AddItem(uri, item, sessionID);
            }
        }

        public bool UpdateItem(InventoryItemBase item)
        {
            if (item == null)
                return false;

            if (IsLocalGridUser(item.Owner))
                return m_GridService.UpdateItem(item);
            else
            {
                UUID sessionID = GetSessionID(item.Owner);
                string uri = GetUserInventoryURI(item.Owner) + "/" + item.Owner.ToString();
                return m_HGService.UpdateItem(uri, item, sessionID);
            }
        }

        public bool DeleteItem(InventoryItemBase item)
        {
            if (item == null)
                return false;

            if (IsLocalGridUser(item.Owner))
                return m_GridService.DeleteItem(item);
            else
            {
                UUID sessionID = GetSessionID(item.Owner);
                string uri = GetUserInventoryURI(item.Owner) + "/" + item.Owner.ToString();
                return m_HGService.DeleteItem(uri, item, sessionID);
            }
        }

        public InventoryItemBase QueryItem(InventoryItemBase item)
        {
            if (item == null)
                return null;

            if (IsLocalGridUser(item.Owner))
                return m_GridService.QueryItem(item);
            else
            {
                UUID sessionID = GetSessionID(item.Owner);
                string uri = GetUserInventoryURI(item.Owner) + "/" + item.Owner.ToString();
                return m_HGService.QueryItem(uri, item, sessionID);
            }
        }

        public InventoryFolderBase QueryFolder(InventoryFolderBase folder)
        {
            if (folder == null)
                return null;

            if (IsLocalGridUser(folder.Owner))
                return m_GridService.QueryFolder(folder);
            else
            {
                UUID sessionID = GetSessionID(folder.Owner);
                string uri = GetUserInventoryURI(folder.Owner) + "/" + folder.Owner.ToString();
                return m_HGService.QueryFolder(uri, folder, sessionID);
            }
        }

        public bool HasInventoryForUser(UUID userID)
        {
            return false;
        }

        public InventoryFolderBase RequestRootFolder(UUID userID)
        {
            return null;
        }

        public List<InventoryItemBase> GetActiveGestures(UUID userId)
        {
            return new List<InventoryItemBase>();
        }

        #endregion

        private UUID GetSessionID(UUID userID)
        {
            ScenePresence sp = m_Scene.GetScenePresence(userID);
            if (sp != null)
                return sp.ControllingClient.SessionId;

            return UUID.Zero;
        }

        private bool IsLocalGridUser(UUID userID)
        {
            if (m_UserProfileService == null)
                return false;

            CachedUserInfo uinfo = m_UserProfileService.GetUserDetails(userID);
            if (uinfo == null)
                return true;

            string userInventoryServerURI = HGNetworkServersInfo.ServerURI(uinfo.UserProfile.UserInventoryURI);

            if ((userInventoryServerURI == m_LocalGridInventoryURI) || (userInventoryServerURI == ""))
            {
                return true;
            }
            return false;
        }

        private string GetUserInventoryURI(UUID userID)
        {
            string invURI = m_LocalGridInventoryURI;

            CachedUserInfo uinfo = m_UserProfileService.GetUserDetails(userID);
            if ((uinfo == null) || (uinfo.UserProfile == null))
                return invURI;

            string userInventoryServerURI = HGNetworkServersInfo.ServerURI(uinfo.UserProfile.UserInventoryURI);

            if ((userInventoryServerURI != null) &&
                (userInventoryServerURI != ""))
                invURI = userInventoryServerURI;
            return invURI;
        }


    }
}
