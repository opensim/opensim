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

using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Modules.World.Serialiser;
using System;
using System.IO;
using System.Reflection;
using libsecondlife;
using log4net;

namespace OpenSim.Region.Environment.Modules.World.Archiver
{
    /// <summary>
    /// Handles an individual archive read request
    /// </summary>
    public class ArchiveReadRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected static System.Text.ASCIIEncoding m_asciiEncoding = new System.Text.ASCIIEncoding();

        protected Scene m_scene;
        protected string m_loadPath;

        public ArchiveReadRequest(Scene scene, string loadPath)
        {
            m_scene = scene;
            m_loadPath = loadPath;

            DearchiveRegion();
        }

        protected void DearchiveRegion()
        {
            TarArchiveReader archive = new TarArchiveReader(m_loadPath);
            AssetsDearchiver dearchiver = new AssetsDearchiver(m_scene.AssetCache);

            string serializedPrims = string.Empty;

            // Just test for now by reading first file
            string filePath = "ERROR";

            byte[] data;
            while ((data = archive.ReadEntry(out filePath)) != null)
            {
                m_log.DebugFormat(
                    "[ARCHIVER]: Successfully read {0} ({1} bytes) from archive {2}", filePath, data.Length, m_loadPath);

                if (filePath.Equals(ArchiveConstants.PRIMS_PATH))
                {
                    serializedPrims = m_asciiEncoding.GetString(data);
                }
                else if (filePath.Equals(ArchiveConstants.ASSETS_METADATA_PATH))
                {
                    string xml = m_asciiEncoding.GetString(data);
                    dearchiver.AddAssetMetadata(xml);
                }
                else if (filePath.StartsWith(ArchiveConstants.ASSETS_PATH))
                {
                    dearchiver.AddAssetData(filePath, data);
                }
            }

            m_log.Debug("[ARCHIVER]: Reached end of archive");

            archive.Close();

            if (serializedPrims.Equals(string.Empty))
            {
                m_log.ErrorFormat("[ARCHIVER]: Archive did not contain a {0} file", ArchiveConstants.PRIMS_PATH);
                return;
            }

            // Reload serialized prims
            m_log.InfoFormat("[ARCHIVER]: Loading prim data");

            IRegionSerialiser serialiser = m_scene.RequestModuleInterface<IRegionSerialiser>();
            serialiser.LoadPrimsFromXml2(m_scene, new StringReader(serializedPrims));
        }
    }
}
