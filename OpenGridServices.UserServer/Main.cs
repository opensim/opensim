/*
Copyright (c) OpenSim project, http://osgrid.org/


* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
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
using System.Reflection;
using System.IO;
using System.Text;
using libsecondlife;
using OpenSim.Framework.User;
using OpenSim.Framework.Sims;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Console;
using OpenSim.Servers;

namespace OpenGridServices.UserServer
{
    /// <summary>
    /// </summary>
    public class OpenUser_Main : BaseServer, conscmd_callback
    {
        private string ConfigDll = "OpenUser.Config.UserConfigDb4o.dll";
        private UserConfig Cfg;

        public UserManager m_userManager; // Replaces below.

        //private UserProfileManager m_userProfileManager; // Depreciated

        public Dictionary<LLUUID, UserProfile> UserSessions = new Dictionary<LLUUID, UserProfile>();

        ConsoleBase m_console;

        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting...\n");

            OpenUser_Main userserver = new OpenUser_Main();

            userserver.Startup();
            userserver.Work();
        }

        private OpenUser_Main()
        {
            m_console = new ConsoleBase("opengrid-userserver-console.log", "OpenUser", this , false);
            MainConsole.Instance = m_console;
        }

        private void Work()
        {
            m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH,"\nEnter help for a list of commands\n");

            while (true)
            {
                m_console.MainConsolePrompt();
            }
        }

        public void Startup()
        {
            MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Main.cs:Startup() - Loading configuration");
            Cfg = this.LoadConfigDll(this.ConfigDll);
            Cfg.InitConfig();

            MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Main.cs:Startup() - Establishing data connection");
            m_userManager = new UserManager();
            m_userManager._config = Cfg;
            m_userManager.AddPlugin("OpenGrid.Framework.Data.MySQL.dll");

            MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW,"Main.cs:Startup() - Starting HTTP process");
            BaseHttpServer httpServer = new BaseHttpServer(8002);

            httpServer.AddXmlRPCHandler("login_to_simulator", m_userManager.XmlRpcLoginMethod);
            httpServer.AddRestHandler("DELETE", "/usersessions/", m_userManager.RestDeleteUserSessionMethod);

            httpServer.Start();
        }


        public void do_create(string what)
        {
            switch (what)
            {
                case "user":
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH,"Commandline user creation is currently disabled.");
                    break;
                    /*
                    string tempfirstname;
                    string templastname;
                    string tempMD5Passwd;

                    tempfirstname = m_console.CmdPrompt("First name");
                    templastname = m_console.CmdPrompt("Last name");
                    tempMD5Passwd = m_console.PasswdPrompt("Password");

                    System.Security.Cryptography.MD5CryptoServiceProvider x = new System.Security.Cryptography.MD5CryptoServiceProvider();
                    byte[] bs = System.Text.Encoding.UTF8.GetBytes(tempMD5Passwd);
                    bs = x.ComputeHash(bs);
                    System.Text.StringBuilder s = new System.Text.StringBuilder();
                    foreach (byte b in bs)
                    {
                        s.Append(b.ToString("x2").ToLower());
                    }
                    tempMD5Passwd = s.ToString();

                    UserProfile newuser = m_userProfileManager.CreateNewProfile(tempfirstname, templastname, tempMD5Passwd);
                    newuser.homelookat = new LLVector3(-0.57343f, -0.819255f, 0f);
                    newuser.homepos = new LLVector3(128f, 128f, 150f);
                    m_userProfileManager.SaveUserProfiles();
                    break;
                    */
            }
        }

        public void RunCmd(string cmd, string[] cmdparams)
        {
            switch (cmd)
            {
                case "help":
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH,"create user - create a new user");
                    m_console.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH,"shutdown - shutdown the grid (USE CAUTION!)");
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

        private UserConfig LoadConfigDll(string dllName)
        {
            Assembly pluginAssembly = Assembly.LoadFrom(dllName);
            UserConfig config = null;

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("IUserConfig", true);

                        if (typeInterface != null)
                        {
                            IUserConfig plug = (IUserConfig)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            config = plug.GetConfigObject();
                            break;
                        }

                        typeInterface = null;
                    }
                }
            }
            pluginAssembly = null;
            return config;
        }

        public void Show(string ShowWhat)
        {
        }
    }
}
