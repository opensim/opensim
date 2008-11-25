/**
 * Copyright (c) 2008, Contributors. All rights reserved.
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 *     * Redistributions of source code must retain the above copyright notice, 
 *       this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright notice, 
 *       this list of conditions and the following disclaimer in the documentation 
 *       and/or other materials provided with the distribution.
 *     * Neither the name of the Organizations nor the names of Individual
 *       Contributors may be used to endorse or promote products derived from 
 *       this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES 
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL 
 * THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, 
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED 
 * AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED 
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 */

using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

using log4net;
using Nini.Config;

using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Grid.AssetServer;

namespace OpenSim.Region.Environment.Modules.Hypergrid
{
    public class HGStandaloneAssetService : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool initialized = false;
        private static bool enabled = false;
        
        Scene m_scene;
        AssetService m_assetService;
        
        #region IRegionModule interface

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (!initialized)
            {
                initialized = true;
                m_scene = scene;

                // This module is only on for standalones in hypergrid mode
                enabled = !config.Configs["Startup"].GetBoolean("gridmode", true) && config.Configs["Startup"].GetBoolean("hypergrid", false);
            }
        }

        public void PostInitialise()
        {
            if (enabled)
            {
                m_log.Info("[HGStandaloneAssetService]: Starting...");

                m_assetService = new AssetService(m_scene);
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "HGStandaloneAssetService"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

    }

    public class AssetService 
    {
        private IUserService m_userService;
        private bool m_doLookup = false;

        public bool DoLookup
        {
            get { return m_doLookup; }
            set { m_doLookup = value; }
        }
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public AssetService(Scene m_scene)
        {
            AddHttpHandlers(m_scene);
            m_userService = m_scene.CommsManager.UserService;
        }

        protected void AddHttpHandlers(Scene m_scene)
        {
            IAssetProviderPlugin m_assetProvider = ((AssetServerBase)m_scene.AssetCache.AssetServer).AssetProviderPlugin;

            m_scene.AddStreamHandler(new GetAssetStreamHandler(m_assetProvider));
            m_scene.AddStreamHandler(new PostAssetStreamHandler(m_assetProvider));

        }


        ///// <summary>
        ///// Check that the source of an inventory request is one that we trust.
        ///// </summary>
        ///// <param name="peer"></param>
        ///// <returns></returns>
        //public bool CheckTrustSource(IPEndPoint peer)
        //{
        //    if (m_doLookup)
        //    {
        //        m_log.InfoFormat("[GRID AGENT INVENTORY]: Checking trusted source {0}", peer);
        //        UriBuilder ub = new UriBuilder(m_userserver_url);
        //        IPAddress[] uaddrs = Dns.GetHostAddresses(ub.Host);
        //        foreach (IPAddress uaddr in uaddrs)
        //        {
        //            if (uaddr.Equals(peer.Address))
        //            {
        //                return true;
        //            }
        //        }

        //        m_log.WarnFormat(
        //            "[GRID AGENT INVENTORY]: Rejecting request since source {0} was not in the list of trusted sources",
        //            peer);

        //        return false;
        //    }
        //    else
        //    {
        //        return true;
        //    }
        //}

        /// <summary>
        /// Check that the source of an inventory request for a particular agent is a current session belonging to
        /// that agent.
        /// </summary>
        /// <param name="session_id"></param>
        /// <param name="avatar_id"></param>
        /// <returns></returns>
        public bool CheckAuthSession(string session_id, string avatar_id)
        {
            if (m_doLookup)
            {
                m_log.InfoFormat("[HGStandaloneInvService]: checking authed session {0} {1}", session_id, avatar_id);
                UUID userID = UUID.Zero;
                UUID sessionID = UUID.Zero;
                UUID.TryParse(avatar_id, out userID);
                UUID.TryParse(session_id, out sessionID);
                if (userID.Equals(UUID.Zero) || sessionID.Equals(UUID.Zero))
                {
                    m_log.Info("[HGStandaloneInvService]: Invalid user or session id " + avatar_id + "; " + session_id);
                    return false;
                }
                UserProfileData userProfile = m_userService.GetUserProfile(userID);
                if (userProfile != null && userProfile.CurrentAgent != null &&
                    userProfile.CurrentAgent.SessionID == sessionID)
                {
                    m_log.Info("[HGStandaloneInvService]: user is logged in and session is valid. Authorizing access.");
                    return true;
                }

                m_log.Warn("[HGStandaloneInvService]: unknown user or session_id, request rejected");
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}
