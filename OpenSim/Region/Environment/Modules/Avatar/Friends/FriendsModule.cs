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
using libsecondlife;
using libsecondlife.Packets;
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

        private Dictionary<LLUUID, List<FriendListItem>> FriendLists = new Dictionary<LLUUID, List<FriendListItem>>();
        private Dictionary<LLUUID, LLUUID> m_pendingFriendRequests = new Dictionary<LLUUID, LLUUID>();
        private Dictionary<LLUUID, ulong> m_rootAgents = new Dictionary<LLUUID, ulong>();
        private Dictionary<LLUUID, List<StoredFriendListUpdate>> StoredFriendListUpdates = new Dictionary<LLUUID, List<StoredFriendListUpdate>>();

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
            scene.EventManager.OnGridInstantMessageToFriendsModule += OnGridInstantMessage;
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
                LLUUID notifyAgentId = LLUUID.Zero;
                LLUUID notifyAboutAgentId = LLUUID.Zero;
                bool notifyOnlineStatus = false;

                if ((string)requestData["status"] == "TRUE")
                    notifyOnlineStatus = true;

                Helpers.TryParse((string)requestData["notify_id"], out notifyAgentId);

                Helpers.TryParse((string)requestData["agent_id"], out notifyAboutAgentId);
                m_log.InfoFormat("[PRESENCE]: Got presence update for {0}, and we're telling {1}, with a status {2}", notifyAboutAgentId.ToString(), notifyAgentId.ToString(), notifyOnlineStatus.ToString());
                ScenePresence avatar = GetPresenceFromAgentID(notifyAgentId);
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

            doFriendListUpdateOnline(client.AgentId);

        }

        private void doFriendListUpdateOnline(LLUUID AgentId)
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

            List<LLUUID> UpdateUsers = new List<LLUUID>();

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
            foreach (LLUUID user in UpdateUsers)
            {
                ScenePresence av = GetPresenceFromAgentID(user);
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
                                LLUUID[] Agents = new LLUUID[1];
                                Agents[0] = AgentId;
                                av.ControllingClient.SendAgentOnline(Agents);

                            }
                        }
                    }
                }
            }

            if (UpdateUsers.Count > 0)
            {
                ScenePresence avatar = GetPresenceFromAgentID(AgentId);
                if (avatar != null)
                {
                    avatar.ControllingClient.SendAgentOnline(UpdateUsers.ToArray());
                }

            }
        }

        private void ClientLoggedOut(LLUUID AgentId)
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
            List<LLUUID> updateUsers = new List<LLUUID>();
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
                    ScenePresence av = GetPresenceFromAgentID(updateUsers[i]);
                    if (av != null)
                    {
                        LLUUID[] agents = new LLUUID[1];
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

        private void AvatarEnteringParcel(ScenePresence avatar, int localLandID, LLUUID regionID)
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

        private ScenePresence GetPresenceFromAgentID(LLUUID AgentID)
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

        #region FriendRequestHandling

        private void OnInstantMessage(IClientAPI client, LLUUID fromAgentID,
                                      LLUUID fromAgentSession, LLUUID toAgentID,
                                      LLUUID imSessionID, uint timestamp, string fromAgentName,
                                      string message, byte dialog, bool fromGroup, byte offline,
                                      uint ParentEstateID, LLVector3 Position, LLUUID RegionID,
                                      byte[] binaryBucket)
        {
            // Friend Requests go by Instant Message..    using the dialog param
            // https://wiki.secondlife.com/wiki/ImprovedInstantMessage

            // 38 == Offer friendship
            if (dialog == (byte) 38)
            {
                LLUUID friendTransactionID = LLUUID.Random();

                m_pendingFriendRequests.Add(friendTransactionID, fromAgentID);

                m_log.Info("[FRIEND]: 38 - From:" + fromAgentID.ToString() + " To: " + toAgentID.ToString() + " Session:" + imSessionID.ToString() + " Message:" +
                           message);
                GridInstantMessage msg = new GridInstantMessage();
                msg.fromAgentID = fromAgentID.UUID;
                msg.fromAgentSession = fromAgentSession.UUID;
                msg.toAgentID = toAgentID.UUID;
                msg.imSessionID = friendTransactionID.UUID; // This is the item we're mucking with here
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
                msg.Position = new sLLVector3(Position);
                msg.RegionID = RegionID.UUID;
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

        private void OnApprovedFriendRequest(IClientAPI client, LLUUID agentID, LLUUID transactionID, List<LLUUID> callingCardFolders)
        {
            if (m_pendingFriendRequests.ContainsKey(transactionID))
            {
                // Found Pending Friend Request with that Transaction..
                Scene SceneAgentIn = m_scene[0];

                // Found Pending Friend Request with that Transaction..
                ScenePresence agentpresence = GetPresenceFromAgentID(agentID);
                if (agentpresence != null)
                {
                    SceneAgentIn = agentpresence.Scene;
                }

                // Compose response to other agent.
                GridInstantMessage msg = new GridInstantMessage();
                msg.toAgentID = m_pendingFriendRequests[transactionID].UUID;
                msg.fromAgentID = agentID.UUID;
                msg.fromAgentName = client.Name;
                msg.fromAgentSession = client.SessionId.UUID;
                msg.fromGroup = false;
                msg.imSessionID = transactionID.UUID;
                msg.message = agentID.UUID.ToString();
                msg.ParentEstateID = 0;
                msg.timestamp = (uint) Util.UnixTimeSinceEpoch();
                msg.RegionID = SceneAgentIn.RegionInfo.RegionID.UUID;
                msg.dialog = (byte) 39; // Approved friend request
                msg.Position = new sLLVector3();
                msg.offline = (byte) 0;
                msg.binaryBucket = new byte[0];
                // We don't really care which scene we pipe it through, it goes to the shared IM Module and/or the database

                SceneAgentIn.TriggerGridInstantMessage(msg, InstantMessageReceiver.IMModule);
                SceneAgentIn.StoreAddFriendship(m_pendingFriendRequests[transactionID], agentID, (uint) 1);


                //LLUUID[] Agents = new LLUUID[1];
                //Agents[0] = msg.toAgentID;
                //av.ControllingClient.SendAgentOnline(Agents);

                m_pendingFriendRequests.Remove(transactionID);
                // TODO: Inform agent that the friend is online
            }
        }

        private void OnDenyFriendRequest(IClientAPI client, LLUUID agentID, LLUUID transactionID, List<LLUUID> callingCardFolders)
        {
            if (m_pendingFriendRequests.ContainsKey(transactionID))
            {
                Scene SceneAgentIn = m_scene[0];

                // Found Pending Friend Request with that Transaction..
                ScenePresence agentpresence = GetPresenceFromAgentID(agentID);
                if (agentpresence != null)
                {
                    SceneAgentIn = agentpresence.Scene;
                }
                // Compose response to other agent.
                GridInstantMessage msg = new GridInstantMessage();
                msg.toAgentID = m_pendingFriendRequests[transactionID].UUID;
                msg.fromAgentID = agentID.UUID;
                msg.fromAgentName = client.Name;
                msg.fromAgentSession = client.SessionId.UUID;
                msg.fromGroup = false;
                msg.imSessionID = transactionID.UUID;
                msg.message = agentID.UUID.ToString();
                msg.ParentEstateID = 0;
                msg.timestamp = (uint) Util.UnixTimeSinceEpoch();
                msg.RegionID = SceneAgentIn.RegionInfo.RegionID.UUID;
                msg.dialog = (byte) 40; // Deny friend request
                msg.Position = new sLLVector3();
                msg.offline = (byte) 0;
                msg.binaryBucket = new byte[0];
                SceneAgentIn.TriggerGridInstantMessage(msg, InstantMessageReceiver.IMModule);
                m_pendingFriendRequests.Remove(transactionID);
            }
        }

        private void OnTerminateFriendship(IClientAPI client, LLUUID agent, LLUUID exfriendID)
        {
            m_scene[0].StoreRemoveFriendship(agent, exfriendID);
            // TODO: Inform the client that the ExFriend is offline
        }

        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            // Trigger the above event handler
            OnInstantMessage(null, new LLUUID(msg.fromAgentID), new LLUUID(msg.fromAgentSession),
                             new LLUUID(msg.toAgentID), new LLUUID(msg.imSessionID), msg.timestamp, msg.fromAgentName,
                             msg.message, msg.dialog, msg.fromGroup, msg.offline, msg.ParentEstateID,
                             new LLVector3(msg.Position.x, msg.Position.y, msg.Position.z), new LLUUID(msg.RegionID),
                             msg.binaryBucket);
        }

        #endregion
    }

    public struct StoredFriendListUpdate
    {
        public LLUUID storedFor;
        public LLUUID storedAbout;
        public bool OnlineYN;
    }
}
