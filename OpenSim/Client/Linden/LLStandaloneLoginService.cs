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
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Client.Linden
{
    public class LLStandaloneLoginService : LoginService
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


        public LLStandaloneLoginService(
            UserManagerBase userManager, string welcomeMess,
            IInterServiceInventoryServices interServiceInventoryService,
            NetworkServersInfo serversInfo,
            bool authenticate, LibraryRootFolder libraryRootFolder, ILoginServiceToRegionsConnector regionsConnector)
            : base(userManager, libraryRootFolder, welcomeMess)
        {
            this.m_serversInfo = serversInfo;
            m_defaultHomeX = this.m_serversInfo.DefaultHomeLocX;
            m_defaultHomeY = this.m_serversInfo.DefaultHomeLocY;
            m_authUsers = authenticate;

            m_interServiceInventoryService = interServiceInventoryService;
            m_regionsConnector = regionsConnector;
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
                            || profile.PasswordHash.Equals(password, StringComparison.InvariantCultureIgnoreCase));
                return loginresult;
            }
        }

        /// <summary>
        /// Customises the login response and fills in missing values.
        /// </summary>
        /// <param name="response">The existing response</param>
        /// <param name="theUser">The user profile</param>
        /// <param name="startLocationRequest">The requested start location</param>
        public override bool CustomiseResponse(LoginResponse response, UserProfileData theUser, string startLocationRequest)
        {
            // add active gestures to login-response
            AddActiveGestures(response, theUser);

            // HomeLocation
            RegionInfo homeInfo = null;

            // use the homeRegionID if it is stored already. If not, use the regionHandle as before
            UUID homeRegionId = theUser.HomeRegionID;
            ulong homeRegionHandle = theUser.HomeRegion;
            if (homeRegionId != UUID.Zero)
            {
                homeInfo = GetRegionInfo(homeRegionId);
            }
            else
            {
                homeInfo = GetRegionInfo(homeRegionHandle);
            }

            if (homeInfo != null)
            {
                response.Home =
                    string.Format(
                        "{{'region_handle':[r{0},r{1}], 'position':[r{2},r{3},r{4}], 'look_at':[r{5},r{6},r{7}]}}",
                        (homeInfo.RegionLocX * Constants.RegionSize),
                        (homeInfo.RegionLocY * Constants.RegionSize),
                        theUser.HomeLocation.X, theUser.HomeLocation.Y, theUser.HomeLocation.Z,
                        theUser.HomeLookAt.X, theUser.HomeLookAt.Y, theUser.HomeLookAt.Z);
            }
            else
            {
                m_log.InfoFormat("not found the region at {0} {1}", theUser.HomeRegionX, theUser.HomeRegionY);
                // Emergency mode: Home-region isn't available, so we can't request the region info.
                // Use the stored home regionHandle instead.
                // NOTE: If the home-region moves, this will be wrong until the users update their user-profile again
                ulong regionX = homeRegionHandle >> 32;
                ulong regionY = homeRegionHandle & 0xffffffff;
                response.Home =
                    string.Format(
                        "{{'region_handle':[r{0},r{1}], 'position':[r{2},r{3},r{4}], 'look_at':[r{5},r{6},r{7}]}}",
                        regionX, regionY,
                        theUser.HomeLocation.X, theUser.HomeLocation.Y, theUser.HomeLocation.Z,
                        theUser.HomeLookAt.X, theUser.HomeLookAt.Y, theUser.HomeLookAt.Z);

                m_log.InfoFormat("[LOGIN] Home region of user {0} {1} is not available; using computed region position {2} {3}",
                                 theUser.FirstName, theUser.SurName,
                                 regionX, regionY);
            }

            // StartLocation
            RegionInfo regionInfo = null;
            if (startLocationRequest == "home")
            {
                regionInfo = homeInfo;
                theUser.CurrentAgent.Position = theUser.HomeLocation;
                response.LookAt = "[r" + theUser.HomeLookAt.X.ToString() + ",r" + theUser.HomeLookAt.Y.ToString() + ",r" + theUser.HomeLookAt.Z.ToString() + "]";
            }
            else if (startLocationRequest == "last")
            {
                UUID lastRegion = theUser.CurrentAgent.Region;
                regionInfo = GetRegionInfo(lastRegion);
                response.LookAt = "[r" + theUser.CurrentAgent.LookAt.X.ToString() + ",r" + theUser.CurrentAgent.LookAt.Y.ToString() + ",r" + theUser.CurrentAgent.LookAt.Z.ToString() + "]";
            }
            else
            {
                Regex reURI = new Regex(@"^uri:(?<region>[^&]+)&(?<x>\d+)&(?<y>\d+)&(?<z>\d+)$");
                Match uriMatch = reURI.Match(startLocationRequest);
                if (uriMatch == null)
                {
                    m_log.InfoFormat("[LOGIN]: Got Custom Login URL {0}, but can't process it", startLocationRequest);
                }
                else
                {
                    string region = uriMatch.Groups["region"].ToString();
                    regionInfo = RequestClosestRegion(region);
                    if (regionInfo == null)
                    {
                        m_log.InfoFormat("[LOGIN]: Got Custom Login URL {0}, can't locate region {1}", startLocationRequest, region);
                    }
                    else
                    {
                        theUser.CurrentAgent.Position = new Vector3(float.Parse(uriMatch.Groups["x"].Value),
                            float.Parse(uriMatch.Groups["y"].Value), float.Parse(uriMatch.Groups["z"].Value));
                    }
                }
                response.LookAt = "[r0,r1,r0]";
                // can be: last, home, safe, url
                response.StartLocation = "url";
            }

            if ((regionInfo != null) && (PrepareLoginToRegion(regionInfo, theUser, response)))
            {
                return true;
            }

            // StartLocation not available, send him to a nearby region instead
            // regionInfo = m_gridService.RequestClosestRegion("");
            //m_log.InfoFormat("[LOGIN]: StartLocation not available sending to region {0}", regionInfo.regionName);

            // Send him to default region instead
            ulong defaultHandle = (((ulong)m_defaultHomeX * Constants.RegionSize) << 32) |
                                  ((ulong)m_defaultHomeY * Constants.RegionSize);

            if ((regionInfo != null) && (defaultHandle == regionInfo.RegionHandle))
            {
                m_log.ErrorFormat("[LOGIN]: Not trying the default region since this is the same as the selected region");
                return false;
            }

            m_log.Error("[LOGIN]: Sending user to default region " + defaultHandle + " instead");
            regionInfo = GetRegionInfo(defaultHandle);

            // Customise the response
            //response.Home =
            //    string.Format(
            //        "{{'region_handle':[r{0},r{1}], 'position':[r{2},r{3},r{4}], 'look_at':[r{5},r{6},r{7}]}}",
            //        (SimInfo.regionLocX * Constants.RegionSize),
            //        (SimInfo.regionLocY*Constants.RegionSize),
            //        theUser.HomeLocation.X, theUser.HomeLocation.Y, theUser.HomeLocation.Z,
            //        theUser.HomeLookAt.X, theUser.HomeLookAt.Y, theUser.HomeLookAt.Z);
            theUser.CurrentAgent.Position = new Vector3(128, 128, 0);
            response.StartLocation = "safe";

            return PrepareLoginToRegion(regionInfo, theUser, response);
        }

        protected RegionInfo RequestClosestRegion(string region)
        {
            return m_regionsConnector.RequestClosestRegion(region);
        }

        protected RegionInfo GetRegionInfo(ulong homeRegionHandle)
        {
            return m_regionsConnector.RequestNeighbourInfo(homeRegionHandle);
        }

        protected RegionInfo GetRegionInfo(UUID homeRegionId)
        {
            return m_regionsConnector.RequestNeighbourInfo(homeRegionId);
        }

        /// <summary>
        /// Add active gestures of the user to the login response.
        /// </summary>
        /// <param name="response">
        /// A <see cref="LoginResponse"/>
        /// </param>
        /// <param name="theUser">
        /// A <see cref="UserProfileData"/>
        /// </param>
        private void AddActiveGestures(LoginResponse response, UserProfileData theUser)
        {
            List<InventoryItemBase> gestures = m_interServiceInventoryService.GetActiveGestures(theUser.ID);
            //m_log.DebugFormat("[LOGIN]: AddActiveGestures, found {0}", gestures == null ? 0 : gestures.Count);
            ArrayList list = new ArrayList();
            if (gestures != null)
            {
                foreach (InventoryItemBase gesture in gestures)
                {
                    Hashtable item = new Hashtable();
                    item["item_id"] = gesture.ID.ToString();
                    item["asset_id"] = gesture.AssetID.ToString();
                    list.Add(item);
                }
            }
            response.ActiveGestures = list;
        }

        /// <summary>
        /// Prepare a login to the given region.  This involves both telling the region to expect a connection
        /// and appropriately customising the response to the user.
        /// </summary>
        /// <param name="sim"></param>
        /// <param name="user"></param>
        /// <param name="response"></param>
        /// <returns>true if the region was successfully contacted, false otherwise</returns>
        protected bool PrepareLoginToRegion(RegionInfo regionInfo, UserProfileData user, LoginResponse response)
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
                seedcap = "https://" + m_serversInfo.HttpSSLCN + ":" + m_serversInfo.httpSSLPort + capsSeedPath;
            }
            else
            {
                seedcap = "http://" + regionInfo.ExternalHostName + ":" + m_serversInfo.HttpListenerPort + capsSeedPath;
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
                m_log.WarnFormat("[INTER]: Appearance not found for {0} {1}. Creating default.", agent.firstname, agent.lastname);
                agent.Appearance = new AvatarAppearance();
            }

            if (m_regionsConnector.RegionLoginsEnabled)
            {
                // m_log.Info("[LLStandaloneLoginModule] Informing region about user");
                return m_regionsConnector.NewUserConnection(regionInfo.RegionHandle, agent);
            }

            return false;
        }

        // See LoginService
        protected override InventoryData GetInventorySkeleton(UUID userID)
        {
            List<InventoryFolderBase> folders = m_interServiceInventoryService.GetInventorySkeleton(userID);

            // If we have user auth but no inventory folders for some reason, create a new set of folders.
            if (null == folders || 0 == folders.Count)
            {
                m_interServiceInventoryService.CreateNewUserInventory(userID);
                folders = m_interServiceInventoryService.GetInventorySkeleton(userID);
            }

            UUID rootID = UUID.Zero;
            ArrayList AgentInventoryArray = new ArrayList();
            Hashtable TempHash;
            foreach (InventoryFolderBase InvFolder in folders)
            {
                if (InvFolder.ParentID == UUID.Zero)
                {
                    rootID = InvFolder.ID;
                }
                TempHash = new Hashtable();
                TempHash["name"] = InvFolder.Name;
                TempHash["parent_id"] = InvFolder.ParentID.ToString();
                TempHash["version"] = (Int32)InvFolder.Version;
                TempHash["type_default"] = (Int32)InvFolder.Type;
                TempHash["folder_id"] = InvFolder.ID.ToString();
                AgentInventoryArray.Add(TempHash);
            }

            return new InventoryData(AgentInventoryArray, rootID);
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
    }
}
