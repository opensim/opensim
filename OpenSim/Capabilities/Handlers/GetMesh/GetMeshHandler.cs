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
using System.Collections.Specialized;
using System.Reflection;
using System.IO;
using System.Web;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Capabilities.Handlers
{
    public class GetMeshHandler 
    {
//        private static readonly ILog m_log =
//            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        private IAssetService m_assetService;

        public GetMeshHandler(IAssetService assService)
        {
            m_assetService = assService;
        }

        public Hashtable ProcessGetMesh(Hashtable request, UUID AgentId, Caps cap)
        {
            Hashtable responsedata = new Hashtable();
            responsedata["int_response_code"] = 400; //501; //410; //404;
            responsedata["content_type"] = "text/plain";
            responsedata["keepalive"] = false;
            responsedata["str_response_string"] = "Request wasn't what was expected";

            string meshStr = string.Empty;

            if (request.ContainsKey("mesh_id"))
                meshStr = request["mesh_id"].ToString();

            UUID meshID = UUID.Zero;
            if (!String.IsNullOrEmpty(meshStr) && UUID.TryParse(meshStr, out meshID))
            {
                if (m_assetService == null)
                {
                    responsedata["int_response_code"] = 404; //501; //410; //404;
                    responsedata["content_type"] = "text/plain";
                    responsedata["keepalive"] = false;
                    responsedata["str_response_string"] = "The asset service is unavailable.  So is your mesh.";
                    return responsedata;
                }

                AssetBase mesh = m_assetService.Get(meshID.ToString());

                if (mesh != null)
                {
                    if (mesh.Type == (SByte)AssetType.Mesh)
                    {
                        responsedata["str_response_string"] = Convert.ToBase64String(mesh.Data);
                        responsedata["content_type"] = "application/vnd.ll.mesh";
                        responsedata["int_response_code"] = 200;
                    }
                    // Optionally add additional mesh types here
                    else
                    {
                        responsedata["int_response_code"] = 404; //501; //410; //404;
                        responsedata["content_type"] = "text/plain";
                        responsedata["keepalive"] = false;
                        responsedata["str_response_string"] = "Unfortunately, this asset isn't a mesh.";
                        return responsedata;
                    }
                }
                else
                {
                    responsedata["int_response_code"] = 404; //501; //410; //404;
                    responsedata["content_type"] = "text/plain";
                    responsedata["keepalive"] = false;
                    responsedata["str_response_string"] = "Your Mesh wasn't found.  Sorry!";
                    return responsedata;
                }
            }

            return responsedata;
        }
    }
}