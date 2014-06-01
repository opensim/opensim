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
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.ServiceAuth;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Presence
{
    public class PresenceServerPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IPresenceService m_PresenceService;

        public PresenceServerPostHandler(IPresenceService service, IServiceAuth auth) :
                base("POST", "/presence", auth)
        {
            m_PresenceService = service;
        }

        protected override byte[] ProcessRequest(string path, Stream requestData,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            //m_log.DebugFormat("[XXX]: query String: {0}", body);
            string method = string.Empty;
            try
            {
                Dictionary<string, object> request =
                        ServerUtils.ParseQueryString(body);

                if (!request.ContainsKey("METHOD"))
                    return FailureResult();

                method = request["METHOD"].ToString();

                switch (method)
                {
                    case "login":
                        return LoginAgent(request);
                    case "logout":
                        return LogoutAgent(request);
                    case "logoutregion":
                        return LogoutRegionAgents(request);
                    case "report":
                        return Report(request);
                    case "getagent":
                        return GetAgent(request);
                    case "getagents":
                        return GetAgents(request);
                }
                m_log.DebugFormat("[PRESENCE HANDLER]: unknown method request: {0}", method);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[PRESENCE HANDLER]: Exception in method {0}: {1}", method, e);
            }

            return FailureResult();

        }

        byte[] LoginAgent(Dictionary<string, object> request)
        {
            string user = String.Empty;
            UUID session = UUID.Zero;
            UUID ssession = UUID.Zero;

            if (!request.ContainsKey("UserID") || !request.ContainsKey("SessionID"))
                return FailureResult();

            user = request["UserID"].ToString();

            if (!UUID.TryParse(request["SessionID"].ToString(), out session))
                return FailureResult();

            if (request.ContainsKey("SecureSessionID"))
                // If it's malformed, we go on with a Zero on it
                UUID.TryParse(request["SecureSessionID"].ToString(), out ssession);

            if (m_PresenceService.LoginAgent(user, session, ssession))
                return SuccessResult();

            return FailureResult();
        }

        byte[] LogoutAgent(Dictionary<string, object> request)
        {
            UUID session = UUID.Zero;

            if (!request.ContainsKey("SessionID"))
                return FailureResult();

            if (!UUID.TryParse(request["SessionID"].ToString(), out session))
                return FailureResult();

            if (m_PresenceService.LogoutAgent(session))
                return SuccessResult();

            return FailureResult();
        }

        byte[] LogoutRegionAgents(Dictionary<string, object> request)
        {
            UUID region = UUID.Zero;

            if (!request.ContainsKey("RegionID"))
                return FailureResult();

            if (!UUID.TryParse(request["RegionID"].ToString(), out region))
                return FailureResult();

            if (m_PresenceService.LogoutRegionAgents(region))
                return SuccessResult();

            return FailureResult();
        }
        
        byte[] Report(Dictionary<string, object> request)
        {
            UUID session = UUID.Zero;
            UUID region = UUID.Zero;

            if (!request.ContainsKey("SessionID") || !request.ContainsKey("RegionID"))
                return FailureResult();

            if (!UUID.TryParse(request["SessionID"].ToString(), out session))
                return FailureResult();

            if (!UUID.TryParse(request["RegionID"].ToString(), out region))
                return FailureResult();

            if (m_PresenceService.ReportAgent(session, region))
            {
                return SuccessResult();
            }

            return FailureResult();
        }

        byte[] GetAgent(Dictionary<string, object> request)
        {
            UUID session = UUID.Zero;

            if (!request.ContainsKey("SessionID"))
                return FailureResult();

            if (!UUID.TryParse(request["SessionID"].ToString(), out session))
                return FailureResult();

            PresenceInfo pinfo = m_PresenceService.GetAgent(session);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if (pinfo == null)
                result["result"] = "null";
            else
                result["result"] = pinfo.ToKeyValuePairs();

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] GetAgents(Dictionary<string, object> request)
        {

            string[] userIDs;

            if (!request.ContainsKey("uuids"))
            {
                m_log.DebugFormat("[PRESENCE HANDLER]: GetAgents called without required uuids argument");
                return FailureResult();
            }

            if (!(request["uuids"] is List<string>))
            {
                m_log.DebugFormat("[PRESENCE HANDLER]: GetAgents input argument was of unexpected type {0}", request["uuids"].GetType().ToString());
                return FailureResult();
            }

            userIDs = ((List<string>)request["uuids"]).ToArray();

            PresenceInfo[] pinfos = m_PresenceService.GetAgents(userIDs);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ((pinfos == null) || ((pinfos != null) && (pinfos.Length == 0)))
                result["result"] = "null";
            else
            {
                int i = 0;
                foreach (PresenceInfo pinfo in pinfos)
                {
                    Dictionary<string, object> rinfoDict = pinfo.ToKeyValuePairs();
                    result["presence" + i] = rinfoDict;
                    i++;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);
            
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
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
