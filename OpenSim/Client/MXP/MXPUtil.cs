using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Client.MXP
{
    static class MXPUtil
    {
        public static string GenerateMXPURL(string server, int port, UUID bubbleID, Vector3 location)
        {
            return string.Format("mxp://{0}:{1}/{2}/{3}", server, port, bubbleID.Guid, location);
        }
    }
}
