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
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

using log4net;

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// A new version of the old Channel class, simplified
    /// </summary>
    public class TerrainChannel : ITerrainChannel
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static string LogHeader = "[TERRAIN CHANNEL]";

        protected TerrainData m_terrainData;

        public int Width { get { return m_terrainData.SizeX; } }  // X dimension
        // Unfortunately, for historical reasons, in this module 'Width' is X and 'Height' is Y
        public int Height { get { return m_terrainData.SizeY; } } // Y dimension
        public int Altitude { get { return m_terrainData.SizeZ; } } // Y dimension

        // Default, not-often-used builder
        public TerrainChannel()
        {
            m_terrainData = new HeightmapTerrainData((int)Constants.RegionSize, (int)Constants.RegionSize, (int)Constants.RegionHeight);
            FlatLand();
            // PinHeadIsland();
        }

        // Create terrain of given size
        public TerrainChannel(int pX, int pY)
        {
            m_terrainData = new HeightmapTerrainData(pX, pY, (int)Constants.RegionHeight);
        }

        // Create terrain of specified size and initialize with specified terrain.
        // TODO: join this with the terrain initializers.
        public TerrainChannel(String type, int pX, int pY, int pZ)
        {
            m_terrainData = new HeightmapTerrainData(pX, pY, pZ);
            if (type.Equals("flat"))
                FlatLand();
            else
                PinHeadIsland();
        }

        public TerrainChannel(double[,] pM, uint pAltitude)
        {
            m_terrainData = new HeightmapTerrainData(pM);
        }

        #region ITerrainChannel Members

        // ITerrainChannel.MakeCopy()
        public ITerrainChannel MakeCopy()
        {
            return this.Copy();
        }

        // ITerrainChannel.GetTerrainData()
        public TerrainData GetTerrainData()
        {
            return m_terrainData;
        }

        // ITerrainChannel.GetFloatsSerialized()
        // NOTICE that the one dimensional form is ordered by Y!!
        public float[] GetFloatsSerialised()
        {
            int points = Width * Height;
            float[] heights = new float[points];

            int idx = 0;
            for (int ii = 0; ii < Height; ii++)
                for (int jj = 0; jj < Width; jj++)
                    heights[idx++] = m_terrainData[jj, ii];

            return heights;
        }

        // ITerrainChannel.GetDoubles()
        public double[,] GetDoubles()
        {
            int w = Width;
            int l = Height;
            double[,] heights = new double[w, l];

            int idx = 0; // index into serialized array
            for (int ii = 0; ii < w; ii++)
            {
                for (int jj = 0; jj < l; jj++)
                {
                    heights[ii, jj] = (double)m_terrainData[ii, jj];
                    idx++;
                }
            }

            return heights;
        }

        // ITerrainChannel.this[x,y]
        public double this[int x, int y]
        {
            get { return (double)m_terrainData[x, y]; }
            set
            {
                if (Double.IsNaN(value) || Double.IsInfinity(value))
                    return;

                m_terrainData[x, y] = (float)value;
            }
        }

        // ITerrainChannel.Tainted()
        public bool Tainted(int x, int y)
        {
            return m_terrainData.IsTaintedAt(x, y);
        }

        // ITerrainChannel.SaveToXmlString()
        public string SaveToXmlString()
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = Util.UTF8;
            using (StringWriter sw = new StringWriter())
            {
                using (XmlWriter writer = XmlWriter.Create(sw, settings))
                {
                    WriteXml(writer);
                }
                string output = sw.ToString();
                return output;
            }
        }

        // ITerrainChannel.LoadFromXmlString()
        public void LoadFromXmlString(string data)
        {
            StringReader sr = new StringReader(data);
            XmlTextReader reader = new XmlTextReader(sr);
            reader.Read();

            ReadXml(reader);
            reader.Close();
            sr.Close();
        }

        #endregion

        /*
        // To save space (especially for large regions), keep the height as a short integer
        //    that is coded as the float height times the compression factor (usually '100'
        //    to make for two decimal points).
        public static short ToCompressedHeight(double pHeight)
        {
            return (short)(pHeight * Constants.TerrainCompression);
        }

        public static float FromCompressedHeight(short pHeight)
        {
            return ((float)pHeight) / Constants.TerrainCompression;
        }
         */

        public TerrainChannel Copy()
        {
            TerrainChannel copy = new TerrainChannel();
            copy.m_terrainData = m_terrainData.Clone();
            return copy;
        }

        private void WriteXml(XmlWriter writer)
        {
            if (Width == Constants.RegionSize && Height == Constants.RegionSize)
            {
                // Downward compatibility for legacy region terrain maps.
                // If region is exactly legacy size, return the old format XML.
                writer.WriteStartElement(String.Empty, "TerrainMap", String.Empty);
                ToXml(writer);
                writer.WriteEndElement();
            }
            else
            {
                // New format XML that includes width and length.
                writer.WriteStartElement(String.Empty, "TerrainMap2", String.Empty);
                ToXml2(writer);
                writer.WriteEndElement();
            }
        }

        private void ReadXml(XmlReader reader)
        {
            // Check the first element. If legacy element, use the legacy reader.
            if (reader.IsStartElement("TerrainMap"))
            {
                reader.ReadStartElement("TerrainMap");
                FromXml(reader);
            }
            else
            {
                reader.ReadStartElement("TerrainMap2");
                FromXml2(reader);
            }
        }

        // Write legacy terrain map. Presumed to be 256x256 of data encoded as floats in a byte array.
        private void ToXml(XmlWriter xmlWriter)
        {
            float[] mapData = GetFloatsSerialised();
            byte[] buffer = new byte[mapData.Length * 4];
            for (int i = 0; i < mapData.Length; i++)
            {
                byte[] value = BitConverter.GetBytes(mapData[i]);
                Array.Copy(value, 0, buffer, (i * 4), 4);
            }
            XmlSerializer serializer = new XmlSerializer(typeof(byte[]));
            serializer.Serialize(xmlWriter, buffer);
        }

        // Read legacy terrain map. Presumed to be 256x256 of data encoded as floats in a byte array.
        private void FromXml(XmlReader xmlReader)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(byte[]));
            byte[] dataArray = (byte[])serializer.Deserialize(xmlReader);
            int index = 0;

            m_terrainData = new HeightmapTerrainData(Width, Height, Altitude);
            
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float value;
                    value = BitConverter.ToSingle(dataArray, index);
                    index += 4;
                    this[x, y] = (double)value;
                }
            }
        }

        private class TerrainChannelXMLPackage
        {
            public int Version;
            public int SizeX;
            public int SizeY;
            public int SizeZ;
            public short[] Map;
            public TerrainChannelXMLPackage(int pX, int pY, int pZ, short[] pMap)
            {
                Version = 1;
                SizeX = pX;
                SizeY = pY;
                SizeZ = pZ;
                Map = pMap;
            }
        }

        // New terrain serialization format that includes the width and length.
        private void ToXml2(XmlWriter xmlWriter)
        {
            TerrainChannelXMLPackage package = new TerrainChannelXMLPackage(Width, Height, Altitude, m_terrainData.GetCompressedMap());
            XmlSerializer serializer = new XmlSerializer(typeof(TerrainChannelXMLPackage));
            serializer.Serialize(xmlWriter, package);
        }

        // New terrain serialization format that includes the width and length.
        private void FromXml2(XmlReader xmlReader)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(TerrainChannelXMLPackage));
            TerrainChannelXMLPackage package = (TerrainChannelXMLPackage)serializer.Deserialize(xmlReader);
            m_terrainData = new HeightmapTerrainData(package.Map, package.SizeX, package.SizeY, package.SizeZ);
        }

        // Fill the heightmap with the center bump terrain
        private void PinHeadIsland()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    m_terrainData[x, y] = (float)TerrainUtil.PerlinNoise2D(x, y, 2, 0.125) * 10;
                    float spherFacA = (float)(TerrainUtil.SphericalFactor(x, y, m_terrainData.SizeX / 2.0, m_terrainData.SizeY / 2.0, 50) * 0.01d);
                    float spherFacB = (float)(TerrainUtil.SphericalFactor(x, y, m_terrainData.SizeX / 2.0, m_terrainData.SizeY / 2.0, 100) * 0.001d);
                    if (m_terrainData[x, y]< spherFacA)
                        m_terrainData[x, y]= spherFacA;
                    if (m_terrainData[x, y]< spherFacB)
                        m_terrainData[x, y] = spherFacB;
                }
            }
        }

        private void FlatLand()
        {
            for (int xx = 0; xx < Width; xx++)
                for (int yy = 0; yy < Height; yy++)
                    m_terrainData[xx, yy] = 21;
        }
    }
}
