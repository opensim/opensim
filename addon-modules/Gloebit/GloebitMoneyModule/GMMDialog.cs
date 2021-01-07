/*
 * GMMDialog.cs is part of OpenSim-MoneyModule-Gloebit
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
 * GMMDialog.cs
 *
 * Helper class for sending dialog messages to OpenSim users.
 * Can provide a question and receive a response.
 * Used for the Subscription Authorization System, which is used
 * by OpenSim's auto-debit scripted objects.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;


namespace Gloebit.GloebitMoneyModule {

    /*********************************************************
     ********** DIALOG helper class **************************
     *********************************************************/

    /// <summary>
    /// Class for sending questions to a user and receiving and processing the clicked resopnse.
    /// Create a derived class (see CreateSubscriptionAuthorizationDialog) to send a new type of message.
    /// All derived classes must implement all abstract methods and properties, as well as a
    /// constructor which calls the base Dialog constructor.
    /// To send a dialog to the user:
    /// --- 1. Call the constructor for the derived Dialog type with new.
    /// --- 2. Call the static method Dialog.Send() with the derived Dialog you just created.
    /// --- 3. Handle the user's response via the derived classes implementation of ProcessResponse()
    /// </summary>
    public abstract class Dialog
    {
        protected static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Master map of Dialogs - Map of AgentIDs to map of channels to Dialog message info
        private static Dictionary<UUID, Dictionary<int, Dialog>> s_clientDialogMap = new Dictionary<UUID, Dictionary<int, Dialog>>();

        // Time of last purge used to purge old Dialogs for which the user didn't respond.  See PurgeOldDialogs()
        private static DateTime s_lastPurgedOldDialogs = DateTime.UtcNow;
        private static readonly Object s_purgeLock = new Object();       // Lock to enforce only a single purge per period

        // Counter used to create unique channels for each dialog message
        private const int c_MinChannel = -1700000000;           // channel limited to -2,147,483,648 -- reset when we get close
        private const int c_MaxChannel = -1600000001;              // Use negative channels only as they are harder for standard viewers to mimic.
        protected static int s_lastChannel = c_MaxChannel + 1;  // Use negative channels only as they are harder for standard viewers to mimic.

        // variables not used by dialog because we are sending the message, not an object from inworld
        protected static readonly UUID c_msgSourceObjectID = UUID.Zero;         // Message from us, not from inworld object, so Zero
        protected static readonly UUID c_msgSourceObjectOwnerID = UUID.Zero;    // Message from us, not from inworld object, so Zero
        protected static readonly UUID c_backgroundTextureID = UUID.Zero;       // Background; was never implemented on client, so Zero

        // variables consistent across all dialog messages from GloebitMoneyModule
        protected const string c_msgHeaderWordOne = "GLOEBIT";        // Word 1 of msg header displayed in Dialog Message - designed to be possessive name
        protected const string c_msgHeaderWordTwo = "MoneyModule";     // Word 2 of msg header displayed in Dialog Message - designed to be possessive name
        // Header: "{0} {1}'s".format(c_msgHeaderWordOne, c_msgHeaderWordTwo)

        // variables common to all Dialog messages
        // TODO: test cTime to make sure they are different.
        protected readonly DateTime cTime = DateTime.UtcNow;     // Time created - used for purging old Dialogs
        protected readonly int Channel = PickChannel();          // Channel response will be received on for this Dialog
        protected readonly IClientAPI Client;                    // Client to whom we're sending the Dialog
        protected readonly UUID AgentID;                         // AgentID of client to whom we're sending the Dialog

        // Properties that derived classes must implement - all are displayed in Dialog message
        protected abstract string MsgTitle { get; }             // Message Title -- submitted as the source ObjectName
        protected abstract string MsgBody { get; }              // Message Body -- submitted as the message from the Object.
        protected abstract string[] ButtonResponses { get; }    // Button Responses

        // Methods that derived classes must implement
        protected abstract void ProcessResponse(IClientAPI client, OSChatMessage chat);

        /// <summary>
        /// base Dialog constructor.
        /// Must be called by all derived class constructors
        /// Sets some universally required parameters which are specific to the Dialog instance.
        /// </summary>
        /// <param name="client">IClientAPI of agent to whom we are sending the Dialog</param>
        /// <param name="agentID">UUID of agent to whom we are sending the Dialog</param>
        protected Dialog(IClientAPI client, UUID agentID)
        {
            this.AgentID = agentID;
            this.Client = client;
        }

        /// <summary>
        /// Creates a channel for this Dialog.
        /// The channel is the chat channel that this dialog sends it's response through.
        /// Chat channels are limited to -2,147,483,648 to 2,147,483,647
        /// channel should always be negative (harder to mimic from standard viewers).
        /// channel should always be unique for other active dialogs for the same user otherwise the
        /// previous dialog will disappear.
        /// channel should also always be unique from other active dialogs because it is used
        /// as the unique identifier to alocate a response to a specific Dialog.
        /// Channel could be made random in the future
        /// </summary>
        private static int PickChannel()
        {
            int local_lc, myChannel;
            do {
                local_lc = s_lastChannel;
                myChannel = local_lc - 1;

                // channel limited to -2,147,483,648 -- reset when we get close
                if (myChannel < c_MinChannel) {
                    myChannel = c_MaxChannel;
                }
            } while (local_lc != Interlocked.CompareExchange(ref s_lastChannel, myChannel, local_lc));
            // while ensures that one and only one thread finishes and modifies s_lastChannel
            // If s_lastChannel has changed since local_lc was set, this fails and the loop runs again.
            // If multiple threads are executing at the same time, at least one will always succeed.

            return myChannel;
        }

        /// <summary>
        /// Send instance of derived dialog to user.
        /// This is the public interface and only way a Dialog message should be sent.
        /// Create an instance of derived Dialog class using new to pass to this method.
        /// </summary>
        /// <param name="dialog">Instance of derived Dialog to track and send to user.</param>
        public static void Send(Dialog dialog)
        {
            dialog.Open();
            dialog.Deliver();
        }

        /// <summary>
        /// Adds dialog to our master map.
        /// If there are no other dialogs for this user, creates a dictionary of dialogs and registers a chat listener
        /// for this user.
        /// Always called before delivering the dialog to the user in order to prepare to handle the response.
        /// See Close() for cleanup
        /// </summary>
        private void Open()
        {
            lock (s_clientDialogMap) {
                /***** Create Dialog Dict for agent and register chat listener if no open dialogs exist for this agent *****/
                Dictionary<int, Dialog> channelDialogMap;
                if (!s_clientDialogMap.TryGetValue(AgentID, out channelDialogMap )) {
                    s_clientDialogMap[AgentID] = channelDialogMap = new Dictionary<int, Dialog>();
                    Client.OnChatFromClient += OnChatFromClientAPI;
                }

                /***** Add Dialog to master map *****/
                channelDialogMap[Channel] = this;
            }
        }

        /// <summary>
        /// Delivers the dialog message to the client.
        /// </summary>
        private void Deliver()
        {
            /***** Send Dialog message to agent *****/
            Client.SendDialog(objectname: MsgTitle,
                objectID: c_msgSourceObjectID, ownerID: c_msgSourceObjectOwnerID,
                ownerFirstName: c_msgHeaderWordOne, ownerLastName: c_msgHeaderWordTwo,
                msg: MsgBody, textureID: c_backgroundTextureID,
                ch: Channel, buttonlabels: ButtonResponses);
        }


        /// <summary>
        /// Catch chat from client and see if it is a response to a dialog message we've delivered.
        /// --- If not, consider purging old Dialogs.
        /// --- If it is on a channel for a Dialog for this user, validate that it's not an imposter.
        /// --- Call ProcessResponse on derived Dialog class
        /// Callback registered in Dialog.Open()
        /// EVENT:
        ///     ChatFromClientEvent is triggered via ChatModule (or
        ///     substitutes thereof) when a chat message
        ///     from the client  comes in.
        /// </summary>
        /// <param name="sender">Sender of message</param>
        /// <param name="chat">message sent</param>
        protected static void OnChatFromClientAPI(Object sender, OSChatMessage chat)
        {
            // m_log.InfoFormat("[GLOEBITMONEYMODULE] OnChatFromClientAPI from:{0} chat:{1}", sender, chat);
            // m_log.InfoFormat("[GLOEBITMONEYMODULE] OnChatFromClientAPI \n\tmessage:{0} \n\ttype: {1} \n\tchannel: {2} \n\tposition: {3} \n\tfrom: {4} \n\tto: {5} \n\tsender: {6} \n\tsenderObject: {7} \n\tsenderUUID: {8} \n\ttargetUUID: {9} \n\tscene: {10}", chat.Message, chat.Type, chat.Channel, chat.Position, chat.From, chat.To, chat.Sender, chat.SenderObject, chat.SenderUUID, chat.TargetUUID, chat.Scene);

            IClientAPI client = (IClientAPI) sender;

            /***** Verify that this is a message intended for us.  Otherwise, ignore or check to see if time to purge old dialogs *****/

            // Since we have to lock the map to look for a dialog with this channel, let's only proceed if the channel is within our range,
            // or we've reached our purge duration.
            if (chat.Channel < c_MinChannel || chat.Channel > c_MaxChannel) {
                // Every so often, cleanup old dialog messages not yet deregistered.
                if (s_lastPurgedOldDialogs.CompareTo(DateTime.UtcNow.AddHours(-6)) < 0) {
                    Dialog.PurgeOldDialogs();
                }
                // message is not for us, so exit
                return;
            }

            Dictionary<int, Dialog> channelDialogDict;
            Dialog dialog = null;
            bool found = false;
            lock (s_clientDialogMap) {
                if ( s_clientDialogMap.TryGetValue(client.AgentId, out channelDialogDict) ) {
                    found = channelDialogDict.TryGetValue(chat.Channel, out dialog);
                }
            }
            if (!found) {
                // message is not for us
                return;
            }

            /***** Validate base Dialog response parameters *****/

            // Check defaults that should always be the same to ensure no one tried to impersonate our dialog response
            // if (chat.SenderUUID != UUID.Zero || chat.TargetUUID != UUID.Zero || !String.IsNullOrEmpty(chat.From) || !String.IsNullOrEmpty(chat.To) || chat.Type != ChatTypeEnum.Region) {
            if (chat.SenderUUID != UUID.Zero || !String.IsNullOrEmpty(chat.From) || chat.Type != ChatTypeEnum.Region) {
                // m_log.WarnFormat("[GLOEBITMONEYMODULE] OnChatFromClientAPI Received message on Gloebit dialog channel:{0} which may be an attempted impersonation. SenderUUID:{1}, TargetUUID:{2}, From:{3} To:{4} Type: {5} Message:{6}", chat.Channel, chat.SenderUUID, chat.TargetUUID, chat.From, chat.To, chat.Type, chat.Message);
                m_log.WarnFormat("[GLOEBITMONEYMODULE] OnChatFromClientAPI Received message on Gloebit dialog channel:{0} which may be an attempted impersonation. SenderUUID:{1}, From:{2}, Type: {3}, Message:{4}", chat.Channel, chat.SenderUUID, chat.From, chat.Type, chat.Message);
                return;
            }

            // TODO: Should we check that chat.Sender/sender is IClientAPI as expected?
            // TODO: Should we check that Chat.Scene is scene we sent this to?

            /***** Process the response *****/

            m_log.InfoFormat("[GLOEBITMONEYMODULE] Dialog.OnChatFromClientAPI Processing Response: {0}", chat.Message);
            dialog.ProcessResponse(client, chat);

            /***** Handle Post Processing Cleanup of Dialog *****/

            dialog.Close();

        }

        /// <summary>
        /// Post processing cleanup of a Dialog.  Cleans everything that Open() set up.
        /// Removes dialog to our master map.
        /// If there are no other dialogs for this user, removes the dictionary of dialogs and deregisters the chat listener
        /// for this user.
        /// Always called after processing the response to a Dialog in order to clean up.
        /// Also called when a Dialog is purged without a response due to age.
        /// See Open() for setup that this cleans up.
        /// </summary>
        private void Close()
        {
            m_log.InfoFormat("[GLOEBITMONEYMODULE] Dialog.Close AgentID:{0} Channel:{1}.", this.AgentID, this.Channel);

            bool foundChannelDialogMap = false;
            bool foundChannel = false;
            bool lastActiveDialog = false;

            /***** Remove Dialog from master map --- also deregister chat listener if no more active dialogs for this agent *****/

            lock (s_clientDialogMap) {
                Dictionary<int, Dialog> channelDialogMap;
                if (s_clientDialogMap.TryGetValue(this.AgentID, out channelDialogMap)) {
                    foundChannelDialogMap = true;
                    if (channelDialogMap.ContainsKey(this.Channel)) {
                        foundChannel = true;

                        if (channelDialogMap.Count == 1) {
                            // Delete channelDialogMap and Deregister chat listener as we're closing the only open dialog for this agent
                            lastActiveDialog = true;
                            this.Client.OnChatFromClient -= OnChatFromClientAPI;
                            s_clientDialogMap.Remove(this.AgentID);
                        } else {
                            // Remove this dialog from the map for this agent
                            channelDialogMap.Remove(this.Channel);
                        }
                    }
                }
            }

            /***** Handle error/info messaging here so it is outside of the lock *****/
            if (!foundChannelDialogMap) {
                m_log.WarnFormat("[GLOEBITMONEYMODULE] Dialog.Close Called on dialog where agent is not in map -  AgentID:{0}.", this.AgentID);
            } else if (!foundChannel){
                m_log.WarnFormat("[GLOEBITMONEYMODULE] Dialog.Close Called on dialog where channel is not in map for agent -  AgentID:{0} Channel:{1}.", this.AgentID, this.Channel);
            } else {
                m_log.InfoFormat("[GLOEBITMONEYMODULE] Dialog.Close Removed dialog - AgentID:{0} Channel:{1}.", this.AgentID, this.Channel);
                if (lastActiveDialog) {
                    m_log.InfoFormat("[GLOEBITMONEYMODULE] Dialog.Close Removed agent dialog event listener - AgentID:{0}", this.AgentID);
                }
            }
        }

        /// <summary>
        /// Called when user logs out to cleanup any active dialogs.
        /// If any dialogs are active, deletes dictionary and deregisters chat listener for this client
        /// </summary>
        /// <param name="client">Client which logged out</param>
        public static void DeregisterAgent(IClientAPI client)
        {
            UUID agentID = client.AgentId;
            m_log.InfoFormat("[GLOEBITMONEYMODULE] Dialog.DeregisterAgent - AgentID:{0}.", agentID);
            bool foundChannelDialogMap = false;

            lock (s_clientDialogMap) {
                if (s_clientDialogMap.ContainsKey(agentID)) {
                    foundChannelDialogMap = true;
                    client.OnChatFromClient -= OnChatFromClientAPI;
                    s_clientDialogMap.Remove(agentID);
                }
            }
            if (!foundChannelDialogMap) {
                m_log.InfoFormat("[GLOEBITMONEYMODULE] Dialog.DeregisterAgent No listener - AgentID:{0}.", agentID);
            } else {
                m_log.InfoFormat("[GLOEBITMONEYMODULE] Dialog.DeregisterAgent Removed listener - AgentID:{0}.", agentID);
            }
        }

        /// <summary>
        /// Called when we receive a chat message from a client for whom we've registered a listener
        /// and m_lastTimeCleanedDialogs is greater than our purge duration (currently 6 hours).
        /// If any active Dialogs are older than ttl (currently 6 hours), calls Close() on those dialogs.
        /// Necessary because a user may not responsd or can ignore or block our messages without us knowing, and
        /// we do not want to add load to the OpenSim server by continuing to get chat events from that user.
        /// Assuming users log out reasonably frequently, this my be unnecessary.
        /// </summary>
        private static void PurgeOldDialogs()
        {
            // Let's avoid two purges running at the same time.
            if (Monitor.TryEnter(s_purgeLock)) {
                try {
                    if (s_lastPurgedOldDialogs.CompareTo(DateTime.UtcNow.AddHours(-6)) < 0) {
                        // Time to purge.  Reset s_lastPurgedOldDialogs so no other thread will purge after the Monitor exists.
                        s_lastPurgedOldDialogs = DateTime.UtcNow;
                    } else {
                        // Not yet time.  Return
                        return;
                    }
                } finally {
                    // Allow other threads access to this resource again.
                    Monitor.Exit(s_purgeLock);
                }
            } else {
                // another thread is making this check.  Return
                return;
            }

            // If we've reached this point, then we have a single thread which has reset s_lastPurgedOldDialogs and is ready to purge.

            m_log.InfoFormat("[GLOEBITMONEYMODULE] Dialog.PurgeOldDialogs.");

            List<Dialog> dialogsToPurge = new List<Dialog>();

            lock (s_clientDialogMap) {
                foreach( KeyValuePair<UUID, Dictionary<int, Dialog>> kvp in s_clientDialogMap )
                {
                    foreach (KeyValuePair<int, Dialog> idp in kvp.Value) {
                        if (idp.Value.cTime.CompareTo(DateTime.UtcNow.AddHours(-6)) < 0) {
                            dialogsToPurge.Add(idp.Value);
                        }
                    }
                }
            }

            foreach( Dialog dialog in dialogsToPurge ) {
                // If any of these have already been closed, we'll produce a WarnFormat log.
                dialog.Close();
            }
        }

    };

    /// <summary>
    /// Class for asking a user to authorize or report a subscription (automated/unattended) payment
    /// triggered by a scripted object which attempted an automatic debit from its owner.
    /// Should be sent to a user whenever LLGiveMoney or LLTransferLindens causes a transaction request
    /// for a subscription (scripted object) for which the user hasn't already authorized from the Gloebit website.
    /// Upon response, we either send a fraud report or build a URL for the user to approve/decline a pending
    /// subscription authorization for this subscription (scripted object).
    /// To send this dialog to a user, use the following command
    /// --- Dialog.Send(new CreateSubscriptionAuthorizationDialog(<constructor params>))
    /// </summary>
    public class CreateSubscriptionAuthorizationDialog : Dialog
    {
        // Name of the agent to whom this dialog is being delivered
        public readonly string AgentName;    // Name of the agent we're sending the dialog to and requesting auths this subscription

        // Details of scripted object which caused this subscription creation
        public readonly UUID ObjectID;       // ID of object which attempted the auto debit.
        public readonly string ObjectName;   // name of object which attempted the auto debit.
        public readonly string ObjectDescription;

        // Details of attempted, failed transaction resulting in this create subscription authorization dialog
        public readonly UUID TransactionID;  // id of the auto debit transaction which failed due to lack of authorization
        public readonly UUID PayeeID;        // ID of the agent receiving the proceeds
        public readonly string PayeeName;    // name of the agent receiving the proceeds
        public readonly int Amount;          // The amount of the auto-debit transaction
        public readonly UUID SubscriptionID; // The subscription id return by GloebitAPI.CreateSubscription

        // TODO: can these be static, or should we be passing in the m_api instead?
        public readonly GloebitAPIWrapper apiW;         // The GloebitAPIWrapper environment that is currently active
        public readonly Uri callbackBaseURI;    // The economyURL for the sim - used if we decide to create callbacks.


        // Create static variables here so we only need one string array
        private const string c_title = "Subscription Authorization Request (scripted object auto-debit)";
        private static readonly string[] c_buttons = new string[3] {"Authorize", "Ignore", "Report Fraud"};

        // Create variable we can format once in constructor to return for MsgBody
        private readonly string m_body;

        protected override string MsgTitle
        {
            get
            {
                return c_title;
            }
        }
        protected override string MsgBody
        {
            get
            {
                return m_body;
            }
        }
        protected override string[] ButtonResponses
        {
            get
            {
                return c_buttons;
            }
        }

        /// <summary>
        /// Constructs a CreateSubscriptionAuthorizationDialog
        /// </summary>
        /// <param name="client">IClientAPI of agent that script attempted to auto-debit</param>
        /// <param name="agentID">UUID of agent that script attempted to auto-debit</param>
        /// <param name="agentName">String name of the OpenSim user who is being asked to authorize</param>
        /// <param name="objectID">UUID of object containing the script which attempted the auto-debit</param>
        /// <param name="objectDescription">Description of object containing the script which attempted the auto-debit</param>
        /// <param name="objectName">Name of object containing the script which attempted the auto-debit</param>
        /// <param name="transactionID">UUID of auto-debit transaction that failed due to lack of authorization</param>
        /// <param name="payeeID">UUID of the OpenSim user who is being paid by the object/script/subscription</param>
        /// <param name="payeeName">String name of the OpenSim user who is being paid by the object/script/subscription</param>
        /// <param name="amount">int amount of the failed transaction which triggered this authorization request</param>
        /// <param name="subscriptionID">UUID of subscription created/returned by Gloebit and for which authorization is being requested</param>
        /// <param name="activeApiW">GloebitAPIWrapper active for this GMM</param>
        /// <param name="appCallbackBaseURI">Base URI for any callbacks this request makes back into the app</param>
        public CreateSubscriptionAuthorizationDialog(IClientAPI client, UUID agentID, string agentName, UUID objectID, string objectName, string objectDescription, UUID transactionID, UUID payeeID, string payeeName, int amount, UUID subscriptionID, GloebitAPIWrapper activeApiW, Uri appCallbackBaseURI) : base(client, agentID)
        {
            this.AgentName = agentName;

            this.ObjectID = objectID;
            this.ObjectName = objectName;
            this.ObjectDescription = objectDescription;

            this.TransactionID = transactionID;
            this.PayeeID = payeeID;
            this.PayeeName = payeeName;
            this.Amount = amount;
            this.SubscriptionID = subscriptionID;

            this.apiW = activeApiW;
            this.callbackBaseURI = appCallbackBaseURI;

            this.m_body = String.Format("\nA payment was attempted by a scripted object you own.  To allow payments triggered by this object, you must authorize it from the Gloebit Website.\n\nObject:\n   {0}\n   {1}\nTo:\n   {2}\n   {3}\nAmount:\n   {4} Gloebits", ObjectName, ObjectID, PayeeName, PayeeID, Amount);

            //this.m_body = String.Format("\nAn auto-debit was attempted by an object which you have not yet authorized to auto-debit from the Gloebit Website.\n\nObject:\n   {0}\n   {1}\nTo:\n   {2}\n   {3}\nAmount:\n   {4} Gloebits", ObjectName, ObjectID, PayeeName, PayeeID, Amount);

            // TODO: what else do we need to track for handling auth or fraud reporting on response?

            // TODO: should we also save and double check all the region/grid/app info?
        }

        /// <summary>
        /// Processes the user response (click of button on dialog) to a CreateSubscriptionAuthorizationDialog.
        /// --- Ignore: does nothing.
        /// --- Report Fraud: sends fraud report to Gloebit
        /// --- Authorize: Creates pending authorization subscription for this user and the referenced subscription
        /// </summary>
        /// <param name="client">IClientAPI of sender of response</param>
        /// <param name="chat">response sent</param>
        protected override void ProcessResponse(IClientAPI client, OSChatMessage chat)
        {
            switch (chat.Message) {
            case "Ignore":
                // User actively ignored.  remove from our message listener
                break;
            case "Authorize":
                // Create authorization

                string subscriptionIDStr = SubscriptionID.ToString();
                string apiUrl = apiW.m_url.ToString();

                GloebitSubscription sub = GloebitSubscription.GetBySubscriptionID(subscriptionIDStr, apiUrl);
                // IF null, there was a db error on storing this -- test store functions for db impl
                if (sub == null) {
                    string msg = String.Format("[GLOEBITMONEYMODULE] CreateSubscriptionAuthorizationDialog.ProcessResponse Could not retrieve subscription.  Likely DB error when storing subID:{0}", subscriptionIDStr);
                    m_log.Error(msg);
                    throw new Exception(msg);
                }
                apiW.AuthorizeSubscription(AgentID, String.Empty, sub, false);
                break;
            case "Report Fraud":
                // Report to Gloebit
                // TODO: fire off fraud report to Gloebit
                break;
            default:
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] CreateSubscriptionAuthorizationDialog.ProcessResponse Received unexpected dialog response message:{0}", chat.Message);
                break;
            }
        }


    };


    /// <summary>
    /// Class for asking a user to authorize or report a subscription (automated/unattended) payment
    /// triggered by a scripted object which attempted an automatic debit from its owner
    /// for which a pending SubscriptionAuthorizationa already exists.
    /// Should be sent to a user whenever LLGiveMoney or LLTransferLindens causes a transaction request
    /// for a subscription (scripted object) for which the user already has a pending authorization request on the Gloebit website.
    /// Upon response, we either send a fraud report or build a URL for the user to approve/decline the pending
    /// subscription authorization for this subscription (scripted object).
    /// To send this dialog to a user, use the following command
    /// --- Dialog.Send(new PendingSubscriptionAuthorizationDialog(<constructor params>))
    /// </summary>
    public class PendingSubscriptionAuthorizationDialog : Dialog
    {
        // Details of scripted object which caused this subscription creation
        public readonly UUID ObjectID;       // ID of object which attempted the auto debit.
        public readonly string ObjectName;   // name of object which attempted the auto debit.
        public readonly string ObjectDescription;

        // Details of attempted, failed transaction resulting in this create subscription authorization dialog
        public readonly UUID TransactionID;  // id of the auto debit transaction which failed due to lack of authorization
        public readonly UUID PayeeID;        // ID of the agent receiving the proceeds
        public readonly string PayeeName;    // name of the agent receiving the proceeds
        public readonly int Amount;          // The amount of the auto-debit transaction
        public readonly UUID SubscriptionID; // The subscription id return by GloebitAPI.CreateSubscription

        // TODO: can these be static, or should we be passing in the m_api instead?
        public readonly GloebitAPIWrapper apiW;      // The GloebitAPIWrapper environment that is currently active
        public readonly Uri callbackBaseURI;      // The economyURL for the sim - used if we decide to create callbacks.

        // Name of the agent to whom this dialog is being delivered
        public readonly string AgentName;    // Name of the agent we're sending the dialog to and requesting auths this subscription

        // pending subscription authorization information
        public readonly UUID SubscriptionAuthorizationID;   // The subscription authorization id returned by the failed transaction.

        // Create static variables here so we only need one string array
        private const string c_title = "Pending Subscription Authorization Request (scripted object auto-debit)";
        private static readonly string[] c_buttons = new string[3] {"Respond", "Ignore", "Report Fraud"};

        // Create variable we can format once in constructor to return for MsgBody
        private readonly string m_body;

        protected override string MsgTitle
        {
            get
            {
                return c_title;
            }
        }
        protected override string MsgBody
        {
            get
            {
                return m_body;
            }
        }
        protected override string[] ButtonResponses
        {
            get
            {
                return c_buttons;
            }
        }


        /// <summary>
        /// Constructs a PendingSubscriptionAuthorizationDialog
        /// </summary>
        /// <param name="client">IClientAPI of agent that script attempted to auto-debit</param>
        /// <param name="agentID">UUID of agent that script attempted to auto-debit</param>
        /// <param name="agentName">String name of the OpenSim user who is being asked to authorize</param>
        /// <param name="objectID">UUID of object containing the script which attempted the auto-debit</param>
        /// <param name="objectDescription">Description of object containing the script which attempted the auto-debit</param>
        /// <param name="objectName">Name of object containing the script which attempted the auto-debit</param>
        /// <param name="transactionID">UUID of auto-debit transaction that failed due to lack of authorization</param>
        /// <param name="payeeID">UUID of the OpenSim user who is being paid by the object/script/subscription</param>
        /// <param name="payeeName">String name of the OpenSim user who is being paid by the object/script/subscription</param>
        /// <param name="amount">int amount of the failed transaction which triggered this authorization request</param>
        /// <param name="subscriptionID">UUID of subscription created/returned by Gloebit and for which authorization is being requested</param>
        /// <param name="subscriptionAuthorizationID">UUID of the pending subscription authorization returned by Gloebit with the failed transaction</param>
        /// <param name="activeApiW">GloebitAPIWrapper active for this GMM</param>
        /// <param name="appCallbackBaseURI">Base URI for any callbacks this request makes back into the app</param>
        public PendingSubscriptionAuthorizationDialog(IClientAPI client, UUID agentID, string agentName, UUID objectID, string objectName, string objectDescription, UUID transactionID, UUID payeeID, string payeeName, int amount, UUID subscriptionID, UUID subscriptionAuthorizationID, GloebitAPIWrapper activeApiW, Uri appCallbackBaseURI) : base(client, agentID)
        {
            this.AgentName = agentName;

            this.ObjectID = objectID;
            this.ObjectName = objectName;
            this.ObjectDescription = objectDescription;

            this.TransactionID = transactionID;
            this.PayeeID = payeeID;
            this.PayeeName = payeeName;
            this.Amount = amount;
            this.SubscriptionID = subscriptionID;
            this.SubscriptionAuthorizationID = subscriptionAuthorizationID;

            this.apiW = activeApiW;
            this.callbackBaseURI = appCallbackBaseURI;

            this.m_body = String.Format("\nA payment was attempted by a scripted object you own.  To allow payments triggered by this object, you must authorize it from the Gloebit Website.  A pending authorization request for this object exists to which you have not yet responded.\n\nObject:\n   {0}\n   {1}\nTo:\n   {2}\n   {3}\nAmount:\n   {4} Gloebits", ObjectName, ObjectID, PayeeName, PayeeID, Amount);

            // TODO: what else do we need to track for handling auth or fraud reporting on response?

            // TODO: should we also save and double check all the region/grid/app info?
        }

        /// <summary>
        /// Processes the user response (click of button on dialog) to a PendingSubscriptionAuthorizationDialog.
        /// --- Ignore: does nothing.
        /// --- Report Fraud: sends fraud report to Gloebit
        /// --- Authorize: Delivers authorization link to user
        /// </summary>
        /// <param name="client">IClientAPI of sender of response</param>
        /// <param name="chat">response sent</param>
        protected override void ProcessResponse(IClientAPI client, OSChatMessage chat)
        {
            switch (chat.Message) {
            case "Ignore":
                // User actively ignored.  remove from our message listener
                break;
            case "Respond":
                // Resend authorization link

                string subscriptionIDStr = SubscriptionID.ToString();
                string apiUrl = apiW.m_url.ToString();
                GloebitSubscription sub = GloebitSubscription.GetBySubscriptionID(subscriptionIDStr, apiUrl);
                // TODO: Do we need to check if this is null?  Shouldn't happen.

                // Send Authorize URL
                apiW.AuthorizeSubscription(client.AgentId, SubscriptionAuthorizationID.ToString(), sub, false);

                break;
            case "Report Fraud":
                // Report to Gloebit
                // TODO: fire off fraud report to Gloebit
                break;
            default:
                m_log.ErrorFormat("[GLOEBITMONEYMODULE] PendingSubscriptionAuthorizationDialog.ProcessResponse Received unexpected dialog response message:{0}", chat.Message);
                break;
            }
        }
    };
}
