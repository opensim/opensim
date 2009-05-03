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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Services;
using Caps = OpenSim.Framework.Communications.Capabilities.Caps;
using LLSDHelpers = OpenSim.Framework.Communications.Capabilities.LLSDHelpers;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.Interfaces;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.CoreModules.ServiceConnectors.Interregion;

using OpenMetaverse.StructuredData;

namespace OpenSim.Region.CoreModules.Hypergrid
{
    public class HGStandaloneInventoryModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool initialized = false;
        private static bool enabled = false;
        private static bool safemode = false;

        private bool m_doLookup = false;
        Scene m_scene;
        HGInventoryService m_inventoryService;
        InventoryServiceBase m_inventoryBase;

        public bool DoLookup
        {
            get { return m_doLookup; }
            set { m_doLookup = value; }
        }

        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (!initialized)
            {
                initialized = true;
                m_scene = scene;

                // This module is only on for standalones
                enabled = !config.Configs["Startup"].GetBoolean("gridmode", true) && config.Configs["Startup"].GetBoolean("hypergrid", false);
                if (config.Configs["Hypergrid"] != null)
                    safemode = config.Configs["Hypergrid"].GetBoolean("safemode", false);
            }
        }

        public void PostInitialise()
        {
            if (enabled)
            {
                m_log.Info("[HGStandaloneInvModule]: Starting...");
                //m_inventoryService = new InventoryService(m_scene);
                m_inventoryBase = (InventoryServiceBase)m_scene.CommsManager.SecureInventoryService;

                m_inventoryService = new HGInventoryService(m_inventoryBase,
                    ((AssetServerBase)m_scene.CommsManager.AssetCache.AssetServer).AssetProviderPlugin,
                    (UserManagerBase)m_scene.CommsManager.UserAdminService, m_scene.CommsManager.HttpServer, 
                    m_scene.CommsManager.NetworkServersInfo.InventoryURL);

                AddHttpHandlers(m_scene.CommsManager.HttpServer);
                m_inventoryService.AddHttpHandlers();
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "HGStandaloneInventoryModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        public virtual void AddHttpHandlers(IHttpServer httpServer)
        {
            if (!safemode)
            {
                httpServer.AddStreamHandler(
                    new RestDeserialiseSecureHandler<Guid, InventoryCollection>(
                        "POST", "/GetInventory/", m_inventoryService.GetUserInventory, CheckAuthSession));
                httpServer.AddStreamHandler(
                    new RestDeserialiseSecureHandler<InventoryItemBase, bool>(
                        "POST", "/DeleteItem/", m_inventoryBase.DeleteItem, CheckAuthSession));
                httpServer.AddStreamHandler(
                    new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                        "POST", "/UpdateFolder/", m_inventoryBase.UpdateFolder, CheckAuthSession));

                httpServer.AddStreamHandler(
                    new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                        "POST", "/MoveFolder/", m_inventoryBase.MoveFolder, CheckAuthSession));

                httpServer.AddStreamHandler(
                    new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                        "POST", "/PurgeFolder/", m_inventoryBase.PurgeFolder, CheckAuthSession));
            }

            httpServer.AddStreamHandler(
                new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                    "POST", "/NewFolder/", m_inventoryBase.AddFolder, CheckAuthSession));

            httpServer.AddStreamHandler(
                new RestDeserialiseSecureHandler<InventoryItemBase, bool>(
                    "POST", "/NewItem/", m_inventoryBase.AddItem, CheckAuthSession));


        }

        /// <summary>
        /// Check that the source of an inventory request for a particular agent is a current session belonging to
        /// that agent.
        /// </summary>
        /// <param name="session_id"></param>
        /// <param name="avatar_id"></param>
        /// <returns></returns>
        public bool CheckAuthSession(string session_id, string avatar_id)
        {
            return true;        
        }

    }

}
