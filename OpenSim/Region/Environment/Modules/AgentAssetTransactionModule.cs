using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class AgentAssetTransactionModule : IRegionModule, IAgentAssetTransactions
    {
        private Dictionary<LLUUID, Scene> RegisteredScenes = new Dictionary<LLUUID, Scene>();
        private Scene m_scene = null;
        private bool m_dumpAssetsToFile = false;

        private AgentAssetTransactionsManager m_transactionManager;

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (!RegisteredScenes.ContainsKey(scene.RegionInfo.RegionID))
            {
                RegisteredScenes.Add(scene.RegionInfo.RegionID, scene);
                scene.RegisterModuleInterface<IAgentAssetTransactions>(this);

                scene.EventManager.OnNewClient += NewClient;

                try
                {
                    m_dumpAssetsToFile = config.Configs["StandAlone"].GetBoolean("dump_assets_to_file", false);
                }
                catch (Exception)
                {
                }
            }

            if (m_scene == null)
            {
                m_scene = scene;
                m_transactionManager = new AgentAssetTransactionsManager(m_scene, m_dumpAssetsToFile);
            }
        }

        public void PostInitialise()
        {

        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "AgentTransactionModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public void NewClient(IClientAPI client)
        {
            client.OnAssetUploadRequest += m_transactionManager.HandleUDPUploadRequest;
            client.OnXferReceive += m_transactionManager.HandleXfer;
        }

        public void HandleItemCreationFromTransaction(IClientAPI remoteClient, LLUUID transactionID, LLUUID folderID,
                                                   uint callbackID, string description, string name, sbyte invType,
                                                   sbyte type, byte wearableType, uint nextOwnerMask)
        {
            m_transactionManager.HandleItemCreationFromTransaction(remoteClient, transactionID, folderID, callbackID, description, name, invType, type, wearableType, nextOwnerMask);
        }

        public void HandleItemUpdateFromTransaction(IClientAPI remoteClient, LLUUID transactionID,
                                               InventoryItemBase item)
        {
            m_transactionManager.HandleItemUpdateFromTransaction(remoteClient, transactionID, item);
        }

        public void RemoveAgentAssetTransactions(LLUUID userID)
        {
            m_transactionManager.RemoveAgentAssetTransactions(userID);
        }
    }

    //should merge this classes and clean up
    public class AgentAssetTransactionsManager
    {
        private static readonly log4net.ILog m_log
            = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        // Fields
        public Scene MyScene;

        /// <summary>
        /// Each agent has its own singleton collection of transactions
        /// </summary>
        private Dictionary<LLUUID, AgentAssetTransactions> AgentTransactions =
            new Dictionary<LLUUID, AgentAssetTransactions>();

        /// <summary>
        /// Should we dump uploaded assets to the filesystem?
        /// </summary>
        private bool m_dumpAssetsToFile;

        public AgentAssetTransactionsManager(Scene scene, bool dumpAssetsToFile)
        {
            MyScene = scene;
            m_dumpAssetsToFile = dumpAssetsToFile;
        }

        /// <summary>
        /// Get the collection of asset transactions for the given user.  If one does not already exist, it
        /// is created.
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        private AgentAssetTransactions GetUserTransactions(LLUUID userID)
        {
            lock (AgentTransactions)
            {
                if (!AgentTransactions.ContainsKey(userID))
                {
                    AgentAssetTransactions transactions
                        = new AgentAssetTransactions(userID, this, m_dumpAssetsToFile);
                    AgentTransactions.Add(userID, transactions);
                }

                return AgentTransactions[userID];
            }
        }

        /// <summary>
        /// Remove the given agent asset transactions.  This should be called when a client is departing
        /// from a scene (and hence won't be making any more transactions here).
        /// </summary>
        /// <param name="userID"></param>
        public void RemoveAgentAssetTransactions(LLUUID userID)
        {
            // m_log.DebugFormat("Removing agent asset transactions structure for agent {0}", userID);

            lock (AgentTransactions)
            {
                AgentTransactions.Remove(userID);
            }
        }

        /// <summary>
        /// Create an inventory item from data that has been received through a transaction.
        /// 
        /// This is called when new clothing or body parts are created.  It may also be called in other
        /// situations.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="transactionID"></param>
        /// <param name="folderID"></param>
        /// <param name="callbackID"></param>
        /// <param name="description"></param>
        /// <param name="name"></param>
        /// <param name="invType"></param>
        /// <param name="type"></param>
        /// <param name="wearableType"></param>
        /// <param name="nextOwnerMask"></param>
        public void HandleItemCreationFromTransaction(IClientAPI remoteClient, LLUUID transactionID, LLUUID folderID,
                                                      uint callbackID, string description, string name, sbyte invType,
                                                      sbyte type, byte wearableType, uint nextOwnerMask)
        {
            m_log.DebugFormat(
                "[TRANSACTIONS MANAGER] Called HandleItemCreationFromTransaction with item {0}", name);

            AgentAssetTransactions transactions = GetUserTransactions(remoteClient.AgentId);

            transactions.RequestCreateInventoryItem(
                remoteClient, transactionID, folderID, callbackID, description,
                name, invType, type, wearableType, nextOwnerMask);
        }

        /// <summary>
        /// Update an inventory item with data that has been received through a transaction.
        /// 
        /// This is called when clothing or body parts are updated (for instance, with new textures or 
        /// colours).  It may also be called in other situations.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="transactionID"></param>
        /// <param name="item"></param>
        public void HandleItemUpdateFromTransaction(IClientAPI remoteClient, LLUUID transactionID,
                                                    InventoryItemBase item)
        {
            m_log.DebugFormat(
               "[TRANSACTIONS MANAGER] Called HandleItemUpdateFromTransaction with item {0}",
                item.inventoryName);

            AgentAssetTransactions transactions
                = GetUserTransactions(remoteClient.AgentId);

            transactions.RequestUpdateInventoryItem(remoteClient, transactionID, item);
        }

        /// <summary>
        /// Request that a client (agent) begin an asset transfer.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="assetID"></param>
        /// <param name="transaction"></param>
        /// <param name="type"></param>
        /// <param name="data"></param></param>
        /// <param name="tempFile"></param>
        public void HandleUDPUploadRequest(IClientAPI remoteClient, LLUUID assetID, LLUUID transaction, sbyte type,
                                           byte[] data, bool storeLocal, bool tempFile)
        {
            // Console.WriteLine("asset upload of " + assetID);
            AgentAssetTransactions transactions = GetUserTransactions(remoteClient.AgentId);

            AgentAssetTransactions.AssetXferUploader uploader = transactions.RequestXferUploader(transaction);
            if (uploader != null)
            {

                if (uploader.Initialise(remoteClient, assetID, transaction, type, data, storeLocal, tempFile))
                {

                }
            }
        }

        /// <summary>
        /// Handle asset transfer data packets received in response to the asset upload request in
        /// HandleUDPUploadRequest()
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="xferID"></param>
        /// <param name="packetID"></param>
        /// <param name="data"></param>
        public void HandleXfer(IClientAPI remoteClient, ulong xferID, uint packetID, byte[] data)
        {
            AgentAssetTransactions transactions = GetUserTransactions(remoteClient.AgentId);

            transactions.HandleXfer(xferID, packetID, data);
        }
    }
}
