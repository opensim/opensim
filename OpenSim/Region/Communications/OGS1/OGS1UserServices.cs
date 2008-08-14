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
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using libsecondlife;
using log4net;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Communications;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGS1UserServices : IUserService, IAvatarService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private CommunicationsOGS1 m_parent;

        public OGS1UserServices(CommunicationsOGS1 parent)
        {
            m_parent = parent;
        }

        public UserProfileData ConvertXMLRPCDataToUserProfile(Hashtable data)
        {
            if (data.Contains("error_type"))
            {
                m_log.Warn("[GRID]: " +
                           "Error sent by user server when trying to get user profile: (" +
                           data["error_type"] +
                           "): " + data["error_desc"]);
                return null;
            }

            UserProfileData userData = new UserProfileData();
            userData.FirstName = (string) data["firstname"];
            userData.SurName = (string) data["lastname"];
            userData.ID = new LLUUID((string) data["uuid"]);
            userData.UserInventoryURI = (string) data["server_inventory"];
            userData.UserAssetURI = (string) data["server_asset"];
            userData.FirstLifeAboutText = (string) data["profile_firstlife_about"];
            userData.FirstLifeImage = new LLUUID((string) data["profile_firstlife_image"]);
            userData.CanDoMask = Convert.ToUInt32((string) data["profile_can_do"]);
            userData.WantDoMask = Convert.ToUInt32(data["profile_want_do"]);
            userData.AboutText = (string)data["profile_about"];
            userData.Image = new LLUUID((string) data["profile_image"]);
            userData.LastLogin = Convert.ToInt32((string) data["profile_lastlogin"]);
            userData.HomeRegion = Convert.ToUInt64((string) data["home_region"]);
            if(data.Contains("home_region_id")) userData.HomeRegionID = new LLUUID((string)data["home_region_id"]);
            else userData.HomeRegionID = LLUUID.Zero;
            userData.HomeLocation =
                new LLVector3((float) Convert.ToDecimal((string) data["home_coordinates_x"]),
                              (float) Convert.ToDecimal((string) data["home_coordinates_y"]),
                              (float) Convert.ToDecimal((string) data["home_coordinates_z"]));
            userData.HomeLookAt =
                new LLVector3((float) Convert.ToDecimal((string) data["home_look_x"]),
                              (float) Convert.ToDecimal((string) data["home_look_y"]),
                              (float) Convert.ToDecimal((string) data["home_look_z"]));
			if(data.Contains("user_flags"))
				userData.UserFlags = Convert.ToInt32((string) data["user_flags"]);
			if(data.Contains("god_level"))
				userData.GodLevel = Convert.ToInt32((string) data["god_level"]);

            return userData;
        }

        /// <summary>
        /// Get a user agent from the user server
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns>null if the request fails</returns>
        public UserAgentData GetAgentByUUID(LLUUID userId)
        {
           try
           {
               Hashtable param = new Hashtable();
               param["avatar_uuid"] = userId.ToString();
               IList parameters = new ArrayList();
               parameters.Add(param);
               XmlRpcRequest req = new XmlRpcRequest("get_agent_by_uuid", parameters);
               XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 6000);
               Hashtable respData = (Hashtable) resp.Value;
               if (respData.Contains("error_type"))
               {
                   m_log.Warn("[GRID]: " +
                              "Error sent by user server when trying to get agent: (" +
                              (string) respData["error_type"] +
                              "): " + (string)respData["error_desc"]);
                   return null;
               }
               LLUUID sessionid = LLUUID.Zero;

               UserAgentData userAgent = new UserAgentData();
               userAgent.Handle = Convert.ToUInt64((string)respData["handle"]);
               Helpers.TryParse((string)respData["sessionid"], out sessionid);
               userAgent.SessionID = sessionid;

               if ((string)respData["agent_online"] == "TRUE")
               {
                   userAgent.AgentOnline = true;
               }
               else
               {
                   userAgent.AgentOnline = false;
               }

               return userAgent;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[OGS1 USER SERVICES]: Error when trying to fetch agent data by uuid from remote user server: {0}",
                    e);
            }

            return null;
        }

        public AvatarAppearance ConvertXMLRPCDataToAvatarAppearance(Hashtable data)
        {
            if (data != null)
            {
                if (data.Contains("error_type"))
                {
                    m_log.Warn("[GRID]: " +
                               "Error sent by user server when trying to get user appearance: (" +
                               data["error_type"] +
                               "): " + data["error_desc"]);
                    return null;
                }
                else
                {
                    return new AvatarAppearance(data);
                }
            }
            else
            {
                m_log.Error("[GRID]: The avatar appearance is null, something bad happenend");
                return null;
            }
        }

        public List<AvatarPickerAvatar> ConvertXMLRPCDataToAvatarPickerList(LLUUID queryID, Hashtable data)
        {
            List<AvatarPickerAvatar> pickerlist = new List<AvatarPickerAvatar>();
            int pickercount = Convert.ToInt32((string) data["avcount"]);
            LLUUID respqueryID = new LLUUID((string) data["queryid"]);
            if (queryID == respqueryID)
            {
                for (int i = 0; i < pickercount; i++)
                {
                    AvatarPickerAvatar apicker = new AvatarPickerAvatar();
                    LLUUID avatarID = new LLUUID((string) data["avatarid" + i.ToString()]);
                    string firstname = (string) data["firstname" + i.ToString()];
                    string lastname = (string) data["lastname" + i.ToString()];
                    apicker.AvatarID = avatarID;
                    apicker.firstName = firstname;
                    apicker.lastName = lastname;
                    pickerlist.Add(apicker);
                }
            }
            else
            {
                m_log.Warn("[OGS1 USER SERVICES]: Got invalid queryID from userServer");
            }
            return pickerlist;
        }

        public List<FriendListItem> ConvertXMLRPCDataToFriendListItemList(Hashtable data)
        {
            List<FriendListItem> buddylist = new List<FriendListItem>();
            int buddycount = Convert.ToInt32((string)data["avcount"]);


            for (int i = 0; i < buddycount; i++)
            {
                FriendListItem buddylistitem = new FriendListItem();

                buddylistitem.FriendListOwner = new LLUUID((string)data["ownerID" + i.ToString()]);
                buddylistitem.Friend = new LLUUID((string)data["friendID" + i.ToString()]);
                buddylistitem.FriendListOwnerPerms = (uint)Convert.ToInt32((string)data["ownerPerms" + i.ToString()]);
                buddylistitem.FriendPerms = (uint)Convert.ToInt32((string)data["friendPerms" + i.ToString()]);

                buddylist.Add(buddylistitem);
            }


            return buddylist;
        }

        /// <summary>
        /// Logs off a user on the user server
        /// </summary>
        /// <param name="UserID">UUID of the user</param>
        /// <param name="regionData">UUID of the Region</param>
        /// <param name="posx">final position x</param>
        /// <param name="posy">final position y</param>
        /// <param name="posz">final position z</param>
        public void LogOffUser(LLUUID userid, LLUUID regionid, ulong regionhandle, float posx, float posy, float posz)
        {
            Hashtable param = new Hashtable();
            param["avatar_uuid"] = userid.UUID.ToString();
            param["region_uuid"] = regionid.UUID.ToString();
            param["region_handle"] = regionhandle.ToString();
            param["region_pos_x"] = posx.ToString();
            param["region_pos_y"] = posy.ToString();
            param["region_pos_z"] = posz.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("logout_of_simulator", parameters);
            
            try
            {
                req.Send(m_parent.NetworkServersInfo.UserURL, 3000);
            }
            catch (WebException)
            {
                m_log.Warn("[LOGOFF]: Unable to notify grid server of user logoff");
            }
        }
        
        public UserProfileData GetUserProfile(string firstName, string lastName)
        {
            return GetUserProfile(firstName + " " + lastName);
        }

        public void UpdateUserCurrentRegion(LLUUID avatarid, LLUUID regionuuid, ulong regionhandle)
        {
            Hashtable param = new Hashtable();
            param.Add("avatar_id", avatarid.ToString());
            param.Add("region_uuid", regionuuid.ToString());
            param.Add("region_handle", regionhandle.ToString());
            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("update_user_current_region", parameters);

            XmlRpcResponse resp;

            try
            {
                resp = req.Send(m_parent.NetworkServersInfo.UserURL, 3000);
            }
            catch(WebException)
            {
                m_log.Warn("[OSG1 USER SERVICES]: Grid not responding. Retrying.");

                try
                {
                    resp = req.Send(m_parent.NetworkServersInfo.UserURL, 3000);
                }
                catch (WebException)
                {
                    m_log.Warn("[OSG1 USER SERVICES]: Grid not responding. Failed.");
                    return;
                }
            }

            if (resp == null)
            {
                m_log.Warn("[OSG1 USER SERVICES]: Got no response, Grid server may not be updated.");
                return;
            }

            Hashtable respData = (Hashtable)resp.Value;

            if (respData == null || !respData.ContainsKey("returnString"))
            {
                m_log.Error("[OSG1 USER SERVICES]: Error updating user record, Grid server may not be updated.");
            }
            else
            {
                if ((string) respData["returnString"] != "TRUE")
                {
                    m_log.Error("[OSG1 USER SERVICES]: Error updating user record");
                }
            }
        }

        public List<AvatarPickerAvatar> GenerateAgentPickerRequestResponse(LLUUID queryID, string query)
        {
            List<AvatarPickerAvatar> pickerlist = new List<AvatarPickerAvatar>();
            Regex objAlphaNumericPattern = new Regex("[^a-zA-Z0-9 ]");
            try
            {
                Hashtable param = new Hashtable();
                param["queryid"] = (string) queryID.ToString();
                param["avquery"] = objAlphaNumericPattern.Replace(query, String.Empty);
                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_avatar_picker_avatar", parameters);
                XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 3000);
                Hashtable respData = (Hashtable) resp.Value;
                pickerlist = ConvertXMLRPCDataToAvatarPickerList(queryID, respData);
            }
            catch (WebException e)
            {
                m_log.Warn("[OGS1 USER SERVICES]: Error when trying to fetch Avatar Picker Response: " +
                           e.Message);
                // Return Empty picker list (no results)
            }
            return pickerlist;
        }

        /// <summary>
        /// Get a user profile from the user server
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns>null if the request fails</returns>
        public UserProfileData GetUserProfile(string name)
        {
            try
            {
                Hashtable param = new Hashtable();
                param["avatar_name"] = name;
                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_user_by_name", parameters);
                XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 30000);
                Hashtable respData = (Hashtable) resp.Value;

                return ConvertXMLRPCDataToUserProfile(respData);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat(
                    "[OGS1 USER SERVICES]: Error when trying to fetch profile data by name from remote user server: {0}",
                    e);
            }

            return null;
        }

        /// <summary>
        /// Get a user profile from the user server
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns>null if the request fails</returns>
        public UserProfileData GetUserProfile(LLUUID avatarID)
        {
            try
            {
                Hashtable param = new Hashtable();
                param["avatar_uuid"] = avatarID.ToString();
                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_user_by_uuid", parameters);
                XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 30000);
                Hashtable respData = (Hashtable) resp.Value;

                return ConvertXMLRPCDataToUserProfile(respData);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[OGS1 USER SERVICES]: Error when trying to fetch profile data by uuid from remote user server: {0}",
                    e);
            }

            return null;
        }


        public void ClearUserAgent(LLUUID avatarID)
        {
            // TODO: implement
        }

        /// <summary>
        /// Retrieve the user information for the given master uuid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public UserProfileData SetupMasterUser(string firstName, string lastName)
        {
            return SetupMasterUser(firstName, lastName, String.Empty);
        }

        /// <summary>
        /// Retrieve the user information for the given master uuid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public UserProfileData SetupMasterUser(string firstName, string lastName, string password)
        {
            UserProfileData profile = GetUserProfile(firstName, lastName);
            return profile;
        }

        /// <summary>
        /// Retrieve the user information for the given master uuid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public UserProfileData SetupMasterUser(LLUUID uuid)
        {
            UserProfileData data = GetUserProfile(uuid);

            if (data == null)
            {
                throw new Exception(
                    "Could not retrieve profile for master user " + uuid + ".  User server did not respond to the request.");
            }

            return data;
        }

        public LLUUID AddUserProfile(string firstName, string lastName, string pass, uint regX, uint regY)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        // TODO
        public bool UpdateUserProfile(UserProfileData data)
        {
            return false;
        }

        public bool UpdateUserProfileProperties(UserProfileData UserProfile)
        {
            m_log.Debug("[OGS1 USER SERVICES]: Asking UserServer to update profile.");
            Hashtable param = new Hashtable();
            param["avatar_uuid"] = UserProfile.ID.ToString();
            //param["AllowPublish"] = UserProfile.ToString();
            param["FLImageID"] = UserProfile.FirstLifeImage.ToString();
            param["ImageID"] = UserProfile.Image.ToString();
            //param["MaturePublish"] = MaturePublish.ToString();
            param["AboutText"] = UserProfile.AboutText;
            param["FLAboutText"] = UserProfile.FirstLifeAboutText;
            //param["ProfileURL"] = UserProfile.ProfileURL.ToString();

            param["home_region"] = UserProfile.HomeRegion.ToString();
            param["home_region_id"] = UserProfile.HomeRegionID.ToString();

            param["home_pos_x"] = UserProfile.HomeLocationX.ToString();
            param["home_pos_y"] = UserProfile.HomeLocationY.ToString();
            param["home_pos_z"] = UserProfile.HomeLocationZ.ToString();
            param["home_look_x"] = UserProfile.HomeLookAtX.ToString();
            param["home_look_y"] = UserProfile.HomeLookAtY.ToString();
            param["home_look_z"] = UserProfile.HomeLookAtZ.ToString();
            param["user_flags"] = UserProfile.UserFlags.ToString();
            param["god_level"] = UserProfile.GodLevel.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);

            XmlRpcRequest req = new XmlRpcRequest("update_user_profile", parameters);
            XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;
            if (respData != null)
            {
                if (respData.Contains("returnString"))
                {
                    if (((string)respData["returnString"]).ToUpper() != "TRUE")
                    {
                        m_log.Warn("[GRID]: Unable to update user profile, User Server Reported an issue");
                        return false;
                    }
                }
                else
                {
                    m_log.Warn("[GRID]: Unable to update user profile, UserServer didn't understand me!");
                    return false;
                }
            }
            else
            {
                m_log.Warn("[GRID]: Unable to update user profile, UserServer didn't understand me!");
                return false;
            }
            return true;
        }

        #region IUserServices Friend Methods
        /// <summary>
        /// Adds a new friend to the database for XUser
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being added to</param>
        /// <param name="friend">The agent that being added to the friends list of the friends list owner</param>
        /// <param name="perms">A uint bit vector for set perms that the friend being added has; 0 = none, 1=This friend can see when they sign on, 2 = map, 4 edit objects </param>
        public void AddNewUserFriend(LLUUID friendlistowner, LLUUID friend, uint perms)
        {
            try
            {
                Hashtable param = new Hashtable();
                param["ownerID"] = friendlistowner.UUID.ToString();
                param["friendID"] = friend.UUID.ToString();
                param["friendPerms"] = perms.ToString();
                IList parameters = new ArrayList();
                parameters.Add(param);

                XmlRpcRequest req = new XmlRpcRequest("add_new_user_friend", parameters);
                XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 3000);
                Hashtable respData = (Hashtable)resp.Value;
                if (respData != null)
                {
                    if (respData.Contains("returnString"))
                    {
                        if ((string)respData["returnString"] == "TRUE")
                        {

                        }
                        else
                        {
                            m_log.Warn("[GRID]: Unable to add new friend, User Server Reported an issue");
                        }
                    }
                    else
                    {
                        m_log.Warn("[GRID]: Unable to add new friend, UserServer didn't understand me!");
                    }
                }
                else
                {
                    m_log.Warn("[GRID]: Unable to add new friend, UserServer didn't understand me!");

                }
            }
            catch (WebException e)
            {
                m_log.Warn("[GRID]: Error when trying to AddNewUserFriend: " +
                           e.Message);

            }

        }

        /// <summary>
        /// Delete friend on friendlistowner's friendlist.
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being updated</param>
        /// <param name="friend">The Ex-friend agent</param>
        public void RemoveUserFriend(LLUUID friendlistowner, LLUUID friend)
        {
            try
            {
                Hashtable param = new Hashtable();
                param["ownerID"] = friendlistowner.UUID.ToString();
                param["friendID"] = friend.UUID.ToString();

                IList parameters = new ArrayList();
                parameters.Add(param);

                XmlRpcRequest req = new XmlRpcRequest("remove_user_friend", parameters);
                XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 3000);
                Hashtable respData = (Hashtable)resp.Value;
                if (respData != null)
                {
                    if (respData.Contains("returnString"))
                    {
                        if ((string)respData["returnString"] == "TRUE")
                        {

                        }
                        else
                        {
                            m_log.Warn("[GRID]: Unable to remove friend, User Server Reported an issue");
                        }
                    }
                    else
                    {
                        m_log.Warn("[GRID]: Unable to remove friend, UserServer didn't understand me!");
                    }
                }
                else
                {
                    m_log.Warn("[GRID]: Unable to remove friend, UserServer didn't understand me!");

                }
            }
            catch (WebException e)
            {
                m_log.Warn("[GRID]: Error when trying to RemoveUserFriend: " +
                           e.Message);

            }
        }

        /// <summary>
        /// Update permissions for friend on friendlistowner's friendlist.
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being updated</param>
        /// <param name="friend">The agent that is getting or loosing permissions</param>
        /// <param name="perms">A uint bit vector for set perms that the friend being added has; 0 = none, 1=This friend can see when they sign on, 2 = map, 4 edit objects </param>
        public void UpdateUserFriendPerms(LLUUID friendlistowner, LLUUID friend, uint perms)
        {
            try
            {
                Hashtable param = new Hashtable();
                param["ownerID"] = friendlistowner.UUID.ToString();
                param["friendID"] = friend.UUID.ToString();
                param["friendPerms"] = perms.ToString();
                IList parameters = new ArrayList();
                parameters.Add(param);

                XmlRpcRequest req = new XmlRpcRequest("update_user_friend_perms", parameters);
                XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 3000);
                Hashtable respData = (Hashtable)resp.Value;
                if (respData != null)
                {
                    if (respData.Contains("returnString"))
                    {
                        if ((string)respData["returnString"] == "TRUE")
                        {

                        }
                        else
                        {
                            m_log.Warn("[GRID]: Unable to update_user_friend_perms, User Server Reported an issue");
                        }
                    }
                    else
                    {
                        m_log.Warn("[GRID]: Unable to update_user_friend_perms, UserServer didn't understand me!");
                    }
                }
                else
                {
                    m_log.Warn("[GRID]: Unable to update_user_friend_perms, UserServer didn't understand me!");

                }
            }
            catch (WebException e)
            {
                m_log.Warn("[GRID]: Error when trying to update_user_friend_perms: " +
                           e.Message);
            }
        }
        /// <summary>
        /// Returns a list of FriendsListItems that describe the friends and permissions in the friend relationship for LLUUID friendslistowner
        /// </summary>
        /// <param name="friendlistowner">The agent that we're retreiving the friends Data.</param>
        public List<FriendListItem> GetUserFriendList(LLUUID friendlistowner)
        {
            List<FriendListItem> buddylist = new List<FriendListItem>();

            try
            {
                Hashtable param = new Hashtable();
                param["ownerID"] = friendlistowner.UUID.ToString();

                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_user_friend_list", parameters);
                XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 8000);
                Hashtable respData = (Hashtable) resp.Value;

                if (respData.Contains("avcount"))
                {
                    buddylist = ConvertXMLRPCDataToFriendListItemList(respData);
                }

            }
            catch (WebException e)
            {
                m_log.Warn("[OGS1 USER SERVICES]: Error when trying to fetch Avatar's friends list: " +
                           e.Message);
                // Return Empty list (no friends)
            }
            return buddylist;
        }

        #endregion

        /// Appearance
        public AvatarAppearance GetUserAppearance(LLUUID user)
        {
            AvatarAppearance appearance = null;
            
            try
            {
                Hashtable param = new Hashtable();
                param["owner"] = user.ToString();

                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_avatar_appearance", parameters);
                XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 8000);
                Hashtable respData = (Hashtable) resp.Value;
                
                return ConvertXMLRPCDataToAvatarAppearance(respData);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 USER SERVICES]: Network problems when trying to fetch appearance for avatar {0}, {1}", user, e.Message);
            }
            
            return appearance;
        }

        public void UpdateUserAppearance(LLUUID user, AvatarAppearance appearance)
        {
            try
            {
                Hashtable param = appearance.ToHashTable();
                param["owner"] = user.ToString();

                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("update_avatar_appearance", parameters);
                XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 8000);
                Hashtable respData = (Hashtable) resp.Value;

                if (respData != null)
                {
                    if (respData.Contains("returnString"))
                    {
                        if ((string)respData["returnString"] == "TRUE")
                        {

                        }
                        else
                        {
                            m_log.Warn("[GRID]: Unable to update_user_appearance, User Server Reported an issue");
                        }
                    }
                    else
                    {
                        m_log.Warn("[GRID]: Unable to update_user_appearance, UserServer didn't understand me!");
                    }
                }
                else
                {
                    m_log.Warn("[GRID]: Unable to update_user_appearance, UserServer didn't understand me!");
                }
            }
            catch (WebException e)
            {
                m_log.Warn("[OGS1 USER SERVICES]: Error when trying to update Avatar's appearance: " +
                           e.Message);
                // Return Empty list (no friends)
            }
        }

        public void AddAttachment(LLUUID user, LLUUID item)
        {
            return;
        }

        public void RemoveAttachment(LLUUID user, LLUUID item)
        {
            return;
        }

        public List<LLUUID> GetAttachments(LLUUID user)
        {
            return new List<LLUUID>();
        }

    }
}
