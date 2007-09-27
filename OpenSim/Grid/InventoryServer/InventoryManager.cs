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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;

using System.Xml;
using OpenSim.Framework.Console;
using OpenSim.Framework.Utilities;
using OpenSim.Framework.Data;
using InventoryCategory = OpenSim.Framework.Data.InventoryCategory;

namespace OpenSim.Grid.InventoryServer
{
    class InventoryManager : IInventoryData 
    {
        IInventoryData _databasePlugin;

        /// <summary>
        /// Adds a new inventory server plugin - user servers will be requested in the order they were loaded.
        /// </summary>
        /// <param name="FileName">The filename to the inventory server plugin DLL</param>
        public void AddDatabasePlugin(string FileName)
        {
            MainLog.Instance.Verbose(OpenInventory_Main.MainLogName, "Invenstorage: Attempting to load " + FileName);
            Assembly pluginAssembly = Assembly.LoadFrom(FileName);

            MainLog.Instance.Verbose(OpenInventory_Main.MainLogName, "Invenstorage: Found " + pluginAssembly.GetTypes().Length + " interfaces.");
            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (!pluginType.IsAbstract)
                {
                    Type typeInterface = pluginType.GetInterface("IInventoryData", true);

                    if (typeInterface != null)
                    {
                        IInventoryData plug = (IInventoryData)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                        plug.Initialise();
                        _databasePlugin = plug;
                        MainLog.Instance.Verbose(OpenInventory_Main.MainLogName, "Invenstorage: Added IInventoryData Interface");
                        break;
                    }

                    typeInterface = null;
                }
            }

            pluginAssembly = null;
        }

        public List<InventoryFolderBase> getRootFolders(LLUUID user)
        {
            return null;
        }

        #region IInventoryData Members


        public List<InventoryItemBase> getInventoryInFolder(LLUUID folderID)
        {
            return _databasePlugin.getInventoryInFolder(folderID);
        }

        public List<InventoryFolderBase> getUserRootFolders(LLUUID user)
        {
            return _databasePlugin.getUserRootFolders(user);
        }

        public List<InventoryFolderBase> getInventoryFolders(LLUUID parentID)
        {
            return _databasePlugin.getInventoryFolders(parentID);
        }

        public InventoryItemBase getInventoryItem(LLUUID item)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public InventoryFolderBase getInventoryFolder(LLUUID folder)
        {
            return _databasePlugin.getInventoryFolder(folder);
        }

        public void addInventoryItem(InventoryItemBase item)
        {
            _databasePlugin.addInventoryItem(item);
        }

        public void updateInventoryItem(InventoryItemBase item)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void deleteInventoryItem(InventoryItemBase item)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void addInventoryFolder(InventoryFolderBase folder)
        {
            _databasePlugin.addInventoryFolder(folder);
        }

        public void updateInventoryFolder(InventoryFolderBase folder)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void Initialise()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void Close()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public string getName()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public string getVersion()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void deleteInventoryCategory(InventoryCategory inventoryCategory)
        {
            _databasePlugin.deleteInventoryCategory(inventoryCategory);
        }

        #endregion
    }
}
