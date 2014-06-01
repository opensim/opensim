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
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace OpenSim.Framework.Servers.HttpServer
{
    /// <summary>
    /// Makes an asynchronous REST request which doesn't require us to do anything with the response.
    /// </summary>
    public class RestObjectPoster
    {
        public static void BeginPostObject<TRequest>(string requestUrl, TRequest obj)
        {
            BeginPostObject("POST", requestUrl, obj);
        }

        public static void BeginPostObject<TRequest>(string verb, string requestUrl, TRequest obj)
        {
            Type type = typeof (TRequest);

            WebRequest request = WebRequest.Create(requestUrl);
            request.Method = verb;
            request.ContentType = "text/xml";

            using (MemoryStream buffer = new MemoryStream())
            {
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Encoding = Encoding.UTF8;

                using (XmlWriter writer = XmlWriter.Create(buffer, settings))
                {
                    XmlSerializer serializer = new XmlSerializer(type);
                    serializer.Serialize(writer, obj);
                    writer.Flush();
                }

                int length = (int)buffer.Length;
                request.ContentLength = length;

                using (Stream requestStream = request.GetRequestStream())
                    requestStream.Write(buffer.ToArray(), 0, length);
            }

            // IAsyncResult result = request.BeginGetResponse(AsyncCallback, request);
            request.BeginGetResponse(AsyncCallback, request);
        }

        private static void AsyncCallback(IAsyncResult result)
        {
            WebRequest request = (WebRequest) result.AsyncState;
            using (WebResponse resp = request.EndGetResponse(result))
            {
            }
        }
    }
}
