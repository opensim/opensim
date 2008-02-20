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
/* Original code: Tedd Hansen */
using System;
using libsecondlife;
using OpenSim.Framework;

namespace OpenSim.Grid.ScriptEngine.DotNetEngine
{
    /// <summary>
    /// Prepares events so they can be directly executed upon a script by EventQueueManager, then queues it.
    /// </summary>
    [Serializable]
    internal class EventManager
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private ScriptEngine myScriptEngine;
        //public IScriptHost TEMP_OBJECT_ID;
        public EventManager(ScriptEngine _ScriptEngine)
        {
            myScriptEngine = _ScriptEngine;
            // TODO: HOOK EVENTS UP TO SERVER!
            //myScriptEngine.m_log.Info("[ScriptEngine]: EventManager Start");
            // TODO: ADD SERVER HOOK TO LOAD A SCRIPT THROUGH myScriptEngine.ScriptManager

            // Hook up a test event to our test form
            myScriptEngine.m_log.Info("[ScriptEngine]: Hooking up to server events");
            myScriptEngine.World.EventManager.OnObjectGrab += touch_start;
            myScriptEngine.World.EventManager.OnRezScript += OnRezScript;
            myScriptEngine.World.EventManager.OnRemoveScript += OnRemoveScript;
        }

        public void touch_start(uint localID, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            // Add to queue for all scripts in ObjectID object
            //myScriptEngine.m_log.Info("[ScriptEngine]: EventManager Event: touch_start");
            //Console.WriteLine("touch_start localID: " + localID);
            myScriptEngine.m_EventQueueManager.AddToObjectQueue(localID, "touch_start", new object[] {(int) 1});
        }

        public void OnRezScript(uint localID, LLUUID itemID, string script)
        {
            //myScriptEngine.myScriptManager.StartScript(
            //    Path.Combine("ScriptEngines", "Default.lsl"), 
            //    new OpenSim.Region.Environment.Scenes.Scripting.NullScriptHost()
            //);
            Console.WriteLine("OnRezScript localID: " + localID + " LLUID: " + itemID.ToString() + " Size: " +
                              script.Length);
            myScriptEngine.m_ScriptManager.StartScript(localID, itemID, script);
        }

        public void OnRemoveScript(uint localID, LLUUID itemID)
        {
            //myScriptEngine.myScriptManager.StartScript(
            //    Path.Combine("ScriptEngines", "Default.lsl"), 
            //    new OpenSim.Region.Environment.Scenes.Scripting.NullScriptHost()
            //);
            Console.WriteLine("OnRemoveScript localID: " + localID + " LLUID: " + itemID.ToString());
            myScriptEngine.m_ScriptManager.StopScript(
                localID,
                itemID
                );
        }

        // TODO: Replace placeholders below
        //  These needs to be hooked up to OpenSim during init of this class
        //   then queued in EventQueueManager.
        // When queued in EventQueueManager they need to be LSL compatible (name and params)

        //public void state_entry() { }
        public void state_exit()
        {
        }

        //public void touch_start() { }
        public void touch()
        {
        }

        public void touch_end()
        {
        }

        public void collision_start()
        {
        }

        public void collision()
        {
        }

        public void collision_end()
        {
        }

        public void land_collision_start()
        {
        }

        public void land_collision()
        {
        }

        public void land_collision_end()
        {
        }

        public void timer()
        {
        }

        public void listen()
        {
        }

        public void on_rez()
        {
        }

        public void sensor()
        {
        }

        public void no_sensor()
        {
        }

        public void control()
        {
        }

        public void money()
        {
        }

        public void email()
        {
        }

        public void at_target()
        {
        }

        public void not_at_target()
        {
        }

        public void at_rot_target()
        {
        }

        public void not_at_rot_target()
        {
        }

        public void run_time_permissions()
        {
        }

        public void changed()
        {
        }

        public void attach()
        {
        }

        public void dataserver()
        {
        }

        public void link_message()
        {
        }

        public void moving_start()
        {
        }

        public void moving_end()
        {
        }

        public void object_rez()
        {
        }

        public void remote_data()
        {
        }

        public void http_response()
        {
        }
    }
}
