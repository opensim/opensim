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

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using System;
using System.Text;
using System.Xml;
using System.IO;
using System.Xml.Serialization;

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// A new version of the old Channel class, simplified
    /// </summary>
    public class TerrainChannel : ITerrainChannel
    {
        protected bool[,] m_taint;
        protected short[] m_map;

        public int Width { get; private set; }  // X dimension
        // Unfortunately, for historical reasons, in this module 'Width' is X and 'Height' is Y
        public int Height { get; private set; } // Y dimension
        public int Altitude { get; private set; } // Y dimension

        // Default, not-often-used builder
        public TerrainChannel()
        {
            InitializeStructures(Constants.RegionSize, Constants.RegionSize, Constants.RegionHeight, false);
            FlatLand();
            // PinHeadIsland();
        }

        // Create terrain of given size
        public TerrainChannel(int pX, int pY)
        {
            InitializeStructures((uint)pX, (uint)pY, Constants.RegionHeight, true);
        }

        // Create terrain of specified size and initialize with specified terrain.
        // TODO: join this with the terrain initializers.
        public TerrainChannel(String type, uint pX, uint pY, uint pZ)
        {
            InitializeStructures(pX, pY, pZ, false);
            if (type.Equals("flat"))
                FlatLand();
            else
                PinHeadIsland();
        }

        public TerrainChannel(double[,] pM, uint pH)
        {
            InitializeStructures((uint)pM.GetLength(0), (uint)pM.GetLength(1), pH, false);
            int idx = 0;
            for (int ii = 0; ii < Height; ii++)
                for (int jj = 0; jj < Width; jj++)
                    m_map[idx++] = ToCompressedHeight(pM[ii, jj]);
        }

        #region ITerrainChannel Members

        // ITerrainChannel.MakeCopy()
        public ITerrainChannel MakeCopy()
        {
            return this.Copy();
        }

        // ITerrainChannel.GetCompressedMap()
        public short[] GetCompressedMap()
        {
            return m_map;
        }

        // ITerrainChannel.GetFloatsSerialized()
        public float[] GetFloatsSerialised()
        {
            int points = Width * Height;
            float[] heights = new float[points];

            for (int ii = 0; ii < points; ii++)
                heights[ii] = FromCompressedHeight(m_map[ii]);

            return heights;
        }

        // ITerrainChannel.GetDoubles()
        public double[,] GetDoubles()
        {
            int w = Width;
            int l = Height;
            double[,] heights = new double[w, l];

            int idx = 0; // index into serialized array
            for (int ii = 0; ii < l; ii++)
            {
                for (int jj = 0; jj < w; jj++)
                {
                    heights[ii, jj] = (double)FromCompressedHeight(m_map[idx]);
                    idx++;
                }
            }

            return heights;
        }

        // ITerrainChannel.this[x,y]
        public double this[int x, int y]
        {
            get { return m_map[x * Width + y]; }
            set
            {
                // Will "fix" terrain hole problems. Although not fantastically.
                if (Double.IsNaN(value) || Double.IsInfinity(value))
                    return;

                int idx = x * Width + y;
                if (m_map[idx] != value)
                {
                    m_taint[x / Constants.TerrainPatchSize, y / Constants.TerrainPatchSize] = true;
                    m_map[idx] = ToCompressedHeight(value);
                }
            }
        }

        // ITerrainChannel.Tainted()
        public bool Tainted(int x, int y)
        {
            if (m_taint[x / Constants.TerrainPatchSize, y / Constants.TerrainPatchSize])
            {
                m_taint[x / Constants.TerrainPatchSize, y / Constants.TerrainPatchSize] = false;
                return true;
            }
            return false;
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

        private void InitializeStructures(uint pX, uint pY, uint pZ, bool shouldInitializeHeightmap)
        {
            Width = (int)pX;
            Height = (int)pY;
            Altitude = (int)pZ;
            m_map = new short[Width * Height];
            m_taint = new bool[Width / Constants.TerrainPatchSize, Height / Constants.TerrainPatchSize];
            ClearTaint();
            if (shouldInitializeHeightmap)
            {
                FlatLand();
            }
        }

        public void ClearTaint()
        {
            for (int ii = 0; ii < Width / Constants.TerrainPatchSize; ii++)
                for (int jj = 0; jj < Height / Constants.TerrainPatchSize; jj++)
                    m_taint[ii, jj] = false;
        }

        // To save space (especially for large regions), keep the height as a short integer
        //    that is coded as the float height times the compression factor (usually '100'
        //    to make for two decimal points).
        public short ToCompressedHeight(double pHeight)
        {
            return (short)(pHeight * Constants.TerrainCompression);
        }

        public float FromCompressedHeight(short pHeight)
        {
            return ((float)pHeight) / Constants.TerrainCompression;
        }

        public TerrainChannel Copy()
        {
            TerrainChannel copy = new TerrainChannel();
            copy.m_map = (short[])m_map.Clone();
            copy.m_taint = (bool[,])m_taint.Clone();
            copy.Width = Width;
            copy.Height = Height;
            copy.Altitude = Altitude;

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
            TerrainChannelXMLPackage package = new TerrainChannelXMLPackage(Width, Height, Altitude, m_map);
            XmlSerializer serializer = new XmlSerializer(typeof(TerrainChannelXMLPackage));
            serializer.Serialize(xmlWriter, package);
        }

        // New terrain serialization format that includes the width and length.
        private void FromXml2(XmlReader xmlReader)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(TerrainChannelXMLPackage));
            TerrainChannelXMLPackage package = (TerrainChannelXMLPackage)serializer.Deserialize(xmlReader);
            Width = package.SizeX;
            Height = package.SizeY;
            Altitude = package.SizeZ;
            m_map = package.Map;
        }

        // Fill the heightmap with the center bump terrain
        private void PinHeadIsland()
        {
            int x;
            for (x = 0; x < Width; x++)
            {
                int y;
                for (y = 0; y < Height; y++)
                {
                    int idx = x * (int)Width + y;
                    m_map[idx] = ToCompressedHeight(TerrainUtil.PerlinNoise2D(x, y, 2, 0.125) * 10);
                    short spherFacA = ToCompressedHeight(TerrainUtil.SphericalFactor(x, y, Constants.RegionSize / 2.0, Constants.RegionSize / 2.0, 50) * 0.01);
                    short spherFacB = ToCompressedHeight(TerrainUtil.SphericalFactor(x, y, Constants.RegionSize / 2.0, Constants.RegionSize / 2.0, 100) * 0.001);
                    if (m_map[idx] < spherFacA)
                        m_map[idx] = spherFacA;
                    if (m_map[idx] < spherFacB)
                        m_map[idx] = spherFacB;
                }
            }
        }

        private void FlatLand()
        {
            short flatHeight = ToCompressedHeight(21);
            for (int ii = 0; ii < m_map.Length; ii++)
                m_map[ii] = flatHeight;
        }
    }
}
