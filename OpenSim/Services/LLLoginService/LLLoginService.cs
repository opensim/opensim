using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;

using log4net;
using Nini.Config;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Console;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Services.LLLoginService
{
    public class LLLoginService : ILoginService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool Initialized = false;

        private IUserAccountService m_UserAccountService;
        private IAuthenticationService m_AuthenticationService;
        private IInventoryService m_InventoryService;
        private IGridService m_GridService;
        private IPresenceService m_PresenceService;
        private ISimulationService m_LocalSimulationService;
        private ISimulationService m_RemoteSimulationService;
        private ILibraryService m_LibraryService;
        private IAvatarService m_AvatarService;

        private string m_DefaultRegionName;
        private string m_WelcomeMessage;
        private bool m_RequireInventory;
        private int m_MinLoginLevel;

        public LLLoginService(IConfigSource config, ISimulationService simService, ILibraryService libraryService)
        {
            IConfig serverConfig = config.Configs["LoginService"];
            if (serverConfig == null)
                throw new Exception(String.Format("No section LoginService in config file"));

            string accountService = serverConfig.GetString("UserAccountService", String.Empty);
            string authService = serverConfig.GetString("AuthenticationService", String.Empty);
            string invService = serverConfig.GetString("InventoryService", String.Empty);
            string gridService = serverConfig.GetString("GridService", String.Empty);
            string presenceService = serverConfig.GetString("PresenceService", String.Empty);
            string libService = serverConfig.GetString("LibraryService", String.Empty);
            string avatarService = serverConfig.GetString("AvatarService", String.Empty);
            string simulationService = serverConfig.GetString("SimulationService", String.Empty);

            m_DefaultRegionName = serverConfig.GetString("DefaultRegion", String.Empty);
            m_WelcomeMessage = serverConfig.GetString("WelcomeMessage", "Welcome to OpenSim!");
            m_RequireInventory = serverConfig.GetBoolean("RequireInventory", true);

            // These are required; the others aren't
            if (accountService == string.Empty || authService == string.Empty)
                throw new Exception("LoginService is missing service specifications");

            Object[] args = new Object[] { config };
            m_UserAccountService = ServerUtils.LoadPlugin<IUserAccountService>(accountService, args);
            m_AuthenticationService = ServerUtils.LoadPlugin<IAuthenticationService>(authService, args);
            m_InventoryService = ServerUtils.LoadPlugin<IInventoryService>(invService, args);
            if (gridService != string.Empty)
                m_GridService = ServerUtils.LoadPlugin<IGridService>(gridService, args);
            if (presenceService != string.Empty)
                m_PresenceService = ServerUtils.LoadPlugin<IPresenceService>(presenceService, args);
            if (avatarService != string.Empty)
                m_AvatarService = ServerUtils.LoadPlugin<IAvatarService>(avatarService, args);
            if (simulationService != string.Empty)
                m_RemoteSimulationService = ServerUtils.LoadPlugin<ISimulationService>(simulationService, args);
            //
            // deal with the services given as argument
            //
            m_LocalSimulationService = simService;
            if (libraryService != null)
            {
                m_log.DebugFormat("[LLOGIN SERVICE]: Using LibraryService given as argument");
                m_LibraryService = libraryService;
            }
            else if (libService != string.Empty)
            {
                m_log.DebugFormat("[LLOGIN SERVICE]: Using instantiated LibraryService");
                m_LibraryService = ServerUtils.LoadPlugin<ILibraryService>(libService, args);
            }

            if (!Initialized)
            {
                Initialized = true;
                RegisterCommands();
            }

            m_log.DebugFormat("[LLOGIN SERVICE]: Starting...");

        }

        public LLLoginService(IConfigSource config) : this(config, null, null)
        {
        }

        public LoginResponse Login(string firstName, string lastName, string passwd, string startLocation, IPEndPoint clientIP)
        {
            bool success = false;
            UUID session = UUID.Random();

            try
            {
                //
                // Get the account and check that it exists
                //
                UserAccount account = m_UserAccountService.GetUserAccount(UUID.Zero, firstName, lastName);
                if (account == null)
                {
                    m_log.InfoFormat("[LLOGIN SERVICE]: Login failed, reason: user not found");
                    return LLFailedLoginResponse.UserProblem;
                }

                if (account.UserLevel < m_MinLoginLevel)
                {
                    m_log.InfoFormat("[LLOGIN SERVICE]: Login failed, reason: login is blocked for user level {0}", account.UserLevel);
                    return LLFailedLoginResponse.LoginBlockedProblem;
                }

                //
                // Authenticate this user
                //
                if (!passwd.StartsWith("$1$"))
                    passwd = "$1$" + Util.Md5Hash(passwd);
                passwd = passwd.Remove(0, 3); //remove $1$
                string token = m_AuthenticationService.Authenticate(account.PrincipalID, passwd, 30);
                UUID secureSession = UUID.Zero;
                if ((token == string.Empty) || (token != string.Empty && !UUID.TryParse(token, out secureSession)))
                {
                    m_log.InfoFormat("[LLOGIN SERVICE]: Login failed, reason: authentication failed");
                    return LLFailedLoginResponse.UserProblem;
                }

                //
                // Get the user's inventory
                //
                if (m_RequireInventory && m_InventoryService == null)
                {
                    m_log.WarnFormat("[LLOGIN SERVICE]: Login failed, reason: inventory service not set up");
                    return LLFailedLoginResponse.InventoryProblem;
                }
                List<InventoryFolderBase> inventorySkel = m_InventoryService.GetInventorySkeleton(account.PrincipalID);
                if (m_RequireInventory && ((inventorySkel == null) || (inventorySkel != null && inventorySkel.Count == 0)))
                {
                    m_log.InfoFormat("[LLOGIN SERVICE]: Login failed, reason: unable to retrieve user inventory");
                    return LLFailedLoginResponse.InventoryProblem;
                }

                //
                // Login the presence
                //
                PresenceInfo presence = null;
                GridRegion home = null;
                if (m_PresenceService != null)
                {
                    success = m_PresenceService.LoginAgent(account.PrincipalID.ToString(), session, secureSession);
                    if (!success)
                    {
                        m_log.InfoFormat("[LLOGIN SERVICE]: Login failed, reason: could not login presence");
                        return LLFailedLoginResponse.GridProblem;
                    }

                    // Get the updated presence info
                    presence = m_PresenceService.GetAgent(session);

                    // Get the home region
                    if ((presence.HomeRegionID != UUID.Zero) && m_GridService != null)
                    {
                        home = m_GridService.GetRegionByUUID(account.ScopeID, presence.HomeRegionID);
                    }
                }

                //
                // Find the destination region/grid
                //
                string where = string.Empty;
                Vector3 position = Vector3.Zero;
                Vector3 lookAt = Vector3.Zero;
                GridRegion destination = FindDestination(account, presence, session, startLocation, out where, out position, out lookAt);
                if (destination == null)
                {
                    m_PresenceService.LogoutAgent(session, presence.Position, presence.LookAt);
                    m_log.InfoFormat("[LLOGIN SERVICE]: Login failed, reason: destination not found");
                    return LLFailedLoginResponse.GridProblem;
                }

                //
                // Get the avatar
                //
                AvatarData avatar = null;
                if (m_AvatarService != null)
                {
                    avatar = m_AvatarService.GetAvatar(account.PrincipalID);
                }

                //
                // Instantiate/get the simulation interface and launch an agent at the destination
                //
                ISimulationService simConnector = null;
                string reason = string.Empty;
                uint circuitCode = 0;
                AgentCircuitData aCircuit = null;
                Object[] args = new Object[] { destination };
                // HG standalones have both a localSimulatonDll and a remoteSimulationDll
                // non-HG standalones have just a localSimulationDll
                // independent login servers have just a remoteSimulationDll
                if (!startLocation.Contains("@") && (m_LocalSimulationService != null))
                    simConnector = m_LocalSimulationService;
                else if (m_RemoteSimulationService != null)
                    simConnector = m_RemoteSimulationService;
                if (simConnector != null)
                {
                    circuitCode = (uint)Util.RandomClass.Next(); ;
                    aCircuit = LaunchAgent(simConnector, destination, account, avatar, session, secureSession, circuitCode, position, out reason);
                }
                if (aCircuit == null)
                {
                    // Try the fallback regions
                    List<GridRegion> fallbacks = m_GridService.GetFallbackRegions(account.ScopeID, destination.RegionLocX, destination.RegionLocY);
                    if (fallbacks != null)
                    {
                        foreach (GridRegion r in fallbacks)
                        {
                            aCircuit = LaunchAgent(simConnector, r, account, avatar, session, secureSession, circuitCode, position, out reason);
                            if (aCircuit != null)
                            {
                                where = "safe";
                                destination = r;
                                break;
                            }
                        }
                    }

                    if (aCircuit == null)
                    {
                        // we tried...
                        m_PresenceService.LogoutAgent(session, presence.Position, presence.LookAt);
                        m_log.InfoFormat("[LLOGIN SERVICE]: Login failed, reason: {0}", reason);
                        return LLFailedLoginResponse.AuthorizationProblem;
                    }
                }

                // TODO: Get Friends list... 

                //
                // Finally, fill out the response and return it
                //
                LLLoginResponse response = new LLLoginResponse(account, aCircuit, presence, destination, inventorySkel, m_LibraryService,
                    where, startLocation, position, lookAt, m_WelcomeMessage, home, clientIP);

                return response;
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[LLOGIN SERVICE]: Exception processing login for {0} {1}: {2}", firstName, lastName, e.StackTrace);
                if (m_PresenceService != null)
                    m_PresenceService.LogoutAgent(session, new Vector3(128, 128, 0), new Vector3(0, 1, 0));
                return LLFailedLoginResponse.InternalError;
            }
        }

        private GridRegion FindDestination(UserAccount account, PresenceInfo pinfo, UUID sessionID, string startLocation, out string where, out Vector3 position, out Vector3 lookAt)
        {
            m_log.DebugFormat("[LLOGIN SERVICE]: FindDestination for start location {0}", startLocation);

            where = "home";
            position = new Vector3(128, 128, 0);
            lookAt = new Vector3(0, 1, 0);

            if (m_GridService == null)
                return null;

            if (startLocation.Equals("home"))
            {
                // logging into home region
                if (pinfo == null)
                    return null;

                GridRegion region = null;

                if (pinfo.HomeRegionID.Equals(UUID.Zero))
                {
                    List<GridRegion> defaults = m_GridService.GetDefaultRegions(account.ScopeID);
                    if (defaults != null && defaults.Count > 0)
                    {
                        region = defaults[0];
                        where = "safe";
                    }
                    else
                        m_log.WarnFormat("[LLOGIN SERVICE]: User {0} {1} does not have a home set and this grid does not have default locations.", 
                            account.FirstName, account.LastName);
                }
                else
                    region = m_GridService.GetRegionByUUID(account.ScopeID, pinfo.HomeRegionID);

                return region;
            }
            else if (startLocation.Equals("last"))
            {
                // logging into last visited region
                where = "last";

                if (pinfo == null)
                    return null;

                GridRegion region = null;

                if (pinfo.RegionID.Equals(UUID.Zero))
                {
                    List<GridRegion> defaults = m_GridService.GetDefaultRegions(account.ScopeID);
                    if (defaults != null && defaults.Count > 0)
                    {
                        region = defaults[0];
                        where = "safe";
                    }
                }
                else
                {
                    region = m_GridService.GetRegionByUUID(account.ScopeID, pinfo.RegionID);
                    position = pinfo.Position;
                    lookAt = pinfo.LookAt;
                }
                return region;

            }
            else
            {
                // free uri form
                // e.g. New Moon&135&46  New Moon@osgrid.org:8002&153&34
                where = "url";
                Regex reURI = new Regex(@"^uri:(?<region>[^&]+)&(?<x>\d+)&(?<y>\d+)&(?<z>\d+)$");
                Match uriMatch = reURI.Match(startLocation);
                if (uriMatch == null)
                {
                    m_log.InfoFormat("[LLLOGIN SERVICE]: Got Custom Login URI {0}, but can't process it", startLocation);
                    return null;
                }
                else
                {
                    position = new Vector3(float.Parse(uriMatch.Groups["x"].Value),
                                           float.Parse(uriMatch.Groups["y"].Value),
                                           float.Parse(uriMatch.Groups["z"].Value));

                    string regionName = uriMatch.Groups["region"].ToString();
                    if (regionName != null)
                    {
                        if (!regionName.Contains("@"))
                        {

                            List<GridRegion> regions = m_GridService.GetRegionsByName(account.ScopeID, regionName, 1);
                            if ((regions == null) || (regions != null && regions.Count == 0))
                            {
                                m_log.InfoFormat("[LLLOGIN SERVICE]: Got Custom Login URI {0}, can't locate region {1}. Trying defaults.", startLocation, regionName);
                                regions = m_GridService.GetDefaultRegions(UUID.Zero);
                                if (regions != null && regions.Count > 0)
                                {
                                    where = "safe"; 
                                    return regions[0];
                                }
                                else
                                {
                                    m_log.InfoFormat("[LLLOGIN SERVICE]: Got Custom Login URI {0}, Grid does not provide default regions.", startLocation);
                                    return null;
                                }
                            }
                            return regions[0];
                        }
                        else
                        {
                            string[] parts = regionName.Split(new char[] { '@' });
                            if (parts.Length < 2)
                            {
                                m_log.InfoFormat("[LLLOGIN SERVICE]: Got Custom Login URI {0}, can't locate region {1}", startLocation, regionName);
                                return null;
                            }
                            // Valid specification of a remote grid
                            regionName = parts[0];
                            string domainLocator = parts[1];
                            parts = domainLocator.Split(new char[] {':'});
                            string domainName = parts[0];
                            uint port = 0;
                            if (parts.Length > 1)
                                UInt32.TryParse(parts[1], out port);
                            GridRegion region = new GridRegion();
                            region.ExternalHostName = domainName;
                            region.HttpPort = port;
                            region.RegionName = regionName;
                            return region;
                        }
                    }
                    else
                    {
                        List<GridRegion> defaults = m_GridService.GetDefaultRegions(account.ScopeID);
                        if (defaults != null && defaults.Count > 0)
                        {
                            where = "safe"; 
                            return defaults[0];
                        }
                        else
                            return null;
                    }
                }
                //response.LookAt = "[r0,r1,r0]";
                //// can be: last, home, safe, url
                //response.StartLocation = "url";

            }

        }

        private AgentCircuitData LaunchAgent(ISimulationService simConnector, GridRegion region, UserAccount account, 
            AvatarData avatar, UUID session, UUID secureSession, uint circuit, Vector3 position, out string reason)
        {
            reason = string.Empty;
            AgentCircuitData aCircuit = new AgentCircuitData();

            aCircuit.AgentID = account.PrincipalID;
            if (avatar != null)
                aCircuit.Appearance = avatar.ToAvatarAppearance(account.PrincipalID);
            else
                aCircuit.Appearance = new AvatarAppearance(account.PrincipalID);

            //aCircuit.BaseFolder = irrelevant
            aCircuit.CapsPath = CapsUtil.GetRandomCapsObjectPath();
            aCircuit.child = false; // the first login agent is root
            aCircuit.ChildrenCapSeeds = new Dictionary<ulong, string>();
            aCircuit.circuitcode = circuit;
            aCircuit.firstname = account.FirstName;
            //aCircuit.InventoryFolder = irrelevant
            aCircuit.lastname = account.LastName;
            aCircuit.SecureSessionID = secureSession;
            aCircuit.SessionID = session;
            aCircuit.startpos = position;
            aCircuit.ServiceURLs = account.ServiceURLs;

            if (simConnector.CreateAgent(region, aCircuit, 0, out reason))
                return aCircuit;

            return null;

        }

        #region Console Commands
        private void RegisterCommands()
        {
            //MainConsole.Instance.Commands.AddCommand
            MainConsole.Instance.Commands.AddCommand("loginservice", false, "login level",
                    "login level <level>",
                    "Set the minimum user level to log in", HandleLoginCommand);

            MainConsole.Instance.Commands.AddCommand("loginservice", false, "login reset",
                    "login reset",
                    "Reset the login level to allow all users",
                    HandleLoginCommand);

            MainConsole.Instance.Commands.AddCommand("loginservice", false, "login text",
                    "login text <text>",
                    "Set the text users will see on login", HandleLoginCommand);

        }

        private void HandleLoginCommand(string module, string[] cmd)
        {
            string subcommand = cmd[1];

            switch (subcommand)
            {
                case "level":
                    // Set the minimum level to allow login 
                    // Useful to allow grid update without worrying about users.
                    // or fixing critical issues
                    //
                    if (cmd.Length > 2)
                        Int32.TryParse(cmd[2], out m_MinLoginLevel);
                    break;
                case "reset":
                    m_MinLoginLevel = 0;
                    break;
                case "text":
                    if (cmd.Length > 2)
                        m_WelcomeMessage = cmd[2];
                    break;
            }
        }
    }

    #endregion
}
