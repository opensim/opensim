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
    public class FriendsModule : ISharedRegionModule, IFriendsModule
    {
        public void Initialise(IConfigSource config)
        {
        }

        public void PostInitialise()
        {
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
            
            client.OnGrantUserRights += GrantUserFriendRights;
            client.OnTrackAgentEvent += FindAgent;
            client.OnFindAgentEvent += FindAgent;

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
>>>>>>> master:OpenSim/Region/CoreModules/Avatar/Friends/FriendsModule.cs
        }

        public void AddRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "FriendsModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void OfferFriendship(UUID fromUserId, IClientAPI toUserClient,
                string offerMessage)
        {
        }
        public void FindAgent(IClientAPI remoteClient, UUID hunter, UUID target)
        {
        	List<FriendListItem> friendList = GetUserFriends(hunter);
        	foreach (FriendListItem item in friendList)
        	{
        		if(item.onlinestatus == true)
        		{
        			if(item.Friend == target && (item.FriendPerms & (uint)FriendRights.CanSeeOnMap) != 0)
        			{
        				ScenePresence SPTarget = ((Scene)remoteClient.Scene).GetScenePresence(target);
        				string regionname =  SPTarget.Scene.RegionInfo.RegionName;
        				remoteClient.SendScriptTeleportRequest("FindAgent", regionname,new Vector3(SPTarget.AbsolutePosition),new Vector3(SPTarget.Lookat));
        			}
        		}
        		else
        		{
        			remoteClient.SendAgentAlertMessage("The agent you are looking for is not online.", false);
        		}
        	}
        }

        public List<FriendListItem> GetUserFriends(UUID agentID)
        {
            return null;
        }
    }
}
