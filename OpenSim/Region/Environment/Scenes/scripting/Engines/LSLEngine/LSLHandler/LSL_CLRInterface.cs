using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.ScriptEngines.LSL
{
    public class LSL_CLRInterface
    {
        public interface LSLScript
        {
            //public virtual void Run(object arg)
            //{
            //}
            //void Run(object arg);

            void event_state_entry(object arg);
            //void event_state_exit();
            void event_touch_start(object arg);
            //void event_touch();
            //void event_touch_end();
            //void event_collision_start();
            //void event_collision();
            //void event_collision_end();
            //void event_land_collision_start();
            //void event_land_collision();
            //void event_land_collision_end();
            //void event_timer();
            //void event_listen();
            //void event_on_rez();
            //void event_sensor();
            //void event_no_sensor();
            //void event_control();
            //void event_money();
            //void event_email();
            //void event_at_target();
            //void event_not_at_target();
            //void event_at_rot_target();
            //void event_not_at_rot_target();
            //void event_run_time_permissions();
            //void event_changed();
            //void event_attach();
            //void event_dataserver();
            //void event_link_message();
            //void event_moving_start();
            //void event_moving_end();
            //void event_object_rez();
            //void event_remote_data();
            //void event_http_response();
        }
    }
}
