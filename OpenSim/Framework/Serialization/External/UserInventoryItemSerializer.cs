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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
    
namespace OpenSim.Framework.Serialization.External
{        
    /// <summary>
    /// Serialize and deserialize user inventory items as an external format.
    /// </summary>
    public class UserInventoryItemSerializer
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static Dictionary<string, Action<InventoryItemBase, XmlReader>> m_InventoryItemXmlProcessors
            = new Dictionary<string, Action<InventoryItemBase, XmlReader>>();

        #region InventoryItemBase Processor initialization 
        static UserInventoryItemSerializer()
        {
            m_InventoryItemXmlProcessors.Add("Name", ProcessName);
            m_InventoryItemXmlProcessors.Add("ID", ProcessID);
            m_InventoryItemXmlProcessors.Add("InvType", ProcessInvType);
            m_InventoryItemXmlProcessors.Add("CreatorUUID", ProcessCreatorUUID);
            m_InventoryItemXmlProcessors.Add("CreatorID", ProcessCreatorID); 
            m_InventoryItemXmlProcessors.Add("CreatorData", ProcessCreatorData);
            m_InventoryItemXmlProcessors.Add("CreationDate", ProcessCreationDate);
            m_InventoryItemXmlProcessors.Add("Owner", ProcessOwner);
            m_InventoryItemXmlProcessors.Add("Description", ProcessDescription);
            m_InventoryItemXmlProcessors.Add("AssetType", ProcessAssetType);
            m_InventoryItemXmlProcessors.Add("AssetID", ProcessAssetID);
            m_InventoryItemXmlProcessors.Add("SaleType", ProcessSaleType);
            m_InventoryItemXmlProcessors.Add("SalePrice", ProcessSalePrice);
            m_InventoryItemXmlProcessors.Add("BasePermissions", ProcessBasePermissions);
            m_InventoryItemXmlProcessors.Add("CurrentPermissions", ProcessCurrentPermissions);
            m_InventoryItemXmlProcessors.Add("EveryOnePermissions", ProcessEveryOnePermissions);
            m_InventoryItemXmlProcessors.Add("NextPermissions", ProcessNextPermissions);
            m_InventoryItemXmlProcessors.Add("Flags", ProcessFlags);
            m_InventoryItemXmlProcessors.Add("GroupID", ProcessGroupID);
            m_InventoryItemXmlProcessors.Add("GroupOwned", ProcessGroupOwned);
        }
        #endregion 

        #region InventoryItemBase Processors
        private static void ProcessName(InventoryItemBase item, XmlReader reader)
        {
            item.Name = reader.ReadElementContentAsString("Name", String.Empty);
        }

        private static void ProcessID(InventoryItemBase item, XmlReader reader)
        {
            item.ID = Util.ReadUUID(reader, "ID");
        }

        private static void ProcessInvType(InventoryItemBase item, XmlReader reader)
        {
            item.InvType = reader.ReadElementContentAsInt("InvType", String.Empty);
        }

        private static void ProcessCreatorUUID(InventoryItemBase item, XmlReader reader)
        {
            item.CreatorId = reader.ReadElementContentAsString("CreatorUUID", String.Empty);
        }

        private static void ProcessCreatorID(InventoryItemBase item, XmlReader reader)
        {
            // when it exists, this overrides the previous
            item.CreatorId = reader.ReadElementContentAsString("CreatorID", String.Empty);
        }

        private static void ProcessCreationDate(InventoryItemBase item, XmlReader reader)
        {
            item.CreationDate = reader.ReadElementContentAsInt("CreationDate", String.Empty);
        }

        private static void ProcessOwner(InventoryItemBase item, XmlReader reader)
        {
            item.Owner = Util.ReadUUID(reader, "Owner");
        }

        private static void ProcessDescription(InventoryItemBase item, XmlReader reader)
        {
            item.Description = reader.ReadElementContentAsString("Description", String.Empty);
        }

