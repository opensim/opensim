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
using System.Threading;
using System.Timers;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Grid.Framework;
using Timer = System.Timers.Timer;

namespace OpenSim.Grid.MessagingServer.Modules
{
    public class InterMessageUserServerModule : IInterServiceUserService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private MessageServerConfig m_cfg;

        private IGridServiceCore m_messageCore;

        /// <value>
        /// Reregister with the user service every 5 minutes
        /// </value>
        private Timer reconnectTimer = new Timer(300000);

        public InterMessageUserServerModule(MessageServerConfig config, IGridServiceCore messageCore)
        {
            m_cfg = config;
            m_messageCore = messageCore;

            reconnectTimer.Elapsed += registerWithUserServer;
            lock (reconnectTimer)
                reconnectTimer.Start();
        }

        public void Initialise()
        {
            m_messageCore.RegisterInterface<IInterServiceUserService>(this);
        }

        public void PostInitialise()
        {
        }

        public void RegisterHandlers()
        {
            //have these in separate method as some servers restart the http server and reregister all the handlers.           
        }

        public void registerWithUserServer(object sender, ElapsedEventArgs e)
        {
            registerWithUserServer();
        }

        public bool registerWithUserServer()
        {
            Hashtable UserParams = new Hashtable();
            // Login / Authentication

            if (m_cfg.HttpSSL)
            {
                UserParams["uri"] = "https://" + m_cfg.MessageServerIP + ":" + m_cfg.HttpPort;
            }
            else
            {
                UserParams["uri"] = "http://" + m_cfg.MessageServerIP + ":" + m_cfg.HttpPort;
            }

            UserParams["recvkey"] = m_cfg.UserRecvKey;
            UserParams["sendkey"] = m_cfg.UserRecvKey;

            // Package into an XMLRPC Request
            ArrayList SendParams = new ArrayList();
            SendParams.Add(UserParams);

            bool success = true;
            string[] servers = m_cfg.UserServerURL.Split(' ');

            foreach (string srv in servers)
            {
                // Send Request
                try
                {
                    XmlRpcRequest UserReq = new XmlRpcRequest("register_messageserver", SendParams);
                    XmlRpcResponse UserResp = (XmlRpcResponse)UserReq.Invoke(srv);
                    //XmlRpcResponse UserResp = UserReq.Send(srv, 16000);

                    // Process Response
                    Hashtable GridRespData = (Hashtable)UserResp.Value;
                    // if we got a response, we were successful
                    if (!GridRespData.ContainsKey("responsestring"))
                        success = false;
                    else
                        m_log.DebugFormat("[SERVER]: Registered with user service at {0}", srv);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[SERVER]: Unable to connect to server {0} for registration. User service not running?  Exception {1} {2}",
                        srv, e.Message, e.StackTrace);
                    success = false;
                }
            }
            
            return success;
        }

        public bool deregisterWithUserServer()
        {
            Hashtable request = new Hashtable();

            return SendToUserServer(request, "deregister_messageserver");
        }

        public bool SendToUserServer(Hashtable request, string method)
        {
            // Login / Authentication

            if (m_cfg.HttpSSL)
            {
                request["uri"] = "https://" + m_cfg.MessageServerIP + ":" + m_cfg.HttpPort;
            }
            else
            {
                request["uri"] = "http://" + m_cfg.MessageServerIP + ":" + m_cfg.HttpPort;
            }

            request["recvkey"] = m_cfg.UserRecvKey;
            request["sendkey"] = m_cfg.UserRecvKey;

            // Package into an XMLRPC Request
            ArrayList SendParams = new ArrayList();
            SendParams.Add(request);

            bool success = true;
            string[] servers = m_cfg.UserServerURL.Split(' ');

            // Send Request
            foreach (string srv in servers)
            {
                try
                {
                    XmlRpcRequest UserReq = new XmlRpcRequest(method, SendParams);
                    XmlRpcResponse UserResp = UserReq.Send(m_cfg.UserServerURL, 16000);
                    // Process Response
                    Hashtable UserRespData = (Hashtable)UserResp.Value;
                    // if we got a response, we were successful
                    if (!UserRespData.ContainsKey("responsestring"))
                        success = false;
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[SERVER]: Unable to connect to server {0} for send. Server not running?  Exception {0} {1}", 
                        srv, e.Message, e.StackTrace);
                    success = false;
                }
            }
            return success;
        }
    }
}
