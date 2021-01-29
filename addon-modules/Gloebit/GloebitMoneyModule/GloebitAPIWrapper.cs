/*
 * GloebitAPIWrapper.cs is part of OpenSim-MoneyModule-Gloebit 
 * Copyright (C) 2015 Gloebit LLC
 *
 * OpenSim-MoneyModule-Gloebit is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * OpenSim-MoneyModule-Gloebit is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with OpenSim-MoneyModule-Gloebit.  If not, see <https://www.gnu.org/licenses/>.
 */

/*
 * GloebitAPIWrapper.cs
 * 
 * Wraps the simple GloebitAPI web calls in a more useful functional layer.
 * 
 * For porting to other systems or implementing new transaction types/flows,
 * this file will likely require some modification.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using log4net;
// TODO: convert OSDMaps to Dictionaries and UUIDs to GUIDs and remove requirement for OpenMetaverse libraries to make this more generic.
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace Gloebit.GloebitMoneyModule {

    public class GloebitAPIWrapper : GloebitAPI.IAsyncEndpointCallback {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public readonly string m_key;
        private string m_keyAlias;
        private string m_secret;
        public readonly Uri m_url;
        private GloebitAPI m_api; 

        private static GloebitTransaction.IAssetCallback m_assetCallbacks;

        public interface IUriLoader {
            // Load funcs below are used in flows where we need to send the user to the Gloebit Website.
            void LoadAuthorizeUriForUser(GloebitUser user, Uri authorizeUri);
            void LoadSubscriptionAuthorizationUriForUser(GloebitUser user, Uri subAuthUri, GloebitSubscription sub, bool isDeclined);
        }
        private static IUriLoader m_uriLoaders;

        public interface IPlatformAccessor {
            Uri GetBaseURI();
            string resolveAgentEmail(UUID agentID);
            string resolveAgentName(UUID agentID);  // TODO: may be able to remove this if we add it to the GloebitUser
        }
        private static IPlatformAccessor m_platformAccessors;

        // TODO: might be better to do these with event handlers
        public interface IUserAlert {
            void AlertUserAuthorized(GloebitUser user, UUID agentID, double balance, OSDMap extraData);
        }
        private static IUserAlert m_userAlerts;

        public interface ITransactionAlert {
            void AlertTransactionBegun(GloebitTransaction txn, string description);
            void AlertTransactionStageCompleted(GloebitTransaction txn, GloebitAPI.TransactionStage stage, string additionalDetails);
            void AlertTransactionFailed(GloebitTransaction txn, GloebitAPI.TransactionStage stage, GloebitAPI.TransactionFailure failure, string additionalFailureDetails, OSDMap extraData);
            void AlertTransactionSucceeded(GloebitTransaction txn);
        }
        private static ITransactionAlert m_transactionAlerts;

        public interface ISubscriptionAlert {
            void AlertSubscriptionCreated(GloebitSubscription subscription);
            void AlertSubscriptionCreationFailed(GloebitSubscription subscription);
        }
        private static ISubscriptionAlert m_subscriptionAlerts;

        public GloebitAPIWrapper(string key, string keyAlias, string secret, Uri url, string dbProvider, string dbConnectionString, GloebitTransaction.IAssetCallback assetCallbacks, IUriLoader uriLoaders, IPlatformAccessor platformAccessors, IUserAlert userAlerts, ITransactionAlert transactionAlerts, ISubscriptionAlert subscriptionAlerts) {
            m_key = key;
            m_keyAlias = keyAlias;
            m_secret = secret;
            m_url = url;
            m_assetCallbacks = assetCallbacks;
            m_uriLoaders = uriLoaders;
            m_platformAccessors = platformAccessors;
            m_userAlerts = userAlerts;
            m_transactionAlerts = transactionAlerts;
            m_subscriptionAlerts = subscriptionAlerts;

            //string key = (m_keyAlias != null && m_keyAlias != "") ? m_keyAlias : m_key;
            m_api = new GloebitAPI(m_key, m_keyAlias, m_secret, m_url, this);
            GloebitUserData.Initialise(dbProvider, dbConnectionString);
            GloebitTransactionData.Initialise(dbProvider, dbConnectionString);
            GloebitSubscriptionData.Initialise(dbProvider, dbConnectionString);
        }


        #region User Auth and Balance

        /**************************************************************/
        /**** LINKING agent on app to Gloebit account *****************/
        /**** AUTHORIZING app from agent / Gloebit account ************/
        /**** BALANCE retrieval for agent which verifies auth/link ****/
        /**************************************************************/

        /***************
         * GloebitUser.Get() should be called to create or retrieve an AppUser
         * --- There is not one central GloebitAPIWrapper function for starting a new user session
         * --- but it may later be centralized.
         * --- See GMM.SendNewSessionMessaging() function for closest thing to a StartUserSession().
         * GloebitUser.Cleanup() should be called to free up memory when a User is no longer active
         * calling GetUserBalance() function with forceAuthoOnInvalidToken=true
         * --- is simplest way to ensure user is authorized and ready to proceed with transactions.
         ***************/

        /// <summary>
        /// Ask this user on this app to link this agent to their Gloebit account and to authorize
        /// the app to take actions on the agent's behalf which will access that Gloebit account.
        /// Retrieves the GloebitUser for this agent, builds the authorization URL, and asks the 
        /// platform to present that URL to the user via the IUriLoader interface.
        /// </summary>
        /// <param name="agentID">The local UUID of this agent on this app.</param>
        /// <param name="agentName">The name of this agent on this app.</param>
        public void Authorize(UUID agentID, string agentName)
        {
            // Get User for agent
            GloebitUser user = GloebitUser.Get(m_key, agentID);
            Authorize(user, agentName);
        }

        /// <summary>
        /// Ask this user on this app to link this agent to their Gloebit account and to authorize
        /// the app to take actions on the agent's behalf which will access that Gloebit account
        /// Builds the authorization URL for this agent, and asks the 
        /// platform to present that URL to the user via the IUriLoader interface.
        /// </summary>
        /// <param name="user">GloebitUser representing this agent on this app.</param>
        /// <param name="userName">The name of this agent on this app.</param>
        public void Authorize(GloebitUser user, string userName)
        {
            Uri authUri = m_api.BuildAuthorizationURI(user, userName, m_platformAccessors.GetBaseURI());
            m_uriLoaders.LoadAuthorizeUriForUser(user, authUri);
        }
            
        /*** GloebitAPI Required HTTP Callback Entrance Point - must be registered by GMM ***/
        /// <summary>
        /// Registered to the redirectURI from GloebitAPI.Authorize.  Called when a user approves authorization.
        /// Enacts the GloebitAPI.ExchangeAccessToken endpoint to exchange the auth_code for the token.
        /// </summary>
        /// <param name="requestData">response data from GloebitAPI.Authorize</param>
        public Hashtable authComplete_func(Hashtable requestData) {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] authComplete_func");
            foreach(DictionaryEntry e in requestData) { m_log.DebugFormat("{0}: {1}", e.Key, e.Value); }

            string agentId = requestData["agentId"] as string;
            string code = requestData["code"] as string;

            UUID parsedAgentId = UUID.Parse(agentId);
            GloebitUser u = GloebitUser.Get(m_key, parsedAgentId);

            // Start async flow to exchange the code for a permanent token
            m_api.ExchangeAccessToken(u, code, m_platformAccessors.GetBaseURI());
            m_log.InfoFormat("[GLOEBITMONEYMODULE] authComplete_func started ExchangeAccessToken");

            // TODO: create interface function to build this response
            Uri url = BuildPurchaseURI(m_platformAccessors.GetBaseURI(), u);
            Hashtable response = new Hashtable();
            response["int_response_code"] = 200;
            response["str_response_string"] = String.Format("<html><head><title>Gloebit authorized</title></head><body><h2>Gloebit authorized</h2>Thank you for authorizing Gloebit.  You may now close this window and return to OpenSim.<br /><br /><br />You'll now be able spend gloebits from your Gloebit account as the agent you authorized on this OpenSim Grid.<br /><br />If you need gloebits, you can <a href=\"{0}\">purchase them here</a>.</body></html>", url);
            response["content_type"] = "text/html";

            return response;
        }
            
        /**** IAsyncEndpointCallback Interface ****/
        /// <summary>
        /// Called by the GloebitAPI after the async call to ExchangeAccessToken completes.
        /// If this was successful, this agent is now authorized and linked to a Gloebit account, and
        /// this retrieves the balance for that Gloebit account and triggers IUserAlert AlertUserAuthorized
        /// </summary>
        /// <param name="success">bool - if true, the exchange was successful and this user is authorized and linked to a Gloebit account</param>
        /// <param name="user">GloebitUser representing the agent and app that the authorization was for</param>
        /// <param name="responseDataMap">OSDMap of response data from GloebitAPI.ExchangeAccessToken</param>
        public void exchangeAccessTokenCompleted(bool success, GloebitUser user, OSDMap responseDataMap)
        {
            if (success) {
                // This is the point where a user is actually authorized/linked

                // Eventually, auth may pass balance back.  It doesn't yet, but will almost always be desired by app, so we'll retrieve it.
                bool invalidatedToken;
                double balance = m_api.GetBalance (user, out invalidatedToken);

                // retrieve the agentID since that is what the calling app provided to us
                UUID agentID = UUID.Parse(user.PrincipalID);

                // TODO: determine what, if anything, should actually be in extraData that could be useful.  For now, entire response
                m_userAlerts.AlertUserAuthorized(user, agentID, balance, responseDataMap);
            } else {
                // May want to log an error or retry.
                // Don't think App should need to know that this failed 
            }
        }

        /// <summary>
        /// Retrieves the gloebit balance of the Gloebit account linked to the user defined by the userIDOnApp.
        /// If there is no token, or an invalid token on file, and forceAuthOnInvalidToken is true, we request authorization from the AppUser.
        /// If we request authorization, the userName is provided to that API Authorize function.  Otherwise, it is not used.
        /// </summary>
        /// <param name="userIDOnApp">ID from this app for the user whose balance is being requested</param>
        /// <param name ="forceAuthOnInvalidToken">Bool indicating whether we should request auth on failures from lack of auth</param>
        /// <param name="userName">string name of the user in this application.</param>
        /// <returns>Gloebit balance for the Gloebit account linked to this user or 0.0.</returns>
        public double GetUserBalance(UUID userIDOnApp, bool forceAuthOnInvalidToken, string userName)
        {
            m_log.DebugFormat("[GLOEBITMONEYMODULE] GetAppUserBalance userIDOnApp:{0}", userIDOnApp);
            double returnfunds = 0.0;
            bool needsAuth = false;

            // Get User for agent
            GloebitUser user = GloebitUser.Get(m_key, userIDOnApp);
            if(!user.IsAuthed()) {
                // If no auth token on file, request authorization.
                needsAuth = true;
            } else {
                returnfunds = m_api.GetBalance(user, out needsAuth);
                // if GetBalance fails due to invalidToken, needsAuth is set to true

                // Fix for having a few old tokens out in the wild without an app_user_id stored as the user.GloebitID
                // TODO: Remove this  once it's been released for awhile, as this fix should only be necessary for a short time.
                if (String.IsNullOrEmpty(user.GloebitID) || user.GloebitID == UUID.Zero.ToString()) {
                    m_log.InfoFormat("[GLOEBITMONEYMODULE] GetAppUserBalance userIDOnApp:{0} INVALIDATING TOKEN FROM GMM", userIDOnApp);
                    user.InvalidateToken();
                    needsAuth = true;
                }
            }

            if (needsAuth && forceAuthOnInvalidToken) {
                Authorize(user, userName);
            }

            return returnfunds;
        }

        #endregion // User Auth and Balance

        #region Purchasing gloebits
        
        /*****************************/
        /**** PURCHASING gloebits ****/
        /*****************************/
                   
        /******************
         * Users can do this directly from their Gloebit account on the website, so these methods are not required.
         * However, they are useful for providing Gloebit purchase links to the user to take them directly to the purchase page.
         * Eventually, these links will enable returning directly to a commerce flow or alerting the app that the user's balance
         * has been updated.
         ******************/

        // TODO: should this be moved to API and replaced with purchase function here which builds and calls send with URI?
        //       GMM definitely needs to just build the URI, though it could call purchase when it needs it for a link and not send it
        //       if it can supply extra info for how to handle the return.  This seems more complex than necessary right now, so we've
        //       left it here.
        /// <summary>
        /// Builds a URI for a user to purchase gloebits
        /// </summary>
        /// <param name="callbackBaseUri">Base URI for where the /gloebit/buy_complete/ path was registered.  Supplied in the purchase uri
        ///                               so we can let the platform know when a user has completed a purchase.  This alert is not
        ///                               yet implemented on the Gloebit server.</param>
        /// <param name ="u">GloebitUser representing the agent and app requesting to purchase gloebits</param>
        /// <returns>URI for the platform to provide at which this user can purchase gloebits.</returns>
        public Uri BuildPurchaseURI(Uri callbackBaseUri, GloebitUser u) {
            UriBuilder purchaseUri = new UriBuilder(m_url);
            purchaseUri.Path = "/purchase";
            if (callbackBaseUri != null) {
                // could do a try/catch here with the errors that UriBuilder can throw to also prevent crash from poorly formatted server uri.
                UriBuilder callbackUrl = new UriBuilder(callbackBaseUri);
                callbackUrl.Path = "/gloebit/buy_complete";
                callbackUrl.Query = String.Format("agentId={0}", u.PrincipalID);
                purchaseUri.Query = String.Format("reset&r={0}&inform={1}", m_keyAlias, callbackUrl.Uri);
            } else {
                purchaseUri.Query = String.Format("reset&r={0}", m_keyAlias);
            }
            return purchaseUri.Uri;
        }

        /*** GloebitAPI Optional HTTP Callback Entrance Point - must be registered by GMM to work - not yet implemented ***/
        /// <summary>
        /// NOT YET IMPLEMENTED BY GLOEBIT SERVICE
        /// Used by the redirect-to parameter to GloebitAPI.Purchase.  Called when a user has finished purchasing gloebits
        /// Sends a balance update to the user
        /// </summary>
        /// <param name="requestData">response data from GloebitAPI.Purchase</param>
        public Hashtable buyComplete_func(Hashtable requestData) {
            // TODO: This is not yet implemented on the api side.  BuildPurchaseURI sets the inform query arg to this.
            // But that's we need to build the functionality to call that url upon purchase completion.
            // We would probably pass this through and do it when we load the purchase success page.
            // TODO: also, since we have a success page and we're not prepared to allow alternate success pages, this should probably just be used
            // to inform the grid of a balance change and shouldn't produce html. -- OR should we allow a full redirect?

            UUID agentID = UUID.Parse(requestData["agentId"] as string);

            // TODO: create interface function to update user's balance in system
            //IClientAPI client = LocateClientObject(agentID);

            // Update balance in viewer.  Request auth if not authed.  Do not send the purchase url.
            //UpdateBalance(agentID, client, 0);
            // TODO: When we implement this, we should supply the balance in the requestData and simply call client.SendMoneyBalance(...)

            // TODO: create interface function to build this response
            Hashtable response = new Hashtable();
            response["int_response_code"] = 200;
            response["str_response_string"] = "<html><head><title>Purchase Complete</title></head><body><h2>Purchase Complete</h2>Thank you for purchasing Gloebits.  You may now close this window.</body></html>";
            response["content_type"] = "text/html";
            return response;
        }

        #endregion // Purchasing gloebits

        #region Transaction

        /*********************/
        /**** TRANSACTION ****/
        /*********************/

        /// <summary>
        /// Helper function to build the minimal transaction description sent to the Gloebit transactU2U endpoint.
        /// Used for tracking as well as information provided in transaction histories.
        /// </summary>
        /// <param name="txnType">String describing the type of transaction.  e.g. ObjectBuy, PayObject, PayUser, etc.</param>
        /// <returns>OSDMap to be sent with the transaction request parameters.  Map contains six dictionary entries, each including an OSDArray.</returns>
        public OSDMap BuildBaseTransactionDescMap(string txnType)
        {
            // Create descMap
            OSDMap descMap = new OSDMap();

            // Create arrays in descMap
            descMap["platform-names"] = new OSDArray();
            descMap["platform-values"] = new OSDArray();
            descMap["location-names"] = new OSDArray();
            descMap["location-values"] = new OSDArray();
            descMap["transaction-names"] = new OSDArray();
            descMap["transaction-values"] = new OSDArray();

            // Add base transaction details
            //// TODO: change arg to toke a TxnTypeID, add that here, and create func to get the string name from a txnTypeId
            AddDescMapEntry(descMap, "transaction", "transaction-type", txnType);

            return descMap;
        }

        /// <summary>
        /// Helper function to add an entryName/entryValue pair to one of the three entryGroup array pairs for a descMap.
        /// Used by buildBaseTransactionDescMap, and to add additional entries to a descMap created by buildBaseTransactionDescMap.
        /// PRECONDITION: The descMap passed to this function must have been created and returned by buildBaseTransactionDescMap.
        /// Any entryName/Value pairs added to a descMap passed to the transactU2U endpoint will be sent to Gloebit, tracked with the transaction, and will appear in the transaction history for all users who are a party to the transaction.
        /// </summary>
        /// <param name="descMap">descMap created by buildBaseTransactionDescMap.</param>
        /// <param name="entryGroup">String group to which to add entryName/Value pair.  Must be one of {"platform", "location", "transaction"}.  Specifies group to which these details are most applicable.</param>
        /// <param name="entryName">String providing the name for entry to be added.  This is the name users will see in their transaction history for this entry.</param>
        /// <param name="entryValue">String providing the value for entry to be added.  This is the value users will see in their transaction history for this entry.</param>
        public void AddDescMapEntry(OSDMap descMap, string entryGroup, string entryName, string entryValue)
        {

            /****** ERROR CHECKING *******/
            if (descMap == null) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] addDescMapEntry: Attempted to add an entry to a NULL descMap.  entryGroup:{0} entryName:{1} entryValue:{2}", entryGroup, entryName, entryValue);
                return;
            }
            if (entryGroup == null || entryName == null || entryValue == null) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] addDescMapEntry: Attempted to add an entry to a descMap where one of the entry strings is NULL.  entryGroup:{0} entryName:{1} entryValue:{2}", entryGroup, entryName, entryValue);
                return;
            }
            if (entryGroup == String.Empty || entryName == String.Empty) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] addDescMapEntry: Attempted to add an entry to a descMap where entryGroup or entryName is the empty string.  entryGroup:{0} entryName:{1} entryValue:{2}", entryGroup, entryName, entryValue);
                return;
            }

            List<string> permittedGroups = new List<string> {"platform", "location", "transaction"};
            if (!permittedGroups.Contains(entryGroup)) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] addDescMapEntry: Attempted to add a transaction description parameter in an entryGroup that is not be tracked by Gloebit.  entryGroup:{0} permittedGroups:{1} entryName:{2} entryValue:{3}", entryGroup, permittedGroups, entryName, entryValue);
                return;
            }

            /******* ADD ENTRY TO PROPER ARRAYS ******/
            switch (entryGroup) {
                case "platform":
                    ((OSDArray)descMap["platform-names"]).Add(entryName);
                    ((OSDArray)descMap["platform-values"]).Add(entryValue);
                    break;
                case "location":
                    ((OSDArray)descMap["location-names"]).Add(entryName);
                    ((OSDArray)descMap["location-values"]).Add(entryValue);
                    break;
                case "transaction":
                    ((OSDArray)descMap["transaction-names"]).Add(entryName);
                    ((OSDArray)descMap["transaction-values"]).Add(entryValue);
                    break;
                default:
                    // SHOULD NEVER GET HERE
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] addDescMapEntry: Attempted to add a transaction description parameter in an entryGroup that is not be tracked by Gloebit and made it to default of switch statement.  entryGroup:{0} permittedGroups:{1} entryName:{2} entryValue:{3}", entryGroup, permittedGroups, entryName, entryValue);
                    break;
            }
            return;
        }

        /// <summary>
        /// Submits a GloebitTransaction to Gloebit for processing and provides any necessary feedback to user/platform.
        /// --- Must call buildTransaction() to create argument 1.
        /// --- Must call buildBaseTransactionDescMap() to create argument 3.
        /// </summary>
        /// <param name="txn">GloebitTransaction created from buildTransaction().  Contains vital transaction details.</param>
        /// <param name="description">Description of transaction for transaction history reporting.</param>
        /// <param name="descMap">Map of platform, location & transaction descriptors for tracking/querying and transaction history details.  For more details, <see cref="GloebitMoneyModule.buildBaseTransactionDescMap"/> helper function.</param>
        /// <param name="u2u">boolean declaring whether this is a user-to-app (false) or user-to-user (true) transaction.</param>
        /// <returns>
        /// true if async transactU2U web request was built and submitted successfully; false if failed to submit request.
        /// If true:
        /// --- IAsyncEndpointCallback transactU2UCompleted should eventually be called with additional details on state of request.
        /// --- IAssetCallback processAsset[Enact|Consume|Cancel]Hold may eventually be called dependent upon processing.
        /// </returns>
        public bool SubmitTransaction(GloebitTransaction txn, string description, OSDMap descMap, bool u2u)
        {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] SubmitTransaction Txn: {0}, from {1} to {2}, for amount {3}, transactionType: {4}, description: {5}", txn.TransactionID, txn.PayerID, txn.PayeeID, txn.Amount, txn.TransactionType, description);
            m_transactionAlerts.AlertTransactionBegun(txn, description);

            // TODO: Update all the alert functions to handle fees properly.

            // TODO: Should we wrap TransactU2U or request.BeginGetResponse in Try/Catch?
            bool result = false;
            if (u2u) {
                result = m_api.TransactU2U(txn, description, descMap, GloebitUser.Get(m_key, txn.PayerID), GloebitUser.Get(m_key, txn.PayeeID), m_platformAccessors.resolveAgentEmail(txn.PayeeID), m_platformAccessors.GetBaseURI());
            } else {
                result = m_api.Transact(txn, description, descMap, GloebitUser.Get(m_key, txn.PayerID), m_platformAccessors.GetBaseURI());
            }

            if (!result) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] SubmitTransaction failed to create HttpWebRequest in GloebitAPI.TransactU2U");
                m_transactionAlerts.AlertTransactionFailed(txn, GloebitAPI.TransactionStage.SUBMIT, GloebitAPI.TransactionFailure.SUBMISSION_FAILED, String.Empty, new OSDMap());
            } else {
                m_transactionAlerts.AlertTransactionStageCompleted(txn, GloebitAPI.TransactionStage.SUBMIT, String.Empty);
            }

            return result;
        }

        /// <summary>
        /// Submits a GloebitTransaction using synchronous web requests to Gloebit for processing and provides any necessary feedback to user/platform.
        /// Rather than solely receiving a "submission" response, TransactU2UCallback happens during request, and receives transaction success/failure response.
        /// --- Must call buildTransaction() to create argument 1.
        /// --- Must call buildBaseTransactionDescMap() to create argument 3.
        /// *** NOTE *** Only use this function if you need a synchronous transaction success response.  Use SubmitTransaction Otherwise.
        /// </summary>
        /// <param name="txn">GloebitTransaction created from buildTransaction().  Contains vital transaction details.</param>
        /// <param name="description">Description of transaction for transaction history reporting.</param>
        /// <param name="descMap">Map of platform, location & transaction descriptors for tracking/querying and transaction history details.  For more details, <see cref="GloebitMoneyModule.buildBaseTransactionDescMap"/> helper function.</param>
        /// <returns>
        /// true if sync transactU2U web request was built and submitted successfully and Gloebit components of transaction were enacted successfully.
        /// false if failed to submit request or if txn failed at any stage prior to successfully enacting Gloebit txn components.
        /// If true:
        /// --- IAsyncEndpointCallback transactU2UCompleted has already been called with additional details on state of request.
        /// --- IAssetCallback processAsset[Enact|Consume|Cancel]Hold will eventually be called by the transaction processor if txn included callbacks.
        /// If false:
        /// --- If stage is any stage after SUBMIT, errors are handled by TransactU2UCompleted callback which was already called.
        /// --- If stage is SUBMIT, errors must be handled by this function
        /// </returns>
        public bool SubmitSyncTransaction(GloebitTransaction txn, string description, OSDMap descMap)
        {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] SubmitSyncTransaction Txn: {0}, from {1} to {2}, for amount {3}, transactionType: {4}, description: {5}", txn.TransactionID, txn.PayerID, txn.PayeeID, txn.Amount, txn.TransactionType, description);
            m_transactionAlerts.AlertTransactionBegun(txn, description);

            // TODO: Should we wrap TransactU2U or request.GetResponse in Try/Catch?
            GloebitAPI.TransactionStage stage = GloebitAPI.TransactionStage.BUILD;
            GloebitAPI.TransactionFailure failure = GloebitAPI.TransactionFailure.NONE;
            bool result = m_api.TransactU2USync(txn, description, descMap, GloebitUser.Get(m_key, txn.PayerID), GloebitUser.Get(m_key, txn.PayeeID), m_platformAccessors.resolveAgentEmail(txn.PayeeID), m_platformAccessors.GetBaseURI(), out stage, out failure);

            if (!result) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] SubmitSyncTransaction failed in stage: {0} with failure: {1}", stage, failure);
                if (stage == GloebitAPI.TransactionStage.SUBMIT) {
                    // currently need to handle these errors here as the TransactU2UCallback is not called unless submission is successful and we receive a response
                    m_transactionAlerts.AlertTransactionFailed(txn, GloebitAPI.TransactionStage.SUBMIT, failure, String.Empty, new OSDMap());
                }
            } else {
                // TODO: figure out how/where to send this alert in a synchronous transaction.  Maybe it should always come from the API.
                // m_transactionAlerts.AlertTransactionStageCompleted(txn, GloebitAPI.TransactionStage.SUBMIT, String.Empty);
            }
            return result;
        }

        /**** IAsyncEndpointCallback Interface ****/
        /// <summary>
        /// Called by the GloebitAPI after the async call to Transact and TransactU2U complete.
        /// Also called prior to completion of sync call to TransactU2USync.
        /// If this was successful, then the transaction was submitted and queued and the monetary pieces
        /// of the transaction were each enacted by the server.  The transaction processor will re-enact those
        /// and call enact/cancel/consume on the local asset / transaction part.
        /// This function parses the response from Gloebit, logs useful information, and calls ITransactionAlert
        /// interface functions with the data needed by the platform for any processing.
        /// </summary>
        /// <param name="success">bool - if true, the exchange was successful and this user is authorized and linked to a Gloebit account</param>
        /// <param name="user">GloebitUser representing the agent and app that the authorization was for</param>
        /// <param name="responseDataMap">OSDMap of response data from GloebitAPI.Transact TransactU2U or TransactU2USync</param>
        /// <param name="payerUser">GloebitUser representing the agent and app that made the payment.</param>
        /// <param name="payerUser">GloebitUser representing the agent and app that received the payment or null if callback from Transact.</param>
        /// <param name="txn">GloebitTransaction detailing this transaction locally.</param>
        /// <param name="stage">enum for the stage of the transaction that either completed successfully or failed.</param>
        /// <param name="failure">enum which details the specific failure on the stage provided or null if the stage provided was successfully completed.</param>
        public void transactU2UCompleted(OSDMap responseDataMap, GloebitUser payerUser, GloebitUser payeeUser, GloebitTransaction txn, GloebitAPI.TransactionStage stage, GloebitAPI.TransactionFailure failure)
        {
            // TODO: should pass success, reason and status through as arguments.  Should probably check tID in GAPI instead of here.
            bool success = (bool)responseDataMap["success"];
            string reason = responseDataMap["reason"];
            string status = "";
            if (responseDataMap.ContainsKey("status")) {
                status = responseDataMap["status"];
            }
            string tID = "";
            if (responseDataMap.ContainsKey("id")) {
                tID = responseDataMap["id"];
            }
            // TODO: verify that tID = txn.TransactionID --- should never be otherwise.

            string additionalDetailStr = String.Empty;
            OSDMap extraData = new OSDMap();

            // Can/should this be moved to QUEUE with no errors?
            if (success) {
                // If we get here, queuing and early enact were successful.
                // When the processor runs this, we are guaranteed that it will call our enact URI eventually, or succeed if no callback-uris were provided.
                m_log.InfoFormat("[GLOEBITMONEYMODULE].transactU2UCompleted with SUCCESS reason:{0} id:{1}", reason, tID);
                if (reason == "success") {                                  /* successfully queued, early enacted all non-asset transaction parts */
                    // TODO: if we update GMM to allow transactions without callback-uris, then we would need to signal full success here.
                    // TODO: we should really provide an interface for checking status or require at least a single callback uri.
                    // Early enact also succeeded, so could add additional details that funds have successfully been transferred or set to stage ENACT_GLOEBIT
                    additionalDetailStr = String.Empty;
                } else if (reason == "resubmitted") {                       /* transaction had already been created.  resubmitted to queue */
                    m_log.InfoFormat("[GLOEBITMONEYMODULE].transactU2UCompleted resubmitted transaction  id:{0}", tID);
                    additionalDetailStr = "Transaction resubmitted to queue.";
                } else {                                                    /* Unhandled success reason */
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE].transactU2UCompleted unhandled response reason:{0}  id:{1}", reason, tID);
                    additionalDetailStr = reason;
                }
                m_transactionAlerts.AlertTransactionStageCompleted(txn, GloebitAPI.TransactionStage.QUEUE, additionalDetailStr);
                return;
            }

            m_log.InfoFormat("[GLOEBITMONEYMODULE].transactU2UCompleted with FAILURE reason:{0} status:{1} id:{2}", reason, status, tID);

            // Handle errors
            switch (stage) {
                case GloebitAPI.TransactionStage.ENACT_GLOEBIT:     /* Placed this first as it is an odd case where early-enact failed. */
                    // We're announcing failure here, but it's possible we should just announce completion of QUEUE state instead.
                    // It is unlikely, though may be possible, for this to fail and the processor (rather than server queuing and then attempting early enact) could succeed.
                    m_log.InfoFormat("[GLOEBITMONEYMODULE].transactU2UCompleted transaction successfully queued for processing, but failed early enact.  id:{0} reason:{1}", tID, reason);
                    // TODO: Should we send a failure alert here?  Could transaction enact successfully?  Need to research this
                    // insufficient-balance; pending probably can't occur; something new?
                    if (failure == GloebitAPI.TransactionFailure.INSUFFICIENT_FUNDS) {
                        m_log.InfoFormat("[GLOEBITMONEYMODULE].transactU2UCompleted transaction failed.  Buyer has insufficient funds.  id:{0}", tID);
                    } else {
                        // unhandled, so pass reason
                        m_log.ErrorFormat("[GLOEBITMONEYMODULE].transactU2UCompleted transaction failed during processing.  reason:{0} id:{1} failure:{2}", reason, tID, failure);
                        additionalDetailStr = reason;
                    }
                    break;
                case GloebitAPI.TransactionStage.AUTHENTICATE:
                    // failed check of OAUTH2 token - currently only one error causes this - invalid token
                    // Could try a behind the scenes renewal/reauth for expired, and then resubmit, but we don't do that right now, so just fail.
                    m_log.InfoFormat("[GLOEBITMONEYMODULE].transactU2UCompleted transaction failed.  Authentication error.  Invalid token.  id:{0}", tID);
                    break;
                case GloebitAPI.TransactionStage.VALIDATE:
                    m_log.InfoFormat("[GLOEBITMONEYMODULE].transactU2UCompleted transaction failed.  Validation error.  id:{0}", tID);

                    // Prepare some variables necessary for log messages for some Validation failures.
                    string subscriptionIDStr = String.Empty;
                    string appSubscriptionIDStr = String.Empty;
                    string subscriptionAuthIDStr = String.Empty;
                    UUID subscriptionAuthID = UUID.Zero;
                    if (responseDataMap.ContainsKey("subscription-id")) {
                        subscriptionIDStr = responseDataMap["subscription-id"];
                        // txn has app-sub-id and can be used to retrieve sub-id, but including this for now anyway;
                        extraData["subscription-id"] = subscriptionIDStr;
                    }
                    if (responseDataMap.ContainsKey("app-subscription-id")) {
                        appSubscriptionIDStr = responseDataMap["app-subscription-id"];
                        // Not adding to extraData since this is in the txn
                    }
                    if (responseDataMap.ContainsKey("subscription-authorization-id")) {
                        subscriptionAuthIDStr = responseDataMap["subscription-authorization-id"];
                        subscriptionAuthID = UUID.Parse(subscriptionAuthIDStr);
                        // Add to extraData for now since not in txn.  Consider storing these locally and in txn.
                        extraData["subscription-authorization-id"] = subscriptionAuthIDStr;
                    }

                    switch (failure) {
                        case GloebitAPI.TransactionFailure.FORM_GENERIC_ERROR:                    /* One of many form errors.  something needs fixing.  See reason */
                            // All form errors are errors the app needs to fix
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE].transactU2UCompleted Transaction failed.  App needs to fix something. id:{0} failure:{1} reason:{2}", tID, failure, reason);
                            additionalDetailStr = reason;
                            break;
                        case GloebitAPI.TransactionFailure.FORM_MISSING_SUBSCRIPTION_ID:          /* marked as subscription, but did not include any subscription id */
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE].transactU2UCompleted Subscription-id missing from transaction marked as unattended/automated transaction.  transactionID:{0}", tID);
                            break;
                        case GloebitAPI.TransactionFailure.SUBSCRIPTION_NOT_FOUND:                /* No subscription exists under id provided */
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE].transactU2UCompleted Gloebit can not identify subscription from identifier(s).  transactionID:{0}, subscription-id:{1}, app-subscription-id:{2}", tID, subscriptionIDStr, appSubscriptionIDStr);
                            // TODO: We should wipe this subscription from the DB and re-create it.
                            break;
                        case GloebitAPI.TransactionFailure.SUBSCRIPTION_AUTH_NOT_FOUND:           /* No sub_auth has been created for this user for this subscription */
                            m_log.InfoFormat("[GLOEBITMONEYMODULE].transactU2UCompleted Gloebit No subscription authorization in place.  transactionID:{0}, subscription-id:{1}, app-subscription-id:{2} PayerID:{3} PayerName:{4}", tID, subscriptionIDStr, appSubscriptionIDStr, txn.PayerID, txn.PayerName);
                            // TODO: Should we store auths so we know if we need to create it before submitting or just to ask user to auth after failed txn?
                            // We have a valid subscription, but no subscription auth for this user-id-on-app+token(gloebit_uid) combo
                            // TODO: should we validate that SubscriptionIDStr matches what we have on file?
                            break;
                        case GloebitAPI.TransactionFailure.SUBSCRIPTION_AUTH_PENDING:             /* User has not yet approved or declined the authorization for this subscription */
                            // Subscription-authorization has already been created.
                            // User has not yet responded to that request.
                            m_log.InfoFormat("[GLOEBITMONEYMODULE] transactU2UCompleted - status:{0} \n subID:{1} appSubID:{2} apiUrl:{3} ", status, subscriptionIDStr, appSubscriptionIDStr, m_url);
                            break;
                        case GloebitAPI.TransactionFailure.SUBSCRIPTION_AUTH_DECLINED:            /* User has declined the authorization for this subscription */
                            m_log.InfoFormat("[GLOEBITMONEYMODULE] transactU2UCompleted - FAILURE -- user declined subscription auth.  id:{0}", tID);

                            // TODO: We should really send another dialog here like the PendingDialog instead of just a url here.
                            // Send dialog asking user to auth or report --- needs different message.
                            m_log.Info("[GLOEBITMONEYMODULE] TransactU2UCompleted - SUBSCRIPTION_AUTH_DECLINED - requesting SubAuth approval");
                            break;
                        case GloebitAPI.TransactionFailure.PAYER_ACCOUNT_LOCKED:                  /* Buyer's Gloebit account is locked and not allowed to spend gloebits */
                            m_log.InfoFormat("[GLOEBITMONEYMODULE] transactU2UCompleted - FAILURE -- payer account locked.  id:{0}", tID);
                            break;
                        case GloebitAPI.TransactionFailure.PAYEE_CANNOT_BE_IDENTIFIED:            /* can not identify merchant from params supplied by app */
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE].transactU2UCompleted Gloebit could not identify payee from params.  transactionID:{0} payeeID:{1}", tID, payeeUser.PrincipalID);
                            break;
                        case GloebitAPI.TransactionFailure.PAYEE_CANNOT_RECEIVE:                  /* Seller's Gloebit account can not receive Gloebits */
                            // TODO: research if/when account is in this state.  Only by admin?  All accounts until merchants?
                            m_log.InfoFormat("[GLOEBITMONEYMODULE] transactU2UCompleted - FAILURE -- payee account locked - can't receive gloebits.  id:{0}", tID);
                            break;
                        default:
                            // Shouldn't get here.
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE].transactU2UCompleted unhandled validation failure:{0}  transactionID:{1}", failure, tID);
                            additionalDetailStr = reason;
                            break;
                    }
                    break;
                case GloebitAPI.TransactionStage.QUEUE:
                    switch (failure) {
                        case GloebitAPI.TransactionFailure.QUEUEING_FAILED:                     /* failed to queue.  net or processor error */
                            m_log.InfoFormat("[GLOEBITMONEYMODULE] transactU2UCompleted - FAILURE -- queuing failed.  id:{0}", tID);
                            break;
                        case GloebitAPI.TransactionFailure.RACE_CONDITION:                      /* race condition - already queued */
                            // nothing to tell user.  buyer doesn't need to know it was double submitted
                            m_log.ErrorFormat ("[GLOEBITMONEYMODULE].transactU2UCompleted race condition.  You double submitted transaction:{0}", tID);
                            return; /* don't report anything as the other flow this hit will handle reporting */
                        default:
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE].transactU2UCompleted unhandled queueing failure:{0}  transactionID:{1}", failure, tID);
                            additionalDetailStr = reason;
                            break;
                    }
                    break;
                default:
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE].transactU2UCompleted unhandled Transaction Stage:{0} failure:{1}  transactionID:{2}", stage, failure, tID);
                    additionalDetailStr = reason;
                    break;
            }
            m_transactionAlerts.AlertTransactionFailed(txn, stage, failure, additionalDetailStr, extraData);
            return;
        }
            
        /*** GloebitAPI Required HTTP Callback Entrance Points --- must be registered by GMM ***/
        /// <summary>
        /// Registered to the enactHoldURI, consumeHoldURI and cancelHoldURI from GloebitTransaction.
        /// Called by the Gloebit transaction processor.
        /// Enacts, cancels, or consumes the GloebitAPI.Asset.
        /// Response of true certifies that the Asset transaction part has been processed as requested.
        /// Response of false alerts transaction processor that asset failed to process as requested.
        /// Additional data can be returned about failures, specifically whether or not to retry.
        /// --- returnMsg of "pending" signifies that this should be retried as opposed to a permanent failure
        /// --- and will requeue the transaction for processing
        /// </summary>
        /// <param name="requestData">GloebitAPI.Asset enactHoldURI, consumeHoldURI or cancelHoldURI query arguments tying this callback to a specific Asset.</param>
        /// <returns>
        /// Web response including JSON array of one or two elements.  First element is bool representing success state of call.
        /// --- If first element is false, the second element is a string providing the reason for failure.
        /// --- If the second element is "pending", then the transaction processor will retry.
        /// --- All other reasons are considered permanent failure.
        /// </returns>
        public Hashtable transactionState_func(Hashtable requestData) {
            m_log.DebugFormat("[GLOEBITMONEYMODULE] transactionState_func **************** Got Callback");
            foreach(DictionaryEntry e in requestData) { m_log.DebugFormat("{0}: {1}", e.Key, e.Value); }

            // TODO: check that these exist in requestData.  If not, signal error and send response with false.
            string transactionIDstr = requestData["id"] as string;
            string stateRequested = requestData["state"] as string;
            string returnMsg = "";

            bool success = GloebitTransaction.ProcessStateRequest(transactionIDstr, stateRequested, m_assetCallbacks, m_transactionAlerts, out returnMsg);

            //JsonValue[] result;
            //JsonValue[0] = JsonValue.CreateBooleanValue(success);
            //JsonValue[1] = JsonValue.CreateStringValue("blah");
            // JsonValue jv = JsonValue.Parse("[true, \"blah\"]")
            //JsonArray ja = new JsonArray();
            //ja.Add(JsonValue.CreateBooleanValue(success));
            //ja.Add(JsonValue.CreateStringValue("blah"));

            OSDArray paramArray = new OSDArray();
            paramArray.Add(success);
            if (!success) {
                paramArray.Add(returnMsg);
            }

            // TODO: build proper response with json
            Hashtable response = new Hashtable();
            response["int_response_code"] = 200;
            //response["str_response_string"] = ja.ToString();
            response["str_response_string"] = OSDParser.SerializeJsonString(paramArray);
            response["content_type"] = "application/json";
            m_log.InfoFormat("[GLOEBITMONEYMODULE].transactionState_func response:{0}", OSDParser.SerializeJsonString(paramArray));
            return response;
        }

        #endregion // Transaction

        #region Subscription

        /**********************/
        /**** SUBSCRIPTION ****/
        /**********************/

        /// <summary>
        /// Creates a GloebitSubscription locally with the id (or random id if null), name and description provided and then calls the GloebitAPI.CreateSubscription method
        /// to request that the Gloebit service create this subscription for this app on the Gloebit servers.
        /// When a user is asked to authorize this subscription from the Gloebit website, they will see the ID name and description.
        /// The method Verifies that a local subscription with this id doesn't already exist locally to avoid overwriting existing subscriptions.
        /// See CreateSubscriptionCompleted for server response regarding creation of this subscription for the app within the Gloebit service.
        /// </summary>
        /// <param name="appSubID">UUID for the subscription to be created.  If UUID.Zero, ID is generated randomly within method.</param>
        /// <param name="subName">Name for this subscription - This is required by the Gloebit service and can not be blank.</param>
        /// <param name="subDescription">Description for this subscription - This is required by the Gloebit service and can not be blank.</param>
        /// <returns>
        /// On success, returns the ID provided, or the random local app Subscription ID generated
        /// On failure, returns UUID.Zero
        /// </returns>
        public UUID CreateSubscription(UUID appSubID, string subName, string subDesc)
        {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPIWrapper.CreateSubscription for appSubID:{0}, subName:{1}, subDesc:{2}", appSubID, subName, subDesc);
            // Validate that subName and subDesc are not empty or null as Gloebit requires both for a Subscription creation
            if (String.IsNullOrEmpty(subName) || String.IsNullOrEmpty(subDesc)) {
                m_log.WarnFormat("[GLOEBITMONEYMODULE] GloebitAPIWrapper.CreateSubscription - Can not create subscription because subscription name or description is blank - Name:{0} Description:{1}", subName, subDesc);
                //TODO: should this throw an exception?
                return UUID.Zero;
            }

            // If no local appSubID provided, then generate one randomly
            bool idIsRandom = false;
            if (appSubID == UUID.Zero) {
                // Create a transaction ID
                appSubID = UUID.Random();
                idIsRandom = true;
            }

            // Create a local subscription
            GloebitSubscription sub = null;
            // Double check that a local subscription hasn't already been created
            sub = GloebitSubscription.Get(appSubID, m_key, m_url);
            if(sub != null) {
                m_log.WarnFormat("[GLOEBITMONEYMODULE] GloebitAPIWrapper.CreateSubscription found existing local sub for appSubID:{0}", appSubID);
                // TODO: Should we check to see if there is a SubscriptionID on sub which would mean that this was already created on Gloebit as well?
                //       For now, we'll assume that this could be an attempt to recreate after an issue and that Gloebit will return the Subscription ID
                //       on a duplicate create request and that this will refresh that ID for the app.
                if(idIsRandom) {
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPIWrapper.CreateSubscription randomly generated appSubID:{0} conflicted with existing sub", appSubID);
                    return UUID.Zero;
                }
                // TODO: Should consider checking that name and desc match, but can't do so until we verify that OpenSim integration doesn't need adjustment.
                //       Can't recall if the UUID of an object is changed when the name or desc are updated.  If not, we need to handle that in GMM first.
            }
            if(sub == null) {
                m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPIWrapper.CreateSubscription - creating local subscription for {0}", subName);
                // Create local sub in cache and db
                sub = GloebitSubscription.Init(appSubID, m_key, m_url.ToString(), subName, subDesc);
            }

            // Ask Gloebit to create this subscription on the server
            m_api.CreateSubscription(sub, m_platformAccessors.GetBaseURI());
            // TODO: should we handle false return from api call?
            return appSubID;
        }

        /**** IAsyncEndpointCallback Interface Function ****/
        /// <summary>
        /// Called by the GloebitAPI after the async call to CreateSubscription completes.
        /// If this was successful, then the subscription was created on the server and the GloebitAPI
        /// has recorded the id of the subscription on the server into the SubscriptionID field of the
        /// local subscription and marked it enabled.
        /// This function parses the response from Gloebit, logs useful information, and calls ISubscriptionAlert
        /// interface functions with the data needed by the platform for any processing.
        /// </summary>
        /// <param name="responseDataMap">OSDMap of response data from GloebitAPI.CreateSubscription</param>
        /// <param name="subscription">local GloebitSubscription detailing this subscription.</param>
        public void createSubscriptionCompleted(OSDMap responseDataMap, GloebitSubscription subscription)
        {
            bool success = (bool)responseDataMap["success"];
            string reason = responseDataMap["reason"];
            string status = responseDataMap["status"];

            if (success) {
                m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPIWrapper.createSubscriptionCompleted with SUCCESS reason:{0} status:{1}", reason, status);
                m_subscriptionAlerts.AlertSubscriptionCreated(subscription);
                return;

            } else if (status == "retry") {                                /* failure could be temporary -- retry. */
                m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPIWrapper.createSubscriptionCompleted with FAILURE but suggested retry.  reason:{0}", reason);
                // TODO: Should we retry?  How do we prevent infinite loop?
            } else if (status == "failed") {                                /* failure permanent -- requires fixing something. */
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPIWrapper.createSubscriptionCompleted with FAILURE permanently.  reason:{0}", reason);
                // TODO: Any action required
            } else {                                                        /* failure - unexpected status */
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPIWrapper.createSubscriptionCompleted with FAILURE - unhandled status:{0} reason:{1}", status, reason);
            }
            // If we added to this map.  remove so we're not leaking memory in failure cases.
            m_subscriptionAlerts.AlertSubscriptionCreationFailed(subscription);  // TODO: determine if interface really requires this
            return;
        }

        /// <summary>
        /// Ask user to authorize a subscription for an agent on an app linked to their Gloebit account.
        /// This builds the URI which must be loaded for the user to take action.
        /// The subscription authorization must be created first if this has not already been done.
        /// Once created, The user can also authorize or decline it on their own from the Subscriptions 
        /// section of their Gloebit account.
        /// </summary>
        /// <param name="agentID">ID of the agent on this app being asked to authorize a subscription.</param>
        /// <param name="subAuthID">
        /// ID of the authorization request the user will be asked to approve.
        /// --- This is provided by Gloebit in the response to CreateSubscriptionAuthorization.
        /// --- value of String.Empty or null signifies that we must first call CreateSubscriptionAuthorization
        ///     to ether create or retrieve this SubscriptionAuthorization.</param>
        /// <param name="sub">GloebitSubscription which this authorization request is for.</param>
        /// <param name="isDeclined">Bool is true if this sub auth has already been declined by the user, in which case different messaging may be necessary.</param>
        public void AuthorizeSubscription(UUID agentID, string subAuthID, GloebitSubscription sub, bool isDeclined)
        {
            GloebitUser user = GloebitUser.Get(m_key, agentID);
            AuthorizeSubscription(user, subAuthID, sub, isDeclined);
        }

        /// <summary>
        /// Ask user to authorize a subscription for an agent on an app linked to their Gloebit account.
        /// This builds the URI which must be loaded for the user to take action.
        /// The subscription authorization must be created first if this has not already been done.
        /// Once created, The user can also authorize or decline it on their own from the Subscriptions 
        /// section of their Gloebit account.
        /// </summary>
        /// <param name="user">GloebitUser representing the agent on an app being asked to authorize a subscription</param>
        /// <param name="subAuthID">
        /// ID of the authorization request the user will be asked to approve.
        /// --- This is provided by Gloebit in the response to CreateSubscriptionAuthorization.
        /// --- value of String.Empty or null signifies that we must first call CreateSubscriptionAuthorization
        ///     to ether create or retrieve this SubscriptionAuthorization.</param>
        /// <param name="sub">GloebitSubscription which this authorization request is for.</param>
        /// <param name="isDeclined">Bool is true if this sub auth has already been declined by the user, in which case different messaging may be necessary.</param>
        public void AuthorizeSubscription(GloebitUser user, string subAuthID, GloebitSubscription sub, bool isDeclined)
        {
            // Call create if necessary
            // Create response will call the send func
            // Else just call send func

            if (String.IsNullOrEmpty(subAuthID)) {
                // TODO: once we start storing Subscription Authorizations, look it up.
                m_api.CreateSubscriptionAuthorization(sub, user, m_platformAccessors.resolveAgentName(UUID.Parse(user.PrincipalID)), m_platformAccessors.GetBaseURI());
            } else {
                SendSubscriptionAuthorizationToUser(user, subAuthID, sub, isDeclined);
            }
        }

        /**** IAsyncEndpointCallback Interface Function ****/
        /// <summary>
        /// Called by the GloebitAPI after the async call to CreateSubscriptionAuthorization completes.
        /// If this was successful, then the subscription authorization was created or located on the Gloebit service
        /// and the ID of the subscription authorization was returned in the response.
        /// This function parses the response from Gloebit, logs useful information, builds the 
        /// URI where the user can authorize the subscription, and asks the platform to send that
        /// URI to the user.
        /// </summary>
        /// <param name="responseDataMap">OSDMap of response data from GloebitAPI.CreateSubscriptionAuthorization</param>
        /// <param name="sub">local GloebitSubscription detailing the subscription the authorization is for.</param>
        /// <param name="user">GloebitUser representing the agent and app the subscription authorization is for.</param>
        public void createSubscriptionAuthorizationCompleted(OSDMap responseDataMap, GloebitSubscription sub, GloebitUser user) {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPIWrapper.createSubscriptionAuthorizationCompleted");

            bool success = (bool)responseDataMap["success"];
            string reason = responseDataMap["reason"];
            string status = responseDataMap["status"];

            UUID agentID = UUID.Parse(user.PrincipalID);

            if (success) {
                m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPIWrapper.createSubscriptionAuthorizationCompleted with SUCCESS reason:{0} status:{1}", reason, status);
                switch (status) {
                    case "success":
                    case "created":
                    case "duplicate":
                        // grab subscription_authorization_id
                        string subAuthID = responseDataMap["id"];

                        // Send Authorize URL
                        SendSubscriptionAuthorizationToUser(user, subAuthID, sub, false);

                        break;
                    case "duplicate-and-already-approved-by-user":
                        // TODO: if we have a transaction pending, should we trigger it?
                        break;
                    case "duplicate-and-previously-declined-by-user":
                        // grab subscription_authorization_id
                        string declinedSubAuthID = responseDataMap["id"];
                        // TODO: Should we send a dialog message or just the url?
                        // Send Authorize URL
                        SendSubscriptionAuthorizationToUser(user, declinedSubAuthID, sub, true);
                        break;
                    default:
                        break;
                    }
            } else if (status == "retry") {                                /* failure could be temporary -- retry. */
                m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPIWrapper.createSubscriptionAuthorizationCompleted with FAILURE but suggested retry.  reason:{0}", reason);

                // TODO: Should we retry?  How do we prevent infinite loop?

            } else if (status == "failed") {                                /* failure permanent -- requires fixing something. */
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPIWrapper.createSubscriptionAuthorizationCompleted with FAILURE permanently.  reason:{0}", reason);

                // TODO: Any action required?
                // TODO: if we move "duplicate-and-previously-declined-by-user" to here, then we should handle it here and we need another endpoint to reset status of this subscription auth to pending

            } else {                                                        /* failure - unexpected status */
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPIWrapper.createSubscriptionAuthorizationCompleted with FAILURE - unhandled status:{0} reason:{1}", status, reason);
            }
            return;
        }

        /// <summary>
        /// Ask user to authorize a subscription.
        /// This builds the URI which must be loaded for the user to take action.
        /// The subscription authorization must be created first.
        /// See AuthorizeSubscription for the public method which calls this method.
        /// </summary>
        /// <param name="user">GloebitUser we are sending the URL to</param>
        /// <param name="subAuthID">ID of the authorization request the user will be asked to approve - provided by Gloebit.</param>
        /// <param name="sub">GloebitSubscription which contains necessary details for message to user.</param>
        /// <param name="isDeclined">Bool is true if this sub auth has already been declined by the user which should present different messaging.</param>
        private void SendSubscriptionAuthorizationToUser(GloebitUser user, string subAuthID, GloebitSubscription sub, bool isDeclined)
        {
            // Build the URL -- consider making a helper to be done in the API once we move this to the GMM
            Uri request_uri = m_api.BuildSubscriptionAuthorizationURI(subAuthID);

            //*********** SEND SUBSCRIPTION AUTHORIZATION REQUEST URI TO USER ***********//

            // Tell platform to Load the SubAuthURL for the user
            m_uriLoaders.LoadSubscriptionAuthorizationUriForUser(user, request_uri, sub, isDeclined);
        }

        #endregion // Subscription

    }
}
