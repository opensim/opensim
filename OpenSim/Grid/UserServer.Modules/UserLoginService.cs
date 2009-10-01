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
 *     * Neither the name of the OpenSimulator Project nor the
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
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using Nini.Config;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Services;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Grid.UserServer.Modules
{
    public delegate void UserLoggedInAtLocation(UUID agentID, UUID sessionID, UUID RegionID,
                                                ulong regionhandle, float positionX, float positionY, float positionZ,
                                                string firstname, string lastname);

    /// <summary>
    /// Login service used in grid mode.
    /// </summary>
    public class UserLoginService : LoginService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public event UserLoggedInAtLocation OnUserLoggedInAtLocation;

        private UserLoggedInAtLocation handlerUserLoggedInAtLocation;

        public UserConfig m_config;
        private readonly IRegionProfileRouter m_regionProfileService;

        private IGridService m_GridService;

        protected BaseHttpServer m_httpServer;

        public UserLoginService(
            UserManagerBase userManager, IInterServiceInventoryServices inventoryService,
            LibraryRootFolder libraryRootFolder,
            UserConfig config, string welcomeMess, IRegionProfileRouter regionProfileService)
            : base(userManager, libraryRootFolder, welcomeMess)
        {
            m_config = config;
            m_defaultHomeX = m_config.DefaultX;
            m_defaultHomeY = m_config.DefaultY;
            m_interInventoryService = inventoryService;
            m_regionProfileService = regionProfileService;

            m_GridService = new GridServicesConnector(config.GridServerURL.ToString());
        }

        public void RegisterHandlers(BaseHttpServer httpServer, bool registerLLSDHandler, bool registerOpenIDHandlers)
        {
            m_httpServer = httpServer;

            m_httpServer.AddXmlRPCHandler("login_to_simulator", XmlRpcLoginMethod);
            m_httpServer.AddHTTPHandler("login", ProcessHTMLLogin);
            m_httpServer.AddXmlRPCHandler("set_login_params", XmlRPCSetLoginParams);
            m_httpServer.AddXmlRPCHandler("check_auth_session", XmlRPCCheckAuthSession, false);

            if (registerLLSDHandler)
            {
                m_httpServer.SetDefaultLLSDHandler(LLSDLoginMethod);
            }

            if (registerOpenIDHandlers)
            {
                // Handler for OpenID avatar identity pages
                m_httpServer.AddStreamHandler(new OpenIdStreamHandler("GET", "/users/", this));
                // Handlers for the OpenID endpoint server
                m_httpServer.AddStreamHandler(new OpenIdStreamHandler("POST", "/openid/server/", this));
                m_httpServer.AddStreamHandler(new OpenIdStreamHandler("GET", "/openid/server/", this));
            }
        }

        public void setloginlevel(int level)
        {
            m_minLoginLevel = level;
            m_log.InfoFormat("[GRID]: Login Level set to {0} ", level);
        }
        public void setwelcometext(string text)
        {
            m_welcomeMessage = text;
            m_log.InfoFormat("[GRID]: Login text  set to {0} ", text);
        }

        public override void LogOffUser(UserProfileData theUser, string message)
        {
            RegionProfileData SimInfo;
            try
            {
                SimInfo = m_regionProfileService.RequestSimProfileData(
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
            SimParams["region_secret2"] = SimInfo.regionSecret;
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

            // Prepare notification
            SimParams = new Hashtable();
            SimParams["agent_id"] = theUser.ID.ToString();
            SimParams["region_secret"] = SimInfo.regionSecret;
            //m_log.Info(SimInfo.regionSecret);
            SimParams["regionhandle"] = theUser.CurrentAgent.Handle.ToString();
            SimParams["message"] = message;
            SendParams = new ArrayList();
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

        protected override RegionInfo RequestClosestRegion(string region)
        {
            return GridRegionToRegionInfo(m_GridService.GetRegionByName(UUID.Zero, region));
        }

        protected override RegionInfo GetRegionInfo(ulong homeRegionHandle)
        {
            uint x = 0, y = 0;
            Utils.LongToUInts(homeRegionHandle, out x, out y);
            return GridRegionToRegionInfo(m_GridService.GetRegionByPosition(UUID.Zero, (int)x, (int)y));
        }

        protected override RegionInfo GetRegionInfo(UUID homeRegionId)
        {
            return GridRegionToRegionInfo(m_GridService.GetRegionByUUID(UUID.Zero, homeRegionId));
        }

        private RegionInfo GridRegionToRegionInfo(GridRegion gregion)
        {
            if (gregion == null)
                return null;

            RegionInfo rinfo = new RegionInfo();
            rinfo.ExternalHostName = gregion.ExternalHostName;
            rinfo.HttpPort = gregion.HttpPort;
            rinfo.InternalEndPoint = gregion.InternalEndPoint;
            rinfo.RegionID = gregion.RegionID;
            rinfo.RegionLocX = (uint)(gregion.RegionLocX / Constants.RegionSize);
            rinfo.RegionLocY = (uint)(gregion.RegionLocY / Constants.RegionSize);
            rinfo.RegionName = gregion.RegionName;
            rinfo.ScopeID = gregion.ScopeID;
            rinfo.ServerURI = gregion.ServerURI;

            return rinfo;
        }

        protected override bool PrepareLoginToRegion(RegionInfo regionInfo, UserProfileData user, LoginResponse response, IPEndPoint remoteClient)
        {
            return PrepareLoginToRegion(RegionProfileData.FromRegionInfo(regionInfo), user, response, remoteClient);
        }

        /// <summary>
        /// Prepare a login to the given region.  This involves both telling the region to expect a connection
        /// and appropriately customising the response to the user.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="user"></param>
        /// <param name="response"></param>
        /// <returns>true if the region was successfully contacted, false otherwise</returns>
        private bool PrepareLoginToRegion(RegionProfileData regionInfo, UserProfileData user, LoginResponse response, IPEndPoint remoteClient)
        {
            try
            {
                response.SimAddress = Util.GetHostFromURL(regionInfo.serverURI).ToString();
                response.SimPort = uint.Parse(regionInfo.serverURI.Split(new char[] { '/', ':' })[4]);
                response.RegionX = regionInfo.regionLocX;
                response.RegionY = regionInfo.regionLocY;

                string capsPath = CapsUtil.GetRandomCapsObjectPath();

                // Adam's working code commented for now -- Diva 5/25/2009
                //// For NAT
                ////string host = NetworkUtil.GetHostFor(remoteClient.Address, regionInfo.ServerIP);
                //string host = response.SimAddress;
                //// TODO: This doesnt support SSL. -Adam
                //string serverURI = "http://" + host + ":" + regionInfo.ServerPort;

                //response.SeedCapability = serverURI + CapsUtil.GetCapsSeedPath(capsPath);

                // Take off trailing / so that the caps path isn't //CAPS/someUUID
                string uri = regionInfo.httpServerURI.Trim(new char[] { '/' });
                response.SeedCapability = uri + CapsUtil.GetCapsSeedPath(capsPath);


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
                loginParams["circuit_code"] = (Int32)Convert.ToUInt32(response.CircuitCode);
                loginParams["startpos_x"] = user.CurrentAgent.Position.X.ToString();
                loginParams["startpos_y"] = user.CurrentAgent.Position.Y.ToString();
                loginParams["startpos_z"] = user.CurrentAgent.Position.Z.ToString();
                loginParams["regionhandle"] = user.CurrentAgent.Handle.ToString();
                loginParams["caps_path"] = capsPath;

                // Get appearance
                AvatarAppearance appearance = m_userManager.GetUserAppearance(user.ID);
                if (appearance != null)
                {
                    loginParams["appearance"] = appearance.ToHashTable();
                    m_log.DebugFormat("[LOGIN]: Found appearance for {0} {1}", user.FirstName, user.SurName);
                }
                else
                {
                    m_log.DebugFormat("[LOGIN]: Appearance not for {0} {1}. Creating default.", user.FirstName, user.SurName);
                    appearance = new AvatarAppearance(user.ID);
                }

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
                        Hashtable resp = (Hashtable)GridResp.Value;
                        if (resp.ContainsKey("success"))
                        {
                            if ((string)resp["success"] == "FALSE")
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

        public XmlRpcResponse XmlRPCSetLoginParams(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            UserProfileData userProfile;
            Hashtable responseData = new Hashtable();

            UUID uid;
            string pass = requestData["password"].ToString();

            if (!UUID.TryParse((string)requestData["avatar_uuid"], out uid))
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