        private static void ProcessAssetType(InventoryItemBase item, XmlReader reader)
        {
            item.AssetType = reader.ReadElementContentAsInt("AssetType", String.Empty);
        }

        private static void ProcessAssetID(InventoryItemBase item, XmlReader reader)
        {
            item.AssetID = Util.ReadUUID(reader, "AssetID");
        }

        private static void ProcessSaleType(InventoryItemBase item, XmlReader reader)
        {
            item.SaleType = (byte)reader.ReadElementContentAsInt("SaleType", String.Empty);
        }

        private static void ProcessSalePrice(InventoryItemBase item, XmlReader reader)
        {
            item.SalePrice = reader.ReadElementContentAsInt("SalePrice", String.Empty);
        }

        private static void ProcessBasePermissions(InventoryItemBase item, XmlReader reader)
        {
            item.BasePermissions = (uint)reader.ReadElementContentAsInt("BasePermissions", String.Empty);
        }

        private static void ProcessCurrentPermissions(InventoryItemBase item, XmlReader reader)
        {
            item.CurrentPermissions = (uint)reader.ReadElementContentAsInt("CurrentPermissions", String.Empty);
        }

        private static void ProcessEveryOnePermissions(InventoryItemBase item, XmlReader reader)
        {
            item.EveryOnePermissions = (uint)reader.ReadElementContentAsInt("EveryOnePermissions", String.Empty);
        }

        private static void ProcessNextPermissions(InventoryItemBase item, XmlReader reader)
        {
            item.NextPermissions = (uint)reader.ReadElementContentAsInt("NextPermissions", String.Empty);
        }

        private static void ProcessFlags(InventoryItemBase item, XmlReader reader)
        {
            item.Flags = (uint)reader.ReadElementContentAsInt("Flags", String.Empty);
        }

        private static void ProcessGroupID(InventoryItemBase item, XmlReader reader)
        {
            item.GroupID = Util.ReadUUID(reader, "GroupID");
        }

        private static void ProcessGroupOwned(InventoryItemBase item, XmlReader reader)
        {
            item.GroupOwned = Util.ReadBoolean(reader);
        }

        private static void ProcessCreatorData(InventoryItemBase item, XmlReader reader)
        {
            item.CreatorData = reader.ReadElementContentAsString("CreatorData", String.Empty);
        }

        #endregion

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

            using (XmlTextReader reader = new XmlTextReader(new StringReader(serialization)))
            {
                reader.ReadStartElement("InventoryItem");

                ExternalRepresentationUtils.ExecuteReadProcessors<InventoryItemBase>(
                    item, m_InventoryItemXmlProcessors, reader);

                reader.ReadEndElement(); // InventoryItem
            }

            //m_log.DebugFormat("[XXX]: parsed InventoryItemBase {0} - {1}", obj.Name, obj.UUID);
            return item;
        }      
        
        public static string Serialize(InventoryItemBase inventoryItem, Dictionary<string, object> options, IUserAccountService userAccountService)
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
            writer.WriteString(OspResolver.MakeOspa(inventoryItem.CreatorIdAsUuid, userAccountService));
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
            if (options.ContainsKey("creators") && !string.IsNullOrEmpty(inventoryItem.CreatorData))
                writer.WriteElementString("CreatorData", inventoryItem.CreatorData);
            else if (options.ContainsKey("home"))
            {
                if (userAccountService != null)
                {
                    UserAccount account = userAccountService.GetUserAccount(UUID.Zero, inventoryItem.CreatorIdAsUuid);
                    if (account != null)
                    {
                        string creatorData = ExternalRepresentationUtils.CalcCreatorData((string)options["home"], inventoryItem.CreatorIdAsUuid, account.FirstName + " " + account.LastName);
                        writer.WriteElementString("CreatorData", creatorData);
                    }
                    writer.WriteElementString("CreatorID", inventoryItem.CreatorId);
                }
            }

            writer.WriteEndElement();
            
            writer.Close();
            sw.Close();
            
            return sw.ToString();
        }        
    }
}
