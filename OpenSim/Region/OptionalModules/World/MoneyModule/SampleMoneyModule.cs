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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using Mono.Addins;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.OptionalModules.World.MoneyModule
{
    /// <summary>
    /// This is only the functionality required to make the functionality associated with money work
    /// (such as land transfers).  There is no money code here!  Use FORGE as an example for money code.
    /// Demo Economy/Money Module.  This is a purposely crippled module!
    ///  // To land transfer you need to add:
    /// -helperuri http://serveraddress:port/
    /// to the command line parameters you use to start up your client
    /// This commonly looks like -helperuri http://127.0.0.1:9000/
    ///
    /// </summary>

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "SampleMoneyModule")]
    public class SampleMoneyModule : IMoneyModule, ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Where Stipends come from and Fees go to.
        /// </summary>
        // private UUID EconomyBaseAccount = UUID.Zero;

        private float EnergyEfficiency = 0f;
        // private ObjectPaid handerOnObjectPaid;
        private bool m_enabled = true;
        private bool m_sellEnabled = false;

        private IConfigSource m_gConfig;

        /// <summary>
        /// Region UUIDS indexed by AgentID
        /// </summary>

        /// <summary>
        /// Scenes by Region Handle
        /// </summary>
        private Dictionary<ulong, Scene> m_scenel = new Dictionary<ulong, Scene>();

        // private int m_stipend = 1000;

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


        #region IMoneyModule Members

#pragma warning disable 0067
        public event ObjectPaid OnObjectPaid;
