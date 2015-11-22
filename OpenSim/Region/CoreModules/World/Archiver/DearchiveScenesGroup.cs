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
using log4net;
using System.Reflection;
using OpenSim.Framework.Serialization;

namespace OpenSim.Region.CoreModules.World.Archiver
{
    /// <summary>
    /// The regions included in an OAR file.
    /// </summary>
    public class DearchiveScenesInfo
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// One region in the archive.
        /// </summary>
        public class RegionInfo
        {
            /// <summary>
            /// The subdirectory in which the region is stored.
            /// </summary>
            public string Directory { get; set; }

            /// <summary>
            /// The region's coordinates (relative to the South-West corner of the block).
            /// </summary>
            public Point Location { get; set; }

            /// <summary>
            /// The UUID of the original scene from which this archived region was saved.
            /// </summary>
            public string OriginalID { get; set; }

            /// <summary>
            /// The scene in the current simulator into which this region is loaded.
            /// If null then the region doesn't have a corresponding scene, and it won't be loaded.
            /// </summary>
            public Scene Scene { get; set; }

            /// <summary>
            /// The size of the region being loaded.
            /// </summary>
            public Vector3 RegionSize { get; set; }

            public RegionInfo()
            {
                RegionSize = new Vector3(256f,256f,float.MaxValue);
            }
        }

        /// <summary>
        /// Whether this archive uses the multi-region format.
        /// </summary>
        public Boolean MultiRegionFormat { get; set; }

        /// <summary>
        /// Maps (Region directory -> region)
        /// </summary>
        protected Dictionary<string, RegionInfo> m_directory2region = new Dictionary<string, RegionInfo>();

        /// <summary>
        /// Maps (UUID of the scene in the simulator where the region will be loaded -> region)
        /// </summary>
        protected Dictionary<UUID, RegionInfo> m_newId2region = new Dictionary<UUID, RegionInfo>();

        public int LoadedCreationDateTime { get; set; }
        public string DefaultOriginalID { get; set; }

        // These variables are used while reading the archive control file
        protected int? m_curY = null;
        protected int? m_curX = null;
        protected RegionInfo m_curRegion;


        public DearchiveScenesInfo()
        {
            MultiRegionFormat = false;
        }


        // The following methods are used while reading the archive control file

        public void StartRow()
        {
            m_curY = (m_curY == null) ? 0 : m_curY + 1;
            m_curX = null;
        }

        public void StartRegion()
        {
            m_curX = (m_curX == null) ? 0 : m_curX + 1;
           // Note: this doesn't mean we have a real region in this location; this could just be a "hole"
        }

        public void SetRegionOriginalID(string id)
        {
            m_curRegion = new RegionInfo();
            int x = (int)((m_curX == null) ? 0 : m_curX);
            int y = (int)((m_curY == null) ? 0 : m_curY);

            m_curRegion.Location = new Point(x, y);
            m_curRegion.OriginalID = id;
            // 'curRegion' will be saved in 'm_directory2region' when SetRegionDir() is called
        }

        public void SetRegionDirectory(string directory)
        {
            if(m_curRegion != null)
            {
                m_curRegion.Directory = directory;
                m_directory2region[directory] = m_curRegion;
            }
        }

        public void SetRegionSize(Vector3 size)
        {
            if(m_curRegion != null)
                m_curRegion.RegionSize = size;
        }

        /// <summary>
        /// Sets all the scenes present in the simulator.
        /// </summary>
        /// <remarks>
        /// This method matches regions in the archive to scenes in the simulator according to
        /// their relative position. We only load regions if there's an existing Scene in the
        /// grid location where the region should be loaded.
        /// </remarks>
        /// <param name="rootScene">The scene where the Load OAR operation was run</param>
        /// <param name="simulatorScenes">All the scenes in the simulator</param>
        public void SetSimulatorScenes(Scene rootScene, ArchiveScenesGroup simulatorScenes)
        {
            foreach (RegionInfo archivedRegion in m_directory2region.Values)
            {
                Point location = new Point((int)rootScene.RegionInfo.RegionLocX,
                            (int)rootScene.RegionInfo.RegionLocY);

                location.Offset(archivedRegion.Location);

                Scene scene;
                if (simulatorScenes.TryGetScene(location, out scene))
                {
                    archivedRegion.Scene = scene;
                    m_newId2region[scene.RegionInfo.RegionID] = archivedRegion;
                }
                else
                {
                    m_log.WarnFormat("[ARCHIVER]: Not loading archived region {0} because there's no existing region at location {1},{2}",
                        archivedRegion.Directory, location.X, location.Y);
                }
            }
        }

        /// <summary>
        /// Returns the archived region according to the path of a file in the archive.
        /// Also, converts the full path into a path that is relative to the region's directory.
        /// </summary>
        /// <param name="fullPath">The path of a file in the archive</param>
        /// <param name="scene">The corresponding Scene, or null if none</param>
        /// <param name="relativePath">The path relative to the region's directory. (Or the original
        /// path, if this file doesn't belong to a region.)</param>
        /// <returns>True: use this file; False: skip it</returns>
        public bool GetRegionFromPath(string fullPath, out Scene scene, out string relativePath)
        {
            scene = null;
            relativePath = fullPath;

            if (!MultiRegionFormat)
            {
                if (m_newId2region.Count > 0)
                    scene = m_newId2region.First().Value.Scene;
                return true;
            }

            if (!fullPath.StartsWith(ArchiveConstants.REGIONS_PATH))
                return true;    // this file doesn't belong to a region

            string[] parts = fullPath.Split(new Char[] { '/' }, 3);
            if (parts.Length != 3)
                return false;
            string regionDirectory = parts[1];
            relativePath = parts[2];
            
            RegionInfo region;
            if (m_directory2region.TryGetValue(regionDirectory, out region))
            {
                scene = region.Scene;
                return (scene != null);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the original UUID of a region (from the simulator where the OAR was saved),
        /// given the UUID of the scene it was loaded into in the current simulator.
        /// </summary>
        /// <param name="newID"></param>
        /// <returns></returns>
        public string GetOriginalRegionID(UUID newID)
        {
            RegionInfo region;
            if (m_newId2region.TryGetValue(newID, out region))
                return region.OriginalID;
            else
                return DefaultOriginalID;
        }

        /// <summary>
        /// Returns the scenes that have been (or will be) loaded.
        /// </summary>
        /// <returns></returns>
        public List<UUID> GetLoadedScenes()
        {
            return m_newId2region.Keys.ToList();
        }

        public int GetScenesCount()
        {
            return m_directory2region.Count;
        }
    }
}
