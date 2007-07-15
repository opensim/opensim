using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;
using OpenSim.Region.Caches;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Terrain;
using OpenSim.Region.Environment;

using Avatar=OpenSim.Region.Environment.Scenes.ScenePresence;

namespace SimpleApp
{
    public class MyWorld : Scene
    {
        private List<ScenePresence> m_avatars;

        public MyWorld(ClientManager clientManager, RegionInfo regionInfo, AuthenticateSessionsBase authen, CommunicationsManager commsMan, AssetCache assetCach, StorageManager storeMan, BaseHttpServer httpServer)
            : base(clientManager, regionInfo, authen, commsMan, assetCach, storeMan, httpServer)
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

            this.Terrain.setHeights1D(map);
            this.CreateTerrainTexture();
        }

        #region IWorld Members

        override public void AddNewClient(IClientAPI client, bool child)
        {
            LLVector3 pos = new LLVector3(128, 128, 128);
            
            client.OnRegionHandShakeReply += SendLayerData;
            client.OnChatFromViewer +=
                delegate(byte[] message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID)
                    {
                        // Echo it (so you know what you typed)
                        client.SendChatMessage(message, type, fromPos, fromName, fromAgentID);
                        client.SendChatMessage("Ready.", 1, pos, "System", LLUUID.Zero );
                    };
            
            client.OnAddPrim += AddNewPrim;
            client.OnUpdatePrimGroupPosition += this.UpdatePrimPosition;
            client.OnRequestMapBlocks += this.RequestMapBlocks;
            client.OnTeleportLocationRequest += this.RequestTeleportLocation;
            client.OnGrapUpdate += this.MoveObject;
            client.OnNameFromUUIDRequest += this.commsManager.HandleUUIDNameRequest;
            
            client.OnCompleteMovementToRegion += delegate()
                 {
                     client.SendChatMessage("Welcome to My World.", 1, pos, "System", LLUUID.Zero );
                 };

            client.SendRegionHandshake(m_regInfo);

            ScenePresence avatar = CreateAndAddScenePresence(client);
            avatar.Pos = new LLVector3(128, 128, 26);        
        }
        
        #endregion
    }
}
