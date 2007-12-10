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
* 
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Text;

using libsecondlife;

using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;

using InventoryManager = OpenSim.Grid.InventoryServer.InventoryManager;

namespace OpenSim.Grid.InventoryServer
{
    public class OpenInventory_Main : conscmd_callback
    {
        LogBase m_console;
        InventoryManager m_inventoryManager;
        InventoryConfig m_config;
        GridInventoryService m_inventoryService;

        public const string LogName = "INVENTORY";

        [STAThread]
        public static void Main(string[] args)
        {
            OpenInventory_Main theServer = new OpenInventory_Main();
            theServer.Startup();

            theServer.Work();
        }

        public OpenInventory_Main()
        {
            m_console = new LogBase("opengrid-inventory-console.log", LogName, this, true);
            MainLog.Instance = m_console;
        }

        public void Startup()
        {
            MainLog.Instance.Notice("Initialising inventory manager...");
            m_config = new InventoryConfig(LogName, (Path.Combine(Util.configDir(), "InventoryServer_Config.xml")));

            m_inventoryService = new GridInventoryService();
           // m_inventoryManager = new InventoryManager();
            m_inventoryService.AddPlugin(m_config.DatabaseProvider);

            MainLog.Instance.Notice(LogName, "Starting HTTP server ...");
            BaseHttpServer httpServer = new BaseHttpServer(m_config.HttpPort);
            httpServer.AddStreamHandler(
                new RestDeserialisehandler<LLUUID, InventoryCollection>("POST", "/GetInventory/",
                                                                                  m_inventoryService.GetUserInventory));
            httpServer.AddStreamHandler(
                new RestDeserialisehandler<LLUUID, bool>("POST", "/CreateInventory/",
                                                                                  m_inventoryService.CreateUsersInventory));
            httpServer.AddStreamHandler(
                new RestDeserialisehandler<InventoryFolderBase, bool>("POST", "/NewFolder/",
                                                                                  m_inventoryService.AddInventoryFolder));

            httpServer.AddStreamHandler(
                new RestDeserialisehandler<InventoryItemBase, bool>("POST", "/NewItem/",
                                                                                  m_inventoryService.AddInventoryItem));
            httpServer.AddStreamHandler(
                new RestDeserialisehandler<InventoryItemBase, bool>("POST", "/DeleteItem/",
                                                                                  m_inventoryService.DeleteInvItem));

            httpServer.AddStreamHandler(
                new RestDeserialisehandler<LLUUID, List<InventoryFolderBase>>("POST", "/RootFolders/",
                                                                                  m_inventoryService.RequestFirstLevelFolders));

          //  httpServer.AddStreamHandler(new InventoryManager.GetInventory(m_inventoryManager));

            httpServer.Start();
            MainLog.Instance.Notice(LogName, "Started HTTP server");
        }

        private void Work()
        {
            m_console.Notice("Enter help for a list of commands\n");

            while (true)
            {
                m_console.MainLogPrompt();
            }
        }

        public void RunCmd(string cmd, string[] cmdparams)
        {
            switch (cmd)
            {
                case "quit":
                case "add-user":
                    m_inventoryService.CreateUsersInventory(LLUUID.Random());
                    break;
                case "shutdown":
                    m_console.Close();
                    Environment.Exit(0);
                    break;
            }
        }

        public void Show(string ShowWhat)
        {
        }
    }
}