#pragma warning restore 0067

        public int UploadCharge
        {
            get { return 0; }
        }

        public int GroupCreationCharge
        {
            get { return 0; }
        }

        /// <summary>
        /// Called on startup so the module can be configured.
        /// </summary>
        /// <param name="config">Configuration source.</param>
        public void Initialise(IConfigSource config)
        {
            m_gConfig = config;

            IConfig startupConfig = m_gConfig.Configs["Startup"];
            IConfig economyConfig = m_gConfig.Configs["Economy"];


            ReadConfigAndPopulate(startupConfig, "Startup");
            ReadConfigAndPopulate(economyConfig, "Economy");
        }

        public void AddRegion(Scene scene)
        {
            if (m_enabled)
            {
                scene.RegisterModuleInterface<IMoneyModule>(this);
                IHttpServer httpServer = MainServer.Instance;

                lock (m_scenel)
                {
                    if (m_scenel.Count == 0)
                    {
                        // XMLRPCHandler = scene;

                        // To use the following you need to add:
                        // -helperuri <ADDRESS TO HERE OR grid MONEY SERVER>
                        // to the command line parameters you use to start up your client
                        // This commonly looks like -helperuri http://127.0.0.1:9000/

                       
                        // Local Server..  enables functionality only.
                        httpServer.AddXmlRPCHandler("getCurrencyQuote", quote_func);
                        httpServer.AddXmlRPCHandler("buyCurrency", buy_func);
                        httpServer.AddXmlRPCHandler("preflightBuyLandPrep", preflightBuyLandPrep_func);
                        httpServer.AddXmlRPCHandler("buyLandPrep", landBuy_func);
                       
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
            }
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }


        // Please do not refactor these to be just one method
        // Existing implementations need the distinction
        //
        public void ApplyCharge(UUID agentID, int amount, MoneyTransactionType type, string extraData)
        {
        }

        public void ApplyCharge(UUID agentID, int amount, MoneyTransactionType type)
        {
        }

        public void ApplyUploadCharge(UUID agentID, int amount, string text)
        {
        }

        public bool ObjectGiveMoney(UUID objectID, UUID fromID, UUID toID, int amount, UUID txn, out string result)
        {
            result = String.Empty;
            string description = String.Format("Object {0} pays {1}", resolveObjectName(objectID), resolveAgentName(toID));

            bool give_result = doMoneyTransfer(fromID, toID, amount, 2, description);

            
            BalanceUpdate(fromID, toID, give_result, description);

            return give_result;
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public Type ReplaceableInterface
        {
            get { return typeof(IMoneyModule); }
        }

        public string Name
        {
            get { return "BetaGridLikeMoneyModule"; }
        }

        #endregion

        /// <summary>
        /// Parse Configuration
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="startupConfig"></param>
        /// <param name="config"></param>
        private void ReadConfigAndPopulate(IConfig startupConfig, string config)
        {
            if (config == "Startup" && startupConfig != null)
            {
                m_enabled = (startupConfig.GetString("economymodule", "BetaGridLikeMoneyModule") == "BetaGridLikeMoneyModule");
            }

            if (config == "Economy" && startupConfig != null)
            {
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
                m_sellEnabled = startupConfig.GetBoolean("SellEnabled", false);
            }
        }

        private void GetClientFunds(IClientAPI client)
        {
            CheckExistAndRefreshFunds(client.AgentId);
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
            bool result = true;
            
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

                client.SendMoneyBalance(TransactionID, true, new byte[0], returnfunds, 0, UUID.Zero, false, UUID.Zero, false, 0, String.Empty);
            }
            else
            {
                client.SendAlertMessage("Unable to send your money balance to you!");
            }
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
            UserAccount account = scene.UserAccountService.GetUserAccount(scene.RegionInfo.ScopeID, agentID);
            if (account != null)
            {
                string avatarname = account.FirstName + " " + account.LastName;
                return avatarname;
            }
            else
            {
                m_log.ErrorFormat(
                    "[MONEY]: Could not resolve user {0}", 
                    agentID);
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
                    sender.SendMoneyBalance(UUID.Random(), transactionresult, Utils.StringToBytes(description), GetFundsForAgentID(senderID), 0, UUID.Zero, false, UUID.Zero, false, 0, String.Empty);
                }

                if (receiver != null)
                {
                    receiver.SendMoneyBalance(UUID.Random(), transactionresult, Utils.StringToBytes(description), GetFundsForAgentID(receiverID), 0, UUID.Zero, false, UUID.Zero, false, 0, String.Empty);
                }
            }
        }

        /// <summary>
        /// XMLRPC handler to send alert message and sound to client
        /// </summary>
        public XmlRpcResponse UserAlert(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse ret = new XmlRpcResponse();
            Hashtable retparam = new Hashtable();
            Hashtable requestData = (Hashtable) request.Params[0];

            UUID agentId;
            UUID soundId;
            UUID regionId;

            UUID.TryParse((string) requestData["agentId"], out agentId);
            UUID.TryParse((string) requestData["soundId"], out soundId);
            UUID.TryParse((string) requestData["regionId"], out regionId);
            string text = (string) requestData["text"];
            string secret = (string) requestData["secret"];

            Scene userScene = GetSceneByUUID(regionId);
            if (userScene != null)
            {
                if (userScene.RegionInfo.regionSecret == secret)
                {

                    IClientAPI client = LocateClientObject(agentId);
                       if (client != null)
                       {

                           if (soundId != UUID.Zero)
                               client.SendPlayAttachedSound(soundId, UUID.Zero, UUID.Zero, 1.0f, 0);

                           client.SendBlueBoxMessage(UUID.Zero, "", text);

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

        public XmlRpcResponse quote_func(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Hashtable requestData = (Hashtable) request.Params[0];
            // UUID agentId = UUID.Zero;
            int amount = 0;
            Hashtable quoteResponse = new Hashtable();
            XmlRpcResponse returnval = new XmlRpcResponse();

            
            Hashtable currencyResponse = new Hashtable();
            currencyResponse.Add("estimatedCost", 0);
            currencyResponse.Add("currencyBuy", amount);

            quoteResponse.Add("success", true);
            quoteResponse.Add("currency", currencyResponse);
            quoteResponse.Add("confirm", "asdfad9fj39ma9fj");

            returnval.Value = quoteResponse;
            return returnval;
            


        }

        public XmlRpcResponse buy_func(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            // Hashtable requestData = (Hashtable) request.Params[0];
            // UUID agentId = UUID.Zero;
            // int amount = 0;
           
            XmlRpcResponse returnval = new XmlRpcResponse();
            Hashtable returnresp = new Hashtable();
            returnresp.Add("success", true);
            returnval.Value = returnresp;
            return returnval;
        }

        public XmlRpcResponse preflightBuyLandPrep_func(XmlRpcRequest request, IPEndPoint remoteClient)
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

        public XmlRpcResponse landBuy_func(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse ret = new XmlRpcResponse();
            Hashtable retparam = new Hashtable();
            // Hashtable requestData = (Hashtable) request.Params[0];

            // UUID agentId = UUID.Zero;
            // int amount = 0;
           
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
            
        }

        /// <summary>
        /// Gets the amount of Funds for an agent
        /// </summary>
        /// <param name="AgentID"></param>
        /// <returns></returns>
        private int GetFundsForAgentID(UUID AgentID)
        {
            int returnfunds = 0;
            
            return returnfunds;
        }

        // private void SetLocalFundsForAgentID(UUID AgentID, int amount)
        // {
            
        // }

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
        /// When the client closes the connection we remove their accounting
        /// info from memory to free up resources.
        /// </summary>
        /// <param name="AgentID">UUID of agent</param>
        /// <param name="scene">Scene the agent was connected to.</param>
        /// <see cref="OpenSim.Region.Framework.Scenes.EventManager.ClientClosed"/>
        public void ClientClosed(UUID AgentID, Scene scene)
        {
            
        }

        /// <summary>
        /// Event called Economy Data Request handler.
        /// </summary>
        /// <param name="agentId"></param>
        public void EconomyDataRequestHandler(IClientAPI user)
        {
            Scene s = (Scene)user.Scene;

            user.SendEconomyData(EnergyEfficiency, s.RegionInfo.ObjectCapacity, ObjectCount, PriceEnergyUnit, PriceGroupCreate,
                                 PriceObjectClaim, PriceObjectRent, PriceObjectScaleFactor, PriceParcelClaim, PriceParcelClaimFactor,
                                 PriceParcelRent, PricePublicObjectDecay, PricePublicObjectDelete, PriceRentLight, PriceUpload,
                                 TeleportMinPrice, TeleportPriceExponent);
        }

        private void ValidateLandBuy(Object osender, EventManager.LandBuyArgs e)
        {
            
            
            lock (e)
            {
                e.economyValidated = true;
            }
       
            
        }

        private void processLandBuy(Object osender, EventManager.LandBuyArgs e)
        {
            
        }

        /// <summary>
        /// THis method gets called when someone pays someone else as a gift.
        /// </summary>
        /// <param name="osender"></param>
        /// <param name="e"></param>
        private void MoneyTransferAction(Object osender, EventManager.MoneyTransferArgs e)
        {
            
        }

        /// <summary>
        /// Event Handler for when a root agent becomes a child agent
        /// </summary>
        /// <param name="avatar"></param>
        private void MakeChildAgent(ScenePresence avatar)
        {
            
        }

        /// <summary>
        /// Event Handler for when the client logs out.
        /// </summary>
        /// <param name="AgentId"></param>
        private void ClientLoggedOut(UUID AgentId, Scene scene)
        {
            
        }

        /// <summary>
        /// Call this when the client disconnects.
        /// </summary>
        /// <param name="client"></param>
        public void ClientClosed(IClientAPI client)
        {
            ClientClosed(client.AgentId, null);
        }

        /// <summary>
        /// Event Handler for when an Avatar enters one of the parcels in the simulator.
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="localLandID"></param>
        /// <param name="regionID"></param>
        private void AvatarEnteringParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            
            //m_log.Info("[FRIEND]: " + avatar.Name + " status:" + (!avatar.IsChildAgent).ToString());
        }

        public int GetBalance(UUID agentID)
        {
            return 0;
        }

        // Please do not refactor these to be just one method
        // Existing implementations need the distinction
        //
        public bool UploadCovered(UUID agentID, int amount)
        {
            return true;
        }
        public bool AmountCovered(UUID agentID, int amount)
        {
            return true;
        }

        #endregion

        public void ObjectBuy(IClientAPI remoteClient, UUID agentID,
                UUID sessionID, UUID groupID, UUID categoryID,
                uint localID, byte saleType, int salePrice)
        {
            if (!m_sellEnabled)
            {
                remoteClient.SendBlueBoxMessage(UUID.Zero, "", "Buying is not implemented in this version");
                return;
            }

            if (salePrice != 0)
            {
                remoteClient.SendBlueBoxMessage(UUID.Zero, "", "Buying anything for a price other than zero is not implemented");
                return;
            }

            Scene s = LocateSceneClientIn(remoteClient.AgentId);

            // Implmenting base sale data checking here so the default OpenSimulator implementation isn't useless 
            // combined with other implementations.  We're actually validating that the client is sending the data
            // that it should.   In theory, the client should already know what to send here because it'll see it when it
            // gets the object data.   If the data sent by the client doesn't match the object, the viewer probably has an 
            // old idea of what the object properties are.   Viewer developer Hazim informed us that the base module 
            // didn't check the client sent data against the object do any.   Since the base modules are the 
            // 'crowning glory' examples of good practice..

            // Validate that the object exists in the scene the user is in
            SceneObjectPart part = s.GetSceneObjectPart(localID);
            if (part == null)
            {
                remoteClient.SendAgentAlertMessage("Unable to buy now. The object was not found.", false);
                return;
            }
            
            // Validate that the client sent the price that the object is being sold for 
            if (part.SalePrice != salePrice)
            {
                remoteClient.SendAgentAlertMessage("Cannot buy at this price. Buy Failed. If you continue to get this relog.", false);
                return;
            }

            // Validate that the client sent the proper sale type the object has set 
            if (part.ObjectSaleType != saleType)
            {
                remoteClient.SendAgentAlertMessage("Cannot buy this way. Buy Failed. If you continue to get this relog.", false);
                return;
            }

            IBuySellModule module = s.RequestModuleInterface<IBuySellModule>();
            if (module != null)
                module.BuyObject(remoteClient, categoryID, localID, saleType, salePrice);
        }

        public void MoveMoney(UUID fromAgentID, UUID toAgentID, int amount, string text)
        {
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
