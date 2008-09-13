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
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;
using System.Xml;
using OpenMetaverse;
using OpenMetaverse.Packets;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Statistics;
using OpenSim.Region.ClientStack.LindenUDP;
using OpenSim.Region.Environment.Scenes;
using Timer = System.Timers.Timer;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public delegate bool PacketMethod(IClientAPI simClient, Packet packet);

    /// <summary>
    /// Handles new client connections
    /// Constructor takes a single Packet and authenticates everything
    /// </summary>
    public class LLClientView : IClientAPI
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //                ~ClientView()
        //                {
        //                    m_log.Info("[CLIENT]: LLClientView destructor called");
        //                }

        /* static variables */
        public static SynchronizeClientHandler SynchronizeClient = null;
        /* private variables */
        private readonly UUID m_sessionId;
        private UUID m_secureSessionId = UUID.Zero;
        //private AgentAssetUpload UploadAssets;
        private int m_debug = 0;
        private readonly AssetCache m_assetCache;
        // private InventoryCache m_inventoryCache;
        private int m_cachedTextureSerial = 0;
        private Timer m_clientPingTimer;

        private bool m_clientBlocked = false;

        private int m_probesWithNoIngressPackets = 0;
        //private int m_lastPacketsReceived = 0;
        //private byte[] ZeroOutBuffer = new byte[4096];

        private readonly UUID m_agentId;
        private readonly uint m_circuitCode;
        private int m_moneyBalance;
        private IPacketHandler m_PacketHandler;

        private int m_animationSequenceNumber = 1;

        private byte[] m_channelVersion = Utils.StringToBytes("OpenSimulator 0.5"); // Dummy value needed by libSL

        private Dictionary<string, UUID> m_defaultAnimations = new Dictionary<string, UUID>();

        private bool m_SendLogoutPacketWhenClosing = true;

        private int m_inPacketsChecked = 0;

        /* protected variables */

        protected static Dictionary<PacketType, PacketMethod> PacketHandlers =
            new Dictionary<PacketType, PacketMethod>(); //Global/static handlers for all clients

        protected Dictionary<PacketType, PacketMethod> m_packetHandlers = new Dictionary<PacketType, PacketMethod>();

        protected IScene m_scene;
        protected AgentCircuitManager m_authenticateSessionsHandler;

        protected LLPacketServer m_networkServer;

        /* public variables */
        protected string m_firstName;
        protected string m_lastName;
        protected Thread m_clientThread;
        protected Vector3 m_startpos;
        protected EndPoint m_userEndPoint;
        protected EndPoint m_proxyEndPoint;
        protected UUID m_activeGroupID = UUID.Zero;
        protected string m_activeGroupName = String.Empty;
        protected ulong m_activeGroupPowers = 0;
        protected Dictionary<UUID,ulong> m_groupPowers = new Dictionary<UUID, ulong>();

        /* Instantiated Designated Event Delegates */
        //- used so we don't create new objects for each incoming packet and then toss it out later */

        private GenericMessage handlerGenericMessage = null;
        private RequestAvatarProperties handlerRequestAvatarProperties = null; //OnRequestAvatarProperties;
        private UpdateAvatarProperties handlerUpdateAvatarProperties = null; // OnUpdateAvatarProperties;
        private ChatMessage handlerChatFromViewer = null; //OnChatFromViewer;
        private ChatMessage handlerChatFromViewer2 = null; //OnChatFromViewer;
        private ImprovedInstantMessage handlerInstantMessage = null; //OnInstantMessage;
        private FriendActionDelegate handlerApproveFriendRequest = null; //OnApproveFriendRequest;
        private FriendshipTermination handlerTerminateFriendship = null; //OnTerminateFriendship;
        private RezObject handlerRezObject = null; //OnRezObject;
        private GenericCall4 handlerDeRezObject = null; //OnDeRezObject;
        private ModifyTerrain handlerModifyTerrain = null;
        private BakeTerrain handlerBakeTerrain = null;
        private EstateChangeInfo handlerEstateChangeInfo = null;
        private Action<IClientAPI> handlerRegionHandShakeReply = null; //OnRegionHandShakeReply;
        private GenericCall2 handlerRequestWearables = null; //OnRequestWearables;
        private Action<IClientAPI> handlerRequestAvatarsData = null; //OnRequestAvatarsData;
        private SetAppearance handlerSetAppearance = null; //OnSetAppearance;
        private AvatarNowWearing handlerAvatarNowWearing = null; //OnAvatarNowWearing;
        private RezSingleAttachmentFromInv handlerRezSingleAttachment = null; //OnRezSingleAttachmentFromInv;
        private UUIDNameRequest handlerDetachAttachmentIntoInv = null; // Detach attachment!
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
        private TeleportLandmarkRequest handlerTeleportLandmarkRequest = null; //OnTeleportLandmarkRequest;
        private LinkObjects handlerLinkObjects = null; //OnLinkObjects;
        private DelinkObjects handlerDelinkObjects = null; //OnDelinkObjects;
        private AddNewPrim handlerAddPrim = null; //OnAddPrim;
        private UpdateShape handlerUpdatePrimShape = null; //null;
        private ObjectExtraParams handlerUpdateExtraParams = null; //OnUpdateExtraParams;
        private ObjectDuplicate handlerObjectDuplicate = null;
        private ObjectDuplicateOnRay handlerObjectDuplicateOnRay = null;
        private ObjectSelect handlerObjectSelect = null;
        private ObjectDeselect handlerObjectDeselect = null;
        private ObjectIncludeInSearch handlerObjectIncludeInSearch = null;
        private UpdatePrimFlags handlerUpdatePrimFlags = null; //OnUpdatePrimFlags;
        private UpdatePrimTexture handlerUpdatePrimTexture = null;
        private UpdateVector handlerGrabObject = null; //OnGrabObject;
        private MoveObject handlerGrabUpdate = null; //OnGrabUpdate;
        private ObjectSelect handlerDeGrabObject = null; //OnDeGrabObject;
        private GenericCall7 handlerObjectDescription = null;
        private GenericCall7 handlerObjectName = null;
        private GenericCall7 handlerObjectClickAction = null;
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
        private MoveTaskInventory handlerMoveTaskItem = null;
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
        private ParcelAbandonRequest handlerParcelAbandonRequest = null;
        private ParcelReclaim handlerParcelReclaim = null;
        private ParcelReturnObjectsRequest handlerParcelReturnObjectsRequest = null;
        private RegionInfoRequest handlerRegionInfoRequest = null; //OnRegionInfoRequest;
        private EstateCovenantRequest handlerEstateCovenantRequest = null; //OnEstateCovenantRequest;
        private RequestGodlikePowers handlerReqGodlikePowers = null; //OnRequestGodlikePowers;
        private GodKickUser handlerGodKickUser = null; //OnGodKickUser;
        private ViewerEffectEventHandler handlerViewerEffect = null; //OnViewerEffect;
        private Action<IClientAPI> handlerLogout = null; //OnLogout;
        private MoneyTransferRequest handlerMoneyTransferRequest = null; //OnMoneyTransferRequest;
        private ParcelBuy handlerParcelBuy = null;
        private EconomyDataRequest handlerEconomoyDataRequest = null;

        private UpdateVector handlerUpdatePrimSinglePosition = null; //OnUpdatePrimSinglePosition;
        private UpdatePrimSingleRotation handlerUpdatePrimSingleRotation = null; //OnUpdatePrimSingleRotation;
        private UpdateVector handlerUpdatePrimScale = null; //OnUpdatePrimScale;
        private UpdateVector handlerUpdatePrimGroupScale = null; //OnUpdateGroupScale;
        private UpdateVector handlerUpdateVector = null; //OnUpdatePrimGroupPosition;
        private UpdatePrimRotation handlerUpdatePrimRotation = null; //OnUpdatePrimGroupRotation;
        // private UpdatePrimGroupRotation handlerUpdatePrimGroupRotation = null; //OnUpdatePrimGroupMouseRotation;
        // private RequestAsset handlerRequestAsset = null; // OnRequestAsset;
        private UUIDNameRequest handlerTeleportHomeRequest = null;

        private RegionHandleRequest handlerRegionHandleRequest = null; // OnRegionHandleRequest
        private ParcelInfoRequest handlerParcelInfoRequest = null; // OnParcelInfoRequest

        private ScriptAnswer handlerScriptAnswer = null;
        private RequestPayPrice handlerRequestPayPrice = null;
        private ObjectSaleInfo handlerObjectSaleInfo = null;
        private ObjectBuy handlerObjectBuy = null;
        //private BuyObjectInventory handlerBuyObjectInventory = null;
        private ObjectDeselect handlerObjectDetach = null;
        private AgentSit handlerOnUndo = null;

        private ForceReleaseControls handlerForceReleaseControls = null;

        private GodLandStatRequest handlerLandStatRequest = null;

        private UUIDNameRequest handlerUUIDGroupNameRequest = null;

        private RequestObjectPropertiesFamily handlerObjectGroupRequest = null;
        private ScriptReset handlerScriptReset = null;
        private GetScriptRunning handlerGetScriptRunning = null;
        private SetScriptRunning handlerSetScriptRunning = null;
        private UpdateVector handlerAutoPilotGo = null;
        //Gesture
        private ActivateGesture handlerActivateGesture = null;
        private DeactivateGesture handlerDeactivateGesture = null;

        //private TerrainUnacked handlerUnackedTerrain = null;

        //**

        /* Properties */

        public UUID SecureSessionId
        {
            get { return m_secureSessionId; }
        }

        public IScene Scene
        {
            get { return m_scene; }
        }

        public UUID SessionId
        {
            get { return m_sessionId; }
        }

        public Vector3 StartPos
        {
            get { return m_startpos; }
            set { m_startpos = value; }
        }

        public UUID AgentId
        {
            get { return m_agentId; }
        }

        public UUID ActiveGroupId
        {
            get { return m_activeGroupID; }
        }

        public string ActiveGroupName
        {
            get { return m_activeGroupName; }
        }

        public ulong ActiveGroupPowers
        {
            get { return m_activeGroupPowers; }
        }

        public ulong GetGroupPowers(UUID groupID)
        {
            if (m_groupPowers.ContainsKey(groupID))
                return m_groupPowers[groupID];
            return 0;
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

        public int NextAnimationSequenceNumber
        {
            get { return m_animationSequenceNumber++; }
        }

        public IPacketHandler PacketHandler
        {
            get { return m_PacketHandler; }
        }

        bool m_IsActive = true;

        public bool IsActive
        {
            get { return m_IsActive; }
            set { m_IsActive = value; }
        }

        public bool SendLogoutPacketWhenClosing
        {
            set { m_SendLogoutPacketWhenClosing = value; }
        }

        /* METHODS */

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="remoteEP"></param>
        /// <param name="scene"></param>
        /// <param name="assetCache"></param>
        /// <param name="packServer"></param>
        /// <param name="authenSessions"></param>
        /// <param name="agentId"></param>
        /// <param name="sessionId"></param>
        /// <param name="circuitCode"></param>
        /// <param name="proxyEP"></param>
        public LLClientView(EndPoint remoteEP, IScene scene, AssetCache assetCache, LLPacketServer packServer,
                          AgentCircuitManager authenSessions, UUID agentId, UUID sessionId, uint circuitCode, EndPoint proxyEP)
        {
            m_moneyBalance = 1000;

            m_channelVersion = Utils.StringToBytes(scene.GetSimulatorVersion());

            InitDefaultAnimations();

            m_scene = scene;
            m_assetCache = assetCache;

            m_networkServer = packServer;
            // m_inventoryCache = inventoryCache;
            m_authenticateSessionsHandler = authenSessions;

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

            m_PacketHandler = new LLPacketHandler(this, m_networkServer);
            m_PacketHandler.SynchronizeClient = SynchronizeClient;

            RegisterLocalPacketHandlers();

            m_clientThread = new Thread(new ThreadStart(AuthUser));
            m_clientThread.Name = "ClientThread";
            m_clientThread.IsBackground = true;
            m_clientThread.Start();
            ThreadTracker.Add(m_clientThread);
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

            Thread.Sleep(2000);

            // Shut down timers
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

                // Sends a KillPacket object, with which, the
                // blockingqueue dequeues and sees it's a killpacket
                // and terminates within the context of the client thread.
                // This ensures that it's done from within the context
                // of the client thread regardless of where Close() is called.
                KillEndDone();
            }
        }

        /// <summary>
        /// Close down the client view.  This *must* be the last method called, since the last  #
        /// statement of CloseCleanup() aborts the thread.
        /// </summary>
        /// <param name="shutdownCircuit"></param>
        public void Close(bool shutdownCircuit)
        {
            m_clientPingTimer.Enabled = false;

            m_log.DebugFormat(
                "[CLIENT]: Close has been called with shutdownCircuit = {0} on scene {1}",
                shutdownCircuit, m_scene.RegionInfo.RegionName);

            m_PacketHandler.Flush();

            // raise an event on the packet server to Shutdown the circuit
            // Now, if we raise the event then the packet server will call this method itself, so don't try cleanup
            // here otherwise we'll end up calling it twice.
            // FIXME: In truth, I might be wrong but this whole business of calling this method twice (with different args) looks
            // horribly tangly.  Hopefully it should be possible to greatly simplify it.
            if (shutdownCircuit)
            {
                OnConnectionClosed(this);
            }
            else
            {
                CloseCleanup(shutdownCircuit);
            }
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
                kupack.UserInfo.Reason = Utils.StringToBytes(message);
                OutPacket(kupack, ThrottleOutPacketType.Task);
                // You must sleep here or users get no message!
                Thread.Sleep(500);
            }
        }

        public void Stop()
        {
            // Shut down timers
            m_clientPingTimer.Stop();
        }

        public void Restart()
        {
            // re-construct
            m_PacketHandler.Clear();

            m_clientPingTimer = new Timer(5000);
            m_clientPingTimer.Elapsed += new ElapsedEventHandler(CheckClientConnectivity);
            m_clientPingTimer.Enabled = true;
        }

        public void Terminate()
        {
            m_PacketHandler.Stop();

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

        /// <summary>
        /// Try to process a packet using registered packet handlers
        /// </summary>
        /// <param name="packet"></param>
        /// <returns>True if a handler was found which successfully processed the packet.</returns>
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
            m_log.Info("[CLIENT]: Entered main packet processing loop");

            while (true)
            {
                LLQueItem nextPacket = m_PacketHandler.PacketQueue.Dequeue();
                if (nextPacket == null)
                {
                    m_log.Error("Got a NULL packet in Client Loop, bailing out of our client loop");
                    break;
                }
                if (nextPacket.Incoming)
                {
                    DebugPacket("IN", nextPacket.Packet);
                    m_PacketHandler.ProcessInPacket(nextPacket);
                }
                else
                {
                    DebugPacket("OUT", nextPacket.Packet);
                    m_PacketHandler.ProcessOutPacket(nextPacket);
                }
            }
        }

        # endregion

        protected int m_terrainCheckerCount = 0;

        /// <summary>
        /// Event handler for check client timer
        /// Checks to ensure that the client is still connected.  If the client has failed to respond to many pings
        /// in succession then close down the connection.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void CheckClientConnectivity(object sender, ElapsedEventArgs e)
        {
            if (m_PacketHandler.PacketsReceived == m_inPacketsChecked)
            {
                // no packet came in since the last time we checked...

                m_probesWithNoIngressPackets++;
                if ((m_probesWithNoIngressPackets > 30 && !m_clientBlocked)    // agent active
                    || (m_probesWithNoIngressPackets > 90 && m_clientBlocked)) // agent paused
                {
                    m_clientPingTimer.Enabled = false;

                    m_log.WarnFormat(
                        "[CLIENT]: Client for agent {0} {1} has stopped responding to pings.  Closing connection",
                        Name, AgentId);

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
                // ... and store the current number of packets received to find out if another one got in on the next cycle
                m_inPacketsChecked = m_PacketHandler.PacketsReceived;
            }

        }

        # region Setup

        /// <summary>
        /// Starts up the timers to check the client and resend unacked packets
        /// Adds the client to the OpenSim.Region.Environment.Scenes.Scene
        /// </summary>
        protected virtual void InitNewClient()
        {
            //this.UploadAssets = new AgentAssetUpload(this, m_assetCache, m_inventoryCache);

            // Establish our two timers.  We could probably get this down to one

            // Ping the client regularly to check that it's still there
            m_clientPingTimer = new Timer(5000);
            m_clientPingTimer.Elapsed += new ElapsedEventHandler(CheckClientConnectivity);
            m_clientPingTimer.Enabled = true;

            m_scene.AddNewClient(this, true);
        }

        /// <summary>
        /// Authorize an incoming user session.  This method lies at the base of the entire client thread.
        /// </summary>
        protected virtual void AuthUser()
        {
            //tell this thread we are using the culture set up for the sim (currently hardcoded to en_US)
            //otherwise it will override this and use the system default
            Culture.SetCurrentCulture();

            try
            {
                // AuthenticateResponse sessionInfo = m_gridServer.AuthenticateSession(m_cirpack.m_circuitCode.m_sessionId, m_cirpack.m_circuitCode.ID, m_cirpack.m_circuitCode.Code);
                AuthenticateResponse sessionInfo =
                    m_authenticateSessionsHandler.AuthenticateSession(m_sessionId, m_agentId, m_circuitCode);

                if (!sessionInfo.Authorised)
                {
                    //session/circuit not authorised
                    m_log.WarnFormat(
                        "[CLIENT]: New user request denied to avatar {0} connecting with circuit code {1} from {2}",
                        m_agentId, m_circuitCode, m_userEndPoint);

                    m_PacketHandler.Stop();
                    m_clientThread.Abort();
                }
                else
                {
                    m_log.Info("[CLIENT]: Got authenticated connection from " + m_userEndPoint.ToString());
                    //session is authorised
                    m_firstName = sessionInfo.LoginInfo.First;
                    m_lastName = sessionInfo.LoginInfo.Last;

                    if (sessionInfo.LoginInfo.SecureSession != UUID.Zero)
                    {
                        m_secureSessionId = sessionInfo.LoginInfo.SecureSession;
                    }

                    // This sets up all the timers
                    InitNewClient();

                    ClientLoop();
                }
            }
            catch (System.Exception e)
            {
                if (e is ThreadAbortException)
                    throw e;

                if (StatsManager.SimExtraStats != null)
                    StatsManager.SimExtraStats.AddAbnormalClientThreadTermination();

                // Don't let a failure in an individual client thread crash the whole sim.
                m_log.ErrorFormat("[CLIENT]: Client thread for {0} {1} crashed.  Logging them out.  Exception {2}", Name, AgentId, e);

                try
                {
                    // Make an attempt to alert the user that their session has crashed
                    AgentAlertMessagePacket packet
                        = BuildAgentAlertPacket(
                            "Unfortunately the session for this client on the server has crashed.\n"
                                + "Any further actions taken will not be processed.\n"
                                + "Please relog", true);

                    LLQueItem item = new LLQueItem();
                    item.Packet = packet;

                    m_PacketHandler.ProcessOutPacket(item);

                    // There may be a better way to do this.  Perhaps kick?  Not sure this propogates notifications to
                    // listeners yet, though.
                    Logout(this);
                }
                catch (Exception e2)
                {
                    if (e2 is ThreadAbortException)
                        throw e2;

                    m_log.ErrorFormat("[CLIENT]: Further exception thrown on forced session logout.  {0}", e2);
                }
            }
        }

        # endregion

        // Previously ClientView.API partial class
        public event GenericMessage OnGenericMessage;
        public event Action<IClientAPI> OnLogout;
        public event ObjectPermissions OnObjectPermissions;
        public event Action<IClientAPI> OnConnectionClosed;
        public event ViewerEffectEventHandler OnViewerEffect;
        public event ImprovedInstantMessage OnInstantMessage;
        public event ChatMessage OnChatFromViewer;
        public event TextureRequest OnRequestTexture;
        public event RezObject OnRezObject;
        public event GenericCall4 OnDeRezObject;
        public event ModifyTerrain OnModifyTerrain;
        public event Action<IClientAPI> OnRegionHandShakeReply;
        public event GenericCall2 OnRequestWearables;
        public event SetAppearance OnSetAppearance;
        public event AvatarNowWearing OnAvatarNowWearing;
        public event RezSingleAttachmentFromInv OnRezSingleAttachmentFromInv;
        public event UUIDNameRequest OnDetachAttachmentIntoInv;
        public event ObjectAttach OnObjectAttach;
        public event ObjectDeselect OnObjectDetach;
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
        public event ObjectDuplicateOnRay OnObjectDuplicateOnRay;
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
        public event GenericCall7 OnObjectClickAction;
        public event ObjectIncludeInSearch OnObjectIncludeInSearch;
        public event RequestObjectPropertiesFamily OnRequestObjectPropertiesFamily;
        public event UpdatePrimFlags OnUpdatePrimFlags;
        public event UpdatePrimTexture OnUpdatePrimTexture;
        public event UpdateVector OnUpdatePrimGroupPosition;
        public event UpdateVector OnUpdatePrimSinglePosition;
        public event UpdatePrimRotation OnUpdatePrimGroupRotation;
        public event UpdatePrimSingleRotation OnUpdatePrimSingleRotation;
        public event UpdatePrimGroupRotation OnUpdatePrimGroupMouseRotation;
        public event UpdateVector OnUpdatePrimScale;
        public event UpdateVector OnUpdatePrimGroupScale;
        public event StatusChange OnChildAgentStatus;
        public event GenericCall2 OnStopMovement;
        public event Action<UUID> OnRemoveAvatar;
        public event RequestMapBlocks OnRequestMapBlocks;
        public event RequestMapName OnMapNameRequest;
        public event TeleportLocationRequest OnTeleportLocationRequest;
        public event TeleportLandmarkRequest OnTeleportLandmarkRequest;
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
        public event MoveTaskInventory OnMoveTaskItem;
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
        public event ParcelAbandonRequest OnParcelAbandonRequest;
        public event ParcelReclaim OnParcelReclaim;
        public event ParcelReturnObjectsRequest OnParcelReturnObjectsRequest;
        public event RegionInfoRequest OnRegionInfoRequest;
        public event EstateCovenantRequest OnEstateCovenantRequest;
        public event FriendActionDelegate OnApproveFriendRequest;
        public event FriendActionDelegate OnDenyFriendRequest;
        public event FriendshipTermination OnTerminateFriendship;
        public event MoneyTransferRequest OnMoneyTransferRequest;
        public event EconomyDataRequest OnEconomyDataRequest;
        public event MoneyBalanceRequest OnMoneyBalanceRequest;
        public event ParcelBuy OnParcelBuy;
        public event UUIDNameRequest OnTeleportHomeRequest;
        public event UUIDNameRequest OnUUIDGroupNameRequest;
        public event ScriptAnswer OnScriptAnswer;
        public event RequestPayPrice OnRequestPayPrice;
        public event ObjectSaleInfo OnObjectSaleInfo;
        public event ObjectBuy OnObjectBuy;
        public event BuyObjectInventory OnBuyObjectInventory;
        public event AgentSit OnUndo;
        public event ForceReleaseControls OnForceReleaseControls;
        public event GodLandStatRequest OnLandStatRequest;
        public event RequestObjectPropertiesFamily OnObjectGroupRequest;
        public event DetailedEstateDataRequest OnDetailedEstateDataRequest;
        public event SetEstateFlagsRequest OnSetEstateFlagsRequest;
        public event SetEstateTerrainBaseTexture OnSetEstateTerrainBaseTexture;
        public event SetEstateTerrainDetailTexture OnSetEstateTerrainDetailTexture;
        public event SetEstateTerrainTextureHeights OnSetEstateTerrainTextureHeights;
        public event CommitEstateTerrainTextureRequest OnCommitEstateTerrainTextureRequest;
        public event SetRegionTerrainSettings OnSetRegionTerrainSettings;
        public event BakeTerrain OnBakeTerrain;
        public event EstateChangeInfo OnEstateChangeInfo;
        public event EstateRestartSimRequest OnEstateRestartSimRequest;
        public event EstateChangeCovenantRequest OnEstateChangeCovenantRequest;
        public event UpdateEstateAccessDeltaRequest OnUpdateEstateAccessDeltaRequest;
        public event SimulatorBlueBoxMessageRequest OnSimulatorBlueBoxMessageRequest;
        public event EstateBlueBoxMessageRequest OnEstateBlueBoxMessageRequest;
        public event EstateDebugRegionRequest OnEstateDebugRegionRequest;
        public event EstateTeleportOneUserHomeRequest OnEstateTeleportOneUserHomeRequest;
        public event RegionHandleRequest OnRegionHandleRequest;
        public event ParcelInfoRequest OnParcelInfoRequest;
        public event ScriptReset OnScriptReset;
        public event GetScriptRunning OnGetScriptRunning;
        public event SetScriptRunning OnSetScriptRunning;
        public event UpdateVector OnAutoPilotGo;

        public event TerrainUnacked OnUnackedTerrain;

        public event ActivateGesture OnActivateGesture;
        public event DeactivateGesture OnDeactivateGesture;


        // voire si c'est necessaire
        public void ActivateGesture(UUID assetId, UUID gestureId)
        {
        }
        public void DeactivateGesture(UUID assetId, UUID gestureId)
        {
        }
        #region Scene/Avatar to Client

        /// <summary>
        ///
        /// </summary>
        /// <param name="regionInfo"></param>
        public void SendRegionHandshake(RegionInfo regionInfo, RegionHandshakeArgs args)
        {
            RegionHandshakePacket handshake = (RegionHandshakePacket)PacketPool.Instance.GetPacket(PacketType.RegionHandshake);

            handshake.RegionInfo.BillableFactor = args.billableFactor;
            handshake.RegionInfo.IsEstateManager = args.isEstateManager;
            handshake.RegionInfo.TerrainHeightRange00 = args.terrainHeightRange0;
            handshake.RegionInfo.TerrainHeightRange01 = args.terrainHeightRange1;
            handshake.RegionInfo.TerrainHeightRange10 = args.terrainHeightRange2;
            handshake.RegionInfo.TerrainHeightRange11 = args.terrainHeightRange3;
            handshake.RegionInfo.TerrainStartHeight00 = args.terrainStartHeight0;
            handshake.RegionInfo.TerrainStartHeight01 = args.terrainStartHeight1;
            handshake.RegionInfo.TerrainStartHeight10 = args.terrainStartHeight2;
            handshake.RegionInfo.TerrainStartHeight11 = args.terrainStartHeight3;
            handshake.RegionInfo.SimAccess = args.simAccess;
            handshake.RegionInfo.WaterHeight = args.waterHeight;

            handshake.RegionInfo.RegionFlags = args.regionFlags;
            handshake.RegionInfo.SimName = Utils.StringToBytes(args.regionName);
            handshake.RegionInfo.SimOwner = args.SimOwner;
            handshake.RegionInfo.TerrainBase0 = args.terrainBase0;
            handshake.RegionInfo.TerrainBase1 = args.terrainBase1;
            handshake.RegionInfo.TerrainBase2 = args.terrainBase2;
            handshake.RegionInfo.TerrainBase3 = args.terrainBase3;
            handshake.RegionInfo.TerrainDetail0 = args.terrainDetail0;
            handshake.RegionInfo.TerrainDetail1 = args.terrainDetail1;
            handshake.RegionInfo.TerrainDetail2 = args.terrainDetail2;
            handshake.RegionInfo.TerrainDetail3 = args.terrainDetail3;
            handshake.RegionInfo.CacheID = UUID.Random(); //I guess this is for the client to remember an old setting?

            OutPacket(handshake, ThrottleOutPacketType.Task);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="regInfo"></param>
        public void MoveAgentIntoRegion(RegionInfo regInfo, Vector3 pos, Vector3 look)
        {
            AgentMovementCompletePacket mov = (AgentMovementCompletePacket)PacketPool.Instance.GetPacket(PacketType.AgentMovementComplete);
            mov.SimData.ChannelVersion = m_channelVersion;
            mov.AgentData.SessionID = m_sessionId;
            mov.AgentData.AgentID = AgentId;
            mov.Data.RegionHandle = regInfo.RegionHandle;
            mov.Data.Timestamp = (uint)Util.UnixTimeSinceEpoch();

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
        public void SendChatMessage(string message, byte type, Vector3 fromPos, string fromName,
                                    UUID fromAgentID, byte source, byte audible)
        {
            SendChatMessage(Utils.StringToBytes(message), type, fromPos, fromName, fromAgentID, source, audible);
        }

        public void SendChatMessage(byte[] message, byte type, Vector3 fromPos, string fromName,
                                    UUID fromAgentID, byte source, byte audible)
        {
            ChatFromSimulatorPacket reply = (ChatFromSimulatorPacket)PacketPool.Instance.GetPacket(PacketType.ChatFromSimulator);
            reply.ChatData.Audible = audible;
            reply.ChatData.Message = message;
            reply.ChatData.ChatType = type;
            reply.ChatData.SourceType = source;
            reply.ChatData.Position = fromPos;
            reply.ChatData.FromName = Utils.StringToBytes(fromName);
            reply.ChatData.OwnerID = fromAgentID;
            reply.ChatData.SourceID = fromAgentID;

            OutPacket(reply, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// Send an instant message to this client
        /// </summary>
        /// <param name="message"></param>
        /// <param name="target"></param>
        public void SendInstantMessage(UUID fromAgent, UUID fromAgentSession, string message, UUID toAgent,
                                       UUID imSessionID, string fromName, byte dialog, uint timeStamp)
        {
            SendInstantMessage(
                fromAgent, fromAgentSession, message, toAgent,
                imSessionID, fromName, dialog, timeStamp, new byte[0]);
        }

        /// <summary>
        /// Send an instant message to this client
        /// </summary>
        /// <param name="message"></param>
        /// <param name="target"></param>
        public void SendInstantMessage(UUID fromAgent, UUID fromAgentSession, string message, UUID toAgent,
                                       UUID imSessionID, string fromName, byte dialog, uint timeStamp,
                                       byte[] binaryBucket)
        {
            if (((Scene)(this.m_scene)).ExternalChecks.ExternalChecksCanInstantMessage(fromAgent, toAgent))
            {
                ImprovedInstantMessagePacket msg
                    = (ImprovedInstantMessagePacket)PacketPool.Instance.GetPacket(PacketType.ImprovedInstantMessage);

                msg.AgentData.AgentID = fromAgent;
                msg.AgentData.SessionID = fromAgentSession;
                msg.MessageBlock.FromAgentName = Utils.StringToBytes(fromName);
                msg.MessageBlock.Dialog = dialog;
                msg.MessageBlock.FromGroup = false;
                msg.MessageBlock.ID = imSessionID;
                msg.MessageBlock.Offline = 0;
                msg.MessageBlock.ParentEstateID = 0;
                msg.MessageBlock.Position = new Vector3();
                msg.MessageBlock.RegionID = UUID.Random();
                msg.MessageBlock.Timestamp = timeStamp;
                msg.MessageBlock.ToAgentID = toAgent;
                msg.MessageBlock.Message = Utils.StringToBytes(message);
                msg.MessageBlock.BinaryBucket = binaryBucket;

                OutPacket(msg, ThrottleOutPacketType.Task);
            }
        }

        public void SendGenericMessage(string method, List<string> message)
        {

            GenericMessagePacket gmp = new GenericMessagePacket();
            gmp.MethodData.Method = Utils.StringToBytes(method);
            gmp.ParamList = new GenericMessagePacket.ParamListBlock[message.Count];
            int i = 0;
            foreach (string val in message)
            {
                gmp.ParamList[i] = new GenericMessagePacket.ParamListBlock();
                gmp.ParamList[i++].Parameter = Utils.StringToBytes(val);
            }
            OutPacket(gmp, ThrottleOutPacketType.Task);
        }

        /// <summary>
        ///  Send the region heightmap to the client
        /// </summary>
        /// <param name="map">heightmap</param>
        public virtual void SendLayerData(float[] map)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(DoSendLayerData), (object)map);
        }

        /// <summary>
        /// Send terrain layer information to the client.
        /// </summary>
        /// <param name="o"></param>
        private void DoSendLayerData(object o)
        {
            float[] map = (float[])o;

            try
            {
                for (int y = 0; y < 16; y++)
                {
                    // For some terrains, sending more than one terrain patch at once results in a libsecondlife exception
                    // see http://opensimulator.org/mantis/view.php?id=1662
                    //for (int x = 0; x < 16; x += 4)
                    //{
                    //    SendLayerPacket(map, y, x);
                    //    Thread.Sleep(150);
                    //}
                    for (int x = 0; x < 16; x++)
                    {
                        SendLayerData(x, y, map);
                        Thread.Sleep(35);
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Warn("[CLIENT]: ClientView.API.cs: SendLayerData() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Sends a set of four patches (x, x+1, ..., x+3) to the client
        /// </summary>
        /// <param name="map">heightmap</param>
        /// <param name="px">X coordinate for patches 0..12</param>
        /// <param name="py">Y coordinate for patches 0..15</param>
        // private void SendLayerPacket(float[] map, int y, int x)
        // {
        //     int[] patches = new int[4];
        //     patches[0] = x + 0 + y * 16;
        //     patches[1] = x + 1 + y * 16;
        //     patches[2] = x + 2 + y * 16;
        //     patches[3] = x + 3 + y * 16;

        //     Packet layerpack = LLClientView.TerrainManager.CreateLandPacket(map, patches);
        //     OutPacket(layerpack, ThrottleOutPacketType.Land);
        // }

        /// <summary>
        /// Sends a specified patch to a client
        /// </summary>
        /// <param name="px">Patch coordinate (x) 0..15</param>
        /// <param name="py">Patch coordinate (y) 0..15</param>
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

                LayerDataPacket layerpack = TerrainCompressor.CreateLandPacket(map, patches);
                layerpack.Header.Zerocoded = true;

                OutPacket(layerpack, ThrottleOutPacketType.Land);
            }
            catch (Exception e)
            {
                m_log.Warn("[client]: ClientView.API.cs: SendLayerData() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Tell the client that the given neighbour region is ready to receive a child agent.
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
            agentData.CapsPath = m_scene.GetCapsPath(m_agentId);
            return agentData;
        }

        public void CrossRegion(ulong newRegionHandle, Vector3 pos, Vector3 lookAt, IPEndPoint externalIPEndPoint,
                                string capsURL)
        {
            Vector3 look = new Vector3(lookAt.X * 10, lookAt.Y * 10, lookAt.Z * 10);

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
            newSimPack.RegionData.SeedCapability = Utils.StringToBytes(capsURL);

            // Hack to get this out immediately and skip throttles
            OutPacket(newSimPack, ThrottleOutPacketType.Unknown);
        }

        internal void SendMapBlockSplit(List<MapBlockData> mapBlocks, uint flag)
        {
            MapBlockReplyPacket mapReply = (MapBlockReplyPacket)PacketPool.Instance.GetPacket(PacketType.MapBlockReply);
            // TODO: don't create new blocks if recycling an old packet

            MapBlockData[] mapBlocks2 = mapBlocks.ToArray();

            mapReply.AgentData.AgentID = AgentId;
            mapReply.Data = new MapBlockReplyPacket.DataBlock[mapBlocks2.Length];
            mapReply.AgentData.Flags = flag;

            for (int i = 0; i < mapBlocks2.Length; i++)
            {
                mapReply.Data[i] = new MapBlockReplyPacket.DataBlock();
                mapReply.Data[i].MapImageID = mapBlocks2[i].MapImageId;
                //m_log.Warn(mapBlocks2[i].MapImageId.ToString());
                mapReply.Data[i].X = mapBlocks2[i].X;
                mapReply.Data[i].Y = mapBlocks2[i].Y;
                mapReply.Data[i].WaterHeight = mapBlocks2[i].WaterHeight;
                mapReply.Data[i].Name = Utils.StringToBytes(mapBlocks2[i].Name);
                mapReply.Data[i].RegionFlags = mapBlocks2[i].RegionFlags;
                mapReply.Data[i].Access = mapBlocks2[i].Access;
                mapReply.Data[i].Agents = mapBlocks2[i].Agents;
            }
            OutPacket(mapReply, ThrottleOutPacketType.Land);
        }

        public void SendMapBlock(List<MapBlockData> mapBlocks, uint flag)
        {

            MapBlockData[] mapBlocks2 = mapBlocks.ToArray();

            int maxsend = 10;

            //int packets = Math.Ceiling(mapBlocks2.Length / maxsend);

            List<MapBlockData> sendingBlocks = new List<MapBlockData>();

            for (int i = 0; i < mapBlocks2.Length; i++)
            {
                sendingBlocks.Add(mapBlocks2[i]);
                if (((i + 1) == mapBlocks2.Length) || ((i % maxsend) == 0))
                {
                    SendMapBlockSplit(sendingBlocks, flag);
                    sendingBlocks = new List<MapBlockData>();
                }
            }
        }

        public void SendLocalTeleport(Vector3 position, Vector3 lookAt, uint flags)
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

            teleport.Info.SeedCapability = Utils.StringToBytes(capsURL);

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
        /// Inform the client that a teleport attempt has failed
        /// </summary>
        public void SendTeleportFailed(string reason)
        {
            TeleportFailedPacket tpFailed = (TeleportFailedPacket)PacketPool.Instance.GetPacket(PacketType.TeleportFailed);
            tpFailed.Info.AgentID = AgentId;
            tpFailed.Info.Reason = Utils.StringToBytes(reason);

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

        public void SendMoneyBalance(UUID transaction, bool success, byte[] description, int balance)
        {
            MoneyBalanceReplyPacket money = (MoneyBalanceReplyPacket)PacketPool.Instance.GetPacket(PacketType.MoneyBalanceReply);
            money.MoneyData.AgentID = AgentId;
            money.MoneyData.TransactionID = transaction;
            money.MoneyData.TransactionSuccess = success;
            money.MoneyData.Description = description;
            money.MoneyData.MoneyBalance = balance;
            OutPacket(money, ThrottleOutPacketType.Task);
        }

        public void SendPayPrice(UUID objectID, int[] payPrice)
        {
            if (payPrice[0] == 0 &&
                payPrice[1] == 0 &&
                payPrice[2] == 0 &&
                payPrice[3] == 0 &&
                payPrice[4] == 0)
                return;

            PayPriceReplyPacket payPriceReply = (PayPriceReplyPacket)PacketPool.Instance.GetPacket(PacketType.PayPriceReply);
            payPriceReply.ObjectData.ObjectID = objectID;
            payPriceReply.ObjectData.DefaultPayPrice = payPrice[0];

            payPriceReply.ButtonData = new PayPriceReplyPacket.ButtonDataBlock[4];
            payPriceReply.ButtonData[0] = new PayPriceReplyPacket.ButtonDataBlock();
            payPriceReply.ButtonData[0].PayButton = payPrice[1];
            payPriceReply.ButtonData[1] = new PayPriceReplyPacket.ButtonDataBlock();
            payPriceReply.ButtonData[1].PayButton = payPrice[2];
            payPriceReply.ButtonData[2] = new PayPriceReplyPacket.ButtonDataBlock();
            payPriceReply.ButtonData[2].PayButton = payPrice[3];
            payPriceReply.ButtonData[3] = new PayPriceReplyPacket.ButtonDataBlock();
            payPriceReply.ButtonData[3].PayButton = payPrice[4];

            OutPacket(payPriceReply, ThrottleOutPacketType.Task);
        }

        public void SendStartPingCheck(byte seq)
        {
            StartPingCheckPacket pc = (StartPingCheckPacket)PacketPool.Instance.GetPacket(PacketType.StartPingCheck);
            pc.PingID.PingID = seq;
            pc.Header.Reliable = false;
            OutPacket(pc, ThrottleOutPacketType.Unknown);
        }

        public void SendKiPrimitive(ulong regionHandle, uint localID)
        {
            KillObjectPacket kill = (KillObjectPacket)PacketPool.Instance.GetPacket(PacketType.KillObject);
            // TODO: don't create new blocks if recycling an old packet
            kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
            kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
            kill.ObjectData[0].ID = localID;
            kill.Header.Reliable = false;
            kill.Header.Zerocoded = true;
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
        public void SendInventoryFolderDetails(UUID ownerID, UUID folderID, List<InventoryItemBase> items,
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

            //Ckrinke This variable is not used, so comment out to remove the warning from the compiler (3-21-08)
            //Ckrinke            uint FULL_MASK_PERMISSIONS = 2147483647;

            if (fetchItems)
            {
                InventoryDescendentsPacket descend = CreateInventoryDescendentsPacket(ownerID, folderID);

                if (items.Count < MAX_ITEMS_PER_PACKET)
                {
                    descend.ItemData = new InventoryDescendentsPacket.ItemDataBlock[items.Count];
                }
                else
                {
                    descend.ItemData = new InventoryDescendentsPacket.ItemDataBlock[MAX_ITEMS_PER_PACKET];
                }

                // Descendents must contain the *total* number of descendents (plus folders, whether we
                // fetch them or not), not the number of entries we send in this packet. For consistency,
                // I'll use it for folder-requests, too, although I wasn't able to get one with
                // FetchFolders = true.
                // TODO this should be checked with FetchFolders = true
                descend.AgentData.Descendents = items.Count + folders.Count;

                int count = 0;
                int i = 0;
                foreach (InventoryItemBase item in items)
                {
                    descend.ItemData[i] = new InventoryDescendentsPacket.ItemDataBlock();
                    descend.ItemData[i].ItemID = item.ID;
                    descend.ItemData[i].AssetID = item.AssetID;
                    descend.ItemData[i].CreatorID = item.Creator;
                    descend.ItemData[i].BaseMask = item.BasePermissions;
                    descend.ItemData[i].Description = Utils.StringToBytes(item.Description);
                    descend.ItemData[i].EveryoneMask = item.EveryOnePermissions;
                    descend.ItemData[i].OwnerMask = item.CurrentPermissions;
                    descend.ItemData[i].FolderID = item.Folder;
                    descend.ItemData[i].InvType = (sbyte)item.InvType;
                    descend.ItemData[i].Name = Utils.StringToBytes(item.Name);
                    descend.ItemData[i].NextOwnerMask = item.NextPermissions;
                    descend.ItemData[i].OwnerID = item.Owner;
                    descend.ItemData[i].Type = (sbyte)item.AssetType;

                    //descend.ItemData[i].GroupID = new UUID("00000000-0000-0000-0000-000000000000");
                    descend.ItemData[i].GroupID = item.GroupID;
                    descend.ItemData[i].GroupOwned = item.GroupOwned;
                    descend.ItemData[i].GroupMask = 0;
                    descend.ItemData[i].CreationDate = item.CreationDate;
                    descend.ItemData[i].SalePrice = item.SalePrice;
                    descend.ItemData[i].SaleType = item.SaleType;
                    descend.ItemData[i].Flags = item.Flags;

                    descend.ItemData[i].CRC =
                        Helpers.InventoryCRC(descend.ItemData[i].CreationDate, descend.ItemData[i].SaleType,
                                             descend.ItemData[i].InvType, descend.ItemData[i].Type,
                                             descend.ItemData[i].AssetID, descend.ItemData[i].GroupID,
                                             descend.ItemData[i].SalePrice,
                                             descend.ItemData[i].OwnerID, descend.ItemData[i].CreatorID,
                                             descend.ItemData[i].ItemID, descend.ItemData[i].FolderID,
                                             descend.ItemData[i].EveryoneMask,
                                             descend.ItemData[i].Flags, descend.ItemData[i].OwnerMask,
                                             descend.ItemData[i].GroupMask, item.CurrentPermissions);

                    i++;
                    count++;
                    if (i == MAX_ITEMS_PER_PACKET)
                    {
                        descend.Header.Zerocoded = true;
                        OutPacket(descend, ThrottleOutPacketType.Asset);

                        if ((items.Count - count) > 0)
                        {
                            descend = CreateInventoryDescendentsPacket(ownerID, folderID);
                            if ((items.Count - count) < MAX_ITEMS_PER_PACKET)
                            {
                                descend.ItemData = new InventoryDescendentsPacket.ItemDataBlock[items.Count - count];
                            }
                            else
                            {
                                descend.ItemData = new InventoryDescendentsPacket.ItemDataBlock[MAX_ITEMS_PER_PACKET];
                            }
                            descend.AgentData.Descendents = items.Count + folders.Count;
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
            if (fetchFolders)
            {
                InventoryDescendentsPacket descend = CreateInventoryDescendentsPacket(ownerID, folderID);

                if (folders.Count < MAX_ITEMS_PER_PACKET)
                {
                    descend.FolderData = new InventoryDescendentsPacket.FolderDataBlock[folders.Count];
                }
                else
                {
                    descend.FolderData = new InventoryDescendentsPacket.FolderDataBlock[MAX_ITEMS_PER_PACKET];
                }

                // Not sure if this scenario ever actually occurs, but nonetheless we include the items
                // count even if we're not sending item data for the same reasons as above.
                descend.AgentData.Descendents = items.Count + folders.Count;

                int i = 0;
                int count = 0;
                foreach (InventoryFolderBase folder in folders)
                {
                    descend.FolderData[i] = new InventoryDescendentsPacket.FolderDataBlock();
                    descend.FolderData[i].FolderID = folder.ID;
                    descend.FolderData[i].Name = Utils.StringToBytes(folder.Name);
                    descend.FolderData[i].ParentID = folder.ParentID;
                    descend.FolderData[i].Type = (sbyte)folder.Type;

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
                            }
                            else
                            {
                                descend.FolderData =
                                    new InventoryDescendentsPacket.FolderDataBlock[MAX_ITEMS_PER_PACKET];
                            }
                            descend.AgentData.Descendents = items.Count + folders.Count;
                            i = 0;
                        }
                    }
                }

                if (i < MAX_ITEMS_PER_PACKET)
                {
                    OutPacket(descend, ThrottleOutPacketType.Asset);
                }
            }
        }

        private InventoryDescendentsPacket CreateInventoryDescendentsPacket(UUID ownerID, UUID folderID)
        {
            InventoryDescendentsPacket descend = (InventoryDescendentsPacket)PacketPool.Instance.GetPacket(PacketType.InventoryDescendents);
            descend.Header.Zerocoded = true;
            descend.AgentData.AgentID = AgentId;
            descend.AgentData.OwnerID = ownerID;
            descend.AgentData.FolderID = folderID;
            descend.AgentData.Version = 1;

            return descend;
        }

        public void SendInventoryItemDetails(UUID ownerID, InventoryItemBase item)
        {
            uint FULL_MASK_PERMISSIONS = (uint)PermissionMask.All;
            FetchInventoryReplyPacket inventoryReply = (FetchInventoryReplyPacket)PacketPool.Instance.GetPacket(PacketType.FetchInventoryReply);
            // TODO: don't create new blocks if recycling an old packet
            inventoryReply.AgentData.AgentID = AgentId;
            inventoryReply.InventoryData = new FetchInventoryReplyPacket.InventoryDataBlock[1];
            inventoryReply.InventoryData[0] = new FetchInventoryReplyPacket.InventoryDataBlock();
            inventoryReply.InventoryData[0].ItemID = item.ID;
            inventoryReply.InventoryData[0].AssetID = item.AssetID;
            inventoryReply.InventoryData[0].CreatorID = item.Creator;
            inventoryReply.InventoryData[0].BaseMask = item.BasePermissions;
            inventoryReply.InventoryData[0].CreationDate =
                (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            inventoryReply.InventoryData[0].Description = Utils.StringToBytes(item.Description);
            inventoryReply.InventoryData[0].EveryoneMask = item.EveryOnePermissions;
            inventoryReply.InventoryData[0].FolderID = item.Folder;
            inventoryReply.InventoryData[0].InvType = (sbyte)item.InvType;
            inventoryReply.InventoryData[0].Name = Utils.StringToBytes(item.Name);
            inventoryReply.InventoryData[0].NextOwnerMask = item.NextPermissions;
            inventoryReply.InventoryData[0].OwnerID = item.Owner;
            inventoryReply.InventoryData[0].OwnerMask = item.CurrentPermissions;
            inventoryReply.InventoryData[0].Type = (sbyte)item.AssetType;

            //inventoryReply.InventoryData[0].GroupID = new UUID("00000000-0000-0000-0000-000000000000");
            inventoryReply.InventoryData[0].GroupID = item.GroupID;
            inventoryReply.InventoryData[0].GroupOwned = item.GroupOwned;
            inventoryReply.InventoryData[0].GroupMask = 0;
            inventoryReply.InventoryData[0].Flags = item.Flags;
            inventoryReply.InventoryData[0].SalePrice = item.SalePrice;
            inventoryReply.InventoryData[0].SaleType = item.SaleType;

            inventoryReply.InventoryData[0].CRC =
                Helpers.InventoryCRC(1000, 0, inventoryReply.InventoryData[0].InvType,
                                     inventoryReply.InventoryData[0].Type, inventoryReply.InventoryData[0].AssetID,
                                     inventoryReply.InventoryData[0].GroupID, 100,
                                     inventoryReply.InventoryData[0].OwnerID, inventoryReply.InventoryData[0].CreatorID,
                                     inventoryReply.InventoryData[0].ItemID, inventoryReply.InventoryData[0].FolderID,
                                     FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS,
                                     FULL_MASK_PERMISSIONS);
            inventoryReply.Header.Zerocoded = true;
            OutPacket(inventoryReply, ThrottleOutPacketType.Asset);
        }

        /// <see>IClientAPI.SendBulkUpdateInventory(InventoryItemBase)</see>
        public void SendBulkUpdateInventory(InventoryItemBase item)
        {
            uint FULL_MASK_PERMISSIONS = (uint)PermissionMask.All;

            BulkUpdateInventoryPacket bulkUpdate
                = (BulkUpdateInventoryPacket)PacketPool.Instance.GetPacket(PacketType.BulkUpdateInventory);

            bulkUpdate.AgentData.AgentID = AgentId;
            bulkUpdate.AgentData.TransactionID = UUID.Random();

            bulkUpdate.FolderData = new BulkUpdateInventoryPacket.FolderDataBlock[1];
            bulkUpdate.FolderData[0] = new BulkUpdateInventoryPacket.FolderDataBlock();
            bulkUpdate.FolderData[0].FolderID = UUID.Zero;
            bulkUpdate.FolderData[0].ParentID = UUID.Zero;
            bulkUpdate.FolderData[0].Type = -1;
            bulkUpdate.FolderData[0].Name = new byte[0];

            bulkUpdate.ItemData = new BulkUpdateInventoryPacket.ItemDataBlock[1];
            bulkUpdate.ItemData[0] = new BulkUpdateInventoryPacket.ItemDataBlock();
            bulkUpdate.ItemData[0].ItemID = item.ID;
            bulkUpdate.ItemData[0].AssetID = item.AssetID;
            bulkUpdate.ItemData[0].CreatorID = item.Creator;
            bulkUpdate.ItemData[0].BaseMask = item.BasePermissions;
            bulkUpdate.ItemData[0].CreationDate = 1000;
            bulkUpdate.ItemData[0].Description = Utils.StringToBytes(item.Description);
            bulkUpdate.ItemData[0].EveryoneMask = item.EveryOnePermissions;
            bulkUpdate.ItemData[0].FolderID = item.Folder;
            bulkUpdate.ItemData[0].InvType = (sbyte)item.InvType;
            bulkUpdate.ItemData[0].Name = Utils.StringToBytes(item.Name);
            bulkUpdate.ItemData[0].NextOwnerMask = item.NextPermissions;
            bulkUpdate.ItemData[0].OwnerID = item.Owner;
            bulkUpdate.ItemData[0].OwnerMask = item.CurrentPermissions;
            bulkUpdate.ItemData[0].Type = (sbyte)item.AssetType;

            //bulkUpdate.ItemData[0].GroupID = new UUID("00000000-0000-0000-0000-000000000000");
            bulkUpdate.ItemData[0].GroupID = item.GroupID;
            bulkUpdate.ItemData[0].GroupOwned = item.GroupOwned;
            bulkUpdate.ItemData[0].GroupMask = 0;
            bulkUpdate.ItemData[0].Flags = item.Flags;
            bulkUpdate.ItemData[0].SalePrice = item.SalePrice;
            bulkUpdate.ItemData[0].SaleType = item.SaleType;

            bulkUpdate.ItemData[0].CRC =
                Helpers.InventoryCRC(1000, 0, bulkUpdate.ItemData[0].InvType,
                                     bulkUpdate.ItemData[0].Type, bulkUpdate.ItemData[0].AssetID,
                                     bulkUpdate.ItemData[0].GroupID, 100,
                                     bulkUpdate.ItemData[0].OwnerID, bulkUpdate.ItemData[0].CreatorID,
                                     bulkUpdate.ItemData[0].ItemID, bulkUpdate.ItemData[0].FolderID,
                                     FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS,
                                     FULL_MASK_PERMISSIONS);
            bulkUpdate.Header.Zerocoded = true;
            OutPacket(bulkUpdate, ThrottleOutPacketType.Asset);
        }

        /// <see>IClientAPI.SendInventoryItemCreateUpdate(InventoryItemBase)</see>
        public void SendInventoryItemCreateUpdate(InventoryItemBase Item)
        {
            uint FULL_MASK_PERMISSIONS = (uint)PermissionMask.All;

            UpdateCreateInventoryItemPacket InventoryReply
                = (UpdateCreateInventoryItemPacket)PacketPool.Instance.GetPacket(
                                                       PacketType.UpdateCreateInventoryItem);

            // TODO: don't create new blocks if recycling an old packet
            InventoryReply.AgentData.AgentID = AgentId;
            InventoryReply.AgentData.SimApproved = true;
            InventoryReply.InventoryData = new UpdateCreateInventoryItemPacket.InventoryDataBlock[1];
            InventoryReply.InventoryData[0] = new UpdateCreateInventoryItemPacket.InventoryDataBlock();
            InventoryReply.InventoryData[0].ItemID = Item.ID;
            InventoryReply.InventoryData[0].AssetID = Item.AssetID;
            InventoryReply.InventoryData[0].CreatorID = Item.Creator;
            InventoryReply.InventoryData[0].BaseMask = Item.BasePermissions;
            InventoryReply.InventoryData[0].Description = Utils.StringToBytes(Item.Description);
            InventoryReply.InventoryData[0].EveryoneMask = Item.EveryOnePermissions;
            InventoryReply.InventoryData[0].FolderID = Item.Folder;
            InventoryReply.InventoryData[0].InvType = (sbyte)Item.InvType;
            InventoryReply.InventoryData[0].Name = Utils.StringToBytes(Item.Name);
            InventoryReply.InventoryData[0].NextOwnerMask = Item.NextPermissions;
            InventoryReply.InventoryData[0].OwnerID = Item.Owner;
            InventoryReply.InventoryData[0].OwnerMask = Item.CurrentPermissions;
            InventoryReply.InventoryData[0].Type = (sbyte)Item.AssetType;

            //InventoryReply.InventoryData[0].GroupID = new UUID("00000000-0000-0000-0000-000000000000");
            InventoryReply.InventoryData[0].GroupID = Item.GroupID;
            InventoryReply.InventoryData[0].GroupOwned = Item.GroupOwned;
            InventoryReply.InventoryData[0].GroupMask = 0;
            InventoryReply.InventoryData[0].Flags = Item.Flags;
            InventoryReply.InventoryData[0].SalePrice = Item.SalePrice;
            InventoryReply.InventoryData[0].SaleType = Item.SaleType;
            InventoryReply.InventoryData[0].CreationDate = Item.CreationDate;

            InventoryReply.InventoryData[0].CRC =
                Helpers.InventoryCRC(1000, 0, InventoryReply.InventoryData[0].InvType,
                                     InventoryReply.InventoryData[0].Type, InventoryReply.InventoryData[0].AssetID,
                                     InventoryReply.InventoryData[0].GroupID, 100,
                                     InventoryReply.InventoryData[0].OwnerID, InventoryReply.InventoryData[0].CreatorID,
                                     InventoryReply.InventoryData[0].ItemID, InventoryReply.InventoryData[0].FolderID,
                                     FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS,
                                     FULL_MASK_PERMISSIONS);
            InventoryReply.Header.Zerocoded = true;
            OutPacket(InventoryReply, ThrottleOutPacketType.Asset);
        }

        public void SendRemoveInventoryItem(UUID itemID)
        {
            RemoveInventoryItemPacket remove = (RemoveInventoryItemPacket)PacketPool.Instance.GetPacket(PacketType.RemoveInventoryItem);
            // TODO: don't create new blocks if recycling an old packet
            remove.AgentData.AgentID = AgentId;
            remove.AgentData.SessionID = m_sessionId;
            remove.InventoryData = new RemoveInventoryItemPacket.InventoryDataBlock[1];
            remove.InventoryData[0] = new RemoveInventoryItemPacket.InventoryDataBlock();
            remove.InventoryData[0].ItemID = itemID;
            remove.Header.Zerocoded = true;
            OutPacket(remove, ThrottleOutPacketType.Asset);
        }

        public void SendTakeControls(int controls, bool passToAgent, bool TakeControls)
        {
            ScriptControlChangePacket scriptcontrol = (ScriptControlChangePacket)PacketPool.Instance.GetPacket(PacketType.ScriptControlChange);
            ScriptControlChangePacket.DataBlock[] data = new ScriptControlChangePacket.DataBlock[1];
            ScriptControlChangePacket.DataBlock ddata = new ScriptControlChangePacket.DataBlock();
            ddata.Controls = (uint)controls;
            ddata.PassToAgent = passToAgent;
            ddata.TakeControls = TakeControls;
            data[0] = ddata;
            scriptcontrol.Data = data;
            OutPacket(scriptcontrol, ThrottleOutPacketType.Task);
        }

        public void SendTaskInventory(UUID taskID, short serial, byte[] fileName)
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

        public void SendEconomyData(float EnergyEfficiency, int ObjectCapacity, int ObjectCount, int PriceEnergyUnit,
                                    int PriceGroupCreate, int PriceObjectClaim, float PriceObjectRent, float PriceObjectScaleFactor,
                                    int PriceParcelClaim, float PriceParcelClaimFactor, int PriceParcelRent, int PricePublicObjectDecay,
                                    int PricePublicObjectDelete, int PriceRentLight, int PriceUpload, int TeleportMinPrice, float TeleportPriceExponent)
        {
            EconomyDataPacket economyData = (EconomyDataPacket)PacketPool.Instance.GetPacket(PacketType.EconomyData);
            economyData.Info.EnergyEfficiency = EnergyEfficiency;
            economyData.Info.ObjectCapacity = ObjectCapacity;
            economyData.Info.ObjectCount = ObjectCount;
            economyData.Info.PriceEnergyUnit = PriceEnergyUnit;
            economyData.Info.PriceGroupCreate = PriceGroupCreate;
            economyData.Info.PriceObjectClaim = PriceObjectClaim;
            economyData.Info.PriceObjectRent = PriceObjectRent;
            economyData.Info.PriceObjectScaleFactor = PriceObjectScaleFactor;
            economyData.Info.PriceParcelClaim = PriceParcelClaim;
            economyData.Info.PriceParcelClaimFactor = PriceParcelClaimFactor;
            economyData.Info.PriceParcelRent = PriceParcelRent;
            economyData.Info.PricePublicObjectDecay = PricePublicObjectDecay;
            economyData.Info.PricePublicObjectDelete = PricePublicObjectDelete;
            economyData.Info.PriceRentLight = PriceRentLight;
            economyData.Info.PriceUpload = PriceUpload;
            economyData.Info.TeleportMinPrice = TeleportMinPrice;
            economyData.Info.TeleportPriceExponent = TeleportPriceExponent;
            economyData.Header.Reliable = true;
            OutPacket(economyData, ThrottleOutPacketType.Unknown);
        }

        public void SendAvatarPickerReply(AvatarPickerReplyAgentDataArgs AgentData, List<AvatarPickerReplyDataArgs> Data)
        {
            //construct the AvatarPickerReply packet.
            AvatarPickerReplyPacket replyPacket = new AvatarPickerReplyPacket();
            replyPacket.AgentData.AgentID = AgentData.AgentID;
            replyPacket.AgentData.QueryID = AgentData.QueryID;
            //int i = 0;
            List<AvatarPickerReplyPacket.DataBlock> data_block = new List<AvatarPickerReplyPacket.DataBlock>();
            foreach (AvatarPickerReplyDataArgs arg in Data)
            {
                AvatarPickerReplyPacket.DataBlock db = new AvatarPickerReplyPacket.DataBlock();
                db.AvatarID = arg.AvatarID;
                db.FirstName = arg.FirstName;
                db.LastName = arg.LastName;
                data_block.Add(db);
            }
            replyPacket.Data = data_block.ToArray();
            OutPacket(replyPacket, ThrottleOutPacketType.Task);
        }

        public void SendAgentDataUpdate(UUID agentid, UUID activegroupid, string firstname, string lastname, ulong grouppowers, string groupname, string grouptitle)
        {

            m_activeGroupID = activegroupid;
            m_activeGroupName = groupname;
            m_activeGroupPowers = grouppowers;

            AgentDataUpdatePacket sendAgentDataUpdate = (AgentDataUpdatePacket)PacketPool.Instance.GetPacket(PacketType.AgentDataUpdate);
            sendAgentDataUpdate.AgentData.ActiveGroupID = activegroupid;
            sendAgentDataUpdate.AgentData.AgentID = agentid;
            sendAgentDataUpdate.AgentData.FirstName = Utils.StringToBytes(firstname);
            sendAgentDataUpdate.AgentData.GroupName = Utils.StringToBytes(groupname);
            sendAgentDataUpdate.AgentData.GroupPowers = grouppowers;
            sendAgentDataUpdate.AgentData.GroupTitle = Utils.StringToBytes(grouptitle);
            sendAgentDataUpdate.AgentData.LastName = Utils.StringToBytes(lastname);
            OutPacket(sendAgentDataUpdate, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// Send an alert message to the client.  On the Linden client (tested 1.19.1.4), this pops up a brief duration
        /// blue information box in the bottom right hand corner.
        /// </summary>
        /// <param name="message"></param>
        public void SendAlertMessage(string message)
        {
            AlertMessagePacket alertPack = (AlertMessagePacket)PacketPool.Instance.GetPacket(PacketType.AlertMessage);
            alertPack.AlertData.Message = Utils.StringToBytes(message);
            OutPacket(alertPack, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// Send an agent alert message to the client.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="modal">On the linden client, if this true then it displays a one button text box placed in the
        /// middle of the window.  If false, the message is displayed in a brief duration blue information box (as for
        /// the AlertMessage packet).</param>
        public void SendAgentAlertMessage(string message, bool modal)
        {
            OutPacket(BuildAgentAlertPacket(message, modal), ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// Construct an agent alert packet
        /// </summary>
        /// <param name="message"></param>
        /// <param name="modal"></param>
        /// <returns></returns>
        protected AgentAlertMessagePacket BuildAgentAlertPacket(string message, bool modal)
        {
            AgentAlertMessagePacket alertPack = (AgentAlertMessagePacket)PacketPool.Instance.GetPacket(PacketType.AgentAlertMessage);
            alertPack.AgentData.AgentID = AgentId;
            alertPack.AlertData.Message = Utils.StringToBytes(message);
            alertPack.AlertData.Modal = modal;

            return alertPack;
        }

        public void SendLoadURL(string objectname, UUID objectID, UUID ownerID, bool groupOwned, string message,
                                string url)
        {
            LoadURLPacket loadURL = (LoadURLPacket)PacketPool.Instance.GetPacket(PacketType.LoadURL);
            loadURL.Data.ObjectName = Utils.StringToBytes(objectname);
            loadURL.Data.ObjectID = objectID;
            loadURL.Data.OwnerID = ownerID;
            loadURL.Data.OwnerIsGroup = groupOwned;
            loadURL.Data.Message = Utils.StringToBytes(message);
            loadURL.Data.URL = Utils.StringToBytes(url);
            OutPacket(loadURL, ThrottleOutPacketType.Task);
        }

        public void SendDialog(string objectname, UUID objectID, UUID ownerID, string msg, UUID textureID, int ch, string[] buttonlabels)
        {
            ScriptDialogPacket dialog = (ScriptDialogPacket)PacketPool.Instance.GetPacket(PacketType.ScriptDialog);
            dialog.Data.ObjectID = objectID;
            dialog.Data.ObjectName = Utils.StringToBytes(objectname);
            dialog.Data.FirstName = Utils.StringToBytes(this.FirstName);
            dialog.Data.LastName = Utils.StringToBytes(this.LastName);
            dialog.Data.Message = Utils.StringToBytes(msg);
            dialog.Data.ImageID = textureID;
            dialog.Data.ChatChannel = ch;
            ScriptDialogPacket.ButtonsBlock[] buttons = new ScriptDialogPacket.ButtonsBlock[buttonlabels.Length];
            for (int i = 0; i < buttonlabels.Length; i++)
            {
                buttons[i] = new ScriptDialogPacket.ButtonsBlock();
                buttons[i].ButtonLabel = Utils.StringToBytes(buttonlabels[i]);
            }
            dialog.Buttons = buttons;
            OutPacket(dialog, ThrottleOutPacketType.Task);
        }

        public void SendPreLoadSound(UUID objectID, UUID ownerID, UUID soundID)
        {
            PreloadSoundPacket preSound = (PreloadSoundPacket)PacketPool.Instance.GetPacket(PacketType.PreloadSound);
            // TODO: don't create new blocks if recycling an old packet
            preSound.DataBlock = new PreloadSoundPacket.DataBlockBlock[1];
            preSound.DataBlock[0] = new PreloadSoundPacket.DataBlockBlock();
            preSound.DataBlock[0].ObjectID = objectID;
            preSound.DataBlock[0].OwnerID = ownerID;
            preSound.DataBlock[0].SoundID = soundID;
            preSound.Header.Zerocoded = true;
            OutPacket(preSound, ThrottleOutPacketType.Task);
        }

        public void SendPlayAttachedSound(UUID soundID, UUID objectID, UUID ownerID, float gain, byte flags)
        {
            AttachedSoundPacket sound = (AttachedSoundPacket)PacketPool.Instance.GetPacket(PacketType.AttachedSound);
            sound.DataBlock.SoundID = soundID;
            sound.DataBlock.ObjectID = objectID;
            sound.DataBlock.OwnerID = ownerID;
            sound.DataBlock.Gain = gain;
            sound.DataBlock.Flags = flags;

            OutPacket(sound, ThrottleOutPacketType.Task);
        }

        public void SendTriggeredSound(UUID soundID, UUID ownerID, UUID objectID, UUID parentID, ulong handle, Vector3 position, float gain)
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

        public void SendAttachedSoundGainChange(UUID objectID, float gain)
        {
            AttachedSoundGainChangePacket sound = (AttachedSoundGainChangePacket)PacketPool.Instance.GetPacket(PacketType.AttachedSoundGainChange);
            sound.DataBlock.ObjectID = objectID;
            sound.DataBlock.Gain = gain;

            OutPacket(sound, ThrottleOutPacketType.Task);
        }

        public void SendSunPos(Vector3 Position, Vector3 Velocity, ulong CurrentTime, uint SecondsPerSunCycle, uint SecondsPerYear, float OrbitalPosition)
        {
            SimulatorViewerTimeMessagePacket viewertime = (SimulatorViewerTimeMessagePacket)PacketPool.Instance.GetPacket(PacketType.SimulatorViewerTimeMessage);
            viewertime.TimeInfo.SunDirection = Position;
            viewertime.TimeInfo.SunAngVelocity = Velocity;
            viewertime.TimeInfo.UsecSinceStart = CurrentTime;
            viewertime.TimeInfo.SecPerDay = SecondsPerSunCycle;
            viewertime.TimeInfo.SecPerYear = SecondsPerYear;
            viewertime.TimeInfo.SunPhase = OrbitalPosition;
            viewertime.Header.Reliable = false;
            viewertime.Header.Zerocoded = true;
            OutPacket(viewertime, ThrottleOutPacketType.Task);
        }

        // Currently Deprecated
        public void SendViewerTime(int phase)
        {
            /*
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
                viewertime.TimeInfo.SunDirection = new Vector3(0f, 0.8f, -0.8f);
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
                viewertime.TimeInfo.SunDirection = new Vector3(0f, yValue, 0.3f);
                Console.WriteLine("sending sun update " + yValue);
            }
            viewertime.TimeInfo.SunAngVelocity = new Vector3(0, 0.0f, 10.0f);
            viewertime.TimeInfo.UsecSinceStart = (ulong)Util.UnixTimeSinceEpoch();
            viewertime.Header.Reliable = false;
            OutPacket(viewertime, ThrottleOutPacketType.Task);
            */
        }

        public void SendAvatarProperties(UUID avatarID, string aboutText, string bornOn, Byte[] charterMember,
                                         string flAbout, uint flags, UUID flImageID, UUID imageID, string profileURL,
                                         UUID partnerID)
        {
            AvatarPropertiesReplyPacket avatarReply = (AvatarPropertiesReplyPacket)PacketPool.Instance.GetPacket(PacketType.AvatarPropertiesReply);
            avatarReply.AgentData.AgentID = AgentId;
            avatarReply.AgentData.AvatarID = avatarID;
            if (aboutText != null)
                avatarReply.PropertiesData.AboutText = Utils.StringToBytes(aboutText);
            else
                avatarReply.PropertiesData.AboutText = Utils.StringToBytes("");
            avatarReply.PropertiesData.BornOn = Utils.StringToBytes(bornOn);
            avatarReply.PropertiesData.CharterMember = charterMember;
            if (flAbout != null)
                avatarReply.PropertiesData.FLAboutText = Utils.StringToBytes(flAbout);
            else
                avatarReply.PropertiesData.FLAboutText = Utils.StringToBytes("");
            avatarReply.PropertiesData.Flags = flags;
            avatarReply.PropertiesData.FLImageID = flImageID;
            avatarReply.PropertiesData.ImageID = imageID;
            avatarReply.PropertiesData.ProfileURL = Utils.StringToBytes(profileURL);
            avatarReply.PropertiesData.PartnerID = partnerID;
            OutPacket(avatarReply, ThrottleOutPacketType.Task);
        }

        #endregion

        // Gesture

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
        public void SendAppearance(UUID agentID, byte[] visualParams, byte[] textureEntry)
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

        public void SendAnimations(UUID[] animations, int[] seqs, UUID sourceAgentId)
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
        public void SendAvatarData(ulong regionHandle, string firstName, string lastName, UUID avatarID,
                                   uint avatarLocalID, Vector3 Pos, byte[] textureEntry, uint parentID, Quaternion rotation)
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
                Utils.StringToBytes("FirstName STRING RW SV " + firstName + "\nLastName STRING RW SV " + lastName);

            Vector3 pos2 = new Vector3((float)Pos.X, (float)Pos.Y, (float)Pos.Z);
            byte[] pb = pos2.GetBytes();
            Array.Copy(pb, 0, objupdate.ObjectData[0].ObjectData, 16, pb.Length);

            byte[] rot = rotation.GetBytes();
            Array.Copy(rot, 0, objupdate.ObjectData[0].ObjectData, 52, rot.Length);

            objupdate.Header.Zerocoded = true;
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
        public void SendAvatarTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, Vector3 position,
                                          Vector3 velocity, Quaternion rotation)
        {
            if (rotation.X == rotation.Y && rotation.Y == rotation.Z && rotation.Z == rotation.W && rotation.W == 0)
                rotation = Quaternion.Identity;

            ImprovedTerseObjectUpdatePacket.ObjectDataBlock terseBlock =
                CreateAvatarImprovedBlock(localID, position, velocity, rotation);
            ImprovedTerseObjectUpdatePacket terse = (ImprovedTerseObjectUpdatePacket)PacketPool.Instance.GetPacket(PacketType.ImprovedTerseObjectUpdate);
            // TODO: don't create new blocks if recycling an old packet
            terse.RegionData.RegionHandle = regionHandle;
            terse.RegionData.TimeDilation = timeDilation;
            terse.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
            terse.ObjectData[0] = terseBlock;

            terse.Header.Reliable = false;

            terse.Header.Zerocoded = true;
            OutPacket(terse, ThrottleOutPacketType.Task);
        }

        public void SendCoarseLocationUpdate(List<Vector3> CoarseLocations)
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
            loc.Header.Zerocoded = true;
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
        public void AttachObject(uint localID, Quaternion rotation, byte attachPoint)
        {

            ObjectAttachPacket attach = (ObjectAttachPacket)PacketPool.Instance.GetPacket(PacketType.ObjectAttach);
            Console.WriteLine("Attach object!");
            // TODO: don't create new blocks if recycling an old packet
            attach.AgentData.AgentID = AgentId;
            attach.AgentData.SessionID = m_sessionId;
            attach.AgentData.AttachmentPoint = attachPoint;
            attach.ObjectData = new ObjectAttachPacket.ObjectDataBlock[1];
            attach.ObjectData[0] = new ObjectAttachPacket.ObjectDataBlock();
            attach.ObjectData[0].ObjectLocalID = localID;
            attach.ObjectData[0].Rotation = rotation;
            attach.Header.Zerocoded = true;
            OutPacket(attach, ThrottleOutPacketType.Task);
        }

        public void SendPrimitiveToClient(
                                          ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape,
                                          Vector3 pos, Vector3 vel, Vector3 acc, Quaternion rotation, Vector3 rvel,
                                          uint flags, UUID objectID, UUID ownerID, string text, byte[] color,
                                          uint parentID, byte[] particleSystem, byte clickAction)
        {
            byte[] textureanim = new byte[0];

            SendPrimitiveToClient(regionHandle, timeDilation, localID, primShape, pos, vel,
                                  acc, rotation, rvel, flags,
                                  objectID, ownerID, text, color, parentID, particleSystem,
                                  clickAction, textureanim, false, (uint)0, UUID.Zero, UUID.Zero, 0, 0, 0);
        }

        public void SendPrimitiveToClient(
            ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape,
            Vector3 pos, Vector3 velocity, Vector3 acceleration, Quaternion rotation, Vector3 rotational_velocity,
            uint flags,
            UUID objectID, UUID ownerID, string text, byte[] color, uint parentID, byte[] particleSystem,
            byte clickAction, byte[] textureanim, bool attachment, uint AttachPoint, UUID AssetId, UUID SoundId, double SoundGain, byte SoundFlags, double SoundRadius)
        {

            if (rotation.X == rotation.Y && rotation.Y == rotation.Z && rotation.Z == rotation.W && rotation.W == 0)
                rotation = Quaternion.Identity;

            ObjectUpdatePacket outPacket = (ObjectUpdatePacket)PacketPool.Instance.GetPacket(PacketType.ObjectUpdate);



            // TODO: don't create new blocks if recycling an old packet
            outPacket.RegionData.RegionHandle = regionHandle;
            outPacket.RegionData.TimeDilation = timeDilation;
            outPacket.ObjectData = new ObjectUpdatePacket.ObjectDataBlock[1];

            outPacket.ObjectData[0] = CreatePrimUpdateBlock(primShape, flags);

            outPacket.ObjectData[0].ID = localID;
            outPacket.ObjectData[0].FullID = objectID;
            outPacket.ObjectData[0].OwnerID = ownerID;

            // Anything more than 254 will cause libsecondlife to barf
            // (libsl 1550) adds an \0 on the Utils.StringToBytes conversion if it isn't present
            if (text.Length > 254)
            {
                text = text.Remove(254);
            }

            outPacket.ObjectData[0].Text = Utils.StringToBytes(text);

            outPacket.ObjectData[0].TextColor[0] = color[0];
            outPacket.ObjectData[0].TextColor[1] = color[1];
            outPacket.ObjectData[0].TextColor[2] = color[2];
            outPacket.ObjectData[0].TextColor[3] = color[3];
            outPacket.ObjectData[0].ParentID = parentID;
            outPacket.ObjectData[0].PSBlock = particleSystem;
            outPacket.ObjectData[0].ClickAction = clickAction;
            outPacket.ObjectData[0].Flags = 0;

            if (attachment)
            {
                // Necessary???
                outPacket.ObjectData[0].JointAxisOrAnchor = new Vector3(0, 0, 2);
                outPacket.ObjectData[0].JointPivot = new Vector3(0, 0, 0);

                // Item from inventory???
                outPacket.ObjectData[0].NameValue =
                    Utils.StringToBytes("AttachItemID STRING RW SV " + AssetId.Guid);
                outPacket.ObjectData[0].State = (byte)((AttachPoint % 16) * 16 + (AttachPoint / 16));
            }

            // Xantor 20080528: Send sound info as well
            // Xantor 20080530: Zero out everything if there's no SoundId, so zerocompression will work again
            outPacket.ObjectData[0].Sound = SoundId;
            if (SoundId == UUID.Zero)
            {
                outPacket.ObjectData[0].OwnerID = UUID.Zero;
                outPacket.ObjectData[0].Gain = 0.0f;
                outPacket.ObjectData[0].Radius = 0.0f;
                outPacket.ObjectData[0].Flags = 0;
            }
            else
            {
                outPacket.ObjectData[0].OwnerID = ownerID;
                outPacket.ObjectData[0].Gain = (float)SoundGain;
                outPacket.ObjectData[0].Radius = (float)SoundRadius;
                outPacket.ObjectData[0].Flags = SoundFlags;
            }

            byte[] pb = pos.GetBytes();
            Array.Copy(pb, 0, outPacket.ObjectData[0].ObjectData, 0, pb.Length);

            byte[] vel = velocity.GetBytes();
            Array.Copy(vel, 0, outPacket.ObjectData[0].ObjectData, pb.Length, vel.Length);

            byte[] rot = rotation.GetBytes();
            Array.Copy(rot, 0, outPacket.ObjectData[0].ObjectData, 36, rot.Length);

            byte[] rvel = rotational_velocity.GetBytes();
            Array.Copy(rvel, 0, outPacket.ObjectData[0].ObjectData, 36 + rot.Length, rvel.Length);

            if (textureanim.Length > 0)
            {
                outPacket.ObjectData[0].TextureAnim = textureanim;
            }
            outPacket.Header.Zerocoded = true;

            OutPacket(outPacket, ThrottleOutPacketType.Task | ThrottleOutPacketType.LowPriority);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="timeDilation"></param>
        /// <param name="localID"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        public void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, Vector3 position,
                                        Quaternion rotation, Vector3 velocity, Vector3 rotationalvelocity, byte state, UUID AssetId)
        {
            if (rotation.X == rotation.Y && rotation.Y == rotation.Z && rotation.Z == rotation.W && rotation.W == 0)
                rotation = Quaternion.Identity;
            ImprovedTerseObjectUpdatePacket terse = (ImprovedTerseObjectUpdatePacket)PacketPool.Instance.GetPacket(PacketType.ImprovedTerseObjectUpdate);
            // TODO: don't create new blocks if recycling an old packet
            terse.RegionData.RegionHandle = regionHandle;
            terse.RegionData.TimeDilation = timeDilation;
            terse.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
            terse.ObjectData[0] = CreatePrimImprovedBlock(localID, position, rotation, velocity, rotationalvelocity, state); // AssetID should fall into here probably somehow...
            terse.Header.Reliable = false;
            terse.Header.Zerocoded = true;
            OutPacket(terse, ThrottleOutPacketType.Task | ThrottleOutPacketType.LowPriority);
        }

        public void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, Vector3 position,
                                        Quaternion rotation, Vector3 velocity, Vector3 rotationalvelocity)
        {
            if (rotation.X == rotation.Y && rotation.Y == rotation.Z && rotation.Z == rotation.W && rotation.W == 0)
                rotation = Quaternion.Identity;
            ImprovedTerseObjectUpdatePacket terse = (ImprovedTerseObjectUpdatePacket)PacketPool.Instance.GetPacket(PacketType.ImprovedTerseObjectUpdate);
            // TODO: don't create new blocks if recycling an old packet
            terse.RegionData.RegionHandle = regionHandle;
            terse.RegionData.TimeDilation = timeDilation;
            terse.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
            terse.ObjectData[0] = CreatePrimImprovedBlock(localID, position, rotation, velocity, rotationalvelocity, 0);
            terse.Header.Reliable = false;
            terse.Header.Zerocoded = true;
            OutPacket(terse, ThrottleOutPacketType.Task | ThrottleOutPacketType.LowPriority);
        }

        public void SendAssetUploadCompleteMessage(sbyte AssetType, bool Success, UUID AssetFullID)
        {
            AssetUploadCompletePacket newPack = new AssetUploadCompletePacket();
            newPack.AssetBlock.Type = AssetType;
            newPack.AssetBlock.Success = Success;
            newPack.AssetBlock.UUID = AssetFullID;
            newPack.Header.Zerocoded = true;
            OutPacket(newPack, ThrottleOutPacketType.Asset);
        }

        public void SendXferRequest(ulong XferID, short AssetType, UUID vFileID, byte FilePath, byte[] FileName)
        {
            RequestXferPacket newPack = new RequestXferPacket();
            newPack.XferID.ID = XferID;
            newPack.XferID.VFileType = AssetType;
            newPack.XferID.VFileID = vFileID;
            newPack.XferID.FilePath = FilePath;
            newPack.XferID.Filename = FileName;
            newPack.Header.Zerocoded = true;
            OutPacket(newPack, ThrottleOutPacketType.Asset);
        }

        public void SendConfirmXfer(ulong xferID, uint PacketID)
        {
            ConfirmXferPacketPacket newPack = new ConfirmXferPacketPacket();
            newPack.XferID.ID = xferID;
            newPack.XferID.Packet = PacketID;
            newPack.Header.Zerocoded = true;
            OutPacket(newPack, ThrottleOutPacketType.Asset);
        }

        public void SendImagePart(ushort numParts, UUID ImageUUID, uint ImageSize, byte[] ImageData, byte imageCodec)
        {
            ImageDataPacket im = new ImageDataPacket();
            im.Header.Reliable = false;
            im.ImageID.Packets = numParts;
            im.ImageID.ID = ImageUUID;

            if (ImageSize > 0)
                im.ImageID.Size = ImageSize;

            im.ImageData.Data = ImageData;
            im.ImageID.Codec = imageCodec;
            im.Header.Zerocoded = true;
            OutPacket(im, ThrottleOutPacketType.Texture);
        }

        public void SendShutdownConnectionNotice()
        {
            OutPacket(PacketPool.Instance.GetPacket(PacketType.DisableSimulator), ThrottleOutPacketType.Unknown);
        }

        public void SendSimStats(Packet pack)
        {
            pack.Header.Reliable = false;
            OutPacket(pack, ThrottleOutPacketType.Task);
        }

        public void SendObjectPropertiesFamilyData(uint RequestFlags, UUID ObjectUUID, UUID OwnerID, UUID GroupID,
                                                    uint BaseMask, uint OwnerMask, uint GroupMask, uint EveryoneMask,
                                                    uint NextOwnerMask, int OwnershipCost, byte SaleType, int SalePrice, uint Category,
                                                    UUID LastOwnerID, string ObjectName, string Description)
        {
            ObjectPropertiesFamilyPacket objPropFamilyPack = (ObjectPropertiesFamilyPacket)PacketPool.Instance.GetPacket(PacketType.ObjectPropertiesFamily);
            // TODO: don't create new blocks if recycling an old packet

            ObjectPropertiesFamilyPacket.ObjectDataBlock objPropDB = new ObjectPropertiesFamilyPacket.ObjectDataBlock();
            objPropDB.RequestFlags = RequestFlags;
            objPropDB.ObjectID = ObjectUUID;
            objPropDB.OwnerID = OwnerID;
            objPropDB.GroupID = GroupID;
            objPropDB.BaseMask = BaseMask;
            objPropDB.OwnerMask = OwnerMask;
            objPropDB.GroupMask = GroupMask;
            objPropDB.EveryoneMask = EveryoneMask;
            objPropDB.NextOwnerMask = NextOwnerMask;

            // TODO: More properties are needed in SceneObjectPart!
            objPropDB.OwnershipCost = OwnershipCost;
            objPropDB.SaleType = SaleType;
            objPropDB.SalePrice = SalePrice;
            objPropDB.Category = Category;
            objPropDB.LastOwnerID = LastOwnerID;
            objPropDB.Name = Utils.StringToBytes(ObjectName);
            objPropDB.Description = Utils.StringToBytes(Description);
            objPropFamilyPack.ObjectData = objPropDB;
            objPropFamilyPack.Header.Zerocoded = true;
            OutPacket(objPropFamilyPack, ThrottleOutPacketType.Task);
        }

        public void SendObjectPropertiesReply(UUID ItemID, ulong CreationDate, UUID CreatorUUID, UUID FolderUUID, UUID FromTaskUUID,
                                              UUID GroupUUID, short InventorySerial, UUID LastOwnerUUID, UUID ObjectUUID,
                                              UUID OwnerUUID, string TouchTitle, byte[] TextureID, string SitTitle, string ItemName,
                                              string ItemDescription, uint OwnerMask, uint NextOwnerMask, uint GroupMask, uint EveryoneMask,
                                              uint BaseMask, byte saleType, int salePrice)
        {
            ObjectPropertiesPacket proper = (ObjectPropertiesPacket)PacketPool.Instance.GetPacket(PacketType.ObjectProperties);
            // TODO: don't create new blocks if recycling an old packet

            proper.ObjectData = new ObjectPropertiesPacket.ObjectDataBlock[1];
            proper.ObjectData[0] = new ObjectPropertiesPacket.ObjectDataBlock();
            proper.ObjectData[0].ItemID = ItemID;
            proper.ObjectData[0].CreationDate = CreationDate;
            proper.ObjectData[0].CreatorID = CreatorUUID;
            proper.ObjectData[0].FolderID = FolderUUID;
            proper.ObjectData[0].FromTaskID = FromTaskUUID;
            proper.ObjectData[0].GroupID = GroupUUID;
            proper.ObjectData[0].InventorySerial = InventorySerial;

            proper.ObjectData[0].LastOwnerID = LastOwnerUUID;
            //            proper.ObjectData[0].LastOwnerID = UUID.Zero;

            proper.ObjectData[0].ObjectID = ObjectUUID;
            proper.ObjectData[0].OwnerID = OwnerUUID;
            proper.ObjectData[0].TouchName = Utils.StringToBytes(TouchTitle);
            proper.ObjectData[0].TextureID = TextureID;
            proper.ObjectData[0].SitName = Utils.StringToBytes(SitTitle);
            proper.ObjectData[0].Name = Utils.StringToBytes(ItemName);
            proper.ObjectData[0].Description = Utils.StringToBytes(ItemDescription);
            proper.ObjectData[0].OwnerMask = OwnerMask;
            proper.ObjectData[0].NextOwnerMask = NextOwnerMask;
            proper.ObjectData[0].GroupMask = GroupMask;
            proper.ObjectData[0].EveryoneMask = EveryoneMask;
            proper.ObjectData[0].BaseMask = BaseMask;
            //            proper.ObjectData[0].AggregatePerms = 53;
            //            proper.ObjectData[0].AggregatePermTextures = 0;
            //            proper.ObjectData[0].AggregatePermTexturesOwner = 0;
            proper.ObjectData[0].SaleType = saleType;
            proper.ObjectData[0].SalePrice = salePrice;
            proper.Header.Zerocoded = true;
            OutPacket(proper, ThrottleOutPacketType.Task);
        }

        #endregion

        #region Estate Data Sending Methods

        private bool convertParamStringToBool(byte[] field)
        {
            string s = Utils.BytesToString(field);
            if (s == "1" || s.ToLower() == "y" || s.ToLower() == "yes" || s.ToLower() == "t" || s.ToLower() == "true")
            {
                return true;
            }
            return false;
        }

        public void SendEstateManagersList(UUID invoice, UUID[] EstateManagers, uint estateID)
        {
            EstateOwnerMessagePacket packet = new EstateOwnerMessagePacket();
            packet.AgentData.TransactionID = UUID.Random();
            packet.AgentData.AgentID = this.AgentId;
            packet.AgentData.SessionID = this.SessionId;
            packet.MethodData.Invoice = invoice;
            packet.MethodData.Method = Utils.StringToBytes("setaccess");

            EstateOwnerMessagePacket.ParamListBlock[] returnblock = new EstateOwnerMessagePacket.ParamListBlock[6 + EstateManagers.Length];

            for (int i = 0; i < (6 + EstateManagers.Length); i++)
            {
                returnblock[i] = new EstateOwnerMessagePacket.ParamListBlock();
            }
            int j = 0;

            returnblock[j].Parameter = Utils.StringToBytes(estateID.ToString()); j++;
            returnblock[j].Parameter = Utils.StringToBytes(((int)Constants.EstateAccessCodex.EstateManagers).ToString()); j++;
            returnblock[j].Parameter = Utils.StringToBytes("0"); j++;
            returnblock[j].Parameter = Utils.StringToBytes("0"); j++;
            returnblock[j].Parameter = Utils.StringToBytes("0"); j++;
            returnblock[j].Parameter = Utils.StringToBytes(EstateManagers.Length.ToString()); j++;
            for (int i = 0; i < EstateManagers.Length; i++)
            {
                returnblock[j].Parameter = EstateManagers[i].GetBytes(); j++;
            }
            packet.ParamList = returnblock;
            packet.Header.Reliable = false;
            this.OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendBannedUserList(UUID invoice, EstateBan[] bl, uint estateID)
        {
            List<UUID>BannedUsers = new List<UUID>();

            for (int i = 0; i < bl.Length; i++)
            {
                if (bl[i] == null)
                    continue;
                if (bl[i].bannedUUID == UUID.Zero)
                    continue;
                BannedUsers.Add(bl[i].bannedUUID);
            }

            EstateOwnerMessagePacket packet = new EstateOwnerMessagePacket();
            packet.AgentData.TransactionID = UUID.Random();
            packet.AgentData.AgentID = this.AgentId;
            packet.AgentData.SessionID = this.SessionId;
            packet.MethodData.Invoice = invoice;
            packet.MethodData.Method = Utils.StringToBytes("setaccess");

            EstateOwnerMessagePacket.ParamListBlock[] returnblock = new EstateOwnerMessagePacket.ParamListBlock[6 + BannedUsers.Count];

            for (int i = 0; i < (6 + BannedUsers.Count); i++)
            {
                returnblock[i] = new EstateOwnerMessagePacket.ParamListBlock();
            }
            int j = 0;

            returnblock[j].Parameter = Utils.StringToBytes(estateID.ToString()); j++;
            returnblock[j].Parameter = Utils.StringToBytes(((int)Constants.EstateAccessCodex.EstateBans).ToString()); j++;
            returnblock[j].Parameter = Utils.StringToBytes("0"); j++;
            returnblock[j].Parameter = Utils.StringToBytes("0"); j++;
            returnblock[j].Parameter = Utils.StringToBytes(BannedUsers.Count.ToString()); j++;
            returnblock[j].Parameter = Utils.StringToBytes("0"); j++;

            foreach (UUID banned in BannedUsers)
            {
                returnblock[j].Parameter = banned.GetBytes(); j++;
            }
            packet.ParamList = returnblock;
            packet.Header.Reliable = false;
            this.OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendRegionInfoToEstateMenu(RegionInfoForEstateMenuArgs args)
        {
            RegionInfoPacket rinfopack = new RegionInfoPacket();
            RegionInfoPacket.RegionInfoBlock rinfoblk = new RegionInfoPacket.RegionInfoBlock();
            rinfopack.AgentData.AgentID = this.AgentId;
            rinfopack.AgentData.SessionID = this.SessionId;
            rinfoblk.BillableFactor = args.billableFactor;
            rinfoblk.EstateID = args.estateID;
            rinfoblk.MaxAgents = args.maxAgents;
            rinfoblk.ObjectBonusFactor = args.objectBonusFactor;
            rinfoblk.ParentEstateID = args.parentEstateID;
            rinfoblk.PricePerMeter = args.pricePerMeter;
            rinfoblk.RedirectGridX = args.redirectGridX;
            rinfoblk.RedirectGridY = args.redirectGridY;
            rinfoblk.RegionFlags = args.regionFlags;
            rinfoblk.SimAccess = args.simAccess;
            rinfoblk.SunHour = args.sunHour;
            rinfoblk.TerrainLowerLimit = args.terrainLowerLimit;
            rinfoblk.TerrainRaiseLimit = args.terrainRaiseLimit;
            rinfoblk.UseEstateSun = args.useEstateSun;
            rinfoblk.WaterHeight = args.waterHeight;
            rinfoblk.SimName = Utils.StringToBytes(args.simName);

            rinfopack.RegionInfo = rinfoblk;

            this.OutPacket(rinfopack, ThrottleOutPacketType.Task);
        }

        public void SendEstateCovenantInformation(UUID covenant)
        {
            EstateCovenantReplyPacket einfopack = new EstateCovenantReplyPacket();
            EstateCovenantReplyPacket.DataBlock edata = new EstateCovenantReplyPacket.DataBlock();
            edata.CovenantID = covenant;
            edata.CovenantTimestamp = 0;
            if (m_scene.RegionInfo.EstateSettings.EstateOwner != UUID.Zero)
                edata.EstateOwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;
            else
                edata.EstateOwnerID = m_scene.RegionInfo.MasterAvatarAssignedUUID;
            edata.EstateName = Utils.StringToBytes(m_scene.RegionInfo.EstateSettings.EstateName);
            einfopack.Data = edata;
            this.OutPacket(einfopack, ThrottleOutPacketType.Task);
        }

        public void SendDetailedEstateData(UUID invoice, string estateName, uint estateID, uint parentEstate, uint estateFlags, uint sunPosition, UUID covenant, string abuseEmail)
        {
            EstateOwnerMessagePacket packet = new EstateOwnerMessagePacket();
            packet.MethodData.Invoice = invoice;
            packet.AgentData.TransactionID = UUID.Random();
            packet.MethodData.Method = Utils.StringToBytes("estateupdateinfo");
            EstateOwnerMessagePacket.ParamListBlock[] returnblock = new EstateOwnerMessagePacket.ParamListBlock[10];

            for (int i = 0; i < 10; i++)
            {
                returnblock[i] = new EstateOwnerMessagePacket.ParamListBlock();
            }

            //Sending Estate Settings
            returnblock[0].Parameter = Utils.StringToBytes(estateName);
            // TODO: remove this cruft once MasterAvatar is fully deprecated
            //
            if (m_scene.RegionInfo.EstateSettings.EstateOwner != UUID.Zero)
                returnblock[1].Parameter = Utils.StringToBytes(m_scene.RegionInfo.EstateSettings.EstateOwner.ToString());
            else
                returnblock[1].Parameter = Utils.StringToBytes(m_scene.RegionInfo.MasterAvatarAssignedUUID.ToString());
            returnblock[2].Parameter = Utils.StringToBytes(estateID.ToString());

            returnblock[3].Parameter = Utils.StringToBytes(estateFlags.ToString());
            returnblock[4].Parameter = Utils.StringToBytes(sunPosition.ToString());
            returnblock[5].Parameter = Utils.StringToBytes(parentEstate.ToString());
            returnblock[6].Parameter = Utils.StringToBytes(covenant.ToString());
            returnblock[7].Parameter = Utils.StringToBytes("1160895077"); // what is this?
            returnblock[8].Parameter = Utils.StringToBytes("1"); // what is this?
            returnblock[9].Parameter = Utils.StringToBytes(abuseEmail);

            packet.ParamList = returnblock;
            packet.Header.Reliable = false;
            //System.Console.WriteLine("[ESTATE]: SIM--->" + packet.ToString());
            this.OutPacket(packet, ThrottleOutPacketType.Task);
        }

        #endregion

        #region Land Data Sending Methods

        public void SendLandParcelOverlay(byte[] data, int sequence_id)
        {

            ParcelOverlayPacket packet;
            packet = (ParcelOverlayPacket)PacketPool.Instance.GetPacket(PacketType.ParcelOverlay);
            packet.ParcelData.Data = data;
            packet.ParcelData.SequenceID = sequence_id;
            packet.Header.Zerocoded = true;
            this.OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendLandProperties(IClientAPI remote_client, int sequence_id, bool snap_selection, int request_result, LandData landData, float simObjectBonusFactor, int parcelObjectCapacity, int simObjectCapacity, uint regionFlags)
        {
            ParcelPropertiesPacket updatePacket = (ParcelPropertiesPacket)PacketPool.Instance.GetPacket(PacketType.ParcelProperties);
            // TODO: don't create new blocks if recycling an old packet

            updatePacket.ParcelData.AABBMax = landData.AABBMax;
            updatePacket.ParcelData.AABBMin = landData.AABBMin;
            updatePacket.ParcelData.Area = landData.Area;
            updatePacket.ParcelData.AuctionID = landData.AuctionID;
            updatePacket.ParcelData.AuthBuyerID = landData.AuthBuyerID; //unemplemented

            updatePacket.ParcelData.Bitmap = landData.Bitmap;

            updatePacket.ParcelData.Desc = Utils.StringToBytes(landData.Description);
            updatePacket.ParcelData.Category = (byte)landData.Category;
            updatePacket.ParcelData.ClaimDate = landData.ClaimDate;
            updatePacket.ParcelData.ClaimPrice = landData.ClaimPrice;
            updatePacket.ParcelData.GroupID = landData.GroupID;
            updatePacket.ParcelData.GroupPrims = landData.GroupPrims;
            updatePacket.ParcelData.IsGroupOwned = landData.IsGroupOwned;
            updatePacket.ParcelData.LandingType = (byte)landData.LandingType;
            updatePacket.ParcelData.LocalID = landData.LocalID;
            if (landData.Area > 0)
            {
                updatePacket.ParcelData.MaxPrims = parcelObjectCapacity;
            }
            else
            {
                updatePacket.ParcelData.MaxPrims = 0;
            }
            updatePacket.ParcelData.MediaAutoScale = landData.MediaAutoScale;
            updatePacket.ParcelData.MediaID = landData.MediaID;
            updatePacket.ParcelData.MediaURL = Utils.StringToBytes(landData.MediaURL);
            updatePacket.ParcelData.MusicURL = Utils.StringToBytes(landData.MusicURL);
            updatePacket.ParcelData.Name = Utils.StringToBytes(landData.Name);
            updatePacket.ParcelData.OtherCleanTime = 0; //unemplemented
            updatePacket.ParcelData.OtherCount = 0; //unemplemented
            updatePacket.ParcelData.OtherPrims = landData.OtherPrims;
            updatePacket.ParcelData.OwnerID = landData.OwnerID;
            updatePacket.ParcelData.OwnerPrims = landData.OwnerPrims;
            updatePacket.ParcelData.ParcelFlags = landData.Flags;
            updatePacket.ParcelData.ParcelPrimBonus = simObjectBonusFactor;
            updatePacket.ParcelData.PassHours = landData.PassHours;
            updatePacket.ParcelData.PassPrice = landData.PassPrice;
            updatePacket.ParcelData.PublicCount = 0; //unemplemented

            updatePacket.ParcelData.RegionDenyAnonymous = ((regionFlags & (uint)Simulator.RegionFlags.DenyAnonymous) >
                                                           0);
            updatePacket.ParcelData.RegionDenyIdentified = ((regionFlags & (uint)Simulator.RegionFlags.DenyIdentified) >
                                                            0);
            updatePacket.ParcelData.RegionDenyTransacted = ((regionFlags & (uint)Simulator.RegionFlags.DenyTransacted) >
                                                            0);
            updatePacket.ParcelData.RegionPushOverride = ((regionFlags & (uint)Simulator.RegionFlags.RestrictPushObject) >
                                                          0);

            updatePacket.ParcelData.RentPrice = 0;
            updatePacket.ParcelData.RequestResult = request_result;
            updatePacket.ParcelData.SalePrice = landData.SalePrice;
            updatePacket.ParcelData.SelectedPrims = landData.SelectedPrims;
            updatePacket.ParcelData.SelfCount = 0; //unemplemented
            updatePacket.ParcelData.SequenceID = sequence_id;
            if (landData.SimwideArea > 0)
            {
                updatePacket.ParcelData.SimWideMaxPrims = parcelObjectCapacity;
            }
            else
            {
                updatePacket.ParcelData.SimWideMaxPrims = 0;
            }
            updatePacket.ParcelData.SimWideTotalPrims = landData.SimwidePrims;
            updatePacket.ParcelData.SnapSelection = snap_selection;
            updatePacket.ParcelData.SnapshotID = landData.SnapshotID;
            updatePacket.ParcelData.Status = (byte)landData.Status;
            updatePacket.ParcelData.TotalPrims = landData.OwnerPrims + landData.GroupPrims + landData.OtherPrims +
                                                 landData.SelectedPrims;
            updatePacket.ParcelData.UserLocation = landData.UserLocation;
            updatePacket.ParcelData.UserLookAt = landData.UserLookAt;
            updatePacket.Header.Zerocoded = true;
            remote_client.OutPacket((Packet)updatePacket, ThrottleOutPacketType.Task);
        }

        public void SendLandAccessListData(List<UUID> avatars, uint accessFlag, int localLandID)
        {
            ParcelAccessListReplyPacket replyPacket = (ParcelAccessListReplyPacket)PacketPool.Instance.GetPacket(PacketType.ParcelAccessListReply);
            replyPacket.Data.AgentID = this.AgentId;
            replyPacket.Data.Flags = accessFlag;
            replyPacket.Data.LocalID = localLandID;
            replyPacket.Data.SequenceID = 0;

            List<ParcelAccessListReplyPacket.ListBlock> list = new List<ParcelAccessListReplyPacket.ListBlock>();
            foreach (UUID avatar in avatars)
            {
                ParcelAccessListReplyPacket.ListBlock block = new ParcelAccessListReplyPacket.ListBlock();
                block.Flags = accessFlag;
                block.ID = avatar;
                block.Time = 0;
            }

            replyPacket.List = list.ToArray();
            replyPacket.Header.Zerocoded = true;
            this.OutPacket((Packet)replyPacket, ThrottleOutPacketType.Task);
        }

        public void SendForceClientSelectObjects(List<uint> ObjectIDs)
        {
            bool firstCall = true;
            int MAX_OBJECTS_PER_PACKET = 251;
            ForceObjectSelectPacket pack = (ForceObjectSelectPacket)PacketPool.Instance.GetPacket(PacketType.ForceObjectSelect);
            ForceObjectSelectPacket.DataBlock[] data;
            while (ObjectIDs.Count > 0)
            {
                if (firstCall)
                {
                    pack._Header.ResetList = true;
                    firstCall = false;
                }
                else
                {
                    pack._Header.ResetList = false;
                }

                if (ObjectIDs.Count > MAX_OBJECTS_PER_PACKET)
                {
                    data = new ForceObjectSelectPacket.DataBlock[MAX_OBJECTS_PER_PACKET];
                }
                else
                {
                    data = new ForceObjectSelectPacket.DataBlock[ObjectIDs.Count];
                }

                int i;
                for (i = 0; i < MAX_OBJECTS_PER_PACKET && ObjectIDs.Count > 0; i++)
                {
                    data[i] = new ForceObjectSelectPacket.DataBlock();
                    data[i].LocalID = Convert.ToUInt32(ObjectIDs[0]);
                    ObjectIDs.RemoveAt(0);
                }
                pack.Data = data;
                pack.Header.Zerocoded = true;
                this.OutPacket((Packet)pack, ThrottleOutPacketType.Task);
            }
        }

        public void SendLandObjectOwners(Dictionary<UUID, int> ownersAndCount)
        {
            int notifyCount = ownersAndCount.Count;
            ParcelObjectOwnersReplyPacket pack = (ParcelObjectOwnersReplyPacket)PacketPool.Instance.GetPacket(PacketType.ParcelObjectOwnersReply);

            if (notifyCount > 0)
            {
                if (notifyCount > 32)
                {
                    m_log.InfoFormat(
                        "[LAND]: More than {0} avatars own prims on this parcel.  Only sending back details of first {0}"
                        + " - a developer might want to investigate whether this is a hard limit", 32);

                    notifyCount = 32;
                }

                ParcelObjectOwnersReplyPacket.DataBlock[] dataBlock
                    = new ParcelObjectOwnersReplyPacket.DataBlock[notifyCount];

                int num = 0;
                foreach (UUID owner in ownersAndCount.Keys)
                {
                    dataBlock[num] = new ParcelObjectOwnersReplyPacket.DataBlock();
                    dataBlock[num].Count = ownersAndCount[owner];
                    dataBlock[num].IsGroupOwned = false; //TODO: fix me when group support is added
                    dataBlock[num].OnlineStatus = true; //TODO: fix me later
                    dataBlock[num].OwnerID = owner;

                    num++;

                    if (num >= notifyCount)
                    {
                        break;
                    }
                }

                pack.Data = dataBlock;
            }
            pack.Header.Zerocoded = true;
            this.OutPacket(pack, ThrottleOutPacketType.Task);
        }

        #endregion

        #region Helper Methods

        protected ImprovedTerseObjectUpdatePacket.ObjectDataBlock CreateAvatarImprovedBlock(uint localID, Vector3 pos,
                                                                                            Vector3 velocity,
                                                                                            Quaternion rotation)
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
            internDirec.X += 1;
            internDirec.Y += 1;
            internDirec.Z += 1;

            InternVelocityX = (ushort)(32768 * internDirec.X);
            InternVelocityY = (ushort)(32768 * internDirec.Y);
            InternVelocityZ = (ushort)(32768 * internDirec.Z);

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
                                                                                          Vector3 position,
                                                                                          Quaternion rotation,
                                                                                          Vector3 velocity,
                                                                                          Vector3 rotationalvelocity,
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
            vel.X += 1;
            vel.Y += 1;
            vel.Z += 1;
            //vel
            velx = (ushort)(32768 * (vel.X));
            vely = (ushort)(32768 * (vel.Y));
            velz = (ushort)(32768 * (vel.Z));

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
            rvel.X += 1;
            rvel.Y += 1;
            rvel.Z += 1;
            //vel
            rvelx = (ushort)(32768 * (rvel.X));
            rvely = (ushort)(32768 * (rvel.Y));
            rvelz = (ushort)(32768 * (rvel.Z));

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
            objdata.JointAxisOrAnchor = new Vector3(0, 0, 0);
            objdata.JointPivot = new Vector3(0, 0, 0);
            objdata.Material = 3;
            objdata.TextureAnim = new byte[0];
            objdata.Sound = UUID.Zero;
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
            //  new OpenMetaverse.Packets.ObjectUpdatePacket.ObjectDataBlock(data1, ref i);

            SetDefaultAvatarPacketValues(ref objdata);
            objdata.UpdateFlags = 61 + (9 << 8) + (130 << 16) + (16 << 24);
            objdata.PathCurve = 16;
            objdata.ProfileCurve = 1;
            objdata.PathScaleX = 100;
            objdata.PathScaleY = 100;
            objdata.ParentID = 0;
            objdata.OwnerID = UUID.Zero;
            objdata.Scale = new Vector3(1, 1, 1);
            objdata.PCode = (byte)PCode.Avatar;
            if (textureEntry != null)
            {
                objdata.TextureEntry = textureEntry;
            }
            Vector3 pos = new Vector3(objdata.ObjectData, 16);
            pos.X = 100f;
            objdata.ID = 8880000;
            objdata.NameValue = Utils.StringToBytes("FirstName STRING RW SV Test \nLastName STRING RW SV User ");
            //Vector3 pos2 = new Vector3(100f, 100f, 23f);
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
            objdata.JointAxisOrAnchor = new Vector3(0, 0, 0);
            objdata.JointPivot = new Vector3(0, 0, 0);
            objdata.Material = 4;
            objdata.TextureAnim = new byte[0];
            objdata.Sound = UUID.Zero;
            Primitive.TextureEntry ntex = new Primitive.TextureEntry(new UUID("00000000-0000-0000-5005-000000000005"));
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

        public void SendNameReply(UUID profileId, string firstname, string lastname)
        {
            UUIDNameReplyPacket packet = (UUIDNameReplyPacket)PacketPool.Instance.GetPacket(PacketType.UUIDNameReply);
            // TODO: don't create new blocks if recycling an old packet
            packet.UUIDNameBlock = new UUIDNameReplyPacket.UUIDNameBlockBlock[1];
            packet.UUIDNameBlock[0] = new UUIDNameReplyPacket.UUIDNameBlockBlock();
            packet.UUIDNameBlock[0].ID = profileId;
            packet.UUIDNameBlock[0].FirstName = Utils.StringToBytes(firstname);
            packet.UUIDNameBlock[0].LastName = Utils.StringToBytes(lastname);

            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        #endregion

        /// <summary>
        /// This is a different way of processing packets then ProcessInPacket
        /// </summary>
        protected virtual void RegisterLocalPacketHandlers()
        {
            AddLocalPacketHandler(PacketType.LogoutRequest, Logout);
            AddLocalPacketHandler(PacketType.ViewerEffect, HandleViewerEffect);
            AddLocalPacketHandler(PacketType.AgentCachedTexture, AgentTextureCached);
            AddLocalPacketHandler(PacketType.MultipleObjectUpdate, MultipleObjUpdate);
            AddLocalPacketHandler(PacketType.MoneyTransferRequest, HandleMoneyTransferRequest);
            AddLocalPacketHandler(PacketType.ParcelBuy, HandleParcelBuyRequest);
            AddLocalPacketHandler(PacketType.UUIDGroupNameRequest, HandleUUIDGroupNameRequest);
            AddLocalPacketHandler(PacketType.ObjectGroup, HandleObjectGroupRequest);
            AddLocalPacketHandler(PacketType.GenericMessage, HandleGenericMessage);
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

        private bool HandleParcelBuyRequest(IClientAPI sender, Packet Pack)
        {
            ParcelBuyPacket parcel = (ParcelBuyPacket)Pack;
            if (parcel.AgentData.AgentID == AgentId && parcel.AgentData.SessionID == this.SessionId)
            {
                handlerParcelBuy = OnParcelBuy;
                if (handlerParcelBuy != null)
                {
                    handlerParcelBuy(parcel.AgentData.AgentID, parcel.Data.GroupID, parcel.Data.Final, parcel.Data.IsGroupOwned,
                                     parcel.Data.RemoveContribution, parcel.Data.LocalID, parcel.ParcelData.Area, parcel.ParcelData.Price,
                                     false);
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool HandleUUIDGroupNameRequest(IClientAPI sender, Packet Pack)
        {
            UUIDGroupNameRequestPacket upack = (UUIDGroupNameRequestPacket)Pack;

            for (int i = 0; i < upack.UUIDNameBlock.Length; i++)
            {
                handlerUUIDGroupNameRequest = OnUUIDGroupNameRequest;
                if (handlerUUIDGroupNameRequest != null)
                {
                    handlerUUIDGroupNameRequest(upack.UUIDNameBlock[i].ID, this);
                }
            }

            return true;
        }

        public bool HandleGenericMessage(IClientAPI sender, Packet pack)
        {
            GenericMessagePacket gmpack = (GenericMessagePacket) pack;
            handlerGenericMessage = OnGenericMessage;

            List<string> msg = new List<string>();

            if (handlerGenericMessage != null)
            {
                string method = Util.FieldToString(gmpack.MethodData.Method);
                foreach (GenericMessagePacket.ParamListBlock block in gmpack.ParamList)
                {
                    msg.Add(Util.FieldToString(block.Parameter));
                }

                handlerGenericMessage(this, method, msg);
            }
            return true;
        }

        public bool HandleObjectGroupRequest(IClientAPI sender, Packet Pack)
        {

            ObjectGroupPacket ogpack = (ObjectGroupPacket)Pack;
            handlerObjectGroupRequest = OnObjectGroupRequest;
            if (handlerObjectGroupRequest != null)
            {
                for (int i = 0; i < ogpack.ObjectData.Length; i++)
                {
                    handlerObjectGroupRequest(this, ogpack.AgentData.GroupID, ogpack.ObjectData[i].ObjectLocalID, UUID.Zero);
                }
            }
            return true;
        }

        private bool HandleViewerEffect(IClientAPI sender, Packet Pack)
        {
            ViewerEffectPacket viewer = (ViewerEffectPacket)Pack;
            handlerViewerEffect = OnViewerEffect;
            if (handlerViewerEffect != null)
            {
                int length = viewer.Effect.Length;
                List<ViewerEffectEventHandlerArg> args = new List<ViewerEffectEventHandlerArg>(length);
                for (int i = 0; i < length; i++)
                {
                    //copy the effects block arguments into the event handler arg.
                    ViewerEffectEventHandlerArg argument = new ViewerEffectEventHandlerArg();
                    argument.AgentID = viewer.Effect[i].AgentID;
                    argument.Color = viewer.Effect[i].Color;
                    argument.Duration = viewer.Effect[i].Duration;
                    argument.ID = viewer.Effect[i].ID;
                    argument.Type = viewer.Effect[i].Type;
                    argument.TypeData = viewer.Effect[i].TypeData;
                    args.Add(argument);
                }

                handlerViewerEffect(sender, args);
            }

            return true;
        }

        public void SendScriptQuestion(UUID taskID, string taskName, string ownerName, UUID itemID, int question)
        {
            ScriptQuestionPacket scriptQuestion = (ScriptQuestionPacket)PacketPool.Instance.GetPacket(PacketType.ScriptQuestion);
            scriptQuestion.Data = new ScriptQuestionPacket.DataBlock();
            // TODO: don't create new blocks if recycling an old packet
            scriptQuestion.Data.TaskID = taskID;
            scriptQuestion.Data.ItemID = itemID;
            scriptQuestion.Data.Questions = question;
            scriptQuestion.Data.ObjectName = Utils.StringToBytes(taskName);
            scriptQuestion.Data.ObjectOwner = Utils.StringToBytes(ownerName);

            OutPacket(scriptQuestion, ThrottleOutPacketType.Task);
        }

        private void InitDefaultAnimations()
        {
            using (XmlTextReader reader = new XmlTextReader("data/avataranimations.xml"))
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(reader);
                foreach (XmlNode nod in doc.DocumentElement.ChildNodes)
                {
                    if (nod.Attributes["name"] != null)
                    {
                        string name = (string)nod.Attributes["name"].Value.ToLower();
                        string id = (string)nod.InnerText;
                        m_defaultAnimations.Add(name, id);
                    }
                }
            }
        }

        public UUID GetDefaultAnimation(string name)
        {
            if (m_defaultAnimations.ContainsKey(name))
                return m_defaultAnimations[name];
            return UUID.Zero;
        }

        /// <summary>
        /// Handler called when we receive a logout packet.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="packet"></param>
        /// <returns></returns>
        protected virtual bool Logout(IClientAPI client, Packet packet)
        {
            return Logout(client);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        protected virtual bool Logout(IClientAPI client)
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
            //Console.WriteLine("texture cached: " + packet.ToString());
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
                cachedresp.WearableData[i].TextureID = UUID.Zero;
                cachedresp.WearableData[i].HostName = new byte[0];
            }

            // Temporarily throw these packets on to the wind queue, so we can identify whether these
            // are somehow the source of the packet bloat.
            cachedresp.Header.Zerocoded = true;
            OutPacket(cachedresp, ThrottleOutPacketType.Wind);
            return true;
        }

        protected bool MultipleObjUpdate(IClientAPI simClient, Packet packet)
        {
            MultipleObjectUpdatePacket multipleupdate = (MultipleObjectUpdatePacket)packet;
            // Console.WriteLine("new multi update packet " + multipleupdate.ToString());
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
                        simClient.SendKiPrimitive(Scene.RegionInfo.RegionHandle,
                                                 localId);
                    }
                    else
                    {
                        // UUID partId = part.UUID;
                        UpdatePrimRotation handlerUpdatePrimRotation = OnUpdatePrimGroupRotation;
                        UpdatePrimGroupRotation handlerUpdatePrimGroupRotation = OnUpdatePrimGroupMouseRotation;

                        switch (block.Type)
                        {
                            case 1:
                                Vector3 pos1 = new Vector3(block.Data, 0);

                                handlerUpdatePrimSinglePosition = OnUpdatePrimSinglePosition;
                                if (handlerUpdatePrimSinglePosition != null)
                                {
                                    // Console.WriteLine("new movement position is " + pos.X + " , " + pos.Y + " , " + pos.Z);
                                    handlerUpdatePrimSinglePosition(localId, pos1, this);
                                }
                                break;
                            case 2:
                                Quaternion rot1 = new Quaternion(block.Data, 0, true);

                                handlerUpdatePrimSingleRotation = OnUpdatePrimSingleRotation;
                                if (handlerUpdatePrimSingleRotation != null)
                                {
                                    //Console.WriteLine("new tab rotation is " + rot.X + " , " + rot.Y + " , " + rot.Z + " , " + rot.W);
                                    handlerUpdatePrimSingleRotation(localId, rot1, this);
                                }
                                break;
                            case 3:

                                Quaternion rot2 = new Quaternion(block.Data, 12, true);
                                handlerUpdatePrimSingleRotation = OnUpdatePrimSingleRotation;
                                if (handlerUpdatePrimSingleRotation != null)
                                {
                                    //Console.WriteLine("new mouse rotation is " + rot.X + " , " + rot.Y + " , " + rot.Z + " , " + rot.W);
                                    handlerUpdatePrimSingleRotation(localId, rot2, this);
                                }
                                break;
                            case 5:

                                Vector3 scale1 = new Vector3(block.Data, 12);
                                Vector3 pos11 = new Vector3(block.Data, 0);

                                handlerUpdatePrimScale = OnUpdatePrimScale;
                                if (handlerUpdatePrimScale != null)
                                {
                                    // Console.WriteLine("new scale is " + scale.X + " , " + scale.Y + " , " + scale.Z);
                                    handlerUpdatePrimScale(localId, scale1, this);

                                    handlerUpdatePrimSinglePosition = OnUpdatePrimSinglePosition;
                                    if (handlerUpdatePrimSinglePosition != null)
                                    {
                                        handlerUpdatePrimSinglePosition(localId, pos11, this);
                                    }
                                }
                                break;
                            case 9:
                                Vector3 pos2 = new Vector3(block.Data, 0);

                                handlerUpdateVector = OnUpdatePrimGroupPosition;

                                if (handlerUpdateVector != null)
                                {

                                    handlerUpdateVector(localId, pos2, this);
                                }
                                break;
                            case 10:
                                Quaternion rot3 = new Quaternion(block.Data, 0, true);

                                handlerUpdatePrimRotation = OnUpdatePrimGroupRotation;
                                if (handlerUpdatePrimRotation != null)
                                {
                                    //  Console.WriteLine("new rotation is " + rot.X + " , " + rot.Y + " , " + rot.Z + " , " + rot.W);
                                    handlerUpdatePrimRotation(localId, rot3, this);
                                }
                                break;
                            case 11:
                                Vector3 pos3 = new Vector3(block.Data, 0);
                                Quaternion rot4 = new Quaternion(block.Data, 12, true);

                                handlerUpdatePrimGroupRotation = OnUpdatePrimGroupMouseRotation;
                                if (handlerUpdatePrimGroupRotation != null)
                                {
                                    //Console.WriteLine("new rotation position is " + pos.X + " , " + pos.Y + " , " + pos.Z);
                                    // Console.WriteLine("new rotation is " + rot.X + " , " + rot.Y + " , " + rot.Z + " , " + rot.W);
                                    handlerUpdatePrimGroupRotation(localId, pos3, rot4, this);
                                }
                                break;
                            case 13:
                                Vector3 scale2 = new Vector3(block.Data, 12);
                                Vector3 pos4 = new Vector3(block.Data, 0);

                                handlerUpdatePrimScale = OnUpdatePrimScale;
                                if (handlerUpdatePrimScale != null)
                                {
                                    //Console.WriteLine("new scale is " + scale.X + " , " + scale.Y + " , " + scale.Z);
                                    handlerUpdatePrimScale(localId, scale2, this);

                                    // Change the position based on scale (for bug number 246)
                                    handlerUpdatePrimSinglePosition = OnUpdatePrimSinglePosition;
                                    // Console.WriteLine("new movement position is " + pos.X + " , " + pos.Y + " , " + pos.Z);
                                    if (handlerUpdatePrimSinglePosition != null)
                                    {
                                        handlerUpdatePrimSinglePosition(localId, pos4, this);
                                    }
                                }
                                break;
                            case 29:
                                Vector3 scale5 = new Vector3(block.Data, 12);
                                Vector3 pos5 = new Vector3(block.Data, 0);

                                handlerUpdatePrimGroupScale = OnUpdatePrimGroupScale;
                                if (handlerUpdatePrimGroupScale != null)
                                {
                                    // Console.WriteLine("new scale is " + scale.X + " , " + scale.Y + " , " + scale.Z);
                                    handlerUpdatePrimGroupScale(localId, scale5, this);
                                    handlerUpdateVector = OnUpdatePrimGroupPosition;

                                    if (handlerUpdateVector != null)
                                    {
                                        handlerUpdateVector(localId, pos5, this);
                                    }
                                }
                                break;
                            case 21:
                                Vector3 scale6 = new Vector3(block.Data, 12);
                                Vector3 pos6 = new Vector3(block.Data, 0);

                                handlerUpdatePrimScale = OnUpdatePrimScale;
                                if (handlerUpdatePrimScale != null)
                                {
                                    // Console.WriteLine("new scale is " + scale.X + " , " + scale.Y + " , " + scale.Z);
                                    handlerUpdatePrimScale(localId, scale6, this);
                                    handlerUpdatePrimSinglePosition = OnUpdatePrimSinglePosition;
                                    if (handlerUpdatePrimSinglePosition != null)
                                    {
                                        handlerUpdatePrimSinglePosition(localId, pos6, this);
                                    }
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
            mapReply.LayerData[0].ImageID = new UUID("00000000-0000-1111-9999-000000000006");
            mapReply.Header.Zerocoded = true;
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
                mbReply.Data[iii].MapImageID = new UUID((string)mp["map-image-id"]);
                mbReply.Data[iii].RegionFlags = System.Convert.ToUInt32(mp["region-flags"]);
                mbReply.Data[iii].WaterHeight = System.Convert.ToByte(mp["water-height"]);
                mbReply.Data[iii].X = System.Convert.ToUInt16(mp["x"]);
                mbReply.Data[iii].Y = System.Convert.ToUInt16(mp["y"]);
            }
            this.OutPacket(mbReply, ThrottleOutPacketType.Land);
             */
        }

        /// <summary>
        /// returns a byte array of the client set throttles Gets multiplied by the multiplier
        ///
        /// </summary>
        /// <param name="multiplier">non 1 multiplier for subdividing the throttles between individual regions</param>
        /// <returns></returns>
        public byte[] GetThrottlesPacked(float multiplier)
        {
            return m_PacketHandler.PacketQueue.GetThrottlesPacked(multiplier);
        }
        /// <summary>
        /// sets the throttles from values supplied by the client
        /// </summary>
        /// <param name="throttles"></param>
        public void SetChildAgentThrottle(byte[] throttles)
        {
            m_PacketHandler.PacketQueue.SetThrottleFromClient(throttles);
        }

        /// <summary>
        /// Method gets called when a new packet has arrived from the UDP
        /// server. This happens after it's been decoded into a libsl object.
        /// </summary>
        /// <param name="NewPack">object containing the packet.</param>
        public virtual void InPacket(object NewPack)
        {
            // Cast NewPack to Packet.
            m_PacketHandler.InPacket((Packet) NewPack);
        }


        /// <summary>
        /// The dreaded OutPacket. This should only be called from within
        /// the ClientStack itself right now
        /// This is the entry point for simulator packets to go out to
        /// the client.
        /// </summary>
        /// <param name="NewPack"></param>
        /// <param name="throttlePacketType">Corresponds to the type of data that is going out.  Enum</param>
        public virtual void OutPacket(Packet NewPack, ThrottleOutPacketType throttlePacketType)
        {
            m_PacketHandler.OutPacket(NewPack, throttlePacketType);
        }

        public bool AddMoney(int debit)
        {
            if (m_moneyBalance + debit >= 0)
            {
                m_moneyBalance += debit;
                SendMoneyBalance(UUID.Zero, true, Utils.StringToBytes("Poof Poof!"), m_moneyBalance);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Breaks down the genericMessagePacket into specific events
        /// </summary>
        /// <param name="gmMethod"></param>
        /// <param name="gmInvoice"></param>
        /// <param name="gmParams"></param>
        public void DecipherGenericMessage(string gmMethod, UUID gmInvoice, GenericMessagePacket.ParamListBlock[] gmParams)
        {
            switch (gmMethod)
            {
                case "autopilot":
                    float locx = 0f;
                    float locy = 0f;
                    float locz = 0f;
                    uint regionX = 0;
                    uint regionY = 0;
                    try
                    {
                        Helpers.LongToUInts(Scene.RegionInfo.RegionHandle, out regionX, out regionY);
                        locx = Convert.ToSingle(Utils.BytesToString(gmParams[0].Parameter)) - (float)regionX;
                        locy = Convert.ToSingle(Utils.BytesToString(gmParams[1].Parameter)) - (float)regionY;
                        locz = Convert.ToSingle(Utils.BytesToString(gmParams[2].Parameter));
                    }
                    catch (InvalidCastException)
                    {
                        m_log.Error("[CLIENT]: Invalid autopilot request");
                        return;
                    }

                    handlerAutoPilotGo = OnAutoPilotGo;
                    if (handlerAutoPilotGo != null)
                    {
                        handlerAutoPilotGo(0, new Vector3(locx, locy, locz), this);
                    }
                    m_log.InfoFormat("[CLIENT]: Client Requests autopilot to position <{0},{1},{2}>", locx, locy, locz);


                    break;
                default:
                    m_log.Debug("[CLIENT]: Unknown Generic Message, Method: " + gmMethod + ". Invoice: " + gmInvoice.ToString() + ".  Dumping Params:");
                    for (int hi = 0; hi < gmParams.Length; hi++)
                    {
                        System.Console.WriteLine(gmParams[hi].ToString());
                    }
                    //gmpack.MethodData.
                    break;

            }
        }

        /// <summary>
        /// Entryway from the client to the simulator
        /// all UDP packets from the client will end up here
        /// </summary>
        /// <param name="Pack">OpenMetaverse.packet</param>
        public void ProcessInPacket(Packet Pack)
        {
            // check if we've got a local packet handler for this packet.type.   See RegisterLocalPacketHandlers()
            if (ProcessPacketMethod(Pack))
            {
                //there is a handler registered that handled this packet type
                return;
            }
            else
            {
                // Main packet processing conditional
                switch (Pack.Type)
                {
                    #region Scene/Avatar

                    case PacketType.GenericMessage:
                        GenericMessagePacket gmpack = (GenericMessagePacket)Pack;

                        DecipherGenericMessage(Utils.BytesToString(gmpack.MethodData.Method), gmpack.MethodData.Invoice, gmpack.ParamList);

                        break;
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
                        Vector3 fromPos = new Vector3(); // ClientAvatar.Pos;
                        // UUID fromAgentID = AgentId;

                        int channel = inchatpack.ChatData.Channel;

                        if (OnChatFromViewer != null)
                        {
                            OSChatMessage args = new OSChatMessage();
                            args.Channel = channel;
                            args.From = fromName;
                            args.Message = Utils.BytesToString(message);
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
                            UserProfile.ID = AgentId;
                            UserProfile.AboutText = Utils.BytesToString(Properties.AboutText);
                            UserProfile.FirstLifeAboutText = Utils.BytesToString(Properties.FLAboutText);
                            UserProfile.FirstLifeImage = Properties.FLImageID;
                            UserProfile.Image = Properties.ImageID;

                            handlerUpdateAvatarProperties(this, UserProfile);
                        }
                        break;

                    case PacketType.ScriptDialogReply:
                        ScriptDialogReplyPacket rdialog = (ScriptDialogReplyPacket)Pack;
                        int ch = rdialog.Data.ChatChannel;
                        byte[] msg = rdialog.Data.ButtonLabel;
                        if (OnChatFromViewer != null)
                        {
                            OSChatMessage args = new OSChatMessage();
                            args.Channel = ch;
                            args.From = String.Empty;
                            args.Message = Utils.BytesToString(msg);
                            args.Type = ChatTypeEnum.Shout;
                            args.Position = new Vector3();
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
                        string IMmessage = Utils.BytesToString(msgpack.MessageBlock.Message);
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
                        List<UUID> callingCardFolders = new List<UUID>();

                        UUID agentID = afriendpack.AgentData.AgentID;
                        UUID transactionID = afriendpack.TransactionBlock.TransactionID;

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
                        UUID listOwnerAgentID = tfriendpack.AgentData.AgentID;
                        UUID exFriendID = tfriendpack.ExBlock.OtherID;

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
                            // Temporarily protect ourselves from the mantis #951 failure.
                            // However, we could do this for several other handlers where a failure isn't terminal
                            // for the client session anyway, in order to protect ourselves against bad code in plugins
                            try
                            {
                                List<byte> visualparams = new List<byte>();
                                foreach (AgentSetAppearancePacket.VisualParamBlock x in appear.VisualParam)
                                {
                                    visualparams.Add(x.ParamValue);
                                }

                                handlerSetAppearance(appear.ObjectData.TextureEntry, visualparams);
                            }
                            catch (Exception e)
                            {
                                m_log.ErrorFormat(
                                    "[CLIENT VIEW]: AgentSetApperance packet handler threw an exception, {0}",
                                    e);
                            }
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
                    case PacketType.DetachAttachmentIntoInv:
                        handlerDetachAttachmentIntoInv = OnDetachAttachmentIntoInv;
                        if (handlerDetachAttachmentIntoInv != null)
                        {
                            DetachAttachmentIntoInvPacket detachtoInv = (DetachAttachmentIntoInvPacket)Pack;

                            UUID itemID = detachtoInv.ObjectData.ItemID;
                            // UUID ATTACH_agentID = detachtoInv.ObjectData.AgentID;

                            handlerDetachAttachmentIntoInv(itemID, this);
                        }
                        break;
                    case PacketType.ObjectAttach:
                        if (OnObjectAttach != null)
                        {
                            ObjectAttachPacket att = (ObjectAttachPacket)Pack;

                            handlerObjectAttach = OnObjectAttach;

                            if (handlerObjectAttach != null)
                            {
                                if (att.ObjectData.Length > 0)
                                {
                                    handlerObjectAttach(this, att.ObjectData[0].ObjectLocalID, att.AgentData.AttachmentPoint, att.ObjectData[0].Rotation);
                                }
                            }
                        }

                        break;
                    case PacketType.ObjectDetach:

                        ObjectDetachPacket dett = (ObjectDetachPacket)Pack;
                        for (int j = 0; j < dett.ObjectData.Length; j++)
                        {
                            uint obj = dett.ObjectData[j].ObjectLocalID;
                            handlerObjectDetach = OnObjectDetach;
                            if (handlerObjectDetach != null)
                            {
                                handlerObjectDetach(obj, this);
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

                            AgentUpdatePacket.AgentDataBlock x = agenUpdate.AgentData;
                            AgentUpdateArgs arg = new AgentUpdateArgs();
                            arg.AgentID = x.AgentID;
                            arg.BodyRotation = x.BodyRotation;
                            arg.CameraAtAxis = x.CameraAtAxis;
                            arg.CameraCenter = x.CameraCenter;
                            arg.CameraLeftAxis = x.CameraLeftAxis;
                            arg.CameraUpAxis = x.CameraUpAxis;
                            arg.ControlFlags = x.ControlFlags;
                            arg.Far = x.Far;
                            arg.Flags = x.Flags;
                            arg.HeadRotation = x.HeadRotation;
                            arg.SessionID = x.SessionID;
                            arg.State = x.State;

                            handlerAgentUpdate = OnAgentUpdate;
                            if (handlerAgentUpdate != null)
                                OnAgentUpdate(this, arg);

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
                                    handlerStartAnim(this, AgentAni.AnimationList[i].AnimID);
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
                        //Console.WriteLine("Agent Sends:" + Utils.BytesToString(querydata.Name));

                        handlerAvatarPickerRequest = OnAvatarPickerRequest;
                        if (handlerAvatarPickerRequest != null)
                        {
                            handlerAvatarPickerRequest(this, Requestdata.AgentID, Requestdata.QueryID,
                                                       Utils.BytesToString(querydata.Name));
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
                        m_PacketHandler.PacketQueue.SetThrottleFromClient(atpack.Throttle.Throttles);
                        break;

                    case PacketType.AgentPause:
                        m_probesWithNoIngressPackets = 0;
                        m_clientBlocked = true;
                        break;

                    case PacketType.AgentResume:
                        m_probesWithNoIngressPackets = 0;
                        m_clientBlocked = false;
                        SendStartPingCheck(0);

                        break;

                    case PacketType.ForceScriptControlRelease:
                        handlerForceReleaseControls = OnForceReleaseControls;
                        if (handlerForceReleaseControls != null)
                        {
                            handlerForceReleaseControls(this, AgentId);
                        }
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
                            handlerLinkObjects(this, parentprimid, childrenprims);
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

                            //Check to see if adding the prim is allowed; useful for any module wanting to restrict the
                            //object from rezing initially

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
                                UpdateShapeArgs shapeData = new UpdateShapeArgs();
                                shapeData.ObjectLocalID = shapePacket.ObjectData[i].ObjectLocalID;
                                shapeData.PathBegin = shapePacket.ObjectData[i].PathBegin;
                                shapeData.PathCurve = shapePacket.ObjectData[i].PathCurve;
                                shapeData.PathEnd = shapePacket.ObjectData[i].PathEnd;
                                shapeData.PathRadiusOffset = shapePacket.ObjectData[i].PathRadiusOffset;
                                shapeData.PathRevolutions = shapePacket.ObjectData[i].PathRevolutions;
                                shapeData.PathScaleX = shapePacket.ObjectData[i].PathScaleX;
                                shapeData.PathScaleY = shapePacket.ObjectData[i].PathScaleY;
                                shapeData.PathShearX = shapePacket.ObjectData[i].PathShearX;
                                shapeData.PathShearY = shapePacket.ObjectData[i].PathShearY;
                                shapeData.PathSkew = shapePacket.ObjectData[i].PathSkew;
                                shapeData.PathTaperX = shapePacket.ObjectData[i].PathTaperX;
                                shapeData.PathTaperY = shapePacket.ObjectData[i].PathTaperY;
                                shapeData.PathTwist = shapePacket.ObjectData[i].PathTwist;
                                shapeData.PathTwistBegin = shapePacket.ObjectData[i].PathTwistBegin;
                                shapeData.ProfileBegin = shapePacket.ObjectData[i].ProfileBegin;
                                shapeData.ProfileCurve = shapePacket.ObjectData[i].ProfileCurve;
                                shapeData.ProfileEnd = shapePacket.ObjectData[i].ProfileEnd;
                                shapeData.ProfileHollow = shapePacket.ObjectData[i].ProfileHollow;

                                handlerUpdatePrimShape(m_agentId, shapePacket.ObjectData[i].ObjectLocalID,
                                                       shapeData);
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
                    case PacketType.ObjectPosition:
                        // DEPRECATED: but till libsecondlife removes it, people will use it
                        ObjectPositionPacket position = (ObjectPositionPacket)Pack;

                        for (int i = 0; i < position.ObjectData.Length; i++)
                        {
                            handlerUpdateVector = OnUpdatePrimGroupPosition;
                            if (handlerUpdateVector != null)
                                handlerUpdateVector(position.ObjectData[i].ObjectLocalID, position.ObjectData[i].Position, this);
                        }

                        break;
                    case PacketType.ObjectScale:
                        // DEPRECATED: but till libsecondlife removes it, people will use it
                        ObjectScalePacket scale = (ObjectScalePacket)Pack;

                        for (int i = 0; i < scale.ObjectData.Length; i++)
                        {
                            handlerUpdatePrimGroupScale = OnUpdatePrimGroupScale;
                            if (handlerUpdatePrimGroupScale != null)
                                handlerUpdatePrimGroupScale(scale.ObjectData[i].ObjectLocalID, scale.ObjectData[i].Scale, this);
                        }

                        break;
                    case PacketType.ObjectRotation:
                        // DEPRECATED: but till libsecondlife removes it, people will use it
                        ObjectRotationPacket rotation = (ObjectRotationPacket)Pack;

                        for (int i = 0; i < rotation.ObjectData.Length; i++)
                        {
                            handlerUpdatePrimRotation = OnUpdatePrimGroupRotation;
                            if (handlerUpdatePrimRotation != null)
                                handlerUpdatePrimRotation(rotation.ObjectData[i].ObjectLocalID, rotation.ObjectData[i].Rotation, this);
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
                                                         Util.FieldToString(objDes.ObjectData[i].Description));
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
                                                  Util.FieldToString(objName.ObjectData[i].Name));
                            }
                        }
                        break;
                    case PacketType.ObjectPermissions:
                        if (OnObjectPermissions != null)
                        {
                            ObjectPermissionsPacket newobjPerms = (ObjectPermissionsPacket)Pack;

                            UUID AgentID = newobjPerms.AgentData.AgentID;
                            UUID SessionID = newobjPerms.AgentData.SessionID;

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
                                    handlerObjectPermissions(this, AgentID, SessionID, field, localID, mask, set);
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

                    case PacketType.Undo:
                        UndoPacket undoitem = (UndoPacket)Pack;
                        if (undoitem.ObjectData.Length > 0)
                        {
                            for (int i = 0; i < undoitem.ObjectData.Length; i++)
                            {
                                UUID objiD = undoitem.ObjectData[i].ObjectID;
                                handlerOnUndo = OnUndo;
                                if (handlerOnUndo != null)
                                {
                                    handlerOnUndo(this, objiD);
                                }

                            }
                        }
                        break;
                    case PacketType.ObjectDuplicateOnRay:
                        ObjectDuplicateOnRayPacket dupeOnRay = (ObjectDuplicateOnRayPacket)Pack;

                        handlerObjectDuplicateOnRay = null;


                        for (int i = 0; i < dupeOnRay.ObjectData.Length; i++)
                        {
                            handlerObjectDuplicateOnRay = OnObjectDuplicateOnRay;
                            if (handlerObjectDuplicateOnRay != null)
                            {
                                handlerObjectDuplicateOnRay(dupeOnRay.ObjectData[i].ObjectLocalID, dupeOnRay.AgentData.DuplicateFlags,
                                                            dupeOnRay.AgentData.AgentID, dupeOnRay.AgentData.GroupID, dupeOnRay.AgentData.RayTargetID, dupeOnRay.AgentData.RayEnd,
                                                            dupeOnRay.AgentData.RayStart, dupeOnRay.AgentData.BypassRaycast, dupeOnRay.AgentData.RayEndIsIntersection,
                                                            dupeOnRay.AgentData.CopyCenters, dupeOnRay.AgentData.CopyRotates);
                            }
                        }

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
                    case PacketType.ObjectIncludeInSearch:
                        //This lets us set objects to appear in search (stuff like DataSnapshot, etc)
                        ObjectIncludeInSearchPacket packInSearch = (ObjectIncludeInSearchPacket)Pack;
                        handlerObjectIncludeInSearch = null;

                        foreach (ObjectIncludeInSearchPacket.ObjectDataBlock objData in packInSearch.ObjectData)
                        {
                            bool inSearch = objData.IncludeInSearch;
                            uint localID = objData.ObjectLocalID;

                            handlerObjectIncludeInSearch = OnObjectIncludeInSearch;

                            if (handlerObjectIncludeInSearch != null)
                            {
                                handlerObjectIncludeInSearch(this, inSearch, localID);
                            }
                        }
                        break;

                    case PacketType.ScriptAnswerYes:
                        ScriptAnswerYesPacket scriptAnswer = (ScriptAnswerYesPacket)Pack;

                        handlerScriptAnswer = OnScriptAnswer;
                        if (handlerScriptAnswer != null)
                        {
                            handlerScriptAnswer(this, scriptAnswer.Data.TaskID, scriptAnswer.Data.ItemID, scriptAnswer.Data.Questions);
                        }
                        break;

                    case PacketType.ObjectClickAction:
                         ObjectClickActionPacket ocpacket = (ObjectClickActionPacket)Pack;

                         handlerObjectClickAction = OnObjectClickAction;
                         if (handlerObjectClickAction != null) 
                         {
                             foreach (ObjectClickActionPacket.ObjectDataBlock odata in ocpacket.ObjectData)
                             {
                                 byte action = odata.ClickAction;
                                 uint localID = odata.ObjectLocalID;
                                 handlerObjectClickAction(this, localID, action.ToString());
                             }
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
                        // Validate inventory transfers
                        // Has to be done here, because AssetCache can't do it
                        //
                        if (transfer.TransferInfo.SourceType == 3)
                        {
                            UUID taskID = null;
                            UUID itemID = null;
                            UUID requestID = null;
                            taskID = new UUID(transfer.TransferInfo.Params, 48);
                            itemID = new UUID(transfer.TransferInfo.Params, 64);
                            requestID = new UUID(transfer.TransferInfo.Params, 80);
                            if (!(((Scene)m_scene).ExternalChecks.ExternalChecksBypassPermissions()))
                            {
                                if (taskID != UUID.Zero) // Prim
                                {
                                    SceneObjectPart part = ((Scene)m_scene).GetSceneObjectPart(taskID);
                                    if (part == null)
                                        break;

                                    if (part.OwnerID != AgentId)
                                        break;

                                    if ((part.OwnerMask & (uint)PermissionMask.Modify) == 0)
                                        break;

                                    TaskInventoryItem ti = part.GetInventoryItem(itemID);
                                    if (ti == null)
                                        break;

                                    if (ti.OwnerID != AgentId)
                                        break;

                                    if ((ti.CurrentPermissions & ((uint)PermissionMask.Modify| (uint)PermissionMask.Copy | (uint)PermissionMask.Transfer)) != ((uint)PermissionMask.Modify| (uint)PermissionMask.Copy | (uint)PermissionMask.Transfer))
                                        break;

                                    if (ti.AssetID != requestID)
                                        break;
                                }
                                else // Agent
                                {
                                    CachedUserInfo userInfo = ((Scene)m_scene).CommsManager.UserProfileCacheService.GetUserDetails(AgentId);
                                    if (userInfo == null)
                                        break;

                                    if (userInfo.RootFolder == null)
                                        break;

                                    InventoryItemBase assetRequestItem = userInfo.RootFolder.FindItem(itemID);
                                    if (assetRequestItem == null)
                                    {
                                        assetRequestItem = ((Scene)m_scene).CommsManager.UserProfileCacheService.libraryRoot.FindItem(itemID);
                                        if (assetRequestItem == null)
                                            return;
                                    }

                                    if ((assetRequestItem.CurrentPermissions & ((uint)PermissionMask.Modify| (uint)PermissionMask.Copy | (uint)PermissionMask.Transfer)) != ((uint)PermissionMask.Modify| (uint)PermissionMask.Copy | (uint)PermissionMask.Transfer))
                                        break;
                                    if (assetRequestItem.AssetID != requestID)
                                        break;
                                }
                            }
                        }

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
                        UUID temp = UUID.Combine(request.AssetBlock.TransactionID, SecureSessionId);

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
                                                          createItem.InventoryBlock.NextOwnerMask,
                                                          Util.UnixTimeSinceEpoch());
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
                                    InventoryItemBase itemUpd = new InventoryItemBase();
                                    itemUpd.ID = update.InventoryData[i].ItemID;
                                    itemUpd.Name = Util.FieldToString(update.InventoryData[i].Name);
                                    itemUpd.Description = Util.FieldToString(update.InventoryData[i].Description);
                                    itemUpd.GroupID = update.InventoryData[i].GroupID;
                                    itemUpd.GroupOwned = update.InventoryData[i].GroupOwned;
                                    itemUpd.NextPermissions = update.InventoryData[i].NextOwnerMask;
                                    itemUpd.EveryOnePermissions = update.InventoryData[i].EveryoneMask;
                                    itemUpd.CreationDate = update.InventoryData[i].CreationDate;
                                    itemUpd.Folder = update.InventoryData[i].FolderID;
                                    itemUpd.InvType = update.InventoryData[i].InvType;
                                    itemUpd.SalePrice = update.InventoryData[i].SalePrice;
                                    itemUpd.SaleType = update.InventoryData[i].SaleType;
                                    itemUpd.Flags = update.InventoryData[i].Flags;
                                    /*
                                    OnUpdateInventoryItem(this, update.InventoryData[i].TransactionID,
                                                          update.InventoryData[i].ItemID,
                                                          Util.FieldToString(update.InventoryData[i].Name),
                                                          Util.FieldToString(update.InventoryData[i].Description),
                                                          update.InventoryData[i].NextOwnerMask);
                                    */
                                    OnUpdateInventoryItem(this, update.InventoryData[i].TransactionID,
                                                          update.InventoryData[i].ItemID,
                                                          itemUpd);
                                }
                            }
                        }
                        //Console.WriteLine(Pack.ToString());
                        /*for (int i = 0; i < update.InventoryData.Length; i++)
                        {
                            if (update.InventoryData[i].TransactionID != UUID.Zero)
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
                                    TaskInventoryItem newTaskItem = new TaskInventoryItem();
                                    newTaskItem.ItemID = updatetask.InventoryData.ItemID;
                                    newTaskItem.ParentID = updatetask.InventoryData.FolderID;
                                    newTaskItem.CreatorID = updatetask.InventoryData.CreatorID;
                                    newTaskItem.OwnerID = updatetask.InventoryData.OwnerID;
                                    newTaskItem.GroupID = updatetask.InventoryData.GroupID;
                                    newTaskItem.BasePermissions = updatetask.InventoryData.BaseMask;
                                    newTaskItem.CurrentPermissions = updatetask.InventoryData.OwnerMask;
                                    newTaskItem.GroupPermissions = updatetask.InventoryData.GroupMask;
                                    newTaskItem.EveryonePermissions = updatetask.InventoryData.EveryoneMask;
                                    newTaskItem.NextPermissions = updatetask.InventoryData.NextOwnerMask;
                                    //newTaskItem.GroupOwned=updatetask.InventoryData.GroupOwned;
                                    newTaskItem.Type = updatetask.InventoryData.Type;
                                    newTaskItem.InvType = updatetask.InventoryData.InvType;
                                    newTaskItem.Flags = updatetask.InventoryData.Flags;
                                    //newTaskItem.SaleType=updatetask.InventoryData.SaleType;
                                    //newTaskItem.SalePrice=updatetask.InventoryData.SalePrice;;
                                    newTaskItem.Name = Util.FieldToString(updatetask.InventoryData.Name);
                                    newTaskItem.Description = Util.FieldToString(updatetask.InventoryData.Description);
                                    newTaskItem.CreationDate = (uint)updatetask.InventoryData.CreationDate;
                                    handlerUpdateTaskInventory(this, updatetask.InventoryData.TransactionID,
                                                               newTaskItem, updatetask.UpdateData.LocalID);
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

                        MoveTaskInventoryPacket moveTaskInventoryPacket = (MoveTaskInventoryPacket)Pack;

                        handlerMoveTaskItem = OnMoveTaskItem;

                        if (handlerMoveTaskItem != null)
                        {
                            handlerMoveTaskItem(
                                this, moveTaskInventoryPacket.AgentData.FolderID,
                                moveTaskInventoryPacket.InventoryData.LocalID,
                                moveTaskInventoryPacket.InventoryData.ItemID);
                        }

                        break;

                    case PacketType.RezScript:

                        //Console.WriteLine(Pack.ToString());
                        RezScriptPacket rezScriptx = (RezScriptPacket)Pack;

                        handlerRezScript = OnRezScript;
                        InventoryItemBase item = new InventoryItemBase();
                        item.ID = rezScriptx.InventoryBlock.ItemID;
                        item.Folder = rezScriptx.InventoryBlock.FolderID;
                        item.Creator = rezScriptx.InventoryBlock.CreatorID;
                        item.Owner = rezScriptx.InventoryBlock.OwnerID;
                        item.BasePermissions = rezScriptx.InventoryBlock.BaseMask;
                        item.CurrentPermissions = rezScriptx.InventoryBlock.OwnerMask;
                        item.EveryOnePermissions = rezScriptx.InventoryBlock.EveryoneMask;
                        item.NextPermissions = rezScriptx.InventoryBlock.NextOwnerMask;
                        item.GroupOwned = rezScriptx.InventoryBlock.GroupOwned;
                        item.GroupID = rezScriptx.InventoryBlock.GroupID;
                        item.AssetType = rezScriptx.InventoryBlock.Type;
                        item.InvType = rezScriptx.InventoryBlock.InvType;
                        item.Flags = rezScriptx.InventoryBlock.Flags;
                        item.SaleType = rezScriptx.InventoryBlock.SaleType;
                        item.SalePrice = rezScriptx.InventoryBlock.SalePrice;
                        item.Name = Util.FieldToString(rezScriptx.InventoryBlock.Name);
                        item.Description = Util.FieldToString(rezScriptx.InventoryBlock.Description);
                        item.CreationDate = (int)rezScriptx.InventoryBlock.CreationDate;

                        if (handlerRezScript != null)
                        {
                            handlerRezScript(this, item, rezScriptx.InventoryBlock.TransactionID, rezScriptx.UpdateBlock.ObjectLocalID);
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
                                                    MapRequest.PositionData.MaxX, MapRequest.PositionData.MaxY, MapRequest.AgentData.Flags);
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
                        UUID lmid = tpReq.Info.LandmarkID;
                        AssetLandmark lm;
                        if (lmid != UUID.Zero)
                        {
                            AssetBase lma = m_assetCache.GetAsset(lmid, false);

                            if (lma == null)
                            {
                                // Failed to find landmark
                                TeleportCancelPacket tpCancel = (TeleportCancelPacket)PacketPool.Instance.GetPacket(PacketType.TeleportCancel);
                                tpCancel.Info.SessionID = tpReq.Info.SessionID;
                                tpCancel.Info.AgentID = tpReq.Info.AgentID;
                                OutPacket(tpCancel, ThrottleOutPacketType.Task);
                            }

                            try
                            {
                                lm = new AssetLandmark(lma);
                            }
                            catch (NullReferenceException)
                            {
                                // asset not found generates null ref inside the assetlandmark constructor.
                                TeleportCancelPacket tpCancel = (TeleportCancelPacket)PacketPool.Instance.GetPacket(PacketType.TeleportCancel);
                                tpCancel.Info.SessionID = tpReq.Info.SessionID;
                                tpCancel.Info.AgentID = tpReq.Info.AgentID;
                                OutPacket(tpCancel, ThrottleOutPacketType.Task);
                                break;
                            }
                        }
                        else
                        {
                            // Teleport home request
                            handlerTeleportHomeRequest = OnTeleportHomeRequest;
                            if (handlerTeleportHomeRequest != null)
                            {
                                handlerTeleportHomeRequest(this.AgentId, this);
                            }
                            break;
                        }

                        handlerTeleportLandmarkRequest = OnTeleportLandmarkRequest;
                        if (handlerTeleportLandmarkRequest != null)
                        {
                            handlerTeleportLandmarkRequest(this, lm.RegionID, lm.Position);
                        }
                        else
                        {
                            //no event handler so cancel request


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

                    case PacketType.RegionHandleRequest:
                        RegionHandleRequestPacket rhrPack = (RegionHandleRequestPacket)Pack;

                        handlerRegionHandleRequest = OnRegionHandleRequest;
                        if (handlerRegionHandleRequest != null)
                        {
                            handlerRegionHandleRequest(this, rhrPack.RequestBlock.RegionID);
                        }
                        break;

                    case PacketType.ParcelInfoRequest:
                        ParcelInfoRequestPacket pirPack = (ParcelInfoRequestPacket)Pack;

                        handlerParcelInfoRequest = OnParcelInfoRequest;
                        if (handlerParcelInfoRequest != null)
                        {
                            handlerParcelInfoRequest(this, pirPack.Data.ParcelID);
                        }
                        break;

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
                            LandUpdateArgs args = new LandUpdateArgs();

                            args.AuthBuyerID = parcelPropertiesPacket.ParcelData.AuthBuyerID;
                            args.Category = (Parcel.ParcelCategory)parcelPropertiesPacket.ParcelData.Category;
                            args.Desc = Utils.BytesToString(parcelPropertiesPacket.ParcelData.Desc);
                            args.GroupID = parcelPropertiesPacket.ParcelData.GroupID;
                            args.LandingType = parcelPropertiesPacket.ParcelData.LandingType;
                            args.MediaAutoScale = parcelPropertiesPacket.ParcelData.MediaAutoScale;
                            args.MediaID = parcelPropertiesPacket.ParcelData.MediaID;
                            args.MediaURL = Utils.BytesToString(parcelPropertiesPacket.ParcelData.MediaURL);
                            args.MusicURL = Utils.BytesToString(parcelPropertiesPacket.ParcelData.MusicURL);
                            args.Name = Utils.BytesToString(parcelPropertiesPacket.ParcelData.Name);
                            args.ParcelFlags = parcelPropertiesPacket.ParcelData.ParcelFlags;
                            args.PassHours = parcelPropertiesPacket.ParcelData.PassHours;
                            args.PassPrice = parcelPropertiesPacket.ParcelData.PassPrice;
                            args.SalePrice = parcelPropertiesPacket.ParcelData.SalePrice;
                            args.SnapshotID = parcelPropertiesPacket.ParcelData.SnapshotID;
                            args.UserLocation = parcelPropertiesPacket.ParcelData.UserLocation;
                            args.UserLookAt = parcelPropertiesPacket.ParcelData.UserLookAt;
                            handlerParcelPropertiesUpdateRequest(args, parcelPropertiesPacket.ParcelData.LocalID, this);
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
                        //Console.WriteLine(Pack.ToString());
                        ParcelObjectOwnersRequestPacket reqPacket = (ParcelObjectOwnersRequestPacket)Pack;

                        handlerParcelObjectOwnerRequest = OnParcelObjectOwnerRequest;

                        if (handlerParcelObjectOwnerRequest != null)
                        {
                            handlerParcelObjectOwnerRequest(reqPacket.ParcelData.LocalID, this);
                        }
                        break;
                    case PacketType.ParcelRelease:
                        ParcelReleasePacket releasePacket = (ParcelReleasePacket)Pack;

                        handlerParcelAbandonRequest = OnParcelAbandonRequest;
                        if (handlerParcelAbandonRequest != null)
                        {
                            handlerParcelAbandonRequest(releasePacket.Data.LocalID, this);
                        }
                        break;
                    case PacketType.ParcelReclaim:
                        ParcelReclaimPacket reclaimPacket = (ParcelReclaimPacket)Pack;

                        handlerParcelReclaim = OnParcelReclaim;
                        if (handlerParcelReclaim != null)
                        {
                            handlerParcelReclaim(reclaimPacket.Data.LocalID, this);
                        }
                        break;
                    case PacketType.ParcelReturnObjects:


                        ParcelReturnObjectsPacket parcelReturnObjects = (ParcelReturnObjectsPacket)Pack;

                        UUID[] puserselectedOwnerIDs = new UUID[parcelReturnObjects.OwnerIDs.Length];
                        for (int parceliterator = 0; parceliterator < parcelReturnObjects.OwnerIDs.Length; parceliterator++)
                            puserselectedOwnerIDs[parceliterator] = parcelReturnObjects.OwnerIDs[parceliterator].OwnerID;

                        UUID[] puserselectedTaskIDs = new UUID[parcelReturnObjects.TaskIDs.Length];

                        for (int parceliterator = 0; parceliterator < parcelReturnObjects.TaskIDs.Length; parceliterator++)
                            puserselectedTaskIDs[parceliterator] = parcelReturnObjects.TaskIDs[parceliterator].TaskID;

                        handlerParcelReturnObjectsRequest = OnParcelReturnObjectsRequest;
                        if (handlerParcelReturnObjectsRequest != null)
                        {
                            handlerParcelReturnObjectsRequest(parcelReturnObjects.ParcelData.LocalID, parcelReturnObjects.ParcelData.ReturnType, puserselectedOwnerIDs, puserselectedTaskIDs, this);

                        }
                        break;

                    #endregion

                    #region Estate Packets

                    case PacketType.EstateOwnerMessage:
                        EstateOwnerMessagePacket messagePacket = (EstateOwnerMessagePacket)Pack;

                        switch (Utils.BytesToString(messagePacket.MethodData.Method))
                        {
                            case "getinfo":
                                if (((Scene)m_scene).ExternalChecks.ExternalChecksCanIssueEstateCommand(this.AgentId, false))
                                {
                                    OnDetailedEstateDataRequest(this, messagePacket.MethodData.Invoice);
                                }
                                break;
                            case "setregioninfo":
                                if (((Scene)m_scene).ExternalChecks.ExternalChecksCanIssueEstateCommand(this.AgentId, false))
                                {
                                    OnSetEstateFlagsRequest(convertParamStringToBool(messagePacket.ParamList[0].Parameter), convertParamStringToBool(messagePacket.ParamList[1].Parameter),
                                        convertParamStringToBool(messagePacket.ParamList[2].Parameter), !convertParamStringToBool(messagePacket.ParamList[3].Parameter),
                                        Convert.ToInt16(Convert.ToDecimal(Utils.BytesToString(messagePacket.ParamList[4].Parameter))),
                                        (float)Convert.ToDecimal(Utils.BytesToString(messagePacket.ParamList[5].Parameter)),
                                        Convert.ToInt16(Utils.BytesToString(messagePacket.ParamList[6].Parameter)),
                                        convertParamStringToBool(messagePacket.ParamList[7].Parameter), convertParamStringToBool(messagePacket.ParamList[8].Parameter));

                                }

                                break;
//                            case "texturebase":
//                                if (((Scene)m_scene).ExternalChecks.ExternalChecksCanIssueEstateCommand(this.AgentId, false))
//                                {
//                                    foreach (EstateOwnerMessagePacket.ParamListBlock block in messagePacket.ParamList)
//                                    {
//                                        string s = Utils.BytesToString(block.Parameter);
//                                        string[] splitField = s.Split(' ');
//                                        if (splitField.Length == 2)
//                                        {
//                                            UUID tempUUID = new UUID(splitField[1]);
//                                            OnSetEstateTerrainBaseTexture(this, Convert.ToInt16(splitField[0]), tempUUID);
//                                        }
//                                    }
//                                }
//                                break;
                            case "texturedetail":
                                if (((Scene)m_scene).ExternalChecks.ExternalChecksCanIssueEstateCommand(this.AgentId, false))
                                {
                                    foreach (EstateOwnerMessagePacket.ParamListBlock block in messagePacket.ParamList)
                                    {
                                        string s = Utils.BytesToString(block.Parameter);
                                        string[] splitField = s.Split(' ');
                                        if (splitField.Length == 2)
                                        {
                                            Int16 corner = Convert.ToInt16(splitField[0]);
                                            UUID textureUUID = new UUID(splitField[1]);

                                            OnSetEstateTerrainDetailTexture(this, corner, textureUUID);
                                        }
                                    }
                                }

                                break;
                            case "textureheights":
                                if (((Scene)m_scene).ExternalChecks.ExternalChecksCanIssueEstateCommand(this.AgentId, false))
                                {
                                    foreach (EstateOwnerMessagePacket.ParamListBlock block in messagePacket.ParamList)
                                    {
                                        string s = Utils.BytesToString(block.Parameter);
                                        string[] splitField = s.Split(' ');
                                        if (splitField.Length == 3)
                                        {
                                            Int16 corner = Convert.ToInt16(splitField[0]);
                                            float lowValue = (float)Convert.ToDecimal(splitField[1]);
                                            float highValue = (float)Convert.ToDecimal(splitField[2]);

                                            OnSetEstateTerrainTextureHeights(this, corner, lowValue, highValue);
                                        }
                                    }
                                }
                                break;
                            case "texturecommit":
                                OnCommitEstateTerrainTextureRequest(this);
                                break;
                            case "setregionterrain":
                                if (((Scene)m_scene).ExternalChecks.ExternalChecksCanIssueEstateCommand(this.AgentId, false))
                                {
                                    if (messagePacket.ParamList.Length != 9)
                                    {
                                        m_log.Error("EstateOwnerMessage: SetRegionTerrain method has a ParamList of invalid length");
                                    }
                                    else
                                    {
                                        try
                                        {
                                            string tmp;
                                            tmp = Utils.BytesToString(messagePacket.ParamList[0].Parameter);
                                            if (!tmp.Contains(".")) tmp += ".00";
                                            float WaterHeight = (float)Convert.ToDecimal(tmp);
                                            tmp = Utils.BytesToString(messagePacket.ParamList[1].Parameter);
                                            if (!tmp.Contains(".")) tmp += ".00";
                                            float TerrainRaiseLimit = (float)Convert.ToDecimal(tmp);
                                            tmp = Utils.BytesToString(messagePacket.ParamList[2].Parameter);
                                            if (!tmp.Contains(".")) tmp += ".00";
                                            float TerrainLowerLimit = (float)Convert.ToDecimal(tmp);
                                            bool UseEstateSun = convertParamStringToBool(messagePacket.ParamList[3].Parameter);
                                            bool UseFixedSun = convertParamStringToBool(messagePacket.ParamList[4].Parameter);
                                            float SunHour = (float)Convert.ToDecimal(Utils.BytesToString(messagePacket.ParamList[5].Parameter));
                                            bool UseGlobal = convertParamStringToBool(messagePacket.ParamList[6].Parameter);
                                            bool EstateFixedSun = convertParamStringToBool(messagePacket.ParamList[7].Parameter);
                                            float EstateSunHour = (float)Convert.ToDecimal(Utils.BytesToString(messagePacket.ParamList[8].Parameter));

                                            OnSetRegionTerrainSettings(WaterHeight, TerrainRaiseLimit, TerrainLowerLimit, UseEstateSun, UseFixedSun, SunHour, UseGlobal, EstateFixedSun, EstateSunHour);

                                        }
                                        catch (Exception ex)
                                        {
                                            m_log.Error("EstateOwnerMessage: Exception while setting terrain settings: \n" + messagePacket.ToString() + "\n" + ex.ToString());
                                        }
                                    }
                                }

                                break;
                            case "restart":
                                if (((Scene)m_scene).ExternalChecks.ExternalChecksCanIssueEstateCommand(this.AgentId, false))
                                {
                                    // There's only 1 block in the estateResetSim..   and that's the number of seconds till restart.
                                    foreach (EstateOwnerMessagePacket.ParamListBlock block in messagePacket.ParamList)
                                    {
                                        float timeSeconds = 0;
                                        Utils.TryParseSingle(Utils.BytesToString(block.Parameter), out timeSeconds);
                                        timeSeconds = (int)timeSeconds;
                                        OnEstateRestartSimRequest(this, (int)timeSeconds);

                                    }
                                }
                                break;
                            case "estatechangecovenantid":
                                if (((Scene)m_scene).ExternalChecks.ExternalChecksCanIssueEstateCommand(this.AgentId, false))
                                {
                                    foreach (EstateOwnerMessagePacket.ParamListBlock block in messagePacket.ParamList)
                                    {
                                        UUID newCovenantID = new UUID(Utils.BytesToString(block.Parameter));
                                        OnEstateChangeCovenantRequest(this, newCovenantID);
                                    }
                                }
                                break;
                            case "estateaccessdelta": // Estate access delta manages the banlist and allow list too.
                                if (((Scene)m_scene).ExternalChecks.ExternalChecksCanIssueEstateCommand(this.AgentId, false))
                                {
                                    int estateAccessType = Convert.ToInt16(Utils.BytesToString(messagePacket.ParamList[1].Parameter));
                                    OnUpdateEstateAccessDeltaRequest(this, messagePacket.MethodData.Invoice, estateAccessType, new UUID(Utils.BytesToString(messagePacket.ParamList[2].Parameter)));

                                }
                                break;
                            case "simulatormessage":
                                if (((Scene)m_scene).ExternalChecks.ExternalChecksCanIssueEstateCommand(this.AgentId, false))
                                {
                                    UUID invoice = messagePacket.MethodData.Invoice;
                                    UUID SenderID = new UUID(Utils.BytesToString(messagePacket.ParamList[2].Parameter));
                                    string SenderName = Utils.BytesToString(messagePacket.ParamList[3].Parameter);
                                    string Message = Utils.BytesToString(messagePacket.ParamList[4].Parameter);
                                    UUID sessionID = messagePacket.AgentData.SessionID;
                                    OnSimulatorBlueBoxMessageRequest(this, invoice, SenderID, sessionID, SenderName, Message);
                                }
                                break;
                            case "instantmessage":
                                if (((Scene)m_scene).ExternalChecks.ExternalChecksCanIssueEstateCommand(this.AgentId, false))
                                {
                                    if (messagePacket.ParamList.Length < 5)
                                        break;
                                    UUID invoice = messagePacket.MethodData.Invoice;
                                    UUID SenderID = new UUID(Utils.BytesToString(messagePacket.ParamList[2].Parameter));
                                    string SenderName = Utils.BytesToString(messagePacket.ParamList[3].Parameter);
                                    string Message = Utils.BytesToString(messagePacket.ParamList[4].Parameter);
                                    UUID sessionID = messagePacket.AgentData.SessionID;
                                    OnEstateBlueBoxMessageRequest(this, invoice, SenderID, sessionID, SenderName, Message);
                                }
                                break;
                            case "setregiondebug":
                                if (((Scene)m_scene).ExternalChecks.ExternalChecksCanIssueEstateCommand(this.AgentId, false))
                                {
                                    UUID invoice = messagePacket.MethodData.Invoice;
                                    UUID SenderID = messagePacket.AgentData.AgentID;
                                    bool scripted = convertParamStringToBool(messagePacket.ParamList[0].Parameter);
                                    bool collisionEvents = convertParamStringToBool(messagePacket.ParamList[1].Parameter);
                                    bool physics = convertParamStringToBool(messagePacket.ParamList[2].Parameter);

                                    OnEstateDebugRegionRequest(this, invoice, SenderID, scripted, collisionEvents, physics);
                                }
                                break;
                            case "teleporthomeuser":
                                if (((Scene)m_scene).ExternalChecks.ExternalChecksCanIssueEstateCommand(this.AgentId, false))
                                {
                                    UUID invoice = messagePacket.MethodData.Invoice;
                                    UUID SenderID = messagePacket.AgentData.AgentID;
                                    UUID Prey = UUID.Zero;

                                    UUID.TryParse(Utils.BytesToString(messagePacket.ParamList[1].Parameter), out Prey);

                                    OnEstateTeleportOneUserHomeRequest(this, invoice, SenderID, Prey);
                                }
                                break;
                            case "colliders":
                                handlerLandStatRequest = OnLandStatRequest;
                                if (handlerLandStatRequest != null)
                                {
                                    handlerLandStatRequest(0, 1, 0, "", this);
                                }
                                break;
                            case "scripts":
                                handlerLandStatRequest = OnLandStatRequest;
                                if (handlerLandStatRequest != null)
                                {
                                    handlerLandStatRequest(0, 0, 0, "", this);
                                }
                                break;
                            case "terrain":
                                if (((Scene)m_scene).ExternalChecks.ExternalChecksCanIssueEstateCommand(this.AgentId, false))
                                {
                                    handlerBakeTerrain = OnBakeTerrain;
                                    if (handlerBakeTerrain != null)
                                    {
                                        handlerBakeTerrain(this);
                                    }
                                }
                                break;

                            case "estatechangeinfo":
                                if (((Scene)m_scene).ExternalChecks.ExternalChecksCanIssueEstateCommand(this.AgentId, false))
                                {
                                    UUID invoice = messagePacket.MethodData.Invoice;
                                    UUID SenderID = messagePacket.AgentData.AgentID;
                                    UInt32 param1 = Convert.ToUInt32(Utils.BytesToString(messagePacket.ParamList[1].Parameter));
                                    UInt32 param2 = Convert.ToUInt32(Utils.BytesToString(messagePacket.ParamList[2].Parameter));

                                    handlerEstateChangeInfo = OnEstateChangeInfo;
                                    if (handlerEstateChangeInfo != null)
                                    {
                                        handlerEstateChangeInfo(this, invoice, SenderID, param1, param2);
                                    }
                                }
                                break;

                            default:
                                m_log.Error("EstateOwnerMessage: Unknown method requested\n" + messagePacket.ToString());
                                break;
                        }
                        break;
                    case PacketType.LandStatRequest:
                        LandStatRequestPacket lsrp = (LandStatRequestPacket)Pack;

                        handlerLandStatRequest = OnLandStatRequest;
                        if (handlerLandStatRequest != null)
                        {
                            handlerLandStatRequest(lsrp.RequestData.ParcelLocalID, lsrp.RequestData.ReportType, lsrp.RequestData.RequestFlags, Utils.BytesToString(lsrp.RequestData.Filter), this);
                        }
                        //int parcelID, uint reportType, uint requestflags, string filter

                        //lsrp.RequestData.ParcelLocalID;
                        //lsrp.RequestData.ReportType; // 1 = colliders, 0 = scripts
                        //lsrp.RequestData.RequestFlags;
                        //lsrp.RequestData.Filter;

                        break;

                    case PacketType.RequestRegionInfo:
                        // RequestRegionInfoPacket.AgentDataBlock mPacket = ((RequestRegionInfoPacket)Pack).AgentData;

                        handlerRegionInfoRequest = OnRegionInfoRequest;
                        if (handlerRegionInfoRequest != null)
                        {
                            handlerRegionInfoRequest(this);
                        }
                        break;
                    case PacketType.EstateCovenantRequest:

                        // EstateCovenantRequestPacket.AgentDataBlock epack =
                        //     ((EstateCovenantRequestPacket)Pack).AgentData;

                        handlerEstateCovenantRequest = OnEstateCovenantRequest;
                        if (handlerEstateCovenantRequest != null)
                        {
                            handlerEstateCovenantRequest(this);
                        }
                        break;

                    #endregion

                    #region GodPackets

                    case PacketType.RequestGodlikePowers:
                        RequestGodlikePowersPacket rglpPack = (RequestGodlikePowersPacket)Pack;
                        RequestGodlikePowersPacket.RequestBlockBlock rblock = rglpPack.RequestBlock;
                        UUID token = rblock.Token;

                        RequestGodlikePowersPacket.AgentDataBlock ablock = rglpPack.AgentData;

                        handlerReqGodlikePowers = OnRequestGodlikePowers;

                        if (handlerReqGodlikePowers != null)
                        {
                            handlerReqGodlikePowers(ablock.AgentID, ablock.SessionID, token, rblock.Godlike, this);
                        }

                        break;
                    case PacketType.GodKickUser:
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

                    #region Economy/Transaction Packets

                    case PacketType.MoneyBalanceRequest:
                        MoneyBalanceRequestPacket moneybalancerequestpacket = (MoneyBalanceRequestPacket)Pack;

                        handlerMoneyBalanceRequest = OnMoneyBalanceRequest;

                        if (handlerMoneyBalanceRequest != null)
                        {
                            handlerMoneyBalanceRequest(this, moneybalancerequestpacket.AgentData.AgentID, moneybalancerequestpacket.AgentData.SessionID, moneybalancerequestpacket.MoneyData.TransactionID);
                        }

                        break;
                    case PacketType.EconomyDataRequest:

                        handlerEconomoyDataRequest = OnEconomyDataRequest;
                        if (handlerEconomoyDataRequest != null)
                        {
                            handlerEconomoyDataRequest(AgentId);
                        }
                        // TODO: handle this packet
                        //m_log.Warn("[CLIENT]: unhandled EconomyDataRequest packet");
                        break;
                    case PacketType.RequestPayPrice:
                        RequestPayPricePacket requestPayPricePacket = (RequestPayPricePacket)Pack;
                        handlerRequestPayPrice = OnRequestPayPrice;
                        if (handlerRequestPayPrice != null)
                        {
                            handlerRequestPayPrice(this, requestPayPricePacket.ObjectData.ObjectID);
                        }
                        break;

                    case PacketType.ObjectSaleInfo:
                        ObjectSaleInfoPacket objectSaleInfoPacket = (ObjectSaleInfoPacket)Pack;
                        handlerObjectSaleInfo = OnObjectSaleInfo;
                        if (handlerObjectSaleInfo != null)
                        {
                            foreach (ObjectSaleInfoPacket.ObjectDataBlock d
                                    in objectSaleInfoPacket.ObjectData)
                            {
                                handlerObjectSaleInfo(this,
                                    objectSaleInfoPacket.AgentData.AgentID,
                                    objectSaleInfoPacket.AgentData.SessionID,
                                    d.LocalID,
                                    d.SaleType,
                                    d.SalePrice);
                            }
                        }
                        break;

                    case PacketType.ObjectBuy:
                        ObjectBuyPacket objectBuyPacket = (ObjectBuyPacket)Pack;
                        handlerObjectBuy = OnObjectBuy;
                        Console.WriteLine(objectBuyPacket.ToString());
                        if (handlerObjectBuy != null)
                        {
                            foreach (ObjectBuyPacket.ObjectDataBlock d
                                    in objectBuyPacket.ObjectData)
                            {
                                handlerObjectBuy(this,
                                    objectBuyPacket.AgentData.AgentID,
                                    objectBuyPacket.AgentData.SessionID,
                                    objectBuyPacket.AgentData.GroupID,
                                    objectBuyPacket.AgentData.CategoryID,
                                    d.ObjectLocalID,
                                    d.SaleType,
                                    d.SalePrice);
                            }
                        }
                        break;

                    #endregion

                    #region Script Packets

                    case PacketType.GetScriptRunning:
                        GetScriptRunningPacket scriptRunning = (GetScriptRunningPacket)Pack;
                        handlerGetScriptRunning = OnGetScriptRunning;
                        if (handlerGetScriptRunning != null)
                        {
                            handlerGetScriptRunning(this, scriptRunning.Script.ObjectID, scriptRunning.Script.ItemID);
                        }
                        break;

                    case PacketType.SetScriptRunning:
                        SetScriptRunningPacket setScriptRunning = (SetScriptRunningPacket)Pack;
                        handlerSetScriptRunning = OnSetScriptRunning;
                        if (handlerSetScriptRunning != null)
                        {
                            handlerSetScriptRunning(this, setScriptRunning.Script.ObjectID, setScriptRunning.Script.ItemID, setScriptRunning.Script.Running);
                        }
                        break;

                    case PacketType.ScriptReset:
                        ScriptResetPacket scriptResetPacket = (ScriptResetPacket)Pack;
                        handlerScriptReset = OnScriptReset;
                        if (handlerScriptReset != null)
                        {
                            handlerScriptReset(this, scriptResetPacket.Script.ObjectID, scriptResetPacket.Script.ItemID);
                        }
                        break;

                    #endregion

                    #region Gesture Managment

                    case PacketType.ActivateGestures:
                        ActivateGesturesPacket activateGesturePacket = (ActivateGesturesPacket)Pack;
                        handlerActivateGesture = OnActivateGesture;
                        if (handlerActivateGesture != null)
                        {
                            handlerActivateGesture(this,
                                activateGesturePacket.Data[0].AssetID,
                                                        activateGesturePacket.Data[0].ItemID);
                        }
                        else m_log.Error("Null pointer for activateGesture");

                        break;

                    case PacketType.DeactivateGestures:
                        DeactivateGesturesPacket deactivateGesturePacket = (DeactivateGesturesPacket)Pack;
                        handlerDeactivateGesture = OnDeactivateGesture;
                        if (handlerDeactivateGesture != null)
                        {
                            handlerDeactivateGesture(this, deactivateGesturePacket.Data[0].ItemID);
                        }
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

                    case PacketType.ViewerStats:
                        // TODO: handle this packet
                        //m_log.Warn("[CLIENT]: unhandled ViewerStats packet");
                        break;

                    case PacketType.CreateGroupRequest:
                        // TODO: handle this packet
                        //m_log.Warn("[CLIENT]: unhandled CreateGroupRequest packet");
                        break;
                    //case PacketType.GenericMessage:
                    // TODO: handle this packet
                    //m_log.Warn("[CLIENT]: unhandled GenericMessage packet");
                    //break;
                    case PacketType.MapItemRequest:
                        // TODO: handle this packet
                        //m_log.Warn("[CLIENT]: unhandled MapItemRequest packet");
                        break;
                    case PacketType.TransferAbort:
                        // TODO: handle this packet
                        //m_log.Warn("[CLIENT]: unhandled TransferAbort packet");
                        break;
                    case PacketType.MuteListRequest:
                        // TODO: handle this packet
                        //m_log.Warn("[CLIENT]: unhandled MuteListRequest packet");
                        break;
                    case PacketType.ParcelDwellRequest:
                        // TODO: handle this packet
                        //m_log.Warn("[CLIENT]: unhandled ParcelDwellRequest packet");
                        break;
                    case PacketType.UseCircuitCode:
                        // TODO: Don't display this one, we handle it at a lower level
                        //m_log.Warn("[CLIENT]: unhandled UseCircuitCode packet");
                        break;

                    case PacketType.AgentHeightWidth:
                        // TODO: handle this packet
                        //m_log.Warn("[CLIENT]: unhandled AgentHeightWidth packet");
                        break;
                    case PacketType.ObjectSpinStop:
                        // TODO: handle this packet
                        //m_log.Warn("[CLIENT]: unhandled ObjectSpinStop packet");
                        break;
                    case PacketType.SoundTrigger:
                        // TODO: handle this packet
                        //m_log.Warn("[CLIENT]: unhandled SoundTrigger packet");
                        break;
                    case PacketType.InventoryDescendents:
                        // TODO: handle this packet
                        //m_log.Warn("[CLIENT]: unhandled InventoryDescent packet");
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
            Primitive.TextureEntry ntex = new Primitive.TextureEntry(new UUID("89556747-24cb-43ed-920b-47caed15465f"));
            shape.TextureEntry = ntex.ToBytes();
            //shape.Textures = ntex;
            return shape;
        }

        /// <summary>
        /// Send the client an Estate message blue box pop-down with a single OK button
        /// </summary>
        /// <param name="FromAvatarID"></param>
        /// <param name="fromSessionID"></param>
        /// <param name="FromAvatarName"></param>
        /// <param name="Message"></param>
        public void SendBlueBoxMessage(UUID FromAvatarID, UUID fromSessionID, String FromAvatarName, String Message)
        {
            if (!ChildAgentStatus())
                SendInstantMessage(FromAvatarID, fromSessionID, Message, AgentId, SessionId, FromAvatarName, (byte)1, (uint)Util.UnixTimeSinceEpoch());

            //SendInstantMessage(FromAvatarID, fromSessionID, Message, AgentId, SessionId, FromAvatarName, (byte)21,(uint) Util.UnixTimeSinceEpoch());
        }

        public void SendLogoutPacket()
        {
            // I know this is a bit of a hack, however there are times when you don't
            // want to send this, but still need to do the rest of the shutdown process
            // this method gets called from the packet server..   which makes it practically
            // impossible to do any other way.

            if (m_SendLogoutPacketWhenClosing)
            {
                LogoutReplyPacket logReply = (LogoutReplyPacket)PacketPool.Instance.GetPacket(PacketType.LogoutReply);
                // TODO: don't create new blocks if recycling an old packet
                logReply.AgentData.AgentID = AgentId;
                logReply.AgentData.SessionID = SessionId;
                logReply.InventoryData = new LogoutReplyPacket.InventoryDataBlock[1];
                logReply.InventoryData[0] = new LogoutReplyPacket.InventoryDataBlock();
                logReply.InventoryData[0].ItemID = UUID.Zero;

                OutPacket(logReply, ThrottleOutPacketType.Task);
            }
        }

        public void SendHealth(float health)
        {
            HealthMessagePacket healthpacket = (HealthMessagePacket)PacketPool.Instance.GetPacket(PacketType.HealthMessage);
            healthpacket.HealthData.Health = health;
            OutPacket(healthpacket, ThrottleOutPacketType.Task);
        }

        public void SendAgentOnline(UUID[] agentIDs)
        {
            OnlineNotificationPacket onp = new OnlineNotificationPacket();
            OnlineNotificationPacket.AgentBlockBlock[] onpb = new OnlineNotificationPacket.AgentBlockBlock[agentIDs.Length];
            for (int i = 0; i < agentIDs.Length; i++)
            {
                OnlineNotificationPacket.AgentBlockBlock onpbl = new OnlineNotificationPacket.AgentBlockBlock();
                onpbl.AgentID = agentIDs[i];
                onpb[i] = onpbl;
            }
            onp.AgentBlock = onpb;
            onp.Header.Reliable = true;
            OutPacket(onp, ThrottleOutPacketType.Task);
        }

        public void SendAgentOffline(UUID[] agentIDs)
        {
            OfflineNotificationPacket offp = new OfflineNotificationPacket();
            OfflineNotificationPacket.AgentBlockBlock[] offpb = new OfflineNotificationPacket.AgentBlockBlock[agentIDs.Length];
            for (int i = 0; i < agentIDs.Length; i++)
            {
                OfflineNotificationPacket.AgentBlockBlock onpbl = new OfflineNotificationPacket.AgentBlockBlock();
                onpbl.AgentID = agentIDs[i];
                offpb[i] = onpbl;
            }
            offp.AgentBlock = offpb;
            offp.Header.Reliable = true;
            OutPacket(offp, ThrottleOutPacketType.Task);
        }

        public void SendSitResponse(UUID TargetID, Vector3 OffsetPos, Quaternion SitOrientation, bool autopilot,
                                        Vector3 CameraAtOffset, Vector3 CameraEyeOffset, bool ForceMouseLook)
        {
            AvatarSitResponsePacket avatarSitResponse = new AvatarSitResponsePacket();
            avatarSitResponse.SitObject.ID = TargetID;
            if (CameraAtOffset != Vector3.Zero)
            {
                avatarSitResponse.SitTransform.CameraAtOffset = CameraAtOffset;
                avatarSitResponse.SitTransform.CameraEyeOffset = CameraEyeOffset;
            }
            avatarSitResponse.SitTransform.ForceMouselook = ForceMouseLook;
            avatarSitResponse.SitTransform.AutoPilot = autopilot;
            avatarSitResponse.SitTransform.SitPosition = OffsetPos;
            avatarSitResponse.SitTransform.SitRotation = SitOrientation;

            OutPacket(avatarSitResponse, ThrottleOutPacketType.Task);
        }

        public void SendAdminResponse(UUID Token, uint AdminLevel)
        {
            GrantGodlikePowersPacket respondPacket = new GrantGodlikePowersPacket();
            GrantGodlikePowersPacket.GrantDataBlock gdb = new GrantGodlikePowersPacket.GrantDataBlock();
            GrantGodlikePowersPacket.AgentDataBlock adb = new GrantGodlikePowersPacket.AgentDataBlock();

            adb.AgentID = AgentId;
            adb.SessionID = SessionId; // More security
            gdb.GodLevel = (byte)AdminLevel;
            gdb.Token = Token;
            //respondPacket.AgentData = (GrantGodlikePowersPacket.AgentDataBlock)ablock;
            respondPacket.GrantData = gdb;
            respondPacket.AgentData = adb;
            OutPacket(respondPacket, ThrottleOutPacketType.Task);
        }

        public void SendGroupMembership(GroupData[] GroupMembership)
        {
            AgentGroupDataUpdatePacket Groupupdate = new AgentGroupDataUpdatePacket();
            AgentGroupDataUpdatePacket.GroupDataBlock[] Groups = new AgentGroupDataUpdatePacket.GroupDataBlock[GroupMembership.Length];
            for (int i = 0; i < GroupMembership.Length; i++)
            {
                AgentGroupDataUpdatePacket.GroupDataBlock Group = new AgentGroupDataUpdatePacket.GroupDataBlock();
                Group.AcceptNotices = GroupMembership[i].AcceptNotices;
                Group.Contribution = GroupMembership[i].contribution;
                Group.GroupID = GroupMembership[i].GroupID;
                Group.GroupInsigniaID = GroupMembership[i].GroupPicture;
                Group.GroupName = Utils.StringToBytes(GroupMembership[i].groupName);
                Group.GroupPowers = GroupMembership[i].groupPowers;
                Groups[i] = Group;
                Groupupdate.GroupData = Groups;

            }
            Groupupdate.AgentData.AgentID = AgentId;
            OutPacket(Groupupdate, ThrottleOutPacketType.Task);

        }
        public void SendGroupNameReply(UUID groupLLUID, string GroupName)
        {
            UUIDGroupNameReplyPacket pack = new UUIDGroupNameReplyPacket();
            UUIDGroupNameReplyPacket.UUIDNameBlockBlock[] uidnameblock = new UUIDGroupNameReplyPacket.UUIDNameBlockBlock[1];
            UUIDGroupNameReplyPacket.UUIDNameBlockBlock uidnamebloc = new UUIDGroupNameReplyPacket.UUIDNameBlockBlock();
            uidnamebloc.ID = groupLLUID;
            uidnamebloc.GroupName = Utils.StringToBytes(GroupName);
            uidnameblock[0] = uidnamebloc;
            pack.UUIDNameBlock = uidnameblock;
            OutPacket(pack, ThrottleOutPacketType.Task);
        }

        public void SendLandStatReply(uint reportType, uint requestFlags, uint resultCount, LandStatReportItem[] lsrpia)
        {
            LandStatReplyPacket lsrp = new LandStatReplyPacket();
            // LandStatReplyPacket.RequestDataBlock lsreqdpb = new LandStatReplyPacket.RequestDataBlock();
            LandStatReplyPacket.ReportDataBlock[] lsrepdba = new LandStatReplyPacket.ReportDataBlock[lsrpia.Length];
            //LandStatReplyPacket.ReportDataBlock lsrepdb = new LandStatReplyPacket.ReportDataBlock();
            // lsrepdb.
            lsrp.RequestData.ReportType = reportType;
            lsrp.RequestData.RequestFlags = requestFlags;
            lsrp.RequestData.TotalObjectCount = resultCount;
            for (int i = 0; i < lsrpia.Length; i++)
            {
                LandStatReplyPacket.ReportDataBlock lsrepdb = new LandStatReplyPacket.ReportDataBlock();
                lsrepdb.LocationX = lsrpia[i].LocationX;
                lsrepdb.LocationY = lsrpia[i].LocationY;
                lsrepdb.LocationZ = lsrpia[i].LocationZ;
                lsrepdb.Score = lsrpia[i].Score;
                lsrepdb.TaskID = lsrpia[i].TaskID;
                lsrepdb.TaskLocalID = lsrpia[i].TaskLocalID;
                lsrepdb.TaskName = Utils.StringToBytes(lsrpia[i].TaskName);
                lsrepdb.OwnerName = Utils.StringToBytes(lsrpia[i].OwnerName);
                lsrepdba[i] = lsrepdb;
            }
            lsrp.ReportData = lsrepdba;
            OutPacket(lsrp, ThrottleOutPacketType.Task);
        }

        public void SendScriptRunningReply(UUID objectID, UUID itemID, bool running)
        {
            ScriptRunningReplyPacket scriptRunningReply = new ScriptRunningReplyPacket();
            scriptRunningReply.Script.ObjectID = objectID;
            scriptRunningReply.Script.ItemID = itemID;
            scriptRunningReply.Script.Running = running;

            OutPacket(scriptRunningReply, ThrottleOutPacketType.Task);
        }

        public void SendAsset(AssetRequestToClient req)
        {

            //Console.WriteLine("sending asset " + req.RequestAssetID);
            TransferInfoPacket Transfer = new TransferInfoPacket();
            Transfer.TransferInfo.ChannelType = 2;
            Transfer.TransferInfo.Status = 0;
            Transfer.TransferInfo.TargetType = 0;
            if (req.AssetRequestSource == 2)
            {
                Transfer.TransferInfo.Params = new byte[20];
                Array.Copy(req.RequestAssetID.GetBytes(), 0, Transfer.TransferInfo.Params, 0, 16);
                int assType = (int)req.AssetInf.Type;
                Array.Copy(Helpers.IntToBytes(assType), 0, Transfer.TransferInfo.Params, 16, 4);
            }
            else if (req.AssetRequestSource == 3)
            {
                Transfer.TransferInfo.Params = req.Params;
                // Transfer.TransferInfo.Params = new byte[100];
                //Array.Copy(req.RequestUser.AgentId.GetBytes(), 0, Transfer.TransferInfo.Params, 0, 16);
                //Array.Copy(req.RequestUser.SessionId.GetBytes(), 0, Transfer.TransferInfo.Params, 16, 16);
            }
            Transfer.TransferInfo.Size = (int)req.AssetInf.Data.Length;
            Transfer.TransferInfo.TransferID = req.TransferRequestID;
            Transfer.Header.Zerocoded = true;
            OutPacket(Transfer, ThrottleOutPacketType.Asset);

            if (req.NumPackets == 1)
            {
                TransferPacketPacket TransferPacket = new TransferPacketPacket();
                TransferPacket.TransferData.Packet = 0;
                TransferPacket.TransferData.ChannelType = 2;
                TransferPacket.TransferData.TransferID = req.TransferRequestID;
                TransferPacket.TransferData.Data = req.AssetInf.Data;
                TransferPacket.TransferData.Status = 1;
                TransferPacket.Header.Zerocoded = true;
                OutPacket(TransferPacket, ThrottleOutPacketType.Asset);
            }
            else
            {
                int processedLength = 0;
                int maxChunkSize = Settings.MAX_PACKET_SIZE - 100;
                int packetNumber = 0;

                while (processedLength < req.AssetInf.Data.Length)
                {
                    TransferPacketPacket TransferPacket = new TransferPacketPacket();
                    TransferPacket.TransferData.Packet = packetNumber;
                    TransferPacket.TransferData.ChannelType = 2;
                    TransferPacket.TransferData.TransferID = req.TransferRequestID;

                    int chunkSize = Math.Min(req.AssetInf.Data.Length - processedLength, maxChunkSize);
                    byte[] chunk = new byte[chunkSize];
                    Array.Copy(req.AssetInf.Data, processedLength, chunk, 0, chunk.Length);

                    TransferPacket.TransferData.Data = chunk;

                    // 0 indicates more packets to come, 1 indicates last packet
                    if (req.AssetInf.Data.Length - processedLength > maxChunkSize)
                    {
                        TransferPacket.TransferData.Status = 0;
                    }
                    else
                    {
                        TransferPacket.TransferData.Status = 1;
                    }
                    TransferPacket.Header.Zerocoded = true;
                    OutPacket(TransferPacket, ThrottleOutPacketType.Asset);

                    processedLength += chunkSize;
                    packetNumber++;
                }
            }
        }

        public void SendTexture(AssetBase TextureAsset)
        {

        }

        public ClientInfo GetClientInfo()
        {
            ClientInfo info = m_PacketHandler.GetClientInfo();

            info.userEP = this.m_userEndPoint;
            info.proxyEP = this.m_proxyEndPoint;
            info.agentcircuit = new sAgentCircuitData(RequestClientInfo());

            return info;
        }

        public void SetClientInfo(ClientInfo info)
        {
            m_PacketHandler.SetClientInfo(info);
        }

        #region Media Parcel Members

        public void SendParcelMediaCommand(uint flags, ParcelMediaCommandEnum command, float time)
        {
            ParcelMediaCommandMessagePacket commandMessagePacket = new ParcelMediaCommandMessagePacket();
            commandMessagePacket.CommandBlock.Flags = (uint) flags;
            commandMessagePacket.CommandBlock.Command =(uint) command;
            commandMessagePacket.CommandBlock.Time = time;

            OutPacket(commandMessagePacket, ThrottleOutPacketType.Unknown);
        }

        public void SendParcelMediaUpdate(string mediaUrl, UUID mediaTextureID,
                                   byte autoScale, string mediaType, string mediaDesc, int mediaWidth, int mediaHeight,
                                   byte mediaLoop)
        {
            ParcelMediaUpdatePacket updatePacket = new ParcelMediaUpdatePacket();
            updatePacket.DataBlock.MediaURL = Utils.StringToBytes(mediaUrl);
            updatePacket.DataBlock.MediaID = mediaTextureID;
            updatePacket.DataBlock.MediaAutoScale = autoScale;

            updatePacket.DataBlockExtended.MediaType = Utils.StringToBytes(mediaType);
            updatePacket.DataBlockExtended.MediaDesc = Utils.StringToBytes(mediaDesc);
            updatePacket.DataBlockExtended.MediaWidth = mediaWidth;
            updatePacket.DataBlockExtended.MediaWidth = mediaHeight;
            updatePacket.DataBlockExtended.MediaLoop = mediaLoop;

            OutPacket(updatePacket, ThrottleOutPacketType.Unknown);
        }

        #endregion


        #region Camera

        public void SendSetFollowCamProperties (UUID objectID, SortedDictionary<int, float> parameters)
        {
            SetFollowCamPropertiesPacket packet = (SetFollowCamPropertiesPacket)PacketPool.Instance.GetPacket(PacketType.SetFollowCamProperties);
            packet.ObjectData.ObjectID = objectID;
            SetFollowCamPropertiesPacket.CameraPropertyBlock[] camPropBlock = new SetFollowCamPropertiesPacket.CameraPropertyBlock[parameters.Count];
            uint idx = 0;
            foreach (KeyValuePair<int, float> pair in parameters)
            {
                SetFollowCamPropertiesPacket.CameraPropertyBlock block = new SetFollowCamPropertiesPacket.CameraPropertyBlock();
                block.Type = pair.Key;
                block.Value = pair.Value;

                camPropBlock[idx++] = block;
            }
            packet.CameraProperty = camPropBlock;
            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendClearFollowCamProperties (UUID objectID)
        {
            ClearFollowCamPropertiesPacket packet = (ClearFollowCamPropertiesPacket)PacketPool.Instance.GetPacket(PacketType.ClearFollowCamProperties);
            packet.ObjectData.ObjectID = objectID;
            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        #endregion

        public void SendRegionHandle(UUID regionID, ulong handle) {
            RegionIDAndHandleReplyPacket reply = (RegionIDAndHandleReplyPacket)PacketPool.Instance.GetPacket(PacketType.RegionIDAndHandleReply);
            reply.ReplyBlock.RegionID = regionID;
            reply.ReplyBlock.RegionHandle = handle;
            OutPacket(reply, ThrottleOutPacketType.Land);
        }

        public void SendParcelInfo(RegionInfo info, LandData land, UUID parcelID, uint x, uint y)
        {
            ParcelInfoReplyPacket reply = (ParcelInfoReplyPacket)PacketPool.Instance.GetPacket(PacketType.ParcelInfoReply);
            reply.AgentData.AgentID = m_agentId;
            reply.Data.ParcelID = parcelID;
            reply.Data.OwnerID = land.OwnerID;
            reply.Data.Name = Utils.StringToBytes(land.Name);
            reply.Data.Desc = Utils.StringToBytes(land.Description);
            reply.Data.ActualArea = land.Area;
            reply.Data.BillableArea = land.Area; // TODO: what is this?

            // Bit 0: Mature, bit 7: on sale, other bits: no idea
            reply.Data.Flags = (byte)(
                ((land.Flags & (uint)Parcel.ParcelFlags.MaturePublish) != 0 ? (1 << 0) : 0) +
                ((land.Flags & (uint)Parcel.ParcelFlags.ForSale) != 0 ? (1 << 7) : 0));

            Vector3 pos = land.UserLocation;
            if (pos.Equals(Vector3.Zero))
            {
                pos = (land.AABBMax + land.AABBMin) * 0.5f;
            }
            reply.Data.GlobalX = info.RegionLocX * Constants.RegionSize + x;
            reply.Data.GlobalY = info.RegionLocY * Constants.RegionSize + y;
            reply.Data.GlobalZ = pos.Z;
            reply.Data.SimName = Utils.StringToBytes(info.RegionName);
            reply.Data.SnapshotID = land.SnapshotID;
            reply.Data.Dwell = 0; // TODO: not implemented yet
            reply.Data.SalePrice = land.SalePrice;
            reply.Data.AuctionID = (int)land.AuctionID;

            OutPacket(reply, ThrottleOutPacketType.Land);
        }

        public void KillEndDone()
        {
            KillPacket kp = new KillPacket();
            OutPacket(kp, ThrottleOutPacketType.Task | ThrottleOutPacketType.LowPriority);
        }
    }
}
