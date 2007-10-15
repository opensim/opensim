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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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
using OpenSim.Framework.Interfaces;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Scenes
{
    /// <summary>
    /// A class for triggering remote scene events.
    /// </summary>
    public class EventManager
    {
        public delegate void OnFrameDelegate();

        public event OnFrameDelegate OnFrame;

        public delegate void OnBackupDelegate(IRegionDataStore datastore);

        public event OnBackupDelegate OnBackup;

        public delegate void OnNewClientDelegate(IClientAPI client);

        public event OnNewClientDelegate OnNewClient;

        public delegate void OnNewPresenceDelegate(ScenePresence presence);

        public event OnNewPresenceDelegate OnNewPresence;

        public delegate void OnRemovePresenceDelegate(LLUUID uuid);

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

        public delegate void SceneGroupMoved(LLUUID groupID, LLVector3 delta);

        public event SceneGroupMoved OnSceneGroupMove;

        public delegate void SceneGroupGrabed(LLUUID groupID, LLVector3 offset);

        public event SceneGroupGrabed OnSceneGroupGrab;

        public void TriggerPermissionError(LLUUID user, string reason)
        {
            if (OnPermissionError != null)
                OnPermissionError(user, reason);
        }

        public void TriggerOnPluginConsole(string[] args)
        {
            if (OnPluginConsole != null)
                OnPluginConsole(args);
        }

        public void TriggerOnFrame()
        {
            if (OnFrame != null)
            {
                OnFrame();
            }
        }

        public void TriggerOnNewClient(IClientAPI client)
        {
            if (OnNewClient != null)
                OnNewClient(client);
        }

        public void TriggerOnNewPresence(ScenePresence presence)
        {
            if (OnNewPresence != null)
                OnNewPresence(presence);
        }

        public void TriggerOnRemovePresence(LLUUID uuid)
        {
            if (OnRemovePresence != null)
            {
                OnRemovePresence(uuid);
            }
        }

        public void TriggerOnBackup(IRegionDataStore dstore)
        {
            if (OnBackup != null)
            {
                OnBackup(dstore);
            }
        }

        public void TriggerParcelPrimCountUpdate()
        {
            if (OnParcelPrimCountUpdate != null)
            {
                OnParcelPrimCountUpdate();
            }
        }

        public void TriggerParcelPrimCountAdd(SceneObjectGroup obj)
        {
            if (OnParcelPrimCountAdd != null)
            {
                OnParcelPrimCountAdd(obj);
            }
        }

        public void TriggerShutdown()
        {
            if (OnShutdown != null)
                OnShutdown();
        }

        public void TriggerObjectGrab(uint localID, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            if (OnObjectGrab != null)
            {
                OnObjectGrab(localID, offsetPos, remoteClient);
            }
        }

        public void TriggerRezScript(uint localID, LLUUID itemID, string script)
        {
            if (OnRezScript != null)
            {
                OnRezScript(localID, itemID, script);
            }
        }

        public void TriggerRemoveScript(uint localID, LLUUID itemID)
        {
            if (OnRemoveScript != null)
            {
                OnRemoveScript(localID, itemID);
            }
        }

        public void TriggerGroupMove(LLUUID groupID, LLVector3 delta)
        {
            if (OnSceneGroupMove != null)
            {
                OnSceneGroupMove(groupID, delta);
            }
        }

        public void TriggerGroupGrab(LLUUID groupID, LLVector3 offset)
        {
            if (OnSceneGroupGrab != null)
            {
                OnSceneGroupGrab(groupID, offset);
            }
        }
    }
}
