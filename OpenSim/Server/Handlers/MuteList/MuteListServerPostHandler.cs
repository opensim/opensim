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

namespace OpenSim.Server.Handlers.GridUser
{
    public class MuteListServerPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IMuteListService m_service;

        public MuteListServerPostHandler(IMuteListService service, IServiceAuth auth) :
                base("POST", "/mutelist", auth)
        {
            m_service = service;
        }

        protected override byte[] ProcessRequest(string path, Stream requestData,
                IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            string body;
            using(StreamReader sr = new StreamReader(requestData))
                body = sr.ReadToEnd();
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
                    case "get":
                        return getmutes(request);
                    case "update":
                        return updatemute(request);
                    case "delete":
                        return deletemute(request);
                }
                m_log.DebugFormat("[MUTELIST HANDLER]: unknown method request: {0}", method);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[MUTELIST HANDLER]: Exception in method {0}: {1}", method, e);
            }

            return FailureResult();
        }

        byte[] getmutes(Dictionary<string, object> request)
        {
            if(!request.ContainsKey("agentid") || !request.ContainsKey("mutecrc"))
                return FailureResult();

            UUID agentID;
            if(!UUID.TryParse(request["agentid"].ToString(), out agentID))
                return FailureResult();

            uint mutecrc;
            if(!UInt32.TryParse(request["mutecrc"].ToString(), out mutecrc))
                    return FailureResult();

            byte[] data = m_service.MuteListRequest(agentID, mutecrc);

            Dictionary<string, object> result = new Dictionary<string, object>();
            result["result"] = Convert.ToBase64String(data);

            string xmlString = ServerUtils.BuildXmlResponse(result);

            //m_log.DebugFormat("[GRID USER HANDLER]: resp string: {0}", xmlString);
            return Util.UTF8NoBomEncoding.GetBytes(xmlString);
        }

        byte[] updatemute(Dictionary<string, object> request)
        {
            if(!request.ContainsKey("agentid") || !request.ContainsKey("muteid"))
                return FailureResult();

            MuteData mute = new MuteData();

            if( !UUID.TryParse(request["agentid"].ToString(), out mute.AgentID))
                return FailureResult();

            if(!UUID.TryParse(request["muteid"].ToString(), out mute.MuteID))
                return FailureResult();

            if(request.ContainsKey("mutename"))
            {
                mute.MuteName = request["mutename"].ToString();
            }
            else
               mute.MuteName = String.Empty;

            if(request.ContainsKey("mutetype"))
            {
                if(!Int32.TryParse(request["mutetype"].ToString(), out mute.MuteType))
                    return FailureResult();
            }
            else
               mute.MuteType = 0;

            if(request.ContainsKey("muteflags"))
            {
                if(!Int32.TryParse(request["muteflags"].ToString(), out mute.MuteFlags))
                    return FailureResult();
            }
            else
                mute.MuteFlags = 0;

            if(request.ContainsKey("mutestamp"))
            {
                if(!Int32.TryParse(request["mutestamp"].ToString(), out mute.Stamp))
                    return FailureResult();
            }
            else
                mute.Stamp = Util.UnixTimeSinceEpoch();

            return m_service.UpdateMute(mute) ? SuccessResult() : FailureResult();
        }

        byte[] deletemute(Dictionary<string, object> request)
        {
            if(!request.ContainsKey("agentid") || !request.ContainsKey("muteid"))
                return FailureResult();

            UUID agentID;
            if( !UUID.TryParse(request["agentid"].ToString(), out agentID))
                return FailureResult();

            UUID muteID;
            if(!UUID.TryParse(request["muteid"].ToString(), out muteID))
                return FailureResult();

            string muteName;
            if(request.ContainsKey("mutename"))
            {
                muteName = request["mutename"].ToString();

            }
            else
               muteName = String.Empty;

            return m_service.RemoveMute(agentID, muteID, muteName) ? SuccessResult() : FailureResult();
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
