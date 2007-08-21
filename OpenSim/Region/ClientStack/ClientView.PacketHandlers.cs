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
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Console;

namespace OpenSim.Region.ClientStack
{
    public partial class ClientView
    {
        protected virtual void RegisterLocalPacketHandlers()
        {
            this.AddLocalPacketHandler(PacketType.LogoutRequest, this.Logout);
            this.AddLocalPacketHandler(PacketType.AgentCachedTexture, this.AgentTextureCached);
            this.AddLocalPacketHandler(PacketType.MultipleObjectUpdate, this.MultipleObjUpdate);
        }

        protected virtual bool Logout(ClientView simClient, Packet packet)
        {
            MainLog.Instance.Verbose( "OpenSimClient.cs:ProcessInPacket() - Got a logout request");
            //send reply to let the client logout
            LogoutReplyPacket logReply = new LogoutReplyPacket();
            logReply.AgentData.AgentID = this.AgentID;
            logReply.AgentData.SessionID = this.SessionID;
            logReply.InventoryData = new LogoutReplyPacket.InventoryDataBlock[1];
            logReply.InventoryData[0] = new LogoutReplyPacket.InventoryDataBlock();
            logReply.InventoryData[0].ItemID = LLUUID.Zero;
            OutPacket(logReply);
	    //
	    this.KillClient();
            return true;
        }

        protected bool AgentTextureCached(ClientView simclient, Packet packet)
        {
            // Console.WriteLine(packet.ToString());
            AgentCachedTexturePacket chechedtex = (AgentCachedTexturePacket)packet;
            AgentCachedTextureResponsePacket cachedresp = new AgentCachedTextureResponsePacket();
            cachedresp.AgentData.AgentID = this.AgentID;
            cachedresp.AgentData.SessionID = this.SessionID;
            cachedresp.AgentData.SerialNum = this.cachedtextureserial;
            this.cachedtextureserial++;
            cachedresp.WearableData = new AgentCachedTextureResponsePacket.WearableDataBlock[chechedtex.WearableData.Length];
            for (int i = 0; i < chechedtex.WearableData.Length; i++)
            {
                cachedresp.WearableData[i] = new AgentCachedTextureResponsePacket.WearableDataBlock();
                cachedresp.WearableData[i].TextureIndex = chechedtex.WearableData[i].TextureIndex;
                cachedresp.WearableData[i].TextureID = LLUUID.Zero;
                cachedresp.WearableData[i].HostName = new byte[0];
            }
            this.OutPacket(cachedresp);
            return true;
        }

