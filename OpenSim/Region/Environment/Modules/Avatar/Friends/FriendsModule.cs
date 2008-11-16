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
using System.Reflection;
using OpenMetaverse;
using OpenMetaverse.Packets;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Region.Interfaces;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Avatar.Friends
{
    /*
        This module handles adding/removing friends, and the the presence
        notification process for login/logoff of friends.

        The presence notification works as follows:
        - After the user initially connects to a region (so we now have a UDP
          connection to work with), this module fetches the friends of user
          (those are cached), their on-/offline status, and info about the
          region they are in from the MessageServer.
        - (*) It then informs the user about the on-/offline status of her friends.
        - It then informs all online friends currently on this region-server about
          user's new online status (this will save some network traffic, as local
          messages don't have to be transferred inter-region, and it will be all
          that has to be done in Standalone Mode).
        - For the rest of the online friends (those not on this region-server),
          this module uses the provided region-information to map users to
          regions, and sends one notification to every region containing the
          friends to inform on that server.
        - The region-server will handle that in the following way:
          - If it finds the friend, it informs her about the user being online.
          - If it doesn't find the friend (maybe she TPed away in the meantime),
            it stores that information.
          - After it processed all friends, it returns the list of friends it
            couldn't find.
        - If this list isn't empty, the FriendsModule re-requests information
          about those online friends that have been missed and starts at (*)
          again until all friends have been found, or until it tried 3 times
          (to prevent endless loops due to some uncaught error).

        NOTE: Online/Offline notifications don't need to be sent on region change.

        We implement two XMLRpc handlers here, handling all the inter-region things
        we have to handle:
        - On-/Offline-Notifications (bulk)
        - Terminate Friendship messages (single)
     */

    public class FriendsModule : IRegionModule
    {
        private class Transaction
        {
            public UUID agentID;
            public string agentName;
            public uint count;

            public Transaction(UUID agentID, string agentName)
            {
                this.agentID = agentID;
                this.agentName = agentName;
                this.count = 1;
            }
        }

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Cache m_friendLists = new Cache(CacheFlags.AllowUpdate);

        private Dictionary<UUID, ulong> m_rootAgents = new Dictionary<UUID, ulong>();

        private Dictionary<UUID, Transaction> m_pendingFriendRequests = new Dictionary<UUID, Transaction>();
        private Dictionary<UUID, UUID> m_pendingCallingcardRequests = new Dictionary<UUID,UUID>();

        private Scene m_initialScene; // saves a lookup if we don't have a specific scene
        private Dictionary<ulong, Scene> m_scenes = new Dictionary<ulong,Scene>();
        private IMessageTransferModule m_TransferModule = null;

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            lock (m_scenes)
            {
                if (m_scenes.Count == 0)
                {
                    scene.AddXmlRPCHandler("presence_update_bulk", processPresenceUpdateBulk);
                    scene.AddXmlRPCHandler("terminate_friend", processTerminateFriend);
                    m_friendLists.DefaultTTL = new TimeSpan(1, 0, 0);  // store entries for one hour max
                    m_initialScene = scene;
                }

                if (!m_scenes.ContainsKey(scene.RegionInfo.RegionHandle))
                    m_scenes[scene.RegionInfo.RegionHandle] = scene;
            }
            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;
            scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
            scene.EventManager.OnMakeChildAgent += MakeChildAgent;
            scene.EventManager.OnClientClosed += ClientClosed;
        }

        public void PostInitialise()
        {
            List<Scene> scenes = new List<Scene>(m_scenes.Values);
            m_TransferModule = scenes[0].RequestModuleInterface<IMessageTransferModule>();
            if (m_TransferModule == null)
                m_log.Error("[FRIENDS]: Unable to find a message transfer module, friendship offers will not work");
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "FriendsModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion

        public XmlRpcResponse processPresenceUpdateBulk(XmlRpcRequest req)
        {
            Hashtable requestData = (Hashtable)req.Params[0];

            List<UUID> friendsNotHere = new List<UUID>();

            // this is called with the expectation that all the friends in the request are on this region-server.
            // But as some time passed since we checked (on the other region-server, via the MessagingServer),
            // some of the friends might have teleported away.
            // Actually, even now, between this line and the sending below, some people could TP away. So,
            // we'll have to lock the m_rootAgents list for the duration to prevent/delay that.
            lock (m_rootAgents)
            {
                List<ScenePresence> friendsHere = new List<ScenePresence>();
                try
                {
                    UUID agentID = new UUID((string)requestData["agentID"]);
                    bool agentOnline = (bool)requestData["agentOnline"];
                    int count = (int)requestData["friendCount"];
                    for (int i = 0; i < count; ++i)
                    {
                        UUID uuid;
                        if (UUID.TryParse((string)requestData["friendID_" + i], out uuid))
                        {
                            if (m_rootAgents.ContainsKey(uuid)) friendsHere.Add(GetRootPresenceFromAgentID(uuid));
                            else friendsNotHere.Add(uuid);
                        }
                    }

                    // now send, as long as they are still here...
                    UUID[] agentUUID = new UUID[] { agentID };
                    if (agentOnline)
                    {
                        foreach (ScenePresence agent in friendsHere)
                        {
                            agent.ControllingClient.SendAgentOnline(agentUUID);
                        }
                    }
                    else
                    {
                        foreach (ScenePresence agent in friendsHere)
                        {
                            agent.ControllingClient.SendAgentOffline(agentUUID);
                        }
                    }
                }
                catch(Exception e)
                {
                    m_log.Warn("[FRIENDS]: Got exception while parsing presence_update_bulk request:", e);
                }
            }

            // no need to lock anymore; if TPs happen now, worst case is that we have an additional agent in this region,
            // which should be caught on the next iteration...
            Hashtable result = new Hashtable();
            int idx = 0;
            foreach (UUID uuid in friendsNotHere)
            {
                result["friendID_" + idx++] = uuid.ToString();
            }
            result["friendCount"] = idx;

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = result;

            return response;
        }

        public XmlRpcResponse processTerminateFriend(XmlRpcRequest req)
        {
            Hashtable requestData = (Hashtable)req.Params[0];

            bool success = false;

            UUID agentID;
            UUID friendID;
            if (requestData.ContainsKey("agentID") && UUID.TryParse((string)requestData["agentID"], out agentID) &&
                requestData.ContainsKey("friendID") && UUID.TryParse((string)requestData["friendID"], out friendID))
            {
                // try to find it and if it is there, prevent it to vanish before we sent the message
                lock (m_rootAgents)
                {
                    if (m_rootAgents.ContainsKey(agentID))
                    {
                        m_log.DebugFormat("[FRIEND]: Sending terminate friend {0} to agent {1}", friendID, agentID);
                        GetRootPresenceFromAgentID(agentID).ControllingClient.SendTerminateFriend(friendID);
                        success = true;
                    }
                }
            }

            // return whether we were successful
            Hashtable result = new Hashtable();
            result["success"] = success;

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = result;
            return response;
        }

        private void OnNewClient(IClientAPI client)
        {
            // All friends establishment protocol goes over instant message
            // There's no way to send a message from the sim
            // to a user to 'add a friend' without causing dialog box spam

            // Subscribe to instant messages
//            client.OnInstantMessage += OnInstantMessage;

            // Friend list management
            client.OnApproveFriendRequest += OnApproveFriendRequest;
            client.OnDenyFriendRequest += OnDenyFriendRequest;
            client.OnTerminateFriendship += OnTerminateFriendship;

            // ... calling card handling...
            client.OnOfferCallingCard += OnOfferCallingCard;
            client.OnAcceptCallingCard += OnAcceptCallingCard;
            client.OnDeclineCallingCard += OnDeclineCallingCard;

            // we need this one exactly once per agent session (see comments in the handler below)
            client.OnEconomyDataRequest += OnEconomyDataRequest;

            // if it leaves, we want to know, too
            client.OnLogout += OnLogout;
        }

        private void ClientClosed(UUID AgentId)
        {
            // agent's client was closed. As we handle logout in OnLogout, this here has only to handle
            // TPing away (root agent is closed) or TPing/crossing in a region far enough away (client
            // agent is closed).
            // NOTE: In general, this doesn't mean that the agent logged out, just that it isn't around
            // in one of the regions here anymore.
            lock (m_rootAgents)
            {
                if (m_rootAgents.ContainsKey(AgentId))
                {
                    m_rootAgents.Remove(AgentId);
                    m_log.Info("[FRIEND]: Removing " + AgentId + ". Agent was closed.");
                }
            }
        }

        private void AvatarEnteringParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            lock (m_rootAgents)
            {
                m_rootAgents[avatar.UUID] = avatar.RegionHandle;
                m_log.Info("[FRIEND]: Claiming " + avatar.Firstname + " " + avatar.Lastname + " in region:" + avatar.RegionHandle + ".");
                // Claim User! my user!  Mine mine mine!
            }
        }

        private void MakeChildAgent(ScenePresence avatar)
        {
            lock (m_rootAgents)
            {
                if (m_rootAgents.ContainsKey(avatar.UUID))
                {
                    // only delete if the region matches. As this is a shared module, the avatar could be
                    // root agent in another region on this server.
                    if (m_rootAgents[avatar.UUID] == avatar.RegionHandle)
                    {
                        m_rootAgents.Remove(avatar.UUID);
                        m_log.Info("[FRIEND]: Removing " + avatar.Firstname + " " + avatar.Lastname + " as a root agent");
                    }
                }
            }
        }

        private ScenePresence GetRootPresenceFromAgentID(UUID AgentID)
        {
            ScenePresence returnAgent = null;
            lock (m_scenes)
            {
                ScenePresence queryagent = null;
                foreach (Scene scene in m_scenes.Values)
                {
                    queryagent = scene.GetScenePresence(AgentID);
                    if (queryagent != null)
                    {
                        if (!queryagent.IsChildAgent)
                        {
                            returnAgent = queryagent;
                            break;
                        }
                    }
                }
            }
            return returnAgent;
        }

        private ScenePresence GetAnyPresenceFromAgentID(UUID AgentID)
        {
            ScenePresence returnAgent = null;
            lock (m_scenes)
            {
                ScenePresence queryagent = null;
                foreach (Scene scene in m_scenes.Values)
                {
                    queryagent = scene.GetScenePresence(AgentID);
                    if (queryagent != null)
                    {
                        returnAgent = queryagent;
                        break;
                    }
                }
            }
            return returnAgent;
        }

        #region FriendRequestHandling

        private void OnInstantMessage(IClientAPI client, UUID fromAgentID,
                                      UUID toAgentID,
                                      UUID imSessionID, uint timestamp, string fromAgentName,
                                      string message, byte dialog, bool fromGroup, byte offline,
                                      uint ParentEstateID, Vector3 Position, UUID RegionID,
                                      byte[] binaryBucket)
        {
            // Friend Requests go by Instant Message..    using the dialog param
            // https://wiki.secondlife.com/wiki/ImprovedInstantMessage

            if (dialog == (byte)InstantMessageDialog.FriendshipOffered) // 38
            {
                // this is triggered by the initiating agent and has two parts:
                // A local agent offers friendship to some possibly remote friend.
                // A IM is triggered, processed here (1), sent to the destination region,
                // and processed there in this part of the code again (2).
                // (1) has fromAgentSession != UUID.Zero,
                // (2) has fromAgentSession == UUID.Zero (don't leak agent sessions to other agents)
                // For (1), build the IM to send to the other region (and trigger sending it)
                // FOr (2), just store the transaction; we will wait for Approval or Decline

                // some properties are misused here:
                // fromAgentName is the *destination* name (the friend we offer friendship to)

                // (1)
                // send the friendship-offer to the target
                m_log.InfoFormat("[FRIEND]: Offer(38) - From: {0}, FromName: {1} To: {2}, Session: {3}, Message: {4}, Offline {5}",
                           fromAgentID, fromAgentName, toAgentID, imSessionID, message, offline);

                UUID transactionID = UUID.Random();

                // 1.20 protocol sends an UUID in the message field, instead of the friendship offer text.
                // For interoperability, we have to clear that
                if (Util.isUUID(message)) message = "";

                GridInstantMessage msg = new GridInstantMessage();
                msg.fromAgentID = fromAgentID.Guid;
                msg.toAgentID = toAgentID.Guid;
                msg.imSessionID = transactionID.Guid; // Start new transaction
                m_log.DebugFormat("[FRIEND]: new transactionID: {0}", msg.imSessionID);
                msg.timestamp = timestamp;
                if (client != null)
                {
                    msg.fromAgentName = client.Name; // fromAgentName;
                }
                else
                {
                    msg.fromAgentName = "(hippos)"; // Added for posterity.  This means that we can't figure out who sent it
                }
                msg.message = message;
                msg.dialog = dialog;
                msg.fromGroup = fromGroup;
                msg.offline = offline;
                msg.ParentEstateID = ParentEstateID;
                msg.Position = Position;
                msg.RegionID = RegionID.Guid;
                msg.binaryBucket = binaryBucket;

                m_log.DebugFormat("[FRIEND]: storing transactionID {0} on sender side", transactionID);
                lock (m_pendingFriendRequests)
                {
                    m_pendingFriendRequests.Add(transactionID, new Transaction(fromAgentID, fromAgentName));
                    outPending();
                }

                // we don't want to get that new IM into here if we aren't local, as only on the destination
                // should receive it. If we *are* local, *we* are the destination, so we have to receive it.
                // As grid-IMs are routed to all modules (in contrast to local IMs), we have to decide here.

                // We don't really care which local scene we pipe it through.
                if (m_TransferModule != null)
                {
                    m_TransferModule.SendInstantMessage(msg,
                        delegate(bool success) {}
                    );
                }
            }
            else if (dialog == (byte)InstantMessageDialog.FriendshipAccepted) // 39
            {
                // accepting the friendship offer causes a type 39 IM being sent to the (possibly remote) initiator
                // toAgentID is initiator, fromAgentID is new friend (which just approved)
                m_log.DebugFormat("[FRIEND]: 39 - from client {0}, agent {1} {2}, imsession {3} to {4}: {5} (dialog {6})",
                  client != null ? client.AgentId.ToString() : "<null>",
                  fromAgentID, fromAgentName, imSessionID, toAgentID, message, dialog);
                lock (m_pendingFriendRequests)
                {
                    if (!m_pendingFriendRequests.ContainsKey(imSessionID))
                    {
                        m_log.DebugFormat("[FRIEND]: Got friendship approval from {0} to {1} without matching transaction {2}",
                                          fromAgentID, toAgentID, imSessionID);
                        return; // unknown transaction
                    }
                    // else found pending friend request with that transaction => remove it if we handled all
                    if (--m_pendingFriendRequests[imSessionID].count <= 0) m_pendingFriendRequests.Remove(imSessionID);
                    outPending();
                }

                // a new friend was added in the initiator's and friend's data, so the cache entries are wrong now.
                lock (m_friendLists)
                {
                    m_friendLists.Invalidate(toAgentID);
                    m_friendLists.Invalidate(fromAgentID);
                }

                // now send presence update and add a calling card for the new friend

                ScenePresence initiator = GetAnyPresenceFromAgentID(toAgentID);
                if (initiator == null)
                {
                    // quite wrong. Shouldn't happen.
                    m_log.WarnFormat("[FRIEND]: Coudn't find initiator of friend request {0}", toAgentID);
                    return;
                }

                // tell initiator that friend is online
                initiator.ControllingClient.SendAgentOnline(new UUID[] { fromAgentID });

                // find the folder for the friend...
                InventoryFolderImpl folder =
                    initiator.Scene.CommsManager.UserProfileCacheService.GetUserDetails(toAgentID).FindFolderForType((int)InventoryType.CallingCard);
                if (folder != null)
                {
                    // ... and add the calling card
                    CreateCallingCard(initiator.ControllingClient, fromAgentID, folder.ID, fromAgentName);
                }
            }
            else if (dialog == (byte)InstantMessageDialog.FriendshipDeclined) // 40
            {
                // declining the friendship offer causes a type 40 IM being sent to the (possibly remote) initiator
                // toAgentID is initiator, fromAgentID declined friendship
                m_log.DebugFormat("[FRIEND]: 40 - from client {0}, agent {1} {2}, imsession {3} to {4}: {5} (dialog {6})",
                  client != null ? client.AgentId.ToString() : "<null>",
                  fromAgentID, fromAgentName, imSessionID, toAgentID, message, dialog);

                // not much to do, just clean up the transaction...
                lock (m_pendingFriendRequests)
                {
                    if (!m_pendingFriendRequests.ContainsKey(imSessionID))
                    {
                        m_log.DebugFormat("[FRIEND]: Got friendship denial from {0} to {1} without matching transaction {2}",
                                          fromAgentID, toAgentID, imSessionID);
                        return; // unknown transaction
                    }
                    // else found pending friend request with that transaction => remove it if we handled all
                    if (--m_pendingFriendRequests[imSessionID].count <= 0) m_pendingFriendRequests.Remove(imSessionID);
                    outPending();
                }
            }
        }

        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            if (msg.dialog == (byte)InstantMessageDialog.FriendshipOffered)
            {
                // we are on the receiving end here; just add the transactionID
                // to the stored transactions for later lookup
                //
                m_log.DebugFormat("[FRIEND]: storing transactionID {0} on "+
                        "receiver side", msg.imSessionID);

                lock (m_pendingFriendRequests)
                {
                    // if both are on the same region-server, the transaction
                    // is stored already, but we have to update the name
                    //
                    if (m_pendingFriendRequests.ContainsKey(
                            new UUID(msg.imSessionID)))
                    {
                        m_pendingFriendRequests[new UUID(msg.imSessionID)].agentName =
                                msg.fromAgentName;
                        m_pendingFriendRequests[new UUID(msg.imSessionID)].count++;
                    }
                    else m_pendingFriendRequests.Add(new UUID(msg.imSessionID),
                            new Transaction(new UUID(msg.fromAgentID),
                            msg.fromAgentName));

                    outPending();
                }

                return;
            }

            // Just call the IM handler above
            // This event won't be raised unless we have that agent,
            // so we can depend on the above not trying to send
            // via grid again
            //
            OnInstantMessage(null, new UUID(msg.fromAgentID),
                    new UUID(msg.toAgentID), new UUID(msg.imSessionID),
                    msg.timestamp, msg.fromAgentName, msg.message,
                    msg.dialog, msg.fromGroup, msg.offline,
                    msg.ParentEstateID, msg.Position,
                    new UUID(msg.RegionID), msg.binaryBucket);
        }

        private void OnApproveFriendRequest(IClientAPI client, UUID agentID, UUID transactionID, List<UUID> callingCardFolders)
        {
            m_log.DebugFormat("[FRIEND]: Got approve friendship from {0} {1}, agentID {2}, tid {3}",
                              client.Name, client.AgentId, agentID, transactionID);
            Transaction transaction;
            lock (m_pendingFriendRequests)
            {
                if (!m_pendingFriendRequests.TryGetValue(transactionID, out transaction))
                {
                    m_log.DebugFormat("[FRIEND]: Got friendship approval {0} from {1} ({2}) without matching transaction {3}",
                                      agentID, client.AgentId, client.Name, transactionID);
                    return; // unknown transaction
                }
                // else found pending friend request with that transaction => remove if done with all
                if (--m_pendingFriendRequests[transactionID].count <= 0) m_pendingFriendRequests.Remove(transactionID);
                outPending();
            }

            UUID friendID = transaction.agentID;
            m_log.DebugFormat("[FRIEND]: {0} ({1}) approved friendship request from {2}",
                              client.Name, client.AgentId, friendID);

            Scene SceneAgentIn = m_initialScene;
            // we need any presence to send the packets to, not necessarily the root agent...
            ScenePresence agentpresence = GetAnyPresenceFromAgentID(agentID);
            if (agentpresence != null)
            {
                SceneAgentIn = agentpresence.Scene;
            }

            // store the new friend persistently for both avatars
            SceneAgentIn.StoreAddFriendship(friendID, agentID, (uint) FriendRights.CanSeeOnline);

            // The cache entries aren't valid anymore either, as we just added a friend to both sides.
            lock (m_friendLists)
            {
                m_friendLists.Invalidate(agentID);
                m_friendLists.Invalidate(friendID);
            }

            // create calling card
            CreateCallingCard(client, friendID, callingCardFolders[0], transaction.agentName);

            // Compose response to other agent.
            GridInstantMessage msg = new GridInstantMessage();
            msg.toAgentID = friendID.Guid;
            msg.fromAgentID = agentID.Guid;
            msg.fromAgentName = client.Name;
            msg.fromGroup = false;
            msg.imSessionID = transactionID.Guid;
            msg.message = agentID.Guid.ToString();
            msg.ParentEstateID = 0;
            msg.timestamp = (uint) Util.UnixTimeSinceEpoch();
            msg.RegionID = SceneAgentIn.RegionInfo.RegionID.Guid;
            msg.dialog = (byte) InstantMessageDialog.FriendshipAccepted;
            msg.Position = Vector3.Zero;
            msg.offline = (byte) 0;
            msg.binaryBucket = new byte[0];

            // we don't want to get that new IM into here if we aren't local, as only on the destination
            // should receive it. If we *are* local, *we* are the destination, so we have to receive it.
            // As grid-IMs are routed to all modules (in contrast to local IMs), we have to decide here.

            // now we have to inform the agent about the friend. For the opposite direction, this happens in the handler
            // of the type 39 IM
            if (m_TransferModule != null)
            {
                m_TransferModule.SendInstantMessage(msg,
                    delegate(bool success) {}
                );
            }

            // tell client that new friend is online
            client.SendAgentOnline(new UUID[] { friendID });
        }

        private void OnDenyFriendRequest(IClientAPI client, UUID agentID, UUID transactionID, List<UUID> callingCardFolders)
        {
            m_log.DebugFormat("[FRIEND]: Got deny friendship from {0} {1}, agentID {2}, tid {3}",
                              client.Name, client.AgentId, agentID, transactionID);
            Transaction transaction;
            lock (m_pendingFriendRequests)
            {
                if (!m_pendingFriendRequests.TryGetValue(transactionID, out transaction))
                {
                    m_log.DebugFormat("[FRIEND]: Got friendship denial {0} from {1} ({2}) without matching transaction {3}",
                                      agentID, client.AgentId, client.Name, transactionID);
                    return;
                }
                // else found pending friend request with that transaction.
                if (--m_pendingFriendRequests[transactionID].count <= 0) m_pendingFriendRequests.Remove(transactionID);
                outPending();
            }
            UUID friendID = transaction.agentID;

            Scene SceneAgentIn = m_initialScene;
            ScenePresence agentpresence = GetRootPresenceFromAgentID(agentID);
            if (agentpresence != null)
            {
                SceneAgentIn = agentpresence.Scene;
            }

            // Compose response to other agent.
            GridInstantMessage msg = new GridInstantMessage();
            msg.toAgentID = friendID.Guid;
            msg.fromAgentID = agentID.Guid;
            msg.fromAgentName = client.Name;
            msg.fromGroup = false;
            msg.imSessionID = transactionID.Guid;
            msg.message = agentID.Guid.ToString();
            msg.ParentEstateID = 0;
            msg.timestamp = (uint) Util.UnixTimeSinceEpoch();
            msg.RegionID = SceneAgentIn.RegionInfo.RegionID.Guid;
            msg.dialog = (byte) InstantMessageDialog.FriendshipDeclined;
            msg.Position = Vector3.Zero;
            msg.offline = (byte) 0;
            msg.binaryBucket = new byte[0];

            // we don't want to get that new IM into here if we aren't local, as only on the destination
            // should receive it. If we *are* local, *we* are the destination, so we have to receive it.
            // As grid-IMs are routed to all modules (in contrast to local IMs), we have to decide here.

            // now we have to inform the agent about the friend. For the opposite direction, this happens in the handler
            // of the type 39 IM
            if (m_TransferModule != null)
            {
                m_TransferModule.SendInstantMessage(msg,
                    delegate(bool success) {}
                );
            }
        }

        private void OnTerminateFriendship(IClientAPI client, UUID agentID, UUID exfriendID)
        {
            // client.AgentId == agentID!

            // this removes the friends from the stored friendlists. After the next login, they will be gone...
            m_initialScene.StoreRemoveFriendship(agentID, exfriendID);

            // ... now tell the two involved clients that they aren't friends anymore.

            // I don't know why we have to tell <agent>, as this was caused by her, but that's how it works in SL...
            client.SendTerminateFriend(exfriendID);

            // now send the friend, if online
            ScenePresence presence = GetAnyPresenceFromAgentID(exfriendID);
            if (presence != null)
            {
                m_log.DebugFormat("[FRIEND]: Sending terminate friend {0} to agent {1}", agentID, exfriendID);
                presence.ControllingClient.SendTerminateFriend(agentID);
            }
            else
            {
                // retry 3 times, in case the agent TPed from the last known region...
                for (int retry = 0; retry < 3; ++retry)
                {
                    // wasn't sent, so ex-friend wasn't around on this region-server. Fetch info and try to send
                    UserAgentData data = m_initialScene.CommsManager.UserService.GetAgentByUUID(exfriendID);
                    if (!data.AgentOnline)
                    {
                        m_log.DebugFormat("[FRIEND]: {0} is offline, so not sending TerminateFriend", exfriendID);
                        break; // if ex-friend isn't online, we don't need to send
                    }

                    m_log.DebugFormat("[FRIEND]: Sending remote terminate friend {0} to agent {1}@{2}",
                                      agentID, exfriendID, data.Handle);

                    // try to send to foreign region, retry if it fails (friend TPed away, for example)
                    if (m_initialScene.TriggerTerminateFriend(data.Handle, exfriendID, agentID)) break;
                }
            }

            // clean up cache: FriendList is wrong now...
            lock (m_friendLists)
            {
                m_friendLists.Invalidate(agentID);
                m_friendLists.Invalidate(exfriendID);
            }
        }

        #endregion

        #region CallingCards

        private void OnOfferCallingCard(IClientAPI client, UUID destID, UUID transactionID)
        {
            m_log.DebugFormat("[CALLING CARD]: got offer from {0} for {1}, transaction {2}",
                              client.AgentId, destID, transactionID);
            // This might be slightly wrong. On a multi-region server, we might get the child-agent instead of the root-agent
            // (or the root instead of the child)
            ScenePresence destAgent = GetAnyPresenceFromAgentID(destID);
            if (destAgent == null)
            {
                client.SendAlertMessage("The person you have offered a card to can't be found anymore.");
                return;
            }

            lock (m_pendingCallingcardRequests)
            {
                m_pendingCallingcardRequests[transactionID] = client.AgentId;
            }
            // inform the destination agent about the offer
            destAgent.ControllingClient.SendOfferCallingCard(client.AgentId, transactionID);
        }

        private void CreateCallingCard(IClientAPI client, UUID creator, UUID folder, string name)
        {
            InventoryItemBase item = new InventoryItemBase();
            item.AssetID = UUID.Zero;
            item.AssetType = (int)AssetType.CallingCard;
            item.BasePermissions = (uint)PermissionMask.Copy;
            item.CreationDate = Util.UnixTimeSinceEpoch();
            item.Creator = creator;
            item.CurrentPermissions = item.BasePermissions;
            item.Description = "";
            item.EveryOnePermissions = (uint)PermissionMask.None;
            item.Flags = 0;
            item.Folder = folder;
            item.GroupID = UUID.Zero;
            item.GroupOwned = false;
            item.ID = UUID.Random();
            item.InvType = (int)InventoryType.CallingCard;
            item.Name = name;
            item.NextPermissions = item.EveryOnePermissions;
            item.Owner = client.AgentId;
            item.SalePrice = 10;
            item.SaleType = (byte)SaleType.Not;
            ((Scene)client.Scene).AddInventoryItem(client, item);
        }

        private void OnAcceptCallingCard(IClientAPI client, UUID transactionID, UUID folderID)
        {
            m_log.DebugFormat("[CALLING CARD]: User {0} ({1} {2}) accepted tid {3}, folder {4}",
                              client.AgentId,
                              client.FirstName, client.LastName,
                              transactionID, folderID);
            UUID destID;
            lock (m_pendingCallingcardRequests)
            {
                if (!m_pendingCallingcardRequests.TryGetValue(transactionID, out destID))
                {
                    m_log.WarnFormat("[CALLING CARD]: Got a AcceptCallingCard from {0} without an offer before.",
                                     client.Name);
                    return;
                }
                // else found pending calling card request with that transaction.
                m_pendingCallingcardRequests.Remove(transactionID);
            }


            ScenePresence destAgent = GetAnyPresenceFromAgentID(destID);
            // inform sender of the card that destination declined the offer
            if (destAgent != null) destAgent.ControllingClient.SendAcceptCallingCard(transactionID);

            // put a calling card into the inventory of receiver
            CreateCallingCard(client, destID, folderID, destAgent.Name);
        }

        private void OnDeclineCallingCard(IClientAPI client, UUID transactionID)
        {
            m_log.DebugFormat("[CALLING CARD]: User {0} declined card, tid {2}",
                              client.AgentId, transactionID);
            UUID destID;
            lock (m_pendingCallingcardRequests)
            {
                if (!m_pendingCallingcardRequests.TryGetValue(transactionID, out destID))
                {
                    m_log.WarnFormat("[CALLING CARD]: Got a AcceptCallingCard from {0} without an offer before.",
                                     client.Name);
                    return;
                }
                // else found pending calling card request with that transaction.
                m_pendingCallingcardRequests.Remove(transactionID);
            }

            ScenePresence destAgent = GetAnyPresenceFromAgentID(destID);
            // inform sender of the card that destination declined the offer
            if (destAgent != null) destAgent.ControllingClient.SendDeclineCallingCard(transactionID);
        }

        private void SendPresenceState(IClientAPI client, List<FriendListItem> friendList, bool iAmOnline)
        {
            m_log.DebugFormat("[FRIEND]: {0} logged {1}; sending presence updates", client.Name, iAmOnline ? "in" : "out");

            if (friendList == null || friendList.Count == 0)
            {
                m_log.DebugFormat("[FRIEND]: {0} doesn't have friends.", client.Name);
                return; // nothing we can do if she doesn't have friends...
            }

            // collect sets of friendIDs; to send to (online and offline), and to receive from
            // TODO: If we ever switch to .NET >= 3, replace those Lists with HashSets.
            // I can't believe that we have Dictionaries, but no Sets, considering Java introduced them years ago...
            List<UUID> friendIDsToSendTo = new List<UUID>();
            List<UUID> friendIDsToReceiveFromOffline = new List<UUID>();
            List<UUID> friendIDsToReceiveFromOnline = new List<UUID>();
            foreach (FriendListItem item in friendList)
            {
                if (((item.FriendListOwnerPerms | item.FriendPerms) & (uint)FriendRights.CanSeeOnline) != 0)
                {
                    // friend is allowed to see my presence => add
                    if ((item.FriendListOwnerPerms & (uint)FriendRights.CanSeeOnline) != 0) friendIDsToSendTo.Add(item.Friend);

                    // I'm allowed to see friend's presence => add as offline, we might reconsider in a momnet...
                    if ((item.FriendPerms & (uint)FriendRights.CanSeeOnline) != 0) friendIDsToReceiveFromOffline.Add(item.Friend);
                }
            }



            // we now have a list of "interesting" friends (which we have to find out on-/offline state for),
            // friends we want to send our online state to (if *they* are online, too), and
            // friends we want to receive online state for (currently unknown whether online or not)

            // as this processing might take some time and friends might TP away, we try up to three times to
            // reach them. Most of the time, we *will* reach them, and this loop won't loop
            int retry = 0;
            do
            {
                // build a list of friends to look up region-information and on-/offline-state for
                List<UUID> friendIDsToLookup = new List<UUID>(friendIDsToSendTo);
                foreach (UUID uuid in friendIDsToReceiveFromOffline)
                {
                    if (!friendIDsToLookup.Contains(uuid)) friendIDsToLookup.Add(uuid);
                }

                m_log.DebugFormat("[FRIEND]: {0} to lookup, {1} to send to, {2} to receive from for agent {3}",
                                  friendIDsToLookup.Count, friendIDsToSendTo.Count, friendIDsToReceiveFromOffline.Count, client.Name);

                // we have to fetch FriendRegionInfos, as the (cached) FriendListItems don't
                // necessarily contain the correct online state...
                Dictionary<UUID, FriendRegionInfo> friendRegions = m_initialScene.GetFriendRegionInfos(friendIDsToLookup);
                m_log.DebugFormat("[FRIEND]: Found {0} regionInfos for {1} friends of {2}",
                                  friendRegions.Count, friendIDsToLookup.Count, client.Name);

                // argument for SendAgentOn/Offline; we shouldn't generate that repeatedly within loops.
                UUID[] agentArr = new UUID[] { client.AgentId };

                // first, send to friend presence state to me, if I'm online...
                if (iAmOnline)
                {
                    for (int i = friendIDsToReceiveFromOffline.Count - 1; i >= 0; --i)
                    {
                        UUID uuid = friendIDsToReceiveFromOffline[i];
                        FriendRegionInfo info;
                        if (friendRegions.TryGetValue(uuid, out info) && info != null && info.isOnline)
                        {
                            friendIDsToReceiveFromOffline.RemoveAt(i);
                            friendIDsToReceiveFromOnline.Add(uuid);
                        }
                    }
                    m_log.DebugFormat("[FRIEND]: Sending {0} offline and {1} online friends to {2}",
                                      friendIDsToReceiveFromOffline.Count, friendIDsToReceiveFromOnline.Count, client.Name);
                    if (friendIDsToReceiveFromOffline.Count > 0) client.SendAgentOffline(friendIDsToReceiveFromOffline.ToArray());
                    if (friendIDsToReceiveFromOnline.Count > 0) client.SendAgentOnline(friendIDsToReceiveFromOnline.ToArray());

                    // clear them for a possible second iteration; we don't have to repeat this
                    friendIDsToReceiveFromOffline.Clear();
                    friendIDsToReceiveFromOnline.Clear();
                }

                // now, send my presence state to my friends
                for (int i = friendIDsToSendTo.Count - 1; i >= 0; --i)
                {
                    UUID uuid = friendIDsToSendTo[i];
                    FriendRegionInfo info;
                    if (friendRegions.TryGetValue(uuid, out info) && info != null && info.isOnline)
                    {
                        // any client is good enough, root or child...
                        ScenePresence agent = GetAnyPresenceFromAgentID(uuid);
                        if (agent != null)
                        {
                            m_log.DebugFormat("[FRIEND]: Found local agent {0}", agent.Name);

                            // friend is online and on this server...
                            if (iAmOnline) agent.ControllingClient.SendAgentOnline(agentArr);
                            else agent.ControllingClient.SendAgentOffline(agentArr);

                            // done, remove it
                            friendIDsToSendTo.RemoveAt(i);
                        }
                    }
                    else
                    {
                        m_log.DebugFormat("[FRIEND]: Friend {0} ({1}) is offline; not sending.", uuid, i);

                        // friend is offline => no need to try sending
                        friendIDsToSendTo.RemoveAt(i);
                    }
                }

                m_log.DebugFormat("[FRIEND]: Have {0} friends to contact via inter-region comms.", friendIDsToSendTo.Count);

                // we now have all the friends left that are online (we think), but not on this region-server
                if (friendIDsToSendTo.Count > 0)
                {
                    // sort them into regions
                    Dictionary<ulong, List<UUID>> friendsInRegion = new Dictionary<ulong,List<UUID>>();
                    foreach (UUID uuid in friendIDsToSendTo)
                    {
                        ulong handle = friendRegions[uuid].regionHandle; // this can't fail as we filtered above already
                        List<UUID> friends;
                        if (!friendsInRegion.TryGetValue(handle, out friends))
                        {
                            friends = new List<UUID>();
                            friendsInRegion[handle] = friends;
                        }
                        friends.Add(uuid);
                    }
                    m_log.DebugFormat("[FRIEND]: Found {0} regions to send to.", friendRegions.Count);

                    // clear uuids list and collect missed friends in it for the next retry
                    friendIDsToSendTo.Clear();

                    // send bulk updates to the region
                    foreach (KeyValuePair<ulong, List<UUID>> pair in friendsInRegion)
                    {
                        m_log.DebugFormat("[FRIEND]: Inform {0} friends in region {1} that user {2} is {3}line",
                                          pair.Value.Count, pair.Key, client.Name, iAmOnline ? "on" : "off");

                        friendIDsToSendTo.AddRange(m_initialScene.InformFriendsInOtherRegion(client.AgentId, pair.Key, pair.Value, iAmOnline));
                    }
                }
                // now we have in friendIDsToSendTo only the agents left that TPed away while we tried to contact them.
                // In most cases, it will be empty, and it won't loop here. But sometimes, we have to work harder and try again...
            }
            while (++retry < 3 && friendIDsToSendTo.Count > 0);
        }

        private void OnEconomyDataRequest(UUID agentID)
        {
            // KLUDGE: This is the only way I found to get a message (only) after login was completed and the
            // client is connected enough to receive UDP packets).
            // This packet seems to be sent only once, just after connection was established to the first
            // region after login.
            // We use it here to trigger a presence update; the old update-on-login was never be heard by
            // the freshly logged in viewer, as it wasn't connected to the region at that time.
            // TODO: Feel free to replace this by a better solution if you find one.

            // get the agent. This should work every time, as we just got a packet from it
            //ScenePresence agent = GetRootPresenceFromAgentID(agentID);
            // KLUDGE 2: As this is sent quite early, the avatar isn't here as root agent yet. So, we have to cheat a bit
            ScenePresence agent = GetAnyPresenceFromAgentID(agentID);

            // just to be paranoid...
            if (agent == null)
            {
                m_log.ErrorFormat("[FRIEND]: Got a packet from agent {0} who can't be found anymore!?", agentID);
                return;
            }

            List<FriendListItem> fl;
            lock (m_friendLists)
            {
                fl = (List<FriendListItem>)m_friendLists.Get(agent.ControllingClient.AgentId,
                                                             m_initialScene.GetFriendList);
            }

            // tell everyone that we are online
            SendPresenceState(agent.ControllingClient, fl, true);
        }

        private void OnLogout(IClientAPI remoteClient)
        {
            m_log.ErrorFormat("[FRIEND]: Client {0} logged out", remoteClient.Name);

            List<FriendListItem> fl;
            lock (m_friendLists)
            {
                fl = (List<FriendListItem>)m_friendLists.Get(remoteClient.AgentId,
                                                             m_initialScene.GetFriendList);
            }

            // tell everyone that we are offline
            SendPresenceState(remoteClient, fl, false);
        }

        private void outPending()
        {
            m_log.DebugFormat("[FRIEND]: got {0} requests pending", m_pendingFriendRequests.Count);
            foreach (KeyValuePair<UUID, Transaction> pair in m_pendingFriendRequests)
            {
                m_log.DebugFormat("[FRIEND]:   tid={0}, agent={1}, name={2}, count={3}",
                                  pair.Key, pair.Value.agentID, pair.Value.agentName, pair.Value.count);
            }
        }
    }

    #endregion
}
