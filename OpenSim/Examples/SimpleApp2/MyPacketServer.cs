using System;
using System.Collections.Generic;
using System.Text;
using OpenSim;
using OpenSim.Assets;
using System.Net;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework;

namespace SimpleApp2
{
    public class MyPacketServer : PacketServer
    {
        private float[] m_map;
        
        public MyPacketServer(float[] map, OpenSimNetworkHandler networkHandler, uint port ) : base( networkHandler, port )
        {
            m_map = map;
        }
        
        protected override ClientView CreateNewClient(EndPoint remoteEP, UseCircuitCodePacket initialcirpack, Dictionary<uint, ClientView> clientThreads, IWorld world, AssetCache assetCache, PacketServer packServer, InventoryCache inventoryCache, AuthenticateSessionsBase authenSessions)
        {
            // (EndPoint remoteEP, UseCircuitCodePacket initialcirpack, Dictionary<uint, ClientView> clientThreads, IWorld world, AssetCache assetCache, PacketServer packServer, InventoryCache inventoryCache, AuthenticateSessionsBase authenSessions)
        
            
            return new MyClientView(m_map, ClientAPIs, remoteEP, initialcirpack, clientThreads, world, assetCache, packServer, inventoryCache, authenSessions);
        }
    }
}
