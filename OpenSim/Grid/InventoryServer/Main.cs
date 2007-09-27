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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using System.Xml;

using libsecondlife;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Console;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Configuration;
using OpenSim.Framework.Data;
using InventoryCategory = OpenSim.Framework.Data.InventoryCategory;

namespace OpenSim.Grid.InventoryServer
{

    public class OpenInventory_Main : conscmd_callback
    {

        public const string MainLogName = "INVENTORY";

        private InventoryConfig m_config;
        LogBase m_console;
        InventoryManager m_inventoryManager;    ///connection the database backend
        InventoryService m_inventoryService;    ///Remoting interface, where messages arrive
        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("Launching InventoryServer...");

            OpenInventory_Main inventoryServer = new OpenInventory_Main();

            inventoryServer.Startup();

//            inventoryServer.RunCmd("load", new string[] { "library", "inventory_library.xml" });
//            inventoryServer.RunCmd("load", new string[] { "default", "inventory_default.xml" });

            inventoryServer.Work();
        }

        public OpenInventory_Main()
        {

            if (!Directory.Exists(Util.logDir()))
            {
                Directory.CreateDirectory(Util.logDir());
            }

            m_console = new LogBase(Path.Combine(Util.logDir(), "opensim-inventory-console.log"), "OpenInventory", this, false);
            MainLog.Instance = m_console;
        }

        private void Work()
        {
            m_console.Notice(OpenInventory_Main.MainLogName, "Enter help for a list of commands\n");

            while (true)
            {
                m_console.MainLogPrompt();
            }
        }

        public void Startup()
        {
            MainLog.Instance.Verbose(OpenInventory_Main.MainLogName, "Initialising inventory manager...");

            m_config = new InventoryConfig(OpenInventory_Main.MainLogName, (Path.Combine(Util.configDir(), "InventoryServer_Config.xml")));
            
            // instantiate the manager, responsible for doing the actual storage
            m_inventoryManager = new InventoryManager();
            m_inventoryManager.AddDatabasePlugin(m_config.DatabaseProvider);

            m_inventoryService = new InventoryService(m_inventoryManager, m_config);

            // Dig out the embedded version number of this assembly
            Assembly assembly = Assembly.GetExecutingAssembly();
            string serverExeName = assembly.ManifestModule.Name;
            Version serverExeVersion = AssemblyName.GetAssemblyName(serverExeName).Version;

            m_console.Status(OpenInventory_Main.MainLogName, "Inventoryserver {0}.{1}.{2}.{3} - Startup complete", serverExeVersion.Major, serverExeVersion.Minor, serverExeVersion.Revision, serverExeVersion.Build);
        }


        public void Load(string[] cmdparams)
        {
            string cmd = cmdparams[0];
            string fileName = cmdparams[1];

            if (cmdparams.Length != 2)
            {
                cmd = "help"; 
            }

            switch (cmd)
            {
                case "library":
                    InventoryServiceSingleton.Instance.loadInventoryFromXmlFile(InventoryCategory.Library, fileName);
                    break;
                case "default":
                    InventoryServiceSingleton.Instance.loadInventoryFromXmlFile(InventoryCategory.Default, fileName);
                    break;
                case "user":
                    InventoryServiceSingleton.Instance.loadInventoryFromXmlFile(InventoryCategory.User, fileName);
                    break;
                case "help":
                    m_console.Notice("load library <filename>, load library inventory, shared between all users");
                    m_console.Notice("load default <filename>, load template inventory, used when creating a new user inventory");
                    m_console.Notice("load user <first> <last>, load inventory for a specific users, warning this will reset the contents of the inventory");
                    break;
            }
        }

        public void RunCmd(string cmd, string[] cmdparams)
        {
            switch (cmd)
            {   
                case "help":
                    m_console.Notice("load - load verious inventories, use \"load help\", to see a list of commands");
                    m_console.Notice("shutdown - shutdown the grid (USE CAUTION!)");
                    m_console.Notice("quit - shutdown the grid (USE CAUTION!)");
                    break;
                case "load":
                    Load(cmdparams);
                    break;
                case "quit":
                case "shutdown":
                    MainLog.Instance.Verbose(OpenInventory_Main.MainLogName, "Shutting down inventory server");
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
