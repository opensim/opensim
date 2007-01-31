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
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using libsecondlife;
using libsecondlife.Packets;
using System.Collections;
using System.Text;
using System.IO;
using Axiom.MathLib;

namespace Second_server
{
	/// <summary>
	/// Description of MainForm.
	/// </summary>
	public partial class Main_server:Server_callback
	{
		
		[STAThread]
		public static void Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new Main_server());
		}
		public Server server;
		
		//public bool intin=false;
		//private libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock avatar_template;
		//private libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock object_template;

		private Agent_Manager agent_man;
		private Prim_manager prim_man;
		private Texture_manager texture_man;
		private Asset_manager asset_man;
		private Login_manager login_man;  //built in login server
		private ulong time;  //ticks 
		
		public Main_server()
		{
			//
			// The InitializeComponent() call is required for Windows Forms designer support.
			//
			InitializeComponent();
			
			//
			// TODO: Add constructor code after the InitializeComponent() call.
			//
			
			server=new Server(this);
			agent_man=new Agent_Manager(this.server);
			prim_man=new Prim_manager(this.server);
			texture_man=new Texture_manager(this.server);
			asset_man=new Asset_manager(this.server);
			prim_man.agent_man=agent_man;
			agent_man.prim_man=prim_man;
			login_man=new Login_manager();  // startup 
			login_man.startup();  			// login server
			
		}
		public  void main_callback(Packet pack, User_Agent_info User_info)
		{
			if((pack.Type!= PacketType.StartPingCheck) && (pack.Type!= PacketType.AgentUpdate))
			{
				//System.Console.WriteLine(pack.Type);
				//this.richTextBox1.Text=this.richTextBox1.Text+"\n  "+pack.Type;
			}
			if(pack.Type== PacketType.AgentSetAppearance)
			{
				//System.Console.WriteLine(pack);
				//this.richTextBox1.Text=this.richTextBox1.Text+"\n  "+pack.Type;
			
			}
			if(pack.Type== PacketType.TransferRequest)
			{
				TransferRequestPacket tran=(TransferRequestPacket)pack;
				LLUUID id=new LLUUID(tran.TransferInfo.Params,0);
		
				if((id==new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73")) ||(id==new LLUUID("e0ee49b5a4184df8d3c9a65361fe7f49")))
				{
				//System.Console.WriteLine(pack);
				//System.Console.WriteLine(tran.TransferInfo.TransferID);
				asset_man.add_request(User_info,id,tran);
				//this.richTextBox1.Text=this.richTextBox1.Text+"\n  "+pack.Type;
				}
				
			}
			if((pack.Type== PacketType.StartPingCheck) )
			{	
				//reply to pingcheck
				libsecondlife.Packets.StartPingCheckPacket startp=(libsecondlife.Packets.StartPingCheckPacket)pack;
				libsecondlife.Packets.CompletePingCheckPacket endping=new CompletePingCheckPacket();
				endping.PingID.PingID=startp.PingID.PingID;
				server.SendPacket(endping,true,User_info);
			}
			if(pack.Type==PacketType.CompleteAgentMovement)
			{
				// new client	
				agent_man.Agent_join(User_info);
			}
			if (pack.Type==PacketType.RequestImage)
			{	
				RequestImagePacket image_req=(RequestImagePacket)pack;
				for(int i=0; i<image_req.RequestImage.Length ;i++)
				{
					this.texture_man.add_request(User_info,image_req.RequestImage[i].Image);
			
				}
			}
			if (pack.Type==PacketType.RegionHandshakeReply)
			{	
				//recieved regionhandshake so can now start sending info
				agent_man.send_intial_data(User_info);
				//this.setuptemplates("objectupate164.dat",User_info,false);
			}
			if(pack.Type== PacketType.ObjectAdd)
			{
				ObjectAddPacket ad=(ObjectAddPacket)pack;
				prim_man.create_prim(User_info,ad.ObjectData.RayEnd, ad);
				//this.send_prim(User_info,ad.ObjectData.RayEnd, ad);
			}
			if(pack.Type== PacketType.ObjectPosition)
			{
				//System.Console.WriteLine(pack.ToString());
			}
			if(pack.Type== PacketType.MultipleObjectUpdate)
			{
				//System.Console.WriteLine(pack.ToString());
				MultipleObjectUpdatePacket mupd=(MultipleObjectUpdatePacket)pack;
				
				for(int i=0; i<mupd.ObjectData.Length; i++)
				{
					if(mupd.ObjectData[i].Type==9) //change position
					{
						libsecondlife.LLVector3 pos=new LLVector3(mupd.ObjectData[i].Data, 0);
					
						prim_man.update_prim_position(User_info, pos.X,pos.Y,pos.Z,mupd.ObjectData[i].ObjectLocalID);
						//should update stored position of the prim
					}
				}
			}
			if(pack.Type== PacketType.AgentWearablesRequest)
			{
				agent_man.send_intial_avatar_apper(User_info);
			}
					
			if(pack.Type==PacketType.AgentUpdate)
			{
				AgentUpdatePacket ag=(AgentUpdatePacket)pack;
				uint mask=ag.AgentData.ControlFlags&(1);
				Avatar_data m_av=agent_man.Get_Agent(User_info.AgentID);
				if(m_av!=null)
				{
				if(m_av.started)
				{
				if(mask==(1))
				{
					if(!m_av.walk)
					{
						//start walking
						agent_man.send_move_command(User_info,false,m_av.pos.X,m_av.pos.Y,m_av.pos.Z,0,ag.AgentData.BodyRotation);
						Axiom.MathLib.Vector3 v3=new Axiom.MathLib.Vector3(1,0,0);
						Axiom.MathLib.Quaternion q=new Axiom.MathLib.Quaternion(ag.AgentData.BodyRotation.W,ag.AgentData.BodyRotation.X,ag.AgentData.BodyRotation.Y,ag.AgentData.BodyRotation.Z);
						Axiom.MathLib.Vector3 direc=q*v3;
						direc.Normalize();
						direc=direc*((0.03f)*128f);
						
						m_av.vel.X=direc.x;
						m_av.vel.Y=direc.y;
						m_av.vel.Z=direc.z;
						m_av.walk=true;
					}
				}
				else{
					if(m_av.walk)
					{
						//walking but key not pressed so need to stop
						agent_man.send_move_command(User_info,true,m_av.pos.X,m_av.pos.Y,m_av.pos.Z,0,ag.AgentData.BodyRotation);
						m_av.walk=false; 
						m_av.vel.X=0;
						m_av.vel.Y=0;
						m_av.vel.Z=0;
					}
				}
				}}
			}
			
			if(pack.Type==PacketType.ChatFromViewer)
			{
				ChatFromViewerPacket chat=(ChatFromViewerPacket)pack;
				System.Text.Encoding enc = System.Text.Encoding.ASCII;
				
				string myString = enc.GetString(chat.ChatData.Message );
				if(myString!="")
				{
					string [] comp= new string[10];
					string delimStr = " ,	";		
            		char [] delimiter = delimStr.ToCharArray();
            		string line;
            
           		    line=myString;
              		comp=line.Split(delimiter);  
              		if(comp[0]=="pos")
              		{             		
              		}
              		else if(comp[0]=="veloc")
              		{             		
              		}
              		else
              		{
              			agent_man.send_chat_message(User_info,line);
              		
              		}
				}
			}
		}
		public void new_user(User_Agent_info User_info)
		{
			this.richTextBox1.Text=this.richTextBox1.Text+"\n  "+"new user - "+User_info.AgentID.ToString()+" - has joined";
			this.richTextBox1.Text=this.richTextBox1.Text+"\n  "+"session id := "+User_info.SessionID.ToString();
			agent_man.New_Agent(User_info);
			
		}
		
		public void error(string text)
		{
			this.richTextBox1.Text=this.richTextBox1.Text+"\n  error report: "+text;
		}
		
		void Timer1Tick(object sender, System.EventArgs e)
		{
			this.time++;
			agent_man.tick();
			texture_man.Do_work(time);
		}
		
		
	}
	
}
