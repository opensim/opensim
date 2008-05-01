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
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using Caps=OpenSim.Framework.Communications.Capabilities.Caps;

namespace OpenSim.Region.Environment.Scenes
{
    /// <summary>
    /// A class for triggering remote scene events.
    /// </summary>
    public class EventManager
    {
        #region Delegates

        public delegate void AvatarEnteringNewParcel(ScenePresence avatar, int localLandID, LLUUID regionID);

        public delegate void ClientClosed(LLUUID clientID);

        public delegate void ClientMovement(ScenePresence client);

        /// <summary>
        /// DeregisterCapsEvent is called by Scene when the caps
        /// handler for an agent are removed.
        /// </summary>
        public delegate void DeregisterCapsEvent(LLUUID agentID, Caps caps);

        public delegate void LandBuy(Object sender, LandBuyArgs e);

        public delegate void LandObjectAdded(ILandObject newParcel);

        public delegate void LandObjectRemoved(LLUUID globalID);

        public delegate void MoneyTransferEvent(Object sender, MoneyTransferArgs e);

        public delegate void NewGridInstantMessage(GridInstantMessage message);

        public delegate void NewInventoryItemUploadComplete(LLUUID avatarID, LLUUID assetID, string name, int userlevel);

        public delegate void NewRezScript(uint localID, LLUUID itemID, string script);

        public delegate void ObjectDeGrabDelegate(uint localID, IClientAPI remoteClient);

        public delegate void ObjectGrabDelegate(uint localID, LLVector3 offsetPos, IClientAPI remoteClient);

        public delegate void OnBackupDelegate(IRegionDataStore datastore);

        public delegate void OnFrameDelegate();

        public delegate void OnNewClientDelegate(IClientAPI client);

        public delegate void OnNewPresenceDelegate(ScenePresence presence);

        public delegate void OnParcelPrimCountAddDelegate(SceneObjectGroup obj);

        public delegate void OnParcelPrimCountUpdateDelegate();

        public delegate void OnPermissionErrorDelegate(LLUUID user, string reason);

        public delegate void OnPluginConsoleDelegate(string[] args);

        public delegate void OnRemovePresenceDelegate(LLUUID agentId);

        public delegate void OnShutdownDelegate();

        public delegate void OnTerrainTickDelegate();

        /// <summary>
        /// RegisterCapsEvent is called by Scene after the Caps object
        /// has been instantiated and before it is return to the
        /// client and provides region modules to add their caps.
        /// </summary>
        public delegate void RegisterCapsEvent(LLUUID agentID, Caps caps);

        public delegate void RemoveScript(uint localID, LLUUID itemID);

        public delegate void SceneGroupGrabed(LLUUID groupID, LLVector3 offset, LLUUID userID);

        public delegate bool SceneGroupMoved(LLUUID groupID, LLVector3 delta);

        public delegate void ScriptAtTargetEvent(uint localID, uint handle, LLVector3 targetpos, LLVector3 atpos);

        public delegate void ScriptChangedEvent(uint localID, uint change);

        public delegate void ScriptNotAtTargetEvent(uint localID);

        #endregion

        private AvatarEnteringNewParcel handlerAvatarEnteringNewParcel; //OnAvatarEnteringNewParcel;
        private OnBackupDelegate handlerBackup; //OnBackup;
        private ClientClosed handlerClientClosed; //OnClientClosed;
        private ClientMovement handlerClientMovement; //OnClientMovement;
        private DeregisterCapsEvent handlerDeregisterCaps; // OnDeregisterCaps;
        private OnFrameDelegate handlerFrame; //OnFrame;
        private NewGridInstantMessage handlerGridInstantMessageToFriends; //OnGridInstantMessageToFriendsModule;
        private NewGridInstantMessage handlerGridInstantMessageToIM; //OnGridInstantMessageToIMModule;
        private LandBuy handlerLandBuy;
        private LandObjectAdded handlerLandObjectAdded; //OnLandObjectAdded;
        private LandObjectRemoved handlerLandObjectRemoved; //OnLandObjectRemoved;
        private OnNewPresenceDelegate handlerMakeChildAgent; //OnMakeChildAgent;
        private MoneyTransferEvent handlerMoneyTransfer; //OnMoneyTransfer;
        private OnNewClientDelegate handlerNewClient; //OnNewClient;
        private NewInventoryItemUploadComplete handlerNewInventoryItemUpdateComplete;
        private OnNewPresenceDelegate handlerNewPresence; //OnNewPresence;
        private ObjectDeGrabDelegate handlerObjectDeGrab; //OnObjectDeGrab;
        private ObjectGrabDelegate handlerObjectGrab; //OnObjectGrab;
        private OnParcelPrimCountAddDelegate handlerParcelPrimCountAdd; //OnParcelPrimCountAdd;
        private OnParcelPrimCountUpdateDelegate handlerParcelPrimCountUpdate; //OnParcelPrimCountUpdate;
        private OnPermissionErrorDelegate handlerPermissionError; //OnPermissionError;
        private OnPluginConsoleDelegate handlerPluginConsole; //OnPluginConsole;
        private RegisterCapsEvent handlerRegisterCaps; // OnRegisterCaps;
        private OnRemovePresenceDelegate handlerRemovePresence; //OnRemovePresence;
        private RemoveScript handlerRemoveScript; //OnRemoveScript;
        private NewRezScript handlerRezScript; //OnRezScript;
        private SceneGroupGrabed handlerSceneGroupGrab; //OnSceneGroupGrab;
        private SceneGroupMoved handlerSceneGroupMove; //OnSceneGroupMove;
        private ScriptAtTargetEvent handlerScriptAtTargetEvent;
        private ScriptChangedEvent handlerScriptChangedEvent; //OnScriptChangedEvent;
        private ScriptNotAtTargetEvent handlerScriptNotAtTargetEvent;
        private OnShutdownDelegate handlerShutdown; //OnShutdown;
        private OnTerrainTickDelegate handlerTerrainTick; // OnTerainTick;
        private LandBuy handlerValidateLandBuy;

