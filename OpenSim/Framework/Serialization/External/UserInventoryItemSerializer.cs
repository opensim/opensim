/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.IO;
using System.Text;
using System.Xml;
using OpenMetaverse;
using OpenSim.Framework;
    
namespace OpenSim.Framework.Serialization.External
{        
    /// <summary>
    /// Serialize and deserialize user inventory items as an external format.
    /// </summary> 
    /// XXX: Please do not use yet.
    public class UserInventoryItemSerializer
    {
        /// <summary>
        /// Deserialize item
        /// </summary>
        /// <param name="serializedSettings"></param>
        /// <returns></returns>
        /// <exception cref="System.Xml.XmlException"></exception>
        public static InventoryItemBase Deserialize(byte[] serialization)
        {
            return Deserialize(Encoding.ASCII.GetString(serialization, 0, serialization.Length));
        }
        
        /// <summary>
        /// Deserialize settings
        /// </summary>
        /// <param name="serializedSettings"></param>
        /// <returns></returns>
        /// <exception cref="System.Xml.XmlException"></exception>
        public static InventoryItemBase Deserialize(string serialization)
        {
            InventoryItemBase item = new InventoryItemBase();
            
            StringReader sr = new StringReader(serialization);
            XmlTextReader xtr = new XmlTextReader(sr);
            
            xtr.ReadStartElement("InventoryItem");
            
            item.Name                   =                   xtr.ReadElementString("Name");
            item.ID                     = UUID.Parse(       xtr.ReadElementString("ID"));
            item.InvType                = Convert.ToInt32(  xtr.ReadElementString("InvType"));            
            item.CreatorId              =                   xtr.ReadElementString("CreatorUUID");
            item.CreationDate           = Convert.ToInt32(  xtr.ReadElementString("CreationDate"));
            item.Owner                  = UUID.Parse(       xtr.ReadElementString("Owner"));
            item.Description            =                   xtr.ReadElementString("Description");
            item.AssetType              = Convert.ToInt32(  xtr.ReadElementString("AssetType"));
            item.AssetID                = UUID.Parse(       xtr.ReadElementString("AssetID"));
            item.SaleType               = Convert.ToByte(   xtr.ReadElementString("SaleType"));
            item.SalePrice              = Convert.ToInt32(  xtr.ReadElementString("SalePrice"));
            item.BasePermissions        = Convert.ToUInt32( xtr.ReadElementString("BasePermissions"));
            item.CurrentPermissions     = Convert.ToUInt32( xtr.ReadElementString("CurrentPermissions"));
            item.EveryOnePermissions    = Convert.ToUInt32( xtr.ReadElementString("EveryOnePermissions"));
            item.NextPermissions        = Convert.ToUInt32( xtr.ReadElementString("NextPermissions"));
            item.Flags                  = Convert.ToUInt32( xtr.ReadElementString("Flags"));
            item.GroupID                = UUID.Parse(       xtr.ReadElementString("GroupID"));
            item.GroupOwned             = Convert.ToBoolean(xtr.ReadElementString("GroupOwned"));
            
            xtr.ReadEndElement();
            
            xtr.Close();
            sr.Close();
            
            return item;
        }      
        
        public static string Serialize(InventoryItemBase inventoryItem)
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter writer = new XmlTextWriter(sw);
            writer.Formatting = Formatting.Indented;
            writer.WriteStartDocument();

            writer.WriteStartElement("InventoryItem");

            writer.WriteStartElement("Name");
            writer.WriteString(inventoryItem.Name);
            writer.WriteEndElement();
            writer.WriteStartElement("ID");
            writer.WriteString(inventoryItem.ID.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("InvType");
            writer.WriteString(inventoryItem.InvType.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("CreatorUUID");
            writer.WriteString(inventoryItem.CreatorId);
            writer.WriteEndElement();
            writer.WriteStartElement("CreationDate");
            writer.WriteString(inventoryItem.CreationDate.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("Owner");
            writer.WriteString(inventoryItem.Owner.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("Description");
            writer.WriteString(inventoryItem.Description);
            writer.WriteEndElement();
            writer.WriteStartElement("AssetType");
            writer.WriteString(inventoryItem.AssetType.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("AssetID");
            writer.WriteString(inventoryItem.AssetID.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("SaleType");
            writer.WriteString(inventoryItem.SaleType.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("SalePrice");
            writer.WriteString(inventoryItem.SalePrice.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("BasePermissions");
            writer.WriteString(inventoryItem.BasePermissions.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("CurrentPermissions");
            writer.WriteString(inventoryItem.CurrentPermissions.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("EveryOnePermissions");
            writer.WriteString(inventoryItem.EveryOnePermissions.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("NextPermissions");
            writer.WriteString(inventoryItem.NextPermissions.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("Flags");
            writer.WriteString(inventoryItem.Flags.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("GroupID");
            writer.WriteString(inventoryItem.GroupID.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("GroupOwned");
            writer.WriteString(inventoryItem.GroupOwned.ToString());
            writer.WriteEndElement();

            writer.WriteEndElement();
            
            writer.Close();
            sw.Close();
            
            return sw.ToString();
        }        
    }
}
