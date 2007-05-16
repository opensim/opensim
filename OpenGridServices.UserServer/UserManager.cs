using System;
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

namespace OpenGridServices.UserServer
{
    public class UserManager
    {
        Dictionary<string, IUserData> _plugins = new Dictionary<string, IUserData>();

        /// <summary>
        /// Adds a new user server plugin - user servers will be requested in the order they were loaded.
        /// </summary>
        /// <param name="FileName">The filename to the user server plugin DLL</param>
        public void AddPlugin(string FileName)
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Storage: Attempting to load " + FileName);
            Assembly pluginAssembly = Assembly.LoadFrom(FileName);

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Storage: Found " + pluginAssembly.GetTypes().Length + " interfaces.");
            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (!pluginType.IsAbstract)
                {
                    Type typeInterface = pluginType.GetInterface("IUserData", true);

                    if (typeInterface != null)
                    {
                        IUserData plug = (IUserData)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                        plug.Initialise();
                        this._plugins.Add(plug.getName(), plug);
                        OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Storage: Added IUserData Interface");
                    }

                    typeInterface = null;
                }
            }

            pluginAssembly = null;
        }

        public UserProfileData getUserProfile(LLUUID uuid)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    return plugin.Value.getUserByUUID(uuid);
                }
                catch (Exception e)
                {
                    OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return null;
        }

        public UserProfileData getUserProfile(string name)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    return plugin.Value.getUserByName(name);
                }
                catch (Exception e)
                {
                    OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return null;
        }

        public UserProfileData getUserProfile(string fname, string lname)
        {
            foreach (KeyValuePair<string, IUserData> plugin in _plugins)
            {
                try
                {
                    return plugin.Value.getUserByName(fname,lname);
                }
                catch (Exception e)
                {
                    OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Unable to find user via " + plugin.Key + "(" + e.ToString() + ")");
                }
            }

            return null;
        }
    }
}
