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
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using log4net;

namespace OpenSim.Grid.AssetInventoryServer.Plugins.OpenSim
{
    public class OpenSimInventoryFrontendPlugin : IAssetInventoryServerPlugin
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private AssetInventoryServer m_server;
        private Utils.InventoryCollectionSerializer collectionSerializer = new Utils.InventoryCollectionSerializer();

        public OpenSimInventoryFrontendPlugin()
        {
        }

        #region IPlugin implementation

        public void Initialise(AssetInventoryServer server)
        {
            m_server = server;

            m_server.HttpServer.AddStreamHandler(new GetInventoryHandler(server, collectionSerializer));
            m_server.HttpServer.AddStreamHandler(new CreateInventoryHandler(server));
            m_server.HttpServer.AddStreamHandler(new NewFolderHandler(server));
            m_server.HttpServer.AddStreamHandler(new UpdateFolderHandler(server));
            m_server.HttpServer.AddStreamHandler(new MoveFolderHandler(server));
            m_server.HttpServer.AddStreamHandler(new PurgeFolderHandler(server));
            m_server.HttpServer.AddStreamHandler(new NewItemHandler(server));
            m_server.HttpServer.AddStreamHandler(new DeleteItemHandler(server));
            m_server.HttpServer.AddStreamHandler(new RootFoldersHandler(server));
            m_server.HttpServer.AddStreamHandler(new ActiveGesturesHandler(server));

            m_log.Info("[OPENSIMINVENTORYFRONTEND]: OpenSim Inventory Frontend loaded.");
        }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        public void Initialise()
        {
            m_log.InfoFormat("[OPENSIMINVENTORYFRONTEND]: {0} cannot be default-initialized!", Name);
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
            get { return "OpenSimInventoryFrontend"; }
        }

        #endregion IPlugin implementation

        public class GetInventoryHandler : BaseStreamHandler
        {
            AssetInventoryServer m_server;
            Utils.InventoryCollectionSerializer m_collectionSerializer;

            //public GetInventoryHandler(AssetInventoryServer server, Utils.InventoryCollectionSerializer collectionSerializer) : base("POST", @"^/GetInventory/")
            public GetInventoryHandler(AssetInventoryServer server, Utils.InventoryCollectionSerializer collectionSerializer) : base("POST", "/GetInventory")
            {
                m_server = server;
                m_collectionSerializer = collectionSerializer;
            }

            public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                byte[] buffer = new byte[] {};
                UUID sessionID, agentID;
                UUID ownerID = DeserializeUUID(httpRequest.InputStream, out agentID, out sessionID);

                if (ownerID != UUID.Zero)
                {
                    m_log.Warn("[OPENSIMINVENTORYFRONTEND]: GetInventory is not scalable on some inventory backends, avoid calling it wherever possible");

                    Uri owner = Utils.GetOpenSimUri(ownerID);
                    InventoryCollection inventory;
                    BackendResponse storageResponse = m_server.InventoryProvider.TryFetchInventory(owner, out inventory);

                    if (storageResponse == BackendResponse.Success)
                    {
                        //collectionSerializer.Serialize(httpResponse.Body, inventory);
                        //httpResponse.Body.Flush();
                        MemoryStream ms = new MemoryStream();
                        m_collectionSerializer.Serialize(ms, inventory);
                        ms.Seek(0, SeekOrigin.Begin);
                        buffer = ms.GetBuffer();
                        Array.Resize<byte>(ref buffer, (int) ms.Length);
                        ms.Close();
                        httpResponse.StatusCode = (int) HttpStatusCode.OK;
                    }
                    else if (storageResponse == BackendResponse.NotFound)
                    {
                        // Return an empty inventory set to mimic OpenSim.Grid.InventoryServer.exe
                        inventory = new InventoryCollection();
                        inventory.UserID = ownerID;
                        inventory.Folders = new Dictionary<UUID, InventoryFolderWithChildren>();
                        inventory.Items = new Dictionary<UUID, InventoryItemBase>();
                        //collectionSerializer.Serialize(httpResponse.Body, inventory);
                        //httpResponse.Body.Flush();
                        MemoryStream ms = new MemoryStream();
                        m_collectionSerializer.Serialize(ms, inventory);
                        ms.Seek(0, SeekOrigin.Begin);
                        buffer = ms.GetBuffer();
                        Array.Resize<byte>(ref buffer, (int) ms.Length);
                        ms.Close();
                        httpResponse.StatusCode = (int) HttpStatusCode.OK;
                    }
                    else
                    {
                        httpResponse.StatusCode = (int) HttpStatusCode.InternalServerError;
                    }
                }
                else
                {
                    httpResponse.StatusCode = (int) HttpStatusCode.BadRequest;
                }

