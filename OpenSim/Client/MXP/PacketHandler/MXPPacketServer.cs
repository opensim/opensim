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

/* This file borrows heavily from MXPServer.cs - the reference MXPServer 
 * See http://www.bubblecloud.org for a copy of the original file and
 * implementation details. */

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using log4net;
using MXP;
using MXP.Messages;
using OpenMetaverse;
using OpenSim.Client.MXP.ClientStack;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework.Communications;
using System.Security.Cryptography;

namespace OpenSim.Client.MXP.PacketHandler
{
    public class MXPPacketServer
    {
        internal static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region Fields

        private readonly List<MXPClientView> m_clients = new List<MXPClientView>();
        private readonly Dictionary<UUID, Scene> m_scenes;
        private readonly Transmitter m_transmitter;

        private readonly Thread m_clientThread;

        private readonly IList<Session> m_sessions = new List<Session>();
        private readonly IList<Session> m_sessionsToClient = new List<Session>();
        private readonly IList<MXPClientView> m_sessionsToRemove = new List<MXPClientView>();

        private readonly int m_port;
        private readonly bool m_accountsAuthenticate;

        private readonly String m_programName;
        private readonly byte m_programMajorVersion;
        private readonly byte m_programMinorVersion;

        #endregion

        #region Constructors

        public MXPPacketServer(int port, Dictionary<UUID, Scene> scenes, bool accountsAuthenticate)
        {
            m_port = port;
            m_accountsAuthenticate = accountsAuthenticate;

            m_scenes = scenes;

            m_programMinorVersion = 63;
            m_programMajorVersion = 0;
            m_programName = "OpenSimulator";

            m_transmitter = new Transmitter(port);

            m_clientThread = new Thread(StartListener);
            m_clientThread.Name = "MXPThread";
            m_clientThread.IsBackground = true;
            m_clientThread.Start();
        }

