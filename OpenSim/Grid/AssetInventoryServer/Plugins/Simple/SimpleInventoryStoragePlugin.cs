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
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenMetaverse;
using OpenSim.Framework;
using log4net;

namespace OpenSim.Grid.AssetInventoryServer.Plugins.Simple
{
    public class SimpleInventoryStoragePlugin : IInventoryStorageProvider
    {
        const string EXTENSION_NAME = "SimpleInventoryStorage"; // Used for metrics reporting
        const string DEFAULT_INVENTORY_DIR = "SimpleInventory";

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        AssetInventoryServer server;
        Dictionary<Uri, InventoryCollection> inventories = new Dictionary<Uri, InventoryCollection>();
        Dictionary<Uri, List<InventoryItemBase>> activeGestures = new Dictionary<Uri, List<InventoryItemBase>>();
        Utils.InventoryItemSerializer itemSerializer = new Utils.InventoryItemSerializer();
        Utils.InventoryFolderSerializer folderSerializer = new Utils.InventoryFolderSerializer();

        public SimpleInventoryStoragePlugin()
        {
        }

        #region Required Interfaces

        public BackendResponse TryFetchItem(Uri owner, UUID itemID, out InventoryItemBase item)
        {
            item = null;
            BackendResponse ret;

            InventoryCollection collection;
            if (inventories.TryGetValue(owner, out collection) && collection.Items.TryGetValue(itemID, out item))
                ret = BackendResponse.Success;
            else
                ret = BackendResponse.NotFound;

            server.MetricsProvider.LogInventoryFetch(EXTENSION_NAME, ret, owner, itemID, false, DateTime.Now);
            return ret;
        }

        public BackendResponse TryFetchFolder(Uri owner, UUID folderID, out InventoryFolderWithChildren folder)
        {
            folder = null;
            BackendResponse ret;

            InventoryCollection collection;
            if (inventories.TryGetValue(owner, out collection) && collection.Folders.TryGetValue(folderID, out folder))
                ret = BackendResponse.Success;
            else
                ret = BackendResponse.NotFound;

            server.MetricsProvider.LogInventoryFetch(EXTENSION_NAME, ret, owner, folderID, true, DateTime.Now);
            return ret;
        }

        public BackendResponse TryFetchFolderContents(Uri owner, UUID folderID, out InventoryCollection contents)
        {
            contents = null;
            BackendResponse ret;

            InventoryCollection collection;
            InventoryFolderWithChildren folder;

            if (inventories.TryGetValue(owner, out collection) && collection.Folders.TryGetValue(folderID, out folder))
            {
                contents = new InventoryCollection();
                contents.UserID = collection.UserID;
                contents.Folders = new Dictionary<UUID, InventoryFolderWithChildren>();
                contents.Items = new Dictionary<UUID, InventoryItemBase>();

                foreach (InventoryNodeBase invBase in folder.Children.Values)
                {
                    if (invBase is InventoryItemBase)
                    {
                        InventoryItemBase invItem = invBase as InventoryItemBase;
                        contents.Items.Add(invItem.ID, invItem);
                    }
                    else
                    {
                        InventoryFolderWithChildren invFolder = invBase as InventoryFolderWithChildren;
                        contents.Folders.Add(invFolder.ID, invFolder);
                    }
                }

                ret = BackendResponse.Success;
            }
            else
            {
                ret = BackendResponse.NotFound;
            }

            server.MetricsProvider.LogInventoryFetchFolderContents(EXTENSION_NAME, ret, owner, folderID, DateTime.Now);
            return ret;
        }

        public BackendResponse TryFetchFolderList(Uri owner, out List<InventoryFolderWithChildren> folders)
        {
            folders = null;
            BackendResponse ret;

            InventoryCollection collection;
            if (inventories.TryGetValue(owner, out collection))
            {
                folders = new List<InventoryFolderWithChildren>(collection.Folders.Values);
                return BackendResponse.Success;
            }
            else
            {
                ret = BackendResponse.NotFound;
            }

            server.MetricsProvider.LogInventoryFetchFolderList(EXTENSION_NAME, ret, owner, DateTime.Now);
            return ret;
        }

