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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using System.Collections;

/*****************************************************
 *
 * ScriptsHttpRequests
 *
 * Implements the llHttpRequest and http_response
 * callback.
 *
 * Some stuff was already in LSLLongCmdHandler, and then
 * there was this file with a stub class in it.  So,
 * I am moving some of the objects and functions out of
 * LSLLongCmdHandler, such as the HttpRequestClass, the
 * start and stop methods, and setting up pending and
 * completed queues.  These are processed in the
 * LSLLongCmdHandler polling loop.  Similiar to the
 * XMLRPCModule, since that seems to work.
 *
 * //TODO
 *
 * This probably needs some throttling mechanism but
 * its wide open right now.  This applies to both
 * number of requests and data volume.
 *
 * Linden puts all kinds of header fields in the requests.
 * Not doing any of that:
 * User-Agent
 * X-SecondLife-Shard
 * X-SecondLife-Object-Name
 * X-SecondLife-Object-Key
 * X-SecondLife-Region
 * X-SecondLife-Local-Position
 * X-SecondLife-Local-Velocity
 * X-SecondLife-Local-Rotation
 * X-SecondLife-Owner-Name
 * X-SecondLife-Owner-Key
 *
 * HTTPS support
 *
 * Configurable timeout?
 * Configurable max repsonse size?
 * Configurable
 *
 * **************************************************/

namespace OpenSim.Region.Environment.Modules.Scripting.HttpRequest
{
    public class HttpRequestModule : IRegionModule, IHttpRequests
    {
        private object HttpListLock = new object();
        private int httpTimeout = 30000;
        private string m_name = "HttpScriptRequests";

        // <request id, HttpRequestClass>
        private Dictionary<LLUUID, HttpRequestClass> m_pendingRequests;
        private Scene m_scene;
        // private Queue<HttpRequestClass> rpcQueue = new Queue<HttpRequestClass>();

        public HttpRequestModule()
        {
        }

        #region IHttpRequests Members

        public LLUUID MakeHttpRequest(string url, string parameters, string body)
        {
            return LLUUID.Zero;
        }

        public LLUUID StartHttpRequest(uint localID, LLUUID itemID, string url, List<string> parameters, Dictionary<string, string> headers, string body)
        {
            LLUUID reqID = LLUUID.Random();
            HttpRequestClass htc = new HttpRequestClass();

            // Partial implementation: support for parameter flags needed
            //   see http://wiki.secondlife.com/wiki/LlHTTPRequest
            //
            // Parameters are expected in {key, value, ... , key, value}
            if (parameters != null)
            {
                string[] parms = parameters.ToArray();
                for (int i = 0; i < parms.Length; i += 2)
                {
                    switch (Int32.Parse(parms[i]))
                    {
                        case HttpRequestClass.HTTP_METHOD:

                            htc.httpMethod = parms[i + 1];
                            break;

                        case HttpRequestClass.HTTP_MIMETYPE:

                            htc.httpMIMEType = parms[i + 1];
                            break;

                        case HttpRequestClass.HTTP_BODY_MAXLENGTH:

                            // TODO implement me
                            break;

                        case HttpRequestClass.HTTP_VERIFY_CERT:

                            // TODO implement me
                            break;
                    }
                }
            }

            htc.localID = localID;
            htc.itemID = itemID;
            htc.url = url;
            htc.reqID = reqID;
            htc.httpTimeout = httpTimeout;
            htc.outbound_body = body;
            htc.response_headers = headers;

            lock (HttpListLock)
            {
                m_pendingRequests.Add(reqID, htc);
            }

            htc.process();

            return reqID;
        }

        public void StopHttpRequest(uint m_localID, LLUUID m_itemID)
        {
            if (m_pendingRequests != null)
            {
                lock (HttpListLock)
                {
                    HttpRequestClass tmpReq;
                    if (m_pendingRequests.TryGetValue(m_itemID, out tmpReq))
                    {
                        tmpReq.Stop();
                        m_pendingRequests.Remove(m_itemID);
                    }
                }
            }
        }

        /*
        * TODO
        * Not sure how important ordering is is here - the next first
        * one completed in the list is returned, based soley on its list
        * position, not the order in which the request was started or
        * finsihed.  I thought about setting up a queue for this, but
        * it will need some refactoring and this works 'enough' right now
        */

