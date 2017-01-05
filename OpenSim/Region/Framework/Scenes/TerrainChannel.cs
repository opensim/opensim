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

using OpenMetaverse;

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

        // Create channel passed a heightmap and expected dimensions of the region.
        // The heightmap might not fit the passed size so accomodations must be made.
        public TerrainChannel(double[,] pM, int pSizeX, int pSizeY, int pAltitude)
        {
            int hmSizeX = pM.GetLength(0);
            int hmSizeY = pM.GetLength(1);

            m_terrainData = new HeightmapTerrainData(pSizeX, pSizeY, pAltitude);

            for (int xx = 0; xx < pSizeX; xx++)
                for (int yy = 0; yy < pSizeY; yy++)
                    if (xx > hmSizeX || yy > hmSizeY)
                        m_terrainData[xx, yy] = TerrainData.DefaultTerrainHeight;
                    else
                        m_terrainData[xx, yy] = (float)pM[xx, yy];
        }

        public TerrainChannel(TerrainData pTerrData)
        {
            m_terrainData = pTerrData;
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
        // This one dimensional version is ordered so height = map[y*sizeX+x];
        // DEPRECATED: don't use this function as it does not retain the dimensions of the terrain
        //     and the caller will probably do the wrong thing if the terrain is not the legacy 256x256.
        public float[] GetFloatsSerialised()
        {
            return m_terrainData.GetFloatsSerialized();
        }

        // ITerrainChannel.GetDoubles()
        public double[,] GetDoubles()
        {
            double[,] heights = new double[Width, Height];

            int idx = 0; // index into serialized array
            for (int ii = 0; ii < Width; ii++)
            {
                for (int jj = 0; jj < Height; jj++)
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
            get {
                if (x < 0 || x >= Width || y < 0 || y >= Height)
                    return 0;
                return (double)m_terrainData[x, y];
            }
            set
            {
                if (Double.IsNaN(value) || Double.IsInfinity(value))
                    return;

                m_terrainData[x, y] = (float)value;
            }
        }

        // ITerrainChannel.GetHieghtAtXYZ(x, y, z)
        public float GetHeightAtXYZ(float x, float y, float z)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return 0;
            return m_terrainData[(int)x, (int)y];
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

        // ITerrainChannel.Merge
        public void Merge(ITerrainChannel newTerrain, Vector3 displacement, float radianRotation, Vector2 rotationDisplacement)
        {
            m_log.DebugFormat("{0} Merge. inSize=<{1},{2}>, disp={3}, rot={4}, rotDisp={5}, outSize=<{6},{7}>", LogHeader,
                                        newTerrain.Width, newTerrain.Height,
                                        displacement, radianRotation, rotationDisplacement,
                                        m_terrainData.SizeX, m_terrainData.SizeY);
            for (int xx = 0; xx < newTerrain.Width; xx++)
            {
                for (int yy = 0; yy < newTerrain.Height; yy++)
                {
                    int dispX = (int)displacement.X;
                    int dispY = (int)displacement.Y;
                    float newHeight = (float)newTerrain[xx, yy] + displacement.Z;
                    if (radianRotation == 0)
                    {
                        // If no rotation, place the new height in the specified location
                        dispX += xx;
                        dispY += yy;
                        if (dispX >= 0 && dispX < m_terrainData.SizeX && dispY >= 0 && dispY < m_terrainData.SizeY)
                        {
                            m_terrainData[dispX, dispY] = newHeight;
                        }
                    }
                    else
                    {
                        // If rotating, we have to smooth the result because the conversion
                        //    to ints will mean heightmap entries will not get changed
                        // First compute the rotation location for the new height.
                        dispX += (int)(rotationDisplacement.X
                            + ((float)xx - rotationDisplacement.X) * Math.Cos(radianRotation)
                            - ((float)yy - rotationDisplacement.Y) * Math.Sin(radianRotation) );

                        dispY += (int)(rotationDisplacement.Y
                            + ((float)xx - rotationDisplacement.X) * Math.Sin(radianRotation)
                            + ((float)yy - rotationDisplacement.Y) * Math.Cos(radianRotation) );

                        if (dispX >= 0 && dispX < m_terrainData.SizeX && dispY >= 0 && dispY < m_terrainData.SizeY)
                        {
                            float oldHeight = m_terrainData[dispX, dispY];
                            // Smooth the heights around this location if the old height is far from this one
                            for (int sxx = dispX - 2; sxx < dispX + 2; sxx++)
                            {
                                for (int syy = dispY - 2; syy < dispY + 2; syy++)
                                {
                                    if (sxx >= 0 && sxx < m_terrainData.SizeX && syy >= 0 && syy < m_terrainData.SizeY)
                                    {
                                        if (sxx == dispX && syy == dispY)
                                        {
                                            // Set height for the exact rotated point
                                            m_terrainData[dispX, dispY] = newHeight;
                                        }
                                        else
                                        {
                                            if (Math.Abs(m_terrainData[sxx, syy] - newHeight) > 1f)
                                            {
                                                // If the adjacent height is far off, force it to this height
                                                m_terrainData[sxx, syy] = newHeight;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (dispX >= 0 && dispX < m_terrainData.SizeX && dispY >= 0 && dispY < m_terrainData.SizeY)
                        {
                            m_terrainData[dispX, dispY] = (float)newTerrain[xx, yy];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A new version of terrain merge that processes the terrain in a specific order and corrects the problems with rotated terrains
        /// having 'holes' in that need to be smoothed. The correct way to rotate something is to iterate over the target, taking data from
        /// the source, not the other way around. This ensures that the target has no holes in it.
        /// The processing order of an incoming terrain is:
        /// 1. Apply rotation
        /// 2. Apply bounding rectangle
        /// 3. Apply displacement
        /// rotationCenter is no longer needed and has been discarded.
        /// </summary>
        /// <param name="newTerrain"></param>
        /// <param name="displacement">&lt;x, y, z&gt;</param>
        /// <param name="rotationDegrees"></param>
        /// <param name="boundingOrigin">&lt;x, y&gt;</param>
        /// <param name="boundingSize">&lt;x, y&gt;</param>
        public void MergeWithBounding(ITerrainChannel newTerrain, Vector3 displacement, float rotationDegrees, Vector2 boundingOrigin, Vector2 boundingSize)
        {
            m_log.DebugFormat("{0} MergeWithBounding: inSize=<{1},{2}>, rot={3}, boundingOrigin={4}, boundingSize={5}, disp={6}, outSize=<{7},{8}>",
                                LogHeader, newTerrain.Width, newTerrain.Height, rotationDegrees, boundingOrigin.ToString(),
                                boundingSize.ToString(), displacement, m_terrainData.SizeX, m_terrainData.SizeY);

            // get the size of the incoming terrain
            int baseX = newTerrain.Width;
            int baseY = newTerrain.Height;

            // create an intermediate terrain map that is 25% bigger on each side that we can work with to handle rotation
            int offsetX = baseX / 4; // the original origin will now be at these coordinates so now we can have imaginary negative coordinates ;)
            int offsetY = baseY / 4;
            int tmpX = baseX + baseX / 2;
            int tmpY = baseY + baseY / 2;
            int centreX = tmpX / 2;
            int centreY = tmpY / 2;
            TerrainData terrain_tmp = new HeightmapTerrainData(tmpX, tmpY, (int)Constants.RegionHeight);
            for (int xx = 0; xx < tmpX; xx++)
                for (int yy = 0; yy < tmpY; yy++)
                    terrain_tmp[xx, yy] = -65535f; //use this height like an 'alpha' mask channel

            double radianRotation = Math.PI * rotationDegrees / 180f;
            double cosR = Math.Cos(radianRotation);
            double sinR = Math.Sin(radianRotation);
            if (rotationDegrees < 0f) rotationDegrees += 360f; //-90=270 -180=180 -270=90

            // So first we apply the rotation to the incoming terrain, storing the result in terrain_tmp
            // We special case orthogonal rotations for accuracy because even using double precision math, Math.Cos(90 degrees) is never fully 0
            // and we can never rotate around a centre 'pixel' because the 'bitmap' size is always even

            int x, y, sx, sy;
            for (y = 0; y <= tmpY; y++)
            {
                for (x = 0; x <= tmpX; x++)
                {
                    if (rotationDegrees == 0f)
                    {
                        sx = x - offsetX;
                        sy = y - offsetY;
                    }
                    else if (rotationDegrees == 90f)
                    {
                        sx = y - offsetX;
                        sy = tmpY - 1 - x - offsetY;
                    }
                    else if (rotationDegrees == 180f)
                    {
                        sx = tmpX - 1 - x - offsetX;
                        sy = tmpY - 1 - y - offsetY;
                    }
                    else if (rotationDegrees == 270f)
                    {
                        sx = tmpX - 1 - y - offsetX;
                        sy = x - offsetY;
                    }
                    else
                    {
                        // arbitary rotation: hmmm should I be using (centreX - 0.5) and (centreY - 0.5) and round cosR and sinR to say only 5 decimal places?
                        sx = centreX + (int)Math.Round((((double)x - centreX) * cosR) + (((double)y - centreY) * sinR)) - offsetX;
                        sy = centreY + (int)Math.Round((((double)y - centreY) * cosR) - (((double)x - centreX) * sinR)) - offsetY;
                    }

                    if (sx >= 0 && sx < baseX && sy >= 0 && sy < baseY)
                    {
                        try
                        {
                            terrain_tmp[x, y] = (float)newTerrain[sx, sy];
                        }
                        catch (Exception)   //just in case we've still not taken care of every way the arrays might go out of bounds! ;)
                        {
                            m_log.DebugFormat("{0} MergeWithBounding - Rotate: Out of Bounds sx={1} sy={2} dx={3} dy={4}", sx, sy, x, y);
                        }
                    }
                }
            }

            // We could also incorporate the next steps, bounding-rectangle and displacement in the loop above, but it's simpler to visualise if done separately
            // and will also make it much easier when later I want the option for maybe a circular or oval bounding shape too ;).

            int newX = m_terrainData.SizeX;
            int newY = m_terrainData.SizeY;
            // displacement is relative to <0,0> in the destination region and defines where the origin of the data selected by the bounding-rectangle is placed
            int dispX = (int)Math.Floor(displacement.X);
            int dispY = (int)Math.Floor(displacement.Y);

            // startX/Y and endX/Y are coordinates in bitmap_tmp
            int startX = (int)Math.Floor(boundingOrigin.X) + offsetX;
            if (startX > tmpX) startX = tmpX;
            if (startX < 0) startX = 0;
            int startY = (int)Math.Floor(boundingOrigin.Y) + offsetY;
            if (startY > tmpY) startY = tmpY;
            if (startY < 0) startY = 0;

            int endX = (int)Math.Floor(boundingOrigin.X + boundingSize.X) + offsetX;
            if (endX > tmpX) endX = tmpX;
            if (endX < 0) endX = 0;
            int endY = (int)Math.Floor(boundingOrigin.Y + boundingSize.Y) + offsetY;
            if (endY > tmpY) endY = tmpY;
            if (endY < 0) endY = 0;

            //m_log.DebugFormat("{0} MergeWithBounding: inSize=<{1},{2}>, disp=<{3},{4}> rot={5}, offset=<{6},{7}>, boundingStart=<{8},{9}>, boundingEnd=<{10},{11}>, cosR={12}, sinR={13}, outSize=<{14},{15}>", LogHeader,
            //                            baseX, baseY, dispX, dispY, radianRotation, offsetX, offsetY, startX, startY, endX, endY, cosR, sinR, newX, newY);

            int dx, dy;
            for (y = startY; y < endY; y++)
            {
                for (x = startX; x < endX; x++)
                {
                    dx = x - startX + dispX;
                    dy = y - startY + dispY;
                    if (dx >= 0 && dx < newX && dy >= 0 && dy < newY)
                    {
                        try
                        {
                            float newHeight = (float)terrain_tmp[x, y]; //use 'alpha' mask
                            if (newHeight != -65535f) m_terrainData[dx, dy] = newHeight + displacement.Z;
                        }
                        catch (Exception)   //just in case we've still not taken care of every way the arrays might go out of bounds! ;)
                        {
                            m_log.DebugFormat("{0} MergeWithBounding - Bound & Displace: Out of Bounds sx={1} sy={2} dx={3} dy={4}", x, y, dx, dy);
                        }
                    }
                }
            }
        }

        #endregion

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

            m_terrainData = new HeightmapTerrainData(Height, Width, (int)Constants.RegionHeight);

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
            public float CompressionFactor;
            public float[] Map;
            public TerrainChannelXMLPackage(int pX, int pY, int pZ, float pCompressionFactor, float[] pMap)
            {
                Version = 1;
                SizeX = pX;
                SizeY = pY;
                SizeZ = pZ;
                CompressionFactor = pCompressionFactor;
                Map = pMap;
            }
        }

        // New terrain serialization format that includes the width and length.
        private void ToXml2(XmlWriter xmlWriter)
        {
            TerrainChannelXMLPackage package = new TerrainChannelXMLPackage(Width, Height, Altitude, m_terrainData.CompressionFactor,
                                            m_terrainData.GetCompressedMap());
            XmlSerializer serializer = new XmlSerializer(typeof(TerrainChannelXMLPackage));
            serializer.Serialize(xmlWriter, package);
        }

        // New terrain serialization format that includes the width and length.
        private void FromXml2(XmlReader xmlReader)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(TerrainChannelXMLPackage));
            TerrainChannelXMLPackage package = (TerrainChannelXMLPackage)serializer.Deserialize(xmlReader);
            m_terrainData = new HeightmapTerrainData(package.Map, package.CompressionFactor, package.SizeX, package.SizeY, package.SizeZ);
        }

        // Fill the heightmap with the center bump terrain
        private void PinHeadIsland()
        {
            float cx = m_terrainData.SizeX * 0.5f;
            float cy = m_terrainData.SizeY * 0.5f;
            float h;
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
 //                   h = (float)TerrainUtil.PerlinNoise2D(x, y, 2, 0.125) * 10;
                    h = 1.0f;
                    float spherFacA = (float)(TerrainUtil.SphericalFactor(x, y, cx, cy, 50) * 0.01d);
                    float spherFacB = (float)(TerrainUtil.SphericalFactor(x, y, cx, cy, 100) * 0.001d);
                    if (h < spherFacA)
                        h = spherFacA;
                    if (h < spherFacB)
                        h = spherFacB;
                    m_terrainData[x, y] = h;
                }
            }
        }

        private void FlatLand()
        {
            m_terrainData.ClearLand();
        }
    }
}
