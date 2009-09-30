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
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Statistics;
using OpenSim.Grid.Communications.OGS1;
using OpenSim.Grid.Framework;
using OpenSim.Grid.UserServer.Modules;
using Nini.Config;

namespace OpenSim.Grid.UserServer
{
    /// <summary>
    /// Grid user server main class
    /// </summary>
    public class OpenUser_Main : BaseOpenSimServer, IGridServiceCore
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected UserConfig Cfg;

        protected UserDataBaseService m_userDataBaseService;

        public UserManager m_userManager;

        protected UserServerAvatarAppearanceModule m_avatarAppearanceModule;
        protected UserServerFriendsModule m_friendsModule;

        public UserLoginService m_loginService;
        public UserLoginAuthService m_loginAuthService;
        public MessageServersConnector m_messagesService;

        protected GridInfoServiceModule m_gridInfoService;

        protected UserServerCommandModule m_consoleCommandModule;
        protected UserServerEventDispatchModule m_eventDispatcher;

        protected AvatarCreationModule m_appearanceModule;

        protected static string m_consoleType = "local";
        protected static IConfigSource m_config = null;
        protected static string m_configFile = "UserServer_Config.xml";

        public static void Main(string[] args)
        {
            ArgvConfigSource argvSource = new ArgvConfigSource(args);
            argvSource.AddSwitch("Startup", "console", "c");
            argvSource.AddSwitch("Startup", "xmlfile", "x");

            IConfig startupConfig = argvSource.Configs["Startup"];
            if (startupConfig != null)
            {
                m_consoleType = startupConfig.GetString("console", "local");
                m_configFile = startupConfig.GetString("xmlfile", "UserServer_Config.xml");
            }

            m_config = argvSource;

            XmlConfigurator.Configure();

            m_log.Info("Launching UserServer...");

            OpenUser_Main userserver = new OpenUser_Main();

            userserver.Startup();
            userserver.Work();
        }

        public OpenUser_Main()
        {
            switch (m_consoleType)
            {
            case "rest":
                m_console = new RemoteConsole("User");
                break;
            case "basic":
                m_console = new CommandConsole("User");
                break;
            default:
                m_console = new LocalConsole("User");
                break;
            }
            MainConsole.Instance = m_console;
        }

        public void Work()
        {
            m_console.Output("Enter help for a list of commands\n");

            while (true)
            {
                m_console.Prompt();
            }
        }

        protected override void StartupSpecific()
        {
            IInterServiceInventoryServices inventoryService = StartupCoreComponents();

            m_stats = StatsManager.StartCollectingUserStats();

            //setup services/modules
            StartupUserServerModules();

            StartOtherComponents(inventoryService);

            //PostInitialise the modules
            PostInitialiseModules();

            //register http handlers and start http server
            m_log.Info("[STARTUP]: Starting HTTP process");
            RegisterHttpHandlers();
            m_httpServer.Start();

            base.StartupSpecific();
        }

        protected virtual IInterServiceInventoryServices StartupCoreComponents()
        {
            Cfg = new UserConfig("USER SERVER", (Path.Combine(Util.configDir(), m_configFile)));

            m_httpServer = new BaseHttpServer(Cfg.HttpPort);

            if (m_console is RemoteConsole)
            {
                RemoteConsole c = (RemoteConsole)m_console;
                c.SetServer(m_httpServer);
                IConfig netConfig = m_config.AddConfig("Network");
                netConfig.Set("ConsoleUser", Cfg.ConsoleUser);
                netConfig.Set("ConsolePass", Cfg.ConsolePass);
                c.ReadConfig(m_config);
            }

            RegisterInterface<CommandConsole>(m_console);
            RegisterInterface<UserConfig>(Cfg);

            //Should be in modules?
            IInterServiceInventoryServices inventoryService = new OGS1InterServiceInventoryService(Cfg.InventoryUrl);
            // IRegionProfileRouter regionProfileService = new RegionProfileServiceProxy();

            RegisterInterface<IInterServiceInventoryServices>(inventoryService);
            // RegisterInterface<IRegionProfileRouter>(regionProfileService);

            return inventoryService;
        }

