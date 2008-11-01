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
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Avatar.Friends
{
    public class FriendsModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<UUID, List<FriendListItem>> FriendLists = new Dictionary<UUID, List<FriendListItem>>();
        private Dictionary<UUID, UUID> m_pendingFriendRequests = new Dictionary<UUID, UUID>();
        private Dictionary<UUID, ulong> m_rootAgents = new Dictionary<UUID, ulong>();
        private Dictionary<UUID, List<StoredFriendListUpdate>> StoredFriendListUpdates = new Dictionary<UUID, List<StoredFriendListUpdate>>();

        private Dictionary<UUID, UUID> m_pendingCallingcardRequests = new Dictionary<UUID,UUID>();

        private List<Scene> m_scene = new List<Scene>();

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            lock (m_scene)
            {
                if (m_scene.Count == 0)
                {
                    scene.AddXmlRPCHandler("presence_update", processPresenceUpdate);
                }

                if (!m_scene.Contains(scene))
                    m_scene.Add(scene);
            }
            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnGridInstantMessage += OnGridInstantMessage;
            scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
            scene.EventManager.OnMakeChildAgent += MakeChildAgent;
            scene.EventManager.OnClientClosed += ClientLoggedOut;
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

        public XmlRpcResponse processPresenceUpdate(XmlRpcRequest req)
        {
            //m_log.Info("[FRIENDS]: Got Notification about a user! OMG");
            Hashtable requestData = (Hashtable)req.Params[0];

            if (requestData.ContainsKey("agent_id") && requestData.ContainsKey("notify_id") && requestData.ContainsKey("status"))
            {
                UUID notifyAgentId = UUID.Zero;
                UUID notifyAboutAgentId = UUID.Zero;
                bool notifyOnlineStatus = false;

                if ((string)requestData["status"] == "TRUE")
                    notifyOnlineStatus = true;

                UUID.TryParse((string)requestData["notify_id"], out notifyAgentId);

                UUID.TryParse((string)requestData["agent_id"], out notifyAboutAgentId);
                m_log.InfoFormat("[PRESENCE]: Got presence update for {0}, and we're telling {1}, with a status {2}", notifyAboutAgentId.ToString(), notifyAgentId.ToString(), notifyOnlineStatus.ToString());
                ScenePresence avatar = GetRootPresenceFromAgentID(notifyAgentId);
                if (avatar != null)
                {
                    if (avatar.IsChildAgent)
                    {
                        StoredFriendListUpdate sob = new StoredFriendListUpdate();
                        sob.OnlineYN = notifyOnlineStatus;
                        sob.storedAbout = notifyAboutAgentId;
                        sob.storedFor = notifyAgentId;
                        lock (StoredFriendListUpdates)
                        {
                            if (StoredFriendListUpdates.ContainsKey(notifyAgentId))
                            {
                                StoredFriendListUpdates[notifyAgentId].Add(sob);
                            }
                            else
                            {
                                List<StoredFriendListUpdate> newitem = new List<StoredFriendListUpdate>();
                                newitem.Add(sob);
                                StoredFriendListUpdates.Add(notifyAgentId, newitem);
                            }
                        }
                    }
                    else
                    {
                        if (notifyOnlineStatus)
                            doFriendListUpdateOnline(notifyAboutAgentId);
                        else
                            ClientLoggedOut(notifyAboutAgentId);
                    }
                }
                else
                {
                    StoredFriendListUpdate sob = new StoredFriendListUpdate();
                    sob.OnlineYN = notifyOnlineStatus;
                    sob.storedAbout = notifyAboutAgentId;
                    sob.storedFor = notifyAgentId;
                    lock (StoredFriendListUpdates)
                    {
                        if (StoredFriendListUpdates.ContainsKey(notifyAgentId))
                        {
                            StoredFriendListUpdates[notifyAgentId].Add(sob);
                        }
                        else
                        {
                            List<StoredFriendListUpdate> newitem = new List<StoredFriendListUpdate>();
                            newitem.Add(sob);
                            StoredFriendListUpdates.Add(notifyAgentId, newitem);
                        }
                    }
                }

            }
            else
            {
                m_log.Warn("[PRESENCE]: Malformed XMLRPC Presence request");
            }
            return new XmlRpcResponse();
        }

        private void OnNewClient(IClientAPI client)
        {
            // All friends establishment protocol goes over instant message
            // There's no way to send a message from the sim
            // to a user to 'add a friend' without causing dialog box spam
            //
            // The base set of friends are added when the user signs on in their XMLRPC response
            // Generated by LoginService.  The friends are retreived from the database by the UserManager

            // Subscribe to instant messages

            client.OnInstantMessage += OnInstantMessage;
            client.OnApproveFriendRequest += OnApprovedFriendRequest;
            client.OnDenyFriendRequest += OnDenyFriendRequest;
            client.OnTerminateFriendship += OnTerminateFriendship;
            client.OnOfferCallingCard += OnOfferCallingCard;
            client.OnAcceptCallingCard += OnAcceptCallingCard;
            client.OnDeclineCallingCard += OnDeclineCallingCard;

            doFriendListUpdateOnline(client.AgentId);

        }

        private void doFriendListUpdateOnline(UUID AgentId)
        {
            List<FriendListItem> fl = new List<FriendListItem>();

            //bool addFLback = false;

            lock (FriendLists)
            {
                if (FriendLists.ContainsKey(AgentId))
                {
                    fl = FriendLists[AgentId];
                }
                else
                {
                    fl = m_scene[0].GetFriendList(AgentId);

                    //lock (FriendLists)
                    //{
                    if (!FriendLists.ContainsKey(AgentId))
                        FriendLists.Add(AgentId, fl);
                    //}
                }
            }

            List<UUID> UpdateUsers = new List<UUID>();

            foreach (FriendListItem f in fl)
            {
                if (m_rootAgents.ContainsKey(f.Friend))
                {
                    if (f.onlinestatus == false)
                    {
                        UpdateUsers.Add(f.Friend);
                        f.onlinestatus = true;
                    }
                }
            }
            foreach (UUID user in UpdateUsers)
            {
                ScenePresence av = GetRootPresenceFromAgentID(user);
                if (av != null)
                {
                    List<FriendListItem> usrfl = new List<FriendListItem>();

                    lock (FriendLists)
                    {
                        usrfl = FriendLists[user];
                    }

                    lock (usrfl)
                    {
                        foreach (FriendListItem fli in usrfl)
                        {
                            if (fli.Friend == AgentId)
                            {
                                fli.onlinestatus = true;
                                UUID[] Agents = new UUID[1];
                                Agents[0] = AgentId;
                                av.ControllingClient.SendAgentOnline(Agents);

                            }
                        }
                    }
                }
            }

            if (UpdateUsers.Count > 0)
            {
                ScenePresence avatar = GetRootPresenceFromAgentID(AgentId);
                if (avatar != null)
                {
                    avatar.ControllingClient.SendAgentOnline(UpdateUsers.ToArray());
                }

            }
        }

        private void ClientLoggedOut(UUID AgentId)
        {
            lock (m_rootAgents)
            {
                if (m_rootAgents.ContainsKey(AgentId))
                {
                    m_rootAgents.Remove(AgentId);
                    m_log.Info("[FRIEND]: Removing " + AgentId + ". Agent logged out.");
                }
            }
            List<FriendListItem> lfli = new List<FriendListItem>();
            lock (FriendLists)
            {
                if (FriendLists.ContainsKey(AgentId))
                {
                    lfli = FriendLists[AgentId];
                }
            }
            List<UUID> updateUsers = new List<UUID>();
            foreach (FriendListItem fli in lfli)
            {
                if (fli.onlinestatus == true)
                {
                    updateUsers.Add(fli.Friend);
                }
            }
            lock (updateUsers)
            {
                for (int i = 0; i < updateUsers.Count; i++)
                {
                    List<FriendListItem> flfli = new List<FriendListItem>();
                    try
                    {
                        lock (FriendLists)
                        {
                            if (FriendLists.ContainsKey(updateUsers[i]))
                                flfli = FriendLists[updateUsers[i]];
                        }
                    }
                    catch (IndexOutOfRangeException)
                    {
                        // Ignore the index out of range exception.
                        // This causes friend lists to get out of sync slightly..  however
                        // prevents a sim crash.
                        m_log.Info("[FRIEND]: Unable to enumerate last friendlist user.  User logged off");
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        // Ignore the index out of range exception.
                        // This causes friend lists to get out of sync slightly..  however
                        // prevents a sim crash.
                        m_log.Info("[FRIEND]: Unable to enumerate last friendlist user.  User logged off");
                    }

                    for (int j = 0; j < flfli.Count; j++)
                    {
                        try
                        {
                            if (flfli[i].Friend == AgentId)
                            {
                                flfli[i].onlinestatus = false;
                            }
                        }

                        catch (IndexOutOfRangeException)
                        {
                            // Ignore the index out of range exception.
                            // This causes friend lists to get out of sync slightly..  however
                            // prevents a sim crash.
                            m_log.Info("[FRIEND]: Unable to enumerate last friendlist user.  User logged off");
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            // Ignore the index out of range exception.
                            // This causes friend lists to get out of sync slightly..  however
                            // prevents a sim crash.
                            m_log.Info("[FRIEND]: Unable to enumerate last friendlist user.  User logged off");
                        }
                    }
                }

                for (int i = 0; i < updateUsers.Count; i++)
                {
                    ScenePresence av = GetRootPresenceFromAgentID(updateUsers[i]);
                    if (av != null)
                    {
                        UUID[] agents = new UUID[1];
                        agents[0] = AgentId;
                        av.ControllingClient.SendAgentOffline(agents);
                    }
                }
            }
            lock (FriendLists)
            {
                FriendLists.Remove(AgentId);
            }
        }

        private void AvatarEnteringParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            lock (m_rootAgents)
            {
                if (m_rootAgents.ContainsKey(avatar.UUID))
                {
                    if (avatar.RegionHandle != m_rootAgents[avatar.UUID])
                    {
                        m_rootAgents[avatar.UUID] = avatar.RegionHandle;
                        m_log.Info("[FRIEND]: Claiming " + avatar.Firstname + " " + avatar.Lastname + " in region:" + avatar.RegionHandle + ".");
                        if (avatar.JID.Length > 0)
                        {
                            // JId avatarID = new JId(avatar.JID);
                            // REST Post XMPP Stanzas!
                        }
                        // Claim User! my user!  Mine mine mine!
                    }
                }
                else
                {
                    m_rootAgents.Add(avatar.UUID, avatar.RegionHandle);
                    m_log.Info("[FRIEND]: Claiming " + avatar.Firstname + " " + avatar.Lastname + " in region:" + avatar.RegionHandle + ".");

                    List<StoredFriendListUpdate> updateme = new List<StoredFriendListUpdate>();
                    lock (StoredFriendListUpdates)
                    {
                        if (StoredFriendListUpdates.ContainsKey(avatar.UUID))
                        {
                            updateme = StoredFriendListUpdates[avatar.UUID];
                            StoredFriendListUpdates.Remove(avatar.UUID);
                        }
                    }

                    if (updateme.Count > 0)
                    {
                        foreach (StoredFriendListUpdate u in updateme)
                        {
                            if (u.OnlineYN)
                                doFriendListUpdateOnline(u.storedAbout);
                            else
                                ClientLoggedOut(u.storedAbout);
                        }
                    }
                }
            }
            //m_log.Info("[FRIEND]: " + avatar.Name + " status:" + (!avatar.IsChildAgent).ToString());
        }

        private void MakeChildAgent(ScenePresence avatar)
        {
            lock (m_rootAgents)
            {
                if (m_rootAgents.ContainsKey(avatar.UUID))
                {
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
            lock (m_scene)
            {
                ScenePresence queryagent = null;
                for (int i = 0; i < m_scene.Count; i++)
                {
                    queryagent = m_scene[i].GetScenePresence(AgentID);
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
            lock (m_scene)
            {
                ScenePresence queryagent = null;
                for (int i = 0; i < m_scene.Count; i++)
                {
                    queryagent = m_scene[i].GetScenePresence(AgentID);
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
                                      UUID fromAgentSession, UUID toAgentID,
                                      UUID imSessionID, uint timestamp, string fromAgentName,
                                      string message, byte dialog, bool fromGroup, byte offline,
                                      uint ParentEstateID, Vector3 Position, UUID RegionID,
                                      byte[] binaryBucket)
        {
            // Friend Requests go by Instant Message..    using the dialog param
            // https://wiki.secondlife.com/wiki/ImprovedInstantMessage

            // 38 == Offer friendship
            if (dialog == (byte) 38)
            {
                UUID friendTransactionID = UUID.Random();

                m_pendingFriendRequests.Add(friendTransactionID, fromAgentID);

                m_log.Info("[FRIEND]: 38 - From:" + fromAgentID.ToString() + " To: " + toAgentID.ToString() + " Session:" + imSessionID.ToString() + " Message:" +
                           message);
                GridInstantMessage msg = new GridInstantMessage();
                msg.fromAgentID = fromAgentID.Guid;
                msg.fromAgentSession = fromAgentSession.Guid;
                msg.toAgentID = toAgentID.Guid;
                msg.imSessionID = friendTransactionID.Guid; // This is the item we're mucking with here
                m_log.Info("[FRIEND]: Filling Session: " + msg.imSessionID.ToString());
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
                // We don't really care which scene we pipe it through.
                m_scene[0].TriggerGridInstantMessage(msg, InstantMessageReceiver.IMModule);
            }

            // 39 == Accept Friendship
            if (dialog == (byte) 39)
            {
                m_log.Info("[FRIEND]: 39 - From:" + fromAgentID.ToString() + " To: " + toAgentID.ToString() + " Session:" + imSessionID.ToString() + " Message:" +
                           message);
            }

            // 40 == Decline Friendship
            if (dialog == (byte) 40)
            {
                m_log.Info("[FRIEND]: 40 - From:" + fromAgentID.ToString() + " To: " + toAgentID.ToString() + " Session:" + imSessionID.ToString() + " Message:" +
                           message);
            }
        }

        private void OnApprovedFriendRequest(IClientAPI client, UUID agentID, UUID transactionID, List<UUID> callingCardFolders)
        {
            if (m_pendingFriendRequests.ContainsKey(transactionID))
            {
                // Found Pending Friend Request with that Transaction..
                Scene SceneAgentIn = m_scene[0];

                // Found Pending Friend Request with that Transaction..
                ScenePresence agentpresence = GetRootPresenceFromAgentID(agentID);
                if (agentpresence != null)
                {
                    SceneAgentIn = agentpresence.Scene;
                }

                // Compose response to other agent.
                GridInstantMessage msg = new GridInstantMessage();
                msg.toAgentID = m_pendingFriendRequests[transactionID].Guid;
                msg.fromAgentID = agentID.Guid;
                msg.fromAgentName = client.Name;
                msg.fromAgentSession = client.SessionId.Guid;
                msg.fromGroup = false;
                msg.imSessionID = transactionID.Guid;
                msg.message = agentID.Guid.ToString();
                msg.ParentEstateID = 0;
                msg.timestamp = (uint) Util.UnixTimeSinceEpoch();
                msg.RegionID = SceneAgentIn.RegionInfo.RegionID.Guid;
                msg.dialog = (byte) 39; // Approved friend request
                msg.Position = Vector3.Zero;
                msg.offline = (byte) 0;
                msg.binaryBucket = new byte[0];
                // We don't really care which scene we pipe it through, it goes to the shared IM Module and/or the database

                SceneAgentIn.TriggerGridInstantMessage(msg, InstantMessageReceiver.IMModule);
                SceneAgentIn.StoreAddFriendship(m_pendingFriendRequests[transactionID], agentID, (uint) 1);


                //UUID[] Agents = new UUID[1];
                //Agents[0] = msg.toAgentID;
                //av.ControllingClient.SendAgentOnline(Agents);

                m_pendingFriendRequests.Remove(transactionID);
                // TODO: Inform agent that the friend is online
            }
        }

        private void OnDenyFriendRequest(IClientAPI client, UUID agentID, UUID transactionID, List<UUID> callingCardFolders)
        {
            if (m_pendingFriendRequests.ContainsKey(transactionID))
            {
                Scene SceneAgentIn = m_scene[0];

                // Found Pending Friend Request with that Transaction..
                ScenePresence agentpresence = GetRootPresenceFromAgentID(agentID);
                if (agentpresence != null)
                {
                    SceneAgentIn = agentpresence.Scene;
                }
                // Compose response to other agent.
                GridInstantMessage msg = new GridInstantMessage();
                msg.toAgentID = m_pendingFriendRequests[transactionID].Guid;
                msg.fromAgentID = agentID.Guid;
                msg.fromAgentName = client.Name;
                msg.fromAgentSession = client.SessionId.Guid;
                msg.fromGroup = false;
                msg.imSessionID = transactionID.Guid;
                msg.message = agentID.Guid.ToString();
                msg.ParentEstateID = 0;
                msg.timestamp = (uint) Util.UnixTimeSinceEpoch();
                msg.RegionID = SceneAgentIn.RegionInfo.RegionID.Guid;
                msg.dialog = (byte) 40; // Deny friend request
                msg.Position = Vector3.Zero;
                msg.offline = (byte) 0;
                msg.binaryBucket = new byte[0];
                SceneAgentIn.TriggerGridInstantMessage(msg, InstantMessageReceiver.IMModule);
                m_pendingFriendRequests.Remove(transactionID);
            }
        }

        private void OnTerminateFriendship(IClientAPI client, UUID agent, UUID exfriendID)
        {
            m_scene[0].StoreRemoveFriendship(agent, exfriendID);
            // TODO: Inform the client that the ExFriend is offline
        }

        private void OnGridInstantMessage(GridInstantMessage msg, InstantMessageReceiver whichModule)
        {
            if ((whichModule & InstantMessageReceiver.FriendsModule) == 0)
                return;

            // Trigger the above event handler
            OnInstantMessage(null, new UUID(msg.fromAgentID), new UUID(msg.fromAgentSession),
                             new UUID(msg.toAgentID), new UUID(msg.imSessionID), msg.timestamp, msg.fromAgentName,
                             msg.message, msg.dialog, msg.fromGroup, msg.offline, msg.ParentEstateID,
                             new Vector3(msg.Position.X, msg.Position.Y, msg.Position.Z), new UUID(msg.RegionID),
                             msg.binaryBucket);
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

            m_pendingCallingcardRequests[transactionID] = client.AgentId;
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
            if (m_pendingCallingcardRequests.TryGetValue(transactionID, out destID))
            {
                m_pendingCallingcardRequests.Remove(transactionID);

                ScenePresence destAgent = GetAnyPresenceFromAgentID(destID);
                // inform sender of the card that destination declined the offer
                if (destAgent != null) destAgent.ControllingClient.SendAcceptCallingCard(transactionID);

                // put a calling card into the inventory of receiver 
                CreateCallingCard(client, destID, folderID, destAgent.Name);
            }
            else m_log.WarnFormat("[CALLING CARD]: Got a AcceptCallingCard from {0} {1} without an offer before.",
                                  client.FirstName, client.LastName);
        }

        private void OnDeclineCallingCard(IClientAPI client, UUID transactionID)
        {
            m_log.DebugFormat("[CALLING CARD]: User {0} declined card, tid {2}",
                              client.AgentId, transactionID);
            UUID destID;
            if (m_pendingCallingcardRequests.TryGetValue(transactionID, out destID))
            {
                m_pendingCallingcardRequests.Remove(transactionID);

                ScenePresence destAgent = GetAnyPresenceFromAgentID(destID);
                // inform sender of the card that destination declined the offer
                if (destAgent != null) destAgent.ControllingClient.SendDeclineCallingCard(transactionID);
            }
            else m_log.WarnFormat("[CALLING CARD]: Got a DeclineCallingCard from {0} {1} without an offer before.",
                                  client.FirstName, client.LastName);
        }
    }

    #endregion

    public struct StoredFriendListUpdate
    {
        public UUID storedFor;
        public UUID storedAbout;
        public bool OnlineYN;
    }
}
