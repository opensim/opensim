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
using System.Timers;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using System.Collections;
using System.Text;
using System.IO;
using Axiom.MathLib;
using log4net;

namespace OpenSim
{
    /// <summary>
    /// Description of MainForm.
    /// </summary>
    public partial class Controller : ServerCallback {



        [STAThread]
        public static void Main( string[] args ) {
            Controller c = new Controller();
            while( true ) // fuckin' a
                System.Threading.Thread.Sleep( 1000 );

        }
        public Server server;
		public Logon _login;
        private AgentManager Agent_Manager;
        private PrimManager Prim_Manager;
        private TextureManager Texture_Manager;
        private AssetManager Asset_Manager;
        private GridManager Grid_Manager;
        private LoginManager Login_Manager;  //built in login server
        private ulong time;  //ticks 
        private Timer timer1 = new Timer();
        

        public Controller() {
        	_login=new Logon();  // should create a list for these.
            server = new Server( this );
            Agent_Manager = new AgentManager( this.server );
            Prim_Manager = new PrimManager( this.server );
            Texture_Manager = new TextureManager( this.server );
            Asset_Manager = new AssetManager( this.server );
            Prim_Manager.Agent_Manager = Agent_Manager;
            Agent_Manager.Prim_Manager = Prim_Manager;
            Grid_Manager=new GridManager(this.server,Agent_Manager);
            if(Globals.Instance.LoginSever)
            {
            	Console.WriteLine("Starting login Server");
           		Login_Manager = new LoginManager(_login);  // startup 
           		Login_Manager.Startup();  			// login server
            }
           	timer1.Enabled = true;
            timer1.Interval = 200;
            timer1.Elapsed +=new ElapsedEventHandler( this.Timer1Tick );
             

        }
        public void MainCallback( Packet pack, User_Agent_info User_info ) {
        	//System.Console.WriteLine(pack.Type);
        	if( ( pack.Type != PacketType.StartPingCheck ) && ( pack.Type != PacketType.AgentUpdate ) ) {
               // System.Console.WriteLine(pack.Type);
                //this.richTextBox1.Text=this.richTextBox1.Text+"\n  "+pack.Type;
            }
            if( pack.Type == PacketType.AgentSetAppearance ) {
              //  System.Console.WriteLine(pack);
                //this.richTextBox1.Text=this.richTextBox1.Text+"\n  "+pack.Type;

            }
        	if(pack.Type== PacketType.MapBlockRequest)
        	{
        		//int MinX, MinY, MaxX, MaxY;
        		MapBlockRequestPacket MapRequest=(MapBlockRequestPacket)pack;
        		this.Grid_Manager.RequestMapBlock(User_info,MapRequest.PositionData.MinX,MapRequest.PositionData.MinY,MapRequest.PositionData.MaxX,MapRequest.PositionData.MaxY);
        	
        	}
        	if(pack.Type== PacketType.CloseCircuit)
        	{
        		this.Agent_Manager.RemoveAgent(User_info);
        	}
        	if(pack.Type== PacketType.MapLayerRequest)
        	{
        		this.Grid_Manager.RequestMapLayer(User_info);
        		
        	}
        	if((pack.Type== PacketType.TeleportRequest ) ||(pack.Type== PacketType.TeleportLocationRequest))
        	{
        		TeleportLocationRequestPacket Request=(TeleportLocationRequestPacket)pack;
        		
        		this.Grid_Manager.RequestTeleport(User_info,Request);
        		
        	}
            if( pack.Type == PacketType.TransferRequest ) {
                TransferRequestPacket tran = (TransferRequestPacket)pack;
                LLUUID id = new LLUUID( tran.TransferInfo.Params, 0 );

                if( ( id == new LLUUID( "66c41e39-38f9-f75a-024e-585989bfab73" ) ) || ( id == new LLUUID( "e0ee49b5a4184df8d3c9a65361fe7f49" ) ) ) {
                    Asset_Manager.AddRequest( User_info, id, tran );
                }

            }
            if( ( pack.Type == PacketType.StartPingCheck ) ) {
                //reply to pingcheck
                libsecondlife.Packets.StartPingCheckPacket startp = (libsecondlife.Packets.StartPingCheckPacket)pack;
                libsecondlife.Packets.CompletePingCheckPacket endping = new CompletePingCheckPacket();
                endping.PingID.PingID = startp.PingID.PingID;
                server.SendPacket( endping, true, User_info );
            }
            if( pack.Type == PacketType.CompleteAgentMovement ) {
                // new client	
                Agent_Manager.AgentJoin( User_info );
            }
            if( pack.Type == PacketType.RequestImage ) {
                RequestImagePacket image_req = (RequestImagePacket)pack;
                for( int i = 0; i < image_req.RequestImage.Length; i++ ) {
                    this.Texture_Manager.AddRequest( User_info, image_req.RequestImage[ i ].Image );

                }
            }
            if( pack.Type == PacketType.RegionHandshakeReply ) {
                //recieved regionhandshake so can now start sending info
                Agent_Manager.SendInitialData( User_info );
                //this.setuptemplates("objectupate164.dat",User_info,false);
            }
            if( pack.Type == PacketType.ObjectAdd ) {
                ObjectAddPacket ad = (ObjectAddPacket)pack;
                Prim_Manager.CreatePrim( User_info, ad.ObjectData.RayEnd, ad );
                //this.send_prim(User_info,ad.ObjectData.RayEnd, ad);
            }
            if( pack.Type == PacketType.ObjectPosition ) {
                //System.Console.WriteLine(pack.ToString());
            }
            if( pack.Type == PacketType.MultipleObjectUpdate ) {
                //System.Console.WriteLine(pack.ToString());
                MultipleObjectUpdatePacket mupd = (MultipleObjectUpdatePacket)pack;

                for( int i = 0; i < mupd.ObjectData.Length; i++ ) {
                    if( mupd.ObjectData[ i ].Type == 9 ) //change position
					{
                        libsecondlife.LLVector3 pos = new LLVector3( mupd.ObjectData[ i ].Data, 0 );
                       // libsecondlife.LLQuaternion rot=new LLQuaternion(mupd.ObjectData[i].Data,12,true);
                        Prim_Manager.UpdatePrimPosition( User_info, pos, mupd.ObjectData[ i ].ObjectLocalID ,false ,libsecondlife.LLQuaternion.Identity);
                        //should update stored position of the prim
                    }
                    else if( mupd.ObjectData[ i ].Type == 10 )
                    {
                    	//System.Console.WriteLine(mupd.ObjectData[ i ].Type);
                    	//System.Console.WriteLine(mupd);
                    	libsecondlife.LLVector3 pos = new LLVector3(100,100,22);
                    	 libsecondlife.LLQuaternion rot=new LLQuaternion(mupd.ObjectData[i].Data,0,true);
                        Prim_Manager.UpdatePrimPosition( User_info, pos, mupd.ObjectData[ i ].ObjectLocalID ,true ,rot);
                       
                    }
                }
            }
            if( pack.Type == PacketType.AgentWearablesRequest ) {
                Agent_Manager.SendIntialAvatarAppearance( User_info );
            }

            if( pack.Type == PacketType.AgentUpdate ) {
                AgentUpdatePacket ag = (AgentUpdatePacket)pack;
                uint mask = ag.AgentData.ControlFlags & ( 1 );
                AvatarData m_av = Agent_Manager.GetAgent( User_info.AgentID );
                if( m_av != null ) {
                    if( m_av.Started ) {
                        if( mask == ( 1 ) ) {
                            if( !m_av.Walk ) {
                                //start walking
                                Agent_Manager.SendMoveCommand( User_info, false, m_av.Position.X, m_av.Position.Y, m_av.Position.Z, 0, ag.AgentData.BodyRotation );
                                Axiom.MathLib.Vector3 v3 = new Axiom.MathLib.Vector3( 1, 0, 0 );
                                Axiom.MathLib.Quaternion q = new Axiom.MathLib.Quaternion( ag.AgentData.BodyRotation.W, ag.AgentData.BodyRotation.X, ag.AgentData.BodyRotation.Y, ag.AgentData.BodyRotation.Z );
                                Axiom.MathLib.Vector3 direc = q * v3;
                                direc.Normalize();
                                direc = direc * ( ( 0.03f ) * 128f );

                                m_av.Velocity.X = direc.x;
                                m_av.Velocity.Y = direc.y;
                                m_av.Velocity.Z = direc.z;
                                m_av.Walk = true;
                            }
                        }
                        else {
                            if( m_av.Walk ) {
                                //walking but key not pressed so need to stop
                                Agent_Manager.SendMoveCommand( User_info, true, m_av.Position.X, m_av.Position.Y, m_av.Position.Z, 0, ag.AgentData.BodyRotation );
                                m_av.Walk = false;
                                m_av.Velocity.X = 0;
                                m_av.Velocity.Y = 0;
                                m_av.Velocity.Z = 0;
                            }
                        }
                    }
                }
            }

            if( pack.Type == PacketType.ChatFromViewer ) {
                ChatFromViewerPacket chat = (ChatFromViewerPacket)pack;
                System.Text.Encoding enc = System.Text.Encoding.ASCII;

                string myString = enc.GetString( chat.ChatData.Message );
                if( myString != "" ) {
                    string[] comp = new string[ 10 ];
                    string delimStr = " ,	";
                    char[] delimiter = delimStr.ToCharArray();
                    string line;

                    line = myString;
                    comp = line.Split( delimiter );
                    if( comp[ 0 ] == "pos" ) {
                    }
                    else if( comp[ 0 ] == "veloc" ) {
                    }
                    else {
                        Agent_Manager.SendChatMessage( User_info, line );

                    }
                }
            }
        }
        public void NewUserCallback( User_Agent_info UserInfo ) {
            Console.WriteLine( "new user - {0} - has joined [session {1}]", UserInfo.AgentID.ToString(), UserInfo.SessionID.ToString() +"curcuit used"+UserInfo.circuitCode);
             string first,last;
            lock(_login)
             {
                first=_login.first;
                last=_login.last;
                
                //should get agentid and sessionid so they can be checked.
             }
            Agent_Manager.NewAgent( UserInfo ,first,last);
            //now because of the lack of Global account management (User server etc)
            //we need to reset the names back to default incase a teleport happens 
            //which will not have a Login name set, so they will use default names
            lock(_login)
            {
                _login.first="Test";
                _login.last="User";
            }
        }

        public void ErrorCallback( string text ) {
            Console.WriteLine( "error report: {0}", text );
        }

        void Timer1Tick( object sender, System.EventArgs e ) {
            this.time++;
            Agent_Manager.UpdatePositions();
            Texture_Manager.DoWork( time );
        }
    }
    public class Logon
    {
    	public string first="Test";
    	public string last="User";
    	public LLUUID Agent;
    	public LLUUID Session;
    	public Logon()
    	{
    		
    	}
    }
}
