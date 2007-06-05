/*
* Copyright (c) OpenSim project, http://www.openmetaverse.org/
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
//using OpenSim.CAPS;
using Nwc.XmlRpc;
using System.Collections;
using OpenSim.Framework.Console;
using OpenSim.Servers;

namespace OpenGridServices.AssetServer
{
    /// <summary>
    /// An HTTP server for sending assets
    /// </summary>
    public class AssetHttpServer :BaseHttpServer
    {
        /// <summary>
        /// Creates the new asset server
        /// </summary>
        /// <param name="port">Port to initalise on</param>
        public AssetHttpServer(int port)
            : base(port)
        {
        }

        /// <summary>
        /// Handles an HTTP request
        /// </summary>
        /// <param name="stateinfo">HTTP State Info</param>
        public override void HandleRequest(Object stateinfo)
        {
            try
            {
                HttpListenerContext context = (HttpListenerContext)stateinfo;

                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                response.KeepAlive = false;
                response.SendChunked = false;

                System.IO.Stream body = request.InputStream;
                System.Text.Encoding encoding = System.Text.Encoding.UTF8;
                System.IO.StreamReader reader = new System.IO.StreamReader(body, encoding);

                string requestBody = reader.ReadToEnd();
                body.Close();
                reader.Close();

                //Console.WriteLine(request.HttpMethod + " " + request.RawUrl + " Http/" + request.ProtocolVersion.ToString() + " content type: " + request.ContentType);
                //Console.WriteLine(requestBody);

                string responseString = "";
                switch (request.ContentType)
                {
                    case "text/xml":
                        // must be XML-RPC, so pass to the XML-RPC parser

                        responseString = ParseXMLRPC(requestBody);
                        responseString = Regex.Replace(responseString, "utf-16", "utf-8");

                        response.AddHeader("Content-type", "text/xml");
                        break;

                    case "application/xml":
                        // probably LLSD we hope, otherwise it should be ignored by the parser
                        responseString = ParseLLSDXML(requestBody);
                        response.AddHeader("Content-type", "application/xml");
                        break;

                    case "application/x-www-form-urlencoded":
                        // a form data POST so send to the REST parser
                        responseString = ParseREST(requestBody, request.RawUrl, request.HttpMethod);
                        response.AddHeader("Content-type", "text/plain");
                        break;

                    case null:
                        // must be REST or invalid crap, so pass to the REST parser
                        responseString = ParseREST(requestBody, request.RawUrl, request.HttpMethod);
                        response.AddHeader("Content-type", "text/plain");
                        break;

                }

                Encoding Windows1252Encoding = Encoding.GetEncoding(1252);
                byte[] buffer = Windows1252Encoding.GetBytes(responseString);
                System.IO.Stream output = response.OutputStream;
                response.SendChunked = false;
                response.ContentLength64 = buffer.Length;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

    }
}
