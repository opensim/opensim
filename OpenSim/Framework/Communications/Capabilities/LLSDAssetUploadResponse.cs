using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.Capabilities
{
    [LLSDMap]
    public class LLSDAssetUploadResponse
    {
        public string uploader = "";
        public string state = "";

        public LLSDAssetUploadResponse()
        {

        }
    }
}
