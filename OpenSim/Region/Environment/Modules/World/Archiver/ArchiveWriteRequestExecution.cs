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
 *     * Neither the name of the OpenSim Project nor the
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
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Xml;
using OpenMetaverse;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.World.Serialiser;
using OpenSim.Region.Environment.Modules.World.Terrain;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.World.Archiver
{
    /// <summary>
    /// Method called when all the necessary assets for an archive request have been received.
    /// </summary>
    public delegate void AssetsRequestCallback(IDictionary<UUID, AssetBase> assetsFound, ICollection<UUID> assetsNotFoundUuids);

    /// <summary>
    /// Execute the write of an archive once we have received all the necessary data
    /// </summary>
    public class ArchiveWriteRequestExecution
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected ITerrainModule m_terrainModule;
        protected IRegionSerialiser m_serialiser;
        protected List<SceneObjectGroup> m_sceneObjects;
        protected string m_sceneName;
        protected string m_savePath;

        public ArchiveWriteRequestExecution(
             List<SceneObjectGroup> sceneObjects,
             ITerrainModule terrainModule,
             IRegionSerialiser serialiser,
             string sceneName,
             string savePath)
        {
            m_sceneObjects = sceneObjects;
            m_terrainModule = terrainModule;
            m_serialiser = serialiser;
            m_sceneName = sceneName;
            m_savePath = savePath;
        }

        protected internal void ReceivedAllAssets(IDictionary<UUID, AssetBase> assetsFound, ICollection<UUID> assetsNotFoundUuids)
        {
            foreach (UUID uuid in assetsNotFoundUuids)
            {
                m_log.DebugFormat("[ARCHIVER]: Could not find asset {0}", uuid);
            }

            m_log.InfoFormat(
                "[ARCHIVER]: Received {0} of {1} assets requested", assetsFound.Count, assetsFound.Count + assetsNotFoundUuids.Count);

            TarArchiveWriter archive = new TarArchiveWriter();

            // Write out control file
            archive.AddFile(ArchiveConstants.CONTROL_FILE_PATH, CreateControlFile());

            // Write out terrain
            string terrainPath = String.Format("{0}{1}.r32", ArchiveConstants.TERRAINS_PATH, m_sceneName);
            MemoryStream ms = new MemoryStream();
            m_terrainModule.SaveToStream(terrainPath, ms);
            archive.AddFile(terrainPath, ms.ToArray());
            ms.Close();

            // Write out scene object metadata
            foreach (SceneObjectGroup sceneObject in m_sceneObjects)
            {
                //m_log.DebugFormat("[ARCHIVER]: Saving {0} {1}, {2}", entity.Name, entity.UUID, entity.GetType());

                Vector3 position = sceneObject.AbsolutePosition;

                string serializedObject = m_serialiser.SaveGroupToXml2(sceneObject);
                string filename
                    = string.Format(
                        "{0}{1}_{2:000}-{3:000}-{4:000}__{5}.xml",
                        ArchiveConstants.OBJECTS_PATH, sceneObject.Name,
                        Math.Round(position.X), Math.Round(position.Y), Math.Round(position.Z),
                        sceneObject.UUID);

                archive.AddFile(filename, serializedObject);
            }

            // Write out assets
            AssetsArchiver assetsArchiver = new AssetsArchiver(assetsFound);
            assetsArchiver.Archive(archive);

            archive.WriteTar(new GZipStream(new FileStream(m_savePath, FileMode.Create), CompressionMode.Compress));

            m_log.InfoFormat("[ARCHIVER]: Wrote out OpenSimulator archive {0}", m_savePath);
        }

        /// <summary>
        /// Create the control file for this archive
        /// </summary>
        /// <returns></returns>
        protected string CreateControlFile()
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter xtw = new XmlTextWriter(sw);
            xtw.Formatting = Formatting.Indented;
            xtw.WriteStartDocument();
            xtw.WriteStartElement("archive");
            xtw.WriteAttributeString("major_version", "0");
            xtw.WriteAttributeString("minor_version", "1");
            xtw.WriteEndElement();

            xtw.Flush();
            xtw.Close();

            String s = sw.ToString();
            sw.Close();

            return s;
        }
    }
}
