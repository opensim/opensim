using OpenMetaverse.StructuredData;
using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.TimeZoneInfo;
using System.Security.AccessControl;

namespace OpenSim.Framework
{
    public abstract class EnvironmentUpdate
    {
        public string ObjectName = string.Empty;
        public UUID OwnerID = UUID.Zero;
        public string ParcelName = string.Empty;
        public int Permission = 0;
        public string Action = string.Empty;
        public float TransitionTime = 0.0f;

        public abstract OSDMap ToMap();
    }

    public class FullEnvironmentUpdate : EnvironmentUpdate
    {
        public UUID AssetID = UUID.Zero;
        public new string Action => "PushFullEnvironment";

        public override OSDMap ToMap()
        {
            OSDMap map = new OSDMap();

            map["ObjectName"] = ObjectName;
            map["OwnerID"] = OwnerID;
            map["ParcelName"] = ParcelName;
            map["Permission"] = Permission;
            map["action"] = Action;

            OSDMap action_data = new OSDMap();
            
            action_data["asset_id"] = AssetID;
            action_data["transition_time"] = TransitionTime;

            map["action_data"] = action_data;

            return map;
        }
    }

    public class PartialEnvironmentUpdate : EnvironmentUpdate
    {
        public OSDMap water = new OSDMap();
        public OSDMap sky = new OSDMap();
        public new string Action => "PushPartialEnvironment";

        public override OSDMap ToMap()
        {
            OSDMap map = new OSDMap();

            map["ObjectName"] = ObjectName;
            map["OwnerID"] = OwnerID;
            map["ParcelName"] = ParcelName;
            map["Permission"] = Permission;
            map["action"] = Action;

            OSDMap settings = new OSDMap();

            if (water.Count > 0)
                settings["water"] = water;

            if (sky.Count > 0)
                settings["sky"] = sky;

            OSDMap action_data = new OSDMap();
            
            action_data["settings"] = settings;
            action_data["transition_time"] = TransitionTime;

            map["action_data"] = action_data;

            return map;
        }
    }

    public class ClearEnvironmentUpdate : EnvironmentUpdate
    {
        public new string Action => "ClearEnvironment";

        public override OSDMap ToMap()
        {
            OSDMap map = new OSDMap();

            map["ObjectName"] = ObjectName;
            map["OwnerID"] = OwnerID;
            map["ParcelName"] = ParcelName;
            map["Permission"] = Permission;
            map["action"] = Action;

            OSDMap action_data = new OSDMap();
            action_data["transition_time"] = TransitionTime;

            map["action_data"] = action_data;

            return map;
        }
    }
}
