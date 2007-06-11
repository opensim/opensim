/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
using System.Collections.Generic;
using System.Collections;
using System.Text;
using OpenSim.Framework.User;
using OpenSim.Framework.Grid;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using libsecondlife;

namespace OpenSim.UserServer
{
    public class LocalUserProfileManager : UserProfileManager 
    {
        private IGridServer m_gridServer;
        private int m_port;
        private string m_ipAddr;
        private uint regionX;
        private uint regionY;
        private AddNewSessionHandler AddSession;

        public LocalUserProfileManager(IGridServer gridServer, int simPort, string ipAddr , uint regX, uint regY)
		{
			m_gridServer = gridServer;
            m_port = simPort;
            m_ipAddr = ipAddr;
            regionX = regX;
            regionY = regY;
		}

        public void SetSessionHandler(AddNewSessionHandler sessionHandler)
        {
            this.AddSession = sessionHandler;
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
            response["region_y"] = (Int32)regionY* 256;
            response["region_x"] = (Int32)regionX* 256;

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
            _login.CircuitCode =(uint) circode;
            _login.BaseFolder = null;
            _login.InventoryFolder = new LLUUID((string)Inventory1["folder_id"]);

            //working on local computer if so lets add to the gridserver's list of sessions?
            /*if (m_gridServer.GetName() == "Local")
            {
                Console.WriteLine("adding login data to gridserver");
                ((LocalGridBase)this.m_gridServer).AddNewSession(_login);
            }*/
            ulong reghand = Helpers.UIntsToLong((regionX * 256), (regionY * 256));
            this.AddSession(reghand, _login);
        }
    }
}
