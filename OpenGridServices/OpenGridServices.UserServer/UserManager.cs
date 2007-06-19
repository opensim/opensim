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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenGrid.Framework.Data;
using libsecondlife;
using System.Reflection;

using System.Xml;
using Nwc.XmlRpc;
using OpenSim.Framework.Sims;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Utilities;
using OpenGrid.Framework.UserManagement;

using System.Security.Cryptography;

namespace OpenGridServices.UserServer
{
    public class UserManager : UserManagerBase
    {
        public UserManager()
        {
        }

        /// <summary>
        /// Customises the login response and fills in missing values.
        /// </summary>
        /// <param name="response">The existing response</param>
        /// <param name="theUser">The user profile</param>
        public override void CustomiseResponse(ref Hashtable response, ref UserProfileData theUser)
        {
            // Load information from the gridserver
            SimProfile SimInfo = new SimProfile();
            SimInfo = SimInfo.LoadFromGrid(theUser.currentAgent.currentHandle, _config.GridServerURL, _config.GridSendKey, _config.GridRecvKey);

            // Customise the response
            // Home Location
            response["home"] = "{'region_handle':[r" + (SimInfo.RegionLocX * 256).ToString() + ",r" + (SimInfo.RegionLocY * 256).ToString() + "], " +
                "'position':[r" + theUser.homeLocation.X.ToString() + ",r" + theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "], " +
                "'look_at':[r" + theUser.homeLocation.X.ToString() + ",r" + theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "]}";

            // Destination
            response["sim_ip"] = SimInfo.sim_ip;
            response["sim_port"] = (Int32)SimInfo.sim_port;
            response["region_y"] = (Int32)SimInfo.RegionLocY * 256;
            response["region_x"] = (Int32)SimInfo.RegionLocX * 256;

            // Notify the target of an incoming user
            Console.WriteLine("Notifying " + SimInfo.regionname + " (" + SimInfo.caps_url + ")");

            // Prepare notification
            Hashtable SimParams = new Hashtable();
            SimParams["session_id"] = theUser.currentAgent.sessionID.ToString();
            SimParams["secure_session_id"] = theUser.currentAgent.secureSessionID.ToString();
            SimParams["firstname"] = theUser.username;
            SimParams["lastname"] = theUser.surname;
            SimParams["agent_id"] = theUser.UUID.ToString();
            SimParams["circuit_code"] = (Int32)Convert.ToUInt32(response["circuit_code"]);
            SimParams["startpos_x"] = theUser.currentAgent.currentPos.X.ToString();
            SimParams["startpos_y"] = theUser.currentAgent.currentPos.Y.ToString();
            SimParams["startpos_z"] = theUser.currentAgent.currentPos.Z.ToString();
            ArrayList SendParams = new ArrayList();
            SendParams.Add(SimParams);

            // Update agent with target sim
            theUser.currentAgent.currentRegion = SimInfo.UUID;
            theUser.currentAgent.currentHandle = SimInfo.regionhandle;

            // Send
            XmlRpcRequest GridReq = new XmlRpcRequest("expect_user", SendParams);
            XmlRpcResponse GridResp = GridReq.Send(SimInfo.caps_url, 3000);
        }
    }
}
