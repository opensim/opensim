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
using log4net.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Services;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Grid.InventoryServer
{
    public class OpenInventory_Main : BaseOpenSimServer
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private GridInventoryService m_inventoryService;
        //private HGInventoryService m_directInventoryService;

        public const string LogName = "INVENTORY";

        public static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            OpenInventory_Main theServer = new OpenInventory_Main();
            theServer.Startup();

            theServer.Work();
        }

        public OpenInventory_Main()
        {
            m_console = new LocalConsole("Inventory");
            MainConsole.Instance = m_console;
        }

        protected override void StartupSpecific()
        {
            InventoryConfig config = new InventoryConfig(LogName, (Path.Combine(Util.configDir(), "InventoryServer_Config.xml")));

            m_inventoryService = new GridInventoryService(config.UserServerURL);
            m_inventoryService.DoLookup = config.SessionLookUp;
            m_inventoryService.AddPlugin(config.DatabaseProvider, config.DatabaseConnect);


            m_log.Info("[" + LogName + "]: Starting HTTP server ...");

            m_httpServer = new BaseHttpServer(config.HttpPort);

            AddHttpHandlers(config.RegionAccessToAgentsInventory);

            m_httpServer.Start();

            m_log.Info("[" + LogName + "]: Started HTTP server");

            new HGInventoryService(m_inventoryService, config.AssetServerURL, config.UserServerURL, m_httpServer, config.InventoryServerURL);

            base.StartupSpecific();

            m_console.Commands.AddCommand("inventoryserver", false, "add user",
                    "add user",
                    "Add a random user inventory", HandleAddUser);
        }

        protected void AddHttpHandlers(bool regionAccess)
        {
            if (regionAccess)
            {
                m_httpServer.AddStreamHandler(
                    new RestDeserialiseSecureHandler<Guid, InventoryCollection>(
                        "POST", "/GetInventory/", m_inventoryService.GetUserInventory, m_inventoryService.CheckAuthSession));

                m_httpServer.AddStreamHandler(
                    new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                        "POST", "/UpdateFolder/", m_inventoryService.UpdateFolder, m_inventoryService.CheckAuthSession));

                m_httpServer.AddStreamHandler(
                    new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                        "POST", "/MoveFolder/", m_inventoryService.MoveFolder, m_inventoryService.CheckAuthSession));

                m_httpServer.AddStreamHandler(
                    new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                        "POST", "/PurgeFolder/", m_inventoryService.PurgeFolder, m_inventoryService.CheckAuthSession));

                m_httpServer.AddStreamHandler(
                    new RestDeserialiseSecureHandler<InventoryItemBase, bool>(
                        "POST", "/DeleteItem/", m_inventoryService.DeleteItem, m_inventoryService.CheckAuthSession));

                m_httpServer.AddStreamHandler(
                    new RestDeserialiseSecureHandler<InventoryItemBase, InventoryItemBase>(
                        "POST", "/QueryItem/", m_inventoryService.QueryItem, m_inventoryService.CheckAuthSession));

                m_httpServer.AddStreamHandler(
                    new RestDeserialiseSecureHandler<InventoryFolderBase, InventoryFolderBase>(
                        "POST", "/QueryFolder/", m_inventoryService.QueryFolder, m_inventoryService.CheckAuthSession));

            }

            m_httpServer.AddStreamHandler(
                new RestDeserialiseTrustedHandler<Guid, bool>(
                    "POST", "/CreateInventory/", m_inventoryService.CreateUsersInventory, m_inventoryService.CheckTrustSource));

            m_httpServer.AddStreamHandler(
                new RestDeserialiseSecureHandler<InventoryFolderBase, bool>(
                    "POST", "/NewFolder/", m_inventoryService.AddFolder, m_inventoryService.CheckAuthSession));

            m_httpServer.AddStreamHandler(
                new RestDeserialiseTrustedHandler<InventoryFolderBase, bool>(
                    "POST", "/CreateFolder/", m_inventoryService.AddFolder, m_inventoryService.CheckTrustSource));

            m_httpServer.AddStreamHandler(
                new RestDeserialiseSecureHandler<InventoryItemBase, bool>(
                    "POST", "/NewItem/", m_inventoryService.AddItem, m_inventoryService.CheckAuthSession));

            m_httpServer.AddStreamHandler(
             new RestDeserialiseTrustedHandler<InventoryItemBase, bool>(
                 "POST", "/AddNewItem/", m_inventoryService.AddItem, m_inventoryService.CheckTrustSource));

            m_httpServer.AddStreamHandler(
                new RestDeserialiseTrustedHandler<Guid, List<InventoryItemBase>>(
                    "POST", "/GetItems/", m_inventoryService.GetFolderItems, m_inventoryService.CheckTrustSource));

            // for persistent active gestures
            m_httpServer.AddStreamHandler(
                new RestDeserialiseTrustedHandler<Guid, List<InventoryItemBase>>
                    ("POST", "/ActiveGestures/", m_inventoryService.GetActiveGestures, m_inventoryService.CheckTrustSource));

            // WARNING: Root folders no longer just delivers the root and immediate child folders (e.g
            // system folders such as Objects, Textures), but it now returns the entire inventory skeleton.
            // It would have been better to rename this request, but complexities in the BaseHttpServer
            // (e.g. any http request not found is automatically treated as an xmlrpc request) make it easier
            // to do this for now.
            m_httpServer.AddStreamHandler(
                new RestDeserialiseTrustedHandler<Guid, List<InventoryFolderBase>>
                    ("POST", "/RootFolders/", m_inventoryService.GetInventorySkeleton, m_inventoryService.CheckTrustSource));
        }

        private void Work()
        {
            m_console.Output("Enter help for a list of commands\n");

            while (true)
            {
                m_console.Prompt();
            }
        }

        private void HandleAddUser(string module, string[] args)
        {
            m_inventoryService.CreateUsersInventory(UUID.Random().Guid);
        }
    }
}
