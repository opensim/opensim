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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using System.Reflection;
using libsecondlife;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Configuration;

namespace OpenSim.Grid.UserServer
{
    /// <summary>
    /// </summary>
    public class OpenUser_Main :  conscmd_callback
    {
        private UserConfig Cfg;

        public UserManager m_userManager;
        public UserLoginService m_loginService;

        LogBase m_console;

        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("Launching UserServer...");

            OpenUser_Main userserver = new OpenUser_Main();

            userserver.Startup();
            userserver.Work();
        }

        private OpenUser_Main()
        {
            if (!Directory.Exists(Util.logDir()))
            {
                Directory.CreateDirectory(Util.logDir());
            }
            m_console = new LogBase((Path.Combine(Util.logDir(),"opengrid-userserver-console.log")), "OpenUser", this , false);
            MainLog.Instance = m_console;
        }

        private void Work()
        {
            m_console.Notice("Enter help for a list of commands\n");

            while (true)
            {
                m_console.MainLogPrompt();
            }
        }

        public void Startup()
        {
            this.Cfg = new UserConfig("USER SERVER", (Path.Combine(Util.configDir(), "UserServer_Config.xml")));

            MainLog.Instance.Verbose("Main.cs:Startup() - Establishing data connection");
            m_userManager = new UserManager();
            m_userManager._config = Cfg;
            m_userManager.AddPlugin(Cfg.DatabaseProvider);

            m_loginService = new UserLoginService(m_userManager, Cfg, Cfg.DefaultStartupMsg);

            MainLog.Instance.Verbose("Main.cs:Startup() - Starting HTTP process");
            BaseHttpServer httpServer = new BaseHttpServer((int)Cfg.HttpPort);

            httpServer.AddXmlRPCHandler("login_to_simulator", m_loginService.XmlRpcLoginMethod);

            httpServer.AddXmlRPCHandler("get_user_by_name", m_userManager.XmlRPCGetUserMethodName);
            httpServer.AddXmlRPCHandler("get_user_by_uuid", m_userManager.XmlRPCGetUserMethodUUID);

            httpServer.AddStreamHandler( new RestStreamHandler("DELETE", "/usersessions/", m_userManager.RestDeleteUserSessionMethod ));
            
            httpServer.Start();
            m_console.Status("SERVER", "Userserver 0.3 - Startup complete");
        }


        public void do_create(string what)
        {
            switch (what)
            {
                case "user":
                    string tempfirstname;
                    string templastname;
                    string tempMD5Passwd;
                    uint regX = 1000;
                    uint regY = 1000;

                    tempfirstname = m_console.CmdPrompt("First name");
                    templastname = m_console.CmdPrompt("Last name");
                    tempMD5Passwd = m_console.PasswdPrompt("Password");
                    regX = Convert.ToUInt32(m_console.CmdPrompt("Start Region X"));
                    regY = Convert.ToUInt32(m_console.CmdPrompt("Start Region Y"));

                    tempMD5Passwd = Util.Md5Hash(Util.Md5Hash(tempMD5Passwd) + ":" + "");

                    m_userManager.AddUserProfile(tempfirstname, templastname, tempMD5Passwd, regX, regY); 
                    break;
            }
        }

        public void RunCmd(string cmd, string[] cmdparams)
        {
            switch (cmd)
            {
                case "help":
                    m_console.Notice("create user - create a new user");
                    m_console.Notice("shutdown - shutdown the grid (USE CAUTION!)");
                    break;

                case "create":
                    do_create(cmdparams[0]);
                    break;

                case "shutdown":
                    m_console.Close();
                    Environment.Exit(0);
                    break;
            }
        }

        /*private void ConfigDB(IGenericConfig configData)
        {
            try
            {
                string attri = "";
                attri = configData.GetAttribute("DataBaseProvider");
                if (attri == "")
                {
                    StorageDll = "OpenSim.Framework.Data.DB4o.dll";
                    configData.SetAttribute("DataBaseProvider", "OpenSim.Framework.Data.DB4o.dll");
                }
                else
                {
                    StorageDll = attri;
                }
                configData.Commit();
            }
            catch
            {

            }
        }*/

        public void Show(string ShowWhat)
        {
        }
    }
}
