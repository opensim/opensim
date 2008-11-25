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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using OpenMetaverse;
using log4net;
using Nwc.XmlRpc;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;

namespace OpenSim.Grid.UserServer
{
    public delegate void UserLoggedInAtLocation(UUID agentID, UUID sessionID, UUID RegionID,
                                                ulong regionhandle, float positionX, float positionY, float positionZ,
                                                string firstname, string lastname);

    public class UserLoginService : LoginService
    {
        protected static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected IInterServiceInventoryServices m_inventoryService;

        public event UserLoggedInAtLocation OnUserLoggedInAtLocation;

        private UserLoggedInAtLocation handlerUserLoggedInAtLocation;

        public UserConfig m_config;

        public UserLoginService(
            UserManagerBase userManager, IInterServiceInventoryServices inventoryService,
            LibraryRootFolder libraryRootFolder,
            UserConfig config, string welcomeMess)
            : base(userManager, libraryRootFolder, welcomeMess)
        {
            m_config = config;
            m_inventoryService = inventoryService;
        }
        public  void  setloginlevel(int level)
        {
              m_minLoginLevel = level;
              m_log.InfoFormat("[GRID] Login Level set to {0} ", level);

        }
        public void setwelcometext(string text)
        {
            m_welcomeMessage = text;
            m_log.InfoFormat("[GRID] Login text  set to {0} ", text);

        }

        public override void LogOffUser(UserProfileData theUser, string message)
        {
            RegionProfileData SimInfo;
            try
            {
                SimInfo = RegionProfileData.RequestSimProfileData(
                    theUser.CurrentAgent.Handle, m_config.GridServerURL,
                    m_config.GridSendKey, m_config.GridRecvKey);

                if (SimInfo == null)
                {
                    m_log.Error("[GRID]: Region user was in isn't currently logged in");
                    return;
                }
            }
            catch (Exception)
            {
                m_log.Error("[GRID]: Unable to look up region to log user off");
                return;
            }

            // Prepare notification
            Hashtable SimParams = new Hashtable();
            SimParams["agent_id"] = theUser.ID.ToString();
            SimParams["region_secret"] = theUser.CurrentAgent.SecureSessionID.ToString();
            //SimParams["region_secret"] = SimInfo.regionSecret;
            //m_log.Info(SimInfo.regionSecret);
            SimParams["regionhandle"] = theUser.CurrentAgent.Handle.ToString();
            SimParams["message"] = message;
            ArrayList SendParams = new ArrayList();
            SendParams.Add(SimParams);

            m_log.InfoFormat(
                "[ASSUMED CRASH]: Telling region {0} @ {1},{2} ({3}) that their agent is dead: {4}",
                SimInfo.regionName, SimInfo.regionLocX, SimInfo.regionLocY, SimInfo.httpServerURI,
                theUser.FirstName + " " + theUser.SurName);

            try
            {
                XmlRpcRequest GridReq = new XmlRpcRequest("logoff_user", SendParams);
                XmlRpcResponse GridResp = GridReq.Send(SimInfo.httpServerURI, 6000);

                if (GridResp.IsFault)
                {
                    m_log.ErrorFormat(
                        "[LOGIN]: XMLRPC request for {0} failed, fault code: {1}, reason: {2}, This is likely an old region revision.",
                        SimInfo.httpServerURI, GridResp.FaultCode, GridResp.FaultString);
                }
            }
            catch (Exception)
            {
                m_log.Error("[LOGIN]: Error telling region to logout user!");
            }

            //base.LogOffUser(theUser);
        }

