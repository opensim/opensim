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
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using OpenSim.Framework.Interfaces;
using OpenSim.UserServer;

namespace OpenSim
{
    public class Grid
    {
        public IAssetServer AssetServer;
        public IGridServer GridServer;
        public IUserServer UserServer;
        public string AssetDll = "";
        public string GridDll = "";

        public Grid()
        {
        }

        public virtual void Initialise()
        {
            //load the dlls 
            this.AssetServer = this.LoadAssetDll(this.AssetDll);
            this.GridServer = this.LoadGridDll(this.GridDll);
        }
        public virtual void Close()
        {
            this.AssetServer.Close();
            this.GridServer.Close();
        }

        private IAssetServer LoadAssetDll(string dllName)
        {
            Assembly pluginAssembly = Assembly.LoadFrom(dllName);
            IAssetServer server = null;

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("IAssetPlugin", true);

                        if (typeInterface != null)
                        {
                            IAssetPlugin plug = (IAssetPlugin)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            server = plug.GetAssetServer();
                            break;
                        }

                        typeInterface = null;
                    }
                }
            }
            pluginAssembly = null;
            return server;
        }

        private IGridServer LoadGridDll(string dllName)
        {
            Assembly pluginAssembly = Assembly.LoadFrom(dllName);
            IGridServer server = null;

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("IGridPlugin", true);

                        if (typeInterface != null)
                        {
                            IGridPlugin plug = (IGridPlugin)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            server = plug.GetGridServer();
                            break;
                        }

                        typeInterface = null;
                    }
                }
            }
            pluginAssembly = null;
            return server;
        }
    }
}
