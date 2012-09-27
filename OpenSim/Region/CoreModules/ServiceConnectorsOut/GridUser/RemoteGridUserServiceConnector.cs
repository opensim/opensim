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
using System.Reflection;

using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.GridUser
{
    public class RemoteGridUserServicesConnector : ISharedRegionModule, IGridUserService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private const int KEEPTIME = 30; // 30 secs
        private ExpiringCache<string, GridUserInfo> m_Infos = new ExpiringCache<string, GridUserInfo>();

        #region ISharedRegionModule

        private bool m_Enabled = false;

        private ActivityDetector m_ActivityDetector;
        private IGridUserService m_RemoteConnector;

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "RemoteGridUserServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("GridUserServices", "");
                if (name == Name)
                {
                    m_RemoteConnector = new GridUserServicesConnector(source);

                    m_Enabled = true;

                    m_ActivityDetector = new ActivityDetector(this);

                    m_log.Info("[REMOTE GRID USER CONNECTOR]: Remote grid user enabled");
                }
            }

        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.RegisterModuleInterface<IGridUserService>(this);
            m_ActivityDetector.AddRegion(scene);

            m_log.InfoFormat("[REMOTE GRID USER CONNECTOR]: Enabled remote grid user for region {0}", scene.RegionInfo.RegionName);

        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_ActivityDetector.RemoveRegion(scene);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

        }

        #endregion

        #region IGridUserService

        public GridUserInfo LoggedIn(string userID)
        {
            m_log.Warn("[REMOTE GRID USER CONNECTOR]: LoggedIn not implemented at the simulators");
            return null;
        }

        public bool LoggedOut(string userID, UUID sessionID, UUID region, Vector3 position, Vector3 lookat)
        {
            if (m_Infos.Contains(userID))
                m_Infos.Remove(userID);

            return m_RemoteConnector.LoggedOut(userID, sessionID, region, position, lookat);
        }


        public bool SetHome(string userID, UUID regionID, Vector3 position, Vector3 lookAt)
        {
            if (m_RemoteConnector.SetHome(userID, regionID, position, lookAt))
            {
                // Update the cache too
                GridUserInfo info = null;
                if (m_Infos.TryGetValue(userID, out info))
                {
                    info.HomeRegionID = regionID;
                    info.HomePosition = position;
                    info.HomeLookAt = lookAt;
                }
                return true;
            }

            return false;
        }

        public bool SetLastPosition(string userID, UUID sessionID, UUID regionID, Vector3 position, Vector3 lookAt)
        {
            if (m_RemoteConnector.SetLastPosition(userID, sessionID, regionID, position, lookAt))
            {
                // Update the cache too
                GridUserInfo info = null;
                if (m_Infos.TryGetValue(userID, out info))
                {
                    info.LastRegionID = regionID;
                    info.LastPosition = position;
                    info.LastLookAt = lookAt;
                }
                return true;
            }

            return false;
        }

        public GridUserInfo GetGridUserInfo(string userID)
        {
            GridUserInfo info = null;
            if (m_Infos.TryGetValue(userID, out info))
                return info;

            info = m_RemoteConnector.GetGridUserInfo(userID);

            m_Infos.AddOrUpdate(userID, info, KEEPTIME);

            return info;
        }

        public GridUserInfo[] GetGridUserInfo(string[] userID)
        {
            return m_RemoteConnector.GetGridUserInfo(userID);
        }

        #endregion

    }
}
