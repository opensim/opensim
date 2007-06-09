using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Console;
using libsecondlife;

namespace SimpleApp
{
    public class MyWorld : IWorld
    {
        private RegionInfo m_regionInfo;

        public MyWorld(RegionInfo regionInfo)
        {
            m_regionInfo = regionInfo;
        }

        private void SendLayerData(IClientAPI remoteClient)
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

        void IWorld.AddNewAvatar(IClientAPI client, LLUUID agentID, bool child)
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
                    client.MoveAgentIntoRegion(m_regionInfo);
                };

            client.OnCompleteMovementToRegion += delegate()
                 {
                     client.SendAvatarData(m_regionInfo, client.FirstName,
                                           client.LastName, client.AgentId, 0,
                                           pos);
                                                         
                     client.SendChatMessage("Welcome to My World.", 1, pos, "System", LLUUID.Zero );
                 };

            client.SendRegionHandshake(m_regionInfo);

        }

        private void SendWearables( IClientAPI client )
        {
            client.SendWearables( AvatarWearable.DefaultWearables );
        }

        void IWorld.RemoveAvatar(LLUUID agentID)
        {

        }

        RegionInfo IWorld.RegionInfo
        {
            get { return m_regionInfo; }
        }

        object IWorld.SyncRoot
        {
            get { return this; }
        }

        private uint m_nextLocalId = 1;

        uint IWorld.NextLocalId
        {
            get { return m_nextLocalId++; }
        }

        #endregion


    }
}
