
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Text;
using libsecondlife;
using OpenSim.Framework.User;
using OpenSim.Framework.Sims;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Console;
using OpenSim.Servers;
using OpenSim.Framework.Utilities;

namespace OpenGridServices.InventoryServer
{
    public class OpenInventory_Main : BaseServer, conscmd_callback
    {
        ConsoleBase m_console;
        InventoryManager m_inventoryManager;

        public static void Main(string[] args)
        {
        }

        public OpenInventory_Main()
        {
            m_console = new ConsoleBase("opengrid-inventory-console.log", "OpenInventory", this, false);
            MainConsole.Instance = m_console;
        }

        public void Startup()
        {
            MainConsole.Instance.Notice("Initialising inventory manager...");
            m_inventoryManager = new InventoryManager();

            MainConsole.Instance.Notice("Starting HTTP server");
            BaseHttpServer httpServer = new BaseHttpServer(8004);

            httpServer.AddXmlRPCHandler("rootfolders", m_inventoryManager.XmlRpcInventoryRequest);
            //httpServer.AddRestHandler("GET","/rootfolders/",Rest
        }

        public void RunCmd(string cmd, string[] cmdparams)
        {
            switch (cmd)
            {
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