        /// <summary>
        /// Start up the user manager
        /// </summary>
        /// <param name="inventoryService"></param>
        protected virtual void StartupUserServerModules()
        {
            m_log.Info("[STARTUP]: Establishing data connection");
            
            //we only need core components so we can request them from here
            IInterServiceInventoryServices inventoryService;
            TryGet<IInterServiceInventoryServices>(out inventoryService);
            
            CommunicationsManager commsManager = new UserServerCommsManager(inventoryService);

            //setup database access service, for now this has to be created before the other modules.
            m_userDataBaseService = new UserDataBaseService(commsManager);
            m_userDataBaseService.Initialise(this);

            //TODO: change these modules so they fetch the databaseService class in the PostInitialise method
            m_userManager = new UserManager(m_userDataBaseService);
            m_userManager.Initialise(this);

            m_avatarAppearanceModule = new UserServerAvatarAppearanceModule(m_userDataBaseService);
            m_avatarAppearanceModule.Initialise(this);

            m_friendsModule = new UserServerFriendsModule(m_userDataBaseService);
            m_friendsModule.Initialise(this);

            m_consoleCommandModule = new UserServerCommandModule();
            m_consoleCommandModule.Initialise(this);

            m_messagesService = new MessageServersConnector();
            m_messagesService.Initialise(this);

            m_gridInfoService = new GridInfoServiceModule();
            m_gridInfoService.Initialise(this);
        }

        protected virtual void StartOtherComponents(IInterServiceInventoryServices inventoryService)
        {
            m_appearanceModule = new AvatarCreationModule(m_userDataBaseService, Cfg, inventoryService);
            m_appearanceModule.Initialise(this);

            StartupLoginService(inventoryService);
            //
            // Get the minimum defaultLevel to access to the grid
            //
            m_loginService.setloginlevel((int)Cfg.DefaultUserLevel);

            RegisterInterface<UserLoginService>(m_loginService); //TODO: should be done in the login service

            m_eventDispatcher = new UserServerEventDispatchModule(m_userManager, m_messagesService, m_loginService);
            m_eventDispatcher.Initialise(this);
        }

        /// <summary>
        /// Start up the login service
        /// </summary>
        /// <param name="inventoryService"></param>
        protected virtual void StartupLoginService(IInterServiceInventoryServices inventoryService)
        {
            m_loginService = new UserLoginService(
                m_userDataBaseService, inventoryService, new LibraryRootFolder(Cfg.LibraryXmlfile), Cfg, Cfg.DefaultStartupMsg, new RegionProfileServiceProxy());
            
            if (Cfg.EnableHGLogin)
                m_loginAuthService = new UserLoginAuthService(m_userDataBaseService, inventoryService, new LibraryRootFolder(Cfg.LibraryXmlfile), 
                    Cfg, Cfg.DefaultStartupMsg, new RegionProfileServiceProxy());
        }

        protected virtual void PostInitialiseModules()
        {
            m_consoleCommandModule.PostInitialise(); //it will register its Console command handlers in here
            m_userDataBaseService.PostInitialise();
            m_messagesService.PostInitialise();
            m_eventDispatcher.PostInitialise(); //it will register event handlers in here
            m_gridInfoService.PostInitialise();
            m_userManager.PostInitialise();
            m_avatarAppearanceModule.PostInitialise();
            m_friendsModule.PostInitialise();
        }

        protected virtual void RegisterHttpHandlers()
        {
            m_loginService.RegisterHandlers(m_httpServer, Cfg.EnableLLSDLogin, true);

            if (m_loginAuthService != null)
                m_loginAuthService.RegisterHandlers(m_httpServer);

            m_userManager.RegisterHandlers(m_httpServer);
            m_friendsModule.RegisterHandlers(m_httpServer);
            m_avatarAppearanceModule.RegisterHandlers(m_httpServer);
            m_messagesService.RegisterHandlers(m_httpServer);
            m_gridInfoService.RegisterHandlers(m_httpServer);
        }

        public override void ShutdownSpecific()
        {
            m_eventDispatcher.Close();
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

        public void TestResponse(List<InventoryFolderBase> resp)
        {
            m_console.Output("response got");
        }
    }
}
