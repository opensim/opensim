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

namespace OpenSim.Server.Handlers.AgentPreferences
{
    public class AgentPreferencesServerPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IAgentPreferencesService m_AgentPreferencesService;

        public AgentPreferencesServerPostHandler(IAgentPreferencesService service, IServiceAuth auth) :
        base("POST", "/agentprefs", auth)
        {
            m_AgentPreferencesService = service;
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
                    case "getagentprefs":
                        return GetAgentPrefs(request);
                    case "setagentprefs":
                        return SetAgentPrefs(request);
                    case "getagentlang":
                        return GetAgentLang(request);
                }
                m_log.DebugFormat("[AGENT PREFERENCES HANDLER]: unknown method request: {0}", method);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[AGENT PREFERENCES HANDLER]: Exception {0}", e);
            }

            return FailureResult();
        }

        byte[] GetAgentPrefs(Dictionary<string, object> request)
        {
            if (!request.ContainsKey("UserID"))
                return FailureResult();

            UUID userID;
            if (!UUID.TryParse(request["UserID"].ToString(), out userID))
                return FailureResult();
            AgentPrefs prefs = m_AgentPreferencesService.GetAgentPreferences(userID);
            Dictionary<string, object> result = new Dictionary<string, object>();
            if (prefs != null)
                result = prefs.ToKeyValuePairs();

            string xmlString = ServerUtils.BuildXmlResponse(result);

            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] SetAgentPrefs(Dictionary<string, object> request)
        {
            if (!request.ContainsKey("PrincipalID") || !request.ContainsKey("AccessPrefs") || !request.ContainsKey("HoverHeight")
                || !request.ContainsKey("Language") || !request.ContainsKey("LanguageIsPublic") || !request.ContainsKey("PermEveryone")
                || !request.ContainsKey("PermGroup") || !request.ContainsKey("PermNextOwner"))
            {
                return FailureResult();
            }

            UUID userID;
            if (!UUID.TryParse(request["PrincipalID"].ToString(), out userID))
                return FailureResult();

            AgentPrefs data = new AgentPrefs(userID);
            data.AccessPrefs = request["AccessPrefs"].ToString();
            data.HoverHeight = double.Parse(request["HoverHeight"].ToString());
            data.Language = request["Language"].ToString();
            data.LanguageIsPublic = bool.Parse(request["LanguageIsPublic"].ToString());
            data.PermEveryone = int.Parse(request["PermEveryone"].ToString());
            data.PermGroup = int.Parse(request["PermGroup"].ToString());
            data.PermNextOwner = int.Parse(request["PermNextOwner"].ToString());

            return m_AgentPreferencesService.StoreAgentPreferences(data) ? SuccessResult() : FailureResult();
        }

        byte[] GetAgentLang(Dictionary<string, object> request)
        {
            if (!request.ContainsKey("UserID"))
                return FailureResult();
            UUID userID;
            if (!UUID.TryParse(request["UserID"].ToString(), out userID))
                return FailureResult();

            string lang = "en-us";
            AgentPrefs prefs = m_AgentPreferencesService.GetAgentPreferences(userID);
            if (prefs != null)
            {
                if (prefs.LanguageIsPublic)
                    lang = prefs.Language;
            }
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["Language"] = lang;
            string xmlString = ServerUtils.BuildXmlResponse(result);
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
