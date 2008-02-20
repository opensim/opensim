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

namespace OpenSim.Grid.ScriptServer
{
    public abstract class RegionBase
    {
        // These are events that the region needs to have

        // TEMP: Using System.Delegate -- needs replacing with a real delegate
        public delegate void DefaultDelegate();

        public event DefaultDelegate onScriptRez;
        public event DefaultDelegate onstate_entry;
        public event DefaultDelegate onstate_exit;
        public event DefaultDelegate ontouch_start;
        public event DefaultDelegate ontouch;
        public event DefaultDelegate ontouch_end;
        public event DefaultDelegate oncollision_start;
        public event DefaultDelegate oncollision;
        public event DefaultDelegate oncollision_end;
        public event DefaultDelegate onland_collision_start;
        public event DefaultDelegate onland_collision;
        public event DefaultDelegate onland_collision_end;
        public event DefaultDelegate ontimer;
        public event DefaultDelegate onlisten;
        public event DefaultDelegate onon_rez;
        public event DefaultDelegate onsensor;
        public event DefaultDelegate onno_sensor;
        public event DefaultDelegate oncontrol;
        public event DefaultDelegate onmoney;
        public event DefaultDelegate onemail;
        public event DefaultDelegate onat_target;
        public event DefaultDelegate onnot_at_target;
        public event DefaultDelegate onat_rot_target;
        public event DefaultDelegate onnot_at_rot_target;
        public event DefaultDelegate onrun_time_permissions;
        public event DefaultDelegate onchanged;
        public event DefaultDelegate onattach;
        public event DefaultDelegate ondataserver;
        public event DefaultDelegate onlink_message;
        public event DefaultDelegate onmoving_start;
        public event DefaultDelegate onmoving_end;
        public event DefaultDelegate onobject_rez;
        public event DefaultDelegate onremote_data;
        public event DefaultDelegate onhttp_response;


        public void ScriptRez()
        {
            onScriptRez();
        }

        public void state_entry()
        {
            onstate_entry();
        }

        public void state_exit()
        {
            onstate_exit();
        }

        public void touch_start()
        {
            ontouch_start();
        }

        public void touch()
        {
            ontouch();
        }

        public void touch_end()
        {
            ontouch_end();
        }

        public void collision_start()
        {
            oncollision_start();
        }

        public void collision()
        {
            oncollision();
        }

        public void collision_end()
        {
            oncollision_end();
        }

        public void land_collision_start()
        {
            onland_collision_start();
        }

        public void land_collision()
        {
            onland_collision();
        }

        public void land_collision_end()
        {
            onland_collision_end();
        }

        public void timer()
        {
            ontimer();
        }

        public void listen()
        {
            onlisten();
        }

        public void on_rez()
        {
            onon_rez();
        }

        public void sensor()
        {
            onsensor();
        }

        public void no_sensor()
        {
            onno_sensor();
        }

        public void control()
        {
            oncontrol();
        }

        public void money()
        {
            onmoney();
        }

        public void email()
        {
            onemail();
        }

        public void at_target()
        {
            onat_target();
        }

        public void not_at_target()
        {
            onnot_at_target();
        }

        public void at_rot_target()
        {
            onat_rot_target();
        }

        public void not_at_rot_target()
        {
            onnot_at_rot_target();
        }

        public void run_time_permissions()
        {
            onrun_time_permissions();
        }

        public void changed()
        {
            onchanged();
        }

        public void attach()
        {
            onattach();
        }

        public void dataserver()
        {
            ondataserver();
        }

        public void link_message()
        {
            onlink_message();
        }

        public void moving_start()
        {
            onmoving_start();
        }

        public void moving_end()
        {
            onmoving_end();
        }

        public void object_rez()
        {
            onobject_rez();
        }

        public void remote_data()
        {
            onremote_data();
        }

        public void http_response()
        {
            onhttp_response();
        }
    }
}