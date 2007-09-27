using System;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Serialization.Formatters;
using System.Collections;
using System.Collections.Generic;

using libsecondlife;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Data;
using OpenSim.Framework.Types;


namespace OpenSim.Region.Communications.OGS1
{
    public class OGS1InventoryService : IInventoryServices
    {

        IUserServices _userServices;
        IInventoryServices _inventoryServices;
 
       public OGS1InventoryService(NetworkServersInfo networkConfig, IUserServices userServices) :
            this(networkConfig.InventoryServerName, networkConfig.InventoryServerPort, userServices)
         {
        }
 
        public OGS1InventoryService(string serverName, int serverPort, IUserServices userServices)
        {
            _userServices = userServices;
 
            // we only need to register the tcp channel once, and we don't know which other modules use remoting
            if (ChannelServices.GetChannel("tcp") == null)
            {
                // Creating a custom formatter for a TcpChannel sink chain.
               BinaryServerFormatterSinkProvider serverProvider = new BinaryServerFormatterSinkProvider();
                serverProvider.TypeFilterLevel = TypeFilterLevel.Full;

                BinaryClientFormatterSinkProvider clientProvider = new BinaryClientFormatterSinkProvider();

                IDictionary props = new Hashtable();
                props["typeFilterLevel"] = TypeFilterLevel.Full;

                // Pass the properties for the port setting and the server provider in the server chain argument. (Client remains null here.)
                TcpChannel chan = new TcpChannel(props, clientProvider, serverProvider);

                ChannelServices.RegisterChannel(chan, true);
            }



            string remotingUrl = string.Format("tcp://{0}:{1}/Inventory", serverName, serverPort);
            _inventoryServices = (IInventoryServices)Activator.GetObject(typeof(IInventoryServices), remotingUrl); 
        }

        #region IInventoryServices Members
       
        public void RequestInventoryForUser(LLUUID userID, InventoryFolderInfo folderCallBack, InventoryItemInfo itemCallBack)
        {
            _inventoryServices.RequestInventoryForUser(userID, folderCallBack, itemCallBack);             
        }

        public void AddNewInventoryFolder(LLUUID userID, InventoryFolderBase folder)
        {
            _inventoryServices.AddNewInventoryFolder(userID, folder);
        }

        public void AddNewInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            _inventoryServices.AddNewInventoryItem(userID, item);
        }

        public void DeleteInventoryItem(LLUUID userID, InventoryItemBase item)
        {
            _inventoryServices.DeleteInventoryItem(userID, item);
        }

        public List<InventoryFolderBase> RequestFirstLevelFolders(LLUUID folderID)
        {
            return _inventoryServices.RequestFirstLevelFolders(folderID);
        }

         public List<InventoryItemBase> RequestFolderItems(LLUUID folderID)
        {
            return _inventoryServices.RequestFolderItems(folderID);
        }

        public void GetRootFoldersForUser(LLUUID user, out LLUUID libraryFolder, out LLUUID personalFolder)
        {
            _inventoryServices.GetRootFoldersForUser(user, out libraryFolder, out personalFolder);
        }

        public void CreateNewUserInventory(LLUUID libraryRootId, LLUUID user)
        {
            throw new Exception("method not implemented");
        }
        #endregion
    }
}