        public HttpRequestClass GetNextCompletedRequest()
        {
            lock (HttpListLock)
            {
                foreach (LLUUID luid in m_pendingRequests.Keys)
                {
                    HttpRequestClass tmpReq;

                    if (m_pendingRequests.TryGetValue(luid, out tmpReq))
                    {
                        if (tmpReq.finished)
                        {
                            return tmpReq;
                        }
                    }
                }
            }
            return null;
        }

        public void RemoveCompletedRequest(LLUUID id)
        {
            lock (HttpListLock)
            {
                HttpRequestClass tmpReq;
                if (m_pendingRequests.TryGetValue(id, out tmpReq))
                {
                    tmpReq.Stop();
                    tmpReq = null;
                    m_pendingRequests.Remove(id);
                }
            }
        }

        #endregion

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_scene = scene;

            m_scene.RegisterModuleInterface<IHttpRequests>(this);

            m_pendingRequests = new Dictionary<LLUUID, HttpRequestClass>();
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return m_name; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion
    }

    public class HttpRequestClass
    {
        // Constants for parameters
        public const int HTTP_BODY_MAXLENGTH = 2;
        public const int HTTP_METHOD = 0;
        public const int HTTP_MIMETYPE = 1;
        public const int HTTP_VERIFY_CERT = 3;
        public bool finished;
        public int httpBodyMaxLen = 2048; // not implemented

        // Parameter members and default values
        public string httpMethod = "GET";
        public string httpMIMEType = "text/plain;charset=utf-8";
        private Thread httpThread;
        public int httpTimeout;
        public bool httpVerifyCert = true; // not implemented

        // Request info
        public LLUUID itemID;
        public uint localID;
        public DateTime next;
        public string outbound_body;
        public LLUUID reqID;
        public HttpWebRequest request;
        public string response_body;
        public List<string> response_metadata;
        public Dictionary<string, string> response_headers;
        public int status;
        public string url;

        public void process()
        {
            httpThread = new Thread(SendRequest);
            httpThread.Name = "HttpRequestThread";
            httpThread.Priority = ThreadPriority.BelowNormal;
            httpThread.IsBackground = true;
            finished = false;
            httpThread.Start();
            ThreadTracker.Add(httpThread);
        }

        /*
         * TODO: More work on the response codes.  Right now
         * returning 200 for success or 499 for exception
         */

        public void SendRequest()
        {
            HttpWebResponse response = null;
            StringBuilder sb = new StringBuilder();
            byte[] buf = new byte[8192];
            string tempString = null;
            int count = 0;

            try
            {
                request = (HttpWebRequest)
                          WebRequest.Create(url);
                request.Method = httpMethod;
                request.ContentType = httpMIMEType;

                foreach (KeyValuePair<string, string> entry in response_headers)
                    request.Headers[entry.Key] = entry.Value;

                // Encode outbound data
                if (outbound_body.Length > 0) {
                    byte[] data = Encoding.UTF8.GetBytes(outbound_body);

                    request.ContentLength = data.Length;
                    Stream bstream = request.GetRequestStream();
                    bstream.Write(data, 0, data.Length);
                    bstream.Close();
                }

                request.Timeout = httpTimeout;
                // execute the request
                response = (HttpWebResponse)
                           request.GetResponse();

                Stream resStream = response.GetResponseStream();

                do
                {
                    // fill the buffer with data
                    count = resStream.Read(buf, 0, buf.Length);

                    // make sure we read some data
                    if (count != 0)
                    {
                        // translate from bytes to ASCII text
                        tempString = Encoding.UTF8.GetString(buf, 0, count);

                        // continue building the string
                        sb.Append(tempString);
                    }
                } while (count > 0); // any more data to read?

                response_body = sb.ToString();
            }
            catch (Exception e)
            {
                status = (int)OSHttpStatusCode.ClientErrorJoker;
                response_body = e.Message;
                finished = true;
                return;
            }

            status = (int)OSHttpStatusCode.SuccessOk;
            finished = true;
        }

        public void Stop()
        {
            try
            {
                httpThread.Abort();
            }
            catch (Exception)
            {
            }
        }
    }
}
