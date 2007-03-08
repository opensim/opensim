using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.types;
using libsecondlife;
using libsecondlife.Packets;
using GridInterfaces;

namespace OpenSim.world
{
    public class Primitive : Entity
    {
        protected float mesh_cutbegin;
        protected float mesh_cutend;
        protected PrimData primData;
        protected bool newPrimFlag;
        protected bool updateFlag;
        protected ObjectUpdatePacket OurPacket;
       
        public bool UpdateFlag
        {
        	get
        	{
        		return updateFlag;
        	}
        	set
        	{
        		updateFlag = value;
        	}
        }
        
        public Primitive()
        {
            mesh_cutbegin = 0.0f;
            mesh_cutend = 1.0f;
        }

        public override Mesh getMesh()
        {
            Mesh mesh = new Mesh();
            Triangle tri = new Triangle(
                new Axiom.MathLib.Vector3(0.0f, 1.0f, 1.0f), 
                new Axiom.MathLib.Vector3(1.0f, 0.0f, 1.0f), 
                new Axiom.MathLib.Vector3(1.0f, 1.0f, 0.0f));

            mesh.AddTri(tri);
            mesh += base.getMesh();

            return mesh;
        }
        
        public override void update()
        {
        	if(this.newPrimFlag)
        	{
        		foreach(OpenSimClient client in OpenSim_Main.sim.ClientThreads.Values) {
        			client.OutPacket(OurPacket);
        		}
        		this.newPrimFlag = false;
        	}
        	else if(this.updateFlag)
        	{
        		ImprovedTerseObjectUpdatePacket terse = new ImprovedTerseObjectUpdatePacket();
    			terse.RegionData.RegionHandle = OpenSim_Main.cfg.RegionHandle; // FIXME
    			terse.RegionData.TimeDilation = 64096;
    			terse.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
    			terse.ObjectData[0] = this.CreateImprovedBlock();
    			foreach(OpenSimClient client in OpenSim_Main.sim.ClientThreads.Values) {
        			client.OutPacket(terse);
        		}
        		this.updateFlag = false;
        	}
        	
        }
        
        public void UpdateClient(OpenSimClient RemoteClient)
        {
        	byte[] pb = this.position.GetBytes();
        	Array.Copy(pb, 0, OurPacket.ObjectData[0].ObjectData, 0, pb.Length);
        	RemoteClient.OutPacket(OurPacket);
        }
        
