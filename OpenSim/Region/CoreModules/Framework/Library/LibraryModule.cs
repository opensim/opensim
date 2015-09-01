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

using OpenSim.Framework;
using OpenSim.Framework.Communications;

using OpenSim.Region.CoreModules.Avatar.Inventory.Archiver;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Server.Base;

using OpenMetaverse;
using log4net;
using Mono.Addins;
using Nini.Config;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Region.CoreModules.Framework.Library
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LibraryModule")]
    public class LibraryModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool m_HasRunOnce = false;

        private bool m_Enabled = false;
//        private string m_LibraryName = "OpenSim Library";
        private Scene m_Scene;

        private ILibraryService m_Library;

        #region ISharedRegionModule

        public void Initialise(IConfigSource config)
        {
            m_Enabled = config.Configs["Modules"].GetBoolean("LibraryModule", m_Enabled);
            if (m_Enabled)
            {
                IConfig libConfig = config.Configs["LibraryService"];
                if (libConfig != null)
                {
                    string dllName = libConfig.GetString("LocalServiceModule", string.Empty);
                    m_log.Debug("[LIBRARY MODULE]: Library service dll is " + dllName);
                    if (dllName != string.Empty)
                    {
                        Object[] args = new Object[] { config };
                        m_Library = ServerUtils.LoadPlugin<ILibraryService>(dllName, args);
                    }
                }
            }
            if (m_Library == null)
            {
                m_log.Warn("[LIBRARY MODULE]: No local library service. Module will be disabled.");
                m_Enabled = false;
            }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public string Name
        {
            get { return "Library Module"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            // Store only the first scene
            if (m_Scene == null)
            {
                m_Scene = scene;
            }
            scene.RegisterModuleInterface<ILibraryService>(m_Library);
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.UnregisterModuleInterface<ILibraryService>(m_Library);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            // This will never run more than once, even if the region is restarted
            if (!m_HasRunOnce) 
            {
                LoadLibrariesFromArchives();
                //DumpLibrary();
                m_HasRunOnce = true;
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
            m_Scene = null;
        }

        #endregion ISharedRegionModule

        #region LoadLibraries
        private string pathToLibraries = "Library";

        protected void LoadLibrariesFromArchives()
        {
            InventoryFolderImpl lib = m_Library.LibraryRootFolder;
            if (lib == null)
            {
                m_log.Debug("[LIBRARY MODULE]: No library. Ignoring Library Module");
                return;
            }

            RegionInfo regInfo = new RegionInfo();
            Scene m_MockScene = new Scene(regInfo, null);
            LocalInventoryService invService = new LocalInventoryService(lib);
            m_MockScene.RegisterModuleInterface<IInventoryService>(invService);
            m_MockScene.RegisterModuleInterface<IAssetService>(m_Scene.AssetService);

            UserAccount uinfo = new UserAccount(lib.Owner);
            uinfo.FirstName = "OpenSim";
            uinfo.LastName = "Library";
            uinfo.ServiceURLs = new Dictionary<string, object>();

            foreach (string iarFileName in Directory.GetFiles(pathToLibraries, "*.iar"))
            {
                string simpleName = Path.GetFileNameWithoutExtension(iarFileName);

                m_log.InfoFormat("[LIBRARY MODULE]: Loading library archive {0} ({1})...", iarFileName, simpleName);
                simpleName = GetInventoryPathFromName(simpleName);

                InventoryArchiveReadRequest archread = new InventoryArchiveReadRequest(m_MockScene.InventoryService, m_MockScene.AssetService, m_MockScene.UserAccountService, uinfo, simpleName, iarFileName, false);
                try
                {
                    HashSet<InventoryNodeBase> nodes = archread.Execute();
                    if (nodes != null && nodes.Count == 0)
                    {
                        // didn't find the subfolder with the given name; place it on the top
                        m_log.InfoFormat("[LIBRARY MODULE]: Didn't find {0} in library. Placing archive on the top level", simpleName);
                        archread.Close();
                        archread = new InventoryArchiveReadRequest(m_MockScene.InventoryService, m_MockScene.AssetService, m_MockScene.UserAccountService, uinfo, "/", iarFileName, false);
                        archread.Execute();
                    }

                    foreach (InventoryNodeBase node in nodes)
                        FixPerms(node);
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[LIBRARY MODULE]: Exception when processing archive {0}: {1}", iarFileName, e.StackTrace);
                }
                finally
                {
                    archread.Close();
                }
            }
        }

        private void FixPerms(InventoryNodeBase node)
        {
            m_log.DebugFormat("[LIBRARY MODULE]: Fixing perms for {0} {1}", node.Name, node.ID);

            if (node is InventoryItemBase)
            {
                InventoryItemBase item = (InventoryItemBase)node;
                item.BasePermissions = (uint)PermissionMask.All;
                item.EveryOnePermissions = (uint)PermissionMask.All - (uint)PermissionMask.Modify;
                item.CurrentPermissions = (uint)PermissionMask.All;
                item.NextPermissions = (uint)PermissionMask.All;
            }
        }

//        private void DumpLibrary()
//        {
//            InventoryFolderImpl lib = m_Library.LibraryRootFolder;
//
//            m_log.DebugFormat(" - folder {0}", lib.Name);
//            DumpFolder(lib);
//        }
//
//        private void DumpLibrary()
//        {
//            InventoryFolderImpl lib = m_Scene.CommsManager.UserProfileCacheService.LibraryRoot;
//
//            m_log.DebugFormat(" - folder {0}", lib.Name);
//            DumpFolder(lib);
//        }

        private void DumpFolder(InventoryFolderImpl folder)
        {
            foreach (InventoryItemBase item in folder.Items.Values)
            {
                m_log.DebugFormat("   --> item {0}", item.Name);
            }
            foreach (InventoryFolderImpl f in folder.RequestListOfFolderImpls())
            {
                m_log.DebugFormat(" - folder {0}", f.Name);
                DumpFolder(f);
            }
        }

        private string GetInventoryPathFromName(string name)
        {
            string[] parts = name.Split(new char[] { ' ' });
            if (parts.Length == 3)
            {
                name = string.Empty;
                // cut the last part
                for (int i = 0; i < parts.Length - 1; i++)
                    name = name + ' ' + parts[i];
            }

            return name;
        }

        #endregion LoadLibraries
    }
}
