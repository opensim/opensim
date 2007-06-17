using System;
using System.Collections.Generic;
using System.Text;
using OpenSim;
using libsecondlife;
using OpenSim.Framework.Interfaces;
using System.Net;
using libsecondlife.Packets;
using OpenSim.Assets;
using OpenSim.Framework.Types;
using OpenSim.Framework;
using OpenSim.Caches;

namespace SimpleApp2
{
    public class MyClientView : ClientView
    {
        private float[] m_map;
        private Dictionary<uint, IClientAPI> m_clientAPIs;

        public MyClientView(float[] map, Dictionary<uint, IClientAPI> clientAPIs, EndPoint remoteEP, UseCircuitCodePacket initialcirpack, Dictionary<uint, ClientView> clientThreads, IWorld world, AssetCache assetCache, PacketServer packServer, InventoryCache inventoryCache, AuthenticateSessionsBase authenSessions)
            : base(remoteEP, initialcirpack, clientThreads, world, assetCache, packServer, inventoryCache, authenSessions)
        {
            m_map = map;
            m_clientAPIs = clientAPIs;
            
            OnRegionHandShakeReply += RegionHandShakeReplyHandler;
            OnChatFromViewer += ChatHandler;
            OnRequestWearables += RequestWearablesHandler;
            OnCompleteMovementToRegion += CompleteMovementToRegionHandler;
        }

        private void ChatHandler(byte[] message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID)
        {            
            // Echo it (so you know what you typed)
            SendChatMessage(message, type, fromPos, fromName, fromAgentID);
            SendChatMessage("Ready.", 1, fromPos, "System", LLUUID.Zero);
        }
        
        private void CompleteMovementToRegionHandler()
        {
            LLVector3 pos = new LLVector3(128, 128, 128);

            MoveAgentIntoRegion(m_world.RegionInfo, pos, LLVector3.Zero );

            SendAvatarData( m_world.RegionInfo, FirstName,
                                           LastName, AgentId, 0,
                                           pos);

            SendChatMessage("Welcome to My World.", 1, pos, "System", LLUUID.Zero);

            
                
            // OpenSim.world.Primitive prim = new OpenSim.world.Primitive( m_clientAPIs, m_world.RegionInfo.RegionHandle, m_world, AgentId );

           // SendNewPrim( prim );
            
        }

        private void RegionHandShakeReplyHandler(IClientAPI client)
        {
            client.SendLayerData(m_map);
        }

        private void RequestWearablesHandler(IClientAPI client)
        {
            SendWearables(AvatarWearable.DefaultWearables);
        }
    }
}