        public BackendResponse TryFetchInventory(Uri owner, out InventoryCollection inventory)
        {
            inventory = null;
            BackendResponse ret;

            if (inventories.TryGetValue(owner, out inventory))
                ret = BackendResponse.Success;
            else
                ret = BackendResponse.NotFound;

            server.MetricsProvider.LogInventoryFetchInventory(EXTENSION_NAME, ret, owner, DateTime.Now);
            return ret;
        }

        public BackendResponse TryFetchActiveGestures(Uri owner, out List<InventoryItemBase> gestures)
        {
            gestures = null;
            BackendResponse ret;

            if (activeGestures.TryGetValue(owner, out gestures))
                ret = BackendResponse.Success;
            else
                ret = BackendResponse.NotFound;

            server.MetricsProvider.LogInventoryFetchActiveGestures(EXTENSION_NAME, ret, owner, DateTime.Now);
            return ret;
        }

        public BackendResponse TryCreateItem(Uri owner, InventoryItemBase item)
        {
            BackendResponse ret;

            InventoryCollection collection;
            if (inventories.TryGetValue(owner, out collection))
            {
                // Delete this item first if it already exists
                InventoryItemBase oldItem;
                if (collection.Items.TryGetValue(item.ID, out oldItem))
                    TryDeleteItem(owner, item.ID);

                try
                {
                    // Create the file
                    SaveItem(item);

                    // Add the item to the collection
                    lock (collection) collection.Items[item.ID] = item;

                    // Add the item to its parent folder
                    InventoryFolderWithChildren parent;
                    if (collection.Folders.TryGetValue(item.Folder, out parent))
                        lock (parent.Children) parent.Children.Add(item.ID, item);

                    // Add active gestures to our list
                    if (item.InvType == (int)InventoryType.Gesture && item.Flags == 1)
                    {
                        lock (activeGestures)
                            activeGestures[owner].Add(item);
                    }

                    ret = BackendResponse.Success;
                }
                catch (Exception ex)
                {
                    m_log.Error("[SIMPLEINVENTORYSTORAGE]: " + ex.Message);
                    ret = BackendResponse.Failure;
                }
            }
            else
            {
                return BackendResponse.NotFound;
            }

            server.MetricsProvider.LogInventoryCreate(EXTENSION_NAME, ret, owner, false, DateTime.Now);
            return ret;
        }

        public BackendResponse TryCreateFolder(Uri owner, InventoryFolderWithChildren folder)
        {
            BackendResponse ret;

            InventoryCollection collection;
            if (inventories.TryGetValue(owner, out collection))
            {
                // Delete this folder first if it already exists
                InventoryFolderWithChildren oldFolder;
                if (collection.Folders.TryGetValue(folder.ID, out oldFolder))
                    TryDeleteFolder(owner, folder.ID);

                try
                {
                    // Create the file
                    SaveFolder(folder);

                    // Add the folder to the collection
                    lock (collection) collection.Folders[folder.ID] = folder;

                    // Add the folder to its parent folder
                    InventoryFolderWithChildren parent;
                    if (collection.Folders.TryGetValue(folder.ParentID, out parent))
                        lock (parent.Children) parent.Children.Add(folder.ID, folder);

                    ret = BackendResponse.Success;
                }
                catch (Exception ex)
                {
                    m_log.Error("[SIMPLEINVENTORYSTORAGE]: " + ex.Message);
                    ret = BackendResponse.Failure;
                }
            }
            else
            {
                ret = BackendResponse.NotFound;
            }

            server.MetricsProvider.LogInventoryCreate(EXTENSION_NAME, ret, owner, true, DateTime.Now);
            return ret;
        }

