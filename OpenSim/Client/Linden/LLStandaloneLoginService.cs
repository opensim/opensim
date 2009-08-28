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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Services;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;

namespace OpenSim.Client.Linden
{
    public class LLStandaloneLoginService : LoginService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected NetworkServersInfo m_serversInfo;
        protected bool m_authUsers = false;

        /// <summary>
        /// Used to make requests to the local regions.
        /// </summary>
        protected ILoginServiceToRegionsConnector m_regionsConnector;

        public LLStandaloneLoginService(
            UserManagerBase userManager, string welcomeMess,
            IInventoryService interServiceInventoryService,
            NetworkServersInfo serversInfo,
            bool authenticate, LibraryRootFolder libraryRootFolder, ILoginServiceToRegionsConnector regionsConnector)
            : base(userManager, libraryRootFolder, welcomeMess)
        {
            this.m_serversInfo = serversInfo;
            m_defaultHomeX = this.m_serversInfo.DefaultHomeLocX;
            m_defaultHomeY = this.m_serversInfo.DefaultHomeLocY;
            m_authUsers = authenticate;

            m_InventoryService = interServiceInventoryService;
            m_regionsConnector = regionsConnector;
            // Standard behavior: In StandAlone, silent logout of last hung session
            m_warn_already_logged = false;
        }

        public override UserProfileData GetTheUser(string firstname, string lastname)
        {
            UserProfileData profile = m_userManager.GetUserProfile(firstname, lastname);
            if (profile != null)
            {
                return profile;
            }

            if (!m_authUsers)
            {
                //no current user account so make one
                m_log.Info("[LOGIN]: No user account found so creating a new one.");

                m_userManager.AddUser(firstname, lastname, "test", "", m_defaultHomeX, m_defaultHomeY);

                return m_userManager.GetUserProfile(firstname, lastname);
            }

            return null;
        }

        public override bool AuthenticateUser(UserProfileData profile, string password)
        {
            if (!m_authUsers)
            {
                //for now we will accept any password in sandbox mode
                m_log.Info("[LOGIN]: Authorising user (no actual password check)");

                return true;
            }
            else
            {
                m_log.Info(
                    "[LOGIN]: Authenticating " + profile.FirstName + " " + profile.SurName);

                if (!password.StartsWith("$1$"))
                    password = "$1$" + Util.Md5Hash(password);

                password = password.Remove(0, 3); //remove $1$

                string s = Util.Md5Hash(password + ":" + profile.PasswordSalt);

                bool loginresult = (profile.PasswordHash.Equals(s.ToString(), StringComparison.InvariantCultureIgnoreCase)
                            || profile.PasswordHash.Equals(password, StringComparison.InvariantCulture));
                return loginresult;
            }
        }

        protected override RegionInfo RequestClosestRegion(string region)
        {
            return m_regionsConnector.RequestClosestRegion(region);
        }

        protected override RegionInfo GetRegionInfo(ulong homeRegionHandle)
        {
            return m_regionsConnector.RequestNeighbourInfo(homeRegionHandle);
        }

        protected override RegionInfo GetRegionInfo(UUID homeRegionId)
        {
            return m_regionsConnector.RequestNeighbourInfo(homeRegionId);
        }

        protected override bool PrepareLoginToRegion(
            RegionInfo regionInfo, UserProfileData user, LoginResponse response, IPEndPoint remoteClient)
        {
            IPEndPoint endPoint = regionInfo.ExternalEndPoint;
            response.SimAddress = endPoint.Address.ToString();
            response.SimPort = (uint)endPoint.Port;
            response.RegionX = regionInfo.RegionLocX;
            response.RegionY = regionInfo.RegionLocY;

            string capsPath = CapsUtil.GetRandomCapsObjectPath();
            string capsSeedPath = CapsUtil.GetCapsSeedPath(capsPath);

            // Don't use the following!  It Fails for logging into any region not on the same port as the http server!
            // Kept here so it doesn't happen again!
            // response.SeedCapability = regionInfo.ServerURI + capsSeedPath;

            string seedcap = "http://";

            if (m_serversInfo.HttpUsesSSL)
            {
                // For NAT
                string host = NetworkUtil.GetHostFor(remoteClient.Address, m_serversInfo.HttpSSLCN);

                seedcap = "https://" + host + ":" + m_serversInfo.httpSSLPort + capsSeedPath;
            }
            else
            {
                // For NAT
                string host = NetworkUtil.GetHostFor(remoteClient.Address, regionInfo.ExternalHostName);

                seedcap = "http://" + host + ":" + m_serversInfo.HttpListenerPort + capsSeedPath;
            }

            response.SeedCapability = seedcap;

            // Notify the target of an incoming user
            m_log.InfoFormat(
                "[LOGIN]: Telling {0} @ {1},{2} ({3}) to prepare for client connection",
                regionInfo.RegionName, response.RegionX, response.RegionY, regionInfo.ServerURI);

            // Update agent with target sim
            user.CurrentAgent.Region = regionInfo.RegionID;
            user.CurrentAgent.Handle = regionInfo.RegionHandle;

            AgentCircuitData agent = new AgentCircuitData();
            agent.AgentID = user.ID;
            agent.firstname = user.FirstName;
            agent.lastname = user.SurName;
            agent.SessionID = user.CurrentAgent.SessionID;
            agent.SecureSessionID = user.CurrentAgent.SecureSessionID;
            agent.circuitcode = Convert.ToUInt32(response.CircuitCode);
            agent.BaseFolder = UUID.Zero;
            agent.InventoryFolder = UUID.Zero;
            agent.startpos = user.CurrentAgent.Position;
            agent.CapsPath = capsPath;
            agent.Appearance = m_userManager.GetUserAppearance(user.ID);
            if (agent.Appearance == null)
            {
                m_log.WarnFormat(
                    "[INTER]: Appearance not found for {0} {1}. Creating default.", agent.firstname, agent.lastname);
                agent.Appearance = new AvatarAppearance(agent.AgentID);
            }

            if (m_regionsConnector.RegionLoginsEnabled)
            {
                string reason;
                bool success = m_regionsConnector.NewUserConnection(regionInfo.RegionHandle, agent, out reason);
                if (!success)
                {
                    response.ErrorReason = "key";
                    response.ErrorMessage = reason;
                }
                return success;
                // return m_regionsConnector.NewUserConnection(regionInfo.RegionHandle, agent, out reason);
            }

            return false;
        }

        public override void LogOffUser(UserProfileData theUser, string message)
        {
            RegionInfo SimInfo;
            try
            {
                SimInfo = this.m_regionsConnector.RequestNeighbourInfo(theUser.CurrentAgent.Handle);

                if (SimInfo == null)
                {
                    m_log.Error("[LOCAL LOGIN]: Region user was in isn't currently logged in");
                    return;
                }
            }
            catch (Exception)
            {
                m_log.Error("[LOCAL LOGIN]: Unable to look up region to log user off");
                return;
            }

            m_regionsConnector.LogOffUserFromGrid(
                SimInfo.RegionHandle, theUser.ID, theUser.CurrentAgent.SecureSessionID, "Logging you off");
        }
    }
}
