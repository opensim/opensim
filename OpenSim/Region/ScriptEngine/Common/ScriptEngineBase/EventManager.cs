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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Environment.Modules.Avatar.Currency.SampleMoney;
using OpenSim.Region.Environment;
using OpenSim.Region.Interfaces;
using OpenSim.Region;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.ScriptEngine.Common.ScriptEngineBase
{
    /// <summary>
    /// Prepares events so they can be directly executed upon a script by EventQueueManager, then queues it.
    /// </summary>
    [Serializable]
    public class EventManager : ScriptServerInterfaces.RemoteEvents, iScriptEngineFunctionModule
    {
        //
        // Class is instanced in "ScriptEngine" and Uses "EventQueueManager" that is also instanced in "ScriptEngine".
        // This class needs a bit of explaining:
        //
        // This class it the link between an event inside OpenSim and the corresponding event in a user script being executed.
        //
        // For example when an user touches an object then the "myScriptEngine.World.EventManager.OnObjectGrab" event is fired inside OpenSim.
        // We hook up to this event and queue a touch_start in EventQueueManager with the proper LSL parameters.
        // It will then be delivered to the script by EventQueueManager.
        //
        // You can check debug C# dump of an LSL script if you need to verify what exact parameters are needed.
        //


        private ScriptEngine myScriptEngine;
        //public IScriptHost TEMP_OBJECT_ID;
        public EventManager(ScriptEngine _ScriptEngine, bool performHookUp)
        {
            myScriptEngine = _ScriptEngine;
            ReadConfig();

            if (performHookUp)
            {
                myScriptEngine.World.EventManager.OnRezScript += OnRezScript;
            }
        }
        
        public void HookUpEvents()
        {
            // Hook up to events from OpenSim
            // We may not want to do it because someone is controlling us and will deliver events to us

            myScriptEngine.Log.Info("[" + myScriptEngine.ScriptEngineName + "]: Hooking up to server events");
            myScriptEngine.World.EventManager.OnObjectGrab += touch_start;
            myScriptEngine.World.EventManager.OnObjectDeGrab += touch_end;
            myScriptEngine.World.EventManager.OnRemoveScript += OnRemoveScript;
            myScriptEngine.World.EventManager.OnScriptChangedEvent += changed;
            myScriptEngine.World.EventManager.OnScriptAtTargetEvent += at_target;
            myScriptEngine.World.EventManager.OnScriptNotAtTargetEvent += not_at_target;
            myScriptEngine.World.EventManager.OnScriptControlEvent += control;
            myScriptEngine.World.EventManager.OnScriptColliderStart += collision_start;
            myScriptEngine.World.EventManager.OnScriptColliding += collision;
            myScriptEngine.World.EventManager.OnScriptCollidingEnd += collision_end;

            // TODO: HOOK ALL EVENTS UP TO SERVER!
            IMoneyModule money=myScriptEngine.World.RequestModuleInterface<IMoneyModule>();
            if (money != null)
            {
                money.OnObjectPaid+=HandleObjectPaid;
            }

        }

        public void ReadConfig()
        {
        }

        private void HandleObjectPaid(UUID objectID, UUID agentID, int amount)
        {
            SceneObjectPart part=myScriptEngine.World.GetSceneObjectPart(objectID);
            if (part != null)
            {
                money(part.LocalId, agentID, amount);
            }
        }

        public void changed(uint localID, uint change)
        {
            // Add to queue for all scripts in localID, Object pass change.
            myScriptEngine.m_EventQueueManager.AddToObjectQueue(localID, "changed", EventQueueManager.llDetectNull, new object[] { new LSL_Types.LSLInteger(change) });
        }

        public void state_entry(uint localID)
        {
            // Add to queue for all scripts in ObjectID object
            myScriptEngine.m_EventQueueManager.AddToObjectQueue(localID, "state_entry", EventQueueManager.llDetectNull, new object[] { });
        }

        public void touch_start(uint localID, uint originalID, Vector3 offsetPos, IClientAPI remoteClient)
        {
            // Add to queue for all scripts in ObjectID object
            EventQueueManager.Queue_llDetectParams_Struct detstruct = new EventQueueManager.Queue_llDetectParams_Struct();
            detstruct._key = new LSL_Types.key[1];
            detstruct._key2 = new LSL_Types.key[1];
            detstruct._string = new string[1];
            detstruct._Vector3 = new LSL_Types.Vector3[1];
            detstruct._Vector32 = new LSL_Types.Vector3[1];
            detstruct._Quaternion = new LSL_Types.Quaternion[1];
            detstruct._int = new int[1];
            ScenePresence av = myScriptEngine.World.GetScenePresence(remoteClient.AgentId);
            if (av != null)
            {
                detstruct._key[0] = new LSL_Types.key(remoteClient.AgentId.ToString());
                detstruct._key2[0] = new LSL_Types.key(remoteClient.AgentId.ToString());
                detstruct._string[0] = remoteClient.Name;
                detstruct._int[0] = 0;
                detstruct._Quaternion[0] = new LSL_Types.Quaternion(av.Rotation.X,av.Rotation.Y,av.Rotation.Z,av.Rotation.W);
                detstruct._Vector3[0] = new LSL_Types.Vector3(av.AbsolutePosition.X,av.AbsolutePosition.Y,av.AbsolutePosition.Z);
                detstruct._Vector32[0] = new LSL_Types.Vector3(av.Velocity.X,av.Velocity.Y,av.Velocity.Z);
            }
            else
            {
                detstruct._key[0] = new LSL_Types.key(remoteClient.AgentId.ToString());
                detstruct._key2[0] = new LSL_Types.key(remoteClient.AgentId.ToString());
                detstruct._string[0] = remoteClient.Name;
                detstruct._int[0] = 0;
                detstruct._Quaternion[0] = new LSL_Types.Quaternion(0, 0, 0, 1);
                detstruct._Vector3[0] = new LSL_Types.Vector3(0, 0, 0);
                detstruct._Vector32[0] = new LSL_Types.Vector3(0, 0, 0);
            }
            myScriptEngine.m_EventQueueManager.AddToObjectQueue(localID, "touch_start", detstruct, new object[] { new LSL_Types.LSLInteger(1) });
        }

        public void touch_end(uint localID, uint originalID, IClientAPI remoteClient)
        {
            // Add to queue for all scripts in ObjectID object
            EventQueueManager.Queue_llDetectParams_Struct detstruct = new EventQueueManager.Queue_llDetectParams_Struct();
            detstruct._key = new LSL_Types.key[1];
            detstruct._key2 = new LSL_Types.key[1];
            detstruct._string = new string[1];
            detstruct._Vector3 = new LSL_Types.Vector3[1];
            detstruct._Vector32 = new LSL_Types.Vector3[1];
            detstruct._Quaternion = new LSL_Types.Quaternion[1];
            detstruct._int = new int[1];
            ScenePresence av = myScriptEngine.World.GetScenePresence(remoteClient.AgentId);
            if (av != null)
            {
                detstruct._key[0] = new LSL_Types.key(remoteClient.AgentId.ToString());
                detstruct._key2[0] = new LSL_Types.key(remoteClient.AgentId.ToString());
                detstruct._string[0] = remoteClient.Name;
                detstruct._int[0] = 0;
                detstruct._Quaternion[0] = new LSL_Types.Quaternion(av.Rotation.X, av.Rotation.Y, av.Rotation.Z, av.Rotation.W);
                detstruct._Vector3[0] = new LSL_Types.Vector3(av.AbsolutePosition.X, av.AbsolutePosition.Y, av.AbsolutePosition.Z);
                detstruct._Vector32[0] = new LSL_Types.Vector3(av.Velocity.X, av.Velocity.Y, av.Velocity.Z);
            }
            else
            {
                detstruct._key[0] = new LSL_Types.key(remoteClient.AgentId.ToString());
                detstruct._key2[0] = new LSL_Types.key(remoteClient.AgentId.ToString());
                detstruct._string[0] = remoteClient.Name;
                detstruct._int[0] = 0;
                detstruct._Quaternion[0] = new LSL_Types.Quaternion(0, 0, 0, 1);
                detstruct._Vector3[0] = new LSL_Types.Vector3(0, 0, 0);
                detstruct._Vector32[0] = new LSL_Types.Vector3(0, 0, 0);
            }
            myScriptEngine.m_EventQueueManager.AddToObjectQueue(localID, "touch_end", detstruct, new object[] { new LSL_Types.LSLInteger(1) });
        }

        public void OnRezScript(uint localID, UUID itemID, string script, int startParam, bool postOnRez, string engine)
        {
            int lineEnd = script.IndexOf('\n');

            if (lineEnd != 1)
            {
                string firstline = script.Substring(0, lineEnd).Trim();

                int colon = firstline.IndexOf(':');
                if (firstline.Length > 2 && firstline.Substring(0, 2) == "//" && colon != -1)
                {
                    engine = firstline.Substring(2, colon-2);
                    script = "//" + script.Substring(script.IndexOf(':')+1);
                }
            }

            if (engine != "DotNetEngine")
                return;

            myScriptEngine.Log.Debug("OnRezScript localID: " + localID + " LLUID: " + itemID.ToString() + " Size: " +
                              script.Length);
            myScriptEngine.m_ScriptManager.StartScript(localID, itemID, script, startParam, postOnRez);
        }

        public void OnRemoveScript(uint localID, UUID itemID)
        {
            myScriptEngine.Log.Debug("OnRemoveScript localID: " + localID + " LLUID: " + itemID.ToString());
            myScriptEngine.m_ScriptManager.StopScript(
                localID,
                itemID
                );
        }

        public void money(uint localID, UUID agentID, int amount)
        {
            myScriptEngine.m_EventQueueManager.AddToObjectQueue(localID, "money", EventQueueManager.llDetectNull, new object[] { new LSL_Types.LSLString(agentID.ToString()), new LSL_Types.LSLInteger(amount) });
        }

        // TODO: Replace placeholders below
        // NOTE! THE PARAMETERS FOR THESE FUNCTIONS ARE NOT CORRECT!
        //  These needs to be hooked up to OpenSim during init of this class
        //   then queued in EventQueueManager.
        // When queued in EventQueueManager they need to be LSL compatible (name and params)

        public void state_exit(uint localID)
        {
            // Add to queue for all scripts in ObjectID object
            myScriptEngine.m_EventQueueManager.AddToObjectQueue(localID, "state_exit", EventQueueManager.llDetectNull, new object[] { });
        }

        public void touch(uint localID, uint originalID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "touch", EventQueueManager.llDetectNull);
        }

        public void touch_end(uint localID, uint originalID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "touch_end", EventQueueManager.llDetectNull, new object[] { new LSL_Types.LSLInteger(1) });
        }

        public void collision_start(uint localID, ColliderArgs col)
        {
            EventQueueManager.Queue_llDetectParams_Struct detstruct = new EventQueueManager.Queue_llDetectParams_Struct();
            detstruct._string = new string[col.Colliders.Count];
            detstruct._Quaternion = new LSL_Types.Quaternion[col.Colliders.Count];
            detstruct._int = new int[col.Colliders.Count];
            detstruct._key = new LSL_Types.key[col.Colliders.Count];
            detstruct._key2 = new LSL_Types.key[col.Colliders.Count];
            detstruct._Vector3 = new LSL_Types.Vector3[col.Colliders.Count];
            detstruct._Vector32 = new LSL_Types.Vector3[col.Colliders.Count];
            detstruct._bool = new bool[col.Colliders.Count];

            int i = 0;
            foreach (DetectedObject detobj in col.Colliders)
            {
                detstruct._key[i] = new LSL_Types.key(detobj.keyUUID.ToString());
                detstruct._key2[i] = new LSL_Types.key(detobj.ownerUUID.ToString());
                detstruct._Quaternion[i] = new LSL_Types.Quaternion(detobj.rotQuat.X, detobj.rotQuat.Y, detobj.rotQuat.Z, detobj.rotQuat.W);
                detstruct._string[i] = detobj.nameStr;
                detstruct._int[i] = detobj.colliderType;
                detstruct._Vector3[i] = new LSL_Types.Vector3(detobj.posVector.X, detobj.posVector.Y, detobj.posVector.Z);
                detstruct._Vector32[i] = new LSL_Types.Vector3(detobj.velVector.X, detobj.velVector.Y, detobj.velVector.Z);
                detstruct._bool[i] = true; // Apparently the script engine uses this to see if this is a valid entry...
                i++;
            }

            myScriptEngine.m_EventQueueManager.AddToObjectQueue(localID, "collision_start", detstruct, new object[] { new LSL_Types.LSLInteger(col.Colliders.Count) });
        }

        public void collision(uint localID, ColliderArgs col)
        {
            EventQueueManager.Queue_llDetectParams_Struct detstruct = new EventQueueManager.Queue_llDetectParams_Struct();
            detstruct._string = new string[col.Colliders.Count];
            detstruct._Quaternion = new LSL_Types.Quaternion[col.Colliders.Count];
            detstruct._int = new int[col.Colliders.Count];
            detstruct._key = new LSL_Types.key[col.Colliders.Count];
            detstruct._key2 = new LSL_Types.key[col.Colliders.Count];
            detstruct._Vector3 = new LSL_Types.Vector3[col.Colliders.Count];
            detstruct._Vector32 = new LSL_Types.Vector3[col.Colliders.Count];
            detstruct._bool = new bool[col.Colliders.Count];

            int i = 0;
            foreach (DetectedObject detobj in col.Colliders)
            {
                detstruct._key[i] = new LSL_Types.key(detobj.keyUUID.ToString());
                detstruct._key2[i] = new LSL_Types.key(detobj.ownerUUID.ToString());
                detstruct._Quaternion[i] = new LSL_Types.Quaternion(detobj.rotQuat.X, detobj.rotQuat.Y, detobj.rotQuat.Z, detobj.rotQuat.W);
                detstruct._string[i] = detobj.nameStr;
                detstruct._int[i] = detobj.colliderType;
                detstruct._Vector3[i] = new LSL_Types.Vector3(detobj.posVector.X, detobj.posVector.Y, detobj.posVector.Z);
                detstruct._Vector32[i] = new LSL_Types.Vector3(detobj.velVector.X, detobj.velVector.Y, detobj.velVector.Z);
                detstruct._bool[i] = true; // Apparently the script engine uses this to see if this is a valid entry...                    i++;
            }
            myScriptEngine.m_EventQueueManager.AddToObjectQueue(localID, "collision", detstruct, new object[] { new LSL_Types.LSLInteger(col.Colliders.Count) });
        }

        public void collision_end(uint localID, ColliderArgs col)
        {
            EventQueueManager.Queue_llDetectParams_Struct detstruct = new EventQueueManager.Queue_llDetectParams_Struct();
            detstruct._string = new string[col.Colliders.Count];
            detstruct._Quaternion = new LSL_Types.Quaternion[col.Colliders.Count];
            detstruct._int = new int[col.Colliders.Count];
            detstruct._key = new LSL_Types.key[col.Colliders.Count];
            detstruct._key2 = new LSL_Types.key[col.Colliders.Count];
            detstruct._Vector3 = new LSL_Types.Vector3[col.Colliders.Count];
            detstruct._Vector32 = new LSL_Types.Vector3[col.Colliders.Count];
            detstruct._bool = new bool[col.Colliders.Count];

            int i = 0;
            foreach (DetectedObject detobj in col.Colliders)
            {
                detstruct._key[i] = new LSL_Types.key(detobj.keyUUID.ToString());
                detstruct._key2[i] = new LSL_Types.key(detobj.ownerUUID.ToString());
                detstruct._Quaternion[i] = new LSL_Types.Quaternion(detobj.rotQuat.X, detobj.rotQuat.Y, detobj.rotQuat.Z, detobj.rotQuat.W);
                detstruct._string[i] = detobj.nameStr;
                detstruct._int[i] = detobj.colliderType;
                detstruct._Vector3[i] = new LSL_Types.Vector3(detobj.posVector.X, detobj.posVector.Y, detobj.posVector.Z);
                detstruct._Vector32[i] = new LSL_Types.Vector3(detobj.velVector.X, detobj.velVector.Y, detobj.velVector.Z);
                detstruct._bool[i] = true; // Apparently the script engine uses this to see if this is a valid entry...
                i++;
            }
            myScriptEngine.m_EventQueueManager.AddToObjectQueue(localID, "collision_end", EventQueueManager.llDetectNull, new object[] { new LSL_Types.LSLInteger(col.Colliders.Count) });
        }

        public void land_collision_start(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "land_collision_start", EventQueueManager.llDetectNull);
        }

        public void land_collision(uint localID, ColliderArgs col)
        {
            myScriptEngine.m_EventQueueManager.AddToObjectQueue(localID, "land_collision", EventQueueManager.llDetectNull);
        }

        public void land_collision_end(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "land_collision_end", EventQueueManager.llDetectNull);
        }

        // Handled by long commands
        public void timer(uint localID, UUID itemID)
        {
            //myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, String.Empty);
        }

        public void listen(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "listen", EventQueueManager.llDetectNull);
        }

        public void on_rez(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "on_rez", EventQueueManager.llDetectNull);
        }

        public void sensor(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "sensor", EventQueueManager.llDetectNull);
        }

        public void no_sensor(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "no_sensor", EventQueueManager.llDetectNull);
        }

        public void control(uint localID, UUID itemID, UUID agentID, uint held, uint change)
        {
            if ((change == 0) && (myScriptEngine.m_EventQueueManager.CheckEeventQueueForEvent(localID,"control"))) return;
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "control", EventQueueManager.llDetectNull, new object[] { new LSL_Types.LSLString(agentID.ToString()), new LSL_Types.LSLInteger(held), new LSL_Types.LSLInteger(change)});
        }

        public void email(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "email", EventQueueManager.llDetectNull);
        }

        public void at_target(uint localID, uint handle, Vector3 targetpos, Vector3 atpos)
        {
            myScriptEngine.m_EventQueueManager.AddToObjectQueue(localID, "at_target", EventQueueManager.llDetectNull, new object[] { new LSL_Types.LSLInteger(handle), new LSL_Types.Vector3(targetpos.X,targetpos.Y,targetpos.Z), new LSL_Types.Vector3(atpos.X,atpos.Y,atpos.Z) });
        }

        public void not_at_target(uint localID)
        {
            myScriptEngine.m_EventQueueManager.AddToObjectQueue(localID, "not_at_target", EventQueueManager.llDetectNull);
        }

        public void at_rot_target(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "at_rot_target", EventQueueManager.llDetectNull);
        }

        public void not_at_rot_target(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "not_at_rot_target", EventQueueManager.llDetectNull);
        }

        public void run_time_permissions(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "run_time_permissions", EventQueueManager.llDetectNull);
        }

        public void changed(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "changed", EventQueueManager.llDetectNull);
        }

        public void attach(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "attach", EventQueueManager.llDetectNull);
        }

        public void dataserver(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "dataserver", EventQueueManager.llDetectNull);
        }

        public void link_message(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "link_message", EventQueueManager.llDetectNull);
        }

        public void moving_start(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "moving_start", EventQueueManager.llDetectNull);
        }

        public void moving_end(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "moving_end", EventQueueManager.llDetectNull);
        }

        public void object_rez(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "object_rez", EventQueueManager.llDetectNull);
        }

        public void remote_data(uint localID, UUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "remote_data", EventQueueManager.llDetectNull);
        }

        // Handled by long commands
        public void http_response(uint localID, UUID itemID)
        {
            //    myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "http_response", EventQueueManager.llDetectNull);
        }

        /// <summary>
        /// If set to true then threads and stuff should try to make a graceful exit
        /// </summary>
        public bool PleaseShutdown
        {
            get { return _PleaseShutdown; }
            set { _PleaseShutdown = value; }
        }
        private bool _PleaseShutdown = false;
    }
}
