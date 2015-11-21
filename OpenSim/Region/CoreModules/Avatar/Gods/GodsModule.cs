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
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Web;
using System.Xml;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;
using OSDArray = OpenMetaverse.StructuredData.OSDArray;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;

namespace OpenSim.Region.CoreModules.Avatar.Gods
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "GodsModule")]
    public class GodsModule : INonSharedRegionModule, IGodsModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>Special UUID for actions that apply to all agents</summary>
        private static readonly UUID ALL_AGENTS = new UUID("44e87126-e794-4ded-05b3-7c42da3d5cdb");

        protected Scene m_scene;
        protected IDialogModule m_dialogModule;

        protected IDialogModule DialogModule
        {
            get
            {
                if (m_dialogModule == null)
                    m_dialogModule = m_scene.RequestModuleInterface<IDialogModule>();

                return m_dialogModule;
            }
        }

        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<IGodsModule>(this);
            m_scene.EventManager.OnNewClient += SubscribeToClientEvents;
            m_scene.EventManager.OnRegisterCaps += OnRegisterCaps;
            scene.EventManager.OnIncomingInstantMessage +=
                    OnIncomingInstantMessage;
        }

        public void RemoveRegion(Scene scene)
        {
            m_scene.UnregisterModuleInterface<IGodsModule>(this);
            m_scene.EventManager.OnNewClient -= SubscribeToClientEvents;
            m_scene = null;
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void Close() {}
        public string Name { get { return "Gods Module"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void SubscribeToClientEvents(IClientAPI client)
        {
            client.OnGodKickUser += KickUser;
            client.OnRequestGodlikePowers += RequestGodlikePowers;
        }
        
        public void UnsubscribeFromClientEvents(IClientAPI client)
        {
            client.OnGodKickUser -= KickUser;
            client.OnRequestGodlikePowers -= RequestGodlikePowers;
        }
        
        private void OnRegisterCaps(UUID agentID, Caps caps)
        {
            string uri = "/CAPS/" + UUID.Random();

            caps.RegisterHandler(
                "UntrustedSimulatorMessage", 
                new RestStreamHandler("POST", uri, HandleUntrustedSimulatorMessage, "UntrustedSimulatorMessage", null));
        }

        private string HandleUntrustedSimulatorMessage(string request,
                string path, string param, IOSHttpRequest httpRequest,
                IOSHttpResponse httpResponse)
        {
            OSDMap osd = (OSDMap)OSDParser.DeserializeLLSDXml(request);

            string message = osd["message"].AsString();

            if (message == "GodKickUser")
            {
                OSDMap body = (OSDMap)osd["body"];
                OSDArray userInfo = (OSDArray)body["UserInfo"];
                OSDMap userData = (OSDMap)userInfo[0];

                UUID agentID = userData["AgentID"].AsUUID();
                UUID godID = userData["GodID"].AsUUID();
                UUID godSessionID = userData["GodSessionID"].AsUUID();
                uint kickFlags = userData["KickFlags"].AsUInteger();
                string reason = userData["Reason"].AsString();

                ScenePresence god = m_scene.GetScenePresence(godID);
                if (god == null || god.ControllingClient.SessionId != godSessionID)
                    return String.Empty;

                KickUser(godID, godSessionID, agentID, kickFlags, Util.StringToBytes1024(reason));
            }
            else
            {
                m_log.ErrorFormat("[GOD]: Unhandled UntrustedSimulatorMessage: {0}", message);
            }
            return String.Empty;
        }

        public void RequestGodlikePowers(
            UUID agentID, UUID sessionID, UUID token, bool godLike, IClientAPI controllingClient)
        {
            ScenePresence sp = m_scene.GetScenePresence(agentID);

            if (sp != null)
            {
                if (godLike == false)
                {
                    sp.GrantGodlikePowers(agentID, sessionID, token, godLike);
                    return;
                }

                // First check that this is the sim owner
                if (m_scene.Permissions.IsGod(agentID))
                {
                    // Next we check for spoofing.....
                    UUID testSessionID = sp.ControllingClient.SessionId;
                    if (sessionID == testSessionID)
                    {
                        if (sessionID == controllingClient.SessionId)
                        {
                            //m_log.Info("godlike: " + godLike.ToString());
                            sp.GrantGodlikePowers(agentID, testSessionID, token, godLike);
                        }
                    }
                }
                else
                {
                    if (DialogModule != null)
                        DialogModule.SendAlertToUser(agentID, "Request for god powers denied");
                }
            }
        }
        
        /// <summary>
        /// Kicks User specified from the simulator. This logs them off of the grid
        /// If the client gets the UUID: 44e87126e7944ded05b37c42da3d5cdb it assumes
        /// that you're kicking it even if the avatar's UUID isn't the UUID that the
        /// agent is assigned
        /// </summary>
        /// <param name="godID">The person doing the kicking</param>
        /// <param name="sessionID">The session of the person doing the kicking</param>
        /// <param name="agentID">the person that is being kicked</param>
        /// <param name="kickflags">Tells what to do to the user</param>
        /// <param name="reason">The message to send to the user after it's been turned into a field</param>
        public void KickUser(UUID godID, UUID sessionID, UUID agentID, uint kickflags, byte[] reason)
        {
            if (!m_scene.Permissions.IsGod(godID))
                return;

            ScenePresence sp = m_scene.GetScenePresence(agentID);

            if (sp == null && agentID != ALL_AGENTS)
            {
                IMessageTransferModule transferModule =
                        m_scene.RequestModuleInterface<IMessageTransferModule>();
                if (transferModule != null)
                {
                    m_log.DebugFormat("[GODS]: Sending nonlocal kill for agent {0}", agentID);
                    transferModule.SendInstantMessage(new GridInstantMessage(
                            m_scene, godID, "God", agentID, (byte)250, false,
                            Utils.BytesToString(reason), UUID.Zero, true,
                            new Vector3(), new byte[] {(byte)kickflags}, true),
                            delegate(bool success) {} );
                }
                return;
            }

            switch (kickflags)
            {
            case 0:
                if (sp != null)
                {
                    KickPresence(sp, Utils.BytesToString(reason));
                }
                else if (agentID == ALL_AGENTS)
                {
                    m_scene.ForEachRootScenePresence(
                            delegate(ScenePresence p)
                            {
                                if (p.UUID != godID && (!m_scene.Permissions.IsGod(p.UUID)))
                                    KickPresence(p, Utils.BytesToString(reason));
                            }
                    );
                }
                break;
            case 1:
                if (sp != null)
                {
                    sp.AllowMovement = false;
                    m_dialogModule.SendAlertToUser(agentID, Utils.BytesToString(reason));
                    m_dialogModule.SendAlertToUser(godID, "User Frozen");
                }
                break;
            case 2:
                if (sp != null)
                {
                    sp.AllowMovement = true;
                    m_dialogModule.SendAlertToUser(agentID, Utils.BytesToString(reason));
                    m_dialogModule.SendAlertToUser(godID, "User Unfrozen");
                }
                break;
            default:
                break;
            }
        }

        private void KickPresence(ScenePresence sp, string reason)
        {
            if (sp.IsChildAgent)
                return;
            sp.ControllingClient.Kick(reason);
            sp.Scene.CloseAgent(sp.UUID, true); 
        }

        private void OnIncomingInstantMessage(GridInstantMessage msg)
        {
            if (msg.dialog == (uint)250) // Nonlocal kick
            {
                UUID agentID = new UUID(msg.toAgentID);
                string reason = msg.message;
                UUID godID = new UUID(msg.fromAgentID);
                uint kickMode = (uint)msg.binaryBucket[0];

                KickUser(godID, UUID.Zero, agentID, kickMode, Util.StringToBytes1024(reason));
            }
        }
    }
}