        public BackendResponse TryCreateInventory(Uri owner, InventoryFolderWithChildren rootFolder)
        {
            BackendResponse ret;

            lock (inventories)
            {
                if (!inventories.ContainsKey(owner))
                {
                    InventoryCollection collection = new InventoryCollection();
                    collection.UserID = rootFolder.Owner;
                    collection.Folders = new Dictionary<UUID, InventoryFolderWithChildren>();
                    collection.Folders.Add(rootFolder.ID, rootFolder);
                    collection.Items = new Dictionary<UUID, InventoryItemBase>();

                    inventories.Add(owner, collection);

                    ret = BackendResponse.Success;
                }
                else
                {
                    ret = BackendResponse.Failure;
                }
            }

            if (ret == BackendResponse.Success)
            {
                string path = Path.Combine(DEFAULT_INVENTORY_DIR, rootFolder.Owner.ToString());
                try
                {
                    // Create the directory for this agent
                    Directory.CreateDirectory(path);

                    // Create an index.txt containing the UUID and URI for this agent
                    string[] index = new string[] { rootFolder.Owner.ToString(), owner.ToString() };
                    File.WriteAllLines(Path.Combine(path, "index.txt"), index);

                    // Create the root folder file
                    SaveFolder(rootFolder);
                }
                catch (Exception ex)
                {
                    m_log.Error("[SIMPLEINVENTORYSTORAGE]: " + ex.Message);
                    ret = BackendResponse.Failure;
                }
            }

            server.MetricsProvider.LogInventoryCreateInventory(EXTENSION_NAME, ret, DateTime.Now);
            return ret;
        }

        public BackendResponse TryDeleteItem(Uri owner, UUID itemID)
        {
            BackendResponse ret;

            InventoryCollection collection;
            InventoryItemBase item;
            if (inventories.TryGetValue(owner, out collection) && collection.Items.TryGetValue(itemID, out item))
            {
                // Remove the item from its parent folder
                InventoryFolderWithChildren parent;
                if (collection.Folders.TryGetValue(item.Folder, out parent))
                    lock (parent.Children) parent.Children.Remove(itemID);

                // Remove the item from the collection
                lock (collection) collection.Items.Remove(itemID);

                // Remove from the active gestures list if applicable
                if (item.InvType == (int)InventoryType.Gesture)
                {
                    lock (activeGestures)
                    {
                        for (int i = 0; i < activeGestures[owner].Count; i++)
                        {
                            if (activeGestures[owner][i].ID == itemID)
                            {
                                activeGestures[owner].RemoveAt(i);
                                break;
                            }
                        }
                    }
                }

                // Delete the file. We don't know exactly what the file name is,
                // so search for it
                string path = PathFromURI(owner);
                string[] matches = Directory.GetFiles(path, String.Format("*{0}.item", itemID), SearchOption.TopDirectoryOnly);
                foreach (string match in matches)
                {
                    try { File.Delete(match); }
                    catch (Exception ex) { m_log.ErrorFormat("[SIMPLEINVENTORYSTORAGE]: Failed to delete file {0}: {1}", match, ex.Message); }
                }

                ret = BackendResponse.Success;
            }
            else
            {
                ret = BackendResponse.NotFound;
            }

            server.MetricsProvider.LogInventoryDelete(EXTENSION_NAME, ret, owner, itemID, false, DateTime.Now);
            return ret;
        }

