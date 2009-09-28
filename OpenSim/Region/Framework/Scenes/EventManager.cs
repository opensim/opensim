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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using Caps=OpenSim.Framework.Capabilities.Caps;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// A class for triggering remote scene events.
    /// </summary>
    public class EventManager
    {
        public delegate void OnFrameDelegate();

        public event OnFrameDelegate OnFrame;

        public delegate void ClientMovement(ScenePresence client);

        public event ClientMovement OnClientMovement;

        public delegate void OnTerrainTickDelegate();

        public event OnTerrainTickDelegate OnTerrainTick;

        public delegate void OnBackupDelegate(IRegionDataStore datastore, bool forceBackup);

        public event OnBackupDelegate OnBackup;

        public delegate void OnClientConnectCoreDelegate(IClientCore client);

        public event OnClientConnectCoreDelegate OnClientConnect;

        public delegate void OnNewClientDelegate(IClientAPI client);

        /// <summary>
        /// Deprecated in favour of OnClientConnect.
        /// Will be marked Obsolete after IClientCore has 100% of IClientAPI interfaces.
        /// </summary>
        public event OnNewClientDelegate OnNewClient;

        public delegate void OnNewPresenceDelegate(ScenePresence presence);

        public event OnNewPresenceDelegate OnNewPresence;

        public delegate void OnRemovePresenceDelegate(UUID agentId);

        public event OnRemovePresenceDelegate OnRemovePresence;

        public delegate void OnParcelPrimCountUpdateDelegate();

        public event OnParcelPrimCountUpdateDelegate OnParcelPrimCountUpdate;

        public delegate void OnParcelPrimCountAddDelegate(SceneObjectGroup obj);

        public event OnParcelPrimCountAddDelegate OnParcelPrimCountAdd;

        public delegate void OnPluginConsoleDelegate(string[] args);

        public event OnPluginConsoleDelegate OnPluginConsole;

        public delegate void OnShutdownDelegate();

        public event OnShutdownDelegate OnShutdown;

        public delegate void ObjectGrabDelegate(uint localID, uint originalID, Vector3 offsetPos, IClientAPI remoteClient, SurfaceTouchEventArgs surfaceArgs);
        public delegate void ObjectDeGrabDelegate(uint localID, uint originalID, IClientAPI remoteClient, SurfaceTouchEventArgs surfaceArgs);
        public delegate void ScriptResetDelegate(uint localID, UUID itemID);

        public delegate void OnPermissionErrorDelegate(UUID user, string reason);

        public delegate void OnSetRootAgentSceneDelegate(UUID agentID, Scene scene);

        public event OnSetRootAgentSceneDelegate OnSetRootAgentScene;

        public event ObjectGrabDelegate OnObjectGrab;
        public event ObjectDeGrabDelegate OnObjectDeGrab;
        public event ScriptResetDelegate OnScriptReset;

        public event OnPermissionErrorDelegate OnPermissionError;

        public delegate void NewRezScript(uint localID, UUID itemID, string script, int startParam, bool postOnRez, string engine, int stateSource);

        public event NewRezScript OnRezScript;

        public delegate void RemoveScript(uint localID, UUID itemID);

        public event RemoveScript OnRemoveScript;

        public delegate void StartScript(uint localID, UUID itemID);

        public event StartScript OnStartScript;

        public delegate void StopScript(uint localID, UUID itemID);

        public event StopScript OnStopScript;

        public delegate bool SceneGroupMoved(UUID groupID, Vector3 delta);

        public event SceneGroupMoved OnSceneGroupMove;

        public delegate void SceneGroupGrabed(UUID groupID, Vector3 offset, UUID userID);

        public event SceneGroupGrabed OnSceneGroupGrab;

        public delegate bool SceneGroupSpinStarted(UUID groupID);

        public event SceneGroupSpinStarted OnSceneGroupSpinStart;

        public delegate bool SceneGroupSpun(UUID groupID, Quaternion rotation);

        public event SceneGroupSpun OnSceneGroupSpin;

        public delegate void LandObjectAdded(ILandObject newParcel);

        public event LandObjectAdded OnLandObjectAdded;

        public delegate void LandObjectRemoved(UUID globalID);

        public event LandObjectRemoved OnLandObjectRemoved;

        public delegate void AvatarEnteringNewParcel(ScenePresence avatar, int localLandID, UUID regionID);

        public event AvatarEnteringNewParcel OnAvatarEnteringNewParcel;

        public delegate void SignificantClientMovement(IClientAPI remote_client);

        public event SignificantClientMovement OnSignificantClientMovement;

        public delegate void IncomingInstantMessage(GridInstantMessage message);

        public event IncomingInstantMessage OnIncomingInstantMessage;

        public event IncomingInstantMessage OnUnhandledInstantMessage;

        public delegate void ClientClosed(UUID clientID, Scene scene);

        public event ClientClosed OnClientClosed;

        public delegate void ScriptChangedEvent(uint localID, uint change);

        public event ScriptChangedEvent OnScriptChangedEvent;

        public delegate void ScriptControlEvent(uint localID, UUID item, UUID avatarID, uint held, uint changed);

        public event ScriptControlEvent OnScriptControlEvent;

        public delegate void ScriptAtTargetEvent(uint localID, uint handle, Vector3 targetpos, Vector3 atpos);

        public event ScriptAtTargetEvent OnScriptAtTargetEvent;

        public delegate void ScriptNotAtTargetEvent(uint localID);

        public event ScriptNotAtTargetEvent OnScriptNotAtTargetEvent;

        public delegate void ScriptColliding(uint localID, ColliderArgs colliders);

        public event ScriptColliding OnScriptColliderStart;
        public event ScriptColliding OnScriptColliding;
        public event ScriptColliding OnScriptCollidingEnd;

        public delegate void OnMakeChildAgentDelegate(ScenePresence presence);
        public event OnMakeChildAgentDelegate OnMakeChildAgent;

        public delegate void OnMakeRootAgentDelegate(ScenePresence presence);
        public event OnMakeRootAgentDelegate OnMakeRootAgent;

        public delegate void NewInventoryItemUploadComplete(UUID avatarID, UUID assetID, string name, int userlevel);

        public event NewInventoryItemUploadComplete OnNewInventoryItemUploadComplete;

        public delegate void RequestChangeWaterHeight(float height);

        public event RequestChangeWaterHeight OnRequestChangeWaterHeight;

        public delegate void AvatarKillData(uint KillerLocalID, ScenePresence avatar);

        public event AvatarKillData OnAvatarKilled;

        public delegate void ScriptTimerEvent(uint localID, double timerinterval);

        public event ScriptTimerEvent OnScriptTimerEvent;

        public delegate void EstateToolsSunUpdate(ulong regionHandle, bool FixedTime, bool EstateSun, float LindenHour);
        public delegate void GetScriptRunning(IClientAPI controllingClient, UUID objectID, UUID itemID);

        public event EstateToolsSunUpdate OnEstateToolsSunUpdate;

        public delegate void ObjectBeingRemovedFromScene(SceneObjectGroup obj);
        public event ObjectBeingRemovedFromScene OnObjectBeingRemovedFromScene;

        public delegate void NoticeNoLandDataFromStorage();
        public event NoticeNoLandDataFromStorage OnNoticeNoLandDataFromStorage;

        public delegate void IncomingLandDataFromStorage(List<LandData> data);
        public event IncomingLandDataFromStorage OnIncomingLandDataFromStorage;

        public delegate void SetAllowForcefulBan(bool allow);
        public event SetAllowForcefulBan OnSetAllowForcefulBan;

        public delegate void RequestParcelPrimCountUpdate();
        public event RequestParcelPrimCountUpdate OnRequestParcelPrimCountUpdate;

        public delegate void ParcelPrimCountTainted();
        public event ParcelPrimCountTainted OnParcelPrimCountTainted;
        public event GetScriptRunning OnGetScriptRunning;

        /// <summary>
        /// RegisterCapsEvent is called by Scene after the Caps object
        /// has been instantiated and before it is return to the
        /// client and provides region modules to add their caps.
        /// </summary>
        public delegate void RegisterCapsEvent(UUID agentID, Caps caps);
        public event RegisterCapsEvent OnRegisterCaps;
        
        /// <summary>
        /// DeregisterCapsEvent is called by Scene when the caps
        /// handler for an agent are removed.
        /// </summary>
        public delegate void DeregisterCapsEvent(UUID agentID, Caps caps);
        public event DeregisterCapsEvent OnDeregisterCaps;
        
        /// <summary>
        /// ChatFromWorldEvent is called via Scene when a chat message
        /// from world comes in.
        /// </summary>
        public delegate void ChatFromWorldEvent(Object sender, OSChatMessage chat);
        public event ChatFromWorldEvent OnChatFromWorld;
        
        /// <summary>
        /// ChatFromClientEvent is triggered via ChatModule (or
        /// substitutes thereof) when a chat message
        /// from the client  comes in.
        /// </summary>
        public delegate void ChatFromClientEvent(Object sender, OSChatMessage chat);
        public event ChatFromClientEvent OnChatFromClient;
        
        /// <summary>
        /// ChatBroadcastEvent is called via Scene when a broadcast chat message
        /// from world comes in
        /// </summary>
        public delegate void ChatBroadcastEvent(Object sender, OSChatMessage chat);
        public event ChatBroadcastEvent OnChatBroadcast;

        public delegate float SunLindenHour();
        public event SunLindenHour OnGetCurrentTimeAsLindenSunHour;

        /// <summary>
        /// Called when oar file has finished loading, although
        /// the scripts may not have started yet
        /// Message is non empty string if there were problems loading the oar file
        /// </summary>
        public delegate void OarFileLoaded(Guid guid, string message);
        public event OarFileLoaded OnOarFileLoaded;
        
        /// <summary>
        /// Called when an oar file has finished saving
        /// Message is non empty string if there were problems saving the oar file
        /// If a guid was supplied on the original call to identify, the request, this is returned.  Otherwise 
        /// Guid.Empty is returned.
        /// </summary>
        public delegate void OarFileSaved(Guid guid, string message);
        public event OarFileSaved OnOarFileSaved;        

        /// <summary>
        /// Called when the script compile queue becomes empty
        /// Returns the number of scripts which failed to start
        /// </summary>
        public delegate void EmptyScriptCompileQueue(int numScriptsFailed, string message);
        public event EmptyScriptCompileQueue OnEmptyScriptCompileQueue;

        /// <summary>
        /// Called whenever an object is attached, or detached
        /// from an in-world presence.
        /// </summary>
        public delegate void Attach(uint localID, UUID itemID, UUID avatarID);
        public event Attach OnAttach;

        public delegate void RegionUp(GridRegion region);
        public event RegionUp OnRegionUp;

        public class MoneyTransferArgs : EventArgs
        {
            public UUID sender;
            public UUID receiver;

            // Always false. The SL protocol sucks.
            public bool authenticated = false;

            public int amount;
            public int transactiontype;
            public string description;

            public MoneyTransferArgs(UUID asender, UUID areceiver, int aamount, int atransactiontype, string adescription)
            {
                sender = asender;
                receiver = areceiver;
                amount = aamount;
                transactiontype = atransactiontype;
                description = adescription;
            }
        }

        public class LandBuyArgs : EventArgs
        {
            public UUID agentId = UUID.Zero;

            public UUID groupId = UUID.Zero;

            public UUID parcelOwnerID = UUID.Zero;

            public bool final = false;
            public bool groupOwned = false;
            public bool removeContribution = false;
            public int parcelLocalID = 0;
            public int parcelArea = 0;
            public int parcelPrice = 0;
            public bool authenticated = false;
            public bool landValidated = false;
            public bool economyValidated = false;
            public int transactionID = 0;
            public int amountDebited = 0;

            public LandBuyArgs(UUID pagentId, UUID pgroupId, bool pfinal, bool pgroupOwned,
                bool premoveContribution, int pparcelLocalID, int pparcelArea, int pparcelPrice,
                bool pauthenticated)
            {
                agentId = pagentId;
                groupId = pgroupId;
                final = pfinal;
                groupOwned = pgroupOwned;
                removeContribution = premoveContribution;
                parcelLocalID = pparcelLocalID;
                parcelArea = pparcelArea;
                parcelPrice = pparcelPrice;
                authenticated = pauthenticated;
            }
        }

        public delegate void MoneyTransferEvent(Object sender, MoneyTransferArgs e);

        public delegate void LandBuy(Object sender, LandBuyArgs e);

        public event MoneyTransferEvent OnMoneyTransfer;
        public event LandBuy OnLandBuy;
        public event LandBuy OnValidateLandBuy;

        /* Designated Event Deletage Instances */

        private ScriptChangedEvent handlerScriptChangedEvent = null; //OnScriptChangedEvent;
        private ScriptAtTargetEvent handlerScriptAtTargetEvent = null;
        private ScriptNotAtTargetEvent handlerScriptNotAtTargetEvent = null;
        private ClientMovement handlerClientMovement = null; //OnClientMovement;
        private OnPermissionErrorDelegate handlerPermissionError = null; //OnPermissionError;
        private OnPluginConsoleDelegate handlerPluginConsole = null; //OnPluginConsole;
        private OnFrameDelegate handlerFrame = null; //OnFrame;
        private OnNewClientDelegate handlerNewClient = null; //OnNewClient;
        private OnClientConnectCoreDelegate handlerClientConnect = null; //OnClientConnect
        private OnNewPresenceDelegate handlerNewPresence = null; //OnNewPresence;
        private OnRemovePresenceDelegate handlerRemovePresence = null; //OnRemovePresence;
        private OnBackupDelegate handlerBackup = null; //OnBackup;
        private OnParcelPrimCountUpdateDelegate handlerParcelPrimCountUpdate = null; //OnParcelPrimCountUpdate;
        private MoneyTransferEvent handlerMoneyTransfer = null; //OnMoneyTransfer;
        private OnParcelPrimCountAddDelegate handlerParcelPrimCountAdd = null; //OnParcelPrimCountAdd;
        private OnShutdownDelegate handlerShutdown = null; //OnShutdown;
        private ObjectGrabDelegate handlerObjectGrab = null; //OnObjectGrab;
        private ObjectDeGrabDelegate handlerObjectDeGrab = null; //OnObjectDeGrab;
        private ScriptResetDelegate handlerScriptReset = null; // OnScriptReset
        private NewRezScript handlerRezScript = null; //OnRezScript;
        private RemoveScript handlerRemoveScript = null; //OnRemoveScript;
        private StartScript handlerStartScript = null; //OnStartScript;
        private StopScript handlerStopScript = null; //OnStopScript;
        private SceneGroupMoved handlerSceneGroupMove = null; //OnSceneGroupMove;
        private SceneGroupGrabed handlerSceneGroupGrab = null; //OnSceneGroupGrab;
        private SceneGroupSpinStarted handlerSceneGroupSpinStarted = null; //OnSceneGroupSpinStart;
        private SceneGroupSpun handlerSceneGroupSpin = null; //OnSceneGroupSpin;
        private LandObjectAdded handlerLandObjectAdded = null; //OnLandObjectAdded;
        private LandObjectRemoved handlerLandObjectRemoved = null; //OnLandObjectRemoved;
        private AvatarEnteringNewParcel handlerAvatarEnteringNewParcel = null; //OnAvatarEnteringNewParcel;
        private IncomingInstantMessage handlerIncomingInstantMessage = null; //OnIncomingInstantMessage;
        private IncomingInstantMessage handlerUnhandledInstantMessage = null; //OnUnhandledInstantMessage;
        private ClientClosed handlerClientClosed = null; //OnClientClosed;
        private OnMakeChildAgentDelegate handlerMakeChildAgent = null; //OnMakeChildAgent;
        private OnMakeRootAgentDelegate handlerMakeRootAgent = null; //OnMakeRootAgent;
        private OnTerrainTickDelegate handlerTerrainTick = null; // OnTerainTick;
        private RegisterCapsEvent handlerRegisterCaps = null; // OnRegisterCaps;
        private DeregisterCapsEvent handlerDeregisterCaps = null; // OnDeregisterCaps;
        private ChatFromWorldEvent handlerChatFromWorld = null; // OnChatFromWorld;
        private ChatFromClientEvent handlerChatFromClient = null; // OnChatFromClient;
        private ChatBroadcastEvent handlerChatBroadcast = null; // OnChatBroadcast;
        private NewInventoryItemUploadComplete handlerNewInventoryItemUpdateComplete = null;
        private RequestChangeWaterHeight handlerRequestChangeWaterHeight = null; //OnRequestChangeWaterHeight
        private ScriptControlEvent handlerScriptControlEvent = null;
        private SignificantClientMovement handlerSignificantClientMovement = null;

        private LandBuy handlerLandBuy = null;
        private LandBuy handlerValidateLandBuy = null;
        private AvatarKillData handlerAvatarKill = null;

        private NoticeNoLandDataFromStorage handlerNoticeNoLandDataFromStorage = null;
        private IncomingLandDataFromStorage handlerIncomingLandDataFromStorage = null;
        private SetAllowForcefulBan handlerSetAllowForcefulBan = null;
        private RequestParcelPrimCountUpdate handlerRequestParcelPrimCountUpdate = null;
        private ParcelPrimCountTainted handlerParcelPrimCountTainted = null;
        private ObjectBeingRemovedFromScene handlerObjectBeingRemovedFromScene = null;
        // TODO: unused: private ScriptTimerEvent handlerScriptTimerEvent = null;
        private EstateToolsSunUpdate handlerEstateToolsSunUpdate = null;

        private ScriptColliding handlerCollidingStart = null;
        private ScriptColliding handlerColliding = null;
        private ScriptColliding handlerCollidingEnd = null;
        private GetScriptRunning handlerGetScriptRunning = null;

        private SunLindenHour handlerCurrentTimeAsLindenSunHour = null;
        private OnSetRootAgentSceneDelegate handlerSetRootAgentScene = null;

        private OarFileLoaded handlerOarFileLoaded = null;
        private OarFileSaved handlerOarFileSaved = null;
        
        private EmptyScriptCompileQueue handlerEmptyScriptCompileQueue = null;

        private Attach handlerOnAttach = null;
        private RegionUp handlerOnRegionUp = null;

        public void TriggerOnAttach(uint localID, UUID itemID, UUID avatarID)
        {
            handlerOnAttach = OnAttach;
            if (handlerOnAttach != null)
                handlerOnAttach(localID, itemID, avatarID);
        }

        public void TriggerGetScriptRunning(IClientAPI controllingClient, UUID objectID, UUID itemID)
        {
            handlerGetScriptRunning = OnGetScriptRunning;
            if (handlerGetScriptRunning != null)
                handlerGetScriptRunning(controllingClient, objectID, itemID);
        }

        public void TriggerOnScriptChangedEvent(uint localID, uint change)
        {
            handlerScriptChangedEvent = OnScriptChangedEvent;
            if (handlerScriptChangedEvent != null)
                handlerScriptChangedEvent(localID, change);
        }

        public void TriggerOnClientMovement(ScenePresence avatar)
        {
            handlerClientMovement = OnClientMovement;
            if (handlerClientMovement != null)
                handlerClientMovement(avatar);
        }

        public void TriggerPermissionError(UUID user, string reason)
        {
            handlerPermissionError = OnPermissionError;
            if (handlerPermissionError != null)
                handlerPermissionError(user, reason);
        }

        public void TriggerOnPluginConsole(string[] args)
        {
            handlerPluginConsole = OnPluginConsole;
            if (handlerPluginConsole != null)
                handlerPluginConsole(args);
        }

        public void TriggerOnFrame()
        {
            handlerFrame = OnFrame;
            if (handlerFrame != null)
            {
                handlerFrame();
            }
        }

        public void TriggerOnNewClient(IClientAPI client)
        {
            handlerNewClient = OnNewClient;
            if (handlerNewClient != null)
                handlerNewClient(client);

            if (client is IClientCore)
            {
                handlerClientConnect = OnClientConnect;
                if (handlerClientConnect != null)
                    handlerClientConnect((IClientCore)client);
            }
        }

        public void TriggerOnNewPresence(ScenePresence presence)
        {
            handlerNewPresence = OnNewPresence;
            if (handlerNewPresence != null)
                handlerNewPresence(presence);
        }

        public void TriggerOnRemovePresence(UUID agentId)
        {
            handlerRemovePresence = OnRemovePresence;
            if (handlerRemovePresence != null)
            {
                handlerRemovePresence(agentId);
            }
        }

        public void TriggerOnBackup(IRegionDataStore dstore)
        {
            handlerBackup = OnBackup;
            if (handlerBackup != null)
            {
                handlerBackup(dstore, false);
            }
        }

        public void TriggerParcelPrimCountUpdate()
        {
            handlerParcelPrimCountUpdate = OnParcelPrimCountUpdate;
            if (handlerParcelPrimCountUpdate != null)
            {
                handlerParcelPrimCountUpdate();
            }
        }

        public void TriggerMoneyTransfer(Object sender, MoneyTransferArgs e)
        {
            handlerMoneyTransfer = OnMoneyTransfer;
            if (handlerMoneyTransfer != null)
            {
                handlerMoneyTransfer(sender, e);
            }
        }

        public void TriggerTerrainTick()
        {
            handlerTerrainTick = OnTerrainTick;
            if (handlerTerrainTick != null)
            {
                handlerTerrainTick();
            }
        }

        public void TriggerParcelPrimCountAdd(SceneObjectGroup obj)
        {
            handlerParcelPrimCountAdd = OnParcelPrimCountAdd;
            if (handlerParcelPrimCountAdd != null)
            {
                handlerParcelPrimCountAdd(obj);
            }
        }

        public void TriggerObjectBeingRemovedFromScene(SceneObjectGroup obj)
        {
            handlerObjectBeingRemovedFromScene = OnObjectBeingRemovedFromScene;
            if (handlerObjectBeingRemovedFromScene != null)
            {
                handlerObjectBeingRemovedFromScene(obj);
            }
        }

        public void TriggerShutdown()
        {
            handlerShutdown = OnShutdown;
            if (handlerShutdown != null)
                handlerShutdown();
        }

        public void TriggerObjectGrab(uint localID, uint originalID, Vector3 offsetPos, IClientAPI remoteClient, SurfaceTouchEventArgs surfaceArgs)
        {
            handlerObjectGrab = OnObjectGrab;
            if (handlerObjectGrab != null)
            {
                handlerObjectGrab(localID, originalID, offsetPos, remoteClient, surfaceArgs);
            }
        }

        public void TriggerObjectDeGrab(uint localID, uint originalID, IClientAPI remoteClient, SurfaceTouchEventArgs surfaceArgs)
        {
            handlerObjectDeGrab = OnObjectDeGrab;
            if (handlerObjectDeGrab != null)
            {
                handlerObjectDeGrab(localID, originalID, remoteClient, surfaceArgs);
            }
        }

        public void TriggerScriptReset(uint localID, UUID itemID)
        {
            handlerScriptReset = OnScriptReset;
            if (handlerScriptReset != null)
            {
                handlerScriptReset(localID, itemID);
            }
        }

        public void TriggerRezScript(uint localID, UUID itemID, string script, int startParam, bool postOnRez, string engine, int stateSource)
        {
            handlerRezScript = OnRezScript;
            if (handlerRezScript != null)
            {
                handlerRezScript(localID, itemID, script, startParam,
                        postOnRez, engine, stateSource);
            }
        }

        public void TriggerStartScript(uint localID, UUID itemID)
        {
            handlerStartScript = OnStartScript;
            if (handlerStartScript != null)
            {
                handlerStartScript(localID, itemID);
            }
        }

        public void TriggerStopScript(uint localID, UUID itemID)
        {
            handlerStopScript = OnStopScript;
            if (handlerStopScript != null)
            {
                handlerStopScript(localID, itemID);
            }
        }

        public void TriggerRemoveScript(uint localID, UUID itemID)
        {
            handlerRemoveScript = OnRemoveScript;
            if (handlerRemoveScript != null)
            {
                handlerRemoveScript(localID, itemID);
            }
        }

        public bool TriggerGroupMove(UUID groupID, Vector3 delta)
        {
            handlerSceneGroupMove = OnSceneGroupMove;

            if (handlerSceneGroupMove != null)
            {
                return handlerSceneGroupMove(groupID, delta);
            }
            return true;
        }

        public bool TriggerGroupSpinStart(UUID groupID)
        {
            handlerSceneGroupSpinStarted = OnSceneGroupSpinStart;

            if (handlerSceneGroupSpinStarted != null)
            {
                return handlerSceneGroupSpinStarted(groupID);
            }
            return true;
        }

        public bool TriggerGroupSpin(UUID groupID, Quaternion rotation)
        {
            handlerSceneGroupSpin = OnSceneGroupSpin;

            if (handlerSceneGroupSpin != null)
            {
                return handlerSceneGroupSpin(groupID, rotation);
            }
            return true;
        }

        public void TriggerGroupGrab(UUID groupID, Vector3 offset, UUID userID)
        {
            handlerSceneGroupGrab = OnSceneGroupGrab;
            if (handlerSceneGroupGrab != null)
            {
                handlerSceneGroupGrab(groupID, offset, userID);
            }
        }

        public void TriggerLandObjectAdded(ILandObject newParcel)
        {
            handlerLandObjectAdded = OnLandObjectAdded;

            if (handlerLandObjectAdded != null)
            {
                handlerLandObjectAdded(newParcel);
            }
        }

        public void TriggerLandObjectRemoved(UUID globalID)
        {
            handlerLandObjectRemoved = OnLandObjectRemoved;
            if (handlerLandObjectRemoved != null)
            {
                handlerLandObjectRemoved(globalID);
            }
        }

        public void TriggerLandObjectUpdated(uint localParcelID, ILandObject newParcel)
        {
            //triggerLandObjectRemoved(localParcelID);

            TriggerLandObjectAdded(newParcel);
        }

        public void TriggerAvatarEnteringNewParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            handlerAvatarEnteringNewParcel = OnAvatarEnteringNewParcel;

            if (handlerAvatarEnteringNewParcel != null)
            {
                handlerAvatarEnteringNewParcel(avatar, localLandID, regionID);
            }
        }

        public void TriggerIncomingInstantMessage(GridInstantMessage message)
        {
            handlerIncomingInstantMessage = OnIncomingInstantMessage;
            if (handlerIncomingInstantMessage != null)
            {
                handlerIncomingInstantMessage(message);
            }
        }

        public void TriggerUnhandledInstantMessage(GridInstantMessage message)
        {
            handlerUnhandledInstantMessage = OnUnhandledInstantMessage;
            if (handlerUnhandledInstantMessage != null)
            {
                handlerUnhandledInstantMessage(message);
            }
        }

        public void TriggerClientClosed(UUID ClientID, Scene scene)
        {
            handlerClientClosed = OnClientClosed;
            if (handlerClientClosed != null)
            {
                handlerClientClosed(ClientID, scene);
            }
        }

        public void TriggerOnMakeChildAgent(ScenePresence presence)
        {
            handlerMakeChildAgent = OnMakeChildAgent;
            if (handlerMakeChildAgent != null)
            {
                handlerMakeChildAgent(presence);
            }
        }

        public void TriggerOnMakeRootAgent(ScenePresence presence)
        {
            handlerMakeRootAgent = OnMakeRootAgent;
            if (handlerMakeRootAgent != null)
            {
                handlerMakeRootAgent(presence);
            }
        }

        public void TriggerOnRegisterCaps(UUID agentID, Caps caps)
        {
            handlerRegisterCaps = OnRegisterCaps;
            if (handlerRegisterCaps != null)
            {
                handlerRegisterCaps(agentID, caps);
            }
        }

        public void TriggerOnDeregisterCaps(UUID agentID, Caps caps)
        {
            handlerDeregisterCaps = OnDeregisterCaps;
            if (handlerDeregisterCaps != null)
            {
                handlerDeregisterCaps(agentID, caps);
            }
        }

        public void TriggerOnNewInventoryItemUploadComplete(UUID agentID, UUID AssetID, String AssetName, int userlevel)
        {
            handlerNewInventoryItemUpdateComplete = OnNewInventoryItemUploadComplete;
            if (handlerNewInventoryItemUpdateComplete != null)
            {
                handlerNewInventoryItemUpdateComplete(agentID, AssetID, AssetName, userlevel);
            }
        }

        public void TriggerLandBuy(Object sender, LandBuyArgs e)
        {
            handlerLandBuy = OnLandBuy;
            if (handlerLandBuy != null)
            {
                handlerLandBuy(sender, e);
            }
        }

        public void TriggerValidateLandBuy(Object sender, LandBuyArgs e)
        {
            handlerValidateLandBuy = OnValidateLandBuy;
            if (handlerValidateLandBuy != null)
            {
                handlerValidateLandBuy(sender, e);
            }
        }

        public void TriggerAtTargetEvent(uint localID, uint handle, Vector3 targetpos, Vector3 currentpos)
        {
            handlerScriptAtTargetEvent = OnScriptAtTargetEvent;
            if (handlerScriptAtTargetEvent != null)
            {
                handlerScriptAtTargetEvent(localID, handle, targetpos, currentpos);
            }
        }

        public void TriggerNotAtTargetEvent(uint localID)
        {
            handlerScriptNotAtTargetEvent = OnScriptNotAtTargetEvent;
            if (handlerScriptNotAtTargetEvent != null)
            {
                handlerScriptNotAtTargetEvent(localID);
            }
        }

        public void TriggerRequestChangeWaterHeight(float height)
        {
            handlerRequestChangeWaterHeight = OnRequestChangeWaterHeight;
            if (handlerRequestChangeWaterHeight != null)
            {
                handlerRequestChangeWaterHeight(height);
            }
        }

        public void TriggerAvatarKill(uint KillerObjectLocalID, ScenePresence DeadAvatar)
        {
            handlerAvatarKill = OnAvatarKilled;
            if (handlerAvatarKill != null)
            {
                handlerAvatarKill(KillerObjectLocalID, DeadAvatar);
            }
        }

        public void TriggerSignificantClientMovement(IClientAPI client)
        {
            handlerSignificantClientMovement = OnSignificantClientMovement;
            if (handlerSignificantClientMovement != null)
            {
                handlerSignificantClientMovement(client);
            }
        }

        public void TriggerOnChatFromWorld(Object sender, OSChatMessage chat)
        {
            handlerChatFromWorld = OnChatFromWorld;
            if (handlerChatFromWorld != null)
            {
                handlerChatFromWorld(sender, chat);
            }
        }

        public void TriggerOnChatFromClient(Object sender, OSChatMessage chat)
        {
            handlerChatFromClient = OnChatFromClient;
            if (handlerChatFromClient != null)
            {
                handlerChatFromClient(sender, chat);
            }
        }

        public void TriggerOnChatBroadcast(Object sender, OSChatMessage chat)
        {
            handlerChatBroadcast = OnChatBroadcast;
            if (handlerChatBroadcast != null)
            {
                handlerChatBroadcast(sender, chat);
            }
        }

        internal void TriggerControlEvent(uint p, UUID scriptUUID, UUID avatarID, uint held, uint _changed)
        {
            handlerScriptControlEvent = OnScriptControlEvent;
            if (handlerScriptControlEvent != null)
            {
                handlerScriptControlEvent(p, scriptUUID,  avatarID, held, _changed);
            }
        }

        public void TriggerNoticeNoLandDataFromStorage()
        {
            handlerNoticeNoLandDataFromStorage = OnNoticeNoLandDataFromStorage;
            if (handlerNoticeNoLandDataFromStorage != null)
            {
                handlerNoticeNoLandDataFromStorage();

            }
        }

        public void TriggerIncomingLandDataFromStorage(List<LandData> landData)
        {
            handlerIncomingLandDataFromStorage = OnIncomingLandDataFromStorage;
            if (handlerIncomingLandDataFromStorage != null)
            {
                handlerIncomingLandDataFromStorage(landData);

            }
        }

        public void TriggerSetAllowForcefulBan(bool allow)
        {
            handlerSetAllowForcefulBan = OnSetAllowForcefulBan;
            if (handlerSetAllowForcefulBan != null)
            {
                handlerSetAllowForcefulBan(allow);

            }
        }

        public void TriggerRequestParcelPrimCountUpdate()
        {
            handlerRequestParcelPrimCountUpdate = OnRequestParcelPrimCountUpdate;
            if (handlerRequestParcelPrimCountUpdate != null)
            {
                handlerRequestParcelPrimCountUpdate();
            }
        }

        public void TriggerParcelPrimCountTainted()
        {
            handlerParcelPrimCountTainted = OnParcelPrimCountTainted;
            if (handlerParcelPrimCountTainted != null)
            {
                handlerParcelPrimCountTainted();
            }
        }

        // this lets us keep track of nasty script events like timer, etc.
        public void TriggerTimerEvent(uint objLocalID, double Interval)
        {
            throw new NotImplementedException("TriggerTimerEvent was thought to be not used anymore and the registration for the event from scene object part has been commented out due to a memory leak");
            //handlerScriptTimerEvent = OnScriptTimerEvent;
            //if (handlerScriptTimerEvent != null)
            //{
            //    handlerScriptTimerEvent(objLocalID, Interval);
            //}
        }

        /// <summary>
        /// Updates the system as to how the position of the sun should be handled.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="FixedTime">True if the Sun Position is fixed</param>
        /// <param name="useEstateTime">True if the Estate Settings should be used instead of region</param>
        /// <param name="FixedSunHour">The hour 0.0 <= FixedSunHour <= 24.0 at which the sun is fixed at. Sun Hour 0 is sun-rise, when Day/Night ratio is 1:1</param>
        public void TriggerEstateToolsSunUpdate(ulong regionHandle, bool FixedTime, bool useEstateTime, float FixedSunHour)
        {
            handlerEstateToolsSunUpdate = OnEstateToolsSunUpdate;
            if (handlerEstateToolsSunUpdate != null)
            {
                handlerEstateToolsSunUpdate(regionHandle, FixedTime, useEstateTime, FixedSunHour);
            }
        }

        public float GetCurrentTimeAsSunLindenHour()
        {
            handlerCurrentTimeAsLindenSunHour = OnGetCurrentTimeAsLindenSunHour;
            if (handlerCurrentTimeAsLindenSunHour != null)
            {
                return handlerCurrentTimeAsLindenSunHour();
            }
            return 6;
        }

        public void TriggerOarFileLoaded(Guid requestId, string message)
        {
            handlerOarFileLoaded = OnOarFileLoaded;
            if (handlerOarFileLoaded != null)
                handlerOarFileLoaded(requestId, message);
        }
        
        public void TriggerOarFileSaved(Guid requestId, string message)
        {
            handlerOarFileSaved = OnOarFileSaved;
            if (handlerOarFileSaved != null)
                handlerOarFileSaved(requestId, message);
        }        

        public void TriggerEmptyScriptCompileQueue(int numScriptsFailed, string message)
        {
            handlerEmptyScriptCompileQueue = OnEmptyScriptCompileQueue;
            if (handlerEmptyScriptCompileQueue != null)
                handlerEmptyScriptCompileQueue(numScriptsFailed, message);
        }

        public void TriggerScriptCollidingStart(uint localId, ColliderArgs colliders)
        {
            handlerCollidingStart = OnScriptColliderStart;
            if (handlerCollidingStart != null)
                handlerCollidingStart(localId, colliders);
        }

        public void TriggerScriptColliding(uint localId, ColliderArgs colliders)
        {
            handlerColliding = OnScriptColliding;
            if (handlerColliding != null)
                handlerColliding(localId, colliders);
        }

        public void TriggerScriptCollidingEnd(uint localId, ColliderArgs colliders)
        {
            handlerCollidingEnd = OnScriptCollidingEnd;
            if (handlerCollidingEnd != null)
                handlerCollidingEnd(localId, colliders);
        }

        public void TriggerSetRootAgentScene(UUID agentID, Scene scene)
        {
            handlerSetRootAgentScene = OnSetRootAgentScene;
            if (handlerSetRootAgentScene != null)
                handlerSetRootAgentScene(agentID, scene);
        }

        public void TriggerOnRegionUp(GridRegion otherRegion)
        {
            handlerOnRegionUp = OnRegionUp;
            if (handlerOnRegionUp != null)
                handlerOnRegionUp(otherRegion);
        }

    }
}
