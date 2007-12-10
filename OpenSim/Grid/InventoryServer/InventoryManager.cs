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
* 
*/
using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using libsecondlife;

using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;

namespace OpenSim.Grid.InventoryServer
{

    public class InventoryManager
    {
        IInventoryData _databasePlugin;

        /// <summary>
        /// Adds a new inventory server plugin - user servers will be requested in the order they were loaded.
        /// </summary>
        /// <param name="FileName">The filename to the inventory server plugin DLL</param>
        public void AddDatabasePlugin(string FileName)
        {
            MainLog.Instance.Verbose(OpenInventory_Main.LogName, "Invenstorage: Attempting to load " + FileName);
            Assembly pluginAssembly = Assembly.LoadFrom(FileName);

            MainLog.Instance.Verbose(OpenInventory_Main.LogName, "Invenstorage: Found " + pluginAssembly.GetTypes().Length + " interfaces.");
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
                        MainLog.Instance.Verbose(OpenInventory_Main.LogName, "Invenstorage: Added IInventoryData Interface");
                        break;
                    }

                    typeInterface = null;
                }
            }

            pluginAssembly = null;
        }

        protected static SerializableInventory loadInventoryFromXmlFile(string fileName)
        {
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            XmlReader reader = new XmlTextReader(fs);
            XmlSerializer x = new XmlSerializer(typeof(SerializableInventory));
            SerializableInventory inventory = (SerializableInventory)x.Deserialize(reader);
            fs.Close();
            fs.Dispose();
            return inventory;
        }

        protected static void saveInventoryToStream(SerializableInventory inventory, Stream s)
        {
            XmlTextWriter writer = new XmlTextWriter(s, Encoding.UTF8);
            writer.Formatting = Formatting.Indented;
            XmlSerializer x = new XmlSerializer(typeof(SerializableInventory));
            x.Serialize(writer, inventory);
        }

        protected static bool fixupFolder(SerializableInventory.SerializableFolder f, SerializableInventory.SerializableFolder parent)
        {
            bool modified = false;

            // ensure we have a valid folder id
            if (f.folderID == LLUUID.Zero)
            {
                f.folderID = LLUUID.Random();
                modified = true;
            }

            // ensure we have  valid agent id 
            if (f.agentID == LLUUID.Zero)
            {
                if (parent != null)
                    f.agentID = parent.agentID;
                else
                    f.agentID = f.folderID;
                modified = true;
            }

            if (f.parentID == LLUUID.Zero && parent != null)
            {
                f.parentID = parent.folderID;
                modified = true;
            }


            foreach (SerializableInventory.SerializableFolder child in f.SubFolders)
            {
                modified |= fixupFolder(child, f);
            }

            return modified;
        }

        protected static bool fixupInventory(SerializableInventory inventory)
        {
            return fixupFolder(inventory.root, null);
        }

        public class GetInventory : BaseStreamHandler
        {
            private SerializableInventory _inventory;
            private InventoryManager _manager;
            public GetInventory(InventoryManager manager)
                : base("GET", "/inventory")
            {
                _manager = manager;

                _inventory = loadInventoryFromXmlFile("Inventory_Library.xml");
                if (fixupInventory(_inventory))
                {
                    FileStream fs = new FileStream("Inventory_Library.xml", FileMode.Truncate, FileAccess.Write);
                    saveInventoryToStream(_inventory, fs);
                    fs.Flush();
                    fs.Close();
                    MainLog.Instance.Debug(OpenInventory_Main.LogName, "Modified");
                }
            }

            private void CreateDefaultInventory(LLUUID userID)
            {
            }

            private byte[] GetUserInventory(LLUUID userID)
            {
                MainLog.Instance.Notice(OpenInventory_Main.LogName, "Getting Inventory for user {0}", userID.ToStringHyphenated());
                byte[] result = new byte[] { };

                InventoryFolderBase fb = _manager._databasePlugin.getUserRootFolder(userID);
                if (fb == null)
                {
                    MainLog.Instance.Notice(OpenInventory_Main.LogName, "Inventory not found for user {0}, creating new", userID.ToStringHyphenated());
                    CreateDefaultInventory(userID);
                }

                return result;
            }

            override public byte[] Handle(string path, Stream request)
            {
                byte[] result = new byte[] { };

                string[] parms = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parms.Length >= 1)
                {
                    if (string.Compare(parms[1], "library", true) == 0)
                    {

                        MemoryStream ms = new MemoryStream();
                        saveInventoryToStream(_inventory, ms);

                        result = ms.GetBuffer();
                        Array.Resize<byte>(ref result, (int)ms.Length);
                    }
                    else if (string.Compare(parms[1], "user", true) == 0)
                    {
                        if (parms.Length >= 2)
                        {
                            result = GetUserInventory(new LLUUID(parms[2]));
                        }
                    }
                }
                return result;
            }
        }
    
    }
}
