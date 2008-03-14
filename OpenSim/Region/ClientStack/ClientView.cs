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
* 
*/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Scenes;
using Timer = System.Timers.Timer;

namespace OpenSim.Region.ClientStack
{
    public delegate bool PacketMethod(IClientAPI simClient, Packet packet);

    /// <summary>
    /// Handles new client connections
    /// Constructor takes a single Packet and authenticates everything
    /// </summary>
    public class ClientView : IClientAPI
    {
        //                ~ClientView()
        //                {
        //                    System.Console.WriteLine("[CLIENTVIEW]: Destructor called");                       
        //                }

        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /* static variables */
        public static TerrainManager TerrainManager;

        public delegate bool SynchronizeClientHandler(IScene scene, Packet packet, LLUUID agentID, ThrottleOutPacketType throttlePacketType);
        public static SynchronizeClientHandler SynchronizeClient = null; 
        /* private variables */
        private readonly LLUUID m_sessionId;
        private LLUUID m_secureSessionId = LLUUID.Zero;
        //private AgentAssetUpload UploadAssets;
        private int m_debug = 0;
        private readonly AssetCache m_assetCache;
        // private InventoryCache m_inventoryCache;
        private int m_cachedTextureSerial = 0;
        private Timer m_clientPingTimer;

        private int m_packetsReceived = 0;
        private int m_lastPacketsReceivedSentToScene = 0;
        private int m_unAckedBytes = 0;

        private int m_packetsSent = 0;
        private int m_lastPacketsSentSentToScene = 0;

        private int m_probesWithNoIngressPackets = 0;
        private int m_lastPacketsReceived = 0;
        private byte[] ZeroOutBuffer = new byte[4096];

        private readonly Encoding m_encoding = Encoding.ASCII;
        private readonly LLUUID m_agentId;
        private readonly uint m_circuitCode;
        private int m_moneyBalance;

        private byte[] m_channelVersion = Helpers.StringToField("OpenSimulator 0.5"); // Dummy value needed by libSL

        /* protected variables */

        protected static Dictionary<PacketType, PacketMethod> PacketHandlers =
            new Dictionary<PacketType, PacketMethod>(); //Global/static handlers for all clients

        protected Dictionary<PacketType, PacketMethod> m_packetHandlers = new Dictionary<PacketType, PacketMethod>();

        protected IScene m_scene;
        protected AgentCircuitManager m_authenticateSessionsHandler;

        protected PacketQueue m_packetQueue;

        protected Dictionary<uint, uint> m_pendingAcks = new Dictionary<uint, uint>();
        protected Dictionary<uint, Packet> m_needAck = new Dictionary<uint, Packet>();

        protected Timer m_ackTimer;
        protected uint m_sequence = 0;
        protected object m_sequenceLock = new object();
        protected const int MAX_APPENDED_ACKS = 10;
        protected const int RESEND_TIMEOUT = 4000;
        protected const int MAX_SEQUENCE = 0xFFFFFF;
        protected PacketServer m_networkServer;

        /* public variables */
        protected string m_firstName;
        protected string m_lastName;
        protected Thread m_clientThread;
        protected LLVector3 m_startpos;
        protected EndPoint m_userEndPoint;
        protected EndPoint m_proxyEndPoint;

        /* Instantiated Designated Event Delegates */
        //- used so we don't create new objects for each incoming packet and then toss it out later */

        private RequestAvatarProperties handlerRequestAvatarProperties = null; //OnRequestAvatarProperties;
        private UpdateAvatarProperties handlerUpdateAvatarProperties = null; // OnUpdateAvatarProperties;
        private ChatFromViewer handlerChatFromViewer = null; //OnChatFromViewer;
        private ChatFromViewer handlerChatFromViewer2 = null; //OnChatFromViewer;
        private ImprovedInstantMessage handlerInstantMessage = null; //OnInstantMessage;
        private FriendActionDelegate handlerApproveFriendRequest = null; //OnApproveFriendRequest;
        private FriendshipTermination handlerTerminateFriendship = null; //OnTerminateFriendship;
        private RezObject handlerRezObject = null; //OnRezObject;
        private GenericCall4 handlerDeRezObject = null; //OnDeRezObject;
        private ModifyTerrain handlerModifyTerrain = null;
        private Action<IClientAPI> handlerRegionHandShakeReply = null; //OnRegionHandShakeReply;
        private GenericCall2 handlerRequestWearables = null; //OnRequestWearables;
        private Action<IClientAPI> handlerRequestAvatarsData = null; //OnRequestAvatarsData;
        private SetAppearance handlerSetAppearance = null; //OnSetAppearance;
        private AvatarNowWearing handlerAvatarNowWearing = null; //OnAvatarNowWearing;
        private RezSingleAttachmentFromInv handlerRezSingleAttachment = null; //OnRezSingleAttachmentFromInv;
        private ObjectAttach handlerObjectAttach = null; //OnObjectAttach;
        private SetAlwaysRun handlerSetAlwaysRun = null; //OnSetAlwaysRun;
        private GenericCall2 handlerCompleteMovementToRegion = null; //OnCompleteMovementToRegion;
        private UpdateAgent handlerAgentUpdate = null; //OnAgentUpdate;
        private StartAnim handlerStartAnim = null;
        private StopAnim handlerStopAnim = null;
        private AgentRequestSit handlerAgentRequestSit = null; //OnAgentRequestSit;
        private AgentSit handlerAgentSit = null; //OnAgentSit;
        private AvatarPickerRequest handlerAvatarPickerRequest = null; //OnAvatarPickerRequest;
        private FetchInventory handlerAgentDataUpdateRequest = null; //OnAgentDataUpdateRequest;
        private FetchInventory handlerUserInfoRequest = null; //OnUserInfoRequest;
        private TeleportLocationRequest handlerSetStartLocationRequest = null; //OnSetStartLocationRequest;
        private LinkObjects handlerLinkObjects = null; //OnLinkObjects;
        private DelinkObjects handlerDelinkObjects = null; //OnDelinkObjects;
        private AddNewPrim handlerAddPrim = null; //OnAddPrim;
        private UpdateShape handlerUpdatePrimShape = null; //null;
        private ObjectExtraParams handlerUpdateExtraParams = null; //OnUpdateExtraParams;
        private ObjectDuplicate handlerObjectDuplicate = null;
        private ObjectSelect handlerObjectSelect = null;
        private ObjectDeselect handlerObjectDeselect = null;
        private UpdatePrimFlags handlerUpdatePrimFlags = null; //OnUpdatePrimFlags;
        private UpdatePrimTexture handlerUpdatePrimTexture = null;
        private UpdateVector handlerGrabObject = null; //OnGrabObject;
        private MoveObject handlerGrabUpdate = null; //OnGrabUpdate;
        private ObjectSelect handlerDeGrabObject = null; //OnDeGrabObject;
        private GenericCall7 handlerObjectDescription = null;
        private GenericCall7 handlerObjectName = null;
        private ObjectPermissions handlerObjectPermissions = null;
        private RequestObjectPropertiesFamily handlerRequestObjectPropertiesFamily = null; //OnRequestObjectPropertiesFamily;
        private TextureRequest handlerTextureRequest = null;
        private UDPAssetUploadRequest handlerAssetUploadRequest = null; //OnAssetUploadRequest;
        private RequestXfer handlerRequestXfer = null; //OnRequestXfer;
        private XferReceive handlerXferReceive = null; //OnXferReceive;
        private ConfirmXfer handlerConfirmXfer = null; //OnConfirmXfer;
        private CreateInventoryFolder handlerCreateInventoryFolder = null; //OnCreateNewInventoryFolder;
        private UpdateInventoryFolder handlerUpdateInventoryFolder = null;
        private MoveInventoryFolder handlerMoveInventoryFolder = null;
        private CreateNewInventoryItem handlerCreateNewInventoryItem = null; //OnCreateNewInventoryItem;
        private FetchInventory handlerFetchInventory = null;
        private FetchInventoryDescendents handlerFetchInventoryDescendents = null; //OnFetchInventoryDescendents;
        private PurgeInventoryDescendents handlerPurgeInventoryDescendents = null; //OnPurgeInventoryDescendents;
        private UpdateInventoryItem handlerUpdateInventoryItem = null;
        private CopyInventoryItem handlerCopyInventoryItem = null;
        private MoveInventoryItem handlerMoveInventoryItem = null;
        private RemoveInventoryItem handlerRemoveInventoryItem = null;
        private RemoveInventoryFolder handlerRemoveInventoryFolder = null;
        private RequestTaskInventory handlerRequestTaskInventory = null; //OnRequestTaskInventory;
        private UpdateTaskInventory handlerUpdateTaskInventory = null; //OnUpdateTaskInventory;
        private RemoveTaskInventory handlerRemoveTaskItem = null; //OnRemoveTaskItem;
        private RezScript handlerRezScript = null; //OnRezScript;
        private RequestMapBlocks handlerRequestMapBlocks = null; //OnRequestMapBlocks;
        private RequestMapName handlerMapNameRequest = null; //OnMapNameRequest;
        private TeleportLocationRequest handlerTeleportLocationRequest = null; //OnTeleportLocationRequest;
        private MoneyBalanceRequest handlerMoneyBalanceRequest = null; //OnMoneyBalanceRequest;
        private UUIDNameRequest handlerNameRequest = null;
        private ParcelAccessListRequest handlerParcelAccessListRequest = null; //OnParcelAccessListRequest;
        private ParcelAccessListUpdateRequest handlerParcelAccessListUpdateRequest = null; //OnParcelAccessListUpdateRequest;
        private ParcelPropertiesRequest handlerParcelPropertiesRequest = null; //OnParcelPropertiesRequest;
        private ParcelDivideRequest handlerParcelDivideRequest = null; //OnParcelDivideRequest;
        private ParcelJoinRequest handlerParcelJoinRequest = null; //OnParcelJoinRequest;
        private ParcelPropertiesUpdateRequest handlerParcelPropertiesUpdateRequest = null; //OnParcelPropertiesUpdateRequest;
        private ParcelSelectObjects handlerParcelSelectObjects = null; //OnParcelSelectObjects;
        private ParcelObjectOwnerRequest handlerParcelObjectOwnerRequest = null; //OnParcelObjectOwnerRequest;
        private EstateOwnerMessageRequest handlerEstateOwnerMessage = null; //OnEstateOwnerMessage;
        private RegionInfoRequest handlerRegionInfoRequest = null; //OnRegionInfoRequest;
        private EstateCovenantRequest handlerEstateCovenantRequest = null; //OnEstateCovenantRequest;
        private RequestGodlikePowers handlerReqGodlikePowers = null; //OnRequestGodlikePowers;
        private GodKickUser handlerGodKickUser = null; //OnGodKickUser;
        private ViewerEffectEventHandler handlerViewerEffect = null; //OnViewerEffect;
        private Action<IClientAPI> handlerLogout = null; //OnLogout;
        private MoneyTransferRequest handlerMoneyTransferRequest = null; //OnMoneyTransferRequest;
        private UpdateVector handlerUpdatePrimSinglePosition = null; //OnUpdatePrimSinglePosition;
        private UpdatePrimSingleRotation handlerUpdatePrimSingleRotation = null; //OnUpdatePrimSingleRotation;
        private UpdateVector handlerUpdatePrimScale = null; //OnUpdatePrimScale;
        private UpdateVector handlerUpdateVector = null; //OnUpdatePrimGroupPosition;
        private UpdatePrimRotation handlerUpdatePrimRotation = null; //OnUpdatePrimGroupRotation;
        private UpdatePrimGroupRotation handlerUpdatePrimGroupRotation = null; //OnUpdatePrimGroupMouseRotation;
        private PacketStats handlerPacketStats = null; // OnPacketStats;#
        private RequestAsset handlerRequestAsset = null; // OnRequestAsset;

        /* Properties */

        public LLUUID SecureSessionId
        {
            get { return m_secureSessionId; }
        }

        public IScene Scene
        {
            get { return m_scene; }
        }

        public LLUUID SessionId
        {
            get { return m_sessionId; }
        }

        public LLVector3 StartPos
        {
            get { return m_startpos; }
            set { m_startpos = value; }
        }

        public LLUUID AgentId
        {
            get { return m_agentId; }
        }

        /// <summary>
        /// This is a utility method used by single states to not duplicate kicks and blue card of death messages.
        /// </summary>
        public bool ChildAgentStatus()
        {
            return m_scene.PresenceChildStatus(AgentId);
        }

        /// <summary>
        /// First name of the agent/avatar represented by the client
        /// </summary>
        public string FirstName
        {
            get { return m_firstName; }
        }

        /// <summary>
        /// Last name of the agent/avatar represented by the client
        /// </summary>
        public string LastName
        {
            get { return m_lastName; }
        }

        /// <summary>
        /// Full name of the client (first name and last name)
        /// </summary>
        public string Name
        {
            get { return FirstName + " " + LastName; }
        }

        public uint CircuitCode
        {
            get { return m_circuitCode; }
        }

        public int MoneyBalance
        {
            get { return m_moneyBalance; }
        }

        /* METHODS */

        public ClientView(EndPoint remoteEP, IScene scene, AssetCache assetCache, PacketServer packServer,
                          AgentCircuitManager authenSessions, LLUUID agentId, LLUUID sessionId, uint circuitCode, EndPoint proxyEP) 
        {
            m_moneyBalance = 1000;

            m_channelVersion = Helpers.StringToField(scene.GetSimulatorVersion());

            m_scene = scene;
            m_assetCache = assetCache;

            m_networkServer = packServer;
            // m_inventoryCache = inventoryCache;
            m_authenticateSessionsHandler = authenSessions;

            m_log.Info("[CLIENT]: Started up new client thread to handle incoming request");

            m_agentId = agentId;
            m_sessionId = sessionId;
            m_circuitCode = circuitCode;

            m_userEndPoint = remoteEP;
            m_proxyEndPoint = proxyEP;

            m_startpos = m_authenticateSessionsHandler.GetPosition(circuitCode);

            // While working on this, the BlockingQueue had me fooled for a bit.
            // The Blocking queue causes the thread to stop until there's something 
            // in it to process.  It's an on-purpose threadlock though because 
            // without it, the clientloop will suck up all sim resources.

            m_packetQueue = new PacketQueue(agentId);

            RegisterLocalPacketHandlers();

            m_clientThread = new Thread(new ThreadStart(AuthUser));
            m_clientThread.Name = "ClientThread";
            m_clientThread.IsBackground = true;
            m_clientThread.Start();
            OpenSim.Framework.ThreadTracker.Add(m_clientThread);
        }

        public void SetDebug(int newDebug)
        {
            m_debug = newDebug;
        }

        # region Client Methods

        private void CloseCleanup(bool shutdownCircuit)
        {
            m_scene.RemoveClient(AgentId);

            //m_log.InfoFormat("[CLIENTVIEW] Memory pre  GC {0}", System.GC.GetTotalMemory(false));
            //m_log.InfoFormat("[CLIENTVIEW] Memory post GC {0}", System.GC.GetTotalMemory(true));   

            // Send the STOP packet 
            DisableSimulatorPacket disable = (DisableSimulatorPacket)PacketPool.Instance.GetPacket(PacketType.DisableSimulator);
            OutPacket(disable, ThrottleOutPacketType.Unknown);

            m_packetQueue.Close();

            Thread.Sleep(2000);


            // Shut down timers
            m_ackTimer.Stop();
            m_clientPingTimer.Stop();

            // This is just to give the client a reasonable chance of
            // flushing out all it's packets.  There should probably
            // be a better mechanism here

            // We can't reach into other scenes and close the connection 
            // We need to do this over grid communications
            //m_scene.CloseAllAgents(CircuitCode);

            // If we're not shutting down the circuit, then this is the last time we'll go here.
            // If we are shutting down the circuit, the UDP Server will come back here with 
            // ShutDownCircuit = false
            if (!(shutdownCircuit))
            {
                GC.Collect();
                m_clientThread.Abort();
            }
        }

        /// <summary>
        /// Close down the client view.  This *must* be the last method called, since the last  #
        /// statement of CloseCleanup() aborts the thread.
        /// </summary>
        /// <param name="shutdownCircuit"></param>
        public void Close(bool shutdownCircuit)
        {
            // Pull Client out of Region
            m_log.Info("[CLIENT]: Close has been called");
            m_packetQueue.Flush();

            //raiseevent on the packet server to Shutdown the circuit
            if (shutdownCircuit)
            {
                OnConnectionClosed(this);
            }

            CloseCleanup(shutdownCircuit);
        }

        public void Kick(string message)
        {
            if (!ChildAgentStatus())
            {
                KickUserPacket kupack = (KickUserPacket)PacketPool.Instance.GetPacket(PacketType.KickUser);
                kupack.UserInfo.AgentID = AgentId;
                kupack.UserInfo.SessionID = SessionId;
                kupack.TargetBlock.TargetIP = (uint)0;
                kupack.TargetBlock.TargetPort = (ushort)0;
                kupack.UserInfo.Reason = Helpers.StringToField(message);
                OutPacket(kupack, ThrottleOutPacketType.Task);
            }
        }

        public void Stop()
        {
            // Shut down timers
            m_ackTimer.Stop();
            m_clientPingTimer.Stop();
        }

        public void Restart()
        {
            // re-construct
            m_pendingAcks = new Dictionary<uint, uint>();
            m_needAck = new Dictionary<uint, Packet>();
            m_sequence += 1000000;

            m_ackTimer = new Timer(750);
            m_ackTimer.Elapsed += new ElapsedEventHandler(AckTimer_Elapsed);
            m_ackTimer.Start();

            m_clientPingTimer = new Timer(5000);
            m_clientPingTimer.Elapsed += new ElapsedEventHandler(CheckClientConnectivity);
            m_clientPingTimer.Enabled = true;
        }

        public void Terminate()
        {
            // disable blocking queue
            m_packetQueue.Enqueue(null);

            // wait for thread stoped
            m_clientThread.Join();

            // delete circuit code
            m_networkServer.CloseClient(this);
        }

        #endregion

        # region Packet Handling

        public static bool AddPacketHandler(PacketType packetType, PacketMethod handler)
        {
            bool result = false;
            lock (PacketHandlers)
            {
                if (!PacketHandlers.ContainsKey(packetType))
                {
                    PacketHandlers.Add(packetType, handler);
                    result = true;
                }
            }
            return result;
        }

        public bool AddLocalPacketHandler(PacketType packetType, PacketMethod handler)
        {
            bool result = false;
            lock (m_packetHandlers)
            {
                if (!m_packetHandlers.ContainsKey(packetType))
                {
                    m_packetHandlers.Add(packetType, handler);
                    result = true;
                }
            }
            return result;
        }

        protected virtual bool ProcessPacketMethod(Packet packet)
        {
            bool result = false;
            bool found = false;
            PacketMethod method;
            if (m_packetHandlers.TryGetValue(packet.Type, out method))
            {
                //there is a local handler for this packet type
                result = method(this, packet);
            }
            else
            {
                //there is not a local handler so see if there is a Global handler
                lock (PacketHandlers)
                {
                    found = PacketHandlers.TryGetValue(packet.Type, out method);
                }
                if (found)
                {
                    result = method(this, packet);
                }
            }
            return result;
        }

        protected void DebugPacket(string direction, Packet packet)
        {
            if (m_debug > 0)
            {
                string info = String.Empty;

                if (m_debug < 255 && packet.Type == PacketType.AgentUpdate)
                    return;
                if (m_debug < 254 && packet.Type == PacketType.ViewerEffect)
                    return;
                if (m_debug < 253 && (
                                         packet.Type == PacketType.CompletePingCheck ||
                                         packet.Type == PacketType.StartPingCheck
                                     ))
                    return;
                if (m_debug < 252 && packet.Type == PacketType.PacketAck)
                    return;

                if (m_debug > 1)
                {
                    info = packet.ToString();
                }
                else
                {
                    info = packet.Type.ToString();
                }
                Console.WriteLine(m_circuitCode + ":" + direction + ": " + info);
            }
        }

        protected virtual void ClientLoop()
        {
            m_log.Info("[CLIENT]: Entered loop");
            while (true)
            {
                QueItem nextPacket = m_packetQueue.Dequeue();
                if (nextPacket == null)
                {
                    break;
                }
                if (nextPacket.Incoming)
                {
                    if (nextPacket.Packet.Type != PacketType.AgentUpdate)
                    {
                        m_packetsReceived++;
                    }
                    DebugPacket("IN", nextPacket.Packet);
                    ProcessInPacket(nextPacket.Packet);
                }
                else
                {
                    DebugPacket("OUT", nextPacket.Packet);
                    ProcessOutPacket(nextPacket.Packet);
                }
            }
        }

        # endregion

        protected void CheckClientConnectivity(object sender, ElapsedEventArgs e)
        {
            if (m_packetsReceived == m_lastPacketsReceived)
            {
                m_probesWithNoIngressPackets++;
                if (m_probesWithNoIngressPackets > 30)
                {
                    if (OnConnectionClosed != null)
                    {
                        OnConnectionClosed(this);
                    }
                }
                else
                {
                    // this will normally trigger at least one packet (ping response)
                    SendStartPingCheck(0);

                }
            }
            else
            {
                // Something received in the meantime - we can reset the counters
                m_probesWithNoIngressPackets = 0;
                m_lastPacketsReceived = m_packetsReceived;

            }
            //SendPacketStats();
        }

        # region Setup

        protected virtual void InitNewClient()
        {
            //this.UploadAssets = new AgentAssetUpload(this, m_assetCache, m_inventoryCache);

            // Establish our two timers.  We could probably get this down to one 
            m_ackTimer = new Timer(750);
            m_ackTimer.Elapsed += new ElapsedEventHandler(AckTimer_Elapsed);
            m_ackTimer.Start();

            m_clientPingTimer = new Timer(5000);
            m_clientPingTimer.Elapsed += new ElapsedEventHandler(CheckClientConnectivity);
            m_clientPingTimer.Enabled = true;

            m_log.Info("[CLIENT]: Adding viewer agent to scene");
            m_scene.AddNewClient(this, true);
        }

        protected virtual void AuthUser()
        {
            // AuthenticateResponse sessionInfo = m_gridServer.AuthenticateSession(m_cirpack.m_circuitCode.m_sessionId, m_cirpack.m_circuitCode.ID, m_cirpack.m_circuitCode.Code);
            AuthenticateResponse sessionInfo =
                m_authenticateSessionsHandler.AuthenticateSession(m_sessionId, m_agentId,
                                                                  m_circuitCode);
            if (!sessionInfo.Authorised)
            {
                //session/circuit not authorised
                m_log.Info("[CLIENT]: New user request denied to " + m_userEndPoint.ToString());
                m_packetQueue.Close();
                m_clientThread.Abort();
            }
            else
            {
                m_log.Info("[CLIENT]: Got authenticated connection from " + m_userEndPoint.ToString());
                //session is authorised
                m_firstName = sessionInfo.LoginInfo.First;
                m_lastName = sessionInfo.LoginInfo.Last;

                if (sessionInfo.LoginInfo.SecureSession != LLUUID.Zero)
                {
                    m_secureSessionId = sessionInfo.LoginInfo.SecureSession;
                }
                // This sets up all the timers
                InitNewClient();

                ClientLoop();
            }
        }

