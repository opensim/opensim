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

namespace OpenSim.ConsoleClient
{
    public delegate void ReplyDelegate(string requestUrl, string requestData, string replyData);

    public class Requester
    {
        public static void MakeRequest(string requestUrl, string data,
                ReplyDelegate action)
        {
            WebRequest request = WebRequest.Create(requestUrl);

            request.Method = "POST";

            request.ContentType = "application/x-www-form-urlencoded";

            byte[] buffer = Encoding.ASCII.GetBytes(data);
            int length = (int) buffer.Length;
            request.ContentLength = length;

            request.BeginGetRequestStream(delegate(IAsyncResult res)
            {
                Stream requestStream = request.EndGetRequestStream(res);

                requestStream.Write(buffer, 0, length);

                request.BeginGetResponse(delegate(IAsyncResult ar)
                {
                    string reply = String.Empty;

                    using (WebResponse response = request.EndGetResponse(ar))
                    {
                        try
                        {
                            using (Stream s = response.GetResponseStream())
                                using (StreamReader r = new StreamReader(s))
                                    reply = r.ReadToEnd();

                        }
                        catch (System.InvalidOperationException)
                        {
                        }
                    }

                    action(requestUrl, data, reply);
                }, null);
            }, null);
        }
    }
}
