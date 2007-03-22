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
