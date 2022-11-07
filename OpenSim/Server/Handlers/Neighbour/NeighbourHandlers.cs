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
using System.IO;
using System.Reflection;
using System.Net;
using System.Text;

using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;


namespace OpenSim.Server.Handlers.Neighbour
{
    public class NeighbourSimpleHandler : SimpleStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private INeighbourService m_NeighbourService;
        private IAuthenticationService m_AuthenticationService;

        public NeighbourSimpleHandler(INeighbourService service, IAuthenticationService authentication) :
                base("/region")
        {
            m_NeighbourService = service;
            m_AuthenticationService = authentication;
        }

        protected override void ProcessRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            httpResponse.KeepAlive = false;

            if (m_NeighbourService == null)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                return;
            }

            switch (httpRequest.HttpMethod)
            {
                case "POST":
                {
                    OSDMap args = RestHandlerUtils.DeserializeOSMap(httpRequest);
                    if (args == null)
                    {
                        httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                        httpResponse.RawBuffer = Util.UTF8.GetBytes("false");
                        return;
                    }

                    if (!RestHandlerUtils.GetParams(httpRequest.UriPath, out UUID regionID, out ulong regionHandle, out string action)
                        || regionID.IsZero())
                    {
                        m_log.InfoFormat("[RegionPostHandler]: Invalid parameters for neighbour message {0}", httpRequest.UriPath);
                        httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }
                    ProcessPostRequest(args, httpRequest, httpResponse, regionID);
                    break;
                }
                case "GET":
                case "PUT":
                case "DELETE":
                    httpResponse.StatusCode = (int)HttpStatusCode.NotImplemented;
                    return;
                default:
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    return;
                }
            }
        }

        // TODO: unused: private bool m_AllowForeignGuests;
        protected void ProcessPostRequest(OSDMap args, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, UUID regionID)
        {
            if (m_AuthenticationService != null)
            {
                // Authentication
                string authority = string.Empty;
                string authToken = string.Empty;
                if (!RestHandlerUtils.GetAuthentication(httpRequest, out authority, out authToken))
                {
                    m_log.InfoFormat("[RegionPostHandler]: Authentication failed for neighbour message");
                    httpResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                    return;
                }
                // TODO: Rethink this
                //if (!m_AuthenticationService.VerifyKey(regionID, authToken))
                //{
                //    m_log.InfoFormat("[RegionPostHandler]: Authentication failed for neighbour message {0}", path);
                //    httpResponse.StatusCode = (int)HttpStatusCode.Forbidden;
                //    return result;
                //}
                m_log.DebugFormat("[RegionPostHandler]: Authentication succeeded for {0}", regionID);
            }

            // retrieve the regionhandle
            ulong regionhandle = 0;
            if (args["destination_handle"] != null)
                UInt64.TryParse(args["destination_handle"].AsString(), out regionhandle);

            RegionInfo aRegion = new RegionInfo();
            try
            {
                aRegion.UnpackRegionInfoData(args);
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[RegionPostHandler]: exception on unpacking region info {0}", ex.Message);
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            // Finally!
            GridRegion thisRegion = m_NeighbourService.HelloNeighbour(regionhandle, aRegion);

            OSDMap resp = new OSDMap(1);

            if (thisRegion != null)
                resp["success"] = OSD.FromBoolean(true);
            else
                resp["success"] = OSD.FromBoolean(false);

            httpResponse.RawBuffer = Util.UTF8.GetBytes(OSDParser.SerializeJsonString(resp));
            httpResponse.StatusCode = (int)HttpStatusCode.OK;
        }
    }
}

