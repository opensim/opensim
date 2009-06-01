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
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.NHibernate
{
    public class Terrain
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private double[,] map;
        private UUID regionID;

        public Terrain(UUID Region, double[,] array)
        {
            map = array;
            regionID = Region;
        }

        public Terrain()
        {
            map = new double[Constants.RegionSize, Constants.RegionSize];
            map.Initialize();
            regionID = UUID.Zero;
        }

        public UUID RegionID
        {
            get { return regionID; }
            set { regionID = value; }
        }

        public byte[] MapData
        {
            get { return serializeTerrain(map); }
            set { map = parseTerrain(value); }
        }

        public double[,] Doubles
        {
            get {return map;}
            set {map = value;}
        }

        private static double[,] parseTerrain(byte[] data)
        {
            double[,] terret = new double[Constants.RegionSize, Constants.RegionSize];
            terret.Initialize();

            MemoryStream str = new MemoryStream(data);
            BinaryReader br = new BinaryReader(str);
            try {
                for (int x = 0; x < Constants.RegionSize; x++)
                {
                    for (int y = 0; y < Constants.RegionSize; y++)
                    {
                        terret[x, y] = br.ReadDouble();
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("Issue parsing Map", e);
            }
            return terret;
        }

        private static byte[] serializeTerrain(double[,] val)
        {
            MemoryStream str = new MemoryStream((int) ((Constants.RegionSize*Constants.RegionSize)*sizeof (double)));
            BinaryWriter bw = new BinaryWriter(str);

            // TODO: COMPATIBILITY - Add byte-order conversions
            for (int x = 0; x < Constants.RegionSize; x++)
            {
                for (int y = 0; y < Constants.RegionSize; y++)
                {
                    double height = val[x, y];
                    if (height <= 0.0)
                        height = double.Epsilon;

                    bw.Write(height);
                }
            }
            return str.ToArray();
        }
    }
}
