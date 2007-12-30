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

namespace OpenSim.Region.ScriptEngine.RemoteServer
{
    /// <summary>
    /// </summary>
    [Serializable]
    internal class EventManager
    {

        System.Collections.Generic.Dictionary<uint, OpenSim.Grid.ScriptServer.RemotingObject> remoteScript = new System.Collections.Generic.Dictionary<uint, OpenSim.Grid.ScriptServer.RemotingObject>();


        private ScriptEngine myScriptEngine;
        public EventManager(ScriptEngine _ScriptEngine)
        {
            myScriptEngine = _ScriptEngine;

            myScriptEngine.Log.Verbose("RemoteEngine", "Hooking up to server events");
            //myScriptEngine.World.EventManager.OnObjectGrab += touch_start;
            myScriptEngine.World.EventManager.OnRezScript += OnRezScript;
            //myScriptEngine.World.EventManager.OnRemoveScript += OnRemoveScript;


        }


        public void OnRezScript(uint localID, LLUUID itemID, string script)
        {
            // WE ARE CREATING A NEW SCRIPT ... CREATE SCRIPT, GET A REMOTEID THAT WE MAP FROM LOCALID
            OpenSim.Grid.ScriptServer.RemotingObject obj = myScriptEngine.m_RemoteServer.Connect("localhost", 1234);
            remoteScript.Add(localID, obj);
            remoteScript[localID].ScriptEngine.m_EventManager.OnRezScript(localID, itemID, script);
        }

        public void touch_start(uint localID, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.touch_start(localID, offsetPos, remoteClient);
        }

        public void OnRemoveScript(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.OnRemoveScript(localID, itemID);
        }

        public void state_exit(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.state_exit(localID, itemID);
        }

        public void touch(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.touch(localID, itemID);
        }

        public void touch_end(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.touch_end(localID, itemID);
        }

        public void collision_start(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.collision_start(localID, itemID);
        }

        public void collision(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.collision(localID, itemID);
        }

        public void collision_end(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.collision_end(localID, itemID);
        }

        public void land_collision_start(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.land_collision_start(localID, itemID);
        }

        public void land_collision(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.land_collision(localID, itemID);
        }

        public void land_collision_end(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.land_collision_end(localID, itemID);
        }

        public void timer(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.timer(localID, itemID);
        }

        public void listen(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.listen(localID, itemID);
        }

        public void on_rez(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.on_rez(localID, itemID);
        }

        public void sensor(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.sensor(localID, itemID);
        }

        public void no_sensor(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.no_sensor(localID, itemID);
        }

        public void control(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.control(localID, itemID);
        }

        public void money(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.money(localID, itemID);
        }

        public void email(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.email(localID, itemID);
        }

        public void at_target(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.at_target(localID, itemID);
        }

        public void not_at_target(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.not_at_target(localID, itemID);
        }

        public void at_rot_target(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.at_rot_target(localID, itemID);
        }

        public void not_at_rot_target(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.not_at_rot_target(localID, itemID);
        }

        public void run_time_permissions(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.run_time_permissions(localID, itemID);
        }

        public void changed(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.changed(localID, itemID);
        }

        public void attach(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.attach(localID, itemID);
        }

        public void dataserver(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.dataserver(localID, itemID);
        }

        public void link_message(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.link_message(localID, itemID);
        }

        public void moving_start(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.moving_start(localID, itemID);
        }

        public void moving_end(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.moving_end(localID, itemID);
        }

        public void object_rez(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.object_rez(localID, itemID);
        }

        public void remote_data(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.remote_data(localID, itemID);
        }

        public void http_response(uint localID, LLUUID itemID)
        {
            remoteScript[localID].ScriptEngine.m_EventManager.http_response(localID, itemID);
        }

    }
}