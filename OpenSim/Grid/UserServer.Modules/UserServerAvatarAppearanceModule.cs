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
    public class UserServerAvatarAppearanceModule
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private UserDataBaseService m_userDataBaseService;
        private BaseHttpServer m_httpServer;

        public UserServerAvatarAppearanceModule(UserDataBaseService userDataBaseService)
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

            m_httpServer.AddXmlRPCHandler("get_avatar_appearance", XmlRPCGetAvatarAppearance);
            m_httpServer.AddXmlRPCHandler("update_avatar_appearance", XmlRPCUpdateAvatarAppearance);
        }

        public XmlRpcResponse XmlRPCGetAvatarAppearance(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            AvatarAppearance appearance;
            Hashtable responseData;
            if (requestData.Contains("owner"))
            {
                appearance = m_userDataBaseService.GetUserAppearance(new UUID((string)requestData["owner"]));
                if (appearance == null)
                {
                    responseData = new Hashtable();
                    responseData["error_type"] = "no appearance";
                    responseData["error_desc"] = "There was no appearance found for this avatar";
                }
                else
                {
                    responseData = appearance.ToHashTable();
                }
            }
            else
            {
                responseData = new Hashtable();
                responseData["error_type"] = "unknown_avatar";
                responseData["error_desc"] = "The avatar appearance requested is not in the database";
            }

            response.Value = responseData;
            return response;
        }

        public XmlRpcResponse XmlRPCUpdateAvatarAppearance(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData;
            if (requestData.Contains("owner"))
            {
                AvatarAppearance appearance = new AvatarAppearance(requestData);
                
                // TODO: Sometime in the future we may have a database layer that is capable of updating appearance when
                // the TextureEntry is null. When that happens, this check can be removed
                if (appearance.Texture != null)
                    m_userDataBaseService.UpdateUserAppearance(new UUID((string)requestData["owner"]), appearance);

                responseData = new Hashtable();
                responseData["returnString"] = "TRUE";
            }
            else
            {
                responseData = new Hashtable();
                responseData["error_type"] = "unknown_avatar";
                responseData["error_desc"] = "The avatar appearance requested is not in the database";
            }
            response.Value = responseData;
            return response;
        }
    }
}
