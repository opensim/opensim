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

namespace OpenSim.Region.ScriptEngine.Yengine
{

    /**
     * @brief List of event codes that can be passed to StartEventHandler().
     *        Must have same name as corresponding event handler name, so
     *        the compiler will know what column in the seht to put the
     *        event handler entrypoint in.
     *
     *        Also, ScriptConst.Init() builds symbols of name XMREVENTCODE_<name>
     *        and XMREVENTMASK<n>_<name> with the values and masks of all symbols
     *        in range 0..63 that begin with a lower-case letter for scripts to
     *        reference.
     */
    public enum ScriptEventCode: int
    {

        // used by XMRInstance to indicate no event being processed
        None = -1,

        // must be bit numbers of equivalent values in ...
        // OpenSim.Region.ScriptEngine.Shared.ScriptBase.scriptEvents
        // ... so they can be passed to m_Part.SetScriptEvents().
        attach = 0,
        state_exit = 1,
        timer = 2,
        touch = 3,
        collision = 4,
        collision_end = 5,
        collision_start = 6,
        control = 7,
        dataserver = 8,
        email = 9,
        http_response = 10,
        land_collision = 11,
        land_collision_end = 12,
        land_collision_start = 13,
        at_target = 14,
        listen = 15,
        money = 16,
        moving_end = 17,
        moving_start = 18,
        not_at_rot_target = 19,
        not_at_target = 20,
        touch_start = 21,
        object_rez = 22,
        remote_data = 23,
        at_rot_target = 24,
        transaction_result = 25,
        run_time_permissions = 28,
        touch_end = 29,
        state_entry = 30,

        // events not passed to m_Part.SetScriptEvents().
        changed = 33,
        link_message = 34,
        no_sensor = 35,
        on_rez = 36,
        sensor = 37,
        http_request = 38,

        path_update = 40,

        // marks highest numbered event, ie, number of columns in seht.
        Size = 41
    }
}
