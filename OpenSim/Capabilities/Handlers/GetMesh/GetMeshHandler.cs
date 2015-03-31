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

using log4net;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using System;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Web;

namespace OpenSim.Capabilities.Handlers
{
    public class GetMeshHandler : BaseStreamHandler
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private IAssetService m_assetService;

        // TODO: Change this to a config option
        private string m_RedirectURL = null;

        public GetMeshHandler(string path, IAssetService assService, string name, string description, string redirectURL)
            : base("GET", path, name, description)
        {
            m_assetService = assService;
            m_RedirectURL = redirectURL;
            if (m_RedirectURL != null && !m_RedirectURL.EndsWith("/"))
                m_RedirectURL += "/";
        }

        protected override byte[] ProcessRequest(string path, Stream request, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            // Try to parse the texture ID from the request URL
            NameValueCollection query = HttpUtility.ParseQueryString(httpRequest.Url.Query);
            string textureStr = query.GetOne("mesh_id");

            if (m_assetService == null)
            {
                m_log.Error("[GETMESH]: Cannot fetch mesh " + textureStr + " without an asset service");
                httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
            }

            UUID meshID;
            if (!String.IsNullOrEmpty(textureStr) && UUID.TryParse(textureStr, out meshID))
            {
                // OK, we have an array with preferred formats, possibly with only one entry

                httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                AssetBase mesh;

                if (!String.IsNullOrEmpty(m_RedirectURL))
                {
                    // Only try to fetch locally cached meshes. Misses are redirected
                    mesh = m_assetService.GetCached(meshID.ToString());

                    if (mesh != null)
                    {
                        if (mesh.Type != (sbyte)AssetType.Mesh)
                        {
                            httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                        }
                        WriteMeshData(httpRequest, httpResponse, mesh);
                    }
                    else
                    {
                        string textureUrl = m_RedirectURL + "?mesh_id="+ meshID.ToString();
                        m_log.Debug("[GETMESH]: Redirecting mesh request to " + textureUrl);
                        httpResponse.StatusCode = (int)OSHttpStatusCode.RedirectMovedPermanently;
                        httpResponse.RedirectLocation = textureUrl;
                        return null;
                    }
                }
                else // no redirect
                {
                    // try the cache
                    mesh = m_assetService.GetCached(meshID.ToString());

                    if (mesh == null)
                    {
                        // Fetch locally or remotely. Misses return a 404
                        mesh = m_assetService.Get(meshID.ToString());

                        if (mesh != null)
                        {
                            if (mesh.Type != (sbyte)AssetType.Mesh)
                            {
                                httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                                return null;
                            }
                            WriteMeshData(httpRequest, httpResponse, mesh);
                            return null;
                        }
                   }
                   else // it was on the cache
                   {
                       if (mesh.Type != (sbyte)AssetType.Mesh)
                       {
                           httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                           return null;
                       }
                       WriteMeshData(httpRequest, httpResponse, mesh);
                       return null;
                   }
                }

                // not found
                httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                return null;
            }
            else
            {
                m_log.Warn("[GETTEXTURE]: Failed to parse a mesh_id from GetMesh request: " + httpRequest.Url);
            }

            return null;
        }

        private void WriteMeshData(IOSHttpRequest request, IOSHttpResponse response, AssetBase texture)
        {
            string range = request.Headers.GetOne("Range");

            if (!String.IsNullOrEmpty(range))
            {
                // Range request
                int start, end;
                if (TryParseRange(range, out start, out end))
                {
                    // Before clamping start make sure we can satisfy it in order to avoid
                    // sending back the last byte instead of an error status
                    if (start >= texture.Data.Length)
                    {
                        response.StatusCode = (int)System.Net.HttpStatusCode.PartialContent;
                        response.ContentType = texture.Metadata.ContentType;
                    }
                    else
                    {
                        // Handle the case where no second range value was given.  This is equivalent to requesting
                        // the rest of the entity.
                        if (end == -1)
                            end = int.MaxValue;

                        end = Utils.Clamp(end, 0, texture.Data.Length - 1);
                        start = Utils.Clamp(start, 0, end);
                        int len = end - start + 1;

                        if (0 == start && len == texture.Data.Length)
                        {
                            response.StatusCode = (int)System.Net.HttpStatusCode.OK;
                        }
                        else
                        {
                            response.StatusCode = (int)System.Net.HttpStatusCode.PartialContent;
                            response.AddHeader("Content-Range", String.Format("bytes {0}-{1}/{2}", start, end, texture.Data.Length));
                        }
                        
                        response.ContentLength = len;
                        response.ContentType = "application/vnd.ll.mesh";
    
                        response.Body.Write(texture.Data, start, len);
                    }
                }
                else
                {
                    m_log.Warn("[GETMESH]: Malformed Range header: " + range);
                    response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
                }
            }
            else 
            {
                // Full content request
                response.StatusCode = (int)System.Net.HttpStatusCode.OK;
                response.ContentLength = texture.Data.Length;
                response.ContentType = "application/vnd.ll.mesh";
                response.Body.Write(texture.Data, 0, texture.Data.Length);
            }
        }

        /// <summary>
        /// Parse a range header.
        /// </summary>
        /// <remarks>
        /// As per http://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html,
        /// this obeys range headers with two values (e.g. 533-4165) and no second value (e.g. 533-).
        /// Where there is no value, -1 is returned.
        /// FIXME: Need to cover the case where only a second value is specified (e.g. -4165), probably by returning -1
        /// for start.</remarks>
        /// <returns></returns>
        /// <param name='header'></param>
        /// <param name='start'>Start of the range.  Undefined if this was not a number.</param>
        /// <param name='end'>End of the range.  Will be -1 if no end specified.  Undefined if there was a raw string but this was not a number.</param>
        private bool TryParseRange(string header, out int start, out int end)
        {
            start = end = 0;

            if (header.StartsWith("bytes="))
            {
                string[] rangeValues = header.Substring(6).Split('-');

                if (rangeValues.Length == 2)
                {
                    if (!Int32.TryParse(rangeValues[0], out start))
                        return false;

                    string rawEnd = rangeValues[1];

                    if (rawEnd == "")
                    {
                        end = -1;
                        return true;
                    }
                    else if (Int32.TryParse(rawEnd, out end))
                    {
                        return true;
                    }
                }
            }

            start = end = 0;
            return false;
        }
    }
}