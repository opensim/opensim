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
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using log4net;
using OpenMetaverse;

namespace OpenSim.Framework.Serialization.External
{
    /// <summary>
    /// Serialize and deserialize LandData as an external format.
    /// </summary>
    public class LandDataSerializer
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly FrozenDictionary<string, Action<LandData, XmlReader>> m_ldProcessors
            = new Dictionary<string, Action<LandData, XmlReader>>()
            {
                { "Area",             (ld, xtr) => ld.Area = Convert.ToInt32(xtr.ReadElementString("Area")) },
                { "AuctionID",        (ld, xtr) => ld.AuctionID = Convert.ToUInt32(xtr.ReadElementString("AuctionID")) },
                { "AuthBuyerID",      (ld, xtr) => ld.AuthBuyerID = UUID.Parse(xtr.ReadElementString("AuthBuyerID"))},
                { "Category",         (ld, xtr) => ld.Category = (ParcelCategory)Convert.ToSByte(xtr.ReadElementString("Category")) },
                { "ClaimDate",        (ld, xtr) => ld.ClaimDate = Convert.ToInt32(xtr.ReadElementString("ClaimDate")) },
                { "ClaimPrice",       (ld, xtr) => ld.ClaimPrice = Convert.ToInt32(xtr.ReadElementString("ClaimPrice")) },
                { "GlobalID",         (ld, xtr) => ld.GlobalID = UUID.Parse(xtr.ReadElementString("GlobalID")) },
                { "GroupID",          (ld, xtr) => ld.GroupID = UUID.Parse(xtr.ReadElementString("GroupID")) },
                { "IsGroupOwned",     (ld, xtr) => ld.IsGroupOwned = Convert.ToBoolean(xtr.ReadElementString("IsGroupOwned")) },
                { "Bitmap",           (ld, xtr) => ld.Bitmap = Convert.FromBase64String(xtr.ReadElementString("Bitmap")) },
                { "Description",      (ld, xtr) => ld.Description = xtr.ReadElementString("Description") },
                { "Flags",            (ld, xtr) => ld.Flags = Convert.ToUInt32(xtr.ReadElementString("Flags")) },
                { "LandingType",      (ld, xtr) => ld.LandingType = Convert.ToByte(xtr.ReadElementString("LandingType")) },
                { "Name",             (ld, xtr) => ld.Name = xtr.ReadElementString("Name") },
                { "Status",           (ld, xtr) => ld.Status = (ParcelStatus) Convert.ToSByte(xtr.ReadElementString("Status")) },
                { "LocalID",          (ld, xtr) => ld.LocalID = Convert.ToInt32(xtr.ReadElementString("LocalID")) },
                { "MediaAutoScale",   (ld, xtr) => ld.MediaAutoScale = Convert.ToByte(xtr.ReadElementString("MediaAutoScale")) },
                { "MediaID",          (ld, xtr) => ld.MediaID = UUID.Parse(xtr.ReadElementString("MediaID")) },
                { "MediaURL",         (ld, xtr) => ld.MediaURL = xtr.ReadElementString("MediaURL") },
                { "MusicURL",         (ld, xtr) => ld.MusicURL = xtr.ReadElementString("MusicURL") },
                { "OwnerID",          (ld, xtr) => ld.OwnerID  = UUID.Parse(xtr.ReadElementString("OwnerID")) },

                { "ParcelAccessList", ProcessParcelAccessList },

                { "Environment", ProcessParcelEnvironment },

                { "PassHours",        (ld, xtr) => ld.PassHours = Convert.ToSingle(xtr.ReadElementString("PassHours"))},
                { "PassPrice",        (ld, xtr) => ld.PassPrice = Convert.ToInt32(xtr.ReadElementString("PassPrice"))},
                { "SalePrice",        (ld, xtr) => ld.SalePrice = Convert.ToInt32(xtr.ReadElementString("SalePrice"))},
                { "SnapshotID",       (ld, xtr) => ld.SnapshotID = UUID.Parse(xtr.ReadElementString("SnapshotID"))},
                { "UserLocation",     (ld, xtr) => ld.UserLocation = Vector3.Parse(xtr.ReadElementString("UserLocation"))},
                { "UserLookAt",       (ld, xtr) => ld.UserLookAt = Vector3.Parse(xtr.ReadElementString("UserLookAt"))},

                { "OtherCleanTime",   (ld, xtr) => ld.OtherCleanTime = Convert.ToInt32(xtr.ReadElementString("OtherCleanTime")) },
                { "SeeAVs",           (ld, xtr) => ld.SeeAVs = xtr.ReadElementString("SeeAVs") == "1" },
                { "AnyAVSnds",        (ld, xtr) => ld.AnyAVSounds = xtr.ReadElementString("AnyAVSnds") == "1" },
                { "GrpAVSnds",        (ld, xtr) => ld.GroupAVSounds = xtr.ReadElementString("GrpAVSnds") == "1" },
            }.ToFrozenDictionary();

