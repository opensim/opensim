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
using System.Collections.Specialized;
using System.Net;
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Server.Base;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

using PresenceInfo = OpenSim.Services.Interfaces.PresenceInfo;

namespace OpenSim.Services.Connectors.SimianGrid
{
    /// <summary>
    /// Connects avatar presence information (for tracking current location and
    /// message routing) to the SimianGrid backend
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class SimianPresenceServiceConnector : IPresenceService, ISharedRegionModule
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_serverUrl = String.Empty;

        #region ISharedRegionModule

        public Type ReplaceableInterface { get { return null; } }
        public void RegionLoaded(Scene scene) { }
        public void PostInitialise() { }
        public void Close() { }

        public SimianPresenceServiceConnector() { }
        public string Name { get { return "SimianPresenceServiceConnector"; } }
        public void AddRegion(Scene scene)
        {
            scene.RegisterModuleInterface<IPresenceService>(this);

            scene.EventManager.OnMakeRootAgent += MakeRootAgentHandler;
            scene.EventManager.OnNewClient += NewClientHandler;
            scene.EventManager.OnSignificantClientMovement += SignificantClientMovementHandler;

            LogoutRegionAgents(scene.RegionInfo.RegionID);
        }
        public void RemoveRegion(Scene scene)
        {
            scene.UnregisterModuleInterface<IPresenceService>(this);

            scene.EventManager.OnMakeRootAgent -= MakeRootAgentHandler;
            scene.EventManager.OnNewClient -= NewClientHandler;
            scene.EventManager.OnSignificantClientMovement -= SignificantClientMovementHandler;

            LogoutRegionAgents(scene.RegionInfo.RegionID);
        }

        #endregion ISharedRegionModule

        public SimianPresenceServiceConnector(IConfigSource source)
        {
            Initialise(source);
        }

        public void Initialise(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["PresenceService"];
            if (gridConfig == null)
            {
                m_log.Error("[PRESENCE CONNECTOR]: PresenceService missing from OpenSim.ini");
                throw new Exception("Presence connector init error");
            }

            string serviceUrl = gridConfig.GetString("PresenceServerURI");
            if (String.IsNullOrEmpty(serviceUrl))
            {
                m_log.Error("[PRESENCE CONNECTOR]: No PresenceServerURI in section PresenceService");
                throw new Exception("Presence connector init error");
            }

            m_serverUrl = serviceUrl;
        }

        #region IPresenceService

        public bool LoginAgent(string userID, UUID sessionID, UUID secureSessionID)
        {
            m_log.ErrorFormat("[PRESENCE CONNECTOR]: Login requested, UserID={0}, SessionID={1}, SecureSessionID={2}",
                userID, sessionID, secureSessionID);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddSession" },
                { "UserID", userID.ToString() }
            };
            if (sessionID != UUID.Zero)
            {
                requestArgs["SessionID"] = sessionID.ToString();
                requestArgs["SecureSessionID"] = secureSessionID.ToString();
            }

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("[PRESENCE CONNECTOR]: Failed to login agent " + userID + ": " + response["Message"].AsString());

