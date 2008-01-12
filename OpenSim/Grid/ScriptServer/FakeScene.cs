using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Grid.ScriptServer
{
    public class FakeScene: Scene
    {
        public FakeScene(RegionInfo regInfo, AgentCircuitManager authen, PermissionManager permissionManager,
                         CommunicationsManager commsMan, SceneCommunicationService sceneGridService,
                         AssetCache assetCach, StorageManager storeManager, BaseHttpServer httpServer,
                         ModuleLoader moduleLoader, bool dumpAssetsToFile, bool physicalPrim, bool sendTasksToChild)
            : base(
                regInfo, authen, permissionManager, commsMan, sceneGridService, assetCach, storeManager, httpServer,
                moduleLoader, dumpAssetsToFile, physicalPrim, sendTasksToChild)
        {
        }

        // What does a scene have to do? :P
    }
}