        private static readonly FrozenDictionary<string, Action<LandAccessEntry, XmlReader>> m_laeProcessors
            = new Dictionary<string, Action<LandAccessEntry, XmlReader>>()
            {
                { "AgentID",          (lae, xtr) => lae.AgentID = UUID.Parse(xtr.ReadElementString("AgentID")) },
                { "Time",             (lae, xtr) =>
                    {
                        // We really don't care about temp vs perm here and this
                        // would break on old oars. Assume all bans are perm
                        xtr.ReadElementString("Time");
                        lae.Expires = 0; // Convert.ToUint(                       xtr.ReadElementString("Time"));
                     } },
                { "AccessList",       (lae, xtr) => lae.Flags = (AccessList) Convert.ToUInt32(xtr.ReadElementString("AccessList")) }
            }.ToFrozenDictionary();

        static LandDataSerializer()
        {
        }

        public static void ProcessParcelEnvironment(LandData ld, XmlReader xtr)
        {
            string senv = xtr.ReadElementString("Environment");
            ld.Environment = ViewerEnvironment.FromOSDString(senv);
            ld.EnvironmentVersion = ld.Environment.version;
        }

        public static void ProcessParcelAccessList(LandData ld, XmlReader xtr)
        {
            if (!xtr.IsEmptyElement)
            {
                while (xtr.Read() && xtr.NodeType != XmlNodeType.EndElement)
                {
                    LandAccessEntry lae = new LandAccessEntry();

                    xtr.ReadStartElement("ParcelAccessEntry");

                    ExternalRepresentationUtils.ExecuteReadProcessors<LandAccessEntry>(lae, m_laeProcessors, xtr);

                    xtr.ReadEndElement();

                    ld.ParcelAccessList.Add(lae);
                }
            }

            xtr.Read();
        }

        /// <summary>
        /// Reify/deserialize landData
        /// </summary>
        /// <param name="serializedLandData"></param>
        /// <returns></returns>
        /// <exception cref="System.Xml.XmlException"></exception>
        public static LandData Deserialize(byte[] serializedLandData)
        {
            return Deserialize(Encoding.UTF8.GetString(serializedLandData, 0, serializedLandData.Length));
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

            using (XmlTextReader reader = new XmlTextReader(new StringReader(serializedLandData)))
            {
                reader.DtdProcessing = DtdProcessing.Ignore;
                reader.ReadStartElement("LandData");

                ExternalRepresentationUtils.ExecuteReadProcessors<LandData>(landData, m_ldProcessors, reader);

                reader.ReadEndElement();
            }

            return landData;
        }

        /// <summary>
        /// Serialize land data
        /// </summary>
        /// <param name='landData'></param>
        /// <param name='options'>
        /// Serialization options.
        /// Can be null if there are no options.
        /// "wipe-owners" will write UUID.Zero rather than the ownerID so that a later reload loads all parcels with the estate owner as the owner
        /// </param>
        public static string Serialize(LandData landData, Dictionary<string, object> options)
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter xtw = new XmlTextWriter(sw);
            xtw.Formatting = Formatting.None;

