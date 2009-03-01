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

namespace OpenSim.Client.MXP.PacketHandler
{
    class MXPPacketServer
    {
        internal static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly List<MXPClientView> Clients = new List<MXPClientView>();
        private readonly Dictionary<UUID, Scene> Scenes;

        #region Fields

        private readonly Transmitter transmitter;

        private readonly Thread m_clientThread;

        private readonly IList<Session> sessions = new List<Session>();
        private readonly IList<Session> sessionsToClient = new List<Session>();
        private readonly IList<MXPClientView> sessionsToRemove = new List<MXPClientView>();

        private readonly String cloudUrl;
        private readonly String programName;
        private readonly byte programMajorVersion;
        private readonly byte programMinorVersion;

        #endregion

        #region Constructors

        public MXPPacketServer(string cloudUrl, int port, Dictionary<UUID, Scene> scenes)
        {
            this.cloudUrl = cloudUrl;

            Scenes = scenes;

            programMinorVersion = 63;
            programMajorVersion = 0;
            programName = "OpenSimulator";

            transmitter = new Transmitter(port);

            m_clientThread = new Thread(StartListener);
            m_clientThread.Name = "MXPThread";
            m_clientThread.IsBackground = true;
            m_clientThread.Start();
            ThreadTracker.Add(m_clientThread);
        }