        public BackendResponse TryDeleteFolder(Uri owner, UUID folderID)
        {
            BackendResponse ret;

            InventoryCollection collection;
            InventoryFolderWithChildren folder;
            if (inventories.TryGetValue(owner, out collection) && collection.Folders.TryGetValue(folderID, out folder))
            {
                // Remove the folder from its parent folder
                InventoryFolderWithChildren parent;
                if (collection.Folders.TryGetValue(folder.ParentID, out parent))
                    lock (parent.Children) parent.Children.Remove(folderID);

                // Remove the folder from the collection
                lock (collection) collection.Items.Remove(folderID);

                // Delete the folder file. We don't know exactly what the file name is,
                // so search for it
                string path = PathFromURI(owner);
                string[] matches = Directory.GetFiles(path, String.Format("*{0}.folder", folderID), SearchOption.TopDirectoryOnly);
                foreach (string match in matches)
                {
                    try { File.Delete(match); }
                    catch (Exception ex) { m_log.ErrorFormat("[SIMPLEINVENTORYSTORAGE]: Failed to delete folder file {0}: {1}", match, ex.Message); }
                }

                ret = BackendResponse.Success;
            }
            else
            {
                ret = BackendResponse.NotFound;
            }

            server.MetricsProvider.LogInventoryDelete(EXTENSION_NAME, ret, owner, folderID, true, DateTime.Now);
            return ret;
        }

        public BackendResponse TryPurgeFolder(Uri owner, UUID folderID)
        {
            BackendResponse ret;

            InventoryCollection collection;
            InventoryFolderWithChildren folder;
            if (inventories.TryGetValue(owner, out collection) && collection.Folders.TryGetValue(folderID, out folder))
            {
                // Delete all of the folder children
                foreach (InventoryNodeBase obj in new List<InventoryNodeBase>(folder.Children.Values))
                {
                    if (obj is InventoryItemBase)
                    {
                        TryDeleteItem(owner, (obj as InventoryItemBase).ID);
                    }
                    else
                    {
                        InventoryFolderWithChildren childFolder = obj as InventoryFolderWithChildren;
                        TryPurgeFolder(owner, childFolder.ID);
                        TryDeleteFolder(owner, childFolder.ID);
                    }
                }

                ret = BackendResponse.Success;
            }
            else
            {
                ret = BackendResponse.NotFound;
            }

            server.MetricsProvider.LogInventoryPurgeFolder(EXTENSION_NAME, ret, owner, folderID, DateTime.Now);
            return ret;
        }

        #endregion Required Interfaces

        void SaveItem(InventoryItemBase item)
        {
            string filename = String.Format("{0}-{1}.item", SanitizeFilename(item.Name), item.ID);

            string path = Path.Combine(DEFAULT_INVENTORY_DIR, item.Owner.ToString());
            path = Path.Combine(path, filename);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                itemSerializer.Serialize(stream, item);
                stream.Flush();
            }
        }

