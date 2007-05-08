using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class StandCommand: Command
    {
        public StandCommand(TestClient testClient)
	{
		Name = "stand";
		Description = "Stand";
	}
	
        public override string Execute(string[] args, LLUUID fromAgentID)
	{
		Client.Self.Status.StandUp = true;
		stand(Client);
		return "Standing up.";  
	}

        void stand(SecondLife client)
        {
            SendAgentUpdate(client, (uint)MainAvatar.ControlFlags.AGENT_CONTROL_STAND_UP);
        }

        const float DRAW_DISTANCE = 96.0f;
        void SendAgentUpdate(SecondLife client, uint ControlID)
        {
            AgentUpdatePacket p = new AgentUpdatePacket();
            p.AgentData.Far = DRAW_DISTANCE;
            //LLVector3 myPos = client.Self.Position;
            p.AgentData.CameraCenter = new LLVector3(0, 0, 0);
            p.AgentData.CameraAtAxis = new LLVector3(0, 0, 0);
            p.AgentData.CameraLeftAxis = new LLVector3(0, 0, 0);
            p.AgentData.CameraUpAxis = new LLVector3(0, 0, 0);
            p.AgentData.HeadRotation = new LLQuaternion(0, 0, 0, 1); ;
            p.AgentData.BodyRotation = new LLQuaternion(0, 0, 0, 1); ;
            p.AgentData.AgentID = client.Network.AgentID;
            p.AgentData.SessionID = client.Network.SessionID;
            p.AgentData.ControlFlags = ControlID;
            client.Network.SendPacket(p);
        }
    }
}
