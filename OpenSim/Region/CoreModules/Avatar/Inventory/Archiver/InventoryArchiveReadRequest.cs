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
using System.Text;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.CoreModules.World.Archiver;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Archiver
{
    public class InventoryArchiveReadRequest
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected TarArchiveReader archive;
        private static ASCIIEncoding m_asciiEncoding = new ASCIIEncoding();

        private CachedUserInfo m_userInfo;
        private string m_invPath;
        
        /// <value>
        /// The stream from which the inventory archive will be loaded.
        /// </value>
        private Stream m_loadStream;
        
        CommunicationsManager commsManager;

        public InventoryArchiveReadRequest(
            CachedUserInfo userInfo, string invPath, string loadPath, CommunicationsManager commsManager)
            : this(
                userInfo,
                invPath, 
                new GZipStream(new FileStream(loadPath, FileMode.Open), CompressionMode.Decompress),
                commsManager)
        {
        }
        
        public InventoryArchiveReadRequest(
            CachedUserInfo userInfo, string invPath, Stream loadStream, CommunicationsManager commsManager)
        {
            m_userInfo = userInfo;
            m_invPath = invPath;
            m_loadStream = loadStream;                        
            this.commsManager = commsManager;
        }        

        protected InventoryItemBase loadInvItem(string path, string contents)
        {
            InventoryItemBase item = new InventoryItemBase();
            StringReader sr = new StringReader(contents);
            XmlTextReader reader = new XmlTextReader(sr);

            if (contents.Equals("")) return null;

            reader.ReadStartElement("InventoryObject");
            reader.ReadStartElement("Name");
            item.Name = reader.ReadString();
            reader.ReadEndElement();
            reader.ReadStartElement("ID");
            item.ID = UUID.Parse(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("InvType");
            item.InvType = Convert.ToInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("CreatorUUID");
            item.Creator = UUID.Parse(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("CreationDate");
            item.CreationDate = Convert.ToInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("Owner");
            item.Owner = UUID.Parse(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadElementString("Description");
            reader.ReadStartElement("AssetType");
            item.AssetType = Convert.ToInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("AssetID");
            item.AssetID = UUID.Parse(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("SaleType");
            item.SaleType = Convert.ToByte(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("SalePrice");
            item.SalePrice = Convert.ToInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("BasePermissions");
            item.BasePermissions = Convert.ToUInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("CurrentPermissions");
            item.CurrentPermissions = Convert.ToUInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("EveryOnePermssions");
            item.EveryOnePermissions = Convert.ToUInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("NextPermissions");
            item.NextPermissions = Convert.ToUInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("Flags");
            item.Flags = Convert.ToUInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("GroupID");
            item.GroupID = UUID.Parse(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("GroupOwned");
            item.GroupOwned = Convert.ToBoolean(reader.ReadString());
            reader.ReadEndElement();
            //reader.ReadStartElement("ParentFolderID");
            //item.Folder = UUID.Parse(reader.ReadString());
            //reader.ReadEndElement();
            //reader.ReadEndElement();

            return item;
        }

        /// <summary>
        /// Execute the request
        /// </summary>
        /// <returns>
        /// A list of the inventory nodes loaded.  If folders were loaded then only the root folders are
        /// returned
        /// </returns>
        public List<InventoryNodeBase> Execute()
        {
            string filePath = "ERROR";
            int successfulAssetRestores = 0;
            int failedAssetRestores = 0;
            int successfulItemRestores = 0;
            List<InventoryNodeBase> nodesLoaded = new List<InventoryNodeBase>();
            
            if (!m_userInfo.HasReceivedInventory)
            {
                // If the region server has access to the user admin service (by which users are created), 
                // then we'll assume that it's okay to fiddle with the user's inventory even if they are not on the 
                // server.
                //
                // FIXME: FetchInventory should probably be assumed to by async anyway, since even standalones might
                // use a remote inventory service, though this is vanishingly rare at the moment.
                if (null == commsManager.UserAdminService)
                {
                    m_log.ErrorFormat(
                        "[INVENTORY ARCHIVER]: Have not yet received inventory info for user {0} {1}",
                        m_userInfo.UserProfile.Name, m_userInfo.UserProfile.ID);

                    return nodesLoaded;
                }
                else
                {
                    m_userInfo.FetchInventory();
                }
            }

            InventoryFolderImpl inventoryFolder = m_userInfo.RootFolder.FindFolderByPath(m_invPath);

            if (null == inventoryFolder)
            {
                // TODO: Later on, automatically create this folder if it does not exist
                m_log.ErrorFormat("[INVENTORY ARCHIVER]: Inventory path {0} does not exist", m_invPath);

                return nodesLoaded;
            }

            archive = new TarArchiveReader(m_loadStream);

            byte[] data;
            TarArchiveReader.TarEntryType entryType;
            while ((data = archive.ReadEntry(out filePath, out entryType)) != null)
            {
                if (entryType == TarArchiveReader.TarEntryType.TYPE_DIRECTORY) {
                    m_log.WarnFormat("[INVENTORY ARCHIVER]: Ignoring directory entry {0}", filePath);
                } 
                else if (filePath.StartsWith(ArchiveConstants.ASSETS_PATH))
                {
                    if (LoadAsset(filePath, data))
                        successfulAssetRestores++;
                    else
                        failedAssetRestores++;
                }
                else
                {
                    InventoryItemBase item = loadInvItem(filePath, m_asciiEncoding.GetString(data));

                    if (item != null)
                    {
                        // Don't use the item ID that's in the file
                        item.ID = UUID.Random();
                        
                        item.Creator = m_userInfo.UserProfile.ID;
                        item.Owner = m_userInfo.UserProfile.ID;

                        // Reset folder ID to the one in which we want to load it
                        // TODO: Properly restore entire folder structure.  At the moment all items are dumped in this
                        // single folder no matter where in the saved folder structure they are.
                        item.Folder = inventoryFolder.ID;

                        m_userInfo.AddItem(item);
                        successfulItemRestores++;
                        nodesLoaded.Add(item);
                    }
                }
            }

            archive.Close();

            m_log.DebugFormat("[INVENTORY ARCHIVER]: Restored {0} assets", successfulAssetRestores);
            m_log.InfoFormat("[INVENTORY ARCHIVER]: Restored {0} items", successfulItemRestores);
            
            return nodesLoaded;
        }

        /// <summary>
        /// Load an asset
        /// </summary>
        /// <param name="assetFilename"></param>
        /// <param name="data"></param>
        /// <returns>true if asset was successfully loaded, false otherwise</returns>
        private bool LoadAsset(string assetPath, byte[] data)
        {
            //IRegionSerialiser serialiser = scene.RequestModuleInterface<IRegionSerialiser>();
            // Right now we're nastily obtaining the UUID from the filename
            string filename = assetPath.Remove(0, ArchiveConstants.ASSETS_PATH.Length);
            int i = filename.LastIndexOf(ArchiveConstants.ASSET_EXTENSION_SEPARATOR);

            if (i == -1)
            {
                m_log.ErrorFormat(
                   "[INVENTORY ARCHIVER]: Could not find extension information in asset path {0} since it's missing the separator {1}.  Skipping",
                    assetPath, ArchiveConstants.ASSET_EXTENSION_SEPARATOR);

                return false;
            }

            string extension = filename.Substring(i);
            string uuid = filename.Remove(filename.Length - extension.Length);

            if (ArchiveConstants.EXTENSION_TO_ASSET_TYPE.ContainsKey(extension))
            {
                sbyte assetType = ArchiveConstants.EXTENSION_TO_ASSET_TYPE[extension];

                m_log.DebugFormat("[INVENTORY ARCHIVER]: Importing asset {0}, type {1}", uuid, assetType);

                AssetBase asset = new AssetBase(new UUID(uuid), "RandomName");

                asset.Type = assetType;
                asset.Data = data;

                commsManager.AssetCache.AddAsset(asset);

                return true;
            }
            else
            {
                m_log.ErrorFormat(
                   "[INVENTORY ARCHIVER]: Tried to dearchive data with path {0} with an unknown type extension {1}",
                    assetPath, extension);

                return false;
            }
        }
    }
}
