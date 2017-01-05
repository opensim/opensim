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
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.CoreModules.World.Terrain
{
    public interface ITerrainLoader
    {
        // Returns true if that extension can be used for terrain save-tile
        // (Look into each file in Region.CoreModules.World.Terrain.FileLoaders)
        bool SupportsTileSave();

        string FileExtension { get; }
        ITerrainChannel LoadFile(string filename);
        ITerrainChannel LoadFile(string filename, int fileStartX, int fileStartY, int fileWidth, int fileHeight, int sectionWidth, int sectionHeight);
        ITerrainChannel LoadStream(Stream stream);
        void SaveFile(string filename, ITerrainChannel map);
        void SaveStream(Stream stream, ITerrainChannel map);

        /// <summary>
        /// Save a number of map tiles to a single big image file.
        /// </summary>
        /// <remarks>
        /// If the image file already exists then the tiles saved will replace those already in the file - other tiles
        /// will be untouched.
        /// </remarks>
        /// <param name="filename">The terrain file to save</param>
        /// <param name="offsetX">The map x co-ordinate at which to begin the save.</param>
        /// <param name="offsetY">The may y co-ordinate at which to begin the save.</param>
        /// <param name="fileWidth">The number of tiles to save along the X axis.</param>
        /// <param name="fileHeight">The number of tiles to save along the Y axis.</param>
        /// <param name="regionSizeX">The width of a map tile.</param>
        /// <param name="regionSizeY">The height of a map tile.</param>
        void SaveFile(ITerrainChannel map, string filename, int offsetX, int offsetY, int fileWidth, int fileHeight, int regionSizeX, int regionSizeY);
    }
}