        /// <summary>
        /// Customises the login response and fills in missing values.
        /// </summary>
        /// <param name="response">The existing response</param>
        /// <param name="theUser">The user profile</param>
        /// <param name="startLocationRequest">The requested start location</param>
        public override bool CustomiseResponse(LoginResponse response, UserProfileData theUser, string startLocationRequest)
        {
            // add active gestures to login-response
            AddActiveGestures(response, theUser);

            // HomeLocation
            RegionProfileData homeInfo = null;
            // use the homeRegionID if it is stored already. If not, use the regionHandle as before
            if (theUser.HomeRegionID != UUID.Zero)
                homeInfo = RegionProfileData.RequestSimProfileData(theUser.HomeRegionID,
                    m_config.GridServerURL, m_config.GridSendKey, m_config.GridRecvKey);
            else
                homeInfo = RegionProfileData.RequestSimProfileData(theUser.HomeRegion,
                    m_config.GridServerURL, m_config.GridSendKey, m_config.GridRecvKey);
            if (homeInfo != null)
            {
                response.Home =
                    string.Format(
                        "{{'region_handle':[r{0},r{1}], 'position':[r{2},r{3},r{4}], 'look_at':[r{5},r{6},r{7}]}}",
                        (homeInfo.regionLocX*Constants.RegionSize),
                        (homeInfo.regionLocY*Constants.RegionSize),
                        theUser.HomeLocation.X, theUser.HomeLocation.Y, theUser.HomeLocation.Z,
                        theUser.HomeLookAt.X, theUser.HomeLookAt.Y, theUser.HomeLookAt.Z);
            }
            else
            {
                // Emergency mode: Home-region isn't available, so we can't request the region info.
                // Use the stored home regionHandle instead.
                // NOTE: If the home-region moves, this will be wrong until the users update their user-profile again
                ulong regionX = theUser.HomeRegion >> 32;
                ulong regionY = theUser.HomeRegion & 0xffffffff;
                response.Home =
                    string.Format(
                        "{{'region_handle':[r{0},r{1}], 'position':[r{2},r{3},r{4}], 'look_at':[r{5},r{6},r{7}]}}",
                        regionX, regionY,
                        theUser.HomeLocation.X, theUser.HomeLocation.Y, theUser.HomeLocation.Z,
                        theUser.HomeLookAt.X, theUser.HomeLookAt.Y, theUser.HomeLookAt.Z);
                m_log.InfoFormat("[LOGIN] Home region of user {0} {1} is not available; using computed region position {2} {3}",
                                 theUser.FirstName, theUser.SurName,
                                 regionX, regionY);
            }

            // StartLocation
            RegionProfileData regionInfo = null;
            if (startLocationRequest == "home")
            {
                regionInfo = homeInfo;
                theUser.CurrentAgent.Position = theUser.HomeLocation;
                response.LookAt = "[r" + theUser.HomeLookAt.X.ToString() + ",r" + theUser.HomeLookAt.Y.ToString() + ",r" + theUser.HomeLookAt.Z.ToString() + "]";
            }
            else if (startLocationRequest == "last")
            {
                regionInfo = RegionProfileData.RequestSimProfileData(theUser.CurrentAgent.Region,
                    m_config.GridServerURL, m_config.GridSendKey, m_config.GridRecvKey);
                response.LookAt = "[r" + theUser.CurrentAgent.LookAt.X.ToString() + ",r" + theUser.CurrentAgent.LookAt.Y.ToString() + ",r" + theUser.CurrentAgent.LookAt.Z.ToString() + "]";
            }
            else
            {
                Regex reURI = new Regex(@"^uri:(?<region>[^&]+)&(?<x>\d+)&(?<y>\d+)&(?<z>\d+)$");
                Match uriMatch = reURI.Match(startLocationRequest);
                if (uriMatch == null)
                {
                    m_log.InfoFormat("[LOGIN]: Got Custom Login URL {0}, but can't process it", startLocationRequest);
                }
                else
                {
                    string region = uriMatch.Groups["region"].ToString();
                    regionInfo = RegionProfileData.RequestSimProfileData(region,
                        m_config.GridServerURL, m_config.GridSendKey, m_config.GridRecvKey);
                    if (regionInfo == null)
                    {
                        m_log.InfoFormat("[LOGIN]: Got Custom Login URL {0}, can't locate region {1}", startLocationRequest, region);
                    }
                    else
                    {
                        theUser.CurrentAgent.Position = new Vector3(float.Parse(uriMatch.Groups["x"].Value),
                            float.Parse(uriMatch.Groups["y"].Value), float.Parse(uriMatch.Groups["x"].Value));
                    }
                }
                response.LookAt = "[r0,r1,r0]";
                // can be: last, home, safe, url
                response.StartLocation = "url";
            }

            if ((regionInfo != null) && (PrepareLoginToRegion(regionInfo, theUser, response)))
            {
                return true;
            }

            // StartLocation not available, send him to a nearby region instead
            //regionInfo = RegionProfileData.RequestSimProfileData("", m_config.GridServerURL, m_config.GridSendKey, m_config.GridRecvKey);
            //m_log.InfoFormat("[LOGIN]: StartLocation not available sending to region {0}", regionInfo.regionName);

            // Send him to default region instead
            // Load information from the gridserver
            ulong defaultHandle = (((ulong) m_config.DefaultX * Constants.RegionSize) << 32) |
                                  ((ulong) m_config.DefaultY * Constants.RegionSize);

            if ((regionInfo != null) && (defaultHandle == regionInfo.regionHandle))
            {
                m_log.ErrorFormat("[LOGIN]: Not trying the default region since this is the same as the selected region");
                return false;
            }

            m_log.Error("[LOGIN]: Sending user to default region " + defaultHandle + " instead");
            regionInfo = RegionProfileData.RequestSimProfileData(defaultHandle, m_config.GridServerURL, m_config.GridSendKey, m_config.GridRecvKey);

            // Customise the response
            //response.Home =
            //    string.Format(
            //        "{{'region_handle':[r{0},r{1}], 'position':[r{2},r{3},r{4}], 'look_at':[r{5},r{6},r{7}]}}",
            //        (SimInfo.regionLocX * Constants.RegionSize),
            //        (SimInfo.regionLocY*Constants.RegionSize),
            //        theUser.HomeLocation.X, theUser.HomeLocation.Y, theUser.HomeLocation.Z,
            //        theUser.HomeLookAt.X, theUser.HomeLookAt.Y, theUser.HomeLookAt.Z);
            theUser.CurrentAgent.Position = new Vector3(128,128,0);
            response.StartLocation = "safe";

            return PrepareLoginToRegion(regionInfo, theUser, response);
        }

