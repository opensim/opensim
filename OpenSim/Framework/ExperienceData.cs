using OpenMetaverse;
using System.Collections.Generic;

namespace OpenSim.Framework
{
    public class ExperienceInfoData
    {
        public UUID public_id;
        public UUID owner_id;
        public UUID group_id;
        public string name;
        public string description;
        public UUID logo;
        public string marketplace;
        public string slurl;
        public int maturity;
        public int properties;


        public Dictionary<string, object> ToDictionary()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict["public_id"] = public_id;
            dict["owner_id"] = owner_id;
            dict["group_id"] = group_id;
            dict["name"] = name;
            dict["description"] = description;
            dict["logo"] = logo;
            dict["marketplace"] = marketplace;
            dict["slurl"] = slurl;
            dict["maturity"] = maturity;
            dict["properties"] = properties;
            return dict;
        }
    }
}
