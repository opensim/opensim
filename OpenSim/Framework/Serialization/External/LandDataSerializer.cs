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
using System.Text;
using System.Xml;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Framework.Serialization.External
{
    /// <summary>
    /// Serialize and deserialize LandData as an external format.
    /// </summary>
    public class LandDataSerializer
    {
        protected static UTF8Encoding m_utf8Encoding = new UTF8Encoding();
        
        /// <summary>
        /// Reify/deserialize landData
        /// </summary>
        /// <param name="serializedLandData"></param>
        /// <returns></returns>
        /// <exception cref="System.Xml.XmlException"></exception>
        public static LandData Deserialize(byte[] serializedLandData)
        {
            return Deserialize(m_utf8Encoding.GetString(serializedLandData, 0, serializedLandData.Length));
        }
        
        /// <summary>
        /// Reify/deserialize landData
        /// </summary>
        /// <param name="serializedLandData"></param>
        /// <returns></returns>
        /// <exception cref="System.Xml.XmlException"></exception>
        public static LandData Deserialize(string serializedLandData)
        {
            LandData landData = new LandData();
            
            StringReader sr = new StringReader(serializedLandData);
            XmlTextReader xtr = new XmlTextReader(sr);
            
            xtr.ReadStartElement("LandData");

            landData.Area           = Convert.ToInt32(                 xtr.ReadElementString("Area"));
            landData.AuctionID      = Convert.ToUInt32(                xtr.ReadElementString("AuctionID"));
            landData.AuthBuyerID    = UUID.Parse(                      xtr.ReadElementString("AuthBuyerID"));
            landData.Category       = (ParcelCategory)Convert.ToSByte( xtr.ReadElementString("Category"));
            landData.ClaimDate      = Convert.ToInt32(                 xtr.ReadElementString("ClaimDate"));
            landData.ClaimPrice     = Convert.ToInt32(                 xtr.ReadElementString("ClaimPrice"));
            landData.GlobalID       = UUID.Parse(                      xtr.ReadElementString("GlobalID"));
            landData.GroupID        = UUID.Parse(                      xtr.ReadElementString("GroupID"));
            landData.IsGroupOwned   = Convert.ToBoolean(               xtr.ReadElementString("IsGroupOwned"));
            landData.Bitmap         = Convert.FromBase64String(        xtr.ReadElementString("Bitmap"));
            landData.Description    =                                  xtr.ReadElementString("Description");
            landData.Flags          = Convert.ToUInt32(                xtr.ReadElementString("Flags"));
            landData.LandingType    = Convert.ToByte(                  xtr.ReadElementString("LandingType"));
            landData.Name           =                                  xtr.ReadElementString("Name");
            landData.Status         = (ParcelStatus)Convert.ToSByte(   xtr.ReadElementString("Status"));
            landData.LocalID        = Convert.ToInt32(                 xtr.ReadElementString("LocalID"));
            landData.MediaAutoScale = Convert.ToByte(                  xtr.ReadElementString("MediaAutoScale"));
            landData.MediaID        = UUID.Parse(                      xtr.ReadElementString("MediaID"));
            landData.MediaURL       =                                  xtr.ReadElementString("MediaURL");
            landData.MusicURL       =                                  xtr.ReadElementString("MusicURL");
            landData.OwnerID        = UUID.Parse(                      xtr.ReadElementString("OwnerID"));

            landData.ParcelAccessList = new List<ParcelManager.ParcelAccessEntry>();
            xtr.ReadStartElement("ParcelAccessList");
            while (xtr.Read() && xtr.NodeType != XmlNodeType.EndElement)
            {
                ParcelManager.ParcelAccessEntry pae;

                xtr.ReadStartElement("ParcelAccessEntry");
                pae.AgentID    = UUID.Parse(                           xtr.ReadElementString("AgentID"));
                pae.Time       = Convert.ToDateTime(                   xtr.ReadElementString("Time"));
                pae.Flags      = (AccessList)Convert.ToUInt32(         xtr.ReadElementString("AccessList"));
                xtr.ReadEndElement();

                landData.ParcelAccessList.Add(pae);
            }
            xtr.ReadEndElement();

            landData.PassHours      = Convert.ToSingle(                xtr.ReadElementString("PassHours"));
            landData.PassPrice      = Convert.ToInt32(                 xtr.ReadElementString("PassPrice"));
            landData.SalePrice      = Convert.ToInt32(                 xtr.ReadElementString("SalePrice"));
            landData.SnapshotID     = UUID.Parse(                      xtr.ReadElementString("SnapshotID"));
            landData.UserLocation   = Vector3.Parse(                   xtr.ReadElementString("UserLocation"));
            landData.UserLookAt     = Vector3.Parse(                   xtr.ReadElementString("UserLookAt"));
            landData.Dwell          = Convert.ToInt32(                 xtr.ReadElementString("Dwell"));
            landData.OtherCleanTime = Convert.ToInt32(                 xtr.ReadElementString("OtherCleanTime"));

            xtr.ReadEndElement();
            
            xtr.Close();
            sr.Close();
            
            return landData;
        }
        
        public static string Serialize(LandData landData)
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter xtw = new XmlTextWriter(sw);
            xtw.Formatting = Formatting.Indented;

            xtw.WriteStartDocument();
            xtw.WriteStartElement("LandData");
            
            xtw.WriteElementString("Area", landData.Area.ToString());            
            xtw.WriteElementString("AuctionID", landData.AuctionID.ToString());
            xtw.WriteElementString("AuthBuyerID", landData.AuthBuyerID.ToString());
            xtw.WriteElementString("Category", landData.Category.ToString());
            xtw.WriteElementString("ClaimDate", landData.ClaimDate.ToString());
            xtw.WriteElementString("ClaimPrice", landData.ClaimPrice.ToString());
            xtw.WriteElementString("GlobalID", landData.GlobalID.ToString());
            xtw.WriteElementString("GroupID", landData.GroupID.ToString());
            xtw.WriteElementString("IsGroupOwned", landData.IsGroupOwned.ToString());
            xtw.WriteElementString("Bitmap", landData.Bitmap.ToString());
            xtw.WriteElementString("Description", landData.Description);
            xtw.WriteElementString("Flags", landData.Flags.ToString());
            xtw.WriteElementString("LandingType", landData.LandingType.ToString());
            xtw.WriteElementString("Name", landData.Name);
            xtw.WriteElementString("Status", landData.Status.ToString());
            xtw.WriteElementString("LocalID", landData.LocalID.ToString());
            xtw.WriteElementString("MediaAutoScale", landData.MediaAutoScale.ToString());
            xtw.WriteElementString("MediaID", landData.MediaID.ToString());
            xtw.WriteElementString("MediaURL", landData.MediaURL.ToString());
            xtw.WriteElementString("MusicURL", landData.MusicURL.ToString());
            xtw.WriteElementString("OwnerID", landData.OwnerID.ToString());

            xtw.WriteStartElement("ParcelAccessList");
            foreach (ParcelManager.ParcelAccessEntry pal in landData.ParcelAccessList)
            {
                xtw.WriteStartElement("ParcelAccessEntry");
                xtw.WriteElementString("AgentID", pal.AgentID.ToString());
                xtw.WriteElementString("Time", pal.Time.ToString());
                xtw.WriteElementString("AccessList", pal.Flags.ToString());
                xtw.WriteEndElement();
            }
            xtw.WriteEndElement();
            
            xtw.WriteElementString("PassHours", landData.PassHours.ToString());
            xtw.WriteElementString("PassPrice", landData.PassPrice.ToString());
            xtw.WriteElementString("SalePrice", landData.SalePrice.ToString());
            xtw.WriteElementString("SnapshotID", landData.SnapshotID.ToString());
            xtw.WriteElementString("UserLocation", landData.UserLocation.ToString());
            xtw.WriteElementString("UserLookAt", landData.UserLookAt.ToString());
            xtw.WriteElementString("Dwell", landData.Dwell.ToString());
            xtw.WriteElementString("OtherCleanTime", landData.OtherCleanTime.ToString());

            xtw.WriteEndElement();

            xtw.Close();
            sw.Close();
            
            return sw.ToString();
        }
    }
}
