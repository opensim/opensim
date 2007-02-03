/*
Copyright (c) OpenSim project, http://sim.opensecondlife.org/

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
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using libsecondlife;
using System.Collections;
using libsecondlife.Packets;
using libsecondlife.AssetSystem;
using System.IO;


namespace OpenSim
{
	/// <summary>
	/// Description of Texture_manager.
	/// </summary>
	public class TextureManager
	{
		public Dictionary<libsecondlife.LLUUID,TextureImage> textures;
		public ArrayList requests=new ArrayList();  //should change to a generic
		public ArrayList uploads=new ArrayList();
		private Server server;
		
		public TextureManager(Server serve)
		{
			server=serve;
			textures=new Dictionary<libsecondlife.LLUUID,TextureImage> ();
			this.initialise();
		}
		
		public void AddRequest(User_Agent_info user, LLUUID image_id)
		{
			
			if(!this.textures.ContainsKey(image_id))
			{
				//not found image so send back image not in data base message
				ImageNotInDatabasePacket im_not=new ImageNotInDatabasePacket();
				im_not.ImageID.ID=image_id;
				server.SendPacket(im_not,true,user);
				return;
			}
			TextureImage imag=this.textures[image_id];
			TextureRequest req=new TextureRequest();
			req.RequestUser=user;
			req.RequestImage=image_id;
			req.image_info=imag;
			
			if(imag.data.LongLength>1000)  //should be bigger or smaller?
			{
				//over 1000 bytes so split up file
				req.num_packets=(int)imag.data.LongLength/1000;
				req.num_packets++;
			}
			else
			{
				req.num_packets=1;
			}
			
			this.requests.Add(req);
			
		}
		
		public void AddTexture(LLUUID image_id, string name, byte[] data)
		{
			
		}
		public void DoWork(ulong time)
		{
			if(this.requests.Count==0)
			{
				//no requests waiting
				return;
			}
			int num;
			//should be running in its own thread but for now is called by timer
			if(this.requests.Count<5)
			{
				//lower than 5 so do all of them
				num=this.requests.Count;
			}
			else
			{
				num=5;
			}
			TextureRequest req;
			for(int i=0; i<num; i++)
			{
				req=(TextureRequest)this.requests[i];
				
				if(req.packet_counter==0)
				{
					//first time for this request so send imagedata packet
					if(req.num_packets==1)
					{		
						//only one packet so send whole file
						ImageDataPacket im=new ImageDataPacket();
						im.ImageID.Packets=1;
						im.ImageID.ID=req.image_info.Full_ID;
						im.ImageID.Size=(uint)req.image_info.data.Length;
						im.ImageData.Data=req.image_info.data;
						im.ImageID.Codec=2;		
						server.SendPacket(im,true,req.RequestUser);
						req.packet_counter++;
						req.image_info.last_used=time;
						System.Console.WriteLine("sent texture: "+req.image_info.Full_ID);
					}
					else
					{
						//more than one packet so split file up
					}
				}
				else
				{
					//send imagepacket 
					
				}
			}
			
			//remove requests that have been completed
			for(int i=0; i<num; i++)
			{
				req=(TextureRequest)this.requests[i];
				if(req.packet_counter==req.num_packets)
				{
					this.requests.Remove(req);
				}
			}
		}
		
		public void RecieveTexture(Packet pack)
		{
			
		}
		
		private void initialise()
		{
			
			TextureImage im=new TextureImage();
			im.filename="testpic2.jp2";
			im.Full_ID=new LLUUID("00000000-0000-0000-5005-000000000005");
			im.Name="test Texture";
			this.LoadImage(im);
			this.textures.Add(im.Full_ID,im);
			
			//Change these filenames to images you want to use. 
			im=new TextureImage();
			im.filename="map_base.jp2";
			im.Full_ID=new LLUUID("00000000-0000-0000-7007-000000000006");
			this.LoadImage(im);
			this.textures.Add(im.Full_ID,im);
			
			im=new TextureImage();
			im.filename="map1.jp2";
			im.Full_ID=new LLUUID("00000000-0000-0000-7009-000000000008");
			this.LoadImage(im);
			this.textures.Add(im.Full_ID,im);
			
		}
		private void LoadImage(TextureImage im)
		{
			//should request Image from StorageManager
			//but for now read from file
			
			string data_path=System.AppDomain.CurrentDomain.BaseDirectory + @"\textures\";
			string filename=data_path+@im.filename;
			FileInfo fInfo = new FileInfo(filename);

			long numBytes = fInfo.Length;

			FileStream fStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
			byte[] idata=new byte[numBytes];
			BinaryReader br = new BinaryReader(fStream);
			idata= br.ReadBytes((int)numBytes);
			br.Close();
			fStream.Close();
			im.data=idata;
			im.loaded=true;
		}
		
	}
	
	public class TextureRequest
	{
		public User_Agent_info RequestUser;
		public LLUUID RequestImage;
		public TextureImage image_info;
		public long data_pointer=0;
		public int num_packets=0;
		public int packet_counter=0;
		
		public TextureRequest()
		{
			
		}
	}
	public class TextureImage: AssetBase
	{
		//public byte[] data;
		//public LLUUID Full_ID;
		//public string name;
		public string filename;
		public bool loaded;
		public ulong last_used;  //need to add a tick/time counter and keep record
								// of how often images are requested to unload unused ones.
		
		public TextureImage()
		{
			
		}
	}
}
