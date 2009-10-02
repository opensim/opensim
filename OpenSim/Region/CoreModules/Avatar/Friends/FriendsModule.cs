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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.Avatar.Friends
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

    public class FriendsModule : IRegionModule, IFriendsModule
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

        private Dictionary<UUID, UUID> m_pendingCallingcardRequests = new Dictionary<UUID,UUID>();

        private Scene m_initialScene; // saves a lookup if we don't have a specific scene
        private Dictionary<ulong, Scene> m_scenes = new Dictionary<ulong,Scene>();
        private IMessageTransferModule m_TransferModule = null;

        private IGridService m_gridServices = null;

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            lock (m_scenes)
            {
                if (m_scenes.Count == 0)
                {
                    MainServer.Instance.AddXmlRPCHandler("presence_update_bulk", processPresenceUpdateBulk);
                    MainServer.Instance.AddXmlRPCHandler("terminate_friend", processTerminateFriend);
                    m_friendLists.DefaultTTL = new TimeSpan(1, 0, 0);  // store entries for one hour max
                    m_initialScene = scene;
                }

                if (!m_scenes.ContainsKey(scene.RegionInfo.RegionHandle))
                    m_scenes[scene.RegionInfo.RegionHandle] = scene;
            }
            
            scene.RegisterModuleInterface<IFriendsModule>(this);
            
            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;
            scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
            scene.EventManager.OnMakeChildAgent += MakeChildAgent;
            scene.EventManager.OnClientClosed += ClientClosed;
        }

        public void PostInitialise()
        {
            if (m_scenes.Count > 0)
            {
                m_TransferModule = m_initialScene.RequestModuleInterface<IMessageTransferModule>();
                m_gridServices = m_initialScene.GridService;
            }
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

        #region IInterregionFriendsComms

        public List<UUID> InformFriendsInOtherRegion(UUID agentId, ulong destRegionHandle, List<UUID> friends, bool online)
        {
            List<UUID> tpdAway = new List<UUID>();

            // destRegionHandle is a region on another server
            uint x = 0, y = 0;
            Utils.LongToUInts(destRegionHandle, out x, out y);
            GridRegion info = m_gridServices.GetRegionByPosition(m_initialScene.RegionInfo.ScopeID, (int)x, (int)y);
            if (info != null)
            {
                string httpServer = "http://" + info.ExternalEndPoint.Address + ":" + info.HttpPort + "/presence_update_bulk";

                Hashtable reqParams = new Hashtable();
                reqParams["agentID"] = agentId.ToString();
                reqParams["agentOnline"] = online;
                int count = 0;
                foreach (UUID uuid in friends)
                {
                    reqParams["friendID_" + count++] = uuid.ToString();
                }
                reqParams["friendCount"] = count;

                IList parameters = new ArrayList();
                parameters.Add(reqParams);
                try
                {
                    XmlRpcRequest request = new XmlRpcRequest("presence_update_bulk", parameters);
                    XmlRpcResponse response = request.Send(httpServer, 5000);
                    Hashtable respData = (Hashtable)response.Value;

                    count = (int)respData["friendCount"];
                    for (int i = 0; i < count; ++i)
                    {
                        UUID uuid;
                        if (UUID.TryParse((string)respData["friendID_" + i], out uuid)) tpdAway.Add(uuid);
                    }
                }
                catch (WebException e)
                {
                    // Ignore connect failures, simulators come and go
                    //
                    if (!e.Message.Contains("ConnectFailure"))
                    {
                        m_log.Error("[OGS1 GRID SERVICES]: InformFriendsInOtherRegion XMLRPC failure: ", e);
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[OGS1 GRID SERVICES]: InformFriendsInOtherRegion XMLRPC failure: ", e);
                }
            }
            else m_log.WarnFormat("[OGS1 GRID SERVICES]: Couldn't find region {0}???", destRegionHandle);

            return tpdAway;
        }

        public bool TriggerTerminateFriend(ulong destRegionHandle, UUID agentID, UUID exFriendID)
        {
            // destRegionHandle is a region on another server
            uint x = 0, y = 0;
            Utils.LongToUInts(destRegionHandle, out x, out y);
            GridRegion info = m_gridServices.GetRegionByPosition(m_initialScene.RegionInfo.ScopeID, (int)x, (int)y);
            if (info == null)
            {
                m_log.WarnFormat("[OGS1 GRID SERVICES]: Couldn't find region {0}", destRegionHandle);
                return false; // region not found???
            }

            string httpServer = "http://" + info.ExternalEndPoint.Address + ":" + info.HttpPort + "/presence_update_bulk";

            Hashtable reqParams = new Hashtable();
            reqParams["agentID"] = agentID.ToString();
            reqParams["friendID"] = exFriendID.ToString();

            IList parameters = new ArrayList();
            parameters.Add(reqParams);
            try
            {
                XmlRpcRequest request = new XmlRpcRequest("terminate_friend", parameters);
                XmlRpcResponse response = request.Send(httpServer, 5000);
                Hashtable respData = (Hashtable)response.Value;

                return (bool)respData["success"];
            }
            catch (Exception e)
            {
                m_log.Error("[OGS1 GRID SERVICES]: InformFriendsInOtherRegion XMLRPC failure: ", e);
                return false;
            }
        }

        #endregion

        #region Incoming XMLRPC messages
        /// <summary>
        /// Receive presence information changes about clients in other regions.
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        public XmlRpcResponse processPresenceUpdateBulk(XmlRpcRequest req, IPEndPoint remoteClient)
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

        public XmlRpcResponse processTerminateFriend(XmlRpcRequest req, IPEndPoint remoteClient)
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

        #endregion

        #region Scene events

        private void OnNewClient(IClientAPI client)
        {
            // All friends establishment protocol goes over instant message
            // There's no way to send a message from the sim
            // to a user to 'add a friend' without causing dialog box spam

            // Subscribe to instant messages
            client.OnInstantMessage += OnInstantMessage;

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

        private void ClientClosed(UUID AgentId, Scene scene)
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
                }
            }
        }

        private void AvatarEnteringParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            lock (m_rootAgents)
            {
                m_rootAgents[avatar.UUID] = avatar.RegionHandle;
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
//                        m_log.Debug("[FRIEND]: Removing " + avatar.Firstname + " " + avatar.Lastname + " as a root agent");
                    }
                }
            }
        }
        #endregion

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
        
        public void OfferFriendship(UUID fromUserId, IClientAPI toUserClient, string offerMessage)
        {
            CachedUserInfo userInfo = m_initialScene.CommsManager.UserProfileCacheService.GetUserDetails(fromUserId);
                
            if (userInfo != null)
            {
                GridInstantMessage msg = new GridInstantMessage(
                    toUserClient.Scene, fromUserId, userInfo.UserProfile.Name, toUserClient.AgentId,
                    (byte)InstantMessageDialog.FriendshipOffered, offerMessage, false, Vector3.Zero); 
            
                FriendshipOffered(msg);
            }
            else
            {
                m_log.ErrorFormat("[FRIENDS]: No user found for id {0} in OfferFriendship()", fromUserId);
            }
        }

        #region FriendRequestHandling

        private void OnInstantMessage(IClientAPI client, GridInstantMessage im)
        {
            // Friend Requests go by Instant Message..    using the dialog param
            // https://wiki.secondlife.com/wiki/ImprovedInstantMessage

            if (im.dialog == (byte)InstantMessageDialog.FriendshipOffered) // 38
            {
                // fromAgentName is the *destination* name (the friend we offer friendship to)
                ScenePresence initiator = GetAnyPresenceFromAgentID(new UUID(im.fromAgentID));
                im.fromAgentName = initiator != null ? initiator.Name : "(hippo)";
                
                FriendshipOffered(im);
            }
            else if (im.dialog == (byte)InstantMessageDialog.FriendshipAccepted) // 39
            {
                FriendshipAccepted(client, im);
            }
            else if (im.dialog == (byte)InstantMessageDialog.FriendshipDeclined) // 40
            {
                FriendshipDeclined(client, im);
            }
        }
        
        /// <summary>
        /// Invoked when a user offers a friendship.
        /// </summary>
        /// 
        /// <param name="im"></param>
        /// <param name="client"></param>
        private void FriendshipOffered(GridInstantMessage im)
        {
            // this is triggered by the initiating agent:
            // A local agent offers friendship to some possibly remote friend.
            // A IM is triggered, processed here and sent to the friend (possibly in a remote region).

            m_log.DebugFormat("[FRIEND]: Offer(38) - From: {0}, FromName: {1} To: {2}, Session: {3}, Message: {4}, Offline {5}",
                       im.fromAgentID, im.fromAgentName, im.toAgentID, im.imSessionID, im.message, im.offline);

            // 1.20 protocol sends an UUID in the message field, instead of the friendship offer text.
            // For interoperability, we have to clear that
            if (Util.isUUID(im.message)) im.message = "";

            // be sneeky and use the initiator-UUID as transactionID. This means we can be stateless.
            // we have to look up the agent name on friendship-approval, though.
            im.imSessionID = im.fromAgentID;

            if (m_TransferModule != null)
            {
                // Send it to whoever is the destination.
                // If new friend is local, it will send an IM to the viewer.
                // If new friend is remote, it will cause a OnGridInstantMessage on the remote server
                m_TransferModule.SendInstantMessage(
                    im,
                    delegate(bool success) 
                    {
                        m_log.DebugFormat("[FRIEND]: sending IM success = {0}", success);
                    }
                );
            }
        }
        
        /// <summary>
        /// Invoked when a user accepts a friendship offer.
        /// </summary>
        /// <param name="im"></param>
        /// <param name="client"></param>
        private void FriendshipAccepted(IClientAPI client, GridInstantMessage im)
        {
            m_log.DebugFormat("[FRIEND]: 39 - from client {0}, agent {2} {3}, imsession {4} to {5}: {6} (dialog {7})",
              client.AgentId, im.fromAgentID, im.fromAgentName, im.imSessionID, im.toAgentID, im.message, im.dialog);
        }
        
        /// <summary>
        /// Invoked when a user declines a friendship offer.
        /// </summary>
        /// May not currently be used - see OnDenyFriendRequest() instead
        /// <param name="im"></param>
        /// <param name="client"></param>
        private void FriendshipDeclined(IClientAPI client, GridInstantMessage im)
        {
            UUID fromAgentID = new UUID(im.fromAgentID);
            UUID toAgentID = new UUID(im.toAgentID);
            
            // declining the friendship offer causes a type 40 IM being sent to the (possibly remote) initiator
            // toAgentID is initiator, fromAgentID declined friendship
            m_log.DebugFormat("[FRIEND]: 40 - from client {0}, agent {1} {2}, imsession {3} to {4}: {5} (dialog {6})",
              client != null ? client.AgentId.ToString() : "<null>",
              fromAgentID, im.fromAgentName, im.imSessionID, im.toAgentID, im.message, im.dialog);

            // Send the decline to whoever is the destination.
            GridInstantMessage msg 
                = new GridInstantMessage(
                    client.Scene, fromAgentID, client.Name, toAgentID,
                    im.dialog, im.message, im.offline != 0, im.Position);
            
            // If new friend is local, it will send an IM to the viewer.
            // If new friend is remote, it will cause a OnGridInstantMessage on the remote server
            m_TransferModule.SendInstantMessage(msg,
                delegate(bool success) {
                    m_log.DebugFormat("[FRIEND]: sending IM success = {0}", success);
                }
            );
        }

        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            // This event won't be raised unless we have that agent,
            // so we can depend on the above not trying to send
            // via grid again
            //m_log.DebugFormat("[FRIEND]: Got GridIM from {0}, to {1}, imSession {2}, message {3}, dialog {4}",
            //                  msg.fromAgentID, msg.toAgentID, msg.imSessionID, msg.message, msg.dialog);
            
            if (msg.dialog == (byte)InstantMessageDialog.FriendshipOffered ||
                msg.dialog == (byte)InstantMessageDialog.FriendshipAccepted ||
                msg.dialog == (byte)InstantMessageDialog.FriendshipDeclined)
            {
                // this should succeed as we *know* the root agent is here.
                m_TransferModule.SendInstantMessage(msg,
                    delegate(bool success) {
                        //m_log.DebugFormat("[FRIEND]: sending IM success = {0}", success);
                    }
                );
            }

            if (msg.dialog == (byte)InstantMessageDialog.FriendshipAccepted)
            {
                // for accept friendship, we have to do a bit more
                ApproveFriendship(new UUID(msg.fromAgentID), new UUID(msg.toAgentID), msg.fromAgentName);
            }
        }

        private void ApproveFriendship(UUID fromAgentID, UUID toAgentID, string fromName)
        {
            m_log.DebugFormat("[FRIEND]: Approve friendship from {0} (ID: {1}) to {2}",
                              fromAgentID, fromName, toAgentID);

            // a new friend was added in the initiator's and friend's data, so the cache entries are wrong now.
            lock (m_friendLists)
            {
                m_friendLists.Invalidate(fromAgentID.ToString());
                m_friendLists.Invalidate(toAgentID.ToString());
            }

            // now send presence update and add a calling card for the new friend

            ScenePresence initiator = GetAnyPresenceFromAgentID(toAgentID);
            if (initiator == null)
            {
                // quite wrong. Shouldn't happen.
                m_log.WarnFormat("[FRIEND]: Coudn't find initiator of friend request {0}", toAgentID);
                return;
            }

            m_log.DebugFormat("[FRIEND]: Tell {0} that {1} is online",
                              initiator.Name, fromName);
            // tell initiator that friend is online
            initiator.ControllingClient.SendAgentOnline(new UUID[] { fromAgentID });

            // find the folder for the friend...
            //InventoryFolderImpl folder =
            //    initiator.Scene.CommsManager.UserProfileCacheService.GetUserDetails(toAgentID).FindFolderForType((int)InventoryType.CallingCard);
            IInventoryService invService = initiator.Scene.InventoryService;
            InventoryFolderBase folder = invService.GetFolderForType(toAgentID, AssetType.CallingCard);
            if (folder != null)
            {
                // ... and add the calling card
                CreateCallingCard(initiator.ControllingClient, fromAgentID, folder.ID, fromName);
            }
        }

        private void OnApproveFriendRequest(IClientAPI client, UUID agentID, UUID friendID, List<UUID> callingCardFolders)
        {
            m_log.DebugFormat("[FRIEND]: Got approve friendship from {0} {1}, agentID {2}, tid {3}",
                              client.Name, client.AgentId, agentID, friendID);

            // store the new friend persistently for both avatars
            m_initialScene.StoreAddFriendship(friendID, agentID, (uint) FriendRights.CanSeeOnline);

            // The cache entries aren't valid anymore either, as we just added a friend to both sides.
            lock (m_friendLists)
            {
                m_friendLists.Invalidate(agentID.ToString());
                m_friendLists.Invalidate(friendID.ToString());
            }

            // if it's a local friend, we don't have to do the lookup
            ScenePresence friendPresence = GetAnyPresenceFromAgentID(friendID);

            if (friendPresence != null)
            {
                m_log.Debug("[FRIEND]: Local agent detected.");

                // create calling card
                CreateCallingCard(client, friendID, callingCardFolders[0], friendPresence.Name);

                // local message means OnGridInstantMessage won't be triggered, so do the work here.
                friendPresence.ControllingClient.SendInstantMessage(
                        new GridInstantMessage(client.Scene, agentID,
                        client.Name, friendID,
                        (byte)InstantMessageDialog.FriendshipAccepted,
                        agentID.ToString(), false, Vector3.Zero));
                ApproveFriendship(agentID, friendID, client.Name);
            }
            else
            {
                m_log.Debug("[FRIEND]: Remote agent detected.");

                // fetch the friend's name for the calling card.
                CachedUserInfo info = m_initialScene.CommsManager.UserProfileCacheService.GetUserDetails(friendID);

                // create calling card
                CreateCallingCard(client, friendID, callingCardFolders[0],
                                  info.UserProfile.FirstName + " " + info.UserProfile.SurName);

                // Compose (remote) response to friend.
                GridInstantMessage msg = new GridInstantMessage(client.Scene, agentID, client.Name, friendID,
                                                                (byte)InstantMessageDialog.FriendshipAccepted,
                                                                agentID.ToString(), false, Vector3.Zero);
                if (m_TransferModule != null)
                {
                    m_TransferModule.SendInstantMessage(msg,
                        delegate(bool success) {
                            m_log.DebugFormat("[FRIEND]: sending IM success = {0}", success);
                        }
                    );
                }
            }

            // tell client that new friend is online
            client.SendAgentOnline(new UUID[] { friendID });
        }

        private void OnDenyFriendRequest(IClientAPI client, UUID agentID, UUID friendID, List<UUID> callingCardFolders)
        {
            m_log.DebugFormat("[FRIEND]: Got deny friendship from {0} {1}, agentID {2}, tid {3}",
                              client.Name, client.AgentId, agentID, friendID);

            // Compose response to other agent.
            GridInstantMessage msg = new GridInstantMessage(client.Scene, agentID, client.Name, friendID,
                                                            (byte)InstantMessageDialog.FriendshipDeclined,
                                                            agentID.ToString(), false, Vector3.Zero);
            // send decline to initiator
            if (m_TransferModule != null)
            {
                m_TransferModule.SendInstantMessage(msg,
                    delegate(bool success) {
                        m_log.DebugFormat("[FRIEND]: sending IM success = {0}", success);
                    }
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
                    
                    if (null == data)
                        break;
                    
                    if (!data.AgentOnline)
                    {
                        m_log.DebugFormat("[FRIEND]: {0} is offline, so not sending TerminateFriend", exfriendID);
                        break; // if ex-friend isn't online, we don't need to send
                    }

                    m_log.DebugFormat("[FRIEND]: Sending remote terminate friend {0} to agent {1}@{2}",
                                      agentID, exfriendID, data.Handle);

                    // try to send to foreign region, retry if it fails (friend TPed away, for example)
                    if (TriggerTerminateFriend(data.Handle, exfriendID, agentID)) break;
                }
            }

            // clean up cache: FriendList is wrong now...
            lock (m_friendLists)
            {
                m_friendLists.Invalidate(agentID.ToString());
                m_friendLists.Invalidate(exfriendID.ToString());
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
            item.CreatorId = creator.ToString();
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
            m_log.DebugFormat("[CALLING CARD]: User {0} (ID:{1}) declined card, tid {2}",
                              client.Name, client.AgentId, transactionID);
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

        /// <summary>
        /// Send presence information about a client to other clients in both this region and others.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="friendList"></param>
        /// <param name="iAmOnline"></param>
        private void SendPresenceState(IClientAPI client, List<FriendListItem> friendList, bool iAmOnline)
        {
            //m_log.DebugFormat("[FRIEND]: {0} logged {1}; sending presence updates", client.Name, iAmOnline ? "in" : "out");

            if (friendList == null || friendList.Count == 0)
            {
                //m_log.DebugFormat("[FRIEND]: {0} doesn't have friends.", client.Name);
                return; // nothing we can do if she doesn't have friends...
            }

            // collect sets of friendIDs; to send to (online and offline), and to receive from
            // TODO: If we ever switch to .NET >= 3, replace those Lists with HashSets.
            // I can't believe that we have Dictionaries, but no Sets, considering Java introduced them years ago...
            List<UUID> friendIDsToSendTo = new List<UUID>();
            List<UUID> candidateFriendIDsToReceive = new List<UUID>();
            
            foreach (FriendListItem item in friendList)
            {
                if (((item.FriendListOwnerPerms | item.FriendPerms) & (uint)FriendRights.CanSeeOnline) != 0)
                {
                    // friend is allowed to see my presence => add
                    if ((item.FriendListOwnerPerms & (uint)FriendRights.CanSeeOnline) != 0) 
                        friendIDsToSendTo.Add(item.Friend);

                    if ((item.FriendPerms & (uint)FriendRights.CanSeeOnline) != 0) 
                        candidateFriendIDsToReceive.Add(item.Friend);
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
                foreach (UUID uuid in candidateFriendIDsToReceive)
                {
                    if (!friendIDsToLookup.Contains(uuid)) friendIDsToLookup.Add(uuid);
                }

                m_log.DebugFormat(
                    "[FRIEND]: {0} to lookup, {1} to send to, {2} candidates to receive from for agent {3}",
                    friendIDsToLookup.Count, friendIDsToSendTo.Count, candidateFriendIDsToReceive.Count, client.Name);

                // we have to fetch FriendRegionInfos, as the (cached) FriendListItems don't
                // necessarily contain the correct online state...
                Dictionary<UUID, FriendRegionInfo> friendRegions = m_initialScene.GetFriendRegionInfos(friendIDsToLookup);
                m_log.DebugFormat(
                    "[FRIEND]: Found {0} regionInfos for {1} friends of {2}",
                    friendRegions.Count, friendIDsToLookup.Count, client.Name);

                // argument for SendAgentOn/Offline; we shouldn't generate that repeatedly within loops.
                UUID[] agentArr = new UUID[] { client.AgentId };

                // first, send to friend presence state to me, if I'm online...
                if (iAmOnline)
                {
                    List<UUID> friendIDsToReceive = new List<UUID>();
                    
                    for (int i = candidateFriendIDsToReceive.Count - 1; i >= 0; --i)
                    {
                        UUID uuid = candidateFriendIDsToReceive[i];
                        FriendRegionInfo info;
                        if (friendRegions.TryGetValue(uuid, out info) && info != null && info.isOnline)
                        {
                            friendIDsToReceive.Add(uuid);
                        }
                    }
                    
                    m_log.DebugFormat(
                        "[FRIEND]: Sending {0} online friends to {1}", friendIDsToReceive.Count, client.Name);
                    
                    if (friendIDsToReceive.Count > 0) 
                        client.SendAgentOnline(friendIDsToReceive.ToArray());

                    // clear them for a possible second iteration; we don't have to repeat this
                    candidateFriendIDsToReceive.Clear();
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
                            //m_log.DebugFormat("[FRIEND]: Found local agent {0}", agent.Name);

                            // friend is online and on this server...
                            if (iAmOnline) agent.ControllingClient.SendAgentOnline(agentArr);
                            else agent.ControllingClient.SendAgentOffline(agentArr);

                            // done, remove it
                            friendIDsToSendTo.RemoveAt(i);
                        }
                    }
                    else
                    {
                        //m_log.DebugFormat("[FRIEND]: Friend {0} ({1}) is offline; not sending.", uuid, i);

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
                        //m_log.DebugFormat("[FRIEND]: Inform {0} friends in region {1} that user {2} is {3}line",
                        //                  pair.Value.Count, pair.Key, client.Name, iAmOnline ? "on" : "off");

                        friendIDsToSendTo.AddRange(InformFriendsInOtherRegion(client.AgentId, pair.Key, pair.Value, iAmOnline));
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
                fl = (List<FriendListItem>)m_friendLists.Get(agent.ControllingClient.AgentId.ToString(),
                                                             m_initialScene.GetFriendList);
            }

            // tell everyone that we are online
            SendPresenceState(agent.ControllingClient, fl, true);
        }

        private void OnLogout(IClientAPI remoteClient)
        {
            List<FriendListItem> fl;
            lock (m_friendLists)
            {
                fl = (List<FriendListItem>)m_friendLists.Get(remoteClient.AgentId.ToString(),
                                                             m_initialScene.GetFriendList);
            }

            // tell everyone that we are offline
            SendPresenceState(remoteClient, fl, false);
        }
    }

    #endregion
}
