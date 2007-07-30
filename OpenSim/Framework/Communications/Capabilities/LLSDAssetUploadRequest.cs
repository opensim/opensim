using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Region.Capabilities
{
    [LLSDMap]
    public class LLSDAssetUploadRequest
    {
        public string asset_type = "";
        public string description = "";
        public LLUUID folder_id = LLUUID.Zero;
        public string inventory_type = "";
        public string name = "";

        public LLSDAssetUploadRequest()
        {
        }
    }
}
