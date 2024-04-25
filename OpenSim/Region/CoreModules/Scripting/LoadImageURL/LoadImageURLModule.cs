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
using System.Drawing;
using System.IO;
using System.Net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using log4net;
using System.Reflection;
using Mono.Addins;

namespace OpenSim.Region.CoreModules.Scripting.LoadImageURL
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LoadImageURLModule")]
    public class LoadImageURLModule : ISharedRegionModule, IDynamicTextureRender
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_name = "LoadImageURL";
        private Scene m_scene;
        private IDynamicTextureManager m_textureManager;

        private OutboundUrlFilter m_outboundUrlFilter;
        WebProxy m_proxy = null;

        #region IDynamicTextureRender Members

        public string GetName()
        {
            return m_name;
        }

        public string GetContentType()
        {
            return ("image");
        }

        public bool SupportsAsynchronous()
        {
            return true;
        }

//        public bool AlwaysIdenticalConversion(string bodyData, string extraParams)
//        {
//            // We don't support conversion of body data.
//            return false;
//        }

        public IDynamicTexture ConvertUrl(string url, string extraParams)
        {
            return null;
        }

        public IDynamicTexture ConvertData(string bodyData, string extraParams)
        {
            return null;
        }

        public bool AsyncConvertUrl(UUID id, string url, string extraParams)
        {
            return MakeHttpRequest(url, id);
        }

        public bool AsyncConvertData(UUID id, string bodyData, string extraParams)
        {
            return false;
        }

        public void GetDrawStringSize(string text, string fontName, int fontSize,
                                      out double xSize, out double ySize)
        {
            xSize = 0;
            ySize = 0;
        }

        #endregion

        #region ISharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
            m_outboundUrlFilter = new OutboundUrlFilter("Script dynamic texture image module", config);
            string proxyurl = config.Configs["Startup"].GetString("HttpProxy");
            if(!string.IsNullOrEmpty(proxyurl))
            {
                string proxyexcepts = config.Configs["Startup"].GetString("HttpProxyExceptions");
                if (!string.IsNullOrEmpty(proxyexcepts))
                {
                    string[] elist = proxyexcepts.Split(';');
                    m_proxy = new WebProxy(proxyurl, true, elist);
                }
                else
                {
                    m_proxy = new WebProxy(proxyurl, true);
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void AddRegion(Scene scene)
        {
            m_scene ??= scene;
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
            if (m_textureManager is null && m_scene == scene)
            {
                m_textureManager = m_scene.RequestModuleInterface<IDynamicTextureManager>();
                m_textureManager?.RegisterRender(GetContentType(), this);
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return m_name; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        private bool MakeHttpRequest(string url, UUID requestID)
        {
            if (m_textureManager is null)
            {
                m_log.WarnFormat("[LOADIMAGEURLMODULE]: No texture manager. Can't function.");
                return false;
            }

            if (!m_outboundUrlFilter.CheckAllowed(new Uri(url)))
                return false;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.AllowAutoRedirect = false;

            if(m_proxy is not null)
                request.Proxy = m_proxy;

            RequestState state = new RequestState(request, requestID);
            // IAsyncResult result = request.BeginGetResponse(new AsyncCallback(HttpRequestReturn), state);
            request.BeginGetResponse(HttpRequestReturn, state);
            return true;
        }

        private void HttpRequestReturn(IAsyncResult result)
        {
            if (m_textureManager == null)
                return;

            RequestState state = (RequestState) result.AsyncState;
            WebRequest request = (WebRequest) state.Request;
            Stream stream = null;
            byte[] imageJ2000 = Array.Empty<byte>();
            Size newSize = new Size(0, 0);
            HttpWebResponse response = null;

            try
            {
                response = (HttpWebResponse)request.EndGetResponse(result);
                if (response != null && response.StatusCode == HttpStatusCode.OK)
                {
                    stream = response.GetResponseStream();
                    if (stream != null)
                    {
                        try
                        {
                            using(Bitmap image = new Bitmap(stream))
                            {
                                // TODO: make this a bit less hard coded
                                if((image.Height < 64) && (image.Width < 64))
                                {
                                    newSize.Width = 32;
                                    newSize.Height = 32;
                                }
                                else if((image.Height < 128) && (image.Width < 128))
                                {
                                    newSize.Width = 64;
                                    newSize.Height = 64;
                                }
                                else if((image.Height < 256) && (image.Width < 256))
                                {
                                    newSize.Width = 128;
                                    newSize.Height = 128;
                                }
                                else if((image.Height < 512 && image.Width < 512))
                                {
                                    newSize.Width = 256;
                                    newSize.Height = 256;
                                }
                                else if((image.Height < 1024 && image.Width < 1024))
                                {
                                    newSize.Width = 512;
                                    newSize.Height = 512;
                                }
                                else
                                {
                                    newSize.Width = 1024;
                                    newSize.Height = 1024;
                                }

                                if(newSize.Width != image.Width || newSize.Height != image.Height)
                                {
                                    using(Bitmap resize = new Bitmap(image, newSize))
                                     imageJ2000 = OpenJPEG.EncodeFromImage(resize, false);
                                }
                                else
                                    imageJ2000 = OpenJPEG.EncodeFromImage(image, false);
                            }
                        }
                        catch (Exception)
                        {
                            m_log.Error("[LOADIMAGEURLMODULE]: OpenJpeg Conversion Failed.  Empty byte data returned!");
                        }
                    }
                    else
                    {
                        m_log.WarnFormat("[LOADIMAGEURLMODULE] No data returned");
                    }
                }
            }
            catch (WebException)
            {
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[LOADIMAGEURLMODULE]: unexpected exception {0}", e.Message);
            }
            finally
            {
                if (stream != null)
                    stream.Close();

                if (response != null)
                {
                    if (response.StatusCode == HttpStatusCode.MovedPermanently
                            || response.StatusCode == HttpStatusCode.Found
                            || response.StatusCode == HttpStatusCode.SeeOther
                            || response.StatusCode == HttpStatusCode.TemporaryRedirect)
                    {
                        string redirectedUrl = response.Headers["Location"];

                        MakeHttpRequest(redirectedUrl, state.RequestID);
                    }
                    else
                    {
                        m_log.DebugFormat("[LOADIMAGEURLMODULE]: Returning {0} bytes of image data for request {1}",
                                          imageJ2000.Length, state.RequestID);

                        m_textureManager.ReturnData(
                            state.RequestID,
                            new OpenSim.Region.CoreModules.Scripting.DynamicTexture.DynamicTexture(
                            request.RequestUri, null, imageJ2000, newSize, false));
                    }
                    response.Close();
                }
            }
        }

        #region Nested type: RequestState

        public class RequestState
        {
            public HttpWebRequest Request = null;
            public UUID RequestID = UUID.Zero;
            public int TimeOfRequest = 0;

            public RequestState(HttpWebRequest request, UUID requestID)
            {
                Request = request;
                RequestID = requestID;
                TimeOfRequest = Util.UnixTimeSinceEpoch();
            }
        }

        #endregion
    }
}