        public void StartListener()
        {
            m_log.Info("[MXP ClientStack] Transmitter starting on UDP server port: " + m_port);
            m_transmitter.Startup();
            m_log.Info("[MXP ClientStack] Transmitter started. MXP version: "+MxpConstants.ProtocolMajorVersion+"."+MxpConstants.ProtocolMinorVersion+" Source Revision: "+MxpConstants.ProtocolSourceRevision);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Number of sessions pending. (Process() accepts pending sessions).
        /// </summary>
        public int PendingSessionCount
        {
            get
            {
                return m_transmitter.PendingSessionCount;
            }
        }
        /// <summary>
        /// Number of connected sessions.
        /// </summary>
        public int SessionCount
        {
            get
            {
                return m_sessions.Count;
            }
        }
        /// <summary>
        /// Property reflecting whether client transmitter threads are alive.
        /// </summary>
        public bool IsTransmitterAlive
        {
            get
            {
                return m_transmitter != null && m_transmitter.IsAlive;
            }
        }
        /// <summary>
        /// Number of packets sent.
        /// </summary>
        public ulong PacketsSent
        {
            get
            {
                return m_transmitter != null ? m_transmitter.PacketsSent : 0;
            }
        }
        /// <summary>
        /// Number of packets received.
        /// </summary>
        public ulong PacketsReceived
        {
            get
            {
                return m_transmitter != null ? m_transmitter.PacketsReceived : 0;
            }
        }
        /// <summary>
        /// Bytes client has received so far.
        /// </summary>
        public ulong BytesReceived
        {
            get
            {
                return m_transmitter != null ? m_transmitter.BytesReceived : 0;
            }
        }
        /// <summary>
        /// Bytes client has sent so far.
        /// </summary>
        public ulong BytesSent
        {
            get
            {
                return m_transmitter != null ? m_transmitter.BytesSent : 0;
            }
        }
        /// <summary>
        /// Number of bytes received (bytes per second) during past second.
        /// </summary>
        public double ReceiveRate
        {
            get
            {
                return m_transmitter != null ? m_transmitter.ReceiveRate : 0;
            }
        }
        /// <summary>
        /// Number of bytes sent (bytes per second) during past second.
        /// </summary>
        public double SendRate
        {
            get
            {
                return m_transmitter != null ? m_transmitter.SendRate : 0;
            }
        }

        #endregion

        #region Session Management

        public void Disconnect(Session session)
        {
            if (session.IsConnected)
            {
                Message message = MessageFactory.Current.ReserveMessage(typeof(LeaveRequestMessage));
                session.Send(message);
                MessageFactory.Current.ReleaseMessage(message);
            }
            else
            {
                throw new Exception("Not connected.");
            }
        }

        #endregion

        #region Processing

        public void Process()
        {
            ProcessMessages();
            Clean();
        }

        public void Clean()
        {
            foreach (MXPClientView clientView in m_clients)
            {
                if (clientView.Session.SessionState == SessionState.Disconnected)
                {
                    m_sessionsToRemove.Add(clientView);
                }
            }

            foreach (MXPClientView clientView in m_sessionsToRemove)
            {
                clientView.Scene.RemoveClient(clientView.AgentId);
                clientView.OnClean();
                m_clients.Remove(clientView);
                m_sessions.Remove(clientView.Session);
            }

            m_sessionsToRemove.Clear();
        }

        public void ProcessMessages()
        {
            if (m_transmitter.PendingSessionCount > 0)
            {
                Session tmp = m_transmitter.AcceptPendingSession();
                m_sessions.Add(tmp);
                m_sessionsToClient.Add(tmp);

            }

            List<Session> tmpRemove = new List<Session>();

            foreach (Session session in m_sessionsToClient)
            {
                while (session.AvailableMessages > 0)
                {
                    Message message = session.Receive();

                    if (message.GetType() == typeof (JoinRequestMessage))
                    {

                        JoinRequestMessage joinRequestMessage = (JoinRequestMessage) message;

                        m_log.Info("[MXP ClientStack]: Session join request: " + session.SessionId + " (" +
                           (session.IsIncoming ? "from" : "to") + " " + session.RemoteEndPoint.Address + ":" +
                            session.RemoteEndPoint.Port + ")");

                        try
                        {

                            if (joinRequestMessage.BubbleId == Guid.Empty)
                            {
                                foreach (Scene scene in m_scenes.Values)
                                {
                                    if (scene.RegionInfo.RegionName == joinRequestMessage.BubbleName)
                                    {
                                        m_log.Info("[MXP ClientStack]: Resolved region by name: " + joinRequestMessage.BubbleName + " (" + scene.RegionInfo.RegionID + ")");
                                        joinRequestMessage.BubbleId = scene.RegionInfo.RegionID.Guid;
                                    }
                                }
                            }

                            if (joinRequestMessage.BubbleId == Guid.Empty)
                            {
                                m_log.Warn("[MXP ClientStack]: Failed to resolve region by name: " + joinRequestMessage.BubbleName);
                            }

                            UUID sceneId = new UUID(joinRequestMessage.BubbleId);

                            bool regionExists = true;
                            if (!m_scenes.ContainsKey(sceneId))
                            {
                                m_log.Info("[MXP ClientStack]: No such region: " + sceneId);
                                regionExists = false;
                            }

                            UserProfileData user = null;
                            UUID userId = UUID.Zero;
                            string firstName = null;
                            string lastName = null;
                            bool authorized = regionExists ? AuthoriseUser(joinRequestMessage.ParticipantName,
                                                            joinRequestMessage.ParticipantPassphrase,
                                                            new UUID(joinRequestMessage.BubbleId), out userId, out firstName, out lastName, out user)
                                                            : false;

                            if (authorized)
                            {
                                Scene scene = m_scenes[sceneId];
                                UUID mxpSessionID = UUID.Random();
                                
                                string reason;

                                m_log.Debug("[MXP ClientStack]: Session join request success: " + session.SessionId + " (" +
                                   (session.IsIncoming ? "from" : "to") + " " + session.RemoteEndPoint.Address + ":" +
                                   session.RemoteEndPoint.Port + ")");

                                m_log.Debug("[MXP ClientStack]: Attaching UserAgent to UserProfile...");
                                AttachUserAgentToUserProfile(session, mxpSessionID, sceneId, user);
                                m_log.Debug("[MXP ClientStack]: Attached UserAgent to UserProfile.");
                                m_log.Debug("[MXP ClientStack]: Preparing Scene to Connection...");
                                if (!PrepareSceneForConnection(mxpSessionID, sceneId, user, out reason))
                                {
                                    m_log.DebugFormat("[MXP ClientStack]: Scene refused connection: {0}", reason);
                                    DeclineConnection(session, joinRequestMessage);
                                    tmpRemove.Add(session);
                                    continue;
                                }
                                m_log.Debug("[MXP ClientStack]: Prepared Scene to Connection.");
                                m_log.Debug("[MXP ClientStack]: Accepting connection...");
                                AcceptConnection(session, joinRequestMessage, mxpSessionID, userId);
                                m_log.Info("[MXP ClientStack]: Accepted connection.");

                                m_log.Debug("[MXP ClientStack]: Creating ClientView....");
                                MXPClientView client = new MXPClientView(session, mxpSessionID, userId, scene, firstName, lastName);
                                m_clients.Add(client);
                                m_log.Debug("[MXP ClientStack]: Created ClientView.");


                                m_log.Debug("[MXP ClientStack]: Adding ClientView to Scene...");
                                scene.ClientManager.Add(client.CircuitCode, client);
                                m_log.Debug("[MXP ClientStack]: Added ClientView to Scene.");


                                client.MXPSendSynchronizationBegin(m_scenes[new UUID(joinRequestMessage.BubbleId)].SceneContents.GetTotalObjectsCount());

                                m_log.Debug("[MXP ClientStack]: Starting ClientView...");
                                try
                                {
                                    client.Start();
                                    m_log.Debug("[MXP ClientStack]: Started ClientView.");
                                }
                                catch (Exception e)
                                {
                                    m_log.Error(e);
                                }

                                m_log.Debug("[MXP ClientStack]: Connected");
                            }
                            else
                            {
                                m_log.Info("[MXP ClientStack]: Session join request failure: " + session.SessionId + " (" +
                                           (session.IsIncoming ? "from" : "to") + " " + session.RemoteEndPoint.Address + ":" +
                                           session.RemoteEndPoint.Port + ")");

                                DeclineConnection(session, joinRequestMessage);
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.Error("[MXP ClientStack]: Session join request failure: " + session.SessionId + " (" +
                               (session.IsIncoming ? "from" : "to") + " " + session.RemoteEndPoint.Address + ":" +
                               session.RemoteEndPoint.Port + "): "+e.ToString()+" :"+e.StackTrace.ToString());
                        }
                        tmpRemove.Add(session);
                    }
                }
            }

            foreach (Session session in tmpRemove)
            {
                m_sessionsToClient.Remove(session);
            }

            foreach (MXPClientView clientView in m_clients)
            {
                int messagesProcessedCount = 0;
                Session session = clientView.Session;

                while (session.AvailableMessages > 0)
                {
                    Message message = session.Receive();

                    if (message.GetType() == typeof(LeaveRequestMessage))
                    {
                        LeaveResponseMessage leaveResponseMessage = (LeaveResponseMessage)MessageFactory.Current.ReserveMessage(
                            typeof(LeaveResponseMessage));

                        m_log.Debug("[MXP ClientStack]: Session leave request: " + session.SessionId + " (" + (session.IsIncoming ? "from" : "to") + " " + session.RemoteEndPoint.Address + ":" + session.RemoteEndPoint.Port + ")");

                        leaveResponseMessage.RequestMessageId = message.MessageId;
                        leaveResponseMessage.FailureCode = 0;
                        session.Send(leaveResponseMessage);

                        if (session.SessionState != SessionState.Disconnected)
                        {
                            session.SetStateDisconnected();
                        }

                        m_log.Debug("[MXP ClientStack]: Removing Client from Scene");
                        //clientView.Scene.RemoveClient(clientView.AgentId);
                    }
                    if (message.GetType() == typeof(LeaveResponseMessage))
                    {
                        LeaveResponseMessage leaveResponseMessage = (LeaveResponseMessage)message;

                        m_log.Debug("[MXP ClientStack]: Session leave response: " + session.SessionId + " (" + (session.IsIncoming ? "from" : "to") + " " + session.RemoteEndPoint.Address + ":" + session.RemoteEndPoint.Port + ")");

                        if (leaveResponseMessage.FailureCode == 0)
                        {
                            session.SetStateDisconnected();
                        }

                        m_log.Debug("[MXP ClientStack]: Removing Client from Scene");
                        //clientView.Scene.RemoveClient(clientView.AgentId);
                    }
                    else
                    {
                        clientView.MXPPRocessMessage(message);
                    }

                    MessageFactory.Current.ReleaseMessage(message);
                    messagesProcessedCount++;
                    if (messagesProcessedCount > 1000)
                    {
                        break;
                    }
                }
            }
        }

        private void AcceptConnection(Session session, JoinRequestMessage joinRequestMessage, UUID mxpSessionID, UUID userId)
        {
            JoinResponseMessage joinResponseMessage = (JoinResponseMessage)MessageFactory.Current.ReserveMessage(
                                                                               typeof(JoinResponseMessage));

            joinResponseMessage.RequestMessageId = joinRequestMessage.MessageId;
            joinResponseMessage.FailureCode = MxpResponseCodes.SUCCESS;
            
            joinResponseMessage.BubbleId = joinRequestMessage.BubbleId;
            joinResponseMessage.ParticipantId = userId.Guid;
            joinResponseMessage.AvatarId = userId.Guid;
            joinResponseMessage.BubbleAssetCacheUrl = "http://" +
                                                      NetworkUtil.GetHostFor(session.RemoteEndPoint.Address,
                                                                             m_scenes[
                                                                                 new UUID(joinRequestMessage.BubbleId)].
                                                                                 RegionInfo.
                                                                                 ExternalHostName) + ":" +
                                                      m_scenes[new UUID(joinRequestMessage.BubbleId)].RegionInfo.
                                                          HttpPort + "/assets/";

            joinResponseMessage.BubbleName = m_scenes[new UUID(joinRequestMessage.BubbleId)].RegionInfo.RegionName;

            joinResponseMessage.BubbleRange = 128;
            joinResponseMessage.BubblePerceptionRange = 128 + 256;
            joinResponseMessage.BubbleRealTime = 0;
            joinResponseMessage.ProgramName = m_programName;
            joinResponseMessage.ProgramMajorVersion = m_programMajorVersion;
            joinResponseMessage.ProgramMinorVersion = m_programMinorVersion;
            joinResponseMessage.ProtocolMajorVersion = MxpConstants.ProtocolMajorVersion;
            joinResponseMessage.ProtocolMinorVersion = MxpConstants.ProtocolMinorVersion;
            joinResponseMessage.ProtocolSourceRevision = MxpConstants.ProtocolSourceRevision;

            session.Send(joinResponseMessage);

            session.SetStateConnected();
        }

        private void DeclineConnection(Session session, Message joinRequestMessage)
        {
            JoinResponseMessage joinResponseMessage = (JoinResponseMessage)MessageFactory.Current.ReserveMessage(typeof(JoinResponseMessage));

            joinResponseMessage.RequestMessageId = joinRequestMessage.MessageId;
            joinResponseMessage.FailureCode = MxpResponseCodes.UNAUTHORIZED_OPERATION;

            joinResponseMessage.ProgramName = m_programName;
            joinResponseMessage.ProgramMajorVersion = m_programMajorVersion;
            joinResponseMessage.ProgramMinorVersion = m_programMinorVersion;
            joinResponseMessage.ProtocolMajorVersion = MxpConstants.ProtocolMajorVersion;
            joinResponseMessage.ProtocolMinorVersion = MxpConstants.ProtocolMinorVersion;
            joinResponseMessage.ProtocolSourceRevision = MxpConstants.ProtocolSourceRevision;

            session.Send(joinResponseMessage);

            session.SetStateDisconnected();
        }

        public bool AuthoriseUser(string participantName, string password, UUID sceneId, out UUID userId, out string firstName, out string lastName, out UserProfileData userProfile)
        {
            userId = UUID.Zero;
            firstName = "";
            lastName = "";
            userProfile = null;

            string[] nameParts = participantName.Split(' ');
            if (nameParts.Length != 2)
            {
                m_log.Error("[MXP ClientStack]: Login failed as user name is not formed of first and last name separated by space: " + participantName);
                return false;
            }
            firstName = nameParts[0];
            lastName = nameParts[1];

            userProfile = m_scenes[sceneId].CommsManager.UserService.GetUserProfile(firstName, lastName);

            if (userProfile == null && !m_accountsAuthenticate)
            {
                userId = ((UserManagerBase)m_scenes[sceneId].CommsManager.UserService).AddUser(firstName, lastName, "test", "", 1000, 1000);
            }
            else
            {
                if (userProfile == null)
                {
                    m_log.Error("[MXP ClientStack]: Login failed as user was not found: " + participantName);
                    return false;
                }
                userId = userProfile.ID;
            }

            if (m_accountsAuthenticate)
            {
                if (!password.StartsWith("$1$"))
                {
                    password = "$1$" + Util.Md5Hash(password);
                }
                password = password.Remove(0, 3); //remove $1$
                string s = Util.Md5Hash(password + ":" + userProfile.PasswordSalt);
                return (userProfile.PasswordHash.Equals(s.ToString(), StringComparison.InvariantCultureIgnoreCase)
                                   || userProfile.PasswordHash.Equals(password, StringComparison.InvariantCulture));
                }
            else
            {
                return true;
            }
        }

        private void AttachUserAgentToUserProfile(Session session, UUID sessionId, UUID sceneId, UserProfileData userProfile)
        {
            //Scene scene = m_scenes[sceneId];
            CommunicationsManager commsManager = m_scenes[sceneId].CommsManager;
            IUserService userService = (IUserService)commsManager.UserService;

            UserAgentData agent = new UserAgentData();

            // User connection
            agent.AgentOnline = true;
            agent.AgentIP = session.RemoteEndPoint.Address.ToString();
            agent.AgentPort = (uint)session.RemoteEndPoint.Port;

            agent.SecureSessionID = UUID.Random();
            agent.SessionID = sessionId;

            // Profile UUID
            agent.ProfileID = userProfile.ID;

            // Current location/position/alignment
            if (userProfile.CurrentAgent != null)
            {
                agent.Region = userProfile.CurrentAgent.Region;
                agent.Handle = userProfile.CurrentAgent.Handle;
                agent.Position = userProfile.CurrentAgent.Position;
                agent.LookAt = userProfile.CurrentAgent.LookAt;
            }
            else
            {
                agent.Region = userProfile.HomeRegionID;
                agent.Handle = userProfile.HomeRegion;
                agent.Position = userProfile.HomeLocation;
                agent.LookAt = userProfile.HomeLookAt;
            }

            // What time did the user login?
            agent.LoginTime = Util.UnixTimeSinceEpoch();
            agent.LogoutTime = 0;

            userProfile.CurrentAgent = agent;


            userService.UpdateUserProfile(userProfile);
            //userService.CommitAgent(ref userProfile);
        }

        private bool PrepareSceneForConnection(UUID sessionId, UUID sceneId, UserProfileData userProfile, out string reason)
        {
            Scene scene = m_scenes[sceneId];
            CommunicationsManager commsManager = m_scenes[sceneId].CommsManager;
            UserManagerBase userService = (UserManagerBase)commsManager.UserService;

            AgentCircuitData agent = new AgentCircuitData();
            agent.AgentID = userProfile.ID;
            agent.firstname = userProfile.FirstName;
            agent.lastname = userProfile.SurName;
            agent.SessionID = sessionId;
            agent.SecureSessionID = userProfile.CurrentAgent.SecureSessionID;
            agent.circuitcode = sessionId.CRC();
            agent.BaseFolder = UUID.Zero;
            agent.InventoryFolder = UUID.Zero;
            agent.startpos = new Vector3(0, 0, 0); // TODO Fill in region start position
            agent.CapsPath = "http://localhost/";
            agent.Appearance = userService.GetUserAppearance(userProfile.ID);

            if (agent.Appearance == null)
            {
                m_log.WarnFormat("[INTER]: Appearance not found for {0} {1}. Creating default.", agent.firstname, agent.lastname);
                agent.Appearance = new AvatarAppearance();
            }
            
            return scene.NewUserConnection(agent, out reason);
        }

        public void PrintDebugInformation()
        {
            m_log.Info("[MXP ClientStack]: Statistics report");
            m_log.Info("Pending Sessions: " + PendingSessionCount);
            m_log.Info("Sessions: " + SessionCount + " (Clients: " + m_clients.Count + " )");
            m_log.Info("Transmitter Alive?: " + IsTransmitterAlive);
            m_log.Info("Packets Sent/Received: " + PacketsSent + " / " + PacketsReceived);
            m_log.Info("Bytes Sent/Received: " + BytesSent + " / " + BytesReceived);
            m_log.Info("Send/Receive Rate (bps): " + SendRate + " / " + ReceiveRate);
        }

        #endregion
    
    }
}