        /// <summary>
        /// Add active gestures of the user to the login response.
        /// </summary>
        /// <param name="response">
        /// A <see cref="LoginResponse"/>
        /// </param>
        /// <param name="theUser">
        /// A <see cref="UserProfileData"/>
        /// </param>
        private void AddActiveGestures(LoginResponse response, UserProfileData theUser)
        {
            List<InventoryItemBase> gestures = m_inventoryService.GetActiveGestures(theUser.ID);
            m_log.DebugFormat("[LOGIN]: AddActiveGestures, found {0}", gestures == null ? 0 : gestures.Count);
            ArrayList list = new ArrayList();
            if (gestures != null)
            {
                foreach (InventoryItemBase gesture in gestures)
                {
                    Hashtable item = new Hashtable();
                    item["item_id"] = gesture.ID.ToString();
                    item["asset_id"] = gesture.AssetID.ToString();
                    list.Add(item);
                }
            }
            response.ActiveGestures = list;
        }

        /// <summary>
        /// Prepare a login to the given region.  This involves both telling the region to expect a connection
        /// and appropriately customising the response to the user.
        /// </summary>
        /// <param name="sim"></param>
        /// <param name="user"></param>
        /// <param name="response"></param>
        /// <returns>true if the region was successfully contacted, false otherwise</returns>
        private bool PrepareLoginToRegion(RegionProfileData regionInfo, UserProfileData user, LoginResponse response)
        {
            try
            {                
                response.SimAddress = Util.GetHostFromURL(regionInfo.serverURI).ToString();
                response.SimPort = uint.Parse(regionInfo.serverURI.Split(new char[] { '/', ':' })[4]);
                response.RegionX = regionInfo.regionLocX;
                response.RegionY = regionInfo.regionLocY;

                //Not sure if the + "/CAPS/" should in fact be +"CAPS/" depending if there is already a / as part of httpServerURI
                string capsPath = Util.GetRandomCapsPath();
                response.SeedCapability = regionInfo.httpServerURI + "CAPS/" + capsPath + "0000/";

                // Notify the target of an incoming user
                m_log.InfoFormat(
                    "[LOGIN]: Telling {0} @ {1},{2} ({3}) to prepare for client connection",
                    regionInfo.regionName, response.RegionX, response.RegionY, regionInfo.httpServerURI);
                // Update agent with target sim
                user.CurrentAgent.Region = regionInfo.UUID;
                user.CurrentAgent.Handle = regionInfo.regionHandle;
                // Prepare notification
                Hashtable loginParams = new Hashtable();
                loginParams["session_id"] = user.CurrentAgent.SessionID.ToString();
                loginParams["secure_session_id"] = user.CurrentAgent.SecureSessionID.ToString();
                loginParams["firstname"] = user.FirstName;
                loginParams["lastname"] = user.SurName;
                loginParams["agent_id"] = user.ID.ToString();
                loginParams["circuit_code"] = (Int32) Convert.ToUInt32(response.CircuitCode);
                loginParams["startpos_x"] = user.CurrentAgent.Position.X.ToString();
                loginParams["startpos_y"] = user.CurrentAgent.Position.Y.ToString();
                loginParams["startpos_z"] = user.CurrentAgent.Position.Z.ToString();
                loginParams["regionhandle"] = user.CurrentAgent.Handle.ToString();
                loginParams["caps_path"] = capsPath;

                ArrayList SendParams = new ArrayList();
                SendParams.Add(loginParams);

                // Send
                XmlRpcRequest GridReq = new XmlRpcRequest("expect_user", SendParams);
                XmlRpcResponse GridResp = GridReq.Send(regionInfo.httpServerURI, 6000);

                if (!GridResp.IsFault)
                {
                    bool responseSuccess = true;

                    if (GridResp.Value != null)
                    {
                        Hashtable resp = (Hashtable) GridResp.Value;
                        if (resp.ContainsKey("success"))
                        {
                            if ((string) resp["success"] == "FALSE")
                            {
                                responseSuccess = false;
                            }
                        }
                    }
                    if (responseSuccess)
                    {
                        handlerUserLoggedInAtLocation = OnUserLoggedInAtLocation;
                        if (handlerUserLoggedInAtLocation != null)
                        {
                            handlerUserLoggedInAtLocation(user.ID, user.CurrentAgent.SessionID,
                                                          user.CurrentAgent.Region,
                                                          user.CurrentAgent.Handle,
                                                          user.CurrentAgent.Position.X,
                                                          user.CurrentAgent.Position.Y,
                                                          user.CurrentAgent.Position.Z,
                                                          user.FirstName, user.SurName);
                        }
                    }
                    else
                    {
                        m_log.ErrorFormat("[LOGIN]: Region responded that it is not available to receive clients");
                        return false;
                    }
                }
                else
                {
                    m_log.ErrorFormat("[LOGIN]: XmlRpc request to region failed with message {0}, code {1} ", GridResp.FaultString, GridResp.FaultCode);
                    return false;
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[LOGIN]: Region not available for login, {0}", e);
                return false;
            }

            return true;
        }

        // See LoginService
        protected override InventoryData GetInventorySkeleton(UUID userID)
        {
            m_log.DebugFormat(
                "[LOGIN]: Contacting inventory service at {0} for inventory skeleton of user {1}",
                m_config.InventoryUrl, userID);

            List<InventoryFolderBase> folders = m_inventoryService.GetInventorySkeleton(userID);

            if (null == folders || folders.Count == 0)
            {
                m_log.InfoFormat(
                    "[LOGIN]: A root inventory folder for user {0} was not found.  Requesting creation.", userID);

                // Although the create user function creates a new agent inventory along with a new user profile, some
                // tools are creating the user profile directly in the database without creating the inventory.  At
                // this time we'll accomodate them by lazily creating the user inventory now if it doesn't already
                // exist.
                if (!m_inventoryService.CreateNewUserInventory(userID))
                {
                    throw new Exception(
                        String.Format(
                            "The inventory creation request for user {0} did not succeed."
                            + "  Please contact your inventory service provider for more information.",
                            userID));
                }
                m_log.InfoFormat("[LOGIN]: A new inventory skeleton was successfully created for user {0}", userID);

                folders = m_inventoryService.GetInventorySkeleton(userID);
            }

            if (folders != null && folders.Count > 0)
            {
                UUID rootID = UUID.Zero;
                ArrayList AgentInventoryArray = new ArrayList();
                Hashtable TempHash;

                foreach (InventoryFolderBase InvFolder in folders)
                {
//                    m_log.DebugFormat("[LOGIN]: Received agent inventory folder {0}", InvFolder.name);

                    if (InvFolder.ParentID == UUID.Zero)
                    {
                        rootID = InvFolder.ID;
                    }
                    TempHash = new Hashtable();
                    TempHash["name"] = InvFolder.Name;
                    TempHash["parent_id"] = InvFolder.ParentID.ToString();
                    TempHash["version"] = (Int32) InvFolder.Version;
                    TempHash["type_default"] = (Int32) InvFolder.Type;
                    TempHash["folder_id"] = InvFolder.ID.ToString();
                    AgentInventoryArray.Add(TempHash);
                }

                return new InventoryData(AgentInventoryArray, rootID);
            }
            throw new Exception(
                String.Format(
                    "A root inventory folder for user {0} could not be retrieved from the inventory service",
                    userID));
        }

        public XmlRpcResponse XmlRPCSetLoginParams(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable) request.Params[0];
            UserProfileData userProfile;
            Hashtable responseData = new Hashtable();

            UUID uid;
            string pass = requestData["password"].ToString();

            if (!UUID.TryParse((string) requestData["avatar_uuid"], out uid))
            {
                responseData["error"] = "No authorization";
                response.Value = responseData;
                return response;
            }

            userProfile = m_userManager.GetUserProfile(uid);

            if (userProfile == null ||
                (!AuthenticateUser(userProfile, pass)) ||
                userProfile.GodLevel < 200)
            {
                responseData["error"] = "No authorization";
                response.Value = responseData;
                return response;
            }

            if (requestData.ContainsKey("login_level"))
            {
                m_minLoginLevel = Convert.ToInt32(requestData["login_level"]);
            }

            if (requestData.ContainsKey("login_motd"))
            {
                m_welcomeMessage = requestData["login_motd"].ToString();
            }

            response.Value = responseData;
            return response;
        }

    }
}
