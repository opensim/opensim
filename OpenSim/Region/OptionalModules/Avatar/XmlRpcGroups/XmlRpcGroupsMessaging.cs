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
//using System.Collections;
using System.Collections.Generic;
using System.Reflection;


using log4net;
using Nini.Config;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Region.CoreModules.Framework.EventQueue;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;


using Caps = OpenSim.Framework.Communications.Capabilities.Caps;

namespace OpenSim.Region.OptionalModules.Avatar.XmlRpcGroups
{
    public class XmlRpcGroupsMessaging : INonSharedRegionModule
    {

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_SceneList = new List<Scene>();

        // must be NonShared for this to work, otherewise we may actually get multiple active clients
        private Dictionary<Guid, IClientAPI> m_ActiveClients = new Dictionary<Guid, IClientAPI>();

        private IMessageTransferModule m_MsgTransferModule = null;

        private IGroupsModule m_GroupsModule = null;

        // Config Options
        private bool m_GroupMessagingEnabled = true;
        private bool m_debugEnabled = true;

        #region IRegionModule Members

        public void Initialise(IConfigSource config)
        {
            IConfig groupsConfig = config.Configs["Groups"];

            m_log.Info("[GROUPS-MESSAGING]: Initializing XmlRpcGroupsMessaging");

            if (groupsConfig == null)
            {
                // Do not run this module by default.
                m_log.Info("[GROUPS-MESSAGING]: No config found in OpenSim.ini -- not enabling XmlRpcGroupsMessaging");
                return;
            }
            else
            {
                if (!groupsConfig.GetBoolean("Enabled", false))
                {
                    m_log.Info("[GROUPS-MESSAGING]: Groups disabled in configuration");
                    return;
                }

                if (groupsConfig.GetString("Module", "Default") != "XmlRpcGroups")
                {
                    m_log.Info("[GROUPS-MESSAGING]: Config Groups Module not set to XmlRpcGroups");

                    return;
                }

                m_GroupMessagingEnabled = groupsConfig.GetBoolean("XmlRpcMessagingEnabled", true);

                if (!m_GroupMessagingEnabled)
                {
                    m_log.Info("[GROUPS-MESSAGING]: XmlRpcGroups Messaging disabled.");
                    return;
                }

                m_debugEnabled = groupsConfig.GetBoolean("XmlRpcDebugEnabled", true);

            }

            m_log.Info("[GROUPS-MESSAGING]: XmlRpcGroupsMessaging starting up");

        }

        public void AddRegion(Scene scene)
        {
        }
        public void RegionLoaded(Scene scene)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS-MESSAGING] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            if (!m_GroupMessagingEnabled)
                return;


            m_GroupsModule = scene.RequestModuleInterface<IGroupsModule>();

            // No groups module, no groups messaging
            if (m_GroupsModule == null)
            {
                m_GroupMessagingEnabled = false;
                m_log.Info("[GROUPS-MESSAGING]: Could not get IGroupsModule, XmlRpcGroupsMessaging is now disabled.");
                Close();
                return;
            }

            m_MsgTransferModule = scene.RequestModuleInterface<IMessageTransferModule>();

            // No message transfer module, no groups messaging
            if (m_MsgTransferModule == null)
            {
                m_GroupMessagingEnabled = false;
                m_log.Info("[GROUPS-MESSAGING]: Could not get MessageTransferModule");
                Close();
                return;
            }


