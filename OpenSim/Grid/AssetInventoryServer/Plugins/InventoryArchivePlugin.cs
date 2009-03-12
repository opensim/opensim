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
using System.Net;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Reflection;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Archive;
using OpenSim.Framework.Servers;
using log4net;

namespace OpenSim.Grid.AssetInventoryServer.Plugins
{
    public class InventoryArchivePlugin : IAssetInventoryServerPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private AssetInventoryServer m_server;

        public InventoryArchivePlugin()
        {
        }

        #region IPlugin implementation

        public void Initialise(AssetInventoryServer server)
        {
            m_server = server;

            m_server.HttpServer.AddStreamHandler(new GetInventoryArchive(server));

            m_log.Info("[INVENTORYARCHIVE]: Inventory Archive loaded.");
        }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        public void Initialise()
        {
            m_log.InfoFormat("[INVENTORYARCHIVE]: {0} cannot be default-initialized!", Name);
            throw new PluginNotInitialisedException(Name);
        }

        public void Dispose()
        {
        }

        public string Version
        {
            // TODO: this should be something meaningful and not hardcoded?
            get { return "0.1"; }
        }

        public string Name
        {
            get { return "InventoryArchive"; }
        }

        #endregion IPlugin implementation

        public class GetInventoryArchive : BaseStreamHandler
        {
            AssetInventoryServer m_server;

            //public GetInventoryArchive(AssetInventoryServer server) : base("GET", @"^/inventoryarchive/")
            public GetInventoryArchive(AssetInventoryServer server) : base("GET", "/inventoryarchive")
            {
                m_server = server;
            }

            public override string ContentType
            {
                get { return "application/x-compressed"; }
            }

            public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                byte[] buffer = new byte[] {};
                UUID ownerID;
                // Split the URL up to get the asset ID out
                string[] rawUrl = httpRequest.Url.PathAndQuery.Split('/');

                if (rawUrl.Length >= 3 && rawUrl[2].Length >= 36 && UUID.TryParse(rawUrl[2].Substring(0, 36), out ownerID))
                {
                    Uri owner = Utils.GetOpenSimUri(ownerID);
                    InventoryCollection inventory;
                    BackendResponse storageResponse = m_server.InventoryProvider.TryFetchInventory(owner, out inventory);

                    if (storageResponse == BackendResponse.Success)
                    {
                        m_log.DebugFormat("[INVENTORYARCHIVE]: Archiving inventory for user UUID {0}", ownerID);
                        buffer = ArchiveInventoryCollection(inventory);
                        httpResponse.StatusCode = (int) HttpStatusCode.OK;
                    }
                    else
                    {
                        httpResponse.StatusCode = (int) HttpStatusCode.InternalServerError;
                    }
                }
                else
                {
                    m_log.Warn("[INVENTORYARCHIVE]: Unrecognized inventory archive request: " + httpRequest.Url.PathAndQuery);
                }

                return buffer;
            }
        }

        private static byte[] ArchiveInventoryCollection(InventoryCollection inventory)
        {
            byte[] buffer = new byte[] {};

            // Fill in each folder's Children dictionary.
            InventoryFolderWithChildren rootFolder = BuildInventoryHierarchy(ref inventory);

            // TODO: It's probably a bad idea to tar to memory for large
            // inventories.
            MemoryStream ms = new MemoryStream();
            GZipStream gzs = new GZipStream(ms, CompressionMode.Compress, true);
            TarArchiveWriter archive = new TarArchiveWriter(gzs);
            WriteInventoryFolderToArchive(archive, rootFolder, ArchiveConstants.INVENTORY_PATH);

            archive.Close();

            ms.Seek(0, SeekOrigin.Begin);
            buffer = ms.GetBuffer();
            Array.Resize<byte>(ref buffer, (int) ms.Length);
            ms.Close();
            return buffer;
        }

        private static InventoryFolderWithChildren BuildInventoryHierarchy(ref InventoryCollection inventory)
        {
            m_log.DebugFormat("[INVENTORYARCHIVE]: Building inventory hierarchy");
            InventoryFolderWithChildren rootFolder = null;

            foreach (InventoryFolderWithChildren parentFolder in inventory.Folders.Values)
            {
                // Grab the root folder, it has no parents.
                if (UUID.Zero == parentFolder.ParentID) rootFolder = parentFolder;

                foreach (InventoryFolderWithChildren folder in inventory.Folders.Values)
                    if (parentFolder.ID == folder.ParentID)
                        parentFolder.Children.Add(folder.ID, folder);

                foreach (InventoryItemBase item in inventory.Items.Values)
                    if (parentFolder.ID == item.Folder)
                        parentFolder.Children.Add(item.ID, item);
            }

            return rootFolder;
        }

        private static void WriteInventoryFolderToArchive(TarArchiveWriter archive, InventoryFolderWithChildren folder, string path)
        {
            path += string.Format("{0}{1}{2}/", folder.Name, ArchiveConstants.INVENTORY_NODE_NAME_COMPONENT_SEPARATOR, folder.ID);
            archive.WriteDir(path);

            foreach (InventoryNodeBase inventoryNode in folder.Children.Values)
            {
                if (inventoryNode is InventoryFolderWithChildren)
                {
                    WriteInventoryFolderToArchive(archive, (InventoryFolderWithChildren) inventoryNode, path);
                }
                else if (inventoryNode is InventoryItemBase)
                {
                    WriteInventoryItemToArchive(archive, (InventoryItemBase) inventoryNode, path);
                }
            }
        }

        private static void WriteInventoryItemToArchive(TarArchiveWriter archive, InventoryItemBase item, string path)
        {
            string filename = string.Format("{0}{1}_{2}.xml", path, item.Name, item.ID);

            StringWriter sw = new StringWriter();
            XmlTextWriter writer = new XmlTextWriter(sw);
            writer.Formatting = Formatting.Indented;

            writer.WriteStartElement("InventoryItem");

            writer.WriteStartElement("Name");
            writer.WriteString(item.Name);
            writer.WriteEndElement();
            writer.WriteStartElement("ID");
            writer.WriteString(item.ID.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("InvType");
            writer.WriteString(item.InvType.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("CreatorUUID");
            writer.WriteString(item.Creator.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("CreationDate");
            writer.WriteString(item.CreationDate.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("Owner");
            writer.WriteString(item.Owner.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("Description");
            writer.WriteString(item.Description);
            writer.WriteEndElement();
            writer.WriteStartElement("AssetType");
            writer.WriteString(item.AssetType.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("AssetID");
            writer.WriteString(item.AssetID.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("SaleType");
            writer.WriteString(item.SaleType.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("SalePrice");
            writer.WriteString(item.SalePrice.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("BasePermissions");
            writer.WriteString(item.BasePermissions.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("CurrentPermissions");
            writer.WriteString(item.CurrentPermissions.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("EveryOnePermssions");
            writer.WriteString(item.EveryOnePermissions.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("NextPermissions");
            writer.WriteString(item.NextPermissions.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("Flags");
            writer.WriteString(item.Flags.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("GroupID");
            writer.WriteString(item.GroupID.ToString());
            writer.WriteEndElement();
            writer.WriteStartElement("GroupOwned");
            writer.WriteString(item.GroupOwned.ToString());
            writer.WriteEndElement();

            writer.WriteEndElement();

            archive.WriteFile(filename, sw.ToString());

            //m_assetGatherer.GatherAssetUuids(item.AssetID, (AssetType) item.AssetType, assetUuids);
        }
    }
}