                return buffer;
            }
        }

        public class CreateInventoryHandler : BaseStreamHandler
        {
            AssetInventoryServer m_server;

            //public CreateInventoryHandler(AssetInventoryServer server) : base("POST", @"^/CreateInventory/")
            public CreateInventoryHandler(AssetInventoryServer server) : base("POST", "/CreateInventory")
            {
                m_server = server;
            }

            public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                UUID ownerID = DeserializeUUID(httpRequest.InputStream);

                if (ownerID != UUID.Zero)
                {
                    Uri owner = Utils.GetOpenSimUri(ownerID);
                    m_log.DebugFormat("[OPENSIMINVENTORYFRONTEND]: Created URI {0} for inventory creation", owner);

                    InventoryFolderWithChildren rootFolder = new InventoryFolderWithChildren("My Inventory", ownerID, UUID.Zero, (short)AssetType.Folder);
                    BackendResponse storageResponse = m_server.InventoryProvider.TryCreateInventory(owner, rootFolder);
                    if (storageResponse == BackendResponse.Success)
                    {
                        // TODO: The CreateFolder calls need to be executed in SimpleStorage.
                        //CreateFolder("Animations", ownerID, rootFolder.ID, AssetType.Animation);
                        //CreateFolder("Body Parts", ownerID, rootFolder.ID, AssetType.Bodypart);
                        //CreateFolder("Calling Cards", ownerID, rootFolder.ID, AssetType.CallingCard);
                        //CreateFolder("Clothing", ownerID, rootFolder.ID, AssetType.Clothing);
                        //CreateFolder("Gestures", ownerID, rootFolder.ID, AssetType.Gesture);
                        //CreateFolder("Landmarks", ownerID, rootFolder.ID, AssetType.Landmark);
                        //CreateFolder("Lost and Found", ownerID, rootFolder.ID, AssetType.LostAndFoundFolder);
                        //CreateFolder("Notecards", ownerID, rootFolder.ID, AssetType.Notecard);
                        //CreateFolder("Objects", ownerID, rootFolder.ID, AssetType.Object);
                        //CreateFolder("Photo Album", ownerID, rootFolder.ID, AssetType.SnapshotFolder);
                        //CreateFolder("Scripts", ownerID, rootFolder.ID, AssetType.LSLText);
                        //CreateFolder("Sounds", ownerID, rootFolder.ID, AssetType.Sound);
                        //CreateFolder("Textures", ownerID, rootFolder.ID, AssetType.Texture);
                        //CreateFolder("Trash", ownerID, rootFolder.ID, AssetType.TrashFolder);

                        return SerializeBool(true);
                    }
                }

                return SerializeBool(false);
            }
        }

        public class NewFolderHandler : BaseStreamHandler
        {
            AssetInventoryServer m_server;

            //public NewFolderHandler(AssetInventoryServer server) : base("POST", @"^/NewFolder/")
            public NewFolderHandler(AssetInventoryServer server) : base("POST", "/NewFolder")
            {
                m_server = server;
            }

            public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                UUID agentID, sessionID;
                InventoryFolderWithChildren folder = DeserializeFolder(httpRequest.InputStream, out agentID, out sessionID);

                if (folder != null)
                {
                    Uri owner = Utils.GetOpenSimUri(folder.Owner);

                    // Some calls that are moving or updating a folder instead
                    // of creating a new one will pass in an InventoryFolder
                    // without the name set and type set to 0. If this is the
                    // case we need to look up the name first and preserver
                    // it's type.
                    if (String.IsNullOrEmpty(folder.Name))
                    {
                        InventoryFolderWithChildren oldFolder;
                        if (m_server.InventoryProvider.TryFetchFolder(owner, folder.ID, out oldFolder) == BackendResponse.Success)
                        {
                            folder.Name = oldFolder.Name;
                            folder.Type = oldFolder.Type;
                        }
                    }

                    BackendResponse storageResponse = m_server.InventoryProvider.TryCreateFolder(owner, folder);

                    if (storageResponse == BackendResponse.Success)
                    {
                        return SerializeBool(true);
                    }
                }

                return SerializeBool(false);
            }
        }

        public class UpdateFolderHandler : BaseStreamHandler
        {
            AssetInventoryServer m_server;

            //public UpdateFolderHandler(AssetInventoryServer server) : base("POST", @"^/UpdateFolder/")
            public UpdateFolderHandler(AssetInventoryServer server) : base("POST", "/UpdateFolder")
            {
                m_server = server;
            }

            public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                return new NewFolderHandler(m_server).Handle(path, request, httpRequest, httpResponse);
            }
        }

        public class MoveFolderHandler : BaseStreamHandler
        {
            AssetInventoryServer m_server;

            //public MoveFolderHandler(AssetInventoryServer server) : base("POST", @"^/MoveFolder/")
            public MoveFolderHandler(AssetInventoryServer server) : base("POST", "/MoveFolder")
            {
                m_server = server;
            }

            public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                return new NewFolderHandler(m_server).Handle(path, request, httpRequest, httpResponse);
            }
        }

        public class PurgeFolderHandler : BaseStreamHandler
        {
            AssetInventoryServer m_server;

            //public PurgeFolderHandler(AssetInventoryServer server) : base("POST", @"^/PurgeFolder/")
            public PurgeFolderHandler(AssetInventoryServer server) : base("POST", "/PurgeFolder")
            {
                m_server = server;
            }

            public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                UUID agentID, sessionID;
                InventoryFolderWithChildren folder = DeserializeFolder(httpRequest.InputStream, out agentID, out sessionID);

                if (folder != null)
                {
                    Uri owner = Utils.GetOpenSimUri(folder.Owner);
                    BackendResponse storageResponse = m_server.InventoryProvider.TryPurgeFolder(owner, folder.ID);

                    if (storageResponse == BackendResponse.Success)
                    {
                        return SerializeBool(true);
                    }
                }

                return SerializeBool(false);
            }
        }

        public class NewItemHandler : BaseStreamHandler
        {
            AssetInventoryServer m_server;

            //public NewItemHandler(AssetInventoryServer server) : base("POST", @"^/NewItem/")
            public NewItemHandler(AssetInventoryServer server) : base("POST", "/NewItem")
            {
                m_server = server;
            }

            public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                UUID agentID, sessionID;
                InventoryItemBase item = DeserializeItem(httpRequest.InputStream, out agentID, out sessionID);

                if (item != null)
                {
                    Uri owner = Utils.GetOpenSimUri(agentID);
                    BackendResponse storageResponse = m_server.InventoryProvider.TryCreateItem(owner, item);

                    if (storageResponse == BackendResponse.Success)
                    {
                        return SerializeBool(true);
                    }
                }

                return SerializeBool(false);
            }
        }

        public class DeleteItemHandler : BaseStreamHandler
        {
            AssetInventoryServer m_server;

            //public DeleteItemHandler(AssetInventoryServer server) : base("POST", @"^/DeleteItem/")
            public DeleteItemHandler(AssetInventoryServer server) : base("POST", "/DeleteItem")
            {
                m_server = server;
            }

            public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                UUID agentID, sessionID;
                InventoryItemBase item = DeserializeItem(httpRequest.InputStream, out agentID, out sessionID);

                if (item != null)
                {
                    Uri owner = Utils.GetOpenSimUri(item.Owner);
                    BackendResponse storageResponse = m_server.InventoryProvider.TryDeleteItem(owner, item.ID);

                    if (storageResponse == BackendResponse.Success)
                    {
                        return SerializeBool(true);
                    }
                }

                return SerializeBool(false);
            }
        }

        public class RootFoldersHandler : BaseStreamHandler
        {
            AssetInventoryServer m_server;

            //public RootFoldersHandler(AssetInventoryServer server) : base("POST", @"^/RootFolders/")
            public RootFoldersHandler(AssetInventoryServer server) : base("POST", "/RootFolders")
            {
                m_server = server;
            }

            public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                byte[] buffer = new byte[] {};
                UUID ownerID = DeserializeUUID(httpRequest.InputStream);

                if (ownerID != UUID.Zero)
                {
                    Uri owner = Utils.GetOpenSimUri(ownerID);
                    List<InventoryFolderWithChildren> skeleton;
                    BackendResponse storageResponse = m_server.InventoryProvider.TryFetchFolderList(owner, out skeleton);

                    if (storageResponse == BackendResponse.Success)
                    {
                        MemoryStream ms = new MemoryStream();
                        SerializeFolderList(ms, skeleton);
                        ms.Seek(0, SeekOrigin.Begin);
                        buffer = ms.GetBuffer();
                        Array.Resize<byte>(ref buffer, (int) ms.Length);
                        ms.Close();
                        httpResponse.StatusCode = (int) HttpStatusCode.OK;
                    }
                    else if (storageResponse == BackendResponse.NotFound)
                    {
                        // Return an empty set of inventory so the requester knows that
                        // an inventory needs to be created for this agent
                        MemoryStream ms = new MemoryStream();
                        SerializeFolderList(ms, new List<InventoryFolderWithChildren>(0));
                        ms.Seek(0, SeekOrigin.Begin);
                        buffer = ms.GetBuffer();
                        Array.Resize<byte>(ref buffer, (int) ms.Length);
                        ms.Close();
                        httpResponse.StatusCode = (int) HttpStatusCode.OK;
                    }
                    else
                    {
                        httpResponse.StatusCode = (int) HttpStatusCode.InternalServerError;
                    }
                }
                else
                {
                    httpResponse.StatusCode = (int) HttpStatusCode.BadRequest;
                }

                return buffer;
            }
        }

        public class ActiveGesturesHandler : BaseStreamHandler
        {
            AssetInventoryServer m_server;

            //public ActiveGesturesHandler(AssetInventoryServer server) : base("POST", @"^/ActiveGestures/")
            public ActiveGesturesHandler(AssetInventoryServer server) : base("POST", "/ActiveGestures")
            {
                m_server = server;
            }

            public override byte[] Handle(string path, Stream request, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
            {
                byte[] buffer = new byte[] {};
                UUID ownerID = DeserializeUUID(httpRequest.InputStream);

                if (ownerID != UUID.Zero)
                {
                    Uri owner = Utils.GetOpenSimUri(ownerID);
                    List<InventoryItemBase> gestures;
                    BackendResponse storageResponse = m_server.InventoryProvider.TryFetchActiveGestures(owner, out gestures);

                    if (storageResponse == BackendResponse.Success)
                    {
                        MemoryStream ms = new MemoryStream();
                        SerializeItemList(ms, gestures);
                        ms.Seek(0, SeekOrigin.Begin);
                        buffer = ms.GetBuffer();
                        Array.Resize<byte>(ref buffer, (int) ms.Length);
                        ms.Close();
                        httpResponse.StatusCode = (int) HttpStatusCode.OK;
                    }
                    else if (storageResponse == BackendResponse.NotFound)
                    {
                        // Return an empty set of gestures to match OpenSim.Grid.InventoryServer.exe behavior
                        MemoryStream ms = new MemoryStream();
                        SerializeItemList(ms, new List<InventoryItemBase>(0));
                        ms.Seek(0, SeekOrigin.Begin);
                        buffer = ms.GetBuffer();
                        Array.Resize<byte>(ref buffer, (int) ms.Length);
                        ms.Close();
                        httpResponse.StatusCode = (int) HttpStatusCode.OK;
                    }
                    else
                    {
                        httpResponse.StatusCode = (int) HttpStatusCode.InternalServerError;
                    }
                }
                else
                {
                    httpResponse.StatusCode = (int) HttpStatusCode.BadRequest;
                }

                return buffer;
            }
        }

        //BackendResponse CreateFolder(string name, UUID ownerID, UUID parentID, AssetType assetType)
        //{
        //    InventoryFolder folder = new InventoryFolder(name, ownerID, parentID, (short)assetType);
        //    Uri owner = Utils.GetOpenSimUri(ownerID);
        //    return m_server.InventoryProvider.TryCreateFolder(owner, folder);
        //}

        private static UUID DeserializeUUID(Stream stream)
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
                m_log.Warn("[OPENSIMINVENTORYFRONTEND]: Failed to parse POST data (expecting guid): " + ex.Message);
            }

            return id;
        }

        private static UUID DeserializeUUID(Stream stream, out UUID agentID, out UUID sessionID)
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
                m_log.Warn("[OPENSIMINVENTORYFRONTEND]: Failed to parse GetInventory POST data: " + ex.Message);
                agentID = UUID.Zero;
                sessionID = UUID.Zero;
                return UUID.Zero;
            }

            return id;
        }

        private static InventoryFolderWithChildren DeserializeFolder(Stream stream, out UUID agentID, out UUID sessionID)
        {
            InventoryFolderWithChildren folder = new InventoryFolderWithChildren();

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

                    UUID dummyUUID;
                    ReadUUID(reader, "ID", out dummyUUID);
                    folder.ID = dummyUUID;
                    ReadUUID(reader, "Owner", out dummyUUID);
                    folder.Owner = dummyUUID;
                    ReadUUID(reader, "ParentID", out dummyUUID);
                    folder.ParentID = dummyUUID;

                    short dummyType;
                    Int16.TryParse(reader.ReadElementContentAsString("Type", String.Empty), out dummyType);
                    folder.Type = dummyType;

                    ushort dummyVersion;
                    UInt16.TryParse(reader.ReadElementContentAsString("Version", String.Empty), out dummyVersion);
                    folder.Version = dummyVersion;

                    reader.ReadEndElement();
                    reader.ReadEndElement();
                }
            }
            catch (Exception ex)
            {
                m_log.Warn("[OPENSIMINVENTORYFRONTEND]: Failed to parse POST data (expecting InventoryFolderBase): " + ex.Message);
                agentID = UUID.Zero;
                sessionID = UUID.Zero;
                return null;
            }

            return folder;
        }

        private static InventoryItemBase DeserializeItem(Stream stream, out UUID agentID, out UUID sessionID)
        {
            InventoryItemBase item = new InventoryItemBase();

            try
            {
                using (XmlReader reader = XmlReader.Create(stream))
                {
                    reader.MoveToContent();
                    reader.ReadStartElement("RestSessionObjectOfInventoryItemBase");
                    UUID.TryParse(reader.ReadElementContentAsString("SessionID", String.Empty), out sessionID);
                    UUID.TryParse(reader.ReadElementContentAsString("AvatarID", String.Empty), out agentID);
                    reader.ReadStartElement("Body");

                    item.Name = reader.ReadElementContentAsString("Name", String.Empty);

                    UUID dummyUUID;
                    ReadUUID(reader, "ID", out dummyUUID);
                    item.ID = dummyUUID;

                    ReadUUID(reader, "Owner", out dummyUUID);
                    item.Owner = dummyUUID;

                    int dummyInt;
                    Int32.TryParse(reader.ReadElementContentAsString("InvType", String.Empty), out dummyInt);
                    item.InvType = dummyInt;

                    ReadUUID(reader, "Folder", out dummyUUID);
                    item.Folder = dummyUUID;

                    item.CreatorId = reader.ReadElementContentAsString("CreatorId", String.Empty);
                    item.Description = reader.ReadElementContentAsString("Description", String.Empty);

                    uint dummyUInt;
                    UInt32.TryParse(reader.ReadElementContentAsString("NextPermissions", String.Empty), out dummyUInt);
                    item.NextPermissions = dummyUInt;
                    UInt32.TryParse(reader.ReadElementContentAsString("CurrentPermissions", String.Empty), out dummyUInt);
                    item.CurrentPermissions = dummyUInt;
                    UInt32.TryParse(reader.ReadElementContentAsString("BasePermissions", String.Empty), out dummyUInt);
                    item.BasePermissions = dummyUInt;
                    UInt32.TryParse(reader.ReadElementContentAsString("EveryOnePermissions", String.Empty), out dummyUInt);
                    item.EveryOnePermissions = dummyUInt;
                    UInt32.TryParse(reader.ReadElementContentAsString("GroupPermissions", String.Empty), out dummyUInt);
                    item.GroupPermissions = dummyUInt;

                    Int32.TryParse(reader.ReadElementContentAsString("AssetType", String.Empty), out dummyInt);
                    item.AssetType = dummyInt;

                    ReadUUID(reader, "AssetID", out dummyUUID);
                    item.AssetID = dummyUUID;
                    ReadUUID(reader, "GroupID", out dummyUUID);
                    item.GroupID = dummyUUID;

                    bool dummyBool;
                    Boolean.TryParse(reader.ReadElementContentAsString("GroupOwned", String.Empty), out dummyBool);
                    item.GroupOwned = dummyBool;

                    Int32.TryParse(reader.ReadElementContentAsString("SalePrice", String.Empty), out dummyInt);
                    item.SalePrice = dummyInt;

                    byte dummyByte;
                    Byte.TryParse(reader.ReadElementContentAsString("SaleType", String.Empty), out dummyByte);
                    item.SaleType = dummyByte;

                    UInt32.TryParse(reader.ReadElementContentAsString("Flags", String.Empty), out dummyUInt);
                    item.Flags = dummyUInt;

                    Int32.TryParse(reader.ReadElementContentAsString("CreationDate", String.Empty), out dummyInt);
                    item.CreationDate = dummyInt;

                    reader.ReadEndElement();
                    reader.ReadEndElement();
                }
            }
            catch (Exception ex)
            {
                m_log.Warn("[OPENSIMINVENTORYFRONTEND]: Failed to parse POST data (expecting InventoryItemBase): " + ex.Message);
                agentID = UUID.Zero;
                sessionID = UUID.Zero;
                return null;
            }

            return item;
        }

        private static byte[] SerializeBool(bool value)
        {
            byte[] buffer;
            MemoryStream ms = new MemoryStream();

            using (XmlWriter writer = XmlWriter.Create(ms))
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

            ms.Seek(0, SeekOrigin.Begin);
            buffer = ms.GetBuffer();
            Array.Resize<byte>(ref buffer, (int) ms.Length);
            ms.Close();

            return buffer;
        }

        private static void SerializeFolderList(Stream stream, List<InventoryFolderWithChildren> folders)
        {
            using (XmlWriter writer = XmlWriter.Create(stream))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("ArrayOfInventoryFolderBase");
                writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");

                if (folders != null)
                {
                    foreach (InventoryFolderWithChildren folder in folders)
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

        private static void SerializeItemList(Stream stream, List<InventoryItemBase> items)
        {
            using (XmlWriter writer = XmlWriter.Create(stream))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("ArrayOfInventoryItemBase");
                writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");

                if (items != null)
                {
                    foreach (InventoryItemBase item in items)
                    {
                        writer.WriteStartElement("InventoryItemBase");
                        WriteUUID(writer, "ID", item.ID);
                        writer.WriteElementString("InvType", XmlConvert.ToString(item.InvType));
                        WriteUUID(writer, "Folder", item.Folder);
                        WriteUUID(writer, "Owner", item.Owner);
                        writer.WriteElementString("Creator", item.CreatorId);
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

        private static void WriteUUID(XmlWriter writer, string name, UUID id)
        {
            writer.WriteStartElement(name);
            writer.WriteElementString("Guid", XmlConvert.ToString(id.Guid));
            writer.WriteEndElement();
        }

        private static void ReadUUID(XmlReader reader, string name, out UUID id)
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
