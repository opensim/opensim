using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules;

namespace OpenSim.Region.Environment
{
    public class ModuleLoader
    {

        public Dictionary<string, Assembly> LoadedAssemblys = new Dictionary<string, Assembly>();

        public ModuleLoader()
        {

        }

        /// <summary>
        /// Really just a test method for loading a set of currently internal modules
        /// </summary>
        /// <param name="scene"></param>
        public void CreateDefaultModules(Scene scene)
        {
            //Testing IRegionModule ideas 
            XferModule xferManager = new XferModule();
            xferManager.Initialise(scene);
            scene.AddModule(xferManager.GetName(), xferManager);

            ChatModule chatModule = new ChatModule();
            chatModule.Initialise(scene);
            scene.AddModule(chatModule.GetName(), chatModule);

            AvatarProfilesModule avatarProfiles = new AvatarProfilesModule();
            avatarProfiles.Initialise(scene);
            scene.AddModule(avatarProfiles.GetName(), avatarProfiles);

            this.LoadModule("OpenSim.Region.ExtensionsScriptModule.dll", "ExtensionsScriptingModule", scene);

            // Post Initialise Modules, which most likely shouldn't be here
            // but should rather be in a separate method that is called after all modules are loaded/created/intialised
            xferManager.PostInitialise();
            // chatModule.PostInitialise();  //for now leave this disabled as it would start up a partially working irc bot
            avatarProfiles.PostInitialise();
        }

        /// <summary>
        ///  Loads/initialises a Module instance that can be used by mutliple Regions
        /// </summary>
        /// <param name="dllName"></param>
        /// <param name="moduleName"></param>
        /// <param name="scene"></param>
        public void LoadSharedModule(string dllName, string moduleName, Scene scene)
        {

        }

        /// <summary>
        /// Loads a external Module (if not already loaded) and creates a new instance of it for the passed Scene.
        /// </summary>
        /// <param name="dllName"></param>
        /// <param name="moduleName"></param>
        /// <param name="scene"></param>
        public void LoadModule(string dllName, string moduleName, Scene scene)
        {
            Assembly pluginAssembly = null;
            if (LoadedAssemblys.ContainsKey(dllName))
            {
                pluginAssembly = LoadedAssemblys[dllName];
            }
            else
            {
                pluginAssembly = Assembly.LoadFrom(dllName);
                this.LoadedAssemblys.Add(dllName, pluginAssembly);
            }

            IRegionModule module = null;
            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("IRegionModule", true);

                        if (typeInterface != null)
                        {
                            module = (IRegionModule)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            break;
                        }
                        typeInterface = null;
                    }
                }
            }
            pluginAssembly = null;

            if (module.GetName() == moduleName)
            {
                module.Initialise(scene);
                scene.AddModule(moduleName, module);
                module.PostInitialise();  //shouldn't be done here
            }

        }

        public void ClearCache()
        {
            this.LoadedAssemblys.Clear();
        }
    }
}
