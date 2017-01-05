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
using System.Text.RegularExpressions;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Server.Base;
using OpenSim.Services.Connectors.Hypergrid;

using OpenMetaverse;

using Nini.Config;
using log4net;

namespace OpenSim.Services.HypergridService
{
    public class GatekeeperService : IGatekeeperService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private static bool m_Initialized = false;

        private static IGridService m_GridService;
        private static IPresenceService m_PresenceService;
        private static IUserAccountService m_UserAccountService;
        private static IUserAgentService m_UserAgentService;
        private static ISimulationService m_SimulationService;
        private static IGridUserService m_GridUserService;
        private static IBansService m_BansService;

        private static string m_AllowedClients = string.Empty;
        private static string m_DeniedClients = string.Empty;
        private static bool m_ForeignAgentsAllowed = true;
        private static List<string> m_ForeignsAllowedExceptions = new List<string>();
        private static List<string> m_ForeignsDisallowedExceptions = new List<string>();

        private static UUID m_ScopeID;
        private static bool m_AllowTeleportsToAnyRegion;
        private static string m_ExternalName;
        private static Uri m_Uri;
        private static GridRegion m_DefaultGatewayRegion;

        public GatekeeperService(IConfigSource config, ISimulationService simService)
        {
            if (!m_Initialized)
            {
                m_Initialized = true;

                IConfig serverConfig = config.Configs["GatekeeperService"];
                if (serverConfig == null)
                    throw new Exception(String.Format("No section GatekeeperService in config file"));

                string accountService = serverConfig.GetString("UserAccountService", String.Empty);
                string homeUsersService = serverConfig.GetString("UserAgentService", string.Empty);
                string gridService = serverConfig.GetString("GridService", String.Empty);
                string presenceService = serverConfig.GetString("PresenceService", String.Empty);
                string simulationService = serverConfig.GetString("SimulationService", String.Empty);
                string gridUserService = serverConfig.GetString("GridUserService", String.Empty);
                string bansService = serverConfig.GetString("BansService", String.Empty);

                // These are mandatory, the others aren't
                if (gridService == string.Empty || presenceService == string.Empty)
                    throw new Exception("Incomplete specifications, Gatekeeper Service cannot function.");

                string scope = serverConfig.GetString("ScopeID", UUID.Zero.ToString());
                UUID.TryParse(scope, out m_ScopeID);
                //m_WelcomeMessage = serverConfig.GetString("WelcomeMessage", "Welcome to OpenSim!");
                m_AllowTeleportsToAnyRegion = serverConfig.GetBoolean("AllowTeleportsToAnyRegion", true);
                m_ExternalName = Util.GetConfigVarFromSections<string>(config, "GatekeeperURI",
                    new string[] { "Startup", "Hypergrid", "GatekeeperService" }, String.Empty);
                m_ExternalName = serverConfig.GetString("ExternalName", m_ExternalName);
                if (m_ExternalName != string.Empty && !m_ExternalName.EndsWith("/"))
                    m_ExternalName = m_ExternalName + "/";

                try
                {
                    m_Uri = new Uri(m_ExternalName);
                }
                catch
                {
                    m_log.WarnFormat("[GATEKEEPER SERVICE]: Malformed gatekeeper address {0}", m_ExternalName);
                }

                Object[] args = new Object[] { config };
                m_GridService = ServerUtils.LoadPlugin<IGridService>(gridService, args);
                m_PresenceService = ServerUtils.LoadPlugin<IPresenceService>(presenceService, args);

                if (accountService != string.Empty)
                    m_UserAccountService = ServerUtils.LoadPlugin<IUserAccountService>(accountService, args);
                if (homeUsersService != string.Empty)
                    m_UserAgentService = ServerUtils.LoadPlugin<IUserAgentService>(homeUsersService, args);
                if (gridUserService != string.Empty)
                    m_GridUserService = ServerUtils.LoadPlugin<IGridUserService>(gridUserService, args);
                if (bansService != string.Empty)
                    m_BansService = ServerUtils.LoadPlugin<IBansService>(bansService, args);

                if (simService != null)
                    m_SimulationService = simService;
                else if (simulationService != string.Empty)
                        m_SimulationService = ServerUtils.LoadPlugin<ISimulationService>(simulationService, args);

                string[] possibleAccessControlConfigSections = new string[] { "AccessControl", "GatekeeperService" };
                m_AllowedClients = Util.GetConfigVarFromSections<string>(
                        config, "AllowedClients", possibleAccessControlConfigSections, string.Empty);
                m_DeniedClients = Util.GetConfigVarFromSections<string>(
                        config, "DeniedClients", possibleAccessControlConfigSections, string.Empty);
                m_ForeignAgentsAllowed = serverConfig.GetBoolean("ForeignAgentsAllowed", true);

                LoadDomainExceptionsFromConfig(serverConfig, "AllowExcept", m_ForeignsAllowedExceptions);
                LoadDomainExceptionsFromConfig(serverConfig, "DisallowExcept", m_ForeignsDisallowedExceptions);

                if (m_GridService == null || m_PresenceService == null || m_SimulationService == null)
                    throw new Exception("Unable to load a required plugin, Gatekeeper Service cannot function.");

                m_log.Debug("[GATEKEEPER SERVICE]: Starting...");
            }
        }

