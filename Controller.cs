/*
Copyright (c) OpenSim project, http://sim.opensecondlife.org/


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
    public partial class Controller : ServerCallback 
    {

        [STAThread]
        public static void Main( string[] args ) 
        {
            Controller c = new Controller();
            while( true ) // fuckin' a
            System.Threading.Thread.Sleep( 1000 );
        }
        
        private Server _server;
		private Logon _login;
        private AgentManager _agentManager;
        private PrimManager _primManager;
        private AssetManagement _assetManager;
        private GridManager _gridManager;
        private InventoryManager _inventoryManager;
        private LoginManager _loginManager;  //built in login server
        private ulong time;  //ticks 
        private Timer timer1 = new Timer();
        private System.Text.Encoding _enc = System.Text.Encoding.ASCII;
      
        public Controller() {
        	_login = new Logon();  // should create a list for these.
            _server = new Server( this );
            _agentManager = new AgentManager( this._server );
            _primManager = new PrimManager( this._server );
            _inventoryManager = new InventoryManager(this._server);
            _assetManager = new AssetManagement(this._server, _inventoryManager );
            _primManager.AgentManagement = _agentManager;
            _agentManager.Prim_Manager = _primManager;
            _agentManager.assetManager = _assetManager;
            _gridManager = new GridManager(this._server, _agentManager);
            
            if(Globals.Instance.LoginSever)
            {
            	Console.WriteLine("Starting login Server");
           		_loginManager = new LoginManager(_login);  // startup 
           		_loginManager.Startup();  			// login server
            }
            
           	timer1.Enabled = true;
            timer1.Interval = 200;
            timer1.Elapsed +=new ElapsedEventHandler( this.Timer1Tick );
             

        }
        public void MainCallback( Packet pack, UserAgentInfo userInfo ) 
        {
        	
        	/*if( ( pack.Type != PacketType.StartPingCheck ) && ( pack.Type != PacketType.AgentUpdate ) ) {
             	//Log packet?
        		//System.Console.WriteLine(pack.Type);
                //this.richTextBox1.Text = this.richTextBox1.Text + "\n  " + pack.Type;
            }*/
        	
        	//should replace with a switch
            if( pack.Type == PacketType.AgentSetAppearance ) {
            	
            }
            else if (pack.Type == PacketType.AgentAnimation) 
            {
                AgentAnimationPacket AgentAni = (AgentAnimationPacket)pack;
                if (AgentAni.AgentData.AgentID == userInfo.AgentID)
                {
                    _agentManager.UpdateAnim(userInfo, AgentAni.AnimationList[0].AnimID, 1);
                }
            }
            else if (pack.Type == PacketType.FetchInventory)
            {
                FetchInventoryPacket FetchInventory = (FetchInventoryPacket)pack;
                _inventoryManager.FetchInventory(userInfo, FetchInventory);
            }
            else if (pack.Type == PacketType.FetchInventoryDescendents)
            {
                FetchInventoryDescendentsPacket Fetch = (FetchInventoryDescendentsPacket)pack;
                _inventoryManager.FetchInventoryDescendents(userInfo, Fetch);
            }
            else if (pack.Type == PacketType.MapBlockRequest)
            {
                MapBlockRequestPacket MapRequest = (MapBlockRequestPacket)pack;
                this._gridManager.RequestMapBlock(userInfo, MapRequest.PositionData.MinX, MapRequest.PositionData.MinY, MapRequest.PositionData.MaxX, MapRequest.PositionData.MaxY);

            }
            else if (pack.Type == PacketType.UUIDNameRequest)
            {
                UUIDNameRequestPacket nameRequest = (UUIDNameRequestPacket)pack;
                UUIDNameReplyPacket nameReply = new UUIDNameReplyPacket();
                nameReply.UUIDNameBlock = new UUIDNameReplyPacket.UUIDNameBlockBlock[nameRequest.UUIDNameBlock.Length];

                for (int i = 0; i < nameRequest.UUIDNameBlock.Length; i++)
                {
                    nameReply.UUIDNameBlock[i] = new UUIDNameReplyPacket.UUIDNameBlockBlock();
                    nameReply.UUIDNameBlock[i].ID = nameRequest.UUIDNameBlock[i].ID;
                    nameReply.UUIDNameBlock[i].FirstName = _enc.GetBytes("harry \0");  //for now send any name
                    nameReply.UUIDNameBlock[i].LastName = _enc.GetBytes("tom \0");	   //in future need to look it up		
                }

                _server.SendPacket(nameReply, true, userInfo);
            }
            else if (pack.Type == PacketType.CloseCircuit)
            {
                this._agentManager.RemoveAgent(userInfo);
            }
            else if (pack.Type == PacketType.MapLayerRequest)
            {
                this._gridManager.RequestMapLayer(userInfo);
            }
            else if ((pack.Type == PacketType.TeleportRequest) || (pack.Type == PacketType.TeleportLocationRequest))
            {
                TeleportLocationRequestPacket Request = (TeleportLocationRequestPacket)pack;
                this._gridManager.RequestTeleport(userInfo, Request);

            }
            else if (pack.Type == PacketType.TransferRequest)
            {
                TransferRequestPacket transfer = (TransferRequestPacket)pack;
                LLUUID id = new LLUUID(transfer.TransferInfo.Params, 0);
                _assetManager.AddAssetRequest(userInfo, id, transfer);
            }
            else if ((pack.Type == PacketType.StartPingCheck))
            {
                //reply to pingcheck
                libsecondlife.Packets.StartPingCheckPacket startping = (libsecondlife.Packets.StartPingCheckPacket)pack;
                libsecondlife.Packets.CompletePingCheckPacket endping = new CompletePingCheckPacket();
                endping.PingID.PingID = startping.PingID.PingID;
                _server.SendPacket(endping, true, userInfo);
            }
            else if (pack.Type == PacketType.CompleteAgentMovement)
            {
                _agentManager.AgentJoin(userInfo);
            }
            else if (pack.Type == PacketType.RequestImage)
            {
                RequestImagePacket imageRequest = (RequestImagePacket)pack;
                for (int i = 0; i < imageRequest.RequestImage.Length; i++)
                {
                    this._assetManager.AddTextureRequest(userInfo, imageRequest.RequestImage[i].Image);
                }
            }
            else if (pack.Type == PacketType.RegionHandshakeReply)
            {
                //recieved regionhandshake so can now start sending info
                _agentManager.SendInitialData(userInfo);
            }
            else if (pack.Type == PacketType.ObjectAdd)
            {
                ObjectAddPacket ad = (ObjectAddPacket)pack;
                _primManager.CreatePrim(userInfo, ad.ObjectData.RayEnd, ad);
            }
            else if (pack.Type == PacketType.ObjectPosition)
            {
                //System.Console.WriteLine(pack.ToString());
            }
            else if (pack.Type == PacketType.MultipleObjectUpdate)
            {
                MultipleObjectUpdatePacket multipleupdate = (MultipleObjectUpdatePacket)pack;

                for (int i = 0; i < multipleupdate.ObjectData.Length; i++)
                {
                    if (multipleupdate.ObjectData[i].Type == 9) //change position
                    {
                        libsecondlife.LLVector3 pos = new LLVector3(multipleupdate.ObjectData[i].Data, 0);
                        _primManager.UpdatePrimPosition(userInfo, pos, multipleupdate.ObjectData[i].ObjectLocalID, false, libsecondlife.LLQuaternion.Identity);
                        //should update stored position of the prim
                    }
                    else if (multipleupdate.ObjectData[i].Type == 10)//rotation
                    {
                        libsecondlife.LLVector3 pos = new LLVector3(100, 100, 22);
                        libsecondlife.LLQuaternion rot = new LLQuaternion(multipleupdate.ObjectData[i].Data, 0, true);
                        _primManager.UpdatePrimPosition(userInfo, pos, multipleupdate.ObjectData[i].ObjectLocalID, true, rot);
                    }
                }
            }
            else if (pack.Type == PacketType.AgentWearablesRequest)
            {
                _agentManager.SendIntialAvatarAppearance(userInfo);
            }
            else if (pack.Type == PacketType.AgentUpdate)
            {
                //	System.Console.WriteLine("agent update");
                AgentUpdatePacket agent = (AgentUpdatePacket)pack;
                uint mask = agent.AgentData.ControlFlags & (1);
                AvatarData avatar = _agentManager.GetAgent(userInfo.AgentID);
                if (avatar != null)
                {
                    if (avatar.Started)
                    {
                        if (mask == (1))
                        {
                            if (!avatar.Walk)
                            {
                                //start walking
                                _agentManager.SendMoveCommand(userInfo, false, avatar.Position.X, avatar.Position.Y, avatar.Position.Z, 0, agent.AgentData.BodyRotation);
                                _agentManager.UpdateAnim(avatar.NetInfo, Globals.Instance.ANIM_AGENT_WALK, 1);
                                Axiom.MathLib.Vector3 v3 = new Axiom.MathLib.Vector3(1, 0, 0);
                                Axiom.MathLib.Quaternion q = new Axiom.MathLib.Quaternion(agent.AgentData.BodyRotation.W, agent.AgentData.BodyRotation.X, agent.AgentData.BodyRotation.Y, agent.AgentData.BodyRotation.Z);
                                Axiom.MathLib.Vector3 direc = q * v3;
                                direc.Normalize();
                                direc = direc * ((0.03f) * 128f);

                                avatar.Velocity.X = direc.x;
                                avatar.Velocity.Y = direc.y;
                                avatar.Velocity.Z = direc.z;
                                avatar.Walk = true;
                            }
                        }
                        else
                        {
                            if (avatar.Walk)
                            {
                                //walking but key not pressed so need to stop
                                _agentManager.SendMoveCommand(userInfo, true, avatar.Position.X, avatar.Position.Y, avatar.Position.Z, 0, agent.AgentData.BodyRotation);
                                _agentManager.UpdateAnim(avatar.NetInfo, Globals.Instance.ANIM_AGENT_STAND, 1);
                                avatar.Walk = false;
                                avatar.Velocity.X = 0;
                                avatar.Velocity.Y = 0;
                                avatar.Velocity.Z = 0;
                            }
                        }
                    }
                }
                else
                {

                }
            }
            else if (pack.Type == PacketType.ChatFromViewer)
            {
                ChatFromViewerPacket chat = (ChatFromViewerPacket)pack;
                System.Text.Encoding enc = System.Text.Encoding.ASCII;

                string myString = enc.GetString(chat.ChatData.Message);
                if (myString != "")
                {
                    string[] comp = new string[10];
                    string delimStr = " ,	";
                    char[] delimiter = delimStr.ToCharArray();
                    string line;

                    line = myString;
                    comp = line.Split(delimiter);
                    if (comp[0] == "pos")
                    {
                    }
                    else if (comp[0] == "veloc")
                    {
                    }
                    else
                    {
                        _agentManager.SendChatMessage(userInfo, line);
                    }
                }
            }
        }
        public void NewUserCallback(UserAgentInfo userInfo ) 
        {
            Console.WriteLine( "new user - {0} - has joined [session {1}]", userInfo.AgentID.ToString(), userInfo.SessionID.ToString() +"curcuit used"+userInfo.circuitCode);
            string first,last;
            LLUUID Base,Inventory;
            lock(_login)
             {
                first=_login.First;
                last=_login.Last;
                Base=_login.BaseFolder;
                Inventory=_login.InventoryFolder; 
                //should get agentid and sessionid so they can be checked.
             }
            _agentManager.NewAgent(userInfo, first, last, Base, Inventory);
            //now because of the lack of Global account management (User server etc)
            //we need to reset the names back to default incase a teleport happens 
            //which will not have a Login name set, so they will use default names
            lock(_login)
            {
                _login.First="Test";
                _login.Last="User";
            }
        }

        public void ErrorCallback( string text ) {
            Console.WriteLine( "error report: {0}", text );
        }

        void Timer1Tick( object sender, System.EventArgs e ) {
            this.time++;
            _agentManager.UpdatePositions();
            this._assetManager.DoWork( time );
        }
    }
    public class Logon
    {
    	public string First = "Test";
    	public string Last = "User";
    	public LLUUID Agent;
    	public LLUUID Session;
    	public LLUUID InventoryFolder;
    	public LLUUID BaseFolder;
    	public Logon()
    	{
    		
    	}
    }
}
