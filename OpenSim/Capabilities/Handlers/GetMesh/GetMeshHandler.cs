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
        private static readonly ILog m_log =
                   LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IAssetService m_assetService;

        public const string DefaultFormat = "vnd.ll.mesh";

        public GetMeshHandler(IAssetService assService)
        {
            m_assetService = assService;
        }
        public Hashtable Handle(Hashtable request)
        {
            return ProcessGetMesh(request, UUID.Zero, null); ;
        }

        public Hashtable ProcessGetMesh(Hashtable request, UUID AgentId, Caps cap)
        {
            Hashtable responsedata = new Hashtable();
            if (m_assetService == null)
            {
                responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.ServiceUnavailable;
                responsedata["str_response_string"] = "The asset service is unavailable";
                responsedata["keepalive"] = false;
                return responsedata;
            }

            responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.BadRequest;
            responsedata["content_type"] = "text/plain";
            responsedata["int_bytes"] = 0;

            string meshStr = string.Empty;
            if (request.ContainsKey("mesh_id"))
                meshStr = request["mesh_id"].ToString();

            if (String.IsNullOrEmpty(meshStr))
                return responsedata;

            UUID meshID = UUID.Zero;
            if(!UUID.TryParse(meshStr, out meshID))
                return responsedata;

            AssetBase mesh = m_assetService.Get(meshID.ToString());
            if(mesh == null)
            {
                responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.NotFound;
                responsedata["str_response_string"] = "Mesh not found.";
                return responsedata;
            }

            if (mesh.Type != (SByte)AssetType.Mesh)
            {
                responsedata["str_response_string"] = "Asset isn't a mesh.";
                return responsedata;
            }

            string range = String.Empty;

            if (((Hashtable)request["headers"])["range"] != null)
               range = (string)((Hashtable)request["headers"])["range"];
            else if (((Hashtable)request["headers"])["Range"] != null)
                range = (string)((Hashtable)request["headers"])["Range"];

            responsedata["content_type"] = "application/vnd.ll.mesh";
            if (String.IsNullOrEmpty(range))
            {
                // full mesh
                responsedata["str_response_string"] = Convert.ToBase64String(mesh.Data);
                responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.OK;
                return responsedata;
            }

            // range request
            int start, end;
            if (Util.TryParseHttpRange(range, out start, out end))
            {
                // Before clamping start make sure we can satisfy it in order to avoid
                // sending back the last byte instead of an error status
                if (start >= mesh.Data.Length)
                {
                    responsedata["str_response_string"] = "This range doesnt exist.";
                    return responsedata;
                }

                end = Utils.Clamp(end, 0, mesh.Data.Length - 1);
                start = Utils.Clamp(start, 0, end);
                int len = end - start + 1;

                //m_log.Debug("Serving " + start + " to " + end + " of " + texture.Data.Length + " bytes for texture " + texture.ID);
                Hashtable headers = new Hashtable();
                headers["Content-Range"] = String.Format("bytes {0}-{1}/{2}", start, end, mesh.Data.Length);
                responsedata["headers"] = headers;
                responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.PartialContent;

                byte[] d = new byte[len];
                Array.Copy(mesh.Data, start, d, 0, len);
                responsedata["bin_response_data"] = d;
                responsedata["int_bytes"] = len;
                return responsedata;
            }

            m_log.Warn("[GETMESH]: Failed to parse a range from GetMesh request, sending full asset: " + (string)request["uri"]);
            responsedata["str_response_string"] = Convert.ToBase64String(mesh.Data);
            responsedata["int_response_code"] = (int)System.Net.HttpStatusCode.OK;
            return responsedata;
        }
    }
}