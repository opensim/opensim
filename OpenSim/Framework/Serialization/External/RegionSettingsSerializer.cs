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

using System.IO;
using System.Text;
using System.Xml;
using OpenMetaverse;
using OpenSim.Framework;
using log4net;
using System.Reflection;

namespace OpenSim.Framework.Serialization.External
{
    /// <summary>
    /// Serialize and deserialize region settings as an external format.
    /// </summary>
    public class RegionSettingsSerializer
    {
        /// <summary>
        /// Deserialize settings
        /// </summary>
        /// <param name="serializedSettings"></param>
        /// <returns></returns>
        /// <exception cref="System.Xml.XmlException"></exception>
        public static RegionSettings Deserialize(byte[] serializedSettings)
        {
            return Deserialize(Encoding.ASCII.GetString(serializedSettings, 0, serializedSettings.Length));
        }

        /// <summary>
        /// Deserialize settings
        /// </summary>
        /// <param name="serializedSettings"></param>
        /// <returns></returns>
        /// <exception cref="System.Xml.XmlException"></exception>
        public static RegionSettings Deserialize(string serializedSettings)
        {
            RegionSettings settings = new RegionSettings();

            StringReader sr = new StringReader(serializedSettings);
            XmlTextReader xtr = new XmlTextReader(sr);

            xtr.ReadStartElement("RegionSettings");

            xtr.ReadStartElement("General");

            while (xtr.Read() && xtr.NodeType != XmlNodeType.EndElement)
            {
                switch (xtr.Name)
                {
                    case "AllowDamage":
                        settings.AllowDamage = bool.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "AllowLandResell":
                        settings.AllowLandResell = bool.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "AllowLandJoinDivide":
                        settings.AllowLandJoinDivide = bool.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "BlockFly":
                        settings.BlockFly = bool.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "BlockLandShowInSearch":
                        settings.BlockShowInSearch = bool.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "BlockTerraform":
                        settings.BlockTerraform = bool.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "DisableCollisions":
                        settings.DisableCollisions = bool.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "DisablePhysics":
                        settings.DisablePhysics = bool.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "DisableScripts":
                        settings.DisableScripts = bool.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "MaturityRating":
                        settings.Maturity = int.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "RestrictPushing":
                        settings.RestrictPushing = bool.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "AgentLimit":
                        settings.AgentLimit = int.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "ObjectBonus":
                        settings.ObjectBonus = double.Parse(xtr.ReadElementContentAsString(), Culture.NumberFormatInfo);
                        break;
                }
            }

            xtr.ReadEndElement();
            xtr.ReadStartElement("GroundTextures");

            while (xtr.Read() && xtr.NodeType != XmlNodeType.EndElement)
            {
                switch (xtr.Name)
                {
                    case "Texture1":
                        settings.TerrainTexture1 = UUID.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "Texture2":
                        settings.TerrainTexture2 = UUID.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "Texture3":
                        settings.TerrainTexture3 = UUID.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "Texture4":
                        settings.TerrainTexture4 = UUID.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "ElevationLowSW":
                        settings.Elevation1SW = double.Parse(xtr.ReadElementContentAsString(), Culture.NumberFormatInfo);
                        break;
                    case "ElevationLowNW":
                        settings.Elevation1NW = double.Parse(xtr.ReadElementContentAsString(), Culture.NumberFormatInfo);
                        break;
                    case "ElevationLowSE":
                        settings.Elevation1SE = double.Parse(xtr.ReadElementContentAsString(), Culture.NumberFormatInfo);
                        break;
                    case "ElevationLowNE":
                        settings.Elevation1NE = double.Parse(xtr.ReadElementContentAsString(), Culture.NumberFormatInfo);
                        break;
                    case "ElevationHighSW":
                        settings.Elevation2SW = double.Parse(xtr.ReadElementContentAsString(), Culture.NumberFormatInfo);
                        break;
                    case "ElevationHighNW":
                        settings.Elevation2NW = double.Parse(xtr.ReadElementContentAsString(), Culture.NumberFormatInfo);
                        break;
                    case "ElevationHighSE":
                        settings.Elevation2SE = double.Parse(xtr.ReadElementContentAsString(), Culture.NumberFormatInfo);
                        break;
                    case "ElevationHighNE":
                        settings.Elevation2NE = double.Parse(xtr.ReadElementContentAsString(), Culture.NumberFormatInfo);
                        break;
                }
            }

            xtr.ReadEndElement();
            xtr.ReadStartElement("Terrain");

            while (xtr.Read() && xtr.NodeType != XmlNodeType.EndElement)
            {
                switch (xtr.Name)
                {
                    case "WaterHeight":
                        settings.WaterHeight = double.Parse(xtr.ReadElementContentAsString(), Culture.NumberFormatInfo);
                        break;
                    case "TerrainRaiseLimit":
                        settings.TerrainRaiseLimit = double.Parse(xtr.ReadElementContentAsString(), Culture.NumberFormatInfo);
                        break;
                    case "TerrainLowerLimit":
                        settings.TerrainLowerLimit = double.Parse(xtr.ReadElementContentAsString(), Culture.NumberFormatInfo);
                        break;
                    case "UseEstateSun":
                        settings.UseEstateSun = bool.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "FixedSun":
                        settings.FixedSun = bool.Parse(xtr.ReadElementContentAsString());
                        break;
                    case "SunPosition":
                        settings.SunPosition = double.Parse(xtr.ReadElementContentAsString());
                        break;
                }
            }

            xtr.ReadEndElement();

            if (xtr.IsStartElement("Telehub"))
            {
                xtr.ReadStartElement("Telehub");

                while (xtr.Read() && xtr.NodeType != XmlNodeType.EndElement)
                {
                    switch (xtr.Name)
                    {
                        case "TelehubObject":
                            settings.TelehubObject = UUID.Parse(xtr.ReadElementContentAsString());
                            break;
                        case "SpawnPoint":
                            string str = xtr.ReadElementContentAsString();
                            SpawnPoint sp = SpawnPoint.Parse(str);
                            settings.AddSpawnPoint(sp);
                            break;
                    }
                }
            }

            xtr.Close();
            sr.Close();

            return settings;
        }

