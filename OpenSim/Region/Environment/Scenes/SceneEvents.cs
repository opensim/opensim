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

using libsecondlife;
using System;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using Caps = OpenSim.Region.Capabilities.Caps;

namespace OpenSim.Region.Environment.Scenes
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

        public delegate void OnBackupDelegate(IRegionDataStore datastore);

        public event OnBackupDelegate OnBackup;

        public delegate void OnNewClientDelegate(IClientAPI client);

        public event OnNewClientDelegate OnNewClient;

        public delegate void OnNewPresenceDelegate(ScenePresence presence);

        public event OnNewPresenceDelegate OnNewPresence;

        public delegate void OnRemovePresenceDelegate(LLUUID agentId);

        public event OnRemovePresenceDelegate OnRemovePresence;

        public delegate void OnParcelPrimCountUpdateDelegate();

        public event OnParcelPrimCountUpdateDelegate OnParcelPrimCountUpdate;

        public delegate void OnParcelPrimCountAddDelegate(SceneObjectGroup obj);

        public event OnParcelPrimCountAddDelegate OnParcelPrimCountAdd;

        public delegate void OnPluginConsoleDelegate(string[] args);

        public event OnPluginConsoleDelegate OnPluginConsole;

        public delegate void OnShutdownDelegate();

        public event OnShutdownDelegate OnShutdown;

        public delegate void ObjectGrabDelegate(uint localID, LLVector3 offsetPos, IClientAPI remoteClient);

        public delegate void OnPermissionErrorDelegate(LLUUID user, string reason);

        public event ObjectGrabDelegate OnObjectGrab;

        public event OnPermissionErrorDelegate OnPermissionError;

        public delegate void NewRezScript(uint localID, LLUUID itemID, string script);

        public event NewRezScript OnRezScript;

        public delegate void RemoveScript(uint localID, LLUUID itemID);

        public event RemoveScript OnRemoveScript;

        public delegate bool SceneGroupMoved(LLUUID groupID, LLVector3 delta);

        public event SceneGroupMoved OnSceneGroupMove;

        public delegate void SceneGroupGrabed(LLUUID groupID, LLVector3 offset, LLUUID userID);

        public event SceneGroupGrabed OnSceneGroupGrab;

        public delegate void LandObjectAdded(ILandObject newParcel);

        public event LandObjectAdded OnLandObjectAdded;

        public delegate void LandObjectRemoved(LLUUID globalID);

        public event LandObjectRemoved OnLandObjectRemoved;

        public delegate void AvatarEnteringNewParcel(ScenePresence avatar, int localLandID, LLUUID regionID);

        public event AvatarEnteringNewParcel OnAvatarEnteringNewParcel;


        public delegate void NewGridInstantMessage(GridInstantMessage message);

        public event NewGridInstantMessage OnGridInstantMessageToIMModule;

        public event NewGridInstantMessage OnGridInstantMessageToFriendsModule;

        public event NewGridInstantMessage OnGridInstantMessageToGroupsModule;

        public delegate void ClientClosed(LLUUID clientID);

        public event ClientClosed OnClientClosed;

        public delegate void ScriptChangedEvent(uint localID, uint change);
       
        public event ScriptChangedEvent OnScriptChangedEvent;

        public event OnNewPresenceDelegate OnMakeChildAgent;

        public delegate void NewInventoryItemUploadComplete(LLUUID avatarID, LLUUID assetID, string name, int userlevel);

        public event NewInventoryItemUploadComplete OnNewInventoryItemUploadComplete;

        /// <summary>
        /// RegisterCapsEvent is called by Scene after the Caps object
        /// has been instantiated and before it is return to the
        /// client and provides region modules to add their caps.
        /// </summary>
        public delegate void RegisterCapsEvent(LLUUID agentID, Caps caps);
        public event RegisterCapsEvent OnRegisterCaps;
        /// <summary>
        /// DeregisterCapsEvent is called by Scene when the caps
        /// handler for an agent are removed.
        /// </summary>
        public delegate void DeregisterCapsEvent(LLUUID agentID, Caps caps);
        public event DeregisterCapsEvent OnDeregisterCaps;

        public class MoneyTransferArgs : System.EventArgs 
        {
            public LLUUID sender;
            public LLUUID receiver;

            // Always false. The SL protocol sucks.
            public bool authenticated = false;

            public int amount;
            public int transactiontype;
            public string description;

            public MoneyTransferArgs(LLUUID asender, LLUUID areciever, int aamount, int atransactiontype, string adescription) {
                sender = asender;
                receiver = areciever;
                amount = aamount;
                transactiontype = atransactiontype;
                description = adescription;
            }
        }

        public class LandBuyArgs : System.EventArgs
        {
            public LLUUID agentId = LLUUID.Zero;
            
            public LLUUID groupId = LLUUID.Zero;

            public LLUUID parcelOwnerID = LLUUID.Zero;

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

        public delegate void MoneyTransferEvent(Object sender, MoneyTransferArgs e);

        public delegate void LandBuy(Object sender, LandBuyArgs e);

        public event MoneyTransferEvent OnMoneyTransfer;
        public event LandBuy OnLandBuy;
        public event LandBuy OnValidatedLandBuy;

        /* Designated Event Deletage Instances */

        private ScriptChangedEvent handlerScriptChangedEvent = null; //OnScriptChangedEvent;
        private ClientMovement handlerClientMovement = null; //OnClientMovement;
        private OnPermissionErrorDelegate handlerPermissionError = null; //OnPermissionError;
        private OnPluginConsoleDelegate handlerPluginConsole = null; //OnPluginConsole;
        private OnFrameDelegate handlerFrame = null; //OnFrame;
        private OnNewClientDelegate handlerNewClient = null; //OnNewClient;
        private OnNewPresenceDelegate handlerNewPresence = null; //OnNewPresence;
        private OnRemovePresenceDelegate handlerRemovePresence = null; //OnRemovePresence;
        private OnBackupDelegate handlerBackup = null; //OnBackup;
        private OnParcelPrimCountUpdateDelegate handlerParcelPrimCountUpdate = null; //OnParcelPrimCountUpdate;
        private MoneyTransferEvent handlerMoneyTransfer = null; //OnMoneyTransfer;
        private OnParcelPrimCountAddDelegate handlerParcelPrimCountAdd = null; //OnParcelPrimCountAdd;
        private OnShutdownDelegate handlerShutdown = null; //OnShutdown;
        private ObjectGrabDelegate handlerObjectGrab = null; //OnObjectGrab;
        private NewRezScript handlerRezScript = null; //OnRezScript;
        private RemoveScript handlerRemoveScript = null; //OnRemoveScript;
        private SceneGroupMoved handlerSceneGroupMove = null; //OnSceneGroupMove;
        private SceneGroupGrabed handlerSceneGroupGrab = null; //OnSceneGroupGrab;
        private LandObjectAdded handlerLandObjectAdded = null; //OnLandObjectAdded;
        private LandObjectRemoved handlerLandObjectRemoved = null; //OnLandObjectRemoved;
        private AvatarEnteringNewParcel handlerAvatarEnteringNewParcel = null; //OnAvatarEnteringNewParcel;
        private NewGridInstantMessage handlerGridInstantMessageToIM = null; //OnGridInstantMessageToIMModule;
        private NewGridInstantMessage handlerGridInstantMessageToFriends = null; //OnGridInstantMessageToFriendsModule;
        private ClientClosed handlerClientClosed = null; //OnClientClosed;
        private OnNewPresenceDelegate handlerMakeChildAgent = null; //OnMakeChildAgent;
        private OnTerrainTickDelegate handlerTerrainTick = null; // OnTerainTick;
        private RegisterCapsEvent handlerRegisterCaps = null; // OnRegisterCaps;
        private DeregisterCapsEvent handlerDeregisterCaps = null; // OnDeregisterCaps;
        private NewInventoryItemUploadComplete handlerNewInventoryItemUpdateComplete = null;
        private LandBuy handlerLandBuy = null;
        private LandBuy handlerValidatedLandBuy = null;

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
        public void TriggerLandBuy (Object sender, LandBuyArgs e)
        {
            handlerLandBuy = OnLandBuy;
            if (handlerLandBuy != null)
            {
                handlerLandBuy(sender, e);
            }
        }
        public void TriggerValidatedLandBuy(Object sender, LandBuyArgs e)
        {
            handlerValidatedLandBuy = OnValidatedLandBuy;
            if (handlerValidatedLandBuy != null)
            {
                handlerValidatedLandBuy(sender, e);
            }
        }
    }
}
