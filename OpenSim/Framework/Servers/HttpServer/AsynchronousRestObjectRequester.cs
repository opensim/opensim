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
    public class AsynchronousRestObjectRequester
    {
        /// <summary>
        /// Perform an asynchronous REST request.
        /// </summary>
        /// <param name="verb"></param>
        /// <param name="requestUrl"></param>
        /// <param name="obj"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        ///
        /// <exception cref="System.Net.WebException">Thrown if we encounter a
        /// network issue while posting the request.  You'll want to make
        /// sure you deal with this as they're not uncommon</exception>
        //
        public static void MakeRequest<TRequest, TResponse>(string verb,
                string requestUrl, TRequest obj, Action<TResponse> action)
        {
            Type type = typeof (TRequest);

            WebRequest request = WebRequest.Create(requestUrl);
            WebResponse response = null;
            TResponse deserial = default(TResponse);
            XmlSerializer deserializer = new XmlSerializer(typeof (TResponse));

            request.Method = verb;

            if (verb == "POST")
            {
                request.ContentType = "text/xml";

                MemoryStream buffer = new MemoryStream();

                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Encoding = Encoding.UTF8;

                using (XmlWriter writer = XmlWriter.Create(buffer, settings))
                {
                    XmlSerializer serializer = new XmlSerializer(type);
                    serializer.Serialize(writer, obj);
                    writer.Flush();
                }

                int length = (int) buffer.Length;
                request.ContentLength = length;

                request.BeginGetRequestStream(delegate(IAsyncResult res)
                {
                    Stream requestStream = request.EndGetRequestStream(res);

                    requestStream.Write(buffer.ToArray(), 0, length);

                    request.BeginGetResponse(delegate(IAsyncResult ar)
                    {
                        response = request.EndGetResponse(ar);

                        try
                        {
                            deserial = (TResponse) deserializer.Deserialize(
                                    response.GetResponseStream());
                        }
                        catch (System.InvalidOperationException)
                        {
                        }

                        action(deserial);
                    }, null);
                }, null);

                return;
            }

            request.BeginGetResponse(delegate(IAsyncResult res2)
            {
                response = request.EndGetResponse(res2);

                try
                {
                    deserial = (TResponse) deserializer.Deserialize(
                            response.GetResponseStream());
                }
                catch (System.InvalidOperationException)
                {
                }

                action(deserial);
            }, null);
        }
    }
}
