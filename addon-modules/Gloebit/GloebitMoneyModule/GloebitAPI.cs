/*
 * GloebitAPI.cs is part of OpenSim-MoneyModule-Gloebit 
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
 * GloebitAPI.cs
 *
 * Handles communication with Gloebit's RESTful API services 
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using log4net;

// TODO: convert OSDMaps to Dictionaries and UUIDs to GUIDs and remove requirement for OpenMetaverse libraries to make this more generic.
using OpenMetaverse;
using OpenMetaverse.StructuredData;

// TODO: consider making this a strict REST API using dictionary forms rather than objects and moving the object implementation
//       to an API wrapper which uses this API.  The separation might make both easier to maintain as this is ported to
//       new platforms.

namespace Gloebit.GloebitMoneyModule {

    public class GloebitAPI {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public readonly string m_key;
        private string m_keyAlias;
        private string m_secret;
        public readonly Uri m_url;


        
        public interface IAsyncEndpointCallback {
            void exchangeAccessTokenCompleted(bool success, GloebitUser user, OSDMap responseDataMap);
            // TODO: may change this to transactCompleted and add a bool for u2u
            void transactU2UCompleted(OSDMap responseDataMap, GloebitUser payerUser, GloebitUser payeeUser, GloebitTransaction transaction, TransactionStage stage, TransactionFailure failure);
            void createSubscriptionCompleted(OSDMap responseDataMap, GloebitSubscription subscription);
            void createSubscriptionAuthorizationCompleted(OSDMap responseDataMap, GloebitSubscription subscription, GloebitUser sender);
        }
        
        public static IAsyncEndpointCallback m_asyncEndpointCallbacks;
        
        private delegate void CompletionCallback(OSDMap responseDataMap);

        private class GloebitRequestState {
            
            // Web request variables
            public HttpWebRequest request;
            public Stream responseStream;
            
            // Variables for storing Gloebit response stream data asynchronously
            public const int BUFFER_SIZE = 1024;    // size of buffer for max individual stream read events
            public byte[] bufferRead;               // buffer read to by stream read events
            public Decoder streamDecoder;           // Decoder for converting buffer to string in parts
            public StringBuilder responseData;      // individual buffer reads compiled/appended to full data

            public CompletionCallback continuation;
            
            // TODO: What to do when error states are reached since there is no longer a return?  Should we store an error state in a member variable?
            
            // Preferred constructor - use if we know the endpoint and agentID at creation time.
            public GloebitRequestState(HttpWebRequest req, CompletionCallback continuation)
            {
                request = req;
                responseStream = null;
                
                bufferRead = new byte[BUFFER_SIZE];
                streamDecoder = Encoding.UTF8.GetDecoder();     // Create Decoder for appropriate encoding type.
                responseData = new StringBuilder(String.Empty);

                this.continuation = continuation;
            }
            
        }

        public GloebitAPI(string key, string keyAlias, string secret, Uri url, IAsyncEndpointCallback asyncEndpointCallbacks) {
            m_key = key;
            m_keyAlias = keyAlias;
            m_secret = secret;
            m_url = url;
            m_asyncEndpointCallbacks = asyncEndpointCallbacks;
        }
        
        /************************************************/
        /******** OAUTH2 AUTHORIZATION FUNCTIONS ********/
        /************************************************/


        /// <summary>
        /// Helper function to build the auth redirect callback url consistently everywhere.
        /// <param name="baseURI">The base url where this server's http services can be accessed.</param>
        /// <param name="agentId">The uuid of the agent being authorized.</param>
        /// </summary>
        private static Uri BuildAuthCallbackURL(Uri baseURI, string agentId) {
            UriBuilder redirect_uri = new UriBuilder(baseURI);
            redirect_uri.Path = "gloebit/auth_complete";
            redirect_uri.Query = String.Format("agentId={0}", agentId);
            return redirect_uri.Uri;
        }

        /// <summary>
        /// Request Authorization for this grid/region to enact Gloebit functionality on behalf of the specified OpenSim user.
        /// Sends Authorize URL to user which will launch a Gloebit authorize dialog.  If the user launches the URL and approves authorization from a Gloebit account, an authorization code will be returned to the redirect_uri.
        /// This is how a user links a Gloebit account to this OpenSim account.
        /// </summary>
        /// <param name="user">GloebitUser for which this app is asking for permission to enact Gloebit functionality.</param>
        /// <param name="userName">string name of user on this app.</param>
        /// <param name="baseURI">URL where Gloebit can send the auth response back to this app.</param>
        public Uri BuildAuthorizationURI(GloebitUser user, string userName, Uri baseURI) {

            //********* BUILD AUTHORIZE QUERY ARG STRING ***************//
            ////Dictionary<string, string> auth_params = new Dictionary<string, string>();
            OSDMap auth_params = new OSDMap();

            auth_params["client_id"] = m_key;
            if(!String.IsNullOrEmpty(m_keyAlias)) {
                auth_params["r"] = m_keyAlias;
            }

            auth_params["scope"] = "balance transact";
            auth_params["redirect_uri"] = BuildAuthCallbackURL(baseURI, user.PrincipalID).ToString();
            auth_params["response_type"] = "code";
            auth_params["user"] = userName;
            auth_params["uid"] = user.PrincipalID;
            // TODO - make use of 'state' param for XSRF protection
            // auth_params["state"] = ???;

            string query_string = BuildURLEncodedParamString(auth_params);

            m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.Authorize query_string: {0}", query_string);

            //********** BUILD FULL AUTHORIZE REQUEST URI **************//

            Uri request_uri = new Uri(m_url, String.Format("oauth2/authorize?{0}", query_string));
            m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPI.Authorize request_uri: {0}", request_uri);
            
            //*********** SEND AUTHORIZE REQUEST URI TO USER ***********//
            
            // currently can not launch browser directly for user, so ask platform to Load the AuthorizeURL for the user
            return(request_uri);
        }
        
        /// <summary>
        /// Begins request to exchange an authorization code granted from the Authorize endpoint for an access token necessary for enacting Gloebit functionality on behalf of this user.
        /// This begins the second phase of the OAuth2 process.  It is activated by the redirect_uri of the Authorize function.
        /// This occurs completely behind the scenes for security purposes.
        /// </summary>
        /// <returns>The authenticated User object containing the access token necessary for enacting Gloebit functionality on behalf of this user.</returns>
        /// <param name="user">GloebitUser for which this app is asking for permission to enact Gloebit functionality.</param>
        /// <param name="auth_code">Authorization Code returned to the redirect_uri from the Gloebit Authorize endpoint.</param>
        public void ExchangeAccessToken(GloebitUser user, string auth_code, Uri baseURI) {
            
            m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPI.ExchangeAccessToken AgentID:{0}", user.PrincipalID);
            
            UUID agentID = UUID.Parse(user.PrincipalID);
            
            // ************ BUILD EXCHANGE ACCESS TOKEN POST REQUEST ******** //
            OSDMap auth_params = new OSDMap();

            auth_params["client_id"] = m_key;
            auth_params["client_secret"] = m_secret;
            auth_params["code"] = auth_code;
            auth_params["grant_type"] = "authorization_code";
            auth_params["scope"] = "balance transact";
            auth_params["redirect_uri"] = BuildAuthCallbackURL(baseURI, user.PrincipalID).ToString();
            
            HttpWebRequest request = BuildGloebitRequest("oauth2/access-token", "POST", null, "application/x-www-form-urlencoded", auth_params);
            if (request == null) {
                // ERROR
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.oauth2/access-token failed to create HttpWebRequest");
                // TODO: signal error
                return;
            }
            
            // **** Asynchronously make web request **** //
            IAsyncResult r = request.BeginGetResponse(GloebitWebResponseCallback,
                new GloebitRequestState(request,
                    delegate(OSDMap responseDataMap) {
                        // ************ PARSE AND HANDLE EXCHANGE ACCESS TOKEN RESPONSE ********* //

                        string token = responseDataMap["access_token"];
                        string app_user_id = responseDataMap["app_user_id"];
                        bool success = false;
                        // TODO - do something to handle the "refresh_token" field properly
                        if(!String.IsNullOrEmpty(token)) {
                            success = true;
                            user = GloebitUser.Authorize(m_key, agentID, token, app_user_id);
                            m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPI.CompleteExchangeAccessToken Success User:{0}", user);
                        } else {
                            success = false;
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.CompleteExchangeAccessToken error: {0}, reason: {1}", responseDataMap["error"], responseDataMap["reason"]);
                            // TODO: signal error;
                        }
                        m_asyncEndpointCallbacks.exchangeAccessTokenCompleted(success, user, responseDataMap);
                    }));
        }
        
        
        /***********************************************/
        /********* GLOEBIT FUNCTIONAL ENDPOINS *********/
        /***********************************************/
        
        // ******* GLOEBIT BALANCE ENDPOINTS ********* //
        // requires "balance" in scope of authorization token
        

        /// <summary>
        /// Requests the Gloebit balance for the OpenSim user with this OpenSim agentID.
        /// Returns zero if a link between this OpenSim user and a Gloebit account have not been created and the user has not granted authorization to this grid/region.
        /// Requires "balance" in scope of authorization token.
        /// </summary>
        /// <returns>The Gloebit balance for the Gloebit account the user has linked to this OpenSim agentID on this grid/region.  Returns zero if a link between this OpenSim user and a Gloebit account has not been created and the user has not granted authorization to this grid/region.</returns>
        /// <param name="user">GloebitUser object for the OpenSim user for whom the balance request is being made. <see cref="GloebitUser.Get(UUID)"/></param>
        /// <param name="invalidatedToken">Bool set to true if request fails due to a bad token which we have invalidated.  Eventually, this should be a more general error interface</param>
        /// <returns>Double balance of user or 0.0 if fails for any reason</returns>
        public double GetBalance(GloebitUser user, out bool invalidatedToken) {
            
            invalidatedToken = false;
            
            //************ BUILD GET BALANCE GET REQUEST ********//
            
            HttpWebRequest request = BuildGloebitRequest("balance", "GET", user);
            if (request == null) {
                // ERROR
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.balance failed to create HttpWebRequest");
                return 0;
            }
            
            //************ PARSE AND HANDLE GET BALANCE RESPONSE *********//
            
            HttpWebResponse response = (HttpWebResponse) request.GetResponse();
            string status = response.StatusDescription;
            m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPI.balance status:{0}", status);
            using(StreamReader response_stream = new StreamReader(response.GetResponseStream())) {
                string response_str = response_stream.ReadToEnd();

                OSDMap responseData = (OSDMap)OSDParser.DeserializeJson(response_str);
                m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.balance responseData:{0}", responseData.ToString());

                if (responseData["success"]) {
                    double balance = responseData["balance"].AsReal();
                    return balance;
                } else {
                    string reason = responseData["reason"];
                    switch(reason) {
                        case "unknown token1":
                        case "unknown token2":
                            // The token is invalid (probably the user revoked our app through the website)
                            // so force a reauthorization next time.
                            m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.GetBalance failed - Invalidating Token");
                            user.InvalidateToken();
                            invalidatedToken = true;
                            break;
                        default:
                            m_log.ErrorFormat("Unknown error getting balance, reason: '{0}'", reason);
                            break;
                    }
                    return 0.0;
                }
            }

        }
        
        // ******* GLOEBIT TRANSACT ENDPOINTS ********* //
        // requires "transact" in scope of authorization token

        /// <summary>
        /// Asynchronously requests Gloebit transaction from the sender to the owner of the Gloebit app this module is connected to.
        /// </summary>
        /// <remarks>
        /// Asynchronous.
        /// Upon async response: parses response data, records response in txn, creates TransactionStage and TransactionFailure from response strings,
        /// handles any necessary failure processing, and calls TransactCompleted callback with response data for module to process and message user.
        /// </remarks>
        /// <param name="txn">GloebitTransaction representing local transaction we are requesting.  This is prebuilt by GMM, and already includes most transaciton details such as amount, payer id and name.  <see cref="GloebitTransaction"/></param>
        /// <param name="description">Description of purpose of transaction recorded in Gloebit transaction histories.  Should eventually be added to txn and removed as parameter</param>
        /// <param name="descMap">Map of platform, location & transaction descriptors for tracking/querying and transaction history details.  For more details, <see cref="GloebitMoneyModule.buildBaseTransactionDescMap"/> helper function.</param>
        /// <param name="payerUser">GloebitUser object for the user sending the gloebits. <see cref="GloebitUser.Get(UUID)"/></param>
        /// <param name="baseURI">The base url where this server's http services can be accessed.  Used by enact/consume/cancel callbacks for local transaction part requiring processing.</param>
        /// <returns>true if async transact web request was built and submitted successfully; false if failed to submit request;  If true, IAsyncEndpointCallback transactCompleted should eventually be called with additional details on state of request.</returns>
        public bool Transact(GloebitTransaction txn, string description, OSDMap descMap, GloebitUser payerUser, Uri baseURI) {
            
            m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPI.transact senderID:{0} senderName:{1} amount:{2} description:{3}", payerUser.PrincipalID, txn.PayerName, txn.Amount, description);
            
            // ************ BUILD AND SEND TRANSACT POST REQUEST ************ //

            OSDMap transact_params = new OSDMap();
            PopulateTransactParamsBase(transact_params, txn, description, payerUser.GloebitID, descMap, baseURI);
            
            HttpWebRequest request = BuildGloebitRequest("v2/transact", "POST", payerUser, "application/json", transact_params);
            if (request == null) {
                // ERROR
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.transact failed to create HttpWebRequest");
                return false;
                // TODO once we return, return error value
            }
                    
            m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.Transact about to BeginGetResponse");
            // **** Asynchronously make web request **** //
            IAsyncResult r = request.BeginGetResponse(GloebitWebResponseCallback,
			                                          new GloebitRequestState(request, 
			                        delegate(OSDMap responseDataMap) {
                                        
                m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.Transact response: {0}", responseDataMap);

                //************ PARSE AND HANDLE TRANSACT-U2U RESPONSE *********//

                // read response and store in txn
                PopulateTransactResponse(txn, responseDataMap);
                                        
                // Build Stage & Failure arguments
                 TransactionStage stage = TransactionStage.BEGIN;
                 TransactionFailure failure = TransactionFailure.NONE;
                // TODO: should we pass the txn instead of the individual string args here?
                PopulateTransactStageAndFailure(out stage, out failure, txn.ResponseSuccess, txn.ResponseStatus, txn.ResponseReason);
                // TODO: consider making stage & failure part of the GloebitTransactions table.
                // still pass explicitly to make sure they can't be modified before callback uses them.
                                        
                // Handle any necessary functional adjustments based on failures
                ProcessTransactFailure(txn, failure, payerUser);

                m_asyncEndpointCallbacks.transactU2UCompleted(responseDataMap, payerUser, null, txn, stage, failure);
            }));
            
            // Successfully submitted transaction request to Gloebit
            txn.Submitted = true;
            // TODO: if we add stage to txn, we should set it to TransactionStage.SUBMIT here.
            GloebitTransactionData.Instance.Store(txn);
            return true;
        }
        

        

        // TODO: does recipient have to authorize app?  Do they need to become a merchant on that platform or opt in to agreeing to receive gloebits?  How do they currently authorize sale on a grid?
        // TODO: Should we pass a bool for charging a fee or the actual fee % -- to the module owner --- could always charge a fee.  could be % set in app for when charged.  could be % set for each transaction type in app.
        // TODO: Should we always charge our fee, or have a bool or transaction type for occasions when we may not charge?
        // TODO: Do we need an endpoint for reversals/refunds, or just an admin interface from Gloebit?
        
        /// <summary>
        /// Asynchronously request Gloebit transaction from the sender to the recipient with the details specified in txn.
        /// </summary>
        /// <remarks>
        /// Asynchronous.  See alternate synchronous transaction if caller needs immediate success/failure response regarding transaction in synchronous flow.
        /// Upon async response: parses response data, records response in txn, creates TransactionStage and TransactionFailure from response strings,
        /// handles any necessary failure processing, and calls TransactU2UCompleted callback with response data for module to process and message user.
        /// </remarks>
        /// <param name="txn">GloebitTransaction representing local transaction we are requesting.  This is prebuilt by GMM, and already includes most transaciton details such as amount, payer/payee id and name.  <see cref="GloebitTransaction"/></param>
        /// <param name="description">Description of purpose of transaction recorded in Gloebit transaction histories.  Should eventually be added to txn and removed as parameter</param>
        /// <param name="descMap">Map of platform, location & transaction descriptors for tracking/querying and transaction history details.  For more details, <see cref="GloebitMoneyModule.buildBaseTransactionDescMap"/> helper function.</param>
        /// <param name="sender">GloebitUser object for the user sending the gloebits. <see cref="GloebitUser.Get(UUID)"/></param>
        /// <param name="recipient">GloebitUser object for the user receiving the gloebits. <see cref="GloebitUser.Get(UUID)"/></param>
        /// <param name="recipientEmail">Email address of the user on this grid receiving the gloebits.  Empty string if user created account without email.</param>
        /// <param name="baseURI">The base url where this server's http services can be accessed.  Used by enact/consume/cancel callbacks for local transaction part requiring processing.</param>
        /// <returns>true if async transactU2U web request was built and submitted successfully; false if failed to submit request;  If true, IAsyncEndpointCallback transactU2UCompleted should eventually be called with additional details on state of request.</returns>
        public bool TransactU2U(GloebitTransaction txn, string description, OSDMap descMap, GloebitUser sender, GloebitUser recipient, string recipientEmail, Uri baseURI) {

            m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPI.Transact-U2U senderID:{0} senderName:{1} recipientID:{2} recipientName:{3} recipientEmail:{4} amount:{5} description:{6} baseURI:{7}", sender.PrincipalID, txn.PayerName, recipient.PrincipalID, txn.PayeeName, recipientEmail, txn.Amount, description, baseURI);
            
            // ************ IDENTIFY GLOEBIT RECIPIENT ******** //
            // 1. If the recipient has authed ever, we'll have a recipient.GloebitID to use.
            // 2. If not, and the recipient's account is on this grid, Get the email from the profile for the account.
            
            // ************ BUILD AND SEND TRANSACT U2U POST REQUEST ******** //
            
            // TODO: Assert that txn != null
            // TODO: Assert that transactionId != UUID.Zero
            
            // TODO: move away from OSDMap to a standard C# dictionary
            OSDMap transact_params = new OSDMap();
            PopulateTransactParamsBase(transact_params, txn, description, sender.GloebitID, descMap, baseURI);
            PopulateTransactParamsU2U(transact_params, txn, recipient.GloebitID, recipientEmail);
            
            HttpWebRequest request = BuildGloebitRequest("transact-u2u", "POST", sender, "application/json", transact_params);
            if (request == null) {
                // ERROR
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.Transact-U2U failed to create HttpWebRequest");
                return false;
                // TODO once we return, return error value
            }

            m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.Transact-U2U about to BeginGetResponse");
            // **** Asynchronously make web request **** //
            IAsyncResult r = request.BeginGetResponse(GloebitWebResponseCallback,
			                                          new GloebitRequestState(request, 
			                        delegate(OSDMap responseDataMap) {
                                        
                m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.Transact-U2U response: {0}", responseDataMap);

                //************ PARSE AND HANDLE TRANSACT-U2U RESPONSE *********//

                // read response and store in txn
                PopulateTransactResponse(txn, responseDataMap);
                                        
                // Build Stage & Failure arguments
                 TransactionStage stage = TransactionStage.BEGIN;
                 TransactionFailure failure = TransactionFailure.NONE;
                // TODO: should we pass the txn instead of the individual string args here?
                PopulateTransactStageAndFailure(out stage, out failure, txn.ResponseSuccess, txn.ResponseStatus, txn.ResponseReason);
                // TODO: consider making stage & failure part of the GloebitTransactions table.
                // still pass explicitly to make sure they can't be modified before callback uses them.
                                        
                // Handle any necessary functional adjustments based on failures
                ProcessTransactFailure(txn, failure, sender);

                // TODO - decide if we really want to issue this callback even if the token was invalid
                m_asyncEndpointCallbacks.transactU2UCompleted(responseDataMap, sender, recipient, txn, stage, failure);
            }));
            
            // Successfully submitted transaction request to Gloebit
            txn.Submitted = true;
            // TODO: if we add stage to txn, we should set it to TransactionStage.SUBMIT here.
            GloebitTransactionData.Instance.Store(txn);
            return true;
        }
        
        /// <summary>
        /// Synchronously request Gloebit transaction from the sender to the recipient with the details specified in txn.
        /// </summary>
        /// <remarks>
        /// Synchronous.  See alternate, and preferred, asynchronous transaction if caller does not need immediate success/failure response
        /// regarding transaction in synchronous flow.
        /// Upon sync response: parses response data, records response in txn, creates TransactionStage and TransactionFailure from response strings,
        /// handles any necessary failure processing, and calls TransactU2UCompleted callback with response data for module to process and message user.
        /// </remarks>
        /// <param name="txn">GloebitTransaction representing local transaction we are requesting.  This is prebuilt by GMM, and already includes most transaciton details such as amount, payer/payee id and name.  <see cref="GloebitTransaction"/></param>
        /// <param name="description">Description of purpose of transaction recorded in Gloebit transaction histories.  Should eventually be added to txn and removed as parameter</param>
        /// <param name="descMap">Map of platform, location & transaction descriptors for tracking/querying and transaction history details.  For more details, <see cref="GloebitMoneyModule.buildBaseTransactionDescMap"/> helper function.</param>
        /// <param name="sender">GloebitUser object for the user sending the gloebits. <see cref="GloebitUser.Get(UUID)"/></param>
        /// <param name="recipient">GloebitUser object for the user receiving the gloebits. <see cref="GloebitUser.Get(UUID)"/></param>
        /// <param name="recipientEmail">Email address of the user on this grid receiving the gloebits.  Empty string if user created account without email.</param>
        /// <param name="baseURI">The base url where this server's http services can be accessed.  Used by enact/consume/cancel callbacks for local transaction part requiring processing.</param>
        /// <param name="stage">TransactionStage handed back to caller representing stage of transaction that failed or completed.</param>
        /// <param name="failure">TransactionFailure handed back to caller representing specific transaction failure, or NONE.</param>
        /// <returns>
        /// true if sync transactU2U web request was built and submitted successfully and returned a successful response from the web service.
        /// --- successful response means that all Gloebit components of the transaction enacted successfully, and transaction would only fail if local
        ///     component enaction failed.  (Note: possibility of only "queue" success if resubmitted)
        /// false if failed to submit request, or if response returned false.
        /// --- See out parameters stage and failure for details on failure.
        /// If true, or if false in any stage after SUBMIT, IAsyncEndpointCallback transactU2UCompleted will be called with additional details on state of request prior to this function returning.
        /// </returns>
        public bool TransactU2USync(GloebitTransaction txn, string description, OSDMap descMap, GloebitUser sender, GloebitUser recipient, string recipientEmail, Uri baseURI, out TransactionStage stage, out TransactionFailure failure)
        {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPI.Transact-U2U-Sync senderID:{0} senderName:{1} recipientID:{2} recipientName:{3} recipientEmail:{4} amount:{5} description:{6} baseURI:{7}", sender.PrincipalID, txn.PayerName, recipient.PrincipalID, txn.PayeeName, recipientEmail, txn.Amount, description, baseURI);
            
            // ************ IDENTIFY GLOEBIT RECIPIENT ******** //
            // If the recipient has ever authorized, we have an AppUserID from Gloebit which will allow identification.
            // If not, Gloebit will attempt id from email.
            // TODO: If we use emails, we may need to make sure account merging works for email/3rd party providers.
            // TODO: If we allow anyone to receive, need to ensure that gloebits received are locked down until user authenticates as merchant.
            
            // ************ BUILD AND SEND TRANSACT U2U POST REQUEST ******** //
            
            // TODO: Assert that txn != null
            // TODO: Assert that transactionId != UUID.Zero
            
            // TODO: move away from OSDMap to a standard C# dictionary
            OSDMap transact_params = new OSDMap();
            PopulateTransactParamsBase(transact_params, txn, description, sender.GloebitID, descMap, baseURI);
            PopulateTransactParamsU2U(transact_params, txn, recipient.GloebitID, recipientEmail);
            ////PopulateTransactParams(transact_params, sender.GloebitID, txn, description, recipientEmail, recipient.GloebitID, descMap, baseURI);
            
            HttpWebRequest request = BuildGloebitRequest("transact-u2u", "POST", sender, "application/json", transact_params);
            if (request == null) {
                // ERROR
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.Transact-U2U-Sync failed to create HttpWebRequest");
                stage = TransactionStage.SUBMIT;
                failure = TransactionFailure.BUILD_WEB_REQUEST_FAILED;
                return false;
            }
            
            m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.Transact-U2U-Sync about to GetResponse");
            // **** Synchronously make web request **** //
            HttpWebResponse response = (HttpWebResponse) request.GetResponse();
            string status = response.StatusDescription;
            m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.Transact-U2U-Sync status:{0}", status);
            // TODO: think we should set submitted here to status
            if (response.StatusCode == HttpStatusCode.OK) {
                // Successfully submitted transaction request to Gloebit
                txn.Submitted = true;
                // TODO: if we add stage to txn, we should set it to TransactionStage.SUBMIT here.
                GloebitTransactionData.Instance.Store(txn);
                // TODO: should this alert that submission was successful?
            } else {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.Transact-U2U-Sync status not OK.  How to handle?");
                stage = TransactionStage.SUBMIT;
                failure = TransactionFailure.SUBMISSION_FAILED;
                return false;
            }
            
            //************ PARSE AND HANDLE TRANSACT-U2U RESPONSE *********//
            using(StreamReader response_stream = new StreamReader(response.GetResponseStream())) {
                // **** Synchronously read response **** //
                string response_str = response_stream.ReadToEnd();
                
                OSDMap responseDataMap = (OSDMap)OSDParser.DeserializeJson(response_str);
                m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.Transact-U2U-Sync responseData:{0}", responseDataMap.ToString());
                
                // read response and store in txn
                PopulateTransactResponse(txn, responseDataMap);
                
                // Populate Stage & Failure arguments based on response
                // TODO: should we pass the txn instead of the individual string args here?
                PopulateTransactStageAndFailure(out stage, out failure, txn.ResponseSuccess, txn.ResponseStatus, txn.ResponseReason);
                // TODO: consider making stage & failure part of the GloebitTransactions table.
                // still pass explicitly to make sure they can't be modified before callback uses them.
                
                // Handle any necessary functional adjustments based on failures
                ProcessTransactFailure(txn, failure, sender);
                
                // TODO - decide if we really want to issue this callback even if the token was invalid
                m_asyncEndpointCallbacks.transactU2UCompleted(responseDataMap, sender, recipient, txn, stage, failure);
                
                if (failure == TransactionFailure.NONE) {
                    // success.  could also check txn.ResponseSuccess or just return txn.ResponseSuccess
                    return true;
                } else {
                    // failure.
                    return false;
                }
            }
        }
        
        /* Transact U2U Helper Functions */
        
        /// <summary>
        /// Builds the base transact form parameters in the format that  GloebitAPI transact endpoints expect for a transaction.
        /// </summary>
        /// <param name="transact_params">OSDMap which will be populated with form parameters.</param>
        /// <param name="txn">GloebitTransaction representing local transaction we are create transact_params from.</param>
        /// <param name="description">Description of purpose of transaction recorded in Gloebit transaction histories.  Should eventually be added to txn and removed as parameter</param>
        /// <param name="senderGloebitID">UUID from the Gloebit system of user making payment.</param>
        /// <param name="descMap">Map of platform, location & transaction descriptors for tracking/querying and transaction history details.  For more details, <see cref="GloebitMoneyModule.buildBaseTransactionDescMap"/> helper function.</param>
        /// <param name="baseURI">The base url where this server's http services can be accessed.  Used by enact/consume/cancel callbacks for local transaction part requiring processing.</param>
        private void PopulateTransactParamsBase(OSDMap transact_params, GloebitTransaction txn, string description, string senderGloebitID, OSDMap descMap, Uri baseURI)
        {
            // TODO: consider passing in version.
            
            /***** Base Params always required *****/
            transact_params["version"] = 1;
            transact_params["application-key"] = m_key;
            transact_params["request-created"] = (int)(DateTime.UtcNow.Ticks / 10000000);  // TODO - figure out if this is in the right units
            transact_params["username-on-application"] = txn.PayerName;
            transact_params["transaction-id"] = txn.TransactionID.ToString();
            
            // TODO: make payerID required in all txns and move to base params section
            transact_params["buyer-id-on-application"] = txn.PayerID;
            transact_params["app-user-id"] = senderGloebitID;
            
            /***** Asset Params *****/
            // TODO: should only build this if asset, not product txn.  u2u txn probably has to be asset.
            transact_params["gloebit-balance-change"] = txn.Amount;
            // TODO: move description into GloebitTransaction and remove from arguments.
            transact_params["asset-code"] = description;
            transact_params["asset-quantity"] = 1;
            
            /***** Product Params *****/
            // GMM doesn't use this, so here for eventual complete client api.  u2u txn probably has to be asset.
            // If product is used instead of asset:
            //// product is required
            //// product-quantity is optional positive integer (assumed 1 if not supplied)
            //// character-id is optional character_id
            
            /***** Callback Params *****/
            // TODO: add a bool to transaction for whether to register callbacks.  For now, this always happens.
            transact_params["asset-enact-hold-url"] = txn.BuildEnactURI(baseURI);
            transact_params["asset-consume-hold-url"] = txn.BuildConsumeURI(baseURI);
            transact_params["asset-cancel-hold-url"] = txn.BuildCancelURI(baseURI);
            
            /***** DescMap Params *****/
            // TODO: make descmap optional or required in all txns and move to own section
            if (descMap != null) {
                transact_params["platform-desc-names"] = descMap["platform-names"];
                transact_params["platform-desc-values"] = descMap["platform-values"];
                transact_params["location-desc-names"] = descMap["location-names"];
                transact_params["location-desc-values"] = descMap["location-values"];
                transact_params["transaction-desc-names"] = descMap["transaction-names"];
                transact_params["transaction-desc-values"] = descMap["transaction-values"];
            }
            
            /***** Subscription Params *****/
            if (txn.IsSubscriptionDebit) {
                transact_params["automated-transaction"] = true;
                transact_params["subscription-id"] = txn.SubscriptionID;
            }
            
        }
        
        /// <summary>
        /// Builds the U2U and base form parameters in the format that the GloebitAPI TransactU2U endpoint expects for this transaction.
        /// </summary>
        /// <param name="transact_params">OSDMap which will be populated with form parameters.</param>
        /// <param name="txn">GloebitTransaction representing local transaction we are create transact_params from.</param>
        /// <param name="recipientGloebitID">UUID from the Gloebit system of user being paid.  May be empty.</param>
        /// <param name="recipientEmail">Email of the user being paid gloebits.  May be empty.</param>
        private void PopulateTransactParamsU2U(OSDMap transact_params, GloebitTransaction txn, string recipientGloebitID, string recipientEmail)
        {
            /***** U2U specific transact params *****/
            transact_params["seller-name-on-application"] = txn.PayeeName;
            transact_params["seller-id-on-application"] = txn.PayeeID;
            if (!String.IsNullOrEmpty(recipientGloebitID) && recipientGloebitID != UUID.Zero.ToString()) {
                transact_params["seller-id-from-gloebit"] = recipientGloebitID;
            }
            if (!String.IsNullOrEmpty(recipientEmail)) {
                transact_params["seller-email-address"] = recipientEmail;
            }
        }
        
        /// <summary>
        /// Given the response from a TransactU2U web request, retrieves and stores vital information in the transaction object and data store.
        /// </summary>
        /// <param name="txn">GloebitTransaction representing local transaction for which we received the response.</param>
        /// <param name="responseDataMap">OSDMap containing the web response body.</param>
        private void PopulateTransactResponse(GloebitTransaction txn, OSDMap responseDataMap)
        {
            // Get response data
            bool success = (bool)responseDataMap["success"];
            // NOTE: if success=false: id, balance, product-count are invalid.
            double balance = responseDataMap["balance"].AsReal();
            string reason = responseDataMap["reason"];
            // TODO: ensure status is always sent
            string status = "";
            if (responseDataMap.ContainsKey("status")) {
                status = responseDataMap["status"];
            }
            
            m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPI.Transact(-U2U) response recieved success: {0} balance: {1} status: {2} reason: {3}", success, balance, status, reason);
            
            // Store response data in GloebitTransaction record
            txn.ResponseReceived = true;
            txn.ResponseSuccess = success;
            txn.ResponseStatus = status;
            txn.ResponseReason = reason;
            if (success) {
                txn.PayerEndingBalance = (int)balance;
            }
            GloebitTransactionData.Instance.Store(txn);
        }
        
        /// <summary>
        /// Convert required TransactU2U response data of success, status and reason into simpler to manage stage and failure parameters.
        /// </summary>
        /// <param name="stage">TransactionStage out parameter will be set to stage completed or failed in.</param>
        /// <param name="failure">TransactionFailure out parameter will be set to specific failure or NONE.</param>
        /// <param name="success">Bool representing success or failure of the TransactU2U api call.</param>
        /// <param name="status">String status returned by TransactU2U api call.</param>
        /// <param name="reason">String reason returned by the Transact U2U call detailing failure or "success".</param>
        private void PopulateTransactStageAndFailure(out TransactionStage stage, out TransactionFailure failure, bool success, string status, string reason)
        {
            stage = TransactionStage.BEGIN;
            failure = TransactionFailure.NONE;
            
            // Build Stage & Failure arguments
            // TODO: eventually, these can be passed directly from server and just converted and passed through to interface func.
            // TODO: Do we want logs here or in GMM?
            if (success) {
                if (reason == "success") {                          /* successfully queued, early enacted all non-asset transaction parts */
                    switch (reason) {
                        case "success":
                            // TODO: could make a new stage here: EARLY_ENACT, or more accurately, ENACT_GLOEBIT is complete.
                        case "resubmitted":
                            // TODO: this is truly only queued.  see transaction processor.  Early-enact not tried.
                        default:
                            // unhandled response.
                            stage = TransactionStage.QUEUE;
                            failure = TransactionFailure.NONE;
                            break;
                    }
                }
            // TODO: Adding an "early-enact-failed" status to make this simpler
            } else if (status == "queued") {                                /* successfully queued.  an early enact failed */
                // This is a complex error/flow response which we should really consider if there is a better way to handle.
                // Is this always a permanent failure?  Could this succeed in queue if user purchased gloebits at same time?
                // Can anything other than insufficient funds cause this problem?  Internet Issue?
                stage = TransactionStage.ENACT_GLOEBIT;
                // TODO: perhaps the stage should be queue here, and early_enact error as this is not being enacted by a transaction processor.
                
                if (reason == "insufficient balance") {                     /* permanent failure - actionable by buyer */
                    failure = TransactionFailure.INSUFFICIENT_FUNDS;
                } else if (reason == "pending") {                           /* queue will retry enacts */
                    // may not be possible.  May only be "pending" if includes a charge part which these will not.
                    failure = TransactionFailure.ENACTING_GLOEBIT_FAILED;
                } else {                                                    /* perm failure - assumes tp will get same response form part.enact */
                    // Shouldn't ever get here.
                    failure = TransactionFailure.ENACTING_GLOEBIT_FAILED;
                }
            } else {                                                        /* failure prior to successful queuing.  Something requires fixing */
                if (reason == "unknown OAuth2 token") {                     /* Invalid Token.  May have been revoked by user or expired */
                    stage = TransactionStage.AUTHENTICATE;
                    failure = TransactionFailure.AUTHENTICATION_FAILED;
                } else if (status == "queuing-failed") {                    /* failed to queue.  net or processor error */
                    stage = TransactionStage.QUEUE;
                    failure = TransactionFailure.QUEUEING_FAILED;
                } else if (status == "failed") {                            /* race condition - already queued */
                    // nothing to tell user.  buyer doesn't need to know it was double submitted
                    stage = TransactionStage.QUEUE;
                    failure = TransactionFailure.RACE_CONDITION;
                } else if (status == "cannot-spend") {                      /* Buyer's Gloebit account is locked and not allowed to spend gloebits */
                    stage = TransactionStage.VALIDATE;
                    failure = TransactionFailure.PAYER_ACCOUNT_LOCKED;
                } else if (status == "cannot-receive") {                    /* Seller's Gloebit account can not receive gloebits */
                    // TODO: Check role in new system.  This is for role=Payee, not role=Application
                    stage = TransactionStage.VALIDATE;
                    failure = TransactionFailure.PAYEE_CANNOT_RECEIVE;
                } else if (status == "unknown-merchant") {                  /* can not identify merchant from params supplied by app */
                    stage = TransactionStage.VALIDATE;
                    failure = TransactionFailure.PAYEE_CANNOT_BE_IDENTIFIED;
                } else if (reason == "transaction is missing parameters needed to identify Gloebit account of seller - supply at least one of seller-email-address or seller-id-from-gloebit.") {
                    // TODO: handle this better in the long run
                    stage = TransactionStage.VALIDATE;
                    failure = TransactionFailure.PAYEE_CANNOT_BE_IDENTIFIED;
                } else if (reason == "Transaction with automated-transaction=True is missing subscription-id") {
                    stage = TransactionStage.VALIDATE;
                    failure = TransactionFailure.FORM_MISSING_SUBSCRIPTION_ID;
                } else if (status == "unknown-subscription") {
                    stage = TransactionStage.VALIDATE;
                    failure = TransactionFailure.SUBSCRIPTION_NOT_FOUND;
                } else if (status == "unknown-subscription-authorization") {
                    stage = TransactionStage.VALIDATE;
                    failure = TransactionFailure.SUBSCRIPTION_AUTH_NOT_FOUND;
                } else if (status == "subscription-authorization-pending") {
                    stage = TransactionStage.VALIDATE;
                    failure = TransactionFailure.SUBSCRIPTION_AUTH_PENDING;
                } else if (status == "subscription-authorization-declined") {
                    stage = TransactionStage.VALIDATE;
                    failure = TransactionFailure.SUBSCRIPTION_AUTH_DECLINED;
                } else {                                                    /* App issue --- Something needs fixing by app */
                    stage = TransactionStage.VALIDATE;
                    failure = TransactionFailure.FORM_GENERIC_ERROR;
                }
            }
            
            // TODO: consider making stage & failure part of the GloebitTransactions table and storing them here.
            // still pass explicitly to make sure they can't be modified before callback uses them.
        }
        
        /// <summary>
        /// Handle any functional adjustments required after a TransactU2U failure.  Currently soley invalidates the token
        /// if failure was due to a bad OAuth2 token.
        /// </summary>
        /// <param name="txn">GloebitTransaction that failed.</param>
        /// <param name="failure">TransactionFailure detailing specific failure or NONE.</param>
        /// <param name="sender">GloebitUser object for payer containing necessary details and OAuth2 token.</param>
        private void ProcessTransactFailure(GloebitTransaction txn, TransactionFailure failure, GloebitUser sender)
        {
            switch (failure) {
                case TransactionFailure.NONE:
                    // default - no error - proceed
                    break;
                case TransactionFailure.AUTHENTICATION_FAILED:
                    // The token is invalid (probably the user revoked our app through the website)
                    // so force a reauthorization next time.
                    sender.InvalidateToken();
                    break;
                case TransactionFailure.SUBSCRIPTION_AUTH_NOT_FOUND:
                case TransactionFailure.SUBSCRIPTION_AUTH_PENDING:
                case TransactionFailure.SUBSCRIPTION_AUTH_DECLINED:
                    // TODO: why are we explicitly logging this here?  Should this be moved to GMM?
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.Transact-U2U Subscription-Auth issue: '{0}'", txn.ResponseReason);
                    break;
                default:
                    // TODO: why are we logging this here?  Should this be moved to GMM?
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.Transact(-U2U) Unknown error posting transaction, reason: '{0}'", txn.ResponseReason);
                    break;
            }
        }
        
        
        /// <summary>
        /// Request a new subscription be created by Gloebit for this app.
        /// Subscriptions are required for any recurring, unattended/automated payments that a user will sign up for.
        /// Upon completion of this request, the interface function CreateSubscriptionCompleted will be called with the results.
        /// If successful, an ID will be created and returned by Gloebit which should be used for requesting user authorization and
        /// creating transactions under this subscription code.
        /// </summary>
        /// <param name="subscription">Local GloebitSubscription with the details for this subscription.</param>
        /// <param name="baseURI">Callback URI -- not currently used.  Included in case we add callback ability.</param>
        /// <returns>
        /// True if the request was successfully submitted to Gloebit;
        /// False if submission fails.
        /// See CreateSubscriptionCompleted for async callback with relevant results of this api call.
        /// </returns>
        public bool CreateSubscription(GloebitSubscription subscription, Uri baseURI) {
            
            //TODO stop logging auth_code
            m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscription GloebitSubscription:{0}", subscription);
            
            // ************ BUILD EXCHANGE ACCESS TOKEN POST REQUEST ******** //
            OSDMap sub_params = new OSDMap();

            sub_params["client_id"] = m_key;
            sub_params["client_secret"] = m_secret;
            
            sub_params["application-key"] = m_key;  // TODO: consider getting rid of this.
            if (m_key != subscription.AppKey) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscription GloebitAPI.m_key:{0} differs from GloebitSubscription.AppKey:{1}", m_key, subscription.AppKey);
                return false;
            }
            sub_params["local-id"] = subscription.ObjectID;
            sub_params["name"] = subscription.ObjectName;
            sub_params["description"] = subscription.Description;
            // TODO: should we add additional-details to sub_params?
            
            HttpWebRequest request = BuildGloebitRequest("create-subscription", "POST", null, "application/json", sub_params);
            if (request == null) {
                // ERROR
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscription failed to create HttpWebRequest");
                // TODO: signal error
                return false;
            }
            
            m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscription about to BeginGetResponse");
            // **** Asynchronously make web request **** //
            IAsyncResult r = request.BeginGetResponse(GloebitWebResponseCallback,
			                                          new GloebitRequestState(request, 
			                        delegate(OSDMap responseDataMap) {
                                        
                m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscription response: {0}", responseDataMap);

                //************ PARSE AND HANDLE CREATE SUBSCRIPTION RESPONSE *********//

                // Grab fields always included in response
                bool success = (bool)responseDataMap["success"];
                string reason = responseDataMap["reason"];
                string status = responseDataMap["status"];
                m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscription success: {0} reason: {1} status: {2}", success, reason, status);
                
                if (success) {
                    string subscriptionIDStr = responseDataMap["id"];
                    bool enabled = (bool) responseDataMap["enabled"];
                    subscription.SubscriptionID = UUID.Parse(subscriptionIDStr);
                    subscription.Enabled = enabled;
                    GloebitSubscriptionData.Instance.UpdateFromGloebit(subscription);
                    if (status == "duplicate") {
                        m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscription duplicate request to create subscription");
                    }
                } else {
                    switch(reason) {
                        case "Unexpected DB insert integrity error.  Please try again.":
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscription failed from {0}", reason);
                            break;
                        case "different subscription exists with this app-subscription-id":
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscription failed due to different subscription with same object id -- subID:{0} name:{1} desc:{2} ad:{3} enabled:{4} ctime:{5}",
                                              responseDataMap["existing-subscription-id"], responseDataMap["existing-subscription-name"], responseDataMap["existing-subscription-description"], responseDataMap["existing-subscription-additional_details"], responseDataMap["existing-subscription-enabled"], responseDataMap["existing-subscription-ctime"]);
                            break;
                        case "Unknown DB Error":
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscription failed from {0}", reason);
                            break;
                        default:
                            m_log.ErrorFormat("Unknown error posting create subscription, reason: '{0}'", reason);
                            break;
                    }
                }

                // TODO - decide if we really want to issue this callback even if the token was invalid
                m_asyncEndpointCallbacks.createSubscriptionCompleted(responseDataMap, subscription);
            }));
            
            return true;
        }
    
        
        /// <summary>
        /// Request creation of a pending authorization for an existing subscription for this application.
        /// A subscription authorization must be created and then approved by the user before a recurring, unattended/automated payment
        /// can be requested for this user by this app.  The authorization is for a single, specific subscription.
        /// The authorization is not only specific to the Gloebit account linked to this user, but also to the app account by the id of the use on this app.
        /// A subscription for this app must already have been created via CreateSubscription.
        /// Upon completion of this request, the interface function CreateSubscriptionAuthorizationCompleted will be called with the results.
        /// The application does not need to store SubscriptionAuthorizations locally.  A transaction can be submitted without knowledge of an
        /// existing approved authorization.  If an approval exists, the transaction will process.  If not, the transaction will fail with relevant
        /// information provided to the transaction completed async callback function.
        /// </summary>
        /// <param name="sub">Local GloebitSubscription with the details for this subscription.</param>
        /// <param name="sender"> GloebitUser of the user for whom we're creating a pending subscription authorization request.</param>
        /// <param name="senderName"> String of the user name on the app.  This is supplied to display back to the user which app account they are authorizing.</param>
        /// <param name="baseURI">Callback URI -- not currently used.  Included in case we add callback ability.</param>
        /// <returns>
        /// True if the request was successfully submitted to Gloebit;
        /// False if submission fails.
        /// See CreateSubscriptionAuthorizationCompleted for async callback with relevant results of this api call.
        /// </returns>
        public bool CreateSubscriptionAuthorization(GloebitSubscription sub, GloebitUser sender, string senderName, Uri baseURI) {

            m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscriptionAuthorization subscriptionID:{0} senderID:{1} senderName:{2} baseURI:{3}", sub.SubscriptionID, sender.PrincipalID, senderName, baseURI);
            
            
            // ************ BUILD AND SEND CREATE SUBSCRIPTION AUTHORIZATION POST REQUEST ******** //
            
            OSDMap sub_auth_params = new OSDMap();
            
            sub_auth_params["application-key"] = m_key;
            sub_auth_params["request-created"] = (int)(DateTime.UtcNow.Ticks / 10000000);  // TODO - figure out if this is in the right units
            //sub_auth_params["username-on-application"] = String.Format("{0} - {1}", senderName, sender.PrincipalID);
            sub_auth_params["username-on-application"] = senderName;
            sub_auth_params["user-id-on-application"] = sender.PrincipalID;
            if (!String.IsNullOrEmpty(sender.GloebitID) && sender.GloebitID != UUID.Zero.ToString()) {
                sub_auth_params["app-user-id"] = sender.GloebitID;
            }
            // TODO: should we add additional-details to sub_auth_params?
            sub_auth_params["subscription-id"] = sub.SubscriptionID;
            
            HttpWebRequest request = BuildGloebitRequest("create-subscription-authorization", "POST", sender, "application/json", sub_auth_params);
            if (request == null) {
                // ERROR
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscriptionAuthorization failed to create HttpWebRequest");
                return false;
                // TODO once we return, return error value
            }

            m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscriptionAuthorization about to BeginGetResponse");
            // **** Asynchronously make web request **** //
            IAsyncResult r = request.BeginGetResponse(GloebitWebResponseCallback,
			                                          new GloebitRequestState(request, 
			                        delegate(OSDMap responseDataMap) {
                                        
                m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscriptionAuthorization response: {0}", responseDataMap);

                //************ PARSE AND HANDLE CREATE SUBSCRIPTION AUTHORIZATION RESPONSE *********//

                // Grab fields always included in response
                bool success = (bool)responseDataMap["success"];
                string reason = responseDataMap["reason"];
                string status = responseDataMap["status"];
                m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscriptionAuthorization success: {0} reason: {1} status: {2}", success, reason, status);
                
                if (success) {
                    string subscriptionAuthIDStr = responseDataMap["id"];
                    // TODO: if we decide to store auths, this would be a place to do so.
                    if (status == "duplicate") {
                        m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscriptionAuthorization duplicate request to create subscription");
                    } else if (status == "duplicate-and-already-approved-by-user") {
                        m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscriptionAuthorization duplicate request to create subscription - subscription has already been approved by user.");
                    } else if (status == "duplicate-and-previously-declined-by-user") {
                        m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscriptionAuthorization SUCCESS & FAILURE - user previously declined authorization -- consider if app should re-request or if that is harassing user or has Gloebit API reset this automatically?. status:{0} reason:{1}", status, reason);
                    }
                    
                    string sPending = responseDataMap["pending"];
                    string sEnabled = responseDataMap["enabled"];
                    m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscriptionAuthorization SUCCESS pending:{0}, enabled:{1}.", sPending, sEnabled);
                } else {
                    switch(status) {
                        case "cannot-transact":
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscriptionAuthorization FAILED - no transact permissions on this user. status:{0} reason:{1}", status, reason);
                            break;
                        case "subscription-not-found":
                        case "mismatched-application-key":
                        case "mis-matched-subscription-ids":
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscriptionAuthorization FAILED - could not properly identify subscription - status:{0} reason:{1}", status, reason);
                            break;
                        case "subscription-disabled":
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscriptionAuthorization FAILED - app has disabled this subscription. status:{0} reason:{1}", status, reason);
                            break;
                        case "duplicate-and-previously-declined-by-user":
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscriptionAuthorization FAILED - user previously declined authorization -- consider if app should re-request or if that is harassing user. status:{0} reason:{1}", status, reason);
                            break;
                        default:
                            switch(reason) {
                                case "Unexpected DB insert integrity error.  Please try again.":
                                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscriptionAuthorization FAILED from {0}", reason);
                                    break;
                                case "Unknown DB Error":
                                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.CreateSubscriptionAuthorization failed from {0}", reason);
                                    break;
                                default:
                                    m_log.ErrorFormat("Unknown error posting create subscription authorization, reason: '{0}'", reason);
                                    break;
                            }
                            break;
                    }
                }

                // TODO - decide if we really want to issue this callback even if the token was invalid
                m_asyncEndpointCallbacks.createSubscriptionAuthorizationCompleted(responseDataMap, sub, sender);
            }));
            
            return true;
        }

        /// <summary>
        /// Request a subscription authorization from a user.
        /// This specifically sends a message with a clickable URL to the client.
        /// </summary>
        /// <param name="subAuthID">ID of the authorization request the user will be asked to approve - provided by Gloebit.</param>
        public Uri BuildSubscriptionAuthorizationURI(string subAuthID)
        {
            // Build and return the URI
            return(new Uri(m_url, String.Format("authorize-subscription/{0}/", subAuthID)));
        }
        
 
        /***********************************************/
        /********* GLOEBIT API HELPER FUNCTIONS ********/
        /***********************************************/
    
        // TODO: OSDMap or Dictionary for params
    
        /// <summary>
        /// Build an HTTPWebRequest for a Gloebit endpoint.
        /// </summary>
        /// <param name="relative_url">endpoint & query args.</param>
        /// <param name="method">HTTP method for request -- eg: "GET", "POST".</param>
        /// <param name="user">GloebitUser object for this authenticated user if one exists.</param>
        /// <param name="content_type">content type of post/put request  -- eg: "application/json", "application/x-www-form-urlencoded".</param>
        /// <param name="paramMap">parameter map for body of request.</param>
        private HttpWebRequest BuildGloebitRequest(string relativeURL, string method, GloebitUser user, string contentType = "", OSDMap paramMap = null) {
            
            // combine Gloebit base url with endpoint and query args in relative url.
            Uri requestURI = new Uri(m_url, relativeURL);
        
            // Create http web request from URL
            HttpWebRequest request = (HttpWebRequest) WebRequest.Create(requestURI);
        
            // Add authorization header
            if (user != null && user.GloebitToken != "") {
                request.Headers.Add("Authorization", String.Format("Bearer {0}", user.GloebitToken));
            }
        
            // Set request method and body
            request.Method = method;
            switch (method) {
                case "GET":
                    m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.BuildGloebitRequest GET baseURL:{0} relativeURL:{1}, fullURL:{2}", m_url, relativeURL, requestURI);
                    break;
                case "POST":
                case "PUT":
                    string paramString = "";
                    byte[] postData = null;
                    request.ContentType = contentType;
                
                    // Build paramString in proper format
                    if (paramMap != null) {
                        if (contentType == "application/x-www-form-urlencoded") {
                            paramString = BuildURLEncodedParamString(paramMap);
                        } else if (contentType == "application/json") {
                            paramString = OSDParser.SerializeJsonString(paramMap);
                        } else {
                            // ERROR - we are not handling this content type properly
                            m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.BuildGloebitRequest relativeURL:{0}, unrecognized content type:{1}", relativeURL, contentType);
                            return null;
                        }
                
                        // Byte encode paramString and write to requestStream
                        postData = System.Text.Encoding.UTF8.GetBytes(paramString);
                        request.ContentLength = postData.Length;
                        // TODO: look into BeginGetRequestStream()
                        using (Stream s = request.GetRequestStream()) {
                            s.Write(postData, 0, postData.Length);
                        }
                    } else {
                        // Probably should be a GET request if it has no paramMap
                        m_log.WarnFormat("[GLOEBITMONEYMODULE] GloebitAPI.BuildGloebitRequest relativeURL:{0}, Empty paramMap on {1} request", relativeURL, method);
                    }
                    break;
                default:
                    // ERROR - we are not handling this request type properly
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.BuildGloebitRequest relativeURL:{0}, unrecognized web request method:{1}", relativeURL, method);
                    return null;
            }
            return request;
        }
        
        /// <summary>
        /// Build an application/x-www-form-urlencoded string from the paramMap.
        /// </summary>
        /// <param name="ParamMap">Parameters to be encoded.</param>
        private string BuildURLEncodedParamString(OSDMap paramMap) {
            // TODO: remove client_secret from this before logging
            m_log.DebugFormat("[GLOEBITMONEYMODULE] GloebitAPI.BuildURLEncodedParamString building from paramMap:{0}:", paramMap);
            StringBuilder paramBuilder = new StringBuilder();
            foreach (KeyValuePair<string, OSD> p in (OSDMap)paramMap) {
                if(paramBuilder.Length != 0) {
                    paramBuilder.Append('&');
                }
                paramBuilder.AppendFormat("{0}={1}", HttpUtility.UrlEncode(p.Key), HttpUtility.UrlEncode(p.Value.ToString()));
            }
            return( paramBuilder.ToString() );
        }
        
        /***********************************************/
        /** GLOEBIT ASYNCHRONOUS API HELPER FUNCTIONS **/
        /***********************************************/
        
        /// <summary>
        /// Handles asynchronous return from web request BeginGetResponse.
        /// Retrieves response stream and asynchronously begins reading response stream.
        /// </summary>
        /// <param name="ar">State details compiled as this web request is processed.</param>
        public void GloebitWebResponseCallback(IAsyncResult ar) {
            
            m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPI.GloebitWebResponseCallback");
            
            // Get the RequestState object from the async result.
            GloebitRequestState myRequestState = (GloebitRequestState) ar.AsyncState;
            HttpWebRequest req = myRequestState.request;
            
            // Call EndGetResponse, which produces the WebResponse object
            //  that came from the request issued above.
            try
            {
                HttpWebResponse resp = (HttpWebResponse)req.EndGetResponse(ar);

                //  Start reading data from the response stream.
                // TODO: look into BeginGetResponseStream();
                Stream responseStream = resp.GetResponseStream();
                myRequestState.responseStream = responseStream;

                // TODO: Do I need to check the CanRead property before reading?

                //  Begin reading response into myRequestState.BufferRead
                // TODO: May want to make use of iarRead for calls by syncronous functions
                IAsyncResult iarRead = responseStream.BeginRead(myRequestState.bufferRead, 0, GloebitRequestState.BUFFER_SIZE, GloebitReadCallBack, myRequestState);

                // TODO: on any failure/exception, propagate error up and provide to user in friendly error message.
            }
            catch (ArgumentNullException e) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.GloebitWebResponseCallback ArgumentNullException e:{0}", e.Message);
            }
            catch (WebException e) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.GloebitWebResponseCallback WebException e:{0} URI:{1}", e.Message, req.RequestUri);
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] response:{0}", e.Response);
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] e:{0}", e.ToString ());
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] source:{0}", e.Source);
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] stack_trace:{0}", e.StackTrace);
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] status:{0}", e.Status);
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] target_site:{0}", e.TargetSite);
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] data_count:{0}", e.Data.Count);
            }
            catch (InvalidOperationException e) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.GloebitWebResponseCallback InvalidOperationException e:{0}", e.Message);
            }
            catch (ArgumentException e) {
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.GloebitWebResponseCallback ArgumentException e:{0}", e.Message);
            }

        }
        
        /// <summary>
        /// Handles asynchronous return from web request response stream BeginRead().
        /// Retrieves and stores buffered read, or closes stream and passes requestState to requestState.continuation().
        /// </summary>
        /// <param name="ar">State details compiled as this web request is processed.</param>
        private void GloebitReadCallBack(IAsyncResult ar)
        {
            // m_log.InfoFormat("[GLOEBITMONEYMODULE] GloebitAPI.GloebitReadCallback");
            
            // Get the RequestState object from AsyncResult.
            GloebitRequestState myRequestState = (GloebitRequestState)ar.AsyncState;
            Stream responseStream = myRequestState.responseStream;
            
            // Handle data read.
            int bytesRead = responseStream.EndRead( ar );
            if (bytesRead > 0)
            {
                // Decode and store the bytesRead in responseData
                Char[] charBuffer = new Char[GloebitRequestState.BUFFER_SIZE];
                int len = myRequestState.streamDecoder.GetChars(myRequestState.bufferRead, 0, bytesRead, charBuffer, 0);
                String str = new String(charBuffer, 0, len);
                myRequestState.responseData.Append(str);
                
                // Continue reading data until
                // responseStream.EndRead returns 0 for end of stream.
                // TODO: should we be doing anything with result???
                IAsyncResult result = responseStream.BeginRead(myRequestState.bufferRead, 0, GloebitRequestState.BUFFER_SIZE, GloebitReadCallBack, myRequestState);
            }
            else
            {
                // Done Reading
                
                // Close down the response stream.
                responseStream.Close();
                
                if (myRequestState.responseData.Length <= 0) {
                    // TODO: Is this necessarily an error if we don't have data???
                    m_log.ErrorFormat("[GLOEBITMONEYMODULE] GloebitAPI.GloebitReadCallback error: No Data");
                    // TODO: signal error
                }
                
                if (myRequestState.continuation != null) {
                    OSDMap responseDataMap = (OSDMap)OSDParser.DeserializeJson(myRequestState.responseData.ToString());
                    myRequestState.continuation(responseDataMap);
                }
            }
        }
        
        public enum TransactionStage : int
        {
            BEGIN           = 0,    // Not really a stage.  may not need this
            BUILD           = 100,   // Preparing the transaction locally for submission
            SUBMIT          = 200,   // Submitting the transaction to Gloebit via the API Endpoints.
            AUTHENTICATE    = 300,   // Checking OAuth Token included in header
            VALIDATE        = 400,   // Validating the txn form submitted to Gloebit -- may need to add in some subscription specific validations
            QUEUE           = 500,   // Queueing the transaction for processing
            ENACT_GLOEBIT   = 600,   // performing Gloebit components of transaction
            ENACT_ASSET     = 650,   // performing local components of transaction
            CONSUME_GLOEBIT = 700,   // committing Gloebit components of transaction
            CONSUME_ASSET   = 750,   // committing local components of transaction
            CANCEL_GLOEBIT  = 800,   // canceling Gloebit components of transaction
            CANCEL_ASSET    = 850,   // canceling local components of transaction
            COMPLETE        = 1000,  // Not really a stage.  May not need this.  Once local asset is consumed, we are complete.
        }
        
        public enum TransactionFailure : int
        {
            NONE                            = 0,
            SUBMISSION_FAILED               = 200,
            BUILD_WEB_REQUEST_FAILED        = 201,
            AUTHENTICATION_FAILED           = 300,
            VALIDATION_FAILED               = 400,
            FORM_GENERIC_ERROR              = 401,
            FORM_MISSING_SUBSCRIPTION_ID    = 411,
            PAYER_ACCOUNT_LOCKED            = 441,
            PAYEE_CANNOT_BE_IDENTIFIED      = 451,
            PAYEE_CANNOT_RECEIVE            = 452,
            SUBSCRIPTION_NOT_FOUND          = 461,
            SUBSCRIPTION_AUTH_NOT_FOUND     = 471,
            SUBSCRIPTION_AUTH_PENDING       = 472,
            SUBSCRIPTION_AUTH_DECLINED      = 473,
            QUEUEING_FAILED                 = 500,
            RACE_CONDITION                  = 501,
            ENACTING_GLOEBIT_FAILED         = 600,
            INSUFFICIENT_FUNDS              = 601,
            ENACTING_ASSET_FAILED           = 650
        }
    }
}
