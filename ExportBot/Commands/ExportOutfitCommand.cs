using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class ExportOutfitCommand : Command
    {
        public ExportOutfitCommand(TestClient testClient)
        {
            Name = "exportoutfit";
            Description = "Exports an avatars outfit to an xml file. Usage: exportoutfit avataruuid outputfile.xml";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            if (args.Length != 2)
                return "Usage: exportoutfit avataruuid outputfile.xml";

            LLUUID id;

            try
            {
                id = new LLUUID(args[0]);
            }
            catch (Exception)
            {
                return "Usage: exportoutfit avataruuid outputfile.xml";
            }

            lock (Client.Appearances)
            {
                if (Client.Appearances.ContainsKey(id))
                {
                    try
                    {
						StreamWriter Outfile = File.AppendText(args[1]);

					
						try
						{
							Outfile.Write(Client.Appearances[id].ToString());
							Console.WriteLine(Client.Appearances[id].ToString());
							//Client.Appearances[id].ToXml(writer);
						}
						finally
						{
							Outfile.Close();
						}
                    }
                    catch (Exception e)
                    {
                        return e.ToString();
                    }

                    return "Exported appearance for avatar " + id.ToString() + " to " + args[1];
                }
                else
                {
                    return "Couldn't find an appearance for avatar " + id.ToString();
                }
            }
        }
    }
}