            xtw.WriteStartDocument();
            xtw.WriteStartElement("LandData");

            xtw.WriteElementString("Area",           Convert.ToString(landData.Area));
            xtw.WriteElementString("AuctionID",      Convert.ToString(landData.AuctionID));
            xtw.WriteElementString("AuthBuyerID",    landData.AuthBuyerID.ToString());
            xtw.WriteElementString("Category",       Convert.ToString((sbyte)landData.Category));
            xtw.WriteElementString("ClaimDate",      Convert.ToString(landData.ClaimDate));
            xtw.WriteElementString("ClaimPrice",     Convert.ToString(landData.ClaimPrice));
            xtw.WriteElementString("GlobalID",       landData.GlobalID.ToString());

            UUID groupID = options.ContainsKey("wipe-owners") ? UUID.Zero : landData.GroupID;
            xtw.WriteElementString("GroupID",        groupID.ToString());

            bool isGroupOwned = options.ContainsKey("wipe-owners") ? false : landData.IsGroupOwned;
            xtw.WriteElementString("IsGroupOwned",   Convert.ToString(isGroupOwned));

            xtw.WriteElementString("Bitmap",         Convert.ToBase64String(landData.Bitmap));
            xtw.WriteElementString("Description",    landData.Description);
            xtw.WriteElementString("Flags",          Convert.ToString((uint)landData.Flags));
            xtw.WriteElementString("LandingType",    Convert.ToString((byte)landData.LandingType));
            xtw.WriteElementString("Name",           landData.Name);
            xtw.WriteElementString("Status",         Convert.ToString((sbyte)landData.Status));
            xtw.WriteElementString("LocalID",        landData.LocalID.ToString());
            xtw.WriteElementString("MediaAutoScale", Convert.ToString(landData.MediaAutoScale));
            xtw.WriteElementString("MediaID",        landData.MediaID.ToString());
            xtw.WriteElementString("MediaURL",       landData.MediaURL);
            xtw.WriteElementString("MusicURL",       landData.MusicURL);

            UUID ownerID = options.ContainsKey("wipe-owners") ? UUID.Zero : landData.OwnerID;
            xtw.WriteElementString("OwnerID",        ownerID.ToString());

            xtw.WriteStartElement("ParcelAccessList");
            foreach (LandAccessEntry pal in landData.ParcelAccessList)
            {
                xtw.WriteStartElement("ParcelAccessEntry");
                xtw.WriteElementString("AgentID",     pal.AgentID.ToString());
                xtw.WriteElementString("Time",        pal.Expires.ToString());
                xtw.WriteElementString("AccessList",  Convert.ToString((uint)pal.Flags));
                xtw.WriteEndElement();
            }
            xtw.WriteEndElement();

            xtw.WriteElementString("PassHours",       Convert.ToString(landData.PassHours));
            xtw.WriteElementString("PassPrice",       Convert.ToString(landData.PassPrice));
            xtw.WriteElementString("SalePrice",       Convert.ToString(landData.SalePrice));
            xtw.WriteElementString("SnapshotID",      landData.SnapshotID.ToString());
            xtw.WriteElementString("UserLocation",    landData.UserLocation.ToString());
            xtw.WriteElementString("UserLookAt",      landData.UserLookAt.ToString());
            xtw.WriteElementString("Dwell",           "0");
            xtw.WriteElementString("OtherCleanTime",  Convert.ToString(landData.OtherCleanTime));

            xtw.WriteElementString("SeeAVs",          landData.SeeAVs ? "1" : "0");
            xtw.WriteElementString("AnyAVSnds",       landData.AnyAVSounds ? "1" : "0");
            xtw.WriteElementString("GrpAVSnds",       landData.GroupAVSounds ? "1" : "0");

            if (landData.Environment != null)
            {
                try
                {
                    string senv = ViewerEnvironment.ToOSDString(landData.Environment);
                    xtw.WriteElementString("Environment", senv);
                }
                catch { }
            }
            xtw.WriteEndElement();

            xtw.Close();
            sw.Close();

            return sw.ToString();
        }
    }
}
