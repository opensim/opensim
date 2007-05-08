using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

using libsecondlife;
using libsecondlife.Packets;
using libsecondlife.InventorySystem;

namespace libsecondlife.TestClient
{
    public class InventoryCommand : Command
    {
		public InventoryCommand(TestClient testClient)
        {
            Name = "i";
            Description = "Prints out inventory.";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            return "Broken until someone fixes me";

            //Client.Inventory.DownloadInventory();
            //StringBuilder result = new StringBuilder();
            //PrintFolder(Client.Inventory.GetRootFolder(), result, 0);
            //return result.ToString();
        }

        //void PrintFolder(InventoryFolder folder, StringBuilder output, int indenting)
        //{
        //    Indent(output, indenting);
        //    output.Append(folder.Name);
        //    output.Append("\n");
        //    foreach (InventoryBase b in folder.GetContents())
        //    {
        //        InventoryItem item = b as InventoryItem;
        //        if (item != null)
        //        {
        //            Indent(output, indenting + 1);
        //            output.Append(item.Name);
        //            output.Append("\n");
        //            continue;
        //        }
        //        InventoryFolder subFolder = b as InventoryFolder;
        //        if (subFolder != null)
        //            PrintFolder(subFolder, output, indenting + 1);
        //    }
        //}

        //void Indent(StringBuilder output, int indenting)
        //{
        //    for (int count = 0; count < indenting; count++)
        //    {
        //        output.Append("  ");
        //    }
        //}
	}
}