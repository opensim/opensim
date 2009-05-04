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
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Web;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using log4net;

namespace OpenSim.Grid.AssetInventoryServer.Plugins
{
    public class BrowseFrontendPlugin : IAssetInventoryServerPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private AssetInventoryServer m_server;

        public BrowseFrontendPlugin()
        {
        }

        #region IPlugin implementation

        public void Initialise(AssetInventoryServer server)
        {
            m_server = server;

            // Request for / or /?...
            m_server.HttpServer.AddStreamHandler(new BrowseRequestHandler(server));

            m_log.Info("[BROWSEFRONTEND]: Browser Frontend loaded.");
        }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        public void Initialise()
        {
            m_log.InfoFormat("[BROWSEFRONTEND]: {0} cannot be default-initialized!", Name);
            throw new PluginNotInitialisedException(Name);
        }

        public void Dispose()
        {
        }

        public string Version
        {
            // TODO: this should be something meaningful and not hardcoded?
            get { return "0.1"; }
        }

        public string Name
        {
            get { return "BrowseFrontend"; }
        }

        #endregion IPlugin implementation

        public class BrowseRequestHandler : BaseStreamHandler
        {
            AssetInventoryServer m_server;

            //public BrowseRequestHandler(AssetInventoryServer server) : base("GET", "(^/$|(^/\?.*)")
            public BrowseRequestHandler(AssetInventoryServer server) : base("GET", "/")
            {
                m_server = server;
            }

            public override string ContentType
            {
                get { return "text/html"; }
            }

            #region IStreamedRequestHandler implementation

            public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                const int ASSETS_PER_PAGE = 25;
                const string HEADER = "<html><head><title>Asset Server</title></head><body>";
                const string TABLE_HEADER =
                    "<table><tr><th>Name</th><th>Description</th><th>Type</th><th>ID</th><th>Temporary</th><th>SHA-1</th></tr>";
                const string TABLE_FOOTER = "</table>";
                const string FOOTER = "</body></html>";

                UUID authToken = Utils.GetAuthToken(httpRequest);

                StringBuilder html = new StringBuilder();
                int start = 0;
                uint page = 0;

                if (!String.IsNullOrEmpty(httpRequest.Url.Query))
                {
                    NameValueCollection query = HttpUtility.ParseQueryString(httpRequest.Url.Query);
                    if (!String.IsNullOrEmpty(query["page"]) && UInt32.TryParse(query["page"], out page))
                        start = (int)page * ASSETS_PER_PAGE;
                }

                html.AppendLine(HEADER);

                html.AppendLine("<p>");
                if (page > 0)
                    html.AppendFormat("<a href=\"{0}?page={1}\">&lt; Previous Page</a> | ", httpRequest.RawUrl, page - 1);
                html.AppendFormat("<a href=\"{0}?page={1}\">Next Page &gt;</a>", httpRequest.RawUrl, page + 1);
                html.AppendLine("</p>");

                html.AppendLine(TABLE_HEADER);

                m_server.StorageProvider.ForEach(
                    delegate(AssetMetadata data)
                    {
                        if (m_server.AuthorizationProvider.IsMetadataAuthorized(authToken, data.FullID))
                        {
                            html.AppendLine(String.Format(
                                "<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td></tr>",
                                data.Name, data.Description, data.ContentType, data.ID, data.Temporary,
                                BitConverter.ToString(data.SHA1).Replace("-", String.Empty)));
                        }
                        else
                        {
                            html.AppendLine(String.Format(
                                "<tr><td>[Protected Asset]</td><td>&nbsp;</td><td>&nbsp;</td><td>{0}</td><td>{1}</td><td>&nbsp;</td></tr>",
                                data.ID, data.Temporary));
                        }
                    }, start, ASSETS_PER_PAGE
                );

                html.AppendLine(TABLE_FOOTER);

                html.AppendLine(FOOTER);

                byte[] responseData = System.Text.Encoding.UTF8.GetBytes(html.ToString());

                httpResponse.StatusCode = (int) HttpStatusCode.OK;
                //httpResponse.Body.Write(responseData, 0, responseData.Length);
                //httpResponse.Body.Flush();
                return responseData;
            }

            #endregion IStreamedRequestHandler implementation
        }
    }
}
