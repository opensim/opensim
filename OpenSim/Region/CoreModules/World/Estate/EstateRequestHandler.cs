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
using System.Reflection;
using System.Xml;

using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;

using OpenMetaverse;
using log4net;

namespace OpenSim.Region.CoreModules.World.Estate
{
    public class EstateSimpleRequestHandler :SimpleStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected EstateModule m_EstateModule;
        protected Object m_RequestLock = new Object();
        private string token;

        public EstateSimpleRequestHandler(EstateModule fmodule, string _token) : base("/estate")
        {
            m_EstateModule = fmodule;
            token = _token;
        }

        protected override void ProcessRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            httpResponse.KeepAlive = false;
            if (httpRequest.HttpMethod != "POST")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            httpResponse.StatusCode = (int)HttpStatusCode.OK;

            // m_log.DebugFormat("[ESTATE HANDLER]: query String: {0}", body);

            try
            {
                string body;
                using (StreamReader sr = new StreamReader(httpRequest.InputStream))
                    body = sr.ReadToEnd();

                body = body.Trim();
                lock (m_RequestLock)
                {
                    Dictionary<string, object> request = ServerUtils.ParseQueryString(body);

                    bool fail = true;
                    while(true)
                    {
                        if (!request.ContainsKey("METHOD"))
                            break;

                        if (!request.ContainsKey("TOKEN"))
                            break;

                        string reqToken = request["TOKEN"].ToString();

                        if(token != reqToken)
                            break;

                        fail = false;
                        break;
                    }
                    if(fail)
                    {
                        httpResponse.RawBuffer = FailureResult();
                        return;
                    }

                    string method = request["METHOD"].ToString();
                    request.Remove("METHOD");
                    request.Remove("TOKEN");

                    try
                    {
                        m_EstateModule.InInfoUpdate = false;
                        switch (method)
                        {
                            case "update_covenant":
                                httpResponse.RawBuffer = UpdateCovenant(request);
                                return;
                            case "update_estate":
                                httpResponse.RawBuffer = UpdateEstate(request);
                                return;
                            case "estate_message":
                                httpResponse.RawBuffer = EstateMessage(request);
                                return;
                            case "teleport_home_one_user":
                                httpResponse.RawBuffer = TeleportHomeOneUser(request);
                                return;
                            case "teleport_home_all_users":
                                httpResponse.RawBuffer = TeleportHomeAllUsers(request);
                                return;
                        }
                    }
                    finally
                    {
                        m_EstateModule.InInfoUpdate = false;
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Debug("[ESTATE]: Exception {0}" + e.ToString());
            }

            httpResponse.RawBuffer = FailureResult();
        }

        byte[] TeleportHomeAllUsers(Dictionary<string, object> request)
        {
            UUID PreyID = UUID.Zero;
            int EstateID = 0;

            if (!request.ContainsKey("EstateID"))
                return FailureResult();

            if (!Int32.TryParse(request["EstateID"].ToString(), out EstateID))
                return FailureResult();

            foreach (Scene s in m_EstateModule.Scenes)
            {
                if (s.RegionInfo.EstateSettings.EstateID == EstateID)
                {
                    s.ForEachScenePresence(delegate(ScenePresence p) {
                        if (p != null && !p.IsChildAgent)
                        {
                            p.ControllingClient.SendTeleportStart(16);
                            s.TeleportClientHome(p.ControllingClient.AgentId, p.ControllingClient);
                        }
                    });
                }
            }

            return SuccessResult();
        }

        byte[] TeleportHomeOneUser(Dictionary<string, object> request)
        {
            UUID PreyID = UUID.Zero;
            int EstateID = 0;

            if (!request.ContainsKey("PreyID") ||
                !request.ContainsKey("EstateID"))
            {
                return FailureResult();
            }

            if (!UUID.TryParse(request["PreyID"].ToString(), out PreyID))
                return FailureResult();

            if (!Int32.TryParse(request["EstateID"].ToString(), out EstateID))
                return FailureResult();

            foreach (Scene s in m_EstateModule.Scenes)
            {
                if (s.RegionInfo.EstateSettings.EstateID == EstateID)
                {
                    ScenePresence p = s.GetScenePresence(PreyID);
                    if (p != null && !p.IsChildAgent)
                    {
                        p.ControllingClient.SendTeleportStart(16);
                        s.TeleportClientHome(PreyID, p.ControllingClient);
                    }
                }
            }

            return SuccessResult();
        }

        byte[] EstateMessage(Dictionary<string, object> request)
        {
            UUID FromID = UUID.Zero;
            string FromName = String.Empty;
            string Message = String.Empty;
            int EstateID = 0;

            if (!request.ContainsKey("FromID") ||
                !request.ContainsKey("FromName") ||
                !request.ContainsKey("Message") ||
                !request.ContainsKey("EstateID"))
            {
                return FailureResult();
            }

            if (!UUID.TryParse(request["FromID"].ToString(), out FromID))
                return FailureResult();

            if (!Int32.TryParse(request["EstateID"].ToString(), out EstateID))
                return FailureResult();

            FromName = request["FromName"].ToString();
            Message = request["Message"].ToString();

            foreach (Scene s in m_EstateModule.Scenes)
            {
                if (s.RegionInfo.EstateSettings.EstateID == EstateID)
                {
                    IDialogModule dm = s.RequestModuleInterface<IDialogModule>();

                    if (dm != null)
                    {
                        dm.SendNotificationToUsersInRegion(FromID, FromName,
                                Message);
                    }
                }
            }

            return SuccessResult();
        }

        byte[] UpdateCovenant(Dictionary<string, object> request)
        {
            UUID CovenantID = UUID.Zero;
            int EstateID = 0;

            if (!request.ContainsKey("CovenantID") || !request.ContainsKey("EstateID"))
                return FailureResult();

            if (!UUID.TryParse(request["CovenantID"].ToString(), out CovenantID))
                return FailureResult();

            if (!Int32.TryParse(request["EstateID"].ToString(), out EstateID))
                return FailureResult();

            foreach (Scene s in m_EstateModule.Scenes)
            {
                if (s.RegionInfo.EstateSettings.EstateID == (uint)EstateID)
                    s.RegionInfo.RegionSettings.Covenant = CovenantID;
            }

            return SuccessResult();
        }

        byte[] UpdateEstate(Dictionary<string, object> request)
        {
            int EstateID = 0;

            if (!request.ContainsKey("EstateID"))
                return FailureResult();
            if (!Int32.TryParse(request["EstateID"].ToString(), out EstateID))
                return FailureResult();

            foreach (Scene s in m_EstateModule.Scenes)
            {
                if (s.RegionInfo.EstateSettings.EstateID == (uint)EstateID)
                    s.ReloadEstateData();
            }
            return SuccessResult();
        }

        private byte[] FailureResult()
        {
            return BoolResult(false);
        }

        private byte[] SuccessResult()
        {
            return BoolResult(true);
        }

        private byte[] BoolResult(bool value)
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "RESULT", "");
            result.AppendChild(doc.CreateTextNode(value.ToString()));

            rootElement.AppendChild(result);

            return Util.DocToBytes(doc);
        }

    }
}
