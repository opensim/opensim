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
using OpenMetaverse;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface ITerrainChannel
    {
        int Width { get;}       // X dimension
        int Height { get;}      // Y dimension
        int Altitude { get;}    // Z dimension

        float this[int x, int y] { get; set; }

        float GetHeightAtXYZ(float x, float y, float z);

        // Return the packaged terrain data for passing into lower levels of communication
        TerrainData GetTerrainData();

        /// <summary>
        /// Squash the entire heightmap into a single dimensioned array
        /// </summary>
        /// <returns></returns>
        float[] GetFloatsSerialised();

        double[,] GetDoubles();

        // Check if a location has been updated. Clears the taint flag as a side effect.
        bool Tainted(int x, int y);

        ITerrainChannel MakeCopy();
        string SaveToXmlString();
        void LoadFromXmlString(string data);
        // Merge some terrain into this channel
        void Merge(ITerrainChannel newTerrain, Vector3 displacement, float radianRotation, Vector2 rotationDisplacement);

        /// </summary>
        /// <param name="newTerrain"></param>
        /// <param name="displacement">&lt;x, y, z&gt;</param>
        /// <param name="rotationDegrees"></param>
        /// <param name="boundingOrigin">&lt;x, y&gt;</param>
        /// <param name="boundingSize">&lt;x, y&gt;</param>
        void MergeWithBounding(ITerrainChannel newTerrain, Vector3 displacement, float rotationDegrees, Vector2 boundingOrigin, Vector2 boundingSize);
    }
}