        public static string Serialize(RegionSettings settings)
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter xtw = new XmlTextWriter(sw);
            xtw.Formatting = Formatting.Indented;
            xtw.WriteStartDocument();

            xtw.WriteStartElement("RegionSettings");

            xtw.WriteStartElement("General");
            xtw.WriteElementString("AllowDamage", settings.AllowDamage.ToString());
            xtw.WriteElementString("AllowLandResell", settings.AllowLandResell.ToString());
            xtw.WriteElementString("AllowLandJoinDivide", settings.AllowLandJoinDivide.ToString());
            xtw.WriteElementString("BlockFly", settings.BlockFly.ToString());
            xtw.WriteElementString("BlockLandShowInSearch", settings.BlockShowInSearch.ToString());
            xtw.WriteElementString("BlockTerraform", settings.BlockTerraform.ToString());
            xtw.WriteElementString("DisableCollisions", settings.DisableCollisions.ToString());
            xtw.WriteElementString("DisablePhysics", settings.DisablePhysics.ToString());
            xtw.WriteElementString("DisableScripts", settings.DisableScripts.ToString());
            xtw.WriteElementString("MaturityRating", settings.Maturity.ToString());
            xtw.WriteElementString("RestrictPushing", settings.RestrictPushing.ToString());
            xtw.WriteElementString("AgentLimit", settings.AgentLimit.ToString());
            xtw.WriteElementString("ObjectBonus", settings.ObjectBonus.ToString());
            xtw.WriteEndElement();

            xtw.WriteStartElement("GroundTextures");
            xtw.WriteElementString("Texture1", settings.TerrainTexture1.ToString());
            xtw.WriteElementString("Texture2", settings.TerrainTexture2.ToString());
            xtw.WriteElementString("Texture3", settings.TerrainTexture3.ToString());
            xtw.WriteElementString("Texture4", settings.TerrainTexture4.ToString());
            xtw.WriteElementString("ElevationLowSW", settings.Elevation1SW.ToString());
            xtw.WriteElementString("ElevationLowNW", settings.Elevation1NW.ToString());
            xtw.WriteElementString("ElevationLowSE", settings.Elevation1SE.ToString());
            xtw.WriteElementString("ElevationLowNE", settings.Elevation1NE.ToString());
            xtw.WriteElementString("ElevationHighSW", settings.Elevation2SW.ToString());
            xtw.WriteElementString("ElevationHighNW", settings.Elevation2NW.ToString());
            xtw.WriteElementString("ElevationHighSE", settings.Elevation2SE.ToString());
            xtw.WriteElementString("ElevationHighNE", settings.Elevation2NE.ToString());
            xtw.WriteEndElement();

            xtw.WriteStartElement("Terrain");
            xtw.WriteElementString("WaterHeight", settings.WaterHeight.ToString());
            xtw.WriteElementString("TerrainRaiseLimit", settings.TerrainRaiseLimit.ToString());
            xtw.WriteElementString("TerrainLowerLimit", settings.TerrainLowerLimit.ToString());
            xtw.WriteElementString("UseEstateSun", settings.UseEstateSun.ToString());
            xtw.WriteElementString("FixedSun", settings.FixedSun.ToString());
            xtw.WriteElementString("SunPosition", settings.SunPosition.ToString());
            // Note: 'SunVector' isn't saved because this value is owned by the Sun Module, which
            // calculates it automatically according to the date and other factors.
            xtw.WriteEndElement();

            xtw.WriteStartElement("Telehub");
            if (settings.TelehubObject != UUID.Zero)
            {
                xtw.WriteElementString("TelehubObject", settings.TelehubObject.ToString());
                foreach (SpawnPoint sp in settings.SpawnPoints())
                    xtw.WriteElementString("SpawnPoint", sp.ToString());
            }
            xtw.WriteEndElement();

            xtw.WriteEndElement();

            xtw.Close();
            sw.Close();

            return sw.ToString();
        }
    }
}
