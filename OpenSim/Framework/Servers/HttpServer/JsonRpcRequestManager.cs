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
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.IO;
using OpenMetaverse.StructuredData;
using OpenMetaverse;
using log4net;

namespace OpenSim.Framework.Servers.HttpServer
{
    /// <summary>
    /// Json rpc request manager.
    /// </summary>
    public class JsonRpcRequestManager
    {
        static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        public JsonRpcRequestManager()
        {
        }

        #region Web Util
        /// <summary>
        /// Sends json-rpc request with a serializable type.
        /// </summary>
        /// <returns>
        /// OSD Map.
        /// </returns>
        /// <param name='parameters'>
        /// Serializable type .
        /// </param>
        /// <param name='method'>
        /// Json-rpc method to call.
        /// </param>
        /// <param name='uri'>
        /// URI of json-rpc service.
        /// </param>
        /// <param name='jsonId'>
        /// Id for our call.
        /// </param>
        public bool JsonRpcRequest(ref object parameters, string method, string uri, string jsonId)
        {
            if (jsonId == null)
                throw new ArgumentNullException ("jsonId");
            if (uri == null)
                throw new ArgumentNullException ("uri");
            if (method == null)
                throw new ArgumentNullException ("method");
            if (parameters == null)
                throw new ArgumentNullException ("parameters");
            
            // Prep our payload
            OSDMap json = new OSDMap();
            
            json.Add("jsonrpc", OSD.FromString("2.0"));
            json.Add("id", OSD.FromString(jsonId));
            json.Add("method", OSD.FromString(method));
            
            json.Add("params", OSD.SerializeMembers(parameters));
            
            string jsonRequestData = OSDParser.SerializeJsonString(json);
            byte[] content = Encoding.UTF8.GetBytes(jsonRequestData);
            
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(uri);
            
            webRequest.ContentType = "application/json-rpc";
            webRequest.Method = "POST";
            
            //Stream dataStream = webRequest.GetRequestStream();
            //dataStream.Write(content, 0, content.Length);
            //dataStream.Close();

            using (Stream dataStream = webRequest.GetRequestStream())
                dataStream.Write(content, 0, content.Length);
            
            WebResponse webResponse = null;
            try
            {
                webResponse = webRequest.GetResponse();
            }
            catch (WebException e)
            {
                Console.WriteLine("Web Error" + e.Message);
                Console.WriteLine ("Please check input");
                return false;
            }
            
            using (webResponse)
            using (Stream rstream = webResponse.GetResponseStream())
            {
                OSDMap mret = (OSDMap)OSDParser.DeserializeJson(rstream);
                        
                if (mret.ContainsKey("error"))
                    return false;
            
                // get params...
                OSD.DeserializeMembers(ref parameters, (OSDMap)mret["result"]);
                return true;
            }
        }
        
        /// <summary>
        /// Sends json-rpc request with OSD parameter.
        /// </summary>
        /// <returns>
        /// The rpc request.
        /// </returns>
        /// <param name='data'>
        /// data - incoming as parameters, outgong as result/error
        /// </param>
        /// <param name='method'>
        /// Json-rpc method to call.
        /// </param>
        /// <param name='uri'>
        /// URI of json-rpc service.
        /// </param>
        /// <param name='jsonId'>
        /// If set to <c>true</c> json identifier.
        /// </param>
        public bool JsonRpcRequest(ref OSD data, string method, string uri, string jsonId)
        {
            OSDMap map = new OSDMap();
            
            map["jsonrpc"] = "2.0";
            if(string.IsNullOrEmpty(jsonId))
                map["id"] = UUID.Random().ToString();
            else
                map["id"] = jsonId;
            
            map["method"] = method;
            map["params"] = data;
            
            string jsonRequestData = OSDParser.SerializeJsonString(map);
            byte[] content = Encoding.UTF8.GetBytes(jsonRequestData);
            
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(uri);
            webRequest.ContentType = "application/json-rpc";
            webRequest.Method = "POST";
            
            using (Stream dataStream = webRequest.GetRequestStream())
              dataStream.Write(content, 0, content.Length);

            WebResponse webResponse = null;
            try
            {
                webResponse = webRequest.GetResponse();
            }
            catch (WebException e)
            {
                Console.WriteLine("Web Error" + e.Message);
                Console.WriteLine ("Please check input");
                return false;
            }
            
            using (webResponse)
            using (Stream rstream = webResponse.GetResponseStream())
            {
                OSDMap response = new OSDMap();
                try
                {
                    response = (OSDMap)OSDParser.DeserializeJson(rstream);
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[JSONRPC]: JsonRpcRequest Error {0}", e.Message);
                    return false;
                }

                if (response.ContainsKey("error"))
                {
                    data = response["error"];
                    return false;
                }

                data = response;            

                return true;
            }
        }
        #endregion Web Util
    }
}
