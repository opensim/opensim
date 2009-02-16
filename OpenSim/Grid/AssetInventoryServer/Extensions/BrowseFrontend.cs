/*
 * Copyright (c) 2008 Intel Corporation
 * All rights reserved.
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 *
 * -- Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * -- Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * -- Neither the name of the Intel Corporation nor the names of its
 *    contributors may be used to endorse or promote products derived from
 *    this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
 * PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE INTEL OR ITS
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Web;
using ExtensionLoader;
using OpenMetaverse;
using HttpServer;

namespace OpenSim.Grid.AssetInventoryServer.Extensions
{
    public class BrowseFrontend : IExtension<AssetInventoryServer>
    {
        AssetInventoryServer server;

        public BrowseFrontend()
        {
        }

        public void Start(AssetInventoryServer server)
        {
            this.server = server;

            // Request for / or /?...
            server.HttpServer.AddHandler("get", null, @"(^/$)|(^/\?.*)", BrowseRequestHandler);
        }

        public void Stop()
        {
        }

        bool BrowseRequestHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            const int ASSETS_PER_PAGE = 25;
            const string HEADER = "<html><head><title>Asset Server</title></head><body>";
            const string TABLE_HEADER =
                "<table><tr><th>Name</th><th>Description</th><th>Type</th><th>ID</th><th>Temporary</th><th>SHA-1</th></tr>";
            const string TABLE_FOOTER = "</table>";
            const string FOOTER = "</body></html>";

            UUID authToken = Utils.GetAuthToken(request);

            StringBuilder html = new StringBuilder();
            int start = 0;
            uint page = 0;

            if (!String.IsNullOrEmpty(request.Uri.Query))
            {
                NameValueCollection query = HttpUtility.ParseQueryString(request.Uri.Query);
                if (!String.IsNullOrEmpty(query["page"]) && UInt32.TryParse(query["page"], out page))
                    start = (int)page * ASSETS_PER_PAGE;
            }

            html.AppendLine(HEADER);

            html.AppendLine("<p>");
            if (page > 0)
                html.AppendFormat("<a href=\"{0}?page={1}\">&lt; Previous Page</a> | ", request.Uri.AbsolutePath, page - 1);
            html.AppendFormat("<a href=\"{0}?page={1}\">Next Page &gt;</a>", request.Uri.AbsolutePath, page + 1);
            html.AppendLine("</p>");

            html.AppendLine(TABLE_HEADER);

            server.StorageProvider.ForEach(
                delegate(Metadata data)
                {
                    if (server.AuthorizationProvider.IsMetadataAuthorized(authToken, data.ID))
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

            response.Status = HttpStatusCode.OK;
            response.Body.Write(responseData, 0, responseData.Length);
            response.Body.Flush();
            return true;
        }
    }
}
