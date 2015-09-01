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
using System.Linq;
using System.Text;
using OpenSim.Region.Framework.Scenes;
using OpenMetaverse;
using System.Drawing;

namespace OpenSim.Region.CoreModules.World.Archiver
{
    /// <summary>
    /// A group of regions arranged in a rectangle, possibly with holes.
    /// </summary>
    /// <remarks>
    /// The regions usually (but not necessarily) belong to an archive file, in which case we
    /// store additional information used to create the archive (e.g., each region's
    /// directory within the archive).
    /// </remarks>
    public class ArchiveScenesGroup
    {
        /// <summary>
        /// All the regions. The outer dictionary contains rows (key: Y coordinate).
        /// The inner dictionaries contain each row's regions (key: X coordinate).
        /// </summary>
        public SortedDictionary<uint, SortedDictionary<uint, Scene>> Regions { get; set; }
        
        /// <summary>
        /// The subdirectory where each region is stored in the archive.
        /// </summary>
        protected Dictionary<UUID, string> m_regionDirs;

        /// <summary>
        /// The grid coordinates of the regions' bounding box.
        /// </summary>
        public Rectangle Rect { get; set; }


        public ArchiveScenesGroup()
        {
            Regions = new SortedDictionary<uint, SortedDictionary<uint, Scene>>();
            m_regionDirs = new Dictionary<UUID, string>();
            Rect = new Rectangle(0, 0, 0, 0);
        }

        public void AddScene(Scene scene)
        {
            uint x = scene.RegionInfo.RegionLocX;
            uint y = scene.RegionInfo.RegionLocY;

            SortedDictionary<uint, Scene> row;
            if (!Regions.TryGetValue(y, out row))
            {
                row = new SortedDictionary<uint, Scene>();
                Regions[y] = row;
            }

            row[x] = scene;
        }

        /// <summary>
        /// Called after all the scenes have been added. Performs calculations that require
        /// knowledge of all the scenes.
        /// </summary>
        public void CalcSceneLocations()
        {
            if (Regions.Count == 0)
                return;

            // Find the bounding rectangle

            uint firstY = Regions.First().Key;
            uint lastY = Regions.Last().Key;

            uint? firstX = null;
            uint? lastX = null;

            foreach (SortedDictionary<uint, Scene> row in Regions.Values)
            {
                uint curFirstX = row.First().Key;
                uint curLastX = row.Last().Key;

                firstX = (firstX == null) ? curFirstX : (firstX < curFirstX) ? firstX : curFirstX;
                lastX = (lastX == null) ? curLastX : (lastX > curLastX) ? lastX : curLastX;
            }

            Rect = new Rectangle((int)firstX, (int)firstY, (int)(lastX - firstX + 1), (int)(lastY - firstY + 1));


            // Calculate the subdirectory in which each region will be stored in the archive

            m_regionDirs.Clear();
            ForEachScene(delegate(Scene scene)
            {
                // We add the region's coordinates to ensure uniqueness even if multiple regions have the same name
                string path = string.Format("{0}_{1}_{2}",
                    scene.RegionInfo.RegionLocX - Rect.X + 1,
                    scene.RegionInfo.RegionLocY - Rect.Y + 1,
                    scene.RegionInfo.RegionName.Replace(' ', '_'));
                m_regionDirs[scene.RegionInfo.RegionID] = path;
            });
        }

        /// <summary>
        /// Returns the subdirectory where the region is stored.
        /// </summary>
        /// <param name="regionID"></param>
        /// <returns></returns>
        public string GetRegionDir(UUID regionID)
        {
            return m_regionDirs[regionID];
        }

        /// <summary>
        /// Performs an action on all the scenes in this order: rows from South to North,
        /// and within each row West to East.
        /// </summary>
        /// <param name="action"></param>
        public void ForEachScene(Action<Scene> action)
        {
            foreach (SortedDictionary<uint, Scene> row in Regions.Values)
            {
                foreach (Scene scene in row.Values)
                {
                    action(scene);
                }
            }
        }
        
        /// <summary>
        /// Returns the scene at position 'location'.
        /// </summary>
        /// <param name="location">A location in the grid</param>
        /// <param name="scene">The scene at this location</param>
        /// <returns>Whether the scene was found</returns>
        public bool TryGetScene(Point location, out Scene scene)
        {
            SortedDictionary<uint, Scene> row;
            if (Regions.TryGetValue((uint)location.Y, out row))
            {
                if (row.TryGetValue((uint)location.X, out scene))
                    return true;
            }

            scene = null;
            return false;
        }

    }
}
