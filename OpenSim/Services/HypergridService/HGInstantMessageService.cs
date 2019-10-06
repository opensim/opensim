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
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private const double CACHE_EXPIRATION_SECONDS = 120000.0; // 33 hours

        static bool m_Initialized = false;

        protected static IGridService m_GridService;
        protected static IPresenceService m_PresenceService;
        protected static IUserAgentService m_UserAgentService;
        protected static IOfflineIMService m_OfflineIMService;

        protected static IInstantMessageSimConnector m_IMSimConnector;

        protected static Dictionary<UUID, object> m_UserLocationMap = new Dictionary<UUID, object>();
        private static ExpiringCache<UUID, GridRegion> m_RegionCache;

        private static bool m_ForwardOfflineGroupMessages;
        private static bool m_InGatekeeper;

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
                    throw new Exception(String.Format("No section HGInstantMessageService in config file"));

                string gridService = serverConfig.GetString("GridService", String.Empty);
                string presenceService = serverConfig.GetString("PresenceService", String.Empty);
                string userAgentService = serverConfig.GetString("UserAgentService", String.Empty);
                m_InGatekeeper = serverConfig.GetBoolean("InGatekeeper", false);
                m_log.DebugFormat("[HG IM SERVICE]: Starting... InRobust? {0}", m_InGatekeeper);

                if (gridService == string.Empty || presenceService == string.Empty)
                    throw new Exception(String.Format("Incomplete specifications, InstantMessage Service cannot function."));

                Object[] args = new Object[] { config };
                m_GridService = ServerUtils.LoadPlugin<IGridService>(gridService, args);
                m_PresenceService = ServerUtils.LoadPlugin<IPresenceService>(presenceService, args);
                try
                {
                    m_UserAgentService = ServerUtils.LoadPlugin<IUserAgentService>(userAgentService, args);
                }
                catch
                {
                    m_log.WarnFormat("[HG IM SERVICE]: Unable to create User Agent Service. Missing config var  in [HGInstantMessageService]?");
                }

                m_RegionCache = new ExpiringCache<UUID, GridRegion>();

                IConfig cnf = config.Configs["Messaging"];
                if (cnf == null)
                {
                    return;
                }

                m_messageKey = cnf.GetString("MessageKey", String.Empty);
                m_ForwardOfflineGroupMessages = cnf.GetBoolean("ForwardOfflineGroupMessages", false);

                if (m_InGatekeeper)
                {
                    string offlineIMService = cnf.GetString("OfflineIMService", string.Empty);
                    if (offlineIMService != string.Empty)
                        m_OfflineIMService = ServerUtils.LoadPlugin<IOfflineIMService>(offlineIMService, args);
                }
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
            if (url != string.Empty)
                return TrySendInstantMessage(im, url, true, foreigner);
            else
            {
                PresenceInfo upd = new PresenceInfo();
                upd.RegionID = UUID.Zero;
                return TrySendInstantMessage(im, upd, true, foreigner);
            }

        }

        protected bool TrySendInstantMessage(GridInstantMessage im, object previousLocation, bool firstTime, bool foreigner)
        {
            UUID toAgentID = new UUID(im.toAgentID);

            PresenceInfo upd = null;
            string url = string.Empty;

            bool lookupAgent = false;

            lock (m_UserLocationMap)
            {
                if (m_UserLocationMap.ContainsKey(toAgentID))
                {
                    object o = m_UserLocationMap[toAgentID];
                    if (o is PresenceInfo)
                        upd = (PresenceInfo)o;
                    else if (o is string)
                        url = (string)o;

                    // We need to compare the current location with the previous
                    // or the recursive loop will never end because it will never try to lookup the agent again
                    if (!firstTime)
                    {
                        lookupAgent = true;
                        upd = null;
                    }
                }
                else
                {
                    lookupAgent = true;
                }
            }

            //m_log.DebugFormat("[XXX] Neeed lookup ? {0}", (lookupAgent ? "yes" : "no"));

            // Are we needing to look-up an agent?
            if (lookupAgent)
            {
                // Non-cached user agent lookup.
                PresenceInfo[] presences = m_PresenceService.GetAgents(new string[] { toAgentID.ToString() });
                if (presences != null && presences.Length > 0)
                {
                    foreach (PresenceInfo p in presences)
                    {
                        if (p.RegionID != UUID.Zero)
                        {
                            //m_log.DebugFormat("[XXX]: Found presence in {0}", p.RegionID);
                            upd = p;
                            break;
                        }
                    }
                }

                if (upd == null && !foreigner)
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
                //
                if (!firstTime && ((previousLocation is PresenceInfo && upd != null && upd.RegionID == ((PresenceInfo)previousLocation).RegionID) ||
                                    (previousLocation is string && upd == null && previousLocation.Equals(url))))
                {
                    // m_log.Error("[GRID INSTANT MESSAGE]: Unable to deliver an instant message");
                    m_log.DebugFormat("[HG IM SERVICE]: Fail 2 {0} {1}", previousLocation, url);

                    return false;
                }
            }

            if (upd != null)
            {
                // ok, the user is around somewhere. Let's send back the reply with "success"
                // even though the IM may still fail. Just don't keep the caller waiting for
                // the entire time we're trying to deliver the IM
                return SendIMToRegion(upd, im, toAgentID, foreigner);
            }
            else if (url != string.Empty)
            {
                // ok, the user is around somewhere. Let's send back the reply with "success"
                // even though the IM may still fail. Just don't keep the caller waiting for
                // the entire time we're trying to deliver the IM
                return ForwardIMToGrid(url, im, toAgentID, foreigner);
            }
            else if (firstTime && previousLocation is string && (string)previousLocation != string.Empty)
            {
                return ForwardIMToGrid((string)previousLocation, im, toAgentID, foreigner);
            }
            else
                m_log.DebugFormat("[HG IM SERVICE]: Unable to locate user {0}", toAgentID);
            return false;
        }

        bool SendIMToRegion(PresenceInfo upd, GridInstantMessage im, UUID toAgentID, bool foreigner)
        {
            bool imresult = false;
            GridRegion reginfo = null;
            if (!m_RegionCache.TryGetValue(upd.RegionID, out reginfo))
            {
                reginfo = m_GridService.GetRegionByUUID(UUID.Zero /*!!!*/, upd.RegionID);
                if (reginfo != null)
                    m_RegionCache.AddOrUpdate(upd.RegionID, reginfo, CACHE_EXPIRATION_SECONDS);
            }

            if (reginfo != null)
            {
                imresult = InstantMessageServiceConnector.SendInstantMessage(reginfo.ServerURI, im, m_messageKey);
            }
            else
            {
                m_log.DebugFormat("[HG IM SERVICE]: Failed to deliver message to {0}", reginfo.ServerURI);
                return false;
            }

            if (imresult)
            {
                // IM delivery successful, so store the Agent's location in our local cache.
                lock (m_UserLocationMap)
                {
                    if (m_UserLocationMap.ContainsKey(toAgentID))
                    {
                        m_UserLocationMap[toAgentID] = upd;
                    }
                    else
                    {
                        m_UserLocationMap.Add(toAgentID, upd);
                    }
                }
                return true;
            }
            else
            {
                // try again, but lookup user this time.
                // Warning, this must call the Async version
                // of this method or we'll be making thousands of threads
                // The version within the spawned thread is SendGridInstantMessageViaXMLRPCAsync
                // The version that spawns the thread is SendGridInstantMessageViaXMLRPC

                // This is recursive!!!!!
                return TrySendInstantMessage(im, upd, false, foreigner);
            }
        }

        bool ForwardIMToGrid(string url, GridInstantMessage im, UUID toAgentID, bool foreigner)
        {
            if (InstantMessageServiceConnector.SendInstantMessage(url, im))
            {
                // IM delivery successful, so store the Agent's location in our local cache.
                lock (m_UserLocationMap)
                {
                    if (m_UserLocationMap.ContainsKey(toAgentID))
                    {
                        m_UserLocationMap[toAgentID] = url;
                    }
                    else
                    {
                        m_UserLocationMap.Add(toAgentID, url);
                    }
                }

                return true;
            }
            else
            {
                // try again, but lookup user this time.

                // This is recursive!!!!!
                return TrySendInstantMessage(im, url, false, foreigner);
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
