using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.world
{
    public class Avatar : Entity
    {
	public string firstname;
        public string lastname;
	public OpenSimClient ControllingClient;
	private libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock AvatarTemplate;

        public Avatar(OpenSimClient TheClient) {
        	Console.WriteLine("Avatar.cs - Loading details from grid (DUMMY)");
		ControllingClient=TheClient;
		SetupTemplate("avatar-template.dat");
	}

	private void SetupTemplate(string name)
		{
			
			int i = 0;
			FileInfo fInfo = new FileInfo(name);
			long numBytes = fInfo.Length;
			FileStream fStream = new FileStream(name, FileMode.Open, FileAccess.Read);
			BinaryReader br = new BinaryReader(fStream);
			byte [] data1 = br.ReadBytes((int)numBytes);
			br.Close();
			fStream.Close();
			
			libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock objdata = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock(data1, ref i);
			
			System.Text.Encoding enc = System.Text.Encoding.ASCII;
			libsecondlife.LLVector3 pos = new LLVector3(objdata.ObjectData, 16);
			pos.X = 100f;
			objdata.ID = 8880000;
			objdata.NameValue = enc.GetBytes("FirstName STRING RW SV Test \nLastName STRING RW SV User \0");
			libsecondlife.LLVector3 pos2 = new LLVector3(13.981f,100.0f,20.0f);
			//objdata.FullID=user.AgentID;
			byte[] pb = pos.GetBytes();			
			Array.Copy(pb, 0, objdata.ObjectData, 16, pb.Length);
			
			AvatarTemplate = objdata;
				
		}

	public void CompleteMovement(World RegionInfo) {
		Console.WriteLine("Avatar.cs:CompleteMovement() - Constructing AgentMovementComplete packet");
		AgentMovementCompletePacket mov = new AgentMovementCompletePacket();
		mov.AgentData.SessionID = this.ControllingClient.SessionID;
		mov.AgentData.AgentID = this.ControllingClient.AgentID;
		mov.Data.RegionHandle = OpenSim_Main.cfg.RegionHandle;
		// TODO - dynamicalise this stuff
		mov.Data.Timestamp = 1169838966;
		mov.Data.Position = new LLVector3(100f, 100f, 35f);
		mov.Data.LookAt = new LLVector3(0.99f, 0.042f, 0);
		
		Console.WriteLine("Sending AgentMovementComplete packet");
		ControllingClient.OutPacket(mov);
	}

	public void SendInitialPosition() {
		System.Text.Encoding _enc = System.Text.Encoding.ASCII;
		

		//send a objectupdate packet with information about the clients avatar
		ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
		objupdate.RegionData.RegionHandle = OpenSim_Main.cfg.RegionHandle;
		objupdate.RegionData.TimeDilation = 64096;
		objupdate.ObjectData = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
			
		objupdate.ObjectData[0] = AvatarTemplate;
		//give this avatar object a local id and assign the user a name
		objupdate.ObjectData[0].ID = 8880000 + OpenSim_Main.local_world._localNumber;
		//User_info.name="Test"+this.local_numer+" User";
		objupdate.ObjectData[0].FullID = ControllingClient.AgentID;
		objupdate.ObjectData[0].NameValue = _enc.GetBytes("FirstName STRING RW SV " + firstname + "\nLastName STRING RW SV " + lastname + " \0");
			
		libsecondlife.LLVector3 pos2 = new LLVector3(100f, 100.0f, 22.0f);
		
		byte[] pb = pos2.GetBytes();
						
		Array.Copy(pb, 0, objupdate.ObjectData[0].ObjectData, 16, pb.Length);
		OpenSim_Main.local_world._localNumber++;
		this.ControllingClient.OutPacket(objupdate);
	}
	
	public void SendInitialAppearance() {
		AgentWearablesUpdatePacket aw = new AgentWearablesUpdatePacket();
		aw.AgentData.AgentID = this.ControllingClient.AgentID;
		aw.AgentData.SerialNum = 0;
		aw.AgentData.SessionID = ControllingClient.SessionID;
 
		aw.WearableData = new AgentWearablesUpdatePacket.WearableDataBlock[13];
		AgentWearablesUpdatePacket.WearableDataBlock awb = new AgentWearablesUpdatePacket.WearableDataBlock();
		awb.WearableType = (byte)0;
		awb.AssetID = new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");
		awb.ItemID = LLUUID.Random();
		aw.WearableData[0] = awb;
 
		for(int i=1; i<13; i++) {
  			awb = new AgentWearablesUpdatePacket.WearableDataBlock();
  			awb.WearableType = (byte)i;
  			awb.AssetID = new LLUUID("00000000-0000-0000-0000-000000000000");
  			awb.ItemID = new LLUUID("00000000-0000-0000-0000-000000000000");
  			aw.WearableData[i] = awb;
		}
	
		ControllingClient.OutPacket(aw);
	}

	public void SendRegionHandshake(World RegionInfo) {
		Console.WriteLine("Avatar.cs:SendRegionHandshake() - Creating empty RegionHandshake packet");
		System.Text.Encoding _enc = System.Text.Encoding.ASCII;
		RegionHandshakePacket handshake = new RegionHandshakePacket();
		
		Console.WriteLine("Avatar.cs:SendRegionhandshake() - Filling in RegionHandshake details");
		handshake.RegionInfo.BillableFactor = 0;
                handshake.RegionInfo.IsEstateManager = false;
                handshake.RegionInfo.TerrainHeightRange00 = 60;
                handshake.RegionInfo.TerrainHeightRange01 = 60;
                handshake.RegionInfo.TerrainHeightRange10 = 60;
                handshake.RegionInfo.TerrainHeightRange11 = 60;
                handshake.RegionInfo.TerrainStartHeight00 = 10;
                handshake.RegionInfo.TerrainStartHeight01 = 10;
                handshake.RegionInfo.TerrainStartHeight10 = 10;
                handshake.RegionInfo.TerrainStartHeight11 = 10;
                handshake.RegionInfo.SimAccess = 13;
                handshake.RegionInfo.WaterHeight = 5;
                handshake.RegionInfo.RegionFlags = 72458694;
                handshake.RegionInfo.SimName = _enc.GetBytes(OpenSim_Main.cfg.RegionName + "\0");
                handshake.RegionInfo.SimOwner = new LLUUID("00000000-0000-0000-0000-000000000000");
                handshake.RegionInfo.TerrainBase0 = new LLUUID("b8d3965a-ad78-bf43-699b-bff8eca6c975");
                handshake.RegionInfo.TerrainBase1 = new LLUUID("abb783e6-3e93-26c0-248a-247666855da3");
                handshake.RegionInfo.TerrainBase2 = new LLUUID("179cdabd-398a-9b6b-1391-4dc333ba321f");
                handshake.RegionInfo.TerrainBase3 = new LLUUID("beb169c7-11ea-fff2-efe5-0f24dc881df2");
                handshake.RegionInfo.TerrainDetail0 = new LLUUID("00000000-0000-0000-0000-000000000000");
                handshake.RegionInfo.TerrainDetail1 = new LLUUID("00000000-0000-0000-0000-000000000000");
                handshake.RegionInfo.TerrainDetail2 = new LLUUID("00000000-0000-0000-0000-000000000000");
                handshake.RegionInfo.TerrainDetail3 = new LLUUID("00000000-0000-0000-0000-000000000000");
                handshake.RegionInfo.CacheID = new LLUUID("545ec0a5-5751-1026-8a0b-216e38a7ab37");
		
		Console.WriteLine("Avatar.cs:SendRegionHandshake() - Sending RegionHandshake packet");
		this.ControllingClient.OutPacket(handshake);
	}
    }
}
