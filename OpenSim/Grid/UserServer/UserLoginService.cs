using System;
using System.Collections;
using System.Net;
using Nwc.XmlRpc;
using OpenSim.Framework.Data;
using OpenSim.Framework.UserManagement;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Configuration;

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
            SimProfileData SimInfo = new SimProfileData();
            SimInfo = SimInfo.RequestSimProfileData(theUser.currentAgent.currentHandle, m_config.GridServerURL, m_config.GridSendKey, m_config.GridRecvKey);

            // Customise the response
            // Home Location
            response.Home = "{'region_handle':[r" + (SimInfo.regionLocX * 256).ToString() + ",r" + (SimInfo.regionLocY * 256).ToString() + "], " +
                "'position':[r" + theUser.homeLocation.X.ToString() + ",r" + theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "], " +
                "'look_at':[r" + theUser.homeLocation.X.ToString() + ",r" + theUser.homeLocation.Y.ToString() + ",r" + theUser.homeLocation.Z.ToString() + "]}";

            // Destination
            Console.WriteLine("CUSTOMISERESPONSE: Region X: " + SimInfo.regionLocX + "; Region Y: " + SimInfo.regionLocY);
            response.SimAddress = Util.GetHostFromDNS(SimInfo.serverIP).ToString();
            response.SimPort = (Int32)SimInfo.serverPort;
            response.RegionX = SimInfo.regionLocX;
            response.RegionY = SimInfo.regionLocY;

            //Not sure if the + "/CAPS/" should in fact be +"CAPS/" depending if there is already a / as part of httpServerURI
            string capsPath = Util.GetRandomCapsPath();
            response.SeedCapability = SimInfo.httpServerURI + "CAPS/" + capsPath + "0000/";

            // Notify the target of an incoming user
            Console.WriteLine("Notifying " + SimInfo.regionName + " (" + SimInfo.serverURI + ")");

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

            System.Console.WriteLine("Informing region --> " + SimInfo.httpServerURI);
            // Send
            XmlRpcRequest GridReq = new XmlRpcRequest("expect_user", SendParams);
            XmlRpcResponse GridResp = GridReq.Send(SimInfo.httpServerURI, 3000);
        }
    }
}

