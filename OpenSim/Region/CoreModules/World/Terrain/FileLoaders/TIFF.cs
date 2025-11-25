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
using System.Drawing;
using System.IO;
using System.Linq;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Terrain.FileLoaders
{
    internal class TIFF : GenericSystemDrawing
    {
        public override int SupportedHeight
        {
            get { return 4096; }
        }
        public override string ToString()
        {
            return "TIFF";
        }

        //Returns true if this extension is supported for terrain save-tile
        public override bool SupportsTileSave()
        {
            return true;
        }

        public override bool SupportsExtendedTileSave()
        {
            return true;
        }

        public override ITerrainChannel LoadFile(string filename)
        {
            return LoadTIFF(filename, out int _, null, null);
        }

        public override ITerrainChannel LoadFile(string filename, int x, int y, int fileWidth, int fileHeight, int w, int h)
        {
            return LoadTIFF(filename, out int _, null, [x, y, fileWidth, fileHeight, w, h]);
        }

        public override ITerrainChannel LoadStream(Stream stream)
        {
            return LoadTIFF(string.Empty, out int _, stream, null);
        }

        public override void SaveFile(string filename, ITerrainChannel map)
        {
            // From now saving in 32 bit format only
            FileStream fs = new(filename, FileMode.Create);
            CreateFile32Bit(fs, map);
        }

        // Saves existing terrain to larger or smaller file with meter based coordinates and offsets
        public override void SaveFile(ITerrainChannel map, string filename, int fileWidth, int fileHeight, int startX, int startY, int stopX, int stopY, int offsetX, int offsetY)
        {
            if (map is null)
            {
                Console.WriteLine("Existing terrain not found");
                return;
            }

            if (startX >= map.Width || stopX >= map.Width || startY >= map.Height || stopY >= map.Height)
            {
                Console.WriteLine("Map coordinates outside of region");
                return;
            }

            if (offsetX >= fileWidth || offsetY >= fileHeight || startX + offsetX >= fileWidth || stopX + offsetX >= fileWidth || startY + offsetY >= fileHeight || stopY + offsetY >= fileHeight)
            {
                Console.WriteLine("Save target coordinates outside of file");
                return;
            }

            string tempName = null;
            int bitsPerPixel = 16;
            ITerrainChannel existingBitmap = null;

            if (File.Exists(filename))
            {
                try
                {
                    tempName = Path.GetTempFileName();
                    File.Copy(filename, tempName, true);
                    Console.WriteLine("Using existing file " + tempName);
                    ITerrainChannel loaded = LoadTIFF(tempName, out bitsPerPixel, null, null);

                    if (loaded != null)
                    {
                        existingBitmap = loaded;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unable to copy temp file " + e.ToString());
                    return;
                }
            }

            if (existingBitmap is null || !File.Exists(filename))
            {
                Console.WriteLine("Generating new image");
                existingBitmap = new TerrainChannel(fileWidth, fileHeight);
            }

            if ((bitsPerPixel.Equals(8) || bitsPerPixel.Equals(16)) && existingBitmap is not null)
            {
                ITerrainChannel cutmap = new TerrainChannel(stopX - startX, stopY - startY);
                // First cut map to size
                for (int x = 0; x < cutmap.Width; x++)
                {
                    for (int y = 0; y < cutmap.Height; y++)
                    {
                        cutmap[x, y] = map[startX + x, startY + y];
                    }
                }

                // Now insert it at the requested spot
                for (int x = 0; x < existingBitmap.Width; x++)
                {
                    for (int y = 0; y < existingBitmap.Height; y++)
                    {
                        if (x >= offsetX || y >= offsetY)
                        {
                            existingBitmap[x, y] = cutmap[(x - offsetX), (y - offsetY)];
                        }
                    }
                }

                Console.WriteLine(existingBitmap.Width + " " + existingBitmap.Height);

                if (bitsPerPixel.Equals(32))
                {
                    FileStream fs = new(filename, FileMode.Create);
                    CreateFile32Bit(fs, existingBitmap);
                }
                else if (bitsPerPixel.Equals(16))
                {
                    FileStream fs = new(filename, FileMode.Create);
                    CreateFile16Bit(fs, existingBitmap);
                }
                else if (bitsPerPixel.Equals(8))
                {
                    CreateFile8Bit(filename, existingBitmap);
                }

                if (tempName is not null)
                {
                    if (File.Exists(tempName))
                    {
                        try
                        {
                            Console.WriteLine("Removing " + tempName);
                            File.Delete(tempName);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Could not remove temp file " + e.ToString());
                            return;
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Unrecognized color bit depth!");
                return;
            }

        }

        public override void SaveFile(ITerrainChannel map, string filename, int offsetX, int offsetY, int fileWidth, int fileHeight, int regionSizeX, int regionSizeY)
        {
            // We need to do this because:
            // "Saving the image to the same file it was constructed from is not allowed and throws an exception."
            string tempName = null;

            ITerrainChannel existingBitmap = null;

            // Assume 16 bit from now on
            int bitsPerPixel = 16;

            // Load existing file or create a new heightmap
            if (File.Exists(filename))
            {
                try
                {
                    tempName = Path.GetTempFileName();
                    File.Copy(filename, tempName, true);
                    Console.WriteLine("Using existing file " + tempName);
                    ITerrainChannel loaded = LoadTIFF(tempName, out bitsPerPixel, null, null);

                    if (loaded != null)
                    {
                        if (loaded.Width.Equals(fileWidth * regionSizeX) && loaded.Height.Equals(fileHeight * regionSizeY))
                        {
                            existingBitmap = loaded;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unable to copy temp file " + e.ToString());
                    return;
                }
            }

            if (existingBitmap is null || !File.Exists(filename))
            {
                Console.WriteLine("Generating new image");
                existingBitmap = new TerrainChannel(fileWidth * regionSizeX, fileHeight * regionSizeY);
            }

            if ((bitsPerPixel.Equals(8) || bitsPerPixel.Equals(16)) && existingBitmap is not null)
            {
                // Go over the whole thing and if the selected offsets match place the data there
                for (int x = 0; x < existingBitmap.Width; x++)
                {
                    for (int y = 0; y < existingBitmap.Height; y++)
                    {
                        if (x >= (offsetX * regionSizeX) && x <= ((offsetX * regionSizeX) + map.Width)
                         && y >= (offsetY * regionSizeY) && y <= ((offsetY * regionSizeY) + map.Height))
                        {
                            existingBitmap[x, y] = map[x - (offsetX * regionSizeX), y - (offsetY * regionSizeY)];
                        }
                    }
                }

                Console.WriteLine(existingBitmap.Width + " " + existingBitmap.Height);

                if (bitsPerPixel.Equals(32))
                {
                    FileStream fs = new(filename, FileMode.Create);
                    CreateFile32Bit(fs, existingBitmap);
                }
                else if (bitsPerPixel.Equals(16))
                {
                    FileStream fs = new(filename, FileMode.Create);
                    CreateFile16Bit(fs, existingBitmap);
                }
                else if (bitsPerPixel.Equals(8))
                {
                    CreateFile8Bit(filename, existingBitmap);
                }

                if (tempName is not null)
                {
                    if (File.Exists(tempName))
                    {
                        try
                        {
                            Console.WriteLine("Removing " + tempName);
                            File.Delete(tempName);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Could not remove temp file " + e.ToString());
                            return;
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Unrecognized color bit depth!");
                return;
            }
        }


        /// <summary>
        /// Exports a stream to 32 bit tiff
        /// </summary>
        /// <param name="stream">The target stream</param>
        /// <param name="map">The terrain channel being saved</param>
        public override void SaveStream(Stream stream, ITerrainChannel map)
        {
            // Switched to 32 bit from now on
            CreateFile32Bit(stream, map);
        }

        protected static ITerrainChannel LoadTIFF(string filename, out int bitsPerPixel, Stream stream = null, int[] options = null)
        {
            bitsPerPixel = 0;
            if (stream == null)
            {
                FileStream fs = new(filename, FileMode.Open, FileAccess.Read);
                bitsPerPixel = GetColorDepth(fs);
                //int bitsPerPixel = Image.GetPixelFormatSize(file.PixelFormat);
                if (!bitsPerPixel.Equals(0))
                {
                    if (bitsPerPixel.Equals(8))
                    {
                        if (options is not null)
                            return Load8BitBitmapTile(filename, options[0], options[1], options[2], options[3], options[4], options[5]);
                        return LoadFrom8BitStream(fs);
                    }
                    else if (bitsPerPixel.Equals(16))
                    {
                        if (options is not null)
                            return Load16BitBitmapTile(filename, options[0], options[1], options[2], options[3], options[4], options[5]);
                        return LoadFrom16BitStream(fs);
                    }
                    else if (bitsPerPixel.Equals(32))
                    {
                        if (options is not null)
                            return Load32BitBitmapTile(filename, options[0], options[1], options[2], options[3], options[4], options[5]);
                        return LoadFrom32BitStream(fs);
                    }
                }
            }
            else
            {
                bitsPerPixel = GetColorDepth(stream);
                //int bitsPerPixel = Image.GetPixelFormatSize(file.PixelFormat);
                if (!bitsPerPixel.Equals(0))
                {
                    if (bitsPerPixel.Equals(8))
                        return LoadFrom8BitStream(stream);
                    else if (bitsPerPixel.Equals(16))
                        return LoadFrom16BitStream(stream);
                    else if (bitsPerPixel.Equals(32))
                        return LoadFrom32BitStream(stream);
                }
            }
            Console.WriteLine("Unrecognized color bit depth!");
            return null;
        }

        protected static void CreateFile8Bit(string filename, ITerrainChannel map)
        {
            Bitmap newbitmap = CreateGrayscaleBitmapFromMap(map);
            newbitmap.Save(filename);
        }

        protected static void CreateFile16Bit(Stream fs, ITerrainChannel map)
        {
            int width = map.Width;
            int height = map.Height;

            ushort bitsPerSample = 16;

            // TIFF Header
            fs.Write(BitConverter.GetBytes((ushort)0x4949), 0, 2); // Byte order: Little endian (II)
            fs.Write(BitConverter.GetBytes((ushort)42), 0, 2); // TIFF magic number
            fs.Write(BitConverter.GetBytes((uint)8), 0, 4); // Offset to first IFD (starts right after header)

            // IFD (Image File Directory)
            fs.Write(BitConverter.GetBytes((ushort)8), 0, 2); // Number of directory entries

            long entriesStartOffset = fs.Position;
            fs.Seek(8 * 12 + 4, SeekOrigin.Current); // Skip space for entries and next IFD offset

            // Image Width tag
            fs.Seek(entriesStartOffset, SeekOrigin.Begin);
            WriteIfdEntry(fs, 256, 4, 1, (uint)width);

            // Image Length tag
            fs.Seek(entriesStartOffset + 12, SeekOrigin.Begin);
            WriteIfdEntry(fs, 257, 4, 1, (uint)height);

            // Bits per Sample tag
            fs.Seek(entriesStartOffset + 24, SeekOrigin.Begin);
            WriteIfdEntry(fs, 258, 3, 1, bitsPerSample);

            // Compression tag
            fs.Seek(entriesStartOffset + 36, SeekOrigin.Begin);
            WriteIfdEntry(fs, 259, 3, 1, 1); // No compression (1)

            // Photometric Interpretation tag
            fs.Seek(entriesStartOffset + 48, SeekOrigin.Begin);
            WriteIfdEntry(fs, 262, 3, 1, 1); // Black is zero

            // Strip Offsets tag
            fs.Seek(entriesStartOffset + 60, SeekOrigin.Begin);
            long stripOffsetsPos = fs.Position;
            WriteIfdEntry(fs, 273, 4, 1, 0); // Placeholder for strip offsets

            // Rows per Strip tag
            fs.Seek(entriesStartOffset + 72, SeekOrigin.Begin);
            WriteIfdEntry(fs, 278, 4, 1, (uint)height);

            // Strip Byte Counts tag
            fs.Seek(entriesStartOffset + 84, SeekOrigin.Begin);
            long stripByteCountsPos = fs.Position;
            WriteIfdEntry(fs, 279, 4, 1, 0); // Placeholder for strip byte counts

            // Next IFD offset
            fs.Seek(entriesStartOffset + 96, SeekOrigin.Begin);
            fs.Write(BitConverter.GetBytes((uint)0), 0, 4); // No next IFD

            // Write Image Data
            long imageDataOffset = fs.Position;
            fs.Seek(stripOffsetsPos, SeekOrigin.Begin);
            fs.Write(BitConverter.GetBytes((uint)imageDataOffset), 0, 4);
            fs.Seek(stripByteCountsPos, SeekOrigin.Begin);
            fs.Write(BitConverter.GetBytes((uint)(width * height * (bitsPerSample / 8))), 0, 4);

            fs.Seek(imageDataOffset, SeekOrigin.Begin);

            // Write grayscale image data (16-bit per pixel)
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float terrainheight = map[x, y] + 256;
                    ushort pixelValue = (ushort)((int)Normalize(terrainheight));
                    fs.Write(BitConverter.GetBytes(pixelValue), 0, 2);
                }
            }
            fs.Dispose();
        }

        /*protected static void CreateFile16Bit(Stream fs, ITerrainChannel map)
        {
            int width = map.Width;
            int height = map.Height;

            ushort bitsPerSample = 16;

            BinaryWriter writer = new(fs);

            // TIFF Header
            writer.Write((ushort)0x4949); // Byte order: Little endian (II)
            writer.Write((ushort)42); // TIFF magic number
            writer.Write((uint)8); // Offset to first IFD (starts right after header)

            // IFD (Image File Directory)
            writer.Write((ushort)8); // Number of directory entries

            long entriesStartOffset = fs.Position;
            fs.Seek(8 * 12 + 4, SeekOrigin.Current); // Skip space for entries and next IFD offset

            // Image Width tag
            writer.Seek((int)entriesStartOffset, SeekOrigin.Begin);
            writer.Write((ushort)256); // Tag ID
            writer.Write((ushort)4); // Type: LONG
            writer.Write((uint)1); // Count
            writer.Write((uint)width); // Value

            // Image Length tag
            writer.Seek((int)entriesStartOffset + 12, SeekOrigin.Begin);
            writer.Write((ushort)257); // Tag ID
            writer.Write((ushort)4); // Type: LONG
            writer.Write((uint)1); // Count
            writer.Write((uint)height); // Value

            // Bits per Sample tag
            writer.Seek((int)entriesStartOffset + 24, SeekOrigin.Begin);
            writer.Write((ushort)258); // Tag ID
            writer.Write((ushort)3); // Type: SHORT
            writer.Write((uint)1); // Count
            writer.Write((uint)bitsPerSample); // Value

            // Compression tag
            writer.Seek((int)entriesStartOffset + 36, SeekOrigin.Begin);
            writer.Write((ushort)259); // Tag ID
            writer.Write((ushort)3); // Type: SHORT
            writer.Write((uint)1); // Count
            writer.Write((ushort)1); // No compression (1)

            // Photometric Interpretation tag
            writer.Seek((int)entriesStartOffset + 48, SeekOrigin.Begin);
            writer.Write((ushort)262); // Tag ID
            writer.Write((ushort)3); // Type: SHORT
            writer.Write((uint)1); // Count
            writer.Write((ushort)1); // Black is zero

            // Strip Offsets tag
            writer.Seek((int)entriesStartOffset + 60, SeekOrigin.Begin);
            writer.Write((ushort)273); // Tag ID
            writer.Write((ushort)4); // Type: LONG
            writer.Write((uint)1); // Count
            long stripOffsetsPos = fs.Position;
            writer.Write((uint)0); // Placeholder for strip offsets

            // Rows per Strip tag
            writer.Seek((int)entriesStartOffset + 72, SeekOrigin.Begin);
            writer.Write((ushort)278); // Tag ID
            writer.Write((ushort)4); // Type: LONG
            writer.Write((uint)1); // Count
            writer.Write((uint)height); // Value

            // Strip Byte Counts tag
            writer.Seek((int)entriesStartOffset + 84, SeekOrigin.Begin);
            writer.Write((ushort)279); // Tag ID
            writer.Write((ushort)4); // Type: LONG
            writer.Write((uint)1); // Count
            long stripByteCountsPos = fs.Position;
            writer.Write((uint)0); // Placeholder for strip byte counts

            // Next IFD offset
            writer.Seek((int)entriesStartOffset + 96, SeekOrigin.Begin);
            writer.Write((uint)0); // No next IFD

            // Write Image Data
            long imageDataOffset = fs.Position;
            writer.Seek((int)stripOffsetsPos, SeekOrigin.Begin);
            writer.Write((uint)imageDataOffset);
            writer.Seek((int)stripByteCountsPos, SeekOrigin.Begin);
            writer.Write((uint)(width * height * (bitsPerSample / 8)));

            writer.Seek((int)imageDataOffset, SeekOrigin.Begin);

            // Write grayscale image data (16-bit per pixel)
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float terrainheight = map[x, y] + 256;
                    ushort pixelValue = (ushort)((int)Normalize(terrainheight));
                    writer.Write(pixelValue);
                }
            }
            fs.Dispose();
        }*/

        protected static void CreateFile32Bit(Stream fs, ITerrainChannel map)
        {
            Console.WriteLine("Saving 32 bit...");

            // Prepare TIFF header
            byte[] tiffHeader = new byte[8] { 0x49, 0x49, 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00 };
            fs.Write(tiffHeader, 0, 8);

            // Number of IFD entries
            ushort numEntries = 13;
            fs.Write(BitConverter.GetBytes(numEntries), 0, 2);

            // Calculate offsets
            uint resolutionOffset = (uint)(8 + 2 + numEntries * 12 + 4);
            uint stripOffset = resolutionOffset + 16; // 16 bytes for XResolution and YResolution
            uint stripByteCount = (uint)(map.Width * map.Height * 4);

            // Write IFD entries
            WriteIfdEntry(fs, 0x0100, 4, 1, (uint)map.Width);                // ImageWidth
            WriteIfdEntry(fs, 0x0101, 4, 1, (uint)map.Height);               // ImageLength
            WriteIfdEntry(fs, 0x0102, 3, 1, 32);                             // BitsPerSample (32 bits per sample)
            WriteIfdEntry(fs, 0x0103, 3, 1, 1);                              // Compression (1 = No compression)
            WriteIfdEntry(fs, 0x0106, 3, 1, 1);                              // Photometric Interpretation (1 = BlackIsZero)
            WriteIfdEntry(fs, 0x0111, 4, 1, stripOffset);                    // StripOffsets
            WriteIfdEntry(fs, 0x0115, 3, 1, 1);                              // SamplesPerPixel (1 = grayscale)
            WriteIfdEntry(fs, 0x0116, 4, 1, (uint)map.Height);               // RowsPerStrip (same as image height)
            WriteIfdEntry(fs, 0x0117, 4, 1, stripByteCount);                 // StripByteCounts
            WriteIfdEntry(fs, 0x011A, 5, 1, resolutionOffset);               // XResolution (offset to resolution data)
            WriteIfdEntry(fs, 0x011B, 5, 1, resolutionOffset + 8);           // YResolution (offset to resolution data)
            WriteIfdEntry(fs, 0x0128, 3, 1, 2);                              // ResolutionUnit (2 = inch)
            WriteIfdEntry(fs, 0x0153, 3, 1, 3);                              // SampleFormat (3 = Floating Point)

            // Offset to next IFD (0 = no next IFD)
            fs.Write(BitConverter.GetBytes(0u), 0, 4);

            // Write resolution data (XResolution and YResolution)
            WriteRational(fs, (uint)map.Width, 1);
            WriteRational(fs, (uint)map.Height, 1);

            // Write pixel data
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    float terrainheight = map[x, y] + 256.0f;
                    float pixelValue = Normalize(terrainheight);
                    //Console.Write(pixelValue.ToString() + " ");
                    byte[] pixelBytes = BitConverter.GetBytes(pixelValue);
                    fs.Write(pixelBytes, 0, pixelBytes.Length);
                }
            }
            fs.Dispose();
        }

        protected static ITerrainChannel LoadFrom8BitStream(Stream stream)
        {
            Bitmap bitmap = new(stream);
            ITerrainChannel retval = new TerrainChannel(bitmap.Width, bitmap.Height);

            for (int x = 0; x < bitmap.Width; x++)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    retval[x, y] = bitmap.GetPixel(x, bitmap.Height - y - 1).GetBrightness() * 128;
                }
            }
            return retval;
        }

        protected static ITerrainChannel LoadFrom16BitStream(Stream stream)
        {
            BinaryReader reader = new(stream);

            // TIFF Header
            ushort byteOrder = reader.ReadUInt16();
            if (byteOrder != 0x4949) // Only handle little-endian (II)
                throw new Exception("Unsupported byte order");

            ushort magicNumber = reader.ReadUInt16();
            if (magicNumber != 42)
                throw new Exception("Invalid TIFF file");

            uint ifdOffset = reader.ReadUInt32();
            stream.Seek(ifdOffset, SeekOrigin.Begin);

            // IFD (Image File Directory)
            ushort numberOfEntries = reader.ReadUInt16();

            Dictionary<ushort, (ushort Type, uint Count, uint ValueOffset)> ifdEntries = [];

            for (int i = 0; i < numberOfEntries; i++)
            {
                ushort tagID = reader.ReadUInt16();
                ushort type = reader.ReadUInt16();
                uint count = reader.ReadUInt32();
                uint valueOffset = reader.ReadUInt32();
                ifdEntries[tagID] = (type, count, valueOffset);
            }

            uint width = (ifdEntries.TryGetValue(256, out (ushort Type, uint Count, uint ValueOffset) out1)) ? out1.ValueOffset : 0;
            uint height = (ifdEntries.TryGetValue(257, out (ushort Type, uint Count, uint ValueOffset) out2)) ? out2.ValueOffset : 0;
            uint bitsPerSample = (ifdEntries.TryGetValue(258, out (ushort Type, uint Count, uint ValueOffset) out3)) ? out3.ValueOffset : 0;
            uint stripOffsets = (ifdEntries.TryGetValue(273, out (ushort Type, uint Count, uint ValueOffset) out4)) ? out4.ValueOffset : 0;
            uint stripByteCounts = (ifdEntries.TryGetValue(279, out (ushort Type, uint Count, uint ValueOffset) out5)) ? out5.ValueOffset : 0;

            if (width == 0 || height == 0 || bitsPerSample != 16 || stripOffsets == 0 || stripByteCounts == 0)
                throw new Exception("Invalid TIFF metadata");

            // Calculate the size of the image data
            long imageDataSize = width * height * (bitsPerSample / 8);

            // Seek from the end of the file to the start of the image data
            stream.Seek(-imageDataSize, SeekOrigin.End);

            Console.WriteLine("Image " + width.ToString() + " " + height.ToString());

            ITerrainChannel map = new TerrainChannel((int)width, (int)height);

            // Convert heightmap values back to the original range
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float heightvalue = InverseNormalize(reader.ReadUInt16()) - 255;
                    //Console.Write(heightvalue.ToString() + " ");
                    map[x, y] = heightvalue;
                }
            }
            return map;
        }

        public static ITerrainChannel LoadFrom32BitStream(Stream fs)
        {
            ITerrainChannel map = null;
            int width = 0;
            int height = 0;

            // Read TIFF header
            byte[] tiffHeader = new byte[8];
            fs.Read(tiffHeader, 0, 8);

            if (tiffHeader[0] != 0x49 || tiffHeader[1] != 0x49 || tiffHeader[2] != 0x2A || tiffHeader[3] != 0x00)
                throw new Exception("Not a valid little-endian TIFF file");

            // Read the IFD (Image File Directory) offset
            uint ifdOffset = BitConverter.ToUInt32(tiffHeader, 4);
            fs.Seek(ifdOffset, SeekOrigin.Begin);

            // Read the number of IFD entries
            byte[] numEntriesBytes = new byte[2];
            fs.Read(numEntriesBytes, 0, 2);
            ushort numEntries = BitConverter.ToUInt16(numEntriesBytes, 0);

            uint imageWidth = 0, imageHeight = 0, stripOffset = 0;
            ushort bitsPerSample = 0, sampleFormat = 0;

            // Read each IFD entry
            for (int i = 0; i < numEntries; i++)
            {
                byte[] entry = new byte[12];
                fs.Read(entry, 0, 12);

                ushort tag = BitConverter.ToUInt16(entry, 0);
                ushort type = BitConverter.ToUInt16(entry, 2);
                uint count = BitConverter.ToUInt32(entry, 4);
                uint valueOffset = BitConverter.ToUInt32(entry, 8);

                switch (tag)
                {
                    case 0x0100: // ImageWidth
                        imageWidth = valueOffset;
                        break;
                    case 0x0101: // ImageLength
                        imageHeight = valueOffset;
                        break;
                    case 0x0102: // BitsPerSample
                        bitsPerSample = (ushort)valueOffset;
                        break;
                    case 0x0111: // StripOffsets
                        stripOffset = valueOffset;
                        break;
                    case 0x0153: // SampleFormat
                        sampleFormat = (ushort)valueOffset;
                        break;
                }
            }

            if (!imageWidth.Equals(0) && !imageHeight.Equals(0))
            {
                width = (int)imageWidth;
                height = (int)imageHeight;

                if (bitsPerSample != 32 || sampleFormat != 3)
                    throw new Exception("TIFF file is not a 32-bit floating-point grayscale image");

                // Seek to the strip offset to read pixel data
                fs.Seek(stripOffset, SeekOrigin.Begin);

                map = new TerrainChannel(width, height);

                // Read pixel data
                for (int y = 0; y < imageHeight; y++)
                {
                    for (int x = 0; x < imageWidth; x++)
                    {
                        byte[] pixelBytes = new byte[4];
                        fs.Read(pixelBytes, 0, 4);
                        float pixelValue = BitConverter.ToSingle(pixelBytes, 0);
                        map[x, y] = InverseNormalize(pixelValue) - 256;
                    }
                }
            }
            fs.Dispose();
            return map;
        }

        protected static ITerrainChannel Load8BitBitmapTile(string filename, int offsetX, int offsetY, int fileWidth, int fileHeight, int w, int h)
        {
            ITerrainChannel retval = new TerrainChannel(w, h);

            string filePath = filename;

            FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
            ITerrainChannel map = LoadFrom8BitStream(fs);

            for (int x = offsetX; x < map.Width && x < fileWidth; x++)
            {
                for (int y = offsetY; y < map.Height && y < fileHeight; y++)
                {
                    retval[x, y] = map[x, y];
                }
            }

            return retval;
        }

        protected static ITerrainChannel Load16BitBitmapTile(string filename, int offsetX, int offsetY, int fileWidth, int fileHeight, int w, int h)
        {
            ITerrainChannel retval = new TerrainChannel(w, h);

            string filePath = filename;

            FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
            ITerrainChannel map = LoadFrom16BitStream(fs);

            for (int x = offsetX; x < map.Width && x < fileWidth; x++)
            {
                for (int y = offsetY; y < map.Height && y < fileHeight; y++)
                {
                    retval[x, y] = map[x, y];
                }
            }

            return retval;
        }

        protected static ITerrainChannel Load32BitBitmapTile(string filename, int offsetX, int offsetY, int fileWidth, int fileHeight, int w, int h)
        {
            ITerrainChannel retval = new TerrainChannel(w, h);

            string filePath = filename;

            FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
            ITerrainChannel map = LoadFrom32BitStream(fs);

            for (int x = offsetX; x < map.Width && x < fileWidth; x++)
            {
                for (int y = offsetY; y < map.Height && y < fileHeight; y++)
                {
                    retval[x, y] = map[x, y];
                }
            }

            return retval;
        }

        #region Helpers

        private static void WriteRational(Stream fs, uint numerator, uint denominator)
        {
            fs.Write(BitConverter.GetBytes(numerator), 0, 4);
            fs.Write(BitConverter.GetBytes(denominator), 0, 4);
        }

        private static void WriteIfdEntry(Stream fs, ushort tag, ushort type, uint count, uint value)
        {
            fs.Write(BitConverter.GetBytes(tag), 0, 2);
            fs.Write(BitConverter.GetBytes(type), 0, 2);
            fs.Write(BitConverter.GetBytes(count), 0, 4);
            fs.Write(BitConverter.GetBytes(value), 0, 4);
        }

        protected static float Normalize(float value)
        {
            float minHeight = 0;
            float maxHeight = 4096;
            return (value - minHeight) / (maxHeight - minHeight);
        }

        protected static float InverseNormalize(float value)
        {
            float minHeight = 0;
            float maxHeight = 4096;
            return minHeight + value * (maxHeight - minHeight);
        }

        protected static int GetColorDepth(Stream stream)
        {
            byte[] buffer = new byte[8];

            // Read byte order mark (first 2 bytes)
            stream.Read(buffer, 0, 2);
            bool isLittleEndian = buffer[0] == 'I' && buffer[1] == 'I';

            // Read TIFF magic number (next 2 bytes)
            stream.Read(buffer, 0, 2);
            ushort magicNumber = BitConverter.ToUInt16(isLittleEndian ? buffer : buffer.Reverse().ToArray(), 0);
            if (magicNumber != 42)
            {
                Console.WriteLine("Not a valid TIFF file.");
                stream.Position = 0;
                return 0;
            }

            // Read offset to first IFD (next 4 bytes)
            stream.Read(buffer, 0, 4);
            uint ifdOffset = BitConverter.ToUInt32(isLittleEndian ? buffer : buffer.Reverse().ToArray(), 0);

            // Move to the first IFD
            stream.Seek(ifdOffset, SeekOrigin.Begin);

            // Read number of directory entries (next 2 bytes)
            stream.Read(buffer, 0, 2);
            ushort numEntries = BitConverter.ToUInt16(isLittleEndian ? buffer : buffer.Reverse().ToArray(), 0);

            buffer = new byte[12]; // Allocate buffer for IFD entries

            for (int i = 0; i < numEntries; i++)
            {
                // Read each IFD entry (12 bytes each)
                stream.Read(buffer, 0, 12);

                // Extract the tag ID
                ushort tagId = BitConverter.ToUInt16(isLittleEndian ? buffer : buffer.Take(2).Reverse().ToArray(), 0);

                // Check for BitsPerSample tag (ID 258)
                if (tagId == 258)
                {
                    ushort type = BitConverter.ToUInt16(isLittleEndian ? buffer.Skip(2).Take(2).ToArray() : buffer.Skip(2).Take(2).Reverse().ToArray(), 0);
                    uint count = BitConverter.ToUInt32(isLittleEndian ? buffer.Skip(4).Take(4).ToArray() : buffer.Skip(4).Take(4).Reverse().ToArray(), 0);

                    // Read the value/offset
                    uint valueOffset = BitConverter.ToUInt32(isLittleEndian ? buffer.Skip(8).Take(4).ToArray() : buffer.Skip(8).Take(4).Reverse().ToArray(), 0);

                    if (count == 1)
                    {
                        ushort bitsPerSample = (ushort)valueOffset;
                        Console.WriteLine($"Color depth: {bitsPerSample} bits per sample");
                        if (bitsPerSample == 8)
                        {
                            Console.WriteLine("The image is 8-bit.");
                            stream.Position = 0;
                            return 8;
                        }
                        else if (bitsPerSample == 16)
                        {
                            Console.WriteLine("The image is 16-bit.");
                            stream.Position = 0;
                            return 16;
                        }
                        else if (bitsPerSample == 32)
                        {
                            Console.WriteLine("The image is 32-bit.");
                            stream.Position = 0;
                            return 32;
                        }
                        else
                        {
                            Console.WriteLine("The image is neither 8-bit nor 16-bit.");
                            stream.Position = 0;
                            return 0;
                        }
                    }
                    else
                    {
                        // Handle the case where BitsPerSample is an offset to an array
                        long currentPos = stream.Position;
                        stream.Seek(valueOffset, SeekOrigin.Begin);
                        List<int> bits = new List<int>();
                        for (int j = 0; j < count; j++)
                        {
                            stream.Read(buffer, 0, 2);
                            ushort bitsPerSample = BitConverter.ToUInt16(isLittleEndian ? buffer : buffer.Take(2).Reverse().ToArray(), 0);
                            Console.WriteLine($"Color depth: {bitsPerSample} bits per sample (channel {j + 1})");
                            bits.Add(bitsPerSample);
                        }
                        stream.Seek(currentPos, SeekOrigin.Begin);
                        if (bits.Any(b => b == 32))
                        {
                            stream.Position = 0;
                            return 32;
                        }
                        else if (bits.Any(b => b == 16))
                        {
                            stream.Position = 0;
                            return 16;
                        }
                        else if (bits.Any(b => b == 8))
                        {
                            stream.Position = 0;
                            return 8;
                        }
                        else
                        {
                            stream.Position = 0;
                            return 0;
                        }
                    }
                }
            }

            Console.WriteLine("BitsPerSample tag not found.");
            stream.Position = 0;
            return 0;
        }

        #endregion
    }
}
