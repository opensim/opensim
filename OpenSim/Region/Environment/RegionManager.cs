using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Framework.Types;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Region.Capabilities;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.LandManagement;

namespace OpenSim.Region.Environment
{
    public delegate TResult ModuleAPIMethod1<TResult, TParam0>(TParam0 param0);
    public delegate TResult ModuleAPIMethod2<TResult, TParam0, TParam1>(TParam0 param0, TParam1 param1);

    public class RegionManager
    {
        protected AgentCircuitManager authenticateHandler;
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