        public GatekeeperService(IConfigSource config)
            : this(config, null)
        {
        }

        protected void LoadDomainExceptionsFromConfig(IConfig config, string variable, List<string> exceptions)
        {
            string value = config.GetString(variable, string.Empty);
            string[] parts = value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string s in parts)
                exceptions.Add(s.Trim());
        }

        public bool LinkRegion(string regionName, out UUID regionID, out ulong regionHandle, out string externalName, out string imageURL, out string reason, out int sizeX, out int sizeY)
        {
            regionID = UUID.Zero;
            regionHandle = 0;
            sizeX = (int)Constants.RegionSize;
            sizeY = (int)Constants.RegionSize;
            externalName = m_ExternalName + ((regionName != string.Empty) ? " " + regionName : "");
            imageURL = string.Empty;
            reason = string.Empty;
            GridRegion region = null;

            m_log.DebugFormat("[GATEKEEPER SERVICE]: Request to link to {0}", (regionName == string.Empty)? "default region" : regionName);
            if (!m_AllowTeleportsToAnyRegion || regionName == string.Empty)
            {
                List<GridRegion> defs = m_GridService.GetDefaultHypergridRegions(m_ScopeID);
                if (defs != null && defs.Count > 0)
                {
                    region = defs[0];
                    m_DefaultGatewayRegion = region;
                }
                else
                {
                    reason = "Grid setup problem. Try specifying a particular region here.";
                    m_log.DebugFormat("[GATEKEEPER SERVICE]: Unable to send information. Please specify a default region for this grid!");
                    return false;
                }
            }
            else
            {
                region = m_GridService.GetRegionByName(m_ScopeID, regionName);
                if (region == null)
                {
                    reason = "Region not found";
                    return false;
                }
            }

            regionID = region.RegionID;
            regionHandle = region.RegionHandle;
            sizeX = region.RegionSizeX;
            sizeY = region.RegionSizeY;

            string regionimage = "regionImage" + regionID.ToString();
            regionimage = regionimage.Replace("-", "");
            imageURL = region.ServerURI + "index.php?method=" + regionimage;

            return true;
        }

        public GridRegion GetHyperlinkRegion(UUID regionID, UUID agentID, string agentHomeURI, out string message)
        {
            message = null;

            if (!m_AllowTeleportsToAnyRegion)
            {
                // Don't even check the given regionID
                m_log.DebugFormat(
                    "[GATEKEEPER SERVICE]: Returning gateway region {0} {1} @ {2} to user {3}{4} as teleporting to arbitrary regions is not allowed.",
                    m_DefaultGatewayRegion.RegionName,
                    m_DefaultGatewayRegion.RegionID,
                    m_DefaultGatewayRegion.ServerURI,
                    agentID,
                    agentHomeURI == null ? "" : " @ " + agentHomeURI);

                message = "Teleporting to the default region.";
                return m_DefaultGatewayRegion;
            }

            GridRegion region = m_GridService.GetRegionByUUID(m_ScopeID, regionID);

            if (region == null)
            {
                m_log.DebugFormat(
                    "[GATEKEEPER SERVICE]: Could not find region with ID {0} as requested by user {1}{2}.  Returning null.",
                    regionID, agentID, (agentHomeURI == null) ? "" : " @ " + agentHomeURI);

                message = "The teleport destination could not be found.";
                return null;
            }

            m_log.DebugFormat(
                "[GATEKEEPER SERVICE]: Returning region {0} {1} @ {2} to user {3}{4}.",
                region.RegionName,
                region.RegionID,
                region.ServerURI,
                agentID,
                agentHomeURI == null ? "" : " @ " + agentHomeURI);

            return region;
        }

        #region Login Agent
        public bool LoginAgent(GridRegion source, AgentCircuitData aCircuit, GridRegion destination, out string reason)
        {
            reason = string.Empty;

            string authURL = string.Empty;
            if (aCircuit.ServiceURLs.ContainsKey("HomeURI"))
                authURL = aCircuit.ServiceURLs["HomeURI"].ToString();

            m_log.InfoFormat("[GATEKEEPER SERVICE]: Login request for {0} {1} @ {2} ({3}) at {4} using viewer {5}, channel {6}, IP {7}, Mac {8}, Id0 {9}, Teleport Flags: {10}. From region {11}",
                aCircuit.firstname, aCircuit.lastname, authURL, aCircuit.AgentID, destination.RegionID,
                aCircuit.Viewer, aCircuit.Channel, aCircuit.IPAddress, aCircuit.Mac, aCircuit.Id0, (TeleportFlags)aCircuit.teleportFlags,
                (source == null) ? "Unknown" : string.Format("{0} ({1}){2}", source.RegionName, source.RegionID, (source.RawServerURI == null) ? "" : " @ " + source.ServerURI));

            string curViewer = Util.GetViewerName(aCircuit);

            //
            // Check client
            //
            if (m_AllowedClients != string.Empty)
            {
                Regex arx = new Regex(m_AllowedClients);
                Match am = arx.Match(curViewer);

                if (!am.Success)
                {
                    reason = "Login failed: client " + curViewer + " is not allowed";
                    m_log.InfoFormat("[GATEKEEPER SERVICE]: Login failed, reason: client {0} is not allowed", curViewer);
                    return false;
                }
            }

            if (m_DeniedClients != string.Empty)
            {
                Regex drx = new Regex(m_DeniedClients);
                Match dm = drx.Match(curViewer);

                if (dm.Success)
                {
                    reason = "Login failed: client " + curViewer + " is denied";
                    m_log.InfoFormat("[GATEKEEPER SERVICE]: Login failed, reason: client {0} is denied", curViewer);
                    return false;
                }
            }

            //
            // Authenticate the user
            //
            if (!Authenticate(aCircuit))
            {
                reason = "Unable to verify identity";
                m_log.InfoFormat("[GATEKEEPER SERVICE]: Unable to verify identity of agent {0} {1}. Refusing service.", aCircuit.firstname, aCircuit.lastname);
                return false;
            }
            m_log.DebugFormat("[GATEKEEPER SERVICE]: Identity verified for {0} {1} @ {2}", aCircuit.firstname, aCircuit.lastname, authURL);

            //
            // Check for impersonations
            //
            UserAccount account = null;
            if (m_UserAccountService != null)
            {
                // Check to see if we have a local user with that UUID
                account = m_UserAccountService.GetUserAccount(m_ScopeID, aCircuit.AgentID);
                if (account != null)
                {
                    // Make sure this is the user coming home, and not a foreign user with same UUID as a local user
                    if (m_UserAgentService != null)
                    {
                        if (!m_UserAgentService.IsAgentComingHome(aCircuit.SessionID, m_ExternalName))
                        {
                            // Can't do, sorry
                            reason = "Unauthorized";
                            m_log.InfoFormat("[GATEKEEPER SERVICE]: Foreign agent {0} {1} has same ID as local user. Refusing service.",
                                aCircuit.firstname, aCircuit.lastname);
                            return false;

                        }
                    }
                }
            }

            //
            // Foreign agents allowed? Exceptions?
            //
            if (account == null)
            {
                bool allowed = m_ForeignAgentsAllowed;

                if (m_ForeignAgentsAllowed && IsException(aCircuit, m_ForeignsAllowedExceptions))
                    allowed = false;

                if (!m_ForeignAgentsAllowed && IsException(aCircuit, m_ForeignsDisallowedExceptions))
                    allowed = true;

                if (!allowed)
                {
                    reason = "Destination does not allow visitors from your world";
                    m_log.InfoFormat("[GATEKEEPER SERVICE]: Foreign agents are not permitted {0} {1} @ {2}. Refusing service.",
                        aCircuit.firstname, aCircuit.lastname, aCircuit.ServiceURLs["HomeURI"]);
                    return false;
                }
            }

            //
            // Is the user banned?
            // This uses a Ban service that's more powerful than the configs
            //
            string uui = (account != null ? aCircuit.AgentID.ToString() : Util.ProduceUserUniversalIdentifier(aCircuit));
            if (m_BansService != null && m_BansService.IsBanned(uui, aCircuit.IPAddress, aCircuit.Id0, authURL))
            {
                reason = "You are banned from this world";
                m_log.InfoFormat("[GATEKEEPER SERVICE]: Login failed, reason: user {0} is banned", uui);
                return false;
            }

            m_log.DebugFormat("[GATEKEEPER SERVICE]: User {0} is ok", aCircuit.Name);

            bool isFirstLogin = false;
            //
            // Login the presence, if it's not there yet (by the login service)
            //
            PresenceInfo presence = m_PresenceService.GetAgent(aCircuit.SessionID);
            if (presence != null) // it has been placed there by the login service
                isFirstLogin = true;

            else
            {
                if (!m_PresenceService.LoginAgent(aCircuit.AgentID.ToString(), aCircuit.SessionID, aCircuit.SecureSessionID))
                {
                    reason = "Unable to login presence";
                    m_log.InfoFormat("[GATEKEEPER SERVICE]: Presence login failed for foreign agent {0} {1}. Refusing service.",
                        aCircuit.firstname, aCircuit.lastname);
                    return false;
                }

                m_log.DebugFormat("[GATEKEEPER SERVICE]: Login presence {0} is ok", aCircuit.Name);

                // Also login foreigners with GridUser service
                if (m_GridUserService != null && account == null)
                {
                    string userId = aCircuit.AgentID.ToString();
                    string first = aCircuit.firstname, last = aCircuit.lastname;
                    if (last.StartsWith("@"))
                    {
                        string[] parts = aCircuit.firstname.Split('.');
                        if (parts.Length >= 2)
                        {
                            first = parts[0];
                            last = parts[1];
                        }
                    }

                    userId += ";" + aCircuit.ServiceURLs["HomeURI"] + ";" + first + " " + last;
                    m_GridUserService.LoggedIn(userId);
                }
            }

            //
            // Get the region
            //
            destination = m_GridService.GetRegionByUUID(m_ScopeID, destination.RegionID);
            if (destination == null)
            {
                reason = "Destination region not found";
                return false;
            }

            m_log.DebugFormat(
                "[GATEKEEPER SERVICE]: Destination {0} is ok for {1}", destination.RegionName, aCircuit.Name);

            //
            // Adjust the visible name
            //
            if (account != null)
            {
                aCircuit.firstname = account.FirstName;
                aCircuit.lastname = account.LastName;
            }
            if (account == null)
            {
                if (!aCircuit.lastname.StartsWith("@"))
                    aCircuit.firstname = aCircuit.firstname + "." + aCircuit.lastname;
                try
                {
                    Uri uri = new Uri(aCircuit.ServiceURLs["HomeURI"].ToString());
                    aCircuit.lastname = "@" + uri.Authority;
                }
                catch
                {
                    m_log.WarnFormat("[GATEKEEPER SERVICE]: Malformed HomeURI (this should never happen): {0}", aCircuit.ServiceURLs["HomeURI"]);
                    aCircuit.lastname = "@" + aCircuit.ServiceURLs["HomeURI"].ToString();
                }
            }

            //
            // Finally launch the agent at the destination
            //
            Constants.TeleportFlags loginFlag = isFirstLogin ? Constants.TeleportFlags.ViaLogin : Constants.TeleportFlags.ViaHGLogin;

            // Preserve our TeleportFlags we have gathered so-far
            loginFlag |= (Constants.TeleportFlags) aCircuit.teleportFlags;

            m_log.DebugFormat("[GATEKEEPER SERVICE]: Launching {0}, Teleport Flags: {1}", aCircuit.Name, loginFlag);

            EntityTransferContext ctx = new EntityTransferContext();

            if (!m_SimulationService.QueryAccess(
                destination, aCircuit.AgentID, aCircuit.ServiceURLs["HomeURI"].ToString(),
                true, aCircuit.startpos, new List<UUID>(), ctx, out reason))
                return false;

            return m_SimulationService.CreateAgent(source, destination, aCircuit, (uint)loginFlag, ctx, out reason);
        }

        protected bool Authenticate(AgentCircuitData aCircuit)
        {
            if (!CheckAddress(aCircuit.ServiceSessionID))
                return false;

            if (string.IsNullOrEmpty(aCircuit.IPAddress))
            {
                m_log.DebugFormat("[GATEKEEPER SERVICE]: Agent did not provide a client IP address.");
                return false;
            }

            string userURL = string.Empty;
            if (aCircuit.ServiceURLs.ContainsKey("HomeURI"))
                userURL = aCircuit.ServiceURLs["HomeURI"].ToString();

            if (userURL == string.Empty)
            {
                m_log.DebugFormat("[GATEKEEPER SERVICE]: Agent did not provide an authentication server URL");
                return false;
            }

            if (userURL == m_ExternalName)
            {
                return m_UserAgentService.VerifyAgent(aCircuit.SessionID, aCircuit.ServiceSessionID);
            }
            else
            {
                IUserAgentService userAgentService = new UserAgentServiceConnector(userURL);

                try
                {
                    return userAgentService.VerifyAgent(aCircuit.SessionID, aCircuit.ServiceSessionID);
                }
                catch
                {
                    m_log.DebugFormat("[GATEKEEPER SERVICE]: Unable to contact authentication service at {0}", userURL);
                    return false;
                }
            }
        }

        // Check that the service token was generated for *this* grid.
        // If it wasn't then that's a fake agent.
        protected bool CheckAddress(string serviceToken)
        {
            string[] parts = serviceToken.Split(new char[] { ';' });
            if (parts.Length < 2)
                return false;

            char[] trailing_slash = new char[] { '/' };
            string addressee = parts[0].TrimEnd(trailing_slash);
            string externalname = m_ExternalName.TrimEnd(trailing_slash);
            m_log.DebugFormat("[GATEKEEPER SERVICE]: Verifying {0} against {1}", addressee, externalname);

            Uri uri;
            try
            {
                uri = new Uri(addressee);
            }
            catch
            {
                m_log.DebugFormat("[GATEKEEPER SERVICE]: Visitor provided malformed service address {0}", addressee);
                return false;
            }

            return string.Equals(uri.GetLeftPart(UriPartial.Authority), m_Uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase) ;
        }

        #endregion


        #region Misc

        private bool IsException(AgentCircuitData aCircuit, List<string> exceptions)
        {
            bool exception = false;
            if (exceptions.Count > 0) // we have exceptions
            {
                // Retrieve the visitor's origin
                string userURL = aCircuit.ServiceURLs["HomeURI"].ToString();
                if (!userURL.EndsWith("/"))
                    userURL += "/";

                if (exceptions.Find(delegate(string s)
                {
                    if (!s.EndsWith("/"))
                        s += "/";
                    return s == userURL;
                }) != null)
                    exception = true;
            }

            return exception;
        }

        #endregion
    }
}
