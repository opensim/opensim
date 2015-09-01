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
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Serialization;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.World.Archiver
{
    /// <summary>
    /// Dearchives assets
    /// </summary>
    public class AssetsDearchiver
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Store for asset data we received before we get the metadata
        /// </summary>
        protected Dictionary<string, byte[]> m_assetDataAwaitingMetadata = new Dictionary<string, byte[]>();

        /// <summary>
        /// Asset metadata.  Is null if asset metadata isn't yet available.
        /// </summary>
        protected Dictionary<string, AssetMetadata> m_metadata;

        /// <summary>
        /// Cache to which dearchived assets will be added
        /// </summary>
        protected IAssetService m_cache;

        public AssetsDearchiver(IAssetService cache)
        {
            m_cache = cache;
        }

        /// <summary>
        /// Add asset data to the dearchiver
        /// </summary>
        /// <param name="assetFilename"></param>
        /// <param name="data"></param>
        public void AddAssetData(string assetFilename, byte[] data)
        {
            if (null == m_metadata)
            {
                m_assetDataAwaitingMetadata[assetFilename] = data;
            }
            else
            {
                ResolveAssetData(assetFilename, data);
            }
        }

        /// <summary>
        /// Add asset metadata xml
        /// </summary>
        /// <param name="xml"></param>
        public void AddAssetMetadata(string xml)
        {
            m_metadata = new Dictionary<string, AssetMetadata>();

            StringReader sr = new StringReader(xml);
            XmlTextReader reader = new XmlTextReader(sr);

            reader.ReadStartElement("assets");
            reader.Read();

            while (reader.Name.Equals("asset"))
            {
                reader.Read();

                AssetMetadata metadata = new AssetMetadata();

                string filename = reader.ReadElementString("filename");
                m_log.DebugFormat("[DEARCHIVER]: Reading node {0}", filename);

                metadata.Name = reader.ReadElementString("name");
                metadata.Description = reader.ReadElementString("description");
                metadata.AssetType = Convert.ToSByte(reader.ReadElementString("asset-type"));

                m_metadata[filename] = metadata;

                // Read asset end tag
                reader.ReadEndElement();

                reader.Read();
            }

            m_log.DebugFormat("[DEARCHIVER]: Resolved {0} items of asset metadata", m_metadata.Count);

            ResolvePendingAssetData();
        }

        /// <summary>
        /// Resolve asset data that we collected before receiving the metadata
        /// </summary>
        protected void ResolvePendingAssetData()
        {
            foreach (string filename in m_assetDataAwaitingMetadata.Keys)
            {
                ResolveAssetData(filename, m_assetDataAwaitingMetadata[filename]);
            }
        }

        /// <summary>
        /// Resolve a new piece of asset data against stored metadata
        /// </summary>
        /// <param name="assetFilename"></param>
        /// <param name="data"></param>
        protected void ResolveAssetData(string assetPath, byte[] data)
        {
            // Right now we're nastily obtaining the UUID from the filename
            string filename = assetPath.Remove(0, ArchiveConstants.ASSETS_PATH.Length);

            if (m_metadata.ContainsKey(filename))
            {
                AssetMetadata metadata = m_metadata[filename];

                if (ArchiveConstants.ASSET_TYPE_TO_EXTENSION.ContainsKey(metadata.AssetType))
                {
                    string extension = ArchiveConstants.ASSET_TYPE_TO_EXTENSION[metadata.AssetType];
                    filename = filename.Remove(filename.Length - extension.Length);
                }

                m_log.DebugFormat("[ARCHIVER]: Importing asset {0}", filename);

                AssetBase asset = new AssetBase(new UUID(filename), metadata.Name, metadata.AssetType, UUID.Zero.ToString());
                asset.Description = metadata.Description;
                asset.Data = data;

                m_cache.Store(asset);
            }
            else
            {
                m_log.ErrorFormat(
                    "[DEARCHIVER]: Tried to dearchive data with filename {0} without any corresponding metadata",
                    assetPath);
            }
        }

        /// <summary>
        /// Metadata for an asset
        /// </summary>
        protected struct AssetMetadata
        {
            public string Name;
            public string Description;
            public sbyte AssetType;
        }
    }
}
