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
using PresenceInfo = OpenSim.Services.Interfaces.PresenceInfo;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Presence
{
    public class HGPresenceBroker : ISharedRegionModule, IPresenceService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region ISharedRegionModule

        private bool m_Enabled = false;

        private PresenceDetector m_PresenceDetector;
        private IPresenceService m_GridService;
        private IPresenceService m_HGService;

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "HGPresenceBroker"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("PresenceServices", "");
                if (name == Name)
                {
                    //m_RemoteConnector = new InventoryServicesConnector(source);

                    m_Enabled = true;

                    m_PresenceDetector = new PresenceDetector(this);


                    IConfig pConfig = source.Configs["PresenceService"];
                    if (pConfig == null)
                    {
                        m_log.Error("[HG PRESENCE CONNECTOR]: PresenceService missing from OpenSim.ini");
                        return;
                    }

                    string localDll = pConfig.GetString("LocalGridPresenceService",
                            String.Empty);
                    string HGDll = pConfig.GetString("HypergridPresenceService",
                            String.Empty);

                    if (localDll == String.Empty)
                    {
                        m_log.Error("[HG PRESENCE CONNECTOR]: No LocalGridPresenceService named in section PresenceService");
                        //return;
                        throw new Exception("Unable to proceed. Please make sure your ini files in config-include are updated according to .example's");
                    }

                    if (HGDll == String.Empty)
                    {
                        m_log.Error("[HG PRESENCE CONNECTOR]: No HypergridPresenceService named in section PresenceService");
                        //return;
                        throw new Exception("Unable to proceed. Please make sure your ini files in config-include are updated according to .example's");
                    }

                    Object[] args = new Object[] { source };
                    m_GridService = ServerUtils.LoadPlugin<IPresenceService>(localDll, args);

                    m_HGService = ServerUtils.LoadPlugin<IPresenceService>(HGDll, args);
                    // no. This will be:
                    // m_HGService = new HGPresenceServiceConnector();

                    if (m_GridService == null)
                    {
                        m_log.Error("[HG PRESENCE CONNECTOR]: Can't load local presence service");
                        return;
                    }
                    if (m_HGService == null)
                    {
                        m_log.Error("[HG PRESENCE CONNECTOR]: Can't load hypergrid presence service");
                        return;
                    }

                    m_log.Info("[HG PRESENCE CONNECTOR]: Hypergrid presence enabled");
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

            scene.RegisterModuleInterface<IPresenceService>(this);
            m_PresenceDetector.AddRegion(scene);

            m_log.InfoFormat("[HG PRESENCE CONNECTOR]: Enabled hypergrid presence for region {0}", scene.RegionInfo.RegionName);

        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_PresenceDetector.RemoveRegion(scene);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

        }

        #endregion

        #region IPresenceService

        public bool LoginAgent(UUID principalID, UUID sessionID, UUID secureSessionID)
        {
            m_log.Warn("[HG PRESENCE CONNECTOR]: LoginAgent connector not implemented at the simulators");
            return false;
        }

        public bool LogoutAgent(UUID sessionID)
        {
            return m_GridService.LogoutAgent(sessionID);
        }


        public bool LogoutRegionAgents(UUID regionID)
        {
            return m_GridService.LogoutRegionAgents(regionID);
        }

        public bool ReportAgent(UUID sessionID, UUID regionID, Vector3 position, Vector3 lookAt)
        {
            return m_GridService.ReportAgent(sessionID, regionID, position, lookAt);
        }

        public PresenceInfo GetAgent(UUID sessionID)
        {
            return m_GridService.GetAgent(sessionID);
        }

        public PresenceInfo[] GetAgents(string[] principalIDs)
        {
            Dictionary<string, List<string>> triage = new Dictionary<string, List<string>>();
            List<PresenceInfo> presences = new List<PresenceInfo>();

            foreach (string s in principalIDs)
            {
                string url = string.Empty;
                string uuid = UUID.Zero.ToString();
                StringToUrlAndUUID(s, out url, out uuid);
                if (triage.ContainsKey(url))
                    triage[url].Add(uuid);
                else
                {
                    List<string> list = new List<string>();
                    list.Add(uuid);
                    triage.Add(url, list);
                }
            }

            foreach (KeyValuePair<string, List<string>> kvp in triage)
            {
                if (kvp.Key == "local")
                {
                    PresenceInfo[] pinfos = m_GridService.GetAgents(kvp.Value.ToArray());
                    presences.AddRange(pinfos);
                }
                else
                {
                    PresenceInfo[] pinfos = m_HGService.GetAgents(/*kvp.Key,*/ kvp.Value.ToArray());
                    presences.AddRange(pinfos);
                }
            }

            return presences.ToArray();
        }

        #endregion

        private void StringToUrlAndUUID(string id, out string url, out string uuid)
        {
            url = String.Empty;
            uuid = String.Empty;

            Uri uri;

            if (Uri.TryCreate(id, UriKind.Absolute, out uri) &&
                    uri.Scheme == Uri.UriSchemeHttp)
            {
                url = "http://" + uri.Authority;
                uuid = uri.LocalPath.Trim(new char[] { '/' });
            }
            else
            {
                url = "local";
                uuid = id;
            }
        }

    }
}
