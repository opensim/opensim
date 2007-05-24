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
    public class AssetHttpServer :BaseHttpServer
    {
        public AssetHttpServer(int port)
            : base(port)
        {
        }

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