        public event OnFrameDelegate OnFrame;

        public event ClientMovement OnClientMovement;

        public event OnTerrainTickDelegate OnTerrainTick;

        public event OnBackupDelegate OnBackup;

        public event OnNewClientDelegate OnNewClient;

        public event OnNewPresenceDelegate OnNewPresence;

        public event OnRemovePresenceDelegate OnRemovePresence;

        public event OnParcelPrimCountUpdateDelegate OnParcelPrimCountUpdate;

        public event OnParcelPrimCountAddDelegate OnParcelPrimCountAdd;

        public event OnPluginConsoleDelegate OnPluginConsole;

        public event OnShutdownDelegate OnShutdown;

        public event ObjectGrabDelegate OnObjectGrab;
        public event ObjectDeGrabDelegate OnObjectDeGrab;

        public event OnPermissionErrorDelegate OnPermissionError;

        public event NewRezScript OnRezScript;

        public event RemoveScript OnRemoveScript;

        public event SceneGroupMoved OnSceneGroupMove;

        public event SceneGroupGrabed OnSceneGroupGrab;

        public event LandObjectAdded OnLandObjectAdded;

        public event LandObjectRemoved OnLandObjectRemoved;

        public event AvatarEnteringNewParcel OnAvatarEnteringNewParcel;


        public event NewGridInstantMessage OnGridInstantMessageToIMModule;

        public event NewGridInstantMessage OnGridInstantMessageToFriendsModule;

        public event NewGridInstantMessage OnGridInstantMessageToGroupsModule;

        public event ClientClosed OnClientClosed;

        public event ScriptChangedEvent OnScriptChangedEvent;

        public event ScriptAtTargetEvent OnScriptAtTargetEvent;

        public event ScriptNotAtTargetEvent OnScriptNotAtTargetEvent;

        public event OnNewPresenceDelegate OnMakeChildAgent;

        public event NewInventoryItemUploadComplete OnNewInventoryItemUploadComplete;

        public event RegisterCapsEvent OnRegisterCaps;

        public event DeregisterCapsEvent OnDeregisterCaps;

        public event MoneyTransferEvent OnMoneyTransfer;
        public event LandBuy OnLandBuy;
        public event LandBuy OnValidateLandBuy;

        /* Designated Event Deletage Instances */

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

        public void TriggerPermissionError(LLUUID user, string reason)
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
        }

        public void TriggerOnNewPresence(ScenePresence presence)
        {
            handlerNewPresence = OnNewPresence;
            if (handlerNewPresence != null)
                handlerNewPresence(presence);
        }

        public void TriggerOnRemovePresence(LLUUID agentId)
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
                handlerBackup(dstore);
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

        public void TriggerShutdown()
        {
            handlerShutdown = OnShutdown;
            if (handlerShutdown != null)
                handlerShutdown();
        }

        public void TriggerObjectGrab(uint localID, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            handlerObjectGrab = OnObjectGrab;
            if (handlerObjectGrab != null)
            {
                handlerObjectGrab(localID, offsetPos, remoteClient);
            }
        }

        public void TriggerObjectDeGrab(uint localID, IClientAPI remoteClient)
        {
            handlerObjectDeGrab = OnObjectDeGrab;
            if (handlerObjectDeGrab != null)
            {
                handlerObjectDeGrab(localID, remoteClient);
            }
        }

