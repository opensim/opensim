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

using Nini.Config;
using log4net;
using System;
using System.Reflection;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Avatar
{
    public class AvatarServerPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IAvatarService m_AvatarService;

        public AvatarServerPostHandler(IAvatarService service, IServiceAuth auth) :
                base("POST", "/avatar", auth)
        {
            m_AvatarService = service;
        }

        protected override byte[] ProcessRequest(string path, Stream requestData,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            //m_log.DebugFormat("[XXX]: query String: {0}", body);

            try
            {
                Dictionary<string, object> request =
                        ServerUtils.ParseQueryString(body);

                if (!request.ContainsKey("METHOD"))
                    return FailureResult();

                string method = request["METHOD"].ToString();

                switch (method)
                {
                    case "getavatar":
                        return GetAvatar(request);
                    case "setavatar":
                        return SetAvatar(request);
                    case "resetavatar":
                        return ResetAvatar(request);
                    case "setitems":
                        return SetItems(request);
                    case "removeitems":
                        return RemoveItems(request);
                }
                m_log.DebugFormat("[AVATAR HANDLER]: unknown method request: {0}", method);
            }
            catch (Exception e)
            {
                m_log.Debug("[AVATAR HANDLER]: Exception {0}" + e);
            }

            return FailureResult();

        }

        byte[] GetAvatar(Dictionary<string, object> request)
        {
            UUID user = UUID.Zero;

            if (!request.ContainsKey("UserID"))
                return FailureResult();

            if (UUID.TryParse(request["UserID"].ToString(), out user))
            {
                AvatarData avatar = m_AvatarService.GetAvatar(user);
                if (avatar == null)
                    return FailureResult();

                Dictionary<string, object> result = new Dictionary<string, object>();
                if (avatar == null)
                    result["result"] = "null";
                else
                    result["result"] = avatar.ToKeyValuePairs();

                string xmlString = ServerUtils.BuildXmlResponse(result);

                return Util.UTF8NoBomEncoding.GetBytes(xmlString);
            }

            return FailureResult();
        }

        byte[] SetAvatar(Dictionary<string, object> request)
        {
            UUID user = UUID.Zero;

            if (!request.ContainsKey("UserID"))
                return FailureResult();

            if (!UUID.TryParse(request["UserID"].ToString(), out user))
                return FailureResult();

            RemoveRequestParamsNotForStorage(request);

            AvatarData avatar = new AvatarData(request);
            if (m_AvatarService.SetAvatar(user, avatar))
                return SuccessResult();

            return FailureResult();
        }

        byte[] ResetAvatar(Dictionary<string, object> request)
        {
            UUID user = UUID.Zero;
            if (!request.ContainsKey("UserID"))
                return FailureResult();

            if (!UUID.TryParse(request["UserID"].ToString(), out user))
                return FailureResult();

            RemoveRequestParamsNotForStorage(request);

            if (m_AvatarService.ResetAvatar(user))
                return SuccessResult();

            return FailureResult();
        }

        /// <summary>
        /// Remove parameters that were used to invoke the method and should not in themselves be persisted.
        /// </summary>
        /// <param name='request'></param>
        private void RemoveRequestParamsNotForStorage(Dictionary<string, object> request)
        {
            request.Remove("VERSIONMAX");
            request.Remove("VERSIONMIN");
            request.Remove("METHOD");
            request.Remove("UserID");
        }
        
        byte[] SetItems(Dictionary<string, object> request)
        {
            UUID user = UUID.Zero;
            string[] names, values;

            if (!request.ContainsKey("UserID") || !request.ContainsKey("Names") || !request.ContainsKey("Values"))
                return FailureResult();

            if (!UUID.TryParse(request["UserID"].ToString(), out user))
                return FailureResult();

            if (!(request["Names"] is List<string> || request["Values"] is List<string>))
                return FailureResult();

            RemoveRequestParamsNotForStorage(request);

            List<string> _names = (List<string>)request["Names"];
            names = _names.ToArray();
            List<string> _values = (List<string>)request["Values"];
            values = _values.ToArray();
            
            if (m_AvatarService.SetItems(user, names, values))
                return SuccessResult();

            return FailureResult();
        }

        byte[] RemoveItems(Dictionary<string, object> request)
        {
            UUID user = UUID.Zero;
            string[] names;

            if (!request.ContainsKey("UserID") || !request.ContainsKey("Names"))
                return FailureResult();

            if (!UUID.TryParse(request["UserID"].ToString(), out user))
                return FailureResult();

            if (!(request["Names"] is List<string>))
                return FailureResult();

            List<string> _names = (List<string>)request["Names"];
            names = _names.ToArray();

            if (m_AvatarService.RemoveItems(user, names))
                return SuccessResult();

            return FailureResult();
        }


        
        private byte[] SuccessResult()
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "result", "");
            result.AppendChild(doc.CreateTextNode("Success"));

            rootElement.AppendChild(result);

            return Util.DocToBytes(doc);
        }

        private byte[] FailureResult()
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "result", "");
            result.AppendChild(doc.CreateTextNode("Failure"));

            rootElement.AppendChild(result);

            return Util.DocToBytes(doc);
        }

    }
}
