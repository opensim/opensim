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
    public class UserServerFriendsModule
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private UserDataBaseService m_userDataBaseService;

        private BaseHttpServer m_httpServer;

        public UserServerFriendsModule(UserDataBaseService userDataBaseService)
        {
            m_userDataBaseService = userDataBaseService;
        }

        public void Initialise(IGridServiceCore core)
        {

        }

        public void PostInitialise()
        {

        }

        public void RegisterHandlers(BaseHttpServer httpServer)
        {
            m_httpServer = httpServer;

            m_httpServer.AddXmlRPCHandler("add_new_user_friend", XmlRpcResponseXmlRPCAddUserFriend);
            m_httpServer.AddXmlRPCHandler("remove_user_friend", XmlRpcResponseXmlRPCRemoveUserFriend);
            m_httpServer.AddXmlRPCHandler("update_user_friend_perms", XmlRpcResponseXmlRPCUpdateUserFriendPerms);
            m_httpServer.AddXmlRPCHandler("get_user_friend_list", XmlRpcResponseXmlRPCGetUserFriendList);
        }

        public XmlRpcResponse FriendListItemListtoXmlRPCResponse(List<FriendListItem> returnUsers)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            // Query Result Information

            responseData["avcount"] = returnUsers.Count.ToString();

            for (int i = 0; i < returnUsers.Count; i++)
            {
                responseData["ownerID" + i] = returnUsers[i].FriendListOwner.ToString();
                responseData["friendID" + i] = returnUsers[i].Friend.ToString();
                responseData["ownerPerms" + i] = returnUsers[i].FriendListOwnerPerms.ToString();
                responseData["friendPerms" + i] = returnUsers[i].FriendPerms.ToString();
            }
            response.Value = responseData;

            return response;
        }

        public XmlRpcResponse XmlRpcResponseXmlRPCAddUserFriend(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();
            string returnString = "FALSE";
            // Query Result Information

            if (requestData.Contains("ownerID") && requestData.Contains("friendID") &&
                requestData.Contains("friendPerms"))
            {
                // UserManagerBase.AddNewuserFriend
                m_userDataBaseService.AddNewUserFriend(new UUID((string)requestData["ownerID"]),
                                 new UUID((string)requestData["friendID"]),
                                 (uint)Convert.ToInt32((string)requestData["friendPerms"]));
                returnString = "TRUE";
            }
            responseData["returnString"] = returnString;
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRpcResponseXmlRPCRemoveUserFriend(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();
            string returnString = "FALSE";
            // Query Result Information

            if (requestData.Contains("ownerID") && requestData.Contains("friendID"))
            {
                // UserManagerBase.AddNewuserFriend
                m_userDataBaseService.RemoveUserFriend(new UUID((string)requestData["ownerID"]),
                                 new UUID((string)requestData["friendID"]));
                returnString = "TRUE";
            }
            responseData["returnString"] = returnString;
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRpcResponseXmlRPCUpdateUserFriendPerms(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();
            string returnString = "FALSE";

            if (requestData.Contains("ownerID") && requestData.Contains("friendID") &&
                requestData.Contains("friendPerms"))
            {
                m_userDataBaseService.UpdateUserFriendPerms(new UUID((string)requestData["ownerID"]),
                                      new UUID((string)requestData["friendID"]),
                                      (uint)Convert.ToInt32((string)requestData["friendPerms"]));
                // UserManagerBase.
                returnString = "TRUE";
            }
            responseData["returnString"] = returnString;
            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRpcResponseXmlRPCGetUserFriendList(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            // Hashtable responseData = new Hashtable();

            List<FriendListItem> returndata = new List<FriendListItem>();

            if (requestData.Contains("ownerID"))
            {
                returndata = m_userDataBaseService.GetUserFriendList(new UUID((string)requestData["ownerID"]));
            }

            return FriendListItemListtoXmlRPCResponse(returndata);
        }
    }
}
