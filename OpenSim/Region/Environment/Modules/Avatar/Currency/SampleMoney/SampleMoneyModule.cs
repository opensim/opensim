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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Xml;
using OpenMetaverse;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework.Communications.Cache;

namespace OpenSim.Region.Environment.Modules.Avatar.Currency.SampleMoney
{
    /// <summary>
    /// Demo Economy/Money Module.  This is not a production quality money/economy module!
    /// This is a demo for you to use when making one that works for you.
    ///  // To use the following you need to add:
    /// -helperuri <ADDRESS TO HERE OR grid MONEY SERVER>
    /// to the command line parameters you use to start up your client
    /// This commonly looks like -helperuri http://127.0.0.1:9000/
    ///
    /// Centralized grid structure example using OpenSimWi Redux revision 9+
    /// svn co https://opensimwiredux.svn.sourceforge.net/svnroot/opensimwiredux
    /// </summary>
    public class SampleMoneyModule : IMoneyModule, IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Where Stipends come from and Fees go to.
        /// </summary>
        // private UUID EconomyBaseAccount = UUID.Zero;

        private float EnergyEfficiency = 0f;
        private bool gridmode = false;
        // private ObjectPaid handerOnObjectPaid;
        private bool m_enabled = true;

        private IConfigSource m_gConfig;

        private bool m_keepMoneyAcrossLogins = true;
        private Dictionary<UUID, int> m_KnownClientFunds = new Dictionary<UUID, int>();
        // private string m_LandAddress = String.Empty;

        private int m_minFundsBeforeRefresh = 100;
        private string m_MoneyAddress = String.Empty;

        /// <summary>
        /// Region UUIDS indexed by AgentID
        /// </summary>
        private Dictionary<UUID, UUID> m_rootAgents = new Dictionary<UUID, UUID>();

        /// <summary>
        /// Scenes by Region Handle
        /// </summary>
        private Dictionary<ulong, Scene> m_scenel = new Dictionary<ulong, Scene>();

        private int m_stipend = 1000;

        private int ObjectCapacity = 45000;
        private int ObjectCount = 0;
        private int PriceEnergyUnit = 0;
        private int PriceGroupCreate = 0;
        private int PriceObjectClaim = 0;
        private float PriceObjectRent = 0f;
        private float PriceObjectScaleFactor = 0f;
        private int PriceParcelClaim = 0;
        private float PriceParcelClaimFactor = 0f;
        private int PriceParcelRent = 0;
        private int PricePublicObjectDecay = 0;
        private int PricePublicObjectDelete = 0;
        private int PriceRentLight = 0;
        private int PriceUpload = 0;
        private int TeleportMinPrice = 0;

        private float TeleportPriceExponent = 0f;
        // private int UserLevelPaysFees = 2;
        // private Scene XMLRPCHandler;

        #region IMoneyModule Members

        public event ObjectPaid OnObjectPaid;

        /// <summary>
        /// Startup
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="config"></param>
        public void Initialise(Scene scene, IConfigSource config)
        {
            m_gConfig = config;

            IConfig startupConfig = m_gConfig.Configs["Startup"];
            IConfig economyConfig = m_gConfig.Configs["Economy"];


            ReadConfigAndPopulate(scene, startupConfig, "Startup");
            ReadConfigAndPopulate(scene, economyConfig, "Economy");

            if (m_enabled)
            {
                scene.RegisterModuleInterface<IMoneyModule>(this);

                lock (m_scenel)
                {
                    if (m_scenel.Count == 0)
                    {
                        // XMLRPCHandler = scene;

                        // To use the following you need to add:
                        // -helperuri <ADDRESS TO HERE OR grid MONEY SERVER>
                        // to the command line parameters you use to start up your client
                        // This commonly looks like -helperuri http://127.0.0.1:9000/

                        if (m_MoneyAddress.Length > 0)
                        {
                            // Centralized grid structure using OpenSimWi Redux revision 9+
                            // https://opensimwiredux.svn.sourceforge.net/svnroot/opensimwiredux
                            scene.AddXmlRPCHandler("balanceUpdateRequest", GridMoneyUpdate);
                            scene.AddXmlRPCHandler("userAlert", UserAlert);
                        }
                        else
                        {
                            // Local Server..  enables functionality only.
                            scene.AddXmlRPCHandler("getCurrencyQuote", quote_func);
                            scene.AddXmlRPCHandler("buyCurrency", buy_func);
                            scene.AddXmlRPCHandler("preflightBuyLandPrep", preflightBuyLandPrep_func);
                            scene.AddXmlRPCHandler("buyLandPrep", landBuy_func);
                        }
                    }

                    if (m_scenel.ContainsKey(scene.RegionInfo.RegionHandle))
                    {
                        m_scenel[scene.RegionInfo.RegionHandle] = scene;
                    }
                    else
                    {
                        m_scenel.Add(scene.RegionInfo.RegionHandle, scene);
                    }
                }

                scene.EventManager.OnNewClient += OnNewClient;
                scene.EventManager.OnMoneyTransfer += MoneyTransferAction;
                scene.EventManager.OnClientClosed += ClientClosed;
                scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
                scene.EventManager.OnMakeChildAgent += MakeChildAgent;
                scene.EventManager.OnClientClosed += ClientLoggedOut;
                scene.EventManager.OnValidateLandBuy += ValidateLandBuy;
                scene.EventManager.OnLandBuy += processLandBuy;
                scene.EventManager.OnAvatarKilled += KillAvatar;
            }
        }

        public void ApplyUploadCharge(UUID agentID)
        {
        }

