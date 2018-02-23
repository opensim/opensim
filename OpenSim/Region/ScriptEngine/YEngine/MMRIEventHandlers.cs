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
 *     * Neither the name of the OpenSimulator Project nor the
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

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.Yengine
{
    public interface IEventHandlers
    {
        void at_rot_target(int tnum, LSL_Rotation targetrot, LSL_Rotation ourrot);
        void at_target(int tnum, LSL_Vector targetpos, LSL_Vector ourpos);
        void attach(string id);
        void changed(int change);
        void collision(int num_detected);
        void collision_end(int num_detected);
        void collision_start(int num_detected);
        void control(string id, int held, int change);
        void dataserver(string queryid, string data);
        void email(string time, string address, string subj, string message, int num_left);
        void http_request(string request_id, string method, string body);
        void http_response(string request_id, int status, LSL_List metadata, string body);
        void land_collision(LSL_Vector pos);
        void land_collision_end(LSL_Vector pos);
        void land_collision_start(LSL_Vector pos);
        void link_message(int sender_num, int num, string str, string id);
        void listen(int channel, string name, string id, string message);
        void money(string id, int amount);
        void moving_end();
        void moving_start();
        void no_sensor();
        void not_at_rot_target();
        void not_at_target();
        void object_rez(string id);
        void on_rez(int start_param);
        void remote_data(int event_type, string channel, string message_id, string sender, int idata, string sdata);
        void run_time_permissions(int perm);
        void sensor(int num_detected);
        void state_entry();
        void state_exit();
        void timer();
        void touch(int num_detected);
        void touch_start(int num_detected);
        void touch_end(int num_detected);
        void transaction_result(string id, int success, string data);
        void path_update(int type, LSL_List data);
        void region_cross(LSL_Vector newpos, LSL_Vector oldpos);
    }
}
