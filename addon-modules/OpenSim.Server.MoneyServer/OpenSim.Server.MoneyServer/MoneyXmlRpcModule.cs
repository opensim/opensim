/*
 * Copyright (c) Contributors, http://opensimulator.org/, http://www.nsl.tuis.ac.jp/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *	 * Redistributions of source code must retain the above copyright
 *	   notice, this list of conditions and the following disclaimer.
 *	 * Redistributions in binary form must reproduce the above copyright
 *	   notice, this list of conditions and the following disclaimer in the
 *	   documentation and/or other materials provided with the distribution.
 *	 * Neither the name of the OpenSim Project nor the
 *	   names of its contributors may be used to endorse or promote products
 *	   derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using log4net;
using Nini.Config;
using Nwc.XmlRpc;

using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Data.MySQL.MoneyData;
using OpenSim.Region.OptionalModules.Currency;
using OpenSim.Region.Framework.Scenes;

using NSL.Network.XmlRpc;
using NSL.Certificate.Tools;


namespace OpenSim.Server.MoneyServer
{
    class MoneyXmlRpcModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private int m_defaultBalance = 1000;
        //
        private bool m_forceTransfer = false;
        private string m_bankerAvatar = "";

        private bool m_scriptSendMoney = false;
        private string m_scriptAccessKey = "";
        private string m_scriptIPaddress = "127.0.0.1";

        private bool m_hg_enable = false;
        private bool m_gst_enable = false;
        private int m_hg_defaultBalance = 0;
        private int m_gst_defaultBalance = 0;

        private bool m_checkServerCert = false;
        private string m_cacertFilename = "";

        private string m_certFilename = "";
        private string m_certPassword = "";
        private X509Certificate2 m_clientCert = null;

        private string m_sslCommonName = "";

        /// <summary>
        /// For server authentication
        /// </summary>
        private NSLCertificateVerify m_certVerify = new NSLCertificateVerify();

        /// <summary>
        ///  Update Balance Messages
        /// </summary>
        private string m_BalanceMessageLandSale = "Paid the Money L${0} for Land.";
        private string m_BalanceMessageRcvLandSale = "";
        private string m_BalanceMessageSendGift = "Sent Gift L${0} to {1}.";
        private string m_BalanceMessageReceiveGift = "Received Gift L${0} from {1}.";
        private string m_BalanceMessagePayCharge = "";
        private string m_BalanceMessageBuyObject = "Bought the Object {2} from {1} by L${0}.";
        private string m_BalanceMessageSellObject = "{1} bought the Object {2} by L${0}.";
        private string m_BalanceMessageGetMoney = "Got the Money L${0} from {1}.";
        private string m_BalanceMessageBuyMoney = "Bought the Money L${0}.";
        private string m_BalanceMessageRollBack = "RollBack the Transaction: L${0} from/to {1}.";
        private string m_BalanceMessageSendMoney = "Paid the Money L${0} to {1}.";
        private string m_BalanceMessageReceiveMoney = "Received L${0} from {1}.";

        private bool m_enableAmountZero = false;

        const int MONEYMODULE_REQUEST_TIMEOUT = 30 * 1000;  //30 seconds
        private long TicksToEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

        private IMoneyDBService m_moneyDBService;
        private IMoneyServiceCore m_moneyCore;

        protected IConfig m_server_config;
        protected IConfig m_cert_config;

        /// <value>
        /// Used to notify old regions as to which OpenSim version to upgrade to
        /// </value>
        //private string m_opensimVersion;

        private Dictionary<string, string> m_sessionDic;
        private Dictionary<string, string> m_secureSessionDic;
        private Dictionary<string, string> m_webSessionDic;

        protected BaseHttpServer m_httpServer;


        public MoneyXmlRpcModule()
        {
        }


        public void Initialise(string opensimVersion, IMoneyDBService moneyDBService, IMoneyServiceCore moneyCore)
        {
            //m_opensimVersion = opensimVersion;
            m_moneyDBService = moneyDBService;
            m_moneyCore = moneyCore;
            m_server_config = m_moneyCore.GetServerConfig();    // [MoneyServer] Section
            m_cert_config = m_moneyCore.GetCertConfig();        // [Certificate] Section

            ////////////////////////////////////////////////////////////////////////
            // [MoneyServer] Section
            m_defaultBalance = m_server_config.GetInt("DefaultBalance", m_defaultBalance);

            m_forceTransfer = m_server_config.GetBoolean("EnableForceTransfer", m_forceTransfer);

            string banker = m_server_config.GetString("BankerAvatar", m_bankerAvatar);
            m_bankerAvatar = banker.ToLower();

            m_enableAmountZero = m_server_config.GetBoolean("EnableAmountZero", m_enableAmountZero);
            m_scriptSendMoney = m_server_config.GetBoolean("EnableScriptSendMoney", m_scriptSendMoney);
            m_scriptAccessKey = m_server_config.GetString("MoneyScriptAccessKey", m_scriptAccessKey);
            m_scriptIPaddress = m_server_config.GetString("MoneyScriptIPaddress", m_scriptIPaddress);

            // Hyper Grid Avatar
            m_hg_enable = m_server_config.GetBoolean("EnableHGAvatar", m_hg_enable);
            m_gst_enable = m_server_config.GetBoolean("EnableGuestAvatar", m_gst_enable);
            m_hg_defaultBalance = m_server_config.GetInt("HGAvatarDefaultBalance", m_hg_defaultBalance);
            m_gst_defaultBalance = m_server_config.GetInt("GuestAvatarDefaultBalance", m_gst_defaultBalance);

            // Update Balance Messages
            m_BalanceMessageLandSale = m_server_config.GetString("BalanceMessageLandSale", m_BalanceMessageLandSale);
            m_BalanceMessageRcvLandSale = m_server_config.GetString("BalanceMessageRcvLandSale", m_BalanceMessageRcvLandSale);
            m_BalanceMessageSendGift = m_server_config.GetString("BalanceMessageSendGift", m_BalanceMessageSendGift);
            m_BalanceMessageReceiveGift = m_server_config.GetString("BalanceMessageReceiveGift", m_BalanceMessageReceiveGift);
            m_BalanceMessagePayCharge = m_server_config.GetString("BalanceMessagePayCharge", m_BalanceMessagePayCharge);
            m_BalanceMessageBuyObject = m_server_config.GetString("BalanceMessageBuyObject", m_BalanceMessageBuyObject);
            m_BalanceMessageSellObject = m_server_config.GetString("BalanceMessageSellObject", m_BalanceMessageSellObject);
            m_BalanceMessageGetMoney = m_server_config.GetString("BalanceMessageGetMoney", m_BalanceMessageGetMoney);
            m_BalanceMessageBuyMoney = m_server_config.GetString("BalanceMessageBuyMoney", m_BalanceMessageBuyMoney);
            m_BalanceMessageRollBack = m_server_config.GetString("BalanceMessageRollBack", m_BalanceMessageRollBack);
            m_BalanceMessageSendMoney = m_server_config.GetString("BalanceMessageSendMoney", m_BalanceMessageSendMoney);
            m_BalanceMessageReceiveMoney = m_server_config.GetString("BalanceMessageReceiveMoney", m_BalanceMessageReceiveMoney);


            ////////////////////////////////////////////////////////////////////////
            // [Certificate] Section

            // XML RPC to Region Server (Client Mode)
            // Client Certificate
            m_certFilename = m_cert_config.GetString("ClientCertFilename", m_certFilename);
            m_certPassword = m_cert_config.GetString("ClientCertPassword", m_certPassword);
            if (m_certFilename != "")
            {
                m_clientCert = new X509Certificate2(m_certFilename, m_certPassword);
                m_log.Info("[MONEY RPC]: Initialise: Issue Authentication of Client. Cert file is " + m_certFilename);
            }

            // Server Authentication
            m_checkServerCert = m_cert_config.GetBoolean("CheckServerCert", m_checkServerCert);
            m_cacertFilename = m_cert_config.GetString("CACertFilename", m_cacertFilename);

            if (m_cacertFilename != "")
            {
                m_certVerify.SetPrivateCA(m_cacertFilename);
                m_log.Info("[MONEY RPC]: Initialise: Execute Authentication of Server. CA file is " + m_cacertFilename);
            }
            else
            {
                m_checkServerCert = false;
                m_log.Info("[MONEY RPC]: Initialise: CACertFilename is empty. Therefor, CheckServerCert is forced to false");
            }
            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(m_certVerify.ValidateServerCertificate);

            m_sessionDic = m_moneyCore.GetSessionDic();
            m_secureSessionDic = m_moneyCore.GetSecureSessionDic();
            m_webSessionDic = m_moneyCore.GetWebSessionDic();
            RegisterHandlers();
        }


        public void PostInitialise()
        {
        }


        public void RegisterHandlers()
        {
            m_httpServer = m_moneyCore.GetHttpServer();
            m_httpServer.AddXmlRPCHandler("ClientLogin", handleClientLogin);
            m_httpServer.AddXmlRPCHandler("ClientLogout", handleClientLogout);
            m_httpServer.AddXmlRPCHandler("GetBalance", handleGetBalance);
            m_httpServer.AddXmlRPCHandler("GetTransaction", handleGetTransaction);

            m_httpServer.AddXmlRPCHandler("CancelTransfer", handleCancelTransfer);
            m_httpServer.AddXmlRPCHandler("TransferMoney", handleTransaction);
            m_httpServer.AddXmlRPCHandler("ForceTransferMoney", handleForceTransaction);        // added
            m_httpServer.AddXmlRPCHandler("PayMoneyCharge", handlePayMoneyCharge);              // added
            m_httpServer.AddXmlRPCHandler("AddBankerMoney", handleAddBankerMoney);              // added
            m_httpServer.AddXmlRPCHandler("SendMoney", handleScriptTransaction);                // added
            m_httpServer.AddXmlRPCHandler("MoveMoney", handleScriptTransaction);                // added

            // this is from original DTL. not check yet.
            m_httpServer.AddXmlRPCHandler("WebLogin", handleWebLogin);
            m_httpServer.AddXmlRPCHandler("WebLogout", handleWebLogout);
            m_httpServer.AddXmlRPCHandler("WebGetBalance", handleWebGetBalance);
            m_httpServer.AddXmlRPCHandler("WebGetTransaction", handleWebGetTransaction);
            m_httpServer.AddXmlRPCHandler("WebGetTransactionNum", handleWebGetTransactionNum);

            // Land Buy Test
            m_httpServer.AddXmlRPCHandler("preflightBuyLandPrep", preflightBuyLandPrep_func);
            m_httpServer.AddXmlRPCHandler("buyLandPrep", landBuy_func);

            // Currency Buy Test
            m_httpServer.AddXmlRPCHandler("getCurrencyQuote", quote_func);
            m_httpServer.AddXmlRPCHandler("buyCurrency", buy_func);
        }


        private XmlRpcResponse buy_func(XmlRpcRequest request, IPEndPoint client)
        {
            m_log.InfoFormat("[MONEY RPC]: handleClient buyCurrency.");
            throw new NotImplementedException();
        }


        private XmlRpcResponse quote_func(XmlRpcRequest request, IPEndPoint client)
        {
            m_log.InfoFormat("[MONEY RPC]: handleClient getCurrencyQuote.");
            throw new NotImplementedException();
        }


        private XmlRpcResponse landBuy_func(XmlRpcRequest request, IPEndPoint client)
        {
            m_log.InfoFormat("[MONEY RPC]: handleClient buyLandPrep.");
            throw new NotImplementedException();
        }


        private XmlRpcResponse preflightBuyLandPrep_func(XmlRpcRequest request, IPEndPoint client)
        {
            m_log.InfoFormat("[MONEY RPC]: handleClient preflightBuyLandPrep.");
            throw new NotImplementedException();
        }


        //
        public string GetSSLCommonName(XmlRpcRequest request)
        {
            if (request.Params.Count > 5)
            {
                m_sslCommonName = (string)request.Params[5];
            }
            else if (request.Params.Count == 5)
            {
                m_sslCommonName = (string)request.Params[4];
                if (m_sslCommonName == "gridproxy") m_sslCommonName = "";
            }
            else
            {
                m_sslCommonName = "";
            }
            return m_sslCommonName;
        }


        //
        public string GetSSLCommonName()
        {
            return m_sslCommonName;
        }


        /// <summary>
        /// Get the user balance when user entering a parcel.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse handleClientLogin(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            m_log.InfoFormat("[MONEY RPC]: handleClientLogin:");

            GetSSLCommonName(request);

            Hashtable requestData = (Hashtable)request.Params[0];
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            responseData["success"] = false;
            responseData["clientBalance"] = 0;

            // Check Client Cert
            if (m_moneyCore.IsCheckClientCert())
            {
                string commonName = GetSSLCommonName();
                if (commonName == "")
                {
                    m_log.ErrorFormat("[MONEY RPC]: handleClientLogin: Warnning: Check Client Cert is set, but SSL Common Name is empty.");
                    responseData["success"] = false;
                    responseData["description"] = "SSL Common Name is empty";
                    return response;
                }
                else
                {
                    m_log.InfoFormat("[MONEY RPC]: handleClientLogin: SSL Common Name is {0}", commonName);
                }

            }

            string universalID = string.Empty;
            string clientUUID = string.Empty;
            string sessionID = string.Empty;
            string secureID = string.Empty;
            string simIP = string.Empty;
            string userName = string.Empty;
            int balance = 0;
            int avatarType = (int)AvatarType.UNKNOWN_AVATAR;
            int avatarClass = (int)AvatarType.UNKNOWN_AVATAR;

            if (requestData.ContainsKey("clientUUID")) clientUUID = (string)requestData["clientUUID"];
            if (requestData.ContainsKey("clientSessionID")) sessionID = (string)requestData["clientSessionID"];
            if (requestData.ContainsKey("clientSecureSessionID")) secureID = (string)requestData["clientSecureSessionID"];
            if (requestData.ContainsKey("universalID")) universalID = (string)requestData["universalID"];
            if (requestData.ContainsKey("userName")) userName = (string)requestData["userName"];
            if (requestData.ContainsKey("openSimServIP")) simIP = (string)requestData["openSimServIP"];
            if (requestData.ContainsKey("avatarType")) avatarType = Convert.ToInt32(requestData["avatarType"]);
            if (requestData.ContainsKey("avatarClass")) avatarClass = Convert.ToInt32(requestData["avatarClass"]);

            //
            string firstName = string.Empty;
            string lastName = string.Empty;
            string serverURL = string.Empty;
            string securePsw = string.Empty;
            //
            if (!String.IsNullOrEmpty(universalID))
            {
                UUID uuid;
                Util.ParseUniversalUserIdentifier(universalID, out uuid, out serverURL, out firstName, out lastName, out securePsw);
            }
            if (String.IsNullOrEmpty(userName))
            {
                userName = firstName + " " + lastName;
            }

            // Information from DB
            UserInfo userInfo = m_moneyDBService.FetchUserInfo(clientUUID);
            if (userInfo != null)
            {
                avatarType = userInfo.Type;     // Avatar Type is not updated
                if (avatarType == (int)AvatarType.LOCAL_AVATAR) avatarClass = (int)AvatarType.LOCAL_AVATAR;
                if (avatarClass == (int)AvatarType.UNKNOWN_AVATAR) avatarClass = userInfo.Class;
                if (String.IsNullOrEmpty(userName)) userName = userInfo.Avatar;
            }
            //
            if (avatarType == (int)AvatarType.UNKNOWN_AVATAR) avatarType = avatarClass;
            if (String.IsNullOrEmpty(serverURL)) avatarClass = (int)AvatarType.NPC_AVATAR;

            m_log.InfoFormat("[MONEY RPC]: handleClientLogon: Avatar {0} ({1}) is logged on.", userName, clientUUID);
            m_log.InfoFormat("[MONEY RPC]: handleClientLogon: Avatar Type is {0} and Avatar Class is {1}", avatarType, avatarClass);

            //if (String.IsNullOrEmpty(serverURL)) {
            //	responseData["description"] = "Server URL is empty. Avatar is a NPC?";
            //	m_log.InfoFormat("[MONEY RPC]: handleClientLogon: {0}", responseData["description"]);
            //	return response;
            //}

            //
            // Check Avatar
            if (avatarClass == (int)AvatarType.GUEST_AVATAR && !m_gst_enable)
            {
                responseData["description"] = "Avatar is a Guest avatar. But this Money Server does not support Guest avatars.";
                m_log.InfoFormat("[MONEY RPC]: handleClientLogon: {0}", responseData["description"]);
                return response;
            }
            else if (avatarClass == (int)AvatarType.HG_AVATAR && !m_hg_enable)
            {
                responseData["description"] = "Avatar is a HG avatar. But this Money Server does not support HG avatars.";
                m_log.InfoFormat("[MONEY RPC]: handleClientLogon: {0}", responseData["description"]);
                return response;
            }
            else if (avatarClass == (int)AvatarType.FOREIGN_AVATAR)
            {
                responseData["description"] = "Avatar is a Foreign avatar.";
                m_log.InfoFormat("[MONEY RPC]: handleClientLogon: {0}", responseData["description"]);
                return response;
            }
            else if (avatarClass == (int)AvatarType.UNKNOWN_AVATAR)
            {
                responseData["description"] = "Avatar is a Unknown avatar.";
                m_log.InfoFormat("[MONEY RPC]: handleClientLogon: {0}", responseData["description"]);
                return response;
            }
            // NPC
            else if (avatarClass == (int)AvatarType.NPC_AVATAR)
            {
                responseData["success"] = true;
                responseData["clientBalance"] = 0;
                responseData["description"] = "Avatar is a NPC.";
                m_log.InfoFormat("[MONEY RPC]: handleClientLogon: {0}", responseData["description"]);
                return response;
            }

            //
            //Update the session and secure session dictionary
            lock (m_sessionDic)
            {
                if (!m_sessionDic.ContainsKey(clientUUID))
                {
                    m_sessionDic.Add(clientUUID, sessionID);
                }
                else m_sessionDic[clientUUID] = sessionID;
            }
            lock (m_secureSessionDic)
            {
                if (!m_secureSessionDic.ContainsKey(clientUUID))
                {
                    m_secureSessionDic.Add(clientUUID, secureID);
                }
                else m_secureSessionDic[clientUUID] = secureID;
            }

            //
            try
            {
                if (userInfo == null) userInfo = new UserInfo();
                userInfo.UserID = clientUUID;
                userInfo.SimIP = simIP;
                userInfo.Avatar = userName;
                userInfo.PswHash = UUID.Zero.ToString();
                userInfo.Type = avatarType;
                userInfo.Class = avatarClass;
                userInfo.ServerURL = serverURL;
                if (!String.IsNullOrEmpty(securePsw)) userInfo.PswHash = securePsw;

                if (!m_moneyDBService.TryAddUserInfo(userInfo))
                {
                    m_log.ErrorFormat("[MONEY RPC]: handleClientLogin: Unable to refresh information for user \"{0}\" in DB.", userName);
                    responseData["success"] = true;         // for FireStorm
                    responseData["description"] = "Update or add user information to db failed";
                    return response;
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[MONEY RPC]: handleClientLogin: Can't update userinfo for user {0}: {1}", clientUUID, e.ToString());
                responseData["description"] = "Exception occured" + e.ToString();
                return response;
            }

            //
            try
            {
                balance = m_moneyDBService.getBalance(clientUUID);

                //add user to balances table if not exist. (if balance is -1, it means avatar is not exist at balances table)
                if (balance == -1)
                {
                    int default_balance = m_defaultBalance;
                    if (avatarClass == (int)AvatarType.HG_AVATAR) default_balance = m_hg_defaultBalance;
                    if (avatarClass == (int)AvatarType.GUEST_AVATAR) default_balance = m_gst_defaultBalance;

                    if (m_moneyDBService.addUser(clientUUID, default_balance, 0, avatarType))
                    {
                        responseData["success"] = true;
                        responseData["description"] = "add user successfully";
                        responseData["clientBalance"] = default_balance;
                    }
                    else
                    {
                        responseData["description"] = "add user failed";
                    }
                }
                //Success
                else if (balance >= 0)
                {
                    responseData["success"] = true;
                    responseData["description"] = "get user balance successfully";
                    responseData["clientBalance"] = balance;
                }

                return response;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[MONEY RPC]: handleClientLogin: Can't get balance of user {0}: {1}", clientUUID, e.ToString());
                responseData["description"] = "Exception occured" + e.ToString();
            }

            return response;
        }


        //
        public XmlRpcResponse handleClientLogout(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            //m_log.InfoFormat("[MONEY RPC]: handleClientLogout:");

            GetSSLCommonName(request);

            Hashtable requestData = (Hashtable)request.Params[0];
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            string clientUUID = string.Empty;
            if (requestData.ContainsKey("clientUUID")) clientUUID = (string)requestData["clientUUID"];

            m_log.InfoFormat("[MONEY RPC]: handleClientLogout: User {0} is logging off.", clientUUID);
            try
            {
                lock (m_sessionDic)
                {
                    if (m_sessionDic.ContainsKey(clientUUID))
                    {
                        m_sessionDic.Remove(clientUUID);
                    }
                }

                lock (m_secureSessionDic)
                {
                    if (m_secureSessionDic.ContainsKey(clientUUID))
                    {
                        m_secureSessionDic.Remove(clientUUID);
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[MONEY RPC]: handleClientLogout: Failed to delete user session: " + e.ToString());
                responseData["success"] = false;
                return response;
            }

            responseData["success"] = true;
            return response;
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        //
        /// <summary>
        /// handle incoming transaction
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse handleTransaction(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            //m_log.InfoFormat("[MONEY RPC]: handleTransaction:");

            GetSSLCommonName(request);

            Hashtable requestData = (Hashtable)request.Params[0];
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            int amount = 0;
            int transactionType = 0;
            string senderID = string.Empty;
            string receiverID = string.Empty;
            string senderSessionID = string.Empty;
            string senderSecureSessionID = string.Empty;
            string objectID = string.Empty;
            string objectName = string.Empty;
            string regionHandle = string.Empty;
            string regionUUID = string.Empty;
            string description = "Newly added on";

            responseData["success"] = false;
            UUID transactionUUID = UUID.Random();

            if (requestData.ContainsKey("senderID")) senderID = (string)requestData["senderID"];
            if (requestData.ContainsKey("receiverID")) receiverID = (string)requestData["receiverID"];
            if (requestData.ContainsKey("senderSessionID")) senderSessionID = (string)requestData["senderSessionID"];
            if (requestData.ContainsKey("senderSecureSessionID")) senderSecureSessionID = (string)requestData["senderSecureSessionID"];
            if (requestData.ContainsKey("amount")) amount = Convert.ToInt32(requestData["amount"]);
            if (requestData.ContainsKey("objectID")) objectID = (string)requestData["objectID"];
            if (requestData.ContainsKey("objectName")) objectName = (string)requestData["objectName"];
            if (requestData.ContainsKey("regionHandle")) regionHandle = (string)requestData["regionHandle"];
            if (requestData.ContainsKey("regionUUID")) regionUUID = (string)requestData["regionUUID"];
            if (requestData.ContainsKey("transactionType")) transactionType = Convert.ToInt32(requestData["transactionType"]);
            if (requestData.ContainsKey("description")) description = (string)requestData["description"];

            m_log.InfoFormat("[MONEY RPC]: handleTransaction: Transfering money from {0} to {1}, Amount = {2}", senderID, receiverID, amount);
            m_log.InfoFormat("[MONEY RPC]: handleTransaction: Object ID = {0}, Object Name = {1}", objectID, objectName);

            if (m_sessionDic.ContainsKey(senderID) && m_secureSessionDic.ContainsKey(senderID))
            {
                if (m_sessionDic[senderID] == senderSessionID && m_secureSessionDic[senderID] == senderSecureSessionID)
                {
                    m_log.InfoFormat("[MONEY RPC]: handleTransaction: Transfering money from {0} to {1}", senderID, receiverID);
                    int time = (int)((DateTime.UtcNow.Ticks - TicksToEpoch) / 10000000);
                    try
                    {
                        TransactionData transaction = new TransactionData();
                        transaction.TransUUID = transactionUUID;
                        transaction.Sender = senderID;
                        transaction.Receiver = receiverID;
                        transaction.Amount = amount;
                        transaction.ObjectUUID = objectID;
                        transaction.ObjectName = objectName;
                        transaction.RegionHandle = regionHandle;
                        transaction.RegionUUID = regionUUID;
                        transaction.Type = transactionType;
                        transaction.Time = time;
                        transaction.SecureCode = UUID.Random().ToString();
                        transaction.Status = (int)Status.PENDING_STATUS;
                        transaction.CommonName = GetSSLCommonName();
                        transaction.Description = description + " " + DateTime.UtcNow.ToString();

                        UserInfo rcvr = m_moneyDBService.FetchUserInfo(receiverID);
                        if (rcvr == null)
                        {
                            m_log.ErrorFormat("[MONEY RPC]: handleTransaction: Receive User is not yet in DB {0}", receiverID);
                            return response;
                        }

                        bool result = m_moneyDBService.addTransaction(transaction);
                        if (result)
                        {
                            UserInfo user = m_moneyDBService.FetchUserInfo(senderID);
                            if (user != null)
                            {
                                if (amount > 0 || (m_enableAmountZero && amount == 0))
                                {
                                    string snd_message = "";
                                    string rcv_message = "";

                                    if (transaction.Type == (int)TransactionType.Gift)
                                    {
                                        snd_message = m_BalanceMessageSendGift;
                                        rcv_message = m_BalanceMessageReceiveGift;
                                    }
                                    else if (transaction.Type == (int)TransactionType.LandSale)
                                    {
                                        snd_message = m_BalanceMessageLandSale;
                                        rcv_message = m_BalanceMessageRcvLandSale;
                                    }
                                    else if (transaction.Type == (int)TransactionType.PayObject)
                                    {
                                        snd_message = m_BalanceMessageBuyObject;
                                        rcv_message = m_BalanceMessageSellObject;
                                    }
                                    else if (transaction.Type == (int)TransactionType.ObjectPays)
                                    {       // ObjectGiveMoney
                                        rcv_message = m_BalanceMessageGetMoney;
                                    }

                                    responseData["success"] = NotifyTransfer(transactionUUID, snd_message, rcv_message, objectName);
                                }
                                else if (amount == 0)
                                {
                                    responseData["success"] = true;     // No messages for L$0 object. by Fumi.Iseki
                                }
                                return response;
                            }
                        }
                        else
                        {   // add transaction failed
                            m_log.ErrorFormat("[MONEY RPC]: handleTransaction: Add transaction for user {0} failed.", senderID);
                        }
                        return response;
                    }
                    catch (Exception e)
                    {
                        m_log.Error("[MONEY RPC]: handleTransaction: Exception occurred while adding transaction: " + e.ToString());
                    }
                    return response;
                }
            }

            m_log.Error("[MONEY RPC]: handleTransaction: Session authentication failure for sender " + senderID);
            responseData["message"] = "Session check failure, please re-login later!";
            return response;
        }


        //
        // added by Fumi.Iseki
        //
        /// <summary>
        /// handle incoming force transaction. no check senderSessionID and senderSecureSessionID
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse handleForceTransaction(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            //m_log.InfoFormat("[MONEY RPC]: handleForceTransaction:");

            GetSSLCommonName(request);

            Hashtable requestData = (Hashtable)request.Params[0];
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            int amount = 0;
            int transactionType = 0;
            string senderID = string.Empty;
            string receiverID = string.Empty;
            string objectID = string.Empty;
            string objectName = string.Empty;
            string regionHandle = string.Empty;
            string regionUUID = string.Empty;
            string description = "Newly added on";

            responseData["success"] = false;
            UUID transactionUUID = UUID.Random();

            //
            if (!m_forceTransfer)
            {
                m_log.Error("[MONEY RPC]: handleForceTransaction: Not allowed force transfer of Money.");
                m_log.Error("[MONEY RPC]: handleForceTransaction: Set enableForceTransfer at [MoneyServer] to true in MoneyServer.ini");
                responseData["message"] = "not allowed force transfer of Money!";
                return response;
            }

            if (requestData.ContainsKey("senderID")) senderID = (string)requestData["senderID"];
            if (requestData.ContainsKey("receiverID")) receiverID = (string)requestData["receiverID"];
            if (requestData.ContainsKey("amount")) amount = Convert.ToInt32(requestData["amount"]);
            if (requestData.ContainsKey("objectID")) objectID = (string)requestData["objectID"];
            if (requestData.ContainsKey("objectName")) objectName = (string)requestData["objectName"];
            if (requestData.ContainsKey("regionHandle")) regionHandle = (string)requestData["regionHandle"];
            if (requestData.ContainsKey("regionUUID")) regionUUID = (string)requestData["regionUUID"];
            if (requestData.ContainsKey("transactionType")) transactionType = Convert.ToInt32(requestData["transactionType"]);
            if (requestData.ContainsKey("description")) description = (string)requestData["description"];

            m_log.InfoFormat("[MONEY RPC]: handleForceTransaction: Force transfering money from {0} to {1}, Amount = {2}", senderID, receiverID, amount);
            m_log.InfoFormat("[MONEY RPC]: handleForceTransaction: Object ID = {0}, Object Name = {1}", objectID, objectName);

            int time = (int)((DateTime.UtcNow.Ticks - TicksToEpoch) / 10000000);

            try
            {
                TransactionData transaction = new TransactionData();
                transaction.TransUUID = transactionUUID;
                transaction.Sender = senderID;
                transaction.Receiver = receiverID;
                transaction.Amount = amount;
                transaction.ObjectUUID = objectID;
                transaction.ObjectName = objectName;
                transaction.RegionHandle = regionHandle;
                transaction.RegionUUID = regionUUID;
                transaction.Type = transactionType;
                transaction.Time = time;
                transaction.SecureCode = UUID.Random().ToString();
                transaction.Status = (int)Status.PENDING_STATUS;
                transaction.CommonName = GetSSLCommonName();
                transaction.Description = description + " " + DateTime.UtcNow.ToString();

                UserInfo rcvr = m_moneyDBService.FetchUserInfo(receiverID);
                if (rcvr == null)
                {
                    m_log.ErrorFormat("[MONEY RPC]: handleForceTransaction: Force receive User is not yet in DB {0}", receiverID);
                    return response;
                }

                bool result = m_moneyDBService.addTransaction(transaction);
                if (result)
                {
                    UserInfo user = m_moneyDBService.FetchUserInfo(senderID);
                    if (user != null)
                    {
                        if (amount > 0 || (m_enableAmountZero && amount == 0))
                        {
                            string snd_message = "";
                            string rcv_message = "";

                            if (transaction.Type == (int)TransactionType.Gift)
                            {
                                snd_message = m_BalanceMessageSendGift;
                                rcv_message = m_BalanceMessageReceiveGift;
                            }
                            else if (transaction.Type == (int)TransactionType.LandSale)
                            {
                                snd_message = m_BalanceMessageLandSale;
                                snd_message = m_BalanceMessageRcvLandSale;
                            }
                            else if (transaction.Type == (int)TransactionType.PayObject)
                            {
                                snd_message = m_BalanceMessageBuyObject;
                                rcv_message = m_BalanceMessageSellObject;
                            }
                            else if (transaction.Type == (int)TransactionType.ObjectPays)
                            {       // ObjectGiveMoney
                                rcv_message = m_BalanceMessageGetMoney;
                            }

                            responseData["success"] = NotifyTransfer(transactionUUID, snd_message, rcv_message, objectName);
                        }
                        else if (amount == 0)
                        {
                            responseData["success"] = true;     // No messages for L$0 object. by Fumi.Iseki
                        }
                        return response;
                    }
                }
                else
                {   // add transaction failed
                    m_log.ErrorFormat("[MONEY RPC]: handleForceTransaction: Add force transaction for user {0} failed.", senderID);
                }
                return response;
            }
            catch (Exception e)
            {
                m_log.Error("[MONEY RPC]: handleForceTransaction: Exception occurred while adding force transaction: " + e.ToString());
            }
            return response;
        }


        //
        // added by Fumi.Iseki
        //
        /// <summary>
        /// handle scripted sending money transaction.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse handleScriptTransaction(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            //m_log.InfoFormat("[MONEY RPC]: handleScriptTransaction:");

            GetSSLCommonName(request);

            Hashtable requestData = (Hashtable)request.Params[0];
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            int amount = 0;
            int transactionType = 0;
            string senderID = UUID.Zero.ToString();
            string receiverID = UUID.Zero.ToString();
            string clientIP = remoteClient.Address.ToString();
            string secretCode = string.Empty;
            string description = "Scripted Send Money from/to Avatar on";

            responseData["success"] = false;
            UUID transactionUUID = UUID.Random();

            if (!m_scriptSendMoney || m_scriptAccessKey == "")
            {
                m_log.Error("[MONEY RPC]: handleScriptTransaction: Not allowed send money to avatar!!");
                m_log.Error("[MONEY RPC]: handleScriptTransaction: Set enableScriptSendMoney and MoneyScriptAccessKey at [MoneyServer] in MoneyServer.ini");
                responseData["message"] = "not allowed set money to avatar!";
                return response;
            }

            if (requestData.ContainsKey("senderID")) senderID = (string)requestData["senderID"];
            if (requestData.ContainsKey("receiverID")) receiverID = (string)requestData["receiverID"];
            if (requestData.ContainsKey("amount")) amount = Convert.ToInt32(requestData["amount"]);
            if (requestData.ContainsKey("transactionType")) transactionType = Convert.ToInt32(requestData["transactionType"]);
            if (requestData.ContainsKey("description")) description = (string)requestData["description"];
            if (requestData.ContainsKey("secretAccessCode")) secretCode = (string)requestData["secretAccessCode"];

            MD5 md5 = MD5.Create();
            byte[] code = md5.ComputeHash(ASCIIEncoding.Default.GetBytes(m_scriptAccessKey + "_" + clientIP));
            string hash = BitConverter.ToString(code).ToLower().Replace("-", "");
            code = md5.ComputeHash(ASCIIEncoding.Default.GetBytes(hash + "_" + m_scriptIPaddress));
            hash = BitConverter.ToString(code).ToLower().Replace("-", "");

            if (secretCode.ToLower() != hash)
            {
                m_log.Error("[MONEY RPC]: handleScriptTransaction: Not allowed send money to avatar!!");
                m_log.Error("[MONEY RPC]: handleScriptTransaction: Not match Script Access Key.");
                responseData["message"] = "not allowed send money to avatar! not match Script Key";
                return response;
            }

            m_log.InfoFormat("[MONEY RPC]: handleScriptTransaction: Send money from {0} to {1}", senderID, receiverID);
            int time = (int)((DateTime.UtcNow.Ticks - TicksToEpoch) / 10000000);

            try
            {
                TransactionData transaction = new TransactionData();
                transaction.TransUUID = transactionUUID;
                transaction.Sender = senderID;
                transaction.Receiver = receiverID;
                transaction.Amount = amount;
                transaction.ObjectUUID = UUID.Zero.ToString();
                transaction.RegionHandle = "0";
                transaction.Type = transactionType;
                transaction.Time = time;
                transaction.SecureCode = UUID.Random().ToString();
                transaction.Status = (int)Status.PENDING_STATUS;
                transaction.CommonName = GetSSLCommonName();
                transaction.Description = description + " " + DateTime.UtcNow.ToString();

                UserInfo senderInfo = null;
                UserInfo receiverInfo = null;
                if (transaction.Sender != UUID.Zero.ToString()) senderInfo = m_moneyDBService.FetchUserInfo(transaction.Sender);
                if (transaction.Receiver != UUID.Zero.ToString()) receiverInfo = m_moneyDBService.FetchUserInfo(transaction.Receiver);

                if (senderInfo == null && receiverInfo == null)
                {
                    m_log.ErrorFormat("[MONEY RPC]: handleScriptTransaction: Sender and Receiver are not yet in DB, or both of them are System: {0}, {1}",
                                                                                                                transaction.Sender, transaction.Receiver);
                    return response;
                }

                bool result = m_moneyDBService.addTransaction(transaction);
                if (result)
                {
                    if (amount > 0 || (m_enableAmountZero && amount == 0))
                    {
                        if (m_moneyDBService.DoTransfer(transactionUUID))
                        {
                            transaction = m_moneyDBService.FetchTransaction(transactionUUID);
                            if (transaction != null && transaction.Status == (int)Status.SUCCESS_STATUS)
                            {
                                m_log.InfoFormat("[MONEY RPC]: handleScriptTransaction: ScriptTransaction money finished successfully, now update balance {0}",
                                                                                                                            transactionUUID.ToString());
                                string message = string.Empty;
                                if (senderInfo != null)
                                {
                                    if (receiverInfo == null) message = string.Format(m_BalanceMessageSendMoney, amount, "SYSTEM", "");
                                    else message = string.Format(m_BalanceMessageSendMoney, amount, receiverInfo.Avatar, "");
                                    UpdateBalance(transaction.Sender, message);
                                    m_log.InfoFormat("[MONEY RPC]: handleScriptTransaction: Update balance of {0}. Message = {1}", transaction.Sender, message);
                                }
                                if (receiverInfo != null)
                                {
                                    if (senderInfo == null) message = string.Format(m_BalanceMessageReceiveMoney, amount, "SYSTEM", "");
                                    else message = string.Format(m_BalanceMessageReceiveMoney, amount, senderInfo.Avatar, "");
                                    UpdateBalance(transaction.Receiver, message);
                                    m_log.InfoFormat("[MONEY RPC]: handleScriptTransaction: Update balance of {0}. Message = {1}", transaction.Receiver, message);
                                }


                                responseData["success"] = true;
                            }
                        }
                    }
                    else if (amount == 0)
                    {
                        responseData["success"] = true;     // No messages for L$0 add
                    }
                    return response;
                }
                else
                {   // add transaction failed
                    m_log.ErrorFormat("[MONEY RPC]: handleScriptTransaction: Add force transaction for user {0} failed.", transaction.Sender);
                }
                return response;
            }
            catch (Exception e)
            {
                m_log.Error("[MONEY RPC]: handleScriptTransaction: Exception occurred while adding money transaction: " + e.ToString());
            }
            return response;
        }


        //
        // added by Fumi.Iseki
        //
        /// <summary>
        /// handle adding money transaction.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse handleAddBankerMoney(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            //m_log.InfoFormat("[MONEY RPC]: handleAddBankerMoney:");

            GetSSLCommonName(request);

            Hashtable requestData = (Hashtable)request.Params[0];
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            int amount = 0;
            int transactionType = 0;
            string senderID = UUID.Zero.ToString();
            string bankerID = string.Empty;
            string regionHandle = "0";
            string regionUUID = UUID.Zero.ToString();
            string description = "Add Money to Avatar on";

            responseData["success"] = false;
            UUID transactionUUID = UUID.Random();

            if (requestData.ContainsKey("bankerID")) bankerID = (string)requestData["bankerID"];
            if (requestData.ContainsKey("amount")) amount = Convert.ToInt32(requestData["amount"]);
            if (requestData.ContainsKey("regionHandle")) regionHandle = (string)requestData["regionHandle"];
            if (requestData.ContainsKey("regionUUID")) regionUUID = (string)requestData["regionUUID"];
            if (requestData.ContainsKey("transactionType")) transactionType = Convert.ToInt32(requestData["transactionType"]);
            if (requestData.ContainsKey("description")) description = (string)requestData["description"];

            // Check Banker Avatar
            if (m_bankerAvatar != UUID.Zero.ToString() && m_bankerAvatar != bankerID)
            {
                m_log.Error("[MONEY RPC]: handleAddBankerMoney: Not allowed add money to avatar!!");
                m_log.Error("[MONEY RPC]: handleAddBankerMoney: Set BankerAvatar at [MoneyServer] in MoneyServer.ini");
                responseData["message"] = "not allowed add money to avatar!";
                responseData["banker"] = false;
                return response;
            }
            responseData["banker"] = true;

            m_log.InfoFormat("[MONEY RPC]: handleAddBankerMoney: Add money to avatar {0}", bankerID);
            int time = (int)((DateTime.UtcNow.Ticks - TicksToEpoch) / 10000000);

            try
            {
                TransactionData transaction = new TransactionData();
                transaction.TransUUID = transactionUUID;
                transaction.Sender = senderID;
                transaction.Receiver = bankerID;
                transaction.Amount = amount;
                transaction.ObjectUUID = UUID.Zero.ToString();
                transaction.RegionHandle = regionHandle;
                transaction.RegionUUID = regionUUID;
                transaction.Type = transactionType;
                transaction.Time = time;
                transaction.SecureCode = UUID.Random().ToString();
                transaction.Status = (int)Status.PENDING_STATUS;
                transaction.CommonName = GetSSLCommonName();
                transaction.Description = description + " " + DateTime.UtcNow.ToString();

                UserInfo rcvr = m_moneyDBService.FetchUserInfo(bankerID);
                if (rcvr == null)
                {
                    m_log.ErrorFormat("[MONEY RPC]: handleAddBankerMoney: Avatar is not yet in DB {0}", bankerID);
                    return response;
                }

                bool result = m_moneyDBService.addTransaction(transaction);
                if (result)
                {
                    if (amount > 0 || (m_enableAmountZero && amount == 0))
                    {
                        if (m_moneyDBService.DoAddMoney(transactionUUID))
                        {
                            transaction = m_moneyDBService.FetchTransaction(transactionUUID);
                            if (transaction != null && transaction.Status == (int)Status.SUCCESS_STATUS)
                            {
                                m_log.InfoFormat("[MONEY RPC]: handleAddBankerMoney: Adding money finished successfully, now update balance: {0}",
                                                                                                                            transactionUUID.ToString());
                                string message = string.Format(m_BalanceMessageBuyMoney, amount, "SYSTEM", "");
                                UpdateBalance(transaction.Receiver, message);
                                responseData["success"] = true;
                            }
                        }
                    }
                    else if (amount == 0)
                    {
                        responseData["success"] = true;     // No messages for L$0 add
                    }
                    return response;
                }
                else
                {   // add transaction failed
                    m_log.ErrorFormat("[MONEY RPC]: handleAddBankerMoney: Add force transaction for user {0} failed.", senderID);
                }
                return response;
            }
            catch (Exception e)
            {
                m_log.Error("[MONEY RPC]: handleAddBankerMoney: Exception occurred while adding money transaction: " + e.ToString());
            }
            return response;
        }


        //
        // added by Fumi.Iseki
        //
        /// <summary>
        /// handle pay charge transaction. no check receiver information.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse handlePayMoneyCharge(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            //m_log.InfoFormat("[MONEY RPC]: handlePayMoneyCharge:");

            GetSSLCommonName(request);

            Hashtable requestData = (Hashtable)request.Params[0];
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            int amount = 0;
            int transactionType = 0;
            string senderID = string.Empty;
            string receiverID = UUID.Zero.ToString();
            string senderSessionID = string.Empty;
            string senderSecureSessionID = string.Empty;
            string objectID = UUID.Zero.ToString();
            string objectName = string.Empty;
            string regionHandle = string.Empty;
            string regionUUID = string.Empty;
            string description = "Pay Charge on";

            responseData["success"] = false;
            UUID transactionUUID = UUID.Random();

            if (requestData.ContainsKey("senderID")) senderID = (string)requestData["senderID"];
            if (requestData.ContainsKey("senderSessionID")) senderSessionID = (string)requestData["senderSessionID"];
            if (requestData.ContainsKey("senderSecureSessionID")) senderSecureSessionID = (string)requestData["senderSecureSessionID"];
            if (requestData.ContainsKey("amount")) amount = Convert.ToInt32(requestData["amount"]);
            if (requestData.ContainsKey("regionHandle")) regionHandle = (string)requestData["regionHandle"];
            if (requestData.ContainsKey("regionUUID")) regionUUID = (string)requestData["regionUUID"];
            if (requestData.ContainsKey("transactionType")) transactionType = Convert.ToInt32(requestData["transactionType"]);
            if (requestData.ContainsKey("description")) description = (string)requestData["description"];

            if (requestData.ContainsKey("receiverID")) receiverID = (string)requestData["receiverID"];
            if (requestData.ContainsKey("objectID")) objectID = (string)requestData["objectID"];
            if (requestData.ContainsKey("objectName")) objectName = (string)requestData["objectName"];

            m_log.InfoFormat("[MONEY RPC]: handlePayMoneyCharge: Transfering money from {0} to {1}, Amount = {2}", senderID, receiverID, amount);
            m_log.InfoFormat("[MONEY RPC]: handlePayMoneyCharge: Object ID = {0}, Object Name = {1}", objectID, objectName);

            if (m_sessionDic.ContainsKey(senderID) && m_secureSessionDic.ContainsKey(senderID))
            {
                if (m_sessionDic[senderID] == senderSessionID && m_secureSessionDic[senderID] == senderSecureSessionID)
                {
                    m_log.InfoFormat("[MONEY RPC]: handlePayMoneyCharge: Pay from {0}", senderID);
                    int time = (int)((DateTime.UtcNow.Ticks - TicksToEpoch) / 10000000);
                    try
                    {
                        TransactionData transaction = new TransactionData();
                        transaction.TransUUID = transactionUUID;
                        transaction.Sender = senderID;
                        transaction.Receiver = receiverID;
                        transaction.Amount = amount;
                        transaction.ObjectUUID = objectID;
                        transaction.ObjectName = objectName;
                        transaction.RegionHandle = regionHandle;
                        transaction.RegionUUID = regionUUID;
                        transaction.Type = transactionType;
                        transaction.Time = time;
                        transaction.SecureCode = UUID.Random().ToString();
                        transaction.Status = (int)Status.PENDING_STATUS;
                        transaction.CommonName = GetSSLCommonName();
                        transaction.Description = description + " " + DateTime.UtcNow.ToString();

                        bool result = m_moneyDBService.addTransaction(transaction);
                        if (result)
                        {
                            UserInfo user = m_moneyDBService.FetchUserInfo(senderID);
                            if (user != null)
                            {
                                if (amount > 0 || (m_enableAmountZero && amount == 0))
                                {
                                    string message = string.Format(m_BalanceMessagePayCharge, amount, "SYSTEM", "");
                                    responseData["success"] = NotifyTransfer(transactionUUID, message, "", "");
                                }
                                else if (amount == 0)
                                {
                                    responseData["success"] = true;     // No messages for L$0 object. by Fumi.Iseki
                                }
                                return response;
                            }
                        }
                        else
                        {   // add transaction failed
                            m_log.ErrorFormat("[MONEY RPC]: handlePayMoneyCharge: Pay money transaction for user {0} failed.", senderID);
                        }
                        return response;
                    }
                    catch (Exception e)
                    {
                        m_log.Error("[MONEY RPC]: handlePayMoneyCharge: Exception occurred while pay money transaction: " + e.ToString());
                    }
                    return response;
                }
            }

            m_log.Error("[MONEY RPC]: handlePayMoneyCharge: Session authentication failure for sender " + senderID);
            responseData["message"] = "Session check failure, please re-login later!";
            return response;
        }



        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        //
        //  added by Fumi.Iseki
        //
        /// <summary>
        /// Continue transaction with no confirm.
        /// </summary>
        /// <param name="transactionUUID"></param>
        /// <returns></returns>
        public bool NotifyTransfer(UUID transactionUUID, string msg2sender, string msg2receiver, string objectName)
        {
            //m_log.InfoFormat("[MONEY RPC]: NotifyTransfer: User has accepted the transaction, now continue with the transaction");

            try
            {
                if (m_moneyDBService.DoTransfer(transactionUUID))
                {
                    TransactionData transaction = m_moneyDBService.FetchTransaction(transactionUUID);
                    if (transaction != null && transaction.Status == (int)Status.SUCCESS_STATUS)
                    {
                        m_log.InfoFormat("[MONEY RPC]: NotifyTransfer: Transaction Type = {0}", transaction.Type);
                        m_log.InfoFormat("[MONEY RPC]: NotifyTransfer: Payment finished successfully, now update balance {0}", transactionUUID.ToString());

                        bool updateSender = true;
                        bool updateReceiv = true;
                        if (transaction.Sender == transaction.Receiver) updateSender = false;
                        //if (transaction.Type==(int)TransactionType.UploadCharge) return true;
                        if (transaction.Type == (int)TransactionType.UploadCharge) updateReceiv = false;

                        if (updateSender)
                        {
                            UserInfo receiverInfo = m_moneyDBService.FetchUserInfo(transaction.Receiver);
                            string receiverName = "unknown user";
                            if (receiverInfo != null) receiverName = receiverInfo.Avatar;
                            string snd_message = string.Format(msg2sender, transaction.Amount, receiverName, objectName);
                            UpdateBalance(transaction.Sender, snd_message);
                        }
                        if (updateReceiv)
                        {
                            UserInfo senderInfo = m_moneyDBService.FetchUserInfo(transaction.Sender);
                            string senderName = "unknown user";
                            if (senderInfo != null) senderName = senderInfo.Avatar;
                            string rcv_message = string.Format(msg2receiver, transaction.Amount, senderName, objectName);
                            UpdateBalance(transaction.Receiver, rcv_message);
                        }

                        // Notify to sender
                        if (transaction.Type == (int)TransactionType.PayObject)
                        {
                            m_log.InfoFormat("[MONEY RPC]: NotifyTransfer: Now notify opensim to give object to customer {0} ", transaction.Sender);
                            Hashtable requestTable = new Hashtable();
                            requestTable["clientUUID"] = transaction.Sender;
                            requestTable["receiverUUID"] = transaction.Receiver;

                            if (m_sessionDic.ContainsKey(transaction.Sender) && m_secureSessionDic.ContainsKey(transaction.Sender))
                            {
                                requestTable["clientSessionID"] = m_sessionDic[transaction.Sender];
                                requestTable["clientSecureSessionID"] = m_secureSessionDic[transaction.Sender];
                            }
                            else
                            {
                                requestTable["clientSessionID"] = UUID.Zero.ToString();
                                requestTable["clientSecureSessionID"] = UUID.Zero.ToString();
                            }
                            requestTable["transactionType"] = transaction.Type;
                            requestTable["amount"] = transaction.Amount;
                            requestTable["objectID"] = transaction.ObjectUUID;
                            requestTable["objectName"] = transaction.ObjectName;
                            requestTable["regionHandle"] = transaction.RegionHandle;

                            UserInfo user = m_moneyDBService.FetchUserInfo(transaction.Sender);
                            if (user != null)
                            {
                                Hashtable responseTable = genericCurrencyXMLRPCRequest(requestTable, "OnMoneyTransfered", user.SimIP);

                                if (responseTable != null && responseTable.ContainsKey("success"))
                                {
                                    //User not online or failed to get object ?
                                    if (!(bool)responseTable["success"])
                                    {
                                        m_log.ErrorFormat("[MONEY RPC]: NotifyTransfer: User {0} can't get the object, rolling back.", transaction.Sender);
                                        if (RollBackTransaction(transaction))
                                        {
                                            m_log.ErrorFormat("[MONEY RPC]: NotifyTransfer: Transaction {0} failed but roll back succeeded.", transactionUUID.ToString());
                                        }
                                        else
                                        {
                                            m_log.ErrorFormat("[MONEY RPC]: NotifyTransfer: Transaction {0} failed and roll back failed as well.",
                                                                                                                        transactionUUID.ToString());
                                        }
                                    }
                                    else
                                    {
                                        m_log.InfoFormat("[MONEY RPC]: NotifyTransfer: Transaction {0} finished successfully.", transactionUUID.ToString());
                                        return true;
                                    }
                                }
                            }
                            return false;
                        }
                        return true;
                    }
                }
                m_log.ErrorFormat("[MONEY RPC]: NotifyTransfer: Transaction {0} failed.", transactionUUID.ToString());
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[MONEY RPC]: NotifyTransfer: exception occurred when transaction {0}: {1}", transactionUUID.ToString(), e.ToString());
            }

            return false;
        }



        /// <summary>
        /// Get the user balance.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse handleGetBalance(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            //m_log.InfoFormat("[MONEY RPC]: handleGetBalance:");

            GetSSLCommonName(request);

            Hashtable requestData = (Hashtable)request.Params[0];
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            string clientUUID = string.Empty;
            string sessionID = string.Empty;
            string secureID = string.Empty;
            int balance;

            responseData["success"] = false;

            if (requestData.ContainsKey("clientUUID")) clientUUID = (string)requestData["clientUUID"];
            if (requestData.ContainsKey("clientSessionID")) sessionID = (string)requestData["clientSessionID"];
            if (requestData.ContainsKey("clientSecureSessionID")) secureID = (string)requestData["clientSecureSessionID"];

            m_log.InfoFormat("[MONEY RPC]: handleGetBalance: Getting balance for user {0}", clientUUID);

            if (m_sessionDic.ContainsKey(clientUUID) && m_secureSessionDic.ContainsKey(clientUUID))
            {
                if (m_sessionDic[clientUUID] == sessionID && m_secureSessionDic[clientUUID] == secureID)
                {
                    try
                    {
                        balance = m_moneyDBService.getBalance(clientUUID);
                        if (balance == -1) // User not found
                        {
                            responseData["description"] = "user not found";
                            responseData["clientBalance"] = 0;
                        }
                        else if (balance >= 0)
                        {
                            responseData["success"] = true;
                            responseData["clientBalance"] = balance;
                        }

                        return response;
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[MONEY RPC]: handleGetBalance: Can't get balance for user {0}, Exception {1}", clientUUID, e.ToString());
                    }
                    return response;
                }
            }

            m_log.Error("[MONEY RPC]: handleGetBalance: Session authentication failed when getting balance for user " + clientUUID);
            responseData["description"] = "Session check failure, please re-login";
            return response;
        }


        /// <summary>   
        /// Generic XMLRPC client abstraction
        /// </summary>   
        /// <param name="ReqParams">Hashtable containing parameters to the method</param>   
        /// <param name="method">Method to invoke</param>   
        /// <returns>Hashtable with success=>bool and other values</returns>   
        private Hashtable genericCurrencyXMLRPCRequest(Hashtable reqParams, string method, string uri)
        {
            //m_log.InfoFormat("[MONEY RPC]: genericCurrencyXMLRPCRequest: to {0}", uri);

            if (reqParams.Count <= 0 || string.IsNullOrEmpty(method)) return null;

            if (m_checkServerCert)
            {
                if (!uri.StartsWith("https://"))
                {
                    m_log.InfoFormat("[MONEY RPC]: genericCurrencyXMLRPCRequest: CheckServerCert is true, but protocol is not HTTPS. Please check INI file.");
                    //return null; 
                }
            }
            else
            {
                if (!uri.StartsWith("https://") && !uri.StartsWith("http://"))
                {
                    m_log.ErrorFormat("[MONEY RPC]: genericCurrencyXMLRPCRequest: Invalid Region Server URL: {0}", uri);
                    return null;
                }
            }

            ArrayList arrayParams = new ArrayList();
            arrayParams.Add(reqParams);
            XmlRpcResponse moneyServResp = null;
            try
            {
                //XmlRpcRequest moneyModuleReq = new XmlRpcRequest(method, arrayParams);
                //moneyServResp = moneyModuleReq.Send(uri, MONEYMODULE_REQUEST_TIMEOUT);
                NSLXmlRpcRequest moneyModuleReq = new NSLXmlRpcRequest(method, arrayParams);
                moneyServResp = moneyModuleReq.certSend(uri, m_clientCert, m_checkServerCert, MONEYMODULE_REQUEST_TIMEOUT);
            }
            catch (Exception ex)
            {
                m_log.ErrorFormat("[MONEY RPC]: genericCurrencyXMLRPCRequest: Unable to connect to Region Server {0}", uri);
                m_log.ErrorFormat("[MONEY RPC]: genericCurrencyXMLRPCRequest: {0}", ex.ToString());

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Failed to perform actions on OpenSim Server";
                ErrorHash["errorURI"] = "";
                return ErrorHash;
            }

            if (moneyServResp == null || moneyServResp.IsFault)
            {
                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Failed to perform actions on OpenSim Server";
                ErrorHash["errorURI"] = "";
                return ErrorHash;
            }

            Hashtable moneyRespData = (Hashtable)moneyServResp.Value;
            return moneyRespData;
        }


        /// <summary>
        /// Update the client balance.We don't care about the result.
        /// </summary>
        /// <param name="userID"></param>
        private void UpdateBalance(string userID, string message)
        {
            //m_log.InfoFormat("[MONEY RPC]: UpdateBalance: ID = {0}, Message = {1}", userID, message);

            string sessionID = string.Empty;
            string secureID = string.Empty;

            if (m_sessionDic.ContainsKey(userID) && m_secureSessionDic.ContainsKey(userID))
            {
                sessionID = m_sessionDic[userID];
                secureID = m_secureSessionDic[userID];

                Hashtable requestTable = new Hashtable();
                requestTable["clientUUID"] = userID;
                requestTable["clientSessionID"] = sessionID;
                requestTable["clientSecureSessionID"] = secureID;
                requestTable["Balance"] = m_moneyDBService.getBalance(userID);
                if (message != "") requestTable["Message"] = message;

                UserInfo user = m_moneyDBService.FetchUserInfo(userID);
                if (user != null)
                {
                    genericCurrencyXMLRPCRequest(requestTable, "UpdateBalance", user.SimIP);
                    m_log.InfoFormat("[MONEY RPC]: UpdateBalance: Sended UpdateBalance Request to {0}", user.SimIP.ToString());
                }
            }
        }


        /// <summary>
        /// RollBack the transaction if user failed to get the object paid
        /// </summary>
        /// <param name="transaction"></param>
        /// <returns></returns>
        protected bool RollBackTransaction(TransactionData transaction)
        {
            //m_log.InfoFormat("[MONEY RPC]: RollBackTransaction:");

            if (m_moneyDBService.withdrawMoney(transaction.TransUUID, transaction.Receiver, transaction.Amount))
            {
                if (m_moneyDBService.giveMoney(transaction.TransUUID, transaction.Sender, transaction.Amount))
                {
                    m_log.InfoFormat("[MONEY RPC]: RollBackTransaction: Transaction {0} is successfully.", transaction.TransUUID.ToString());
                    m_moneyDBService.updateTransactionStatus(transaction.TransUUID, (int)Status.FAILED_STATUS,
                                                                    "The buyer failed to get the object, roll back the transaction");
                    UserInfo senderInfo = m_moneyDBService.FetchUserInfo(transaction.Sender);
                    UserInfo receiverInfo = m_moneyDBService.FetchUserInfo(transaction.Receiver);
                    string senderName = "unknown user";
                    string receiverName = "unknown user";
                    if (senderInfo != null) senderName = senderInfo.Avatar;
                    if (receiverInfo != null) receiverName = receiverInfo.Avatar;

                    string snd_message = string.Format(m_BalanceMessageRollBack, transaction.Amount, receiverName, transaction.ObjectName);
                    string rcv_message = string.Format(m_BalanceMessageRollBack, transaction.Amount, senderName, transaction.ObjectName);

                    if (transaction.Sender != transaction.Receiver) UpdateBalance(transaction.Sender, snd_message);
                    UpdateBalance(transaction.Receiver, rcv_message);
                    return true;
                }
            }
            return false;
        }


        //
        public XmlRpcResponse handleCancelTransfer(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            //m_log.InfoFormat("[MONEY RPC]: handleCancelTransfer:");

            GetSSLCommonName(request);

            Hashtable requestData = (Hashtable)request.Params[0];
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            string secureCode = string.Empty;
            string transactionID = string.Empty;
            UUID transactionUUID = UUID.Zero;

            responseData["success"] = false;

            if (requestData.ContainsKey("secureCode")) secureCode = (string)requestData["secureCode"];
            if (requestData.ContainsKey("transactionID"))
            {
                transactionID = (string)requestData["transactionID"];
                UUID.TryParse(transactionID, out transactionUUID);
            }

            if (string.IsNullOrEmpty(secureCode) || string.IsNullOrEmpty(transactionID))
            {
                m_log.Error("[MONEY RPC]: handleCancelTransfer: secureCode and/or transactionID are empty.");
                return response;
            }

            TransactionData transaction = m_moneyDBService.FetchTransaction(transactionUUID);
            UserInfo user = m_moneyDBService.FetchUserInfo(transaction.Sender);

            try
            {
                m_log.InfoFormat("[MONEY RPC]: handleCancelTransfer: User {0} wanted to cancel the transaction.", user.Avatar);
                if (m_moneyDBService.ValidateTransfer(secureCode, transactionUUID))
                {
                    m_log.InfoFormat("[MONEY RPC]: handleCancelTransfer: User {0} has canceled the transaction {1}", user.Avatar, transactionID);
                    m_moneyDBService.updateTransactionStatus(transactionUUID, (int)Status.FAILED_STATUS,
                                                            "User canceled the transaction on " + DateTime.UtcNow.ToString());
                    responseData["success"] = true;
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[MONEY RPC]: handleCancelTransfer: Exception occurred when transaction {0}: {1}", transactionID, e.ToString());
            }
            return response;
        }


        //
        public XmlRpcResponse handleGetTransaction(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            //m_log.InfoFormat("[MONEY RPC]: handleGetTransaction:");

            GetSSLCommonName(request);

            Hashtable requestData = (Hashtable)request.Params[0];
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            string clientID = string.Empty;
            string sessionID = string.Empty;
            string secureID = string.Empty;
            string transactionID = string.Empty;
            UUID transactionUUID = UUID.Zero;

            responseData["success"] = false;

            if (requestData.ContainsKey("clientUUID")) clientID = (string)requestData["clientUUID"];
            if (requestData.ContainsKey("clientSessionID")) sessionID = (string)requestData["clientSessionID"];
            if (requestData.ContainsKey("clientSecureSessionID")) secureID = (string)requestData["clientSecureSessionID"];

            if (requestData.ContainsKey("transactionID"))
            {
                transactionID = (string)requestData["transactionID"];
                UUID.TryParse(transactionID, out transactionUUID);
            }

            if (m_sessionDic.ContainsKey(clientID) && m_secureSessionDic.ContainsKey(clientID))
            {
                if (m_sessionDic[clientID] == sessionID && m_secureSessionDic[clientID] == secureID)
                {
                    //
                    if (string.IsNullOrEmpty(transactionID))
                    {
                        responseData["description"] = "TransactionID is empty";
                        m_log.Error("[MONEY RPC]: handleGetTransaction: TransactionID is empty.");
                        return response;
                    }

                    try
                    {
                        TransactionData transaction = m_moneyDBService.FetchTransaction(transactionUUID);
                        if (transaction != null)
                        {
                            responseData["success"] = true;
                            responseData["amount"] = transaction.Amount;
                            responseData["time"] = transaction.Time;
                            responseData["type"] = transaction.Type;
                            responseData["sender"] = transaction.Sender.ToString();
                            responseData["receiver"] = transaction.Receiver.ToString();
                            responseData["description"] = transaction.Description;
                        }
                        else
                        {
                            responseData["description"] = "Invalid Transaction UUID";
                        }

                        return response;
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[MONEY RPC]: handleGetTransaction: {0}", e.ToString());
                        m_log.ErrorFormat("[MONEY RPC]: handleGetTransaction: Can't get transaction information for {0}", transactionUUID.ToString());
                    }
                    return response;
                }
            }

            responseData["success"] = false;
            responseData["description"] = "Session check failure, please re-login";
            return response;
        }



        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //
        // In development
        //

        public XmlRpcResponse handleWebLogin(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            //m_log.InfoFormat("[MONEY RPC]: handleWebLogin:");

            GetSSLCommonName(request);

            Hashtable requestData = (Hashtable)request.Params[0];
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            string userID = string.Empty;
            string webSessionID = string.Empty;

            responseData["success"] = false;

            if (requestData.ContainsKey("userID")) userID = (string)requestData["userID"];
            if (requestData.ContainsKey("sessionID")) webSessionID = (string)requestData["sessionID"];

            if (string.IsNullOrEmpty(userID) || string.IsNullOrEmpty(webSessionID))
            {
                responseData["errorMessage"] = "userID or sessionID can`t be empty, login failed!";
                return response;
            }

            //Update the web session dictionary
            lock (m_webSessionDic)
            {
                if (!m_webSessionDic.ContainsKey(userID))
                {
                    m_webSessionDic.Add(userID, webSessionID);
                }
                else m_webSessionDic[userID] = webSessionID;
            }

            m_log.InfoFormat("[MONEY RPC]: handleWebLogin: User {0} has logged in from web.", userID);
            responseData["success"] = true;
            return response;
        }


        //
        public XmlRpcResponse handleWebLogout(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            //m_log.InfoFormat("[MONEY RPC]: handleWebLogout:");

            GetSSLCommonName(request);

            Hashtable requestData = (Hashtable)request.Params[0];
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            string userID = string.Empty;
            string webSessionID = string.Empty;

            responseData["success"] = false;

            if (requestData.ContainsKey("userID")) userID = (string)requestData["userID"];
            if (requestData.ContainsKey("sessionID")) webSessionID = (string)requestData["sessionID"];

            if (string.IsNullOrEmpty(userID) || string.IsNullOrEmpty(webSessionID))
            {
                responseData["errorMessage"] = "userID or sessionID can`t be empty, log out failed!";
                return response;
            }

            //Update the web session dictionary
            lock (m_webSessionDic)
            {
                if (m_webSessionDic.ContainsKey(userID))
                {
                    m_webSessionDic.Remove(userID);
                }
            }

            m_log.InfoFormat("[MONEY RPC]: handleWebLogout: User {0} has logged out from web.", userID);
            responseData["success"] = true;
            return response;
        }


        /// <summary>
        /// Get balance method for web pages.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse handleWebGetBalance(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            //m_log.InfoFormat("[MONEY RPC]: handleWebGetBalance:");

            GetSSLCommonName(request);

            Hashtable requestData = (Hashtable)request.Params[0];
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            string userID = string.Empty;
            string webSessionID = string.Empty;
            int balance = 0;

            responseData["success"] = false;

            if (requestData.ContainsKey("userID")) userID = (string)requestData["userID"];
            if (requestData.ContainsKey("sessionID")) webSessionID = (string)requestData["sessionID"];

            m_log.InfoFormat("[MONEY RPC]: handleWebGetBalance: Getting balance for user {0}", userID);

            //perform session check
            if (m_webSessionDic.ContainsKey(userID))
            {
                if (m_webSessionDic[userID] == webSessionID)
                {
                    try
                    {
                        balance = m_moneyDBService.getBalance(userID);
                        UserInfo user = m_moneyDBService.FetchUserInfo(userID);
                        if (user != null)
                        {
                            responseData["userName"] = user.Avatar;
                        }
                        else
                        {
                            responseData["userName"] = "unknown user";
                        }
                        //
                        // User not found
                        if (balance == -1)
                        {
                            responseData["errorMessage"] = "User not found";
                            responseData["balance"] = 0;
                        }
                        else if (balance >= 0)
                        {
                            responseData["success"] = true;
                            responseData["balance"] = balance;
                        }
                        return response;
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[MONEY RPC]: handleWebGetBalance: Can't get balance for user {0}, Exception {1}", userID, e.ToString());
                        responseData["errorMessage"] = "Exception occurred when getting balance";
                        return response;
                    }
                }
            }

            m_log.Error("[MONEY RPC]: handleWebLogout: Session authentication failed when getting balance for user " + userID);
            responseData["errorMessage"] = "Session check failure, please re-login";
            return response;
        }


        /// <summary>
        /// Get transaction for web pages
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse handleWebGetTransaction(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            //m_log.InfoFormat("[MONEY RPC]: handleWebGetTransaction:");

            GetSSLCommonName(request);

            Hashtable requestData = (Hashtable)request.Params[0];
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            string userID = string.Empty;
            string webSessionID = string.Empty;
            int lastIndex = -1;
            int startTime = 0;
            int endTime = 0;

            responseData["success"] = false;

            if (requestData.ContainsKey("userID")) userID = (string)requestData["userID"];
            if (requestData.ContainsKey("sessionID")) webSessionID = (string)requestData["sessionID"];
            if (requestData.ContainsKey("startTime")) startTime = (int)requestData["startTime"];
            if (requestData.ContainsKey("endTime")) endTime = (int)requestData["endTime"];
            if (requestData.ContainsKey("lastIndex")) lastIndex = (int)requestData["lastIndex"];

            if (m_webSessionDic.ContainsKey(userID))
            {
                if (m_webSessionDic[userID] == webSessionID)
                {
                    try
                    {
                        int total = m_moneyDBService.getTransactionNum(userID, startTime, endTime);
                        TransactionData tran = null;
                        m_log.InfoFormat("[MONEY RPC]: handleWebGetTransaction: Getting transation[{0}] for user {1}", lastIndex + 1, userID);
                        if (total > lastIndex + 2)
                        {
                            responseData["isEnd"] = false;
                        }
                        else
                        {
                            responseData["isEnd"] = true;
                        }

                        tran = m_moneyDBService.FetchTransaction(userID, startTime, endTime, lastIndex);
                        if (tran != null)
                        {
                            UserInfo senderInfo = m_moneyDBService.FetchUserInfo(tran.Sender);
                            UserInfo receiverInfo = m_moneyDBService.FetchUserInfo(tran.Receiver);
                            if (senderInfo != null && receiverInfo != null)
                            {
                                responseData["senderName"] = senderInfo.Avatar;
                                responseData["receiverName"] = receiverInfo.Avatar;
                            }
                            else
                            {
                                responseData["senderName"] = "unknown user";
                                responseData["receiverName"] = "unknown user";
                            }
                            responseData["success"] = true;
                            responseData["transactionIndex"] = lastIndex + 1;
                            responseData["transactionUUID"] = tran.TransUUID.ToString();
                            responseData["senderID"] = tran.Sender;
                            responseData["receiverID"] = tran.Receiver;
                            responseData["amount"] = tran.Amount;
                            responseData["type"] = tran.Type;
                            responseData["time"] = tran.Time;
                            responseData["status"] = tran.Status;
                            responseData["description"] = tran.Description;
                        }
                        else
                        {
                            responseData["errorMessage"] = string.Format("Unable to fetch transaction data with the index {0}", lastIndex + 1);
                        }
                        return response;
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat("[MONEY RPC]: handleWebGetTransaction: Can't get transaction for user {0}, Exception {1}", userID, e.ToString());
                        responseData["errorMessage"] = "Exception occurred when getting transaction";
                        return response;
                    }
                }
            }

            m_log.Error("[MONEY RPC]: handleWebGetTransaction: Session authentication failed when getting transaction for user " + userID);
            responseData["errorMessage"] = "Session check failure, please re-login";
            return response;
        }


        /// <summary>
        /// Get total number of transactions for web pages.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public XmlRpcResponse handleWebGetTransactionNum(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            //m_log.InfoFormat("[MONEY RPC]: handleWebGetTransactionNum:");

            GetSSLCommonName(request);

            Hashtable requestData = (Hashtable)request.Params[0];
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();
            response.Value = responseData;

            string userID = string.Empty;
            string webSessionID = string.Empty;
            int startTime = 0;
            int endTime = 0;

            responseData["success"] = false;

            if (requestData.ContainsKey("userID")) userID = (string)requestData["userID"];
            if (requestData.ContainsKey("sessionID")) webSessionID = (string)requestData["sessionID"];
            if (requestData.ContainsKey("startTime")) startTime = (int)requestData["startTime"];
            if (requestData.ContainsKey("endTime")) endTime = (int)requestData["endTime"];

            if (m_webSessionDic.ContainsKey(userID))
            {
                if (m_webSessionDic[userID] == webSessionID)
                {
                    int it = m_moneyDBService.getTransactionNum(userID, startTime, endTime);
                    if (it >= 0)
                    {
                        m_log.InfoFormat("[MONEY RPC]: handleWebGetTransactionNum: Get {0} transactions for user {1}", it, userID);
                        responseData["success"] = true;
                        responseData["number"] = it;
                    }
                    return response;
                }
            }

            m_log.Error("[MONEY RPC]: handleWebGetTransactionNum: Session authentication failed when getting transaction number for user " + userID);
            responseData["errorMessage"] = "Session check failure, please re-login";
            return response;
        }
    }

}

