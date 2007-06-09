/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
            OpenSim.Framework.Console.MainConsole.Instance.Verbose( "Invenstorage: Attempting to load " + FileName);
            Assembly pluginAssembly = Assembly.LoadFrom(FileName);

            OpenSim.Framework.Console.MainConsole.Instance.Verbose( "Invenstorage: Found " + pluginAssembly.GetTypes().Length + " interfaces.");
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
                        OpenSim.Framework.Console.MainConsole.Instance.Verbose( "Invenstorage: Added IUserData Interface");
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
                    OpenSim.Framework.Console.MainConsole.Instance.Notice("Unable to get root folders via " + kvp.Key + " (" + e.ToString() + ")");
                }
            }
            return null;
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
