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

using libsecondlife;
using System;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.LandManagement;

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

        public delegate void LandObjectAdded(Land newParcel, LLUUID regionUUID);

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


        public class MoneyTransferArgs : System.EventArgs 
        {
            public LLUUID sender;
            public LLUUID reciever;

            // Always false. The SL protocol sucks.
            public bool authenticated = false;

            public int amount;
            public int transactiontype;
            public string description;

            public MoneyTransferArgs(LLUUID asender, LLUUID areciever, int aamount, int atransactiontype, string adescription) {
                sender = asender;
                reciever = areciever;
                amount = aamount;
                transactiontype = atransactiontype;
                description = adescription;
            }
        }

        public delegate void MoneyTransferEvent(Object sender, MoneyTransferArgs e);

        public event MoneyTransferEvent OnMoneyTransfer;


        /* Designated Event Deletage Instances */

        private ScriptChangedEvent handler001 = null; //OnScriptChangedEvent;
        private ClientMovement handler002 = null; //OnClientMovement;
        private OnPermissionErrorDelegate handler003 = null; //OnPermissionError;
        private OnPluginConsoleDelegate handler004 = null; //OnPluginConsole;
        private OnFrameDelegate handler005 = null; //OnFrame;
        private OnNewClientDelegate handler006 = null; //OnNewClient;
        private OnNewPresenceDelegate handler007 = null; //OnNewPresence;
        private OnRemovePresenceDelegate handler008 = null; //OnRemovePresence;
        private OnBackupDelegate handler009 = null; //OnBackup;
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

        public void TriggerOnScriptChangedEvent(uint localID, uint change)
        {
            handler001 = OnScriptChangedEvent;
            if (handler001 != null)
                handler001(localID, change);
        }

        public void TriggerOnClientMovement(ScenePresence avatar)
        {
            handler002 = OnClientMovement;
            if (handler002 != null)
                handler002(avatar);
        }

        public void TriggerPermissionError(LLUUID user, string reason)
        {
            handler003 = OnPermissionError;
            if (handler003 != null)
                handler003(user, reason);
        }

        public void TriggerOnPluginConsole(string[] args)
        {
            handler004 = OnPluginConsole;
            if (handler004 != null)
                handler004(args);
        }

        public void TriggerOnFrame()
        {
            handler005 = OnFrame;
            if (handler005 != null)
            {
                handler005();
            }
        }

        public void TriggerOnNewClient(IClientAPI client)
        {
            handler006 = OnNewClient;
            if (handler006 != null)
                handler006(client);
        }

        public void TriggerOnNewPresence(ScenePresence presence)
        {
            handler007 = OnNewPresence;
            if (handler007 != null)
                handler007(presence);
        }

        public void TriggerOnRemovePresence(LLUUID agentId)
        {
            handler008 = OnRemovePresence;
            if (handler008 != null)
            {
                handler008(agentId);
            }
        }

        public void TriggerOnBackup(IRegionDataStore dstore)
        {
            handler009 = OnBackup;
            if (handler009 != null)
            {
                handler009(dstore);
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

        public void TriggerLandObjectAdded(Land newParcel, LLUUID regionID)
        {
            handlerLandObjectAdded = OnLandObjectAdded;

            if (handlerLandObjectAdded != null)
            {
                handlerLandObjectAdded(newParcel, regionID);
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

        public void TriggerLandObjectUpdated(uint localParcelID, Land newParcel)
        {
            //triggerLandObjectRemoved(localParcelID);

            TriggerLandObjectAdded(newParcel, newParcel.m_scene.RegionInfo.RegionID);
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
        
    }
}