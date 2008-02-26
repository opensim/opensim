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
* 
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Statistics;
using OpenSim.Framework.UserManagement;

namespace OpenSim.Grid.UserServer
{
    public delegate void logOffUser(LLUUID AgentID);

    public class UserManager : UserManagerBase
    {            
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public event logOffUser OnLogOffUser;
        private logOffUser handler001 = null;
        /// <summary>
        /// Deletes an active agent session
        /// </summary>
        /// <param name="request">The request</param>
        /// <param name="path">The path (eg /bork/narf/test)</param>
        /// <param name="param">Parameters sent</param>
        /// <returns>Success "OK" else error</returns>
        public string RestDeleteUserSessionMethod(string request, string path, string param)
        {
            // TODO! Important!

            return "OK";
        }

        /// <summary>
        /// Returns an error message that the user could not be found in the database
        /// </summary>
        /// <returns>XML string consisting of a error element containing individual error(s)</returns>
        public XmlRpcResponse CreateUnknownUserErrorResponse()
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            responseData["error_type"] = "unknown_user";
            responseData["error_desc"] = "The user requested is not in the database";

            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse AvatarPickerListtoXmlRPCResponse(LLUUID queryID, List<AvatarPickerAvatar> returnUsers)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            // Query Result Information
            responseData["queryid"] = (string) queryID.ToString();
            responseData["avcount"] = (string) returnUsers.Count.ToString();

            for (int i = 0; i < returnUsers.Count; i++)
            {
                responseData["avatarid" + i.ToString()] = returnUsers[i].AvatarID.ToString();
                responseData["firstname" + i.ToString()] = returnUsers[i].firstName;
                responseData["lastname" + i.ToString()] = returnUsers[i].lastName;
            }
            response.Value = responseData;
            
            return response;
        }

        public XmlRpcResponse FriendListItemListtoXmlRPCResponse(List<FriendListItem> returnUsers)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            // Query Result Information

            responseData["avcount"] = (string)returnUsers.Count.ToString();

            for (int i = 0; i < returnUsers.Count; i++)
            {
                responseData["ownerID" + i.ToString()] = returnUsers[i].FriendListOwner.UUID.ToString();
                responseData["friendID" + i.ToString()] = returnUsers[i].Friend.UUID.ToString();
                responseData["ownerPerms" + i.ToString()] = returnUsers[i].FriendListOwnerPerms.ToString();
                responseData["friendPerms" + i.ToString()] = returnUsers[i].FriendPerms.ToString();
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
            responseData["firstname"] = profile.username;
            responseData["lastname"] = profile.surname;
            responseData["uuid"] = profile.UUID.ToString();
            // Server Information
            responseData["server_inventory"] = profile.userInventoryURI;
            responseData["server_asset"] = profile.userAssetURI;
            // Profile Information
            responseData["profile_about"] = profile.profileAboutText;
            responseData["profile_firstlife_about"] = profile.profileFirstText;
            responseData["profile_firstlife_image"] = profile.profileFirstImage.ToString();
            responseData["profile_can_do"] = profile.profileCanDoMask.ToString();
            responseData["profile_want_do"] = profile.profileWantDoMask.ToString();
            responseData["profile_image"] = profile.profileImage.ToString();
            responseData["profile_created"] = profile.created.ToString();
            responseData["profile_lastlogin"] = profile.lastLogin.ToString();
            // Home region information
            responseData["home_coordinates_x"] = profile.homeLocation.X.ToString();
            responseData["home_coordinates_y"] = profile.homeLocation.Y.ToString();
            responseData["home_coordinates_z"] = profile.homeLocation.Z.ToString();

            responseData["home_region"] = profile.homeRegion.ToString();

            responseData["home_look_x"] = profile.homeLookAt.X.ToString();
            responseData["home_look_y"] = profile.homeLookAt.Y.ToString();
            responseData["home_look_z"] = profile.homeLookAt.Z.ToString();
            response.Value = responseData;

            return response;
        }

        #region XMLRPC User Methods

        public XmlRpcResponse XmlRPCGetAvatarPickerAvatar(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable) request.Params[0];
            List<AvatarPickerAvatar> returnAvatar = new List<AvatarPickerAvatar>();
            LLUUID queryID = new LLUUID(LLUUID.Zero.ToString());

            if (requestData.Contains("avquery") && requestData.Contains("queryid"))
            {
                queryID = new LLUUID((string) requestData["queryid"]);
                returnAvatar = GenerateAgentPickerRequestResponse(queryID, (string) requestData["avquery"]);
            }

