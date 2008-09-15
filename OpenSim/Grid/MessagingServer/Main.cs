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
using System.IO;
using System.Reflection;
using OpenMetaverse;
using log4net;
using log4net.Config;

using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;

namespace OpenSim.Grid.MessagingServer
{
    /// <summary>
    /// </summary>
    public class OpenMessage_Main : BaseOpenSimServer, conscmd_callback
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private MessageServerConfig Cfg;
        private MessageService msgsvc;

        // private UUID m_lastCreatedUser = UUID.Random();

        public static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            m_log.Info("Launching MessagingServer...");



            OpenMessage_Main messageserver = new OpenMessage_Main();

            messageserver.Startup();
            messageserver.Work();
        }

        private OpenMessage_Main()
        {
            m_console = new ConsoleBase("Messaging", this);
            MainConsole.Instance = m_console;
        }

        private void Work()
        {
            m_console.Notice("Enter help for a list of commands\n");

            while (true)
            {
                m_console.Prompt();
            }
        }

        public override void Startup()
        {
            base.Startup();

            Cfg = new MessageServerConfig("MESSAGING SERVER", (Path.Combine(Util.configDir(), "MessagingServer_Config.xml")));

            m_log.Info("[REGION]: Starting HTTP process");
            m_httpServer = new BaseHttpServer(Cfg.HttpPort);

            msgsvc = new MessageService(Cfg);

            if (msgsvc.registerWithUserServer())
            {
                m_httpServer.AddXmlRPCHandler("login_to_simulator", msgsvc.UserLoggedOn);
                m_httpServer.AddXmlRPCHandler("logout_of_simulator", msgsvc.UserLoggedOff);
                //httpServer.AddXmlRPCHandler("get_user_by_name", m_userManager.XmlRPCGetUserMethodName);
                //httpServer.AddXmlRPCHandler("get_user_by_uuid", m_userManager.XmlRPCGetUserMethodUUID);
                //httpServer.AddXmlRPCHandler("get_avatar_picker_avatar", m_userManager.XmlRPCGetAvatarPickerAvatar);
                //httpServer.AddXmlRPCHandler("add_new_user_friend", m_userManager.XmlRpcResponseXmlRPCAddUserFriend);
                //httpServer.AddXmlRPCHandler("remove_user_friend", m_userManager.XmlRpcResponseXmlRPCRemoveUserFriend);
                //httpServer.AddXmlRPCHandler("update_user_friend_perms", m_userManager.XmlRpcResponseXmlRPCUpdateUserFriendPerms);
                //httpServer.AddXmlRPCHandler("get_user_friend_list", m_userManager.XmlRpcResponseXmlRPCGetUserFriendList);


                //httpServer.AddStreamHandler(
                //new RestStreamHandler("DELETE", "/usersessions/", m_userManager.RestDeleteUserSessionMethod));

                m_httpServer.Start();
                m_log.Info("[SERVER]: Messageserver 0.5 - Startup complete");
            }
            else
            {
                m_log.Error("[STARTUP]: Unable to connect to User Server");
            }
        }

        public void do_create(string what)
        {
            switch (what)
            {
                case "user":
                    try
                    {
                        //userID =
                            //m_userManager.AddUserProfile(tempfirstname, templastname, tempMD5Passwd, regX, regY);
                    } catch (Exception ex)
                    {
                        m_console.Error("[SERVER]: Error creating user: {0}", ex.ToString());
                    }

                    try
                    {
                        //RestObjectPoster.BeginPostObject<Guid>(m_userManager._config.InventoryUrl + "CreateInventory/",
                                                               //userID.Guid);
                    }
                    catch (Exception ex)
                    {
                        m_console.Error("[SERVER]: Error creating inventory for user: {0}", ex.ToString());
                    }
                    // m_lastCreatedUser = userID;
                    break;
            }
        }

        public override void RunCmd(string cmd, string[] cmdparams)
        {
            base.RunCmd(cmd, cmdparams);

            switch (cmd)
            {
                case "clear-cache":
                    int entries = msgsvc.ClearRegionCache();
                    m_console.Notice("Region cache cleared! Cleared " + entries.ToString() + " entries");
                    break;
            }
        }
        
        protected override void ShowHelp(string[] helpArgs)
        {
            base.ShowHelp(helpArgs);
            
            m_console.Notice("clear-cache - Clears region cache.  Should be done when regions change position.  The region cache gets stale after a while.");
        }

        public override void Shutdown()
        {
            msgsvc.deregisterWithUserServer();

            base.Shutdown();
        }
    }
}
