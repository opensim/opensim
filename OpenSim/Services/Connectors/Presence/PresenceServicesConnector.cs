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

using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;

using OpenSim.Framework.ServiceAuth;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Server.Base;
using OpenMetaverse;

namespace OpenSim.Services.Connectors
{
    public class PresenceServicesConnector : BaseServiceConnector, IPresenceService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;

        public PresenceServicesConnector()
        {
        }

        public PresenceServicesConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/');
        }

        public PresenceServicesConnector(IConfigSource source)
        {
            Initialise(source);
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["PresenceService"];
            if (gridConfig == null)
            {
                m_log.Error("[PRESENCE CONNECTOR]: PresenceService missing from OpenSim.ini");
                throw new Exception("Presence connector init error");
            }

            string serviceURI = gridConfig.GetString("PresenceServerURI",
                    String.Empty);

            if (serviceURI == String.Empty)
            {
                m_log.Error("[PRESENCE CONNECTOR]: No Server URI named in section PresenceService");
                throw new Exception("Presence connector init error");
            }
            m_ServerURI = serviceURI;

            base.Initialise(source, "PresenceService");
        }


        #region IPresenceService

        public bool LoginAgent(string userID, UUID sessionID, UUID secureSessionID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["SCOPEID"] = scopeID.ToString();
            sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "login";

            sendData["UserID"] = userID;
            sendData["SessionID"] = sessionID.ToString();
            sendData["SecureSessionID"] = secureSessionID.ToString();

            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/presence";
            // m_log.DebugFormat("[PRESENCE CONNECTOR]: queryString = {0}", reqString);
            try
            {
                string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        reqString,
                        m_Auth);
                if (reply != string.Empty)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if (replyData.ContainsKey("result"))
                    {
                        if (replyData["result"].ToString().ToLower() == "success")
                            return true;
                        else
                            return false;
                    }
                    else
                        m_log.DebugFormat("[PRESENCE CONNECTOR]: LoginAgent reply data does not contain result field");

                }
                else
                    m_log.DebugFormat("[PRESENCE CONNECTOR]: LoginAgent received empty reply");
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[PRESENCE CONNECTOR]: Exception when contacting presence server at {0}: {1}", uri, e.Message);
            }

            return false;

        }

        public bool LogoutAgent(UUID sessionID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["SCOPEID"] = scopeID.ToString();
            sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "logout";

            sendData["SessionID"] = sessionID.ToString();

            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/presence";
            // m_log.DebugFormat("[PRESENCE CONNECTOR]: queryString = {0}", reqString);
            try
            {
                string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        reqString,
                        m_Auth);
                if (reply != string.Empty)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if (replyData.ContainsKey("result"))
                    {
                        if (replyData["result"].ToString().ToLower() == "success")
                            return true;
                        else
                            return false;
                    }
                    else
                        m_log.DebugFormat("[PRESENCE CONNECTOR]: LogoutAgent reply data does not contain result field");

                }
                else
                    m_log.DebugFormat("[PRESENCE CONNECTOR]: LogoutAgent received empty reply");
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[PRESENCE CONNECTOR]: Exception when contacting presence server at {0}: {1}", uri, e.Message);
            }

            return false;
        }

        public bool LogoutRegionAgents(UUID regionID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["SCOPEID"] = scopeID.ToString();
            sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "logoutregion";

            sendData["RegionID"] = regionID.ToString();

            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/presence";
            // m_log.DebugFormat("[PRESENCE CONNECTOR]: queryString = {0}", reqString);
            try
            {
                string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        reqString,
                        m_Auth);
                if (reply != string.Empty)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if (replyData.ContainsKey("result"))
                    {
                        if (replyData["result"].ToString().ToLower() == "success")
                            return true;
                        else
                            return false;
                    }
                    else
                        m_log.DebugFormat("[PRESENCE CONNECTOR]: LogoutRegionAgents reply data does not contain result field");

                }
                else
                    m_log.DebugFormat("[PRESENCE CONNECTOR]: LogoutRegionAgents received empty reply");
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[PRESENCE CONNECTOR]: Exception when contacting presence server at {0}: {1}", uri, e.Message);
            }

            return false;
        }

        public bool ReportAgent(UUID sessionID, UUID regionID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["SCOPEID"] = scopeID.ToString();
            sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "report";

            sendData["SessionID"] = sessionID.ToString();
            sendData["RegionID"] = regionID.ToString();

            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/presence";
            // m_log.DebugFormat("[PRESENCE CONNECTOR]: queryString = {0}", reqString);
            try
            {
                string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        reqString,
                        m_Auth);
                if (reply != string.Empty)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if (replyData.ContainsKey("result"))
                    {
                        if (replyData["result"].ToString().ToLower() == "success")
                            return true;
                        else
                            return false;
                    }
                    else
                        m_log.DebugFormat("[PRESENCE CONNECTOR]: ReportAgent reply data does not contain result field");

                }
                else
                    m_log.DebugFormat("[PRESENCE CONNECTOR]: ReportAgent received empty reply");
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[PRESENCE CONNECTOR]: Exception when contacting presence server at {0}: {1}", uri, e.Message);
            }

            return false;
        }

        public PresenceInfo GetAgent(UUID sessionID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["SCOPEID"] = scopeID.ToString();
            sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "getagent";

            sendData["SessionID"] = sessionID.ToString();

            string reply = string.Empty;
            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/presence";
            // m_log.DebugFormat("[PRESENCE CONNECTOR]: queryString = {0}", reqString);
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        reqString,
                        m_Auth);
                if (reply == null || (reply != null && reply == string.Empty))
                {
                    m_log.DebugFormat("[PRESENCE CONNECTOR]: GetAgent received null or empty reply");
                    return null;
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[PRESENCE CONNECTOR]: Exception when contacting presence server at {0}: {1}", uri, e.Message);
                return null;
            }

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
            PresenceInfo pinfo = null;

            if ((replyData != null) && replyData.ContainsKey("result") && (replyData["result"] != null))
            {
                if (replyData["result"] is Dictionary<string, object>)
                {
                    pinfo = new PresenceInfo((Dictionary<string, object>)replyData["result"]);
                }
                else
                {
                    if (replyData["result"].ToString() == "null")
                        return null;

                    m_log.DebugFormat("[PRESENCE CONNECTOR]: Invalid reply (result not dictionary) received from presence server when querying for sessionID {0}", sessionID.ToString());
                }
            }
            else
            {
                m_log.DebugFormat("[PRESENCE CONNECTOR]: Invalid reply received from presence server when querying for sessionID {0}", sessionID.ToString());
            }

            return pinfo;
        }

        public PresenceInfo[] GetAgents(string[] userIDs)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            //sendData["SCOPEID"] = scopeID.ToString();
            sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "getagents";

            sendData["uuids"] = new List<string>(userIDs);

            string reply = string.Empty;
            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/presence";
            //m_log.DebugFormat("[PRESENCE CONNECTOR]: queryString = {0}", reqString);
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        reqString,
                        m_Auth);
                if (reply == null || (reply != null && reply == string.Empty))
                {
                    m_log.DebugFormat("[PRESENCE CONNECTOR]: GetAgents received null or empty reply");
                    return null;
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[PRESENCE CONNECTOR]: Exception when contacting presence server at {0}: {1}", uri, e.Message);
            }

            List<PresenceInfo> rinfos = new List<PresenceInfo>();

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            if (replyData != null)
            {
                if (replyData.ContainsKey("result") &&
                    (replyData["result"].ToString() == "null" || replyData["result"].ToString() == "Failure"))
                {
                    return new PresenceInfo[0];
                }

                Dictionary<string, object>.ValueCollection pinfosList = replyData.Values;
                //m_log.DebugFormat("[PRESENCE CONNECTOR]: GetAgents returned {0} elements", pinfosList.Count);
                foreach (object presence in pinfosList)
                {
                    if (presence is Dictionary<string, object>)
                    {
                        PresenceInfo pinfo = new PresenceInfo((Dictionary<string, object>)presence);
                        rinfos.Add(pinfo);
                    }
                    else
                        m_log.DebugFormat("[PRESENCE CONNECTOR]: GetAgents received invalid response type {0}",
                            presence.GetType());
                }
            }
            else
                m_log.DebugFormat("[PRESENCE CONNECTOR]: GetAgents received null response");

            return rinfos.ToArray();
        }


        #endregion

    }
}
