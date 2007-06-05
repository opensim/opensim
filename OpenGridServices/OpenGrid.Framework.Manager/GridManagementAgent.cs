/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Servers;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;

namespace OpenGrid.Framework.Manager
{
    /// <summary>
    /// Used to pass messages to the gridserver
    /// </summary>
    /// <param name="param">Pass this argument</param>
    public delegate void GridManagerCallback(string param);

    /// <summary>
    /// Serverside listener for grid commands
    /// </summary>
    public class GridManagementAgent
    {
        /// <summary>
        /// Passes grid server messages
        /// </summary>
        private GridManagerCallback thecallback;

        /// <summary>
        /// Security keys
        /// </summary>
        private string sendkey;
        private string recvkey;

        /// <summary>
        /// Our component type
        /// </summary>
        private string component_type;

        /// <summary>
        /// List of active sessions
        /// </summary>
        private static ArrayList Sessions;

        /// <summary>
        /// Initialises a new GridManagementAgent
        /// </summary>
        /// <param name="app_httpd">HTTP Daemon for this server</param>
        /// <param name="component_type">What component type are we?</param>
        /// <param name="sendkey">Security send key</param>
        /// <param name="recvkey">Security recieve key</param>
        /// <param name="thecallback">Message callback</param>
        public GridManagementAgent(BaseHttpServer app_httpd, string component_type, string sendkey, string recvkey, GridManagerCallback thecallback)
        {
            this.sendkey = sendkey;
            this.recvkey = recvkey;
            this.component_type = component_type;
            this.thecallback = thecallback;
            Sessions = new ArrayList();

            app_httpd.AddXmlRPCHandler("manager_login", XmlRpcLoginMethod);

            switch (component_type)
            {
                case "gridserver":
                    GridServerManager.sendkey = this.sendkey;
                    GridServerManager.recvkey = this.recvkey;
                    GridServerManager.thecallback = thecallback;
                    app_httpd.AddXmlRPCHandler("shutdown", GridServerManager.XmlRpcShutdownMethod);
                    break;
            }
        }

        /// <summary>
        /// Checks if a session exists
        /// </summary>
        /// <param name="sessionID">The session ID</param>
        /// <returns>Exists?</returns>
        public static bool SessionExists(LLUUID sessionID)
        {
            return Sessions.Contains(sessionID);
        }

        /// <summary>
        /// Logs a new session to the grid manager
        /// </summary>
        /// <param name="request">the XMLRPC request</param>
        /// <returns>An XMLRPC reply</returns>
        public static XmlRpcResponse XmlRpcLoginMethod(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();

            // TODO: Switch this over to using OpenGrid.Framework.Data
            if (requestData["username"].Equals("admin") && requestData["password"].Equals("supersecret"))
            {
                response.IsFault = false;
                LLUUID new_session = LLUUID.Random();
                Sessions.Add(new_session);
                responseData["session_id"] = new_session.ToString();
                responseData["msg"] = "Login OK";
            }
            else
            {
                response.IsFault = true;
                responseData["error"] = "Invalid username or password";
            }

            response.Value = responseData;
            return response;

        }

    }
}
