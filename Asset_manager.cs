/*
Copyright (c) 2007 Michael Wright

* Copyright (c) <year>, <copyright holder>
* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE. */

using System;
using System.Collections.Generic;
using libsecondlife;
using System.Collections;
using libsecondlife.Packets;
using libsecondlife.AssetSystem;
using System.IO;

namespace Second_server
{
	/// <summary>
	/// Description of Asset_manager.
	/// </summary>
	public class Asset_manager
	{
		public Dictionary<libsecondlife.LLUUID,Asset_info> Assets;
		public ArrayList requests=new ArrayList();  //should change to a generic
	//	public ArrayList uploads=new ArrayList();
		private Server server;
		
		public Asset_manager(Server serve)
		{
			server=serve;
			Assets=new Dictionary<libsecondlife.LLUUID,Asset_info> ();
			this.initialise();
		}
		
		public void add_request(User_Agent_info user, LLUUID asset_id, TransferRequestPacket tran_req)
		{
			
			if(!this.Assets.ContainsKey(asset_id))
			{
				//not found asset	
				return;
			}
			Asset_info info=this.Assets[asset_id];
			System.Console.WriteLine("send asset : "+asset_id);
			//for now as it will be only skin or shape request just send back the asset
			TransferInfoPacket tran_i=new TransferInfoPacket();
			tran_i.TransferInfo.ChannelType=2;
			tran_i.TransferInfo.Status=0;
			tran_i.TransferInfo.TargetType=0;
			tran_i.TransferInfo.Params=tran_req.TransferInfo.Params;
			tran_i.TransferInfo.Size=info.data.Length;
			tran_i.TransferInfo.TransferID=tran_req.TransferInfo.TransferID;
			
			server.SendPacket(tran_i,true,user);
			
			TransferPacketPacket tran_p=new TransferPacketPacket();
			tran_p.TransferData.Packet=0;
			tran_p.TransferData.ChannelType=2;
			tran_p.TransferData.TransferID=tran_req.TransferInfo.TransferID;
			if(info.data.Length>1000)  //but needs to be less than 2000 at the moment
			{
				byte[] chunk=new byte[1000];
				Array.Copy(info.data,chunk,1000);
				tran_p.TransferData.Data=chunk;
				tran_p.TransferData.Status=0;
				server.SendPacket(tran_p,true,user);
				
				tran_p=new TransferPacketPacket();
				tran_p.TransferData.Packet=1;
				tran_p.TransferData.ChannelType=2;
				tran_p.TransferData.TransferID=tran_req.TransferInfo.TransferID;
				byte[] chunk1=new byte[(info.data.Length-1000)];
				Array.Copy(info.data,1000,chunk1,0,chunk1.Length);
				tran_p.TransferData.Data=chunk1;
				tran_p.TransferData.Status=1;
				server.SendPacket(tran_p,true,user);
			}
			else
			{
				tran_p.TransferData.Status=1;  //last packet? so set to 1
				tran_p.TransferData.Data=info.data;
				server.SendPacket(tran_p,true,user);
			}
			
			
			
		}
		
		private void initialise()
		{
			//for now read in our test image 
			Asset_info im=new Asset_info();
			im.filename="base_shape.dat";
			im.Full_ID=new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");		
			this.load_asset(im);
			this.Assets.Add(im.Full_ID,im);
		}
		private void load_asset(Asset_info info)
		{
			 string data_path=System.Windows.Forms.Application.StartupPath + @"\assets\";
			string filename=data_path+@info.filename;
			FileInfo fInfo = new FileInfo(filename);

			long numBytes = fInfo.Length;

			FileStream fStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
			byte[] idata=new byte[numBytes];
			BinaryReader br = new BinaryReader(fStream);
			idata= br.ReadBytes((int)numBytes);
			br.Close();
			fStream.Close();
			info.data=idata;
			info.loaded=true;
		}
	}
	
	public class Asset_request
	{
		public User_Agent_info req_user;
		public LLUUID req_image;
		public Asset_info asset_inf;
		public long data_pointer=0;
		public int num_packets=0;
		public int packet_counter=0;
		
		public Asset_request()
		{
			
		}
	}
	public class Asset_info
	{
		public byte[] data;
		public LLUUID Full_ID;
		public string name;
		public string filename;
		public bool loaded;
		public ulong last_used;  //need to add a tick/time counter and keep record
								// of how often images are requested to unload unused ones.
		
		public Asset_info()
		{
			
		}
	}
}
