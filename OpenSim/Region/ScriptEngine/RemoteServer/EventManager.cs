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

/* Original code: Tedd Hansen */

using System;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.ScriptEngine.Common;
using OpenSim.Region.ScriptEngine.Common.TRPC;

namespace OpenSim.Region.ScriptEngine.RemoteServer
{
    /// <summary>
    /// Handles events from OpenSim. Uses RemoteServer to send commands.
    /// </summary>
    [Serializable]
    internal class EventManager
    {
        // TODO: unused: System.Collections.Generic.Dictionary<uint, ScriptServerInterfaces.ServerRemotingObject> remoteScript = new System.Collections.Generic.Dictionary<uint, ScriptServerInterfaces.ServerRemotingObject>();
        TCPClient m_TCPClient;
        TRPC_Remote RPC;
        int myScriptServerID;

        string remoteHost = "127.0.0.1";
        int remotePort = 8010;

        private ScriptEngine myScriptEngine;
        public EventManager(ScriptEngine _ScriptEngine)
        {
            myScriptEngine = _ScriptEngine;

            m_TCPClient = new TCPClient();
            RPC = new TRPC_Remote(m_TCPClient);
            RPC.ReceiveCommand += new TRPC_Remote.ReceiveCommandDelegate(RPC_ReceiveCommand);
            myScriptServerID = m_TCPClient.ConnectAndReturnID(remoteHost, remotePort);

            myScriptEngine.Log.Info("[RemoteEngine]: Hooking up to server events");
            //myScriptEngine.World.EventManager.OnObjectGrab += touch_start;
            myScriptEngine.World.EventManager.OnRezScript += OnRezScript;
            //myScriptEngine.World.EventManager.OnRemoveScript += OnRemoveScript;
        }

        void RPC_ReceiveCommand(int ID, string Command, params object[] p)
        {
            myScriptEngine.Log.Info("[REMOTESERVER]: Received command: '" + Command + "'");
            if (p != null)
            {
                for (int i = 0; i < p.Length; i++)
                {
                    myScriptEngine.Log.Info("[REMOTESERVER]: Param " + i + ": " + p[i].ToString());
                }
            }
        }

        public void OnRezScript(uint localID, UUID itemID, string script, int startParam, bool postOnRez)
        {
            // WE ARE CREATING A NEW SCRIPT ... CREATE SCRIPT, GET A REMOTEID THAT WE MAP FROM LOCALID
            myScriptEngine.Log.Info("[RemoteEngine]: Creating new script (with connection)");

            // Temp for now: We have one connection only - this is hardcoded in myScriptServerID
            RPC.SendCommand(myScriptServerID, "OnRezScript", localID, itemID.ToString(), script);

            //ScriptServerInterfaces.ServerRemotingObject obj = myScriptEngine.m_RemoteServer.Connect("localhost", 1234);
            //remoteScript.Add(localID, obj);
            //remoteScript[localID].Events().OnRezScript(localID, itemID, script);
        }

        public void touch_start(uint localID, Vector3 offsetPos, IClientAPI remoteClient)
        {
            //remoteScript[localID].Events.touch_start(localID, offsetPos, remoteClient);
            RPC.SendCommand(myScriptServerID, "touch_start", offsetPos, "How to transfer IClientAPI?");
        }


        // PLACEHOLDERS -- CODE WILL CHANGE!


        //public void OnRemoveScript(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.OnRemoveScript(localID, itemID);
        //}

        //public void state_exit(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.state_exit(localID, itemID);
        //}

        //public void touch(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.touch(localID, itemID);
        //}

        //public void touch_end(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.touch_end(localID, itemID);
        //}

        //public void collision_start(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.collision_start(localID, itemID);
        //}

        //public void collision(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.collision(localID, itemID);
        //}

        //public void collision_end(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.collision_end(localID, itemID);
        //}

        //public void land_collision_start(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.land_collision_start(localID, itemID);
        //}

        //public void land_collision(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.land_collision(localID, itemID);
        //}

        //public void land_collision_end(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.land_collision_end(localID, itemID);
        //}

        //public void timer(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.timer(localID, itemID);
        //}

        //public void listen(uint localID, UUID itemID)
        //{
        //    remoteScript[localID].Events.listen(localID, itemID);
        //}

        //public void on_rez(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.on_rez(localID, itemID);
        //}

        //public void sensor(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.sensor(localID, itemID);
        //}

        //public void no_sensor(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.no_sensor(localID, itemID);
        //}

        //public void control(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.control(localID, itemID);
        //}

        //public void money(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.money(localID, itemID);
        //}

        //public void email(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.email(localID, itemID);
        //}

        //public void at_target(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.at_target(localID, itemID);
        //}

        //public void not_at_target(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.not_at_target(localID, itemID);
        //}

        //public void at_rot_target(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.at_rot_target(localID, itemID);
        //}

        //public void not_at_rot_target(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.not_at_rot_target(localID, itemID);
        //}

        //public void run_time_permissions(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.run_time_permissions(localID, itemID);
        //}

        //public void changed(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.changed(localID, itemID);
        //}

        //public void attach(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.attach(localID, itemID);
        //}

        //public void dataserver(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.dataserver(localID, itemID);
        //}

        //public void link_message(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.link_message(localID, itemID);
        //}

        //public void moving_start(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.moving_start(localID, itemID);
        //}

        //public void moving_end(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.moving_end(localID, itemID);
        //}

        //public void object_rez(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.object_rez(localID, itemID);
        //}

        //public void remote_data(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.remote_data(localID, itemID);
        //}

        //public void http_response(uint localID, UUID itemID)
        //{
        //        remoteScript[localID].Events.http_response(localID, itemID);
        //}
    }
}
