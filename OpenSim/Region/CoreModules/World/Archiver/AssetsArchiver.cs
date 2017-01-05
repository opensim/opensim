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

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Serialization;

namespace OpenSim.Region.CoreModules.World.Archiver
{
    /// <summary>
    /// Archives assets
    /// </summary>
    public class AssetsArchiver
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <value>
        /// Post a message to the log every x assets as a progress bar
        /// </value>
        protected static int LOG_ASSET_LOAD_NOTIFICATION_INTERVAL = 50;

        /// <value>
        /// Keep a count of the number of assets written so that we can provide status updates
        /// </value>
        protected int m_assetsWritten;

        protected TarArchiveWriter m_archiveWriter;

        public AssetsArchiver(TarArchiveWriter archiveWriter)
        {
            m_archiveWriter = archiveWriter;
        }

        /// <summary>
        /// Archive the assets given to this archiver to the given archive.
        /// </summary>
        /// <param name="archive"></param>
        public void WriteAsset(AssetBase asset)
        {
            //WriteMetadata(archive);
            WriteData(asset);
        }

        /// <summary>
        /// Write an assets metadata file to the given archive
        /// </summary>
        /// <param name="archive"></param>
//        protected void WriteMetadata(TarArchiveWriter archive)
//        {
//            StringWriter sw = new StringWriter();
//            XmlTextWriter xtw = new XmlTextWriter(sw);
//
//            xtw.Formatting = Formatting.Indented;
//            xtw.WriteStartDocument();
//
//            xtw.WriteStartElement("assets");
//
//            foreach (UUID uuid in m_assets.Keys)
//            {
//                AssetBase asset = m_assets[uuid];
//
//                if (asset != null)
//                {
//                    xtw.WriteStartElement("asset");
//
//                    string extension = string.Empty;
//
//                    if (ArchiveConstants.ASSET_TYPE_TO_EXTENSION.ContainsKey(asset.Type))
//                    {
//                        extension = ArchiveConstants.ASSET_TYPE_TO_EXTENSION[asset.Type];
//                    }
//
//                    xtw.WriteElementString("filename", uuid.ToString() + extension);
//
//                    xtw.WriteElementString("name", asset.Name);
//                    xtw.WriteElementString("description", asset.Description);
//                    xtw.WriteElementString("asset-type", asset.Type.ToString());
//
//                    xtw.WriteEndElement();
//                }
//            }
//
//            xtw.WriteEndElement();
//
//            xtw.WriteEndDocument();
//
//            archive.WriteFile("assets.xml", sw.ToString());
//        }

        /// <summary>
        /// Write asset data files to the given archive
        /// </summary>
        /// <param name="asset"></param>
        protected void WriteData(AssetBase asset)
        {
            // It appears that gtar, at least, doesn't need the intermediate directory entries in the tar
            //archive.AddDir("assets");

            string extension = string.Empty;

            if (ArchiveConstants.ASSET_TYPE_TO_EXTENSION.ContainsKey(asset.Type))
            {
                extension = ArchiveConstants.ASSET_TYPE_TO_EXTENSION[asset.Type];
            }
            else
            {
                m_log.ErrorFormat(
                    "[ARCHIVER]: Unrecognized asset type {0} with uuid {1}.  This asset will be saved but not reloaded",
                    asset.Type, asset.ID);
            }

            m_archiveWriter.WriteFile(
                ArchiveConstants.ASSETS_PATH + asset.FullID.ToString() + extension,
                asset.Data);

            m_assetsWritten++;

            //m_log.DebugFormat("[ARCHIVER]: Added asset {0}", m_assetsWritten);

            if (m_assetsWritten % LOG_ASSET_LOAD_NOTIFICATION_INTERVAL == 0)
                m_log.InfoFormat("[ARCHIVER]: Added {0} assets to archive", m_assetsWritten);
        }

    }
}
