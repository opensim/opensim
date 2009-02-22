/* This file borrows heavily from MXPServer.cs - the reference MXPServer 
 * See http://www.bubblecloud.org for a copy of the original file and
 * implementation details. */
using System;
using System.Collections.Generic;
using System.Reflection;
using log4net;
using MXP;
using MXP.Messages;
using OpenMetaverse;
using OpenSim.Client.MXP.ClientStack;
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

        private readonly IList<Session> sessions = new List<Session>();
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
            m_log.Info("Packets Sent/Recieved: " + PacketsSent + " / " + PacketsReceived);
            m_log.Info("Bytes Sent/Recieved: " + BytesSent + " / " + BytesReceived);
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

        public bool AuthoriseUser(string participantName, string pass, UUID scene)
        {
            if (Scenes.ContainsKey(scene))
                return true;

            return false;
        }

        public void ProcessMessages()
        {
            if (transmitter.PendingSessionCount > 0)
            {
                sessions.Add(transmitter.AcceptPendingSession());
            }

            foreach (MXPClientView clientView in Clients)
            {
                
                int messagesProcessedCount = 0;
                Session session = clientView.Session;

                while (session.AvailableMessages > 0)
                {

                    Message message = session.Receive();

                    if (message.GetType() == typeof(JoinRequestMessage))
                    {

                        JoinRequestMessage joinRequestMessage = (JoinRequestMessage)message;

                        bool authorized = AuthoriseUser(joinRequestMessage.ParticipantName,
                                                        joinRequestMessage.ParticipantPassphrase,
                                                        new UUID(joinRequestMessage.BubbleId));

                        if (authorized)
                        {
                            Scene target = Scenes[new UUID(joinRequestMessage.BubbleId)];

                            UUID mxpSessionID = UUID.Random();

                            m_log.Info("[MXP ClientStack] Session join request success: " + session.SessionId + " (" + (session.IsIncoming ? "from" : "to") + " " + session.RemoteEndPoint.Address + ":" + session.RemoteEndPoint.Port + ")");

                            AcceptConnection(session, joinRequestMessage, mxpSessionID);

                            MXPClientView client = new MXPClientView(session, mxpSessionID, target,
                                                                     joinRequestMessage.ParticipantName);
                            Clients.Add(client);

                            target.AddNewClient(client);
                        }
                        else
                        {
                            m_log.Info("[MXP ClientStack] Session join request failure: " + session.SessionId + " (" + (session.IsIncoming ? "from" : "to") + " " + session.RemoteEndPoint.Address + ":" + session.RemoteEndPoint.Port + ")");

                            DeclineConnection(session, joinRequestMessage);
                        }

                    }
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

        private void AcceptConnection(Session session, JoinRequestMessage joinRequestMessage, UUID mxpSessionID)
        {
            JoinResponseMessage joinResponseMessage = (JoinResponseMessage)MessageFactory.Current.ReserveMessage(
                                                                               typeof(JoinResponseMessage));

            joinResponseMessage.RequestMessageId = joinRequestMessage.MessageId;
            joinResponseMessage.FailureCode = 0;

            joinResponseMessage.ParticipantId = mxpSessionID.Guid;
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
