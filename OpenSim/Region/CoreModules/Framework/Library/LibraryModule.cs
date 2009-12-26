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
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.CoreModules.Avatar.Inventory.Archiver;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Region.CoreModules.Framework.Library
{
    public class LibraryModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool m_HasRunOnce = false;

        private bool m_Enabled = false;
        private string m_LibraryName = "OpenSim Library";
        private Scene m_Scene;

        #region ISharedRegionModule

        public void Initialise(IConfigSource config)
        {
            m_Enabled = config.Configs["Modules"].GetBoolean("LibraryModule", m_Enabled);
            if (m_Enabled)
            {
                IConfig libConfig = config.Configs["LibraryModule"];
                if (libConfig != null)
                    m_LibraryName = libConfig.GetString("LibraryName", m_LibraryName);
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
        }

        public void RemoveRegion(Scene scene)
        {
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
            InventoryFolderImpl lib = m_Scene.CommsManager.UserProfileCacheService.LibraryRoot;
            if (lib == null)
            {
                m_log.Debug("[LIBRARY MODULE]: No library. Ignoring Library Module");
                return;
            }

            lib.Name = m_LibraryName;

            RegionInfo regInfo = new RegionInfo();
            Scene m_MockScene = new Scene(regInfo);
            m_MockScene.CommsManager = m_Scene.CommsManager;
            LocalInventoryService invService = new LocalInventoryService((LibraryRootFolder)lib);
            m_MockScene.RegisterModuleInterface<IInventoryService>(invService);
            m_MockScene.RegisterModuleInterface<IAssetService>(m_Scene.AssetService);

            UserProfileData profile = new UserProfileData();
            profile.FirstName = "OpenSim";
            profile.ID = lib.Owner;
            profile.SurName = "Library";
            CachedUserInfo uinfo = new CachedUserInfo(invService, profile);

            foreach (string iarFileName in Directory.GetFiles(pathToLibraries, "*.iar"))
            {
                string simpleName = Path.GetFileNameWithoutExtension(iarFileName);

                m_log.InfoFormat("[LIBRARY MODULE]: Loading library archive {0} ({1})...", iarFileName, simpleName);
                simpleName = GetInventoryPathFromName(simpleName);

                try
                {
                    InventoryArchiveReadRequest archread = new InventoryArchiveReadRequest(m_MockScene, uinfo, simpleName, iarFileName);
                    List<InventoryNodeBase> nodes = archread.Execute();
                    if (nodes.Count == 0)
                    {
                        // didn't find the subfolder with the given name; place it on the top
                        m_log.InfoFormat("[LIBRARY MODULE]: Didn't find {0} in library. Placing archive on the top level", simpleName);
                        archread.Close();
                        archread = new InventoryArchiveReadRequest(m_MockScene, uinfo, "/", iarFileName);
                        archread.Execute();
                    }
                    archread.Close();
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[LIBRARY MODULE]: Exception when processing archive {0}: {1}", iarFileName, e.Message);
                }

            }

        }

        private void DumpLibrary()
        {
            InventoryFolderImpl lib = m_Scene.CommsManager.UserProfileCacheService.LibraryRoot;

            m_log.DebugFormat(" - folder {0}", lib.Name);
            DumpFolder(lib);
        }

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
