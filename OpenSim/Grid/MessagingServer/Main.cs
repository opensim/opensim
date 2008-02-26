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
* 
*/

using System;
using System.Collections.Generic;
using System.IO;
using libsecondlife;
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
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private MessageServerConfig Cfg;
        private MessageService msgsvc;

        //public UserManager m_userManager;
        //public UserLoginService m_loginService;
        
        private LLUUID m_lastCreatedUser = LLUUID.Random();

        [STAThread]
        public static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();

            m_log.Info("Launching MessagingServer...");

            

            OpenMessage_Main messageserver = new OpenMessage_Main();

            messageserver.Startup();
            messageserver.Work();
        }

        private OpenMessage_Main()
        {
            m_console = new ConsoleBase("OpenMessage", this);
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

        public void Startup()
        {
            Cfg = new MessageServerConfig("MESSAGING SERVER", (Path.Combine(Util.configDir(), "MessagingServer_Config.xml")));

            m_log.Info("[REGION]: Starting HTTP process");
            BaseHttpServer httpServer = new BaseHttpServer(Cfg.HttpPort);
            
            msgsvc = new MessageService(Cfg);

            if (msgsvc.registerWithUserServer())
            {
                httpServer.AddXmlRPCHandler("login_to_simulator", msgsvc.UserLoggedOn);
                httpServer.AddXmlRPCHandler("logout_of_simulator", msgsvc.UserLoggedOff);
                //httpServer.AddXmlRPCHandler("get_user_by_name", m_userManager.XmlRPCGetUserMethodName);
                //httpServer.AddXmlRPCHandler("get_user_by_uuid", m_userManager.XmlRPCGetUserMethodUUID);
                //httpServer.AddXmlRPCHandler("get_avatar_picker_avatar", m_userManager.XmlRPCGetAvatarPickerAvatar);
                //httpServer.AddXmlRPCHandler("add_new_user_friend", m_userManager.XmlRpcResponseXmlRPCAddUserFriend);
                //httpServer.AddXmlRPCHandler("remove_user_friend", m_userManager.XmlRpcResponseXmlRPCRemoveUserFriend);
                //httpServer.AddXmlRPCHandler("update_user_friend_perms", m_userManager.XmlRpcResponseXmlRPCUpdateUserFriendPerms);
                //httpServer.AddXmlRPCHandler("get_user_friend_list", m_userManager.XmlRpcResponseXmlRPCGetUserFriendList);


                //httpServer.AddStreamHandler(
                //new RestStreamHandler("DELETE", "/usersessions/", m_userManager.RestDeleteUserSessionMethod));

                httpServer.Start();
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
                                                               //userID.UUID);
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
                case "help":
                    m_console.Notice("shutdown - shutdown the message server (USE CAUTION!)");
                    break;

                case "shutdown":
                    msgsvc.deregisterWithUserServer();
                    m_console.Close();
                    Environment.Exit(0);
                    break;
            }
        }

        public void Show(string ShowWhat)
        {
        }
    }
}
