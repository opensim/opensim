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
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Grid.Framework;

namespace OpenSim.Grid.UserServer.Modules
{
    public delegate void logOffUser(UUID AgentID);

    public class UserManager
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public event logOffUser OnLogOffUser;
        private logOffUser handlerLogOffUser;

        private UserDataBaseService m_userDataBaseService;
        private BaseHttpServer m_httpServer;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userDataBaseService"></param>
        public UserManager(UserDataBaseService userDataBaseService)
        {
            m_userDataBaseService = userDataBaseService;
        }

        public void Initialise(IGridServiceCore core)
        {

        }

        public void PostInitialise()
        {

        }

        private string RESTGetUserProfile(string request, string path, string param, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            UUID id;
            UserProfileData userProfile;

            try
            {
                id = new UUID(param);
            }
            catch (Exception)
            {
                httpResponse.StatusCode = 500;
                return "Malformed Param [" + param + "]";
            }

            userProfile = m_userDataBaseService.GetUserProfile(id);

            if (userProfile == null)
            {
                httpResponse.StatusCode = 404;
                return "Not Found.";
            }

            return ProfileToXmlRPCResponse(userProfile).ToString();
        }

        public void RegisterHandlers(BaseHttpServer httpServer)
        {
            m_httpServer = httpServer;

            m_httpServer.AddStreamHandler(new RestStreamHandler("GET", "/users/", RESTGetUserProfile));

            m_httpServer.AddXmlRPCHandler("get_user_by_name", XmlRPCGetUserMethodName);
            m_httpServer.AddXmlRPCHandler("get_user_by_uuid", XmlRPCGetUserMethodUUID);
            m_httpServer.AddXmlRPCHandler("get_avatar_picker_avatar", XmlRPCGetAvatarPickerAvatar);

            // Used by IAR module to do password checks
            m_httpServer.AddXmlRPCHandler("authenticate_user_by_password", XmlRPCAuthenticateUserMethodPassword);

            m_httpServer.AddXmlRPCHandler("update_user_current_region", XmlRPCAtRegion);
            m_httpServer.AddXmlRPCHandler("logout_of_simulator", XmlRPCLogOffUserMethodUUID);
            m_httpServer.AddXmlRPCHandler("get_agent_by_uuid", XmlRPCGetAgentMethodUUID);

            m_httpServer.AddXmlRPCHandler("update_user_profile", XmlRpcResponseXmlRPCUpdateUserProfile);

            m_httpServer.AddStreamHandler(new RestStreamHandler("DELETE", "/usersessions/", RestDeleteUserSessionMethod));
        }

        /// <summary>
        /// Deletes an active agent session
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="path">The path (eg /bork/narf/test)</param>
        /// <param name="param">Parameters sent</param>
        /// <param name="httpRequest">HTTP request header object</param>
        /// <param name="httpResponse">HTTP response header object</param>
        /// <returns>Success "OK" else error</returns>
        public string RestDeleteUserSessionMethod(string request, string path, string param,
                                                  OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            // TODO! Important!

            return "OK";
        }

        public XmlRpcResponse AvatarPickerListtoXmlRPCResponse(UUID queryID, List<AvatarPickerAvatar> returnUsers)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            // Query Result Information
            responseData["queryid"] = queryID.ToString();
            responseData["avcount"] = returnUsers.Count.ToString();

            for (int i = 0; i < returnUsers.Count; i++)
            {
                responseData["avatarid" + i] = returnUsers[i].AvatarID.ToString();
                responseData["firstname" + i] = returnUsers[i].firstName;
                responseData["lastname" + i] = returnUsers[i].lastName;
            }
            response.Value = responseData;

