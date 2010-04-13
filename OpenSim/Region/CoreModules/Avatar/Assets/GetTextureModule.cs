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
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.CoreModules.Avatar.ObjectCaps
{
    #region Stream Handler

    public delegate byte[] StreamHandlerCallback(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse);

    public class StreamHandler : BaseStreamHandler
    {
        StreamHandlerCallback m_callback;

        public StreamHandler(string httpMethod, string path, StreamHandlerCallback callback)
            : base(httpMethod, path)
        {
            m_callback = callback;
        }

        public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            return m_callback(path, request, httpRequest, httpResponse);
        }
    }

    #endregion Stream Handler

    public class GetTextureModule : IRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Scene m_scene;
        private IAssetService m_assetService;

        #region IRegionModule Members

        public void Initialise(Scene pScene, IConfigSource pSource)
        {
            m_scene = pScene;
        }

        public void PostInitialise()
        {
            m_assetService = m_scene.RequestModuleInterface<IAssetService>();
            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
        }

        public void Close() { }

        public string Name { get { return "GetTextureModule"; } }
        public bool IsSharedModule { get { return false; } }

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            UUID capID = UUID.Random();

            m_log.Info("[GETTEXTURE]: /CAPS/" + capID);
            caps.RegisterHandler("GetTexture", new StreamHandler("GET", "/CAPS/" + capID, ProcessGetTexture));
        }

        #endregion

        private byte[] ProcessGetTexture(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            // TODO: Change this to a config option
            const string REDIRECT_URL = null;

            // Try to parse the texture ID from the request URL
            NameValueCollection query = HttpUtility.ParseQueryString(httpRequest.Url.Query);
            string textureStr = GetOne(query, "texture_id");

            if (m_assetService == null)
            {
                m_log.Error("[GETTEXTURE]: Cannot fetch texture " + textureStr + " without an asset service");
                httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                return null;
            }

            UUID textureID;
            if (!String.IsNullOrEmpty(textureStr) && UUID.TryParse(textureStr, out textureID))
            {
                AssetBase texture;

                if (!String.IsNullOrEmpty(REDIRECT_URL))
                {
                    // Only try to fetch locally cached textures. Misses are redirected
                    texture = m_assetService.GetCached(textureID.ToString());

                    if (texture != null)
                    {
                        SendTexture(httpRequest, httpResponse, texture);
                    }
                    else
                    {
                        string textureUrl = REDIRECT_URL + textureID.ToString();
                        m_log.Debug("[GETTEXTURE]: Redirecting texture request to " + textureUrl);
                        httpResponse.RedirectLocation = textureUrl;
                    }
                }
                else
                {
                    // Fetch locally or remotely. Misses return a 404
                    texture = m_assetService.Get(textureID.ToString());

                    if (texture != null)
                    {
                        SendTexture(httpRequest, httpResponse, texture);
                    }
                    else
                    {
                        m_log.Warn("[GETTEXTURE]: Texture " + textureID + " not found");
                        httpResponse.StatusCode = (int)System.Net.HttpStatusCode.NotFound;
                    }
                }
            }
            else
            {
                m_log.Warn("[GETTEXTURE]: Failed to parse a texture_id from GetTexture request: " + httpRequest.Url);
            }

            httpResponse.Send();
            return null;
        }

        private void SendTexture(OSHttpRequest request, OSHttpResponse response, AssetBase texture)
        {
            string range = GetOne(request.Headers, "Range");
            if (!String.IsNullOrEmpty(range))
            {
                // Range request
                int start, end;
                if (TryParseRange(range, out start, out end))
                {
                    end = Utils.Clamp(end, 1, texture.Data.Length);
                    start = Utils.Clamp(start, 0, end - 1);

                    m_log.Debug("Serving " + start + " to " + end + " of " + texture.Data.Length + " bytes for texture " + texture.ID);

                    if (end - start < texture.Data.Length)
                        response.StatusCode = (int)System.Net.HttpStatusCode.PartialContent;

                    response.ContentLength = end - start;
                    response.ContentType = texture.Metadata.ContentType;

                    response.Body.Write(texture.Data, start, end - start);
                }
                else
                {
                    m_log.Warn("Malformed Range header: " + range);
                    response.StatusCode = (int)System.Net.HttpStatusCode.BadRequest;
                }
            }
            else
            {
                // Full content request
                response.ContentLength = texture.Data.Length;
                response.ContentType = texture.Metadata.ContentType;
                response.Body.Write(texture.Data, 0, texture.Data.Length);
            }
        }

        private bool TryParseRange(string header, out int start, out int end)
        {
            if (header.StartsWith("bytes="))
            {
                string[] rangeValues = header.Substring(6).Split('-');
                if (rangeValues.Length == 2)
                {
                    if (Int32.TryParse(rangeValues[0], out start) && Int32.TryParse(rangeValues[1], out end))
                        return true;
                }
            }

            start = end = 0;
            return false;
        }

        private static string GetOne(NameValueCollection collection, string key)
        {
            string[] values = collection.GetValues(key);
            if (values != null && values.Length > 0)
                return values[0];

            return null;
        }
    }
}
