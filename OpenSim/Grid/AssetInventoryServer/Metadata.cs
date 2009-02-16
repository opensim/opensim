using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Grid.AssetInventoryServer
{
    public class Metadata
    {
        public UUID ID;
        public string Name;
        public string Description;
        public DateTime CreationDate;
        public string ContentType;
        public byte[] SHA1;
        public bool Temporary;
        public Dictionary<string, Uri> Methods = new Dictionary<string, Uri>();
        public OSDMap ExtraData;

        public OSDMap SerializeToOSD()
        {
            OSDMap osdata = new OSDMap();

            if (ID != UUID.Zero) osdata["id"] = OSD.FromUUID(ID);
            osdata["name"] = OSD.FromString(Name);
            osdata["description"] = OSD.FromString(Description);
            osdata["creation_date"] = OSD.FromDate(CreationDate);
            osdata["type"] = OSD.FromString(ContentType);
            osdata["sha1"] = OSD.FromBinary(SHA1);
            osdata["temporary"] = OSD.FromBoolean(Temporary);

            OSDMap methods = new OSDMap(Methods.Count);
            foreach (KeyValuePair<string, Uri> kvp in Methods)
                methods.Add(kvp.Key, OSD.FromUri(kvp.Value));
            osdata["methods"] = methods;

            if (ExtraData != null) osdata["extra_data"] = ExtraData;

            return osdata;
        }

        public byte[] SerializeToBytes()
        {
            LitJson.JsonData jsonData = OSDParser.SerializeJson(SerializeToOSD());
            return System.Text.Encoding.UTF8.GetBytes(jsonData.ToJson());
        }

        public void Deserialize(byte[] data)
        {
            OSD osdata = OSDParser.DeserializeJson(System.Text.Encoding.UTF8.GetString(data));
            Deserialize(osdata);
        }

        public void Deserialize(string data)
        {
            OSD osdata = OSDParser.DeserializeJson(data);
            Deserialize(osdata);
        }

        public void Deserialize(OSD osdata)
        {
            if (osdata.Type == OSDType.Map)
            {
                OSDMap map = (OSDMap)osdata;
                ID = map["id"].AsUUID();
                Name = map["name"].AsString();
                Description = map["description"].AsString();
                CreationDate = map["creation_date"].AsDate();
                ContentType = map["type"].AsString();
                SHA1 = map["sha1"].AsBinary();
                Temporary = map["temporary"].AsBoolean();

                OSDMap methods = map["methods"] as OSDMap;
                if (methods != null)
                {
                    foreach (KeyValuePair<string, OSD> kvp in methods)
                        Methods.Add(kvp.Key, kvp.Value.AsUri());
                }

                ExtraData = map["extra_data"] as OSDMap;
            }
        }
    }
}