            Console.WriteLine("[AVATARINFO]: Servicing Avatar Query: " + (string) requestData["avquery"]);
            return AvatarPickerListtoXmlRPCResponse(queryID, returnAvatar);
        }

        public XmlRpcResponse XmlRpcResponseXmlRPCAddUserFriend(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();
            string returnString = "FALSE";
            // Query Result Information
            
            if (requestData.Contains("ownerID") && requestData.Contains("friendID") && requestData.Contains("friendPerms"))
            {
                // UserManagerBase.AddNewuserFriend
                AddNewUserFriend(new LLUUID((string)requestData["ownerID"]), new LLUUID((string)requestData["friendID"]), (uint)Convert.ToInt32((string)requestData["friendPerms"]));
                returnString = "TRUE";
            }
            responseData["returnString"] = returnString;
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRpcResponseXmlRPCRemoveUserFriend(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();
            string returnString = "FALSE";
            // Query Result Information
            
            if (requestData.Contains("ownerID") && requestData.Contains("friendID"))
            {
                // UserManagerBase.AddNewuserFriend
                RemoveUserFriend(new LLUUID((string)requestData["ownerID"]), new LLUUID((string)requestData["friendID"]));
                returnString = "TRUE";
            }
            responseData["returnString"] = returnString;
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRpcResponseXmlRPCUpdateUserFriendPerms(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();
            string returnString = "FALSE";
           
            if (requestData.Contains("ownerID") && requestData.Contains("friendID") && requestData.Contains("friendPerms"))
            {
                UpdateUserFriendPerms(new LLUUID((string)requestData["ownerID"]), new LLUUID((string)requestData["friendID"]), (uint)Convert.ToInt32((string)requestData["friendPerms"]));
                // UserManagerBase.
                returnString = "TRUE";
            }
            responseData["returnString"] = returnString;
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRpcResponseXmlRPCGetUserFriendList(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();

            List<FriendListItem> returndata = new List<FriendListItem>();

            if (requestData.Contains("ownerID"))
            {
                returndata = this.GetUserFriendList(new LLUUID((string)requestData["ownerID"]));
            }
            
            return FriendListItemListtoXmlRPCResponse(returndata);
        }

        public XmlRpcResponse XmlRPCGetUserMethodName(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable) request.Params[0];
            UserProfileData userProfile;
            if (requestData.Contains("avatar_name"))
            {
                string query = (string) requestData["avatar_name"];

                Regex objAlphaNumericPattern = new Regex("[^a-zA-Z0-9]");

                string[] querysplit;
                querysplit = query.Split(' ');

                if (querysplit.Length == 2)
                {
                    userProfile = GetUserProfile(querysplit[0], querysplit[1]);
                    if (userProfile == null)
                    {
                        return CreateUnknownUserErrorResponse();
                    }
                }
                else
                {
                    return CreateUnknownUserErrorResponse();
                }
            }
            else
            {
                return CreateUnknownUserErrorResponse();
            }

            return ProfileToXmlRPCResponse(userProfile);
        }

        public XmlRpcResponse XmlRPCGetUserMethodUUID(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable) request.Params[0];
            UserProfileData userProfile;
            //CFK: this clogs the UserServer log and is not necessary at this time.
            //CFK: Console.WriteLine("METHOD BY UUID CALLED");
            if (requestData.Contains("avatar_uuid"))
            {
                LLUUID guess = new LLUUID();
                try
                {
                    guess = new LLUUID((string) requestData["avatar_uuid"]);

                    userProfile = GetUserProfile(guess);
                }
                catch (FormatException)
                {
                    return CreateUnknownUserErrorResponse();
                }

                if (userProfile == null)
                {
                    return CreateUnknownUserErrorResponse();
                }
            }
            else
            {
                return CreateUnknownUserErrorResponse();
            }

            return ProfileToXmlRPCResponse(userProfile);
        }

        public XmlRpcResponse XmlRPCLogOffUserMethodUUID(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            
            UserProfileData userProfile;

            if (requestData.Contains("avatar_uuid"))
            {
                try
                {
                    LLUUID userUUID = new LLUUID((string)requestData["avatar_uuid"]);
                    LLUUID RegionID = new LLUUID((string)requestData["region_uuid"]);
                    ulong regionhandle = (ulong)Convert.ToInt64((string)requestData["region_handle"]);
                    float posx = (float)Convert.ToDecimal((string)requestData["region_pos_x"]);
                    float posy = (float)Convert.ToDecimal((string)requestData["region_pos_y"]);
                    float posz = (float)Convert.ToDecimal((string)requestData["region_pos_z"]);

                    handler001 = OnLogOffUser;
                    if (handler001 != null)
                        handler001(userUUID);

                    LogOffUser(userUUID, RegionID, regionhandle, posx, posy, posz);
                }
                catch (FormatException)
                {
                    m_log.Warn("[LOGOUT]: Error in Logout XMLRPC Params");
                    return response;
                }
            }
            else
            {
                return CreateUnknownUserErrorResponse();
            }

            return response;
        }

        #endregion

        public override UserProfileData SetupMasterUser(string firstName, string lastName)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override UserProfileData SetupMasterUser(string firstName, string lastName, string password)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override UserProfileData SetupMasterUser(LLUUID uuid)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}
