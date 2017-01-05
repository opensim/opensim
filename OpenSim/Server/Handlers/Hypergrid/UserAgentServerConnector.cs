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

using Nini.Config;
using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Hypergrid
{
    public class UserAgentServerConnector : ServiceConnector
    {
//        private static readonly ILog m_log =
//                LogManager.GetLogger(
//                MethodBase.GetCurrentMethod().DeclaringType);

        private IUserAgentService m_HomeUsersService;
        public IUserAgentService HomeUsersService
        {
            get { return m_HomeUsersService; }
        }

        private string[] m_AuthorizedCallers;

        private bool m_VerifyCallers = false;

        public UserAgentServerConnector(IConfigSource config, IHttpServer server) :
            this(config, server, (IFriendsSimConnector)null)
        {
        }

        public UserAgentServerConnector(IConfigSource config, IHttpServer server, string configName) :
            this(config, server)
        {
        }

        public UserAgentServerConnector(IConfigSource config, IHttpServer server, IFriendsSimConnector friendsConnector) :
                base(config, server, String.Empty)
        {
            IConfig gridConfig = config.Configs["UserAgentService"];
            if (gridConfig != null)
            {
                string serviceDll = gridConfig.GetString("LocalServiceModule", string.Empty);

                Object[] args = new Object[] { config, friendsConnector };
                m_HomeUsersService = ServerUtils.LoadPlugin<IUserAgentService>(serviceDll, args);
            }
            if (m_HomeUsersService == null)
                throw new Exception("UserAgent server connector cannot proceed because of missing service");

            string loginServerIP = gridConfig.GetString("LoginServerIP", "127.0.0.1");
            bool proxy = gridConfig.GetBoolean("HasProxy", false);

            m_VerifyCallers = gridConfig.GetBoolean("VerifyCallers", false);
            string csv = gridConfig.GetString("AuthorizedCallers", "127.0.0.1");
            csv = csv.Replace(" ", "");
            m_AuthorizedCallers = csv.Split(',');

            server.AddXmlRPCHandler("agent_is_coming_home", AgentIsComingHome, false);
            server.AddXmlRPCHandler("get_home_region", GetHomeRegion, false);
            server.AddXmlRPCHandler("verify_agent", VerifyAgent, false);
            server.AddXmlRPCHandler("verify_client", VerifyClient, false);
            server.AddXmlRPCHandler("logout_agent", LogoutAgent, false);

#pragma warning disable 0612
            server.AddXmlRPCHandler("status_notification", StatusNotification, false);
            server.AddXmlRPCHandler("get_online_friends", GetOnlineFriends, false);
#pragma warning restore 0612
            server.AddXmlRPCHandler("get_user_info", GetUserInfo, false);
            server.AddXmlRPCHandler("get_server_urls", GetServerURLs, false);

            server.AddXmlRPCHandler("locate_user", LocateUser, false);
            server.AddXmlRPCHandler("get_uui", GetUUI, false);
            server.AddXmlRPCHandler("get_uuid", GetUUID, false);

            server.AddStreamHandler(new HomeAgentHandler(m_HomeUsersService, loginServerIP, proxy));
        }

        public XmlRpcResponse GetHomeRegion(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            string userID_str = (string)requestData["userID"];
            UUID userID = UUID.Zero;
            UUID.TryParse(userID_str, out userID);

            Vector3 position = Vector3.UnitY, lookAt = Vector3.UnitY;
            GridRegion regInfo = m_HomeUsersService.GetHomeRegion(userID, out position, out lookAt);

            Hashtable hash = new Hashtable();
            if (regInfo == null)
                hash["result"] = "false";
            else
            {
                hash["result"] = "true";
                hash["uuid"] = regInfo.RegionID.ToString();
                hash["x"] = regInfo.RegionLocX.ToString();
                hash["y"] = regInfo.RegionLocY.ToString();
                hash["size_x"] = regInfo.RegionSizeX.ToString();
                hash["size_y"] = regInfo.RegionSizeY.ToString();
                hash["region_name"] = regInfo.RegionName;
                hash["hostname"] = regInfo.ExternalHostName;
                hash["http_port"] = regInfo.HttpPort.ToString();
                hash["server_uri"] = regInfo.ServerURI;
                hash["internal_port"] = regInfo.InternalEndPoint.Port.ToString();
                hash["position"] = position.ToString();
                hash["lookAt"] = lookAt.ToString();
            }
            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

        public XmlRpcResponse AgentIsComingHome(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            string sessionID_str = (string)requestData["sessionID"];
            UUID sessionID = UUID.Zero;
            UUID.TryParse(sessionID_str, out sessionID);
            string gridName = (string)requestData["externalName"];

            bool success = m_HomeUsersService.IsAgentComingHome(sessionID, gridName);

            Hashtable hash = new Hashtable();
            hash["result"] = success.ToString();
            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

        public XmlRpcResponse VerifyAgent(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            string sessionID_str = (string)requestData["sessionID"];
            UUID sessionID = UUID.Zero;
            UUID.TryParse(sessionID_str, out sessionID);
            string token = (string)requestData["token"];

            bool success = m_HomeUsersService.VerifyAgent(sessionID, token);

            Hashtable hash = new Hashtable();
            hash["result"] = success.ToString();
            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

        public XmlRpcResponse VerifyClient(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            string sessionID_str = (string)requestData["sessionID"];
            UUID sessionID = UUID.Zero;
            UUID.TryParse(sessionID_str, out sessionID);
            string token = (string)requestData["token"];

            bool success = m_HomeUsersService.VerifyClient(sessionID, token);

            Hashtable hash = new Hashtable();
            hash["result"] = success.ToString();
            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

        public XmlRpcResponse LogoutAgent(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            string sessionID_str = (string)requestData["sessionID"];
            UUID sessionID = UUID.Zero;
            UUID.TryParse(sessionID_str, out sessionID);
            string userID_str = (string)requestData["userID"];
            UUID userID = UUID.Zero;
            UUID.TryParse(userID_str, out userID);

            m_HomeUsersService.LogoutAgent(userID, sessionID);

            Hashtable hash = new Hashtable();
            hash["result"] = "true";
            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

        [Obsolete]
        public XmlRpcResponse StatusNotification(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable hash = new Hashtable();
            hash["result"] = "false";

            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            if (requestData.ContainsKey("userID") && requestData.ContainsKey("online"))
            {
                string userID_str = (string)requestData["userID"];
                UUID userID = UUID.Zero;
                UUID.TryParse(userID_str, out userID);
                List<string> ids = new List<string>();
                foreach (object key in requestData.Keys)
                {
                    if (key is string && ((string)key).StartsWith("friend_") && requestData[key] != null)
                        ids.Add(requestData[key].ToString());
                }
                bool online = false;
                bool.TryParse(requestData["online"].ToString(), out online);

                // let's spawn a thread for this, because it may take a long time...
                List<UUID> friendsOnline = m_HomeUsersService.StatusNotification(ids, userID, online);
                if (friendsOnline.Count > 0)
                {
                    int i = 0;
                    foreach (UUID id in friendsOnline)
                    {
                        hash["friend_" + i.ToString()] = id.ToString();
                        i++;
                    }
                }
                else
                    hash["result"] = "No Friends Online";

            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

        [Obsolete]
        public XmlRpcResponse GetOnlineFriends(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable hash = new Hashtable();

            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            if (requestData.ContainsKey("userID"))
            {
                string userID_str = (string)requestData["userID"];
                UUID userID = UUID.Zero;
                UUID.TryParse(userID_str, out userID);
                List<string> ids = new List<string>();
                foreach (object key in requestData.Keys)
                {
                    if (key is string && ((string)key).StartsWith("friend_") && requestData[key] != null)
                        ids.Add(requestData[key].ToString());
                }

                //List<UUID> online = m_HomeUsersService.GetOnlineFriends(userID, ids);
                //if (online.Count > 0)
                //{
                //    int i = 0;
                //    foreach (UUID id in online)
                //    {
                //        hash["friend_" + i.ToString()] = id.ToString();
                //        i++;
                //    }
                //}
                //else
                //    hash["result"] = "No Friends Online";
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

        public XmlRpcResponse GetUserInfo(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable hash = new Hashtable();
            Hashtable requestData = (Hashtable)request.Params[0];

            // This needs checking!
            if (requestData.ContainsKey("userID"))
            {
                string userID_str = (string)requestData["userID"];
                UUID userID = UUID.Zero;
                UUID.TryParse(userID_str, out userID);

                //int userFlags = m_HomeUsersService.GetUserFlags(userID);
                Dictionary<string,object> userInfo = m_HomeUsersService.GetUserInfo(userID);
                if (userInfo.Count > 0)
                {
                    foreach (KeyValuePair<string, object> kvp in userInfo)
                    {
                        hash[kvp.Key] = kvp.Value;
                    }
                }
                else
                {
                    hash["result"] = "failure";
                }
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;
        }

        public XmlRpcResponse GetServerURLs(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable hash = new Hashtable();

            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            if (requestData.ContainsKey("userID"))
            {
                string userID_str = (string)requestData["userID"];
                UUID userID = UUID.Zero;
                UUID.TryParse(userID_str, out userID);

                Dictionary<string, object> serverURLs = m_HomeUsersService.GetServerURLs(userID);
                if (serverURLs.Count > 0)
                {
                    foreach (KeyValuePair<string, object> kvp in serverURLs)
                        hash["SRV_" + kvp.Key] = kvp.Value.ToString();
                }
                else
                    hash["result"] = "No Service URLs";
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

        /// <summary>
        /// Locates the user.
        /// This is a sensitive operation, only authorized IP addresses can perform it.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="remoteClient"></param>
        /// <returns></returns>
        public XmlRpcResponse LocateUser(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable hash = new Hashtable();

            bool authorized = true;
            if (m_VerifyCallers)
            {
                authorized = false;
                foreach (string s in m_AuthorizedCallers)
                    if (s == remoteClient.Address.ToString())
                    {
                        authorized = true;
                        break;
                    }
            }

            if (authorized)
            {
                Hashtable requestData = (Hashtable)request.Params[0];
                //string host = (string)requestData["host"];
                //string portstr = (string)requestData["port"];
                if (requestData.ContainsKey("userID"))
                {
                    string userID_str = (string)requestData["userID"];
                    UUID userID = UUID.Zero;
                    UUID.TryParse(userID_str, out userID);

                    string url = m_HomeUsersService.LocateUser(userID);
                    if (url != string.Empty)
                        hash["URL"] = url;
                    else
                        hash["result"] = "Unable to locate user";
                }
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;

        }

        /// <summary>
        /// Returns the UUI of a user given a UUID.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="remoteClient"></param>
        /// <returns></returns>
        public XmlRpcResponse GetUUI(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable hash = new Hashtable();

            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            if (requestData.ContainsKey("userID") && requestData.ContainsKey("targetUserID"))
            {
                string userID_str = (string)requestData["userID"];
                UUID userID = UUID.Zero;
                UUID.TryParse(userID_str, out userID);

                string tuserID_str = (string)requestData["targetUserID"];
                UUID targetUserID = UUID.Zero;
                UUID.TryParse(tuserID_str, out targetUserID);
                string uui = m_HomeUsersService.GetUUI(userID, targetUserID);
                if (uui != string.Empty)
                    hash["UUI"] = uui;
                else
                    hash["result"] = "User unknown";
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;
        }

        /// <summary>
        /// Gets the UUID of a user given First name, Last name.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="remoteClient"></param>
        /// <returns></returns>
        public XmlRpcResponse GetUUID(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable hash = new Hashtable();

            Hashtable requestData = (Hashtable)request.Params[0];
            //string host = (string)requestData["host"];
            //string portstr = (string)requestData["port"];
            if (requestData.ContainsKey("first") && requestData.ContainsKey("last"))
            {
                string first = (string)requestData["first"];
                string last = (string)requestData["last"];
                UUID uuid = m_HomeUsersService.GetUUID(first, last);
                hash["UUID"] = uuid.ToString();
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;
            return response;
        }
    }
}