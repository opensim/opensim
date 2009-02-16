/*
 * Copyright (c) 2008 Intel Corporation
 * All rights reserved.
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 *
 * -- Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * -- Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * -- Neither the name of the Intel Corporation nor the names of its
 *    contributors may be used to endorse or promote products derived from
 *    this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
 * PARTICULAR PURPOSE ARE DISCLAIMED.  IN NO EVENT SHALL THE INTEL OR ITS
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
 * PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Xml;
using ExtensionLoader;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using HttpServer;
using OpenSim.Framework;

namespace OpenSim.Grid.AssetInventoryServer.Plugins.OpenSim
{
    public class OpenSimInventoryFrontendPlugin : IAssetInventoryServerPlugin
    {
        private AssetInventoryServer server;
        private Utils.InventoryItemSerializer itemSerializer = new Utils.InventoryItemSerializer();
        private Utils.InventoryFolderSerializer folderSerializer = new Utils.InventoryFolderSerializer();
        private Utils.InventoryCollectionSerializer collectionSerializer = new Utils.InventoryCollectionSerializer();

        public OpenSimInventoryFrontendPlugin()
        {
        }

        #region IPlugin implementation

        public void Initialise(AssetInventoryServer server)
        {
            this.server = server;

            server.HttpServer.AddHandler("post", null, @"^/GetInventory/", GetInventoryHandler);
            server.HttpServer.AddHandler("post", null, @"^/CreateInventory/", CreateInventoryHandler);
            server.HttpServer.AddHandler("post", null, @"^/NewFolder/", NewFolderHandler);
            server.HttpServer.AddHandler("post", null, @"^/UpdateFolder/", UpdateFolderHandler);
            server.HttpServer.AddHandler("post", null, @"^/MoveFolder/", MoveFolderHandler);
            server.HttpServer.AddHandler("post", null, @"^/PurgeFolder/", PurgeFolderHandler);
            server.HttpServer.AddHandler("post", null, @"^/NewItem/", NewItemHandler);
            server.HttpServer.AddHandler("post", null, @"^/DeleteItem/", DeleteItemHandler);
            server.HttpServer.AddHandler("post", null, @"^/RootFolders/", RootFoldersHandler);
            server.HttpServer.AddHandler("post", null, @"^/ActiveGestures/", ActiveGesturesHandler);

            Logger.Log.Info("[INVENTORY] OpenSim Inventory Frontend loaded.");
        }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        public void Initialise()
        {
            Logger.Log.InfoFormat("[INVENTORY]: {0} cannot be default-initialized!", Name);
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
            get { return "AssetInventoryServer OpenSim asset frontend"; }
        }

        #endregion IPlugin implementation

        bool GetInventoryHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            UUID sessionID, agentID;
            UUID ownerID = DeserializeUUID(request.Body, out agentID, out sessionID);

            if (ownerID != UUID.Zero)
            {
                Logger.Log.Warn("GetInventory is not scalable on some inventory backends, avoid calling it wherever possible");

                Uri owner = Utils.GetOpenSimUri(ownerID);
                InventoryCollection inventory;
                BackendResponse storageResponse = server.InventoryProvider.TryFetchInventory(owner, out inventory);

                if (storageResponse == BackendResponse.Success)
                {
                    collectionSerializer.Serialize(response.Body, inventory);
                    response.Body.Flush();
                }
                else if (storageResponse == BackendResponse.NotFound)
                {
                    // Return an empty inventory set to mimic OpenSim.Grid.InventoryServer.exe
                    inventory = new InventoryCollection();
                    inventory.UserID = ownerID;
                    inventory.Folders = new Dictionary<UUID, InventoryFolder>();
                    inventory.Items = new Dictionary<UUID, InventoryItem>();
                    collectionSerializer.Serialize(response.Body, inventory);
                    response.Body.Flush();
                }
                else
                {
                    response.Status = HttpStatusCode.InternalServerError;
                }
            }
            else
            {
                response.Status = HttpStatusCode.BadRequest;
            }

            return true;
        }

        bool CreateInventoryHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            UUID ownerID = DeserializeUUID(request.Body);

            if (ownerID != UUID.Zero)
            {
                Uri owner = Utils.GetOpenSimUri(ownerID);
                Logger.Log.DebugFormat("Created URI {0} for inventory creation", owner);

                InventoryFolder rootFolder = new InventoryFolder("My Inventory", ownerID, UUID.Zero, (short)AssetType.Folder);
                BackendResponse storageResponse = server.InventoryProvider.TryCreateInventory(owner, rootFolder);
                if (storageResponse == BackendResponse.Success)
                {
                    CreateFolder("Animations", ownerID, rootFolder.ID, AssetType.Animation);
                    CreateFolder("Body Parts", ownerID, rootFolder.ID, AssetType.Bodypart);
                    CreateFolder("Calling Cards", ownerID, rootFolder.ID, AssetType.CallingCard);
                    CreateFolder("Clothing", ownerID, rootFolder.ID, AssetType.Clothing);
                    CreateFolder("Gestures", ownerID, rootFolder.ID, AssetType.Gesture);
                    CreateFolder("Landmarks", ownerID, rootFolder.ID, AssetType.Landmark);
                    CreateFolder("Lost and Found", ownerID, rootFolder.ID, AssetType.LostAndFoundFolder);
                    CreateFolder("Notecards", ownerID, rootFolder.ID, AssetType.Notecard);
                    CreateFolder("Objects", ownerID, rootFolder.ID, AssetType.Object);
                    CreateFolder("Photo Album", ownerID, rootFolder.ID, AssetType.SnapshotFolder);
                    CreateFolder("Scripts", ownerID, rootFolder.ID, AssetType.LSLText);
                    CreateFolder("Sounds", ownerID, rootFolder.ID, AssetType.Sound);
                    CreateFolder("Textures", ownerID, rootFolder.ID, AssetType.Texture);
                    CreateFolder("Trash", ownerID, rootFolder.ID, AssetType.TrashFolder);

                    SerializeBool(response.Body, true);
                    return true;
                }
            }

            SerializeBool(response.Body, false);
            return true;
        }

        bool NewFolderHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            UUID agentID, sessionID;
            InventoryFolder folder = DeserializeFolder(request.Body, out agentID, out sessionID);

            if (folder != null)
            {
                Uri owner = Utils.GetOpenSimUri(folder.Owner);

                // Some calls that are moving or updating a folder instead of creating a new one
                // will pass in an InventoryFolder without the name set. If this is the case we
                // need to look up the name first
                if (String.IsNullOrEmpty(folder.Name))
                {
                    InventoryFolder oldFolder;
                    if (server.InventoryProvider.TryFetchFolder(owner, folder.ID, out oldFolder) == BackendResponse.Success)
                        folder.Name = oldFolder.Name;
                }

                BackendResponse storageResponse = server.InventoryProvider.TryCreateFolder(owner, folder);

                if (storageResponse == BackendResponse.Success)
                {
                    SerializeBool(response.Body, true);
                    return true;
                }
            }

            SerializeBool(response.Body, false);
            return true;
        }

        bool UpdateFolderHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            return NewFolderHandler(client, request, response);
        }

        bool MoveFolderHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            return NewFolderHandler(client, request, response);
        }

        bool PurgeFolderHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            UUID agentID, sessionID;
            InventoryFolder folder = DeserializeFolder(request.Body, out agentID, out sessionID);

            if (folder != null)
            {
                Uri owner = Utils.GetOpenSimUri(folder.Owner);
                BackendResponse storageResponse = server.InventoryProvider.TryPurgeFolder(owner, folder.ID);

                if (storageResponse == BackendResponse.Success)
                {
                    SerializeBool(response.Body, true);
                    return true;
                }
            }

            SerializeBool(response.Body, false);
            return true;
        }

        bool NewItemHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            UUID agentID, sessionID;
            InventoryItem item = DeserializeItem(request.Body, out agentID, out sessionID);

            if (item != null)
            {
                Uri owner = Utils.GetOpenSimUri(agentID);
                BackendResponse storageResponse = server.InventoryProvider.TryCreateItem(owner, item);

                if (storageResponse == BackendResponse.Success)
                {
                    SerializeBool(response.Body, true);
                    return true;
                }
            }

            SerializeBool(response.Body, false);
            return true;
        }

        bool DeleteItemHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            UUID agentID, sessionID;
            InventoryItem item = DeserializeItem(request.Body, out agentID, out sessionID);

            if (item != null)
            {
                Uri owner = Utils.GetOpenSimUri(item.Owner);
                BackendResponse storageResponse = server.InventoryProvider.TryDeleteItem(owner, item.ID);

                if (storageResponse == BackendResponse.Success)
                {
                    SerializeBool(response.Body, true);
                    return true;
                }
            }

            SerializeBool(response.Body, false);
            return true;
        }

        bool RootFoldersHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            UUID ownerID = DeserializeUUID(request.Body);

            if (ownerID != UUID.Zero)
            {
                Uri owner = Utils.GetOpenSimUri(ownerID);
                List<InventoryFolder> skeleton;
                BackendResponse storageResponse = server.InventoryProvider.TryFetchFolderList(owner, out skeleton);

                if (storageResponse == BackendResponse.Success)
                {
                    SerializeFolderList(response.Body, skeleton);
                }
                else if (storageResponse == BackendResponse.NotFound)
                {
                    // Return an empty set of inventory so the requester knows that
                    // an inventory needs to be created for this agent
                    SerializeFolderList(response.Body, new List<InventoryFolder>(0));
                }
                else
                {
                    response.Status = HttpStatusCode.InternalServerError;
                }
            }
            else
            {
                response.Status = HttpStatusCode.BadRequest;
            }

            return true;
        }

        bool ActiveGesturesHandler(IHttpClientContext client, IHttpRequest request, IHttpResponse response)
        {
            UUID ownerID = DeserializeUUID(request.Body);

            if (ownerID != UUID.Zero)
            {
                Uri owner = Utils.GetOpenSimUri(ownerID);
                List<InventoryItem> gestures;
                BackendResponse storageResponse = server.InventoryProvider.TryFetchActiveGestures(owner, out gestures);

                if (storageResponse == BackendResponse.Success)
                {
                    SerializeItemList(response.Body, gestures);
                }
                else if (storageResponse == BackendResponse.NotFound)
                {
                    // Return an empty set of gestures to match OpenSim.Grid.InventoryServer.exe behavior
                    SerializeItemList(response.Body, new List<InventoryItem>(0));
                }
                else
                {
                    response.Status = HttpStatusCode.InternalServerError;
                }
            }
            else
            {
                response.Status = HttpStatusCode.BadRequest;
            }

            return true;
        }

        BackendResponse CreateFolder(string name, UUID ownerID, UUID parentID, AssetType assetType)
        {
            InventoryFolder folder = new InventoryFolder(name, ownerID, parentID, (short)assetType);
            Uri owner = Utils.GetOpenSimUri(ownerID);
            return server.InventoryProvider.TryCreateFolder(owner, folder);
        }

        UUID DeserializeUUID(Stream stream)
        {
            UUID id = UUID.Zero;

            try
            {
                using (XmlReader reader = XmlReader.Create(stream))
                {
                    reader.MoveToContent();
                    UUID.TryParse(reader.ReadElementContentAsString("guid", String.Empty), out id);
                }
            }
            catch (Exception ex)
            {
                Logger.Log.Warn("Failed to parse POST data (expecting guid): " + ex.Message);
            }

            return id;
        }

        UUID DeserializeUUID(Stream stream, out UUID agentID, out UUID sessionID)
        {
            UUID id;

            try
            {
                using (XmlReader reader = XmlReader.Create(stream))
                {
                    reader.MoveToContent();
                    reader.ReadStartElement("RestSessionObjectOfGuid");
                    UUID.TryParse(reader.ReadElementContentAsString("SessionID", String.Empty), out sessionID);
                    UUID.TryParse(reader.ReadElementContentAsString("AvatarID", String.Empty), out agentID);
                    UUID.TryParse(reader.ReadElementContentAsString("Body", String.Empty), out id);
                    reader.ReadEndElement();
                }
            }
            catch (Exception ex)
            {
                Logger.Log.Warn("Failed to parse GetInventory POST data: " + ex.Message);
                agentID = UUID.Zero;
                sessionID = UUID.Zero;
                return UUID.Zero;
            }

            return id;
        }

        InventoryFolder DeserializeFolder(Stream stream, out UUID agentID, out UUID sessionID)
        {
            InventoryFolder folder = new InventoryFolder();

            try
            {
                using (XmlReader reader = XmlReader.Create(stream))
                {
                    reader.MoveToContent();
                    reader.ReadStartElement("RestSessionObjectOfInventoryFolderBase");
                    UUID.TryParse(reader.ReadElementContentAsString("SessionID", String.Empty), out sessionID);
                    UUID.TryParse(reader.ReadElementContentAsString("AvatarID", String.Empty), out agentID);
                    reader.ReadStartElement("Body");
                    if (reader.Name == "Name")
                        folder.Name = reader.ReadElementContentAsString("Name", String.Empty);
                    else
                        folder.Name = String.Empty;
                    ReadUUID(reader, "Owner", out folder.Owner);
                    ReadUUID(reader, "ParentID", out folder.ParentID);
                    ReadUUID(reader, "ID", out folder.ID);
                    Int16.TryParse(reader.ReadElementContentAsString("Type", String.Empty), out folder.Type);
                    UInt16.TryParse(reader.ReadElementContentAsString("Version", String.Empty), out folder.Version);
                    reader.ReadEndElement();
                    reader.ReadEndElement();
                }
            }
            catch (Exception ex)
            {
                Logger.Log.Warn("Failed to parse POST data (expecting InventoryFolderBase): " + ex.Message);
                agentID = UUID.Zero;
                sessionID = UUID.Zero;
                return null;
            }

            return folder;
        }

        InventoryItem DeserializeItem(Stream stream, out UUID agentID, out UUID sessionID)
        {
            InventoryItem item = new InventoryItem();

            try
            {
                using (XmlReader reader = XmlReader.Create(stream))
                {
                    reader.MoveToContent();
                    reader.ReadStartElement("RestSessionObjectOfInventoryItemBase");
                    UUID.TryParse(reader.ReadElementContentAsString("SessionID", String.Empty), out sessionID);
                    UUID.TryParse(reader.ReadElementContentAsString("AvatarID", String.Empty), out agentID);
                    reader.ReadStartElement("Body");
                    ReadUUID(reader, "ID", out item.ID);
                    Int32.TryParse(reader.ReadElementContentAsString("InvType", String.Empty), out item.InvType);
                    ReadUUID(reader, "Folder", out item.Folder);
                    ReadUUID(reader, "Owner", out item.Owner);
                    ReadUUID(reader, "Creator", out item.Creator);
                    item.Name = reader.ReadElementContentAsString("Name", String.Empty);
                    item.Description = reader.ReadElementContentAsString("Description", String.Empty);
                    UInt32.TryParse(reader.ReadElementContentAsString("NextPermissions", String.Empty), out item.NextPermissions);
                    UInt32.TryParse(reader.ReadElementContentAsString("CurrentPermissions", String.Empty), out item.CurrentPermissions);
                    UInt32.TryParse(reader.ReadElementContentAsString("BasePermissions", String.Empty), out item.BasePermissions);
                    UInt32.TryParse(reader.ReadElementContentAsString("EveryOnePermissions", String.Empty), out item.EveryOnePermissions);
                    UInt32.TryParse(reader.ReadElementContentAsString("GroupPermissions", String.Empty), out item.GroupPermissions);
                    Int32.TryParse(reader.ReadElementContentAsString("AssetType", String.Empty), out item.AssetType);
                    ReadUUID(reader, "AssetID", out item.AssetID);
                    ReadUUID(reader, "GroupID", out item.GroupID);
                    Boolean.TryParse(reader.ReadElementContentAsString("GroupOwned", String.Empty), out item.GroupOwned);
                    Int32.TryParse(reader.ReadElementContentAsString("SalePrice", String.Empty), out item.SalePrice);
                    Byte.TryParse(reader.ReadElementContentAsString("SaleType", String.Empty), out item.SaleType);
                    UInt32.TryParse(reader.ReadElementContentAsString("Flags", String.Empty), out item.Flags);
                    Int32.TryParse(reader.ReadElementContentAsString("CreationDate", String.Empty), out item.CreationDate);
                    reader.ReadEndElement();
                    reader.ReadEndElement();
                }
            }
            catch (Exception ex)
            {
                Logger.Log.Warn("Failed to parse POST data (expecting InventoryItemBase): " + ex.Message);
                agentID = UUID.Zero;
                sessionID = UUID.Zero;
                return null;
            }

            return item;
        }

        void SerializeBool(Stream stream, bool value)
        {
            using (XmlWriter writer = XmlWriter.Create(stream))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("boolean");
                writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");
                writer.WriteString(value.ToString().ToLower());
                writer.WriteEndElement();
                writer.WriteEndDocument();
                writer.Flush();
            }

            stream.Flush();
        }

        void SerializeFolderList(Stream stream, List<InventoryFolder> folders)
        {
            using (XmlWriter writer = XmlWriter.Create(stream))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("ArrayOfInventoryFolderBase");
                writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");

                if (folders != null)
                {
                    foreach (InventoryFolder folder in folders)
                    {
                        writer.WriteStartElement("InventoryFolderBase");
                        writer.WriteElementString("Name", folder.Name);
                        WriteUUID(writer, "Owner", folder.Owner);
                        WriteUUID(writer, "ParentID", folder.ParentID);
                        WriteUUID(writer, "ID", folder.ID);
                        writer.WriteElementString("Type", XmlConvert.ToString(folder.Type));
                        writer.WriteElementString("Version", XmlConvert.ToString(folder.Version));
                        writer.WriteEndElement();
                    }
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();

                writer.Flush();
            }

            stream.Flush();
        }

        void SerializeItemList(Stream stream, List<InventoryItem> items)
        {
            using (XmlWriter writer = XmlWriter.Create(stream))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("ArrayOfInventoryItemBase");
                writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");

                if (items != null)
                {
                    foreach (InventoryItem item in items)
                    {
                        writer.WriteStartElement("InventoryItemBase");
                        WriteUUID(writer, "ID", item.ID);
                        writer.WriteElementString("InvType", XmlConvert.ToString(item.InvType));
                        WriteUUID(writer, "Folder", item.Folder);
                        WriteUUID(writer, "Owner", item.Owner);
                        WriteUUID(writer, "Creator", item.Creator);
                        writer.WriteElementString("Name", item.Name);
                        writer.WriteElementString("Description", item.Description);
                        writer.WriteElementString("NextPermissions", XmlConvert.ToString(item.NextPermissions));
                        writer.WriteElementString("CurrentPermissions", XmlConvert.ToString(item.CurrentPermissions));
                        writer.WriteElementString("BasePermissions", XmlConvert.ToString(item.BasePermissions));
                        writer.WriteElementString("EveryOnePermissions", XmlConvert.ToString(item.EveryOnePermissions));
                        writer.WriteElementString("GroupPermissions", XmlConvert.ToString(item.GroupPermissions));
                        writer.WriteElementString("AssetType", XmlConvert.ToString(item.AssetType));
                        WriteUUID(writer, "AssetID", item.AssetID);
                        WriteUUID(writer, "GroupID", item.GroupID);
                        writer.WriteElementString("GroupOwned", XmlConvert.ToString(item.GroupOwned));
                        writer.WriteElementString("SalePrice", XmlConvert.ToString(item.SalePrice));
                        writer.WriteElementString("SaleType", XmlConvert.ToString(item.SaleType));
                        writer.WriteElementString("Flags", XmlConvert.ToString(item.Flags));
                        writer.WriteElementString("CreationDate", XmlConvert.ToString(item.CreationDate));
                        writer.WriteEndElement();
                    }
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();

                writer.Flush();
            }

            stream.Flush();
        }

        void WriteUUID(XmlWriter writer, string name, UUID id)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString("Guid", XmlConvert.ToString(id.Guid));
            writer.WriteEndElement();
        }

        void ReadUUID(XmlReader reader, string name, out UUID id)
        {
            reader.ReadStartElement(name);
            UUID.TryParse(reader.ReadElementContentAsString("Guid", String.Empty), out id);
            reader.ReadEndElement();
        }
    }

    #region OpenSim AssetType

    /// <summary>
    /// The different types of grid assets
    /// </summary>
    public enum AssetType : sbyte
    {
        /// <summary>Unknown asset type</summary>
        Unknown = -1,
        /// <summary>Texture asset, stores in JPEG2000 J2C stream format</summary>
        Texture = 0,
        /// <summary>Sound asset</summary>
        Sound = 1,
        /// <summary>Calling card for another avatar</summary>
        CallingCard = 2,
        /// <summary>Link to a location in world</summary>
        Landmark = 3,
        // <summary>Legacy script asset, you should never see one of these</summary>
        //[Obsolete]
        //Script = 4,
        /// <summary>Collection of textures and parameters that can be
        /// worn by an avatar</summary>
        Clothing = 5,
        /// <summary>Primitive that can contain textures, sounds,
        /// scripts and more</summary>
        Object = 6,
        /// <summary>Notecard asset</summary>
        Notecard = 7,
        /// <summary>Holds a collection of inventory items</summary>
        Folder = 8,
        /// <summary>Root inventory folder</summary>
        RootFolder = 9,
        /// <summary>Linden scripting language script</summary>
        LSLText = 10,
        /// <summary>LSO bytecode for a script</summary>
        LSLBytecode = 11,
        /// <summary>Uncompressed TGA texture</summary>
        TextureTGA = 12,
        /// <summary>Collection of textures and shape parameters that can
        /// be worn</summary>
        Bodypart = 13,
        /// <summary>Trash folder</summary>
        TrashFolder = 14,
        /// <summary>Snapshot folder</summary>
        SnapshotFolder = 15,
        /// <summary>Lost and found folder</summary>
        LostAndFoundFolder = 16,
        /// <summary>Uncompressed sound</summary>
        SoundWAV = 17,
        /// <summary>Uncompressed TGA non-square image, not to be used as a
        /// texture</summary>
        ImageTGA = 18,
        /// <summary>Compressed JPEG non-square image, not to be used as a
        /// texture</summary>
        ImageJPEG = 19,
        /// <summary>Animation</summary>
        Animation = 20,
        /// <summary>Sequence of animations, sounds, chat, and pauses</summary>
        Gesture = 21,
        /// <summary>Simstate file</summary>
        Simstate = 22,
    }

    #endregion OpenSim AssetType
}
