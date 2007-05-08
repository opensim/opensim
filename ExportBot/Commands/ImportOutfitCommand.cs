using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class ImportOutfitCommand : Command
    {
        private uint SerialNum = 1;

        public ImportOutfitCommand(TestClient testClient)
        {
            Name = "importoutfit";
            Description = "Imports an appearance from an xml file. Usage: importoutfit inputfile.xml";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            if (args.Length != 1)
                return "Usage: importoutfit inputfile.xml";

            try
            {
                XmlReader reader = XmlReader.Create(args[0]);
                XmlSerializer serializer = new XmlSerializer(typeof(Packet));
                AvatarAppearancePacket appearance = (AvatarAppearancePacket)serializer.Deserialize(reader);
                reader.Close();

                AgentSetAppearancePacket set = new AgentSetAppearancePacket();

                set.AgentData.AgentID = Client.Network.AgentID;
                set.AgentData.SessionID = Client.Network.SessionID;
                set.AgentData.SerialNum = SerialNum++;

                float AV_Height_Range = 2.025506f - 1.50856f;
                float AV_Height = 1.50856f + (((float)appearance.VisualParam[25].ParamValue / 255.0f) * AV_Height_Range);
                set.AgentData.Size = new LLVector3(0.45f, 0.6f, AV_Height);

                set.ObjectData.TextureEntry = appearance.ObjectData.TextureEntry;
                set.VisualParam = new AgentSetAppearancePacket.VisualParamBlock[appearance.VisualParam.Length];

                int i = 0;
                foreach (AvatarAppearancePacket.VisualParamBlock block in appearance.VisualParam)
                {
                    set.VisualParam[i] = new AgentSetAppearancePacket.VisualParamBlock();
                    set.VisualParam[i].ParamValue = block.ParamValue;
                    i++;
                }

                set.WearableData = new AgentSetAppearancePacket.WearableDataBlock[0];

                Client.Network.SendPacket(set);
            }
            catch (Exception)
            {
                return "Failed to import the appearance XML file, maybe it doesn't exist or is in the wrong format?";
            }

            return "Imported " + args[0] + " and sent an AgentSetAppearance packet";
        }
    }
}
