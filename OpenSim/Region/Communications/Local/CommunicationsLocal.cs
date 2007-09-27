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
using libsecondlife;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Data;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;

namespace OpenSim.Region.Communications.Local
{
    public class CommunicationsLocal : CommunicationsManager
    {      
        public CommunicationsLocal(NetworkServersInfo serversInfo, BaseHttpServer httpServer, AssetCache assetCache, LocalSettings settings)
            : base(serversInfo, httpServer, assetCache)
        {
            LocalInventoryService inventoryService = new LocalInventoryService();
            inventoryService.AddPlugin(settings.InventoryPlugin);
            m_inventoryService = inventoryService;

            LocalUserServices userService = new LocalUserServices(this, serversInfo);
            userService.AddPlugin(settings.UserDatabasePlugin);
            m_userService = userService;

            LocalBackEndServices backendService = new LocalBackEndServices();
            m_gridService = backendService;
            m_interRegion = backendService;

            LocalLoginService loginService = new LocalLoginService(userService, settings.WelcomeMessage, this, serversInfo, settings.AccountAuthentication);
            loginService.OnLoginToRegion += backendService.AddNewSession;

            httpServer.AddXmlRPCHandler("login_to_simulator", loginService.XmlRpcLoginMethod);
        }

        public void doCreate(string[] cmmdParams)
        {
            switch (cmmdParams[0])
            {
                case "user":
                    string firstName;
                    string lastName;
                    string password;
                    uint regX = 1000;
                    uint regY = 1000;

                    if (cmmdParams.Length < 2)
                    {

                        firstName = MainLog.Instance.CmdPrompt("First name", "Default");
                        lastName = MainLog.Instance.CmdPrompt("Last name", "User");
                        password = MainLog.Instance.PasswdPrompt("Password");
                        regX = Convert.ToUInt32(MainLog.Instance.CmdPrompt("Start Region X", "1000"));
                        regY = Convert.ToUInt32(MainLog.Instance.CmdPrompt("Start Region Y", "1000"));
                    }
                    else
                    {
                        firstName = cmmdParams[1];
                        lastName = cmmdParams[2];
                        password = cmmdParams[3];
                        regX = Convert.ToUInt32(cmmdParams[4]);
                        regY = Convert.ToUInt32(cmmdParams[5]);

                    }

                    AddUser(firstName, lastName, password, regX, regY);
                    break;
            }
        }

        public LLUUID AddUser(string firstName, string lastName, string password, uint regX, uint regY)
        {
            string md5PasswdHash = Util.Md5Hash(Util.Md5Hash(password) + ":" + "");

            m_userService.AddUserProfile(firstName, lastName, md5PasswdHash, regX, regY);
            UserProfileData userProf = this.UserService.GetUserProfile(firstName, lastName);
            if (userProf == null)
            {
                return LLUUID.Zero;
            }
            else
            {
                this.m_inventoryService.CreateNewUserInventory(LLUUID.Zero, userProf.UUID);
                Console.WriteLine("Created new inventory set for " + firstName + " " + lastName);
                return userProf.UUID;
            }
        }

        public class LocalSettings
        {
            public string WelcomeMessage;
            public bool AccountAuthentication = false;
            public string InventoryPlugin;
            public string UserDatabasePlugin;

            public LocalSettings(string welcomeMessage, bool accountsAuthenticate, string inventoryPlugin, string userPlugin)
            {
                WelcomeMessage = welcomeMessage;
                AccountAuthentication = accountsAuthenticate;
                InventoryPlugin = inventoryPlugin;
                UserDatabasePlugin = userPlugin;
            }
        }

    }
}
