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
using OpenSim.Region.Environment.Modules.Avatar.Currency.SampleMoney;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.ScriptEngine.XEngine.Script;
using Axiom.Math;

namespace OpenSim.Region.ScriptEngine.XEngine
{
    /// <summary>
    /// Prepares events so they can be directly executed upon a script by EventQueueManager, then queues it.
    /// </summary>
    public class EventManager
    {
        private XEngine myScriptEngine;

        public EventManager(XEngine _ScriptEngine)
        {
            myScriptEngine = _ScriptEngine;

            myScriptEngine.Log.Info("[XEngine] Hooking up to server events");
            myScriptEngine.World.EventManager.OnObjectGrab += touch_start;
            myScriptEngine.World.EventManager.OnObjectDeGrab += touch_end;
            myScriptEngine.World.EventManager.OnScriptChangedEvent += changed;
            myScriptEngine.World.EventManager.OnScriptAtTargetEvent += at_target;
            myScriptEngine.World.EventManager.OnScriptNotAtTargetEvent += not_at_target;
            myScriptEngine.World.EventManager.OnScriptControlEvent += control;
            IMoneyModule money=myScriptEngine.World.RequestModuleInterface<IMoneyModule>();
            if (money != null)
            {
                money.OnObjectPaid+=HandleObjectPaid;
            }
        }

        private void HandleObjectPaid(LLUUID objectID, LLUUID agentID,
                int amount)
        {
            SceneObjectPart part =
                    myScriptEngine.World.GetSceneObjectPart(objectID);

            if (part != null)
            {
                money(part.LocalId, agentID, amount);
            }
        }

        public void touch_start(uint localID, LLVector3 offsetPos,
                IClientAPI remoteClient)
        {
            // Add to queue for all scripts in ObjectID object
            XDetectParams[] det = new XDetectParams[1];
            det[0].Key = remoteClient.AgentId;

            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "touch_start", new Object[] { new LSL_Types.LSLInteger(1) },
                    det));
        }

        public void touch(uint localID, LLVector3 offsetPos,
                IClientAPI remoteClient)
        {
            // Add to queue for all scripts in ObjectID object
            XDetectParams[] det = new XDetectParams[1];
            det[0].Key = remoteClient.AgentId;
            det[0].OffsetPos = new LSL_Types.Vector3(offsetPos.X,
                                                     offsetPos.Y,
                                                     offsetPos.Z);

            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "touch", new Object[] { new LSL_Types.LSLInteger(1) },
                    det));
        }

        public void touch_end(uint localID, IClientAPI remoteClient)
        {
            // Add to queue for all scripts in ObjectID object
            XDetectParams[] det = new XDetectParams[1];
            det[0].Key = remoteClient.AgentId;

            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "touch_end", new Object[] { new LSL_Types.LSLInteger(1) },
                    det));
        }

        public void changed(uint localID, uint change)
        {
            // Add to queue for all scripts in localID, Object pass change.
            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "changed",new object[] { new LSL_Types.LSLInteger(change) },
                    new XDetectParams[0]));
        }

        // state_entry: not processed here
        // state_exit: not processed here

        public void money(uint localID, LLUUID agentID, int amount)
        {
            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "money", new object[] {
                    new LSL_Types.LSLString(agentID.ToString()),
                    new LSL_Types.LSLInteger(amount) },
                    new XDetectParams[0]));
        }

        public void collision_start(uint localID, LLUUID itemID,
                IClientAPI remoteClient)
        {
            // Add to queue for all scripts in ObjectID object
            XDetectParams[] det = new XDetectParams[1];
            det[0].Key = remoteClient.AgentId;

            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "collision_start",
                    new Object[] { new LSL_Types.LSLInteger(1) },
                    det));
        }

        public void collision(uint localID, LLUUID itemID,
                IClientAPI remoteClient)
        {
            // Add to queue for all scripts in ObjectID object
            XDetectParams[] det = new XDetectParams[1];
            det[0].Key = remoteClient.AgentId;

            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "collision", new Object[] { new LSL_Types.LSLInteger(1) },
                    det));
        }

        public void collision_end(uint localID, LLUUID itemID,
                IClientAPI remoteClient)
        {
            // Add to queue for all scripts in ObjectID object
            XDetectParams[] det = new XDetectParams[1];
            det[0].Key = remoteClient.AgentId;

            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "collision_end",
                    new Object[] { new LSL_Types.LSLInteger(1) },
                    det));
        }

        public void land_collision_start(uint localID, LLUUID itemID)
        {
            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "land_collision_start",
                    new object[0],
                    new XDetectParams[0]));
        }

        public void land_collision(uint localID, LLUUID itemID)
        {
            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "land_collision",
                    new object[0],
                    new XDetectParams[0]));
        }

        public void land_collision_end(uint localID, LLUUID itemID)
        {
            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "land_collision_end",
                    new object[0],
                    new XDetectParams[0]));
        }

        // timer: not handled here
        // listen: not handled here

        public void on_rez(uint localID, LLUUID itemID, int startParam)
        {
            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "on_rez",new object[] { 
                    new LSL_Types.LSLInteger(startParam)},
                    new XDetectParams[0]));
        }

        public void control(uint localID, LLUUID itemID, LLUUID agentID, uint held, uint change)
        {
            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "control",new object[] { 
                    new LSL_Types.LSLString(agentID.ToString()),
                    new LSL_Types.LSLInteger(held),
                    new LSL_Types.LSLInteger(change)},
                    new XDetectParams[0]));
        }

        public void email(uint localID, LLUUID itemID, string timeSent,
                string address, string subject, string message, int numLeft)
        {
            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "email",new object[] { 
                    new LSL_Types.LSLString(timeSent),
                    new LSL_Types.LSLString(address),
                    new LSL_Types.LSLString(subject),
                    new LSL_Types.LSLString(message),
                    new LSL_Types.LSLInteger(numLeft)},
                    new XDetectParams[0]));
        }

        public void at_target(uint localID, uint handle, LLVector3 targetpos,
                LLVector3 atpos)
        {
            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "at_target", new object[] {
                    new LSL_Types.LSLInteger(handle),
                    new LSL_Types.Vector3(targetpos.X,targetpos.Y,targetpos.Z),
                    new LSL_Types.Vector3(atpos.X,atpos.Y,atpos.Z) },
                    new XDetectParams[0]));
        }

        public void not_at_target(uint localID)
        {
            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "not_at_target",new object[0],
                    new XDetectParams[0]));
        }

        public void at_rot_target(uint localID, LLUUID itemID)
        {
            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "at_rot_target",new object[0],
                    new XDetectParams[0]));
        }

        public void not_at_rot_target(uint localID, LLUUID itemID)
        {
            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "not_at_rot_target",new object[0],
                    new XDetectParams[0]));
        }

        // run_time_permissions: not handled here

        public void attach(uint localID, LLUUID itemID, LLUUID avatar)
        {
            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "attach",new object[] {
                    new LSL_Types.LSLString(avatar.ToString()) },
                    new XDetectParams[0]));
        }

        // dataserver: not handled here
        // link_message: not handled here

        public void moving_start(uint localID, LLUUID itemID)
        {
            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "moving_start",new object[0],
                    new XDetectParams[0]));
        }

        public void moving_end(uint localID, LLUUID itemID)
        {
            myScriptEngine.PostObjectEvent(localID, new XEventParams(
                    "moving_end",new object[0],
                    new XDetectParams[0]));
        }

        // object_rez: not handled here
        // remote_data: not handled here
        // http_response: not handled here
    }
}
