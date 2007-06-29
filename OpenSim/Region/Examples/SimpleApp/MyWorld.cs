using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Console;
using libsecondlife;
using OpenSim.Region.Environment;
using Avatar=OpenSim.Region.Environment.Scenes.ScenePresence;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework;
using OpenSim.Region.Caches;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;

namespace SimpleApp
{
    public class MyWorld : Scene
    {
        private RegionInfo m_regionInfo;
        private List<OpenSim.Region.Environment.Scenes.ScenePresence> m_avatars;

        public MyWorld(Dictionary<uint, IClientAPI> clientThreads, RegionInfo regionInfo, AuthenticateSessionsBase authen, CommunicationsManager commsMan, AssetCache assetCach, BaseHttpServer httpServer)
            : base(clientThreads, regionInfo, authen, commsMan, assetCach, httpServer)
        {
            m_regionInfo = regionInfo;
            m_avatars = new List<Avatar>();
        }

        public override void SendLayerData(IClientAPI remoteClient)
        {
            float[] map = new float[65536];

            for (int i = 0; i < 65536; i++)
            {
                int x = i % 256;
                int y = i / 256;

                map[i] = (float)(x + y / 2);
            }

            remoteClient.SendLayerData(map);
        }

        #region IWorld Members

        override public void AddNewClient(IClientAPI client, LLUUID agentID, bool child)

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
            
            client.OnRequestWearables += SendWearables;

            client.OnCompleteMovementToRegion += delegate()
                {
                    client.MoveAgentIntoRegion(m_regionInfo, pos, LLVector3.Zero );
                };

            client.OnCompleteMovementToRegion += delegate()
                 {
                     client.SendAvatarData(m_regionInfo.RegionHandle, client.FirstName,
                                           client.LastName, client.AgentId, 0,
                                           pos, null);
                                                         
                     client.SendChatMessage("Welcome to My World.", 1, pos, "System", LLUUID.Zero );
                 };

            client.SendRegionHandshake(m_regionInfo);

            OpenSim.Region.Environment.Scenes.ScenePresence avatar = new Avatar( client, this, m_regionInfo );
            
        }

        private void SendWearables( IClientAPI client )
        {
            client.SendWearables( AvatarWearable.DefaultWearables );
        }


       override public void RemoveClient(LLUUID agentID)
        {

        }

        public RegionInfo RegionInfo
        {
            get { return m_regionInfo; }
        }

        public object SyncRoot
        {
            get { return this; }
        }

        private uint m_nextLocalId = 1;

        public uint NextLocalId
        {
            get { return m_nextLocalId++; }
        }

        #endregion
    }
}
