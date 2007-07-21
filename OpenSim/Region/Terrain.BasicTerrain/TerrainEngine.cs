/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using libTerrain;
using OpenJPEGNet;

namespace OpenSim.Region.Terrain
{
    public class TerrainCommand
    {
        public virtual bool run(string[] cmdargs, ref string output)
        {
            return false;
        }

        public string args;
        public string help;
    }

    public class TerrainEngine
    {
        /// <summary>
        /// Plugin library for scripts
        /// </summary>
        public FilterHost customFilters = new FilterHost();

        /// <summary>
        /// A [normally] 256x256 heightmap
        /// </summary>
        public Channel heightmap;

        /// <summary>
        /// A copy of heightmap at the last save point (for reverting)
        /// </summary>
        public Channel revertmap;

        /// <summary>
        /// Water heightmap (needs clientside mods to work)
        /// </summary>
        public Channel watermap;

        /// <summary>
        /// Max amount the terrain can be raised from the revert parameters
        /// </summary>
        public double maxRaise = 500.0;

        /// <summary>
        /// Min amount the terrain can be lowered from the revert parameters
        /// </summary>
        public double minLower = 500.0;


        /// <summary>
        /// Whether or not the terrain has been modified since it was last saved and sent to the Physics engine.
        /// Counts the number of modifications since the last save. (0 = Untainted)
        /// </summary>
        public int tainted;

        int w, h;

        /// <summary>
        /// Generate a new TerrainEngine instance and creates a new heightmap
        /// </summary>
        public TerrainEngine()
        {
            w = 256;
            h = 256;
            heightmap = new Channel(w, h);

            tainted++;
        }

