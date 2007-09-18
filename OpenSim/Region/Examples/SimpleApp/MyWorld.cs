using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;
 
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Terrain;
using OpenSim.Region.Environment;
using OpenSim.Framework.Communications.Caches;

using Avatar=OpenSim.Region.Environment.Scenes.ScenePresence;

namespace SimpleApp
{
    public class MyWorld : Scene
    {
        private List<ScenePresence> m_avatars;

        public MyWorld( RegionInfo regionInfo, AgentCircuitManager authen, CommunicationsManager commsMan, AssetCache assetCach, StorageManager storeMan, BaseHttpServer httpServer, ModuleLoader moduleLoader)
            : base( regionInfo, authen, commsMan, assetCach, storeMan, httpServer, moduleLoader)
        {
            m_avatars = new List<Avatar>();
        }

        public override void LoadWorldMap()
        {
            float[] map = new float[65536];

            for (int i = 0; i < 65536; i++)
            {
                int x = i % 256;
                int y = i / 256;

                map[i] = 25f;
            }

            this.Terrain.GetHeights1D(map);
            this.CreateTerrainTexture();
        }

        public override void ProcessObjectGrab(uint localID, LLVector3 offsetPos, IClientAPI remoteClient)
        {
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectGroup obj = ent as SceneObjectGroup;
                    
                    if( obj.HasChildPrim( localID ) )
                    {
                        obj.ObjectGrabHandler(localID, offsetPos, remoteClient);
                        return;
                    }                    
                }
            }
            
            base.ProcessObjectGrab(localID, offsetPos, remoteClient);
        }

        #region IWorld Members

        override public void AddNewClient(IClientAPI client, bool child)
        {
            SubscribeToClientEvents(client);

            ScenePresence avatar = CreateAndAddScenePresence(client, child );
            avatar.AbsolutePosition = new LLVector3(128, 128, 26);

            LLVector3 pos = new LLVector3(128, 128, 128);

            client.OnCompleteMovementToRegion += delegate()
                 {
                     client.SendChatMessage("Welcome to My World.", 1, pos, "System", LLUUID.Zero );
                 };


            client.SendRegionHandshake(m_regInfo);
        }

        #endregion
    }
}
