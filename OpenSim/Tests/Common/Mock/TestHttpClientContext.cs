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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HttpServer;
using OpenSim.Framework;

namespace OpenSim.Tests.Common
{
/*
    public class TestHttpClientContext: IHttpClientContext
    {
        /// <summary>
        /// Bodies of responses from the server.
        /// </summary>
        public string ResponseBody
        {
            get { return Encoding.UTF8.GetString(m_responseStream.ToArray()); }
        }

        public Byte[] ResponseBodyBytes
        {
            get{ return m_responseStream.ToArray(); }
        }

        private MemoryStream m_responseStream = new MemoryStream();

        public bool IsSecured { get; set; }

        public bool Secured
        {
            get { return IsSecured; }
            set { IsSecured = value; }
        }

        public TestHttpClientContext(bool secured)
        {
            Secured = secured;
        }

        public void Disconnect(SocketError error)
        {
//            Console.WriteLine("TestHttpClientContext.Disconnect Received disconnect with status {0}", error);
        }

        public void Respond(string httpVersion, HttpStatusCode statusCode, string reason, string body) {Console.WriteLine("x");}
        public void Respond(string httpVersion, HttpStatusCode statusCode, string reason) {Console.WriteLine("xx");}
        public void Respond(string body) { Console.WriteLine("xxx");}

        public void Send(byte[] buffer)
        {
            // Getting header data here
//            Console.WriteLine("xxxx: Got {0}", Encoding.UTF8.GetString(buffer));
        }

        public void Send(byte[] buffer, int offset, int size)
        {
//            Util.PrintCallStack();
//
//            Console.WriteLine(
//                "TestHttpClientContext.Send(byte[], int, int) got offset={0}, size={1}, buffer={2}",
//                offset, size, Encoding.UTF8.GetString(buffer));

            m_responseStream.Write(buffer, offset, size);
        }

        public void Respond(string httpVersion, HttpStatusCode statusCode, string reason, string body, string contentType) {Console.WriteLine("xxxxxx");}
        public void Close() { }
        public bool EndWhenDone { get { return false;} set { return;}}

        public HTTPNetworkContext GiveMeTheNetworkStreamIKnowWhatImDoing()
        {
            return new HTTPNetworkContext();
        }

        public event EventHandler<DisconnectedEventArgs> Disconnected = delegate { };
        /// <summary>
        /// A request have been received in the context.
        /// </summary>
        public event EventHandler<RequestEventArgs> RequestReceived = delegate { };
    }
*/
}