        # endregion

        // Previously ClientView.API partial class
        public event Action<IClientAPI> OnLogout;
        public event ObjectPermissions OnObjectPermissions;

        public event Action<IClientAPI> OnConnectionClosed;
        public event ViewerEffectEventHandler OnViewerEffect;
        public event ImprovedInstantMessage OnInstantMessage;
        public event ChatFromViewer OnChatFromViewer;
        public event TextureRequest OnRequestTexture;
        public event RezObject OnRezObject;
        public event GenericCall4 OnDeRezObject;
        public event ModifyTerrain OnModifyTerrain;
        public event Action<IClientAPI> OnRegionHandShakeReply;
        public event GenericCall2 OnRequestWearables;
        public event SetAppearance OnSetAppearance;
        public event AvatarNowWearing OnAvatarNowWearing;
        public event RezSingleAttachmentFromInv OnRezSingleAttachmentFromInv;
        public event ObjectAttach OnObjectAttach;
        public event GenericCall2 OnCompleteMovementToRegion;
        public event UpdateAgent OnAgentUpdate;
        public event AgentRequestSit OnAgentRequestSit;
        public event AgentSit OnAgentSit;
        public event AvatarPickerRequest OnAvatarPickerRequest;
        public event StartAnim OnStartAnim;
        public event StopAnim OnStopAnim;
        public event Action<IClientAPI> OnRequestAvatarsData;
        public event LinkObjects OnLinkObjects;
        public event DelinkObjects OnDelinkObjects;
        public event UpdateVector OnGrabObject;
        public event ObjectSelect OnDeGrabObject;
        public event ObjectDuplicate OnObjectDuplicate;
        public event MoveObject OnGrabUpdate;
        public event AddNewPrim OnAddPrim;
        public event RequestGodlikePowers OnRequestGodlikePowers;
        public event GodKickUser OnGodKickUser;
        public event ObjectExtraParams OnUpdateExtraParams;
        public event UpdateShape OnUpdatePrimShape;
        public event ObjectSelect OnObjectSelect;
        public event ObjectDeselect OnObjectDeselect;
        public event GenericCall7 OnObjectDescription;
        public event GenericCall7 OnObjectName;
        public event RequestObjectPropertiesFamily OnRequestObjectPropertiesFamily;
        public event UpdatePrimFlags OnUpdatePrimFlags;
        public event UpdatePrimTexture OnUpdatePrimTexture;
        public event UpdateVector OnUpdatePrimGroupPosition;
        public event UpdateVector OnUpdatePrimSinglePosition;
        public event UpdatePrimRotation OnUpdatePrimGroupRotation;
        public event UpdatePrimSingleRotation OnUpdatePrimSingleRotation;
        public event UpdatePrimGroupRotation OnUpdatePrimGroupMouseRotation;
        public event UpdateVector OnUpdatePrimScale;
        public event StatusChange OnChildAgentStatus;
        public event GenericCall2 OnStopMovement;
        public event Action<LLUUID> OnRemoveAvatar;
        public event RequestMapBlocks OnRequestMapBlocks;
        public event RequestMapName OnMapNameRequest;
        public event TeleportLocationRequest OnTeleportLocationRequest;
        public event DisconnectUser OnDisconnectUser;
        public event RequestAvatarProperties OnRequestAvatarProperties;
        public event SetAlwaysRun OnSetAlwaysRun;

        public event FetchInventory OnAgentDataUpdateRequest;
        public event FetchInventory OnUserInfoRequest;
        public event TeleportLocationRequest OnSetStartLocationRequest;
        public event UpdateAvatarProperties OnUpdateAvatarProperties;


        public event CreateNewInventoryItem OnCreateNewInventoryItem;
        public event CreateInventoryFolder OnCreateNewInventoryFolder;
        public event UpdateInventoryFolder OnUpdateInventoryFolder;
        public event MoveInventoryFolder OnMoveInventoryFolder;
        public event FetchInventoryDescendents OnFetchInventoryDescendents;
        public event PurgeInventoryDescendents OnPurgeInventoryDescendents;
        public event FetchInventory OnFetchInventory;
        public event RequestTaskInventory OnRequestTaskInventory;
        public event UpdateInventoryItem OnUpdateInventoryItem;
        public event CopyInventoryItem OnCopyInventoryItem;
        public event MoveInventoryItem OnMoveInventoryItem;
        public event RemoveInventoryItem OnRemoveInventoryItem;
        public event RemoveInventoryFolder OnRemoveInventoryFolder;
        public event UDPAssetUploadRequest OnAssetUploadRequest;
        public event XferReceive OnXferReceive;
        public event RequestXfer OnRequestXfer;
        public event ConfirmXfer OnConfirmXfer;
        public event RezScript OnRezScript;
        public event UpdateTaskInventory OnUpdateTaskInventory;
        public event RemoveTaskInventory OnRemoveTaskItem;
        public event RequestAsset OnRequestAsset;

        public event UUIDNameRequest OnNameFromUUIDRequest;

        public event ParcelAccessListRequest OnParcelAccessListRequest;
        public event ParcelAccessListUpdateRequest OnParcelAccessListUpdateRequest;
        public event ParcelPropertiesRequest OnParcelPropertiesRequest;
        public event ParcelDivideRequest OnParcelDivideRequest;
        public event ParcelJoinRequest OnParcelJoinRequest;
        public event ParcelPropertiesUpdateRequest OnParcelPropertiesUpdateRequest;
        public event ParcelSelectObjects OnParcelSelectObjects;
        public event ParcelObjectOwnerRequest OnParcelObjectOwnerRequest;
        public event EstateOwnerMessageRequest OnEstateOwnerMessage;
        public event RegionInfoRequest OnRegionInfoRequest;
        public event EstateCovenantRequest OnEstateCovenantRequest;

        public event FriendActionDelegate OnApproveFriendRequest;
        public event FriendActionDelegate OnDenyFriendRequest;
        public event FriendshipTermination OnTerminateFriendship;

        public event PacketStats OnPacketStats;

        public event MoneyTransferRequest OnMoneyTransferRequest;

        public event MoneyBalanceRequest OnMoneyBalanceRequest;


