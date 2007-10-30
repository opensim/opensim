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
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.UserManagement;

namespace OpenSim.Grid.UserServer
{
    public class UserManager : UserManagerBase
    {
        public UserManager()
        {
        }


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
            responseData["uuid"] = profile.UUID.ToStringHyphenated();
            // Server Information
            responseData["server_inventory"] = profile.userInventoryURI;
            responseData["server_asset"] = profile.userAssetURI;
            // Profile Information
            responseData["profile_about"] = profile.profileAboutText;
            responseData["profile_firstlife_about"] = profile.profileFirstText;
            responseData["profile_firstlife_image"] = profile.profileFirstImage.ToStringHyphenated();
            responseData["profile_can_do"] = profile.profileCanDoMask.ToString();
            responseData["profile_want_do"] = profile.profileWantDoMask.ToString();
            responseData["profile_image"] = profile.profileImage.ToStringHyphenated();
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

        public XmlRpcResponse XmlRPCGetUserMethodName(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable) request.Params[0];
            UserProfileData userProfile;
            if (requestData.Contains("avatar_name"))
            {
                userProfile = GetUserProfile((string) requestData["avatar_name"]);
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

        public XmlRpcResponse XmlRPCGetUserMethodUUID(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable) request.Params[0];
            UserProfileData userProfile;
            Console.WriteLine("METHOD BY UUID CALLED");
            if (requestData.Contains("avatar_uuid"))
            {
                userProfile = GetUserProfile((LLUUID) (string) requestData["avatar_uuid"]);
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

        #endregion

        public override UserProfileData SetupMasterUser(string firstName, string lastName)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override UserProfileData SetupMasterUser(string firstName, string lastName, string password)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}