        public void CreateFromPacket( ObjectAddPacket addPacket, LLUUID agentID, uint localID)
        {
        	ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
        	objupdate.RegionData.RegionHandle = OpenSim_Main.cfg.RegionHandle;
        	objupdate.RegionData.TimeDilation = 64096;
        	
        	objupdate.ObjectData = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
        	PrimData PData = new PrimData();
        	this.primData = PData;
        	objupdate.ObjectData[0] = new ObjectUpdatePacket.ObjectDataBlock();
        	objupdate.ObjectData[0].PSBlock = new byte[0];
        	objupdate.ObjectData[0].ExtraParams = new byte[1];
        	objupdate.ObjectData[0].MediaURL = new byte[0];
        	objupdate.ObjectData[0].NameValue = new byte[0];
        	objupdate.ObjectData[0].PSBlock = new byte[0];
        	objupdate.ObjectData[0].Text = new byte[0];
        	objupdate.ObjectData[0].TextColor = new byte[4];
        	objupdate.ObjectData[0].JointAxisOrAnchor = new LLVector3(0,0,0);
        	objupdate.ObjectData[0].JointPivot = new LLVector3(0,0,0);
        	objupdate.ObjectData[0].Material = 3;
        	objupdate.ObjectData[0].UpdateFlags=32+65536+131072+256+4+8+2048+524288+268435456;
        	objupdate.ObjectData[0].TextureAnim = new byte[0];
        	objupdate.ObjectData[0].Sound = LLUUID.Zero;
        	LLObject.TextureEntry ntex = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-5005-000000000005"));
        	objupdate.ObjectData[0].TextureEntry = ntex.ToBytes();
        	objupdate.ObjectData[0].State = 0;
        	objupdate.ObjectData[0].Data = new byte[0];
        	PData.OwnerID = objupdate.ObjectData[0].OwnerID = agentID;
        	PData.PCode = objupdate.ObjectData[0].PCode = addPacket.ObjectData.PCode;
        	PData.PathBegin = objupdate.ObjectData[0].PathBegin = addPacket.ObjectData.PathBegin;
        	PData.PathEnd = objupdate.ObjectData[0].PathEnd = addPacket.ObjectData.PathEnd;
        	PData.PathScaleX = objupdate.ObjectData[0].PathScaleX = addPacket.ObjectData.PathScaleX;
        	PData.PathScaleY = objupdate.ObjectData[0].PathScaleY = addPacket.ObjectData.PathScaleY;
        	PData.PathShearX = objupdate.ObjectData[0].PathShearX = addPacket.ObjectData.PathShearX;
        	PData.PathShearY = objupdate.ObjectData[0].PathShearY = addPacket.ObjectData.PathShearY;
        	PData.PathSkew = objupdate.ObjectData[0].PathSkew = addPacket.ObjectData.PathSkew;
        	PData.ProfileBegin = objupdate.ObjectData[0].ProfileBegin = addPacket.ObjectData.ProfileBegin;
        	PData.ProfileEnd = objupdate.ObjectData[0].ProfileEnd = addPacket.ObjectData.ProfileEnd;
        	PData.Scale = objupdate.ObjectData[0].Scale = addPacket.ObjectData.Scale;
        	PData.PathCurve = objupdate.ObjectData[0].PathCurve = addPacket.ObjectData.PathCurve;
        	PData.ProfileCurve = objupdate.ObjectData[0].ProfileCurve = addPacket.ObjectData.ProfileCurve;
        	PData.ParentID = objupdate.ObjectData[0].ParentID = 0;
        	PData.ProfileHollow = objupdate.ObjectData[0].ProfileHollow = addPacket.ObjectData.ProfileHollow;
        	
        	//finish off copying rest of shape data
        	
        	objupdate.ObjectData[0].ID = (uint)(localID);
        	objupdate.ObjectData[0].FullID = new LLUUID("edba7151-5857-acc5-b30b-f01efef" + (localID- 702000).ToString("00000"));
        	objupdate.ObjectData[0].ObjectData = new byte[60];
        	objupdate.ObjectData[0].ObjectData[46] = 128;
        	objupdate.ObjectData[0].ObjectData[47] = 63;
        	LLVector3 pos1= addPacket.ObjectData.RayEnd;
        	//update position
        	byte[] pb = pos1.GetBytes();
        	Array.Copy(pb, 0, objupdate.ObjectData[0].ObjectData, 0, pb.Length);

        	this.newPrimFlag = true;
        	this.uuid = objupdate.ObjectData[0].FullID;
        	this.localid = objupdate.ObjectData[0].ID;
        	this.position = pos1;
        	this.OurPacket = objupdate;
        }
        
