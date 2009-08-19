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
using Nini.Config;
using log4net.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Grid.Framework;
using OpenSim.Grid.MessagingServer.Modules;

namespace OpenSim.Grid.MessagingServer
{
    /// <summary>
    /// </summary>
    public class OpenMessage_Main : BaseOpenSimServer , IGridServiceCore
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private MessageServerConfig Cfg;
        private MessageService msgsvc;

        private MessageRegionModule m_regionModule;
        private InterMessageUserServerModule m_userServerModule;

        private UserDataBaseService m_userDataBaseService;

        // private UUID m_lastCreatedUser = UUID.Random();

        protected static string m_consoleType = "local";
        protected static IConfigSource m_config = null;

        public static void Main(string[] args)
        {
            ArgvConfigSource argvSource = new ArgvConfigSource(args);
            argvSource.AddSwitch("Startup", "console", "c");

            IConfig startupConfig = argvSource.Configs["Startup"];
            if (startupConfig != null)
            {
                m_consoleType = startupConfig.GetString("console", "local");
            }

            m_config = argvSource;

            XmlConfigurator.Configure();

            m_log.Info("[SERVER]: Launching MessagingServer...");

            OpenMessage_Main messageserver = new OpenMessage_Main();

            messageserver.Startup();
            messageserver.Work();
        }

        public OpenMessage_Main()
        {
            switch (m_consoleType)
            {
            case "rest":
                m_console = new RemoteConsole("Messaging");
                break;
            case "basic":
                m_console = new CommandConsole("Messaging");
                break;
            default:
                m_console = new LocalConsole("Messaging");
                break;
            }
            MainConsole.Instance = m_console;
        }

        private void Work()
        {
            m_console.Output("Enter help for a list of commands\n");

            while (true)
            {
                m_console.Prompt();
            }
        }

        private void registerWithUserServer()
        {
            if (m_userServerModule.registerWithUserServer())
            {
                if (m_httpServer == null)
                {
                    m_log.Info("[SERVER]: Starting HTTP process");
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

                    m_httpServer.AddXmlRPCHandler("login_to_simulator", msgsvc.UserLoggedOn);
                    m_httpServer.AddXmlRPCHandler("logout_of_simulator", msgsvc.UserLoggedOff);
                    m_httpServer.AddXmlRPCHandler("get_presence_info_bulk", msgsvc.GetPresenceInfoBulk);
                    m_httpServer.AddXmlRPCHandler("process_region_shutdown", msgsvc.ProcessRegionShutdown);
                    m_httpServer.AddXmlRPCHandler("agent_location", msgsvc.AgentLocation);
                    m_httpServer.AddXmlRPCHandler("agent_leaving", msgsvc.AgentLeaving);

                    m_httpServer.AddXmlRPCHandler("region_startup", m_regionModule.RegionStartup);
                    m_httpServer.AddXmlRPCHandler("region_shutdown", m_regionModule.RegionShutdown);

                    m_httpServer.Start();
                }
                m_log.Info("[SERVER]: Userserver registration was successful");
            }
            else
            {
                m_log.Error("[STARTUP]: Unable to connect to User Server");
            }

        }

        private void deregisterFromUserServer()
        {
            m_userServerModule.deregisterWithUserServer();
//            if (m_httpServer != null)
//            {
                // try a completely fresh registration, with fresh handlers, too
//                m_httpServer.Stop();
//                m_httpServer = null;
//            }
            m_console.Output("[SERVER]: Deregistered from userserver.");
        }

        protected override void StartupSpecific()
        {
            Cfg = new MessageServerConfig("MESSAGING SERVER", (Path.Combine(Util.configDir(), "MessagingServer_Config.xml")));

            m_userDataBaseService = new UserDataBaseService();
            m_userDataBaseService.AddPlugin(Cfg.DatabaseProvider, Cfg.DatabaseConnect);

            //Register the database access service so modules can fetch it
           // RegisterInterface<UserDataBaseService>(m_userDataBaseService);

            m_userServerModule = new InterMessageUserServerModule(Cfg, this);
            m_userServerModule.Initialise();

            msgsvc = new MessageService(Cfg, this, m_userDataBaseService);
            msgsvc.Initialise();

            m_regionModule = new MessageRegionModule(Cfg, this);
            m_regionModule.Initialise();

            registerWithUserServer();

            m_userServerModule.PostInitialise();
            msgsvc.PostInitialise();
            m_regionModule.PostInitialise();

            m_log.Info("[SERVER]: Messageserver 0.5 - Startup complete");

            base.StartupSpecific();

            m_console.Commands.AddCommand("messageserver", false, "clear cache",
                    "clear cache",
                    "Clear presence cache", HandleClearCache);

            m_console.Commands.AddCommand("messageserver", false, "register",
                    "register",
                    "Re-register with user server(s)", HandleRegister);
        }

        public void do_create(string what)
        {
            //switch (what)
            //{
            //    case "user":
            //        try
            //        {
            //            //userID =
            //                //m_userManager.AddUserProfile(tempfirstname, templastname, tempMD5Passwd, regX, regY);
            //        } catch (Exception ex)
            //        {
            //            m_console.Error("[SERVER]: Error creating user: {0}", ex.ToString());
            //        }

            //        try
            //        {
            //            //RestObjectPoster.BeginPostObject<Guid>(m_userManager._config.InventoryUrl + "CreateInventory/",
            //                                                   //userID.Guid);
            //        }
            //        catch (Exception ex)
            //        {
            //            m_console.Error("[SERVER]: Error creating inventory for user: {0}", ex.ToString());
            //        }
            //        // m_lastCreatedUser = userID;
            //        break;
            //}
        }

        private void HandleClearCache(string module, string[] cmd)
        {
            int entries = m_regionModule.ClearRegionCache();
            m_console.Output("Region cache cleared! Cleared " +
                    entries.ToString() + " entries");
        }

        private void HandleRegister(string module, string[] cmd)
        {
            deregisterFromUserServer();
            registerWithUserServer();
        }

        public override void ShutdownSpecific()
        {
            m_userServerModule.deregisterWithUserServer();
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
    }
}
