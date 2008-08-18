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
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.ScriptEngine.Common.ScriptEngineBase;


namespace OpenSim.Region.ScriptEngine.Common
{
    public class ScriptServerInterfaces
    {
        public interface RemoteEvents
        {
            void touch_start(uint localID, LLVector3 offsetPos, IClientAPI remoteClient);
            void OnRezScript(uint localID, LLUUID itemID, string script, int startParam, bool postOnRez);
            void OnRemoveScript(uint localID, LLUUID itemID);
            void state_exit(uint localID);
            void touch(uint localID, LLUUID itemID);
            void touch_end(uint localID, LLUUID itemID);
            void collision_start(uint localID, ColliderArgs col);
            void collision(uint localID, ColliderArgs col);
            void collision_end(uint localID, ColliderArgs col);
            void land_collision_start(uint localID, LLUUID itemID);
            void land_collision(uint localID, ColliderArgs col);
            void land_collision_end(uint localID, LLUUID itemID);
            void timer(uint localID, LLUUID itemID);
            void listen(uint localID, LLUUID itemID);
            void on_rez(uint localID, LLUUID itemID);
            void sensor(uint localID, LLUUID itemID);
            void no_sensor(uint localID, LLUUID itemID);
            void control(uint localID, LLUUID itemID, LLUUID agentID, uint held, uint change);
            void money(uint LocalID, LLUUID agentID, int amount);
            void email(uint localID, LLUUID itemID);
            void at_target(uint localID, uint handle, LLVector3 targetpos, LLVector3 atpos);
            void not_at_target(uint localID);
            void at_rot_target(uint localID, LLUUID itemID);
            void not_at_rot_target(uint localID, LLUUID itemID);
            void run_time_permissions(uint localID, LLUUID itemID);
            void changed(uint localID, LLUUID itemID);
            void attach(uint localID, LLUUID itemID);
            void dataserver(uint localID, LLUUID itemID);
            void link_message(uint localID, LLUUID itemID);
            void moving_start(uint localID, LLUUID itemID);
            void moving_end(uint localID, LLUUID itemID);
            void object_rez(uint localID, LLUUID itemID);
            void remote_data(uint localID, LLUUID itemID);
            void http_response(uint localID, LLUUID itemID);
        }

        public interface ServerRemotingObject
        {
            RemoteEvents Events();
        }

        public interface ScriptEngine
        {
            RemoteEvents EventManager();
            void InitializeEngine(Scene Sceneworld, IConfigSource config, bool DontHookUp, ScriptManager newScriptManager);
            ScriptManager GetScriptManager();
        }
    }
}
