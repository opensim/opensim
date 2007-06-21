using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class StatsCommand : Command
    {
        public StatsCommand(TestClient testClient)
        {
            Name = "stats";
            Description = "Provide connection figures and statistics";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            StringBuilder output = new StringBuilder();

            lock (Client.Network.Simulators)
            {
                for (int i = 0; i < Client.Network.Simulators.Count; i++)
                {
                    Simulator sim = Client.Network.Simulators[i];

                    output.AppendLine(String.Format(
                        "[{0}] Dilation: {1} InBPS: {2} OutBPS: {3} ResentOut: {4}  ResentIn: {5}",
                        sim.ToString(), sim.Dilation, sim.IncomingBPS, sim.OutgoingBPS, sim.ResentPackets, 
                        sim.ReceivedResends));
                }
            }
            output.Append("Packets in the queue: " + Client.Network.InboxCount);
			output.AppendLine(String.Format("FPS : {0} PhysicsFPS : {1} AgentUpdates : {2} Objects : {3} Scripted Objects : {4}",
				Client.Network.CurrentSim.FPS, Client.Network.CurrentSim.PhysicsFPS, Client.Network.CurrentSim.AgentUpdates, Client.Network.CurrentSim.Objects, Client.Network.CurrentSim.ScriptedObjects));
			output.AppendLine(String.Format("Frame Time : {0} Net Time : {1} Image Time : {2} Physics Time : {3} Script Time : {4} Other Time : {5}",
				Client.Network.CurrentSim.FrameTime, Client.Network.CurrentSim.NetTime, Client.Network.CurrentSim.ImageTime, Client.Network.CurrentSim.PhysicsTime, Client.Network.CurrentSim.ScriptTime, Client.Network.CurrentSim.OtherTime));
			output.AppendLine(String.Format("Agents : {0} Child Agents : {1} Active Scripts : {2}",
				Client.Network.CurrentSim.Agents, Client.Network.CurrentSim.ChildAgents, Client.Network.CurrentSim.ActiveScripts));

            return output.ToString();
        }
    }
}
