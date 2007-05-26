using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Framework.Types
{
    public class AssetLandmark : AssetBase
    {
        public int Version;
        public LLVector3 Position;
        public LLUUID RegionID;

        public AssetLandmark(AssetBase a)
        {
            this.Data = a.Data;
            this.FullID = a.FullID;
            this.Type = a.Type;
            this.InvType = a.InvType;
            this.Name = a.Name;
            this.Description = a.Description;
            InternData();
        }

        private void InternData()
        {
            string temp = System.Text.Encoding.UTF8.GetString(Data).Trim(); 
            string[] parts = temp.Split('\n');
            int.TryParse(parts[0].Substring(17, 1), out Version);
            LLUUID.TryParse(parts[1].Substring(10, 36), out RegionID);
            LLVector3.TryParse(parts[2].Substring(11, parts[2].Length - 11), out Position);
        }
    }
}