        public bool ObjectGiveMoney(UUID objectID, UUID fromID, UUID toID, int amount)
        {
            string description = String.Format("Object {0} pays {1}", resolveObjectName(objectID), resolveAgentName(toID));

            bool give_result = doMoneyTransfer(fromID, toID, amount, 2, description);

            if (m_MoneyAddress.Length == 0)
                BalanceUpdate(fromID, toID, give_result, description);

            return give_result;
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "BetaGridLikeMoneyModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        /// <summary>
        /// Parse Configuration
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="startupConfig"></param>
        /// <param name="config"></param>
        private void ReadConfigAndPopulate(Scene scene, IConfig startupConfig, string config)
        {
            if (config == "Startup" && startupConfig != null)
            {
                gridmode = startupConfig.GetBoolean("gridmode", false);
                m_enabled = (startupConfig.GetString("economymodule", "BetaGridLikeMoneyModule") == "BetaGridLikeMoneyModule");
            }

            if (config == "Economy" && startupConfig != null)
            {
                ObjectCapacity = startupConfig.GetInt("ObjectCapacity", 45000);
                PriceEnergyUnit = startupConfig.GetInt("PriceEnergyUnit", 100);
                PriceObjectClaim = startupConfig.GetInt("PriceObjectClaim", 10);
                PricePublicObjectDecay = startupConfig.GetInt("PricePublicObjectDecay", 4);
                PricePublicObjectDelete = startupConfig.GetInt("PricePublicObjectDelete", 4);
                PriceParcelClaim = startupConfig.GetInt("PriceParcelClaim", 1);
                PriceParcelClaimFactor = startupConfig.GetFloat("PriceParcelClaimFactor", 1f);
                PriceUpload = startupConfig.GetInt("PriceUpload", 0);
                PriceRentLight = startupConfig.GetInt("PriceRentLight", 5);
                TeleportMinPrice = startupConfig.GetInt("TeleportMinPrice", 2);
                TeleportPriceExponent = startupConfig.GetFloat("TeleportPriceExponent", 2f);
                EnergyEfficiency = startupConfig.GetFloat("EnergyEfficiency", 1);
                PriceObjectRent = startupConfig.GetFloat("PriceObjectRent", 1);
                PriceObjectScaleFactor = startupConfig.GetFloat("PriceObjectScaleFactor", 10);
                PriceParcelRent = startupConfig.GetInt("PriceParcelRent", 1);
                PriceGroupCreate = startupConfig.GetInt("PriceGroupCreate", -1);
                // string EBA = startupConfig.GetString("EconomyBaseAccount", UUID.Zero.ToString());
                // Helpers.TryParse(EBA, out EconomyBaseAccount);

                // UserLevelPaysFees = startupConfig.GetInt("UserLevelPaysFees", -1);
                m_stipend = startupConfig.GetInt("UserStipend", 500);
                m_minFundsBeforeRefresh = startupConfig.GetInt("IssueStipendWhenClientIsBelowAmount", 10);
                m_keepMoneyAcrossLogins = startupConfig.GetBoolean("KeepMoneyAcrossLogins", true);
                m_MoneyAddress = startupConfig.GetString("CurrencyServer", String.Empty);
                // m_LandAddress = startupConfig.GetString("LandServer", String.Empty);
            }

            // Send ObjectCapacity to Scene..  Which sends it to the SimStatsReporter.
            scene.SetObjectCapacity(ObjectCapacity);
        }

        private void GetClientFunds(IClientAPI client)
        {
            // Here we check if we're in grid mode
            // I imagine that the 'check balance'
            // function for the client should be here or shortly after

            if (gridmode)
            {
                if (m_MoneyAddress.Length == 0)
                {
                    CheckExistAndRefreshFunds(client.AgentId);
                }
                else
                {
                    bool childYN = true;
                    ScenePresence agent = null;
                    //client.SecureSessionId;
                    Scene s = LocateSceneClientIn(client.AgentId);
                    if (s != null)
                    {
                        agent = s.GetScenePresence(client.AgentId);
                        if (agent != null)
                            childYN = agent.IsChildAgent;
                    }
                    if (s != null && agent != null && childYN == false)
                    {
                        //s.RegionInfo.RegionHandle;
                        UUID agentID = UUID.Zero;
                        int funds = 0;

                        Hashtable hbinfo =
                            GetBalanceForUserFromMoneyServer(client.AgentId, client.SecureSessionId, s.RegionInfo.originRegionID.ToString(),
                                                             s.RegionInfo.regionSecret);
                        if ((bool) hbinfo["success"] == true)
                        {
                            UUID.TryParse((string)hbinfo["agentId"], out agentID);
                            try
                            {
                                funds = (Int32) hbinfo["funds"];
                            }
                            catch (ArgumentException)
                            {
                            }
                            catch (FormatException)
                            {
                            }
                            catch (OverflowException)
                            {
                                m_log.ErrorFormat("[MONEY]: While getting the Currency for user {0}, the return funds overflowed.", agentID);
                                client.SendAlertMessage("Unable to get your money balance, money operations will be unavailable");
                            }
                            catch (InvalidCastException)
                            {
                                funds = 0;
                            }

                            m_KnownClientFunds[agentID] = funds;
                        }
                        else
                        {
                            m_log.WarnFormat("[MONEY]: Getting Money for user {0} failed with the following message:{1}", agentID,
                                             (string) hbinfo["errorMessage"]);
                            client.SendAlertMessage((string) hbinfo["errorMessage"]);
                        }
                        SendMoneyBalance(client, agentID, client.SessionId, UUID.Zero);
                    }
                }
            }
            else
            {
                CheckExistAndRefreshFunds(client.AgentId);
            }

        }

        /// <summary>
        /// New Client Event Handler
        /// </summary>
        /// <param name="client"></param>
        private void OnNewClient(IClientAPI client)
        {
            GetClientFunds(client);

            // Subscribe to Money messages
            client.OnEconomyDataRequest += EconomyDataRequestHandler;
            client.OnMoneyBalanceRequest += SendMoneyBalance;
            client.OnRequestPayPrice += requestPayPrice;
            client.OnObjectBuy += ObjectBuy;
            client.OnLogout += ClientClosed;
        }

        /// <summary>
        /// Transfer money
        /// </summary>
        /// <param name="Sender"></param>
        /// <param name="Receiver"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        private bool doMoneyTransfer(UUID Sender, UUID Receiver, int amount, int transactiontype, string description)
        {
            bool result = false;
            if (amount >= 0)
            {
                lock (m_KnownClientFunds)
                {
                    // If we don't know about the sender, then the sender can't
                    // actually be here and therefore this is likely fraud or outdated.
                    if (m_MoneyAddress.Length == 0)
                    {
                        if (m_KnownClientFunds.ContainsKey(Sender))
                        {
                            // Does the sender have enough funds to give?
                            if (m_KnownClientFunds[Sender] >= amount)
                            {
                                // Subtract the funds from the senders account
                                m_KnownClientFunds[Sender] -= amount;

                                // do we know about the receiver?
                                if (!m_KnownClientFunds.ContainsKey(Receiver))
                                {
                                    // Make a record for them so they get the updated balance when they login
                                    CheckExistAndRefreshFunds(Receiver);
                                }
                                if (m_enabled)
                                {
                                    //Add the amount to the Receiver's funds
                                    m_KnownClientFunds[Receiver] += amount;
                                    result = true;
                                }
                            }
                            else
                            {
                                // These below are redundant to make this clearer to read
                                result = false;
                            }
                        }
                        else
                        {
                            result = false;
                        }
                    }
                    else
                    {
                        result = TransferMoneyonMoneyServer(Sender, Receiver, amount, transactiontype, description);
                    }
                }
            }
            return result;
        }


        /// <summary>
        /// Sends the the stored money balance to the client
        /// </summary>
        /// <param name="client"></param>
        /// <param name="agentID"></param>
        /// <param name="SessionID"></param>
        /// <param name="TransactionID"></param>
        public void SendMoneyBalance(IClientAPI client, UUID agentID, UUID SessionID, UUID TransactionID)
        {
            if (client.AgentId == agentID && client.SessionId == SessionID)
            {
                int returnfunds = 0;

                try
                {
                    returnfunds = GetFundsForAgentID(agentID);
                }
                catch (Exception e)
                {
                    client.SendAlertMessage(e.Message + " ");
                }

                client.SendMoneyBalance(TransactionID, true, new byte[0], returnfunds);
            }
            else
            {
                client.SendAlertMessage("Unable to send your money balance to you!");
            }
        }

        /// <summary>
        /// Gets the current balance for the user from the Grid Money Server
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="secureSessionID"></param>
        /// <param name="regionId"></param>
        /// <param name="regionSecret"></param>
        /// <returns></returns>
        public Hashtable GetBalanceForUserFromMoneyServer(UUID agentId, UUID secureSessionID, UUID regionId, string regionSecret)
        {
            Hashtable MoneyBalanceRequestParams = new Hashtable();
            MoneyBalanceRequestParams["agentId"] = agentId.ToString();
            MoneyBalanceRequestParams["secureSessionId"] = secureSessionID.ToString();
            MoneyBalanceRequestParams["regionId"] = regionId.ToString();
            MoneyBalanceRequestParams["secret"] = regionSecret;
            MoneyBalanceRequestParams["currencySecret"] = ""; // per - region/user currency secret gotten from the money system

            Hashtable MoneyRespData = genericCurrencyXMLRPCRequest(MoneyBalanceRequestParams, "simulatorUserBalanceRequest");

            return MoneyRespData;
        }


        /// <summary>
        /// Generic XMLRPC client abstraction
        /// </summary>
        /// <param name="ReqParams">Hashtable containing parameters to the method</param>
        /// <param name="method">Method to invoke</param>
        /// <returns>Hashtable with success=>bool and other values</returns>
        public Hashtable genericCurrencyXMLRPCRequest(Hashtable ReqParams, string method)
        {
            ArrayList SendParams = new ArrayList();
            SendParams.Add(ReqParams);
            // Send Request
            XmlRpcResponse MoneyResp;
            try
            {
                XmlRpcRequest BalanceRequestReq = new XmlRpcRequest(method, SendParams);
                MoneyResp = BalanceRequestReq.Send(m_MoneyAddress, 30000);
            }
            catch (WebException ex)
            {
                m_log.ErrorFormat(
                    "[MONEY]: Unable to connect to Money Server {0}.  Exception {1}",
                    m_MoneyAddress, ex);

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to manage your money at this time. Purchases may be unavailable";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
                //throw (ex);
            }
            catch (SocketException ex)
            {
                m_log.ErrorFormat(
                    "[MONEY]: Unable to connect to Money Server {0}.  Exception {1}",
                    m_MoneyAddress, ex);

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to manage your money at this time. Purchases may be unavailable";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
                //throw (ex);
            }
            catch (XmlException ex)
            {
                m_log.ErrorFormat(
                    "[MONEY]: Unable to connect to Money Server {0}.  Exception {1}",
                    m_MoneyAddress, ex);

                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to manage your money at this time. Purchases may be unavailable";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
            }
            if (MoneyResp.IsFault)
            {
                Hashtable ErrorHash = new Hashtable();
                ErrorHash["success"] = false;
                ErrorHash["errorMessage"] = "Unable to manage your money at this time. Purchases may be unavailable";
                ErrorHash["errorURI"] = "";

                return ErrorHash;
            }
            Hashtable MoneyRespData = (Hashtable) MoneyResp.Value;

            return MoneyRespData;
        }

        /// <summary>
        /// This informs the Money Grid Server that the avatar is in this simulator
        /// </summary>
        /// <param name="agentId"></param>
        /// <param name="secureSessionID"></param>
        /// <param name="regionId"></param>
        /// <param name="regionSecret"></param>
        /// <returns></returns>
        public Hashtable claim_user(UUID agentId, UUID secureSessionID, UUID regionId, string regionSecret)
        {
            Hashtable MoneyBalanceRequestParams = new Hashtable();
            MoneyBalanceRequestParams["agentId"] = agentId.ToString();
            MoneyBalanceRequestParams["secureSessionId"] = secureSessionID.ToString();
            MoneyBalanceRequestParams["regionId"] = regionId.ToString();
            MoneyBalanceRequestParams["secret"] = regionSecret;

            Hashtable MoneyRespData = genericCurrencyXMLRPCRequest(MoneyBalanceRequestParams, "simulatorClaimUserRequest");
            IClientAPI sendMoneyBal = LocateClientObject(agentId);
            if (sendMoneyBal != null)
            {
                SendMoneyBalance(sendMoneyBal, agentId, sendMoneyBal.SessionId, UUID.Zero);
            }
            return MoneyRespData;
        }

        private SceneObjectPart findPrim(UUID objectID)
        {
            lock (m_scenel)
            {
                foreach (Scene s in m_scenel.Values)
                {
                    SceneObjectPart part = s.GetSceneObjectPart(objectID);
                    if (part != null)
                    {
                        return part;
                    }
                }
            }
            return null;
        }

        private string resolveObjectName(UUID objectID)
        {
            SceneObjectPart part = findPrim(objectID);
            if (part != null)
            {
                return part.Name;
            }
            return String.Empty;
        }

        private string resolveAgentName(UUID agentID)
        {
            // try avatar username surname
            Scene scene = GetRandomScene();
            CachedUserInfo profile = scene.CommsManager.UserProfileCacheService.GetUserDetails(agentID);
            if (profile != null && profile.UserProfile != null)
            {
                string avatarname = profile.UserProfile.FirstName + " " + profile.UserProfile.SurName;
                return avatarname;
            }
            return String.Empty;
        }

        private void BalanceUpdate(UUID senderID, UUID receiverID, bool transactionresult, string description)
        {
            IClientAPI sender = LocateClientObject(senderID);
            IClientAPI receiver = LocateClientObject(receiverID);

            if (senderID != receiverID)
            {
                if (sender != null)
                {
                    sender.SendMoneyBalance(UUID.Random(), transactionresult, Utils.StringToBytes(description), GetFundsForAgentID(senderID));
                }

                if (receiver != null)
                {
                    receiver.SendMoneyBalance(UUID.Random(), transactionresult, Utils.StringToBytes(description), GetFundsForAgentID(receiverID));
                }
            }
        }

        /// <summary>
        /// Informs the Money Grid Server of a transfer.
        /// </summary>
        /// <param name="sourceId"></param>
        /// <param name="destId"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public bool TransferMoneyonMoneyServer(UUID sourceId, UUID destId, int amount, int transactiontype, string description)
        {
            int aggregatePermInventory = 0;
            int aggregatePermNextOwner = 0;
            int flags = 0;
            bool rvalue = false;

            IClientAPI cli = LocateClientObject(sourceId);
            if (cli != null)
            {
                Scene userScene = null;
                lock (m_rootAgents)
                {
                    userScene = GetSceneByUUID(m_rootAgents[sourceId]);
                }
                if (userScene != null)
                {
                    Hashtable ht = new Hashtable();
                    ht["agentId"] = sourceId.ToString();
                    ht["secureSessionId"] = cli.SecureSessionId.ToString();
                    ht["regionId"] = userScene.RegionInfo.originRegionID.ToString();
                    ht["secret"] = userScene.RegionInfo.regionSecret;
                    ht["currencySecret"] = " ";
                    ht["destId"] = destId.ToString();
                    ht["cash"] = amount;
                    ht["aggregatePermInventory"] = aggregatePermInventory;
                    ht["aggregatePermNextOwner"] = aggregatePermNextOwner;
                    ht["flags"] = flags;
                    ht["transactionType"] = transactiontype;
                    ht["description"] = description;

                    Hashtable hresult = genericCurrencyXMLRPCRequest(ht, "regionMoveMoney");

                    if ((bool) hresult["success"] == true)
                    {
                        int funds1 = 0;
                        int funds2 = 0;
                        try
                        {
                            funds1 = (Int32) hresult["funds"];
                        }
                        catch (InvalidCastException)
                        {
                            funds1 = 0;
                        }
                        SetLocalFundsForAgentID(sourceId, funds1);
                        if (m_KnownClientFunds.ContainsKey(destId))
                        {
                            try
                            {
                                funds2 = (Int32) hresult["funds2"];
                            }
                            catch (InvalidCastException)
                            {
                                funds2 = 0;
                            }
                            SetLocalFundsForAgentID(destId, funds2);
                        }


                        rvalue = true;
                    }
                    else
                    {
                        cli.SendAgentAlertMessage((string) hresult["errorMessage"], true);
                    }
                }
            }
            else
            {
                m_log.ErrorFormat("[MONEY]: Client {0} not found", sourceId.ToString());
            }

            return rvalue;
        }

        public int GetRemoteBalance(UUID agentId)
        {
            int funds = 0;

            IClientAPI aClient = LocateClientObject(agentId);
            if (aClient != null)
            {
                Scene s = LocateSceneClientIn(agentId);
                if (s != null)
                {
                    if (m_MoneyAddress.Length > 0)
                    {
                        Hashtable hbinfo =
                            GetBalanceForUserFromMoneyServer(aClient.AgentId, aClient.SecureSessionId, s.RegionInfo.originRegionID.ToString(),
                                                             s.RegionInfo.regionSecret);
                        if ((bool) hbinfo["success"] == true)
                        {
                            try
                            {
                                funds = (Int32) hbinfo["funds"];
                            }
                            catch (ArgumentException)
                            {
                            }
                            catch (FormatException)
                            {
                            }
                            catch (OverflowException)
                            {
                                m_log.ErrorFormat("[MONEY]: While getting the Currency for user {0}, the return funds overflowed.", agentId);
                                aClient.SendAlertMessage("Unable to get your money balance, money operations will be unavailable");
                            }
                            catch (InvalidCastException)
                            {
                                funds = 0;
                            }
                        }
                        else
                        {
                            m_log.WarnFormat("[MONEY]: Getting Money for user {0} failed with the following message:{1}", agentId,
                                             (string) hbinfo["errorMessage"]);
                            aClient.SendAlertMessage((string) hbinfo["errorMessage"]);
                        }
                    }

                    SetLocalFundsForAgentID(agentId, funds);
                    SendMoneyBalance(aClient, agentId, aClient.SessionId, UUID.Zero);
                }
                else
                {
                    m_log.Debug("[MONEY]: Got balance request update for agent that is here, but couldn't find which scene.");
                }
            }
            else
            {
                m_log.Debug("[MONEY]: Got balance request update for agent that isn't here.");
            }
            return funds;
        }

        public XmlRpcResponse GridMoneyUpdate(XmlRpcRequest request)
        {
            m_log.Debug("[MONEY]: Dynamic balance update called.");
            Hashtable requestData = (Hashtable) request.Params[0];

            if (requestData.ContainsKey("agentId"))
            {
                UUID agentId = UUID.Zero;

                UUID.TryParse((string) requestData["agentId"], out agentId);
                if (agentId != UUID.Zero)
                {
                    GetRemoteBalance(agentId);
                }
                else
                {
                    m_log.Debug("[MONEY]: invalid agentId specified, dropping.");
                }
            }
            else
            {
                m_log.Debug("[MONEY]: no agentId specified, dropping.");
            }
            XmlRpcResponse r = new XmlRpcResponse();
            Hashtable rparms = new Hashtable();
            rparms["success"] = true;

            r.Value = rparms;
            return r;
        }

        /// <summary>
        /// XMLRPC handler to send alert message and sound to client
        /// </summary>
        public XmlRpcResponse UserAlert(XmlRpcRequest request)
        {
            XmlRpcResponse ret = new XmlRpcResponse();
            Hashtable retparam = new Hashtable();
            Hashtable requestData = (Hashtable) request.Params[0];

            UUID agentId = UUID.Zero;
            UUID soundId = UUID.Zero;
            UUID regionId = UUID.Zero;

            UUID.TryParse((string) requestData["agentId"], out agentId);
            UUID.TryParse((string) requestData["soundId"], out soundId);
            UUID.TryParse((string) requestData["regionId"], out regionId);
            string text = (string) requestData["text"];
            string secret = (string) requestData["secret"];

            Scene userScene = GetSceneByUUID(regionId);
            if (userScene != null)
            {
                if (userScene.RegionInfo.regionSecret.ToString() == secret)
                {

                    IClientAPI client = LocateClientObject(agentId);
                       if (client != null)
                       {

                           if (soundId != UUID.Zero)
                               client.SendPlayAttachedSound(soundId, UUID.Zero, UUID.Zero, 1.0f, 0);

                           client.SendBlueBoxMessage(UUID.Zero, UUID.Zero, "", text);

                           retparam.Add("success", true);
                       }
                    else
                    {
                        retparam.Add("success", false);
                    }
                }
                else
                {
                    retparam.Add("success", false);
                }
            }

            ret.Value = retparam;
            return ret;
        }

        # region Standalone box enablers only

        public XmlRpcResponse quote_func(XmlRpcRequest request)
        {
            Hashtable requestData = (Hashtable) request.Params[0];
            UUID agentId = UUID.Zero;
            int amount = 0;
            Hashtable quoteResponse = new Hashtable();
            XmlRpcResponse returnval = new XmlRpcResponse();

            if (requestData.ContainsKey("agentId") && requestData.ContainsKey("currencyBuy"))
            {
                UUID.TryParse((string) requestData["agentId"], out agentId);
                try
                {
                    amount = (Int32) requestData["currencyBuy"];
                }
                catch (InvalidCastException)
                {
                }
                Hashtable currencyResponse = new Hashtable();
                currencyResponse.Add("estimatedCost", 0);
                currencyResponse.Add("currencyBuy", amount);

                quoteResponse.Add("success", true);
                quoteResponse.Add("currency", currencyResponse);
                quoteResponse.Add("confirm", "asdfad9fj39ma9fj");

                returnval.Value = quoteResponse;
                return returnval;
            }


            quoteResponse.Add("success", false);
            quoteResponse.Add("errorMessage", "Invalid parameters passed to the quote box");
            quoteResponse.Add("errorURI", "http://www.opensimulator.org/wiki");
            returnval.Value = quoteResponse;
            return returnval;
        }

        public XmlRpcResponse buy_func(XmlRpcRequest request)
        {
            Hashtable requestData = (Hashtable) request.Params[0];
            UUID agentId = UUID.Zero;
            int amount = 0;
            if (requestData.ContainsKey("agentId") && requestData.ContainsKey("currencyBuy"))
            {
                UUID.TryParse((string) requestData["agentId"], out agentId);
                try
                {
                    amount = (Int32) requestData["currencyBuy"];
                }
                catch (InvalidCastException)
                {
                }
                if (agentId != UUID.Zero)
                {
                    lock (m_KnownClientFunds)
                    {
                        if (m_KnownClientFunds.ContainsKey(agentId))
                        {
                            m_KnownClientFunds[agentId] += amount;
                        }
                        else
                        {
                            m_KnownClientFunds.Add(agentId, amount);
                        }
                    }
                    IClientAPI client = LocateClientObject(agentId);
                    if (client != null)
                    {
                        SendMoneyBalance(client, agentId, client.SessionId, UUID.Zero);
                    }
                }
            }
            XmlRpcResponse returnval = new XmlRpcResponse();
            Hashtable returnresp = new Hashtable();
            returnresp.Add("success", true);
            returnval.Value = returnresp;
            return returnval;
        }

        public XmlRpcResponse preflightBuyLandPrep_func(XmlRpcRequest request)
        {
            XmlRpcResponse ret = new XmlRpcResponse();
            Hashtable retparam = new Hashtable();
            Hashtable membershiplevels = new Hashtable();
            ArrayList levels = new ArrayList();
            Hashtable level = new Hashtable();
            level.Add("id", "00000000-0000-0000-0000-000000000000");
            level.Add("description", "some level");
            levels.Add(level);
            //membershiplevels.Add("levels",levels);

            Hashtable landuse = new Hashtable();
            landuse.Add("upgrade", false);
            landuse.Add("action", "http://invaliddomaininvalid.com/");

            Hashtable currency = new Hashtable();
            currency.Add("estimatedCost", 0);

            Hashtable membership = new Hashtable();
            membershiplevels.Add("upgrade", false);
            membershiplevels.Add("action", "http://invaliddomaininvalid.com/");
            membershiplevels.Add("levels", membershiplevels);

            retparam.Add("success", true);
            retparam.Add("currency", currency);
            retparam.Add("membership", membership);
            retparam.Add("landuse", landuse);
            retparam.Add("confirm", "asdfajsdkfjasdkfjalsdfjasdf");

            ret.Value = retparam;

            return ret;
        }

        public XmlRpcResponse landBuy_func(XmlRpcRequest request)
        {
            XmlRpcResponse ret = new XmlRpcResponse();
            Hashtable retparam = new Hashtable();
            Hashtable requestData = (Hashtable) request.Params[0];

            UUID agentId = UUID.Zero;
            int amount = 0;
            if (requestData.ContainsKey("agentId") && requestData.ContainsKey("currencyBuy"))
            {
                UUID.TryParse((string) requestData["agentId"], out agentId);
                try
                {
                    amount = (Int32) requestData["currencyBuy"];
                }
                catch (InvalidCastException)
                {
                }
                if (agentId != UUID.Zero)
                {
                    lock (m_KnownClientFunds)
                    {
                        if (m_KnownClientFunds.ContainsKey(agentId))
                        {
                            m_KnownClientFunds[agentId] += amount;
                        }
                        else
                        {
                            m_KnownClientFunds.Add(agentId, amount);
                        }
                    }
                    IClientAPI client = LocateClientObject(agentId);
                    if (client != null)
                    {
                        SendMoneyBalance(client, agentId, client.SessionId, UUID.Zero);
                    }
                }
            }
            retparam.Add("success", true);
            ret.Value = retparam;

            return ret;
        }

        #endregion

        #region local Fund Management

        /// <summary>
        /// Ensures that the agent accounting data is set up in this instance.
        /// </summary>
        /// <param name="agentID"></param>
        private void CheckExistAndRefreshFunds(UUID agentID)
        {
            lock (m_KnownClientFunds)
            {
                if (!m_KnownClientFunds.ContainsKey(agentID))
                {
                    m_KnownClientFunds.Add(agentID, m_stipend);
                }
                else
                {
                    if (m_KnownClientFunds[agentID] <= m_minFundsBeforeRefresh)
                    {
                        m_KnownClientFunds[agentID] = m_stipend;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the amount of Funds for an agent
        /// </summary>
        /// <param name="AgentID"></param>
        /// <returns></returns>
        private int GetFundsForAgentID(UUID AgentID)
        {
            int returnfunds = 0;
            lock (m_KnownClientFunds)
            {
                if (m_KnownClientFunds.ContainsKey(AgentID))
                {
                    returnfunds = m_KnownClientFunds[AgentID];
                }
                else
                {
                    //throw new Exception("Unable to get funds.");
                }
            }
            return returnfunds;
        }

        private void SetLocalFundsForAgentID(UUID AgentID, int amount)
        {
            lock (m_KnownClientFunds)
            {
                if (m_KnownClientFunds.ContainsKey(AgentID))
                {
                    m_KnownClientFunds[AgentID] = amount;
                }
                else
                {
                    m_KnownClientFunds.Add(AgentID, amount);
                }
            }
        }

        #endregion

        #region Utility Helpers

        /// <summary>
        /// Locates a IClientAPI for the client specified
        /// </summary>
        /// <param name="AgentID"></param>
        /// <returns></returns>
        private IClientAPI LocateClientObject(UUID AgentID)
        {
            ScenePresence tPresence = null;
            IClientAPI rclient = null;

            lock (m_scenel)
            {
                foreach (Scene _scene in m_scenel.Values)
                {
                    tPresence = _scene.GetScenePresence(AgentID);
                    if (tPresence != null)
                    {
                        if (!tPresence.IsChildAgent)
                        {
                            rclient = tPresence.ControllingClient;
                        }
                    }
                    if (rclient != null)
                    {
                        return rclient;
                    }
                }
            }
            return null;
        }

        private Scene LocateSceneClientIn(UUID AgentId)
        {
            lock (m_scenel)
            {
                foreach (Scene _scene in m_scenel.Values)
                {
                    ScenePresence tPresence = _scene.GetScenePresence(AgentId);
                    if (tPresence != null)
                    {
                        if (!tPresence.IsChildAgent)
                        {
                            return _scene;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Utility function Gets a Random scene in the instance.  For when which scene exactly you're doing something with doesn't matter
        /// </summary>
        /// <returns></returns>
        public Scene GetRandomScene()
        {
            lock (m_scenel)
            {
                foreach (Scene rs in m_scenel.Values)
                    return rs;
            }
            return null;
        }

        /// <summary>
        /// Utility function to get a Scene by RegionID in a module
        /// </summary>
        /// <param name="RegionID"></param>
        /// <returns></returns>
        public Scene GetSceneByUUID(UUID RegionID)
        {
            lock (m_scenel)
            {
                foreach (Scene rs in m_scenel.Values)
                {
                    if (rs.RegionInfo.originRegionID == RegionID)
                    {
                        return rs;
                    }
                }
            }
            return null;
        }

        #endregion

        #region event Handlers

        public void requestPayPrice(IClientAPI client, UUID objectID)
        {
            Scene scene = LocateSceneClientIn(client.AgentId);
            if (scene == null)
                return;

            SceneObjectPart task = scene.GetSceneObjectPart(objectID);
            if (task == null)
                return;
            SceneObjectGroup group = task.ParentGroup;
            SceneObjectPart root = group.RootPart;

            client.SendPayPrice(objectID, root.PayPrice);
        }

        /// <summary>
        /// When the client closes the connection we remove their accounting info from memory to free up resources.
        /// </summary>
        /// <param name="AgentID"></param>
        public void ClientClosed(UUID AgentID)
        {
            lock (m_KnownClientFunds)
            {
                if (m_keepMoneyAcrossLogins && m_MoneyAddress.Length == 0)
                {
                }
                else
                {
                    m_KnownClientFunds.Remove(AgentID);
                }
            }
        }

        /// <summary>
        /// Event called Economy Data Request handler.
        /// </summary>
        /// <param name="agentId"></param>
        public void EconomyDataRequestHandler(UUID agentId)
        {
            IClientAPI user = LocateClientObject(agentId);

            if (user != null)
            {
                user.SendEconomyData(EnergyEfficiency, ObjectCapacity, ObjectCount, PriceEnergyUnit, PriceGroupCreate,
                                     PriceObjectClaim, PriceObjectRent, PriceObjectScaleFactor, PriceParcelClaim, PriceParcelClaimFactor,
                                     PriceParcelRent, PricePublicObjectDecay, PricePublicObjectDelete, PriceRentLight, PriceUpload,
                                     TeleportMinPrice, TeleportPriceExponent);
            }
        }

        private void ValidateLandBuy(Object osender, EventManager.LandBuyArgs e)
        {
            if (m_MoneyAddress.Length == 0)
            {
                lock (m_KnownClientFunds)
                {
                    if (m_KnownClientFunds.ContainsKey(e.agentId))
                    {
                        // Does the sender have enough funds to give?
                        if (m_KnownClientFunds[e.agentId] >= e.parcelPrice)
                        {
                            lock (e)
                            {
                                e.economyValidated = true;
                            }
                        }
                    }
                }
            }
            else
            {
                if (GetRemoteBalance(e.agentId) >= e.parcelPrice)
                {
                    lock (e)
                    {
                        e.economyValidated = true;
                    }
                }
            }
        }

        private void processLandBuy(Object osender, EventManager.LandBuyArgs e)
        {
            lock (e)
            {
                if (e.economyValidated == true && e.transactionID == 0)
                {
                    e.transactionID = Util.UnixTimeSinceEpoch();

                    if (doMoneyTransfer(e.agentId, e.parcelOwnerID, e.parcelPrice, 0, "Land purchase"))
                    {
                        lock (e)
                        {
                            e.amountDebited = e.parcelPrice;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// THis method gets called when someone pays someone else as a gift.
        /// </summary>
        /// <param name="osender"></param>
        /// <param name="e"></param>
        private void MoneyTransferAction(Object osender, EventManager.MoneyTransferArgs e)
        {
            IClientAPI sender = null;
            IClientAPI receiver = null;

            if (m_MoneyAddress.Length > 0) // Handled on server
                e.description = String.Empty;

            if (e.transactiontype == 5008) // Object gets paid
            {
                sender = LocateClientObject(e.sender);
                if (sender != null)
                {
                    SceneObjectPart part = findPrim(e.receiver);
                    if (part == null)
                        return;

                    string name = resolveAgentName(part.OwnerID);
                    if (name == String.Empty)
                        name = "(hippos)";

                    receiver = LocateClientObject(part.OwnerID);

                    string description = String.Format("Paid {0} via object {1}", name, e.description);
                    bool transactionresult = doMoneyTransfer(e.sender, part.OwnerID, e.amount, e.transactiontype, description);

                    if (transactionresult)
                    {
                        ObjectPaid handlerOnObjectPaid = OnObjectPaid;
                        if (handlerOnObjectPaid != null)
                        {
                            handlerOnObjectPaid(e.receiver, e.sender, e.amount);
                        }
                    }

                    if (e.sender != e.receiver)
                    {
                        sender.SendMoneyBalance(UUID.Random(), transactionresult, Utils.StringToBytes(e.description), GetFundsForAgentID(e.sender));
                    }
                    if (receiver != null)
                    {
                        receiver.SendMoneyBalance(UUID.Random(), transactionresult, Utils.StringToBytes(e.description), GetFundsForAgentID(part.OwnerID));
                    }
                }
                return;
            }

            sender = LocateClientObject(e.sender);
            if (sender != null)
            {
                receiver = LocateClientObject(e.receiver);

                bool transactionresult = doMoneyTransfer(e.sender, e.receiver, e.amount, e.transactiontype, e.description);

                if (e.sender != e.receiver)
                {
                    if (sender != null)
                    {
                        sender.SendMoneyBalance(UUID.Random(), transactionresult, Utils.StringToBytes(e.description), GetFundsForAgentID(e.sender));
                    }
                }

                if (receiver != null)
                {
                    receiver.SendMoneyBalance(UUID.Random(), transactionresult, Utils.StringToBytes(e.description), GetFundsForAgentID(e.receiver));
                }
            }
            else
            {
                m_log.Warn("[MONEY]: Potential Fraud Warning, got money transfer request for avatar that isn't in this simulator - Details; Sender:" +
                           e.sender.ToString() + " Receiver: " + e.receiver.ToString() + " Amount: " + e.amount.ToString());
            }
        }

        /// <summary>
        /// Event Handler for when a root agent becomes a child agent
        /// </summary>
        /// <param name="avatar"></param>
        private void MakeChildAgent(ScenePresence avatar)
        {
            lock (m_rootAgents)
            {
                if (m_rootAgents.ContainsKey(avatar.UUID))
                {
                    if (m_rootAgents[avatar.UUID] == avatar.Scene.RegionInfo.originRegionID)
                    {
                        m_rootAgents.Remove(avatar.UUID);
                        m_log.Info("[MONEY]: Removing " + avatar.Firstname + " " + avatar.Lastname + " as a root agent");
                    }
                }
            }
        }

        /// <summary>
        /// Event Handler for when the client logs out.
        /// </summary>
        /// <param name="AgentId"></param>
        private void ClientLoggedOut(UUID AgentId)
        {
            lock (m_rootAgents)
            {
                if (m_rootAgents.ContainsKey(AgentId))
                {
                    m_rootAgents.Remove(AgentId);
                    //m_log.Info("[MONEY]: Removing " + AgentId + ". Agent logged out.");
                }
            }
        }

        /// <summary>
        /// Call this when the client disconnects.
        /// </summary>
        /// <param name="client"></param>
        public void ClientClosed(IClientAPI client)
        {
            ClientClosed(client.AgentId);
        }

        /// <summary>
        /// Event Handler for when an Avatar enters one of the parcels in the simulator.
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="localLandID"></param>
        /// <param name="regionID"></param>
        private void AvatarEnteringParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            lock (m_rootAgents)
            {
                if (m_rootAgents.ContainsKey(avatar.UUID))
                {
                    if (avatar.Scene.RegionInfo.originRegionID != m_rootAgents[avatar.UUID])
                    {
                        m_rootAgents[avatar.UUID] = avatar.Scene.RegionInfo.originRegionID;


                        //m_log.Info("[MONEY]: Claiming " + avatar.Firstname + " " + avatar.Lastname + " in region:" + avatar.RegionHandle + ".");
                        // Claim User! my user!  Mine mine mine!
                        if (m_MoneyAddress.Length > 0)
                        {
                            Scene RegionItem = GetSceneByUUID(regionID);
                            if (RegionItem != null)
                            {
                                Hashtable hresult =
                                    claim_user(avatar.UUID, avatar.ControllingClient.SecureSessionId, regionID, RegionItem.RegionInfo.regionSecret);
                                if ((bool)hresult["success"] == true)
                                {
                                    int funds = 0;
                                    try
                                    {
                                        funds = (Int32)hresult["funds"];
                                    }
                                    catch (InvalidCastException)
                                    {
                                    }
                                    SetLocalFundsForAgentID(avatar.UUID, funds);
                                }
                                else
                                {
                                    avatar.ControllingClient.SendAgentAlertMessage((string)hresult["errorMessage"], true);
                                }
                            }
                        }
                    }
                    else
                    {
                        ILandObject obj = avatar.Scene.LandChannel.GetLandObject(avatar.AbsolutePosition.X, avatar.AbsolutePosition.Y);
                        if ((obj.landData.Flags & (uint)Parcel.ParcelFlags.AllowDamage) != 0)
                        {
                            avatar.Invulnerable = false;
                        }
                        else
                        {
                            avatar.Invulnerable = true;
                        }
                    }
                }
                else
                {
                    lock (m_rootAgents)
                    {
                        m_rootAgents.Add(avatar.UUID, avatar.Scene.RegionInfo.originRegionID);
                    }
                    if (m_MoneyAddress.Length > 0)
                    {
                        Scene RegionItem = GetSceneByUUID(regionID);
                        if (RegionItem != null)
                        {
                            Hashtable hresult = claim_user(avatar.UUID, avatar.ControllingClient.SecureSessionId, regionID, RegionItem.RegionInfo.regionSecret);
                            if ((bool) hresult["success"] == true)
                            {
                                int funds = 0;
                                try
                                {
                                    funds = (Int32) hresult["funds"];
                                }
                                catch (InvalidCastException)
                                {
                                }
                                SetLocalFundsForAgentID(avatar.UUID, funds);
                            }
                            else
                            {
                                avatar.ControllingClient.SendAgentAlertMessage((string) hresult["errorMessage"], true);
                            }
                        }
                    }

                    //m_log.Info("[MONEY]: Claiming " + avatar.Firstname + " " + avatar.Lastname + " in region:" + avatar.RegionHandle + ".");
                }
            }
            //m_log.Info("[FRIEND]: " + avatar.Name + " status:" + (!avatar.IsChildAgent).ToString());
        }

        private void KillAvatar(uint killerObjectLocalID, ScenePresence DeadAvatar)
        {
            if (killerObjectLocalID == 0)
                DeadAvatar.ControllingClient.SendAgentAlertMessage("You committed suicide!", true);
            else
            {
                bool foundResult = false;
                string resultstring = "";
                List<ScenePresence> allav = DeadAvatar.Scene.GetScenePresences();
                try
                {
                    foreach (ScenePresence av in allav)
                    {
                        if (av.LocalId == killerObjectLocalID)
                        {
                            av.ControllingClient.SendAlertMessage("You fragged " + DeadAvatar.Firstname + " " + DeadAvatar.Lastname);
                            resultstring = av.Firstname + " " + av.Lastname;
                            foundResult = true;
                        }
                    }
                } catch (System.InvalidOperationException)
                {

                }

                if (!foundResult)
                {
                    SceneObjectPart part = DeadAvatar.Scene.GetSceneObjectPart(killerObjectLocalID);
                    if (part != null)
                    {
                        ScenePresence av = DeadAvatar.Scene.GetScenePresence(part.OwnerID);
                        if (av != null)
                        {
                            av.ControllingClient.SendAlertMessage("You fragged " + DeadAvatar.Firstname + " " + DeadAvatar.Lastname);
                            resultstring = av.Firstname + " " + av.Lastname;
                            DeadAvatar.ControllingClient.SendAgentAlertMessage("You got killed by " + resultstring + "!", true);
                        }
                        else
                        {
                            string killer = DeadAvatar.Scene.CommsManager.UUIDNameRequestString(part.OwnerID);
                            DeadAvatar.ControllingClient.SendAgentAlertMessage("You impaled yourself on " + part.Name + " owned by " + killer +"!", true);
                        }
                        //DeadAvatar.Scene. part.ObjectOwner
                    }
                    else
                    {
                        DeadAvatar.ControllingClient.SendAgentAlertMessage("You died!", true);
                    }
                }
            }
            DeadAvatar.Health = 100;
            DeadAvatar.Scene.TeleportClientHome(DeadAvatar.UUID, DeadAvatar.ControllingClient);
        }

        public int GetBalance(IClientAPI client)
        {
            GetClientFunds(client);

            lock (m_KnownClientFunds)
            {
                if (!m_KnownClientFunds.ContainsKey(client.AgentId))
                    return 0;

                return m_KnownClientFunds[client.AgentId];
            }
        }

        public bool UploadCovered(IClientAPI client)
        {
            if (GetBalance(client) < PriceUpload)
                return false;

            return true;
        }

        #endregion

        public void ObjectBuy(IClientAPI remoteClient, UUID agentID,
                UUID sessionID, UUID groupID, UUID categoryID,
                uint localID, byte saleType, int salePrice)
        {
            GetClientFunds(remoteClient);

            if (!m_KnownClientFunds.ContainsKey(remoteClient.AgentId))
            {
                remoteClient.SendAgentAlertMessage("Unable to buy now. Your account balance was not found.", false);
                return;
            }

            int funds = m_KnownClientFunds[remoteClient.AgentId];

            if (salePrice != 0 && funds < salePrice)
            {
                remoteClient.SendAgentAlertMessage("Unable to buy now. You don't have sufficient funds.", false);
                return;
            }

            Scene s = LocateSceneClientIn(remoteClient.AgentId);

            SceneObjectPart part = s.GetSceneObjectPart(localID);
            if (part == null)
            {
                remoteClient.SendAgentAlertMessage("Unable to buy now. The object was not found.", false);
                return;
            }

            doMoneyTransfer(remoteClient.AgentId, part.OwnerID, salePrice, 5000, "Object buy");

            s.PerformObjectBuy(remoteClient, categoryID, localID, saleType);
        }
    }

    public enum TransactionType : int
    {
        SystemGenerated = 0,
        RegionMoneyRequest = 1,
        Gift = 2,
        Purchase = 3
    }
}
