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
using OpenSim.Services.Connectors.InstantMessage;
using OpenSim.Services.Connectors.Hypergrid;
using OpenMetaverse;

using Nini.Config;
using log4net;

namespace OpenSim.Services.HypergridService
{
    public class GatekeeperService : IGatekeeperService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static bool m_Initialized = false;

        private static IGridService m_GridService;
        private static IPresenceService m_PresenceService;
        private static IUserAccountService m_UserAccountService;
        private static IUserAgentService m_UserAgentService;
        private static ISimulationService m_SimulationService;
        private static IGridUserService m_GridUserService;
        private static IBansService m_BansService;

        private static Regex m_AllowedClientsRegex = null;
        private static Regex m_DeniedClientsRegex = null;
        private static string m_DeniedMacs = string.Empty;
        private static string m_DeniedID0s = string.Empty;
        private static bool m_ForeignAgentsAllowed = true;
        private static readonly List<string> m_ForeignsAllowedExceptions = new();
        private static readonly List<string> m_ForeignsDisallowedExceptions = new();

        private static UUID m_ScopeID;
        private static bool m_AllowTeleportsToAnyRegion;

        private static OSHHTPHost m_gatekeeperHost;
        private static string m_gatekeeperURL;
        private static HashSet<OSHHTPHost> m_gateKeeperAlias;

        private static GridRegion m_DefaultGatewayRegion;
        private static bool m_allowDuplicatePresences = false;
        private static string m_messageKey;

