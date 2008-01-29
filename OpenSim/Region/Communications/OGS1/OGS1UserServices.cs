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
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Console;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGS1UserServices : IUserService
    {
        private CommunicationsOGS1 m_parent;

        public OGS1UserServices(CommunicationsOGS1 parent)
        {
            m_parent = parent;
        }

        public UserProfileData ConvertXMLRPCDataToUserProfile(Hashtable data)
        {
            if (data.Contains("error_type"))
            {
                MainLog.Instance.Warn("GRID",
                                      "Error sent by user server when trying to get user profile: (" +
                                      data["error_type"] +
                                      "): " + data["error_desc"]);
                return null;
            }

            UserProfileData userData = new UserProfileData();
            userData.username = (string) data["firstname"];
            userData.surname = (string) data["lastname"];
            userData.UUID = new LLUUID((string) data["uuid"]);
            userData.userInventoryURI = (string) data["server_inventory"];
            userData.userAssetURI = (string) data["server_asset"];
            userData.profileFirstText = (string) data["profile_firstlife_about"];
            userData.profileFirstImage = new LLUUID((string) data["profile_firstlife_image"]);
            userData.profileCanDoMask = Convert.ToUInt32((string) data["profile_can_do"]);
            userData.profileWantDoMask = Convert.ToUInt32(data["profile_want_do"]);
            userData.profileImage = new LLUUID((string) data["profile_image"]);
            userData.lastLogin = Convert.ToInt32((string) data["profile_lastlogin"]);
            userData.homeRegion = Convert.ToUInt64((string) data["home_region"]);
            userData.homeLocation =
                new LLVector3((float) Convert.ToDecimal((string) data["home_coordinates_x"]),
                              (float) Convert.ToDecimal((string) data["home_coordinates_y"]),
                              (float) Convert.ToDecimal((string) data["home_coordinates_z"]));
            userData.homeLookAt =
                new LLVector3((float) Convert.ToDecimal((string) data["home_look_x"]),
                              (float) Convert.ToDecimal((string) data["home_look_y"]),
                              (float) Convert.ToDecimal((string) data["home_look_z"]));

            return userData;
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
                MainLog.Instance.Warn("INTERGRID", "Got invalid queryID from userServer");
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
                XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 3000);
            }
            catch (System.Net.WebException)
            {
                MainLog.Instance.Warn("LOGOFF", "Unable to notify grid server of user logoff");
            }


        }
        public UserProfileData GetUserProfile(string firstName, string lastName)
        {
            return GetUserProfile(firstName + " " + lastName);
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
                MainLog.Instance.Warn("Error when trying to fetch Avatar Picker Response: " +
                                      e.Message);
                // Return Empty picker list (no results)
            }
            return pickerlist;
        }

        public UserProfileData GetUserProfile(string name)
        {
            try
            {
                Hashtable param = new Hashtable();
                param["avatar_name"] = name;
                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_user_by_name", parameters);
                XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 3000);
                Hashtable respData = (Hashtable) resp.Value;

                return ConvertXMLRPCDataToUserProfile(respData);
            }
            catch (WebException e)
            {
                MainLog.Instance.Warn("Error when trying to fetch profile data by name from remote user server: " +
                                      e.Message);
            }
            return null;
        }

        public UserProfileData GetUserProfile(LLUUID avatarID)
        {
            try
            {
                Hashtable param = new Hashtable();
                param["avatar_uuid"] = avatarID.ToString();
                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_user_by_uuid", parameters);
                XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 3000);
                Hashtable respData = (Hashtable) resp.Value;

                return ConvertXMLRPCDataToUserProfile(respData);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error when trying to fetch profile data by uuid from remote user server: " +
                                  e.Message);
            }
            return null;
        }

        public void clearUserAgent(LLUUID avatarID)
        {
            // TODO: implement
        }

        public UserProfileData SetupMasterUser(string firstName, string lastName)
        {
            return SetupMasterUser(firstName, lastName, String.Empty);
        }

        public UserProfileData SetupMasterUser(string firstName, string lastName, string password)
        {
            UserProfileData profile = GetUserProfile(firstName, lastName);
            return profile;
        }

        public UserProfileData SetupMasterUser(LLUUID uuid)
        {
            UserProfileData data = GetUserProfile(uuid);
            if (data == null)
            {
                throw new Exception("Unknown master user UUID");
            }
            return data;
        }

        public LLUUID AddUserProfile(string firstName, string lastName, string pass, uint regX, uint regY)
        {
            throw new Exception("The method or operation is not implemented.");
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
                            MainLog.Instance.Warn("GRID", "Unable to add new friend, User Server Reported an issue");
                        }
                    }
                    else
                    {
                        MainLog.Instance.Warn("GRID", "Unable to add new friend, UserServer didn't understand me!");
                    }
                }
                else
                {
                    MainLog.Instance.Warn("GRID", "Unable to add new friend, UserServer didn't understand me!");

                }
            }
            catch (WebException e)
            {
                MainLog.Instance.Warn("GRID","Error when trying to AddNewUserFriend: " +
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
                            MainLog.Instance.Warn("GRID", "Unable to remove friend, User Server Reported an issue");
                        }
                    }
                    else
                    {
                        MainLog.Instance.Warn("GRID", "Unable to remove friend, UserServer didn't understand me!");
                    }
                }
                else
                {
                    MainLog.Instance.Warn("GRID", "Unable to remove friend, UserServer didn't understand me!");

                }
            }
            catch (WebException e)
            {
                MainLog.Instance.Warn("GRID", "Error when trying to RemoveUserFriend: " +
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
                            MainLog.Instance.Warn("GRID", "Unable to update_user_friend_perms, User Server Reported an issue");
                        }
                    }
                    else
                    {
                        MainLog.Instance.Warn("GRID", "Unable to update_user_friend_perms, UserServer didn't understand me!");
                    }
                }
                else
                {
                    MainLog.Instance.Warn("GRID", "Unable to update_user_friend_perms, UserServer didn't understand me!");

                }
            }
            catch (WebException e)
            {
                MainLog.Instance.Warn("GRID", "Error when trying to update_user_friend_perms: " +
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
                XmlRpcResponse resp = req.Send(m_parent.NetworkServersInfo.UserURL, 3000);
                Hashtable respData = (Hashtable) resp.Value;

                if (respData.Contains("avcount")) 
                {
                    buddylist = ConvertXMLRPCDataToFriendListItemList(respData);
                }
                
            }
            catch (WebException e)
            {
                MainLog.Instance.Warn("Error when trying to fetch Avatar's friends list: " +
                                      e.Message);
                // Return Empty list (no friends)
            }
            return buddylist;
             
        }

        #endregion
    }
}