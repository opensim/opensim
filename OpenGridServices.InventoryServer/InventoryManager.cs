using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using OpenGrid.Framework.Data;
using libsecondlife;
using System.Reflection;

using System.Xml;
using Nwc.XmlRpc;
using OpenSim.Framework.Sims;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Utilities;

using System.Security.Cryptography;

namespace OpenGridServices.InventoryServer
{
    class InventoryManager
    {
        Dictionary<string, IInventoryData> _plugins = new Dictionary<string, IInventoryData>();

        /// <summary>
        /// Adds a new inventory server plugin - user servers will be requested in the order they were loaded.
        /// </summary>
        /// <param name="FileName">The filename to the inventory server plugin DLL</param>
        public void AddPlugin(string FileName)
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Invenstorage: Attempting to load " + FileName);
            Assembly pluginAssembly = Assembly.LoadFrom(FileName);

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Invenstorage: Found " + pluginAssembly.GetTypes().Length + " interfaces.");
            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (!pluginType.IsAbstract)
                {
                    Type typeInterface = pluginType.GetInterface("IInventoryData", true);

                    if (typeInterface != null)
                    {
                        IInventoryData plug = (IInventoryData)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                        plug.Initialise();
                        this._plugins.Add(plug.getName(), plug);
                        OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Invenstorage: Added IUserData Interface");
                    }

                    typeInterface = null;
                }
            }

            pluginAssembly = null;
        }

        public List<InventoryFolderBase> getRootFolders(LLUUID user)
        {
            foreach (KeyValuePair<string, IInventoryData> kvp in _plugins)
            {
                try
                {
                    return kvp.Value.getUserRootFolders(user);
                }
                catch (Exception e)
                {
                    OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Unable to get root folders via " + kvp.Key + " (" + e.ToString() + ")");
                }
            }
        }

        public XmlRpcResponse XmlRpcInventoryRequest(XmlRpcRequest request)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];

            Hashtable responseData = new Hashtable();

            // Stuff happens here

            if (requestData.ContainsKey("Access-type"))
            {
                if (requestData["access-type"] == "rootfolders")
                {
//                    responseData["rootfolders"] =
                }
            }
            else
            {
                responseData["error"] = "No access-type specified.";
            }


            // Stuff stops happening here

            response.Value = responseData;
            return response;
        }
    }
}