        /// <summary>
        /// Checks to make sure the terrain is within baked values +/- maxRaise/minLower
        /// </summary>
        public void CheckHeightValues()
        {
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    if ((heightmap.get(x, y) > revertmap.get(x, y) + maxRaise))
                    {
                        heightmap.map[x, y] = revertmap.get(x, y) + maxRaise;
                    }
                    if ((heightmap.get(x, y) > revertmap.get(x, y) - minLower))
                    {
                        heightmap.map[x, y] = revertmap.get(x, y) - minLower;
                    }
                }
            }
        }


        /// <summary>
        /// Converts the heightmap to a 65536 value 1D floating point array
        /// </summary>
        /// <returns>A float[65536] array containing the heightmap</returns>
        public float[] GetHeights1D()
        {
            float[] heights = new float[w * h];
            int i;

            for (i = 0; i < w * h; i++)
            {
                heights[i] = (float)heightmap.map[i / w, i % w];
            }

            return heights;
        }

        /// <summary>
        /// Converts the heightmap to a 256x256 value 2D floating point array.
        /// </summary>
        /// <returns>An array of 256,256 values containing the heightmap</returns>
        public float[,] GetHeights2D()
        {
            float[,] heights = new float[w, h];
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    heights[x, y] = (float)heightmap.map[x, y];
                }
            }
            return heights;
        }

        /// <summary>
        /// Converts the heightmap to a 256x256 value 2D floating point array. Double precision version.
        /// </summary>
        /// <returns>An array of 256,256 values containing the heightmap</returns>
        public double[,] GetHeights2DD()
        {
            return heightmap.map;
        }

        /// <summary>
        /// Imports a 1D floating point array into the 2D heightmap array
        /// </summary>
        /// <param name="heights">The array to import (must have 65536 members)</param>
        public void GetHeights1D(float[] heights)
        {
            int i;
            for (i = 0; i < w * h; i++)
            {
                heightmap.map[i / w, i % w] = heights[i];
            }

            tainted++;
        }

        /// <summary>
        /// Loads a 2D array of values into the heightmap
        /// </summary>
        /// <param name="heights">An array of 256,256 float values</param>
        public void SetHeights2D(float[,] heights)
        {
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    heightmap.set(x, y, (double)heights[x, y]);
                }
            }
            SaveRevertMap();
            tainted++;
        }

        /// <summary>
        /// Loads a 2D array of values into the heightmap (Double Precision Version)
        /// </summary>
        /// <param name="heights">An array of 256,256 float values</param>
        public void SetHeights2D(double[,] heights)
        {
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    heightmap.set(x, y, heights[x, y]);
                }
            }
            SaveRevertMap();
            tainted++;
        }

        /// <summary>
        /// Swaps the two heightmap buffers (the 'revert map' and the heightmap)
        /// </summary>
        public void SwapRevertMaps()
        {
            Channel backup = heightmap.copy();
            heightmap = revertmap;
            revertmap = backup;
        }

        /// <summary>
        /// Saves the current heightmap into the revertmap
        /// </summary>
        public void SaveRevertMap()
        {
            revertmap = heightmap.copy();
        }

        /// <summary>
        /// Processes a terrain-specific command
        /// </summary>
        /// <param name="args">Commandline arguments (space seperated)</param>
        /// <param name="resultText">Reference that returns error or help text if returning false</param>
        /// <returns>If the operation was successful (if not, the error is placed into resultText)</returns>
        public bool RunTerrainCmd(string[] args, ref string resultText, string simName)
        {
            string command = args[0];

            try
            {

                switch (command)
                {
                    case "help":
                        resultText += "terrain regenerate - rebuilds the sims terrain using a default algorithm\n";
                        resultText += "terrain hills <type> <number of hills> <min height> <max height> <island t/f> <additive t/f> <noisy t/f>\n";
                        resultText += "   type should be spheres, blocks, cones, or squared\n";
                        resultText += "terrain voronoi <points> <blocksize> - generates a worley fractal with X points per block";
                        resultText += "terrain seed <seed> - sets the random seed value to <seed>\n";
                        resultText += "terrain load <type> <filename> - loads a terrain from disk, type can be 'F32', 'F64', 'RAW' or 'IMG'\n";
                        resultText += "terrain save <type> <filename> - saves a terrain to disk, type can be 'F32', 'F64', 'PNG', 'RAW' or 'HIRAW'\n";
                        resultText += "terrain save grdmap <filename> <gradient map> - creates a PNG snapshot of the region using a named gradient map\n";
                        resultText += "terrain rescale <min> <max> - rescales a terrain to be between <min> and <max> meters high\n";
                        resultText += "terrain erode aerobic <windspeed> <pickupmin> <dropmin> <carry> <rounds> <lowest>\n";
                        resultText += "terrain erode thermal <talus> <rounds> <carry>\n";
                        resultText += "terrain multiply <val> - multiplies a terrain by <val>\n";
                        resultText += "terrain revert - reverts the terrain to the stored original\n";
                        resultText += "terrain bake - saves the current terrain into the revert map\n";
                        resultText += "terrain csfilter <filename.cs> - loads a new filter from the specified .cs file\n";
                        resultText += "terrain jsfilter <filename.js> - loads a new filter from the specified .js file\n";
                        foreach (KeyValuePair<string, ITerrainFilter> filter in customFilters.filters)
                        {
                            resultText += filter.Value.Help();
                        }

                        return false;

                    case "revert":
                        SwapRevertMaps();
                        SaveRevertMap();
                        break;

                    case "bake":
                        SaveRevertMap();
                        break;

                    case "seed":
                        SetSeed(Convert.ToInt32(args[1]));
                        break;

                    case "erode":
                        return ConsoleErosion(args, ref resultText);

                    case "voronoi":
                        double[] c = new double[2];
                        c[0] = -1;
                        c[1] = 1;
                        heightmap.voronoiDiagram(Convert.ToInt32(args[1]), Convert.ToInt32(args[2]), c);
                        break;

                    case "hills":
                        return ConsoleHills(args, ref resultText);

                    case "regenerate":
                        HillsGenerator();
                        break;

                    case "rescale":
                        SetRange(Convert.ToSingle(args[1]), Convert.ToSingle(args[2]));
                        break;

                    case "multiply":
                        heightmap *= Convert.ToDouble(args[1]);
                        tainted++;
                        break;

                    case "load":
                        args[2].Replace("%name%", simName);
                        switch (args[1].ToLower())
                        {
                            case "f32":
                                LoadFromFileF32(args[2]);
                                break;

                            case "f64":
                                LoadFromFileF64(args[2]);
                                break;

                            case "raw":
                                LoadFromFileSLRAW(args[2]);
                                break;

                            case "img":
                                heightmap.loadImage(args[2]);
                                return false;

                            default:
                                resultText = "Unknown image or data format";
                                return false;
                        }
                        break;

                    case "save":
                        args[2].Replace("%name%", simName);
                        switch (args[1].ToLower())
                        {
                            case "f32":
                                WriteToFileF32(args[2]);
                                break;

                            case "f64":
                                WriteToFileF64(args[2]);
                                break;

                            case "grdmap":
                                ExportImage(args[2], args[3]);
                                break;

                            case "png":
                                heightmap.saveImage(args[2]);
                                break;

                            case "raw":
                                WriteToFileRAW(args[2]);
                                break;

                            case "hiraw":
                                WriteToFileHiRAW(args[2]);
                                break;

                            default:
                                resultText = "Unknown image or data format";
                                return false;
                        }
                        break;

                    case "csfilter":
                        customFilters.LoadFilterCSharp(args[1]);
                        break;
                    case "jsfilter":
                        customFilters.LoadFilterJScript(args[1]);
                        break;

                    default:
                        // Run any custom registered filters
                        if (customFilters.filters.ContainsKey(command))
                        {
                            customFilters.filters[command].Filter(heightmap, args);
                            break;
                        }
                        else
                        {
                            resultText = "Unknown terrain command";
                            return false;
                        }
                }
                return true;
            }
            catch (Exception e)
            {
                resultText = "Error running terrain command: " + e.ToString();
                return false;
            }
        }

        private bool ConsoleErosion(string[] args, ref string resultText)
        {
            switch (args[1].ToLower())
            {
                case "aerobic":
                    // WindSpeed, PickupMinimum,DropMinimum,Carry,Rounds,Lowest
                    heightmap.AerobicErosion(Convert.ToDouble(args[2]), Convert.ToDouble(args[3]), Convert.ToDouble(args[4]), Convert.ToDouble(args[5]), Convert.ToInt32(args[6]), Convert.ToBoolean(args[7]));
                    break;
                case "thermal":
                    heightmap.thermalWeathering(Convert.ToDouble(args[2]), Convert.ToInt32(args[3]), Convert.ToDouble(args[4]));
                    break;
                default:
                    resultText = "Unknown erosion type";
                    return false;
            }
            return true;
        }

        private bool ConsoleHills(string[] args, ref string resultText)
        {
            Random RandomClass = new Random();
            SetSeed(RandomClass.Next());
            int count;
            double sizeMin;
            double sizeRange;
            bool island;
            bool additive;
            bool noisy;

            if (args.GetLength(0) > 2)
            {
                int.TryParse(args[2].ToString(), out count);
                double.TryParse(args[3].ToString(), out sizeMin);
                double.TryParse(args[4].ToString(), out sizeRange);
                bool.TryParse(args[5].ToString(), out island);
                bool.TryParse(args[6].ToString(), out additive);
                bool.TryParse(args[7].ToString(), out noisy);
            }
            else
            {
                count = 200;
                sizeMin = 20;
                sizeRange = 40;
                island = true;
                additive = true;
                noisy = false;
            }

            switch (args[1].ToLower())
            {
                case "blocks":
                    heightmap.hillsBlocks(count, sizeMin, sizeRange, island, additive, noisy);
                    break;
                case "cones":
                    heightmap.hillsCones(count, sizeMin, sizeRange, island, additive, noisy);
                    break;
                case "spheres":
                    heightmap.hillsSpheres(count, sizeMin, sizeRange, island, additive, noisy);
                    break;
                case "squared":
                    heightmap.hillsSquared(count, sizeMin, sizeRange, island, additive, noisy);
                    break;
                default:
                    resultText = "Unknown hills type";
                    return false;
            }
            tainted++;
            return true;
        }

        /// <summary>
        /// Renormalises the array between min and max
        /// </summary>
        /// <param name="min">Minimum value of the new array</param>
        /// <param name="max">Maximum value of the new array</param>
        public void SetRange(float min, float max)
        {
            heightmap.normalise((double)min, (double)max);
            tainted++;
        }

        /// <summary>
        /// Loads a file consisting of 256x256 doubles and imports it as an array into the map.
        /// </summary>
        /// <remarks>TODO: Move this to libTerrain itself</remarks>
        /// <param name="filename">The filename of the double array to import</param>
        public void LoadFromFileF64(string filename)
        {
            FileInfo file = new FileInfo(filename);
            FileStream s = file.Open(FileMode.Open, FileAccess.Read);
            BinaryReader bs = new BinaryReader(s);
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    heightmap.map[x, y] = bs.ReadDouble();
                }
            }

            bs.Close();
            s.Close();

            tainted++;
        }

        /// <summary>
        /// Loads a file consisting of 256x256 floats and imports it as an array into the map.
        /// </summary>
        /// <remarks>TODO: Move this to libTerrain itself</remarks>
        /// <param name="filename">The filename of the float array to import</param>
        public void LoadFromFileF32(string filename)
        {
            FileInfo file = new FileInfo(filename);
            FileStream s = file.Open(FileMode.Open, FileAccess.Read);
            BinaryReader bs = new BinaryReader(s);
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    heightmap.map[x, y] = (double)bs.ReadSingle();
                }
            }

            bs.Close();
            s.Close();

            tainted++;
        }

        /// <summary>
        /// Loads a file formatted in the SL .RAW Format used on the main grid
        /// </summary>
        /// <remarks>This file format stinks and is best avoided.</remarks>
        /// <param name="filename">A path to the .RAW format</param>
        public void LoadFromFileSLRAW(string filename)
        {
            FileInfo file = new FileInfo(filename);
            FileStream s = file.Open(FileMode.Open, FileAccess.Read);
            BinaryReader bs = new BinaryReader(s);
            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    heightmap.map[x, y] = (double)bs.ReadByte() * ((double)bs.ReadByte() / 127.0);
                    bs.ReadBytes(11); // Advance the stream to next bytes.
                }
            }

            bs.Close();
            s.Close();

            tainted++;
        }

        /// <summary>
        /// Writes the current terrain heightmap to disk, in the format of a 65536 entry double[] array.
        /// </summary>
        /// <param name="filename">The desired output filename</param>
        public void WriteToFileF64(string filename)
        {
            FileInfo file = new FileInfo(filename);
            FileStream s = file.Open(FileMode.CreateNew, FileAccess.Write);
            BinaryWriter bs = new BinaryWriter(s);

            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    bs.Write(heightmap.get(x, y));
                }
            }

            bs.Close();
            s.Close();
        }

        /// <summary>
        /// Writes the current terrain heightmap to disk, in the format of a 65536 entry float[] array
        /// </summary>
        /// <param name="filename">The desired output filename</param>
        public void WriteToFileF32(string filename)
        {
            FileInfo file = new FileInfo(filename);
            FileStream s = file.Open(FileMode.CreateNew, FileAccess.Write);
            BinaryWriter bs = new BinaryWriter(s);

            int x, y;
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    bs.Write((float)heightmap.get(x, y));
                }
            }

            bs.Close();
            s.Close();
        }

        /// <summary>
        /// A very fast LL-RAW file output mechanism - lower precision mechanism but wont take 5 minutes to run either.
        /// (is also editable in an image application)
        /// </summary>
        /// <param name="filename">Filename to write to</param>
        public void WriteToFileRAW(string filename)
        {
            FileInfo file = new FileInfo(filename);
            FileStream s = file.Open(FileMode.CreateNew, FileAccess.Write);
            BinaryWriter binStream = new BinaryWriter(s);

            int x, y;

            // Used for the 'green' channel.
            byte avgMultiplier = (byte)heightmap.avg();
            byte backupMultiplier = (byte)revertmap.avg();

            // Limit the multiplier so it can represent points >64m.
            if (avgMultiplier > 196)
                avgMultiplier = 196;
            if(backupMultiplier > 196) 
                backupMultiplier = 196;
            // Make sure it's at least one to prevent a div by zero
            if (avgMultiplier < 1)
                avgMultiplier = 1;
            if(backupMultiplier < 1)
                backupMultiplier = 1;

            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    byte red = (byte)(heightmap.get(x, y) / ((double)avgMultiplier / 128.0));
                    byte green = avgMultiplier;
                    byte blue = (byte)watermap.get(x, y);
                    byte alpha1 = 0; // Land Parcels
                    byte alpha2 = 0; // For Sale Land
                    byte alpha3 = 0; // Public Edit Object
                    byte alpha4 = 0; // Public Edit Land
                    byte alpha5 = 255; // Safe Land
                    byte alpha6 = 255; // Flying Allowed
                    byte alpha7 = 255; // Create Landmark
                    byte alpha8 = 255; // Outside Scripts
                    byte alpha9 = (byte)(revertmap.get(x, y) / ((double)backupMultiplier / 128.0));
                    byte alpha10 = backupMultiplier;

                    binStream.Write(red);
                    binStream.Write(green);
                    binStream.Write(blue);
                    binStream.Write(alpha1);
                    binStream.Write(alpha2);
                    binStream.Write(alpha3);
                    binStream.Write(alpha4);
                    binStream.Write(alpha5);
                    binStream.Write(alpha6);
                    binStream.Write(alpha7);
                    binStream.Write(alpha8);
                    binStream.Write(alpha9);
                    binStream.Write(alpha10);
                }
            }
            binStream.Close();
            s.Close();
        }

        /// <summary>
        /// Outputs to a LL compatible RAW in the most efficient manner possible
        /// </summary>
        /// <remarks>Does not calculate the revert map</remarks>
        /// <param name="filename">The filename to output to</param>
        public void WriteToFileHiRAW(string filename)
        {
            FileInfo file = new FileInfo(filename);
            FileStream s = file.Open(FileMode.CreateNew, FileAccess.Write);
            BinaryWriter binStream = new BinaryWriter(s);

            // Generate a smegging big lookup table to speed the operation up (it needs it)
            double[] lookupHeightTable = new double[65536];
            int i, j, x, y;
            for (i = 0; i < 256; i++)
            {
                for (j = 0; j < 256; j++)
                {
                    lookupHeightTable[i + (j * 256)] = ((double)i * ((double)j / 127.0));
                }
            }

            // Output the calculated raw
            for (x = 0; x < w; x++)
            {
                for (y = 0; y < h; y++)
                {
                    double t = heightmap.get(x, y);
                    double min = double.MaxValue;
                    int index = 0;

                    for (i = 0; i < 65536; i++)
                    {
                        if (Math.Abs(t - lookupHeightTable[i]) < min)
                        {
                            min = Math.Abs(t - lookupHeightTable[i]);
                            index = i;
                        }
                    }

                    byte red = (byte)(index & 0xFF);
                    byte green = (byte)((index >> 8) & 0xFF);
                    byte blue = (byte)watermap.get(x, y);
                    byte alpha1 = 0; // Land Parcels
                    byte alpha2 = 0; // For Sale Land
                    byte alpha3 = 0; // Public Edit Object
                    byte alpha4 = 0; // Public Edit Land
                    byte alpha5 = 255; // Safe Land
                    byte alpha6 = 255; // Flying Allowed
                    byte alpha7 = 255; // Create Landmark
                    byte alpha8 = 255; // Outside Scripts
                    byte alpha9 = red;
                    byte alpha10 = green;

                    binStream.Write(red);
                    binStream.Write(green);
                    binStream.Write(blue);
                    binStream.Write(alpha1);
                    binStream.Write(alpha2);
                    binStream.Write(alpha3);
                    binStream.Write(alpha4);
                    binStream.Write(alpha5);
                    binStream.Write(alpha6);
                    binStream.Write(alpha7);
                    binStream.Write(alpha8);
                    binStream.Write(alpha9);
                    binStream.Write(alpha10);
                }
            }

            binStream.Close();
            s.Close();
        }

        /// <summary>
        /// Sets the random seed to be used by procedural functions which involve random numbers.
        /// </summary>
        /// <param name="val">The desired seed</param>
        public void SetSeed(int val)
        {
            heightmap.seed = val;
        }

        /// <summary>
        /// Raises land in a sphere around the specified coordinates
        /// </summary>
        /// <param name="rx">Center of the sphere on the X axis</param>
        /// <param name="ry">Center of the sphere on the Y axis</param>
        /// <param name="size">The radius of the sphere</param>
        /// <param name="amount">Scale the height of the sphere by this amount (recommended 0..2)</param>
        public void RaiseTerrain(double rx, double ry, double size, double amount)
        {
            lock (heightmap)
            {
                heightmap.raise(rx, ry, size, amount);
            }

            tainted++;
        }

        /// <summary>
        /// Lowers the land in a sphere around the specified coordinates
        /// </summary>
        /// <param name="rx">The center of the sphere at the X axis</param>
        /// <param name="ry">The center of the sphere at the Y axis</param>
        /// <param name="size">The radius of the sphere in meters</param>
        /// <param name="amount">Scale the height of the sphere by this amount (recommended 0..2)</param>
        public void LowerTerrain(double rx, double ry, double size, double amount)
        {
            lock (heightmap)
            {
                heightmap.lower(rx, ry, size, amount);
            }

            tainted++;
        }

        /// <summary>
        /// Flattens the land under the brush of specified coordinates (spherical mask)
        /// </summary>
        /// <param name="rx">Center of sphere</param>
        /// <param name="ry">Center of sphere</param>
        /// <param name="size">Radius of the sphere</param>
        /// <param name="amount">Thickness of the mask (0..2 recommended)</param>
        public void FlattenTerrain(double rx, double ry, double size, double amount)
        {
            lock (heightmap)
            {
                heightmap.flatten(rx, ry, size, amount);
            }

            tainted++;
        }

        /// <summary>
        /// Creates noise within the specified bounds
        /// </summary>
        /// <param name="rx">Center of the bounding sphere</param>
        /// <param name="ry">Center of the bounding sphere</param>
        /// <param name="size">The radius of the sphere</param>
        /// <param name="amount">Strength of the mask (0..2) recommended</param>
        public void NoiseTerrain(double rx, double ry, double size, double amount)
        {
            lock (heightmap)
            {
                Channel smoothed = new Channel();
                smoothed.noise();

                Channel mask = new Channel();
                mask.raise(rx, ry, size, amount);

                heightmap.blend(smoothed, mask);
            }

            tainted++;
        }

        /// <summary>
        /// Reverts land within the specified bounds
        /// </summary>
        /// <param name="rx">Center of the bounding sphere</param>
        /// <param name="ry">Center of the bounding sphere</param>
        /// <param name="size">The radius of the sphere</param>
        /// <param name="amount">Strength of the mask (0..2) recommended</param>
        public void RevertTerrain(double rx, double ry, double size, double amount)
        {
            lock (heightmap)
            {
                Channel mask = new Channel();
                mask.raise(rx, ry, size, amount);

                heightmap.blend(revertmap, mask);
            }

            tainted++;
        }

        /// <summary>
        /// Smooths land under the brush of specified coordinates (spherical mask)
        /// </summary>
        /// <param name="rx">Center of the sphere</param>
        /// <param name="ry">Center of the sphere</param>
        /// <param name="size">Radius of the sphere</param>
        /// <param name="amount">Thickness of the mask (0..2 recommended)</param>
        public void SmoothTerrain(double rx, double ry, double size, double amount)
        {
            lock (heightmap)
            {
                Channel smoothed = heightmap.copy();
                smoothed.smooth(amount);

                Channel mask = new Channel();
                mask.raise(rx,ry,size,amount);

                heightmap.blend(smoothed, mask);
            }

            tainted++;
        }

        /// <summary>
        /// Generates a simple set of hills in the shape of an island
        /// </summary>
        public void HillsGenerator()
        {
            lock (heightmap)
            {
                heightmap.hillsSpheres(200, 20, 40, true, true, false);
                heightmap.normalise();
                heightmap *= 60.0; // Raise to 60m
            }

            tainted++;
        }

        /// <summary>
        /// Wrapper to heightmap.get()
        /// </summary>
        /// <param name="x">X coord</param>
        /// <param name="y">Y coord</param>
        /// <returns>Height at specified coordinates</returns>
        public double GetHeight(int x, int y)
        {
            return heightmap.get(x, y);
        }

        /// <summary>
        /// Multiplies the heightfield by val
        /// </summary>
        /// <param name="meep">The heightfield</param>
        /// <param name="val">The multiplier</param>
        /// <returns></returns>
        public static TerrainEngine operator *(TerrainEngine terrain, Double val)
        {
            terrain.heightmap *= val;
            terrain.tainted++;
            return terrain;
        }

        /// <summary>
        /// Exports the current heightmap to a PNG file
        /// </summary>
        /// <param name="filename">The destination filename for the image</param>
        /// <param name="gradientmap">A 1x*height* image which contains the colour gradient to export with. Must be at least 1x2 pixels, 1x256 or more is ideal.</param>
        public void ExportImage(string filename, string gradientmap)
        {
            try
            {
                Bitmap gradientmapLd = new Bitmap(gradientmap);

                int pallete = gradientmapLd.Height;

                Bitmap bmp = new Bitmap(heightmap.w, heightmap.h);
                Color[] colours = new Color[pallete];

                for (int i = 0; i < pallete; i++)
                {
                    colours[i] = gradientmapLd.GetPixel(0, i);
                }

                Channel copy = heightmap.copy();
                for (int x = 0; x < copy.w; x++)
                {
                    for (int y = 0; y < copy.h; y++)
                    {
                        // 512 is the largest possible height before colours clamp
                        int colorindex = (int)(Math.Max(Math.Min(1.0, copy.get(x, y) / 512.0), 0.0) * (pallete - 1));
                        bmp.SetPixel(x, y, colours[colorindex]);
                    }
                }

                bmp.Save(filename, ImageFormat.Png);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed generating terrain map: " + e.ToString());
            }
        }

        /// <summary>
        /// Exports the current heightmap in Jpeg2000 format to a byte[]
        /// </summary>
        /// <param name="gradientmap">A 1x*height* image which contains the colour gradient to export with. Must be at least 1x2 pixels, 1x256 or more is ideal.</param>
        public byte[] ExportJpegImage(string gradientmap)
        {
            byte[] imageData = null;
            try
            {
                Bitmap gradientmapLd = new Bitmap(gradientmap);

                int pallete = gradientmapLd.Height;

                Bitmap bmp = new Bitmap(heightmap.w, heightmap.h);
                Color[] colours = new Color[pallete];

                for (int i = 0; i < pallete; i++)
                {
                    colours[i] = gradientmapLd.GetPixel(0, i);
                }

                Channel copy = heightmap.copy();
                for (int x = 0; x < copy.w; x++)
                {
                    for (int y = 0; y < copy.h; y++)
                    {
                        // 512 is the largest possible height before colours clamp
                        int colorindex = (int)(Math.Max(Math.Min(1.0, copy.get(copy.h - y,  x) / 512.0), 0.0) * pallete);
                        bmp.SetPixel(x, y, colours[colorindex]);
                    }
                }

                //bmp.Save(filename, System.Drawing.Imaging.ImageFormat.Png);
                imageData = OpenJPEG.EncodeFromImage(bmp, true );
                
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed generating terrain map: " + e.ToString());
            }

            return imageData;
        }
    }
}