        #region Scene/Avatar to Client

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionInfo"></param>
        public void SendRegionHandshake(RegionInfo regionInfo)
        {
            RegionHandshakePacket handshake = (RegionHandshakePacket)PacketPool.Instance.GetPacket(PacketType.RegionHandshake);

            bool estatemanager = false;
            LLUUID[] EstateManagers = regionInfo.EstateSettings.estateManagers;
            for (int i = 0; i < EstateManagers.Length; i++)
            {
                if (EstateManagers[i] == AgentId)
                    estatemanager = true;
            }

            handshake.RegionInfo.BillableFactor = regionInfo.EstateSettings.billableFactor;
            handshake.RegionInfo.IsEstateManager = estatemanager;
            handshake.RegionInfo.TerrainHeightRange00 = regionInfo.EstateSettings.terrainHeightRange0;
            handshake.RegionInfo.TerrainHeightRange01 = regionInfo.EstateSettings.terrainHeightRange1;
            handshake.RegionInfo.TerrainHeightRange10 = regionInfo.EstateSettings.terrainHeightRange2;
            handshake.RegionInfo.TerrainHeightRange11 = regionInfo.EstateSettings.terrainHeightRange3;
            handshake.RegionInfo.TerrainStartHeight00 = regionInfo.EstateSettings.terrainStartHeight0;
            handshake.RegionInfo.TerrainStartHeight01 = regionInfo.EstateSettings.terrainStartHeight1;
            handshake.RegionInfo.TerrainStartHeight10 = regionInfo.EstateSettings.terrainStartHeight2;
            handshake.RegionInfo.TerrainStartHeight11 = regionInfo.EstateSettings.terrainStartHeight3;
            handshake.RegionInfo.SimAccess = (byte)regionInfo.EstateSettings.simAccess;
            handshake.RegionInfo.WaterHeight = regionInfo.EstateSettings.waterHeight;

            handshake.RegionInfo.RegionFlags = (uint)regionInfo.EstateSettings.regionFlags;
            handshake.RegionInfo.SimName = Helpers.StringToField(regionInfo.RegionName);
            handshake.RegionInfo.SimOwner = regionInfo.MasterAvatarAssignedUUID;
            handshake.RegionInfo.TerrainBase0 = regionInfo.EstateSettings.terrainBase0;
            handshake.RegionInfo.TerrainBase1 = regionInfo.EstateSettings.terrainBase1;
            handshake.RegionInfo.TerrainBase2 = regionInfo.EstateSettings.terrainBase2;
            handshake.RegionInfo.TerrainBase3 = regionInfo.EstateSettings.terrainBase3;
            handshake.RegionInfo.TerrainDetail0 = regionInfo.EstateSettings.terrainDetail0;
            handshake.RegionInfo.TerrainDetail1 = regionInfo.EstateSettings.terrainDetail1;
            handshake.RegionInfo.TerrainDetail2 = regionInfo.EstateSettings.terrainDetail2;
            handshake.RegionInfo.TerrainDetail3 = regionInfo.EstateSettings.terrainDetail3;
            handshake.RegionInfo.CacheID = LLUUID.Random(); //I guess this is for the client to remember an old setting?

            OutPacket(handshake, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regInfo"></param>
        public void MoveAgentIntoRegion(RegionInfo regInfo, LLVector3 pos, LLVector3 look)
        {
            AgentMovementCompletePacket mov = (AgentMovementCompletePacket)PacketPool.Instance.GetPacket(PacketType.AgentMovementComplete);
            mov.SimData.ChannelVersion = m_channelVersion;
            mov.AgentData.SessionID = m_sessionId;
            mov.AgentData.AgentID = AgentId;
            mov.Data.RegionHandle = regInfo.RegionHandle;
            mov.Data.Timestamp = 1172750370; // TODO - dynamicalise this

            if ((pos.X == 0) && (pos.Y == 0) && (pos.Z == 0))
            {
                mov.Data.Position = m_startpos;
            }
            else
            {
                mov.Data.Position = pos;
            }
            mov.Data.LookAt = look;

            // Hack to get this out immediately and skip the throttles
            OutPacket(mov, ThrottleOutPacketType.Unknown);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        /// <param name="fromPos"></param>
        /// <param name="fromName"></param>
        /// <param name="fromAgentID"></param>
        public void SendChatMessage(string message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID)
        {
            SendChatMessage(Helpers.StringToField(message), type, fromPos, fromName, fromAgentID);
        }

        public void SendChatMessage(byte[] message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID)
        {
            ChatFromSimulatorPacket reply = (ChatFromSimulatorPacket)PacketPool.Instance.GetPacket(PacketType.ChatFromSimulator);
            reply.ChatData.Audible = 1;
            reply.ChatData.Message = message;
            reply.ChatData.ChatType = type;
            reply.ChatData.SourceType = 1;
            reply.ChatData.Position = fromPos;
            reply.ChatData.FromName = Helpers.StringToField(fromName);
            reply.ChatData.OwnerID = fromAgentID;
            reply.ChatData.SourceID = fromAgentID;

            OutPacket(reply, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="target"></param>
        public void SendInstantMessage(LLUUID fromAgent, LLUUID fromAgentSession, string message, LLUUID toAgent,
                                       LLUUID imSessionID, string fromName, byte dialog, uint timeStamp)
        {
            ImprovedInstantMessagePacket msg = (ImprovedInstantMessagePacket)PacketPool.Instance.GetPacket(PacketType.ImprovedInstantMessage);
            msg.AgentData.AgentID = fromAgent;
            msg.AgentData.SessionID = fromAgentSession;
            msg.MessageBlock.FromAgentName = Helpers.StringToField(fromName);
            msg.MessageBlock.Dialog = dialog;
            msg.MessageBlock.FromGroup = false;
            msg.MessageBlock.ID = imSessionID;
            msg.MessageBlock.Offline = 0;
            msg.MessageBlock.ParentEstateID = 0;
            msg.MessageBlock.Position = new LLVector3();
            msg.MessageBlock.RegionID = LLUUID.Random();
            msg.MessageBlock.Timestamp = timeStamp;
            msg.MessageBlock.ToAgentID = toAgent;
            msg.MessageBlock.Message = Helpers.StringToField(message);
            msg.MessageBlock.BinaryBucket = new byte[0];

            OutPacket(msg, ThrottleOutPacketType.Task);
        }

        /// <summary>
        ///  Send the region heightmap to the client
        /// </summary>
        /// <param name="map">heightmap</param>
        public virtual void SendLayerData(float[] map)
        {
            try
            {
                int[] patches = new int[4];

                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x += 4)
                    {
                        patches[0] = x + 0 + y * 16;
                        patches[1] = x + 1 + y * 16;
                        patches[2] = x + 2 + y * 16;
                        patches[3] = x + 3 + y * 16;

                        Packet layerpack = TerrainManager.CreateLandPacket(map, patches);
                        OutPacket(layerpack, ThrottleOutPacketType.Land);
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Warn("[client]: " +
                                      "ClientView.API.cs: SendLayerData() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Sends a specified patch to a client
        /// </summary>
        /// <param name="px">Patch coordinate (x) 0..16</param>
        /// <param name="py">Patch coordinate (y) 0..16</param>
        /// <param name="map">heightmap</param>
        public void SendLayerData(int px, int py, float[] map)
        {
            try
            {
                int[] patches = new int[1];
                int patchx, patchy;
                patchx = px;
                patchy = py;

                patches[0] = patchx + 0 + patchy * 16;

                Packet layerpack = TerrainManager.CreateLandPacket(map, patches);
                OutPacket(layerpack, ThrottleOutPacketType.Land);
            }
            catch (Exception e)
            {
                m_log.Warn("[client]: " +
                                      "ClientView.API.cs: SendLayerData() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="neighbourHandle"></param>
        /// <param name="neighbourIP"></param>
        /// <param name="neighbourPort"></param>
        public void InformClientOfNeighbour(ulong neighbourHandle, IPEndPoint neighbourEndPoint)
        {
            IPAddress neighbourIP = neighbourEndPoint.Address;
            ushort neighbourPort = (ushort)neighbourEndPoint.Port;

            EnableSimulatorPacket enablesimpacket = (EnableSimulatorPacket)PacketPool.Instance.GetPacket(PacketType.EnableSimulator);
            // TODO: don't create new blocks if recycling an old packet
            enablesimpacket.SimulatorInfo = new EnableSimulatorPacket.SimulatorInfoBlock();
            enablesimpacket.SimulatorInfo.Handle = neighbourHandle;

            byte[] byteIP = neighbourIP.GetAddressBytes();
            enablesimpacket.SimulatorInfo.IP = (uint)byteIP[3] << 24;
            enablesimpacket.SimulatorInfo.IP += (uint)byteIP[2] << 16;
            enablesimpacket.SimulatorInfo.IP += (uint)byteIP[1] << 8;
            enablesimpacket.SimulatorInfo.IP += (uint)byteIP[0];
            enablesimpacket.SimulatorInfo.Port = neighbourPort;
            OutPacket(enablesimpacket, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public AgentCircuitData RequestClientInfo()
        {
            AgentCircuitData agentData = new AgentCircuitData();
            agentData.AgentID = AgentId;
            agentData.SessionID = m_sessionId;
            agentData.SecureSessionID = SecureSessionId;
            agentData.circuitcode = m_circuitCode;
            agentData.child = false;
            agentData.firstname = m_firstName;
            agentData.lastname = m_lastName;
            agentData.CapsPath = String.Empty;
            return agentData;
        }

        public void CrossRegion(ulong newRegionHandle, LLVector3 pos, LLVector3 lookAt, IPEndPoint externalIPEndPoint,
                                string capsURL)
        {
            LLVector3 look = new LLVector3(lookAt.X * 10, lookAt.Y * 10, lookAt.Z * 10);

            //CrossedRegionPacket newSimPack = (CrossedRegionPacket)PacketPool.Instance.GetPacket(PacketType.CrossedRegion);
            CrossedRegionPacket newSimPack = new CrossedRegionPacket();
            // TODO: don't create new blocks if recycling an old packet
            newSimPack.AgentData = new CrossedRegionPacket.AgentDataBlock();
            newSimPack.AgentData.AgentID = AgentId;
            newSimPack.AgentData.SessionID = m_sessionId;
            newSimPack.Info = new CrossedRegionPacket.InfoBlock();
            newSimPack.Info.Position = pos;
            newSimPack.Info.LookAt = look;
            newSimPack.RegionData = new CrossedRegionPacket.RegionDataBlock();
            newSimPack.RegionData.RegionHandle = newRegionHandle;
            byte[] byteIP = externalIPEndPoint.Address.GetAddressBytes();
            newSimPack.RegionData.SimIP = (uint)byteIP[3] << 24;
            newSimPack.RegionData.SimIP += (uint)byteIP[2] << 16;
            newSimPack.RegionData.SimIP += (uint)byteIP[1] << 8;
            newSimPack.RegionData.SimIP += (uint)byteIP[0];
            newSimPack.RegionData.SimPort = (ushort)externalIPEndPoint.Port;
            newSimPack.RegionData.SeedCapability = Helpers.StringToField(capsURL);

            // Hack to get this out immediately and skip throttles
            OutPacket(newSimPack, ThrottleOutPacketType.Unknown);
        }

        public void SendMapBlock(List<MapBlockData> mapBlocks)
        {
            MapBlockReplyPacket mapReply = (MapBlockReplyPacket)PacketPool.Instance.GetPacket(PacketType.MapBlockReply);
            // TODO: don't create new blocks if recycling an old packet
            mapReply.AgentData.AgentID = AgentId;
            mapReply.Data = new MapBlockReplyPacket.DataBlock[mapBlocks.Count];
            mapReply.AgentData.Flags = 0;

            for (int i = 0; i < mapBlocks.Count; i++)
            {
                mapReply.Data[i] = new MapBlockReplyPacket.DataBlock();
                mapReply.Data[i].MapImageID = mapBlocks[i].MapImageId;
                mapReply.Data[i].X = mapBlocks[i].X;
                mapReply.Data[i].Y = mapBlocks[i].Y;
                mapReply.Data[i].WaterHeight = mapBlocks[i].WaterHeight;
                mapReply.Data[i].Name = Helpers.StringToField(mapBlocks[i].Name);
                mapReply.Data[i].RegionFlags = mapBlocks[i].RegionFlags;
                mapReply.Data[i].Access = mapBlocks[i].Access;
                mapReply.Data[i].Agents = mapBlocks[i].Agents;
            }
            OutPacket(mapReply, ThrottleOutPacketType.Land);
        }

        public void SendLocalTeleport(LLVector3 position, LLVector3 lookAt, uint flags)
        {
            TeleportLocalPacket tpLocal = (TeleportLocalPacket)PacketPool.Instance.GetPacket(PacketType.TeleportLocal);
            tpLocal.Info.AgentID = AgentId;
            tpLocal.Info.TeleportFlags = flags;
            tpLocal.Info.LocationID = 2;
            tpLocal.Info.LookAt = lookAt;
            tpLocal.Info.Position = position;

            // Hack to get this out immediately and skip throttles
            OutPacket(tpLocal, ThrottleOutPacketType.Unknown);
        }

        public void SendRegionTeleport(ulong regionHandle, byte simAccess, IPEndPoint newRegionEndPoint, uint locationID,
                                       uint flags, string capsURL)
        {
            //TeleportFinishPacket teleport = (TeleportFinishPacket)PacketPool.Instance.GetPacket(PacketType.TeleportFinish);

            TeleportFinishPacket teleport = new TeleportFinishPacket();
            teleport.Info.AgentID = AgentId;
            teleport.Info.RegionHandle = regionHandle;
            teleport.Info.SimAccess = simAccess;

            teleport.Info.SeedCapability = Helpers.StringToField(capsURL);

            IPAddress oIP = newRegionEndPoint.Address;
            byte[] byteIP = oIP.GetAddressBytes();
            uint ip = (uint)byteIP[3] << 24;
            ip += (uint)byteIP[2] << 16;
            ip += (uint)byteIP[1] << 8;
            ip += (uint)byteIP[0];

            teleport.Info.SimIP = ip;
            teleport.Info.SimPort = (ushort)newRegionEndPoint.Port;
            teleport.Info.LocationID = 4;
            teleport.Info.TeleportFlags = 1 << 4;

            // Hack to get this out immediately and skip throttles.
            OutPacket(teleport, ThrottleOutPacketType.Unknown);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendTeleportFailed(string reason)
        {
            TeleportFailedPacket tpFailed = (TeleportFailedPacket)PacketPool.Instance.GetPacket(PacketType.TeleportFailed);
            tpFailed.Info.AgentID = AgentId;
            tpFailed.Info.Reason = Helpers.StringToField(reason);

            // Hack to get this out immediately and skip throttles
            OutPacket(tpFailed, ThrottleOutPacketType.Unknown);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendTeleportLocationStart()
        {
            //TeleportStartPacket tpStart = (TeleportStartPacket)PacketPool.Instance.GetPacket(PacketType.TeleportStart);
            TeleportStartPacket tpStart = new TeleportStartPacket();
            tpStart.Info.TeleportFlags = 16; // Teleport via location

            // Hack to get this out immediately and skip throttles
            OutPacket(tpStart, ThrottleOutPacketType.Unknown);
        }

        public void SendMoneyBalance(LLUUID transaction, bool success, byte[] description, int balance)
        {
            MoneyBalanceReplyPacket money = (MoneyBalanceReplyPacket)PacketPool.Instance.GetPacket(PacketType.MoneyBalanceReply);
            money.MoneyData.AgentID = AgentId;
            money.MoneyData.TransactionID = transaction;
            money.MoneyData.TransactionSuccess = success;
            money.MoneyData.Description = description;
            money.MoneyData.MoneyBalance = balance;
            OutPacket(money, ThrottleOutPacketType.Task);
        }

        public void SendStartPingCheck(byte seq)
        {
            StartPingCheckPacket pc = (StartPingCheckPacket)PacketPool.Instance.GetPacket(PacketType.StartPingCheck);
            pc.PingID.PingID = seq;
            pc.Header.Reliable = false;
            OutPacket(pc, ThrottleOutPacketType.Unknown);
        }

        public void SendKillObject(ulong regionHandle, uint localID)
        {
            KillObjectPacket kill = (KillObjectPacket)PacketPool.Instance.GetPacket(PacketType.KillObject);
            // TODO: don't create new blocks if recycling an old packet
            kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
            kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
            kill.ObjectData[0].ID = localID;
            kill.Header.Reliable = false;
            OutPacket(kill, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// Send information about the items contained in a folder to the client.
        /// 
        /// XXX This method needs some refactoring loving
        /// </summary>
        /// <param name="ownerID">The owner of the folder</param>
        /// <param name="folderID">The id of the folder</param>
        /// <param name="items">The items contained in the folder identified by folderID</param>
        /// <param name="fetchFolders">Do we need to send folder information?</param>
        /// <param name="fetchItems">Do we need to send item information?</param>
        public void SendInventoryFolderDetails(LLUUID ownerID, LLUUID folderID, List<InventoryItemBase> items,
                                               List<InventoryFolderBase> folders,
                                               bool fetchFolders, bool fetchItems)
        {
            // An inventory descendents packet consists of a single agent section and an inventory details 
            // section for each inventory item.  The size of each inventory item is approximately 550 bytes.
            // In theory, UDP has a maximum packet size of 64k, so it should be possible to send descendent
            // packets containing metadata for in excess of 100 items.  But in practice, there may be other
            // factors (e.g. firewalls) restraining the maximum UDP packet size.  See,
            //
            // http://opensimulator.org/mantis/view.php?id=226
            //
            // for one example of this kind of thing.  In fact, the Linden servers appear to only send about
            // 6 to 7 items at a time, so let's stick with 6
            int MAX_ITEMS_PER_PACKET = 6;

            Encoding enc = Encoding.ASCII;
            InventoryDescendentsPacket descend;
            int i;
            int count;

            if (fetchItems)
            {
                descend = CreateInventoryDescendentsPacket(ownerID, folderID);

                if (items.Count < MAX_ITEMS_PER_PACKET)
                {
                    descend.ItemData = new InventoryDescendentsPacket.ItemDataBlock[items.Count];
                    descend.AgentData.Descendents = items.Count;
                }
                else
                {
                    descend.ItemData = new InventoryDescendentsPacket.ItemDataBlock[MAX_ITEMS_PER_PACKET];
                    descend.AgentData.Descendents = MAX_ITEMS_PER_PACKET;
                }

                // Even if we aren't fetching the folders, we still need to include the folder count
                // in the total number of descendents.  Failure to do so will cause subtle bugs such
                // as the failure of textures which haven't been expanded in inventory to show up
                // in the texture prim edit selection panel.
                if (!fetchFolders)
                {
                    descend.AgentData.Descendents += folders.Count;
                }

                count = 0;
                i = 0;
                foreach (InventoryItemBase item in items)
                {
                    descend.ItemData[i] = new InventoryDescendentsPacket.ItemDataBlock();
                    descend.ItemData[i].ItemID = item.inventoryID;
                    descend.ItemData[i].AssetID = item.assetID;
                    descend.ItemData[i].CreatorID = item.creatorsID;
                    descend.ItemData[i].BaseMask = item.inventoryBasePermissions;
                    descend.ItemData[i].CreationDate = 1000;
                    descend.ItemData[i].Description = Helpers.StringToField(item.inventoryDescription);
                    descend.ItemData[i].EveryoneMask = item.inventoryEveryOnePermissions;
                    descend.ItemData[i].Flags = 1;
                    descend.ItemData[i].FolderID = item.parentFolderID;
                    descend.ItemData[i].GroupID = new LLUUID("00000000-0000-0000-0000-000000000000");
                    descend.ItemData[i].GroupMask = 0;
                    descend.ItemData[i].InvType = (sbyte)item.invType;
                    descend.ItemData[i].Name = Helpers.StringToField(item.inventoryName);
                    descend.ItemData[i].NextOwnerMask = item.inventoryNextPermissions;
                    descend.ItemData[i].OwnerID = item.avatarID;
                    descend.ItemData[i].OwnerMask = item.inventoryCurrentPermissions;
                    descend.ItemData[i].SalePrice = 0;
                    descend.ItemData[i].SaleType = 0;
                    descend.ItemData[i].Type = (sbyte)item.assetType;
                    descend.ItemData[i].CRC =
                        Helpers.InventoryCRC(descend.ItemData[i].CreationDate, descend.ItemData[i].SaleType,
                                             descend.ItemData[i].InvType, descend.ItemData[i].Type,
                                             descend.ItemData[i].AssetID, descend.ItemData[i].GroupID,
                                             descend.ItemData[i].SalePrice,
                                             descend.ItemData[i].OwnerID, descend.ItemData[i].CreatorID,
                                             descend.ItemData[i].ItemID, descend.ItemData[i].FolderID,
                                             descend.ItemData[i].EveryoneMask,
                                             descend.ItemData[i].Flags, descend.ItemData[i].OwnerMask,
                                             descend.ItemData[i].GroupMask, item.inventoryCurrentPermissions);

                    i++;
                    count++;
                    if (i == MAX_ITEMS_PER_PACKET)
                    {
                        OutPacket(descend, ThrottleOutPacketType.Asset);

                        if ((items.Count - count) > 0)
                        {
                            descend = CreateInventoryDescendentsPacket(ownerID, folderID);
                            if ((items.Count - count) < MAX_ITEMS_PER_PACKET)
                            {
                                descend.ItemData = new InventoryDescendentsPacket.ItemDataBlock[items.Count - count];
                                descend.AgentData.Descendents = items.Count - count;
                            }
                            else
                            {
                                descend.ItemData = new InventoryDescendentsPacket.ItemDataBlock[MAX_ITEMS_PER_PACKET];
                                descend.AgentData.Descendents = MAX_ITEMS_PER_PACKET;
                            }
                            i = 0;
                        }
                    }
                }

                if (i < MAX_ITEMS_PER_PACKET)
                {
                    OutPacket(descend, ThrottleOutPacketType.Asset);
                }
            }

            //send subfolders

            descend = CreateInventoryDescendentsPacket(ownerID, folderID);

            if (folders.Count < MAX_ITEMS_PER_PACKET)
            {
                descend.FolderData = new InventoryDescendentsPacket.FolderDataBlock[folders.Count];
                descend.AgentData.Descendents = folders.Count;
            }
            else
            {
                descend.FolderData = new InventoryDescendentsPacket.FolderDataBlock[MAX_ITEMS_PER_PACKET];
                descend.AgentData.Descendents = MAX_ITEMS_PER_PACKET;
            }

            // Not sure if this scenario ever actually occurs, but nonetheless we include the items
            // count even if we're not sending item data for the same reasons as above.
            if (!fetchItems)
            {
                descend.AgentData.Descendents += items.Count;
            }

            i = 0;
            count = 0;
            foreach (InventoryFolderBase folder in folders)
            {
                descend.FolderData[i] = new InventoryDescendentsPacket.FolderDataBlock();
                descend.FolderData[i].FolderID = folder.folderID;
                descend.FolderData[i].Name = Helpers.StringToField(folder.name);
                descend.FolderData[i].ParentID = folder.parentID;
                descend.FolderData[i].Type = (sbyte)folder.type;

                i++;
                count++;
                if (i == MAX_ITEMS_PER_PACKET)
                {
                    OutPacket(descend, ThrottleOutPacketType.Asset);

                    if ((folders.Count - count) > 0)
                    {
                        descend = CreateInventoryDescendentsPacket(ownerID, folderID);
                        if ((folders.Count - count) < MAX_ITEMS_PER_PACKET)
                        {
                            descend.FolderData =
                                new InventoryDescendentsPacket.FolderDataBlock[folders.Count - count];
                            descend.AgentData.Descendents = folders.Count - count;
                        }
                        else
                        {
                            descend.FolderData =
                                new InventoryDescendentsPacket.FolderDataBlock[MAX_ITEMS_PER_PACKET];
                            descend.AgentData.Descendents = MAX_ITEMS_PER_PACKET;
                        }
                        i = 0;
                    }
                }
            }

            if (i < MAX_ITEMS_PER_PACKET)
            {
                OutPacket(descend, ThrottleOutPacketType.Asset);
            }

        }

        private InventoryDescendentsPacket CreateInventoryDescendentsPacket(LLUUID ownerID, LLUUID folderID)
        {
            InventoryDescendentsPacket descend = (InventoryDescendentsPacket)PacketPool.Instance.GetPacket(PacketType.InventoryDescendents);
            descend.AgentData.AgentID = AgentId;
            descend.AgentData.OwnerID = ownerID;
            descend.AgentData.FolderID = folderID;
            descend.AgentData.Version = 0;

            return descend;
        }

        public void SendInventoryItemDetails(LLUUID ownerID, InventoryItemBase item)
        {
            Encoding enc = Encoding.ASCII;
            uint FULL_MASK_PERMISSIONS = (uint)PermissionMask.All;
            FetchInventoryReplyPacket inventoryReply = (FetchInventoryReplyPacket)PacketPool.Instance.GetPacket(PacketType.FetchInventoryReply);
            // TODO: don't create new blocks if recycling an old packet
            inventoryReply.AgentData.AgentID = AgentId;
            inventoryReply.InventoryData = new FetchInventoryReplyPacket.InventoryDataBlock[1];
            inventoryReply.InventoryData[0] = new FetchInventoryReplyPacket.InventoryDataBlock();
            inventoryReply.InventoryData[0].ItemID = item.inventoryID;
            inventoryReply.InventoryData[0].AssetID = item.assetID;
            inventoryReply.InventoryData[0].CreatorID = item.creatorsID;
            inventoryReply.InventoryData[0].BaseMask = item.inventoryBasePermissions;
            inventoryReply.InventoryData[0].CreationDate =
                (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            inventoryReply.InventoryData[0].Description = Helpers.StringToField(item.inventoryDescription);
            inventoryReply.InventoryData[0].EveryoneMask = item.inventoryEveryOnePermissions;
            inventoryReply.InventoryData[0].Flags = 0;
            inventoryReply.InventoryData[0].FolderID = item.parentFolderID;
            inventoryReply.InventoryData[0].GroupID = new LLUUID("00000000-0000-0000-0000-000000000000");
            inventoryReply.InventoryData[0].GroupMask = 0;
            inventoryReply.InventoryData[0].InvType = (sbyte)item.invType;
            inventoryReply.InventoryData[0].Name = Helpers.StringToField(item.inventoryName);
            inventoryReply.InventoryData[0].NextOwnerMask = item.inventoryNextPermissions;
            inventoryReply.InventoryData[0].OwnerID = item.avatarID;
            inventoryReply.InventoryData[0].OwnerMask = item.inventoryCurrentPermissions;
            inventoryReply.InventoryData[0].SalePrice = 0;
            inventoryReply.InventoryData[0].SaleType = 0;
            inventoryReply.InventoryData[0].Type = (sbyte)item.assetType;
            inventoryReply.InventoryData[0].CRC =
                Helpers.InventoryCRC(1000, 0, inventoryReply.InventoryData[0].InvType,
                                     inventoryReply.InventoryData[0].Type, inventoryReply.InventoryData[0].AssetID,
                                     inventoryReply.InventoryData[0].GroupID, 100,
                                     inventoryReply.InventoryData[0].OwnerID, inventoryReply.InventoryData[0].CreatorID,
                                     inventoryReply.InventoryData[0].ItemID, inventoryReply.InventoryData[0].FolderID,
                                     FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS,
                                     FULL_MASK_PERMISSIONS);

            OutPacket(inventoryReply, ThrottleOutPacketType.Asset);
        }

        /// <see>IClientAPI.SendInventoryItemCreateUpdate(InventoryItemBase)</see>
        public void SendInventoryItemCreateUpdate(InventoryItemBase Item)
        {
            Encoding enc = Encoding.ASCII;
            uint FULL_MASK_PERMISSIONS = (uint)PermissionMask.All;
            UpdateCreateInventoryItemPacket InventoryReply = (UpdateCreateInventoryItemPacket)PacketPool.Instance.GetPacket(PacketType.UpdateCreateInventoryItem);
            // TODO: don't create new blocks if recycling an old packet
            InventoryReply.AgentData.AgentID = AgentId;
            InventoryReply.AgentData.SimApproved = true;
            InventoryReply.InventoryData = new UpdateCreateInventoryItemPacket.InventoryDataBlock[1];
            InventoryReply.InventoryData[0] = new UpdateCreateInventoryItemPacket.InventoryDataBlock();
            InventoryReply.InventoryData[0].ItemID = Item.inventoryID;
            InventoryReply.InventoryData[0].AssetID = Item.assetID;
            InventoryReply.InventoryData[0].CreatorID = Item.creatorsID;
            InventoryReply.InventoryData[0].BaseMask = Item.inventoryBasePermissions;
            InventoryReply.InventoryData[0].CreationDate = 1000;
            InventoryReply.InventoryData[0].Description = Helpers.StringToField(Item.inventoryDescription);
            InventoryReply.InventoryData[0].EveryoneMask = Item.inventoryEveryOnePermissions;
            InventoryReply.InventoryData[0].Flags = 0;
            InventoryReply.InventoryData[0].FolderID = Item.parentFolderID;
            InventoryReply.InventoryData[0].GroupID = new LLUUID("00000000-0000-0000-0000-000000000000");
            InventoryReply.InventoryData[0].GroupMask = 0;
            InventoryReply.InventoryData[0].InvType = (sbyte)Item.invType;
            InventoryReply.InventoryData[0].Name = Helpers.StringToField(Item.inventoryName);
            InventoryReply.InventoryData[0].NextOwnerMask = Item.inventoryNextPermissions;
            InventoryReply.InventoryData[0].OwnerID = Item.avatarID;
            InventoryReply.InventoryData[0].OwnerMask = Item.inventoryCurrentPermissions;
            InventoryReply.InventoryData[0].SalePrice = 100;
            InventoryReply.InventoryData[0].SaleType = 0;
            InventoryReply.InventoryData[0].Type = (sbyte)Item.assetType;
            InventoryReply.InventoryData[0].CRC =
                Helpers.InventoryCRC(1000, 0, InventoryReply.InventoryData[0].InvType,
                                     InventoryReply.InventoryData[0].Type, InventoryReply.InventoryData[0].AssetID,
                                     InventoryReply.InventoryData[0].GroupID, 100,
                                     InventoryReply.InventoryData[0].OwnerID, InventoryReply.InventoryData[0].CreatorID,
                                     InventoryReply.InventoryData[0].ItemID, InventoryReply.InventoryData[0].FolderID,
                                     FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS,
                                     FULL_MASK_PERMISSIONS);

            OutPacket(InventoryReply, ThrottleOutPacketType.Asset);
        }

        public void SendRemoveInventoryItem(LLUUID itemID)
        {
            RemoveInventoryItemPacket remove = (RemoveInventoryItemPacket)PacketPool.Instance.GetPacket(PacketType.RemoveInventoryItem);
            // TODO: don't create new blocks if recycling an old packet
            remove.AgentData.AgentID = AgentId;
            remove.AgentData.SessionID = m_sessionId;
            remove.InventoryData = new RemoveInventoryItemPacket.InventoryDataBlock[1];
            remove.InventoryData[0] = new RemoveInventoryItemPacket.InventoryDataBlock();
            remove.InventoryData[0].ItemID = itemID;

            OutPacket(remove, ThrottleOutPacketType.Asset);
        }

        public void SendTaskInventory(LLUUID taskID, short serial, byte[] fileName)
        {
            ReplyTaskInventoryPacket replytask = (ReplyTaskInventoryPacket)PacketPool.Instance.GetPacket(PacketType.ReplyTaskInventory);
            replytask.InventoryData.TaskID = taskID;
            replytask.InventoryData.Serial = serial;
            replytask.InventoryData.Filename = fileName;
            OutPacket(replytask, ThrottleOutPacketType.Asset);
        }

        public void SendXferPacket(ulong xferID, uint packet, byte[] data)
        {
            SendXferPacketPacket sendXfer = (SendXferPacketPacket)PacketPool.Instance.GetPacket(PacketType.SendXferPacket);
            sendXfer.XferID.ID = xferID;
            sendXfer.XferID.Packet = packet;
            sendXfer.DataPacket.Data = data;
            OutPacket(sendXfer, ThrottleOutPacketType.Task);
        }

        public void SendAvatarPickerReply(AvatarPickerReplyPacket replyPacket)
        {
            OutPacket(replyPacket, ThrottleOutPacketType.Task);
        }

        public void SendAgentDataUpdate(LLUUID agentid, LLUUID activegroupid, string firstname, string lastname, ulong grouppowers, string groupname, string grouptitle)
        {
            AgentDataUpdatePacket sendAgentDataUpdate = (AgentDataUpdatePacket)PacketPool.Instance.GetPacket(PacketType.AgentDataUpdate);
            sendAgentDataUpdate.AgentData.ActiveGroupID = activegroupid;
            sendAgentDataUpdate.AgentData.AgentID = agentid;
            sendAgentDataUpdate.AgentData.FirstName = Helpers.StringToField(firstname);
            sendAgentDataUpdate.AgentData.GroupName = Helpers.StringToField(groupname);
            sendAgentDataUpdate.AgentData.GroupPowers = grouppowers;
            sendAgentDataUpdate.AgentData.GroupTitle = Helpers.StringToField(grouptitle);
            sendAgentDataUpdate.AgentData.LastName = Helpers.StringToField(lastname);
            OutPacket(sendAgentDataUpdate, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public void SendAlertMessage(string message)
        {
            AlertMessagePacket alertPack = (AlertMessagePacket)PacketPool.Instance.GetPacket(PacketType.AlertMessage);
            alertPack.AlertData.Message = Helpers.StringToField(message);
            OutPacket(alertPack, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="modal"></param>
        public void SendAgentAlertMessage(string message, bool modal)
        {
            AgentAlertMessagePacket alertPack = (AgentAlertMessagePacket)PacketPool.Instance.GetPacket(PacketType.AgentAlertMessage);
            alertPack.AgentData.AgentID = AgentId;
            alertPack.AlertData.Message = Helpers.StringToField(message);
            alertPack.AlertData.Modal = modal;
            OutPacket(alertPack, ThrottleOutPacketType.Task);
        }

        public void SendLoadURL(string objectname, LLUUID objectID, LLUUID ownerID, bool groupOwned, string message,
                                string url)
        {
            LoadURLPacket loadURL = (LoadURLPacket)PacketPool.Instance.GetPacket(PacketType.LoadURL);
            loadURL.Data.ObjectName = Helpers.StringToField(objectname);
            loadURL.Data.ObjectID = objectID;
            loadURL.Data.OwnerID = ownerID;
            loadURL.Data.OwnerIsGroup = groupOwned;
            loadURL.Data.Message = Helpers.StringToField(message);
            loadURL.Data.URL = Helpers.StringToField(url);
            OutPacket(loadURL, ThrottleOutPacketType.Task);
        }

        public void SendDialog(string objectname, LLUUID objectID, LLUUID ownerID, string msg, LLUUID textureID, int ch, string[] buttonlabels)
        {
            ScriptDialogPacket dialog = (ScriptDialogPacket)PacketPool.Instance.GetPacket(PacketType.ScriptDialog);
            dialog.Data.ObjectID = objectID;
            dialog.Data.ObjectName = Helpers.StringToField(objectname);
            dialog.Data.FirstName = Helpers.StringToField(this.FirstName);
            dialog.Data.LastName = Helpers.StringToField(this.LastName);
            dialog.Data.Message = Helpers.StringToField(msg);
            dialog.Data.ImageID = textureID;
            dialog.Data.ChatChannel = ch;
            ScriptDialogPacket.ButtonsBlock[] buttons = new ScriptDialogPacket.ButtonsBlock[buttonlabels.Length];
            for (int i = 0; i < buttonlabels.Length; i++)
            {
                buttons[i] = new ScriptDialogPacket.ButtonsBlock();
                buttons[i].ButtonLabel = Helpers.StringToField(buttonlabels[i]);
            }
            dialog.Buttons = buttons;
            OutPacket(dialog, ThrottleOutPacketType.Task);
        }

        public void SendPreLoadSound(LLUUID objectID, LLUUID ownerID, LLUUID soundID)
        {
            PreloadSoundPacket preSound = (PreloadSoundPacket)PacketPool.Instance.GetPacket(PacketType.PreloadSound);
            // TODO: don't create new blocks if recycling an old packet
            preSound.DataBlock = new PreloadSoundPacket.DataBlockBlock[1];
            preSound.DataBlock[0] = new PreloadSoundPacket.DataBlockBlock();
            preSound.DataBlock[0].ObjectID = objectID;
            preSound.DataBlock[0].OwnerID = ownerID;
            preSound.DataBlock[0].SoundID = soundID;
            OutPacket(preSound, ThrottleOutPacketType.Task);
        }

        public void SendPlayAttachedSound(LLUUID soundID, LLUUID objectID, LLUUID ownerID, float gain, byte flags)
        {
            AttachedSoundPacket sound = (AttachedSoundPacket)PacketPool.Instance.GetPacket(PacketType.AttachedSound);
            sound.DataBlock.SoundID = soundID;
            sound.DataBlock.ObjectID = objectID;
            sound.DataBlock.OwnerID = ownerID;
            sound.DataBlock.Gain = gain;
            sound.DataBlock.Flags = flags;

            OutPacket(sound, ThrottleOutPacketType.Task);
        }

        public void SendTriggeredSound(LLUUID soundID, LLUUID ownerID, LLUUID objectID, LLUUID parentID, ulong handle, LLVector3 position, float gain)
        {
            SoundTriggerPacket sound = (SoundTriggerPacket)PacketPool.Instance.GetPacket(PacketType.SoundTrigger);
            sound.SoundData.SoundID = soundID;
            sound.SoundData.OwnerID = ownerID;
            sound.SoundData.ObjectID = objectID;
            sound.SoundData.ParentID = parentID;
            sound.SoundData.Handle = handle;
            sound.SoundData.Position = position;
            sound.SoundData.Gain = gain;

            OutPacket(sound, ThrottleOutPacketType.Task);
        }

        public void SendSunPos(LLVector3 sunPos, LLVector3 sunVel)
        {
            SimulatorViewerTimeMessagePacket viewertime = (SimulatorViewerTimeMessagePacket)PacketPool.Instance.GetPacket(PacketType.SimulatorViewerTimeMessage);
            viewertime.TimeInfo.SunDirection = sunPos;
            viewertime.TimeInfo.SunAngVelocity = sunVel;
            viewertime.TimeInfo.UsecSinceStart = (ulong)Util.UnixTimeSinceEpoch();
            viewertime.Header.Reliable = false;
            OutPacket(viewertime, ThrottleOutPacketType.Task);
        }

        public void SendViewerTime(int phase)
        {
            Console.WriteLine("SunPhase: {0}", phase);
            SimulatorViewerTimeMessagePacket viewertime = (SimulatorViewerTimeMessagePacket)PacketPool.Instance.GetPacket(PacketType.SimulatorViewerTimeMessage);
            //viewertime.TimeInfo.SecPerDay = 86400;
            //viewertime.TimeInfo.SecPerYear = 31536000;
            viewertime.TimeInfo.SecPerDay = 1000;
            viewertime.TimeInfo.SecPerYear = 365000;
            viewertime.TimeInfo.SunPhase = 1;
            int sunPhase = (phase + 2) / 2;
            if ((sunPhase < 6) || (sunPhase > 36))
            {
                viewertime.TimeInfo.SunDirection = new LLVector3(0f, 0.8f, -0.8f);
                Console.WriteLine("sending night");
            }
            else
            {
                if (sunPhase < 12)
                {
                    sunPhase = 12;
                }
                sunPhase = sunPhase - 12;

                float yValue = 0.1f * (sunPhase);
                Console.WriteLine("Computed SunPhase: {0}, yValue: {1}", sunPhase, yValue);
                if (yValue > 1.2f)
                {
                    yValue = yValue - 1.2f;
                }

                yValue = Util.Clip(yValue, 0, 1);

                if (sunPhase < 14)
                {
                    yValue = 1 - yValue;
                }
                if (sunPhase < 12)
                {
                    yValue *= -1;
                }
                viewertime.TimeInfo.SunDirection = new LLVector3(0f, yValue, 0.3f);
                Console.WriteLine("sending sun update " + yValue);
            }
            viewertime.TimeInfo.SunAngVelocity = new LLVector3(0, 0.0f, 10.0f);
            viewertime.TimeInfo.UsecSinceStart = (ulong)Util.UnixTimeSinceEpoch();
            viewertime.Header.Reliable = false;
            OutPacket(viewertime, ThrottleOutPacketType.Task);
        }

        public void SendAvatarProperties(LLUUID avatarID, string aboutText, string bornOn, string charterMember,
                                         string flAbout, uint flags, LLUUID flImageID, LLUUID imageID, string profileURL,
                                         LLUUID partnerID)
        {
            AvatarPropertiesReplyPacket avatarReply = (AvatarPropertiesReplyPacket)PacketPool.Instance.GetPacket(PacketType.AvatarPropertiesReply);
            avatarReply.AgentData.AgentID = AgentId;
            avatarReply.AgentData.AvatarID = avatarID;
            if (aboutText != null)
                avatarReply.PropertiesData.AboutText = Helpers.StringToField(aboutText);
            else
                avatarReply.PropertiesData.AboutText = Helpers.StringToField("");
            avatarReply.PropertiesData.BornOn = Helpers.StringToField(bornOn);
            avatarReply.PropertiesData.CharterMember = Helpers.StringToField(charterMember);
            if (flAbout != null)
                avatarReply.PropertiesData.FLAboutText = Helpers.StringToField(flAbout);
            else
                avatarReply.PropertiesData.FLAboutText = Helpers.StringToField("");
            avatarReply.PropertiesData.Flags = 0;
            avatarReply.PropertiesData.FLImageID = flImageID;
            avatarReply.PropertiesData.ImageID = imageID;
            avatarReply.PropertiesData.ProfileURL = Helpers.StringToField(profileURL);
            avatarReply.PropertiesData.PartnerID = partnerID;
            OutPacket(avatarReply, ThrottleOutPacketType.Task);
        }

        #endregion

        #region Appearance/ Wearables Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="wearables"></param>
        public void SendWearables(AvatarWearable[] wearables, int serial)
        {
            AgentWearablesUpdatePacket aw = (AgentWearablesUpdatePacket)PacketPool.Instance.GetPacket(PacketType.AgentWearablesUpdate);
            aw.AgentData.AgentID = AgentId;
            aw.AgentData.SerialNum = (uint)serial;
            aw.AgentData.SessionID = m_sessionId;

            // TODO: don't create new blocks if recycling an old packet
            aw.WearableData = new AgentWearablesUpdatePacket.WearableDataBlock[13];
            AgentWearablesUpdatePacket.WearableDataBlock awb;
            for (int i = 0; i < wearables.Length; i++)
            {
                awb = new AgentWearablesUpdatePacket.WearableDataBlock();
                awb.WearableType = (byte)i;
                awb.AssetID = wearables[i].AssetID;
                awb.ItemID = wearables[i].ItemID;
                aw.WearableData[i] = awb;
            }

            OutPacket(aw, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="visualParams"></param>
        /// <param name="textureEntry"></param>
        public void SendAppearance(LLUUID agentID, byte[] visualParams, byte[] textureEntry)
        {
            AvatarAppearancePacket avp = (AvatarAppearancePacket)PacketPool.Instance.GetPacket(PacketType.AvatarAppearance);
            // TODO: don't create new blocks if recycling an old packet
            avp.VisualParam = new AvatarAppearancePacket.VisualParamBlock[218];
            avp.ObjectData.TextureEntry = textureEntry;

            AvatarAppearancePacket.VisualParamBlock avblock = null;
            for (int i = 0; i < visualParams.Length; i++)
            {
                avblock = new AvatarAppearancePacket.VisualParamBlock();
                avblock.ParamValue = visualParams[i];
                avp.VisualParam[i] = avblock;
            }

            avp.Sender.IsTrial = false;
            avp.Sender.ID = agentID;
            OutPacket(avp, ThrottleOutPacketType.Task);
        }

        public void SendAnimations(LLUUID[] animations, int[] seqs, LLUUID sourceAgentId)
        {
            AvatarAnimationPacket ani = (AvatarAnimationPacket)PacketPool.Instance.GetPacket(PacketType.AvatarAnimation);
            // TODO: don't create new blocks if recycling an old packet
            ani.AnimationSourceList = new AvatarAnimationPacket.AnimationSourceListBlock[1];
            ani.AnimationSourceList[0] = new AvatarAnimationPacket.AnimationSourceListBlock();
            ani.AnimationSourceList[0].ObjectID = sourceAgentId;
            ani.Sender = new AvatarAnimationPacket.SenderBlock();
            ani.Sender.ID = sourceAgentId;
            ani.AnimationList = new AvatarAnimationPacket.AnimationListBlock[animations.Length];

            for (int i = 0; i < animations.Length; ++i)
            {
                ani.AnimationList[i] = new AvatarAnimationPacket.AnimationListBlock();
                ani.AnimationList[i].AnimID = animations[i];
                ani.AnimationList[i].AnimSequenceID = seqs[i];
            }
            ani.Header.Reliable = false;
            OutPacket(ani, ThrottleOutPacketType.Task);
        }

        #endregion

        #region Avatar Packet/data sending Methods

        /// <summary>
        /// send a objectupdate packet with information about the clients avatar
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="avatarID"></param>
        /// <param name="avatarLocalID"></param>
        /// <param name="Pos"></param>
        public void SendAvatarData(ulong regionHandle, string firstName, string lastName, LLUUID avatarID,
                                   uint avatarLocalID, LLVector3 Pos, byte[] textureEntry, uint parentID)
        {
            ObjectUpdatePacket objupdate = (ObjectUpdatePacket)PacketPool.Instance.GetPacket(PacketType.ObjectUpdate);
            // TODO: don't create new blocks if recycling an old packet
            objupdate.RegionData.RegionHandle = regionHandle;
            objupdate.RegionData.TimeDilation = ushort.MaxValue;
            objupdate.ObjectData = new ObjectUpdatePacket.ObjectDataBlock[1];
            objupdate.ObjectData[0] = CreateDefaultAvatarPacket(textureEntry);

            //give this avatar object a local id and assign the user a name
            objupdate.ObjectData[0].ID = avatarLocalID;
            objupdate.ObjectData[0].FullID = avatarID;
            objupdate.ObjectData[0].ParentID = parentID;
            objupdate.ObjectData[0].NameValue =
                Helpers.StringToField("FirstName STRING RW SV " + firstName + "\nLastName STRING RW SV " + lastName);
            LLVector3 pos2 = new LLVector3((float)Pos.X, (float)Pos.Y, (float)Pos.Z);
            byte[] pb = pos2.GetBytes();
            Array.Copy(pb, 0, objupdate.ObjectData[0].ObjectData, 16, pb.Length);

            OutPacket(objupdate, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="timeDilation"></param>
        /// <param name="localID"></param>
        /// <param name="position"></param>
        /// <param name="velocity"></param>
        public void SendAvatarTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, LLVector3 position,
                                          LLVector3 velocity, LLQuaternion rotation)
        {
            ImprovedTerseObjectUpdatePacket.ObjectDataBlock terseBlock =
                CreateAvatarImprovedBlock(localID, position, velocity, rotation);
            ImprovedTerseObjectUpdatePacket terse = (ImprovedTerseObjectUpdatePacket)PacketPool.Instance.GetPacket(PacketType.ImprovedTerseObjectUpdate);
            // TODO: don't create new blocks if recycling an old packet
            terse.RegionData.RegionHandle = regionHandle;
            terse.RegionData.TimeDilation = timeDilation;
            terse.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
            terse.ObjectData[0] = terseBlock;

            terse.Header.Reliable = false;


            OutPacket(terse, ThrottleOutPacketType.Task);
        }

        public void SendCoarseLocationUpdate(List<LLVector3> CoarseLocations)
        {
            CoarseLocationUpdatePacket loc = (CoarseLocationUpdatePacket)PacketPool.Instance.GetPacket(PacketType.CoarseLocationUpdate);
            // TODO: don't create new blocks if recycling an old packet
            int total = CoarseLocations.Count;
            CoarseLocationUpdatePacket.IndexBlock ib =
                new CoarseLocationUpdatePacket.IndexBlock();
            loc.Location = new CoarseLocationUpdatePacket.LocationBlock[total];
            for (int i = 0; i < total; i++)
            {
                CoarseLocationUpdatePacket.LocationBlock lb =
                    new CoarseLocationUpdatePacket.LocationBlock();
                lb.X = (byte)CoarseLocations[i].X;
                lb.Y = (byte)CoarseLocations[i].Y;
                lb.Z = (byte)(CoarseLocations[i].Z / 4);
                loc.Location[i] = lb;
            }
            ib.You = -1;
            ib.Prey = -1;
            loc.Index = ib;
            loc.Header.Reliable = false;
            OutPacket(loc, ThrottleOutPacketType.Task);
        }

        #endregion

        #region Primitive Packet/data Sending Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="rotation"></param>
        /// <param name="attachPoint"></param>
        public void AttachObject(uint localID, LLQuaternion rotation, byte attachPoint)
        {
            ObjectAttachPacket attach = (ObjectAttachPacket)PacketPool.Instance.GetPacket(PacketType.ObjectAttach);
            System.Console.WriteLine("Attach object!");
            // TODO: don't create new blocks if recycling an old packet
            attach.AgentData.AgentID = AgentId;
            attach.AgentData.SessionID = m_sessionId;
            attach.AgentData.AttachmentPoint = attachPoint;
            attach.ObjectData = new ObjectAttachPacket.ObjectDataBlock[1];
            attach.ObjectData[0] = new ObjectAttachPacket.ObjectDataBlock();
            attach.ObjectData[0].ObjectLocalID = localID;
            attach.ObjectData[0].Rotation = rotation;

            OutPacket(attach, ThrottleOutPacketType.Task);
        }

        public void SendPrimitiveToClient(
            ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape, LLVector3 pos,
            uint flags,
            LLUUID objectID, LLUUID ownerID, string text, byte[] color, uint parentID, byte[] particleSystem,
            LLQuaternion rotation, byte clickAction)
        {
            byte[] textureanim = new byte[0];

            SendPrimitiveToClient(regionHandle, timeDilation, localID, primShape, pos, flags,
                                  objectID, ownerID, text, color, parentID, particleSystem,
                                    rotation, clickAction, textureanim);
        }
        public void SendPrimitiveToClient(
            ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape, LLVector3 pos,
            uint flags,
            LLUUID objectID, LLUUID ownerID, string text, byte[] color, uint parentID, byte[] particleSystem,
            LLQuaternion rotation, byte clickAction, byte[] textureanim)
        {
            ObjectUpdatePacket outPacket = (ObjectUpdatePacket)PacketPool.Instance.GetPacket(PacketType.ObjectUpdate);
            // TODO: don't create new blocks if recycling an old packet
            outPacket.RegionData.RegionHandle = regionHandle;
            outPacket.RegionData.TimeDilation = timeDilation;
            outPacket.ObjectData = new ObjectUpdatePacket.ObjectDataBlock[1];

            outPacket.ObjectData[0] = CreatePrimUpdateBlock(primShape, flags);

            outPacket.ObjectData[0].ID = localID;
            outPacket.ObjectData[0].FullID = objectID;
            outPacket.ObjectData[0].OwnerID = ownerID;
            outPacket.ObjectData[0].Text = Helpers.StringToField(text);
            outPacket.ObjectData[0].TextColor[0] = color[0];
            outPacket.ObjectData[0].TextColor[1] = color[1];
            outPacket.ObjectData[0].TextColor[2] = color[2];
            outPacket.ObjectData[0].TextColor[3] = color[3];
            outPacket.ObjectData[0].ParentID = parentID;
            outPacket.ObjectData[0].PSBlock = particleSystem;
            outPacket.ObjectData[0].ClickAction = clickAction;
            //outPacket.ObjectData[0].Flags = 0;

            // Sound Radius
            outPacket.ObjectData[0].Radius = 20;

            byte[] pb = pos.GetBytes();
            Array.Copy(pb, 0, outPacket.ObjectData[0].ObjectData, 0, pb.Length);

            byte[] rot = rotation.GetBytes();
            Array.Copy(rot, 0, outPacket.ObjectData[0].ObjectData, 36, rot.Length);

            if (textureanim.Length > 0)
            {
                outPacket.ObjectData[0].TextureAnim = textureanim;
            }

            OutPacket(outPacket, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="timeDilation"></param>
        /// <param name="localID"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        public void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, LLVector3 position,
                                        LLQuaternion rotation, byte state)
        {
            LLVector3 velocity = new LLVector3(0f, 0f, 0f);
            LLVector3 rotationalvelocity = new LLVector3(0f, 0f, 0f);
            ImprovedTerseObjectUpdatePacket terse = (ImprovedTerseObjectUpdatePacket)PacketPool.Instance.GetPacket(PacketType.ImprovedTerseObjectUpdate);
            // TODO: don't create new blocks if recycling an old packet
            terse.RegionData.RegionHandle = regionHandle;
            terse.RegionData.TimeDilation = timeDilation;
            terse.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
            terse.ObjectData[0] = CreatePrimImprovedBlock(localID, position, rotation, velocity, rotationalvelocity, state);
            terse.Header.Reliable = false;
            OutPacket(terse, ThrottleOutPacketType.Task);
        }

        public void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, LLVector3 position,
                                        LLQuaternion rotation, LLVector3 velocity, LLVector3 rotationalvelocity)
        {
            ImprovedTerseObjectUpdatePacket terse = (ImprovedTerseObjectUpdatePacket)PacketPool.Instance.GetPacket(PacketType.ImprovedTerseObjectUpdate);
            // TODO: don't create new blocks if recycling an old packet
            terse.RegionData.RegionHandle = regionHandle;
            terse.RegionData.TimeDilation = timeDilation;
            terse.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
            terse.ObjectData[0] = CreatePrimImprovedBlock(localID, position, rotation, velocity, rotationalvelocity, 0);
            terse.Header.Reliable = false;
            OutPacket(terse, ThrottleOutPacketType.Task);
        }

        #endregion

        #region Helper Methods

        protected ImprovedTerseObjectUpdatePacket.ObjectDataBlock CreateAvatarImprovedBlock(uint localID, LLVector3 pos,
                                                                                            LLVector3 velocity,
                                                                                            LLQuaternion rotation)
        {
            byte[] bytes = new byte[60];
            int i = 0;
            ImprovedTerseObjectUpdatePacket.ObjectDataBlock dat = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock();

            dat.TextureEntry = new byte[0]; // AvatarTemplate.TextureEntry;

            uint ID = localID;

            bytes[i++] = (byte)(ID % 256);
            bytes[i++] = (byte)((ID >> 8) % 256);
            bytes[i++] = (byte)((ID >> 16) % 256);
            bytes[i++] = (byte)((ID >> 24) % 256);
            bytes[i++] = 0;
            bytes[i++] = 1;
            i += 14;
            bytes[i++] = 128;
            bytes[i++] = 63;

            byte[] pb = pos.GetBytes();
            Array.Copy(pb, 0, bytes, i, pb.Length);
            i += 12;
            ushort InternVelocityX;
            ushort InternVelocityY;
            ushort InternVelocityZ;
            Vector3 internDirec = new Vector3(0, 0, 0);

            internDirec = new Vector3(velocity.X, velocity.Y, velocity.Z);

            internDirec = internDirec / 128.0f;
            internDirec.x += 1;
            internDirec.y += 1;
            internDirec.z += 1;

            InternVelocityX = (ushort)(32768 * internDirec.x);
            InternVelocityY = (ushort)(32768 * internDirec.y);
            InternVelocityZ = (ushort)(32768 * internDirec.z);

            ushort ac = 32767;
            bytes[i++] = (byte)(InternVelocityX % 256);
            bytes[i++] = (byte)((InternVelocityX >> 8) % 256);
            bytes[i++] = (byte)(InternVelocityY % 256);
            bytes[i++] = (byte)((InternVelocityY >> 8) % 256);
            bytes[i++] = (byte)(InternVelocityZ % 256);
            bytes[i++] = (byte)((InternVelocityZ >> 8) % 256);

            //accel
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);

            //rotation
            ushort rw, rx, ry, rz;
            rw = (ushort)(32768 * (rotation.W + 1));
            rx = (ushort)(32768 * (rotation.X + 1));
            ry = (ushort)(32768 * (rotation.Y + 1));
            rz = (ushort)(32768 * (rotation.Z + 1));

            //rot
            bytes[i++] = (byte)(rx % 256);
            bytes[i++] = (byte)((rx >> 8) % 256);
            bytes[i++] = (byte)(ry % 256);
            bytes[i++] = (byte)((ry >> 8) % 256);
            bytes[i++] = (byte)(rz % 256);
            bytes[i++] = (byte)((rz >> 8) % 256);
            bytes[i++] = (byte)(rw % 256);
            bytes[i++] = (byte)((rw >> 8) % 256);

            //rotation vel
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);

            dat.Data = bytes;

            return (dat);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localID"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        protected ImprovedTerseObjectUpdatePacket.ObjectDataBlock CreatePrimImprovedBlock(uint localID,
                                                                                          LLVector3 position,
                                                                                          LLQuaternion rotation,
                                                                                          LLVector3 velocity,
                                                                                          LLVector3 rotationalvelocity,
                                                                                          byte state)
        {
            uint ID = localID;
            byte[] bytes = new byte[60];

            int i = 0;
            ImprovedTerseObjectUpdatePacket.ObjectDataBlock dat = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock();
            dat.TextureEntry = new byte[0];
            bytes[i++] = (byte)(ID % 256);
            bytes[i++] = (byte)((ID >> 8) % 256);
            bytes[i++] = (byte)((ID >> 16) % 256);
            bytes[i++] = (byte)((ID >> 24) % 256);
            bytes[i++] = state;
            bytes[i++] = 0;

            byte[] pb = position.GetBytes();
            Array.Copy(pb, 0, bytes, i, pb.Length);
            i += 12;
            ushort ac = 32767;

            ushort velx, vely, velz;
            Vector3 vel = new Vector3(velocity.X, velocity.Y, velocity.Z);

            vel = vel / 128.0f;
            vel.x += 1;
            vel.y += 1;
            vel.z += 1;
            //vel
            velx = (ushort)(32768 * (vel.x));
            vely = (ushort)(32768 * (vel.y));
            velz = (ushort)(32768 * (vel.z));

            bytes[i++] = (byte)(velx % 256);
            bytes[i++] = (byte)((velx >> 8) % 256);
            bytes[i++] = (byte)(vely % 256);
            bytes[i++] = (byte)((vely >> 8) % 256);
            bytes[i++] = (byte)(velz % 256);
            bytes[i++] = (byte)((velz >> 8) % 256);

            //accel
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);

            ushort rw, rx, ry, rz;
            rw = (ushort)(32768 * (rotation.W + 1));
            rx = (ushort)(32768 * (rotation.X + 1));
            ry = (ushort)(32768 * (rotation.Y + 1));
            rz = (ushort)(32768 * (rotation.Z + 1));

            //rot
            bytes[i++] = (byte)(rx % 256);
            bytes[i++] = (byte)((rx >> 8) % 256);
            bytes[i++] = (byte)(ry % 256);
            bytes[i++] = (byte)((ry >> 8) % 256);
            bytes[i++] = (byte)(rz % 256);
            bytes[i++] = (byte)((rz >> 8) % 256);
            bytes[i++] = (byte)(rw % 256);
            bytes[i++] = (byte)((rw >> 8) % 256);

            //rotation vel
            ushort rvelx, rvely, rvelz;
            Vector3 rvel = new Vector3(rotationalvelocity.X, rotationalvelocity.Y, rotationalvelocity.Z);

            rvel = rvel / 128.0f;
            rvel.x += 1;
            rvel.y += 1;
            rvel.z += 1;
            //vel
            rvelx = (ushort)(32768 * (rvel.x));
            rvely = (ushort)(32768 * (rvel.y));
            rvelz = (ushort)(32768 * (rvel.z));

            bytes[i++] = (byte)(rvelx % 256);
            bytes[i++] = (byte)((rvelx >> 8) % 256);
            bytes[i++] = (byte)(rvely % 256);
            bytes[i++] = (byte)((rvely >> 8) % 256);
            bytes[i++] = (byte)(rvelz % 256);
            bytes[i++] = (byte)((rvelz >> 8) % 256);

            dat.Data = bytes;
            return dat;
        }

        /// <summary>
        /// Create the ObjectDataBlock for a ObjectUpdatePacket  (for a Primitive)
        /// </summary>
        /// <param name="primData"></param>
        /// <returns></returns>
        protected ObjectUpdatePacket.ObjectDataBlock CreatePrimUpdateBlock(PrimitiveBaseShape primShape, uint flags)
        {
            ObjectUpdatePacket.ObjectDataBlock objupdate = new ObjectUpdatePacket.ObjectDataBlock();
            SetDefaultPrimPacketValues(objupdate);
            objupdate.UpdateFlags = flags;
            SetPrimPacketShapeData(objupdate, primShape);
            // if ((primShape.PCode == 111) || (primShape.PCode == 255))
            if ((primShape.PCode == (byte)PCode.NewTree) || (primShape.PCode == (byte)PCode.Tree) || (primShape.PCode == (byte)PCode.Grass))
            {
                objupdate.Data = new byte[1];
                objupdate.Data[0] = primShape.State;
            }
            return objupdate;
        }

        protected void SetPrimPacketShapeData(ObjectUpdatePacket.ObjectDataBlock objectData, PrimitiveBaseShape primData)
        {
            objectData.TextureEntry = primData.TextureEntry;
            objectData.PCode = primData.PCode;
            objectData.State = primData.State;
            objectData.PathBegin = primData.PathBegin;
            objectData.PathEnd = primData.PathEnd;
            objectData.PathScaleX = primData.PathScaleX;
            objectData.PathScaleY = primData.PathScaleY;
            objectData.PathShearX = primData.PathShearX;
            objectData.PathShearY = primData.PathShearY;
            objectData.PathSkew = primData.PathSkew;
            objectData.ProfileBegin = primData.ProfileBegin;
            objectData.ProfileEnd = primData.ProfileEnd;
            objectData.Scale = primData.Scale;
            objectData.PathCurve = primData.PathCurve;
            objectData.ProfileCurve = primData.ProfileCurve;
            objectData.ProfileHollow = primData.ProfileHollow;
            objectData.PathRadiusOffset = primData.PathRadiusOffset;
            objectData.PathRevolutions = primData.PathRevolutions;
            objectData.PathTaperX = primData.PathTaperX;
            objectData.PathTaperY = primData.PathTaperY;
            objectData.PathTwist = primData.PathTwist;
            objectData.PathTwistBegin = primData.PathTwistBegin;
            objectData.ExtraParams = primData.ExtraParams;
        }

        /// <summary>
        /// Set some default values in a ObjectUpdatePacket
        /// </summary>
        /// <param name="objdata"></param>
        protected void SetDefaultPrimPacketValues(ObjectUpdatePacket.ObjectDataBlock objdata)
        {
            objdata.PSBlock = new byte[0];
            objdata.ExtraParams = new byte[1];
            objdata.MediaURL = new byte[0];
            objdata.NameValue = new byte[0];
            objdata.Text = new byte[0];
            objdata.TextColor = new byte[4];
            objdata.JointAxisOrAnchor = new LLVector3(0, 0, 0);
            objdata.JointPivot = new LLVector3(0, 0, 0);
            objdata.Material = 3;
            objdata.TextureAnim = new byte[0];
            objdata.Sound = LLUUID.Zero;
            objdata.State = 0;
            objdata.Data = new byte[0];

            objdata.ObjectData = new byte[60];
            objdata.ObjectData[46] = 128;
            objdata.ObjectData[47] = 63;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public ObjectUpdatePacket.ObjectDataBlock CreateDefaultAvatarPacket(byte[] textureEntry)
        {
            ObjectUpdatePacket.ObjectDataBlock objdata = new ObjectUpdatePacket.ObjectDataBlock();
            //  new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock(data1, ref i);

            SetDefaultAvatarPacketValues(ref objdata);
            objdata.UpdateFlags = 61 + (9 << 8) + (130 << 16) + (16 << 24);
            objdata.PathCurve = 16;
            objdata.ProfileCurve = 1;
            objdata.PathScaleX = 100;
            objdata.PathScaleY = 100;
            objdata.ParentID = 0;
            objdata.OwnerID = LLUUID.Zero;
            objdata.Scale = new LLVector3(1, 1, 1);
            objdata.PCode = 47;
            if (textureEntry != null)
            {
                objdata.TextureEntry = textureEntry;
            }
            Encoding enc = Encoding.ASCII;
            LLVector3 pos = new LLVector3(objdata.ObjectData, 16);
            pos.X = 100f;
            objdata.ID = 8880000;
            objdata.NameValue = enc.GetBytes("FirstName STRING RW SV Test \nLastName STRING RW SV User \0");
            //LLVector3 pos2 = new LLVector3(100f, 100f, 23f);
            //objdata.FullID=user.AgentId;
            byte[] pb = pos.GetBytes();
            Array.Copy(pb, 0, objdata.ObjectData, 16, pb.Length);

            return objdata;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objdata"></param>
        protected void SetDefaultAvatarPacketValues(ref ObjectUpdatePacket.ObjectDataBlock objdata)
        {
            objdata.PSBlock = new byte[0];
            objdata.ExtraParams = new byte[1];
            objdata.MediaURL = new byte[0];
            objdata.NameValue = new byte[0];
            objdata.Text = new byte[0];
            objdata.TextColor = new byte[4];
            objdata.JointAxisOrAnchor = new LLVector3(0, 0, 0);
            objdata.JointPivot = new LLVector3(0, 0, 0);
            objdata.Material = 4;
            objdata.TextureAnim = new byte[0];
            objdata.Sound = LLUUID.Zero;
            LLObject.TextureEntry ntex = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-5005-000000000005"));
            objdata.TextureEntry = ntex.ToBytes();

            objdata.State = 0;
            objdata.Data = new byte[0];

            objdata.ObjectData = new byte[76];
            objdata.ObjectData[15] = 128;
            objdata.ObjectData[16] = 63;
            objdata.ObjectData[56] = 128;
            objdata.ObjectData[61] = 102;
            objdata.ObjectData[62] = 40;
            objdata.ObjectData[63] = 61;
            objdata.ObjectData[64] = 189;
        }

        public void SendNameReply(LLUUID profileId, string firstname, string lastname)
        {
            UUIDNameReplyPacket packet = (UUIDNameReplyPacket)PacketPool.Instance.GetPacket(PacketType.UUIDNameReply);
            // TODO: don't create new blocks if recycling an old packet
            packet.UUIDNameBlock = new UUIDNameReplyPacket.UUIDNameBlockBlock[1];
            packet.UUIDNameBlock[0] = new UUIDNameReplyPacket.UUIDNameBlockBlock();
            packet.UUIDNameBlock[0].ID = profileId;
            packet.UUIDNameBlock[0].FirstName = Helpers.StringToField(firstname);
            packet.UUIDNameBlock[0].LastName = Helpers.StringToField(lastname);

            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        #endregion

        protected virtual void RegisterLocalPacketHandlers()
        {
            AddLocalPacketHandler(PacketType.LogoutRequest, Logout);
            AddLocalPacketHandler(PacketType.ViewerEffect, HandleViewerEffect);
            AddLocalPacketHandler(PacketType.AgentCachedTexture, AgentTextureCached);
            AddLocalPacketHandler(PacketType.MultipleObjectUpdate, MultipleObjUpdate);
            AddLocalPacketHandler(PacketType.MoneyTransferRequest, HandleMoneyTransferRequest);
        }

        private bool HandleMoneyTransferRequest(IClientAPI sender, Packet Pack)
        {
            MoneyTransferRequestPacket money = (MoneyTransferRequestPacket)Pack;
            // validate the agent owns the agentID and sessionID
            if (money.MoneyData.SourceID == sender.AgentId && money.AgentData.AgentID == sender.AgentId && money.AgentData.SessionID == sender.SessionId)
            {
                handlerMoneyTransferRequest = OnMoneyTransferRequest;
                if (handlerMoneyTransferRequest != null)
                {
                    handlerMoneyTransferRequest(money.MoneyData.SourceID, money.MoneyData.DestID,
                        money.MoneyData.Amount, money.MoneyData.TransactionType,
                        Util.FieldToString(money.MoneyData.Description));
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        private bool HandleViewerEffect(IClientAPI sender, Packet Pack)
        {
            ViewerEffectPacket viewer = (ViewerEffectPacket)Pack;
            handlerViewerEffect = OnViewerEffect;
            if (handlerViewerEffect != null)
            {
                handlerViewerEffect(sender, viewer.Effect);
            }

            return true;
        }

        protected virtual bool Logout(IClientAPI client, Packet packet)
        {
            m_log.Info("[CLIENT]: Got a logout request");

            handlerLogout = OnLogout;

            if (handlerLogout != null)
            {
                handlerLogout(client);
            }

            return true;
        }

        /// <summary>
        /// Send a response back to a client when it asks the asset server (via the region server) if it has
        /// its appearance texture cached.
        /// 
        /// At the moment, we always reply that there is no cached texture.
        /// </summary>
        /// <param name="simclient"></param>
        /// <param name="packet"></param>
        /// <returns></returns>
        protected bool AgentTextureCached(IClientAPI simclient, Packet packet)
        {
            //System.Console.WriteLine("texture cached: " + packet.ToString());
            AgentCachedTexturePacket cachedtex = (AgentCachedTexturePacket)packet;
            AgentCachedTextureResponsePacket cachedresp = (AgentCachedTextureResponsePacket)PacketPool.Instance.GetPacket(PacketType.AgentCachedTextureResponse);
            // TODO: don't create new blocks if recycling an old packet
            cachedresp.AgentData.AgentID = AgentId;
            cachedresp.AgentData.SessionID = m_sessionId;
            cachedresp.AgentData.SerialNum = m_cachedTextureSerial;
            m_cachedTextureSerial++;
            cachedresp.WearableData =
                new AgentCachedTextureResponsePacket.WearableDataBlock[cachedtex.WearableData.Length];
            
            for (int i = 0; i < cachedtex.WearableData.Length; i++)
            {
                cachedresp.WearableData[i] = new AgentCachedTextureResponsePacket.WearableDataBlock();
                cachedresp.WearableData[i].TextureIndex = cachedtex.WearableData[i].TextureIndex;
                cachedresp.WearableData[i].TextureID = LLUUID.Zero;
                cachedresp.WearableData[i].HostName = new byte[0];
            }
            
            // Temporarily throw these packets on to the wind queue, so we can identify whether these 
            // are somehow the source of the packet bloat.
            OutPacket(cachedresp, ThrottleOutPacketType.Wind);
            return true;
        }

        protected bool MultipleObjUpdate(IClientAPI simClient, Packet packet)
        {
            MultipleObjectUpdatePacket multipleupdate = (MultipleObjectUpdatePacket)packet;
            // System.Console.WriteLine("new multi update packet " + multipleupdate.ToString());
            Scene tScene = (Scene)m_scene;

            for (int i = 0; i < multipleupdate.ObjectData.Length; i++)
            {
                MultipleObjectUpdatePacket.ObjectDataBlock block = multipleupdate.ObjectData[i];

                // Can't act on Null Data
                if (block.Data != null)
                {
                    uint localId = block.ObjectLocalID;
                    SceneObjectPart part = tScene.GetSceneObjectPart(localId);

                    if (part == null)
                    {
                        // It's a ghost! tell the client to delete it from view.
                        simClient.SendKillObject(Scene.RegionInfo.RegionHandle,
                                                 localId);
                    }
                    else
                    {
                        LLUUID partId = part.UUID;





                        UpdatePrimRotation handlerUpdatePrimRotation = OnUpdatePrimGroupRotation;
                        UpdatePrimGroupRotation handlerUpdatePrimGroupRotation = OnUpdatePrimGroupMouseRotation;

                        switch (block.Type)
                        {
                            case 1:
                                LLVector3 pos1 = new LLVector3(block.Data, 0);

                                handlerUpdatePrimSinglePosition = OnUpdatePrimSinglePosition;
                                if (handlerUpdatePrimSinglePosition != null)
                                {

                                    // System.Console.WriteLine("new movement position is " + pos.X + " , " + pos.Y + " , " + pos.Z);
                                    handlerUpdatePrimSinglePosition(localId, pos1, this);
                                }
                                break;
                            case 2:
                                LLQuaternion rot1 = new LLQuaternion(block.Data, 0, true);

                                handlerUpdatePrimSingleRotation = OnUpdatePrimSingleRotation;
                                if (handlerUpdatePrimSingleRotation != null)
                                {

                                    //System.Console.WriteLine("new tab rotation is " + rot.X + " , " + rot.Y + " , " + rot.Z + " , " + rot.W);
                                    handlerUpdatePrimSingleRotation(localId, rot1, this);
                                }
                                break;
                            case 3:

                                LLQuaternion rot2 = new LLQuaternion(block.Data, 12, true);
                                handlerUpdatePrimSingleRotation = OnUpdatePrimSingleRotation;
                                if (handlerUpdatePrimSingleRotation != null)
                                {

                                    //System.Console.WriteLine("new mouse rotation is " + rot.X + " , " + rot.Y + " , " + rot.Z + " , " + rot.W);
                                    handlerUpdatePrimSingleRotation(localId, rot2, this);
                                }
                                break;
                            case 5:

                                LLVector3 scale1 = new LLVector3(block.Data, 12);

                                handlerUpdatePrimScale = OnUpdatePrimScale;
                                if (handlerUpdatePrimScale != null)
                                {

                                    // Console.WriteLine("new scale is " + scale.X + " , " + scale.Y + " , " + scale.Z);
                                    handlerUpdatePrimScale(localId, scale1, this);
                                }
                                break;
                            case 9:
                                LLVector3 pos2 = new LLVector3(block.Data, 0);

                                handlerUpdateVector = OnUpdatePrimGroupPosition;

                                if (handlerUpdateVector != null)
                                {

                                    handlerUpdateVector(localId, pos2, this);
                                }
                                break;
                            case 10:

                                LLQuaternion rot3 = new LLQuaternion(block.Data, 0, true);

                                handlerUpdatePrimRotation = OnUpdatePrimGroupRotation;
                                if (handlerUpdatePrimRotation != null)
                                {

                                    //  Console.WriteLine("new rotation is " + rot.X + " , " + rot.Y + " , " + rot.Z + " , " + rot.W);
                                    handlerUpdatePrimRotation(localId, rot3, this);
                                }
                                break;
                            case 11:

                                LLVector3 pos3 = new LLVector3(block.Data, 0);
                                LLQuaternion rot4 = new LLQuaternion(block.Data, 12, true);

                                handlerUpdatePrimGroupRotation = OnUpdatePrimGroupMouseRotation;
                                if (handlerUpdatePrimGroupRotation != null)
                                {

                                    //Console.WriteLine("new rotation position is " + pos.X + " , " + pos.Y + " , " + pos.Z);
                                    // Console.WriteLine("new rotation is " + rot.X + " , " + rot.Y + " , " + rot.Z + " , " + rot.W);
                                    handlerUpdatePrimGroupRotation(localId, pos3, rot4, this);
                                }
                                break;
                            case 13:

                                LLVector3 scale2 = new LLVector3(block.Data, 12);
                                LLVector3 pos4 = new LLVector3(block.Data, 0);

                                handlerUpdatePrimScale = OnUpdatePrimScale;
                                if (handlerUpdatePrimScale != null)
                                {

                                    //Console.WriteLine("new scale is " + scale.X + " , " + scale.Y + " , " + scale.Z);
                                    handlerUpdatePrimScale(localId, scale2, this);

                                    // Change the position based on scale (for bug number 246)
                                    handlerUpdatePrimSinglePosition = OnUpdatePrimSinglePosition;
                                    // System.Console.WriteLine("new movement position is " + pos.X + " , " + pos.Y + " , " + pos.Z);
                                    if (handlerUpdatePrimSinglePosition != null)
                                    {
                                        handlerUpdatePrimSinglePosition(localId, pos4, this);
                                    }
                                }
                                break;
                            case 29:
                                LLVector3 scale5 = new LLVector3(block.Data, 12);
                                LLVector3 pos5 = new LLVector3(block.Data, 0);

                                handlerUpdatePrimScale = OnUpdatePrimScale;
                                if (handlerUpdatePrimScale != null)
                                {

                                    // Console.WriteLine("new scale is " + scale.X + " , " + scale.Y + " , " + scale.Z );
                                    handlerUpdatePrimScale(localId, scale5, this);

                                    handlerUpdatePrimSinglePosition = OnUpdatePrimSinglePosition;
                                    if (handlerUpdatePrimSinglePosition != null)
                                    {
                                        handlerUpdatePrimSinglePosition(localId, pos5, this);
                                    }
                                }
                                break;
                            case 21:
                                LLVector3 scale6 = new LLVector3(block.Data, 12);
                                handlerUpdatePrimScale = OnUpdatePrimScale;
                                if (handlerUpdatePrimScale != null)
                                {
                                    // Console.WriteLine("new scale is " + scale.X + " , " + scale.Y + " , " + scale.Z);
                                    handlerUpdatePrimScale(localId, scale6, this);
                                }
                                break;
                        }
                    }
                }
            }
            return true;
        }

        public void RequestMapLayer()
        {
            //should be getting the map layer from the grid server
            //send a layer covering the 800,800 - 1200,1200 area (should be covering the requested area)
            MapLayerReplyPacket mapReply = (MapLayerReplyPacket)PacketPool.Instance.GetPacket(PacketType.MapLayerReply);
            // TODO: don't create new blocks if recycling an old packet
            mapReply.AgentData.AgentID = AgentId;
            mapReply.AgentData.Flags = 0;
            mapReply.LayerData = new MapLayerReplyPacket.LayerDataBlock[1];
            mapReply.LayerData[0] = new MapLayerReplyPacket.LayerDataBlock();
            mapReply.LayerData[0].Bottom = 0;
            mapReply.LayerData[0].Left = 0;
            mapReply.LayerData[0].Top = 30000;
            mapReply.LayerData[0].Right = 30000;
            mapReply.LayerData[0].ImageID = new LLUUID("00000000-0000-1111-9999-000000000006");
            OutPacket(mapReply, ThrottleOutPacketType.Land);
        }

        public void RequestMapBlocksX(int minX, int minY, int maxX, int maxY)
        {
            /*
            IList simMapProfiles = m_gridServer.RequestMapBlocks(minX, minY, maxX, maxY);
            MapBlockReplyPacket mbReply = new MapBlockReplyPacket();
            mbReply.AgentData.AgentId = this.AgentId;
            int len;
            if (simMapProfiles == null)
                len = 0;
            else
                len = simMapProfiles.Count;

            mbReply.Data = new MapBlockReplyPacket.DataBlock[len];
            int iii;
            for (iii = 0; iii < len; iii++)
            {
                Hashtable mp = (Hashtable)simMapProfiles[iii];
                mbReply.Data[iii] = new MapBlockReplyPacket.DataBlock();
                mbReply.Data[iii].Name = System.Text.Encoding.UTF8.GetBytes((string)mp["name"]);
                mbReply.Data[iii].Access = System.Convert.ToByte(mp["access"]);
                mbReply.Data[iii].Agents = System.Convert.ToByte(mp["agents"]);
                mbReply.Data[iii].MapImageID = new LLUUID((string)mp["map-image-id"]);
                mbReply.Data[iii].RegionFlags = System.Convert.ToUInt32(mp["region-flags"]);
                mbReply.Data[iii].WaterHeight = System.Convert.ToByte(mp["water-height"]);
                mbReply.Data[iii].X = System.Convert.ToUInt16(mp["x"]);
                mbReply.Data[iii].Y = System.Convert.ToUInt16(mp["y"]);
            }
            this.OutPacket(mbReply, ThrottleOutPacketType.Land);
             */
        }

        public byte[] GetThrottlesPacked(float multiplier)
        {
            return m_packetQueue.GetThrottlesPacked(multiplier);
        }

        public void SetChildAgentThrottle(byte[] throttles)
        {
            m_packetQueue.SetThrottleFromClient(throttles);
        }

        // Previously ClientView.m_packetQueue

        // A thread safe sequence number allocator.  
        protected uint NextSeqNum()
        {
            // Set the sequence number
            uint seq = 1;
            lock (m_sequenceLock)
            {
                if (m_sequence >= MAX_SEQUENCE)
                {
                    m_sequence = 1;
                }
                else
                {
                    m_sequence++;
                }
                seq = m_sequence;
            }
            return seq;
        }

        protected void AddAck(Packet Pack)
        {
            lock (m_needAck)
            {
                if (!m_needAck.ContainsKey(Pack.Header.Sequence))
                {
                    try
                    {
                        m_needAck.Add(Pack.Header.Sequence, Pack);
                        m_unAckedBytes += Pack.ToBytes().Length;
                    }
                    catch (Exception) // HACKY
                    {
                        // Ignore
                        // Seems to throw a exception here occasionally
                        // of 'duplicate key' despite being locked.
                        // !?!?!?
                    }
                }
                else
                {
                    //  Client.Log("Attempted to add a duplicate sequence number (" +
                    //     packet.Header.m_sequence + ") to the m_needAck dictionary for packet type " +
                    //      packet.Type.ToString(), Helpers.LogLevel.Warning);
                }
            }
        }

        protected virtual void SetPendingAcks(ref Packet Pack)
        {
            // Append any ACKs that need to be sent out to this packet
            lock (m_pendingAcks)
            {
                // TODO: If we are over MAX_APPENDED_ACKS we should drain off some of these
                if (m_pendingAcks.Count > 0 && m_pendingAcks.Count < MAX_APPENDED_ACKS)
                {
                    Pack.Header.AckList = new uint[m_pendingAcks.Count];
                    int i = 0;

                    foreach (uint ack in m_pendingAcks.Values)
                    {
                        Pack.Header.AckList[i] = ack;
                        i++;
                    }

                    m_pendingAcks.Clear();
                    Pack.Header.AppendedAcks = true;
                }
            }
        }

        protected virtual void ProcessOutPacket(Packet Pack)
        {
            // Keep track of when this packet was sent out
            Pack.TickCount = System.Environment.TickCount;

            if (!Pack.Header.Resent)
            {
                Pack.Header.Sequence = NextSeqNum();

                if (Pack.Header.Reliable) //DIRTY HACK
                {
                    AddAck(Pack); // this adds the need to ack this packet later


                    if (Pack.Type != PacketType.PacketAck && Pack.Type != PacketType.LogoutRequest)
                    {
                        SetPendingAcks(ref Pack);
                    }
                }
            }

            // Actually make the byte array and send it
            try
            {
                byte[] sendbuffer = Pack.ToBytes();
                PacketPool.Instance.ReturnPacket(Pack); 

                if (Pack.Header.Zerocoded)
                {
                    int packetsize = Helpers.ZeroEncode(sendbuffer, sendbuffer.Length, ZeroOutBuffer);
                    m_networkServer.SendPacketTo(ZeroOutBuffer, packetsize, SocketFlags.None, m_circuitCode);
                }
                else
                {
	              //Need some extra space in case we need to add proxy information to the message later
                  Buffer.BlockCopy(sendbuffer, 0, ZeroOutBuffer, 0, sendbuffer.Length);
                  m_networkServer.SendPacketTo(ZeroOutBuffer, sendbuffer.Length, SocketFlags.None, m_circuitCode);
                }

            }
            catch (Exception e)
            {
                m_log.Warn("[client]: " +
                                      "ClientView.m_packetQueue.cs:ProcessOutPacket() - WARNING: Socket exception occurred on connection " +
                                      m_userEndPoint.ToString() + " - killing thread");
                m_log.Error(e.ToString());
                Close(true);
            }
        }

        public virtual void InPacket(Packet NewPack)
        {
            if(!m_packetProcessingEnabled && NewPack.Type != PacketType.LogoutRequest)
            {
                PacketPool.Instance.ReturnPacket(NewPack);
                return;
            }

            // Handle appended ACKs
            if (NewPack != null)
            {
                if (NewPack.Header.AppendedAcks)
                {
                    lock (m_needAck)
                    {
                        foreach (uint ackedPacketId in NewPack.Header.AckList)
                        {
                            Packet ackedPacket;

                            if (m_needAck.TryGetValue(ackedPacketId, out ackedPacket))
                            {
                                m_unAckedBytes -= ackedPacket.ToBytes().Length;
                                m_needAck.Remove(ackedPacketId);
                            }
                        }
                    }
                }


                // Handle PacketAck packets
                if (NewPack.Type == PacketType.PacketAck)
                {
                    PacketAckPacket ackPacket = (PacketAckPacket)NewPack;

                    lock (m_needAck)
                    {
                        foreach (PacketAckPacket.PacketsBlock block in ackPacket.Packets)
                        {
                            uint ackedPackId = block.ID;
                            Packet ackedPacket;
                            if (m_needAck.TryGetValue(ackedPackId, out ackedPacket))
                            {
                                m_unAckedBytes -= ackedPacket.ToBytes().Length;
                                m_needAck.Remove(ackedPackId);
                            }
                        }
                    }
                }
                else if ((NewPack.Type == PacketType.StartPingCheck))
                {
                    //reply to pingcheck
                    StartPingCheckPacket startPing = (StartPingCheckPacket)NewPack;
                    CompletePingCheckPacket endPing = (CompletePingCheckPacket)PacketPool.Instance.GetPacket(PacketType.CompletePingCheck);
                    endPing.PingID.PingID = startPing.PingID.PingID;
                    OutPacket(endPing, ThrottleOutPacketType.Task);
                }
                else
                {
                    QueItem item = new QueItem();
                    item.Packet = NewPack;
                    item.Incoming = true;
                    m_packetQueue.Enqueue(item);
                }
            }
        }

        public virtual void OutPacket(Packet NewPack, ThrottleOutPacketType throttlePacketType)
        {
            if ((SynchronizeClient != null) && (!PacketProcessingEnabled))
            {
               // Sending packet to active client's server.
               if (SynchronizeClient(m_scene, NewPack, m_agentId, throttlePacketType))
                {
                   return;
                }
            }

            QueItem item = new QueItem();
            item.Packet = NewPack;
            item.Incoming = false;
            item.throttleType = throttlePacketType; // Packet throttle type
            m_packetQueue.Enqueue(item);
            m_packetsSent++;
        }

        # region Low Level Packet Methods

        protected void ack_pack(Packet Pack)
        {
            if (Pack.Header.Reliable)
            {
                PacketAckPacket ack_it = (PacketAckPacket)PacketPool.Instance.GetPacket(PacketType.PacketAck);
                // TODO: don't create new blocks if recycling an old packet
                ack_it.Packets = new PacketAckPacket.PacketsBlock[1];
                ack_it.Packets[0] = new PacketAckPacket.PacketsBlock();
                ack_it.Packets[0].ID = Pack.Header.Sequence;
                ack_it.Header.Reliable = false;

                OutPacket(ack_it, ThrottleOutPacketType.Unknown);
            }
            /*
            if (Pack.Header.Reliable)
            {
                lock (m_pendingAcks)
                {
                    uint sequence = (uint)Pack.Header.m_sequence;
                    if (!m_pendingAcks.ContainsKey(sequence)) { m_pendingAcks[sequence] = sequence; }
                }
            }*/
        }

        protected void ResendUnacked()
        {
            int now = System.Environment.TickCount;

            lock (m_needAck)
            {
                foreach (Packet packet in m_needAck.Values)
                {
                    if ((now - packet.TickCount > RESEND_TIMEOUT) && (!packet.Header.Resent))
                    {
                        //m_log.Debug("[NETWORK]: Resending " + packet.Type.ToString() + " packet, " +
                        //(now - packet.TickCount) + "ms have passed");

                        packet.Header.Resent = true;
                        OutPacket(packet, ThrottleOutPacketType.Resend);
                    }
                }
            }
        }

        protected void SendAcks()
        {
            lock (m_pendingAcks)
            {
                if (m_pendingAcks.Count > 0)
                {
                    if (m_pendingAcks.Count > 250)
                    {
                        // FIXME: Handle the odd case where we have too many pending ACKs queued up
                        m_log.Info("[NETWORK]: Too many ACKs queued up!");
                        return;
                    }

                    //m_log.Info("[NETWORK]: Sending PacketAck");

                    int i = 0;
                    PacketAckPacket acks = (PacketAckPacket)PacketPool.Instance.GetPacket(PacketType.PacketAck);
                    // TODO: don't create new blocks if recycling an old packet
                    acks.Packets = new PacketAckPacket.PacketsBlock[m_pendingAcks.Count];

                    foreach (uint ack in m_pendingAcks.Values)
                    {
                        acks.Packets[i] = new PacketAckPacket.PacketsBlock();
                        acks.Packets[i].ID = ack;
                        i++;
                    }

                    acks.Header.Reliable = false;
                    OutPacket(acks, ThrottleOutPacketType.Unknown);

                    m_pendingAcks.Clear();
                }
            }
        }

        protected void AckTimer_Elapsed(object sender, ElapsedEventArgs ea)
        {

            SendAcks();
            ResendUnacked();
            SendPacketStats();

        }

        protected void SendPacketStats()
        {
            handlerPacketStats = OnPacketStats;
            if (handlerPacketStats != null)
            {
                handlerPacketStats(m_packetsReceived - m_lastPacketsReceivedSentToScene, m_packetsSent - m_lastPacketsSentSentToScene, m_unAckedBytes);
                m_lastPacketsReceivedSentToScene = m_packetsReceived;
                m_lastPacketsSentSentToScene = m_packetsSent;
            }
        }

        #endregion

        // Previously ClientView.ProcessPackets

        public bool AddMoney(int debit)
        {
            if (m_moneyBalance + debit >= 0)
            {
                m_moneyBalance += debit;
                SendMoneyBalance(LLUUID.Zero, true, Helpers.StringToField("Poof Poof!"), m_moneyBalance);
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool m_packetProcessingEnabled = true; 

        public bool PacketProcessingEnabled {
            get { return m_packetProcessingEnabled; }
            set { m_packetProcessingEnabled = value; }
        }

        protected void ProcessInPacket(Packet Pack)
        {
            ack_pack(Pack);

            if (ProcessPacketMethod(Pack))
            {
                //there is a handler registered that handled this packet type 
                return;
            }
            else
            {
                Encoding _enc = Encoding.ASCII;

                switch (Pack.Type)
                {
                    #region  Scene/Avatar

                    case PacketType.AvatarPropertiesRequest:
                        AvatarPropertiesRequestPacket avatarProperties = (AvatarPropertiesRequestPacket)Pack;

                        handlerRequestAvatarProperties = OnRequestAvatarProperties;
                        if (handlerRequestAvatarProperties != null)
                        {
                            handlerRequestAvatarProperties(this, avatarProperties.AgentData.AvatarID);
                        }


                        break;
                    case PacketType.ChatFromViewer:
                        ChatFromViewerPacket inchatpack = (ChatFromViewerPacket)Pack;

                        string fromName = String.Empty; //ClientAvatar.firstname + " " + ClientAvatar.lastname;
                        byte[] message = inchatpack.ChatData.Message;
                        byte type = inchatpack.ChatData.Type;
                        LLVector3 fromPos = new LLVector3(); // ClientAvatar.Pos;
                        LLUUID fromAgentID = AgentId;

                        int channel = inchatpack.ChatData.Channel;

                        if (OnChatFromViewer != null)
                        {
                            ChatFromViewerArgs args = new ChatFromViewerArgs();
                            args.Channel = channel;
                            args.From = fromName;
                            args.Message = Helpers.FieldToUTF8String(message);
                            args.Type = (ChatTypeEnum)type;
                            args.Position = fromPos;

                            args.Scene = Scene;
                            args.Sender = this;

                            handlerChatFromViewer = OnChatFromViewer;
                            if (handlerChatFromViewer != null)
                                handlerChatFromViewer(this, args);
                        }
                        break;
                     case PacketType.AvatarPropertiesUpdate:
                         AvatarPropertiesUpdatePacket Packet = (AvatarPropertiesUpdatePacket)Pack;

                         handlerUpdateAvatarProperties = OnUpdateAvatarProperties;
                         if (handlerUpdateAvatarProperties != null)
                         {
                             AvatarPropertiesUpdatePacket.PropertiesDataBlock Properties = Packet.PropertiesData;
                             UserProfileData UserProfile = new UserProfileData();
                             UserProfile.UUID = AgentId;
                             UserProfile.profileAboutText = Util.FieldToString(Properties.AboutText);
                             UserProfile.profileFirstText = Util.FieldToString(Properties.FLAboutText);
                             UserProfile.profileFirstImage = Properties.FLImageID;
                             UserProfile.profileImage = Properties.ImageID;

                             handlerUpdateAvatarProperties(this, UserProfile);
                          }
                        break;

                    case PacketType.ScriptDialogReply:
                        ScriptDialogReplyPacket rdialog = (ScriptDialogReplyPacket)Pack;
                        int ch = rdialog.Data.ChatChannel;
                        byte[] msg = rdialog.Data.ButtonLabel;
                        if (OnChatFromViewer != null)
                        {
                            ChatFromViewerArgs args = new ChatFromViewerArgs();
                            args.Channel = ch;
                            args.From = String.Empty;
                            args.Message = Helpers.FieldToUTF8String(msg);
                            args.Type = ChatTypeEnum.Shout;
                            args.Position = new LLVector3();
                            args.Scene = Scene;
                            args.Sender = this;
                            handlerChatFromViewer2 = OnChatFromViewer;
                            if (handlerChatFromViewer2 != null)
                                handlerChatFromViewer2(this, args);
                        }

                        break;
                    case PacketType.ImprovedInstantMessage:
                        ImprovedInstantMessagePacket msgpack = (ImprovedInstantMessagePacket)Pack;
                        string IMfromName = Util.FieldToString(msgpack.MessageBlock.FromAgentName);
                        string IMmessage = Helpers.FieldToUTF8String(msgpack.MessageBlock.Message);
                        handlerInstantMessage = OnInstantMessage;

                        if (handlerInstantMessage != null)
                        {
                            handlerInstantMessage(this, msgpack.AgentData.AgentID, msgpack.AgentData.SessionID,
                                             msgpack.MessageBlock.ToAgentID, msgpack.MessageBlock.ID,
                                             msgpack.MessageBlock.Timestamp, IMfromName, IMmessage,
                                             msgpack.MessageBlock.Dialog, msgpack.MessageBlock.FromGroup,
                                             msgpack.MessageBlock.Offline, msgpack.MessageBlock.ParentEstateID,
                                             msgpack.MessageBlock.Position, msgpack.MessageBlock.RegionID,
                                             msgpack.MessageBlock.BinaryBucket);
                        }


                        break;

                    case PacketType.AcceptFriendship:
                        AcceptFriendshipPacket afriendpack = (AcceptFriendshipPacket)Pack;

                        // My guess is this is the folder to stick the calling card into
                        List<LLUUID> callingCardFolders = new List<LLUUID>();

                        LLUUID agentID = afriendpack.AgentData.AgentID;
                        LLUUID transactionID = afriendpack.TransactionBlock.TransactionID;

                        for (int fi = 0; fi < afriendpack.FolderData.Length; fi++)
                        {
                            callingCardFolders.Add(afriendpack.FolderData[fi].FolderID);
                        }

                        handlerApproveFriendRequest = OnApproveFriendRequest;
                        if (handlerApproveFriendRequest != null)
                        {
                            handlerApproveFriendRequest(this, agentID, transactionID, callingCardFolders);
                        }




                        break;
                    case PacketType.TerminateFriendship:
                        TerminateFriendshipPacket tfriendpack = (TerminateFriendshipPacket)Pack;
                        LLUUID listOwnerAgentID = tfriendpack.AgentData.AgentID;
                        LLUUID exFriendID = tfriendpack.ExBlock.OtherID;

                        handlerTerminateFriendship = OnTerminateFriendship;
                        if (handlerTerminateFriendship != null)
                        {
                            handlerTerminateFriendship(this, listOwnerAgentID, exFriendID);
                        }


                        break;
                    case PacketType.RezObject:
                        RezObjectPacket rezPacket = (RezObjectPacket)Pack;

                        handlerRezObject = OnRezObject;
                        if (handlerRezObject != null)
                        {
                            //rezPacket.RezData.BypassRaycast;
                            //rezPacket.RezData.RayEnd;
                            //rezPacket.RezData.RayEndIsIntersection;
                            //rezPacket.RezData.RayStart;
                            //rezPacket.RezData.RayTargetID;
                            //rezPacket.RezData.RemoveItem;
                            //rezPacket.RezData.RezSelected;
                            //rezPacket.RezData.FromTaskID;
                            //m_log.Info("[REZData]: " + rezPacket.ToString());

                            handlerRezObject(this, rezPacket.InventoryData.ItemID, rezPacket.RezData.RayEnd,
                                rezPacket.RezData.RayStart, rezPacket.RezData.RayTargetID,
                                rezPacket.RezData.BypassRaycast, rezPacket.RezData.RayEndIsIntersection,
                                rezPacket.RezData.EveryoneMask, rezPacket.RezData.GroupMask,
                                rezPacket.RezData.NextOwnerMask, rezPacket.RezData.ItemFlags,
                                rezPacket.RezData.RezSelected, rezPacket.RezData.RemoveItem,
                                rezPacket.RezData.FromTaskID);
                        }

                        break;
                    case PacketType.DeRezObject:
                        handlerDeRezObject = OnDeRezObject;
                        if (handlerDeRezObject != null)
                        {
                            handlerDeRezObject(Pack, this);
                        }

                        break;
                    case PacketType.ModifyLand:
                        ModifyLandPacket modify = (ModifyLandPacket)Pack;
                        //m_log.Info("[LAND]: LAND:" + modify.ToString());
                        if (modify.ParcelData.Length > 0)
                        {
                            if (OnModifyTerrain != null)
                            {

                                for (int i = 0; i < modify.ParcelData.Length; i++)
                                {
                                    handlerModifyTerrain = OnModifyTerrain;
                                    if (handlerModifyTerrain != null)
                                    {
                                        handlerModifyTerrain(modify.ModifyBlock.Height, modify.ModifyBlock.Seconds,
                                                        modify.ModifyBlock.BrushSize,
                                                        modify.ModifyBlock.Action, modify.ParcelData[i].North,
                                                        modify.ParcelData[i].West, modify.ParcelData[i].South,
                                                        modify.ParcelData[i].East, this);
                                    }
                                }
                            }
                        }

                        break;
                    case PacketType.RegionHandshakeReply:

                        handlerRegionHandShakeReply = OnRegionHandShakeReply;
                        if (handlerRegionHandShakeReply != null)
                        {
                            handlerRegionHandShakeReply(this);
                        }

                        break;
                    case PacketType.AgentWearablesRequest:
                        handlerRequestWearables = OnRequestWearables;

                        if (handlerRequestWearables != null)
                        {
                            handlerRequestWearables();
                        }


                        handlerRequestAvatarsData = OnRequestAvatarsData;

                        if (handlerRequestAvatarsData != null)
                        {
                            handlerRequestAvatarsData(this);
                        }


                        break;
                    case PacketType.AgentSetAppearance:
                        AgentSetAppearancePacket appear = (AgentSetAppearancePacket)Pack;

                        handlerSetAppearance = OnSetAppearance;
                        if (handlerSetAppearance != null)
                        {
                            handlerSetAppearance(appear.ObjectData.TextureEntry, appear.VisualParam);
                        }

                        break;
                    case PacketType.AgentIsNowWearing:
                        if (OnAvatarNowWearing != null)
                        {
                            AgentIsNowWearingPacket nowWearing = (AgentIsNowWearingPacket)Pack;
                            AvatarWearingArgs wearingArgs = new AvatarWearingArgs();
                            for (int i = 0; i < nowWearing.WearableData.Length; i++)
                            {
                                AvatarWearingArgs.Wearable wearable =
                                    new AvatarWearingArgs.Wearable(nowWearing.WearableData[i].ItemID,
                                                                   nowWearing.WearableData[i].WearableType);
                                wearingArgs.NowWearing.Add(wearable);
                            }

                            handlerAvatarNowWearing = OnAvatarNowWearing;
                            if (handlerAvatarNowWearing != null)
                            {
                                handlerAvatarNowWearing(this, wearingArgs);
                            }


                        }
                        break;
                    case PacketType.RezSingleAttachmentFromInv:

                        handlerRezSingleAttachment = OnRezSingleAttachmentFromInv;
                        if (handlerRezSingleAttachment != null)
                        {
                            RezSingleAttachmentFromInvPacket rez = (RezSingleAttachmentFromInvPacket)Pack;
                            handlerRezSingleAttachment(this, rez.ObjectData.ItemID,
                                        rez.ObjectData.AttachmentPt, rez.ObjectData.ItemFlags, rez.ObjectData.NextOwnerMask);
                        }

                        break;
                    case PacketType.ObjectAttach:


                        if (OnObjectAttach != null)
                        {
                            ObjectAttachPacket att = (ObjectAttachPacket)Pack;

                            handlerObjectAttach = OnObjectAttach;

                            if (handlerObjectAttach != null)
                            {
                                handlerObjectAttach(this, att.ObjectData[0].ObjectLocalID, att.AgentData.AttachmentPoint, att.ObjectData[0].Rotation);
                            }
                        }

                        break;
                    case PacketType.SetAlwaysRun:
                        SetAlwaysRunPacket run = (SetAlwaysRunPacket)Pack;

                        handlerSetAlwaysRun = OnSetAlwaysRun;
                        if (handlerSetAlwaysRun != null)
                            handlerSetAlwaysRun(this, run.AgentData.AlwaysRun);



                        break;
                    case PacketType.CompleteAgentMovement:

                        handlerCompleteMovementToRegion = OnCompleteMovementToRegion;
                        if (handlerCompleteMovementToRegion != null)
                        {
                            handlerCompleteMovementToRegion();
                        }
                        handlerCompleteMovementToRegion = null;

                        break;
                    case PacketType.AgentUpdate:
                        if (OnAgentUpdate != null)
                        {
                            AgentUpdatePacket agenUpdate = (AgentUpdatePacket)Pack;

                            handlerAgentUpdate = OnAgentUpdate;
                            if (handlerAgentUpdate != null)
                                OnAgentUpdate(this, agenUpdate);

                            handlerAgentUpdate = null;
                            //agenUpdate.AgentData.ControlFlags, agenUpdate.AgentData.BodyRotationa);
                        }
                        break;
                    case PacketType.AgentAnimation:
                        AgentAnimationPacket AgentAni = (AgentAnimationPacket)Pack;

                        handlerStartAnim = null;
                        handlerStopAnim = null;

                        for (int i = 0; i < AgentAni.AnimationList.Length; i++)
                        {
                            if (AgentAni.AnimationList[i].StartAnim)
                            {
                                handlerStartAnim = OnStartAnim;
                                if (handlerStartAnim != null)
                                {
                                    handlerStartAnim(this, AgentAni.AnimationList[i].AnimID, 1);
                                }
                            }
                            else
                            {
                                handlerStopAnim = OnStopAnim;
                                if (handlerStopAnim != null)
                                {
                                    handlerStopAnim(this, AgentAni.AnimationList[i].AnimID);
                                }
                            }
                        }
                        break;
                    case PacketType.AgentRequestSit:
                        if (OnAgentRequestSit != null)
                        {
                            AgentRequestSitPacket agentRequestSit = (AgentRequestSitPacket)Pack;

                            handlerAgentRequestSit = OnAgentRequestSit;
                            if (handlerAgentRequestSit != null)
                                handlerAgentRequestSit(this, agentRequestSit.AgentData.AgentID,
                                              agentRequestSit.TargetObject.TargetID, agentRequestSit.TargetObject.Offset);
                        }
                        break;
                    case PacketType.AgentSit:
                        if (OnAgentSit != null)
                        {
                            AgentSitPacket agentSit = (AgentSitPacket)Pack;

                            handlerAgentSit = OnAgentSit;
                            if (handlerAgentSit != null)
                            {
                                OnAgentSit(this, agentSit.AgentData.AgentID);
                            }
                        }
                        break;
                    case PacketType.AvatarPickerRequest:
                        AvatarPickerRequestPacket avRequestQuery = (AvatarPickerRequestPacket)Pack;
                        AvatarPickerRequestPacket.AgentDataBlock Requestdata = avRequestQuery.AgentData;
                        AvatarPickerRequestPacket.DataBlock querydata = avRequestQuery.Data;
                        //System.Console.WriteLine("Agent Sends:" + Helpers.FieldToUTF8String(querydata.Name));

                        handlerAvatarPickerRequest = OnAvatarPickerRequest;
                        if (handlerAvatarPickerRequest != null)
                        {
                            handlerAvatarPickerRequest(this, Requestdata.AgentID, Requestdata.QueryID,
                                                  Helpers.FieldToUTF8String(querydata.Name));
                        }
                        break;
                    case PacketType.AgentDataUpdateRequest:
                        AgentDataUpdateRequestPacket avRequestDataUpdatePacket = (AgentDataUpdateRequestPacket)Pack;

                        handlerAgentDataUpdateRequest = OnAgentDataUpdateRequest;

                        if (handlerAgentDataUpdateRequest != null)
                        {
                            handlerAgentDataUpdateRequest(this, avRequestDataUpdatePacket.AgentData.AgentID, avRequestDataUpdatePacket.AgentData.SessionID);
                        }

                        break;
                    case PacketType.UserInfoRequest:
                        UserInfoRequestPacket avUserInfoRequestPacket = (UserInfoRequestPacket)Pack;

                        handlerUserInfoRequest = OnUserInfoRequest;
                        if (handlerUserInfoRequest != null)
                        {
                            handlerUserInfoRequest(this, avUserInfoRequestPacket.AgentData.AgentID, avUserInfoRequestPacket.AgentData.SessionID);

                        }
                        break;

                    case PacketType.SetStartLocationRequest:
                        SetStartLocationRequestPacket avSetStartLocationRequestPacket = (SetStartLocationRequestPacket)Pack;

                        if (avSetStartLocationRequestPacket.AgentData.AgentID == AgentId && avSetStartLocationRequestPacket.AgentData.SessionID == SessionId)
                        {
                            handlerSetStartLocationRequest = OnSetStartLocationRequest;
                            if (handlerSetStartLocationRequest != null)
                            {
                                handlerSetStartLocationRequest(this, 0, avSetStartLocationRequestPacket.StartLocationData.LocationPos,
                                                            avSetStartLocationRequestPacket.StartLocationData.LocationLookAt,
                                                            avSetStartLocationRequestPacket.StartLocationData.LocationID);
                            }
                        }
                        break;

                    case PacketType.AgentThrottle:
                        AgentThrottlePacket atpack = (AgentThrottlePacket)Pack;
                        m_packetQueue.SetThrottleFromClient(atpack.Throttle.Throttles);
                        break;

                    #endregion

                    #region Objects/m_sceneObjects

                    case PacketType.ObjectLink:
                        ObjectLinkPacket link = (ObjectLinkPacket)Pack;
                        uint parentprimid = 0;
                        List<uint> childrenprims = new List<uint>();
                        if (link.ObjectData.Length > 1)
                        {
                            parentprimid = link.ObjectData[0].ObjectLocalID;

                            for (int i = 1; i < link.ObjectData.Length; i++)
                            {
                                childrenprims.Add(link.ObjectData[i].ObjectLocalID);
                            }
                        }
                        handlerLinkObjects = OnLinkObjects;
                        if (handlerLinkObjects != null)
                        {
                            handlerLinkObjects(parentprimid, childrenprims);
                        }
                        break;
                    case PacketType.ObjectDelink:
                        ObjectDelinkPacket delink = (ObjectDelinkPacket)Pack;

                        // It appears the prim at index 0 is not always the root prim (for
                        // instance, when one prim of a link set has been edited independently
                        // of the others).  Therefore, we'll pass all the ids onto the delink
                        // method for it to decide which is the root.
                        List<uint> prims = new List<uint>();
                        for (int i = 0; i < delink.ObjectData.Length; i++)
                        {
                            prims.Add(delink.ObjectData[i].ObjectLocalID);
                        }
                        handlerDelinkObjects = OnDelinkObjects;
                        if (handlerDelinkObjects != null)
                        {
                            handlerDelinkObjects(prims);
                        }

                        break;
                    case PacketType.ObjectAdd:
                        if (OnAddPrim != null)
                        {
                            ObjectAddPacket addPacket = (ObjectAddPacket)Pack;
                            PrimitiveBaseShape shape = GetShapeFromAddPacket(addPacket);
                            // m_log.Info("[REZData]: " + addPacket.ToString());
                            //BypassRaycast: 1
                            //RayStart: <69.79469, 158.2652, 98.40343>
                            //RayEnd: <61.97724, 141.995, 92.58341>   
                            //RayTargetID: 00000000-0000-0000-0000-000000000000

                            handlerAddPrim = OnAddPrim;
                            if (handlerAddPrim != null)
                                handlerAddPrim(AgentId, addPacket.ObjectData.RayEnd, addPacket.ObjectData.Rotation, shape, addPacket.ObjectData.BypassRaycast, addPacket.ObjectData.RayStart, addPacket.ObjectData.RayTargetID, addPacket.ObjectData.RayEndIsIntersection);
                        }
                        break;
                    case PacketType.ObjectShape:
                        ObjectShapePacket shapePacket = (ObjectShapePacket)Pack;
                        handlerUpdatePrimShape = null;
                        for (int i = 0; i < shapePacket.ObjectData.Length; i++)
                        {
                            handlerUpdatePrimShape = OnUpdatePrimShape;
                            if (handlerUpdatePrimShape != null)
                            {
                                handlerUpdatePrimShape(m_agentId, shapePacket.ObjectData[i].ObjectLocalID,
                                                  shapePacket.ObjectData[i]);
                            }
                        }
                        break;
                    case PacketType.ObjectExtraParams:
                        ObjectExtraParamsPacket extraPar = (ObjectExtraParamsPacket)Pack;

                        handlerUpdateExtraParams = OnUpdateExtraParams;
                        if (handlerUpdateExtraParams != null)
                        {
                            handlerUpdateExtraParams(m_agentId, extraPar.ObjectData[0].ObjectLocalID,
                                            extraPar.ObjectData[0].ParamType,
                                            extraPar.ObjectData[0].ParamInUse, extraPar.ObjectData[0].ParamData);
                        }
                        break;
                    case PacketType.ObjectDuplicate:
                        ObjectDuplicatePacket dupe = (ObjectDuplicatePacket)Pack;
                        ObjectDuplicatePacket.AgentDataBlock AgentandGroupData = dupe.AgentData;

                        handlerObjectDuplicate = null;

                        for (int i = 0; i < dupe.ObjectData.Length; i++)
                        {
                            handlerObjectDuplicate = OnObjectDuplicate;
                            if (handlerObjectDuplicate != null)
                            {
                                handlerObjectDuplicate(dupe.ObjectData[i].ObjectLocalID, dupe.SharedData.Offset,
                                                  dupe.SharedData.DuplicateFlags, AgentandGroupData.AgentID,
                                                  AgentandGroupData.GroupID);
                            }
                        }

                        break;

                    case PacketType.ObjectSelect:
                        ObjectSelectPacket incomingselect = (ObjectSelectPacket)Pack;

                        handlerObjectSelect = null;

                        for (int i = 0; i < incomingselect.ObjectData.Length; i++)
                        {
                            handlerObjectSelect = OnObjectSelect;
                            if (handlerObjectSelect != null)
                            {
                                handlerObjectSelect(incomingselect.ObjectData[i].ObjectLocalID, this);
                            }
                        }
                        break;
                    case PacketType.ObjectDeselect:
                        ObjectDeselectPacket incomingdeselect = (ObjectDeselectPacket)Pack;

                        handlerObjectDeselect = null;

                        for (int i = 0; i < incomingdeselect.ObjectData.Length; i++)
                        {
                            handlerObjectDeselect = OnObjectDeselect;
                            if (handlerObjectDeselect != null)
                            {
                                OnObjectDeselect(incomingdeselect.ObjectData[i].ObjectLocalID, this);
                            }
                        }
                        break;
                    case PacketType.ObjectFlagUpdate:
                        ObjectFlagUpdatePacket flags = (ObjectFlagUpdatePacket)Pack;

                        handlerUpdatePrimFlags = OnUpdatePrimFlags;

                        if (handlerUpdatePrimFlags != null)
                        {
                            handlerUpdatePrimFlags(flags.AgentData.ObjectLocalID, Pack, this);
                        }
                        break;
                    case PacketType.ObjectImage:
                        ObjectImagePacket imagePack = (ObjectImagePacket)Pack;

                        handlerUpdatePrimTexture = null;
                        for (int i = 0; i < imagePack.ObjectData.Length; i++)
                        {
                            handlerUpdatePrimTexture = OnUpdatePrimTexture;
                            if (handlerUpdatePrimTexture != null)
                            {
                                handlerUpdatePrimTexture(imagePack.ObjectData[i].ObjectLocalID,
                                           imagePack.ObjectData[i].TextureEntry, this);
                            }
                        }
                        break;
                    case PacketType.ObjectGrab:
                        ObjectGrabPacket grab = (ObjectGrabPacket)Pack;

                        handlerGrabObject = OnGrabObject;

                        if (handlerGrabObject != null)
                        {
                            handlerGrabObject(grab.ObjectData.LocalID, grab.ObjectData.GrabOffset, this);
                        }
                        break;
                    case PacketType.ObjectGrabUpdate:
                        ObjectGrabUpdatePacket grabUpdate = (ObjectGrabUpdatePacket)Pack;

                        handlerGrabUpdate = OnGrabUpdate;

                        if (handlerGrabUpdate != null)
                        {
                            handlerGrabUpdate(grabUpdate.ObjectData.ObjectID, grabUpdate.ObjectData.GrabOffsetInitial,
                                       grabUpdate.ObjectData.GrabPosition, this);
                        }
                        break;
                    case PacketType.ObjectDeGrab:
                        ObjectDeGrabPacket deGrab = (ObjectDeGrabPacket)Pack;

                        handlerDeGrabObject = OnDeGrabObject;
                        if (handlerDeGrabObject != null)
                        {
                            handlerDeGrabObject(deGrab.ObjectData.LocalID, this);
                        }
                        break;
                    case PacketType.ObjectDescription:
                        ObjectDescriptionPacket objDes = (ObjectDescriptionPacket)Pack;

                        handlerObjectDescription = null;

                        for (int i = 0; i < objDes.ObjectData.Length; i++)
                        {
                            handlerObjectDescription = OnObjectDescription;
                            if (handlerObjectDescription != null)
                            {
                                handlerObjectDescription(this, objDes.ObjectData[i].LocalID,
                                           m_encoding.GetString(objDes.ObjectData[i].Description));
                            }
                        }
                        break;
                    case PacketType.ObjectName:
                        ObjectNamePacket objName = (ObjectNamePacket)Pack;

                        handlerObjectName = null;
                        for (int i = 0; i < objName.ObjectData.Length; i++)
                        {
                            handlerObjectName = OnObjectName;
                            if (handlerObjectName != null)
                            {
                                handlerObjectName(this, objName.ObjectData[i].LocalID,
                                           m_encoding.GetString(objName.ObjectData[i].Name));
                            }
                        }
                        break;
                    case PacketType.ObjectPermissions:
                        if (OnObjectPermissions != null)
                        {
                            ObjectPermissionsPacket newobjPerms = (ObjectPermissionsPacket)Pack;

                            LLUUID AgentID = newobjPerms.AgentData.AgentID;
                            LLUUID SessionID = newobjPerms.AgentData.SessionID;

                            handlerObjectPermissions = null;

                            for (int i = 0; i < newobjPerms.ObjectData.Length; i++)
                            {
                                ObjectPermissionsPacket.ObjectDataBlock permChanges = newobjPerms.ObjectData[i];


                                byte field = permChanges.Field;
                                uint localID = permChanges.ObjectLocalID;
                                uint mask = permChanges.Mask;
                                byte set = permChanges.Set;

                                handlerObjectPermissions = OnObjectPermissions;

                                if (handlerObjectPermissions != null)
                                    OnObjectPermissions(this, AgentID, SessionID, field, localID, mask, set);
                            }
                        }

                        // Here's our data,  
                        // PermField contains the field the info goes into
                        // PermField determines which mask we're changing
                        // 
                        // chmask is the mask of the change
                        // setTF is whether we're adding it or taking it away
                        //
                        // objLocalID is the localID of the object.

                        // Unfortunately, we have to pass the event the packet because objData is an array
                        // That means multiple object perms may be updated in a single packet.


                        break;

                    case PacketType.RequestObjectPropertiesFamily:
                        //This powers the little tooltip that appears when you move your mouse over an object
                        RequestObjectPropertiesFamilyPacket packToolTip = (RequestObjectPropertiesFamilyPacket)Pack;


                        RequestObjectPropertiesFamilyPacket.ObjectDataBlock packObjBlock = packToolTip.ObjectData;

                        handlerRequestObjectPropertiesFamily = OnRequestObjectPropertiesFamily;

                        if (handlerRequestObjectPropertiesFamily != null)
                        {
                            handlerRequestObjectPropertiesFamily(this, m_agentId, packObjBlock.RequestFlags,
                                       packObjBlock.ObjectID);
                        }

                        break;

                    #endregion

                    #region Inventory/Asset/Other related packets

                    case PacketType.RequestImage:
                        RequestImagePacket imageRequest = (RequestImagePacket)Pack;
                        //Console.WriteLine("image request: " + Pack.ToString());

                        handlerTextureRequest = null;

                        for (int i = 0; i < imageRequest.RequestImage.Length; i++)
                        {
                            if (OnRequestTexture != null)
                            {
                                TextureRequestArgs args = new TextureRequestArgs();
                                args.RequestedAssetID = imageRequest.RequestImage[i].Image;
                                args.DiscardLevel = imageRequest.RequestImage[i].DiscardLevel;
                                args.PacketNumber = imageRequest.RequestImage[i].Packet;
                                args.Priority = imageRequest.RequestImage[i].DownloadPriority;

                                handlerTextureRequest = OnRequestTexture;

                                if (handlerTextureRequest != null)
                                    OnRequestTexture(this, args);
                            }
                        }
                        break;
                    case PacketType.TransferRequest:
                        //Console.WriteLine("ClientView.ProcessPackets.cs:ProcessInPacket() - Got transfer request");
                        TransferRequestPacket transfer = (TransferRequestPacket)Pack;
                        m_assetCache.AddAssetRequest(this, transfer);
                        /* RequestAsset = OnRequestAsset;
                         if (RequestAsset != null)
                         {
                             RequestAsset(this, transfer);
                         }*/
                        break;
                    case PacketType.AssetUploadRequest:
                        AssetUploadRequestPacket request = (AssetUploadRequestPacket)Pack;
                        // Console.WriteLine("upload request " + Pack.ToString());
                        // Console.WriteLine("upload request was for assetid: " + request.AssetBlock.TransactionID.Combine(this.SecureSessionId).ToString());
                        LLUUID temp = LLUUID.Combine(request.AssetBlock.TransactionID, SecureSessionId);

                        handlerAssetUploadRequest = OnAssetUploadRequest;

                        if (handlerAssetUploadRequest != null)
                        {

                            handlerAssetUploadRequest(this, temp,
                                                 request.AssetBlock.TransactionID, request.AssetBlock.Type,
                                                 request.AssetBlock.AssetData, request.AssetBlock.StoreLocal,
                                                 request.AssetBlock.Tempfile);
                        }
                        break;
                    case PacketType.RequestXfer:
                        RequestXferPacket xferReq = (RequestXferPacket)Pack;

                        handlerRequestXfer = OnRequestXfer;

                        if (handlerRequestXfer != null)
                        {
                            handlerRequestXfer(this, xferReq.XferID.ID, Util.FieldToString(xferReq.XferID.Filename));
                        }
                        break;
                    case PacketType.SendXferPacket:
                        SendXferPacketPacket xferRec = (SendXferPacketPacket)Pack;

                        handlerXferReceive = OnXferReceive;
                        if (handlerXferReceive != null)
                        {
                            handlerXferReceive(this, xferRec.XferID.ID, xferRec.XferID.Packet, xferRec.DataPacket.Data);
                        }
                        break;
                    case PacketType.ConfirmXferPacket:
                        ConfirmXferPacketPacket confirmXfer = (ConfirmXferPacketPacket)Pack;

                        handlerConfirmXfer = OnConfirmXfer;
                        if (handlerConfirmXfer != null)
                        {
                            handlerConfirmXfer(this, confirmXfer.XferID.ID, confirmXfer.XferID.Packet);
                        }
                        break;
                    case PacketType.CreateInventoryFolder:
                        CreateInventoryFolderPacket invFolder = (CreateInventoryFolderPacket)Pack;

                        handlerCreateInventoryFolder = OnCreateNewInventoryFolder;
                        if (handlerCreateInventoryFolder != null)
                        {

                            handlerCreateInventoryFolder(this, invFolder.FolderData.FolderID,
                                       (ushort)invFolder.FolderData.Type,
                                       Util.FieldToString(invFolder.FolderData.Name),
                                       invFolder.FolderData.ParentID);
                        }
                        break;
                    case PacketType.UpdateInventoryFolder:
                        if (OnUpdateInventoryFolder != null)
                        {
                            UpdateInventoryFolderPacket invFolderx = (UpdateInventoryFolderPacket)Pack;

                            handlerUpdateInventoryFolder = null;

                            for (int i = 0; i < invFolderx.FolderData.Length; i++)
                            {
                                handlerUpdateInventoryFolder = OnUpdateInventoryFolder;
                                if (handlerUpdateInventoryFolder != null)
                                {
                                    OnUpdateInventoryFolder(this, invFolderx.FolderData[i].FolderID,
                                                            (ushort)invFolderx.FolderData[i].Type,
                                                            Util.FieldToString(invFolderx.FolderData[i].Name),
                                                            invFolderx.FolderData[i].ParentID);
                                }
                            }
                        }
                        break;
                    case PacketType.MoveInventoryFolder:
                        if (OnMoveInventoryFolder != null)
                        {
                            MoveInventoryFolderPacket invFoldery = (MoveInventoryFolderPacket)Pack;

                            handlerMoveInventoryFolder = null;

                            for (int i = 0; i < invFoldery.InventoryData.Length; i++)
                            {
                                handlerMoveInventoryFolder = OnMoveInventoryFolder;
                                if (handlerMoveInventoryFolder != null)
                                {
                                    OnMoveInventoryFolder(this, invFoldery.InventoryData[i].FolderID,
                                                          invFoldery.InventoryData[i].ParentID);
                                }
                            }
                        }
                        break;
                    case PacketType.CreateInventoryItem:
                        CreateInventoryItemPacket createItem = (CreateInventoryItemPacket)Pack;

                        handlerCreateNewInventoryItem = OnCreateNewInventoryItem;
                        if (handlerCreateNewInventoryItem != null)
                        {
                            handlerCreateNewInventoryItem(this, createItem.InventoryBlock.TransactionID,
                                         createItem.InventoryBlock.FolderID,
                                         createItem.InventoryBlock.CallbackID,
                                         Util.FieldToString(createItem.InventoryBlock.Description),
                                         Util.FieldToString(createItem.InventoryBlock.Name),
                                         createItem.InventoryBlock.InvType,
                                         createItem.InventoryBlock.Type,
                                         createItem.InventoryBlock.WearableType,
                                         createItem.InventoryBlock.NextOwnerMask);
                        }
                        break;
                    case PacketType.FetchInventory:
                        if (OnFetchInventory != null)
                        {
                            FetchInventoryPacket FetchInventoryx = (FetchInventoryPacket)Pack;

                            handlerFetchInventory = null;

                            for (int i = 0; i < FetchInventoryx.InventoryData.Length; i++)
                            {
                                handlerFetchInventory = OnFetchInventory;

                                if (handlerFetchInventory != null)
                                {
                                    OnFetchInventory(this, FetchInventoryx.InventoryData[i].ItemID,
                                                     FetchInventoryx.InventoryData[i].OwnerID);
                                }
                            }
                        }
                        break;
                    case PacketType.FetchInventoryDescendents:
                        FetchInventoryDescendentsPacket Fetch = (FetchInventoryDescendentsPacket)Pack;

                        handlerFetchInventoryDescendents = OnFetchInventoryDescendents;
                        if (handlerFetchInventoryDescendents != null)
                        {

                            handlerFetchInventoryDescendents(this, Fetch.InventoryData.FolderID, Fetch.InventoryData.OwnerID,
                                        Fetch.InventoryData.FetchFolders, Fetch.InventoryData.FetchItems,
                                        Fetch.InventoryData.SortOrder);
                        }
                        break;
                    case PacketType.PurgeInventoryDescendents:

                        PurgeInventoryDescendentsPacket Purge = (PurgeInventoryDescendentsPacket)Pack;

                        handlerPurgeInventoryDescendents = OnPurgeInventoryDescendents;
                        if (handlerPurgeInventoryDescendents != null)
                        {
                            handlerPurgeInventoryDescendents(this, Purge.InventoryData.FolderID);
                        }
                        break;
                    case PacketType.UpdateInventoryItem:
                        UpdateInventoryItemPacket update = (UpdateInventoryItemPacket)Pack;
                        if (OnUpdateInventoryItem != null)
                        {
                            handlerUpdateInventoryItem = null;
                            for (int i = 0; i < update.InventoryData.Length; i++)
                            {
                                handlerUpdateInventoryItem = OnUpdateInventoryItem;

                                if (handlerUpdateInventoryItem != null)
                                {
                                    OnUpdateInventoryItem(this, update.InventoryData[i].TransactionID,
                                                          update.InventoryData[i].ItemID,
                                                          Util.FieldToString(update.InventoryData[i].Name),
                                                          Util.FieldToString(update.InventoryData[i].Description),
                                                          update.InventoryData[i].NextOwnerMask);
                                }
                            }
                        }
                        //Console.WriteLine(Pack.ToString());
                        /*for (int i = 0; i < update.InventoryData.Length; i++)
                        {
                            if (update.InventoryData[i].TransactionID != LLUUID.Zero)
                            {
                                AssetBase asset = m_assetCache.GetAsset(update.InventoryData[i].TransactionID.Combine(this.SecureSessionId));
                                if (asset != null)
                                {
                                    // Console.WriteLine("updating inventory item, found asset" + asset.FullID.ToString() + " already in cache");
                                    m_inventoryCache.UpdateInventoryItemAsset(this, update.InventoryData[i].ItemID, asset);
                                }
                                else
                                {
                                    asset = this.UploadAssets.AddUploadToAssetCache(update.InventoryData[i].TransactionID);
                                    if (asset != null)
                                    {
                                        //Console.WriteLine("updating inventory item, adding asset" + asset.FullID.ToString() + " to cache");
                                        m_inventoryCache.UpdateInventoryItemAsset(this, update.InventoryData[i].ItemID, asset);
                                    }
                                    else
                                    {
                                        //Console.WriteLine("trying to update inventory item, but asset is null");
                                    }
                                }
                            }
                            else
                            {
                                m_inventoryCache.UpdateInventoryItemDetails(this, update.InventoryData[i].ItemID, update.InventoryData[i]); ;
                            }
                        }*/
                        break;
                    case PacketType.CopyInventoryItem:
                        CopyInventoryItemPacket copyitem = (CopyInventoryItemPacket)Pack;

                        handlerCopyInventoryItem = null;
                        if (OnCopyInventoryItem != null)
                        {
                            foreach (CopyInventoryItemPacket.InventoryDataBlock datablock in copyitem.InventoryData)
                            {
                                handlerCopyInventoryItem = OnCopyInventoryItem;
                                if (handlerCopyInventoryItem != null)
                                {
                                    handlerCopyInventoryItem(this, datablock.CallbackID, datablock.OldAgentID,
                                                        datablock.OldItemID, datablock.NewFolderID,
                                                        Util.FieldToString(datablock.NewName));
                                }
                            }
                        }
                        break;
                    case PacketType.MoveInventoryItem:
                        MoveInventoryItemPacket moveitem = (MoveInventoryItemPacket)Pack;
                        if (OnMoveInventoryItem != null)
                        {
                            handlerMoveInventoryItem = null;
                            foreach (MoveInventoryItemPacket.InventoryDataBlock datablock in moveitem.InventoryData)
                            {
                                handlerMoveInventoryItem = OnMoveInventoryItem;
                                if (handlerMoveInventoryItem != null)
                                {
                                    handlerMoveInventoryItem(this, datablock.FolderID, datablock.ItemID, datablock.Length,
                                                Util.FieldToString(datablock.NewName));
                                }
                            }
                        }
                        break;
                    case PacketType.RemoveInventoryItem:
                        RemoveInventoryItemPacket removeItem = (RemoveInventoryItemPacket)Pack;
                        if (OnRemoveInventoryItem != null)
                        {
                            handlerRemoveInventoryItem = null;
                            foreach (RemoveInventoryItemPacket.InventoryDataBlock datablock in removeItem.InventoryData)
                            {
                                handlerRemoveInventoryItem = OnRemoveInventoryItem;
                                if (handlerRemoveInventoryItem != null)
                                {
                                    handlerRemoveInventoryItem(this, datablock.ItemID);
                                }
                            }
                        }
                        break;
                    case PacketType.RemoveInventoryFolder:
                        RemoveInventoryFolderPacket removeFolder = (RemoveInventoryFolderPacket)Pack;
                        if (OnRemoveInventoryFolder != null)
                        {
                            handlerRemoveInventoryFolder = null;
                            foreach (RemoveInventoryFolderPacket.FolderDataBlock datablock in removeFolder.FolderData)
                            {
                                handlerRemoveInventoryFolder = OnRemoveInventoryFolder;

                                if (handlerRemoveInventoryFolder != null)
                                {
                                    handlerRemoveInventoryFolder(this, datablock.FolderID);
                                }
                            }
                        }
                        break;
                    case PacketType.RequestTaskInventory:
                        RequestTaskInventoryPacket requesttask = (RequestTaskInventoryPacket)Pack;

                        handlerRequestTaskInventory = OnRequestTaskInventory;
                        if (handlerRequestTaskInventory != null)
                        {
                            handlerRequestTaskInventory(this, requesttask.InventoryData.LocalID);
                        }
                        break;
                    case PacketType.UpdateTaskInventory:
                        UpdateTaskInventoryPacket updatetask = (UpdateTaskInventoryPacket)Pack;
                        if (OnUpdateTaskInventory != null)
                        {
                            if (updatetask.UpdateData.Key == 0)
                            {
                                handlerUpdateTaskInventory = OnUpdateTaskInventory;
                                if (handlerUpdateTaskInventory != null)
                                {
                                    handlerUpdateTaskInventory(this, updatetask.InventoryData.ItemID,
                                                updatetask.InventoryData.FolderID, updatetask.UpdateData.LocalID);
                                }
                            }
                        }
                        break;
                    case PacketType.RemoveTaskInventory:
                        RemoveTaskInventoryPacket removeTask = (RemoveTaskInventoryPacket)Pack;

                        handlerRemoveTaskItem = OnRemoveTaskItem;

                        if (handlerRemoveTaskItem != null)
                        {
                            handlerRemoveTaskItem(this, removeTask.InventoryData.ItemID, removeTask.InventoryData.LocalID);
                        }
                        break;
                    case PacketType.MoveTaskInventory:
                        m_log.Warn("[CLIENT]: unhandled MoveTaskInventory packet");
                        break;
                    case PacketType.RezScript:
                        //Console.WriteLine(Pack.ToString());
                        RezScriptPacket rezScriptx = (RezScriptPacket)Pack;

                        handlerRezScript = OnRezScript;

                        if (handlerRezScript != null)
                        {
                            handlerRezScript(this, rezScriptx.InventoryBlock.ItemID, rezScriptx.UpdateBlock.ObjectLocalID);
                        }
                        break;
                    case PacketType.MapLayerRequest:
                        RequestMapLayer();
                        break;
                    case PacketType.MapBlockRequest:
                        MapBlockRequestPacket MapRequest = (MapBlockRequestPacket)Pack;

                        handlerRequestMapBlocks = OnRequestMapBlocks;
                        if (handlerRequestMapBlocks != null)
                        {
                            handlerRequestMapBlocks(this, MapRequest.PositionData.MinX, MapRequest.PositionData.MinY,
                                               MapRequest.PositionData.MaxX, MapRequest.PositionData.MaxY);
                        }
                        break;
                    case PacketType.MapNameRequest:
                        MapNameRequestPacket map = (MapNameRequestPacket)Pack;
                        string mapName = UTF8Encoding.UTF8.GetString(map.NameData.Name, 0,
                                                                     map.NameData.Name.Length - 1);
                        handlerMapNameRequest = OnMapNameRequest;
                        if (handlerMapNameRequest != null)
                        {
                            handlerMapNameRequest(this, mapName);
                        }
                        break;
                    case PacketType.TeleportLandmarkRequest:
                        TeleportLandmarkRequestPacket tpReq = (TeleportLandmarkRequestPacket)Pack;

                        TeleportStartPacket tpStart = (TeleportStartPacket)PacketPool.Instance.GetPacket(PacketType.TeleportStart);
                        tpStart.Info.TeleportFlags = 8; // tp via lm
                        OutPacket(tpStart, ThrottleOutPacketType.Task);

                        TeleportProgressPacket tpProgress = (TeleportProgressPacket)PacketPool.Instance.GetPacket(PacketType.TeleportProgress);
                        tpProgress.Info.Message = (new ASCIIEncoding()).GetBytes("sending_landmark");
                        tpProgress.Info.TeleportFlags = 8;
                        tpProgress.AgentData.AgentID = tpReq.Info.AgentID;
                        OutPacket(tpProgress, ThrottleOutPacketType.Task);

                        // Fetch landmark
                        LLUUID lmid = tpReq.Info.LandmarkID;
                        AssetBase lma = m_assetCache.GetAsset(lmid, false);
                        if (lma != null)
                        {
                            AssetLandmark lm = new AssetLandmark(lma);

                            if (lm.RegionID == m_scene.RegionInfo.RegionID)
                            {
                                TeleportLocalPacket tpLocal = (TeleportLocalPacket)PacketPool.Instance.GetPacket(PacketType.TeleportLocal);

                                tpLocal.Info.AgentID = tpReq.Info.AgentID;
                                tpLocal.Info.TeleportFlags = 8; // Teleport via landmark
                                tpLocal.Info.LocationID = 2;
                                tpLocal.Info.Position = lm.Position;
                                OutPacket(tpLocal, ThrottleOutPacketType.Task);
                            }
                            else
                            {
                                TeleportCancelPacket tpCancel = (TeleportCancelPacket)PacketPool.Instance.GetPacket(PacketType.TeleportCancel);
                                tpCancel.Info.AgentID = tpReq.Info.AgentID;
                                tpCancel.Info.SessionID = tpReq.Info.SessionID;
                                OutPacket(tpCancel, ThrottleOutPacketType.Task);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Cancelling Teleport - fetch asset not yet implemented");

                            TeleportCancelPacket tpCancel = (TeleportCancelPacket)PacketPool.Instance.GetPacket(PacketType.TeleportCancel);
                            tpCancel.Info.AgentID = tpReq.Info.AgentID;
                            tpCancel.Info.SessionID = tpReq.Info.SessionID;
                            OutPacket(tpCancel, ThrottleOutPacketType.Task);
                        }
                        break;
                    case PacketType.TeleportLocationRequest:
                        TeleportLocationRequestPacket tpLocReq = (TeleportLocationRequestPacket)Pack;
                        // Console.WriteLine(tpLocReq.ToString());

                        handlerTeleportLocationRequest = OnTeleportLocationRequest;
                        if (handlerTeleportLocationRequest != null)
                        {
                            handlerTeleportLocationRequest(this, tpLocReq.Info.RegionHandle, tpLocReq.Info.Position,
                                                      tpLocReq.Info.LookAt, 16);
                        }
                        else
                        {
                            //no event handler so cancel request
                            TeleportCancelPacket tpCancel = (TeleportCancelPacket)PacketPool.Instance.GetPacket(PacketType.TeleportCancel);
                            tpCancel.Info.SessionID = tpLocReq.AgentData.SessionID;
                            tpCancel.Info.AgentID = tpLocReq.AgentData.AgentID;
                            OutPacket(tpCancel, ThrottleOutPacketType.Task);
                        }
                        break;

                    #endregion

                    case PacketType.MoneyBalanceRequest:
                        MoneyBalanceRequestPacket moneybalancerequestpacket = (MoneyBalanceRequestPacket)Pack;

                        handlerMoneyBalanceRequest = OnMoneyBalanceRequest;

                        if (handlerMoneyBalanceRequest != null)
                        {
                            handlerMoneyBalanceRequest(this, moneybalancerequestpacket.AgentData.AgentID, moneybalancerequestpacket.AgentData.SessionID, moneybalancerequestpacket.MoneyData.TransactionID);
                        }


                        break;
                    case PacketType.UUIDNameRequest:
                        UUIDNameRequestPacket incoming = (UUIDNameRequestPacket)Pack;
                        foreach (UUIDNameRequestPacket.UUIDNameBlockBlock UUIDBlock in incoming.UUIDNameBlock)
                        {
                            handlerNameRequest = OnNameFromUUIDRequest;
                            if (handlerNameRequest != null)
                            {
                                handlerNameRequest(UUIDBlock.ID, this);
                            }
                        }
                        break;

                    #region Parcel related packets

                    case PacketType.ParcelAccessListRequest:
                        ParcelAccessListRequestPacket requestPacket = (ParcelAccessListRequestPacket)Pack;

                        handlerParcelAccessListRequest = OnParcelAccessListRequest;

                        if (handlerParcelAccessListRequest != null)
                        {
                            handlerParcelAccessListRequest(requestPacket.AgentData.AgentID, requestPacket.AgentData.SessionID,
                                          requestPacket.Data.Flags, requestPacket.Data.SequenceID,
                                          requestPacket.Data.LocalID, this);
                        }
                        break;

                    case PacketType.ParcelAccessListUpdate:
                        ParcelAccessListUpdatePacket updatePacket = (ParcelAccessListUpdatePacket)Pack;
                        List<ParcelManager.ParcelAccessEntry> entries = new List<ParcelManager.ParcelAccessEntry>();
                        foreach (ParcelAccessListUpdatePacket.ListBlock block in updatePacket.List)
                        {
                            ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
                            entry.AgentID = block.ID;
                            entry.Flags = (ParcelManager.AccessList)block.Flags;
                            entry.Time = new DateTime();
                            entries.Add(entry);
                        }

                        handlerParcelAccessListUpdateRequest = OnParcelAccessListUpdateRequest;
                        if (handlerParcelAccessListUpdateRequest != null)
                        {
                            handlerParcelAccessListUpdateRequest(updatePacket.AgentData.AgentID,
                                        updatePacket.AgentData.SessionID, updatePacket.Data.Flags,
                                        updatePacket.Data.LocalID, entries, this);
                        }
                        break;
                    case PacketType.ParcelPropertiesRequest:

                        ParcelPropertiesRequestPacket propertiesRequest = (ParcelPropertiesRequestPacket)Pack;

                        handlerParcelPropertiesRequest = OnParcelPropertiesRequest;
                        if (handlerParcelPropertiesRequest != null)
                        {
                            handlerParcelPropertiesRequest((int)Math.Round(propertiesRequest.ParcelData.West),
                                      (int)Math.Round(propertiesRequest.ParcelData.South),
                                      (int)Math.Round(propertiesRequest.ParcelData.East),
                                      (int)Math.Round(propertiesRequest.ParcelData.North),
                                      propertiesRequest.ParcelData.SequenceID,
                                      propertiesRequest.ParcelData.SnapSelection, this);
                        }
                        break;
                    case PacketType.ParcelDivide:
                        ParcelDividePacket landDivide = (ParcelDividePacket)Pack;

                        handlerParcelDivideRequest = OnParcelDivideRequest;
                        if (handlerParcelDivideRequest != null)
                        {
                            handlerParcelDivideRequest((int)Math.Round(landDivide.ParcelData.West),
                                      (int)Math.Round(landDivide.ParcelData.South),
                                      (int)Math.Round(landDivide.ParcelData.East),
                                      (int)Math.Round(landDivide.ParcelData.North), this);
                        }
                        break;
                    case PacketType.ParcelJoin:
                        ParcelJoinPacket landJoin = (ParcelJoinPacket)Pack;

                        handlerParcelJoinRequest = OnParcelJoinRequest;

                        if (handlerParcelJoinRequest != null)
                        {
                            handlerParcelJoinRequest((int)Math.Round(landJoin.ParcelData.West),
                                                    (int)Math.Round(landJoin.ParcelData.South),
                                                    (int)Math.Round(landJoin.ParcelData.East),
                                                    (int)Math.Round(landJoin.ParcelData.North), this);
                        }
                        break;
                    case PacketType.ParcelPropertiesUpdate:
                        ParcelPropertiesUpdatePacket parcelPropertiesPacket = (ParcelPropertiesUpdatePacket)Pack;

                        handlerParcelPropertiesUpdateRequest = OnParcelPropertiesUpdateRequest;

                        if (handlerParcelPropertiesUpdateRequest != null)
                        {
                            handlerParcelPropertiesUpdateRequest(parcelPropertiesPacket, this);
                        }
                        break;
                    case PacketType.ParcelSelectObjects:
                        ParcelSelectObjectsPacket selectPacket = (ParcelSelectObjectsPacket)Pack;

                        handlerParcelSelectObjects = OnParcelSelectObjects;

                        if (handlerParcelSelectObjects != null)
                        {
                            handlerParcelSelectObjects(selectPacket.ParcelData.LocalID,
                                        Convert.ToInt32(selectPacket.ParcelData.ReturnType), this);
                        }
                        break;
                    case PacketType.ParcelObjectOwnersRequest:
                        //System.Console.WriteLine(Pack.ToString());
                        ParcelObjectOwnersRequestPacket reqPacket = (ParcelObjectOwnersRequestPacket)Pack;

                        handlerParcelObjectOwnerRequest = OnParcelObjectOwnerRequest;

                        if (handlerParcelObjectOwnerRequest != null)
                        {
                            handlerParcelObjectOwnerRequest(reqPacket.ParcelData.LocalID, this);
                        }
                        break;

                    #endregion

                    #region Estate Packets

                    case PacketType.EstateOwnerMessage:
                        EstateOwnerMessagePacket messagePacket = (EstateOwnerMessagePacket)Pack;

                        handlerEstateOwnerMessage = OnEstateOwnerMessage;

                        if (handlerEstateOwnerMessage != null)
                        {
                            handlerEstateOwnerMessage(messagePacket, this);
                        }
                        break;
                    case PacketType.RequestRegionInfo:
                        RequestRegionInfoPacket.AgentDataBlock mPacket = ((RequestRegionInfoPacket)Pack).AgentData;

                        handlerRegionInfoRequest = OnRegionInfoRequest;
                        if (handlerRegionInfoRequest != null)
                        {
                            handlerRegionInfoRequest(this, mPacket.SessionID);
                        }
                        break;
                    case PacketType.EstateCovenantRequest:

                        EstateCovenantRequestPacket.AgentDataBlock epack =
                            ((EstateCovenantRequestPacket)Pack).AgentData;

                        handlerEstateCovenantRequest = OnEstateCovenantRequest;
                        if (handlerEstateCovenantRequest != null)
                        {
                            handlerEstateCovenantRequest(this, epack.SessionID);
                        }
                        break;


                    #endregion

                    #region GodPackets

                    case PacketType.RequestGodlikePowers:
                        RequestGodlikePowersPacket rglpPack = (RequestGodlikePowersPacket)Pack;
                        RequestGodlikePowersPacket.RequestBlockBlock rblock = rglpPack.RequestBlock;
                        LLUUID token = rblock.Token;
                        
                        RequestGodlikePowersPacket.AgentDataBlock ablock = rglpPack.AgentData;

                        handlerReqGodlikePowers = OnRequestGodlikePowers;

                        if (handlerReqGodlikePowers != null)
                        {
                            handlerReqGodlikePowers(ablock.AgentID, ablock.SessionID, token, rblock.Godlike, this);
                        }

                        break;
                    case PacketType.GodKickUser:
                        m_log.Warn("[CLIENT]: unhandled GodKickUser packet");

                        GodKickUserPacket gkupack = (GodKickUserPacket)Pack;


                        if (gkupack.UserInfo.GodSessionID == SessionId && AgentId == gkupack.UserInfo.GodID)
                        {
                            handlerGodKickUser = OnGodKickUser;
                            if (handlerGodKickUser != null)
                            {
                                handlerGodKickUser(gkupack.UserInfo.GodID, gkupack.UserInfo.GodSessionID,
                                              gkupack.UserInfo.AgentID, (uint)0, gkupack.UserInfo.Reason);
                            }
                        }
                        else
                        {
                            SendAgentAlertMessage("Kick request denied", false);
                        }
                        //KickUserPacket kupack = new KickUserPacket();
                        //KickUserPacket.UserInfoBlock kupackib = kupack.UserInfo;

                        //kupack.UserInfo.AgentID = gkupack.UserInfo.AgentID;
                        //kupack.UserInfo.SessionID = gkupack.UserInfo.GodSessionID;

                        //kupack.TargetBlock.TargetIP = (uint)0;
                        //kupack.TargetBlock.TargetPort = (ushort)0;
                        //kupack.UserInfo.Reason = gkupack.UserInfo.Reason;

                        //OutPacket(kupack, ThrottleOutPacketType.Task);
                        break;


                    #endregion

                    #region unimplemented handlers


                    case PacketType.StartPingCheck:
                        // Send the client the ping response back
                        // Pass the same PingID in the matching packet
                        // Handled In the packet processing
                        //m_log.Debug("[CLIENT]: possibly unhandled StartPingCheck packet");
                        break;
                    case PacketType.CompletePingCheck:
                        // TODO: Perhaps this should be processed on the Sim to determine whether or not to drop a dead client
                        //m_log.Warn("[CLIENT]: unhandled CompletePingCheck packet");
                        break;
                    case PacketType.ObjectScale:
                        // TODO: handle this packet
                        m_log.Warn("[CLIENT]: unhandled ObjectScale packet");
                        break;
                    case PacketType.ViewerStats:
                        // TODO: handle this packet
                        m_log.Warn("[CLIENT]: unhandled ViewerStats packet");
                        break;

                    case PacketType.CreateGroupRequest:
                        // TODO: handle this packet
                        m_log.Warn("[CLIENT]: unhandled CreateGroupRequest packet");
                        break;
                    case PacketType.GenericMessage:
                        // TODO: handle this packet
                        m_log.Warn("[CLIENT]: unhandled GenericMessage packet");
                        break;
                    case PacketType.MapItemRequest:
                        // TODO: handle this packet
                        m_log.Warn("[CLIENT]: unhandled MapItemRequest packet");
                        break;
                    case PacketType.AgentResume:
                        // TODO: handle this packet
                        m_log.Warn("[CLIENT]: unhandled AgentResume packet");
                        break;
                    case PacketType.AgentPause:
                        // TODO: handle this packet
                        m_log.Warn("[CLIENT]: unhandled AgentPause packet");
                        break;
                    case PacketType.TransferAbort:
                        // TODO: handle this packet
                        m_log.Warn("[CLIENT]: unhandled TransferAbort packet");
                        break;
                    case PacketType.MuteListRequest:
                        // TODO: handle this packet
                        m_log.Warn("[CLIENT]: unhandled MuteListRequest packet");
                        break;
                    //case PacketType.AgentDataUpdateRequest:
                    // TODO: handle this packet
                    //m_log.Warn("[CLIENT]: unhandled AgentDataUpdateRequest packet");
                    //break;

                    case PacketType.ParcelDwellRequest:
                        // TODO: handle this packet
                        m_log.Warn("[CLIENT]: unhandled ParcelDwellRequest packet");
                        break;
                    case PacketType.UseCircuitCode:
                        // TODO: Don't display this one, we handle it at a lower level
                        //m_log.Warn("[CLIENT]: unhandled UseCircuitCode packet");
                        break;
                    case PacketType.EconomyDataRequest:
                        // TODO: handle this packet
                        m_log.Warn("[CLIENT]: unhandled EconomyDataRequest packet");
                        break;
                    case PacketType.AgentHeightWidth:
                        // TODO: handle this packet
                        m_log.Warn("[CLIENT]: unhandled AgentHeightWidth packet");
                        break;
                    case PacketType.ObjectSpinStop:
                        // TODO: handle this packet
                        m_log.Warn("[CLIENT]: unhandled ObjectSpinStop packet");
                        break;
                    case PacketType.SoundTrigger:
                        // TODO: handle this packet
                        m_log.Warn("[CLIENT]: unhandled SoundTrigger packet");
                        break;
                    //case PacketType.UserInfoRequest:
                    // TODO: handle this packet
                    //m_log.Warn("[CLIENT]: unhandled UserInfoRequest packet");
                    //break;
                    case PacketType.InventoryDescendents:
                        // TODO: handle this packet
                        m_log.Warn("[CLIENT]: unhandled InventoryDescent packet");
                        break;
                    default:
                        m_log.Warn("[CLIENT]: unhandled packet " + Pack.ToString());
                        break;

                    #endregion
                }
            }

            PacketPool.Instance.ReturnPacket(Pack);
        }

        private static PrimitiveBaseShape GetShapeFromAddPacket(ObjectAddPacket addPacket)
        {
            PrimitiveBaseShape shape = new PrimitiveBaseShape();

            shape.PCode = addPacket.ObjectData.PCode;
            shape.State = addPacket.ObjectData.State;
            shape.PathBegin = addPacket.ObjectData.PathBegin;
            shape.PathEnd = addPacket.ObjectData.PathEnd;
            shape.PathScaleX = addPacket.ObjectData.PathScaleX;
            shape.PathScaleY = addPacket.ObjectData.PathScaleY;
            shape.PathShearX = addPacket.ObjectData.PathShearX;
            shape.PathShearY = addPacket.ObjectData.PathShearY;
            shape.PathSkew = addPacket.ObjectData.PathSkew;
            shape.ProfileBegin = addPacket.ObjectData.ProfileBegin;
            shape.ProfileEnd = addPacket.ObjectData.ProfileEnd;
            shape.Scale = addPacket.ObjectData.Scale;
            shape.PathCurve = addPacket.ObjectData.PathCurve;
            shape.ProfileCurve = addPacket.ObjectData.ProfileCurve;
            shape.ProfileHollow = addPacket.ObjectData.ProfileHollow;
            shape.PathRadiusOffset = addPacket.ObjectData.PathRadiusOffset;
            shape.PathRevolutions = addPacket.ObjectData.PathRevolutions;
            shape.PathTaperX = addPacket.ObjectData.PathTaperX;
            shape.PathTaperY = addPacket.ObjectData.PathTaperY;
            shape.PathTwist = addPacket.ObjectData.PathTwist;
            shape.PathTwistBegin = addPacket.ObjectData.PathTwistBegin;
            LLObject.TextureEntry ntex = new LLObject.TextureEntry(new LLUUID("89556747-24cb-43ed-920b-47caed15465f"));
            shape.TextureEntry = ntex.ToBytes();
            //shape.Textures = ntex;
            return shape;
        }

        public void SendBlueBoxMessage(LLUUID FromAvatarID, LLUUID fromSessionID, String FromAvatarName, String Message)
        {
            if (!ChildAgentStatus())
                SendInstantMessage(FromAvatarID, fromSessionID, Message, AgentId, SessionId, FromAvatarName, (byte)1, (uint)Util.UnixTimeSinceEpoch());

            //SendInstantMessage(FromAvatarID, fromSessionID, Message, AgentId, SessionId, FromAvatarName, (byte)21,(uint) Util.UnixTimeSinceEpoch());
        }

        public void SendLogoutPacket()
        {
            LogoutReplyPacket logReply = (LogoutReplyPacket)PacketPool.Instance.GetPacket(PacketType.LogoutReply);
            // TODO: don't create new blocks if recycling an old packet
            logReply.AgentData.AgentID = AgentId;
            logReply.AgentData.SessionID = SessionId;
            logReply.InventoryData = new LogoutReplyPacket.InventoryDataBlock[1];
            logReply.InventoryData[0] = new LogoutReplyPacket.InventoryDataBlock();
            logReply.InventoryData[0].ItemID = LLUUID.Zero;

            OutPacket(logReply, ThrottleOutPacketType.Task);
        }

        public ClientInfo GetClientInfo()
        {
            //MainLog.Instance.Verbose("CLIENT", "GetClientInfo BGN");
            
            ClientInfo info = new ClientInfo();
            info.userEP = this.m_userEndPoint;
            info.proxyEP = this.m_proxyEndPoint;
            info.agentcircuit = new sAgentCircuitData(RequestClientInfo());

            info.pendingAcks = m_pendingAcks;

            info.needAck = new Dictionary<uint,byte[]>();

            lock (m_needAck)
            {
                foreach (uint key in m_needAck.Keys)
                {
                    info.needAck.Add(key, m_needAck[key].ToBytes());
                }
            }

/* pending
            QueItem[] queitems = m_packetQueue.GetQueueArray();

            MainLog.Instance.Verbose("CLIENT", "Queue Count : [{0}]", queitems.Length);
            
            for (int i = 0; i < queitems.Length; i++)
            {
                if (queitems[i].Incoming == false)
                {
                    info.out_packets.Add(queitems[i].Packet.ToBytes());
                    MainLog.Instance.Verbose("CLIENT", "Add OutPacket [{0}]", queitems[i].Packet.Type.ToString());
                }
            }
*/

            info.sequence = m_sequence;

            //MainLog.Instance.Verbose("CLIENT", "GetClientInfo END");

            return info;
        }

        public void SetClientInfo(ClientInfo info)
        {
            m_pendingAcks = info.pendingAcks;

            m_needAck = new Dictionary<uint,Packet>();

            Packet packet = null;
            int packetEnd = 0;
            byte[] zero = new byte[3000];

            foreach (uint key in info.needAck.Keys)
            {
                byte[] buff = info.needAck[key];

                packetEnd = buff.Length - 1;

                try
                {
                    packet = PacketPool.Instance.GetPacket(buff, ref packetEnd, zero);
                }
                catch (Exception)
                {
                    //MainLog.Instance.Debug("UDPSERVER", e.ToString());
                }

                m_needAck.Add(key, packet);
            }

            m_sequence = info.sequence;
        }
    }
}
