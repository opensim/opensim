/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
/* Original code: Tedd Hansen */
using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Interfaces;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    /// <summary>
    /// Prepares events so they can be directly executed upon a script by EventQueueManager, then queues it.
    /// </summary>
    class EventManager
    {
        private ScriptEngine myScriptEngine;
        public EventManager(ScriptEngine _ScriptEngine)
        {
            myScriptEngine = _ScriptEngine;
            // TODO: HOOK EVENTS UP TO SERVER!
            //myScriptEngine.m_logger.Verbose("ScriptEngine", "EventManager Start");
            // TODO: ADD SERVER HOOK TO LOAD A SCRIPT THROUGH myScriptEngine.ScriptManager

            // Hook up a test event to our test form
            myScriptEngine.Log.Verbose("ScriptEngine", "EventManager Hooking up dummy-event: touch_start");
            // TODO: REPLACE THIS WITH A REAL TOUCH_START EVENT IN SERVER
            myScriptEngine.World.EventManager.OnObjectGrab += new OpenSim.Region.Environment.Scenes.EventManager.ObjectGrabDelegate(touch_start);
            //myScriptEngine.World.touch_start += new TempWorldInterfaceEventDelegates.touch_start(touch_start);
        }

        public void touch_start(uint localID, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            // Add to queue for all scripts in ObjectID object
            //myScriptEngine.m_logger.Verbose("ScriptEngine", "EventManager Event: touch_start");
            myScriptEngine.myEventQueueManager.AddToObjectQueue("TEST", "touch_start", new object[] { (int)0 });
        }


        // TODO: Replace placeholders below
        //  These needs to be hooked up to OpenSim during init of this class
        //   then queued in EventQueueManager.
        // When queued in EventQueueManager they need to be LSL compatible (name and params)
        public void state_entry() { }
        public void state_exit() { }
        //public void touch_start() { }
        public void touch() { }
        public void touch_end() { }
        public void collision_start() { }
        public void collision() { }
        public void collision_end() { }
        public void land_collision_start() { }
        public void land_collision() { }
        public void land_collision_end() { }
        public void timer() { }
        public void listen() { }
        public void on_rez() { }
        public void sensor() { }
        public void no_sensor() { }
        public void control() { }
        public void money() { }
        public void email() { }
        public void at_target() { }
        public void not_at_target() { }
        public void at_rot_target() { }
        public void not_at_rot_target() { }
        public void run_time_permissions() { }
        public void changed() { }
        public void attach() { }
        public void dataserver() { }
        public void link_message() { }
        public void moving_start() { }
        public void moving_end() { }
        public void object_rez() { }
        public void remote_data() { }
        public void http_response() { }

    }
}
