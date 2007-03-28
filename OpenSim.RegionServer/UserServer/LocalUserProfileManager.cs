using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using OpenSim.Framework.User;
using OpenSim.Framework.Grid;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Interfaces;
using libsecondlife;

namespace OpenSim.UserServer
{
    class LocalUserProfileManager : UserProfileManager 
    {
        private IGridServer m_gridServer;
        private int m_port;
        private string m_ipAddr;

        public LocalUserProfileManager(IGridServer gridServer, int simPort, string ipAddr)
		{
			m_gridServer = gridServer;
            m_port = simPort;
            m_ipAddr = ipAddr;
		}

        public override void InitUserProfiles()
        {
            // TODO: need to load from database
        }

        public override void CustomiseResponse(ref System.Collections.Hashtable response, UserProfile theUser)
        {
            Int32 circode = (Int32)response["circuit_code"];
            theUser.AddSimCircuit((uint)circode, LLUUID.Random());
            response["home"] = "{'region_handle':[r" + (997 * 256).ToString() + ",r" + (996 * 256).ToString() + "], 'position':[r" + theUser.homepos.X.ToString() + ",r" + theUser.homepos.Y.ToString() + ",r" + theUser.homepos.Z.ToString() + "], 'look_at':[r" + theUser.homelookat.X.ToString() + ",r" + theUser.homelookat.Y.ToString() + ",r" + theUser.homelookat.Z.ToString() + "]}";
            response["sim_port"] = m_port;
            response["sim_ip"] = m_ipAddr;
            response["region_y"] = (Int32)996 * 256;
            response["region_x"] = (Int32)997* 256;

            string first;
            string last;
            if (response.Contains("first_name"))
            {
                first = (string)response["first_name"];
            }
            else
            {
                first = "test";
            }

            if (response.Contains("last_name"))
            {
                last = (string)response["last_name"];
            }
            else
            {
                last = "User";
            }

            ArrayList InventoryList = (ArrayList)response["inventory-skeleton"];
            Hashtable Inventory1 = (Hashtable)InventoryList[0];

            Login _login = new Login();
            //copy data to login object
            _login.First = first;
            _login.Last = last;
            _login.Agent = new LLUUID((string)response["agent_id"]) ;
            _login.Session = new LLUUID((string)response["session_id"]);
            _login.SecureSession = new LLUUID((string)response["secure_session_id"]);
            _login.BaseFolder = null;
            _login.InventoryFolder = new LLUUID((string)Inventory1["folder_id"]);

            //working on local computer if so lets add to the gridserver's list of sessions?
            if (m_gridServer.GetName() == "Local")
            {
                Console.WriteLine("adding login data to gridserver");
                ((LocalGridBase)this.m_gridServer).AddNewSession(_login);
            }
        }
    }
}
