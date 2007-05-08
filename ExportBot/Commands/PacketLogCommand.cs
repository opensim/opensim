using System;
using System.Collections.Generic;
using System.Xml;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class PacketLogCommand : Command
    {
        List<Packet> Packets = new List<Packet>();
        bool Done = false;
        int Count = 0;
        int Total = 0;

        public PacketLogCommand(TestClient testClient)
        {
            Name = "packetlog";
            Description = "Logs a given number of packets to an xml file. Usage: packetlog 10 tenpackets.xml";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            if (args.Length != 2)
                return "Usage: packetlog 10 tenpackets.xml";

            XmlWriter writer;
            NetworkManager.PacketCallback callback = new NetworkManager.PacketCallback(OnPacket);

            Packets.Clear();
            Done = false;
            Count = 0;

            try
            {
                Total = Int32.Parse(args[0]);
                writer = XmlWriter.Create(args[1]);

                Client.Network.RegisterCallback(PacketType.Default, callback);
            }
            catch (Exception e)
            {
                return "Usage: packetlog 10 tenpackets.xml (" + e + ")";
            }

            while (!Done)
            {
                System.Threading.Thread.Sleep(100);
            }

            Client.Network.UnregisterCallback(PacketType.Default, callback);

            try
            {
                Helpers.PacketListToXml(Packets, writer);
            }
            catch (Exception e)
            {
                return "Serialization failed: " + e.ToString();
            }

            writer.Close();
            Packets.Clear();

            return "Exported " + Count + " packets to " + args[1];
        }

        private void OnPacket(Packet packet, Simulator simulator)
        {
            lock (Packets)
            {
                if (Count >= Total)
                {
                    Done = true;
                }
                else
                {
                    Packets.Add(packet);
                    Count++;
                }
            }
        }
    }
}
