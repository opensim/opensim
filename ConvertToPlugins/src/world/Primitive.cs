using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.types;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.world
{
    public class Primitive : Entity
    {
        protected float mesh_cutbegin;
        protected float mesh_cutend;
        protected PrimData primData;
        protected bool newPrimFlag;
        protected ObjectUpdatePacket OurPacket;

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
        }
        
        public void CreateFromPacket( ObjectAddPacket addPacket, LLUUID agentID, uint localID)
        {
        	ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
        	objupdate.RegionData.RegionHandle = OpenSim_Main.cfg.RegionHandle;
        	objupdate.RegionData.TimeDilation = 64096;
        	
        	objupdate.ObjectData = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
        	PrimData PData = new PrimData();
        	this.primData = PData;
        	objupdate.ObjectData[0] = new ObjectUpdatePacket.ObjectDataBlock();//OpenSim_Main.local_world.PrimTemplate;
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
        	objupdate.ObjectData[0].FullID = new LLUUID("edba7151-5857-acc5-b30b-f01efefda" + (localID- 702000).ToString("000"));
        	
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
    }
    
    public class PrimData
    {
    	public LLUUID OwnerID;
    	public byte PCode;
    	public byte PathBegin;
    	public byte PathEnd;
    	public byte PathScaleX;
    	public byte PathScaleY;
    	public byte PathShearX;
    	public byte PathShearY;
    	public sbyte PathSkew;
    	public byte ProfileBegin;
    	public byte ProfileEnd;
    	public LLVector3 Scale;
    	public byte PathCurve;
    	public byte ProfileCurve;
    	public uint ParentID=0;
    	public byte ProfileHollow;
    	
    	public PrimData()
    	{
    		
    	}
    }
}