        public void CreateFromStorage(PrimStorage store)
        {
        	//need to clean this up as it shares a lot of code with CreateFromPacket()
        	ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
        	objupdate.RegionData.RegionHandle = OpenSim_Main.cfg.RegionHandle;
        	objupdate.RegionData.TimeDilation = 64096;	
        	objupdate.ObjectData = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
        	
        	this.primData = store.Data;
        	objupdate.ObjectData[0] = new ObjectUpdatePacket.ObjectDataBlock();
        	objupdate.ObjectData[0].PSBlock = new byte[0];
        	objupdate.ObjectData[0].ExtraParams = new byte[1];
        	objupdate.ObjectData[0].MediaURL = new byte[0];
        	objupdate.ObjectData[0].NameValue = new byte[0];
        	objupdate.ObjectData[0].PSBlock = new byte[0];
        	objupdate.ObjectData[0].Text = new byte[0];
        	objupdate.ObjectData[0].TextColor = new byte[4];
        	objupdate.ObjectData[0].JointAxisOrAnchor = new LLVector3(0,0,0);
        	objupdate.ObjectData[0].JointPivot = new LLVector3(0,0,0);
        	objupdate.ObjectData[0].Material = 3;
        	objupdate.ObjectData[0].UpdateFlags=32+65536+131072+256+4+8+2048+524288+268435456;
        	objupdate.ObjectData[0].TextureAnim = new byte[0];
        	objupdate.ObjectData[0].Sound = LLUUID.Zero;
        	LLObject.TextureEntry ntex = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-5005-000000000005"));
        	objupdate.ObjectData[0].TextureEntry = ntex.ToBytes();
        	objupdate.ObjectData[0].State = 0;
        	objupdate.ObjectData[0].Data = new byte[0];
        	objupdate.ObjectData[0].OwnerID = this.primData.OwnerID;
        	objupdate.ObjectData[0].PCode = this.primData.PCode;
        	objupdate.ObjectData[0].PathBegin = this.primData.PathBegin;
        	objupdate.ObjectData[0].PathEnd = this.primData.PathEnd;
        	objupdate.ObjectData[0].PathScaleX = this.primData.PathScaleX;
        	objupdate.ObjectData[0].PathScaleY = this.primData.PathScaleY;
        	objupdate.ObjectData[0].PathShearX = this.primData.PathShearX;
        	objupdate.ObjectData[0].PathShearY = this.primData.PathShearY;
        	objupdate.ObjectData[0].PathSkew = this.primData.PathSkew;
        	objupdate.ObjectData[0].ProfileBegin = this.primData.ProfileBegin;
        	objupdate.ObjectData[0].ProfileEnd = this.primData.ProfileEnd;
        	objupdate.ObjectData[0].Scale = this.primData.Scale;
        	objupdate.ObjectData[0].PathCurve = this.primData.PathCurve;
        	objupdate.ObjectData[0].ProfileCurve = this.primData.ProfileCurve;
        	objupdate.ObjectData[0].ParentID = 0;
        	objupdate.ObjectData[0].ProfileHollow = this.primData.ProfileHollow;
        	//finish off copying rest of shape data
        	
        	objupdate.ObjectData[0].ID = (uint)store.LocalID;
        	objupdate.ObjectData[0].FullID = store.FullID;
        	
        	objupdate.ObjectData[0].ObjectData = new byte[60];
        	objupdate.ObjectData[0].ObjectData[46] = 128;
        	objupdate.ObjectData[0].ObjectData[47] = 63;
        	LLVector3 pos1= store.Position;
        	//update position
        	byte[] pb = pos1.GetBytes();
        	Array.Copy(pb, 0, objupdate.ObjectData[0].ObjectData, 0, pb.Length);

        	this.uuid = objupdate.ObjectData[0].FullID;
        	this.localid = objupdate.ObjectData[0].ID;
        	this.position = pos1;
        	this.OurPacket = objupdate;
        }
        public ImprovedTerseObjectUpdatePacket.ObjectDataBlock CreateImprovedBlock()
        {
        	uint ID = this.localid;
        	byte[] bytes = new byte[60];
			
			
			int i = 0;
			ImprovedTerseObjectUpdatePacket.ObjectDataBlock dat = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock();
			dat.TextureEntry = this.OurPacket.ObjectData[0].TextureEntry;
			
			bytes[i++] = (byte)(ID % 256);
			bytes[i++] = (byte)((ID >> 8) % 256);
			bytes[i++] = (byte)((ID >> 16) % 256);
			bytes[i++] = (byte)((ID >> 24) % 256);
			bytes[i++]= 0;
			bytes[i++]= 0;

			byte[] pb = this.position.GetBytes();
			Array.Copy(pb, 0, bytes, i, pb.Length);
			i += 12;
			ushort ac = 32767;

			//vel
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			
			//accel
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			
			ushort rw, rx,ry,rz;
			rw = (ushort)(32768 * (this.rotation.w+1));
			rx = (ushort)(32768 * (this.rotation.x+1));
			ry = (ushort)(32768 * (this.rotation.y+1));
			rz = (ushort)(32768 * (this.rotation.z+1));
			
			//rot
			bytes[i++] = (byte)(rx % 256);
			bytes[i++] = (byte)((rx >> 8) % 256);
			bytes[i++] = (byte)(ry % 256);
			bytes[i++] = (byte)((ry >> 8) % 256);
			bytes[i++] = (byte)(rz % 256);
			bytes[i++] = (byte)((rz >> 8) % 256);
			bytes[i++] = (byte)(rw % 256);
			bytes[i++] = (byte)((rw >> 8) % 256);
			
			//rotation vel
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			bytes[i++] = (byte)(ac % 256);
			bytes[i++] = (byte)((ac >> 8) % 256);
			
			dat.Data=bytes;
			return dat;
        }
        
        public override void BackUp()
        {
        	PrimStorage pStore = new PrimStorage();
        	pStore.Data = this.primData;
        	pStore.FullID = this.uuid;
        	pStore.LocalID = this.localid;
        	pStore.Position = this.position;
        	pStore.Rotation = new LLQuaternion(this.rotation.x, this.rotation.y, this.rotation.z , this.rotation.w);
        	OpenSim_Main.local_world.localStorage.StorePrim(pStore);
        }
    }
    
}
