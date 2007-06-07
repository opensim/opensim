/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.RegionServer.Simulator;
using OpenSim.RegionServer.Client;
using libsecondlife.Packets;

namespace OpenSim.RegionServer
{
    public class PacketServer
    {
        private OpenSimNetworkHandler _networkHandler;
        private World _localWorld;
        public Dictionary<uint, ClientView> ClientThreads = new Dictionary<uint, ClientView>();

        public PacketServer(OpenSimNetworkHandler networkHandler)
        {
            _networkHandler = networkHandler;
            _networkHandler.RegisterPacketServer(this);
        }

        public World LocalWorld
        {
            set
            {
                this._localWorld = value;
            }
        }

        public virtual void ClientInPacket(uint circuitCode, Packet packet)
        {
            if (this.ClientThreads.ContainsKey(circuitCode))
            {
                ClientThreads[circuitCode].InPacket(packet);
            }
        }

        public virtual bool AddNewCircuitCodeClient(uint circuitCode)
        {
            return false;
        }

        public virtual void SendPacketToAllClients(Packet packet)
        {

        }

        public virtual void SendPacketToAllExcept(Packet packet, ClientView simClient)
        {

        }

        public virtual void AddClientPacketHandler(PacketType packetType, PacketMethod handler)
        {

        }

        public virtual void RegisterClientPacketHandlers()
        {
            if (this._localWorld != null)
            {
                ClientView.AddPacketHandler(PacketType.UUIDNameRequest, this.RequestUUIDName);
            }
        }

        #region Client Packet Handlers

        public bool RequestUUIDName(ClientView simClient, Packet packet)
        {
            System.Text.Encoding enc = System.Text.Encoding.ASCII;
            Console.WriteLine(packet.ToString());
            UUIDNameRequestPacket nameRequest = (UUIDNameRequestPacket)packet;
            UUIDNameReplyPacket nameReply = new UUIDNameReplyPacket();
            nameReply.UUIDNameBlock = new UUIDNameReplyPacket.UUIDNameBlockBlock[nameRequest.UUIDNameBlock.Length];

            for (int i = 0; i < nameRequest.UUIDNameBlock.Length; i++)
            {
                nameReply.UUIDNameBlock[i] = new UUIDNameReplyPacket.UUIDNameBlockBlock();
                nameReply.UUIDNameBlock[i].ID = nameRequest.UUIDNameBlock[i].ID;
                nameReply.UUIDNameBlock[i].FirstName = enc.GetBytes("Who\0");  //for now send any name
                nameReply.UUIDNameBlock[i].LastName = enc.GetBytes("Knows\0");	   //in future need to look it up		
            }
            simClient.OutPacket(nameReply);
            return true;
        }

        #endregion
    }
}
