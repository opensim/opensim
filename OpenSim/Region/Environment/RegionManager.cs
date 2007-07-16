using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Region.Capabilities;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.LandManagement;

namespace OpenSim.Region.Environment
{
    public class RegionManager //needs renaming , but first we need to rename the namespace
    {
        protected AuthenticateSessionsBase authenticateHandler;
        protected RegionCommsListener regionCommsHost;
        protected CommunicationsManager commsManager;
        protected List<Caps> capsHandlers = new List<Caps>();
        protected BaseHttpServer httpListener;

        protected Scene m_Scene;

        public LandManager LandManager;
        public EstateManager estateManager;

        public RegionManager()
        {

        }

    }
}