            return success;
        }

        public bool LogoutAgent(UUID sessionID, Vector3 position, Vector3 lookAt)
        {
            m_log.InfoFormat("[PRESENCE CONNECTOR]: Logout requested for agent with sessionID " + sessionID);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "RemoveSession" },
                { "SessionID", sessionID.ToString() }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("[PRESENCE CONNECTOR]: Failed to logout agent with sessionID " + sessionID + ": " + response["Message"].AsString());

            return success;
        }

        public bool LogoutRegionAgents(UUID regionID)
        {
            m_log.InfoFormat("[PRESENCE CONNECTOR]: Logout requested for all agents in region " + regionID);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "RemoveSessions" },
                { "SceneID", regionID.ToString() }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("[PRESENCE CONNECTOR]: Failed to logout agents from region " + regionID + ": " + response["Message"].AsString());

            return success;
        }

        public bool ReportAgent(UUID sessionID, UUID regionID, Vector3 position, Vector3 lookAt)
        {
            //m_log.DebugFormat("[PRESENCE CONNECTOR]: Updating session data for agent with sessionID " + sessionID);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "UpdateSession" },
                { "SessionID", sessionID.ToString() },
                { "SceneID", regionID.ToString() },
                { "ScenePosition", position.ToString() },
                { "SceneLookAt", lookAt.ToString() }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("[PRESENCE CONNECTOR]: Failed to update agent session " + sessionID + ": " + response["Message"].AsString());

            return success;
        }

        public PresenceInfo GetAgent(UUID sessionID)
        {
            m_log.DebugFormat("[PRESENCE CONNECTOR]: Requesting session data for agent with sessionID " + sessionID);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetSession" },
                { "SessionID", sessionID.ToString() }
            };

            OSDMap sessionResponse = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (sessionResponse["Success"].AsBoolean())
            {
                UUID userID = sessionResponse["UserID"].AsUUID();
                m_log.DebugFormat("[PRESENCE CONNECTOR]: Requesting user data for " + userID);

                requestArgs = new NameValueCollection
                {
                    { "RequestMethod", "GetUser" },
                    { "UserID", userID.ToString() }
                };

                OSDMap userResponse = WebUtil.PostToService(m_serverUrl, requestArgs);
                if (userResponse["Success"].AsBoolean())
                    return ResponseToPresenceInfo(sessionResponse, userResponse);
                else
                    m_log.Warn("[PRESENCE CONNECTOR]: Failed to retrieve user data for " + userID + ": " + userResponse["Message"].AsString());
            }
            else
            {
                m_log.Warn("[PRESENCE CONNECTOR]: Failed to retrieve session " + sessionID + ": " + sessionResponse["Message"].AsString());
            }

            return null;
        }

        public PresenceInfo[] GetAgents(string[] userIDs)
        {
            List<PresenceInfo> presences = new List<PresenceInfo>(userIDs.Length);

            for (int i = 0; i < userIDs.Length; i++)
            {
                UUID userID;
                if (UUID.TryParse(userIDs[i], out userID) && userID != UUID.Zero)
                    presences.AddRange(GetSessions(userID));
            }

            return presences.ToArray();
        }

        public bool SetHomeLocation(string userID, UUID regionID, Vector3 position, Vector3 lookAt)
        {
            m_log.DebugFormat("[PRESENCE CONNECTOR]: Setting home location for user  " + userID);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddUserData" },
                { "UserID", userID.ToString() },
                { "HomeLocation", SerializeLocation(regionID, position, lookAt) }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("[PRESENCE CONNECTOR]: Failed to set home location for " + userID + ": " + response["Message"].AsString());

            return success;
        }

        #endregion IPresenceService

        #region Presence Detection

        private void MakeRootAgentHandler(ScenePresence sp)
        {
            m_log.DebugFormat("[PRESENCE DETECTOR]: Detected root presence {0} in {1}", sp.UUID, sp.Scene.RegionInfo.RegionName);

            ReportAgent(sp.ControllingClient.SessionId, sp.Scene.RegionInfo.RegionID, sp.AbsolutePosition, sp.Lookat);
            SetLastLocation(sp.UUID, sp.Scene.RegionInfo.RegionID, sp.AbsolutePosition, sp.Lookat);
        }

        private void NewClientHandler(IClientAPI client)
        {
            client.OnConnectionClosed += LogoutHandler;
        }

        private void SignificantClientMovementHandler(IClientAPI client)
        {
            ScenePresence sp;
            if (client.Scene is Scene && ((Scene)client.Scene).TryGetAvatar(client.AgentId, out sp))
                ReportAgent(sp.ControllingClient.SessionId, sp.Scene.RegionInfo.RegionID, sp.AbsolutePosition, sp.Lookat);
        }

        private void LogoutHandler(IClientAPI client)
        {
            if (client.IsLoggingOut)
            {
                client.OnConnectionClosed -= LogoutHandler;

                object obj;
                if (client.Scene.TryGetAvatar(client.AgentId, out obj) && obj is ScenePresence)
                {
                    // The avatar is still in the scene, we can get the exact logout position
                    ScenePresence sp = (ScenePresence)obj;
                    SetLastLocation(client.AgentId, client.Scene.RegionInfo.RegionID, sp.AbsolutePosition, sp.Lookat);
                }
                else
                {
                    // The avatar was already removed from the scene, store LastLocation using the most recent session data
                    m_log.Warn("[PRESENCE]: " + client.Name + " has already been removed from the scene, storing approximate LastLocation");
                    SetLastLocation(client.SessionId);
                }

                LogoutAgent(client.SessionId, Vector3.Zero, Vector3.UnitX);
            }
        }

        #endregion Presence Detection

        #region Helpers

        private OSDMap GetUserData(UUID userID)
        {
            m_log.DebugFormat("[PRESENCE CONNECTOR]: Requesting user data for " + userID);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetUser" },
                { "UserID", userID.ToString() }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean() && response["User"] is OSDMap)
                return response;
            else
                m_log.Warn("[PRESENCE CONNECTOR]: Failed to retrieve user data for " + userID + ": " + response["Message"].AsString());

            return null;
        }

        private OSDMap GetSessionData(UUID sessionID)
        {
            m_log.DebugFormat("[PRESENCE CONNECTOR]: Requesting session data for session " + sessionID);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetSession" },
                { "SessionID", sessionID.ToString() }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean())
                return response;
            else
                m_log.Warn("[PRESENCE CONNECTOR]: Failed to retrieve session data for session " + sessionID);

            return null;
        }

        private List<PresenceInfo> GetSessions(UUID userID)
        {
            List<PresenceInfo> presences = new List<PresenceInfo>(1);

            OSDMap userResponse = GetUserData(userID);
            if (userResponse != null)
            {
                m_log.DebugFormat("[PRESENCE CONNECTOR]: Requesting sessions for " + userID);

                NameValueCollection requestArgs = new NameValueCollection
                {
                    { "RequestMethod", "GetSession" },
                    { "UserID", userID.ToString() }
                };

                OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
                if (response["Success"].AsBoolean())
                {
                    PresenceInfo presence = ResponseToPresenceInfo(response, userResponse);
                    if (presence != null)
                        presences.Add(presence);
                }
                else
                {
                    m_log.Warn("[PRESENCE CONNECTOR]: Failed to retrieve sessions for " + userID + ": " + response["Message"].AsString());
                }
            }

            return presences;
        }

        /// <summary>
        /// Fetch the last known avatar location with GetSession and persist it
        /// as user data with AddUserData
        /// </summary>
        private bool SetLastLocation(UUID sessionID)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetSession" },
                { "SessionID", sessionID.ToString() }
            };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (success)
            {
                UUID userID = response["UserID"].AsUUID();
                UUID sceneID = response["SceneID"].AsUUID();
                Vector3 position = response["ScenePosition"].AsVector3();
                Vector3 lookAt = response["SceneLookAt"].AsVector3();

                return SetLastLocation(userID, sceneID, position, lookAt);
            }
            else
            {
                m_log.Warn("[PRESENCE CONNECTOR]: Failed to retrieve presence information for session " + sessionID +
                    " while saving last location: " + response["Message"].AsString());
            }

            return success;
        }

        private bool SetLastLocation(UUID userID, UUID sceneID, Vector3 position, Vector3 lookAt)
        {
            NameValueCollection requestArgs = new NameValueCollection
                {
                    { "RequestMethod", "AddUserData" },
                    { "UserID", userID.ToString() },
                    { "LastLocation", SerializeLocation(sceneID, position, lookAt) }
                };

            OSDMap response = WebUtil.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("[PRESENCE CONNECTOR]: Failed to set last location for " + userID + ": " + response["Message"].AsString());

            return success;
        }

        private PresenceInfo ResponseToPresenceInfo(OSDMap sessionResponse, OSDMap userResponse)
        {
            if (sessionResponse == null)
                return null;

            PresenceInfo info = new PresenceInfo();

            info.Online = true;
            info.UserID = sessionResponse["UserID"].AsUUID().ToString();
            info.RegionID = sessionResponse["SceneID"].AsUUID();
            info.Position = sessionResponse["ScenePosition"].AsVector3();
            info.LookAt = sessionResponse["SceneLookAt"].AsVector3();

            if (userResponse != null && userResponse["User"] is OSDMap)
            {
                OSDMap user = (OSDMap)userResponse["User"];

                info.Login = user["LastLoginDate"].AsDate();
                info.Logout = user["LastLogoutDate"].AsDate();
                DeserializeLocation(user["HomeLocation"].AsString(), out info.HomeRegionID, out info.HomePosition, out info.HomeLookAt);
            }

            return info;
        }

        private string SerializeLocation(UUID regionID, Vector3 position, Vector3 lookAt)
        {
            return "{" + String.Format("\"SceneID\":\"{0}\",\"Position\":\"{1}\",\"LookAt\":\"{2}\"", regionID, position, lookAt) + "}";
        }

        private bool DeserializeLocation(string location, out UUID regionID, out Vector3 position, out Vector3 lookAt)
        {
            OSDMap map = null;

            try { map = OSDParser.DeserializeJson(location) as OSDMap; }
            catch { }

            if (map != null)
            {
                regionID = map["SceneID"].AsUUID();
                if (Vector3.TryParse(map["Position"].AsString(), out position) &&
                    Vector3.TryParse(map["LookAt"].AsString(), out lookAt))
                {
                    return true;
                }
            }

            regionID = UUID.Zero;
            position = Vector3.Zero;
            lookAt = Vector3.Zero;
            return false;
        }

        #endregion Helpers
    }
}
