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

            return output.ToString();
        }
    }
}