        public void StartListener()
        {
            transmitter.Startup();
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
                return transmitter.PendingSessionCount;
            }
        }
        /// <summary>
        /// Number of connected sessions.
        /// </summary>
        public int SessionCount
        {
            get
            {
                return sessions.Count;
            }
        }
        /// <summary>
        /// Property reflecting whether client transmitter threads are alive.
        /// </summary>
        public bool IsTransmitterAlive
        {
            get
            {
                return transmitter != null && transmitter.IsAlive;
            }
        }
        /// <summary>
        /// Number of packets sent.
        /// </summary>
        public ulong PacketsSent
        {
            get
            {
                return transmitter != null ? transmitter.PacketsSent : 0;
            }
        }
        /// <summary>
        /// Number of packets received.
        /// </summary>
        public ulong PacketsReceived
        {
            get
            {
                return transmitter != null ? transmitter.PacketsReceived : 0;
            }
        }
        /// <summary>
        /// Bytes client has received so far.
        /// </summary>
        public ulong BytesReceived
        {
            get
            {
                return transmitter != null ? transmitter.BytesReceived : 0;
            }
        }
        /// <summary>
        /// Bytes client has sent so far.
        /// </summary>
        public ulong BytesSent
        {
            get
            {
                return transmitter != null ? transmitter.BytesSent : 0;
            }
        }
        /// <summary>
        /// Number of bytes received (bytes per second) during past second.
        /// </summary>
        public double ReceiveRate
        {
            get
            {
                return transmitter != null ? transmitter.ReceiveRate : 0;
            }
        }
        /// <summary>
        /// Number of bytes sent (bytes per second) during past second.
        /// </summary>
        public double SendRate
        {
            get
            {
                return transmitter != null ? transmitter.SendRate : 0;
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

        public void PrintDebugInformation()
        {
            m_log.Info("[MXP ClientStack] Statistics report");
            m_log.Info("Pending Sessions: " + PendingSessionCount);
            m_log.Info("Sessions: " + SessionCount + " (Clients: " + Clients.Count + " )");
            m_log.Info("Transmitter Alive?: " + IsTransmitterAlive);
            m_log.Info("Packets Sent/Received: " + PacketsSent + " / " + PacketsReceived);
            m_log.Info("Bytes Sent/Received: " + BytesSent + " / " + BytesReceived);
            m_log.Info("Send/Recieve Rate (bps): " + SendRate + " / " + ReceiveRate);
        }

        public void Process()
        {
            ProcessMessages();
            Clean();
        }

        public void Clean()
        {
            foreach (MXPClientView clientView in Clients)
            {
                if (clientView.Session.SessionState == SessionState.Disconnected)
                {
                    sessionsToRemove.Add(clientView);
                }
            }

            foreach (MXPClientView clientView in sessionsToRemove)
            {
                clientView.Scene.RemoveClient(clientView.AgentId);
                Clients.Remove(clientView);
                sessions.Remove(clientView.Session);
            }

            sessionsToRemove.Clear();
        }

        public bool AuthoriseUser(string participantName, string password, UUID sceneId, out UUID userId, out string firstName, out string lastName)
        {
            userId = UUID.Zero;
            firstName = "";
            lastName = "";

            if (!Scenes.ContainsKey(sceneId))
            {
                m_log.Info("Login failed as region was not found: " + sceneId);
                return false;
            }
            
            string[] nameParts=participantName.Split(' ');
            if (nameParts.Length != 2)
            {
                m_log.Info("Login failed as user name is not formed of first and last name separated by space: " + participantName);
                return false;
            }
            firstName = nameParts[0];
            lastName = nameParts[1];
            
            UserProfileData userProfile = Scenes[sceneId].CommsManager.UserService.GetUserProfile(firstName, lastName);
            if (userProfile == null)
            {
                m_log.Info("Login failed as user was not found: " + participantName);
                return false;
            }
            userId = userProfile.ID;

            if (!password.StartsWith("$1$"))
            {
                password = "$1$" + Util.Md5Hash(password);
            }
            password = password.Remove(0, 3); //remove $1$
            string s = Util.Md5Hash(password + ":" + userProfile.PasswordSalt);
            return (userProfile.PasswordHash.Equals(s.ToString(), StringComparison.InvariantCultureIgnoreCase)
                               || userProfile.PasswordHash.Equals(password, StringComparison.InvariantCultureIgnoreCase));
        }

        public void ProcessMessages()
        {
            if (transmitter.PendingSessionCount > 0)
            {
                Session tmp = transmitter.AcceptPendingSession();
                sessions.Add(tmp);
                sessionsToClient.Add(tmp);

            }

            List<Session> tmpRemove = new List<Session>();

            foreach (Session session in sessionsToClient)
            {
                while (session.AvailableMessages > 0)
                {
                    Message message = session.Receive();

                    if (message.GetType() == typeof (JoinRequestMessage))
                    {

                        JoinRequestMessage joinRequestMessage = (JoinRequestMessage) message;

                        UUID userId;
                        string firstName;
                        string lastName;

                        bool authorized = AuthoriseUser(joinRequestMessage.ParticipantName,
                                                        joinRequestMessage.ParticipantPassphrase,
                                                        new UUID(joinRequestMessage.BubbleId), out userId, out firstName, out lastName);

                        if (authorized)
                        {
                            Scene target = Scenes[new UUID(joinRequestMessage.BubbleId)];

                            UUID mxpSessionID = UUID.Random();

                            m_log.Info("[MXP ClientStack] Session join request success: " + session.SessionId + " (" +
                                       (session.IsIncoming ? "from" : "to") + " " + session.RemoteEndPoint.Address + ":" +
                                       session.RemoteEndPoint.Port + ")");

                            AcceptConnection(session, joinRequestMessage, mxpSessionID,userId);

                            MXPClientView client = new MXPClientView(session, mxpSessionID,userId, target,
                                                                     firstName, lastName);
                            m_log.Info("[MXP ClientStack] Created Client");
                            Clients.Add(client);

                            m_log.Info("[MXP ClientStack] Adding to Scene");
                            target.ClientManager.Add(client.CircuitCode, client);

                            m_log.Info("[MXP ClientStack] Initialising...");
                            try
                            {
                                client.Start();
                            } catch( Exception e)
                            {
                                m_log.Info(e);
                            }

                            m_log.Info("[MXP ClientStack] Connected");
                            //target.EventManager.TriggerOnNewClient(client);
                        }
                        else
                        {
                            m_log.Info("[MXP ClientStack] Session join request failure: " + session.SessionId + " (" +
                                       (session.IsIncoming ? "from" : "to") + " " + session.RemoteEndPoint.Address + ":" +
                                       session.RemoteEndPoint.Port + ")");

                            DeclineConnection(session, joinRequestMessage);
                        }

                        tmpRemove.Add(session);
                    }
                }
            }

            foreach (Session session in tmpRemove)
            {
                sessionsToClient.Remove(session);
            }

            foreach (MXPClientView clientView in Clients)
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

                        m_log.Info("[MXP ClientStack] Session leave request: " + session.SessionId + " (" + (session.IsIncoming ? "from" : "to") + " " + session.RemoteEndPoint.Address + ":" + session.RemoteEndPoint.Port + ")");

                        leaveResponseMessage.RequestMessageId = message.MessageId;
                        leaveResponseMessage.FailureCode = 0;
                        session.Send(leaveResponseMessage);

                        if (session.SessionState != SessionState.Disconnected)
                        {
                            session.SetStateDisconnected();
                        }

                        m_log.Info("[MXP ClientStack] Removing Client from Scene");
                        clientView.Scene.RemoveClient(clientView.AgentId);
                    }
                    if (message.GetType() == typeof(LeaveResponseMessage))
                    {
                        LeaveResponseMessage leaveResponseMessage = (LeaveResponseMessage)message;

                        m_log.Info("[MXP ClientStack] Session leave response: " + session.SessionId + " (" + (session.IsIncoming ? "from" : "to") + " " + session.RemoteEndPoint.Address + ":" + session.RemoteEndPoint.Port + ")");

                        if (leaveResponseMessage.FailureCode == 0)
                        {
                            session.SetStateDisconnected();
                        }

                        m_log.Info("[MXP ClientStack] Removing Client from Scene");
                        clientView.Scene.RemoveClient(clientView.AgentId);
                    }
                    else
                    {
                        clientView.ProcessMXPPacket(message);
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
            joinResponseMessage.FailureCode = 0;

            joinResponseMessage.ParticipantId = userId.Guid;
            joinResponseMessage.CloudUrl = cloudUrl;

            joinResponseMessage.BubbleName = Scenes[new UUID(joinRequestMessage.BubbleId)].RegionInfo.RegionName;

            joinResponseMessage.BubbleRealTime = 0;
            joinResponseMessage.ProgramName = programName;
            joinResponseMessage.ProgramMajorVersion = programMajorVersion;
            joinResponseMessage.ProgramMinorVersion = programMinorVersion;
            joinResponseMessage.ProtocolMajorVersion = MxpConstants.ProtocolMajorVersion;
            joinResponseMessage.ProtocolMinorVersion = MxpConstants.ProtocolMinorVersion;

            session.Send(joinResponseMessage);

            session.SetStateConnected();
        }

        private void DeclineConnection(Session session, Message joinRequestMessage)
        {
            JoinResponseMessage joinResponseMessage = (JoinResponseMessage)MessageFactory.Current.ReserveMessage(typeof(JoinResponseMessage));

            joinResponseMessage.RequestMessageId = joinRequestMessage.MessageId;
            joinResponseMessage.FailureCode = 1;

            joinResponseMessage.CloudUrl = cloudUrl;

            joinResponseMessage.BubbleName = "Declined OpenSim Region"; // Dont reveal anything about the sim in the disconnect notice

            joinResponseMessage.BubbleRealTime = 0;
            joinResponseMessage.ProgramName = programName;
            joinResponseMessage.ProgramMajorVersion = programMajorVersion;
            joinResponseMessage.ProgramMinorVersion = programMinorVersion;
            joinResponseMessage.ProtocolMajorVersion = MxpConstants.ProtocolMajorVersion;
            joinResponseMessage.ProtocolMinorVersion = MxpConstants.ProtocolMinorVersion;

            session.Send(joinResponseMessage);

            session.SetStateDisconnected();
        }

        #endregion
    }
}