        void SaveFolder(InventoryFolderWithChildren folder)
        {
            string filename = String.Format("{0}-{1}.folder", SanitizeFilename(folder.Name), folder.ID);

            string path = Path.Combine(DEFAULT_INVENTORY_DIR, folder.Owner.ToString());
            path = Path.Combine(path, filename);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                folderSerializer.Serialize(stream, folder);
                stream.Flush();
            }
        }

        string SanitizeFilename(string filename)
        {
            string output = filename;

            if (output.Length > 64)
                output = output.Substring(0, 64);

            foreach (char i in Path.GetInvalidFileNameChars())
                output = output.Replace(i, '_');

            return output;
        }

        static string PathFromURI(Uri uri)
        {
            byte[] hash = OpenMetaverse.Utils.SHA1(Encoding.UTF8.GetBytes(uri.ToString()));
            StringBuilder digest = new StringBuilder(40);

            // Convert the hash to a hex string
            foreach (byte b in hash)
                digest.AppendFormat(OpenMetaverse.Utils.EnUsCulture, "{0:x2}", b);

            return Path.Combine(DEFAULT_INVENTORY_DIR, digest.ToString());
        }

        void LoadFiles(string folder)
        {
            // Try to create the directory if it doesn't already exist
            if (!Directory.Exists(folder))
            {
                try { Directory.CreateDirectory(folder); }
                catch (Exception ex)
                {
                    m_log.Warn("[SIMPLEINVENTORYSTORAGE]: " + ex.Message);
                    return;
                }
            }

            try
            {
                string[] agentFolders = Directory.GetDirectories(DEFAULT_INVENTORY_DIR);

                for (int i = 0; i < agentFolders.Length; i++)
                {
                    string foldername = agentFolders[i];
                    string indexPath = Path.Combine(foldername, "index.txt");
                    UUID ownerID = UUID.Zero;
                    Uri owner = null;

                    try
                    {
                        string[] index = File.ReadAllLines(indexPath);
                        ownerID = UUID.Parse(index[0]);
                        owner = new Uri(index[1]);
                    }
                    catch (Exception ex)
                    {
                        m_log.WarnFormat("[SIMPLEINVENTORYSTORAGE]: Failed loading the index file {0}: {1}", indexPath, ex.Message);
                    }

                    if (ownerID != UUID.Zero && owner != null)
                    {
                        // Initialize the active gestures list for this agent
                        activeGestures.Add(owner, new List<InventoryItemBase>());

                        InventoryCollection collection = new InventoryCollection();
                        collection.UserID = ownerID;

                        // Load all of the folders for this agent
                        string[] folders = Directory.GetFiles(foldername, "*.folder", SearchOption.TopDirectoryOnly);
                        collection.Folders = new Dictionary<UUID,InventoryFolderWithChildren>(folders.Length);

                        for (int j = 0; j < folders.Length; j++)
                        {
                            InventoryFolderWithChildren invFolder = (InventoryFolderWithChildren)folderSerializer.Deserialize(
                                new FileStream(folders[j], FileMode.Open, FileAccess.Read));
                            collection.Folders[invFolder.ID] = invFolder;
                        }

                        // Iterate over the folders collection, adding children to their parents
                        foreach (InventoryFolderWithChildren invFolder in collection.Folders.Values)
                        {
                            InventoryFolderWithChildren parent;
                            if (collection.Folders.TryGetValue(invFolder.ParentID, out parent))
                                parent.Children[invFolder.ID] = invFolder;
                        }

                        // Load all of the items for this agent
                        string[] files = Directory.GetFiles(foldername, "*.item", SearchOption.TopDirectoryOnly);
                        collection.Items = new Dictionary<UUID, InventoryItemBase>(files.Length);

                        for (int j = 0; j < files.Length; j++)
                        {
                            InventoryItemBase invItem = (InventoryItemBase)itemSerializer.Deserialize(
                                new FileStream(files[j], FileMode.Open, FileAccess.Read));
                            collection.Items[invItem.ID] = invItem;

                            // Add items to their parent folders
                            InventoryFolderWithChildren parent;
                            if (collection.Folders.TryGetValue(invItem.Folder, out parent))
                                parent.Children[invItem.ID] = invItem;

                            // Add active gestures to our list
                            if (invItem.InvType == (int)InventoryType.Gesture && invItem.Flags != 0)
                                activeGestures[owner].Add(invItem);
                        }

                        inventories.Add(owner, collection);
                    }
                }
            }
            catch (Exception ex)
            {
                m_log.ErrorFormat("[SIMPLEINVENTORYSTORAGE]: Failed loading inventory from {0}: {1}", folder, ex.Message);
            }
        }

        #region IPlugin implementation

        public void Initialise(AssetInventoryServer server)
        {
            this.server = server;

            LoadFiles(DEFAULT_INVENTORY_DIR);

            m_log.InfoFormat("[SIMPLEINVENTORYSTORAGE]: Initialized the inventory index with data for {0} avatars",
                inventories.Count);
        }

        /// <summary>
        /// <para>Initialises asset interface</para>
        /// </summary>
        public void Initialise()
        {
            m_log.InfoFormat("[SIMPLEINVENTORYSTORAGE]: {0} cannot be default-initialized!", Name);
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
            get { return "SimpleInventoryStorage"; }
        }

        #endregion IPlugin implementation
    }
}
