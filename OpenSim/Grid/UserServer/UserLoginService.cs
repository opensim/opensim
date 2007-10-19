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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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
using System.Net;
using Nwc.XmlRpc;
using OpenSim.Framework.Data;
using OpenSim.Framework.UserManagement;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Configuration;
using OpenSim.Framework.Types;
using OpenSim.Framework.Console;

namespace OpenSim.Grid.UserServer
{
    public class UserLoginService : LoginService
    {
        public UserConfig m_config;

        public UserLoginService(UserManagerBase userManager, UserConfig config, string welcomeMess)
            : base(userManager, welcomeMess)
        {
            m_config = config;
        }

        /// <summary>
        /// Customises the login response and fills in missing values.
        /// </summary>
        /// <param name="response">The existing response</param>
        /// <param name="theUser">The user profile</param>
        public override void CustomiseResponse(LoginResponse response, UserProfileData theUser)
        {
            // Load information from the gridserver
            RegionProfileData SimInfo = new RegionProfileData();
            SimInfo = SimInfo.RequestSimProfileData(theUser.currentAgent.currentHandle, m_config.GridServerURL, m_config.GridSendKey, m_config.GridRecvKey);

            // Customise the response
            // Home Location
            response.Home = "{'region_handle':[r" + (SimInfo.regionLocX * 256).ToString() + ",r" + (SimInfo.regionLocY * 256).ToString() + "], " +
                "'position':[r" + theUser.homeLocation.X.ToString() + ",r" + theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "], " +
                "'look_at':[r" + theUser.homeLocation.X.ToString() + ",r" + theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "]}";

            // Destination
            MainLog.Instance.Verbose("CUSTOMISERESPONSE: Region X: " + SimInfo.regionLocX + "; Region Y: " + SimInfo.regionLocY);
            response.SimAddress = Util.GetHostFromDNS(SimInfo.serverIP).ToString();
            response.SimPort = (Int32)SimInfo.serverPort;
            response.RegionX = SimInfo.regionLocX;
            response.RegionY = SimInfo.regionLocY;

            //Not sure if the + "/CAPS/" should in fact be +"CAPS/" depending if there is already a / as part of httpServerURI
            string capsPath = Util.GetRandomCapsPath();
            response.SeedCapability = SimInfo.httpServerURI + "CAPS/" + capsPath + "0000/";

            // Notify the target of an incoming user
            MainLog.Instance.Verbose("Notifying " + SimInfo.regionName + " (" + SimInfo.serverURI + ")");

            // Prepare notification
            Hashtable SimParams = new Hashtable();
            SimParams["session_id"] = theUser.currentAgent.sessionID.ToString();
            SimParams["secure_session_id"] = theUser.currentAgent.secureSessionID.ToString();
            SimParams["firstname"] = theUser.username;
            SimParams["lastname"] = theUser.surname;
            SimParams["agent_id"] = theUser.UUID.ToString();
            SimParams["circuit_code"] = (Int32)Convert.ToUInt32(response.CircuitCode);
            SimParams["startpos_x"] = theUser.currentAgent.currentPos.X.ToString();
            SimParams["startpos_y"] = theUser.currentAgent.currentPos.Y.ToString();
            SimParams["startpos_z"] = theUser.currentAgent.currentPos.Z.ToString();
            SimParams["regionhandle"] = theUser.currentAgent.currentHandle.ToString();
            SimParams["caps_path"] = capsPath;
            ArrayList SendParams = new ArrayList();
            SendParams.Add(SimParams);

            // Update agent with target sim
            theUser.currentAgent.currentRegion = SimInfo.UUID;
            theUser.currentAgent.currentHandle = SimInfo.regionHandle;

            MainLog.Instance.Verbose("Informing region --> " + SimInfo.httpServerURI);
            // Send
            try
            {
                XmlRpcRequest GridReq = new XmlRpcRequest("expect_user", SendParams);
                XmlRpcResponse GridResp = GridReq.Send(SimInfo.httpServerURI, 6000);
            }
            catch( WebException e )
            {
                switch( e.Status )
                {
                    case WebExceptionStatus.Timeout:
                        //TODO: Send him to nearby or default region instead
			MainLog.Instance.Verbose("Unable to connect to " + SimInfo.regionName + " (" + SimInfo.serverURI + ")");
                        break;
                       
                    default:
                        throw;
                }
            }
        }
    }
}