        protected bool MultipleObjUpdate(ClientView simClient, Packet packet)
        {
            MultipleObjectUpdatePacket multipleupdate = (MultipleObjectUpdatePacket)packet;
           // System.Console.WriteLine("new multi update packet " + multipleupdate.ToString());
            for (int i = 0; i < multipleupdate.ObjectData.Length; i++)
            {
                #region position
                if (multipleupdate.ObjectData[i].Type == 9) //change position
                {
                    if (OnUpdatePrimGroupPosition != null)
                    {
                        LLVector3 pos = new LLVector3(multipleupdate.ObjectData[i].Data, 0);
                        OnUpdatePrimGroupPosition(multipleupdate.ObjectData[i].ObjectLocalID, pos, this);
                    }

                }
                else if (multipleupdate.ObjectData[i].Type == 1) //single item of group change position
                {
                    if (OnUpdatePrimSinglePosition != null)
                    {
                        libsecondlife.LLVector3 pos = new LLVector3(multipleupdate.ObjectData[i].Data, 0);
                       // System.Console.WriteLine("new movement position is " + pos.X + " , " + pos.Y + " , " + pos.Z);
                        OnUpdatePrimSinglePosition(multipleupdate.ObjectData[i].ObjectLocalID, pos, this);
                    }
                }
                #endregion position
                #region rotation
                else if (multipleupdate.ObjectData[i].Type == 2)// single item of group rotation from tab
                {
                    if (OnUpdatePrimSingleRotation != null)
                    {
                        LLQuaternion rot = new LLQuaternion(multipleupdate.ObjectData[i].Data, 0, true);
                       // Console.WriteLine("new rotation is " + rot.X + " , " + rot.Y + " , " + rot.Z + " , " + rot.W);
                        OnUpdatePrimSingleRotation(multipleupdate.ObjectData[i].ObjectLocalID, rot, this);
                    }
                }
                else if (multipleupdate.ObjectData[i].Type == 3)// single item of group rotation from mouse
                {
                    if (OnUpdatePrimSingleRotation != null)
                    {
                        libsecondlife.LLQuaternion rot = new LLQuaternion(multipleupdate.ObjectData[i].Data, 12, true);
                      //  Console.WriteLine("new rotation is " + rot.X + " , " + rot.Y + " , " + rot.Z + " , " + rot.W);
                        OnUpdatePrimSingleRotation(multipleupdate.ObjectData[i].ObjectLocalID, rot, this);
                    }
                }
                else if (multipleupdate.ObjectData[i].Type == 10)//group rotation from object tab
                {
                    if (OnUpdatePrimGroupRotation != null)
                    {
                        libsecondlife.LLQuaternion rot = new LLQuaternion(multipleupdate.ObjectData[i].Data, 0, true);
                      //  Console.WriteLine("new rotation is " + rot.X + " , " + rot.Y + " , " + rot.Z + " , " + rot.W);
                        OnUpdatePrimGroupRotation(multipleupdate.ObjectData[i].ObjectLocalID, rot, this);
                    }
                }
                else if (multipleupdate.ObjectData[i].Type == 11)//group rotation from mouse
                {
                    if (OnUpdatePrimGroupMouseRotation != null)
                    {
                        libsecondlife.LLVector3 pos = new LLVector3(multipleupdate.ObjectData[i].Data, 0);
                        libsecondlife.LLQuaternion rot = new LLQuaternion(multipleupdate.ObjectData[i].Data, 12, true);
                        //Console.WriteLine("new rotation position is " + pos.X + " , " + pos.Y + " , " + pos.Z);
                       // Console.WriteLine("new rotation is " + rot.X + " , " + rot.Y + " , " + rot.Z + " , " + rot.W);
                        OnUpdatePrimGroupMouseRotation(multipleupdate.ObjectData[i].ObjectLocalID, pos, rot, this);
                    }
                }
                #endregion
                #region scale
                else if (multipleupdate.ObjectData[i].Type == 13)//group scale from object tab
                {
                    if (OnUpdatePrimScale != null)
                    {
                        LLVector3 scale = new LLVector3(multipleupdate.ObjectData[i].Data, 12);
                        //Console.WriteLine("new scale is " + scale.X + " , " + scale.Y + " , " + scale.Z);
                        OnUpdatePrimScale(multipleupdate.ObjectData[i].ObjectLocalID, scale, this);

                        // Change the position based on scale (for bug number 246)
                        libsecondlife.LLVector3 pos = new LLVector3(multipleupdate.ObjectData[i].Data, 0);
                        // System.Console.WriteLine("new movement position is " + pos.X + " , " + pos.Y + " , " + pos.Z);
                        OnUpdatePrimSinglePosition(multipleupdate.ObjectData[i].ObjectLocalID, pos, this);
                    }
                }
                else if (multipleupdate.ObjectData[i].Type == 29)//group scale from mouse
                {
                    if (OnUpdatePrimScale != null)
                    {
                        libsecondlife.LLVector3 scale = new LLVector3(multipleupdate.ObjectData[i].Data, 0);
                       // Console.WriteLine("new scale is " + scale.X + " , " + scale.Y + " , " + scale.Z );
                       // OnUpdatePrimScale(multipleupdate.ObjectData[i].ObjectLocalID, scale, this);
                    }
                }
                else if (multipleupdate.ObjectData[i].Type == 5)//single prim scale from object tab
                {
                    if (OnUpdatePrimScale != null)
                    {
                        libsecondlife.LLVector3 scale = new LLVector3(multipleupdate.ObjectData[i].Data, 12);
                       // Console.WriteLine("new scale is " + scale.X + " , " + scale.Y + " , " + scale.Z);
                        OnUpdatePrimScale(multipleupdate.ObjectData[i].ObjectLocalID, scale, this);
                    }
                }
                else if (multipleupdate.ObjectData[i].Type == 21)//single prim scale from mouse
                {
                    if (OnUpdatePrimScale != null)
                    {
                        libsecondlife.LLVector3 scale = new LLVector3(multipleupdate.ObjectData[i].Data, 12);
                       // Console.WriteLine("new scale is " + scale.X + " , " + scale.Y + " , " + scale.Z);
                        OnUpdatePrimScale(multipleupdate.ObjectData[i].ObjectLocalID, scale, this);
                    }
                }
                #endregion
            }
            return true;
        }

        public void RequestMapLayer()
        {
            //should be getting the map layer from the grid server
            //send a layer covering the 800,800 - 1200,1200 area (should be covering the requested area)
            MapLayerReplyPacket mapReply = new MapLayerReplyPacket();
            mapReply.AgentData.AgentID = this.AgentID;
            mapReply.AgentData.Flags = 0;
            mapReply.LayerData = new MapLayerReplyPacket.LayerDataBlock[1];
            mapReply.LayerData[0] = new MapLayerReplyPacket.LayerDataBlock();
            mapReply.LayerData[0].Bottom = 0;
            mapReply.LayerData[0].Left = 0;
            mapReply.LayerData[0].Top = 30000;
            mapReply.LayerData[0].Right = 30000;
            mapReply.LayerData[0].ImageID = new LLUUID("00000000-0000-0000-9999-000000000006");
            this.OutPacket(mapReply);
        }

        public void RequestMapBlocks(int minX, int minY, int maxX, int maxY)
        {
            /*
            IList simMapProfiles = m_gridServer.RequestMapBlocks(minX, minY, maxX, maxY);
            MapBlockReplyPacket mbReply = new MapBlockReplyPacket();
            mbReply.AgentData.AgentID = this.AgentID;
            int len;
            if (simMapProfiles == null)
                len = 0;
            else
                len = simMapProfiles.Count;

            mbReply.Data = new MapBlockReplyPacket.DataBlock[len];
            int iii;
            for (iii = 0; iii < len; iii++)
            {
                Hashtable mp = (Hashtable)simMapProfiles[iii];
                mbReply.Data[iii] = new MapBlockReplyPacket.DataBlock();
                mbReply.Data[iii].Name = System.Text.Encoding.UTF8.GetBytes((string)mp["name"]);
                mbReply.Data[iii].Access = System.Convert.ToByte(mp["access"]);
                mbReply.Data[iii].Agents = System.Convert.ToByte(mp["agents"]);
                mbReply.Data[iii].MapImageID = new LLUUID((string)mp["map-image-id"]);
                mbReply.Data[iii].RegionFlags = System.Convert.ToUInt32(mp["region-flags"]);
                mbReply.Data[iii].WaterHeight = System.Convert.ToByte(mp["water-height"]);
                mbReply.Data[iii].X = System.Convert.ToUInt16(mp["x"]);
                mbReply.Data[iii].Y = System.Convert.ToUInt16(mp["y"]);
            }
            this.OutPacket(mbReply);
             */
        }
    }
}