        public void TriggerRezScript(uint localID, LLUUID itemID, string script)
        {
            handlerRezScript = OnRezScript;
            if (handlerRezScript != null)
            {
                handlerRezScript(localID, itemID, script);
            }
        }

        public void TriggerRemoveScript(uint localID, LLUUID itemID)
        {
            handlerRemoveScript = OnRemoveScript;
            if (handlerRemoveScript != null)
            {
                handlerRemoveScript(localID, itemID);
            }
        }

        public bool TriggerGroupMove(LLUUID groupID, LLVector3 delta)
        {
            handlerSceneGroupMove = OnSceneGroupMove;

            if (handlerSceneGroupMove != null)
            {
                return handlerSceneGroupMove(groupID, delta);
            }
            return true;
        }

        public void TriggerGroupGrab(LLUUID groupID, LLVector3 offset, LLUUID userID)
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

        public void TriggerLandObjectRemoved(LLUUID globalID)
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

        public void TriggerAvatarEnteringNewParcel(ScenePresence avatar, int localLandID, LLUUID regionID)
        {
            handlerAvatarEnteringNewParcel = OnAvatarEnteringNewParcel;

            if (handlerAvatarEnteringNewParcel != null)
            {
                handlerAvatarEnteringNewParcel(avatar, localLandID, regionID);
            }
        }

        ///<summary>Used to pass instnat messages around between the Scene, the Friends Module and the Instant Messsage Module</summary>
        ///<param name="message">Object containing the Instant Message Data</param>
        ///<param name="whichModule">A bit vector containing the modules to send the message to</param>
        public void TriggerGridInstantMessage(GridInstantMessage message, InstantMessageReceiver whichModule)
        {
            if ((whichModule & InstantMessageReceiver.IMModule) != 0)
            {
                handlerGridInstantMessageToIM = OnGridInstantMessageToIMModule;
                if (handlerGridInstantMessageToIM != null)
                {
                    handlerGridInstantMessageToIM(message);
                }
            }
            if ((whichModule & InstantMessageReceiver.FriendsModule) != 0)
            {
                handlerGridInstantMessageToFriends = OnGridInstantMessageToFriendsModule;
                if (handlerGridInstantMessageToFriends != null)
                {
                    handlerGridInstantMessageToFriends(message);
                }
            }
        }

        public void TriggerClientClosed(LLUUID ClientID)
        {
            handlerClientClosed = OnClientClosed;
            if (handlerClientClosed != null)
            {
                handlerClientClosed(ClientID);
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

        public void TriggerOnRegisterCaps(LLUUID agentID, Caps caps)
        {
            handlerRegisterCaps = OnRegisterCaps;
            if (handlerRegisterCaps != null)
            {
                handlerRegisterCaps(agentID, caps);
            }
        }

        public void TriggerOnDeregisterCaps(LLUUID agentID, Caps caps)
        {
            handlerDeregisterCaps = OnDeregisterCaps;
            if (handlerDeregisterCaps != null)
            {
                handlerDeregisterCaps(agentID, caps);
            }
        }

        public void TriggerOnNewInventoryItemUploadComplete(LLUUID agentID, LLUUID AssetID, String AssetName, int userlevel)
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

        public void TriggerAtTargetEvent(uint localID, uint handle, LLVector3 targetpos, LLVector3 currentpos)
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

        #region Nested type: LandBuyArgs

        public class LandBuyArgs : EventArgs
        {
            public LLUUID agentId = LLUUID.Zero;
            public int amountDebited;
            public bool authenticated;
            public bool economyValidated;

            public bool final;
            public LLUUID groupId = LLUUID.Zero;
            public bool groupOwned;
            public bool landValidated;
            public int parcelArea;
            public int parcelLocalID;
            public LLUUID parcelOwnerID = LLUUID.Zero;
            public int parcelPrice;
            public bool removeContribution;
            public int transactionID;


            public LandBuyArgs(LLUUID pagentId, LLUUID pgroupId, bool pfinal, bool pgroupOwned,
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

        #endregion

        #region Nested type: MoneyTransferArgs

        public class MoneyTransferArgs : EventArgs
        {
            public int amount;
            public bool authenticated;
            public string description;
            public LLUUID receiver;
            public LLUUID sender;
            public int transactiontype;

            public MoneyTransferArgs(LLUUID asender, LLUUID areceiver, int aamount, int atransactiontype, string adescription)
            {
                sender = asender;
                receiver = areceiver;
                amount = aamount;
                transactiontype = atransactiontype;
                description = adescription;
            }
        }

        #endregion
    }
}