            return response;
        }

        /// <summary>
        /// Converts a user profile to an XML element which can be returned
        /// </summary>
        /// <param name="profile">The user profile</param>
        /// <returns>A string containing an XML Document of the user profile</returns>
        public XmlRpcResponse ProfileToXmlRPCResponse(UserProfileData profile)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();

            // Account information
            responseData["firstname"] = profile.FirstName;
            responseData["lastname"] = profile.SurName;
            responseData["email"] = profile.Email;
            responseData["uuid"] = profile.ID.ToString();
            // Server Information
            responseData["server_inventory"] = profile.UserInventoryURI;
            responseData["server_asset"] = profile.UserAssetURI;
            // Profile Information
            responseData["profile_about"] = profile.AboutText;
            responseData["profile_firstlife_about"] = profile.FirstLifeAboutText;
            responseData["profile_firstlife_image"] = profile.FirstLifeImage.ToString();
            responseData["profile_can_do"] = profile.CanDoMask.ToString();
            responseData["profile_want_do"] = profile.WantDoMask.ToString();
            responseData["profile_image"] = profile.Image.ToString();
            responseData["profile_created"] = profile.Created.ToString();
            responseData["profile_lastlogin"] = profile.LastLogin.ToString();
            // Home region information
            responseData["home_coordinates_x"] = profile.HomeLocation.X.ToString();
            responseData["home_coordinates_y"] = profile.HomeLocation.Y.ToString();
            responseData["home_coordinates_z"] = profile.HomeLocation.Z.ToString();

            responseData["home_region"] = profile.HomeRegion.ToString();
            responseData["home_region_id"] = profile.HomeRegionID.ToString();

            responseData["home_look_x"] = profile.HomeLookAt.X.ToString();
            responseData["home_look_y"] = profile.HomeLookAt.Y.ToString();
            responseData["home_look_z"] = profile.HomeLookAt.Z.ToString();

            responseData["user_flags"] = profile.UserFlags.ToString();
            responseData["god_level"] = profile.GodLevel.ToString();
            responseData["custom_type"] = profile.CustomType;
            responseData["partner"] = profile.Partner.ToString();
            response.Value = responseData;

            return response;
        }

        #region XMLRPC User Methods

        /// <summary>
        /// Authenticate a user using their password
        /// </summary>
        /// <param name="request">Must contain values for "user_uuid" and "password" keys</param>
        /// <param name="remoteClient"></param>
        /// <returns></returns>
        public XmlRpcResponse XmlRPCAuthenticateUserMethodPassword(XmlRpcRequest request, IPEndPoint remoteClient)
        {
//            m_log.DebugFormat("[USER MANAGER]: Received authenticated user by password request from {0}", remoteClient);
            
            Hashtable requestData = (Hashtable)request.Params[0];
            string userUuidRaw = (string)requestData["user_uuid"];
            string password = (string)requestData["password"];

            if (null == userUuidRaw)
                return Util.CreateUnknownUserErrorResponse();

            UUID userUuid;
            if (!UUID.TryParse(userUuidRaw, out userUuid))
                return Util.CreateUnknownUserErrorResponse();

            UserProfileData userProfile = m_userDataBaseService.GetUserProfile(userUuid);
            if (null == userProfile)
                return Util.CreateUnknownUserErrorResponse();

            string authed;
            
            if (null == password)
            {
                authed = "FALSE";
            }
            else
            {
                if (m_userDataBaseService.AuthenticateUserByPassword(userUuid, password))
                    authed = "TRUE";
                else
                    authed = "FALSE";
            }

//            m_log.DebugFormat(
//                "[USER MANAGER]: Authentication by password result from {0} for {1} is {2}",
//                remoteClient, userUuid, authed);

            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            responseData["auth_user"] = authed;
            response.Value = responseData;
            
            return response;
        }

        public XmlRpcResponse XmlRPCGetAvatarPickerAvatar(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            List<AvatarPickerAvatar> returnAvatar = new List<AvatarPickerAvatar>();
            UUID queryID = new UUID(UUID.Zero.ToString());

            if (requestData.Contains("avquery") && requestData.Contains("queryid"))
            {
                queryID = new UUID((string)requestData["queryid"]);
                returnAvatar = m_userDataBaseService.GenerateAgentPickerRequestResponse(queryID, (string)requestData["avquery"]);
            }

            m_log.InfoFormat("[AVATARINFO]: Servicing Avatar Query: " + (string)requestData["avquery"]);
            return AvatarPickerListtoXmlRPCResponse(queryID, returnAvatar);
        }

        public XmlRpcResponse XmlRPCAtRegion(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();
            string returnstring = "FALSE";

            if (requestData.Contains("avatar_id") && requestData.Contains("region_handle") &&
                requestData.Contains("region_uuid"))
            {
                // ulong cregionhandle = 0;
                UUID regionUUID;
                UUID avatarUUID;

                UUID.TryParse((string)requestData["avatar_id"], out avatarUUID);
                UUID.TryParse((string)requestData["region_uuid"], out regionUUID);

                if (avatarUUID != UUID.Zero)
                {
                    UserProfileData userProfile = m_userDataBaseService.GetUserProfile(avatarUUID);
                    userProfile.CurrentAgent.Region = regionUUID;
                    userProfile.CurrentAgent.Handle = (ulong)Convert.ToInt64((string)requestData["region_handle"]);
                    //userProfile.CurrentAgent.
                    m_userDataBaseService.CommitAgent(ref userProfile);
                    //setUserProfile(userProfile);

                    returnstring = "TRUE";
                }
            }
                
            responseData.Add("returnString", returnstring);
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRPCGetUserMethodName(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            UserProfileData userProfile;
            if (requestData.Contains("avatar_name"))
            {
                string query = (string)requestData["avatar_name"];

                if (null == query)
                    return Util.CreateUnknownUserErrorResponse();

                // Regex objAlphaNumericPattern = new Regex("[^a-zA-Z0-9]");

                string[] querysplit = query.Split(' ');

                if (querysplit.Length == 2)
                {
                    userProfile = m_userDataBaseService.GetUserProfile(querysplit[0], querysplit[1]);
                    if (userProfile == null)
                    {
                        return Util.CreateUnknownUserErrorResponse();
                    }
                }
                else
                {
                    return Util.CreateUnknownUserErrorResponse();
                }
            }
            else
            {
                return Util.CreateUnknownUserErrorResponse();
            }

            return ProfileToXmlRPCResponse(userProfile);
        }

        public XmlRpcResponse XmlRPCGetUserMethodUUID(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            UserProfileData userProfile;
            //CFK: this clogs the UserServer log and is not necessary at this time.
            //CFK: m_log.Debug("METHOD BY UUID CALLED");
            if (requestData.Contains("avatar_uuid"))
            {
                try
                {
                    UUID guess = new UUID((string)requestData["avatar_uuid"]);

                    userProfile = m_userDataBaseService.GetUserProfile(guess);
                }
                catch (FormatException)
                {
                    return Util.CreateUnknownUserErrorResponse();
                }

                if (userProfile == null)
                {
                    return Util.CreateUnknownUserErrorResponse();
                }
            }
            else
            {
                return Util.CreateUnknownUserErrorResponse();
            }

            return ProfileToXmlRPCResponse(userProfile);
        }

        public XmlRpcResponse XmlRPCGetAgentMethodUUID(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            UserProfileData userProfile;
            //CFK: this clogs the UserServer log and is not necessary at this time.
            //CFK: m_log.Debug("METHOD BY UUID CALLED");
            if (requestData.Contains("avatar_uuid"))
            {
                UUID guess;

                UUID.TryParse((string)requestData["avatar_uuid"], out guess);

                if (guess == UUID.Zero)
                {
                    return Util.CreateUnknownUserErrorResponse();
                }

                userProfile = m_userDataBaseService.GetUserProfile(guess);

                if (userProfile == null)
                {
                    return Util.CreateUnknownUserErrorResponse();
                }

                // no agent???
                if (userProfile.CurrentAgent == null)
                {
                    return Util.CreateUnknownUserErrorResponse();
                }
                Hashtable responseData = new Hashtable();

                responseData["handle"] = userProfile.CurrentAgent.Handle.ToString();
                responseData["session"] = userProfile.CurrentAgent.SessionID.ToString();
                if (userProfile.CurrentAgent.AgentOnline)
                    responseData["agent_online"] = "TRUE";
                else
                    responseData["agent_online"] = "FALSE";

                response.Value = responseData;
            }
            else
            {
                return Util.CreateUnknownUserErrorResponse();
            }

            return response;
        }

        public XmlRpcResponse XmlRpcResponseXmlRPCUpdateUserProfile(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            m_log.Debug("[UserManager]: Got request to update user profile");
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();

            if (!requestData.Contains("avatar_uuid"))
            {
                return Util.CreateUnknownUserErrorResponse();
            }

            UUID UserUUID = new UUID((string)requestData["avatar_uuid"]);
            UserProfileData userProfile = m_userDataBaseService.GetUserProfile(UserUUID);
            if (null == userProfile)
            {
                return Util.CreateUnknownUserErrorResponse();
            }
            // don't know how yet.
            if (requestData.Contains("AllowPublish"))
            {
            }
            if (requestData.Contains("FLImageID"))
            {
                userProfile.FirstLifeImage = new UUID((string)requestData["FLImageID"]);
            }
            if (requestData.Contains("ImageID"))
            {
                userProfile.Image = new UUID((string)requestData["ImageID"]);
            }
            // dont' know how yet
            if (requestData.Contains("MaturePublish"))
            {
            }
            if (requestData.Contains("AboutText"))
            {
                userProfile.AboutText = (string)requestData["AboutText"];
            }
            if (requestData.Contains("FLAboutText"))
            {
                userProfile.FirstLifeAboutText = (string)requestData["FLAboutText"];
            }
            // not in DB yet.
            if (requestData.Contains("ProfileURL"))
            {
            }
            if (requestData.Contains("home_region"))
            {
                try
                {
                    userProfile.HomeRegion = Convert.ToUInt64((string)requestData["home_region"]);
                }
                catch (ArgumentException)
                {
                    m_log.Error("[PROFILE]:Failed to set home region, Invalid Argument");
                }
                catch (FormatException)
                {
                    m_log.Error("[PROFILE]:Failed to set home region, Invalid Format");
                }
                catch (OverflowException)
                {
                    m_log.Error("[PROFILE]:Failed to set home region, Value was too large");
                }
            }
            if (requestData.Contains("home_region_id"))
            {
                UUID regionID;
                UUID.TryParse((string)requestData["home_region_id"], out regionID);
                userProfile.HomeRegionID = regionID;
            }
            if (requestData.Contains("home_pos_x"))
            {
                try
                {
                    userProfile.HomeLocationX = (float)Convert.ToDecimal((string)requestData["home_pos_x"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set home postion x");
                }
            }
            if (requestData.Contains("home_pos_y"))
            {
                try
                {
                    userProfile.HomeLocationY = (float)Convert.ToDecimal((string)requestData["home_pos_y"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set home postion y");
                }
            }
            if (requestData.Contains("home_pos_z"))
            {
                try
                {
                    userProfile.HomeLocationZ = (float)Convert.ToDecimal((string)requestData["home_pos_z"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set home postion z");
                }
            }
            if (requestData.Contains("home_look_x"))
            {
                try
                {
                    userProfile.HomeLookAtX = (float)Convert.ToDecimal((string)requestData["home_look_x"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set home lookat x");
                }
            }
            if (requestData.Contains("home_look_y"))
            {
                try
                {
                    userProfile.HomeLookAtY = (float)Convert.ToDecimal((string)requestData["home_look_y"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set home lookat y");
                }
            }
            if (requestData.Contains("home_look_z"))
            {
                try
                {
                    userProfile.HomeLookAtZ = (float)Convert.ToDecimal((string)requestData["home_look_z"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set home lookat z");
                }
            }
            if (requestData.Contains("user_flags"))
            {
                try
                {
                    userProfile.UserFlags = Convert.ToInt32((string)requestData["user_flags"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set user flags");
                }
            }
            if (requestData.Contains("god_level"))
            {
                try
                {
                    userProfile.GodLevel = Convert.ToInt32((string)requestData["god_level"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set god level");
                }
            }
            if (requestData.Contains("custom_type"))
            {
                try
                {
                    userProfile.CustomType = (string)requestData["custom_type"];
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set custom type");
                }
            }
            if (requestData.Contains("partner"))
            {
                try
                {
                    userProfile.Partner = new UUID((string)requestData["partner"]);
                }
                catch (InvalidCastException)
                {
                    m_log.Error("[PROFILE]:Failed to set partner");
                }
            }
            else
            {
                userProfile.Partner = UUID.Zero;
            }

            // call plugin!
            bool ret = m_userDataBaseService.UpdateUserProfile(userProfile);
            responseData["returnString"] = ret.ToString();
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRPCLogOffUserMethodUUID(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            if (requestData.Contains("avatar_uuid"))
            {
                try
                {
                    UUID userUUID = new UUID((string)requestData["avatar_uuid"]);
                    UUID RegionID = new UUID((string)requestData["region_uuid"]);
                    ulong regionhandle = (ulong)Convert.ToInt64((string)requestData["region_handle"]);
                    Vector3 position = new Vector3(
                        (float)Convert.ToDecimal((string)requestData["region_pos_x"]),
                        (float)Convert.ToDecimal((string)requestData["region_pos_y"]),
                        (float)Convert.ToDecimal((string)requestData["region_pos_z"]));
                    Vector3 lookat = new Vector3(
                        (float)Convert.ToDecimal((string)requestData["lookat_x"]),
                        (float)Convert.ToDecimal((string)requestData["lookat_y"]),
                        (float)Convert.ToDecimal((string)requestData["lookat_z"]));

                    handlerLogOffUser = OnLogOffUser;
                    if (handlerLogOffUser != null)
                        handlerLogOffUser(userUUID);

                    m_userDataBaseService.LogOffUser(userUUID, RegionID, regionhandle, position, lookat);
                }
                catch (FormatException)
                {
                    m_log.Warn("[LOGOUT]: Error in Logout XMLRPC Params");
                    return response;
                }
            }
            else
            {
                return Util.CreateUnknownUserErrorResponse();
            }

            return response;
        }

        #endregion


        public void HandleAgentLocation(UUID agentID, UUID regionID, ulong regionHandle)
        {
            UserProfileData userProfile = m_userDataBaseService.GetUserProfile(agentID);
            if (userProfile != null)
            {
                userProfile.CurrentAgent.Region = regionID;
                userProfile.CurrentAgent.Handle = regionHandle;
                m_userDataBaseService.CommitAgent(ref userProfile);
            }
        }

        public void HandleAgentLeaving(UUID agentID, UUID regionID, ulong regionHandle)
        {
            UserProfileData userProfile = m_userDataBaseService.GetUserProfile(agentID);
            if (userProfile != null)
            {
                if (userProfile.CurrentAgent.Region == regionID)
                {
                    UserAgentData userAgent = userProfile.CurrentAgent;
                    if (userAgent != null && userAgent.AgentOnline)
                    {
                        userAgent.AgentOnline = false;
                        userAgent.LogoutTime = Util.UnixTimeSinceEpoch();
                        if (regionID != UUID.Zero)
                        {
                            userAgent.Region = regionID;
                        }
                        userAgent.Handle = regionHandle;
                        userProfile.LastLogin = userAgent.LogoutTime;

                        m_userDataBaseService.CommitAgent(ref userProfile);

                        handlerLogOffUser = OnLogOffUser;
                        if (handlerLogOffUser != null)
                            handlerLogOffUser(agentID);
                    }
                }
            }
        }

        public void HandleRegionStartup(UUID regionID)
        {
            m_userDataBaseService.LogoutUsers(regionID);
        }

        public void HandleRegionShutdown(UUID regionID)
        {
            m_userDataBaseService.LogoutUsers(regionID);
        }
    }
}
