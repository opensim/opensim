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
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;

using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Communications;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Services.Connectors
{
    public class MapImageServicesConnector : IMapImageService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;
        private IImprovedAssetCache m_Cache = null;

        public MapImageServicesConnector()
        {
        }

        public MapImageServicesConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/');
        }

        public MapImageServicesConnector(IConfigSource source)
        {
            Initialise(source);
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["MapImageService"];
            if (config == null)
            {
                m_log.Error("[MAP IMAGE CONNECTOR]: MapImageService missing");
                throw new Exception("MapImage connector init error");
            }

            string serviceURI = config.GetString("MapImageServerURI",
                    String.Empty);

            if (serviceURI == String.Empty)
            {
                m_log.Error("[MAP IMAGE CONNECTOR]: No Server URI named in section MapImageService");
                throw new Exception("MapImage connector init error");
            }
            m_ServerURI = serviceURI;
        }

        public bool AddMapTile(int x, int y, byte[] pngData, out string reason)
        {
            List<MultipartForm.Element> postParameters = new List<MultipartForm.Element>()
            {
                new MultipartForm.Parameter("X", x.ToString()),
                new MultipartForm.Parameter("Y", y.ToString()),
                new MultipartForm.File("Tile", "tile.png", "image/png", pngData)
            };

           reason = string.Empty;
            int tickstart = Util.EnvironmentTickCount();

            // Make the remote storage request
            try
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(m_ServerURI);
                request.Timeout = 20000;
                request.ReadWriteTimeout = 5000;

                using (HttpWebResponse response = MultipartForm.Post(request, postParameters))
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        string responseStr = responseStream.GetStreamString();
                        OSD responseOSD = OSDParser.Deserialize(responseStr);
                        if (responseOSD.Type == OSDType.Map)
                        {
                            OSDMap responseMap = (OSDMap)responseOSD;
                            if (responseMap["Success"].AsBoolean())
                                return true;

                            reason = "Upload failed: " + responseMap["Message"].AsString();
                        }
                        else
                        {
                            reason = "Response format was invalid:\n" + responseStr;
                        }
                    }
                }
            }
            catch (WebException we)
            {
                reason = we.Message;
                if (we.Status == WebExceptionStatus.ProtocolError)
                {
                    HttpWebResponse webResponse = (HttpWebResponse)we.Response;
                    reason = String.Format("[{0}] {1}", webResponse.StatusCode, webResponse.StatusDescription);
                }
            }
            catch (Exception ex)
            {
                reason = ex.Message;
            }
            finally
            {
                // This just dumps a warning for any operation that takes more than 100 ms
                int tickdiff = Util.EnvironmentTickCountSubtract(tickstart);
                m_log.DebugFormat("[MAP IMAGE CONNECTOR]: map tile uploaded in {0}ms", tickdiff);
            }

            return false;
        }

        public byte[] GetMapTile(string fileName, out string format)
        {
            format = string.Empty;
            new Exception("GetMapTile method not Implemented");
            return null;
        }
    }
}
