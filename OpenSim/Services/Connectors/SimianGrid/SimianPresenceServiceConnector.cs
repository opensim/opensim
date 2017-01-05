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
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

using PresenceInfo = OpenSim.Services.Interfaces.PresenceInfo;

namespace OpenSim.Services.Connectors.SimianGrid
{
    /// <summary>
    /// Connects avatar presence information (for tracking current location and
    /// message routing) to the SimianGrid backend
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "SimianPresenceServiceConnector")]
    public class SimianPresenceServiceConnector : IPresenceService, IGridUserService, ISharedRegionModule
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_serverUrl = String.Empty;
        private SimianActivityDetector m_activityDetector;
        private bool m_Enabled = false;

        #region ISharedRegionModule

        public Type ReplaceableInterface { get { return null; } }
        public void RegionLoaded(Scene scene) { }
        public void PostInitialise() { }
        public void Close() { }

        public SimianPresenceServiceConnector() { }
        public string Name { get { return "SimianPresenceServiceConnector"; } }
        public void AddRegion(Scene scene)
        {
            if (m_Enabled)
            {
                scene.RegisterModuleInterface<IPresenceService>(this);
                scene.RegisterModuleInterface<IGridUserService>(this);

                m_activityDetector.AddRegion(scene);

                LogoutRegionAgents(scene.RegionInfo.RegionID);
            }
        }
        public void RemoveRegion(Scene scene)
        {
            if (m_Enabled)
            {
                scene.UnregisterModuleInterface<IPresenceService>(this);
                scene.UnregisterModuleInterface<IGridUserService>(this);

                m_activityDetector.RemoveRegion(scene);

                LogoutRegionAgents(scene.RegionInfo.RegionID);
            }
        }

        #endregion ISharedRegionModule

        public SimianPresenceServiceConnector(IConfigSource source)
        {
            CommonInit(source);
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("PresenceServices", "");
                if (name == Name)
                    CommonInit(source);
            }
        }

        private void CommonInit(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["PresenceService"];
            if (gridConfig != null)
            {
                string serviceUrl = gridConfig.GetString("PresenceServerURI");
                if (!String.IsNullOrEmpty(serviceUrl))
                {
                    if (!serviceUrl.EndsWith("/") && !serviceUrl.EndsWith("="))
                        serviceUrl = serviceUrl + '/';
                    m_serverUrl = serviceUrl;
                    m_activityDetector = new SimianActivityDetector(this);
                    m_Enabled = true;
                }
            }

            if (String.IsNullOrEmpty(m_serverUrl))
                m_log.Info("[SIMIAN PRESENCE CONNECTOR]: No PresenceServerURI specified, disabling connector");
        }

        #region IPresenceService

        public bool LoginAgent(string userID, UUID sessionID, UUID secureSessionID)
        {
            m_log.ErrorFormat("[SIMIAN PRESENCE CONNECTOR]: Login requested, UserID={0}, SessionID={1}, SecureSessionID={2}",
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

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("[SIMIAN PRESENCE CONNECTOR]: Failed to login agent " + userID + ": " + response["Message"].AsString());

            return success;
        }

        public bool LogoutAgent(UUID sessionID)
        {
            // m_log.InfoFormat("[SIMIAN PRESENCE CONNECTOR]: Logout requested for agent with sessionID " + sessionID);

            NameValueCollection requestArgs = new NameValueCollection
                {
                        { "RequestMethod", "RemoveSession" },
                        { "SessionID", sessionID.ToString() }
                };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("[SIMIAN PRESENCE CONNECTOR]: Failed to logout agent with sessionID " + sessionID + ": " + response["Message"].AsString());

            return success;
        }

        public bool LogoutRegionAgents(UUID regionID)
        {
            // m_log.InfoFormat("[SIMIAN PRESENCE CONNECTOR]: Logout requested for all agents in region " + regionID);

            NameValueCollection requestArgs = new NameValueCollection
                {
                        { "RequestMethod", "RemoveSessions" },
                        { "SceneID", regionID.ToString() }
                };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("[SIMIAN PRESENCE CONNECTOR]: Failed to logout agents from region " + regionID + ": " + response["Message"].AsString());

            return success;
        }

        public bool ReportAgent(UUID sessionID, UUID regionID)
        {
            // Not needed for SimianGrid
            return true;
        }

        public PresenceInfo GetAgent(UUID sessionID)
        {
            OSDMap sessionResponse = GetSessionDataFromSessionID(sessionID);
            if (sessionResponse == null)
            {
                m_log.WarnFormat("[SIMIAN PRESENCE CONNECTOR]: Failed to retrieve session {0}: {1}",sessionID.ToString(),sessionResponse["Message"].AsString());
                return null;
            }

            UUID userID = sessionResponse["UserID"].AsUUID();
            OSDMap userResponse = GetUserData(userID);
            if (userResponse == null)
            {
                m_log.WarnFormat("[SIMIAN PRESENCE CONNECTOR]: Failed to retrieve user data for {0}: {1}",userID.ToString(),userResponse["Message"].AsString());
                return null;
            }

            return ResponseToPresenceInfo(sessionResponse);
        }

        public PresenceInfo[] GetAgents(string[] userIDs)
        {
            List<PresenceInfo> presences = new List<PresenceInfo>();

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetSessions" },
                { "UserIDList", String.Join(",",userIDs) }
            };

            OSDMap sessionListResponse = SimianGrid.PostToService(m_serverUrl, requestArgs);
            if (! sessionListResponse["Success"].AsBoolean())
            {
                m_log.WarnFormat("[SIMIAN PRESENCE CONNECTOR]: Failed to retrieve sessions: {0}",sessionListResponse["Message"].AsString());
                return null;
            }

            OSDArray sessionList = sessionListResponse["Sessions"] as OSDArray;
            for (int i = 0; i < sessionList.Count; i++)
            {
                OSDMap sessionInfo = sessionList[i] as OSDMap;
                presences.Add(ResponseToPresenceInfo(sessionInfo));
            }

            return presences.ToArray();
        }

        #endregion IPresenceService

        #region IGridUserService

        public GridUserInfo LoggedIn(string userID)
        {
            // Never implemented at the sim
            return null;
        }

        public bool LoggedOut(string userID, UUID sessionID, UUID regionID, Vector3 lastPosition, Vector3 lastLookAt)
        {
            // m_log.DebugFormat("[SIMIAN PRESENCE CONNECTOR]: Logging out user " + userID);

            // Remove the session to mark this user offline
            if (!LogoutAgent(sessionID))
                return false;

            // Save our last position as user data
            NameValueCollection requestArgs = new NameValueCollection
                {
                        { "RequestMethod", "AddUserData" },
                        { "UserID", userID.ToString() },
                        { "LastLocation", SerializeLocation(regionID, lastPosition, lastLookAt) }
                };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("[SIMIAN PRESENCE CONNECTOR]: Failed to set last location for " + userID + ": " + response["Message"].AsString());

            return success;
        }

        public bool SetHome(string userID, UUID regionID, Vector3 position, Vector3 lookAt)
        {
            // m_log.DebugFormat("[SIMIAN PRESENCE CONNECTOR]: Setting home location for user  " + userID);

            NameValueCollection requestArgs = new NameValueCollection
                {
                        { "RequestMethod", "AddUserData" },
                        { "UserID", userID.ToString() },
                        { "HomeLocation", SerializeLocation(regionID, position, lookAt) }
                };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("[SIMIAN PRESENCE CONNECTOR]: Failed to set home location for " + userID + ": " + response["Message"].AsString());

            return success;
        }

        public bool SetLastPosition(string userID, UUID sessionID, UUID regionID, Vector3 lastPosition, Vector3 lastLookAt)
        {
            return UpdateSession(sessionID, regionID, lastPosition, lastLookAt);
        }

        public GridUserInfo GetGridUserInfo(string user)
        {
            // m_log.DebugFormat("[SIMIAN PRESENCE CONNECTOR]: Requesting session data for agent " + user);

            UUID userID = new UUID(user);
            OSDMap userResponse = GetUserData(userID);

            if (userResponse == null)
            {
                m_log.WarnFormat("[SIMIAN PRESENCE CONNECTOR]: Failed to retrieve user data for {0}", userID);
            }

            // Note that ResponseToGridUserInfo properly checks for and returns a null if passed a null.
            return ResponseToGridUserInfo(userResponse);

        }

        #endregion

        #region Helpers

        private OSDMap GetUserData(UUID userID)
        {
            // m_log.DebugFormat("[SIMIAN PRESENCE CONNECTOR]: Requesting user data for " + userID);

            NameValueCollection requestArgs = new NameValueCollection
                {
                        { "RequestMethod", "GetUser" },
                        { "UserID", userID.ToString() }
                };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean() && response["User"] is OSDMap)
                return response;

            m_log.WarnFormat("[SIMIAN PRESENCE CONNECTOR]: Failed to retrieve user data for {0}; {1}",userID.ToString(),response["Message"].AsString());
            return null;
        }

        private OSDMap GetSessionDataFromSessionID(UUID sessionID)
        {
            NameValueCollection requestArgs = new NameValueCollection
                {
                        { "RequestMethod", "GetSession" },
                        { "SessionID", sessionID.ToString() }
                };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            if (response["Success"].AsBoolean())
                return response;

            m_log.WarnFormat("[SIMIAN PRESENCE CONNECTOR]: Failed to retrieve session data for {0}; {1}",sessionID.ToString(),response["Message"].AsString());
            return null;
        }

        private bool UpdateSession(UUID sessionID, UUID regionID, Vector3 lastPosition, Vector3 lastLookAt)
        {
            // Save our current location as session data
            NameValueCollection requestArgs = new NameValueCollection
                {
                        { "RequestMethod", "UpdateSession" },
                        { "SessionID", sessionID.ToString() },
                        { "SceneID", regionID.ToString() },
                        { "ScenePosition", lastPosition.ToString() },
                        { "SceneLookAt", lastLookAt.ToString() }
                };

            OSDMap response = SimianGrid.PostToService(m_serverUrl, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("[SIMIAN PRESENCE CONNECTOR]: Failed to update agent session " + sessionID + ": " + response["Message"].AsString());

            return success;
        }

        private PresenceInfo ResponseToPresenceInfo(OSDMap sessionResponse)
        {
            if (sessionResponse == null)
                return null;

            PresenceInfo info = new PresenceInfo();

            info.UserID = sessionResponse["UserID"].AsUUID().ToString();
            info.RegionID = sessionResponse["SceneID"].AsUUID();

            return info;
        }

        private GridUserInfo ResponseToGridUserInfo(OSDMap userResponse)
        {
            if (userResponse != null && userResponse["User"] is OSDMap)
            {
                GridUserInfo info = new GridUserInfo();

                info.Online = true;
                info.UserID = userResponse["UserID"].AsUUID().ToString();
                info.LastRegionID = userResponse["SceneID"].AsUUID();
                info.LastPosition = userResponse["ScenePosition"].AsVector3();
                info.LastLookAt = userResponse["SceneLookAt"].AsVector3();

                OSDMap user = (OSDMap)userResponse["User"];

                info.Login = user["LastLoginDate"].AsDate();
                info.Logout = user["LastLogoutDate"].AsDate();
                DeserializeLocation(user["HomeLocation"].AsString(), out info.HomeRegionID, out info.HomePosition, out info.HomeLookAt);

                return info;
            }

            return null;
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

        public GridUserInfo[] GetGridUserInfo(string[] userIDs)
        {
            return new GridUserInfo[0];
         }
        #endregion Helpers
    }
}