            m_SceneList.Add(scene);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClientClosed += OnClientClosed;
            scene.EventManager.OnIncomingInstantMessage += OnGridInstantMessage;

        }

        public void RemoveRegion(Scene scene)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS-MESSAGING] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            m_SceneList.Remove(scene);
        }


        public void Close()
        {
            m_log.Debug("[GROUPS-MESSAGING]: Shutting down XmlRpcGroupsMessaging module.");


            foreach (Scene scene in m_SceneList)
            {
                scene.EventManager.OnNewClient -= OnNewClient;
                scene.EventManager.OnClientClosed -= OnClientClosed;
                scene.EventManager.OnIncomingInstantMessage -= OnGridInstantMessage;
            }

            m_SceneList.Clear();

            m_GroupsModule = null;
            m_MsgTransferModule = null;
        }

        public string Name
        {
            get { return "XmlRpcGroupsMessaging"; }
        }

        #endregion

        #region SimGridEventHandlers

        private void OnNewClient(IClientAPI client)
        {
            RegisterClientAgent(client);
        }
        private void OnClientClosed(UUID AgentId)
        {
            UnregisterClientAgent(AgentId);
        }

        private void OnGridInstantMessage(GridInstantMessage msg)
        {
            m_log.InfoFormat("[GROUPS-MESSAGING] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            DebugGridInstantMessage(msg);

            // Incoming message from a group
            if ((msg.dialog == (byte)InstantMessageDialog.SessionSend) && (msg.fromGroup == true))
            {
                if (m_ActiveClients.ContainsKey(msg.toAgentID))
                {
                    UUID GroupID = new UUID(msg.fromAgentID);
                    // SendMessageToGroup(im);

                    GroupRecord GroupInfo = m_GroupsModule.GetGroupRecord(GroupID);
                    if (GroupInfo != null)
                    {

                        if (m_debugEnabled) m_log.InfoFormat("[GROUPS-MESSAGING] Sending chatterbox invite instant message");

                        // Force? open the group session dialog???
                        IEventQueue eq = m_ActiveClients[msg.toAgentID].Scene.RequestModuleInterface<IEventQueue>();
                        eq.ChatterboxInvitation(
                            GroupID
                            , GroupInfo.GroupName
                            , new UUID(msg.fromAgentID)
                            , msg.message, new UUID(msg.toAgentID)
                            , msg.fromAgentName
                            , msg.dialog
                            , msg.timestamp
                            , msg.offline==1
                            , (int)msg.ParentEstateID
                            , msg.Position
                            , 1
                            , new UUID(msg.imSessionID)
                            , msg.fromGroup
                            , Utils.StringToBytes(GroupInfo.GroupName)
                            );

                        eq.ChatterBoxSessionAgentListUpdates(
                            new UUID(GroupID)
                            , new UUID(msg.fromAgentID)
                            , new UUID(msg.toAgentID)
                            , false //canVoiceChat
                            , false //isModerator
                            , false //text mute
                            );

                    }
                }
            }

        }

        #endregion

        #region ClientEvents
        private void OnInstantMessage(IClientAPI remoteClient, GridInstantMessage im)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS-MESSAGING] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            DebugGridInstantMessage(im);

            // Start group IM session
            if ((im.dialog == (byte)InstantMessageDialog.SessionGroupStart))
            {
                UUID GroupID = new UUID(im.toAgentID);

                GroupRecord GroupInfo = m_GroupsModule.GetGroupRecord(GroupID);
                if (GroupInfo != null)
                {
                    if (m_debugEnabled) m_log.InfoFormat("[GROUPS-MESSAGING] Start Group Session for {0}", GroupInfo.GroupName);

                    // remoteClient.SendInstantMessage(new GridInstantMessage(remoteClient.Scene, GroupID, GroupProfile.Name, remoteClient.AgentId, (byte)OpenMetaverse.InstantMessageDialog.SessionSend, true, "Welcome", GroupID, false, new Vector3(), new byte[0]));

                    ChatterBoxSessionStartReplyViaCaps(remoteClient, GroupInfo.GroupName, GroupID);

                    IEventQueue queue = remoteClient.Scene.RequestModuleInterface<IEventQueue>();
                    queue.ChatterBoxSessionAgentListUpdates(
                        new UUID(GroupID)
                        , new UUID(im.fromAgentID)
                        , new UUID(im.toAgentID)
                        , false //canVoiceChat
                        , false //isModerator
                        , false //text mute
                        );
                }
            }

            // Send a message to a group
            if ((im.dialog == (byte)InstantMessageDialog.SessionSend))
            {
                UUID GroupID = new UUID(im.toAgentID);

                if (m_debugEnabled) m_log.InfoFormat("[GROUPS-MESSAGING] Send message to session for group {0}", GroupID);

                SendMessageToGroup(im, GroupID);
            }

            // Incoming message from a group
            if ((im.dialog == (byte)InstantMessageDialog.SessionSend) && (im.fromGroup == true))
            {
                if (m_debugEnabled) m_log.InfoFormat("[GROUPS-MESSAGING] Message from group session {0} going to agent {1}", im.fromAgentID, im.toAgentID);
            }
        }
        #endregion

        private void RegisterClientAgent(IClientAPI client)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS-MESSAGING] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            lock (m_ActiveClients)
            {
                if (!m_ActiveClients.ContainsKey(client.AgentId.Guid))
                {
                    client.OnInstantMessage += OnInstantMessage;

                    if (m_debugEnabled) m_log.InfoFormat("[GROUPS-MESSAGING] OnInstantMessage registered for {0}", client.Name);

                    m_ActiveClients.Add(client.AgentId.Guid, client);
                }
                else
                {
                    // Remove old client connection for this agent
                    UnregisterClientAgent(client.AgentId);

                    // Add new client connection
                    RegisterClientAgent(client);
                }
            }
        }
        private void UnregisterClientAgent(UUID agentID)
        {
            lock (m_ActiveClients)
            {
                if (m_ActiveClients.ContainsKey(agentID.Guid))
                {
                    IClientAPI client = m_ActiveClients[agentID.Guid];
                    client.OnInstantMessage -= OnInstantMessage;

                    if (m_debugEnabled) m_log.InfoFormat("[GROUPS-MESSAGING] OnInstantMessage unregistered for {0}", client.Name);

                    m_ActiveClients.Remove(agentID.Guid);
                }
                else
                {
                    m_log.InfoFormat("[GROUPS-MESSAGING] Client closed that wasn't registered here.");
                }
            }
        }

        private void SendMessageToGroup(GridInstantMessage im, UUID groupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS-MESSAGING] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            GridInstantMessage msg = new GridInstantMessage();
            msg.imSessionID = im.imSessionID;
            msg.fromAgentID = im.imSessionID; // GroupID
            msg.timestamp = (uint)Util.UnixTimeSinceEpoch();
            msg.fromAgentName = im.fromAgentName;
            msg.message = im.message;
            msg.dialog = im.dialog;
            msg.fromGroup = true;
            msg.offline = (byte)0;
            msg.ParentEstateID = im.ParentEstateID;
            msg.Position = im.Position;
            msg.RegionID = im.RegionID;
            msg.binaryBucket = new byte[1] { 0 };

            foreach (GroupMembersData member in m_GroupsModule.GroupMembersRequest(null, groupID))
            {
                msg.toAgentID = member.AgentID.Guid;
                m_MsgTransferModule.SendInstantMessage(msg, delegate(bool success) { });
            }
        }

        void ChatterBoxSessionStartReplyViaCaps(IClientAPI remoteClient, string groupName, UUID groupID)
        {
            if (m_debugEnabled) m_log.InfoFormat("[GROUPS-MESSAGING] {0} called", System.Reflection.MethodBase.GetCurrentMethod().Name);

            OSDMap moderatedMap = new OSDMap(4);
            moderatedMap.Add("voice", OSD.FromBoolean(false));

            OSDMap sessionMap = new OSDMap(4);
            sessionMap.Add("moderated_mode", moderatedMap);
            sessionMap.Add("session_name", OSD.FromString(groupName));
            sessionMap.Add("type", OSD.FromInteger(0));
            sessionMap.Add("voice_enabled", OSD.FromBoolean(false));


            OSDMap bodyMap = new OSDMap(4);
            bodyMap.Add("session_id", OSD.FromUUID(groupID));
            bodyMap.Add("temp_session_id", OSD.FromUUID(groupID));
            bodyMap.Add("success", OSD.FromBoolean(true));
            bodyMap.Add("session_info", sessionMap);


            IEventQueue queue = remoteClient.Scene.RequestModuleInterface<IEventQueue>();

            if (queue != null)
            {
                queue.Enqueue(EventQueueHelper.buildEvent("ChatterBoxSessionStartReply", bodyMap), remoteClient.AgentId);
            }

        }


        private void DebugGridInstantMessage(GridInstantMessage im)
        {
            if (m_debugEnabled)
            {
                m_log.WarnFormat("[GROUPS-MESSAGING] IM: fromGroup({0})", im.fromGroup ? "True" : "False");
                m_log.WarnFormat("[GROUPS-MESSAGING] IM: Dialog({0})", ((InstantMessageDialog)im.dialog).ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING] IM: fromAgentID({0})", im.fromAgentID.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING] IM: fromAgentName({0})", im.fromAgentName.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING] IM: imSessionID({0})", im.imSessionID.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING] IM: message({0})", im.message.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING] IM: offline({0})", im.offline.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING] IM: toAgentID({0})", im.toAgentID.ToString());
                m_log.WarnFormat("[GROUPS-MESSAGING] IM: binaryBucket({0})", OpenMetaverse.Utils.BytesToHexString(im.binaryBucket, "BinaryBucket"));
            }
        }

    }
}
