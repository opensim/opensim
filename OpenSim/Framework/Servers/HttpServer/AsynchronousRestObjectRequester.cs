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
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using log4net;

namespace OpenSim.Framework.Servers.HttpServer
{
    public class AsynchronousRestObjectRequester
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        /// <summary>
        /// Perform an asynchronous REST request.
        /// </summary>
        /// <param name="verb">GET or POST</param>
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
//            m_log.DebugFormat("[ASYNC REQUEST]: Starting {0} {1}", verb, requestUrl);
            
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
                    requestStream.Close();

                    request.BeginGetResponse(delegate(IAsyncResult ar)
                    {
                        response = request.EndGetResponse(ar);
                        Stream respStream = null;
                        try
                        {
                            respStream = response.GetResponseStream();
                            deserial = (TResponse)deserializer.Deserialize(
                                    respStream);
                        }
                        catch (System.InvalidOperationException)
                        {
                        }
                        finally
                        {
                            buffer.Close();
                            respStream.Close();
                            response.Close();
                        }

                        action(deserial);

                    }, null);
                }, null);

                
                return;
            }

            request.BeginGetResponse(delegate(IAsyncResult res2)
            {
                try
                {
                    // If the server returns a 404, this appears to trigger a System.Net.WebException even though that isn't
                    // documented in MSDN
                    response = request.EndGetResponse(res2);

                    Stream respStream = null;
                    try
                    {
                        respStream = response.GetResponseStream();
                        deserial = (TResponse)deserializer.Deserialize(respStream);
                    }
                    catch (System.InvalidOperationException)
                    {
                    }
                    finally
                    {
                        respStream.Close();
                        response.Close();
                    }
                }
                catch (WebException e)
                {
                    if (e.Status == WebExceptionStatus.ProtocolError)
                    {
                        if (e.Response is HttpWebResponse)
                        {
                            HttpWebResponse httpResponse = (HttpWebResponse)e.Response;
                        
                            if (httpResponse.StatusCode != HttpStatusCode.NotFound)
                            {
                                // We don't appear to be handling any other status codes, so log these feailures to that
                                // people don't spend unnecessary hours hunting phantom bugs.
                                m_log.DebugFormat(
                                    "[ASYNC REQUEST]: Request {0} {1} failed with unexpected status code {2}", 
                                    verb, requestUrl, httpResponse.StatusCode);
                            }
                        }
                    }
                    else
                    {
                        m_log.ErrorFormat("[ASYNC REQUEST]: Request {0} {1} failed with status {2} and message {3}", verb, requestUrl, e.Status, e.Message);
                    }
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[ASYNC REQUEST]: Request {0} {1} failed with exception {2}", verb, requestUrl, e);
                }

                //  m_log.DebugFormat("[ASYNC REQUEST]: Received {0}", deserial.ToString());

                try
                {
                    action(deserial);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[ASYNC REQUEST]: Request {0} {1} callback failed with exception {2}", verb, requestUrl, e);
                }
                    
            }, null);
        }
    }
}