        public GatekeeperService(IConfigSource config, ISimulationService simService)
        {
            if (!m_Initialized)
            {
                m_Initialized = true;

                IConfig serverConfig = config.Configs["GatekeeperService"];
                if (serverConfig is null)
                    throw new Exception(String.Format("No section GatekeeperService in config file"));

                string accountService = serverConfig.GetString("UserAccountService", string.Empty);
                string homeUsersService = serverConfig.GetString("UserAgentService", string.Empty);
                string gridService = serverConfig.GetString("GridService", string.Empty);
                string presenceService = serverConfig.GetString("PresenceService", string.Empty);
                string simulationService = serverConfig.GetString("SimulationService", string.Empty);
                string gridUserService = serverConfig.GetString("GridUserService", string.Empty);
                string bansService = serverConfig.GetString("BansService", string.Empty);
                // These are mandatory, the others aren't
                if (gridService.Length == 0 || presenceService.Length == 0)
                    throw new Exception("Incomplete specifications, Gatekeeper Service cannot function.");

                string scope = serverConfig.GetString("ScopeID", UUID.Zero.ToString());
                UUID.TryParse(scope, out m_ScopeID);
                //m_WelcomeMessage = serverConfig.GetString("WelcomeMessage", "Welcome to OpenSim!");
                m_AllowTeleportsToAnyRegion = serverConfig.GetBoolean("AllowTeleportsToAnyRegion", true);

                string[] sections = new string[] { "Const, Startup", "Hypergrid", "GatekeeperService" };
                string externalName = Util.GetConfigVarFromSections<string>(config, "GatekeeperURI", sections, string.Empty);
                if(string.IsNullOrEmpty(externalName))
                    externalName = serverConfig.GetString("ExternalName", string.Empty);

                m_gatekeeperHost = new OSHHTPHost(externalName, true);
                if (!m_gatekeeperHost.IsResolvedHost)
                {
                    m_log.Error((m_gatekeeperHost.IsValidHost ? "Could not resolve GatekeeperURI" : "GatekeeperURI is a invalid host ") + externalName ?? "");
                    throw new Exception("GatekeeperURI is invalid");
                }
                m_gatekeeperURL = m_gatekeeperHost.URIwEndSlash;

                string gatekeeperURIAlias = Util.GetConfigVarFromSections<string>(config, "GatekeeperURIAlias", sections, string.Empty);

                if (!string.IsNullOrWhiteSpace(gatekeeperURIAlias))
                {
                    string[] alias = gatekeeperURIAlias.Split(',');
                    for (int i = 0; i < alias.Length; ++i)
                    {
                        OSHHTPHost tmp = new(alias[i].Trim(), false);
                        if (tmp.IsValidHost)
                        {
                            m_gateKeeperAlias ??= new HashSet<OSHHTPHost>();
                            m_gateKeeperAlias.Add(tmp);
                        }
                    }
                }

                object[] args = new object[] { config };
                m_GridService = ServerUtils.LoadPlugin<IGridService>(gridService, args);
                m_PresenceService = ServerUtils.LoadPlugin<IPresenceService>(presenceService, args);

                if (!string.IsNullOrEmpty(accountService))
                    m_UserAccountService = ServerUtils.LoadPlugin<IUserAccountService>(accountService, args);
                if (!string.IsNullOrEmpty(homeUsersService))
                    m_UserAgentService = ServerUtils.LoadPlugin<IUserAgentService>(homeUsersService, args);
                if (!string.IsNullOrEmpty(gridUserService))
                    m_GridUserService = ServerUtils.LoadPlugin<IGridUserService>(gridUserService, args);
                if (!string.IsNullOrEmpty(bansService))
                    m_BansService = ServerUtils.LoadPlugin<IBansService>(bansService, args);

                if (simService is not null)
                    m_SimulationService = simService;
                else if (simulationService != string.Empty)
                        m_SimulationService = ServerUtils.LoadPlugin<ISimulationService>(simulationService, args);

                string[] possibleAccessControlConfigSections = new string[] { "AccessControl", "GatekeeperService" };
                string AllowedClients = Util.GetConfigVarFromSections<string>(config, "AllowedClients", possibleAccessControlConfigSections, string.Empty);
                if (!string.IsNullOrEmpty(AllowedClients))
                {
                    try
                    {
                        m_AllowedClientsRegex = new Regex(AllowedClients, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    }
                    catch
                    {
                        m_AllowedClientsRegex = null;
                        m_log.Error("[GATEKEEPER SERVICE]: failed to parse AllowedClients");
                    }
                }

                string DeniedClients = Util.GetConfigVarFromSections<string>(config, "DeniedClients", possibleAccessControlConfigSections, string.Empty);
                if (!string.IsNullOrEmpty(DeniedClients))
                {
                    try
                    {
                        m_DeniedClientsRegex = new Regex(DeniedClients, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    }
                    catch
                    {
                        m_DeniedClientsRegex = null;
                        m_log.Error("[GATEKEEPER SERVICE]: failed to parse DeniedClients");
                    }
                }

                m_DeniedMacs = Util.GetConfigVarFromSections<string>(config, "DeniedMacs", possibleAccessControlConfigSections, string.Empty);
                m_DeniedID0s = Util.GetConfigVarFromSections<string>(config, "DeniedID0s", possibleAccessControlConfigSections, string.Empty);

                m_ForeignAgentsAllowed = serverConfig.GetBoolean("ForeignAgentsAllowed", true);

                LoadDomainExceptionsFromConfig(serverConfig, "AllowExcept", m_ForeignsAllowedExceptions);
                LoadDomainExceptionsFromConfig(serverConfig, "DisallowExcept", m_ForeignsDisallowedExceptions);

                if (m_GridService is null || m_PresenceService is null || m_SimulationService is null)
                    throw new Exception("Unable to load a required plugin, Gatekeeper Service cannot function.");

                IConfig presenceConfig = config.Configs["PresenceService"];
                if (presenceConfig is not null)
                {
                    m_allowDuplicatePresences = presenceConfig.GetBoolean("AllowDuplicatePresences", m_allowDuplicatePresences);
                }

                IConfig messagingConfig = config.Configs["Messaging"];
                if (messagingConfig is not null)
                    m_messageKey = messagingConfig.GetString("MessageKey", String.Empty);
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

            foreach (string ps in parts)
            {
                string s = ps.Trim();
                if(!s.EndsWith("/"))
                    s += '/';
                exceptions.Add(s);
            }
        }

        public bool LinkLocalRegion(string regionName, out UUID regionID, out ulong regionHandle, out string externalName, out string imageURL, out string reason, out int sizeX, out int sizeY)
        {
            regionID = UUID.Zero;
            regionHandle = 0;
            sizeX = (int)Constants.RegionSize;
            sizeY = (int)Constants.RegionSize;
            externalName = m_gatekeeperURL + ((regionName != string.Empty) ? " " + regionName : "");
            imageURL = string.Empty;
            reason = string.Empty;
            GridRegion region;

            //m_log.DebugFormat("[GATEKEEPER SERVICE]: Request to link to {0}", (regionName.Length == 0)? "default region" : regionName);
            if (!m_AllowTeleportsToAnyRegion || regionName.Length == 0)
            {
                List<GridRegion> defs = m_GridService.GetDefaultHypergridRegions(m_ScopeID);
                if (defs is not null && defs.Count > 0)
                {
                    region = defs[0];
                    m_DefaultGatewayRegion = region;
                }
                else
                {
                    reason = "Grid setup problem. Try specifying a particular region here.";
                    m_log.Debug("[GATEKEEPER SERVICE]: Unable to send information. Please specify a default region for this grid!");
                    return false;
                }
            }
            else
            {
                region = m_GridService.GetLocalRegionByName(m_ScopeID, regionName);
                if (region is null)
                {
                    m_log.DebugFormat($"[GATEKEEPER SERVICE]: LinkLocalRegion could not find local region {regionName}");
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
                    agentHomeURI is null ? "" : " @ " + agentHomeURI);

                message = "Teleporting to the default region.";
                return m_DefaultGatewayRegion;
            }

            GridRegion region = m_GridService.GetRegionByUUID(m_ScopeID, regionID);

            if (region == null)
            {
                m_log.DebugFormat(
                    "[GATEKEEPER SERVICE]: Could not find region with ID {0} as requested by user {1}{2}.  Returning null.",
                    regionID, agentID, (agentHomeURI is null) ? "" : " @ " + agentHomeURI);

                message = "The teleport destination could not be found.";
                return null;
            }

            m_log.DebugFormat(
                "[GATEKEEPER SERVICE]: Returning region {0} {1} @ {2} to user {3}{4}.",
                region.RegionName,
                region.RegionID,
                region.ServerURI,
                agentID,
                agentHomeURI is null ? "" : " @ " + agentHomeURI);

            return region;
        }

        #region Login Agent
        public bool LoginAgent(GridRegion source, AgentCircuitData aCircuit, GridRegion destination, out string reason)
        {
            reason = string.Empty;

            string authURL = aCircuit.ServiceURLs.TryGetValue("HomeURI", out object value) ? value.ToString() : string.Empty;

            m_log.InfoFormat("[GATEKEEPER SERVICE]: Login request for {0} {1} @ {2} ({3}) at {4} using viewer {5}, channel {6}, IP {7}, Mac {8}, Id0 {9}, Teleport Flags: {10}. From region {11}",
                aCircuit.firstname, aCircuit.lastname, authURL, aCircuit.AgentID, destination.RegionID,
                aCircuit.Viewer, aCircuit.Channel, aCircuit.IPAddress, aCircuit.Mac, aCircuit.Id0, (TeleportFlags)aCircuit.teleportFlags,
                (source == null) ? "Unknown" : string.Format("{0} ({1}){2}", source.RegionName, source.RegionID, (source.RawServerURI == null) ? "" : " @ " + source.ServerURI));

            string curViewer = Util.GetViewerName(aCircuit);
            string curMac = aCircuit.Mac.ToString();


            //
            // Check client
            //
            if (m_AllowedClientsRegex is not null)
            {
                lock(m_AllowedClientsRegex)
                {
                    Match am = m_AllowedClientsRegex.Match(curViewer);

                    if (!am.Success)
                    {
                        reason = "Login failed: client " + curViewer + " is not allowed";
                        m_log.InfoFormat("[GATEKEEPER SERVICE]: Login failed, reason: client {0} is not allowed", curViewer);
                        return false;
                    }
                }
            }

            if (m_DeniedClientsRegex is not null)
            {
                lock(m_DeniedClientsRegex)
                {
                    Match dm = m_DeniedClientsRegex.Match(curViewer);

                    if (dm.Success)
                    {
                        reason = "Login failed: client " + curViewer + " is denied";
                        m_log.InfoFormat("[GATEKEEPER SERVICE]: Login failed, reason: client {0} is denied", curViewer);
                        return false;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(m_DeniedMacs))
            {
                //m_log.InfoFormat("[GATEKEEPER SERVICE]: Checking users Mac {0} against list of denied macs {1} ...", curMac, m_DeniedMacs);
                if (m_DeniedMacs.Contains(curMac, StringComparison.InvariantCultureIgnoreCase))
                {
                    reason = "Login failed: client with Mac " + curMac + " is denied";
                    m_log.InfoFormat("[GATEKEEPER SERVICE]: Login failed, reason: client with mac {0} is denied", curMac);
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(m_DeniedID0s))
            {
                //m_log.InfoFormat("[GATEKEEPER SERVICE]: Checking users Mac {0} against list of denied macs {1} ...", curMac, m_DeniedMacs);
                if (m_DeniedID0s.Contains(aCircuit.Id0, StringComparison.InvariantCultureIgnoreCase))
                {
                    reason = "Login failed: client with id0 " + aCircuit.Id0 + " is denied";
                    m_log.InfoFormat("[GATEKEEPER SERVICE]: Login failed, reason: client with mac {0} is denied", aCircuit.Id0);
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
            if (m_UserAccountService is not null)
            {
                // Check to see if we have a local user with that UUID
                account = m_UserAccountService.GetUserAccount(m_ScopeID, aCircuit.AgentID);
                if (account is not null)
                {
                    // Make sure this is the user coming home, and not a foreign user with same UUID as a local user
                    if (m_UserAgentService is not null)
                    {
                        if (!m_UserAgentService.IsAgentComingHome(aCircuit.SessionID, m_gatekeeperURL))
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
            if (account is null)
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
            string uui = (account is not null ? aCircuit.AgentID.ToString() : Util.ProduceUserUniversalIdentifier(aCircuit));
            if (m_BansService is not null && m_BansService.IsBanned(uui, aCircuit.IPAddress, aCircuit.Id0, authURL))
            {
                reason = "You are banned from this world";
                m_log.InfoFormat("[GATEKEEPER SERVICE]: Login failed, reason: user {0} is banned", uui);
                return false;
            }

            UUID agentID = aCircuit.AgentID;
            if(agentID.Equals(Constants.servicesGodAgentID))
            {
                // really?
                reason = "Invalid account ID";
                return false;
            }

            if(m_GridUserService is not null)
            {
                GridUserInfo guinfo = m_GridUserService.GetGridUserInfo(uui);
                if (guinfo is not null)
                {
                    if (!m_allowDuplicatePresences)
                    {
                        if (guinfo.Online && !guinfo.LastRegionID.IsZero())
                        {
                            if (SendAgentGodKillToRegion(UUID.Zero, agentID, uui, guinfo))
                            {
                                if (account is not null)
                                    m_log.InfoFormat(
                                        "[GATEKEEPER SERVICE]: Login failed for {0} {1}, reason: already logged in",
                                        account.FirstName, account.LastName);
                                reason = "You appear to be already logged in on the destination grid " +
                                        "Please wait a a minute or two and retry. " +
                                        "If this takes longer than a few minutes please contact the grid owner.";
                                return false;
                            }
                        }
                    }
                }
            }

            m_log.DebugFormat("[GATEKEEPER SERVICE]: User {0} is ok", aCircuit.Name);

            bool isFirstLogin = false;
            //
            // Login the presence, if it's not there yet (by the login service)
            //
            PresenceInfo presence = m_PresenceService.GetAgent(aCircuit.SessionID);
            if (presence is not null) // it has been placed there by the login service
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

            }

            //
            // Get the region
            //
            destination = m_GridService.GetRegionByUUID(m_ScopeID, destination.RegionID);
            if (destination is null)
            {
                reason = "Destination region not found";
                return false;
            }

            m_log.DebugFormat(
                "[GATEKEEPER SERVICE]: Destination {0} is ok for {1}", destination.RegionName, aCircuit.Name);

            //
            // Adjust the visible name
            //
            if (account is not null)
            {
                aCircuit.firstname = account.FirstName;
                aCircuit.lastname = account.LastName;
            }
            if (account is null)
            {
                if (!aCircuit.lastname.StartsWith("@"))
                    aCircuit.firstname = aCircuit.firstname + "." + aCircuit.lastname;
                try
                {
                    Uri uri = new(aCircuit.ServiceURLs["HomeURI"].ToString());
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

            EntityTransferContext ctx = new();

            if (!m_SimulationService.QueryAccess(
                destination, aCircuit.AgentID, aCircuit.ServiceURLs["HomeURI"].ToString(),
                true, aCircuit.startpos, new List<UUID>(), ctx, out reason))
                return false;

            bool didit = m_SimulationService.CreateAgent(source, destination, aCircuit, (uint)loginFlag, ctx, out reason);

            if(didit)
            {
                m_log.DebugFormat("[GATEKEEPER SERVICE]: Login presence {0} is ok", aCircuit.Name);

                if(!isFirstLogin && m_GridUserService is not null && account is null) 
                {
                    // Also login foreigners with GridUser service
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

            return didit;
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

            OSHHTPHost userHomeHost = new(userURL, true);
            if(!userHomeHost.IsResolvedHost)
            {
                m_log.DebugFormat("[GATEKEEPER SERVICE]: Agent did not provide an authentication server URL");
                return false;
            }

            if (m_gatekeeperHost.Equals(userHomeHost))
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

            OSHHTPHost reqGrid = new(parts[0], false);
            if(!reqGrid.IsValidHost)
            {
                m_log.DebugFormat("[GATEKEEPER SERVICE]: Visitor provided malformed gird address {0}", parts[0]);
                return false;
            }

            m_log.DebugFormat("[GATEKEEPER SERVICE]: Verifying grid {0} against {1}", reqGrid.URI, m_gatekeeperHost.URI);

            if(m_gatekeeperHost.Equals(reqGrid))
                return true;
            if (m_gateKeeperAlias != null && m_gateKeeperAlias.Contains(reqGrid))
                return true;
            return false;
        }

        #endregion


        #region Misc

        private bool IsException(AgentCircuitData aCircuit, List<string> exceptions)
        {
            if (exceptions.Count > 0) // we have exceptions
            {
                // Retrieve the visitor's origin
                string userURL = aCircuit.ServiceURLs["HomeURI"].ToString().Trim();
                if (string.IsNullOrEmpty(userURL))
                    return false;

                if (!userURL.EndsWith("/"))
                    userURL += "/";

                foreach (string s in exceptions)
                {
                    if (userURL.Equals(s))
                        return true;
                }
            }
            return false;
        }

        private bool SendAgentGodKillToRegion(UUID scopeID, UUID agentID, string uui, GridUserInfo guinfo)
        {
            UUID regionID = guinfo.LastRegionID;
            GridRegion regInfo = m_GridService.GetRegionByUUID(scopeID, regionID);
            if(regInfo is null)
                return false;

            string regURL = regInfo.ServerURI;
            if(string.IsNullOrEmpty(regURL))
                return false;

            GridInstantMessage msg = new GridInstantMessage();
            msg.imSessionID = UUID.Zero.Guid;
            msg.fromAgentID = Constants.servicesGodAgentID.Guid;
            msg.toAgentID = agentID.Guid;
            msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
            msg.fromAgentName = "GRID";
            msg.message = string.Format("New login detected");
            msg.dialog = 250; // God kick
            msg.fromGroup = false;
            msg.offline = (byte)0;
            msg.ParentEstateID = 0;
            msg.Position = Vector3.Zero;
            msg.RegionID = scopeID.Guid;
            msg.binaryBucket = new byte[1] {0};
            InstantMessageServiceConnector.SendInstantMessage(regURL,msg, m_messageKey);

            m_GridUserService.LoggedOut(uui,
                UUID.Zero, guinfo.LastRegionID, guinfo.LastPosition, guinfo.LastLookAt);

            return true;
        }
        #endregion
    }
}
