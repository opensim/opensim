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
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Modules.World.Archiver;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using log4net;
using OpenSim.Region.Environment.Modules.World.Serialiser;


namespace OpenSim.Region.Environment.Modules.Avatar.Inventory.Archiver
{
    public class InventoryArchiveReadRequest
    {
        protected Scene scene;
        protected TarArchiveReader archive;
        private static System.Text.ASCIIEncoding m_asciiEncoding = new System.Text.ASCIIEncoding();
        ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        CachedUserInfo userInfo;
        UserProfileData userProfile;
        CommunicationsManager commsManager;
        string loadPath;

        public InventoryArchiveReadRequest(Scene currentScene, CommunicationsManager commsManager)
        {
            List<string> serialisedObjects = new List<string>();
            scene = currentScene;
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
            item.InvType = System.Convert.ToInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("CreatorUUID");
            item.Creator = UUID.Parse(reader.ReadString());
            item.Creator = userProfile.ID;
            reader.ReadEndElement();
            reader.ReadStartElement("CreationDate");
            item.CreationDate = System.Convert.ToInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("Owner");
            item.Owner = UUID.Parse(reader.ReadString());
            item.Owner = userProfile.ID;
            reader.ReadEndElement();
            //No description would kill it
            if (reader.IsEmptyElement)
            {
                reader.ReadStartElement("Description");
            }
            else
            {
                reader.ReadStartElement("Description"); 
                item.Description = reader.ReadString();
                reader.ReadEndElement();
            }
            reader.ReadStartElement("AssetType");
            item.AssetType = System.Convert.ToInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("AssetID");
            item.AssetID = UUID.Parse(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("SaleType");
            item.SaleType = System.Convert.ToByte(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("SalePrice");
            item.SalePrice = System.Convert.ToInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("BasePermissions");
            item.BasePermissions = System.Convert.ToUInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("CurrentPermissions");
            item.CurrentPermissions = System.Convert.ToUInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("EveryOnePermssions");
            item.EveryOnePermissions = System.Convert.ToUInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("NextPermissions");
            item.NextPermissions = System.Convert.ToUInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("Flags");
            item.Flags = System.Convert.ToUInt32(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("GroupID");
            item.GroupID = UUID.Parse(reader.ReadString());
            reader.ReadEndElement();
            reader.ReadStartElement("GroupOwned");
            item.GroupOwned = System.Convert.ToBoolean(reader.ReadString());
            reader.ReadEndElement();
            //reader.ReadStartElement("ParentFolderID");
            //item.Folder = UUID.Parse(reader.ReadString());
            //reader.ReadEndElement();
            //reader.ReadEndElement();

            return item;
        }

        public void execute(string[] cmdparams)
        {
            string filePath = "ERROR";
            int successfulAssetRestores = 0;
            int failedAssetRestores = 0;

            string firstName = cmdparams[0];
            string lastName = cmdparams[1];
            string invPath = cmdparams[2];
            loadPath = (cmdparams.Length > 3 ? cmdparams[3] : "inventory.tar.gz");

            archive
                = new TarArchiveReader(new GZipStream(
                    new FileStream(loadPath, FileMode.Open), CompressionMode.Decompress));


            userProfile = commsManager.UserService.GetUserProfile(firstName, lastName);
            userInfo = commsManager.UserProfileCacheService.GetUserDetails(userProfile.ID);

            byte[] data;
            while ((data = archive.ReadEntry(out filePath)) != null)
            {
                if (filePath.StartsWith(ArchiveConstants.ASSETS_PATH))
                {
                    if (LoadAsset(filePath, data))
                        successfulAssetRestores++;
                    else
                        failedAssetRestores++;
                }
                else
                {
                    //Load the item
                    InventoryItemBase item = loadInvItem(filePath, m_asciiEncoding.GetString(data));
                    if (item != null) userInfo.AddItem(item);
                }
            }

            archive.Close();

            m_log.InfoFormat("[ARCHIVER]: Restored {0} assets", successfulAssetRestores);
        }

        /// <summary>
        /// Load an asset
        /// </summary>
        /// <param name="assetFilename"></param>
        /// <param name="data"></param>
        /// <returns>true if asset was successfully loaded, false otherwise</returns>
        private bool LoadAsset(string assetPath, byte[] data)
        {
            IRegionSerialiser serialiser = scene.RequestModuleInterface<IRegionSerialiser>();
            // Right now we're nastily obtaining the UUID from the filename
            string filename = assetPath.Remove(0, ArchiveConstants.ASSETS_PATH.Length);
            int i = filename.LastIndexOf(ArchiveConstants.ASSET_EXTENSION_SEPARATOR);

            if (i == -1)
            {
                m_log.ErrorFormat(
                   "[ARCHIVER]: Could not find extension information in asset path {0} since it's missing the separator {1}.  Skipping",
                    assetPath, ArchiveConstants.ASSET_EXTENSION_SEPARATOR);

                return false;
            }

            string extension = filename.Substring(i);
            string uuid = filename.Remove(filename.Length - extension.Length);

            if (ArchiveConstants.EXTENSION_TO_ASSET_TYPE.ContainsKey(extension))
            {
                sbyte assetType = ArchiveConstants.EXTENSION_TO_ASSET_TYPE[extension];

                m_log.DebugFormat("[ARCHIVER]: Importing asset {0}, type {1}", uuid, assetType);

                AssetBase asset = new AssetBase(new UUID(uuid), "RandomName");

                asset.Type = assetType;
                asset.Data = data;

                scene.AssetCache.AddAsset(asset);


                return true;
            }
            else
            {
                m_log.ErrorFormat(
                   "[ARCHIVER]: Tried to dearchive data with path {0} with an unknown type extension {1}",
                    assetPath, extension);

                return false;
            }
        }
    }
}
