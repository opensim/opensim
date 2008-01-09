using libsecondlife;
using OpenSim.Framework;

namespace OpenSim.Region.ScriptEngine.Common
{
    public class ScriptServerInterfaces
    {
        public interface RemoteEvents
        {
            void touch_start(uint localID, LLVector3 offsetPos, IClientAPI remoteClient);
            void OnRezScript(uint localID, LLUUID itemID, string script);
            void OnRemoveScript(uint localID, LLUUID itemID);
            void state_exit(uint localID, LLUUID itemID);
            void touch(uint localID, LLUUID itemID);
            void touch_end(uint localID, LLUUID itemID);
            void collision_start(uint localID, LLUUID itemID);
            void collision(uint localID, LLUUID itemID);
            void collision_end(uint localID, LLUUID itemID);
            void land_collision_start(uint localID, LLUUID itemID);
            void land_collision(uint localID, LLUUID itemID);
            void land_collision_end(uint localID, LLUUID itemID);
            void timer(uint localID, LLUUID itemID);
            void listen(uint localID, LLUUID itemID);
            void on_rez(uint localID, LLUUID itemID);
            void sensor(uint localID, LLUUID itemID);
            void no_sensor(uint localID, LLUUID itemID);
            void control(uint localID, LLUUID itemID);
            void money(uint localID, LLUUID itemID);
            void email(uint localID, LLUUID itemID);
            void at_target(uint localID, LLUUID itemID);
            void not_at_target(uint localID, LLUUID itemID);
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
        }

    }
}
