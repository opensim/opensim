/*
* Copyright (c) OpenSim project, http://sim.opensecondlife.org/
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
* 
*/

using Nwc.XmlRpc;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Collections;
using System.Security.Cryptography;
using System.Xml;
using libsecondlife;
using OpenSim;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Grid;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.User;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Types;

namespace OpenSim.UserServer
{
    /// <summary>
    /// When running in local (default) mode , handles client logins.
    /// </summary>
    public class LoginServer : LoginService, IUserServer
    {
        private IGridServer m_gridServer;
        public IPAddress clientAddress = IPAddress.Loopback;
        public IPAddress remoteAddress = IPAddress.Any;
        private int NumClients;
        private bool userAccounts = false;
        private string _mpasswd;
        private bool _needPasswd = false;
        private LocalUserProfileManager userManager;
        private int m_simPort;
        private string m_simAddr;
        private uint regionX;
        private uint regionY;

        public LocalUserProfileManager LocalUserManager
        {
            get
            {
                return userManager;
            }
        }

        public LoginServer(IGridServer gridServer, string simAddr, int simPort, uint regX, uint regY, bool useAccounts)
        {
            m_gridServer = gridServer;
            m_simPort = simPort;
            m_simAddr = simAddr;
            regionX = regX;
            regionY = regY;
            this.userAccounts = useAccounts;
        }

        public void Startup()
        {
            this._needPasswd = false;
            // read in default response string
           /* StreamReader SR;
            string lines;
            SR = File.OpenText("new-login.dat");

            while (!SR.EndOfStream)
            {
                lines = SR.ReadLine();
                _defaultResponse += lines;
            }
            SR.Close();
            * */

            this._mpasswd = EncodePassword("testpass");

            userManager = new LocalUserProfileManager(this.m_gridServer, m_simPort, m_simAddr, regionX, regionY);
            //userManager.InitUserProfiles();
            userManager.SetKeys("", "", "", "Welcome to OpenSim");
        }

        public XmlRpcResponse XmlRpcLoginMethod(XmlRpcRequest request)
        {
            Console.WriteLine("login attempt");
            Hashtable requestData = (Hashtable)request.Params[0];
            string first;
            string last;
            string passwd;

            LoginResponse loginResponse = new LoginResponse();
            loginResponse.RegionX = regionX;
            loginResponse.RegionY = regionY;

            //get login name
            if (requestData.Contains("first"))
            {
                first = (string)requestData["first"];
            }
            else
            {
                first = "test";
            }

            if (requestData.Contains("last"))
            {
                last = (string)requestData["last"];
            }
            else
            {
                last = "User" + NumClients.ToString();
            }

            if (requestData.Contains("passwd"))
            {
                passwd = (string)requestData["passwd"];
            }
            else
            {
                passwd = "notfound";
            }

            if (!Authenticate(first, last, passwd))
            {
                return loginResponse.LoginFailedResponse();
            }

            NumClients++;

            // Create a agent and session LLUUID
            // Agent = GetAgentId(first, last);
            // int SessionRand = Util.RandomClass.Next(1, 999);
            // Session = new LLUUID("aaaabbbb-0200-" + SessionRand.ToString("0000") + "-8664-58f53e442797");
            // LLUUID secureSess = LLUUID.Random();            

            loginResponse.SimPort = m_simPort.ToString();
            loginResponse.SimAddress = m_simAddr.ToString();
            // loginResponse.AgentID = Agent.ToStringHyphenated();
            // loginResponse.SessionID = Session.ToStringHyphenated();
            // loginResponse.SecureSessionID = secureSess.ToStringHyphenated();
            loginResponse.CircuitCode = (Int32)(Util.RandomClass.Next());
            XmlRpcResponse response = loginResponse.ToXmlRpcResponse();
            Hashtable responseData = (Hashtable)response.Value;

            //inventory
            /* ArrayList InventoryList = (ArrayList)responseData["inventory-skeleton"];
             Hashtable Inventory1 = (Hashtable)InventoryList[0];
            Hashtable Inventory2 = (Hashtable)InventoryList[1];
            LLUUID BaseFolderID = LLUUID.Random();
             LLUUID InventoryFolderID = LLUUID.Random();
             Inventory2["name"] = "Textures";
             Inventory2["folder_id"] = BaseFolderID.ToStringHyphenated();
             Inventory2["type_default"] = 0;
             Inventory1["folder_id"] = InventoryFolderID.ToStringHyphenated();

             ArrayList InventoryRoot = (ArrayList)responseData["inventory-root"];
             Hashtable Inventoryroot = (Hashtable)InventoryRoot[0];
             Inventoryroot["folder_id"] = InventoryFolderID.ToStringHyphenated();
            */
            CustomiseLoginResponse(responseData, first, last);

            Login _login = new Login();
            //copy data to login object
            _login.First = first;
            _login.Last = last;
            _login.Agent = loginResponse.AgentID;
            _login.Session = loginResponse.SessionID;
            _login.SecureSession = loginResponse.SecureSessionID;

            _login.BaseFolder = loginResponse.BaseFolderID;
            _login.InventoryFolder = loginResponse.InventoryFolderID;

            //working on local computer if so lets add to the gridserver's list of sessions?
            if (m_gridServer.GetName() == "Local")
            {
                ((LocalGridBase)m_gridServer).AddNewSession(_login);
            }

            return response;
        }

        protected virtual void CustomiseLoginResponse(Hashtable responseData, string first, string last)
        {
        }

        protected virtual LLUUID GetAgentId(string firstName, string lastName)
        {
            LLUUID Agent;
            int AgentRand = Util.RandomClass.Next(1, 9999);
            Agent = new LLUUID("99998888-0100-" + AgentRand.ToString("0000") + "-8ec1-0b1d5cd6aead");
            return Agent;
        }

        protected virtual bool Authenticate(string first, string last, string passwd)
        {
            if (this._needPasswd)
            {
                //every user needs the password to login
                string encodedPass = passwd.Remove(0, 3); //remove $1$
                if (encodedPass == this._mpasswd)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                //do not need password to login
                return true;
            }
        }

        private static string EncodePassword(string passwd)
        {
            Byte[] originalBytes;
            Byte[] encodedBytes;
            MD5 md5;

            md5 = new MD5CryptoServiceProvider();
            originalBytes = ASCIIEncoding.Default.GetBytes(passwd);
            encodedBytes = md5.ComputeHash(originalBytes);

            return Regex.Replace(BitConverter.ToString(encodedBytes), "-", "").ToLower();
        }

        public bool CreateUserAccount(string firstName, string lastName, string password)
        {
            Console.WriteLine("creating new user account");
            string mdPassword = EncodePassword(password);
            Console.WriteLine("with password: " + mdPassword);
            this.userManager.CreateNewProfile(firstName, lastName, mdPassword);
            return true;
        }

        //IUserServer implementation
        public AgentInventory RequestAgentsInventory(LLUUID agentID)
        {
            AgentInventory aInventory = null;
            if (this.userAccounts)
            {
                aInventory = this.userManager.GetUsersInventory(agentID);
            }

            return aInventory;
        }

        public bool UpdateAgentsInventory(LLUUID agentID, AgentInventory inventory)
        {
            return true;
        }

        public void SetServerInfo(string ServerUrl, string SendKey, string RecvKey)
        {

        }
    }


}
