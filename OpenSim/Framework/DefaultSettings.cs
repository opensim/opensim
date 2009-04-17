using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework
{
    public static class DefaultSettings
    {
        public const uint DefaultAssetServerHttpPort = 8003;
        public const uint DefaultRegionHttpPort = 9000;
        public static uint DefaultRegionRemotingPort = 8895; // This is actually assigned to, but then again, the remoting is obsolete, right?
        public const uint DefaultUserServerHttpPort = 8002;
        public const bool DefaultUserServerHttpSSL = false;
        public const uint DefaultMessageServerHttpPort = 8006;
        public const bool DefaultMessageServerHttpSSL = false;
        public const uint DefaultGridServerHttpPort = 8001;
        public const uint DefaultInventoryServerHttpPort = 8004;
    }
}
