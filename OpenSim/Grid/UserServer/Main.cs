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
 *     * Neither the name of the OpenSim Project nor the
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
using System.IO;
using System.Reflection;
using log4net;
using log4net.Config;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Statistics;
using OpenSim.Grid.Communications.OGS1;
using OpenSim.Grid.Framework;
using OpenSim.Grid.UserServer.Modules;

namespace OpenSim.Grid.UserServer
{
    /// <summary>
    /// Grid user server main class
    /// </summary>
    public class OpenUser_Main : BaseOpenSimServer, IUGAIMCore
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected UserConfig Cfg;

        protected UserDataBaseService m_userDataBaseService;

        public UserManager m_userManager;

        protected UserServerAvatarAppearanceModule m_avatarAppearanceModule;
        protected UserServerFriendsModule m_friendsModule;

        public UserLoginService m_loginService;
        public GridInfoService m_gridInfoService;
        public MessageServersConnector m_messagesService;

        protected UserServerCommandModule m_consoleCommandModule;

        public static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            m_log.Info("Launching UserServer...");

            OpenUser_Main userserver = new OpenUser_Main();

            userserver.Startup();
            userserver.Work();
        }

        public OpenUser_Main()
        {
            m_console = new ConsoleBase("User");
            MainConsole.Instance = m_console;
        }

        public void Work()
        {
            m_console.Notice("Enter help for a list of commands\n");

            while (true)
            {
                m_console.Prompt();
            }
        }

        protected override void StartupSpecific()
        {
            IInterServiceInventoryServices inventoryService = SetupRegisterCoreComponents();

            m_stats = StatsManager.StartCollectingUserStats();

            m_log.Info("[STARTUP]: Establishing data connection");
            //setup database access service
            m_userDataBaseService = new UserDataBaseService();
            m_userDataBaseService.Initialise(this);

            //setup services/modules
            StartupUserServerModules();

            StartOtherComponents(inventoryService);

            m_consoleCommandModule = new UserServerCommandModule(m_loginService);
            m_consoleCommandModule.Initialise(this);

            //register event handlers
            RegisterEventHandlers();

            //PostInitialise the modules
            m_consoleCommandModule.PostInitialise(); //it will register its Console command handlers in here
            m_userDataBaseService.PostInitialise();

            //register http handlers and start http server
            m_log.Info("[STARTUP]: Starting HTTP process");
            RegisterHttpHandlers();
            m_httpServer.Start();
            
            base.StartupSpecific();
        }

        private void StartOtherComponents(IInterServiceInventoryServices inventoryService)
        {
            m_gridInfoService = new GridInfoService();

            StartupLoginService(inventoryService);
            //
            // Get the minimum defaultLevel to access to the grid
            //
            m_loginService.setloginlevel((int)Cfg.DefaultUserLevel);

            m_messagesService = new MessageServersConnector();
        }

        private IInterServiceInventoryServices SetupRegisterCoreComponents()
        {
            Cfg = new UserConfig("USER SERVER", (Path.Combine(Util.configDir(), "UserServer_Config.xml")));

            IInterServiceInventoryServices inventoryService = new OGS1InterServiceInventoryService(Cfg.InventoryUrl);

            m_httpServer = new BaseHttpServer(Cfg.HttpPort);

            RegisterInterface<ConsoleBase>(m_console);
            RegisterInterface<UserConfig>(Cfg);
            RegisterInterface<IInterServiceInventoryServices>(inventoryService);

            return inventoryService;
        }

        /// <summary>
        /// Start up the user manager
        /// </summary>
        /// <param name="inventoryService"></param>
        protected virtual void StartupUserServerModules()
        {
            m_userManager = new UserManager(m_userDataBaseService);
            m_avatarAppearanceModule = new UserServerAvatarAppearanceModule(m_userDataBaseService);
            m_friendsModule = new UserServerFriendsModule(m_userDataBaseService);
        }

        /// <summary>
        /// Start up the login service
        /// </summary>
        /// <param name="inventoryService"></param>
        protected virtual void StartupLoginService(IInterServiceInventoryServices inventoryService)
        {
            m_loginService = new UserLoginService(
                m_userDataBaseService, inventoryService, new LibraryRootFolder(Cfg.LibraryXmlfile), Cfg, Cfg.DefaultStartupMsg, new RegionProfileServiceProxy());
        }

        protected virtual void RegisterEventHandlers()
        {
            m_loginService.OnUserLoggedInAtLocation += NotifyMessageServersUserLoggedInToLocation;
            m_userManager.OnLogOffUser += NotifyMessageServersUserLoggOff;

            m_messagesService.OnAgentLocation += HandleAgentLocation;
            m_messagesService.OnAgentLeaving += HandleAgentLeaving;
            m_messagesService.OnRegionStartup += HandleRegionStartup;
            m_messagesService.OnRegionShutdown += HandleRegionShutdown;
        }

        protected virtual void RegisterHttpHandlers()
        {
            m_loginService.RegisterHandlers(m_httpServer, Cfg.EnableLLSDLogin, true);
           
            m_userManager.RegisterHandlers(m_httpServer);
            m_friendsModule.RegisterHandlers(m_httpServer);
            m_avatarAppearanceModule.RegisterHandlers(m_httpServer);
            m_messagesService.RegisterHandlers(m_httpServer);

            m_httpServer.AddStreamHandler(new RestStreamHandler("GET", "/get_grid_info",
                                                                m_gridInfoService.RestGetGridInfoMethod));
            m_httpServer.AddXmlRPCHandler("get_grid_info", m_gridInfoService.XmlRpcGridInfoMethod);
        }

        public override void ShutdownSpecific()
        {
            m_loginService.OnUserLoggedInAtLocation -= NotifyMessageServersUserLoggedInToLocation;
        }

        #region IUGAIMCore
        protected Dictionary<Type, object> m_moduleInterfaces = new Dictionary<Type, object>();

        /// <summary>
        /// Register an Module interface.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="iface"></param>
        public void RegisterInterface<T>(T iface)
        {
            lock (m_moduleInterfaces)
            {
                if (!m_moduleInterfaces.ContainsKey(typeof(T)))
                {
                    m_moduleInterfaces.Add(typeof(T), iface);
                }
            }
        }

        public bool TryGet<T>(out T iface)
        {
            if (m_moduleInterfaces.ContainsKey(typeof(T)))
            {
                iface = (T)m_moduleInterfaces[typeof(T)];
                return true;
            }
            iface = default(T);
            return false;
        }

        public T Get<T>()
        {
            return (T)m_moduleInterfaces[typeof(T)];
        }

        public BaseHttpServer GetHttpServer()
        {
            return m_httpServer;
        }

     
        #endregion
        
        #region Console Command Handlers
      
        protected override void ShowHelp(string[] helpArgs)
        {
            base.ShowHelp(helpArgs);
        }
        #endregion

        public void TestResponse(List<InventoryFolderBase> resp)
        {
            m_console.Notice("response got");
        }

        #region Event Handlers
        public void NotifyMessageServersUserLoggOff(UUID agentID)
        {
            m_messagesService.TellMessageServersAboutUserLogoff(agentID);
        }

        public void NotifyMessageServersUserLoggedInToLocation(UUID agentID, UUID sessionID, UUID RegionID,
                                                               ulong regionhandle, float positionX, float positionY,
                                                               float positionZ, string firstname, string lastname)
        {
            m_messagesService.TellMessageServersAboutUser(agentID, sessionID, RegionID, regionhandle, positionX,
                                                          positionY, positionZ, firstname, lastname);
        }

        public void HandleAgentLocation(UUID agentID, UUID regionID, ulong regionHandle)
        {
            m_userManager.HandleAgentLocation(agentID, regionID, regionHandle);
        }

        public void HandleAgentLeaving(UUID agentID, UUID regionID, ulong regionHandle)
        {
            m_userManager.HandleAgentLeaving(agentID, regionID, regionHandle);
        }

        public void HandleRegionStartup(UUID regionID)
        {
            // This might seem strange, that we send this back to the
            // server it came from. But there is method to the madness.
            // There can be multiple user servers on the same database,
            // and each can have multiple messaging servers. So, we send
            // it to all known user servers, who send it to all known
            // message servers. That way, we should be able to finally
            // update presence to all regions and thereby all friends
            //
            m_userManager.HandleRegionStartup(regionID);
            m_messagesService.TellMessageServersAboutRegionShutdown(regionID);
        }

        public void HandleRegionShutdown(UUID regionID)
        {
            // This might seem strange, that we send this back to the
            // server it came from. But there is method to the madness.
            // There can be multiple user servers on the same database,
            // and each can have multiple messaging servers. So, we send
            // it to all known user servers, who send it to all known
            // message servers. That way, we should be able to finally
            // update presence to all regions and thereby all friends
            //
            m_userManager.HandleRegionShutdown(regionID);
            m_messagesService.TellMessageServersAboutRegionShutdown(regionID);
        }
        #endregion
    }
}
