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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using libsecondlife;
using log4net;
using log4net.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;

namespace OpenSim.Grid.InventoryServer
{
    public class OpenInventory_Main : BaseOpenSimServer, conscmd_callback
    {
        public const string LogName = "INVENTORY";
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private InventoryConfig m_config;
        private InventoryManager m_inventoryManager;
        private GridInventoryService m_inventoryService;

        public OpenInventory_Main()
        {
            m_console = new ConsoleBase(LogName, this);
            MainConsole.Instance = m_console;
        }

        #region conscmd_callback Members

        public override void RunCmd(string cmd, string[] cmdparams)
        {
            base.RunCmd(cmd, cmdparams);

            switch (cmd)
            {
                case "add-user":
                    m_inventoryService.CreateUsersInventory(LLUUID.Random().UUID);
                    break;
                case "shutdown":
                    m_console.Close();
                    Environment.Exit(0);
                    break;
            }
        }

        #endregion

        [STAThread]
        public static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            OpenInventory_Main theServer = new OpenInventory_Main();
            theServer.Startup();

            theServer.Work();
        }

        public void Startup()
        {
            m_log.Info("Initialising inventory manager...");
            m_config = new InventoryConfig(LogName, (Path.Combine(Util.configDir(), "InventoryServer_Config.xml")));

            m_inventoryService = new GridInventoryService();
            // m_inventoryManager = new InventoryManager();
            m_inventoryService.AddPlugin(m_config.DatabaseProvider, m_config.DatabaseConnect);

            m_log.Info("[" + LogName + "]: Starting HTTP server ...");

            m_httpServer = new BaseHttpServer(m_config.HttpPort);
            AddHttpHandlers();
            m_httpServer.Start();

            m_log.Info("[" + LogName + "]: Started HTTP server");
        }

        protected void AddHttpHandlers()
        {
            m_httpServer.AddStreamHandler(
                new RestDeserialisehandler<Guid, InventoryCollection>(
                    "POST", "/GetInventory/", m_inventoryService.GetUserInventory));

            m_httpServer.AddStreamHandler(
                new RestDeserialisehandler<Guid, bool>(
                    "POST", "/CreateInventory/", m_inventoryService.CreateUsersInventory));

            m_httpServer.AddStreamHandler(
                new RestDeserialisehandler<InventoryFolderBase, bool>(
                    "POST", "/NewFolder/", m_inventoryService.AddInventoryFolder));

            m_httpServer.AddStreamHandler(
                new RestDeserialisehandler<InventoryFolderBase, bool>(
                    "POST", "/MoveFolder/", m_inventoryService.MoveInventoryFolder));

            m_httpServer.AddStreamHandler(
                new RestDeserialisehandler<InventoryFolderBase, bool>(
                    "POST", "/PurgeFolder/", m_inventoryService.PurgeInventoryFolder));

            m_httpServer.AddStreamHandler(
                new RestDeserialisehandler<InventoryItemBase, bool>(
                    "POST", "/NewItem/", m_inventoryService.AddInventoryItem));

            m_httpServer.AddStreamHandler(
                new RestDeserialisehandler<InventoryItemBase, bool>(
                    "POST", "/DeleteItem/", m_inventoryService.DeleteInvItem));

            // WARNING: Root folders no longer just delivers the root and immediate child folders (e.g
            // system folders such as Objects, Textures), but it now returns the entire inventory skeleton.
            // It would have been better to rename this request, but complexities in the BaseHttpServer
            // (e.g. any http request not found is automatically treated as an xmlrpc request) make it easier
            // to do this for now.
            m_httpServer.AddStreamHandler(
                new RestDeserialisehandler<Guid, List<InventoryFolderBase>>
                    ("POST", "/RootFolders/", m_inventoryService.GetInventorySkeleton));

            // httpServer.AddStreamHandler(new InventoryManager.GetInventory(m_inventoryManager));
        }

        private void Work()
        {
            m_console.Notice("Enter help for a list of commands\n");

            while (true)
            {
                m_console.Prompt();
            }
        }
    }
}