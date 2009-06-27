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
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers;

using OpenMetaverse;

using log4net;
using Nini.Config;
using Nwc.XmlRpc;

namespace OpenSim.Framework.Communications.Services
{
    public class HGLoginAuthService : LoginService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected NetworkServersInfo m_serversInfo;
        protected bool m_authUsers = false;

        /// <summary>
        /// Used by the login service to make requests to the inventory service.
        /// </summary>
        protected IInterServiceInventoryServices m_interServiceInventoryService;

        /// <summary>
        /// Used to make requests to the local regions.
        /// </summary>
        protected ILoginServiceToRegionsConnector m_regionsConnector;

        public HGLoginAuthService(
            UserManagerBase userManager, string welcomeMess,
            IInterServiceInventoryServices interServiceInventoryService,
            NetworkServersInfo serversInfo,
            bool authenticate, LibraryRootFolder libraryRootFolder, ILoginServiceToRegionsConnector regionsConnector)
            : base(userManager, libraryRootFolder, welcomeMess)
        {
            this.m_serversInfo = serversInfo;
            if (m_serversInfo != null)
            {
                m_defaultHomeX = this.m_serversInfo.DefaultHomeLocX;
                m_defaultHomeY = this.m_serversInfo.DefaultHomeLocY;
            }
            m_authUsers = authenticate;

            m_interServiceInventoryService = interServiceInventoryService;
            m_regionsConnector = regionsConnector;
            m_interInventoryService = interServiceInventoryService;
        }

        public void SetServersInfo(NetworkServersInfo sinfo)
        {
            m_serversInfo = sinfo;
        }

        public override XmlRpcResponse XmlRpcLoginMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            m_log.Info("[HGLOGIN]: HGLogin called " + request.MethodName);
            XmlRpcResponse response = base.XmlRpcLoginMethod(request, remoteClient);
            Hashtable responseData = (Hashtable)response.Value;

            responseData["grid_service"] = m_serversInfo.GridURL;
            responseData["grid_service_send_key"] = m_serversInfo.GridSendKey;
            responseData["inventory_service"] = m_serversInfo.InventoryURL;
            responseData["asset_service"] = m_serversInfo.AssetURL;
            responseData["asset_service_send_key"] = m_serversInfo.AssetSendKey;
            int x = (Int32)responseData["region_x"];
            int y = (Int32)responseData["region_y"];
            uint ux = (uint)(x / Constants.RegionSize);
            uint uy = (uint)(y / Constants.RegionSize);
            ulong regionHandle = Util.UIntsToLong(ux, uy);
            responseData["region_handle"] = regionHandle.ToString();

            // Let's remove the seed cap from the login
            //responseData.Remove("seed_capability");

            // Let's add the appearance
            UUID userID = UUID.Zero;
            UUID.TryParse((string)responseData["agent_id"], out userID);
            AvatarAppearance appearance = m_userManager.GetUserAppearance(userID);
            if (appearance == null)
            {
                m_log.WarnFormat("[INTER]: Appearance not found for {0}. Creating default.", userID);
                appearance = new AvatarAppearance();
            }

            responseData["appearance"] = appearance.ToHashTable();

            // Let's also send the auth token
            UUID token = UUID.Random();
            responseData["auth_token"] = token.ToString();
            UserProfileData userProfile = m_userManager.GetUserProfile(userID);
            if (userProfile != null)
            {
                userProfile.WebLoginKey = token;
                m_userManager.CommitAgent(ref userProfile);
            }
            m_log.Warn("[HGLOGIN]: Auth token: " + token);


            return response;
        }

        public XmlRpcResponse XmlRpcGenerateKeyMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Verify the key of who's calling
            UUID userID = UUID.Zero;
            UUID authKey = UUID.Zero; 
            UUID.TryParse((string)request.Params[0], out userID);
            UUID.TryParse((string)request.Params[1], out authKey);

            m_log.InfoFormat("[HGLOGIN] HGGenerateKey called with authToken ", authKey);
            string newKey = string.Empty;

            if (!(m_userManager is IAuthentication))
            {
                m_log.Debug("[HGLOGIN]: UserManager is not IAuthentication service. Returning empty key.");
            }
            else
            {
                newKey = ((IAuthentication)m_userManager).GetNewKey(m_serversInfo.UserURL, userID, authKey);
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = (string) newKey;
            return response;
        }

        public XmlRpcResponse XmlRpcVerifyKeyMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            bool success = false;

            if (request.Params.Count >= 2)
            {
                // Verify the key of who's calling
                UUID userID = UUID.Zero;
                string authKey = string.Empty;
                if (UUID.TryParse((string)request.Params[0], out userID))
                {
                    authKey = (string)request.Params[1];

                    m_log.InfoFormat("[HGLOGIN] HGVerifyKey called with key {0}", authKey);

                    if (!(m_userManager is IAuthentication))
                    {
                        m_log.Debug("[HGLOGIN]: UserManager is not IAuthentication service. Denying.");
                    }
                    else
                    {
                        success = ((IAuthentication)m_userManager).VerifyKey(userID, authKey);
                    }
                }
            }

            m_log.DebugFormat("[HGLOGIN]: Response to VerifyKey is {0}", success);
            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = success;
            return response;
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

        /// <summary>
        /// Not really informing the region. Just filling out the response fields related to the region. 
        /// </summary>
        /// <param name="sim"></param>
        /// <param name="user"></param>
        /// <param name="response"></param>
        /// <returns>true if the region was successfully contacted, false otherwise</returns>
        protected override bool PrepareLoginToRegion(RegionInfo regionInfo, UserProfileData user, LoginResponse response, IPEndPoint remoteClient)
        {
            IPEndPoint endPoint = regionInfo.ExternalEndPoint;
            response.SimAddress = endPoint.Address.ToString();
            response.SimPort = (uint)endPoint.Port;
            response.RegionX = regionInfo.RegionLocX;
            response.RegionY = regionInfo.RegionLocY;
            response.SimHttpPort = regionInfo.HttpPort;

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

            return true;
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

            m_regionsConnector.LogOffUserFromGrid(SimInfo.RegionHandle, theUser.ID, theUser.CurrentAgent.SecureSessionID, "Logging you off");
        }

        protected override bool AllowLoginWithoutInventory()
        {
            return true;
        }

    }
}
