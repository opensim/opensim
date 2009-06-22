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
using System.IO;
using System.Reflection;
using System.Net;
using System.Text;

using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Nwc.XmlRpc;
using Nini.Config;
using log4net;

namespace OpenSim.Server.Handlers.Authentication
{
    public class HGAuthenticationHandlers
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IAuthenticationService m_LocalService;

        public HGAuthenticationHandlers(IAuthenticationService service)
        {
            m_LocalService = service;
        }


        public XmlRpcResponse GenerateKeyMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();

            if (request.Params.Count < 2)
            {
                response.IsFault = true;
                response.SetFault(-1, "Invalid parameters");
                return response;
            }

            // Verify the key of who's calling
            UUID userID = UUID.Zero;
            string authKey = string.Empty;
            UUID.TryParse((string)request.Params[0], out userID);
            authKey = (string)request.Params[1];

            m_log.InfoFormat("[AUTH HANDLER] GenerateKey called with authToken {0}", authKey);
            string newKey = string.Empty;

            newKey = m_LocalService.GetKey(userID, authKey.ToString());
 
            response.Value = (string)newKey;
            return response;
        }

        public XmlRpcResponse VerifyKeyMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            bool success = false;
            XmlRpcResponse response = new XmlRpcResponse();

            if (request.Params.Count != 2)
            {
                response.IsFault = true;
                response.SetFault(-1, "Invalid parameters");
                return response;
            }

            // Verify the key of who's calling
            UUID userID = UUID.Zero;
            string authKey = string.Empty;
            if (UUID.TryParse((string)request.Params[0], out userID))
            {
                authKey = (string)request.Params[1];

                m_log.InfoFormat("[AUTH HANDLER] VerifyKey called with key {0}", authKey);

                success = m_LocalService.VerifyKey(userID, authKey);
            }

            m_log.DebugFormat("[AUTH HANDLER]: Response to VerifyKey is {0}", success);
            response.Value = success;
            return response;
        }

    }
}
