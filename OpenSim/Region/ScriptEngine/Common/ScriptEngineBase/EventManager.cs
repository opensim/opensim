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

namespace OpenSim.Region.ScriptEngine.Common.ScriptEngineBase
{
    /// <summary>
    /// Prepares events so they can be directly executed upon a script by EventQueueManager, then queues it.
    /// </summary>
    [Serializable]
    public class EventManager : OpenSim.Region.ScriptEngine.Common.ScriptServerInterfaces.RemoteEvents, iScriptEngineFunctionModule
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

            // Hook up to events from OpenSim
            // We may not want to do it because someone is controlling us and will deliver events to us
            if (performHookUp)
            {
                myScriptEngine.Log.Info("[" + myScriptEngine.ScriptEngineName + "]: Hooking up to server events");
                myScriptEngine.World.EventManager.OnObjectGrab += touch_start;
                myScriptEngine.World.EventManager.OnRezScript += OnRezScript;
                myScriptEngine.World.EventManager.OnRemoveScript += OnRemoveScript;
                myScriptEngine.World.EventManager.OnScriptChangedEvent += changed;
                // TODO: HOOK ALL EVENTS UP TO SERVER!
            }
        }

        public void ReadConfig()
        {
        }

        public void changed(uint localID, uint change)
        {
            // Add to queue for all scripts in localID, Object pass change.
            myScriptEngine.m_EventQueueManager.AddToObjectQueue(localID, "changed", EventQueueManager.llDetectNull, new object[] { (int)change });
        }

        public void state_entry(uint localID)
        {
            // Add to queue for all scripts in ObjectID object
            myScriptEngine.m_EventQueueManager.AddToObjectQueue(localID, "state_entry", EventQueueManager.llDetectNull, new object[] { });
        }

        public void touch_start(uint localID, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            // Add to queue for all scripts in ObjectID object
            EventQueueManager.Queue_llDetectParams_Struct detstruct = new EventQueueManager.Queue_llDetectParams_Struct();
            detstruct._key = new LSL_Types.key[1];
            detstruct._key[0] = new LSL_Types.key(remoteClient.AgentId.ToString());
            myScriptEngine.m_EventQueueManager.AddToObjectQueue(localID, "touch_start", detstruct, new object[] { (int)1 });
        }

        public void OnRezScript(uint localID, LLUUID itemID, string script)
        {
            Console.WriteLine("OnRezScript localID: " + localID + " LLUID: " + itemID.ToString() + " Size: " +
                              script.Length);
            myScriptEngine.m_ScriptManager.StartScript(localID, itemID, script);
        }

        public void OnRemoveScript(uint localID, LLUUID itemID)
        {
            Console.WriteLine("OnRemoveScript localID: " + localID + " LLUID: " + itemID.ToString());
            myScriptEngine.m_ScriptManager.StopScript(
                localID,
                itemID
                );
        }

        // TODO: Replace placeholders below
        // NOTE! THE PARAMETERS FOR THESE FUNCTIONS ARE NOT CORRECT!
        //  These needs to be hooked up to OpenSim during init of this class
        //   then queued in EventQueueManager.
        // When queued in EventQueueManager they need to be LSL compatible (name and params)

        public void state_exit(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "state_exit", EventQueueManager.llDetectNull);
        }

        public void touch(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "touch", EventQueueManager.llDetectNull);
        }

        public void touch_end(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "touch_end", EventQueueManager.llDetectNull);
        }

        public void collision_start(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "collision_start", EventQueueManager.llDetectNull, new object[] { (int)1 });
        }

        public void collision(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "collision", EventQueueManager.llDetectNull, new object[] { (int)1 });
        }

        public void collision_end(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "collision_end", EventQueueManager.llDetectNull, new object[] { (int)1 });
        }

        public void land_collision_start(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "land_collision_start", EventQueueManager.llDetectNull);
        }

        public void land_collision(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "land_collision", EventQueueManager.llDetectNull);
        }

        public void land_collision_end(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "land_collision_end", EventQueueManager.llDetectNull);
        }

        // Handled by long commands
        public void timer(uint localID, LLUUID itemID)
        {
            //myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, String.Empty);
        }

        public void listen(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "listen", EventQueueManager.llDetectNull);
        }

        public void on_rez(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "on_rez", EventQueueManager.llDetectNull);
        }

        public void sensor(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "sensor", EventQueueManager.llDetectNull);
        }

        public void no_sensor(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "no_sensor", EventQueueManager.llDetectNull);
        }

        public void control(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "control", EventQueueManager.llDetectNull);
        }

        public void money(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "money", EventQueueManager.llDetectNull);
        }

        public void email(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "email", EventQueueManager.llDetectNull);
        }

        public void at_target(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "at_target", EventQueueManager.llDetectNull);
        }

        public void not_at_target(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "not_at_target", EventQueueManager.llDetectNull);
        }

        public void at_rot_target(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "at_rot_target", EventQueueManager.llDetectNull);
        }

        public void not_at_rot_target(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "not_at_rot_target", EventQueueManager.llDetectNull);
        }

        public void run_time_permissions(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "run_time_permissions", EventQueueManager.llDetectNull);
        }

        public void changed(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "changed", EventQueueManager.llDetectNull);
        }

        public void attach(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "attach", EventQueueManager.llDetectNull);
        }

        public void dataserver(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "dataserver", EventQueueManager.llDetectNull);
        }

        public void link_message(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "link_message", EventQueueManager.llDetectNull);
        }

        public void moving_start(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "moving_start", EventQueueManager.llDetectNull);
        }

        public void moving_end(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "moving_end", EventQueueManager.llDetectNull);
        }

        public void object_rez(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "object_rez", EventQueueManager.llDetectNull);
        }

        public void remote_data(uint localID, LLUUID itemID)
        {
            myScriptEngine.m_EventQueueManager.AddToScriptQueue(localID, itemID, "remote_data", EventQueueManager.llDetectNull);
        }

        // Handled by long commands
        public void http_response(uint localID, LLUUID itemID)
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
