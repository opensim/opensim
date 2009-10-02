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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Agent.InternetRelayClientView.Server
{
    public delegate void OnIRCClientReadyDelegate(IRCClientView cv);

    public class IRCClientView : IClientAPI, IClientCore, IClientIPEndpoint
    {
        public event OnIRCClientReadyDelegate OnIRCReady;

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly TcpClient m_client;
        private readonly Scene m_scene;

        private UUID m_agentID = UUID.Random();

        private string m_username;
        private string m_nick;

        private bool m_hasNick = false;
        private bool m_hasUser = false;

        private bool m_connected = true;

        public IRCClientView(TcpClient client, Scene scene)
        {
            m_client = client;
            m_scene = scene;
            
            Thread loopThread = new Thread(InternalLoop);
            loopThread.Start();
        }

        private void SendServerCommand(string command)
        {
            SendCommand(":opensimircd " + command);
        }

        private void SendCommand(string command)
        {
            m_log.Info("[IRCd] Sending >>> " + command);

            byte[] buf = Encoding.UTF8.GetBytes(command + "\r\n");

            m_client.GetStream().BeginWrite(buf, 0, buf.Length, SendComplete, null);
        }

        private void SendComplete(IAsyncResult result)
        {
            m_log.Info("[IRCd] Send Complete.");
        }

        private string IrcRegionName
        {
            // I know &Channel is more technically correct, but people are used to seeing #Channel
            // Dont shoot me!
            get { return "#" + m_scene.RegionInfo.RegionName.Replace(" ", "-"); }
        }

        private void InternalLoop()
        {
            try
            {
                string strbuf = "";

                while (m_connected && m_client.Connected)
                {
                    byte[] buf = new byte[8]; // RFC1459 defines max message size as 512.

                    int count = m_client.GetStream().Read(buf, 0, buf.Length);
                    string line = Encoding.UTF8.GetString(buf, 0, count);

                    strbuf += line;

                    string message = ExtractMessage(strbuf);
                    if (message != null)
                    {
                        // Remove from buffer
                        strbuf = strbuf.Remove(0, message.Length);

                        m_log.Info("[IRCd] Recieving <<< " + message);
                        message = message.Trim();

                        // Extract command sequence
                        string command = ExtractCommand(message);
                        ProcessInMessage(message, command);
                    }
                    else
                    {
                        //m_log.Info("[IRCd] Recieved data, but not enough to make a message. BufLen is " + strbuf.Length +
                        //           "[" + strbuf + "]");
                        if (strbuf.Length == 0)
                        {
                            m_connected = false;
                            m_log.Info("[IRCd] Buffer zero, closing...");
                            if (OnDisconnectUser != null)
                                OnDisconnectUser();
                        }
                    }

                    Thread.Sleep(0);
                }
            }
            catch (IOException)
            {
                if (OnDisconnectUser != null)
                    OnDisconnectUser();

                m_log.Warn("[IRCd] Disconnected client.");
            }
            catch (SocketException)
            {
                if (OnDisconnectUser != null)
                    OnDisconnectUser();

                m_log.Warn("[IRCd] Disconnected client.");
            }
        }

        private void ProcessInMessage(string message, string command)
        {
            m_log.Info("[IRCd] Processing [MSG:" + message + "] [COM:" + command + "]");
            if (command != null)
            {
                switch (command)
                {
                    case "ADMIN":
                    case "AWAY":
                    case "CONNECT":
                    case "DIE":
                    case "ERROR":
                    case "INFO":
                    case "INVITE":
                    case "ISON":
                    case "KICK":
                    case "KILL":
                    case "LINKS":
                    case "LUSERS":
                    case "OPER":
                    case "PART":
                    case "REHASH":
                    case "SERVICE":
                    case "SERVLIST":
                    case "SERVER":
                    case "SQUERY":
                    case "SQUIT":
                    case "STATS":
                    case "SUMMON":
                    case "TIME":
                    case "TRACE":
                    case "VERSION":
                    case "WALLOPS":
                    case "WHOIS":
                    case "WHOWAS":
                        SendServerCommand("421 " + command + " :Command unimplemented");
                        break;

                    // Connection Commands
                    case "PASS":
                        break; // Ignore for now. I want to implement authentication later however.

                    case "JOIN":
                        IRC_SendReplyJoin();
                        break;

                    case "MODE":
                        IRC_SendReplyModeChannel();
                        break;

                    case "USER":
                        IRC_ProcessUser(message);
                        IRC_Ready();
                        break;

                    case "USERHOST":
                        string[] userhostArgs = ExtractParameters(message);
                        if (userhostArgs[0] == ":" + m_nick)
                        {
                            SendServerCommand("302 :" + m_nick + "=+" + m_nick + "@" +
                                        ((IPEndPoint) m_client.Client.RemoteEndPoint).Address);
                        }
                        break;
                    case "NICK":
                        IRC_ProcessNick(message);
                        IRC_Ready();

                        break;
                    case "TOPIC":
                        IRC_SendReplyTopic();
                        break;
                    case "USERS":
                        IRC_SendReplyUsers();
                        break;

                    case "LIST":
                        break; // TODO

                    case "MOTD":
                        IRC_SendMOTD();
                        break;

                    case "NOTICE": // TODO
                        break;

                    case "WHO": // TODO
                        IRC_SendNamesReply();
                        IRC_SendWhoReply();
                        break;

                    case "PING":
                        IRC_ProcessPing(message);
                        break;

                    // Special case, ignore this completely.
                    case "PONG":
                        break;

                    case "QUIT":
                        if (OnDisconnectUser != null)
                            OnDisconnectUser();
                        break;

                    case "NAMES":
                        IRC_SendNamesReply();
                        break;
                    case "PRIVMSG":
                        IRC_ProcessPrivmsg(message);
                        break;

                    default:
                        SendServerCommand("421 " + command + " :Unknown command");
                        break;
                }
            }
        }

        private void IRC_Ready()
        {
            if (m_hasUser && m_hasNick)
            {
                SendServerCommand("001 " + m_nick + " :Welcome to OpenSimulator IRCd");
                SendServerCommand("002 " + m_nick + " :Running OpenSimVersion");
                SendServerCommand("003 " + m_nick + " :This server was created over 9000 years ago");
                SendServerCommand("004 " + m_nick + " :opensimirc r1 aoOirw abeiIklmnoOpqrstv");
                SendServerCommand("251 " + m_nick + " :There are 0 users and 0 services on 1 servers");
                SendServerCommand("252 " + m_nick + " 0 :operators online");
                SendServerCommand("253 " + m_nick + " 0 :unknown connections");
                SendServerCommand("254 " + m_nick + " 1 :channels formed");
                SendServerCommand("255 " + m_nick + " :I have 1 users, 0 services  and 1 servers");
                SendCommand(":" + m_nick + " MODE " + m_nick + " :+i");
                SendCommand(":" + m_nick + " JOIN :" + IrcRegionName);

                // Rename to 'Real Name'
                SendCommand(":" + m_nick + " NICK :" + m_username.Replace(" ", ""));
                m_nick = m_username.Replace(" ", "");

                IRC_SendReplyJoin();
                IRC_SendChannelPrivmsg("System", "Welcome to OpenSimulator.");
                IRC_SendChannelPrivmsg("System", "You are in a maze of twisty little passages, all alike.");
                IRC_SendChannelPrivmsg("System", "It is pitch black. You are likely to be eaten by a grue.");

                if (OnIRCReady != null)
                    OnIRCReady(this);
            }
        }

        private void IRC_SendReplyJoin()
        {
            IRC_SendReplyTopic();
            IRC_SendNamesReply();
        }

        private void IRC_SendReplyModeChannel()
        {
            SendServerCommand("324 " + m_nick + " " + IrcRegionName + " +n");
            //SendCommand(":" + IrcRegionName + " MODE +n");
        }

        private void IRC_ProcessUser(string message)
        {
            string[] userArgs = ExtractParameters(message);
            // TODO: unused: string username = userArgs[0];
            // TODO: unused: string hostname = userArgs[1];
            // TODO: unused: string servername = userArgs[2];
            string realname = userArgs[3].Replace(":", "");

            m_username = realname;
            m_hasUser = true;
        }

        private void IRC_ProcessNick(string message)
        {
            string[] nickArgs = ExtractParameters(message);
            string nickname = nickArgs[0].Replace(":","");
            m_nick = nickname;
            m_hasNick = true;
        }

        private void IRC_ProcessPing(string message)
        {
            string[] pingArgs = ExtractParameters(message);
            string pingHost = pingArgs[0];
            SendCommand("PONG " + pingHost);
        }

        private void IRC_ProcessPrivmsg(string message)
        {
            string[] privmsgArgs = ExtractParameters(message);
            if (privmsgArgs[0] == IrcRegionName)
            {
                if (OnChatFromClient != null)
                {
                    OSChatMessage msg = new OSChatMessage();
                    msg.Sender = this;
                    msg.Channel = 0;
                    msg.From = this.Name;
                    msg.Message = privmsgArgs[1].Replace(":", "");
                    msg.Position = Vector3.Zero;
                    msg.Scene = m_scene;
                    msg.SenderObject = null;
                    msg.SenderUUID = this.AgentId;
                    msg.Type = ChatTypeEnum.Say;

                    OnChatFromClient(this, msg);
                }
            }
            else
            {
                // Handle as an IM, later.
            }
        }

        private void IRC_SendNamesReply()
        {
            List<EntityBase> users = m_scene.Entities.GetAllByType<ScenePresence>();

            foreach (EntityBase user in users)
            {
                SendServerCommand("353 " + m_nick + " = " + IrcRegionName + " :" + user.Name.Replace(" ", ""));
            }
            SendServerCommand("366 " + IrcRegionName + " :End of /NAMES list");
        }

        private void IRC_SendWhoReply()
        {
            List<EntityBase> users = m_scene.Entities.GetAllByType<ScenePresence>();

            foreach (EntityBase user in users)
            {
                /*SendServerCommand(String.Format("352 {0} {1} {2} {3} {4} {5} :0 {6}", IrcRegionName,
                                                user.Name.Replace(" ", ""), "nohost.com", "opensimircd",
                                                user.Name.Replace(" ", ""), 'H', user.Name));*/

                SendServerCommand("352 " + m_nick + " " + IrcRegionName + " n=" + user.Name.Replace(" ", "") + " fakehost.com " + user.Name.Replace(" ", "") + " H " + ":0 " + user.Name);

                //SendServerCommand("352 " + IrcRegionName + " " + user.Name.Replace(" ", "") + " nohost.com irc.opensimulator " + user.Name.Replace(" ", "") + " H " + ":0 " + user.Name);
            }
            SendServerCommand("315 " + m_nick + " " + IrcRegionName + " :End of /WHO list");
        }

        private void IRC_SendMOTD()
        {
            SendServerCommand("375 :- OpenSimulator Message of the day -");
            SendServerCommand("372 :- Hiya!");
            SendServerCommand("376 :End of /MOTD command");
        }

        private void IRC_SendReplyTopic()
        {
            SendServerCommand("332 " + IrcRegionName + " :OpenSimulator IRC Server");
        }

        private void IRC_SendReplyUsers()
        {
            List<EntityBase> users = m_scene.Entities.GetAllByType<ScenePresence>();

            SendServerCommand("392 :UserID   Terminal  Host");

            if (users.Count == 0)
            {
                SendServerCommand("395 :Nobody logged in");
                return;
            }

            foreach (EntityBase user in users)
            {
                char[] nom = new char[8];
                char[] term = "terminal_".ToCharArray();
                char[] host = "hostname".ToCharArray();

                string userName = user.Name.Replace(" ","");
                for (int i = 0; i < nom.Length; i++)
                {
                    if (userName.Length < i)
                        nom[i] = userName[i];
                    else
                        nom[i] = ' ';
                }

                SendServerCommand("393 :" + nom + " " + term + " " + host + "");
            }

            SendServerCommand("394 :End of users");
        }

        private static string ExtractMessage(string buffer)
        {
            int pos = buffer.IndexOf("\r\n");

            if (pos == -1)
                return null;

            string command = buffer.Substring(0, pos + 2);

            return command;
        }

        private static string ExtractCommand(string msg)
        {
            string[] msgs = msg.Split(' ');

            if (msgs.Length < 2)
            {
                m_log.Warn("[IRCd] Dropped msg: " + msg);
                return null;
            }

            if (msgs[0].StartsWith(":"))
                return msgs[1];

            return msgs[0];
        }

        private static string[] ExtractParameters(string msg)
        {
            string[] msgs = msg.Split(' ');
            List<string> parms = new List<string>(msgs.Length);

            bool foundCommand = false;
            string command = ExtractCommand(msg);


            for (int i=0;i<msgs.Length;i++)
            {
                if (msgs[i] == command)
                {
                    foundCommand = true;
                    continue;
                }

                if (foundCommand != true)
                    continue;

                if (i != 0 && msgs[i].StartsWith(":"))
                {
                    List<string> tmp = new List<string>();
                    for (int j=i;j<msgs.Length;j++)
                    {
                        tmp.Add(msgs[j]);
                    }
                    parms.Add(string.Join(" ", tmp.ToArray()));
                    break;
                }

                parms.Add(msgs[i]);
            }

            return parms.ToArray();
        }

        #region Implementation of IClientAPI

        public Vector3 StartPos
        {
            get { return new Vector3(((int)Constants.RegionSize * 0.5f), ((int)Constants.RegionSize * 0.5f), 50); }
            set { }
        }

        public bool TryGet<T>(out T iface)
        {
            iface = default(T);
            return false;
        }

        public T Get<T>()
        {
            return default(T);
        }

        public UUID AgentId
        {
            get { return m_agentID; }
        }

        public void Disconnect(string reason)
        {
            IRC_SendChannelPrivmsg("System", "You have been eaten by a grue. (" + reason + ")");

            m_connected = false;
            m_client.Close();
        }

        public void Disconnect()
        {
            IRC_SendChannelPrivmsg("System", "You have been eaten by a grue.");

            m_connected = false;
            m_client.Close();
        }

        public UUID SessionId
        {
            get { return m_agentID; }
        }

        public UUID SecureSessionId
        {
            get { return m_agentID; }
        }

        public UUID ActiveGroupId
        {
            get { return UUID.Zero; }
        }

        public string ActiveGroupName
        {
            get { return "IRCd User"; }
        }

        public ulong ActiveGroupPowers
        {
            get { return 0; }
        }

        public ulong GetGroupPowers(UUID groupID)
        {
            return 0;
        }

        public bool IsGroupMember(UUID GroupID)
        {
            return false;
        }

        public string FirstName
        {
            get
            {
                string[] names = m_username.Split(' ');
                return names[0];
            }
        }

        public string LastName
        {
            get
            {
                string[] names = m_username.Split(' ');
                if (names.Length > 1)
                    return names[1];
                return names[0];
            }
        }

        public IScene Scene
        {
            get { return m_scene; }
        }

        public int NextAnimationSequenceNumber
        {
            get { return 0; }
        }

        public string Name
        {
            get { return m_username; }
        }

        public bool IsActive
        {
            get { return true; }
            set { if (!value) Disconnect("IsActive Disconnected?"); }
        }

        public bool SendLogoutPacketWhenClosing
        {
            set { }
        }

        public uint CircuitCode
        {
            get { return (uint)Util.RandomClass.Next(0,int.MaxValue); }
        }
#pragma warning disable 67
        public event GenericMessage OnGenericMessage;
        public event ImprovedInstantMessage OnInstantMessage;
        public event ChatMessage OnChatFromClient;
        public event TextureRequest OnRequestTexture;
        public event RezObject OnRezObject;
        public event ModifyTerrain OnModifyTerrain;
        public event BakeTerrain OnBakeTerrain;
        public event EstateChangeInfo OnEstateChangeInfo;
        public event SetAppearance OnSetAppearance;
        public event AvatarNowWearing OnAvatarNowWearing;
        public event RezSingleAttachmentFromInv OnRezSingleAttachmentFromInv;
        public event RezMultipleAttachmentsFromInv OnRezMultipleAttachmentsFromInv;
        public event UUIDNameRequest OnDetachAttachmentIntoInv;
        public event ObjectAttach OnObjectAttach;
        public event ObjectDeselect OnObjectDetach;
        public event ObjectDrop OnObjectDrop;
        public event StartAnim OnStartAnim;
        public event StopAnim OnStopAnim;
        public event LinkObjects OnLinkObjects;
        public event DelinkObjects OnDelinkObjects;
        public event RequestMapBlocks OnRequestMapBlocks;
        public event RequestMapName OnMapNameRequest;
        public event TeleportLocationRequest OnTeleportLocationRequest;
        public event DisconnectUser OnDisconnectUser;
        public event RequestAvatarProperties OnRequestAvatarProperties;
        public event SetAlwaysRun OnSetAlwaysRun;
        public event TeleportLandmarkRequest OnTeleportLandmarkRequest;
        public event DeRezObject OnDeRezObject;
        public event Action<IClientAPI> OnRegionHandShakeReply;
        public event GenericCall2 OnRequestWearables;
        public event GenericCall2 OnCompleteMovementToRegion;
        public event UpdateAgent OnAgentUpdate;
        public event AgentRequestSit OnAgentRequestSit;
        public event AgentSit OnAgentSit;
        public event AvatarPickerRequest OnAvatarPickerRequest;
        public event Action<IClientAPI> OnRequestAvatarsData;
        public event AddNewPrim OnAddPrim;
        public event FetchInventory OnAgentDataUpdateRequest;
        public event TeleportLocationRequest OnSetStartLocationRequest;
        public event RequestGodlikePowers OnRequestGodlikePowers;
        public event GodKickUser OnGodKickUser;
        public event ObjectDuplicate OnObjectDuplicate;
        public event ObjectDuplicateOnRay OnObjectDuplicateOnRay;
        public event GrabObject OnGrabObject;
        public event DeGrabObject OnDeGrabObject;
        public event MoveObject OnGrabUpdate;
        public event SpinStart OnSpinStart;
        public event SpinObject OnSpinUpdate;
        public event SpinStop OnSpinStop;
        public event UpdateShape OnUpdatePrimShape;
        public event ObjectExtraParams OnUpdateExtraParams;
        public event ObjectRequest OnObjectRequest;
        public event ObjectSelect OnObjectSelect;
        public event ObjectDeselect OnObjectDeselect;
        public event GenericCall7 OnObjectDescription;
        public event GenericCall7 OnObjectName;
        public event GenericCall7 OnObjectClickAction;
        public event GenericCall7 OnObjectMaterial;
        public event RequestObjectPropertiesFamily OnRequestObjectPropertiesFamily;
        public event UpdatePrimFlags OnUpdatePrimFlags;
        public event UpdatePrimTexture OnUpdatePrimTexture;
        public event UpdateVector OnUpdatePrimGroupPosition;
        public event UpdateVector OnUpdatePrimSinglePosition;
        public event UpdatePrimRotation OnUpdatePrimGroupRotation;
        public event UpdatePrimSingleRotation OnUpdatePrimSingleRotation;
        public event UpdatePrimSingleRotationPosition OnUpdatePrimSingleRotationPosition;
        public event UpdatePrimGroupRotation OnUpdatePrimGroupMouseRotation;
        public event UpdateVector OnUpdatePrimScale;
        public event UpdateVector OnUpdatePrimGroupScale;
        public event StatusChange OnChildAgentStatus;
        public event GenericCall2 OnStopMovement;
        public event Action<UUID> OnRemoveAvatar;
        public event ObjectPermissions OnObjectPermissions;
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
        public event RemoveInventoryFolder OnRemoveInventoryFolder;
        public event RemoveInventoryItem OnRemoveInventoryItem;
        public event UDPAssetUploadRequest OnAssetUploadRequest;
        public event XferReceive OnXferReceive;
        public event RequestXfer OnRequestXfer;
        public event ConfirmXfer OnConfirmXfer;
        public event AbortXfer OnAbortXfer;
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
        public event ParcelGodForceOwner OnParcelGodForceOwner;
        public event ParcelReclaim OnParcelReclaim;
        public event ParcelReturnObjectsRequest OnParcelReturnObjectsRequest;
        public event ParcelDeedToGroup OnParcelDeedToGroup;
        public event RegionInfoRequest OnRegionInfoRequest;
        public event EstateCovenantRequest OnEstateCovenantRequest;
        public event FriendActionDelegate OnApproveFriendRequest;
        public event FriendActionDelegate OnDenyFriendRequest;
        public event FriendshipTermination OnTerminateFriendship;
        public event MoneyTransferRequest OnMoneyTransferRequest;
        public event EconomyDataRequest OnEconomyDataRequest;
        public event MoneyBalanceRequest OnMoneyBalanceRequest;
        public event UpdateAvatarProperties OnUpdateAvatarProperties;
        public event ParcelBuy OnParcelBuy;
        public event RequestPayPrice OnRequestPayPrice;
        public event ObjectSaleInfo OnObjectSaleInfo;
        public event ObjectBuy OnObjectBuy;
        public event BuyObjectInventory OnBuyObjectInventory;
        public event RequestTerrain OnRequestTerrain;
        public event RequestTerrain OnUploadTerrain;
        public event ObjectIncludeInSearch OnObjectIncludeInSearch;
        public event UUIDNameRequest OnTeleportHomeRequest;
        public event ScriptAnswer OnScriptAnswer;
        public event AgentSit OnUndo;
        public event ForceReleaseControls OnForceReleaseControls;
        public event GodLandStatRequest OnLandStatRequest;
        public event DetailedEstateDataRequest OnDetailedEstateDataRequest;
        public event SetEstateFlagsRequest OnSetEstateFlagsRequest;
        public event SetEstateTerrainBaseTexture OnSetEstateTerrainBaseTexture;
        public event SetEstateTerrainDetailTexture OnSetEstateTerrainDetailTexture;
        public event SetEstateTerrainTextureHeights OnSetEstateTerrainTextureHeights;
        public event CommitEstateTerrainTextureRequest OnCommitEstateTerrainTextureRequest;
        public event SetRegionTerrainSettings OnSetRegionTerrainSettings;
        public event EstateRestartSimRequest OnEstateRestartSimRequest;
        public event EstateChangeCovenantRequest OnEstateChangeCovenantRequest;
        public event UpdateEstateAccessDeltaRequest OnUpdateEstateAccessDeltaRequest;
        public event SimulatorBlueBoxMessageRequest OnSimulatorBlueBoxMessageRequest;
        public event EstateBlueBoxMessageRequest OnEstateBlueBoxMessageRequest;
        public event EstateDebugRegionRequest OnEstateDebugRegionRequest;
        public event EstateTeleportOneUserHomeRequest OnEstateTeleportOneUserHomeRequest;
        public event EstateTeleportAllUsersHomeRequest OnEstateTeleportAllUsersHomeRequest;
        public event UUIDNameRequest OnUUIDGroupNameRequest;
        public event RegionHandleRequest OnRegionHandleRequest;
        public event ParcelInfoRequest OnParcelInfoRequest;
        public event RequestObjectPropertiesFamily OnObjectGroupRequest;
        public event ScriptReset OnScriptReset;
        public event GetScriptRunning OnGetScriptRunning;
        public event SetScriptRunning OnSetScriptRunning;
        public event UpdateVector OnAutoPilotGo;
        public event TerrainUnacked OnUnackedTerrain;
        public event ActivateGesture OnActivateGesture;
        public event DeactivateGesture OnDeactivateGesture;
        public event ObjectOwner OnObjectOwner;
        public event DirPlacesQuery OnDirPlacesQuery;
        public event DirFindQuery OnDirFindQuery;
        public event DirLandQuery OnDirLandQuery;
        public event DirPopularQuery OnDirPopularQuery;
        public event DirClassifiedQuery OnDirClassifiedQuery;
        public event EventInfoRequest OnEventInfoRequest;
        public event ParcelSetOtherCleanTime OnParcelSetOtherCleanTime;
        public event MapItemRequest OnMapItemRequest;
        public event OfferCallingCard OnOfferCallingCard;
        public event AcceptCallingCard OnAcceptCallingCard;
        public event DeclineCallingCard OnDeclineCallingCard;
        public event SoundTrigger OnSoundTrigger;
        public event StartLure OnStartLure;
        public event TeleportLureRequest OnTeleportLureRequest;
        public event NetworkStats OnNetworkStatsUpdate;
        public event ClassifiedInfoRequest OnClassifiedInfoRequest;
        public event ClassifiedInfoUpdate OnClassifiedInfoUpdate;
        public event ClassifiedDelete OnClassifiedDelete;
        public event ClassifiedDelete OnClassifiedGodDelete;
        public event EventNotificationAddRequest OnEventNotificationAddRequest;
        public event EventNotificationRemoveRequest OnEventNotificationRemoveRequest;
        public event EventGodDelete OnEventGodDelete;
        public event ParcelDwellRequest OnParcelDwellRequest;
        public event UserInfoRequest OnUserInfoRequest;
        public event UpdateUserInfo OnUpdateUserInfo;
        public event RetrieveInstantMessages OnRetrieveInstantMessages;
        public event PickDelete OnPickDelete;
        public event PickGodDelete OnPickGodDelete;
        public event PickInfoUpdate OnPickInfoUpdate;
        public event AvatarNotesUpdate OnAvatarNotesUpdate;
        public event MuteListRequest OnMuteListRequest;
        public event PlacesQuery OnPlacesQuery;
#pragma warning restore 67

        public void SetDebugPacketLevel(int newDebug)
        {
            
        }

        public void InPacket(object NewPack)
        {
            
        }

        public void ProcessInPacket(Packet NewPack)
        {
            
        }

        public void Close(bool ShutdownCircuit)
        {
            Disconnect();
        }

        public void Kick(string message)
        {
            Disconnect(message);
        }

        public void Start()
        {
            Scene.AddNewClient(this);

            // Mimicking LLClientView which gets always set appearance from client.
            Scene scene = (Scene)Scene;
            AvatarAppearance appearance;
            scene.GetAvatarAppearance(this, out appearance);
            OnSetAppearance(appearance.Texture, (byte[])appearance.VisualParams.Clone());
        }

        public void SendRegionHandshake(RegionInfo regionInfo, RegionHandshakeArgs args)
        {
            m_log.Info("[IRCd ClientStack] Completing Handshake to Region");

            if (OnRegionHandShakeReply != null)
            {
                OnRegionHandShakeReply(this);
            }

            if (OnCompleteMovementToRegion != null)
            {
                OnCompleteMovementToRegion();
            }
        }

        public void Stop()
        {
            Disconnect();
        }

        public void SendWearables(AvatarWearable[] wearables, int serial)
        {
            
        }

        public void SendAppearance(UUID agentID, byte[] visualParams, byte[] textureEntry)
        {
            
        }

        public void SendStartPingCheck(byte seq)
        {
            
        }

        public void SendKillObject(ulong regionHandle, uint localID)
        {
            
        }

        public void SendAnimations(UUID[] animID, int[] seqs, UUID sourceAgentId, UUID[] objectIDs)
        {
            
        }

        public void SendChatMessage(string message, byte type, Vector3 fromPos, string fromName, UUID fromAgentID, byte source, byte audible)
        {
            if (audible > 0 && message.Length > 0)
                IRC_SendChannelPrivmsg(fromName, message);
        }

        private void IRC_SendChannelPrivmsg(string fromName, string message)
        {
            SendCommand(":" + fromName.Replace(" ", "") + " PRIVMSG " + IrcRegionName + " :" + message);
        }

        public void SendInstantMessage(GridInstantMessage im)
        {
            // TODO
        }

        public void SendGenericMessage(string method, List<string> message)
        {
            
        }

        public void SendLayerData(float[] map)
        {
            
        }

        public void SendLayerData(int px, int py, float[] map)
        {
            
        }

        public void SendWindData(Vector2[] windSpeeds)
        {
            
        }

        public void SendCloudData(float[] cloudCover)
        {
            
        }

        public void MoveAgentIntoRegion(RegionInfo regInfo, Vector3 pos, Vector3 look)
        {
            
        }

        public void InformClientOfNeighbour(ulong neighbourHandle, IPEndPoint neighbourExternalEndPoint)
        {
            
        }

        public AgentCircuitData RequestClientInfo()
        {
            return new AgentCircuitData();
        }

        public void CrossRegion(ulong newRegionHandle, Vector3 pos, Vector3 lookAt, IPEndPoint newRegionExternalEndPoint, string capsURL)
        {
            
        }

        public void SendMapBlock(List<MapBlockData> mapBlocks, uint flag)
        {
            
        }

        public void SendLocalTeleport(Vector3 position, Vector3 lookAt, uint flags)
        {
            
        }

        public void SendRegionTeleport(ulong regionHandle, byte simAccess, IPEndPoint regionExternalEndPoint, uint locationID, uint flags, string capsURL)
        {
            
        }

        public void SendTeleportFailed(string reason)
        {
            
        }

        public void SendTeleportLocationStart()
        {
            
        }

        public void SendMoneyBalance(UUID transaction, bool success, byte[] description, int balance)
        {
            
        }

        public void SendPayPrice(UUID objectID, int[] payPrice)
        {
            
        }

        public void SendAvatarData(ulong regionHandle, string firstName, string lastName, string grouptitle, UUID avatarID, uint avatarLocalID, Vector3 Pos, byte[] textureEntry, uint parentID, Quaternion rotation)
        {
            
        }

        public void SendAvatarTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, Vector3 position, Vector3 velocity, Quaternion rotation, UUID agentid)
        {
            
        }

        public void SendCoarseLocationUpdate(List<UUID> users, List<Vector3> CoarseLocations)
        {
            
        }

        public void AttachObject(uint localID, Quaternion rotation, byte attachPoint, UUID ownerID)
        {
            
        }

        public void SetChildAgentThrottle(byte[] throttle)
        {
            
        }

        public void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape, Vector3 pos, Vector3 vel, Vector3 acc, Quaternion rotation, Vector3 rvel, uint flags, UUID objectID, UUID ownerID, string text, byte[] color, uint parentID, byte[] particleSystem, byte clickAction, byte material, byte[] textureanim, bool attachment, uint AttachPoint, UUID AssetId, UUID SoundId, double SoundVolume, byte SoundFlags, double SoundRadius)
        {
            
        }

        public void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape, Vector3 pos, Vector3 vel, Vector3 acc, Quaternion rotation, Vector3 rvel, uint flags, UUID objectID, UUID ownerID, string text, byte[] color, uint parentID, byte[] particleSystem, byte clickAction, byte material)
        {
            
        }

        public void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 rotationalvelocity, byte state, UUID AssetId, UUID owner, int attachPoint)
        {
            
        }

        public void SendInventoryFolderDetails(UUID ownerID, UUID folderID, List<InventoryItemBase> items, List<InventoryFolderBase> folders, bool fetchFolders, bool fetchItems)
        {
            
        }

        public void FlushPrimUpdates()
        {
            
        }

        public void SendInventoryItemDetails(UUID ownerID, InventoryItemBase item)
        {
            
        }

        public void SendInventoryItemCreateUpdate(InventoryItemBase Item, uint callbackId)
        {
            
        }

        public void SendRemoveInventoryItem(UUID itemID)
        {
            
        }

        public void SendTakeControls(int controls, bool passToAgent, bool TakeControls)
        {
            
        }

        public void SendTaskInventory(UUID taskID, short serial, byte[] fileName)
        {
            
        }

        public void SendBulkUpdateInventory(InventoryNodeBase node)
        {
            
        }

        public void SendXferPacket(ulong xferID, uint packet, byte[] data)
        {
            
        }

        public void SendEconomyData(float EnergyEfficiency, int ObjectCapacity, int ObjectCount, int PriceEnergyUnit, int PriceGroupCreate, int PriceObjectClaim, float PriceObjectRent, float PriceObjectScaleFactor, int PriceParcelClaim, float PriceParcelClaimFactor, int PriceParcelRent, int PricePublicObjectDecay, int PricePublicObjectDelete, int PriceRentLight, int PriceUpload, int TeleportMinPrice, float TeleportPriceExponent)
        {
            
        }

        public void SendAvatarPickerReply(AvatarPickerReplyAgentDataArgs AgentData, List<AvatarPickerReplyDataArgs> Data)
        {
            
        }

        public void SendAgentDataUpdate(UUID agentid, UUID activegroupid, string firstname, string lastname, ulong grouppowers, string groupname, string grouptitle)
        {
            
        }

        public void SendPreLoadSound(UUID objectID, UUID ownerID, UUID soundID)
        {
            
        }

        public void SendPlayAttachedSound(UUID soundID, UUID objectID, UUID ownerID, float gain, byte flags)
        {
            
        }

        public void SendTriggeredSound(UUID soundID, UUID ownerID, UUID objectID, UUID parentID, ulong handle, Vector3 position, float gain)
        {
            
        }

        public void SendAttachedSoundGainChange(UUID objectID, float gain)
        {
            
        }

        public void SendNameReply(UUID profileId, string firstname, string lastname)
        {
            
        }

        public void SendAlertMessage(string message)
        {
            IRC_SendChannelPrivmsg("Alert",message);
        }

        public void SendAgentAlertMessage(string message, bool modal)
        {
            
        }

        public void SendLoadURL(string objectname, UUID objectID, UUID ownerID, bool groupOwned, string message, string url)
        {
            IRC_SendChannelPrivmsg(objectname,url);
        }

        public void SendDialog(string objectname, UUID objectID, string ownerFirstName, string ownerLastName, string msg, UUID textureID, int ch, string[] buttonlabels)
        {
            
        }

        public bool AddMoney(int debit)
        {
            return true;
        }

        public void SendSunPos(Vector3 sunPos, Vector3 sunVel, ulong CurrentTime, uint SecondsPerSunCycle, uint SecondsPerYear, float OrbitalPosition)
        {
            
        }

        public void SendViewerEffect(ViewerEffectPacket.EffectBlock[] effectBlocks)
        {
            
        }

        public void SendViewerTime(int phase)
        {
            
        }

        public UUID GetDefaultAnimation(string name)
        {
            return UUID.Zero;
        }

        public void SendAvatarProperties(UUID avatarID, string aboutText, string bornOn, byte[] charterMember, string flAbout, uint flags, UUID flImageID, UUID imageID, string profileURL, UUID partnerID)
        {
            
        }

        public void SendScriptQuestion(UUID taskID, string taskName, string ownerName, UUID itemID, int question)
        {
            
        }

        public void SendHealth(float health)
        {
            
        }

        public void SendEstateManagersList(UUID invoice, UUID[] EstateManagers, uint estateID)
        {
            
        }

        public void SendBannedUserList(UUID invoice, EstateBan[] banlist, uint estateID)
        {
            
        }

        public void SendRegionInfoToEstateMenu(RegionInfoForEstateMenuArgs args)
        {
            
        }

        public void SendEstateCovenantInformation(UUID covenant)
        {
            
        }

        public void SendDetailedEstateData(UUID invoice, string estateName, uint estateID, uint parentEstate, uint estateFlags, uint sunPosition, UUID covenant, string abuseEmail, UUID estateOwner)
        {
            
        }

        public void SendLandProperties(int sequence_id, bool snap_selection, int request_result, LandData landData, float simObjectBonusFactor, int parcelObjectCapacity, int simObjectCapacity, uint regionFlags)
        {
            
        }

        public void SendLandAccessListData(List<UUID> avatars, uint accessFlag, int localLandID)
        {
            
        }

        public void SendForceClientSelectObjects(List<uint> objectIDs)
        {
            
        }

        public void SendCameraConstraint(Vector4 ConstraintPlane)
        {

        }

        public void SendLandObjectOwners(LandData land, List<UUID> groups, Dictionary<UUID, int> ownersAndCount)
        {
            
        }

        public void SendLandParcelOverlay(byte[] data, int sequence_id)
        {
            
        }

        public void SendParcelMediaCommand(uint flags, ParcelMediaCommandEnum command, float time)
        {
            
        }

        public void SendParcelMediaUpdate(string mediaUrl, UUID mediaTextureID, byte autoScale, string mediaType, string mediaDesc, int mediaWidth, int mediaHeight, byte mediaLoop)
        {
            
        }

        public void SendAssetUploadCompleteMessage(sbyte AssetType, bool Success, UUID AssetFullID)
        {
            
        }

        public void SendConfirmXfer(ulong xferID, uint PacketID)
        {
            
        }

        public void SendXferRequest(ulong XferID, short AssetType, UUID vFileID, byte FilePath, byte[] FileName)
        {
            
        }

        public void SendInitiateDownload(string simFileName, string clientFileName)
        {
            
        }

        public void SendImageFirstPart(ushort numParts, UUID ImageUUID, uint ImageSize, byte[] ImageData, byte imageCodec)
        {
            
        }

        public void SendImageNextPart(ushort partNumber, UUID imageUuid, byte[] imageData)
        {
            
        }

        public void SendImageNotFound(UUID imageid)
        {
            
        }

        public void SendShutdownConnectionNotice()
        {
            // TODO
        }

        public void SendSimStats(SimStats stats)
        {
            
        }

        public void SendObjectPropertiesFamilyData(uint RequestFlags, UUID ObjectUUID, UUID OwnerID, UUID GroupID, uint BaseMask, uint OwnerMask, uint GroupMask, uint EveryoneMask, uint NextOwnerMask, int OwnershipCost, byte SaleType, int SalePrice, uint Category, UUID LastOwnerID, string ObjectName, string Description)
        {
            
        }

        public void SendObjectPropertiesReply(UUID ItemID, ulong CreationDate, UUID CreatorUUID, UUID FolderUUID, UUID FromTaskUUID, UUID GroupUUID, short InventorySerial, UUID LastOwnerUUID, UUID ObjectUUID, UUID OwnerUUID, string TouchTitle, byte[] TextureID, string SitTitle, string ItemName, string ItemDescription, uint OwnerMask, uint NextOwnerMask, uint GroupMask, uint EveryoneMask, uint BaseMask, byte saleType, int salePrice)
        {
            
        }

        public void SendAgentOffline(UUID[] agentIDs)
        {
            
        }

        public void SendAgentOnline(UUID[] agentIDs)
        {
            
        }

        public void SendSitResponse(UUID TargetID, Vector3 OffsetPos, Quaternion SitOrientation, bool autopilot, Vector3 CameraAtOffset, Vector3 CameraEyeOffset, bool ForceMouseLook)
        {
            
        }

        public void SendAdminResponse(UUID Token, uint AdminLevel)
        {
            
        }

        public void SendGroupMembership(GroupMembershipData[] GroupMembership)
        {
            
        }

        public void SendGroupNameReply(UUID groupLLUID, string GroupName)
        {
            
        }

        public void SendJoinGroupReply(UUID groupID, bool success)
        {
            
        }

        public void SendEjectGroupMemberReply(UUID agentID, UUID groupID, bool success)
        {
            
        }

        public void SendLeaveGroupReply(UUID groupID, bool success)
        {
            
        }

        public void SendCreateGroupReply(UUID groupID, bool success, string message)
        {
            
        }

        public void SendLandStatReply(uint reportType, uint requestFlags, uint resultCount, LandStatReportItem[] lsrpia)
        {
            
        }

        public void SendScriptRunningReply(UUID objectID, UUID itemID, bool running)
        {
            
        }

        public void SendAsset(AssetRequestToClient req)
        {
            
        }

        public void SendTexture(AssetBase TextureAsset)
        {
            
        }

        public byte[] GetThrottlesPacked(float multiplier)
        {
            return new byte[0];
        }

        public event ViewerEffectEventHandler OnViewerEffect;
        public event Action<IClientAPI> OnLogout;
        public event Action<IClientAPI> OnConnectionClosed;

        public void SendBlueBoxMessage(UUID FromAvatarID, string FromAvatarName, string Message)
        {
            IRC_SendChannelPrivmsg(FromAvatarName, Message);
        }

        public void SendLogoutPacket()
        {
            Disconnect();
        }

        public EndPoint GetClientEP()
        {
            return null;
        }

        public ClientInfo GetClientInfo()
        {
            return new ClientInfo();
        }

        public void SetClientInfo(ClientInfo info)
        {
            
        }

        public void SetClientOption(string option, string value)
        {
            
        }

        public string GetClientOption(string option)
        {
            return String.Empty;
        }

        public void Terminate()
        {
            Disconnect();
        }

        public void SendSetFollowCamProperties(UUID objectID, SortedDictionary<int, float> parameters)
        {
            
        }

        public void SendClearFollowCamProperties(UUID objectID)
        {
            
        }

        public void SendRegionHandle(UUID regoinID, ulong handle)
        {
            
        }

        public void SendParcelInfo(RegionInfo info, LandData land, UUID parcelID, uint x, uint y)
        {
            
        }

        public void SendScriptTeleportRequest(string objName, string simName, Vector3 pos, Vector3 lookAt)
        {
            
        }

        public void SendDirPlacesReply(UUID queryID, DirPlacesReplyData[] data)
        {
            
        }

        public void SendDirPeopleReply(UUID queryID, DirPeopleReplyData[] data)
        {
            
        }

        public void SendDirEventsReply(UUID queryID, DirEventsReplyData[] data)
        {
            
        }

        public void SendDirGroupsReply(UUID queryID, DirGroupsReplyData[] data)
        {
            
        }

        public void SendDirClassifiedReply(UUID queryID, DirClassifiedReplyData[] data)
        {
            
        }

        public void SendDirLandReply(UUID queryID, DirLandReplyData[] data)
        {
            
        }

        public void SendDirPopularReply(UUID queryID, DirPopularReplyData[] data)
        {
            
        }

        public void SendEventInfoReply(EventData info)
        {
            
        }

        public void SendMapItemReply(mapItemReply[] replies, uint mapitemtype, uint flags)
        {
            
        }

        public void SendAvatarGroupsReply(UUID avatarID, GroupMembershipData[] data)
        {
            
        }

        public void SendOfferCallingCard(UUID srcID, UUID transactionID)
        {
            
        }

        public void SendAcceptCallingCard(UUID transactionID)
        {
            
        }

        public void SendDeclineCallingCard(UUID transactionID)
        {
            
        }

        public void SendTerminateFriend(UUID exFriendID)
        {
            
        }

        public void SendAvatarClassifiedReply(UUID targetID, UUID[] classifiedID, string[] name)
        {
            
        }

        public void SendClassifiedInfoReply(UUID classifiedID, UUID creatorID, uint creationDate, uint expirationDate, uint category, string name, string description, UUID parcelID, uint parentEstate, UUID snapshotID, string simName, Vector3 globalPos, string parcelName, byte classifiedFlags, int price)
        {
            
        }

        public void SendAgentDropGroup(UUID groupID)
        {
            
        }

        public void RefreshGroupMembership()
        {
            
        }

        public void SendAvatarNotesReply(UUID targetID, string text)
        {
            
        }

        public void SendAvatarPicksReply(UUID targetID, Dictionary<UUID, string> picks)
        {
            
        }

        public void SendPickInfoReply(UUID pickID, UUID creatorID, bool topPick, UUID parcelID, string name, string desc, UUID snapshotID, string user, string originalName, string simName, Vector3 posGlobal, int sortOrder, bool enabled)
        {
            
        }

        public void SendAvatarClassifiedReply(UUID targetID, Dictionary<UUID, string> classifieds)
        {
            
        }

        public void SendParcelDwellReply(int localID, UUID parcelID, float dwell)
        {
            
        }

        public void SendUserInfoReply(bool imViaEmail, bool visible, string email)
        {
            
        }

        public void SendUseCachedMuteList()
        {
            
        }

        public void SendMuteListUpdate(string filename)
        {
            
        }

        public void KillEndDone()
        {
            
        }

        public bool AddGenericPacketHandler(string MethodName, GenericMessage handler)
        {
            return true;
        }

        #endregion

        #region Implementation of IClientIPEndpoint

        public IPAddress EndPoint
        {
            get { return ((IPEndPoint) m_client.Client.RemoteEndPoint).Address; }
        }

        #endregion

        public void SendRebakeAvatarTextures(UUID textureID)
        {
        }
    }
}
