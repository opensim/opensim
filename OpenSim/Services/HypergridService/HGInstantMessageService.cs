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
using System.Net;
using System.Reflection;

using OpenSim.Framework;
using OpenSim.Services.Connectors.Friends;
using OpenSim.Services.Connectors.Hypergrid;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.InstantMessage;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Server.Base;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// Inter-grid IM
    /// </summary>
    public class HGInstantMessageService : IInstantMessage
    {
        private static readonly ILog m_log = LogManager.GetLogger( MethodBase.GetCurrentMethod().DeclaringType);

        static bool m_Initialized = false;

        protected static IGridService m_GridService;
        protected static IPresenceService m_PresenceService;
        protected static IUserAgentService m_UserAgentService;
        protected static IOfflineIMService m_OfflineIMService;

        protected static IInstantMessageSimConnector m_IMSimConnector;
        protected static readonly ExpiringCacheOS<UUID, string> m_UserLocationMap = new ExpiringCacheOS<UUID, string>(1000);

        private static bool m_ForwardOfflineGroupMessages;
        private static bool m_InGatekeeper;
        private string m_messageKey;

        public HGInstantMessageService(IConfigSource config)
            : this(config, null)
        {
        }

        public HGInstantMessageService(IConfigSource config, IInstantMessageSimConnector imConnector)
        {
            if (imConnector != null)
                m_IMSimConnector = imConnector;

            if (!m_Initialized)
            {
                m_Initialized = true;

                IConfig serverConfig = config.Configs["HGInstantMessageService"];
                if (serverConfig == null)
                    throw new Exception("No section HGInstantMessageService in config file");

                string gridService = serverConfig.GetString("GridService", string.Empty);
                if (string.IsNullOrEmpty(gridService))
                    throw new Exception("[HG IM SERVICE]: GridService not set in [HGInstantMessageService]");
                string presenceService = serverConfig.GetString("PresenceService", string.Empty);
                if (string.IsNullOrEmpty(presenceService))
                    throw new Exception("[HG IM SERVICE]: PresenceService not set in [HGInstantMessageService]");
                string userAgentService = serverConfig.GetString("UserAgentService", string.Empty);
                if (string.IsNullOrEmpty(userAgentService))
                    m_log.WarnFormat("[HG IM SERVICE]: UserAgentService not set in [HGInstantMessageService]");

                object[] args = new object[] { config };
                try
                {
                    m_GridService = ServerUtils.LoadPlugin<IGridService>(gridService, args);
                }
                catch
                {
                    throw new Exception("[HG IM SERVICE]: Unable to load GridService");
                }

                try
                {
                    m_PresenceService = ServerUtils.LoadPlugin<IPresenceService>(presenceService, args);
                }
                catch
                {
                    throw new Exception("[HG IM SERVICE]: Unable to load PresenceService");
                }

                try
                {
                    m_UserAgentService = ServerUtils.LoadPlugin<IUserAgentService>(userAgentService, args);
                }
                catch
                {
                    m_log.WarnFormat("[HG IM SERVICE]: Unable to load PresenceService");
                }


                m_InGatekeeper = serverConfig.GetBoolean("InGatekeeper", false);

                IConfig cnf = config.Configs["Messaging"];
                if (cnf == null)
                    return;

                m_messageKey = cnf.GetString("MessageKey", String.Empty);
                m_ForwardOfflineGroupMessages = cnf.GetBoolean("ForwardOfflineGroupMessages", false);

                if (m_InGatekeeper)
                {
                    m_log.Debug("[HG IM SERVICE]: Starting In Robust GateKeeper");

                    string offlineIMService = cnf.GetString("OfflineIMService", string.Empty);
                    if (offlineIMService != string.Empty)
                        m_OfflineIMService = ServerUtils.LoadPlugin<IOfflineIMService>(offlineIMService, args);
                }
                else
                    m_log.Debug("[HG IM SERVICE]: Starting");
            }
        }

        public bool IncomingInstantMessage(GridInstantMessage im)
        {
//            m_log.DebugFormat("[HG IM SERVICE]: Received message from {0} to {1}", im.fromAgentID, im.toAgentID);
//            UUID toAgentID = new UUID(im.toAgentID);

            bool success = false;
            if (m_IMSimConnector != null)
            {
                //m_log.DebugFormat("[XXX] SendIMToRegion local im connector");
                success = m_IMSimConnector.SendInstantMessage(im);
            }
            else
            {
                success = TrySendInstantMessage(im, "", true, false);
            }

            if (!success && m_InGatekeeper) // we do this only in the Gatekeeper IM service
                UndeliveredMessage(im);

            return success;
        }

        public bool OutgoingInstantMessage(GridInstantMessage im, string url, bool foreigner)
        {
//            m_log.DebugFormat("[HG IM SERVICE]: Sending message from {0} to {1}@{2}", im.fromAgentID, im.toAgentID, url);
            return TrySendInstantMessage(im, url, true, foreigner);
        }

        protected bool TrySendInstantMessage(GridInstantMessage im, string previousLocation, bool firstTime, bool foreigner)
        {
            UUID toAgentID = new UUID(im.toAgentID);

            string url = null;
            if(firstTime)
            {
                if(!string.IsNullOrEmpty(previousLocation))
                    url = previousLocation;
                else 
                    m_UserLocationMap.TryGetValue(toAgentID, out url);
            }

            //m_log.DebugFormat("[XXX] Neeed lookup ? {0}", (lookupAgent ? "yes" : "no"));
            // Are we needing to look-up an agent?
            if (string.IsNullOrEmpty(url))
            {
                PresenceInfo[] presences = m_PresenceService.GetAgents(new string[] { toAgentID.ToString() });
                if (presences != null && presences.Length > 0)
                {
                    foreach (PresenceInfo p in presences)
                    {
                        if (!p.RegionID.IsZero())
                        {
                            //m_log.DebugFormat("[XXX]: Found presence in {0}", p.RegionID);
                            GridRegion reginfo = m_GridService.GetRegionByUUID(UUID.Zero, p.RegionID);
                            if (reginfo != null)
                            {
                                url = reginfo.ServerURI;
                                break;
                            }
                        }
                    }
                }

                if (!foreigner && string.IsNullOrEmpty(url) && m_UserAgentService != null)
                {
                    // Let's check with the UAS if the user is elsewhere
                    m_log.DebugFormat("[HG IM SERVICE]: User is not present. Checking location with User Agent service");
                    try
                    {
                        url = m_UserAgentService.LocateUser(toAgentID);
                    }
                    catch (Exception e)
                    {
                        m_log.Warn("[HG IM SERVICE]: LocateUser call failed ", e);
                        url = string.Empty;
                    }
                }

                // check if we've tried this before..
                // This is one way to end the recursive loop
                if (!firstTime && previousLocation.Equals(url))
                {
                    // m_log.Error("[GRID INSTANT MESSAGE]: Unable to deliver an instant message");
                    m_log.DebugFormat("[HG IM SERVICE]: Fail 2 {0} {1}", previousLocation, url);
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(url))
            {
                // ok, the user is around somewhere. Let's send back the reply with "success"
                // even though the IM may still fail. Just don't keep the caller waiting for
                // the entire time we're trying to deliver the IM
                return ForwardIMToGrid(url, im, toAgentID, foreigner);
            }

            m_log.DebugFormat("[HG IM SERVICE]: Unable to locate user {0}", toAgentID);
            return false;
        }

        bool ForwardIMToGrid(string url, GridInstantMessage im, UUID toAgentID, bool foreigner)
        {
            if (InstantMessageServiceConnector.SendInstantMessage(url, im, m_messageKey))
            {
                // IM delivery successful, so store the Agent's location in our local cache.
                m_UserLocationMap.AddOrUpdate(toAgentID, url, 30);
                return true;
            }
            else
            {
                // try again, but lookup user this time.
                m_UserLocationMap.Remove(toAgentID);
                // This is recursive!!!!!
                return TrySendInstantMessage(im, "", false, foreigner);
            }
        }

        private bool UndeliveredMessage(GridInstantMessage im)
        {
            if (m_OfflineIMService == null)
                return false;

            if (im.dialog != (byte)InstantMessageDialog.MessageFromObject &&
                im.dialog != (byte)InstantMessageDialog.MessageFromAgent &&
                im.dialog != (byte)InstantMessageDialog.GroupNotice &&
                im.dialog != (byte)InstantMessageDialog.GroupInvitation &&
                im.dialog != (byte)InstantMessageDialog.InventoryOffered)
            {
                return false;
            }

            if (!m_ForwardOfflineGroupMessages)
            {
                if (im.dialog == (byte)InstantMessageDialog.GroupNotice ||
                    im.dialog == (byte)InstantMessageDialog.GroupInvitation)
                    return false;
            }

//                m_log.DebugFormat("[HG IM SERVICE]: Message saved");
            string reason = string.Empty;
            return m_OfflineIMService.StoreMessage(im, out reason);
        }
    }
}
