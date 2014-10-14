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
using System.Text;
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
    public class GetMeshHandler : BaseStreamHandler
    {
//        private static readonly ILog m_log =
//            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        private IAssetService m_assetService;

        public GetMeshHandler(string path, IAssetService assService, string name, string description)
            : base("GET", path, name, description)
        {
            m_assetService = assService;
        }

        protected override byte[] ProcessRequest(string path, Stream request, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            // Try to parse the texture ID from the request URL
            NameValueCollection query = HttpUtility.ParseQueryString(httpRequest.Url.Query);
            string meshStr = query.GetOne("mesh_id");

//            m_log.DebugFormat("Fetching mesh {0}", meshStr);

            UUID meshID = UUID.Zero;
            if (!String.IsNullOrEmpty(meshStr) && UUID.TryParse(meshStr, out meshID))
            {
                if (m_assetService == null)
                {
                    httpResponse.StatusCode = 404;
                    httpResponse.ContentType = "text/plain";
                    byte[] data = Encoding.UTF8.GetBytes("The asset service is unavailable.  So is your mesh.");
                    httpResponse.Body.Write(data, 0, data.Length);
                    return null;
                }

                AssetBase mesh = m_assetService.Get(meshID.ToString());

                if (mesh != null)
                {
                    if (mesh.Type == (SByte)AssetType.Mesh)
                    {
                        byte[] data = mesh.Data;
                        httpResponse.Body.Write(data, 0, data.Length);
                        httpResponse.ContentType = "application/vnd.ll.mesh";
                        httpResponse.StatusCode = 200;
                    }
                    // Optionally add additional mesh types here
                    else
                    {
                        httpResponse.StatusCode = 404;
                        httpResponse.ContentType = "text/plain";
                        byte[] data = Encoding.UTF8.GetBytes("Unfortunately, this asset isn't a mesh.");
                        httpResponse.Body.Write(data, 0, data.Length);
                        httpResponse.KeepAlive = false;
                    }
                }
                else
                {
                    httpResponse.StatusCode = 404;
                    httpResponse.ContentType = "text/plain";
                    byte[] data = Encoding.UTF8.GetBytes("Your Mesh wasn't found.  Sorry!");
                    httpResponse.Body.Write(data, 0, data.Length);
                    httpResponse.KeepAlive = false;
                }
            }

            return null;
        }
    }
}